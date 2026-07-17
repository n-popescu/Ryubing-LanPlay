using Ryujinx.Common.Logging;
using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Ryujinx.Common.SystemInterop
{
    public static partial class ForceDpiAware
    {
        // Windows
        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool SetProcessDPIAware();

        // X11
        private const string X11LibraryName = "libX11.so.6";

        [LibraryImport(X11LibraryName)]
        private static partial nint XOpenDisplay([MarshalAs(UnmanagedType.LPStr)] string display);

        [LibraryImport(X11LibraryName)]
        private static partial nint XGetDefault(nint display, [MarshalAs(UnmanagedType.LPStr)] string program, [MarshalAs(UnmanagedType.LPStr)] string option);

        [LibraryImport(X11LibraryName)]
        private static partial nint XDisplayWidth(nint display, int screenNumber);

        [LibraryImport(X11LibraryName)]
        private static partial nint XDisplayWidthMM(nint display, int screenNumber);

        [LibraryImport(X11LibraryName)]
        private static partial nint XCloseDisplay(nint display);
        
        // Wayland
        private const string WaylandLibraryName = "wayland-client";

        private const double StandardDpiScale = 96.0;
        private const double MaxScaleFactor = 1.25;

        /// <summary>
        /// Marks the application as DPI-Aware when running on the Windows operating system.
        /// </summary>
        public static void Windows()
        {
            // Make process DPI aware for proper window sizing on high-res screens.
            if (OperatingSystem.IsWindowsVersionAtLeast(6))
            {
                SetProcessDPIAware();
            }
        }

        public static double GetActualScaleFactor(bool useWayland)
        {
            double userDpiScale = 96.0;

            try
            {
                if (OperatingSystem.IsWindows())
                {
                    userDpiScale = GdiPlusHelper.GetDpiX(nint.Zero);
                }
                else if (OperatingSystem.IsLinux())
                {
                    string xdgSessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE")?.ToLower();
                    if (xdgSessionType is not null && xdgSessionType == "wayland" && useWayland)
                    {
                        // Check compositor
                        string compositor = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
                        if (compositor is null)
                        {
                            Logger.Warning?.Print(LogClass.Application, "Couldn't determine monitor DPI: Wayland display value is null");
                            return userDpiScale;
                        }
                        
                        Logger.Warning?.PrintMsg(LogClass.Application, "Wayland DPI support is currently not implemented.");
                        // TODO: Implement Wayland DPI scaling support.
                        
                    }
                    else if ((xdgSessionType is null or "x11") || (xdgSessionType is "wayland" && !useWayland))
                    {
                        nint display = XOpenDisplay(null);
                        string dpiString = Marshal.PtrToStringAnsi(XGetDefault(display, "Xft", "dpi"));
                        if (dpiString == null || !double.TryParse(dpiString, NumberStyles.Any, CultureInfo.InvariantCulture, out userDpiScale))
                        {
                            userDpiScale = XDisplayWidth(display, 0) * 25.4 / XDisplayWidthMM(display, 0);
                        }

                        _ = XCloseDisplay(display);
                    }
                    else
                    {
                        Logger.Warning?.Print(LogClass.Application, $"Couldn't determine monitor DPI: Unrecognised XDG_SESSION_TYPE: {xdgSessionType}");
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Warning?.Print(LogClass.Application, $"Couldn't determine monitor DPI: {e.Message}");
            }

            return userDpiScale;
        }

        public static double GetWindowScaleFactor(bool useWayland)
        {
            double userDpiScale = GetActualScaleFactor(useWayland);

            return Math.Min(userDpiScale / StandardDpiScale, MaxScaleFactor);
        }
    }
}
