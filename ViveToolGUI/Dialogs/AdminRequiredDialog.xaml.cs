using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.Resources;

namespace ViVeToolGUI.Dialogs
{
    public sealed partial class AdminRequiredDialog : ContentDialog
    {
        public AdminRequiredDialog()
        {
            this.InitializeComponent();

            var loader = ResourceLoader.GetForViewIndependentUse();
            Title = loader.GetString("Dialog_AdminRequiredTitle");
            CloseButtonText = loader.GetString("Dialog_Close");
        }
    }
}