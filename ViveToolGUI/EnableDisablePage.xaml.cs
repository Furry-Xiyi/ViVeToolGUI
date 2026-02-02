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

        public EnableDisablePage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Enabled;
            _resourceLoader = ResourceLoader.GetForViewIndependentUse();
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
            int variant = (int)VariantNumberBox.Value;

            EnableButton.IsEnabled = false;
            DisableButton.IsEnabled = false;
            ResultBorder.Visibility = Visibility.Collapsed;

            try
            {
                // 规范化 ID 列表：去掉空格，只保留逗号分隔
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

                if (variant > 0)
                {
                    arguments += $" /variant:{variant}";
                }

                var result = await MainWindow.ExecuteViVeToolCommandAsync(arguments);

                ResultBorder.Visibility = Visibility.Visible;

                if (result.ExitCode == 0)
                {
                    // 这里你可以简单显示原始输出，或者自己格式化
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