using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

using FlickerVoid.Engine.Graphics.AccelerationStructures;
using FlickerVoid.Engine.Graphics.Buffers;
using FlickerVoid.Engine.Graphics.Resources;
using FlickerVoid.Engine.Graphics.Vulkan.Buffers;
using FlickerVoid.Engine.Graphics.Vulkan.Devices;

namespace FlickerVoid.Engine.Graphics.Vulkan.AccelerationStructures;

/// <summary>
/// A Vulkan acceleration structure in a buffer of its own, sized for its triangles or instances, with the scratch its builds and
/// refits need; top-level structures take a bindless slot shaders trace against.
/// </summary>
internal sealed unsafe class VulkanAccelerationStructure : IAccelerationStructure
{
    public const BuildAccelerationStructureFlagsKHR TopLevelFlags = BuildAccelerationStructureFlagsKHR.PreferFastTraceBitKhr;

    private readonly VulkanGraphicsDevice _device;
    private readonly VulkanBuffer _storage;
    private bool _disposed;

    private VulkanAccelerationStructure(VulkanGraphicsDevice device, AccelerationStructureTypeKHR type, in AccelerationStructureBuildSizesInfoKHR sizes, string? name)
    {
        _device = device;
        Type = type;
        BuildScratchSize = sizes.BuildScratchSize;
        UpdateScratchSize = sizes.UpdateScratchSize;
        BufferDescription storageDescription = new((long)sizes.AccelerationStructureSize, BufferUsage.None) { Name = name };
        _storage = new VulkanBuffer(device, storageDescription, BufferUsageFlags.AccelerationStructureStorageBitKhr | BufferUsageFlags.ShaderDeviceAddressBit);
        KhrAccelerationStructure extension = device.RequireAccelerationStructures();
        AccelerationStructureCreateInfoKHR info = new()
        {
            SType = StructureType.AccelerationStructureCreateInfoKhr,
            Buffer = _storage.Handle,
            Offset = 0,
            Size = sizes.AccelerationStructureSize,
            Type = type,
        };
        VulkanResult.Check(extension.CreateAccelerationStructure(device.Handle, &info, null, out AccelerationStructureKHR handle));
        Handle = handle;
        AccelerationStructureDeviceAddressInfoKHR addressInfo = new()
        {
            SType = StructureType.AccelerationStructureDeviceAddressInfoKhr,
            AccelerationStructure = handle,
        };
        Address = extension.GetAccelerationStructureDeviceAddress(device.Handle, &addressInfo);
        if (type == AccelerationStructureTypeKHR.TopLevelKhr)
        {
            BindlessIndex = device.Bindless.AccelerationStructures!.Allocate();
            device.Bindless.WriteAccelerationStructure(BindlessIndex, handle);
        }

        device.SetName(ObjectType.AccelerationStructureKhr, handle.Handle, name);
    }

    public AccelerationStructureKHR Handle { get; }

    public AccelerationStructureTypeKHR Type { get; }

    public uint BindlessIndex { get; }

    public ulong Address { get; }

    /// <summary>The scratch memory a build takes.</summary>
    public ulong BuildScratchSize { get; }

    /// <summary>The scratch memory a refit takes.</summary>
    public ulong UpdateScratchSize { get; }

    /// <summary>The triangles' vertices or the instances it was sized for; builds may take fewer.</summary>
    public int VertexCount { get; private init; }

    public int IndexCount { get; private init; }

    public int InstanceCapacity { get; private init; }

    public bool Refittable { get; private init; }

    /// <summary>Whether a build was recorded, so a refit has something to refit.</summary>
    public bool Built { get; set; }

    public static VulkanAccelerationStructure CreateBottomLevel(VulkanGraphicsDevice device, in BottomLevelDescription description)
    {
        if (description.VertexCount <= 0 || description.IndexCount <= 0 || description.IndexCount % 3 != 0)
        {
            throw new ArgumentException($"A bottom-level structure holds whole triangles; got {description.VertexCount} vertices and {description.IndexCount} indices.", nameof(description));
        }

        AccelerationStructureGeometryKHR geometry = DescribeTriangles(default, description.VertexCount, 0, default);
        AccelerationStructureBuildGeometryInfoKHR info = new()
        {
            SType = StructureType.AccelerationStructureBuildGeometryInfoKhr,
            Type = AccelerationStructureTypeKHR.BottomLevelKhr,
            Flags = GetBottomLevelFlags(description.Refittable),
            Mode = BuildAccelerationStructureModeKHR.BuildKhr,
            GeometryCount = 1,
            PGeometries = &geometry,
        };
        uint triangles = (uint)(description.IndexCount / 3);
        AccelerationStructureBuildSizesInfoKHR sizes = Measure(device, info, triangles);
        return new VulkanAccelerationStructure(device, AccelerationStructureTypeKHR.BottomLevelKhr, sizes, description.Name)
        {
            VertexCount = description.VertexCount,
            IndexCount = description.IndexCount,
            Refittable = description.Refittable,
        };
    }

