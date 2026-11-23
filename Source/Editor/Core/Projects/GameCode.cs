using System.Reflection;
using System.Runtime.Loader;

using FlickerVoid.Engine.Runtime.Loop;

namespace FlickerVoid.Editor.Core.Projects;

/// <summary>
/// The project's code in the editor: the game modules of the built game and of the project assemblies it references, loaded
/// into a collectible context of their own from copies in memory, so the game can be rebuilt and loaded again while the editor
/// runs. Arch's component registry is static and keeps every type it has seen, so an unloaded game is never fully collected:
/// each reload leaks a little memory until the editor restarts, as Unreal's Live Coding does.
/// </summary>
public sealed class GameCode
{
    private readonly GameLoadContext _context;

    private GameCode(GameLoadContext context, IReadOnlyList<IGameModule> modules)
    {
        _context = context;
        Modules = modules;
    }

    /// <summary>A game module of each type the game's assemblies export.</summary>
    public IReadOnlyList<IGameModule> Modules { get; }

    /// <summary>Loads the built game at <paramref name="program"/> and creates each game module it holds.</summary>
    public static GameCode Load(string program)
    {
        string fullPath = Path.GetFullPath(program);
        GameLoadContext context = new(fullPath);
        Assembly game = context.LoadCopy(fullPath);
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

        return new GameCode(context, modules);
    }

    /// <summary>Lets the game's assemblies go once nothing uses their types; the editor must have dropped the game made with them first.</summary>
    public void Unload()
    {
        _context.Unload();
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
