using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Ryujinx.Ava.UI.Helpers
{
    /// <summary>
    /// Minimal Objective-C bridge for tweaking the macOS application (Apple) menu after
    /// Avalonia has built it. Avalonia 11.x's <c>AvaloniaNativeMenuExporter</c> hardcodes
    /// the application-menu "Quit" title to the literal string "Quit" instead of the
    /// macOS-conventional "Quit AppName"; until that's fixed upstream we patch the
    /// item directly through <c>NSApp.mainMenu</c>.
    /// </summary>
    [SupportedOSPlatform("macos")]
    internal static class AppleMenu
    {
        private const string LibObjC = "/usr/lib/libobjc.dylib";

        [DllImport(LibObjC, EntryPoint = "objc_getClass")]
        private static extern IntPtr GetClass([MarshalAs(UnmanagedType.LPStr)] string name);

        [DllImport(LibObjC, EntryPoint = "sel_registerName")]
        private static extern IntPtr GetSelector([MarshalAs(UnmanagedType.LPStr)] string name);

        [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
        private static extern IntPtr SendIntPtr(IntPtr receiver, IntPtr selector);

        [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
        private static extern IntPtr SendIntPtr_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg);

        [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
        private static extern IntPtr SendIntPtr_NInt(IntPtr receiver, IntPtr selector, nint arg);

        [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
        private static extern nint SendNInt(IntPtr receiver, IntPtr selector);

        [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
        private static extern void SendVoid_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg);

        [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
        private static extern bool SendBool_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg);

        public static void RenameQuitItem(string newTitle)
        {
            try
            {
                IntPtr nsApp = SendIntPtr(GetClass("NSApplication"), GetSelector("sharedApplication"));
                if (nsApp == IntPtr.Zero)
                    return;

                IntPtr mainMenu = SendIntPtr(nsApp, GetSelector("mainMenu"));
                if (mainMenu == IntPtr.Zero)
                    return;

                // The application menu (the bold "Ryujinx" / "Ryujinx Canary" entry next
                // to the Apple logo) is always the first submenu of the main menu.
                IntPtr appMenuItem = SendIntPtr_NInt(mainMenu, GetSelector("itemAtIndex:"), 0);
                if (appMenuItem == IntPtr.Zero)
                    return;

                IntPtr appMenu = SendIntPtr(appMenuItem, GetSelector("submenu"));
                if (appMenu == IntPtr.Zero)
                    return;

                // Iterate items and find the one whose title is exactly "Quit". Avalonia's
                // NSMenuItem uses a custom action selector (not terminate:), so we match
                // by the hardcoded title rather than the action.
                nint count = SendNInt(appMenu, GetSelector("numberOfItems"));
                IntPtr getItemSel = GetSelector("itemAtIndex:");
                IntPtr titleSel = GetSelector("title");
                IntPtr setTitleSel = GetSelector("setTitle:");
                IntPtr quitNs = NSStringFrom("Quit");
                IntPtr isEqualSel = GetSelector("isEqualToString:");

                for (nint i = 0; i < count; i++)
                {
                    IntPtr item = SendIntPtr_NInt(appMenu, getItemSel, i);
                    if (item == IntPtr.Zero)
                        continue;
                    IntPtr currentTitle = SendIntPtr(item, titleSel);
                    if (currentTitle == IntPtr.Zero)
                        continue;
                    if (SendBool_IntPtr(currentTitle, isEqualSel, quitNs))
                    {
                        IntPtr nsTitle = NSStringFrom(newTitle);
                        if (nsTitle != IntPtr.Zero)
                            SendVoid_IntPtr(item, setTitleSel, nsTitle);
                        return;
                    }
                }
            }
            catch
            {
                // Best-effort cosmetic patch. Never let a rename failure crash the app.
            }
        }

        private static IntPtr NSStringFrom(string value)
        {
            IntPtr utf8 = Marshal.StringToCoTaskMemUTF8(value);
            try
            {
                return SendIntPtr_IntPtr(
                    GetClass("NSString"),
                    GetSelector("stringWithUTF8String:"),
                    utf8);
            }
            finally
            {
                Marshal.FreeCoTaskMem(utf8);
            }
        }
    }
}
