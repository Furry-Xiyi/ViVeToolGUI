using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.Resources;

namespace ViVeToolGUI.Dialogs
{
    public sealed partial class ConfirmDialog : ContentDialog
    {
        public ConfirmDialog(string title, string message)
        {
            this.InitializeComponent();

            var loader = ResourceLoader.GetForViewIndependentUse();
            this.Title = title;
            this.PrimaryButtonText = loader.GetString("Dialog_Confirm");
            this.CloseButtonText = loader.GetString("Dialog_Cancel");

            ConfirmMessage.Text = message;
        }
    }
}