using SidebarChecklist.Models;
using SidebarChecklist.Services;
using SidebarChecklist.ViewModels;
using SidebarChecklist.Win32;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;

namespace SidebarChecklist
{
    public partial class MainWindow : Window
    {
        private const int DefaultWidth = 400;
        private const int MinWidthPx = 280;
        private const int MaxWidthPx = 900;
        private const int MinChecklistFontSize = 10;
        private const int MaxChecklistFontSize = 28;
        private const int MinChecklistCheckboxSize = 12;
        private const int MaxChecklistCheckboxSize = 28;

        private readonly string _appDir;
        private readonly SettingsService _settingsService;
        private ChecklistService _checklistService = null!;
        private ChecklistSaveService _checklistSaveService = null!;
        private readonly MonitorService _monitorService;
        private readonly AppBarService _appBarService;

        private SettingsRoot _settings = new();
        private ChecklistRoot? _checklistRoot;

        private readonly MainViewModel _vm = new();
        private readonly DispatcherTimer _foregroundTimer;
        private readonly DispatcherTimer _toastTimer;
        private HwndSource? _hwndSource;
        private bool _isTopmostSuspended;
        private ICollectionView? _listView;
        private string _listSearchText = "";

        // Resize state
        private bool _isResizing;
        private Point _resizeStartPoint;
        private double _resizeStartWidthPx;

        public MainWindow()
        {
            InitializeComponent();

            _appDir = AppDomain.CurrentDomain.BaseDirectory;
            _settingsService = new SettingsService(_appDir);
            _monitorService = new MonitorService();
            _appBarService = new AppBarService(this);
            _foregroundTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _foregroundTimer.Tick += ForegroundTimer_Tick;
            _toastTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _toastTimer.Tick += ToastTimer_Tick;

            DataContext = _vm;

            Loaded += MainWindow_Loaded;
            SourceInitialized += MainWindow_SourceInitialized;
            Closing += MainWindow_Closing;
        }

        private void MainWindow_SourceInitialized(object? sender, EventArgs e)
        {
            // AppBar登録（作業領域確保）
            _appBarService.Register();
            _hwndSource = PresentationSource.FromVisual(this) as HwndSource;
            _hwndSource?.AddHook(WndProc);
            SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // AppBar解除
            _appBarService.Unregister();
            _foregroundTimer.Stop();
            _toastTimer.Stop();
            if (_hwndSource is not null)
            {
                _hwndSource.RemoveHook(WndProc);
                _hwndSource = null;
            }
            SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;
        }

        private void SystemEvents_DisplaySettingsChanged(object? sender, EventArgs e)
        {
            Dispatcher.InvokeAsync(ApplyDock);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_SYSCOMMAND)
            {
                var command = wParam.ToInt32() & 0xFFF0;
                if (command == NativeMethods.SC_MOVE)
                {
                    handled = true;
                    return IntPtr.Zero;
                }
            }

            if (msg == NativeMethods.WM_NCLBUTTONDOWN && wParam.ToInt32() == NativeMethods.HTCAPTION)
            {
                handled = true;
                return IntPtr.Zero;
            }

            return IntPtr.Zero;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 1) settings.json 必須：読めないなら「JSONファイルエラー」→終了してよい
            try
            {
                _settings = _settingsService.LoadRequiredOrThrow();
            }
            catch
            {
                MessageBox.Show("JSONファイルエラー", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
                return;
            }

            // 2) settings 値の丸め/フォールバック
            _settings.Window.SidebarWidthPx = Clamp(_settings.Window.SidebarWidthPx, MinWidthPx, MaxWidthPx);
            _settings.Checklist.FontSize = Clamp(_settings.Checklist.FontSize, MinChecklistFontSize, MaxChecklistFontSize);
            _settings.Checklist.CheckboxSize = Clamp(_settings.Checklist.CheckboxSize, MinChecklistCheckboxSize, MaxChecklistCheckboxSize);

            if (!string.Equals(_settings.Display.TargetMonitor, "main", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(_settings.Display.TargetMonitor, "sub", StringComparison.OrdinalIgnoreCase))
            {
                _settings.Display.TargetMonitor = "main";
            }

            // sub存在しないなら mainへ
            if (string.Equals(_settings.Display.TargetMonitor, "sub", StringComparison.OrdinalIgnoreCase) &&
                !_monitorService.HasSubMonitor())
            {
                _settings.Display.TargetMonitor = "main";
            }

            _checklistService = new ChecklistService(_appDir, _settings.Checklist.Path);
            _checklistSaveService = new ChecklistSaveService(_appDir, _settings.Checklist.SavePath);

            // 3) checklist.json 読み込み（任意）
            LoadChecklist();

            ApplyChecklistAppearance();
            UpdateMonitorButtonsState();
            ApplyDock();
            _foregroundTimer.Start();
        }

        private void ForegroundTimer_Tick(object? sender, EventArgs e)
        {
            UpdateTopmostForForegroundWindow();
        }

        private void UpdateTopmostForForegroundWindow()
        {
            var hwnd = NativeMethods.GetForegroundWindow();
            var shouldSuspend = IsRemoteDesktopForegroundWindow(hwnd);

            if (shouldSuspend && Topmost)
            {
                Topmost = false;
                _isTopmostSuspended = true;
            }
            else if (!shouldSuspend && _isTopmostSuspended)
            {
                Topmost = true;
                _isTopmostSuspended = false;
            }
        }

        private static bool IsRemoteDesktopForegroundWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
                return false;

            var processName = GetProcessNameFromWindow(hwnd);
            return IsRemoteDesktopProcess(processName);
        }

