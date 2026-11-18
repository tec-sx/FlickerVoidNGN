namespace FlickerVoid.Engine.Graphics;

/// <summary>Compiled shader code for the device's API (DXIL or SPIR-V) and the entry point to run.</summary>
public sealed record ShaderBytecode(byte[] Code, string EntryPoint = "main");
