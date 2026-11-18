using System.Text;
using Silk.NET.Vulkan;

namespace FlickerVoid.Engine.Graphics.Vulkan;

/// <summary>A graphics pipeline on the bindless pipeline layout, built for dynamic rendering with dynamic viewport and scissor.</summary>
internal sealed unsafe class VulkanPipeline : IPipeline
{
    private readonly VulkanGraphicsDevice _device;
    private bool _disposed;

    public VulkanPipeline(VulkanGraphicsDevice device, GraphicsPipelineDescription description)
    {
        _device = device;
        Vk vk = device.Vk;
        ShaderModule vertexModule = CreateModule(description.VertexShader.Code);
        ShaderModule pixelModule = CreateModule(description.PixelShader.Code);
        byte[] vertexEntry = Encoding.UTF8.GetBytes(description.VertexShader.EntryPoint + '\0');
        byte[] pixelEntry = Encoding.UTF8.GetBytes(description.PixelShader.EntryPoint + '\0');

        try
        {
            fixed (byte* vertexName = vertexEntry)
            fixed (byte* pixelName = pixelEntry)
            {
                PipelineShaderStageCreateInfo* stages = stackalloc PipelineShaderStageCreateInfo[2];
                stages[0] = CreateStage(ShaderStageFlags.VertexBit, vertexModule, vertexName);
                stages[1] = CreateStage(ShaderStageFlags.FragmentBit, pixelModule, pixelName);
                Handle = CreatePipeline(description, stages);
            }
        }
        finally
        {
            vk.DestroyShaderModule(device.Handle, vertexModule, null);
            vk.DestroyShaderModule(device.Handle, pixelModule, null);
        }

        device.SetName(ObjectType.Pipeline, Handle.Handle, description.Name);
    }

    public Pipeline Handle { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Pipeline pipeline = Handle;
        VulkanGraphicsDevice device = _device;
        device.ReleaseLater(() => device.Vk.DestroyPipeline(device.Handle, pipeline, null));
    }

    private static PipelineShaderStageCreateInfo CreateStage(ShaderStageFlags stage, ShaderModule module, byte* entryPoint)
    {
        return new PipelineShaderStageCreateInfo
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = stage,
            Module = module,
            PName = entryPoint,
        };
    }

    private ShaderModule CreateModule(byte[] code)
    {
        fixed (byte* bytes = code)
        {
            ShaderModuleCreateInfo info = new()
            {
                SType = StructureType.ShaderModuleCreateInfo,
                CodeSize = (nuint)code.Length,
                PCode = (uint*)bytes,
            };
            VulkanResult.Check(_device.Vk.CreateShaderModule(_device.Handle, &info, null, out ShaderModule module));
            return module;
        }
    }

    private Pipeline CreatePipeline(GraphicsPipelineDescription description, PipelineShaderStageCreateInfo* stages)
    {
        int colorCount = description.ColorFormats.Count;
        Format* colorFormats = stackalloc Format[colorCount];
        PipelineColorBlendAttachmentState* blends = stackalloc PipelineColorBlendAttachmentState[colorCount];
        PipelineColorBlendAttachmentState blend = description.Blend.ToVulkan();
        for (int index = 0; index < colorCount; index++)
        {
            colorFormats[index] = description.ColorFormats[index].ToVulkan();
            blends[index] = blend;
        }

        Format depthFormat = description.DepthFormat.ToVulkan();
        Format stencilFormat = Format.Undefined;
        if (description.DepthFormat.HasStencil())
        {
            stencilFormat = depthFormat;
        }

        PipelineRenderingCreateInfo rendering = new()
        {
            SType = StructureType.PipelineRenderingCreateInfo,
            ColorAttachmentCount = (uint)colorCount,
            PColorAttachmentFormats = colorFormats,
            DepthAttachmentFormat = depthFormat,
            StencilAttachmentFormat = stencilFormat,
        };

        // Vertices come from bindless buffers, so there is no vertex input.
        PipelineVertexInputStateCreateInfo vertexInput = new() { SType = StructureType.PipelineVertexInputStateCreateInfo };
        PipelineInputAssemblyStateCreateInfo inputAssembly = new()
        {
            SType = StructureType.PipelineInputAssemblyStateCreateInfo,
            Topology = description.Topology.ToVulkan(),
        };
        PipelineViewportStateCreateInfo viewport = new()
        {
            SType = StructureType.PipelineViewportStateCreateInfo,
            ViewportCount = 1,
            ScissorCount = 1,
        };

        // The viewport is flipped (VulkanCommandList.SetViewport), so the image and its winding match Direct3D's: front faces are clockwise.
        PipelineRasterizationStateCreateInfo rasterization = new()
        {
            SType = StructureType.PipelineRasterizationStateCreateInfo,
            PolygonMode = PolygonMode.Fill,
            CullMode = description.CullMode.ToVulkan(),
            FrontFace = FrontFace.Clockwise,
            LineWidth = 1,
        };
        PipelineMultisampleStateCreateInfo multisample = new()
        {
            SType = StructureType.PipelineMultisampleStateCreateInfo,
            RasterizationSamples = SampleCountFlags.Count1Bit,
        };
        PipelineDepthStencilStateCreateInfo depth = new()
        {
            SType = StructureType.PipelineDepthStencilStateCreateInfo,
            DepthTestEnable = description.Depth.Test,
            DepthWriteEnable = description.Depth.Write,
            DepthCompareOp = description.Depth.Compare.ToVulkan(),
        };
        PipelineColorBlendStateCreateInfo colorBlend = new()
        {
            SType = StructureType.PipelineColorBlendStateCreateInfo,
            AttachmentCount = (uint)colorCount,
            PAttachments = blends,
        };
        DynamicState* dynamicStates = stackalloc DynamicState[] { DynamicState.Viewport, DynamicState.Scissor };
        PipelineDynamicStateCreateInfo dynamic = new()
        {
            SType = StructureType.PipelineDynamicStateCreateInfo,
            DynamicStateCount = 2,
            PDynamicStates = dynamicStates,
        };

        GraphicsPipelineCreateInfo info = new()
        {
            SType = StructureType.GraphicsPipelineCreateInfo,
            PNext = &rendering,
            StageCount = 2,
            PStages = stages,
            PVertexInputState = &vertexInput,
            PInputAssemblyState = &inputAssembly,
            PViewportState = &viewport,
            PRasterizationState = &rasterization,
            PMultisampleState = &multisample,
            PDepthStencilState = &depth,
            PColorBlendState = &colorBlend,
            PDynamicState = &dynamic,
            Layout = _device.Bindless.PipelineLayout,
        };
        VulkanResult.Check(_device.Vk.CreateGraphicsPipelines(_device.Handle, default, 1, &info, null, out Pipeline pipeline));
        return pipeline;
    }
}
