using System.Text;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D.Compilers;
using DxcBuffer = Silk.NET.Direct3D.Compilers.Buffer;

namespace FlickerVoid.Engine.Shaders;

/// <summary>Compiles HLSL with DXC to DXIL (signed, so Direct3D 12 loads it) or SPIR-V. Not thread-safe; use one per thread.</summary>
public sealed unsafe class DxcShaderCompiler : IShaderCompiler, IDisposable
{
    private const string ShaderModel = "6_0";
    private const string VulkanVersion = "vulkan1.3";

    /// <summary>CLSID_DxcCompiler from dxcapi.h; Silk.NET doesn't define the class ids.</summary>
    private static readonly Guid CompilerClassId = new("73e22d93-e6ce-47f3-b5bf-f0664f39c1b0");

    private readonly DXC _dxc;
    private ComPtr<IDxcCompiler3> _compiler;

    public DxcShaderCompiler()
    {
        _dxc = DXC.GetApi();
        Guid classId = CompilerClassId;
        _compiler = _dxc.CreateInstance<IDxcCompiler3>(ref classId);
    }

    public ShaderCompileResult Compile(ShaderSource source, ShaderTarget target)
    {
        List<string> arguments = BuildArguments(source, target);
        using NativeWideStrings wideArguments = new(arguments);
        byte[] code = Encoding.UTF8.GetBytes(source.Code);

        fixed (byte* codePointer = code)
        {
            DxcBuffer buffer = new() { Ptr = codePointer, Size = (nuint)code.Length, Encoding = DXC.CPUtf8 };
            Guid resultId = IDxcResult.Guid;
            IDxcResult* result = null;
            int hr = _compiler.Get().Compile(&buffer, wideArguments.Pointers, wideArguments.Count, (IDxcIncludeHandler*)null, &resultId, (void**)&result);
            SilkMarshal.ThrowHResult(hr);

            try
            {
                return ReadResult(result);
            }
            finally
            {
                result->Release();
            }
        }
    }

    public void Dispose()
    {
        _compiler.Dispose();
        _dxc.Dispose();
    }

    private static List<string> BuildArguments(ShaderSource source, ShaderTarget target)
    {
        string profile = GetProfile(source.Stage);
        List<string> arguments = [source.Name, "-E", source.EntryPoint, "-T", profile];
        foreach (string define in source.Defines)
        {
            arguments.Add("-D");
            arguments.Add(define);
        }

        if (target == ShaderTarget.Spirv)
        {
            arguments.Add("-spirv");
            arguments.Add($"-fspv-target-env={VulkanVersion}");
        }

        return arguments;
    }

    private static string GetProfile(ShaderStage stage)
    {
        switch (stage)
        {
            case ShaderStage.Vertex:
                return $"vs_{ShaderModel}";
            case ShaderStage.Pixel:
                return $"ps_{ShaderModel}";
            case ShaderStage.Compute:
                return $"cs_{ShaderModel}";
            default:
                throw new ArgumentOutOfRangeException(nameof(stage), stage, null);
        }
    }

    private static ShaderCompileResult ReadResult(IDxcResult* result)
    {
        int status;
        SilkMarshal.ThrowHResult(result->GetStatus(&status));
        string messages = ReadMessages(result);
        if (status < 0)
        {
            return new ShaderCompileResult(false, [], messages);
        }

        IDxcBlob* blob = null;
        SilkMarshal.ThrowHResult(result->GetResult(&blob));
        try
        {
            byte[] bytecode = ReadBytes(blob->GetBufferPointer(), blob->GetBufferSize());
            return new ShaderCompileResult(true, bytecode, messages);
        }
        finally
        {
            blob->Release();
        }
    }

    /// <summary>Errors and warnings, which IDxcCompiler3 writes as UTF-8.</summary>
    private static string ReadMessages(IDxcResult* result)
    {
        IDxcBlobEncoding* errors = null;
        SilkMarshal.ThrowHResult(result->GetErrorBuffer(&errors));
        if (errors is null)
        {
            return string.Empty;
        }

        try
        {
            byte[] bytes = ReadBytes(errors->GetBufferPointer(), errors->GetBufferSize());
            string text = Encoding.UTF8.GetString(bytes);
            return text.TrimEnd('\0');
        }
        finally
        {
            errors->Release();
        }
    }

    private static byte[] ReadBytes(void* pointer, nuint size)
    {
        ReadOnlySpan<byte> bytes = new(pointer, (int)size);
        return bytes.ToArray();
    }
}
