using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.Resources;
using System;

namespace ViVeToolGUI.Dialogs
{
    public sealed partial class SuccessDialog : ContentDialog
    {
        private readonly string? _path;

        public SuccessDialog(string message, string? path = null)
        {
            InitializeComponent();

            var loader = ResourceLoader.GetForViewIndependentUse();
            this.Title = loader.GetString("Dialog_Success/Title");
            this.CloseButtonText = loader.GetString("Dialog_Success/CloseButtonText");

            SuccessMessage.Text = message;
            _path = path;

            if (!string.IsNullOrEmpty(path))
            {
                PathLink.Content = path;
                PathLink.Visibility = Visibility.Visible;
            }
        }

        private async void PathLink_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_path))
                return;

            try
            {
                var folder = await Windows.Storage.StorageFolder.GetFolderFromPathAsync(_path);
                await Windows.System.Launcher.LaunchFolderAsync(folder);
            }
            catch
            {
                try
                {
                    await Windows.System.Launcher.LaunchUriAsync(new Uri(_path));
                }
                catch { }
            }
        }
    }
}