using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;

namespace ViVeToolGUI.Pages
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
                _resourceLoader.GetString("Reset_ConfirmMessage"));

            confirmDialog.XamlRoot = this.XamlRoot;

            var result = await confirmDialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
                return;

            await ExecuteResetAsync();
        }

        private async Task ExecuteResetAsync()
        {
            ResetButton.IsEnabled = false;
            ResultBorder.Visibility = Visibility.Collapsed;

            MainWindow.Instance?.ShowLoadingOverlay(_resourceLoader.GetString("Reset_Breadcrumb"));
            MainWindow.Instance?.ShowTaskbarIndeterminate();

            try
            {
                var result = await MainWindow.ExecuteViVeToolCommandAsync("/fullreset");

                ResultBorder.Visibility = Visibility.Visible;

                if (result.ExitCode == 0)
                {
                    MainWindow.Instance?.ShowTaskbarCompleted();

                    ResultText.Text = string.IsNullOrWhiteSpace(result.Output)
                        ? _resourceLoader.GetString("Reset_SuccessMessage")
                        : result.Output;

                    var dialog = new Dialogs.SuccessDialog(_resourceLoader.GetString("Reset_SuccessMessage"));
                    dialog.XamlRoot = this.XamlRoot;
                    await dialog.ShowAsync();
                }
                else
                {
                    MainWindow.Instance?.ShowTaskbarError();

                    string message = GetCommandError(result);
                    ResultText.Text = message;

                    var dialog = new Dialogs.ErrorDialog(message);
                    dialog.XamlRoot = this.XamlRoot;
                    await dialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                MainWindow.Instance?.ShowTaskbarError();

                ResultText.Text = ex.Message;
                ResultBorder.Visibility = Visibility.Visible;

                var dialog = new Dialogs.ErrorDialog(ex.Message);
                dialog.XamlRoot = this.XamlRoot;
                await dialog.ShowAsync();
            }
            finally
            {
                MainWindow.Instance?.HideLoadingOverlay();
                ResetButton.IsEnabled = true;
            }
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