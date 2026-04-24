using Ryujinx.Common.Logging;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Ryujinx.Common.Helper
{
    public static partial class ConsoleHelper
    {
        [SupportedOSPlatform("windows")]
        [LibraryImport("kernel32")]
        private static partial nint GetConsoleWindow();

        [SupportedOSPlatform("windows")]
        [LibraryImport("user32")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool ShowWindow(nint hWnd, int nCmdShow);

        [SupportedOSPlatform("windows")]
        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool AllocConsole();

        [SupportedOSPlatform("windows")]
        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool FreeConsole();

        public static bool SetConsoleWindowStateSupported => OperatingSystem.IsWindows();

        public static void SetConsoleWindowState(bool show)
        {
            if (OperatingSystem.IsWindows())
            {
                SetConsoleWindowStateWindows(show);
            }
            else if (show == false)
            {
                Logger.Warning?.Print(LogClass.Application, "OS doesn't support hiding console window");
            }
        }

        [SupportedOSPlatform("windows")]
        private static void SetConsoleWindowStateWindows(bool show)
        {
            if (show)
            {
                EnsureConsoleAttached();
                Logger.SetConsoleTargetEnabled(true);

                nint hWnd = GetConsoleWindow();

                if (hWnd != nint.Zero)
                {
                    const int SW_SHOW = 5;
                    ShowWindow(hWnd, SW_SHOW);
                }

                return;
            }

            Logger.SetConsoleTargetEnabled(false);
            DetachConsole();
        }

        [SupportedOSPlatform("windows")]
        private static void EnsureConsoleAttached()
        {
            if (GetConsoleWindow() != nint.Zero)
            {
                return;
            }

            if (!AllocConsole())
            {
                Logger.Warning?.Print(LogClass.Application, "Attempted to allocate console window but the operation failed");
                return;
            }

            RebindConsoleStreams();
        }

        [SupportedOSPlatform("windows")]
        private static void DetachConsole()
        {
            if (GetConsoleWindow() == nint.Zero)
            {
                return;
            }

            if (!FreeConsole())
            {
                Logger.Warning?.Print(LogClass.Application, "Attempted to detach console window but the operation failed");
                return;
            }

            Console.SetIn(TextReader.Null);
            Console.SetOut(TextWriter.Null);
            Console.SetError(TextWriter.Null);
        }

        [SupportedOSPlatform("windows")]
        private static void RebindConsoleStreams()
        {
            StreamWriter stdout = new(Console.OpenStandardOutput())
            {
                AutoFlush = true,
            };

            StreamWriter stderr = new(Console.OpenStandardError())
            {
                AutoFlush = true,
            };

            Console.SetIn(new StreamReader(Console.OpenStandardInput()));
            Console.SetOut(stdout);
            Console.SetError(stderr);
        }
    }
}
