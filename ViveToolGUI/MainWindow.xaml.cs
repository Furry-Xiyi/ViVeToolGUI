using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Runtime.InteropServices;
using Windows.Storage;
using Windows.UI;
using WinRT.Interop;

namespace ViVeToolGUI
{
    public sealed partial class MainWindow : Window
    {
        public AppWindow AppWindow { get; private set; }
        private IntPtr _hwnd;
        private IntPtr _oldWndProc;
        private WndProcDelegate _newWndProc;

        public MainWindow()
        {
            this.InitializeComponent();
            InitializeAppWindow();
            HookMinWindowSize();

            Activated += MainWindow_Activated;

            ContentFrame.Navigate(typeof(EnableDisablePage));
            NavView.SelectedItem = NavView.MenuItems[0];
        }

        private void InitializeAppWindow()
        {
            _hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(_hwnd);
            AppWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                var titleBar = AppWindow.TitleBar;
                titleBar.ExtendsContentIntoTitleBar = true;
                titleBar.ButtonBackgroundColor = Colors.Transparent;
                titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

                SetTitleBar(AppTitleBar);
            }

            AppWindow.Resize(new Windows.Graphics.SizeInt32(1200, 800));

            var loader = new Microsoft.Windows.ApplicationModel.Resources.ResourceLoader();
            AppWindow.Title = loader.GetString("AppTitle");

            try
            {
                AppWindow.SetIcon("Assets/AppIcon.ico");
            }
            catch { }
        }

        private void HookMinWindowSize()
        {
            _newWndProc = CustomWndProc;
            _oldWndProc = SetWindowLongPtr(_hwnd, -4, Marshal.GetFunctionPointerForDelegate(_newWndProc));
        }

        private IntPtr CustomWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            const int WM_GETMINMAXINFO = 0x0024;

            if (msg == WM_GETMINMAXINFO)
            {
                var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                mmi.ptMinTrackSize.x = 800;
                mmi.ptMinTrackSize.y = 600;
                Marshal.StructureToPtr(mmi, lParam, false);
                return IntPtr.Zero;
            }

            return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
        }

        private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            bool isActive = args.WindowActivationState != WindowActivationState.Deactivated;
            AppTitle.Opacity = isActive ? 1.0 : 0.6;

            var localSettings = ApplicationData.Current.LocalSettings;
            string themeSetting = localSettings.Values["AppTheme"]?.ToString() ?? "System";

            ElementTheme theme = themeSetting switch
            {
                "Light" => ElementTheme.Light,
                "Dark" => ElementTheme.Dark,
                _ => GetSystemTheme()
            };

            bool followSystem = themeSetting == "System";
            UpdateTitleBarColors(theme, followSystem, isActive);
        }

        private ElementTheme GetSystemTheme()
        {
            return Application.Current.RequestedTheme == ApplicationTheme.Dark
                ? ElementTheme.Dark
                : ElementTheme.Light;
        }

        public void UpdateTitleBarColors(ElementTheme theme, bool followSystem, bool isActive)
        {
            if (!AppWindowTitleBar.IsCustomizationSupported())
                return;

            var titleBar = AppWindow.TitleBar;

            if (followSystem)
            {
                theme = GetSystemTheme();
            }

            if (theme == ElementTheme.Dark)
            {
                titleBar.ButtonForegroundColor = isActive ? Colors.White : Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF);
                titleBar.ButtonHoverForegroundColor = Colors.White;
                titleBar.ButtonPressedForegroundColor = Colors.White;
                titleBar.ButtonBackgroundColor = Colors.Transparent;
                titleBar.ButtonHoverBackgroundColor = Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF);
                titleBar.ButtonPressedBackgroundColor = Color.FromArgb(0x50, 0xFF, 0xFF, 0xFF);
            }
            else
            {
                titleBar.ButtonForegroundColor = isActive ? Colors.Black : Color.FromArgb(0x99, 0x00, 0x00, 0x00);
                titleBar.ButtonHoverForegroundColor = Colors.Black;
                titleBar.ButtonPressedForegroundColor = Colors.Black;
                titleBar.ButtonBackgroundColor = Colors.Transparent;
                titleBar.ButtonHoverBackgroundColor = Color.FromArgb(0x30, 0x00, 0x00, 0x00);
                titleBar.ButtonPressedBackgroundColor = Color.FromArgb(0x50, 0x00, 0x00, 0x00);
            }
        }

        public void ApplyMaterial(string material)
        {
            SystemBackdrop = material switch
            {
                "Acrylic" => new DesktopAcrylicBackdrop(),
                "Mica" => new MicaBackdrop { Kind = MicaKind.Base },
                _ => new MicaBackdrop { Kind = MicaKind.BaseAlt }
            };

            var localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values["AppMaterial"] = material;
        }

        public void ShowSplashOverlay()
        {
            SplashOverlay.Visibility = Visibility.Visible;
        }

        public void HideSplashOverlay()
        {
            SplashOverlay.Visibility = Visibility.Collapsed;
        }

        public void BringToFront()
        {
            if (AppWindow != null && AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.Restore();
                AppWindow.Show();
            }
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                ContentFrame.Navigate(typeof(SettingsPage));
            }
            else
            {
                var selectedItem = args.SelectedItem as NavigationViewItem;
                if (selectedItem != null)
                {
                    string tag = selectedItem.Tag?.ToString();
                    switch (tag)
                    {
                        case "EnableDisable":
                            ContentFrame.Navigate(typeof(EnableDisablePage));
                            break;
                        case "Query":
                            ContentFrame.Navigate(typeof(QueryPage));
                            break;
                        case "Reset":
                            ContentFrame.Navigate(typeof(ResetPage));
                            break;
                    }
                }
            }
        }

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr newProc);

        [DllImport("user32.dll")]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int x; public int y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }
    }
}