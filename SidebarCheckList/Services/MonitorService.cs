using SidebarChecklist.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;

namespace SidebarChecklist.Services
{
    internal sealed class MonitorService
    {
        private readonly List<MonitorInfo> _monitors;

        public MonitorService()
        {
            _monitors = LoadMonitors();
        }

        public bool HasSubMonitor()
            => _monitors.Any(m => !m.IsPrimary);

        public MonitorInfo GetTarget(string target)
        {
            var primary = _monitors.FirstOrDefault(m => m.IsPrimary) ?? _monitors.First();

            if (string.Equals(target, "sub", StringComparison.OrdinalIgnoreCase))
            {
                var sub = _monitors.FirstOrDefault(m => !m.IsPrimary);
                if (sub is not null)
                {
                    return sub;
                }
            }

            return primary;
        }

        private static List<MonitorInfo> LoadMonitors()
        {
            var result = new List<MonitorInfo>();

            NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (handle, _, ref NativeMethods.RECT bounds, IntPtr _) =>
            {
                var info = new NativeMethods.MONITORINFOEX
                {
                    cbSize = Marshal.SizeOf<NativeMethods.MONITORINFOEX>()
                };

                if (NativeMethods.GetMonitorInfo(handle, ref info))
                {
                    result.Add(new MonitorInfo(handle, info.rcMonitor, info.rcWork, (info.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0));
                }

                return true;
            }, IntPtr.Zero);

            if (result.Count == 0)
            {
                var fallback = new NativeMethods.RECT
                {
                    left = 0,
                    top = 0,
                    right = (int)SystemParameters.PrimaryScreenWidth,
                    bottom = (int)SystemParameters.PrimaryScreenHeight
                };
                result.Add(new MonitorInfo(IntPtr.Zero, fallback, fallback, true));
            }

            return result;
        }

        internal sealed record MonitorInfo(IntPtr Handle, NativeMethods.RECT Bounds, NativeMethods.RECT WorkArea, bool IsPrimary);
    }
}
