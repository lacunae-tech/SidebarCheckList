using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using static SidebarChecklist.Win32.NativeMethods;

namespace SidebarChecklist.Win32
{
    internal enum AppBarEdge
    {
        Left = ABE_LEFT,
        Top = ABE_TOP,
        Right = ABE_RIGHT,
        Bottom = ABE_BOTTOM
    }

    internal sealed class AppBarManager
    {
        private readonly DispatcherTimer _debounceTimer;
        private Window? _window;
        private HwndSource? _source;
        private uint _callbackMsg;
        private bool _registered;
        private int _sidebarWidthPx;
        private RECT? _pendingDpiRect;
        private AppBarEdge _edge = AppBarEdge.Right;

        public AppBarManager()
        {
            _debounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _debounceTimer.Tick += DebounceTimer_Tick;
        }

        public void RegisterAppBar(Window window)
        {
            if (_registered)
            {
                return;
            }

            _window = window;
            var hwnd = new WindowInteropHelper(window).Handle;
            _callbackMsg = RegisterWindowMessage("SidebarChecklist.AppBarMessage.v1");

            var abd = new APPBARDATA
            {
                cbSize = Marshal.SizeOf<APPBARDATA>(),
                hWnd = hwnd,
                uCallbackMessage = _callbackMsg
            };

            SHAppBarMessage(ABM_NEW, ref abd);
            _registered = true;

            _source = PresentationSource.FromVisual(window) as HwndSource;
            _source?.AddHook(WndProc);
        }

        public void UnregisterAppBar()
        {
            if (!_registered)
            {
                return;
            }

            if (_source is not null)
            {
                _source.RemoveHook(WndProc);
                _source = null;
            }

            _debounceTimer.Stop();

            if (_window is null)
            {
                _registered = false;
                return;
            }

            var hwnd = new WindowInteropHelper(_window).Handle;
            var abd = new APPBARDATA
            {
                cbSize = Marshal.SizeOf<APPBARDATA>(),
                hWnd = hwnd
            };

            SHAppBarMessage(ABM_REMOVE, ref abd);
            _registered = false;
        }

        public void UpdateSidebarWidth(int widthPx)
        {
            _sidebarWidthPx = widthPx;
        }

        public void ReRegisterAndReposition()
        {
            if (_window is null)
            {
                return;
            }

            var hwnd = new WindowInteropHelper(_window).Handle;
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            if (_registered)
            {
                // 画面構成変更時は再ネゴシエーションが必要なため、いったん解除して再登録する。
                var remove = new APPBARDATA
                {
                    cbSize = Marshal.SizeOf<APPBARDATA>(),
                    hWnd = hwnd
                };
                SHAppBarMessage(ABM_REMOVE, ref remove);
            }

            var add = new APPBARDATA
            {
                cbSize = Marshal.SizeOf<APPBARDATA>(),
                hWnd = hwnd,
                uCallbackMessage = _callbackMsg
            };

            SHAppBarMessage(ABM_NEW, ref add);
            _registered = true;

            ApplyRightDock(hwnd);
        }

        private void ApplyRightDock(IntPtr hwnd)
        {
            if (_window is null)
            {
                return;
            }

            if (_pendingDpiRect.HasValue)
            {
                var dpiRect = _pendingDpiRect.Value;
                // DPI変更時の推奨矩形を先に反映し、その後に右端固定位置へ再配置する。
                SetWindowPos(hwnd, IntPtr.Zero, dpiRect.left, dpiRect.top, dpiRect.Width, dpiRect.Height, SWP_NOZORDER | SWP_NOACTIVATE);
                _pendingDpiRect = null;
            }

            var screen = Screen.FromHandle(hwnd);
            var bounds = screen.Bounds;
            var widthPx = _sidebarWidthPx;

            // WorkAreaはAppBar予約後に縮むため、位置決めはBounds基準で算出する。
            var rc = new RECT
            {
                top = bounds.Top,
                bottom = bounds.Bottom,
                right = bounds.Right,
                left = bounds.Right - widthPx
            };

            var abd = new APPBARDATA
            {
                cbSize = Marshal.SizeOf<APPBARDATA>(),
                hWnd = hwnd,
                uEdge = (uint)_edge,
                rc = rc
            };

            // 1) OSに候補位置を問い合わせ
            SHAppBarMessage(ABM_QUERYPOS, ref abd);

            // 2) 幅を確定
            abd.rc.left = abd.rc.right - widthPx;

            // 3) 確定
            SHAppBarMessage(ABM_SETPOS, ref abd);

            // 4) 最終RECTへ移動
            var finalRect = abd.rc;
            SetWindowPos(hwnd, IntPtr.Zero, finalRect.left, finalRect.top, finalRect.Width, finalRect.Height, SWP_NOZORDER | SWP_NOACTIVATE);

            // WPF側のDIPプロパティも同期しておく（DIP/ピクセル混在を避けるため変換）。
            var dpi = VisualTreeHelper.GetDpi(_window);
            var scaleX = dpi.DpiScaleX == 0 ? 1.0 : dpi.DpiScaleX;
            var scaleY = dpi.DpiScaleY == 0 ? 1.0 : dpi.DpiScaleY;
            _window.Left = finalRect.left / scaleX;
            _window.Top = finalRect.top / scaleY;
            _window.Width = finalRect.Width / scaleX;
            _window.Height = finalRect.Height / scaleY;
        }

        private void RequestReposition()
        {
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        private void DebounceTimer_Tick(object? sender, EventArgs e)
        {
            _debounceTimer.Stop();
            ReRegisterAndReposition();
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == _callbackMsg && wParam.ToInt32() == ABN_POSCHANGED)
            {
                RequestReposition();
                return IntPtr.Zero;
            }

            if (msg == WM_DISPLAYCHANGE || msg == WM_SETTINGCHANGE)
            {
                RequestReposition();
                return IntPtr.Zero;
            }

            if (msg == WM_DPICHANGED)
            {
                _pendingDpiRect = Marshal.PtrToStructure<RECT>(lParam);
                RequestReposition();
                handled = true;
                return IntPtr.Zero;
            }

            return IntPtr.Zero;
        }
    }
}
