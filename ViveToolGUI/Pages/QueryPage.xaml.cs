using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.Resources;
using Windows.Storage;
using Windows.System;
using Windows.System.UserProfile;

namespace ViVeToolGUI.Pages
{
    public sealed class FeatureInfo
    {
        public string Id { get; set; } = "";
        public string State { get; set; } = "";
        public string Variant { get; set; } = "";
        public string VariantPayload { get; set; } = "";
    }

    public sealed partial class QueryPage : Page
    {
        private readonly ResourceLoader _resourceLoader;
        private readonly ObservableCollection<FeatureInfo> _features = new();
        private FeatureInfo[] _allFeatures = Array.Empty<FeatureInfo>();

        public QueryPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Enabled;
            _resourceLoader = ResourceLoader.GetForViewIndependentUse();
            FeaturesListView.ItemsSource = _features;
        }

        private async void QueryAllButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadAsync();
        }

        private async Task LoadAsync()
        {
            QueryAllButton.IsEnabled = false;
            ExportButton.IsEnabled = false;
            SearchBox.IsEnabled = false;
            MainWindow.Instance?.ShowLoadingOverlay(_resourceLoader.GetString("Query_Loading"));

            try
            {
                MainWindow.Instance?.ShowLoadingOverlay(_resourceLoader.GetString("Query_Loading"));

                if (!await App.EnsureViVeToolInitializedAsync())
                    throw new InvalidOperationException(_resourceLoader.GetString("ViVeTool_Initializing"));

                var result = await MainWindow.ExecuteViVeToolCommandAsync("/query");

                if (result.ExitCode != 0)
                    throw new InvalidOperationException(GetCommandError(result));

                _allFeatures = await Task.Run(() => ParseQueryOutput(result.Output));

                ApplyFilter();

                ExportButton.IsEnabled = _allFeatures.Length > 0;
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
                SearchBox.IsEnabled = true;
                MainWindow.Instance?.HideLoadingOverlay();
            }
        }

        private FeatureInfo[] ParseQueryOutput(string output)
        {
            if (string.IsNullOrWhiteSpace(output))
                return Array.Empty<FeatureInfo>();

            output = Regex.Replace(output, @"\x1B\[[0-9;]*m", "");
            output = output.Replace("\uFEFF", "");

            var list = new Collection<FeatureInfo>();
            var lines = Regex.Split(output, @"\r\n|\n|\r")
                .Select(x => x.TrimEnd())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToArray();

            FeatureInfo current = null;

            foreach (string line in lines)
            {
                var idMatch = Regex.Match(line, @"\[(\d+)\](?:\s*\(([^)]+)\))?");
                if (idMatch.Success)
                {
                    if (current != null)
                        list.Add(current);

                    current = new FeatureInfo
                    {
                        Id = Clean(idMatch.Groups[1].Value),
                        VariantPayload = idMatch.Groups[2].Success ? Clean(idMatch.Groups[2].Value.Trim()) : ""
                    };

                    continue;
                }

                if (current == null)
                    continue;

                if (line.Contains("State", StringComparison.OrdinalIgnoreCase))
                {
                    var match = Regex.Match(line, @"State\s*:\s*([A-Za-z]+)", RegexOptions.IgnoreCase);
                    if (match.Success)
                        current.State = Clean(match.Groups[1].Value);

                    continue;
                }

                if (line.Contains("Priority", StringComparison.OrdinalIgnoreCase))
                {
                    var match = Regex.Match(line, @"Priority\s*:\s*([A-Za-z]+)", RegexOptions.IgnoreCase);
                    if (match.Success)
                        current.Variant = Clean(match.Groups[1].Value);

                    continue;
                }

                if (line.Contains("Type", StringComparison.OrdinalIgnoreCase))
                {
                    var match = Regex.Match(line, @"Type\s*:\s*([A-Za-z]+)", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        if (string.IsNullOrWhiteSpace(current.VariantPayload))
                            current.VariantPayload = Clean(match.Groups[1].Value);
                        else
                            current.VariantPayload = $"{current.VariantPayload} / {Clean(match.Groups[1].Value)}";
                    }
                }
            }

            if (current != null)
                list.Add(current);

            return list
                .Where(x => !string.IsNullOrWhiteSpace(x.Id))
                .GroupBy(x => x.Id)
                .Select(x => x.First())
                .OrderBy(x => uint.TryParse(x.Id, out uint id) ? id : uint.MaxValue)
                .ToArray();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            string text = SearchBox.Text?.Trim() ?? "";

            _features.Clear();

            var source = string.IsNullOrWhiteSpace(text)
                ? _allFeatures
                : _allFeatures.Where(x =>
                    x.Id.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                    x.State.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                    x.Variant.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                    x.VariantPayload.Contains(text, StringComparison.OrdinalIgnoreCase));

            foreach (var item in source)
                _features.Add(item);
        }

        private async void EnableFeature_Click(object sender, RoutedEventArgs e)
        {
            await ApplyFromMenuAsync(sender, true);
        }

        private async void DisableFeature_Click(object sender, RoutedEventArgs e)
        {
            await ApplyFromMenuAsync(sender, false);
        }

        private async void RestoreFeature_Click(object sender, RoutedEventArgs e)
        {
            if (GetFeature(sender) is not FeatureInfo feature || !uint.TryParse(feature.Id, out uint id))
                return;

            MainWindow.Instance?.ShowLoadingOverlay(_resourceLoader.GetString("Query_Loading"));

            try
            {
                MainWindow.Instance?.ShowLoadingOverlay(_resourceLoader.GetString("Query_Loading"));
                var result = await MainWindow.ExecuteViVeToolCommandAsync($"/reset /id:{id}");

                if (result.ExitCode != 0)
                    throw new InvalidOperationException(GetCommandError(result));

                await LoadAsync();
            }
            catch (Exception ex)
            {
                var dialog = new Dialogs.ErrorDialog(ex.Message);
                dialog.XamlRoot = this.XamlRoot;
                await dialog.ShowAsync();
            }
            finally
            {
                MainWindow.Instance?.HideLoadingOverlay();
            }
        }

        private async Task ApplyFromMenuAsync(object sender, bool enable)
        {
            if (GetFeature(sender) is not FeatureInfo feature || !uint.TryParse(feature.Id, out uint id))
                return;

            MainWindow.Instance?.ShowLoadingOverlay(_resourceLoader.GetString("Query_Loading"));

            try
            {
                MainWindow.Instance?.ShowLoadingOverlay(_resourceLoader.GetString("Query_Loading"));
                string command = enable ? "/enable" : "/disable";
                var result = await MainWindow.ExecuteViVeToolCommandAsync($"{command} /id:{id}");

                if (result.ExitCode != 0)
                    throw new InvalidOperationException(GetCommandError(result));

                var dialog = new Dialogs.SuccessDialog(string.IsNullOrWhiteSpace(result.Output)
                    ? _resourceLoader.GetString("Command_Success")
                    : result.Output);

                dialog.XamlRoot = this.XamlRoot;
                await dialog.ShowAsync();

                await LoadAsync();
            }
            catch (Exception ex)
            {
                var dialog = new Dialogs.ErrorDialog(ex.Message);
                dialog.XamlRoot = this.XamlRoot;
                await dialog.ShowAsync();
            }
            finally
            {
                MainWindow.Instance?.HideLoadingOverlay();
            }
        }

        private void CopyFeatureId_Click(object sender, RoutedEventArgs e)
        {
            if (GetFeature(sender) is not FeatureInfo feature)
                return;

            var dataPackage = new DataPackage();
            dataPackage.SetText(feature.Id);
            Clipboard.SetContent(dataPackage);
        }

        private FeatureInfo GetFeature(object sender)
        {
            if (sender is MenuFlyoutItem item && item.CommandParameter is FeatureInfo feature)
                return feature;

            return null;
        }

        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_allFeatures.Length == 0)
                    return;

                ExportButton.IsEnabled = false;

                var documentsFolder = await StorageFolder.GetFolderFromPathAsync(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));

                string fileName = $"{_resourceLoader.GetString("Export_FilePrefix")}_{DateTime.Now:yyyyMMdd_HHmmss}.md";
                var file = await documentsFolder.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName);

                var sb = new StringBuilder();

                string idHeader = GetResourceText("Query_HeaderID");
                string stateHeader = GetResourceText("Query_HeaderState");
                string variantHeader = GetResourceText("Query_HeaderVariant");
                string payloadHeader = GetResourceText("Query_HeaderVariantPayload");

                sb.AppendLine($"| {EscapeMarkdown(idHeader)} | {EscapeMarkdown(stateHeader)} | {EscapeMarkdown(variantHeader)} | {EscapeMarkdown(payloadHeader)} |");
                sb.AppendLine("|---|---|---|---|");

                foreach (var f in _allFeatures)
                {
                    sb.AppendLine(
                        $"| {EscapeMarkdown(f.Id)} | {EscapeMarkdown(f.State)} | {EscapeMarkdown(f.Variant)} | {EscapeMarkdown(f.VariantPayload)} |");
                }

                await FileIO.WriteTextAsync(file, sb.ToString());

                var dialog = new Dialogs.SuccessDialog(_resourceLoader.GetString("Export_Success"), documentsFolder.Path);
                dialog.XamlRoot = this.XamlRoot;
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                var dialog = new Dialogs.ErrorDialog(ex.Message);
                dialog.XamlRoot = this.XamlRoot;
                await dialog.ShowAsync();
            }
            finally
            {
                ExportButton.IsEnabled = _allFeatures.Length > 0;
            }
        }

        private async void ViewButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string folder = GetFeatureTextFolder();
                var file = await StorageFile.GetFileFromApplicationUriAsync(
                    new Uri($"ms-appx:///Strings/{folder}/Features.txt"));

                bool launched = await Launcher.LaunchFileAsync(file);

                if (!launched)
                {
                    var dialog = new Dialogs.ErrorDialog(file.Path);
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
        }

        private string GetFeatureTextFolder()
        {
            string language = "";

            try
            {
                language = GlobalizationPreferences.Languages.FirstOrDefault() ?? "";
            }
            catch { }

            if (string.IsNullOrWhiteSpace(language))
                language = CultureInfo.CurrentUICulture.Name;

            if (language.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                return "zh-CN";

            return "en-US";
        }

        private string GetResourceText(string key)
        {
            string value = _resourceLoader.GetString(key);
            if (!string.IsNullOrWhiteSpace(value))
                return value;

            value = _resourceLoader.GetString($"{key}/Text");
            if (!string.IsNullOrWhiteSpace(value))
                return value;

            return key;
        }

        private string EscapeMarkdown(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            return text
                .Replace("\\", "\\\\")
                .Replace("|", "\\|")
                .Replace("\r", " ")
                .Replace("\n", " ");
        }

        private string Clean(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            var cleaned = new string(text.Where(c => !char.IsControl(c)).ToArray());
            return cleaned.Length > 10000 ? cleaned.Substring(0, 10000) : cleaned;
        }

        private string GetCommandError(CommandResult result)
        {
            if (!string.IsNullOrWhiteSpace(result.Output))
                return result.Output;

            if (!string.IsNullOrWhiteSpace(result.Error))
                return result.Error;

            return $"ViVeTool exited with code {result.ExitCode}.";
        }
    }
}