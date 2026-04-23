namespace Ryujinx.Common.Configuration
{
    public static class GraphicsConfigurationState
    {
        /// <summary>
        /// Enables or disables the Vulkan RGBA16 presentation path.
        /// When disabled, Vulkan presentation falls back to the legacy RGBA8 path.
        /// </summary>
        public static bool EnableVulkanFloatPresentation { get; set; } = false;

        /// <summary>
        /// Indicates whether the active Vulkan swapchain is actually using the float presentation format.
        /// This can be false when float presentation is requested but the surface falls back to an 8-bit format.
        /// </summary>
        public static bool ActiveVulkanFloatPresentation { get; set; } = false;
    }
}
