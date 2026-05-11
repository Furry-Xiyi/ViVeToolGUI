using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using Windows.Graphics;
using Windows.Storage;
using Windows.UI;
using Microsoft.UI.Dispatching;

namespace ViVeToolGUI.AppWindows
{
    public sealed partial class TextFileViewerWindow : Window
    {
        private AppWindow _appWindow;
        private List<(int Start, int Length)> _matches = new();
        private int _currentMatchIndex = -1;
        private string _fullText = "";
        private DispatcherQueueTimer _searchTimer;

        public TextFileViewerWindow(string uriString)
        {
            this.InitializeComponent();

            _searchTimer = DispatcherQueue.CreateTimer();
            _searchTimer.Interval = TimeSpan.FromMilliseconds(300);
            _searchTimer.Tick += (s, e) => { _searchTimer.Stop(); PerformSearch(); };

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            _appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(hwnd));
            _appWindow.TitleBar.ExtendsContentIntoTitleBar = true;

            // 设置系统按钮背景为透明以匹配标题栏样式
            _appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            _appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

            LoadText(uriString);
        }

        private void RootGrid_Loaded(object sender, RoutedEventArgs e) => UpdateDragRects();

        private void UpdateDragRects()
        {
            if (_appWindow == null || Content == null) return;

            // 使用 Content.XamlRoot 确保获取到有效的缩放比例
            var scale = Content.XamlRoot.RasterizationScale;
            var transform = ControlContainer.TransformToVisual(null);
            var rect = transform.TransformBounds(new Windows.Foundation.Rect(0, 0, ControlContainer.ActualWidth, ControlContainer.ActualHeight));

            var dragRect = new RectInt32(
                (int)(rect.X * scale), (int)(rect.Y * scale),
                (int)(rect.Width * scale), (int)(rect.Height * scale));

            _appWindow.TitleBar.SetDragRectangles(new[] { dragRect });
        }

        private async void LoadText(string uriString)
        {
            var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri(uriString));
            _fullText = await FileIO.ReadTextAsync(file);
            ContentRichEdit.IsReadOnly = false;
            ContentRichEdit.Document.SetText(Microsoft.UI.Text.TextSetOptions.None, _fullText);
            ContentRichEdit.IsReadOnly = true;
        }

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e) { ShowControlsStoryboard.Begin(); }
        private void SearchBox_LostFocus(object sender, RoutedEventArgs e) { if (string.IsNullOrEmpty(SearchBox.Text)) HideControlsStoryboard.Begin(); }
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) { _searchTimer.Stop(); _searchTimer.Start(); }

        private void PerformSearch()
        {
            ContentRichEdit.IsReadOnly = false;
            var doc = ContentRichEdit.Document;
            doc.Selection.SetRange(0, _fullText.Length);
            doc.Selection.CharacterFormat.BackgroundColor = Colors.Transparent;
            doc.Selection.CharacterFormat.ForegroundColor = Colors.Black;

            _matches.Clear();
            var query = SearchBox.Text;
            if (!string.IsNullOrEmpty(query))
            {
                var comparison = MatchCaseToggle.IsChecked == true ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                int index = 0;
                while ((index = _fullText.IndexOf(query, index, comparison)) != -1)
                {
                    _matches.Add((index, query.Length));
                    var range = doc.GetRange(index, index + query.Length);
                    range.CharacterFormat.BackgroundColor = (Color)Application.Current.Resources["SystemAccentColor"];
                    range.CharacterFormat.ForegroundColor = Colors.White;
                    index += query.Length;
                }
            }
            ContentRichEdit.IsReadOnly = true;
        }

        private void MatchCaseToggle_Click(object sender, RoutedEventArgs e) => PerformSearch();
        private void PrevButton_Click(object sender, RoutedEventArgs e) { if (_matches.Count > 0) { _currentMatchIndex = (_currentMatchIndex - 1 + _matches.Count) % _matches.Count; NavigateToCurrentMatch(); } }
        private void NextButton_Click(object sender, RoutedEventArgs e) { if (_matches.Count > 0) { _currentMatchIndex = (_currentMatchIndex + 1) % _matches.Count; NavigateToCurrentMatch(); } }

        private void NavigateToCurrentMatch()
        {
            var match = _matches[_currentMatchIndex];
            ContentRichEdit.Document.Selection.SetRange(match.Start, match.Start + match.Length);
            ContentRichEdit.Document.Selection.ScrollIntoView(Microsoft.UI.Text.PointOptions.None);
        }
    }
}