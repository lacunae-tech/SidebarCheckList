using System;
using System.Windows;
using System.Windows.Interop;
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

        public void ApplyRightDock(NativeMethods.RECT workArea, int width)
        {
            if (!_registered) return;

            var hwnd = new WindowInteropHelper(_window).Handle;

            // 希望位置（右端）
            var rc = new NativeMethods.RECT
            {
                top = workArea.top,
                bottom = workArea.bottom,
                right = workArea.right,
                left = workArea.right - width
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
            _window.Left = abd.rc.left;
            _window.Top = abd.rc.top;
            _window.Width = width;
            _window.Height = abd.rc.bottom - abd.rc.top;
        }
    }
}
