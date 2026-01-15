using System;
using System.Threading;
using System.Windows;

namespace SidebarChecklist
{
    public partial class App : Application
    {
        private Mutex? _mutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            // 二重起動禁止（既存インスタンス優先：新規は起動しない）
            _mutex = new Mutex(true, "SidebarChecklist.SingletonMutex.v1", out bool createdNew);
            if (!createdNew)
            {
                Shutdown();
                return;
            }

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
            base.OnExit(e);
        }
    }
}
