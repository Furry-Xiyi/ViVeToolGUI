using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
        private ObservableCollection<FeatureInfo> _allFeatures = new ObservableCollection<FeatureInfo>();

        public QueryPage()
        {
            this.InitializeComponent();
            _resourceLoader = ResourceLoader.GetForViewIndependentUse();
        }

        private async void QueryAllButton_Click(object sender, RoutedEventArgs e)
        {
            QueryAllButton.IsEnabled = false;
            QueryProgressBar.Visibility = Visibility.Visible;
            _allFeatures.Clear();

            try
            {
                var result = await ExecuteViVeToolAsync("/query");

                if (result.ExitCode == 0)
                {
                    ParseQueryOutput(result.Output);
                    FeaturesListView.ItemsSource = _allFeatures;
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
                var dialog = new Dialogs.ErrorDialog(ex.Message);
                dialog.XamlRoot = this.XamlRoot;
                await dialog.ShowAsync();
            }
            finally
            {
                QueryAllButton.IsEnabled = true;
                QueryProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        private void ParseQueryOutput(string output)
        {
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            FeatureInfo currentFeature = null;

            foreach (var line in lines)
            {
                var idMatch = Regex.Match(line, @"\[(\d+)\](?:\s*\(([^)]+)\))?");
                if (idMatch.Success)
                {
                    if (currentFeature != null)
                    {
                        _allFeatures.Add(currentFeature);
                    }

                    currentFeature = new FeatureInfo
                    {
                        Id = idMatch.Groups[1].Value,
                        Name = idMatch.Groups[2].Success ? idMatch.Groups[2].Value : ""
                    };
                }
                else if (currentFeature != null)
                {
                    if (line.Contains("Priority"))
                    {
                        var match = Regex.Match(line, @"Priority\s*:\s*(.+?)(?:\s*\(|$)");
                        if (match.Success)
                            currentFeature.Priority = match.Groups[1].Value.Trim();
                    }
                    else if (line.Contains("State"))
                    {
                        var match = Regex.Match(line, @"State\s*:\s*(.+?)(?:\s*\(|$)");
                        if (match.Success)
                            currentFeature.State = match.Groups[1].Value.Trim();
                    }
                    else if (line.Contains("Type") || line.Contains("Variant"))
                    {
                        var match = Regex.Match(line, @"(?:Type|Variant)\s*:\s*(.+?)(?:\s*\(|$)");
                        if (match.Success)
                            currentFeature.Type = match.Groups[1].Value.Trim();
                    }
                }
            }

            if (currentFeature != null)
            {
                _allFeatures.Add(currentFeature);
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = SearchBox.Text.ToLower();

            if (string.IsNullOrWhiteSpace(searchText))
            {
                FeaturesListView.ItemsSource = _allFeatures;
            }
            else
            {
                var filtered = _allFeatures.Where(f =>
                    (f.Id?.Contains(searchText) ?? false) ||
                    (f.Name?.ToLower().Contains(searchText) ?? false) ||
                    (f.State?.ToLower().Contains(searchText) ?? false) ||
                    (f.Priority?.ToLower().Contains(searchText) ?? false)
                ).ToList();

                FeaturesListView.ItemsSource = new ObservableCollection<FeatureInfo>(filtered);
            }
        }

        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            var savePicker = new FileSavePicker();
            var hWnd = WindowNative.GetWindowHandle(App.MainWindow);
            InitializeWithWindow.Initialize(savePicker, hWnd);

            savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            savePicker.FileTypeChoices.Add("Text File", new List<string>() { ".txt" });
            savePicker.FileTypeChoices.Add("CSV File", new List<string>() { ".csv" });
            savePicker.SuggestedFileName = $"ViVeTool_Query_{DateTime.Now:yyyyMMdd_HHmmss}";

            var file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                await ExportToFileAsync(file);
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
                    foreach (var feature in _allFeatures)
                    {
                        content += $"{feature.Id},{feature.Priority},{feature.State},{feature.Type},{feature.Name}\n";
                    }
                }
                else
                {
                    content = "";
                    foreach (var feature in _allFeatures)
                    {
                        content += $"[{feature.Id}] {feature.Name}\n";
                        content += $"  Priority: {feature.Priority}\n";
                        content += $"  State: {feature.State}\n";
                        content += $"  Type: {feature.Type}\n\n";
                    }
                }

                await FileIO.WriteTextAsync(file, content);

                var successDialog = new Dialogs.SuccessDialog(_resourceLoader.GetString("Export_Success"));
                successDialog.XamlRoot = this.XamlRoot;
                await successDialog.ShowAsync();
            }
            catch (Exception ex)
            {
                var dialog = new Dialogs.ErrorDialog(ex.Message);
                dialog.XamlRoot = this.XamlRoot;
                await dialog.ShowAsync();
            }
        }

        private async Task<CommandResult> ExecuteViVeToolAsync(string arguments)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"& '{App.ViVeToolPath}' {arguments}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    };

                    using var process = new Process { StartInfo = psi };
                    var output = new StringBuilder();
                    var error = new StringBuilder();

                    process.OutputDataReceived += (s, e) => { if (e.Data != null) output.AppendLine(e.Data); };
                    process.ErrorDataReceived += (s, e) => { if (e.Data != null) error.AppendLine(e.Data); };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();

                    return new CommandResult
                    {
                        ExitCode = process.ExitCode,
                        Output = output.ToString(),
                        Error = error.ToString()
                    };
                }
                catch (Exception ex)
                {
                    return new CommandResult
                    {
                        ExitCode = -1,
                        Output = "",
                        Error = ex.Message
                    };
                }
            });
        }

        private class CommandResult
        {
            public int ExitCode { get; set; }
            public string Output { get; set; } = "";
            public string Error { get; set; } = "";
        }
    }
}