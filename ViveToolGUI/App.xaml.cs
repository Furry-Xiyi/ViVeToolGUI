using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading.Tasks;
using Windows.Storage;

namespace ViVeToolGUI
{
    public partial class App : Application
    {
        public static Window MainWindow { get; private set; }
        public static MainWindow MainWindowInstance { get; private set; }
        public static string ViVeToolPath { get; private set; }

        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            // 单实例检查
            var mainInstance = AppInstance.FindOrRegisterForKey("ViVeToolGUI_MAIN");
            var activationArgs = AppInstance.GetCurrent().GetActivatedEventArgs();

            if (!mainInstance.IsCurrent)
            {
                mainInstance.RedirectActivationToAsync(activationArgs).AsTask().Wait();
                Environment.Exit(0);
                return;
            }

            mainInstance.Activated += OnActivated;

            // 初始化 ViVeTool
            _ = InitializeViVeToolAsync();

            // 创建窗口
            var win = new MainWindow();
            MainWindowInstance = win;
            MainWindow = win;

            // 应用主题
            ApplyGlobalTheme();

            // 应用材质
            win.ApplyMaterial(GetSavedMaterial());

            // 显示启动画面
            win.ShowSplashOverlay();

            // 激活窗口
            win.Activate();

            // 异步初始化
            _ = InitializeAppAsync();
        }

        private void OnActivated(object sender, AppActivationArguments e)
        {
            MainWindowInstance?.DispatcherQueue.TryEnqueue(() =>
            {
                MainWindowInstance.BringToFront();
            });
        }

        private async Task InitializeAppAsync()
        {
            await Task.Delay(1500);
            MainWindowInstance?.HideSplashOverlay();
        }

        private async Task InitializeViVeToolAsync()
        {
            try
            {
                var localFolder = ApplicationData.Current.LocalFolder;
                var vivetoolFolder = await localFolder.CreateFolderAsync("ViVeTool", CreationCollisionOption.OpenIfExists);

                // 检测架构
                string arch = RuntimeInformation.ProcessArchitecture switch
                {
                    Architecture.X64 => "x64",
                    Architecture.Arm64 => "arm64",
                    _ => "x64"
                };

                var vivetoolExe = await vivetoolFolder.TryGetItemAsync("ViVeTool.exe") as StorageFile;

                if (vivetoolExe == null)
                {
                    // 复制文件
                    var installFolder = Windows.ApplicationModel.Package.Current.InstalledLocation;
                    var assetsFolder = await installFolder.GetFolderAsync("Assets");
                    var archFolder = await assetsFolder.GetFolderAsync(arch);

                    var files = await archFolder.GetFilesAsync();
                    foreach (var file in files)
                    {
                        await file.CopyAsync(vivetoolFolder, file.Name, NameCollisionOption.ReplaceExisting);
                    }
                }

                ViVeToolPath = Path.Combine(vivetoolFolder.Path, "ViVeTool.exe");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to initialize ViVeTool: {ex.Message}");
            }
        }

        private bool IsAdministrator()
        {
            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        

        private void ShowAdminRequiredDialogAndExit()
        {
            // 创建一个临时窗口用于显示对话框
            var tempWindow = new Window
            {
                Title = "ViVeTool GUI"
            };

            var rootGrid = new Grid();
            tempWindow.Content = rootGrid;
            tempWindow.Activate();

            // 等待窗口完全激活
            tempWindow.DispatcherQueue.TryEnqueue(async () =>
            {
                await Task.Delay(100); // 短暂延迟确保窗口已准备好

                var dialog = new Dialogs.AdminRequiredDialog
                {
                    XamlRoot = rootGrid.XamlRoot
                };

                await dialog.ShowAsync();

                // 用户关闭对话框后退出应用
                Environment.Exit(1);
            });
        }

        public static void ApplyGlobalTheme()
        {
            string themeSetting = GetSavedTheme();

            ElementTheme theme = themeSetting switch
            {
                "Light" => ElementTheme.Light,
                "Dark" => ElementTheme.Dark,
                _ => ElementTheme.Default
            };

            if (MainWindowInstance?.Content is FrameworkElement root)
            {
                root.RequestedTheme = theme;

                bool followSystem = themeSetting == "System";
                bool isActive = true;

                MainWindowInstance.UpdateTitleBarColors(theme, followSystem, isActive);
            }
        }

        private static string GetSavedTheme()
        {
            try
            {
                var localSettings = ApplicationData.Current.LocalSettings;
                return localSettings.Values["AppTheme"]?.ToString() ?? "System";
            }
            catch
            {
                return "System";
            }
        }

        private static string GetSavedMaterial()
        {
            try
            {
                var localSettings = ApplicationData.Current.LocalSettings;
                return localSettings.Values["AppMaterial"]?.ToString() ?? "MicaAlt";
            }
            catch
            {
                return "MicaAlt";
            }
        }
    }
}