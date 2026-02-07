using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Windows.ApplicationModel.Resources;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ViVeToolGUI.Dialogs;
using ViVeToolGUI.Pages;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.System;

namespace ViVeToolGUI
{
    public sealed partial class MainWindow : Window
    {
        private ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        private static SUBCLASSPROC _subclassProc;
        public static MainWindow Instance { get; private set; }
        public ObservableCollection<string> BreadcrumbItems { get; } = new ObservableCollection<string>();
        private readonly ResourceLoader _loader = new ResourceLoader();
        public static SemaphoreSlim _commandLock = new(1, 1);

        public async void OpenExternalLink(object sender, RoutedEventArgs e)
        {
            var root = (ContentFrame.Content as FrameworkElement)?.XamlRoot;
            if (root == null)
                return;

            string url = "";

            if (sender is Button btn && btn.Tag is string buttonUrl)
                url = buttonUrl;
            else if (sender is HyperlinkButton link && link.Tag is string linkUrl)
                url = linkUrl;

            if (string.IsNullOrWhiteSpace(url))
                return;

            var dialog = new ExternalOpenDialog { XamlRoot = root };
            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
                await Launcher.LaunchUriAsync(new Uri(url));
        }

        public MainWindow()
        {
            this.InitializeComponent();
            Instance = this;

            if (Content is FrameworkElement root)
                root.RequestedTheme = AppThemeManager.CurrentTheme;

            this.SetTitleBar(TitleBarArea);

            ContentFrame.Navigated += ContentFrame_Navigated;

            this.Activated += MainWindow_Activated;
            this.Closed += MainWindow_Closed;

            if (Content is FrameworkElement rootEl)
                rootEl.Loaded += Root_Loaded;

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            SetMinWindowSize(hwnd, minWidth: 800, minHeight: 520);
        }

        public void StartLoadingContent()
        {
            NavView.SelectedItem = NavView.MenuItems[0];
            NavigateByTag("enableDisable");
        }

        public void ShowLoadingOverlay(string text = null)
        {
            if (CommonLoadingText != null)
                CommonLoadingText.Text = text ?? "";

            CommonLoadingOverlay.Visibility = Visibility.Visible;
        }

        public void HideLoadingOverlay()
        {
            CommonLoadingOverlay.Visibility = Visibility.Collapsed;
        }

        private static Type TagToPageType(string tag)
        {
            return tag switch
            {
                "enableDisable" => typeof(EnableDisablePage),
                "query" => typeof(QueryPage),
                "reset" => typeof(ResetPage),
                _ => null
            };
        }

        private void NavigateByTag(string tag)
        {
            var pageType = TagToPageType(tag);
            if (pageType != null && ContentFrame.CurrentSourcePageType != pageType)
                ContentFrame.Navigate(pageType);
        }

        private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.IsSettingsInvoked)
            {
                if (ContentFrame.CurrentSourcePageType != typeof(SettingsPage))
                    ContentFrame.Navigate(typeof(SettingsPage));

                return;
            }

