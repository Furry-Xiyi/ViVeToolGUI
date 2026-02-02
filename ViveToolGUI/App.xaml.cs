using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
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

            // 创建窗口（尽快显示）
            var win = new MainWindow();
            MainWindowInstance = win;
            MainWindow = win;

            // 显示启动画面
            win.ShowSplashOverlay();

            // 激活窗口（立即显示）
            win.Activate();

            // 延迟应用主题和材质（不阻塞启动）
            win.DispatcherQueue.TryEnqueue(() =>
            {
                ApplyGlobalTheme();
                win.ApplyMaterial(GetSavedMaterial());
            });

            // 延迟初始化 ViVeTool（不阻塞启动）
            _ = Task.Run(InitializeViVeToolAsync);

            // 缩短启动画面时间
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
                string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                string vivetoolDir = Path.Combine(programData, "ViVeToolGUI");
                string targetExe = Path.Combine(vivetoolDir, "ViVeTool.exe");
                string flagFile = Path.Combine(vivetoolDir, "initialized.flag");

                // 如果已经初始化过 → 秒返回
                if (File.Exists(flagFile) && File.Exists(targetExe))
                {
                    ViVeToolPath = targetExe;
                    return;
                }

                // 确保目录存在
                Directory.CreateDirectory(vivetoolDir);

                // 检测架构
                string arch = RuntimeInformation.ProcessArchitecture switch
                {
                    Architecture.X64 => "x64",
                    Architecture.Arm64 => "arm64",
                    _ => "x64"
                };

                // 复制文件
                var installFolder = Windows.ApplicationModel.Package.Current.InstalledLocation;
                var assetsFolder = await installFolder.GetFolderAsync("Assets");
                var archFolder = await assetsFolder.GetFolderAsync(arch);
                var files = await archFolder.GetFilesAsync();

                var targetFolder = await StorageFolder.GetFolderFromPathAsync(vivetoolDir);

                foreach (var file in files)
                {
                    await file.CopyAsync(targetFolder, file.Name, NameCollisionOption.ReplaceExisting);
                }

                // 写入初始化标记
                File.WriteAllText(flagFile, "ok");

                ViVeToolPath = targetExe;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[InitViVeTool] EXCEPTION: {ex.Message}");
            }
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