using Ryujinx.Common;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Shader;
using Ryujinx.Graphics.Shader.Translation;
using System;
using System.Text.RegularExpressions;

namespace Ryujinx.Graphics.Vulkan.Effects
{
    internal static partial class EffectShaderHelper
    {
        [GeneratedRegex(@"layout\(\s*rgba8\s*,\s*binding\s*=\s*0\s*,\s*set\s*=\s*3\s*\)\s*uniform\s+image2D\s+imgOutput\s*;", RegexOptions.CultureInvariant)]
        private static partial Regex OutputImageRegex();

        public static ShaderSource CreateComputeShader(string shaderName, bool floatImageOutputs)
        {
            if (!floatImageOutputs)
            {
                byte[] spirv = EmbeddedResources.Read($"Ryujinx.Graphics.Vulkan/Effects/Shaders/{shaderName}.spv");
                return new ShaderSource(spirv, ShaderStage.Compute, TargetLanguage.Spirv);
            }

            string glsl = EmbeddedResources.ReadAllText($"Ryujinx.Graphics.Vulkan/Effects/Shaders/{shaderName}.glsl");
            string rewritten = OutputImageRegex().Replace(glsl, "layout(rgba16f, binding = 0, set = 3) uniform image2D imgOutput;", 1);

            if (ReferenceEquals(glsl, rewritten) || glsl == rewritten)
            {
                throw new InvalidOperationException($"Shader '{shaderName}' does not declare the expected rgba8 imgOutput binding.");
            }

            return new ShaderSource(rewritten, ShaderStage.Compute, TargetLanguage.Glsl);
        }
    }
}