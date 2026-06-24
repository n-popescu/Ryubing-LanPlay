using Ryujinx.Common.Logging;
using System;
using System.IO;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Vulkan.KosmicKrisp
{
    [SupportedOSPlatform("macos")]
    public static class KKInitialization
    {
        public static void Initialize()
        {
            string contentsDir = Path.GetDirectoryName(AppContext.BaseDirectory.TrimEnd('/'))!;
            string basePath = Path.Combine(contentsDir, "Resources");
            string icdPath = Path.Combine(basePath, "vulkan", "icd.d", "libkosmickrisp_icd.json");

            if (!File.Exists(icdPath))
                throw new FileNotFoundException("KosmicKrisp ICD JSON not found", icdPath);

            Environment.SetEnvironmentVariable("VK_DRIVER_FILES", icdPath);

            Console.WriteLine($"[KKInit] VK_DRIVER_FILES just set to: {Environment.GetEnvironmentVariable("VK_DRIVER_FILES")}");
        }
    }
}
