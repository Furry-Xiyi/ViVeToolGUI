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
        private static bool _vivetoolInitialized = false;

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

            _ = InitializeViVeToolAsync();

            // 创建窗口
            var win = new MainWindow();
            MainWindowInstance = win;
            MainWindow = win;

            // 应用主题
            ApplyGlobalTheme();

            // 应用材质
            win.ApplyMaterial(GetSavedMaterial());


            win.ShowSplashOverlay();

            // 激活窗口
            win.Activate();
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
            if (_vivetoolInitialized) return;

            try
            {
                Debug.WriteLine("[InitViVeTool] Starting initialization...");

                string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                string vivetoolDir = Path.Combine(programData, "ViVeToolGUI");

                Directory.CreateDirectory(vivetoolDir);

                string arch = RuntimeInformation.ProcessArchitecture switch
                {
                    Architecture.X64 => "x64",
                    Architecture.Arm64 => "arm64",
                    _ => "x64"
                };

                string targetExe = Path.Combine(vivetoolDir, "ViVeTool.exe");

                // 检查是否需要复制
                if (!File.Exists(targetExe))
                {
                    Debug.WriteLine($"[InitViVeTool] Copying files from Assets/{arch}...");

                    var installFolder = Windows.ApplicationModel.Package.Current.InstalledLocation;
                    var assetsFolder = await installFolder.GetFolderAsync("Assets");
                    var archFolder = await assetsFolder.GetFolderAsync(arch);
                    var files = await archFolder.GetFilesAsync();
                    var targetFolder = await StorageFolder.GetFolderFromPathAsync(vivetoolDir);

                    foreach (var file in files)
                    {
                        await file.CopyAsync(targetFolder, file.Name, NameCollisionOption.ReplaceExisting);
                    }
                }

                ViVeToolPath = targetExe;
                _vivetoolInitialized = true;

                Debug.WriteLine($"[InitViVeTool] SUCCESS! Path: {ViVeToolPath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[InitViVeTool] ERROR: {ex.Message}");
            }
        }

        // 🔥 添加等待初始化完成的方法
        public static async Task<bool> EnsureViVeToolInitializedAsync()
        {
            if (_vivetoolInitialized) return true;

            // 最多等待5秒
            for (int i = 0; i < 50; i++)
            {
                if (_vivetoolInitialized) return true;
                await Task.Delay(100);
            }

            return _vivetoolInitialized;
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