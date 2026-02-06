using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;

namespace ViVeToolGUI
{
    public sealed partial class EnableDisablePage : Page
    {
        private readonly ResourceLoader _resourceLoader;
        private string _variantMode = "Custom";
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

            if (VariantModeComboBox.SelectedItem is ComboBoxItem item)
            {
                _variantMode = item.Tag?.ToString() ?? "Custom";

                VariantNumberBox.IsEnabled = _variantMode == "Custom";

                if (_variantMode != "Custom")
                {
                    VariantNumberBox.Value = 0;
                }
            }
        }

        private void FeatureIDTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string input = FeatureIDTextBox.Text;
            bool isValid = !string.IsNullOrWhiteSpace(input) &&
                          Regex.IsMatch(input, @"^[\d,\s]+$");

            EnableButton.IsEnabled = isValid;
            DisableButton.IsEnabled = isValid;

            if (!string.IsNullOrWhiteSpace(input) && !isValid)
            {
                ValidationText.Text = _resourceLoader.GetString("EnableDisable_InvalidID");
                ValidationText.Visibility = Visibility.Visible;
            }
            else
            {
                ValidationText.Visibility = Visibility.Collapsed;
            }
        }

        private async void EnableButton_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteFeatureCommandAsync(true);
        }

        private async void DisableButton_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteFeatureCommandAsync(false);
        }

        private async Task ExecuteFeatureCommandAsync(bool enable)
        {
            string featureIds = FeatureIDTextBox.Text;

            EnableButton.IsEnabled = false;
            DisableButton.IsEnabled = false;
            ResultBorder.Visibility = Visibility.Collapsed;

            try
            {
                var ids = featureIds
                    .Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(id => id.Trim())
                    .Where(id => !string.IsNullOrEmpty(id))
                    .ToArray();

                if (ids.Length == 0)
                {
                    ResultText.Text = _resourceLoader.GetString("EnableDisable_InvalidID");
                    ResultBorder.Visibility = Visibility.Visible;
                    return;
                }

                string idArg = string.Join(",", ids);
                string action = enable ? "/enable" : "/disable";
                string arguments = $"{action} /id:{idArg}";

                // 🔥 改进的 Variant 参数构建
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
                        {
                            arguments += $" /variant:{variant}";
                        }
                        break;
                }

                var result = await MainWindow.ExecuteViVeToolCommandAsync(arguments);

                ResultBorder.Visibility = Visibility.Visible;

                if (result.ExitCode == 0)
                {
                    ResultText.Text = string.IsNullOrWhiteSpace(result.Output)
                        ? "Success"
                        : result.Output;
                }
                else
                {
                    ResultText.Text = $"Error: {result.Error}";
                }
            }
            catch (Exception ex)
            {
                ResultText.Text = $"{_resourceLoader.GetString("Command_Error")}\n{ex.Message}";
                var dialog = new Dialogs.ErrorDialog(ex.Message);
                dialog.XamlRoot = this.XamlRoot;
                await dialog.ShowAsync();
            }
            finally
            {
                EnableButton.IsEnabled = true;
                DisableButton.IsEnabled = true;
            }
        }
    }
}