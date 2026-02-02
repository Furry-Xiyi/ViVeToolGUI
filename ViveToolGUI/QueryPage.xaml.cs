using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace ViVeToolGUI
{
    public class FeatureInfo
    {
        public string Id { get; set; } = "";
        public string Priority { get; set; } = "";
        public string State { get; set; } = "";
        public string Type { get; set; } = "";
        public string Name { get; set; } = "";
    }

    public sealed partial class QueryPage : Page
    {
        private readonly ResourceLoader _resourceLoader;
        private readonly List<FeatureInfo> _allFeatures = new();
        private CancellationTokenSource _renderCancellation;
        private DispatcherTimer _searchDebounceTimer;
        public QueryPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Enabled;
            _resourceLoader = ResourceLoader.GetForViewIndependentUse();
            _searchDebounceTimer = new DispatcherTimer();
            _searchDebounceTimer.Interval = TimeSpan.FromMilliseconds(250);
            _searchDebounceTimer.Tick += SearchDebounceTimer_Tick;
        }

        private async void QueryAllButton_Click(object sender, RoutedEventArgs e)
        {
            QueryAllButton.IsEnabled = false;
            ExportButton.IsEnabled = false;
            QueryProgressBar.Visibility = Visibility.Visible;

            _allFeatures.Clear();
            FeaturesListView.Items.Clear();

            // 取消之前的渲染
            _renderCancellation?.Cancel();

            try
            {
                var result = await MainWindow.ExecuteViVeToolCommandAsync("/query");

                if (this.Frame == null || this.Frame.Content != this)
                    return;

                if (result.ExitCode == 0)
                {
                    ParseQueryOutput(result.Output);

                    System.Diagnostics.Debug.WriteLine($"[QueryPage] Parsed features count = {_allFeatures.Count}");

                    // 异步分批渲染
                    await RenderFeaturesAsync(_allFeatures);

                    ExportButton.IsEnabled = _allFeatures.Count > 0;
                }
                else
                {
                    var dialog = new Dialogs.ErrorDialog(result.Error);
                    dialog.XamlRoot = this.XamlRoot;
                    await dialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                if (this.Frame == null || this.Frame.Content != this)
                    return;

                var dialog = new Dialogs.ErrorDialog(ex.Message);
                dialog.XamlRoot = this.XamlRoot;
                await dialog.ShowAsync();
            }
            finally
            {
                if (this.Frame != null && this.Frame.Content == this)
                {
                    QueryAllButton.IsEnabled = true;
                    QueryProgressBar.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void ParseQueryOutput(string output)
        {
            _allFeatures.Clear();

            if (string.IsNullOrWhiteSpace(output))
                return;

            output = Regex.Replace(output, @"\x1B\[[0-9;]*m", "");
            output = output.Replace("\uFEFF", "");

            var lines = Regex.Split(output, @"\r\n|\n|\r")
                             .Select(l => l.TrimEnd())
                             .Where(l => !string.IsNullOrWhiteSpace(l))
                             .ToList();

            FeatureInfo current = null;

            foreach (var line in lines)
            {
                var idMatch = Regex.Match(line, @"\[(\d+)\](?:\s*\(([^)]+)\))?");
                if (idMatch.Success)
                {
                    if (current != null)
                        _allFeatures.Add(current);

                    current = new FeatureInfo
                    {
                        Id = idMatch.Groups[1].Value,
                        Name = idMatch.Groups[2].Success ? idMatch.Groups[2].Value.Trim() : ""
                    };
                    continue;
                }

                if (current == null)
                    continue;

                if (line.Contains("Priority"))
                {
                    var m = Regex.Match(line, @"Priority\s*:\s*([A-Za-z]+)");
                    if (m.Success)
                        current.Priority = m.Groups[1].Value;
                    continue;
                }

                if (line.Contains("State"))
                {
                    var m = Regex.Match(line, @"State\s*:\s*([A-Za-z]+)");
                    if (m.Success)
                        current.State = m.Groups[1].Value;
                    continue;
                }

                if (line.Contains("Type"))
                {
                    var m = Regex.Match(line, @"Type\s*:\s*([A-Za-z]+)");
                    if (m.Success)
                        current.Type = m.Groups[1].Value;
                    continue;
                }
            }

            if (current != null)
                _allFeatures.Add(current);
        }

        // 异步分批渲染，每批 100 项，避免 UI 卡死
        private async Task RenderFeaturesAsync(IEnumerable<FeatureInfo> features)
        {
            _renderCancellation?.Cancel();
            _renderCancellation = new CancellationTokenSource();
            var token = _renderCancellation.Token;

            FeaturesListView.Items.Clear();

            var featureList = features.ToList();
            const int batchSize = 100;

            for (int i = 0; i < featureList.Count; i += batchSize)
            {
                if (token.IsCancellationRequested)
                    return;

                var batch = featureList.Skip(i).Take(batchSize);

                foreach (var f in batch)
                {
                    if (token.IsCancellationRequested)
                        return;

                    var item = CreateFeatureItem(f);
                    FeaturesListView.Items.Add(item);
                }

                await Task.Delay(10, token);
            }

            SearchBox.IsEnabled = true;

            System.Diagnostics.Debug.WriteLine($"[RenderFeatures] Finished rendering {FeaturesListView.Items.Count} items");
        }

        private Grid CreateFeatureItem(FeatureInfo f)
        {
            var grid = new Grid
            {
                Padding = new Thickness(12),
                ColumnSpacing = 12,
                Height = 40
            };

            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // ID
            var idText = new TextBlock
            {
                Text = f.Id ?? "",
                FontFamily = new FontFamily("Consolas"),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 14
            };
            Grid.SetColumn(idText, 0);
            grid.Children.Add(idText);

            // Priority
            var priorityText = new TextBlock
            {
                Text = f.Priority ?? "",
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 14
            };
            Grid.SetColumn(priorityText, 1);
            grid.Children.Add(priorityText);

            // State
            var stateText = new TextBlock
            {
                Text = f.State ?? "",
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 14
            };
            Grid.SetColumn(stateText, 2);
            grid.Children.Add(stateText);

            // Type
            var typeText = new TextBlock
            {
                Text = f.Type ?? "",
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 14
            };
            Grid.SetColumn(typeText, 3);
            grid.Children.Add(typeText);

            // Name
            var nameText = new TextBlock
            {
                Text = f.Name ?? "",
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontSize = 14
            };
            Grid.SetColumn(nameText, 4);
            grid.Children.Add(nameText);

            return grid;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchDebounceTimer.Stop();
            _searchDebounceTimer.Start();
        }

        private async void SearchDebounceTimer_Tick(object sender, object e)
        {
            _searchDebounceTimer.Stop();

            string text = SearchBox.Text?.Trim() ?? "";

            bool isAllDigits = text.All(char.IsDigit);

            if (!isAllDigits || text.Length < 3)
            {
                return;
            }

            // ⭐ 后台线程过滤（不卡 UI）
            var filtered = await Task.Run(() =>
            {
                return _allFeatures.Where(f =>
                    (f.Id?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false)
                ).ToList();
            });

            await RenderFeaturesAsync(filtered);
        }

        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ExportButton.IsEnabled = false;

                // ① 弹出格式选择对话框
                var optionsDialog = new Dialogs.ExportOptionsDialog();
                optionsDialog.XamlRoot = this.XamlRoot;

                var result = await optionsDialog.ShowAsync();
                if (result != ContentDialogResult.Primary)
                    return;

                bool exportTxt = optionsDialog.ExportTxt;
                bool exportCsv = optionsDialog.ExportCsv;
                bool exportJson = optionsDialog.ExportJson;

                if (!exportTxt && !exportCsv && !exportJson)
                {
                    var warn = new Dialogs.ErrorDialog("Please select at least one format.");
                    warn.XamlRoot = this.XamlRoot;
                    await warn.ShowAsync();
                    return;
                }

                // ② 获取文档目录
                var documentsFolder = await StorageFolder.GetFolderFromPathAsync(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));

                string baseFileName = $"ViVeTool_Query_{DateTime.Now:yyyyMMdd_HHmmss}";

                // ③ 后台线程生成内容（避免卡顿）
                var (txtContent, csvContent, jsonContent) = await Task.Run(() =>
                {
                    var txt = new System.Text.StringBuilder();
                    var csv = new System.Text.StringBuilder();
                    var json = new System.Text.StringBuilder();

                    csv.AppendLine("ID,Priority,State,Type,Name");
                    json.AppendLine("[");

                    foreach (var f in _allFeatures)
                    {
                        // TXT
                        txt.AppendLine($"[{f.Id}] {f.Name}");
                        txt.AppendLine($"  Priority: {f.Priority}");
                        txt.AppendLine($"  State: {f.State}");
                        txt.AppendLine($"  Type: {f.Type}");
                        txt.AppendLine();

                        // CSV
                        csv.AppendLine($"{f.Id},{f.Priority},{f.State},{f.Type},{f.Name}");

                        // JSON
                        json.AppendLine(
                            $"  {{ \"Id\": \"{f.Id}\", \"Priority\": \"{f.Priority}\", \"State\": \"{f.State}\", \"Type\": \"{f.Type}\", \"Name\": \"{f.Name}\" }},"
                        );
                    }

                    json.AppendLine("]");

                    return (txt.ToString(), csv.ToString(), json.ToString());
                });

                // ④ 写入文件
                StorageFile? txtFile = null;
                StorageFile? csvFile = null;
                StorageFile? jsonFile = null;

                if (exportTxt)
                {
                    txtFile = await documentsFolder.CreateFileAsync($"{baseFileName}.txt",
                        CreationCollisionOption.GenerateUniqueName);
                    await FileIO.WriteTextAsync(txtFile, txtContent);
                }

                if (exportCsv)
                {
                    csvFile = await documentsFolder.CreateFileAsync($"{baseFileName}.csv",
                        CreationCollisionOption.GenerateUniqueName);
                    await FileIO.WriteTextAsync(csvFile, csvContent);
                }

                if (exportJson)
                {
                    jsonFile = await documentsFolder.CreateFileAsync($"{baseFileName}.json",
                        CreationCollisionOption.GenerateUniqueName);
                    await FileIO.WriteTextAsync(jsonFile, jsonContent);
                }

                // ⑤ 生成成功信息
                var msg = "Exported successfully!\n\n";

                if (txtFile != null) msg += $"TXT: {txtFile.Name}\n";
                if (csvFile != null) msg += $"CSV: {csvFile.Name}\n";
                if (jsonFile != null) msg += $"JSON: {jsonFile.Name}\n";

                msg += "\nLocation:";

                // ⑥ 使用你最新版本的 SuccessDialog（带可点击路径）
                var dialog = new Dialogs.SuccessDialog(msg, documentsFolder.Path);
                dialog.XamlRoot = this.XamlRoot;
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                var dialog = new Dialogs.ErrorDialog($"Export failed: {ex.Message}");
                dialog.XamlRoot = this.XamlRoot;
                await dialog.ShowAsync();
            }
            finally
            {
                ExportButton.IsEnabled = true;
            }
        }

        private async Task ExportToFileAsync(StorageFile file)
        {
            try
            {
                string content;

                if (file.FileType == ".csv")
                {
                    content = "ID,Priority,State,Type,Name\n";
                    foreach (var f in _allFeatures)
                    {
                        content += $"{f.Id},{f.Priority},{f.State},{f.Type},{f.Name}\n";
                    }
                }
                else
                {
                    content = "";
                    foreach (var f in _allFeatures)
                    {
                        content += $"[{f.Id}] {f.Name}\n";
                        content += $"  Priority: {f.Priority}\n";
                        content += $"  State: {f.State}\n";
                        content += $"  Type: {f.Type}\n\n";
                    }
                }

                await FileIO.WriteTextAsync(file, content);

                var dialog = new Dialogs.SuccessDialog(_resourceLoader.GetString("Export_Success"));
                dialog.XamlRoot = this.XamlRoot;
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                var dialog = new Dialogs.ErrorDialog(ex.Message);
                dialog.XamlRoot = this.XamlRoot;
                await dialog.ShowAsync();
            }
        }
    }
}