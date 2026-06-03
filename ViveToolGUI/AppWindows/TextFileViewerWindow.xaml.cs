using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.ApplicationModel.Resources;
using System;
using System.Collections.Generic;
using Windows.Graphics;
using Windows.Storage;
using Windows.UI;

namespace ViVeToolGUI.AppWindows
{
    public sealed partial class TextFileViewerWindow : Window
    {
        private static TextFileViewerWindow _instance;
        private AppWindow _appWindow;
        private List<(int Start, int Length)> _matches = new();
        private int _currentMatchIndex = -1;
        private string _docText = "";
        private DispatcherQueueTimer _searchTimer;
        private bool _controlsExpanded = false;

        public static void ShowOrActivate(string uriString)
        {
            if (_instance != null)
            {
                _instance._appWindow.Show(true);
                return;
            }
            _instance = new TextFileViewerWindow(uriString);
            _instance.Activate();
        }

        public TextFileViewerWindow(string uriString)
        {
            this.InitializeComponent();

            _searchTimer = DispatcherQueue.CreateTimer();
            _searchTimer.Interval = TimeSpan.FromMilliseconds(300);
            _searchTimer.Tick += (s, e) => { _searchTimer.Stop(); PerformSearch(); };

            _appWindow = this.AppWindow;
            _appWindow.SetIcon("Assets\\AppIcon.ico");
            _appWindow.Title = new ResourceLoader().GetString("TextFileViewerWindowTitle");
            _appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            _appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
            _appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            _appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

            var presenter = OverlappedPresenter.Create();
            presenter.PreferredMinimumWidth = 800;
            presenter.PreferredMinimumHeight = 500;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            _appWindow.SetPresenter(presenter);

            this.SystemBackdrop = AppThemeManager.CurrentMaterial switch
            {
                BackgroundMaterial.MicaAlt => new MicaBackdrop { Kind = MicaKind.BaseAlt },
                BackgroundMaterial.Acrylic => new DesktopAcrylicBackdrop(),
                _ => new MicaBackdrop { Kind = MicaKind.Base }
            };

            _appWindow.Changed += AppWindow_Changed;
            this.Closed += TextFileViewerWindow_Closed; // 新增生命周期接管

            LoadText(uriString);
        }

        // 处理窗口销毁时的安全清理
        private void TextFileViewerWindow_Closed(object sender, WindowEventArgs args)
        {
            _instance = null;

            // 1. 取消订阅尺寸变化事件，防止关闭瞬间继续计算布局引发异常
            if (_appWindow != null)
            {
                _appWindow.Changed -= AppWindow_Changed;
            }

            // 2. 停掉所有运行中的 DispatcherTimer，防止野指针
            if (_searchTimer != null)
            {
                _searchTimer.Stop();
            }

            // 3. 释放材质背景，防止 WinUI 3 底层回收崩溃
            this.SystemBackdrop = null;
        }

        private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if (args.DidSizeChange)
                UpdateDragRects();
        }

        private void RootGrid_Loaded(object sender, RoutedEventArgs e)
        {
            if (Content is FrameworkElement root)
                root.RequestedTheme = AppThemeManager.CurrentTheme;

            bool isDark = AppThemeManager.GetIsDarkTheme();
            var fg = isDark ? Colors.White : Colors.Black;
            var inactiveFg = isDark
                ? Color.FromArgb(255, 128, 128, 128)
                : Color.FromArgb(255, 160, 160, 160);
            var hoverBg = isDark
                ? Color.FromArgb(20, 255, 255, 255)
                : Color.FromArgb(20, 0, 0, 0);

            _appWindow.TitleBar.ButtonForegroundColor = fg;
            _appWindow.TitleBar.ButtonInactiveForegroundColor = inactiveFg;
            _appWindow.TitleBar.ButtonHoverBackgroundColor = hoverBg;
            _appWindow.TitleBar.ButtonHoverForegroundColor = fg;
            _appWindow.TitleBar.ButtonPressedBackgroundColor = Color.FromArgb(30, hoverBg.R, hoverBg.G, hoverBg.B);
            _appWindow.TitleBar.ButtonPressedForegroundColor = fg;

            UpdateDragRects();
        }

        private void UpdateDragRects()
        {
            // 加上 IsLoaded 判断，防止销毁阶段进入
            if (_appWindow == null || Content?.XamlRoot == null || SearchBox == null || !SearchBox.IsLoaded) return;

            try
            {
                var scale = Content.XamlRoot.RasterizationScale;
                var titleBarHeight = (int)(_appWindow.TitleBar.Height * scale);
                var windowWidth = _appWindow.Size.Width;

                var rects = new List<RectInt32>();

                int exX, exRight;

                if (_controlsExpanded)
                {
                    var searchTransform = SearchBox.TransformToVisual(null);
                    var searchBounds = searchTransform.TransformBounds(
                        new Windows.Foundation.Rect(0, 0, SearchBox.ActualWidth, SearchBox.ActualHeight));

                    double left = searchBounds.X - 40 - 8;
                    double right = searchBounds.X + searchBounds.Width + 8 + 32 + 4 + 32;

                    exX = (int)(left * scale);
                    exRight = (int)(right * scale);
                }
                else
                {
                    var transform = SearchBox.TransformToVisual(null);
                    var bounds = transform.TransformBounds(
                        new Windows.Foundation.Rect(0, 0, SearchBox.ActualWidth, SearchBox.ActualHeight));

                    exX = (int)(bounds.X * scale);
                    exRight = exX + (int)(bounds.Width * scale);
                }

                if (exX > 0)
                    rects.Add(new RectInt32(0, 0, exX, titleBarHeight));
                if (exRight < windowWidth)
                    rects.Add(new RectInt32(exRight, 0, windowWidth - exRight, titleBarHeight));

                if (rects.Count > 0)
                    _appWindow.TitleBar.SetDragRectangles(rects.ToArray());
            }
            catch
            {
                // WinUI 3 视觉树销毁期间调用 TransformToVisual 极易抛出异常，这里通过 catch 吃掉以保主进程存活
            }
        }

