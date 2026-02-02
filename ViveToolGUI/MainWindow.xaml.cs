using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
        public static SemaphoreSlim _commandLock = new(1, 1);

        public MainWindow()
        {
            this.InitializeComponent();
            InitializeAppWindow();
            HookMinWindowSize();

            Activated += MainWindow_Activated;
            Closed += MainWindow_Closed;

            ContentFrame.Navigate(typeof(EnableDisablePage));
            NavView.SelectedItem = NavView.MenuItems[0];
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            // 清理临时文件
            try
            {
                string tempDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "ViVeToolGUI", "Temp");

                if (Directory.Exists(tempDir))
                {
                    var files = Directory.GetFiles(tempDir, "vivetool_*.txt");
                    foreach (var file in files)
                    {
                        try { File.Delete(file); } catch { }
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// 执行管理员 ViVeTool 命令 - 所有页面通用方法
        /// </summary>
        public static async Task<CommandResult> ExecuteViVeToolCommandAsync(string arguments)
        {
            await _commandLock.WaitAsync();

            try
            {
                // 验证 ViVeTool 路径
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

                // 验证依赖文件
                string vivetoolDir = Path.GetDirectoryName(App.ViVeToolPath);
                string viveDll = Path.Combine(vivetoolDir, "vive.dll");
                if (!File.Exists(viveDll))
                {
                    Debug.WriteLine($"[ExecuteViVeTool] WARNING: vive.dll not found at: {viveDll}");
                }

                Debug.WriteLine($"[ExecuteViVeTool] ViVeToolPath: {App.ViVeToolPath}");
                Debug.WriteLine($"[ExecuteViVeTool] Arguments: {arguments}");

                // 将输出文件也放在 ProgramData 目录（管理员进程有权限）
                string tempDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "ViVeToolGUI", "Temp");
                Directory.CreateDirectory(tempDir);

                string outputFile = Path.Combine(tempDir, $"vivetool_{Guid.NewGuid():N}.txt");
                Debug.WriteLine($"[ExecuteViVeTool] Output file: {outputFile}");

                // 创建批处理脚本来执行命令（更可靠）
                string batchFile = Path.Combine(tempDir, $"vivetool_{Guid.NewGuid():N}.bat");
                string batchContent = $@"@echo off
cd /d ""{vivetoolDir}""
""{App.ViVeToolPath}"" {arguments} > ""{outputFile}"" 2>&1
echo EXIT_CODE=%ERRORLEVEL% >> ""{outputFile}""
";
                await File.WriteAllTextAsync(batchFile, batchContent, Encoding.UTF8);
                Debug.WriteLine($"[ExecuteViVeTool] Batch file created: {batchFile}");
                Debug.WriteLine($"[ExecuteViVeTool] Batch content:\n{batchContent}");

                var psi = new ProcessStartInfo
                {
                    FileName = batchFile,
                    Verb = "runas", // 请求管理员权限
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    WorkingDirectory = vivetoolDir
                };

                Debug.WriteLine($"[ExecuteViVeTool] Starting batch file with runas...");

                int exitCode = 0;

                try
                {
                    using var proc = Process.Start(psi);
                    if (proc == null)
                    {
                        Debug.WriteLine("[ExecuteViVeTool] ERROR: Process.Start returned null");
                        try { File.Delete(batchFile); } catch { }
                        return new CommandResult
                        {
                            ExitCode = -1,
                            Output = "",
                            Error = "Failed to start elevated process"
                        };
                    }

                    Debug.WriteLine($"[ExecuteViVeTool] Process started with ID: {proc.Id}");

                    await Task.Run(() =>
                    {
                        proc.WaitForExit();
                        exitCode = proc.ExitCode;
                        Debug.WriteLine($"[ExecuteViVeTool] Process exited with code: {exitCode}");
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
                        Error = "User cancelled UAC elevation"
                    };
                }

                // 等待文件写入完成
                await Task.Delay(1000);

                string output = "";

                // 读取输出文件（最多重试5次）
                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        if (File.Exists(outputFile))
                        {
                            output = await File.ReadAllTextAsync(outputFile, Encoding.UTF8);
                            Debug.WriteLine($"[ExecuteViVeTool] Output file read successfully. Length: {output.Length}");

                            if (output.Length > 0)
                            {
                                Debug.WriteLine($"[ExecuteViVeTool] Output preview (first 1000 chars):");
                                Debug.WriteLine(output.Substring(0, Math.Min(1000, output.Length)));

                                // 提取实际退出代码
                                var exitCodeMatch = System.Text.RegularExpressions.Regex.Match(output, @"EXIT_CODE=(\d+)");
                                if (exitCodeMatch.Success)
                                {
                                    exitCode = int.Parse(exitCodeMatch.Groups[1].Value);
                                    // 移除退出代码行
                                    output = output.Replace(exitCodeMatch.Value, "").Trim();
                                    Debug.WriteLine($"[ExecuteViVeTool] Actual exit code from batch: {exitCode}");
                                }
                            }

                            try { File.Delete(outputFile); } catch { }
                            break;
                        }
                        else
                        {
                            Debug.WriteLine($"[ExecuteViVeTool] Output file not found (attempt {i + 1}/5)");
                            await Task.Delay(500);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[ExecuteViVeTool] Error reading output file (attempt {i + 1}/5): {ex.Message}");
                        await Task.Delay(500);
                    }
                }

                // 清理批处理文件
                try { File.Delete(batchFile); } catch { }

                if (string.IsNullOrEmpty(output))
                {
                    Debug.WriteLine("[ExecuteViVeTool] WARNING: Output is empty!");

                    // 尝试手动检查文件是否存在
                    if (File.Exists(outputFile))
                    {
                        Debug.WriteLine($"[ExecuteViVeTool] Output file exists but couldn't read. Size: {new FileInfo(outputFile).Length}");
                    }
                    else
                    {
                        Debug.WriteLine($"[ExecuteViVeTool] Output file was never created");
                    }
                }

                return new CommandResult
                {
                    ExitCode = exitCode,
                    Output = output,
                    Error = exitCode == 0 ? "" : $"ViVeTool exited with code {exitCode}"
                };
            }
            finally
            {
                _commandLock.Release();
            }
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
            AppWindow.Title = loader.GetString("AppDisplayName");

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

        public async void HideSplashOverlay()
        {
            var visual = ElementCompositionPreview.GetElementVisual(SplashOverlay);
            var compositor = visual.Compositor;

            var fade = compositor.CreateScalarKeyFrameAnimation();
            fade.InsertKeyFrame(1f, 0f);
            fade.Duration = TimeSpan.FromMilliseconds(250);

            visual.StartAnimation("Opacity", fade);

            await Task.Delay(250);
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

    public class CommandResult
    {
        public int ExitCode { get; set; }
        public string Output { get; set; } = "";
        public string Error { get; set; } = "";
    }
}