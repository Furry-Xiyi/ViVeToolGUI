using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
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
                var ids = featureIds.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                   .Select(id => id.Trim())
                                   .Where(id => !string.IsNullOrEmpty(id));

                StringBuilder resultBuilder = new StringBuilder();

                foreach (var id in ids)
                {
                    string action = enable ? "/enable" : "/disable";
                    string arguments = $"{action} /id:{id}";

                    if (variant > 0)
                    {
                        arguments += $" /variant:{variant}";
                    }

                    var result = await ExecuteViVeToolAsync(arguments);
                    resultBuilder.AppendLine($"[{id}] {result.Output}");

                    if (result.ExitCode != 0 && !string.IsNullOrEmpty(result.Error))
                    {
                        resultBuilder.AppendLine($"Error: {result.Error}");
                    }
                }

                ResultBorder.Visibility = Visibility.Visible;
                ResultText.Text = resultBuilder.ToString();
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