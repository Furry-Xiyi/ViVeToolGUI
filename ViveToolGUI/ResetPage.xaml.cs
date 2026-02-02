using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.Text;
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
                var result = await ExecuteViVeToolAsync(command);

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