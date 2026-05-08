using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;
using Windows.System;

namespace ViVeToolGUI.Pages
{
    public enum FeatureOverrideState
    {
        Unknown = 0,
        Disabled = 1,
        Enabled = 2
    }

    public sealed partial class EnableDisablePage : Page
    {
        private readonly ResourceLoader _resourceLoader;
        private string _variantMode = "None";

        public EnableDisablePage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Enabled;
            _resourceLoader = ResourceLoader.GetForViewIndependentUse();
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
                string arguments = $"/reset /id:{BuildIdArgument(ids)}";
                var result = await MainWindow.ExecuteViVeToolCommandAsync(arguments);

                if (result.ExitCode == 0)
                {
                    MainWindow.Instance?.ShowTaskbarCompleted();

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
                string arguments = $"{action} /id:{BuildIdArgument(ids)}";

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
            if (!string.IsNullOrWhiteSpace(text))
                return text;

            text = _resourceLoader.GetString($"{key}/Text");
            if (!string.IsNullOrWhiteSpace(text))
                return text;

            return key;
        }
    }
}