using System;
using System.Runtime.InteropServices;

namespace SidebarChecklist.Win32
{
    internal static class NativeMethods
    {
        internal const int MONITORINFOF_PRIMARY = 0x00000001;
        internal const int ABM_NEW = 0x00000000;
        internal const int ABM_REMOVE = 0x00000001;
        internal const int ABM_QUERYPOS = 0x00000002;
        internal const int ABM_SETPOS = 0x00000003;
        internal const int ABE_RIGHT = 0x00000002;

        internal const uint SWP_NOACTIVATE = 0x0010;
        internal const uint SWP_NOZORDER = 0x0004;
        internal const uint SWP_SHOWWINDOW = 0x0040;

        internal static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        internal delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [DllImport("user32.dll")]
        internal static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        internal static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

        [DllImport("user32.dll")]
        internal static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        internal static extern int RegisterWindowMessage(string lpString);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        internal static extern uint SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

        [DllImport("user32.dll")]
        internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [StructLayout(LayoutKind.Sequential)]
        internal struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        internal struct MONITORINFOEX
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct APPBARDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public uint uCallbackMessage;
            public uint uEdge;
            public RECT rc;
            public int lParam;
        }
    }
}