        private static bool IsRemoteDesktopProcess(string? processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return false;

            return string.Equals(processName, "mstsc", StringComparison.OrdinalIgnoreCase)
                || string.Equals(processName, "msrdc", StringComparison.OrdinalIgnoreCase);
        }

        private static string? GetProcessNameFromWindow(IntPtr hwnd)
        {
            NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == 0)
                return null;

            try
            {
                return Process.GetProcessById((int)pid).ProcessName;
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return null;
            }
        }

        private void SetVersionLabel(string? version)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                VersionText.Visibility = Visibility.Collapsed;
                return;
            }

            VersionText.Text = $"v{version}";
            VersionText.Visibility = Visibility.Visible;
        }

        private void ApplyChecklistAppearance()
        {
            Resources["ChecklistFontSize"] = (double)_settings.Checklist.FontSize;
            Resources["ChecklistCheckboxSize"] = (double)_settings.Checklist.CheckboxSize;
        }

        private void UpdateMonitorButtonsState()
        {
            var hasSub = _monitorService.HasSubMonitor();

            // 選択中の見た目（Windowsデフォルト範囲）
            var target = (_settings.Display.TargetMonitor ?? "main").ToLowerInvariant();
            if (target == "sub" && !hasSub)
                target = "main";

            MainBtn.IsEnabled = target != "main";
            SubBtn.IsEnabled = hasSub && target != "sub";
            MainBtn.FontWeight = target == "main" ? FontWeights.Bold : FontWeights.Normal;
            SubBtn.FontWeight = target == "sub" ? FontWeights.Bold : FontWeights.Normal;
        }

        private void ApplyDock()
        {
            var target = (_settings.Display.TargetMonitor ?? "main").ToLowerInvariant();
            if (target == "sub" && !_monitorService.HasSubMonitor())
                target = "main";

            var mon = _monitorService.GetTarget(target);
            var width = Clamp(_settings.Window.SidebarWidthPx, MinWidthPx, MaxWidthPx);

            // AppBarで作業領域確保＋右端ドック
            _appBarService.ApplyRightDock(mon.WorkArea, mon.MonitorArea, width);
        }

        private void ShowBodyMessage(string msg)
        {
            MessageText.Text = msg;
            MessageOverlay.Visibility = Visibility.Visible;
            ItemsCtl.ItemsSource = null;
            ListCombo.ItemsSource = null;
            _listView = null;
        }

        private void HideBodyMessage()
        {
            MessageOverlay.Visibility = Visibility.Collapsed;
        }

        // --- リスト切替：変更時に即 settings.json 保存（10.3）
        private void ListCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            if (_checklistRoot is null) return;

            var id = (ListCombo.SelectedValue as string) ?? "";

            // 存在しない場合：静かに先頭へ
            if (!_checklistRoot.Lists.Any(l => l.Id == id))
            {
                var first = _checklistRoot.Lists.FirstOrDefault();
                if (first is null) return;

                id = first.Id;
                ListCombo.SelectedValue = id;
            }

            _vm.SelectByIdOrFirst(id);
            ItemsCtl.ItemsSource = _vm.Items;

            _settings.Selection.SelectedListId = id;
            SafeSaveSettings();
        }

        private void ListSearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            _listSearchText = ListSearchBox.Text ?? "";
            ApplyListFilter();
        }

        // --- モニタ切替：即移動＋即保存（12.5）
        private void MainBtn_Click(object sender, RoutedEventArgs e)
        {
            _settings.Display.TargetMonitor = "main";
            UpdateMonitorButtonsState();
            ApplyDock();
            SafeSaveSettings();
        }

        private void SubBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!_monitorService.HasSubMonitor())
            {
                // ツールチップ不要：無効化済みなので通常ここには来ない
                return;
            }

            _settings.Display.TargetMonitor = "sub";
            UpdateMonitorButtonsState();
            ApplyDock();
            SafeSaveSettings();
        }

        private void RefreshBtn_Click(object sender, RoutedEventArgs e)
        {
            LoadChecklist();
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_checklistRoot is null || _vm.SelectedList is null)
            {
                MessageBox.Show("チェックリストが存在しません", "保存", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var entry = new ChecklistSaveEntry
            {
                Id = _vm.SelectedList.Id ?? "",
                Timestamp = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                ChecklistVersion = _checklistRoot.Version ?? "",
                Items = _vm.Items.Select(item => new ChecklistSavedItem
                {
                    Text = item.Text ?? "",
                    IsChecked = item.IsChecked
                }).ToList()
            };

            try
            {
                _checklistSaveService.Save(entry);
                LoadChecklist();
                ShowToast("保存しました");
            }
            catch
            {
                MessageBox.Show("保存に失敗しました", "保存", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowToast(string message)
        {
            ToastText.Text = message;
            ToastOverlay.Visibility = Visibility.Visible;
            _toastTimer.Stop();
            _toastTimer.Start();
        }

        private void HideToast()
        {
            ToastOverlay.Visibility = Visibility.Collapsed;
        }

        private void ToastTimer_Tick(object? sender, EventArgs e)
        {
            _toastTimer.Stop();
            HideToast();
        }

        private void SafeSaveSettings()
        {
            try
            {
                _settingsService.Save(_settings);
            }
            catch
            {
                // 仕様に「settings保存失敗時の挙動」未記載のため、ここでは黙って握りつぶし
                // （必要なら MessageBox に変更可能）
            }
        }

        private static int Clamp(int v, int min, int max)
            => v < min ? min : (v > max ? max : v);

        private void LoadChecklist()
        {
            var load = _checklistService.LoadOptional(_settings);
            if (load.Root is null)
            {
                _checklistRoot = null;
                // 「チェックリストが存在しません」または「JSONファイルエラー」
                SetVersionLabel(null);
                ShowBodyMessage(load.ErrorMessage ?? "チェックリストが存在しません");
                SaveBtn.IsEnabled = false;
                return;
            }

            _checklistRoot = load.Root;
            SetVersionLabel(_checklistRoot.Version);

            // 4) selected_list_id 不正 → 静かに先頭へ（UIエラーなし）
            var preferredId = _settings.Selection.SelectedListId ?? "";
            _vm.SetLists(_checklistRoot.Lists, preferredId);

            // UIバインド（ComboBox/Items）
            ListCombo.ItemsSource = _vm.Lists;
            ListCombo.SelectedValue = _vm.SelectedList?.Id;
            _listView = CollectionViewSource.GetDefaultView(_vm.Lists);
            ApplyListFilter();

            ItemsCtl.ItemsSource = _vm.Items;
            HideBodyMessage();

            SaveBtn.IsEnabled = true;

            // settings上のselected_list_idを実際の選択に合わせて補正し、起動時に保存はしない（仕様に未記載のため）
            // → ただし “保持” が必要なので、ユーザー操作時に保存する（10.3）。
        }

        private double GetDpiScaleX()
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            return dpi.DpiScaleX == 0 ? 1.0 : dpi.DpiScaleX;
        }

        private void ApplyListFilter()
        {
            if (_listView is null) return;

            var keyword = _listSearchText.Trim();
            _listView.Filter = item =>
            {
                if (item is not ChecklistListViewModel list) return false;
                if (string.IsNullOrEmpty(keyword)) return true;

                return list.Name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
            };
            _listView.Refresh();

            if (ListCombo.SelectedItem is null)
            {
                var first = _listView.Cast<ChecklistListViewModel>().FirstOrDefault();
                if (first is not null)
                {
                    ListCombo.SelectedItem = first;
                }
            }
        }
    }
}
