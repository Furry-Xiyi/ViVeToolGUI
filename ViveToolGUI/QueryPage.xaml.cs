using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.Resources;
using Windows.Storage;
using WinRT.Interop;
using System.Collections.ObjectModel;

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
        // 保持原始完整数据，用于搜索过滤
        private List<FeatureInfo> _allFeatures = new();

        public QueryPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Enabled;
            _resourceLoader = ResourceLoader.GetForViewIndependentUse();
        }

        private async void QueryAllButton_Click(object sender, RoutedEventArgs e)
        {
            const int BatchSize = 50; // 每批添加多少项（可根据情况调小）
            QueryAllButton.IsEnabled = false;
            ExportButton.IsEnabled = false;
            SearchBox.IsEnabled = false;

            LoadingOverlay.Visibility = Visibility.Visible;
            LoadingText.Text = "Querying features...";

            // 清空当前显示（使用 Items.Clear 而不是 ItemsSource）
            try
            {
                FeaturesListView.ItemsSource = null;
            }
            catch
            {
                // 忽略 ItemsSource 清理可能抛出的异常，继续使用 Items.Clear
            }
            FeaturesListView.Items.Clear();

            _allFeatures.Clear();

            try
            {
                if (!await App.EnsureViVeToolInitializedAsync())
                {
                    var dialog = new Dialogs.ErrorDialog("ViVeTool is still initializing. Please wait and try again.");
                    dialog.XamlRoot = this.XamlRoot;
                    await dialog.ShowAsync();
                    return;
                }

                var result = await MainWindow.ExecuteViVeToolCommandAsync("/query");

                if (this.Frame == null || this.Frame.Content != this)
                    return;

                if (result.ExitCode != 0)
                {
                    var dialog = new Dialogs.ErrorDialog(result.Error);
                    dialog.XamlRoot = this.XamlRoot;
                    await dialog.ShowAsync();
                    return;
                }

                LoadingText.Text = "Parsing results...";
                await Task.Delay(50); // 给 UI 一点时间更新 Loading 文本

                // 在后台线程解析并返回列表（ParseQueryOutput 返回 List<FeatureInfo>）
                var parsed = await Task.Run(() => ParseQueryOutput(result.Output) ?? new List<FeatureInfo>());

                System.Diagnostics.Debug.WriteLine($"[Debug] Parsed count = {parsed?.Count ?? 0}");
                if (parsed != null && parsed.Count > 0)
                {
                    var first = parsed[0];
                    System.Diagnostics.Debug.WriteLine($"[Debug] First Item: ID={first?.Id}, Name={first?.Name}");
                }

                // 清洗并重建对象，确保字段安全
                var cleaned = parsed
                    .Where(f => f != null)
                    .Select(f => new FeatureInfo
                    {
                        Id = Clean(f.Id),
                        Priority = Clean(f.Priority),
                        State = Clean(f.State),
                        Type = Clean(f.Type),
                        Name = Clean(f.Name)
                    })
                    .ToList();

                System.Diagnostics.Debug.WriteLine($"[Debug] Cleaned count = {cleaned.Count}");

                // 分批添加到 ListView.Items（在 UI 线程）
                int total = cleaned.Count;
                int added = 0;

                while (added < total)
                {
                    int take = Math.Min(BatchSize, total - added);
                    var batch = cleaned.Skip(added).Take(take).ToList();

                    // 在 UI 线程添加这一批
                    bool enqueued = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()?.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
                    {
                        try
                        {
                            foreach (var feature in batch)
                            {
                                try
                                {
                                    // 直接添加数据对象，ListView 会使用 ItemTemplate 渲染
                                    FeaturesListView.Items.Add(feature);
                                }
                                catch (Exception addEx)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[Debug][AddItemError] id={feature?.Id} ex={addEx}");
                                }
                            }
                        }
                        catch (Exception exInner)
                        {
                            System.Diagnostics.Debug.WriteLine($"[Debug][BatchAddException] ex={exInner}");
                        }
                    }) ?? false;

                    // 如果无法通过 DispatcherQueue 在当前线程入队（极少见），直接在当前上下文添加（UI 线程）
                    if (!enqueued)
                    {
                        foreach (var feature in batch)
                        {
                            try
                            {
                                FeaturesListView.Items.Add(feature);
                            }
                            catch (Exception addEx)
                            {
                                System.Diagnostics.Debug.WriteLine($"[Debug][AddItemError-Fallback] id={feature?.Id} ex={addEx}");
                            }
                        }
                    }

                    added += take;

                    // 更新 Loading 文本
                    LoadingText.Text = $"Loaded {added}/{total}";

                    // 给 UI 一点喘息时间，避免一次性占用太多渲染资源
                    await Task.Delay(60);
                }

                // 保存成员变量供搜索/导出使用
                _allFeatures = cleaned;

                ExportButton.IsEnabled = _allFeatures.Count > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[QueryAllButton_Click] Exception: {ex}");
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
                    SearchBox.IsEnabled = true;
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                }
            }
        }

        private List<FeatureInfo> ParseQueryOutput(string output)
        {
            var tempFeatures = new List<FeatureInfo>();

            if (string.IsNullOrWhiteSpace(output))
                return tempFeatures;

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
                        tempFeatures.Add(current);

                    current = new FeatureInfo
                    {
                        Id = idMatch.Groups[1].Value ?? "",
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
                        current.Priority = m.Groups[1].Value ?? "";
                    continue;
                }

                if (line.Contains("State"))
                {
                    var m = Regex.Match(line, @"State\s*:\s*([A-Za-z]+)");
                    if (m.Success)
                        current.State = m.Groups[1].Value ?? "";
                    continue;
                }

                if (line.Contains("Type"))
                {
                    var m = Regex.Match(line, @"Type\s*:\s*([A-Za-z]+)");
                    if (m.Success)
                        current.Type = m.Groups[1].Value ?? "";
                    continue;
                }
            }

            if (current != null)
                tempFeatures.Add(current);

            return tempFeatures.Where(f => f != null).ToList();
        }

        // 搜索功能重写：不再增量渲染，而是直接过滤并重新绑定
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_allFeatures.Count == 0)
                return;

            string text = SearchBox.Text?.ToLower() ?? "";

            if (string.IsNullOrWhiteSpace(text))
            {
                FeaturesListView.ItemsSource = new ObservableCollection<FeatureInfo>(_allFeatures);
            }
            else
            {
                var filtered = _allFeatures.Where(f =>
                    (!string.IsNullOrEmpty(f.Id) && f.Id.Contains(text, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(f.Name) && f.Name.Contains(text, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(f.State) && f.State.Contains(text, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(f.Priority) && f.Priority.Contains(text, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(f.Type) && f.Type.Contains(text, StringComparison.OrdinalIgnoreCase))
                ).ToList();

                FeaturesListView.ItemsSource = new ObservableCollection<FeatureInfo>(filtered);
            }
        }

        private string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var cleaned = new string(s.Where(c => !char.IsControl(c)).ToArray());
            return cleaned.Length > 10000 ? cleaned.Substring(0, 10000) : cleaned;
        }

        // --- 右键菜单事件处理 ---

        // 辅助方法：从菜单点击事件中获取 FeatureInfo 数据
        private FeatureInfo GetFeatureFromMenu(object sender)
        {
            try
            {
                if (sender is MenuFlyoutItem menuItem)
                {
                    // 1. 优先使用 CommandParameter
                    if (menuItem.CommandParameter is FeatureInfo cpFeature)
                        return cpFeature;

                    // 2. 尝试 MenuFlyoutItem.DataContext（有时会被设置）
                    if (menuItem.DataContext is FeatureInfo dcFeature)
                        return dcFeature;

                    // 3. 尝试从父 MenuFlyout 的 Target / PlacementTarget 获取 DataContext（反射兼容不同 WinUI 版本）
                    if (menuItem.Parent is MenuFlyout parentFlyout)
                    {
                        try
                        {
                            // 常见属性名：Target 或 PlacementTarget
                            var targetProp = parentFlyout.GetType().GetProperty("Target")
                                             ?? parentFlyout.GetType().GetProperty("PlacementTarget");

                            if (targetProp != null)
                            {
                                var targetObj = targetProp.GetValue(parentFlyout);
                                if (targetObj is FrameworkElement fe && fe.DataContext is FeatureInfo feFeature)
                                    return feFeature;
                            }

                            // 有些 WinUI 版本把目标放在内部字段或不同属性，尝试查找第一个 FrameworkElement 字段/属性
                            foreach (var prop in parentFlyout.GetType().GetProperties())
                            {
                                try
                                {
                                    var val = prop.GetValue(parentFlyout);
                                    if (val is FrameworkElement fe2 && fe2.DataContext is FeatureInfo f2)
                                        return f2;
                                }
                                catch { }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[GetFeatureFromMenu] reflection error: {ex}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GetFeatureFromMenu] Exception: {ex}");
            }

            return null;
        }

        private async void EnableFeature_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuFlyoutItem item && item.CommandParameter is FeatureInfo feature)
                {
                    await EnableFeatureAsync(feature.Id);
                    return;
                }

                // 兜底：尝试从发起者的 DataContext 取值
                if (sender is FrameworkElement fe && fe.DataContext is FeatureInfo dc)
                {
                    await EnableFeatureAsync(dc.Id);
                    return;
                }

                System.Diagnostics.Debug.WriteLine("[EnableFeature_Click] feature is null");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EnableFeature_Click] Exception: {ex}");
            }
        }

        private async void DisableFeature_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuFlyoutItem item && item.CommandParameter is FeatureInfo feature)
                {
                    await DisableFeatureAsync(feature.Id);
                    return;
                }

                if (sender is FrameworkElement fe && fe.DataContext is FeatureInfo dc)
                {
                    await DisableFeatureAsync(dc.Id);
                    return;
                }

                System.Diagnostics.Debug.WriteLine("[DisableFeature_Click] feature is null");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DisableFeature_Click] Exception: {ex}");
            }
        }

        private void CopyFeatureId_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuFlyoutItem item && item.CommandParameter is FeatureInfo feature)
                {
                    CopyToClipboard(feature.Id);
                    return;
                }

                if (sender is FrameworkElement fe && fe.DataContext is FeatureInfo dc)
                {
                    CopyToClipboard(dc.Id);
                    return;
                }

                System.Diagnostics.Debug.WriteLine("[CopyFeatureId_Click] feature is null");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CopyFeatureId_Click] Exception: {ex}");
            }
        }

        private async Task EnableFeatureAsync(string featureId)
        {
            try
            {
                LoadingOverlay.Visibility = Visibility.Visible;
                LoadingText.Text = $"Enabling feature {featureId}...";

                var result = await MainWindow.ExecuteViVeToolCommandAsync($"/enable /id:{featureId}");

                LoadingOverlay.Visibility = Visibility.Collapsed;

                if (result.ExitCode == 0)
                {
                    var dialog = new Dialogs.SuccessDialog($"Feature {featureId} enabled successfully!");
                    dialog.XamlRoot = this.XamlRoot;
                    await dialog.ShowAsync();
                }
                else
                {
                    var dialog = new Dialogs.ErrorDialog($"Failed to enable feature:\n{result.Error}");
                    dialog.XamlRoot = this.XamlRoot;
                    await dialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                var dialog = new Dialogs.ErrorDialog(ex.Message);
                dialog.XamlRoot = this.XamlRoot;
                await dialog.ShowAsync();
            }
        }

        private async Task DisableFeatureAsync(string featureId)
        {
            try
            {
                LoadingOverlay.Visibility = Visibility.Visible;
                LoadingText.Text = $"Disabling feature {featureId}...";

                var result = await MainWindow.ExecuteViVeToolCommandAsync($"/disable /id:{featureId}");

                LoadingOverlay.Visibility = Visibility.Collapsed;

                if (result.ExitCode == 0)
                {
                    var dialog = new Dialogs.SuccessDialog($"Feature {featureId} disabled successfully!");
                    dialog.XamlRoot = this.XamlRoot;
                    await dialog.ShowAsync();
                }
                else
                {
                    var dialog = new Dialogs.ErrorDialog($"Failed to disable feature:\n{result.Error}");
                    dialog.XamlRoot = this.XamlRoot;
                    await dialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                var dialog = new Dialogs.ErrorDialog(ex.Message);
                dialog.XamlRoot = this.XamlRoot;
                await dialog.ShowAsync();
            }
        }

        private void CopyToClipboard(string text)
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(text);
            Clipboard.SetContent(dataPackage);
        }

        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            // 导出逻辑基本不变，只是数据源现在是 _allFeatures (List)
            try
            {
                ExportButton.IsEnabled = false;

                var documentsFolder = await StorageFolder.GetFolderFromPathAsync(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));

                string baseFileName = $"ViVeTool_Query_{DateTime.Now:yyyyMMdd_HHmmss}";

                var txtFile = await documentsFolder.CreateFileAsync(
                    $"{baseFileName}.txt",
                    CreationCollisionOption.GenerateUniqueName);

                // 使用 StringBuilder 优化大字符串拼接
                var sbTxt = new System.Text.StringBuilder();
                foreach (var f in _allFeatures)
                {
                    sbTxt.AppendLine($"[{f.Id}] {f.Name}");
                    sbTxt.AppendLine($"  Priority: {f.Priority}");
                    sbTxt.AppendLine($"  State: {f.State}");
                    sbTxt.AppendLine($"  Type: {f.Type}");
                    sbTxt.AppendLine();
                }
                await FileIO.WriteTextAsync(txtFile, sbTxt.ToString());

                var csvFile = await documentsFolder.CreateFileAsync(
                    $"{baseFileName}.csv",
                    CreationCollisionOption.GenerateUniqueName);

                var sbCsv = new System.Text.StringBuilder();
                sbCsv.AppendLine("ID,Priority,State,Type,Name");
                foreach (var f in _allFeatures)
                {
                    sbCsv.AppendLine($"{f.Id},{f.Priority},{f.State},{f.Type},\"{f.Name}\"");
                }
                await FileIO.WriteTextAsync(csvFile, sbCsv.ToString());

                var dialog = new Dialogs.SuccessDialog(
                    $"Exported successfully!\n\nTXT: {txtFile.Name}\nCSV: {csvFile.Name}",
                    documentsFolder.Path);
                dialog.XamlRoot = this.XamlRoot;
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ExportButton] Exception: {ex}");
                var dialog = new Dialogs.ErrorDialog($"Export failed: {ex.Message}");
                dialog.XamlRoot = this.XamlRoot;
                await dialog.ShowAsync();
            }
            finally
            {
                ExportButton.IsEnabled = true;
            }
        }
    }
}
