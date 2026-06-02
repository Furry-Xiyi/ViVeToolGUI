using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;
using Windows.Storage;

namespace ViVeToolGUI.Dialogs
{
    public sealed partial class DisclaimerDialog : ContentDialog
    {
        private readonly ApplicationDataContainer _localSettings = ApplicationData.Current.LocalSettings;

        public DisclaimerDialog()
        {
            InitializeComponent();

            this.PrimaryButtonClick += (s, e) =>
            {
                if (DoNotShowAgainCheck.IsChecked == true)
                    _localSettings.Values["DisclaimerAccepted"] = true;
            };
        }
    }
}