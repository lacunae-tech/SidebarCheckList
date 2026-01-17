using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using static SidebarChecklist.Win32.NativeMethods;

namespace SidebarChecklist.Win32
{
    internal sealed class AppBarService
    {
        private readonly Window _window;
        private uint _callbackMsg;
        private bool _registered;

        public AppBarService(Window window)
        {
            _window = window;
        }

        public void Register()
        {
            if (_registered) return;

            var hwnd = new WindowInteropHelper(_window).Handle;
            _callbackMsg = RegisterWindowMessage("SidebarChecklist.AppBarMessage.v1");

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

            var hwnd = new WindowInteropHelper(_window).Handle;
            var abd = new APPBARDATA
            {
                cbSize = System.Runtime.InteropServices.Marshal.SizeOf<APPBARDATA>(),
                hWnd = hwnd
            };

            SHAppBarMessage(ABM_REMOVE, ref abd);
            _registered = false;
        }

        public void ApplyRightDock(NativeMethods.RECT workArea, NativeMethods.RECT monitorArea, int width)
        {
            if (!_registered) return;

            var hwnd = new WindowInteropHelper(_window).Handle;
            var dpi = VisualTreeHelper.GetDpi(_window);
            var scaleX = dpi.DpiScaleX == 0 ? 1.0 : dpi.DpiScaleX;
            var scaleY = dpi.DpiScaleY == 0 ? 1.0 : dpi.DpiScaleY;

            // 希望位置（右端）
            var rc = new NativeMethods.RECT
            {
                top = workArea.top,
                bottom = workArea.bottom,
                right = monitorArea.right,
                left = monitorArea.right - width
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
            abd.rc.left = abd.rc.right - width;

            // 確定
            SHAppBarMessage(ABM_SETPOS, ref abd);

            // WPF側の位置・サイズも追従（“被らない”を成立させる）
            _window.Left = abd.rc.left / scaleX;
            _window.Top = abd.rc.top / scaleY;
            _window.Width = width / scaleX;
            _window.Height = (abd.rc.bottom - abd.rc.top) / scaleY;
        }
    }
}
