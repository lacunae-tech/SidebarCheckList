using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using static SidebarChecklist.Win32.NativeMethods;

namespace SidebarChecklist.Win32
{
    internal sealed class AppBarService
    {
        private readonly Window _window;
        private readonly MonitorService _monitorService;
        private readonly DispatcherTimer _reapplyTimer;
        private uint _callbackMsg;
        private bool _registered;
        private int _lastWidthDip;
        private HwndSource? _hwndSource;
        private string _targetMonitor;

        public AppBarService(Window window, MonitorService monitorService, string targetMonitor)
        {
            _window = window;
            _monitorService = monitorService;
            _targetMonitor = targetMonitor;
            _reapplyTimer = new DispatcherTimer(DispatcherPriority.Background, _window.Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _reapplyTimer.Tick += ReapplyTimer_Tick;
        }

        public void Register()
        {
            if (_registered) return;

            var hwnd = new WindowInteropHelper(_window).Handle;
            _callbackMsg = RegisterWindowMessage("SidebarChecklist.AppBarMessage.v1");
            _hwndSource = HwndSource.FromHwnd(hwnd);
            _hwndSource?.AddHook(WndProc);

            var abd = new APPBARDATA
            {
                cbSize = System.Runtime.InteropServices.Marshal.SizeOf<APPBARDATA>(),
                hWnd = hwnd,
                uCallbackMessage = _callbackMsg
            };

            SHAppBarMessage(ABM_NEW, ref abd);
            _registered = true;
        }

        public void Unregister()
        {
            if (!_registered) return;

            _reapplyTimer.Stop();
            if (_hwndSource is not null)
            {
                _hwndSource.RemoveHook(WndProc);
                _hwndSource = null;
            }

            var hwnd = new WindowInteropHelper(_window).Handle;
            var abd = new APPBARDATA
            {
                cbSize = System.Runtime.InteropServices.Marshal.SizeOf<APPBARDATA>(),
                hWnd = hwnd
            };

            SHAppBarMessage(ABM_REMOVE, ref abd);
            _registered = false;
        }

        public void UpdateTargetMonitor(string targetMonitor)
        {
            _targetMonitor = targetMonitor;
        }

        public void ApplyRightDock(int widthDip)
        {
            _lastWidthDip = widthDip;
            if (!_registered) return;

            var hwnd = new WindowInteropHelper(_window).Handle;
            var dpi = VisualTreeHelper.GetDpi(_window);
            var scaleX = dpi.DpiScaleX == 0 ? 1.0 : dpi.DpiScaleX;
            var scaleY = dpi.DpiScaleY == 0 ? 1.0 : dpi.DpiScaleY;
            var widthPx = (int)Math.Round(widthDip * scaleX);

            var rcMonitor = ResolveMonitorArea(hwnd);

            // 希望位置（右端）
            var rc = new NativeMethods.RECT
            {
                top = rcMonitor.top,
                bottom = rcMonitor.bottom,
                right = rcMonitor.right,
                left = rcMonitor.right - widthPx
            };

            var abd = new APPBARDATA
            {
                cbSize = System.Runtime.InteropServices.Marshal.SizeOf<APPBARDATA>(),
                hWnd = hwnd,
                uEdge = ABE_RIGHT,
                rc = rc
            };

            // OSに調整させる
            SHAppBarMessage(ABM_QUERYPOS, ref abd);

            // 幅を確保
            abd.rc.left = abd.rc.right - widthPx;

            // 確定
            SHAppBarMessage(ABM_SETPOS, ref abd);

            // WPF側の位置・サイズも追従（“被らない”を成立させる）
            _window.Left = abd.rc.left / scaleX;
            _window.Top = abd.rc.top / scaleY;
            _window.Width = (abd.rc.right - abd.rc.left) / scaleX;
            _window.Height = (abd.rc.bottom - abd.rc.top) / scaleY;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_DISPLAYCHANGE || msg == WM_DPICHANGED || msg == WM_SETTINGCHANGE)
            {
                ScheduleReapply();
            }
            else if (msg == _callbackMsg && wParam.ToInt32() == ABN_POSCHANGED)
            {
                ScheduleReapply();
            }

            return IntPtr.Zero;
        }

        private void ScheduleReapply()
        {
            if (!_registered || _lastWidthDip <= 0) return;
            _reapplyTimer.Stop();
            _reapplyTimer.Start();
        }

        private void ReapplyTimer_Tick(object? sender, EventArgs e)
        {
            _reapplyTimer.Stop();
            Reapply();
        }

        private void Reapply()
        {
            if (_lastWidthDip <= 0) return;
            ApplyRightDock(_lastWidthDip);
        }

        private NativeMethods.RECT ResolveMonitorArea(IntPtr hwnd)
        {
            if (string.Equals(_targetMonitor, "auto", StringComparison.OrdinalIgnoreCase))
            {
                var monitorHandle = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
                if (monitorHandle != IntPtr.Zero)
                {
                    var monitorInfo = new MONITORINFOEX
                    {
                        cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFOEX>()
                    };

                    if (GetMonitorInfo(monitorHandle, ref monitorInfo))
                    {
                        return monitorInfo.rcMonitor;
                    }
                }
            }

            return _monitorService.GetTarget(_targetMonitor).MonitorArea;
        }
    }
}
