using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.Resources;

namespace ViVeToolGUI.Dialogs
{
    public sealed partial class ExportOptionsDialog : ContentDialog
    {
        public bool ExportTxt => OptionTxt.IsChecked == true;
        public bool ExportCsv => OptionCsv.IsChecked == true;
        public bool ExportJson => OptionJson.IsChecked == true;

        public ExportOptionsDialog()
        {
            InitializeComponent();

            var loader = ResourceLoader.GetForViewIndependentUse();
            this.Title = loader.GetString("Dialog_ExportOptions/Title");
            this.PrimaryButtonText = loader.GetString("Dialog_ExportOptions/PrimaryButtonText");
            this.CloseButtonText = loader.GetString("Dialog_ExportOptions/CloseButtonText");
        }
    }
}