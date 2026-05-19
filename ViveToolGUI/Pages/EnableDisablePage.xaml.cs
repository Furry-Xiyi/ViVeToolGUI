using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.Resources;
using Windows.Storage;
using Windows.System;

namespace ViVeToolGUI.Pages
{
    public enum FeatureOverrideState
    {
        Unknown = 0,
        Disabled = 1,
        Enabled = 2
    }
        public sealed class OperationRecord
        {
            public string Time { get; set; } = "";
            public string FeatureId { get; set; } = "";
            public string Operation { get; set; } = "";
        }

        public static class OperationLogger
        {
            private static readonly string _filePath = Path.Combine(
                ApplicationData.Current.LocalFolder.Path, "operation_history.log");

            public static List<OperationRecord> Load()
            {
                var list = new List<OperationRecord>();
                if (!File.Exists(_filePath)) return list;

                foreach (var line in File.ReadAllLines(_filePath))
                {
                    var parts = line.Split('|');
                    if (parts.Length >= 3)
                    {
                        list.Add(new OperationRecord
                        {
                            Time = parts[0],
                            FeatureId = parts[1],
                            Operation = parts[2]
                        });
                    }
                }
                list.Reverse();
                return list;
            }

            public static void Append(string featureId, string operation)
            {
                var time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                File.AppendAllText(_filePath,
                    $"{time}|{featureId}|{operation}{Environment.NewLine}");
            }

            public static void Delete(OperationRecord record)
            {
                if (!File.Exists(_filePath)) return;
                var lines = new List<string>(File.ReadAllLines(_filePath));
                var target = $"{record.Time}|{record.FeatureId}|{record.Operation}";
                lines.Remove(target);
                File.WriteAllLines(_filePath, lines);
            }

            public static void Clear()
            {
                if (File.Exists(_filePath)) File.Delete(_filePath);
            }
        }

    public sealed partial class EnableDisablePage : Page
    {
        private readonly ResourceLoader _resourceLoader;
        private string _variantMode = "None";
        private readonly ObservableCollection<OperationRecord> _history = new();

        public EnableDisablePage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Enabled;
            _resourceLoader = ResourceLoader.GetForViewIndependentUse();
            HistoryListView.ItemsSource = _history;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            LoadHistory();
        }

        private void LoadHistory()
        {
            _history.Clear();
            foreach (var record in OperationLogger.Load())
                _history.Add(record);
        }

        private void VariantModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (VariantModeComboBox == null || VariantNumberBox == null)
                return;

            if (VariantModeComboBox.SelectedItem is not ComboBoxItem item)
                return;

            _variantMode = item.Tag?.ToString() ?? "None";
            VariantNumberBox.IsEnabled = _variantMode == "Custom";