        private async void LoadText(string uriString)
        {
            var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri(uriString));
            var rawText = await FileIO.ReadTextAsync(file);
            ContentRichEdit.IsReadOnly = false;
            ContentRichEdit.Document.SetText(Microsoft.UI.Text.TextSetOptions.None, rawText);
            ContentRichEdit.IsReadOnly = true;
            ContentRichEdit.Document.GetText(Microsoft.UI.Text.TextGetOptions.UseCrlf, out _docText);
        }

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            _controlsExpanded = true;
            MatchCaseToggle.IsHitTestVisible = true;
            NavButtonsPanel.IsHitTestVisible = true;
            ShowControlsStoryboard.Completed -= Storyboard_Completed;
            ShowControlsStoryboard.Completed += Storyboard_Completed;
            ShowControlsStoryboard.Begin();
        }

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var focused = FocusManager.GetFocusedElement(Content.XamlRoot);
            if (focused == MatchCaseToggle || focused == PrevButton || focused == NextButton)
                return;

            if (string.IsNullOrEmpty(SearchBox.Text))
            {
                _controlsExpanded = false;
                MatchCaseToggle.IsHitTestVisible = false;
                NavButtonsPanel.IsHitTestVisible = false;
                HideControlsStoryboard.Completed -= Storyboard_Completed;
                HideControlsStoryboard.Completed += Storyboard_Completed;
                HideControlsStoryboard.Begin();
            }
        }

        private void Storyboard_Completed(object sender, object e)
        {
            UpdateDragRects();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchTimer.Stop();
            _searchTimer.Start();
        }

        private void PerformSearch()
        {
            _matches.Clear();
            _currentMatchIndex = -1;

            ContentRichEdit.IsReadOnly = false;
            var doc = ContentRichEdit.Document;

            doc.GetText(Microsoft.UI.Text.TextGetOptions.UseCrlf, out _docText);

            var wholeRange = doc.GetRange(0, Microsoft.UI.Text.TextConstants.MaxUnitCount);
            wholeRange.CharacterFormat.BackgroundColor = Colors.Transparent;
            wholeRange.CharacterFormat.ForegroundColor =
                AppThemeManager.GetIsDarkTheme() ? Colors.White : Colors.Black;

            var query = SearchBox.Text;
            if (!string.IsNullOrEmpty(query))
            {
                var comparison = MatchCaseToggle.IsChecked == true
                    ? StringComparison.Ordinal
                    : StringComparison.OrdinalIgnoreCase;

                int searchStart = 0;
                while ((searchStart = _docText.IndexOf(query, searchStart, comparison)) != -1)
                {
                    int docStart = ConvertToDocIndex(searchStart);
                    int docEnd = ConvertToDocIndex(searchStart + query.Length);
                    _matches.Add((docStart, docEnd - docStart));

                    var range = doc.GetRange(docStart, docEnd);
                    range.CharacterFormat.BackgroundColor =
                        (Color)Application.Current.Resources["SystemAccentColor"];
                    range.CharacterFormat.ForegroundColor = Colors.White;
                    searchStart += query.Length;
                }

                if (_matches.Count > 0)
                {
                    _currentMatchIndex = 0;
                    HighlightCurrentMatch();
                }
            }
            ContentRichEdit.IsReadOnly = true;
        }

        private int ConvertToDocIndex(int crlfIndex)
        {
            int crCount = 0;
            for (int i = 0; i < crlfIndex && i < _docText.Length; i++)
            {
                if (_docText[i] == '\r' && i + 1 < _docText.Length && _docText[i + 1] == '\n')
                    crCount++;
            }
            return crlfIndex - crCount;
        }

        private void HighlightCurrentMatch()
        {
            var match = _matches[_currentMatchIndex];
            var doc = ContentRichEdit.Document;
            doc.Selection.SetRange(match.Start, match.Start + match.Length);
            doc.Selection.ScrollIntoView(Microsoft.UI.Text.PointOptions.Start);
        }

        private void MatchCaseToggle_Click(object sender, RoutedEventArgs e) => PerformSearch();

        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            if (_matches.Count == 0) return;
            _currentMatchIndex = (_currentMatchIndex - 1 + _matches.Count) % _matches.Count;
            HighlightCurrentMatch();
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (_matches.Count == 0) return;
            _currentMatchIndex = (_currentMatchIndex + 1) % _matches.Count;
            HighlightCurrentMatch();
        }
    }
}