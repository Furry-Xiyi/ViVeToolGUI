using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.Resources;

namespace ViVeToolGUI.Dialogs
{
    public sealed partial class ErrorDialog : ContentDialog
    {
        public ErrorDialog(string message)
        {
            this.InitializeComponent();

            var loader = ResourceLoader.GetForViewIndependentUse();
            this.Title = loader.GetString("Dialog_ErrorTitle");
            this.CloseButtonText = loader.GetString("Dialog_Close");

            ErrorMessage.Text = message;
        }
    }
}