            if (_variantMode != "Custom")
                VariantNumberBox.Value = 0;
        }

        private void FeatureIDTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (FeatureIDTextBox == null)
                return;

            bool valid = TryParseFeatureIds(out _);

            EnableButton.IsEnabled = valid;
            DisableButton.IsEnabled = valid;
            RestoreButton.IsEnabled = valid;

            ValidationText.Visibility = valid || string.IsNullOrWhiteSpace(FeatureIDTextBox.Text)
                ? Visibility.Collapsed
                : Visibility.Visible;

            if (ValidationText.Visibility == Visibility.Visible)
                ValidationText.Text = _resourceLoader.GetString("EnableDisable_InvalidID");
        }

        private async void FeatureIDTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != VirtualKey.Enter)
                return;

            e.Handled = true;

            if (EnableButton.IsEnabled)
                await ExecuteFeatureCommandAsync(true);
        }

        private void HistoryMenuFlyout_Opening(object sender, object e)
        {
            if (sender is not MenuFlyout menu) return;

            if (menu.Target is not FrameworkElement el || el.DataContext is not OperationRecord record)
                return;

            var enableItem = menu.Items.FirstOrDefault(i => i is MenuFlyoutItem m && m.Name == "MenuHistoryEnable") as MenuFlyoutItem;
            var disableItem = menu.Items.FirstOrDefault(i => i is MenuFlyoutItem m && m.Name == "MenuHistoryDisable") as MenuFlyoutItem;

            if (enableItem != null)
                enableItem.Visibility = record.Operation.Equals("Enable", StringComparison.OrdinalIgnoreCase)
                    ? Visibility.Collapsed : Visibility.Visible;

            if (disableItem != null)
                disableItem.Visibility = record.Operation.Equals("Disable", StringComparison.OrdinalIgnoreCase)
                    ? Visibility.Collapsed : Visibility.Visible;
        }

        private async void EnableButton_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteFeatureCommandAsync(true);
        }

        private async void DisableButton_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteFeatureCommandAsync(false);
        }

        private async void RestoreButton_Click(object sender, RoutedEventArgs e)
        {
            if (!TryParseFeatureIds(out var ids))
                return;

            SetBusy(false);
            MainWindow.Instance?.ShowLoadingOverlay(GetResourceText("EnableDisable_RestoreButton"));
            MainWindow.Instance?.ShowTaskbarIndeterminate();

            try
            {
                string idArg = BuildIdArgument(ids);
                string arguments = $"/reset /id:{idArg}";
                var result = await MainWindow.ExecuteViVeToolCommandAsync(arguments);

                if (result.ExitCode == 0)
                {
                    MainWindow.Instance?.ShowTaskbarCompleted();
                    OperationLogger.Append(idArg, "Restore");
                    LoadHistory();

                    var dialog = new Dialogs.SuccessDialog(_resourceLoader.GetString("FeatureStore_RestoreSuccess"));
                    dialog.XamlRoot = this.XamlRoot;
                    await dialog.ShowAsync();
                }
                else
                {
                    MainWindow.Instance?.ShowTaskbarError();
                    await ShowErrorAsync(GetCommandError(result));
                }
            }
            catch (Exception ex)
            {
                MainWindow.Instance?.ShowTaskbarError();
                await ShowErrorAsync(ex.Message);
            }
            finally
            {
                MainWindow.Instance?.HideLoadingOverlay();
                SetBusy(true);
            }
        }

        private async Task ExecuteFeatureCommandAsync(bool enable)
        {
            if (!TryParseFeatureIds(out var ids))
                return;

            SetBusy(false);
            MainWindow.Instance?.ShowLoadingOverlay(GetResourceText(enable
                ? "EnableDisable_EnableButton"
                : "EnableDisable_DisableButton"));
            MainWindow.Instance?.ShowTaskbarIndeterminate();

            try
            {
                string action = enable ? "/enable" : "/disable";
                string idArg = BuildIdArgument(ids);
                string arguments = $"{action} /id:{idArg}";

                switch (_variantMode)
                {
                    case "Default":
                        arguments += " /variant:default";
                        break;
                    case "Clear":
                        arguments += " /variant:clear";
                        break;
                    case "Custom":
                        int variant = (int)VariantNumberBox.Value;
                        if (variant > 0)
                            arguments += $" /variant:{variant}";
                        break;
                }

                var result = await MainWindow.ExecuteViVeToolCommandAsync(arguments);

                if (result.ExitCode == 0)
                {
                    MainWindow.Instance?.ShowTaskbarCompleted();
                    OperationLogger.Append(idArg, enable ? "Enable" : "Disable");
                    LoadHistory();

                    string successKey = enable
                        ? "FeatureStore_EnableSuccess"
                        : "FeatureStore_DisableSuccess";

                    var dialog = new Dialogs.SuccessDialog(_resourceLoader.GetString(successKey));
                    dialog.XamlRoot = this.XamlRoot;
                    await dialog.ShowAsync();
                }
                else
                {
                    MainWindow.Instance?.ShowTaskbarError();
                    await ShowErrorAsync(GetCommandError(result));
                }
            }
            catch (Exception ex)
            {
                MainWindow.Instance?.ShowTaskbarError();
                await ShowErrorAsync(ex.Message);
            }
            finally
            {
                MainWindow.Instance?.HideLoadingOverlay();
                SetBusy(true);
            }
        }

        private async void HistoryEnable_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.CommandParameter is OperationRecord record &&
                uint.TryParse(record.FeatureId, out uint id))
            {
                MainWindow.Instance?.ShowLoadingOverlay(GetResourceText("EnableDisable_EnableButton"));
                MainWindow.Instance?.ShowTaskbarIndeterminate();
                try
                {
                    var result = await MainWindow.ExecuteViVeToolCommandAsync($"/enable /id:{id}");
                    if (result.ExitCode == 0)
                    {
                        MainWindow.Instance?.ShowTaskbarCompleted();
                        OperationLogger.Append(record.FeatureId, "Enable");
                        LoadHistory();
                    }
                    else
                    {
                        MainWindow.Instance?.ShowTaskbarError();
                        await ShowErrorAsync(GetCommandError(result));
                    }
                }
                catch (Exception ex)
                {
                    MainWindow.Instance?.ShowTaskbarError();
                    await ShowErrorAsync(ex.Message);
                }
                finally { MainWindow.Instance?.HideLoadingOverlay(); }
            }
        }

        private async void HistoryDisable_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.CommandParameter is OperationRecord record &&
                uint.TryParse(record.FeatureId, out uint id))
            {
                MainWindow.Instance?.ShowLoadingOverlay(GetResourceText("EnableDisable_DisableButton"));
                MainWindow.Instance?.ShowTaskbarIndeterminate();
                try
                {
                    var result = await MainWindow.ExecuteViVeToolCommandAsync($"/disable /id:{id}");
                    if (result.ExitCode == 0)
                    {
                        MainWindow.Instance?.ShowTaskbarCompleted();
                        OperationLogger.Append(record.FeatureId, "Disable");
                        LoadHistory();
                    }
                    else
                    {
                        MainWindow.Instance?.ShowTaskbarError();
                        await ShowErrorAsync(GetCommandError(result));
                    }
                }
                catch (Exception ex)
                {
                    MainWindow.Instance?.ShowTaskbarError();
                    await ShowErrorAsync(ex.Message);
                }
                finally { MainWindow.Instance?.HideLoadingOverlay(); }
            }
        }

        private async void HistoryRestore_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.CommandParameter is OperationRecord record &&
                uint.TryParse(record.FeatureId, out uint id))
            {
                MainWindow.Instance?.ShowLoadingOverlay(GetResourceText("EnableDisable_RestoreButton"));
                MainWindow.Instance?.ShowTaskbarIndeterminate();
                try
                {
                    var result = await MainWindow.ExecuteViVeToolCommandAsync($"/reset /id:{id}");
                    if (result.ExitCode == 0)
                    {
                        MainWindow.Instance?.ShowTaskbarCompleted();
                        OperationLogger.Append(record.FeatureId, "Restore");
                        LoadHistory();
                    }
                    else
                    {
                        MainWindow.Instance?.ShowTaskbarError();
                        await ShowErrorAsync(GetCommandError(result));
                    }
                }
                catch (Exception ex)
                {
                    MainWindow.Instance?.ShowTaskbarError();
                    await ShowErrorAsync(ex.Message);
                }
                finally { MainWindow.Instance?.HideLoadingOverlay(); }
            }
        }

        private void HistoryCopyId_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.CommandParameter is OperationRecord record)
            {
                var dataPackage = new DataPackage();
                dataPackage.SetText(record.FeatureId);
                Clipboard.SetContent(dataPackage);
            }
        }

        private void HistoryDelete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.CommandParameter is OperationRecord record)
            {
                OperationLogger.Delete(record);
                _history.Remove(record);
            }
        }

        private void ClearHistoryButton_Click(object sender, RoutedEventArgs e)
        {
            OperationLogger.Clear();
            _history.Clear();
        }

        private bool TryParseFeatureIds(out List<uint> ids)
        {
            ids = new List<uint>();
            string input = FeatureIDTextBox.Text ?? "";

            if (string.IsNullOrWhiteSpace(input))
                return false;

            if (!Regex.IsMatch(input, @"^[\d,\s]+$"))
                return false;

            foreach (string part in input.Split(new[] { ',', ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!uint.TryParse(part, out uint id))
                    return false;
                ids.Add(id);
            }

            return ids.Count > 0;
        }

        private string BuildIdArgument(IEnumerable<uint> ids)
        {
            return string.Join(",", ids.Distinct());
        }

        private string GetCommandError(CommandResult result)
        {
            if (!string.IsNullOrWhiteSpace(result.Output))
                return result.Output;
            if (!string.IsNullOrWhiteSpace(result.Error))
                return result.Error;
            return $"ViVeTool exited with code {result.ExitCode}.";
        }

        private void SetBusy(bool enabled)
        {
            bool valid = enabled && TryParseFeatureIds(out _);
            EnableButton.IsEnabled = valid;
            DisableButton.IsEnabled = valid;
            RestoreButton.IsEnabled = valid;
            FeatureIDTextBox.IsEnabled = enabled;
            VariantModeComboBox.IsEnabled = enabled;
            VariantNumberBox.IsEnabled = enabled && _variantMode == "Custom";
        }

        private async Task ShowErrorAsync(string message)
        {
            var dialog = new Dialogs.ErrorDialog(message);
            dialog.XamlRoot = this.XamlRoot;
            await dialog.ShowAsync();
        }

        private string GetResourceText(string key)
        {
            string text = _resourceLoader.GetString(key);
            if (!string.IsNullOrWhiteSpace(text)) return text;
            text = _resourceLoader.GetString($"{key}/Text");
            if (!string.IsNullOrWhiteSpace(text)) return text;
            return key;
        }
    }
}