            string tag = args.InvokedItemContainer?.Tag?.ToString();
            NavigateByTag(tag);
        }

        private void NavView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {
            if (ContentFrame.CanGoBack)
                ContentFrame.GoBack();
        }

        private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
        {
            UpdateBackButton();
            UpdateSelectedNavItem(e.SourcePageType);
            UpdateBreadcrumb(e.SourcePageType);
        }

        private void UpdateBackButton() =>
            NavView.IsBackEnabled = ContentFrame.CanGoBack;

        private void UpdateSelectedNavItem(Type pageType)
        {
            if (pageType == typeof(SettingsPage))
            {
                NavView.SelectedItem = NavView.SettingsItem;
                return;
            }

            foreach (var item in NavView.MenuItems.OfType<NavigationViewItem>())
            {
                string tag = item.Tag?.ToString();
                if (TagToPageType(tag) == pageType)
                {
                    NavView.SelectedItem = item;
                    return;
                }
            }
        }

        private void UpdateBreadcrumb(Type pageType)
        {
            BreadcrumbItems.Clear();

            if (pageType == typeof(EnableDisablePage))
                BreadcrumbItems.Add(_loader.GetString("EnableDisable_Breadcrumb"));
            else if (pageType == typeof(QueryPage))
                BreadcrumbItems.Add(_loader.GetString("Query_Breadcrumb"));
            else if (pageType == typeof(ResetPage))
                BreadcrumbItems.Add(_loader.GetString("Reset_Breadcrumb"));
            else if (pageType == typeof(SettingsPage))
                BreadcrumbItems.Add(_loader.GetString("Settings_Breadcrumb"));

            BreadcrumbPanel.Visibility = BreadcrumbItems.Count > 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            bool isActive = args.WindowActivationState != WindowActivationState.Deactivated;
            TitleBarAppName.Opacity = isActive ? 1.0 : 0.5;
        }

        public async Task FinishLoadingAndHideSplashAsync()
        {
            await Task.Delay(500);

            SplashFadeOut.Completed += (s, e) =>
            {
                SplashOverlay.Visibility = Visibility.Collapsed;

                bool sound = localSettings.Values["EnableSound"] is bool b ? b : true;
                ElementSoundPlayer.State = sound
                    ? ElementSoundPlayerState.On
                    : ElementSoundPlayerState.Off;
            };

            SplashFadeOut.Begin();
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            try
            {
                string tempDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "ViVeToolGUI",
                    "Temp");

                if (Directory.Exists(tempDir))
                {
                    foreach (var file in Directory.GetFiles(tempDir, "vivetool_*.txt"))
                    {
                        try { File.Delete(file); } catch { }
                    }

                    foreach (var file in Directory.GetFiles(tempDir, "vivetool_*.bat"))
                    {
                        try { File.Delete(file); } catch { }
                    }
                }
            }
            catch { }
        }

        public static async Task<CommandResult> ExecuteViVeToolCommandAsync(string arguments)
        {
            await _commandLock.WaitAsync();

            try
            {
                if (!await App.EnsureViVeToolInitializedAsync())
                {
                    return new CommandResult
                    {
                        ExitCode = -1,
                        Output = "",
                        Error = "ViVeTool initialization failed."
                    };
                }

                if (string.IsNullOrEmpty(App.ViVeToolPath) || !File.Exists(App.ViVeToolPath))
                {
                    Debug.WriteLine($"[ExecuteViVeTool] ERROR: ViVeTool.exe not found at: {App.ViVeToolPath}");
                    return new CommandResult
                    {
                        ExitCode = -1,
                        Output = "",
                        Error = "ViVeTool.exe not found. Please restart the application."
                    };
                }

                string vivetoolDir = Path.GetDirectoryName(App.ViVeToolPath);
                string viveDll = Path.Combine(vivetoolDir, "vive.dll");

                if (!File.Exists(viveDll))
                    Debug.WriteLine($"[ExecuteViVeTool] WARNING: vive.dll not found at: {viveDll}");

                Debug.WriteLine($"[ExecuteViVeTool] ViVeToolPath: {App.ViVeToolPath}");
                Debug.WriteLine($"[ExecuteViVeTool] Arguments: {arguments}");

                string tempDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "ViVeToolGUI",
                    "Temp");

                Directory.CreateDirectory(tempDir);

                string outputFile = Path.Combine(tempDir, $"vivetool_{Guid.NewGuid():N}.txt");
                string batchFile = Path.Combine(tempDir, $"vivetool_{Guid.NewGuid():N}.bat");

                string batchContent = $@"@echo off
cd /d ""{vivetoolDir}""
""{App.ViVeToolPath}"" {arguments} > ""{outputFile}"" 2>&1
echo EXIT_CODE=%ERRORLEVEL% >> ""{outputFile}""
";

                await File.WriteAllTextAsync(batchFile, batchContent, Encoding.UTF8);

                var psi = new ProcessStartInfo
                {
                    FileName = batchFile,
                    Verb = "runas",
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    WorkingDirectory = vivetoolDir
                };

                int exitCode = 0;

                try
                {
                    using var proc = Process.Start(psi);

                    if (proc == null)
                    {
                        try { File.Delete(batchFile); } catch { }

                        return new CommandResult
                        {
                            ExitCode = -1,
                            Output = "",
                            Error = "Failed to start elevated process."
                        };
                    }

                    await Task.Run(() =>
                    {
                        proc.WaitForExit();
                        exitCode = proc.ExitCode;
                    });
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    Debug.WriteLine($"[ExecuteViVeTool] User cancelled UAC: {ex.Message}");

                    try { File.Delete(batchFile); } catch { }

                    return new CommandResult
                    {
                        ExitCode = -1,
                        Output = "",
                        Error = "User cancelled UAC elevation."
                    };
                }

                await Task.Delay(1000);

                string output = "";

                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        if (File.Exists(outputFile))
                        {
                            output = await File.ReadAllTextAsync(outputFile, Encoding.UTF8);

                            if (output.Length > 0)
                            {
                                var exitCodeMatch = System.Text.RegularExpressions.Regex.Match(output, @"EXIT_CODE=(-?\d+)");
                                if (exitCodeMatch.Success)
                                {
                                    exitCode = int.Parse(exitCodeMatch.Groups[1].Value);
                                    output = output.Replace(exitCodeMatch.Value, "").Trim();
                                }
                            }

                            try { File.Delete(outputFile); } catch { }
                            break;
                        }

                        await Task.Delay(500);
                    }
                    catch
                    {
                        await Task.Delay(500);
                    }
                }

                try { File.Delete(batchFile); } catch { }

                return new CommandResult
                {
                    ExitCode = exitCode,
                    Output = output,
                    Error = exitCode == 0 ? "" : $"ViVeTool exited with code {exitCode}."
                };
            }
            finally
            {
                _commandLock.Release();
            }
        }

        static int _minW, _minH;

        static void SetMinWindowSize(IntPtr hwnd, int minWidth, int minHeight)
        {
            _minW = minWidth;
            _minH = minHeight;
            _subclassProc = SubclassProc;
            SetWindowSubclass(hwnd, _subclassProc, 0, 0);
        }

        static nuint SubclassProc(IntPtr hWnd, uint uMsg, nuint wParam, nint lParam,
                                   nuint uIdSubclass, nuint dwRefData)
        {
            if (uMsg == 0x0024)
            {
                double dpi = GetDpiForWindow(hWnd) / 96.0;
                var info = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                info.ptMinTrackSize.x = (int)(_minW * dpi);
                info.ptMinTrackSize.y = (int)(_minH * dpi);
                Marshal.StructureToPtr(info, lParam, true);
            }

            return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }

        delegate nuint SUBCLASSPROC(IntPtr hWnd, uint uMsg, nuint wParam, nint lParam,
                                     nuint uIdSubclass, nuint dwRefData);

        [DllImport("comctl32.dll")]
        static extern bool SetWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass,
                                              nuint uIdSubclass, nuint dwRefData);

        [DllImport("comctl32.dll")]
        static extern nuint DefSubclassProc(IntPtr hWnd, uint uMsg, nuint wParam, nint lParam);

        [DllImport("user32.dll")]
        static extern uint GetDpiForWindow(IntPtr hWnd);

        [StructLayout(LayoutKind.Sequential)]
        struct MINMAXINFO
        {
            public POINT ptReserved, ptMaxSize, ptMaxPosition, ptMinTrackSize, ptMaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct POINT
        {
            public int x, y;
        }

        private void Root_Loaded(object sender, RoutedEventArgs e)
        {
            TitleBarAppName.Text = Package.Current.DisplayName;
            ImgAppIcon.Source = new BitmapImage(Package.Current.Logo);

            ApplySettings();
            UpdateBackButton();

            if (Content is FrameworkElement root)
            {
                root.ActualThemeChanged -= AppThemeManager.OnActualThemeChanged;
                root.ActualThemeChanged += AppThemeManager.OnActualThemeChanged;
            }
        }

        public void ApplySettings()
        {
            try
            {
                string position = localSettings.Values["PanePosition"] as string ?? "Left";

                if (localSettings.Values["PanePosition"] == null)
                    localSettings.Values["PanePosition"] = "Left";

                NavView.PaneDisplayMode = position == "Top"
                    ? NavigationViewPaneDisplayMode.Top
                    : NavigationViewPaneDisplayMode.LeftCompact;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ApplySettings Error: {ex.Message}");
            }
        }
    }

    public class CommandResult
    {
        public int ExitCode { get; set; }
        public string Output { get; set; } = "";
        public string Error { get; set; } = "";
    }
}