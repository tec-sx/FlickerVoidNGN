// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See THIRD-PARTY-NOTICES.md for more information.
//
// Copyright (c) 2010-2013 SharpDX - Alexandre Mutel
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

namespace FlickerVoid.Engine.Runtime.Services;

/// <summary>Services kept in a locked dictionary, so systems on any thread can use them; adapted from Stride's ServiceRegistry.</summary>
public sealed class ServiceRegistry : IServiceRegistry
{
    private readonly Dictionary<Type, object> _services = [];

    public T? GetService<T>()
        where T : class
    {
        object? service = GetService(typeof(T));
        return (T?)service;
    }

    public object? GetService(Type serviceType)
    {
        lock (_services)
        {
            return _services.GetValueOrDefault(serviceType);
        }
    }

    public void AddService<T>(T service)
        where T : class
    {
        lock (_services)
        {
            if (!_services.TryAdd(typeof(T), service))
            {
                throw new ArgumentException($"A {typeof(T).Name} service is already registered.", nameof(service));
            }
        }
    }

    public bool RemoveService<T>()
        where T : class
    {
        lock (_services)
        {
            return _services.Remove(typeof(T));
        }
    }
}
