using Ryujinx.Common;
using Ryujinx.Common.Configuration;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Shader;
using Ryujinx.Graphics.Shader.Translation;
using Silk.NET.Vulkan;
using System;
using SamplerCreateInfo = Ryujinx.Graphics.GAL.SamplerCreateInfo;

namespace Ryujinx.Graphics.Vulkan.Effects
{
    internal class FxaaPostProcessingEffect : IPostProcessingEffect
    {
        private readonly VulkanRenderer _renderer;
        private ISampler _samplerLinear;
        private ShaderCollection _shaderProgram;

        private readonly PipelineHelperShader _pipeline;
        private TextureView _texture;
        private bool _useFloatImageOutputs;
        public FxaaPostProcessingEffect(VulkanRenderer renderer, Device device)
        {
            _renderer = renderer;
            _pipeline = new PipelineHelperShader(renderer, device);

            Initialize();
        }

        public void Dispose()
        {
            _shaderProgram.Dispose();
            _pipeline.Dispose();
            _samplerLinear.Dispose();
            _texture?.Dispose();
        }

        private void Initialize()
        {
            _pipeline.Initialize();

            _samplerLinear = _renderer.CreateSampler(SamplerCreateInfo.Create(MinFilter.Linear, MagFilter.Linear));

            RecreateShaderProgram();
        }

        private void RecreateShaderProgram()
        {
            _useFloatImageOutputs = GraphicsConfigurationState.EnableVulkanFloatPresentation;
            _shaderProgram?.Dispose();

            ResourceLayout resourceLayout = new ResourceLayoutBuilder()
                .Add(ResourceStages.Compute, ResourceType.UniformBuffer, 2)
                .Add(ResourceStages.Compute, ResourceType.TextureAndSampler, 1)
                .Add(ResourceStages.Compute, ResourceType.Image, 0, true).Build();

            _shaderProgram = _renderer.CreateProgramWithMinimalLayout([
                EffectShaderHelper.CreateComputeShader("Fxaa", _useFloatImageOutputs)
            ], resourceLayout);
        }

        public TextureView Run(TextureView view, CommandBufferScoped cbs, int width, int height)
        {
            if (_useFloatImageOutputs != GraphicsConfigurationState.EnableVulkanFloatPresentation)
            {
                RecreateShaderProgram();
            }

            Ryujinx.Graphics.GAL.Format outputFormat = GraphicsConfigurationState.EnableVulkanFloatPresentation
                ? Ryujinx.Graphics.GAL.Format.R16G16B16A16Float
                : view.Info.Format;
            int outputBpp = outputFormat == Ryujinx.Graphics.GAL.Format.R16G16B16A16Float ? 8 : view.Info.BytesPerPixel;

            if (_texture == null || _texture.Width != view.Width || _texture.Height != view.Height || _texture.Info.Format != outputFormat)
            {
                TextureCreateInfo viewInfo = view.Info;
                TextureCreateInfo textureInfo = TextureStorage.NewCreateInfoWith(ref viewInfo, outputFormat, outputBpp);

                _texture?.Dispose();
                _texture = _renderer.CreateTexture(textureInfo) as TextureView;
                _texture?.SetDebugName("Vulkan.Present.FxaaOutput");
            }

            _pipeline.SetCommandBuffer(cbs);
            _pipeline.SetProgram(_shaderProgram);
            _pipeline.SetTextureAndSampler(ShaderStage.Compute, 1, view, _samplerLinear);

            ReadOnlySpan<float> resolutionBuffer = [view.Width, view.Height];
            int rangeSize = resolutionBuffer.Length * sizeof(float);
            using ScopedTemporaryBuffer buffer = _renderer.BufferManager.ReserveOrCreate(_renderer, cbs, rangeSize);

            buffer.Holder.SetDataUnchecked(buffer.Offset, resolutionBuffer);

            _pipeline.SetUniformBuffers([new BufferAssignment(2, buffer.Range)]);

            int dispatchX = BitUtils.DivRoundUp(view.Width, IPostProcessingEffect.LocalGroupSize);
            int dispatchY = BitUtils.DivRoundUp(view.Height, IPostProcessingEffect.LocalGroupSize);

            _pipeline.SetImage(ShaderStage.Compute, 0, _texture.GetView(FormatTable.ConvertRgba8SrgbToUnorm(_texture.Info.Format)));
            _pipeline.DispatchCompute(dispatchX, dispatchY, 1);

            _pipeline.ComputeBarrier();

            _pipeline.Finish();

            return _texture;
        }
    }
}
