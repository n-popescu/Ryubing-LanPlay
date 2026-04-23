using System;
using Extent2D = Ryujinx.Graphics.GAL.Extents2D;

namespace Ryujinx.Graphics.Vulkan.Effects
{
    internal interface IScalingFilter : IDisposable
    {
        float Level { get; set; }
        TextureView Run(
            TextureView view,
            CommandBufferScoped cbs,
            Ryujinx.Graphics.GAL.Format outputFormat,
            int outputBpp,
            int width,
            int height,
            Extent2D source,
            Extent2D destination);
    }
}
