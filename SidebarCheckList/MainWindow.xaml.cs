using SidebarChecklist.Models;
using SidebarChecklist.Services;
using SidebarChecklist.ViewModels;
using SidebarChecklist.Win32;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace SidebarChecklist
{
    public partial class MainWindow : Window
    {
        private const int DefaultWidth = 400;
        private const int MinWidthPx = 280;
        private const int MaxWidthPx = 900;

        private readonly string _appDir;
        private readonly SettingsService _settingsService;
        private readonly ChecklistService _checklistService;
        private readonly MonitorService _monitorService;
        private readonly AppBarService _appBarService;

        private SettingsRoot _settings = new();
        private ChecklistRoot? _checklistRoot;

        private readonly MainViewModel _vm = new();

        // Resize state
        private bool _isResizing;
        private Point _resizeStartPoint;
        private double _resizeStartWidth;
        private double _resizeStartWidthPx;
        private double _resizeScale; // px per DIP

        private int _resizeAnchorRightPx;        // workAreaの右端(px)
        private IntPtr _resizeMonitorHandle;     // 対象モニタ
        private SidebarChecklist.Win32.NativeMethods.RECT _resizeWorkAreaPx;


        public MainWindow()
        {
            InitializeComponent();

            _appDir = AppDomain.CurrentDomain.BaseDirectory;
            _settingsService = new SettingsService(_appDir);
            _checklistService = new ChecklistService(_appDir);
            _monitorService = new MonitorService();
            _appBarService = new AppBarService(this);

            DataContext = _vm;

            Loaded += MainWindow_Loaded;
            SourceInitialized += MainWindow_SourceInitialized;
            Closing += MainWindow_Closing;
        }

        private void MainWindow_SourceInitialized(object? sender, EventArgs e)
        {
            // AppBar登録（作業領域確保）
            _appBarService.Register();
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // AppBar解除
            _appBarService.Unregister();
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

            // 3) checklist.json 読み込み（任意）
            var load = _checklistService.LoadOptional();
            if (load.Root is null)
            {
                // 「チェックリストが存在しません」または「JSONファイルエラー」
                ShowBodyMessage(load.ErrorMessage ?? "チェックリストが存在しません");
                UpdateMonitorButtonsState();
                ApplyDock(); // 右端ドックは行う

                return;
            }

            _checklistRoot = load.Root;

            // 4) selected_list_id 不正 → 静かに先頭へ（UIエラーなし）
            var preferredId = _settings.Selection.SelectedListId ?? "";
            _vm.SetLists(_checklistRoot.Lists, preferredId);

            // UIバインド（ComboBox/Items）
            ListCombo.ItemsSource = _vm.Lists;
            ListCombo.SelectedValue = _vm.SelectedList?.Id;

            ItemsCtl.ItemsSource = _vm.Items;
            HideBodyMessage();

            // settings上のselected_list_idを実際の選択に合わせて補正し、起動時に保存はしない（仕様に未記載のため）
            // → ただし “保持” が必要なので、ユーザー操作時に保存する（10.3）。

            UpdateMonitorButtonsState();
            ApplyDock();
        }

        private void UpdateMonitorButtonsState()
        {
            var hasSub = _monitorService.HasSubMonitor();
            SubBtn.IsEnabled = hasSub;

            // 選択中の見た目（Windowsデフォルト範囲）
            var target = (_settings.Display.TargetMonitor ?? "main").ToLowerInvariant();
            MainBtn.FontWeight = target == "main" ? FontWeights.Bold : FontWeights.Normal;
            SubBtn.FontWeight = target == "sub" ? FontWeights.Bold : FontWeights.Normal;
        }

        private void ApplyDock()
        {
            var target = (_settings.Display.TargetMonitor ?? "main").ToLowerInvariant();
            if (target == "sub" && !_monitorService.HasSubMonitor())
                target = "main";

            var mon = _monitorService.GetTarget(target);
            var widthPx = Clamp(_settings.Window.SidebarWidthPx, MinWidthPx, MaxWidthPx);

            // ★変更：Handle と WorkArea(px) を渡す
            _appBarService.ApplyRightDock(mon.Handle, mon.WorkArea, widthPx);
        }


        private void ShowBodyMessage(string msg)
        {
            MessageText.Text = msg;
            MessageOverlay.Visibility = Visibility.Visible;
            ItemsCtl.ItemsSource = null;
            ListCombo.ItemsSource = null;
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

        // --- 幅変更：左端ドラッグ、ドラッグ終了時に保存（8.3）
        private void ResizeGrip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isResizing = true;
            ResizeGrip.CaptureMouse();

            // 現在のターゲット（main/sub）モニタのWorkArea(px)をアンカーとして保持
            var target = (_settings.Display.TargetMonitor ?? "main").ToLowerInvariant();
            if (target == "sub" && !_monitorService.HasSubMonitor())
                target = "main";

            var mon = _monitorService.GetTarget(target);
            _resizeMonitorHandle = mon.Handle;
            _resizeWorkAreaPx = mon.WorkArea;
            _resizeAnchorRightPx = mon.WorkArea.right;

            e.Handled = true;
        }

        private void ResizeGrip_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isResizing) return;

            // マウス位置はスクリーン座標(px)で取得（DPI影響を受けない）
            if (!SidebarChecklist.Win32.NativeMethods.GetCursorPos(out var pt))
                return;

            // 右端固定：幅(px) = 右端(px) - マウスX(px)
            var newWidthPx = _resizeAnchorRightPx - pt.x;
            var clampedPx = Clamp((int)Math.Round((double)newWidthPx), MinWidthPx, MaxWidthPx);

            if (clampedPx == _settings.Window.SidebarWidthPx) return;

            _settings.Window.SidebarWidthPx = clampedPx;

            // ドラッグ中も追従させたいのでAppBar再配置
            _appBarService.ApplyRightDock(_resizeMonitorHandle, _resizeWorkAreaPx, clampedPx);

            e.Handled = true;
        }

        private void ResizeGrip_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isResizing) return;

            _isResizing = false;
            ResizeGrip.ReleaseMouseCapture();

            // ドラッグ終了時に保存（仕様通り）
            SafeSaveSettings();

            e.Handled = true;
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
    }
}
