using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;

namespace ViVeToolGUI
{
    public sealed partial class SubfeaturesPage : Page
    {
        private readonly ResourceLoader _resourceLoader;

        public SubfeaturesPage()
        {
            this.InitializeComponent();
            _resourceLoader = ResourceLoader.GetForViewIndependentUse();
        }

        private void QuerySubFeatureIDTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            bool isValid = !string.IsNullOrWhiteSpace(QuerySubFeatureIDTextBox.Text) &&
                          Regex.IsMatch(QuerySubFeatureIDTextBox.Text, @"^\d+$");
            QuerySubsButton.IsEnabled = isValid;
        }

        private void AddFeatureIDTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ValidateAddInputs();
        }

        private void AddSubIDTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ValidateAddInputs();
        }

        private void ValidateAddInputs()
        {
            bool featureValid = !string.IsNullOrWhiteSpace(AddFeatureIDTextBox.Text) &&
                               Regex.IsMatch(AddFeatureIDTextBox.Text, @"^\d+$");
            bool subValid = !string.IsNullOrWhiteSpace(AddSubIDTextBox.Text) &&
                           Regex.IsMatch(AddSubIDTextBox.Text, @"^\d+$");

            AddSubButton.IsEnabled = featureValid && subValid;
        }

        private void DelFeatureIDTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ValidateDelInputs();
        }

        private void DelSubIDTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ValidateDelInputs();
        }

        private void ValidateDelInputs()
        {
            bool featureValid = !string.IsNullOrWhiteSpace(DelFeatureIDTextBox.Text) &&
                               Regex.IsMatch(DelFeatureIDTextBox.Text, @"^\d+$");
            bool subValid = !string.IsNullOrWhiteSpace(DelSubIDTextBox.Text) &&
                           Regex.IsMatch(DelSubIDTextBox.Text, @"^\d+$");

            DelSubButton.IsEnabled = featureValid && subValid;
        }

        private async void QuerySubsButton_Click(object sender, RoutedEventArgs e)
        {
            await QuerySubfeaturesAsync();
        }

        private async void AddSubButton_Click(object sender, RoutedEventArgs e)
        {
            await AddSubfeatureAsync();
        }

        private async void DelSubButton_Click(object sender, RoutedEventArgs e)
        {
            await DeleteSubfeatureAsync();
        }

        private async Task QuerySubfeaturesAsync()
        {
            QuerySubsButton.IsEnabled = false;
            SubfeaturesListBorder.Visibility = Visibility.Collapsed;
            ResultBorder.Visibility = Visibility.Collapsed;

            try
            {
                // 等待 ViVeTool 初始化完成
                if (!await App.EnsureViVeToolInitializedAsync())
                {
                    var dialog = new Dialogs.ErrorDialog("ViVeTool is still initializing. Please wait a moment and try again.");
                    dialog.XamlRoot = this.XamlRoot;
                    await dialog.ShowAsync();
                    return;
                }

                string featureId = QuerySubFeatureIDTextBox.Text;
                string arguments = $"/querysubs /id:{featureId}";

                var result = await MainWindow.ExecuteViVeToolCommandAsync(arguments);

                if (result.ExitCode == 0)
                {
                    SubfeaturesListText.Text = string.IsNullOrWhiteSpace(result.Output)
                        ? "No subfeatures found."
                        : result.Output;
                    SubfeaturesListBorder.Visibility = Visibility.Visible;
                }
                else
                {
                    ResultBorder.Visibility = Visibility.Visible;
                    ResultText.Text = $"Error: {result.Error}";
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
                QuerySubsButton.IsEnabled = true;
            }
        }

        private async Task AddSubfeatureAsync()
        {
            AddSubButton.IsEnabled = false;
            ResultBorder.Visibility = Visibility.Collapsed;

            try
            {
                if (!await App.EnsureViVeToolInitializedAsync())
                {
                    var dialog = new Dialogs.ErrorDialog("ViVeTool is still initializing. Please wait a moment and try again.");
                    dialog.XamlRoot = this.XamlRoot;
                    await dialog.ShowAsync();
                    return;
                }

                string featureId = AddFeatureIDTextBox.Text;
                string subId = AddSubIDTextBox.Text;
                string arguments = $"/addsub /id:{featureId} /subid:{subId}";

                var result = await MainWindow.ExecuteViVeToolCommandAsync(arguments);

                ResultBorder.Visibility = Visibility.Visible;

                if (result.ExitCode == 0)
                {
                    ResultText.Text = string.IsNullOrWhiteSpace(result.Output)
                        ? $"Subfeature {subId} added to Feature {featureId} successfully."
                        : result.Output;

                    var successDialog = new Dialogs.SuccessDialog($"Subfeature {subId} added successfully!");
                    successDialog.XamlRoot = this.XamlRoot;
                    await successDialog.ShowAsync();
                }
                else
                {
                    ResultText.Text = $"Error: {result.Error}";
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
                AddSubButton.IsEnabled = true;
            }
        }

        private async Task DeleteSubfeatureAsync()
        {
            DelSubButton.IsEnabled = false;
            ResultBorder.Visibility = Visibility.Collapsed;

            try
            {
                if (!await App.EnsureViVeToolInitializedAsync())
                {
                    var dialog = new Dialogs.ErrorDialog("ViVeTool is still initializing. Please wait a moment and try again.");
                    dialog.XamlRoot = this.XamlRoot;
                    await dialog.ShowAsync();
                    return;
                }

                // 确认对话框
                var confirmDialog = new Dialogs.ConfirmDialog(
                    "Delete Subfeature",
                    $"Are you sure you want to delete Subfeature {DelSubIDTextBox.Text} from Feature {DelFeatureIDTextBox.Text}?"
                );
                confirmDialog.XamlRoot = this.XamlRoot;

                var confirmResult = await confirmDialog.ShowAsync();
                if (confirmResult != ContentDialogResult.Primary)
                    return;

                string featureId = DelFeatureIDTextBox.Text;
                string subId = DelSubIDTextBox.Text;
                string arguments = $"/delsub /id:{featureId} /subid:{subId}";

                var result = await MainWindow.ExecuteViVeToolCommandAsync(arguments);

                ResultBorder.Visibility = Visibility.Visible;

                if (result.ExitCode == 0)
                {
                    ResultText.Text = string.IsNullOrWhiteSpace(result.Output)
                        ? $"Subfeature {subId} deleted from Feature {featureId} successfully."
                        : result.Output;

                    var successDialog = new Dialogs.SuccessDialog($"Subfeature {subId} deleted successfully!");
                    successDialog.XamlRoot = this.XamlRoot;
                    await successDialog.ShowAsync();
                }
                else
                {
                    ResultText.Text = $"Error: {result.Error}";
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
                DelSubButton.IsEnabled = true;
            }
        }
    }
}