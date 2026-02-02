using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;

namespace ViVeToolGUI
{
    public sealed partial class ResetPage : Page
    {
        private readonly ResourceLoader _resourceLoader;

        public ResetPage()
        {
            this.InitializeComponent();
            _resourceLoader = ResourceLoader.GetForViewIndependentUse();
        }

        private async void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            var confirmDialog = new Dialogs.ConfirmDialog(
                _resourceLoader.GetString("Reset_ConfirmTitle"),
                _resourceLoader.GetString("Reset_ConfirmMessage")
            );
            confirmDialog.XamlRoot = this.XamlRoot;

            var result = await confirmDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await ExecuteResetAsync("/reset");
            }
        }

        private async void FullResetButton_Click(object sender, RoutedEventArgs e)
        {
            var confirmDialog = new Dialogs.ConfirmDialog(
                _resourceLoader.GetString("Reset_FullConfirmTitle"),
                _resourceLoader.GetString("Reset_FullConfirmMessage")
            );
            confirmDialog.XamlRoot = this.XamlRoot;

            var result = await confirmDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await ExecuteResetAsync("/fullreset");
            }
        }

        private async void FixPriorityButton_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteResetAsync("/fixpriority");
        }

        private async Task ExecuteResetAsync(string command)
        {
            ResetButton.IsEnabled = false;
            FullResetButton.IsEnabled = false;
            FixPriorityButton.IsEnabled = false;
            ResultBorder.Visibility = Visibility.Collapsed;

            try
            {
                var result = await MainWindow.ExecuteViVeToolCommandAsync(command);

                ResultBorder.Visibility = Visibility.Visible;

                if (result.ExitCode == 0)
                {
                    ResultText.Text = string.IsNullOrWhiteSpace(result.Output)
                        ? _resourceLoader.GetString("Command_Success")
                        : result.Output;

                    var successDialog = new Dialogs.SuccessDialog(_resourceLoader.GetString("Reset_SuccessMessage"));
                    successDialog.XamlRoot = this.XamlRoot;
                    await successDialog.ShowAsync();
                }
                else
                {
                    ResultText.Text = $"{_resourceLoader.GetString("Command_Failed")}\n{result.Error}";
                    var errorDialog = new Dialogs.ErrorDialog(result.Error);
                    errorDialog.XamlRoot = this.XamlRoot;
                    await errorDialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                ResultText.Text = $"{_resourceLoader.GetString("Command_Error")}\n{ex.Message}";
                var errorDialog = new Dialogs.ErrorDialog(ex.Message);
                errorDialog.XamlRoot = this.XamlRoot;
                await errorDialog.ShowAsync();
            }
            finally
            {
                ResetButton.IsEnabled = true;
                FullResetButton.IsEnabled = true;
                FixPriorityButton.IsEnabled = true;
            }
        }
    }
}