using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Resources;
using Windows.Storage;

namespace ViVeToolGUI
{
    public sealed partial class SettingsPage : Page
    {
        private readonly ResourceLoader _resourceLoader;
        private readonly ApplicationDataContainer _localSettings;

        public SettingsPage()
        {
            this.InitializeComponent();
            _resourceLoader = ResourceLoader.GetForViewIndependentUse();
            _localSettings = ApplicationData.Current.LocalSettings;

            LoadSettings();
            LoadAppInfo();
        }

        private void LoadSettings()
        {
            var theme = _localSettings.Values["AppTheme"]?.ToString() ?? "System";
            switch (theme)
            {
                case "Light":
                    ThemeLight.IsChecked = true;
                    break;
                case "Dark":
                    ThemeDark.IsChecked = true;
                    break;
                default:
                    ThemeSystem.IsChecked = true;
                    break;
            }

            var material = _localSettings.Values["AppMaterial"]?.ToString() ?? "MicaAlt";
            switch (material)
            {
                case "Acrylic":
                    MaterialAcrylic.IsChecked = true;
                    break;
                case "Mica":
                    MaterialMica.IsChecked = true;
                    break;
                default:
                    MaterialMicaAlt.IsChecked = true;
                    break;
            }
        }

        private void LoadAppInfo()
        {
            try
            {
                var package = Package.Current;
                var version = package.Id.Version;

                AppTitle.Text = _resourceLoader.GetString("AppDisplayName");
                AppPublisher.Text = package.PublisherDisplayName;
                AppVersion.Text = $"Version {version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
            }
            catch
            {
                AppTitle.Text = "ViVeTool GUI";
                AppPublisher.Text = "Developer";
                AppVersion.Text = "Version 1.0.0.0";
            }
        }

        private void Theme_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.IsChecked == true)
            {
                string themeName = rb == ThemeLight ? "Light" : rb == ThemeDark ? "Dark" : "System";
                _localSettings.Values["AppTheme"] = themeName;
                App.ApplyGlobalTheme();
            }
        }

        private void Material_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.IsChecked == true)
            {
                string materialName = rb == MaterialAcrylic ? "Acrylic" : rb == MaterialMica ? "Mica" : "MicaAlt";

                if (App.MainWindowInstance is MainWindow mainWindow)
                {
                    mainWindow.ApplyMaterial(materialName);
                }
            }
        }
    }
}