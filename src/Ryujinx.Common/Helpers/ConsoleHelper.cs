using Ryujinx.Common.Logging;
using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;

namespace Ryujinx.Common.Helper
{
    public static partial class ConsoleHelper
    {
        private static int _windowStateRequestId;

        [SupportedOSPlatform("windows")]
        [LibraryImport("kernel32")]
        private static partial nint GetConsoleWindow();

        [SupportedOSPlatform("windows")]
        [LibraryImport("user32")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool ShowWindow(nint hWnd, int nCmdShow);

        [SupportedOSPlatform("windows")]
        [LibraryImport("user32")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool IsWindowVisible(nint hWnd);

        public static bool SetConsoleWindowStateSupported => OperatingSystem.IsWindows();

        public static void SetConsoleWindowState(bool show)
        {
            if (OperatingSystem.IsWindows())
            {
                SetConsoleWindowStateWindows(show, Interlocked.Increment(ref _windowStateRequestId));
            }
            else if (show == false)
            {
                Logger.Warning?.Print(LogClass.Application, "OS doesn't support hiding console window");
            }
        }

        [SupportedOSPlatform("windows")]
        private static void SetConsoleWindowStateWindows(bool show, int requestId)
        {
            if (TrySetConsoleWindowStateWindows(show))
            {
                return;
            }

            if (show)
            {
                Logger.Warning?.Print(LogClass.Application, "Attempted to show/hide console window but console window does not exist");
                return;
            }

            ThreadPool.QueueUserWorkItem(static state =>
            {
                int queuedRequestId = (int)state!;

                for (int attempt = 0; attempt < 10; attempt++)
                {
                    Thread.Sleep(50);

                    if (queuedRequestId != Volatile.Read(ref _windowStateRequestId))
                    {
                        return;
                    }

                    if (TrySetConsoleWindowStateWindows(false))
                    {
                        return;
                    }
                }

                if (queuedRequestId == Volatile.Read(ref _windowStateRequestId))
                {
                    Logger.Warning?.Print(LogClass.Application, "Attempted to hide console window but it did not become hidden");
                }
            }, requestId);
        }

        [SupportedOSPlatform("windows")]
        private static bool TrySetConsoleWindowStateWindows(bool show)
        {
            const int SW_HIDE = 0;
            const int SW_SHOW = 5;

            nint hWnd = GetConsoleWindow();

            if (hWnd == nint.Zero)
            {
                return false;
            }

            ShowWindow(hWnd, show ? SW_SHOW : SW_HIDE);

            return IsWindowVisible(hWnd) == show;
        }
    }
}
