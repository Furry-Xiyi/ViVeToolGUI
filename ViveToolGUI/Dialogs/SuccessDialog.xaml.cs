using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.Resources;

namespace ViVeToolGUI.Dialogs
{
    public sealed partial class SuccessDialog : ContentDialog
    {
        public SuccessDialog(string message)
        {
            this.InitializeComponent();

            var loader = ResourceLoader.GetForViewIndependentUse();
            this.Title = loader.GetString("Dialog_SuccessTitle");
            this.CloseButtonText = loader.GetString("Dialog_Close");

            SuccessMessage.Text = message;
        }
    }
}