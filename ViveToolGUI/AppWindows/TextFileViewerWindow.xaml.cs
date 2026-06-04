using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.ApplicationModel.Resources;
using System.ComponentModel;
using Microsoft.UI.Xaml.Documents;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Windows.Globalization;
using Windows.Graphics;
using Windows.System;
using Windows.System.UserProfile;
using Windows.UI;
using Windows.Foundation;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace ViVeToolGUI.AppWindows
{
    public sealed partial class FeatureEntry : INotifyPropertyChanged
    {
        public string Id { get; set; } = "";
        public string Variant { get; set; } = "";
        public string Description { get; set; } = "";


        private string _query = "";
        public string Query { get => _query; set { _query = value; OnChanged(nameof(Query)); } }

        private bool _caseSensitive;
        public bool CaseSensitive { get => _caseSensitive; set { _caseSensitive = value; OnChanged(nameof(CaseSensitive)); } }

        private int _currentField;
        public int CurrentField
        {
            get => _currentField;
            set
            {
                if (_currentField == value) return;
                _currentField = value;
                OnChanged(nameof(IdIsCurrent));
                OnChanged(nameof(VariantIsCurrent));
                OnChanged(nameof(DescIsCurrent));
            }
        }

        private int _currentMatchPos = -1;
        public int CurrentMatchPos
        {
            get => _currentMatchPos;
            set { if (_currentMatchPos == value) return; _currentMatchPos = value; OnChanged(nameof(CurrentMatchPos)); }
        }

        public bool IdIsCurrent => _currentField == 1;
        public bool VariantIsCurrent => _currentField == 2;
        public bool DescIsCurrent => _currentField == 3;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    internal static class SearchHighlight
    {
        public static readonly DependencyProperty SourceTextProperty =
            DependencyProperty.RegisterAttached("SourceText", typeof(string), typeof(SearchHighlight), new PropertyMetadata(null, OnPropChanged));
        public static string GetSourceText(DependencyObject o) => (string)o.GetValue(SourceTextProperty);
        public static void SetSourceText(DependencyObject o, string v) => o.SetValue(SourceTextProperty, v);

        public static readonly DependencyProperty QueryProperty =
            DependencyProperty.RegisterAttached("Query", typeof(string), typeof(SearchHighlight), new PropertyMetadata("", OnPropChanged));
        public static string GetQuery(DependencyObject o) => (string)o.GetValue(QueryProperty);
        public static void SetQuery(DependencyObject o, string v) => o.SetValue(QueryProperty, v);

        public static readonly DependencyProperty CaseSensitiveProperty =
            DependencyProperty.RegisterAttached("CaseSensitive", typeof(bool), typeof(SearchHighlight), new PropertyMetadata(false, OnPropChanged));
        public static bool GetCaseSensitive(DependencyObject o) => (bool)o.GetValue(CaseSensitiveProperty);
        public static void SetCaseSensitive(DependencyObject o, bool v) => o.SetValue(CaseSensitiveProperty, v);

        public static readonly DependencyProperty IsCurrentFieldProperty =
            DependencyProperty.RegisterAttached("IsCurrentField", typeof(bool), typeof(SearchHighlight), new PropertyMetadata(false, OnPropChanged));
        public static bool GetIsCurrentField(DependencyObject o) => (bool)o.GetValue(IsCurrentFieldProperty);
        public static void SetIsCurrentField(DependencyObject o, bool v) => o.SetValue(IsCurrentFieldProperty, v);

        public static readonly DependencyProperty CurrentMatchPosProperty =
            DependencyProperty.RegisterAttached("CurrentMatchPos", typeof(int), typeof(SearchHighlight), new PropertyMetadata(-1, OnPropChanged));
        public static int GetCurrentMatchPos(DependencyObject o) => (int)o.GetValue(CurrentMatchPosProperty);
        public static void SetCurrentMatchPos(DependencyObject o, int v) => o.SetValue(CurrentMatchPosProperty, v);

        private static void OnPropChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBlock tb) Apply(tb);
        }

        private static void Apply(TextBlock tb)
        {
            string text = GetSourceText(tb) ?? "";
            tb.Text = text;
            tb.TextHighlighters.Clear();

            string query = GetQuery(tb);
            if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(text)) return;

            var cmp = GetCaseSensitive(tb) ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            bool isCurrentField = GetIsCurrentField(tb);
            int currentPos = GetCurrentMatchPos(tb);

            var defaultHl = new TextHighlighter
            {
                Background = new SolidColorBrush((Color)Application.Current.Resources["SystemAccentColor"]),
                Foreground = new SolidColorBrush(Colors.White)
            };
            var currentHl = new TextHighlighter
            {
                Background = new SolidColorBrush(Color.FromArgb(255, 255, 185, 0)),
                Foreground = new SolidColorBrush(Colors.Black)
            };

            int pos = 0;
            while ((pos = text.IndexOf(query, pos, cmp)) >= 0)
            {
                if (isCurrentField && pos == currentPos)
                    currentHl.Ranges.Add(new TextRange(pos, query.Length));
                else
                    defaultHl.Ranges.Add(new TextRange(pos, query.Length));
                pos += query.Length;
            }
            if (defaultHl.Ranges.Count > 0)
                tb.TextHighlighters.Add(defaultHl);
            if (currentHl.Ranges.Count > 0)
                tb.TextHighlighters.Add(currentHl);
        }
    }

    public sealed partial class TextFileViewerWindow : Window
    {
        private static TextFileViewerWindow? _instance;
        private AppWindow _appWindow;
        private Windows.System.DispatcherQueueTimer _searchTimer;
        private List<FeatureEntry> _allItems = new();
        private List<(int Index, int Field, int Pos)> _matches = new();
        private int _prevCurrentIndex = -1;
        private int _currentMatchIndex = -1;
        private ScrollViewer? _listScrollViewer;
        public ObservableCollection<FeatureEntry> FilteredItems { get; } = new();
        private const int GWLP_WNDPROC = -4;
        private const uint WM_NCLBUTTONDBLCLK = 0x00A3;
        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        private WndProcDelegate? _newWndProc;
        private IntPtr _oldWndProc;
        private IntPtr _hwnd;

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, WndProcDelegate newProc);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtrRaw(IntPtr hWnd, int nIndex, IntPtr newProc);

        [DllImport("user32.dll", EntryPoint = "CallWindowProcW")]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        private void DisableDoubleClickMaximize()
        {
            _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            _newWndProc = WndProc;
            _oldWndProc = SetWindowLongPtr(_hwnd, GWLP_WNDPROC, _newWndProc);
        }

        private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_NCLBUTTONDBLCLK)
                return IntPtr.Zero;
            return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
        }

        public static void ShowOrActivate()
        {
            if (_instance != null)
            {
                _instance._appWindow.Show(true);
                return;
            }
            _instance = new TextFileViewerWindow();
            _instance.Activate();
        }

        public TextFileViewerWindow()
        {
            this.InitializeComponent();

            _searchTimer = Windows.System.DispatcherQueue.GetForCurrentThread().CreateTimer();
            _searchTimer.Interval = TimeSpan.FromMilliseconds(300);
            _searchTimer.Tick += (s, e) => { _searchTimer.Stop(); ApplySearch(); };

            _appWindow = this.AppWindow;
            _appWindow.SetIcon("Assets\\AppIcon.ico");
            _appWindow.Title = new ResourceLoader().GetString("TextFileViewerWindowTitle");
            _appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            _appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
            _appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            _appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

            var presenter = OverlappedPresenter.Create();
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
            presenter.IsResizable = true;
            presenter.PreferredMinimumWidth = 900;
            presenter.PreferredMinimumHeight = 500;
            _appWindow.SetPresenter(presenter);
            DisableDoubleClickMaximize();

            this.SystemBackdrop = AppThemeManager.CurrentMaterial switch
            {
                BackgroundMaterial.MicaAlt => new MicaBackdrop { Kind = MicaKind.BaseAlt },
                BackgroundMaterial.Acrylic => new DesktopAcrylicBackdrop(),
                _ => new MicaBackdrop { Kind = MicaKind.Base }
            };

            _appWindow.Changed += AppWindow_Changed;
            this.Closed += TextFileViewerWindow_Closed;

            LoadCsv();
            SetLocalizedHeaders();

            FeaturesListView.AddHandler(UIElement.RightTappedEvent, new RightTappedEventHandler(FeaturesListView_RightTapped), true);
        }

        private void TextFileViewerWindow_Closed(object sender, WindowEventArgs args)
        {
            _instance = null;
            if (_appWindow != null)
                _appWindow.Changed -= AppWindow_Changed;
            _searchTimer?.Stop();
            if (_oldWndProc != IntPtr.Zero && _hwnd != IntPtr.Zero)
            {
                SetWindowLongPtrRaw(_hwnd, GWLP_WNDPROC, _oldWndProc);
                _oldWndProc = IntPtr.Zero;
            }
            _newWndProc = null;
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
                    var b = ControlContainer.TransformToVisual(null).TransformBounds(
                        new Windows.Foundation.Rect(0, 0, ControlContainer.ActualWidth, ControlContainer.ActualHeight));
                    exX = (int)(b.X * scale);
                    exRight = (int)((b.X + b.Width) * scale);
                }
                else
                {
                    var b = SearchBox.TransformToVisual(null).TransformBounds(
                        new Windows.Foundation.Rect(0, 0, SearchBox.ActualWidth, SearchBox.ActualHeight));
                    exX = (int)(b.X * scale);
                    exRight = exX + (int)(b.Width * scale);
                }

                if (exX > 0)
                    rects.Add(new RectInt32(0, 0, exX, titleBarHeight));
                if (exRight < windowWidth)
                    rects.Add(new RectInt32(exRight, 0, windowWidth - exRight, titleBarHeight));

                if (rects.Count > 0)
                    _appWindow.TitleBar.SetDragRectangles(rects.ToArray());
            }
            catch { }
        }

        private void HeaderTable_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (HeaderDataTable == null || ColDescriptionColumn == null
                || ColIdColumn == null || ColVariantColumn == null)
                return;

            double total = HeaderDataTable.ActualWidth;
            if (total <= 0)
                return;

            double idW = ColIdColumn.ActualWidth > 0 ? ColIdColumn.ActualWidth : ColIdColumn.DesiredWidth.Value;
            double varW = ColVariantColumn.ActualWidth > 0 ? ColVariantColumn.ActualWidth : ColVariantColumn.DesiredWidth.Value;

            double available = total - idW - varW - 2 - HeaderDataTable.ColumnSpacing * 3;
            if (available < 40) available = 40;

            if (Math.Abs(ColDescriptionColumn.DesiredWidth.Value - available) < 0.5
                && ColDescriptionColumn.DesiredWidth.GridUnitType == GridUnitType.Pixel)
                return;

            ColDescriptionColumn.DesiredWidth = new GridLength(available, GridUnitType.Pixel);

            this.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                InvalidateAllDataRows(FeaturesListView);
            });
        }

        private static void InvalidateAllDataRows(DependencyObject root)
        {
            if (root == null) return;
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is CommunityToolkit.WinUI.Controls.DataRow row)
                    row.InvalidateMeasure();
                InvalidateAllDataRows(child);
            }
        }

        private void SetLocalizedHeaders()
        {
            bool isChinese = IsChinese();
            ColIdHeader.Text = "ID";
            ColVariantHeader.Text = isChinese ? "\u53D8\u4F53" : "Variant";
            ColDescriptionHeader.Text = isChinese ? "\u63CF\u8FF0" : "Description";
        }

        private void LoadCsv()
        {
            string folder = GetFeatureTextFolder();
            string basePath = AppContext.BaseDirectory;
            string csvPath = Path.Combine(basePath, "Strings", folder, "Features.csv");

            if (!File.Exists(csvPath))
                return;

            var lines = File.ReadAllLines(csvPath);
            for (int i = 1; i < lines.Length; i++)
            {
                var entry = ParseCsvLine(lines[i]);
                if (entry != null)
                {
                    _allItems.Add(entry);
                }
            }

            foreach (var item in _allItems)
                FilteredItems.Add(item);
        }

        private static FeatureEntry? ParseCsvLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return null;

            var fields = new List<string>();
            bool inQuotes = false;
            var current = new System.Text.StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            current.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        current.Append(c);
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inQuotes = true;
                    }
                    else if (c == ',')
                    {
                        fields.Add(current.ToString());
                        current.Clear();
                    }
                    else
                    {
                        current.Append(c);
                    }
                }
            }
            fields.Add(current.ToString());

            if (fields.Count < 3)
                return null;

            return new FeatureEntry
            {
                Id = fields[0].Trim(),
                Variant = fields[1].Trim(),
                Description = fields[2].Trim()
            };
        }

        private bool _controlsExpanded = false;

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

        private void Storyboard_Completed(object sender, object e) => UpdateDragRects();

        private void MatchCaseToggle_Click(object sender, RoutedEventArgs e) => ApplySearch();

        private void ApplySearch()
        {
            string query = SearchBox.Text;
            bool caseSensitive = MatchCaseToggle.IsChecked == true;
            var cmp = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            _matches.Clear();
            _currentMatchIndex = -1;
            _prevCurrentIndex = -1;

            foreach (var item in FilteredItems)
            {
                item.CaseSensitive = caseSensitive;
                item.Query = query;
                item.CurrentField = 0;
                item.CurrentMatchPos = -1;
            }

            if (string.IsNullOrEmpty(query))
            {
                MatchCountText.Text = "";
                return;
            }

            for (int i = 0; i < FilteredItems.Count; i++)
            {
                var item = FilteredItems[i];
                AddMatchesIn(item.Id, i, 1, query, cmp);
                AddMatchesIn(item.Variant, i, 2, query, cmp);
                AddMatchesIn(item.Description, i, 3, query, cmp);
            }

            if (_matches.Count > 0)
            {
                _currentMatchIndex = 0;
                NavigateToCurrentMatch();
            }
            else
            {
                MatchCountText.Text = "0/0";
            }
        }

        private void AddMatchesIn(string text, int idx, int field, string query, StringComparison cmp)
        {
            int pos = 0;
            while ((pos = text.IndexOf(query, pos, cmp)) >= 0)
            {
                _matches.Add((idx, field, pos));
                pos += query.Length;
            }
        }

        private void NavigateToCurrentMatch()
        {
            if (_matches.Count == 0 || _currentMatchIndex < 0) return;

            if (_prevCurrentIndex >= 0 && _prevCurrentIndex < FilteredItems.Count)
            {
                FilteredItems[_prevCurrentIndex].CurrentField = 0;
                FilteredItems[_prevCurrentIndex].CurrentMatchPos = -1;
            }

            var (idx, field, pos) = _matches[_currentMatchIndex];
            var item = FilteredItems[idx];
            item.CurrentField = field;
            item.CurrentMatchPos = pos;
            _prevCurrentIndex = idx;
            FeaturesListView.ScrollIntoView(item);
            MatchCountText.Text = $"{_currentMatchIndex + 1}/{_matches.Count}";

            this.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                var sv = GetListScrollViewer();
                if (sv == null || HeaderBorder == null) return;
                if (FeaturesListView.ContainerFromItem(item) is not ListViewItem container) return;

                try
                {
                    var p = container.TransformToVisual(sv).TransformPoint(new Point(0, 0));
                    double headerH = HeaderBorder.ActualHeight;
                    if (p.Y < headerH)
                    {
                        double delta = headerH - p.Y + 4;
                        sv.ChangeView(null, Math.Max(0, sv.VerticalOffset - delta), null, true);
                    }
                    else if (p.Y + container.ActualHeight > sv.ViewportHeight)
                    {
                        double delta = p.Y + container.ActualHeight - sv.ViewportHeight + 4;
                        sv.ChangeView(null, sv.VerticalOffset + delta, null, true);
                    }
                }
                catch { }
            });
        }

        private ScrollViewer? GetListScrollViewer()
        {
            if (_listScrollViewer != null) return _listScrollViewer;
            _listScrollViewer = FindDescendant<ScrollViewer>(FeaturesListView);
            return _listScrollViewer;
        }

        private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
        {
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var c = VisualTreeHelper.GetChild(root, i);
                if (c is T t) return t;
                var nested = FindDescendant<T>(c);
                if (nested != null) return nested;
            }
            return null;
        }

        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            if (_matches.Count == 0) return;
            _currentMatchIndex = (_currentMatchIndex - 1 + _matches.Count) % _matches.Count;
            NavigateToCurrentMatch();
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (_matches.Count == 0) return;
            _currentMatchIndex = (_currentMatchIndex + 1) % _matches.Count;
            NavigateToCurrentMatch();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchTimer.Stop();
            _searchTimer.Start();
        }

        private static bool IsChinese()
        {
            string language = "";
            try
            {
                language = GlobalizationPreferences.Languages.FirstOrDefault() ?? "";
            }
            catch { }
            return language.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetFeatureTextFolder()
        {
            return IsChinese() ? "zh-CN" : "en-US";
        }

        private static readonly System.Text.RegularExpressions.Regex _idPattern =
            new(@"^\s*\d{8,}(\s*,\s*\d{8,})*\s*$", System.Text.RegularExpressions.RegexOptions.Compiled);

        private void FeaturesListView_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (e.OriginalSource is not DependencyObject src) return;

            var item = FindAncestor<ListViewItem>(src) ?? FindItemAtPoint(e.GetPosition(null));
            if (item == null) return;
            if (item.Content is not FeatureEntry fe) return;

            string selectedText = GetSelectedTextFromRow(item);
            bool hasSelection = !string.IsNullOrEmpty(selectedText);
            bool isIdSelection = hasSelection && _idPattern.IsMatch(selectedText);

            string idCsv = isIdSelection ? NormalizeIdCsv(selectedText) : fe.Id;
            string copyContent = hasSelection ? selectedText : fe.Id;
            bool isCopyId = !hasSelection || isIdSelection;
            TextBlock copySourceTb = hasSelection ? FindFocusedTextBlock(item) : null;

            e.Handled = true;
            ShowRowCommandBarFlyout(item, e.GetPosition(item), idCsv, copyContent, isCopyId, copySourceTb);
        }

        private ListViewItem FindItemAtPoint(Point hostPoint)
        {
            try
            {
                var elements = VisualTreeHelper.FindElementsInHostCoordinates(hostPoint, FeaturesListView);
                foreach (var el in elements)
                    if (el is ListViewItem lvi) return lvi;
            }
            catch { }
            return null;
        }

        private static T FindAncestor<T>(DependencyObject d) where T : DependencyObject
        {
            while (d != null)
            {
                if (d is T t) return t;
                d = VisualTreeHelper.GetParent(d);
            }
            return null;
        }

        private static string GetSelectedTextFromRow(DependencyObject root)
        {
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var c = VisualTreeHelper.GetChild(root, i);
                if (c is TextBlock tb && !string.IsNullOrEmpty(tb.SelectedText))
                    return tb.SelectedText;
                var inner = GetSelectedTextFromRow(c);
                if (!string.IsNullOrEmpty(inner)) return inner;
            }
            return null;
        }

        private static TextBlock FindFocusedTextBlock(DependencyObject root)
        {
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var c = VisualTreeHelper.GetChild(root, i);
                if (c is TextBlock tb && !string.IsNullOrEmpty(tb.SelectedText))
                    return tb;
                var inner = FindFocusedTextBlock(c);
                if (inner != null) return inner;
            }
            return null;
        }

        private static string NormalizeIdCsv(string raw)
        {
            var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
                parts[i] = parts[i].Trim();
            return string.Join(",", parts);
        }

        private void ShowRowCommandBarFlyout(FrameworkElement target, Point pos, string idCsv, string copyContent, bool isCopyId, TextBlock copySourceTb)
        {
            var rl = new ResourceLoader();

            var flyout = new CommandBarFlyout { Placement = FlyoutPlacementMode.RightEdgeAlignedTop, AlwaysExpanded = true };

            var enableBtn = new AppBarButton { Label = rl.GetString("Menu_EnableFeature/Text"), Icon = new SymbolIcon(Symbol.Accept) };
            enableBtn.Click += async (s, e) => await ExecuteFeatureAsync(idCsv, "Enable");
            var disableBtn = new AppBarButton { Label = rl.GetString("Menu_DisableFeature/Text"), Icon = new SymbolIcon(Symbol.Cancel) };
            disableBtn.Click += async (s, e) => await ExecuteFeatureAsync(idCsv, "Disable");
            var restoreBtn = new AppBarButton { Label = rl.GetString("Menu_RestoreFeature/Text"), Icon = new SymbolIcon(Symbol.Undo) };
            restoreBtn.Click += async (s, e) => await ExecuteFeatureAsync(idCsv, "Restore");

            flyout.PrimaryCommands.Add(enableBtn);
            flyout.PrimaryCommands.Add(disableBtn);
            flyout.PrimaryCommands.Add(restoreBtn);

            string copyLabel = isCopyId
                ? rl.GetString("Menu_CopyFeatureId/Text")
                : rl.GetString("Menu_CopyText/Text");
            var copyBtn = new AppBarButton { Label = copyLabel, Icon = new SymbolIcon(Symbol.Copy) };
            copyBtn.Click += (s, e) =>
            {
                var pkg = new Windows.ApplicationModel.DataTransfer.DataPackage();
                pkg.SetText(copyContent);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(pkg);
            };
            flyout.SecondaryCommands.Add(copyBtn);

            if (copySourceTb != null)
            {
                var selectAllBtn = new AppBarButton { Label = rl.GetString("Menu_SelectAll/Text"), Icon = new FontIcon { Glyph = "\uE8B3" } };
                selectAllBtn.Click += (s, e) => copySourceTb.SelectAll();
                flyout.SecondaryCommands.Add(selectAllBtn);
            }

            flyout.ShowAt(target, new FlyoutShowOptions { Position = pos });
        }

        private async System.Threading.Tasks.Task ExecuteFeatureAsync(string idCsv, string action)
        {
            var rl = new ResourceLoader();
            string busyText = action switch
            {
                "Enable" => rl.GetString("EnableDisable_EnableButton/Text"),
                "Disable" => rl.GetString("EnableDisable_DisableButton/Text"),
                "Restore" => rl.GetString("EnableDisable_RestoreButton/Text"),
                _ => ""
            };
            ShowLoadingOverlay(busyText);
            MainWindow.Instance?.ShowTaskbarIndeterminate();

            var page = ViVeToolGUI.Pages.EnableDisablePage.Instance;
            if (page == null)
            {
                HideLoadingOverlay();
                MainWindow.Instance?.ShowTaskbarError();
                await ShowErrorAsync("EnableDisablePage not initialized.");
                return;
            }

            var (ok, msg) = await page.RunFeatureCommandAsync(idCsv, action);
            HideLoadingOverlay();

            if (ok)
            {
                MainWindow.Instance?.ShowTaskbarCompleted();
                var dlg = new ViVeToolGUI.Dialogs.SuccessDialog(msg) { XamlRoot = this.Content.XamlRoot };
                await dlg.ShowAsync();
            }
            else
            {
                MainWindow.Instance?.ShowTaskbarError();
                await ShowErrorAsync(msg);
            }
        }

        private async System.Threading.Tasks.Task ShowErrorAsync(string message)
        {
            var dlg = new ViVeToolGUI.Dialogs.ErrorDialog(message) { XamlRoot = this.Content.XamlRoot };
            await dlg.ShowAsync();
        }

        private void ShowLoadingOverlay(string text)
        {
            if (LoadingText != null) LoadingText.Text = text ?? "";
            if (LoadingOverlay != null) LoadingOverlay.Visibility = Visibility.Visible;
        }

        private void HideLoadingOverlay()
        {
            if (LoadingOverlay != null) LoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }
}