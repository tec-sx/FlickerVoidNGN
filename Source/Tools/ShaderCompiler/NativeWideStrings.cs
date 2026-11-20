using System.Runtime.InteropServices;
using System.Text;

namespace FlickerVoid.Tools.ShaderCompiler;

/// <summary>
/// Null-terminated wchar_t strings in native memory: UTF-16 on Windows, UTF-32 elsewhere.
/// Silk.NET types DXC's wide strings as UTF-16 char pointers, which Linux builds of DXC misread.
/// </summary>
internal sealed unsafe class NativeWideStrings : IDisposable
{
    private readonly char*[] _pointers;
    private readonly GCHandle _handle;

    public NativeWideStrings(IReadOnlyList<string> values)
    {
        Encoding encoding = Encoding.UTF32;
        if (OperatingSystem.IsWindows())
        {
            encoding = Encoding.Unicode;
        }

        _pointers = new char*[values.Count];
        for (int index = 0; index < values.Count; index++)
        {
            byte[] bytes = encoding.GetBytes(values[index] + '\0');
            void* memory = NativeMemory.Alloc((nuint)bytes.Length);
            Marshal.Copy(bytes, 0, (nint)memory, bytes.Length);
            _pointers[index] = (char*)memory;
        }

        _handle = GCHandle.Alloc(_pointers, GCHandleType.Pinned);
    }

    public char** Pointers => (char**)_handle.AddrOfPinnedObject();

    public uint Count => (uint)_pointers.Length;

    public void Dispose()
    {
        _handle.Free();
        foreach (char* pointer in _pointers)
        {
            NativeMemory.Free(pointer);
        }
    }
}
