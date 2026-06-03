using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;
using Windows.Storage;

namespace ViVeToolGUI.Dialogs
{
    public sealed partial class DisclaimerDialog : ContentDialog
    {
        private readonly ApplicationDataContainer _localSettings = ApplicationData.Current.LocalSettings;

        public DisclaimerDialog() : this(false) { }

        public DisclaimerDialog(bool isManualTrigger)
        {
            InitializeComponent();

            bool isAccepted = _localSettings.Values["DisclaimerAccepted"] as bool? ?? false;

            // 处理“不再提示”复选框显示逻辑
            if (isManualTrigger && isAccepted)
            {
                DoNotShowAgainCheck.Visibility = Visibility.Collapsed;
            }
            else
            {
                DoNotShowAgainCheck.Visibility = Visibility.Visible;
                this.PrimaryButtonClick += (s, e) =>
                {
                    if (DoNotShowAgainCheck.IsChecked == true)
                    {
                        _localSettings.Values["DisclaimerAccepted"] = true;
                    }
                };
            }

            // 处理第二按钮（退出应用）显示逻辑
            if (!isManualTrigger)
            {
                // 仅在启动时弹出才加载第二按钮文本，手动触发时保持为空以隐藏按钮
                var resourceLoader = new ResourceLoader();
                this.SecondaryButtonText = resourceLoader.GetString("Dialog_Disclaimer_SecondaryButtonText");

                this.SecondaryButtonClick += (s, e) =>
                {
                    Application.Current.Exit();
                };
            }
        }
    }
}