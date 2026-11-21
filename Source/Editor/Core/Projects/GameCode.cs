using System.Reflection;
using System.Runtime.Loader;

using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Editor.Core.Projects;

/// <summary>
/// The project's code in the editor: the game modules of the built game and of the project assemblies it references. The game is
/// loaded once per editor run and stays loaded, because Arch keeps every component type it has seen; a rebuilt game needs the
/// project opened again.
/// </summary>
public static class GameCode
{
    /// <summary>Loads the built game at <paramref name="program"/> and creates each game module it holds.</summary>
    public static IReadOnlyList<IGameModule> Load(string program)
    {
        string fullPath = Path.GetFullPath(program);
        GameLoadContext context = new(fullPath);
        Assembly game = context.LoadFromAssemblyPath(fullPath);
        List<IGameModule> modules = [];
        HashSet<string> seen = [];
        Queue<Assembly> pending = new([game]);
        while (pending.TryDequeue(out Assembly? assembly))
        {
            AddModules(assembly, modules);
            foreach (AssemblyName reference in assembly.GetReferencedAssemblies())
            {
                if (!seen.Add(reference.FullName))
                {
                    continue;
                }

                Assembly loaded = context.LoadFromAssemblyName(reference);
                if (AssemblyLoadContext.GetLoadContext(loaded) == context)
                {
                    pending.Enqueue(loaded);
                }
            }
        }

        return modules;
    }

    private static void AddModules(Assembly assembly, List<IGameModule> modules)
    {
        foreach (Type type in assembly.GetExportedTypes())
        {
            if (type.IsAbstract || !typeof(IGameModule).IsAssignableFrom(type))
            {
                continue;
            }

            IGameModule module = (IGameModule)Activator.CreateInstance(type)!;
            modules.Add(module);
        }
    }
}
