using SidebarChecklist.Win32;
using System;
using System.Windows;
using System.Windows.Interop;

namespace SidebarChecklist.Services
{
    internal sealed class AppBarService
    {
        private readonly Window _window;
        private IntPtr _handle;
        private bool _registered;

        public AppBarService(Window window)
        {
            _window = window;
        }

        public void Register()
        {
            EnsureHandle();
            if (_handle == IntPtr.Zero || _registered)
            {
                return;
            }

            var abd = new NativeMethods.APPBARDATA
            {
                cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.APPBARDATA>(),
                hWnd = _handle,
                uCallbackMessage = (uint)NativeMethods.RegisterWindowMessage("SidebarChecklistAppBar")
            };

            NativeMethods.SHAppBarMessage(NativeMethods.ABM_NEW, ref abd);
            _registered = true;
        }

        public void Unregister()
        {
            if (!_registered)
            {
                return;
            }

            EnsureHandle();
            if (_handle == IntPtr.Zero)
            {
                return;
            }

            var abd = new NativeMethods.APPBARDATA
            {
                cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.APPBARDATA>(),
                hWnd = _handle
            };

            NativeMethods.SHAppBarMessage(NativeMethods.ABM_REMOVE, ref abd);
            _registered = false;
        }

        public void ApplyRightDock(IntPtr monitorHandle, NativeMethods.RECT monitorBounds, int widthPx)
        {
            EnsureHandle();
            if (_handle == IntPtr.Zero)
            {
                return;
            }
            _ = monitorHandle;

            var abd = new NativeMethods.APPBARDATA
            {
                cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.APPBARDATA>(),
                hWnd = _handle,
                uEdge = NativeMethods.ABE_RIGHT,
                rc = new NativeMethods.RECT
                {
                    left = monitorBounds.right - widthPx,
                    top = monitorBounds.top,
                    right = monitorBounds.right,
                    bottom = monitorBounds.bottom
                }
            };

            NativeMethods.SHAppBarMessage(NativeMethods.ABM_QUERYPOS, ref abd);

            abd.rc.left = abd.rc.right - widthPx;
            NativeMethods.SHAppBarMessage(NativeMethods.ABM_SETPOS, ref abd);

            NativeMethods.SetWindowPos(
                _handle,
                NativeMethods.HWND_TOPMOST,
                abd.rc.left,
                abd.rc.top,
                Math.Max(0, abd.rc.right - abd.rc.left),
                Math.Max(0, abd.rc.bottom - abd.rc.top),
                NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
        }

        private void EnsureHandle()
        {
            if (_handle != IntPtr.Zero)
            {
                return;
            }

            var helper = new WindowInteropHelper(_window);
            _handle = helper.Handle;
        }
    }
}
