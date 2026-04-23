using Ryujinx.Common;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Shader;
using Ryujinx.Graphics.Shader.Translation;
using Silk.NET.Vulkan;
using System;
using Extent2D = Ryujinx.Graphics.GAL.Extents2D;
using SamplerCreateInfo = Ryujinx.Graphics.GAL.SamplerCreateInfo;

namespace Ryujinx.Graphics.Vulkan.Effects
{
    internal class AreaScalingFilter : IScalingFilter
    {
        private readonly VulkanRenderer _renderer;
        private PipelineHelperShader _pipeline;
        private ISampler _sampler;
        private ShaderCollection _scalingProgram;
        private Device _device;
        private TextureView _outputTexture;
        private bool _useFloatImageOutputs;

        public float Level { get; set; }

        public AreaScalingFilter(VulkanRenderer renderer, Device device)
        {
            _device = device;
            _renderer = renderer;

            Initialize();
        }

        public void Dispose()
        {
            _pipeline.Dispose();
            _scalingProgram.Dispose();
            _sampler.Dispose();
            _outputTexture?.Dispose();
        }

        public void Initialize()
        {
            _pipeline = new PipelineHelperShader(_renderer, _device);
            _pipeline.Initialize();

            ResourceLayout scalingResourceLayout = new ResourceLayoutBuilder()
                .Add(ResourceStages.Compute, ResourceType.UniformBuffer, 2)
                .Add(ResourceStages.Compute, ResourceType.TextureAndSampler, 1)
                .Add(ResourceStages.Compute, ResourceType.Image, 0, true).Build();

            _sampler = _renderer.CreateSampler(SamplerCreateInfo.Create(MinFilter.Linear, MagFilter.Linear));

            RecreateShaders(false, scalingResourceLayout);
        }

        private void RecreateShaders(bool useFloatImageOutputs, ResourceLayout scalingResourceLayout)
        {
            _useFloatImageOutputs = useFloatImageOutputs;
            _scalingProgram?.Dispose();

            _scalingProgram = _renderer.CreateProgramWithMinimalLayout([
                EffectShaderHelper.CreateComputeShader("AreaScaling", useFloatImageOutputs)
            ], scalingResourceLayout);
        }

        public TextureView Run(
            TextureView view,
            CommandBufferScoped cbs,
            Ryujinx.Graphics.GAL.Format outputFormat,
            int outputBpp,
            int width,
            int height,
            Extent2D source,
            Extent2D destination)
        {
            bool useFloatImageOutputs = outputFormat == Ryujinx.Graphics.GAL.Format.R16G16B16A16Float;

            if (_useFloatImageOutputs != useFloatImageOutputs)
            {
                ResourceLayout scalingResourceLayout = new ResourceLayoutBuilder()
                    .Add(ResourceStages.Compute, ResourceType.UniformBuffer, 2)
                    .Add(ResourceStages.Compute, ResourceType.TextureAndSampler, 1)
                    .Add(ResourceStages.Compute, ResourceType.Image, 0, true).Build();

                RecreateShaders(useFloatImageOutputs, scalingResourceLayout);
            }

            if (_outputTexture == null || _outputTexture.Width != width || _outputTexture.Height != height || _outputTexture.Info.Format != outputFormat)
            {
                TextureCreateInfo viewInfo = view.Info;
                TextureCreateInfo outputInfo = TextureStorage.NewCreateInfoWith(ref viewInfo, outputFormat, outputBpp, width, height);

                _outputTexture?.Dispose();
                _outputTexture = _renderer.CreateTexture(outputInfo) as TextureView;
                _outputTexture?.SetDebugName("Vulkan.Present.AreaOutput");
            }

            _pipeline.SetCommandBuffer(cbs);
            _pipeline.SetProgram(_scalingProgram);
            _pipeline.SetTextureAndSampler(ShaderStage.Compute, 1, view, _sampler);

            ReadOnlySpan<float> dimensionsBuffer =
            [
                source.X1,
                source.X2,
                source.Y1,
                source.Y2,
                destination.X1,
                destination.X2,
                destination.Y1,
                destination.Y2
            ];

            int rangeSize = dimensionsBuffer.Length * sizeof(float);
            using ScopedTemporaryBuffer buffer = _renderer.BufferManager.ReserveOrCreate(_renderer, cbs, rangeSize);
            buffer.Holder.SetDataUnchecked(buffer.Offset, dimensionsBuffer);

            int threadGroupWorkRegionDim = 16;
            int dispatchX = (width + (threadGroupWorkRegionDim - 1)) / threadGroupWorkRegionDim;
            int dispatchY = (height + (threadGroupWorkRegionDim - 1)) / threadGroupWorkRegionDim;

            _pipeline.SetUniformBuffers([new BufferAssignment(2, buffer.Range)]);
            _pipeline.SetImage(ShaderStage.Compute, 0, _outputTexture.GetView(FormatTable.ConvertRgba8SrgbToUnorm(_outputTexture.Info.Format)));
            _pipeline.DispatchCompute(dispatchX, dispatchY, 1);
            _pipeline.ComputeBarrier();

            _pipeline.Finish();

            return _outputTexture;
        }
    }
}
