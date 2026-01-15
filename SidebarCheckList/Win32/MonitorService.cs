using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using static SidebarChecklist.Win32.NativeMethods;

namespace SidebarChecklist.Win32
{
    internal sealed class MonitorInfo
    {
        public IntPtr Handle { get; init; }
        public bool IsPrimary { get; init; }
        public RECT WorkArea { get; init; }
        public RECT MonitorArea { get; init; }
        public string DeviceName { get; init; } = "";
    }

    internal sealed class MonitorService
    {
        public List<MonitorInfo> GetMonitors()
        {
            var list = new List<MonitorInfo>();

            // ★ラムダ引数を「すべて明示」(型も ref も)
            MonitorEnumProc proc = (IntPtr hMon, IntPtr hdcMon, ref RECT rcMon, IntPtr dwData) =>
            {
                var mi = new MONITORINFOEX
                {
                    cbSize = Marshal.SizeOf<MONITORINFOEX>()
                };

                if (GetMonitorInfo(hMon, ref mi))
                {
                    list.Add(new MonitorInfo
                    {
                        Handle = hMon,
                        IsPrimary = (mi.dwFlags & MONITORINFOF_PRIMARY) != 0,
                        WorkArea = mi.rcWork,
                        MonitorArea = mi.rcMonitor,
                        DeviceName = mi.szDevice ?? ""
                    });
                }

                return true;
            };

            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, proc, IntPtr.Zero);

            // 安定化：Primary優先で並べる
            return list
                .OrderByDescending(m => m.IsPrimary)
                .ThenBy(m => m.DeviceName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public bool HasSubMonitor()
        {
            var mons = GetMonitors();
            return mons.Count >= 2;
        }

        public MonitorInfo GetTarget(string targetMonitor)
        {
            var mons = GetMonitors();
            var main = mons.FirstOrDefault(m => m.IsPrimary) ?? mons.First();

            if (!string.Equals(targetMonitor, "sub", StringComparison.OrdinalIgnoreCase))
                return main;

            // sub: “メイン以外の1台目（＝2台目）”
            var sub = mons.FirstOrDefault(m => !m.IsPrimary);
            return sub ?? main;
        }
    }
}
