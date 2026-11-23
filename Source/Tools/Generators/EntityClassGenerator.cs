using System.Collections.Immutable;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace FlickerVoid.Tools.Generators;

/// <summary>
/// Turns each <c>partial struct</c> marked <c>[EntityClass]</c> into a typed handle over an entity: its <c>partial ref</c> properties
/// return the entity's components, and it implements <c>IEntityClass</c> (its components, template, query and checks) for spawning.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class EntityClassGenerator : IIncrementalGenerator
{
    private const string AttributeName = "FlickerVoid.Engine.Runtime.Entities.EntityClassAttribute";
    private const string Category = "FlickerVoid";

    private static readonly DiagnosticDescriptor NotAComponent = new(
        "FVG001",
        "Entity class property is not a component",
        "The partial property '{0}' of entity class '{1}' must return a component struct by ref, with only a getter",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor NoComponents = new(
        "FVG002",
        "Entity class has no components",
        "The entity class '{0}' declares no components; give it a 'partial ref' property for each",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor NotTopLevel = new(
        "FVG003",
        "Entity class is nested or generic",
        "The entity class '{0}' must be a non-generic struct declared directly in a namespace",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<EntityClass> classes = context.SyntaxProvider.ForAttributeWithMetadataName(
            AttributeName,
            static (node, _) => node is StructDeclarationSyntax,
            static (attributed, _) => Read(attributed));
        context.RegisterSourceOutput(classes, static (output, found) => Write(output, found));
    }

    private static EntityClass Read(GeneratorAttributeSyntaxContext attributed)
    {
        INamedTypeSymbol type = (INamedTypeSymbol)attributed.TargetSymbol;
        string fullName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        Location location = attributed.TargetNode.GetLocation();
        if (type.ContainingType is not null || type.IsGenericType)
        {
            Problem nested = new(NotTopLevel, location, type.Name, "");
            return EntityClass.Failed(fullName, nested);
        }

        string? template = null;
        foreach (System.Collections.Generic.KeyValuePair<string, TypedConstant> argument in attributed.Attributes[0].NamedArguments)
        {
            if (argument.Key == "Template")
            {
                template = argument.Value.Value as string;
            }
        }

        ImmutableArray<Component>.Builder components = ImmutableArray.CreateBuilder<Component>();
        ImmutableArray<Problem>.Builder problems = ImmutableArray.CreateBuilder<Problem>();
        foreach (IPropertySymbol property in type.GetMembers().OfType<IPropertySymbol>())
        {
            if (!property.IsPartialDefinition || property.PartialImplementationPart is not null)
            {
                continue;
            }

            if (!property.ReturnsByRef || !property.Type.IsValueType || property.SetMethod is not null || property.IsStatic)
            {
                Location propertyLocation = property.Locations.FirstOrDefault() ?? location;
                problems.Add(new Problem(NotAComponent, propertyLocation, property.Name, type.Name));
                continue;
            }

            string componentType = property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            string accessibility = SyntaxFacts.GetText(property.DeclaredAccessibility);
            components.Add(new Component(property.Name, componentType, accessibility));
        }

        if (components.Count == 0 && problems.Count == 0)
        {
            problems.Add(new Problem(NoComponents, location, type.Name, ""));
        }

        return new EntityClass(fullName, type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString(), type.Name, template, components.ToImmutable(), problems.ToImmutable());
    }

    private static void Write(SourceProductionContext output, EntityClass found)
    {
        foreach (Problem problem in found.Problems)
        {
            Diagnostic diagnostic = Diagnostic.Create(problem.Descriptor, problem.Location, problem.First, problem.Second);
            output.ReportDiagnostic(diagnostic);
        }

        if (!found.Problems.IsEmpty)
        {
            return;
        }

        const string World = "global::Arch.Core.World";
        const string Entity = "global::Arch.Core.Entity";
        string types = string.Join(", ", found.Components.Select(static component => component.Type));
        string typeofs = string.Join(", ", found.Components.Select(static component => $"typeof({component.Type})"));
        string checks = string.Join(" && ", found.Components.Select(static component => $"world.Has<{component.Type}>(entity)"));
        string template = found.Template is null ? "null" : SymbolDisplay.FormatLiteral(found.Template, quote: true);

        StringBuilder text = new();
        text.AppendLine("// <auto-generated/>");
        text.AppendLine("#nullable enable");
        if (found.Namespace is not null)
        {
            text.AppendLine($"namespace {found.Namespace};");
        }

        text.AppendLine();
        text.AppendLine($"partial struct {found.Name} : global::FlickerVoid.Engine.Runtime.Entities.IEntityClass<{found.FullName}>");
        text.AppendLine("{");
        text.AppendLine($"    private static readonly global::System.Type[] ComponentTypes = [{typeofs}];");
        text.AppendLine();
        text.AppendLine("    /// <summary>The handle over an entity, which must have every component of the class.</summary>");
        text.AppendLine($"    public {found.Name}({World} world, {Entity} entity)");
        text.AppendLine("    {");
        text.AppendLine("        World = world;");
        text.AppendLine("        Entity = entity;");
        text.AppendLine("    }");
        text.AppendLine();
        text.AppendLine("    public static global::System.Collections.Generic.IReadOnlyList<global::System.Type> Components => ComponentTypes;");
        text.AppendLine();
        text.AppendLine($"    public static string? Template => {template};");
        text.AppendLine();
        text.AppendLine($"    public static global::Arch.Core.QueryDescription Query {{ get; }} = new global::Arch.Core.QueryDescription().WithAll<{types}>();");
        text.AppendLine();
        text.AppendLine($"    public {World} World {{ get; }}");
        text.AppendLine();
        text.AppendLine($"    public {Entity} Entity {{ get; }}");
        foreach (Component component in found.Components)
        {
            text.AppendLine();
            text.AppendLine($"    {component.Accessibility} partial ref {component.Type} {component.Name} => ref World.Get<{component.Type}>(Entity);");
        }

        text.AppendLine();
        text.AppendLine($"    public static bool Is({World} world, {Entity} entity) => world.IsAlive(entity) && {checks};");
        text.AppendLine();
        text.AppendLine($"    public static {found.FullName} From({World} world, {Entity} entity) => new(world, entity);");
        text.AppendLine();
        text.AppendLine("    /// <summary>The handle over the entity when it is alive and has every component of the class.</summary>");
        text.AppendLine($"    public static bool TryGet({World} world, {Entity} entity, out {found.FullName} handle)");
        text.AppendLine("    {");
        text.AppendLine("        if (Is(world, entity))");
        text.AppendLine("        {");
        text.AppendLine("            handle = new(world, entity);");
        text.AppendLine("            return true;");
        text.AppendLine("        }");
        text.AppendLine();
        text.AppendLine("        handle = default;");
        text.AppendLine("        return false;");
        text.AppendLine("    }");
        text.AppendLine("}");
        string hint = found.FullName.Replace("global::", "") + ".g.cs";
        output.AddSource(hint, SourceText.From(text.ToString(), Encoding.UTF8));
    }

    private sealed class Component(string name, string type, string accessibility) : System.IEquatable<Component>
    {
        public string Name { get; } = name;

        public string Type { get; } = type;

        public string Accessibility { get; } = accessibility;

        public bool Equals(Component? other) => other is not null && Name == other.Name && Type == other.Type && Accessibility == other.Accessibility;

        public override bool Equals(object? obj) => obj is Component other && Equals(other);

        public override int GetHashCode() => (Name.GetHashCode() * 31) + Type.GetHashCode();
    }

    private sealed class Problem(DiagnosticDescriptor descriptor, Location location, string first, string second) : System.IEquatable<Problem>
    {
        public DiagnosticDescriptor Descriptor { get; } = descriptor;

        public Location Location { get; } = location;

        public string First { get; } = first;

        public string Second { get; } = second;

        public bool Equals(Problem? other) =>
            other is not null && Descriptor.Id == other.Descriptor.Id && Location.Equals(other.Location) && First == other.First && Second == other.Second;

        public override bool Equals(object? obj) => obj is Problem other && Equals(other);

        public override int GetHashCode() => (Descriptor.Id.GetHashCode() * 31) + First.GetHashCode();
    }

    /// <summary>What the generator read of one entity class, compared by value so unchanged classes aren't written again.</summary>
    private sealed class EntityClass(
        string fullName,
        string? space,
        string name,
        string? template,
        ImmutableArray<Component> components,
        ImmutableArray<Problem> problems) : System.IEquatable<EntityClass>
    {
        public string FullName { get; } = fullName;

        public string? Namespace { get; } = space;

        public string Name { get; } = name;

        public string? Template { get; } = template;

        public ImmutableArray<Component> Components { get; } = components;

        public ImmutableArray<Problem> Problems { get; } = problems;

        public static EntityClass Failed(string fullName, Problem problem) => new(fullName, null, "", null, [], [problem]);

        public bool Equals(EntityClass? other) =>
            other is not null && FullName == other.FullName && Namespace == other.Namespace && Name == other.Name && Template == other.Template
            && Components.SequenceEqual(other.Components) && Problems.SequenceEqual(other.Problems);

        public override bool Equals(object? obj) => obj is EntityClass other && Equals(other);

        public override int GetHashCode() => FullName.GetHashCode();
    }
}
