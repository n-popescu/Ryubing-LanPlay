using Ryujinx.Common.Logging;
using System;
using System.IO;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Vulkan.MoltenVK
{
    [SupportedOSPlatform("macos")]
    public static class MVKInitialization
    {
        public static void Initialize()
        {
            Environment.SetEnvironmentVariable("MVK_CONFIG_USE_METAL_ARGUMENT_BUFFERS", "1");
            Environment.SetEnvironmentVariable("MVK_CONFIG_VK_SEMAPHORE_SUPPORT_STYLE", "0");
            Environment.SetEnvironmentVariable("MVK_CONFIG_SYNCHRONOUS_QUEUE_SUBMITS", "0");
            Environment.SetEnvironmentVariable("MVK_CONFIG_RESUME_LOST_DEVICE", "1");

            string contentsDir = Path.GetDirectoryName(AppContext.BaseDirectory.TrimEnd('/'))!;
            string basePath = Path.Combine(contentsDir, "Resources");
            string icdPath = Path.Combine(basePath, "vulkan", "icd.d", "MoltenVK_icd.json");

            if (!File.Exists(icdPath))
                throw new FileNotFoundException("MoltenVK ICD JSON not found", icdPath);

            Logger.Notice.Print(LogClass.Application,
                $"MVKInitialization.Initialize() called, VK_DRIVER_FILES will be set to: {icdPath}");

            Environment.SetEnvironmentVariable("VK_DRIVER_FILES", icdPath);

            Console.WriteLine($"[MVKInit] VK_DRIVER_FILES just set to: {Environment.GetEnvironmentVariable("VK_DRIVER_FILES")}");
        }
    }
}