    public static VulkanAccelerationStructure CreateTopLevel(VulkanGraphicsDevice device, in TopLevelDescription description)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(description.InstanceCapacity);
        AccelerationStructureGeometryKHR geometry = DescribeInstances(0);
        AccelerationStructureBuildGeometryInfoKHR info = new()
        {
            SType = StructureType.AccelerationStructureBuildGeometryInfoKhr,
            Type = AccelerationStructureTypeKHR.TopLevelKhr,
            Flags = TopLevelFlags,
            Mode = BuildAccelerationStructureModeKHR.BuildKhr,
            GeometryCount = 1,
            PGeometries = &geometry,
        };
        AccelerationStructureBuildSizesInfoKHR sizes = Measure(device, info, (uint)description.InstanceCapacity);
        return new VulkanAccelerationStructure(device, AccelerationStructureTypeKHR.TopLevelKhr, sizes, description.Name)
        {
            InstanceCapacity = description.InstanceCapacity,
        };
    }

    public static BuildAccelerationStructureFlagsKHR GetBottomLevelFlags(bool refittable)
    {
        if (refittable)
        {
            return BuildAccelerationStructureFlagsKHR.PreferFastTraceBitKhr | BuildAccelerationStructureFlagsKHR.AllowUpdateBitKhr;
        }

        return BuildAccelerationStructureFlagsKHR.PreferFastTraceBitKhr;
    }

    /// <summary>Opaque triangles: float3 positions at the start of each vertex and 32-bit indices, at those addresses.</summary>
    public static AccelerationStructureGeometryKHR DescribeTriangles(ulong vertices, int vertexCount, int vertexStride, ulong indices)
    {
        AccelerationStructureGeometryTrianglesDataKHR triangles = new()
        {
            SType = StructureType.AccelerationStructureGeometryTrianglesDataKhr,
            VertexFormat = Format.R32G32B32Sfloat,
            VertexData = new DeviceOrHostAddressConstKHR { DeviceAddress = vertices },
            VertexStride = (ulong)vertexStride,
            MaxVertex = (uint)(vertexCount - 1),
            IndexType = IndexType.Uint32,
            IndexData = new DeviceOrHostAddressConstKHR { DeviceAddress = indices },
        };
        return new AccelerationStructureGeometryKHR
        {
            SType = StructureType.AccelerationStructureGeometryKhr,
            GeometryType = GeometryTypeKHR.TrianglesKhr,
            Geometry = new AccelerationStructureGeometryDataKHR { Triangles = triangles },
            Flags = GeometryFlagsKHR.OpaqueBitKhr,
        };
    }

    /// <summary>Instances packed one after another at that address.</summary>
    public static AccelerationStructureGeometryKHR DescribeInstances(ulong instances)
    {
        AccelerationStructureGeometryInstancesDataKHR data = new()
        {
            SType = StructureType.AccelerationStructureGeometryInstancesDataKhr,
            ArrayOfPointers = false,
            Data = new DeviceOrHostAddressConstKHR { DeviceAddress = instances },
        };
        return new AccelerationStructureGeometryKHR
        {
            SType = StructureType.AccelerationStructureGeometryKhr,
            GeometryType = GeometryTypeKHR.InstancesKhr,
            Geometry = new AccelerationStructureGeometryDataKHR { Instances = data },
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        AccelerationStructureKHR handle = Handle;
        uint index = BindlessIndex;
        VulkanGraphicsDevice device = _device;
        device.ReleaseLater(() =>
        {
            device.RequireAccelerationStructures().DestroyAccelerationStructure(device.Handle, handle, null);
            device.Bindless.AccelerationStructures?.Free(index);
        });
        _storage.Dispose();
    }

    private static AccelerationStructureBuildSizesInfoKHR Measure(VulkanGraphicsDevice device, in AccelerationStructureBuildGeometryInfoKHR info, uint primitives)
    {
        AccelerationStructureBuildSizesInfoKHR sizes = new() { SType = StructureType.AccelerationStructureBuildSizesInfoKhr };
        fixed (AccelerationStructureBuildGeometryInfoKHR* infoPointer = &info)
        {
            device.RequireAccelerationStructures().GetAccelerationStructureBuildSizes(device.Handle, AccelerationStructureBuildTypeKHR.DeviceKhr, infoPointer, &primitives, &sizes);
        }

        return sizes;
    }
}
