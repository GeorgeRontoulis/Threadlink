// RS1024 misreports collections built with SymbolEqualityComparer.Default in this analyzer version.
#pragma warning disable RS1024
namespace Threadforge.Threadlink.Generators
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Text;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;

    /// <summary>
    /// Writes the mapping code of Vault definitions. For a <c>partial</c> class deriving from VaultAsset&lt;T&gt; that does
    /// not write BuildRuntime, it generates BuildRuntime from the class's serialized fields. For a <c>partial</c> class
    /// deriving from VaultVariant&lt;T&gt;, it generates a VaultOverride field per value of T (unless declared) and
    /// BuildDefault and ApplyOverrides (unless written). When T is an empty <c>partial</c> type, it also generates T: one
    /// read-only property per serialized field of the one definition class that uses it, and a constructor.
    /// </summary>
    [Generator]
    public sealed class VaultDefinitionGenerator : IIncrementalGenerator
    {
        private const string AssetBase = "Threadforge.Threadlink.VaultAsset<T>";
        private const string VariantBase = "Threadforge.Threadlink.VaultVariant<T>";
        private const string OverrideType = "Threadforge.Threadlink.VaultOverride<T>";
        private const string ReadOnlyList = "System.Collections.Generic.IReadOnlyList<T>";
        private const string List = "System.Collections.Generic.List<T>";

        private static readonly DiagnosticDescriptor CannotMap = new DiagnosticDescriptor(
            "TL0002",
            "Vault definition mapping cannot be generated",
            "Threadlink cannot generate the mapping of '{0}': {1}. Write it by hand or fix the definition.",
            "Threadlink",
            DiagnosticSeverity.Warning,
            true
        );

        private static readonly DiagnosticDescriptor NotPartial = new DiagnosticDescriptor(
            "TL0003",
            "Vault definition is not partial",
            "Make '{0}' (and any class containing it) partial so Threadlink generates {1}, or write {1} by hand",
            "Threadlink",
            DiagnosticSeverity.Warning,
            true
        );

        private static readonly DiagnosticDescriptor Ambiguous = new DiagnosticDescriptor(
            "TL0004",
            "Generated Vault runtime type has several definitions",
            "'{0}' is generated from the fields of one Vault definition, but {1} use it. Give it members by hand, or use one definition class.",
            "Threadlink",
            DiagnosticSeverity.Warning,
            true
        );

        private sealed class Member
        {
            public string Property;
            public string PropertyType;
            public string Parameter;
            public string Tooltip;

            // Collections map to IReadOnlyList<Element> at runtime and are authored as Authored (E[] or List<E>).
            public ITypeSymbol Element;
            public string AuthoredCollection;

            public ITypeSymbol Type;
        }

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var candidates = context
                .SyntaxProvider.CreateSyntaxProvider(
                    static (node, _) => node is ClassDeclarationSyntax declaration && declaration.BaseList != null,
                    static (syntax, token) => syntax.SemanticModel.GetDeclaredSymbol(syntax.Node, token) as INamedTypeSymbol
                )
                .Where(static symbol => symbol != null);
            context.RegisterSourceOutput(context.CompilationProvider.Combine(candidates.Collect()), static (output, input) => Emit(output, input.Left, input.Right));
        }

        private static void Emit(SourceProductionContext output, Compilation compilation, ImmutableArray<INamedTypeSymbol> candidates)
        {
            var assets = new List<(INamedTypeSymbol type, ITypeSymbol runtime)>();
            var variants = new List<(INamedTypeSymbol type, ITypeSymbol runtime)>();
            var seen = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
            foreach (var type in candidates)
            {
                if (!seen.Add(type) || type.IsAbstract || type.IsGenericType || type.TypeKind != TypeKind.Class)
                    continue;
                var (isVariant, runtime) = Definition(type);
                if (runtime == null)
                    continue;
                (isVariant ? variants : assets).Add((type, runtime));
            }
            if (assets.Count == 0 && variants.Count == 0)
                return;

            // Runtime types Threadlink writes: empty partial types used by exactly one definition class.
            var shapes = new Dictionary<ISymbol, List<Member>>(SymbolEqualityComparer.Default);
            foreach (var group in assets.GroupBy(asset => asset.runtime, SymbolEqualityComparer.Default))
            {
                var runtime = (ITypeSymbol)group.Key;
                if (!IsGeneratedRuntime(runtime))
                    continue;
                var owners = group.Select(asset => asset.type).ToList();
                if (owners.Count > 1)
                {
                    output.ReportDiagnostic(Diagnostic.Create(Ambiguous, runtime.Locations.FirstOrDefault(), runtime.Name, string.Join(", ", owners.Select(owner => owner.Name))));
                    continue;
                }
                var members = AuthoredMembers(owners[0]);
                shapes[runtime] = members;
                EmitRuntime(output, (INamedTypeSymbol)runtime, members);
            }

            foreach (var (type, runtime) in assets)
            {
                if (Declares(type, "BuildRuntime"))
                    continue;
                if (!IsPartial(type))
                {
                    output.ReportDiagnostic(Diagnostic.Create(NotPartial, type.Locations.FirstOrDefault(), type.Name, "BuildRuntime"));
                    continue;
                }
                var members = Shape(runtime, shapes, out string problem);
                var fields = members == null ? null : AuthoredFields(type);
                var arguments = new List<string>();
                if (members != null)
                    foreach (var member in members)
                    {
                        var field = fields.FirstOrDefault(candidate => Normalize(candidate.Name) == Normalize(member.Property));
                        if (field == null)
                        {
                            problem = $"it has no serialized field for {runtime.Name}.{member.Property}";
                            break;
                        }
                        if (SymbolEqualityComparer.Default.Equals(field.Type, member.Type))
                            arguments.Add(Id(field.Name));
                        else if (member.Element != null && SymbolEqualityComparer.Default.Equals(ElementOf(field.Type), member.Element))
                            arguments.Add("ReadOnly(" + Id(field.Name) + ")");
                        else
                        {
                            problem = $"its field {field.Name} is a {field.Type.ToDisplayString()}, but {runtime.Name}.{member.Property} is a {member.PropertyType.Replace("global::", string.Empty)}";
                            break;
                        }
                    }
                if (problem != null)
                {
                    output.ReportDiagnostic(Diagnostic.Create(CannotMap, type.Locations.FirstOrDefault(), type.Name, problem));
                    continue;
                }
                var body = new StringBuilder();
                body.Append("        /// <summary>Generated: builds the runtime value from the serialized fields.</summary>\n");
                body.Append("        protected override ").Append(Name(runtime)).Append(" BuildRuntime(global::Threadforge.Threadlink.VaultContext context) => new ")
                    .Append(Name(runtime)).Append('(').Append(string.Join(", ", arguments)).Append(");\n");
                EmitPartial(output, type, body.ToString());
            }

            foreach (var (type, runtime) in variants)
            {
                bool writesDefault = Declares(type, "BuildDefault");
                bool writesOverrides = Declares(type, "ApplyOverrides");
                if (writesDefault && writesOverrides)
                    continue;
                if (!IsPartial(type))
                {
                    output.ReportDiagnostic(Diagnostic.Create(NotPartial, type.Locations.FirstOrDefault(), type.Name, "BuildDefault and ApplyOverrides"));
                    continue;
                }
                var members = Shape(runtime, shapes, out string problem);
                if (problem != null)
                {
                    output.ReportDiagnostic(Diagnostic.Create(CannotMap, type.Locations.FirstOrDefault(), type.Name, problem));
                    continue;
                }
                var declared = type.GetMembers().OfType<IFieldSymbol>().Where(field => !field.IsStatic && !field.IsImplicitlyDeclared).ToList();
                var body = new StringBuilder();
                var overrides = new List<(Member member, string field, bool collection)>();
                foreach (var member in members)
                {
                    var existing = declared.FirstOrDefault(field => Normalize(field.Name) == Normalize(member.Property));
                    if (existing != null)
                    {
                        var value = existing.Type is INamedTypeSymbol named && named.IsGenericType && named.OriginalDefinition.ToDisplayString() == OverrideType ? named.TypeArguments[0] : null;
                        bool plain = value != null && SymbolEqualityComparer.Default.Equals(value, member.Type);
                        bool collection = value != null && member.Element != null && SymbolEqualityComparer.Default.Equals(ElementOf(value), member.Element);
                        if (!plain && !collection)
                        {
                            problem = $"its field {existing.Name} must be a VaultOverride<{member.PropertyType.Replace("global::", string.Empty)}> for {runtime.Name}.{member.Property}";
                            break;
                        }
                        overrides.Add((member, Id(existing.Name), !plain));
                        continue;
                    }
                    string name = Camel(member.Property);
                    string authored = member.Element != null ? member.AuthoredCollection ?? "global::System.Collections.Generic.List<" + Name(member.Element) + ">" : member.PropertyType;
                    if (!string.IsNullOrEmpty(member.Tooltip))
                        body.Append("        [global::UnityEngine.Tooltip(").Append(SymbolDisplay.FormatLiteral(member.Tooltip, true)).Append(")]\n");
                    body.Append("        [global::UnityEngine.SerializeField]\n");
                    body.Append("        private global::Threadforge.Threadlink.VaultOverride<").Append(authored).Append("> ").Append(name).Append(";\n\n");
                    overrides.Add((member, name, member.Element != null));
                }
                if (problem != null)
                {
                    output.ReportDiagnostic(Diagnostic.Create(CannotMap, type.Locations.FirstOrDefault(), type.Name, problem));
                    continue;
                }
                string runtimeName = Name(runtime);
                if (!writesDefault)
                {
                    body.Append("        /// <summary>Generated: the value of every override, for a variant without a parent.</summary>\n");
                    body.Append("        protected override ").Append(runtimeName).Append(" BuildDefault(global::Threadforge.Threadlink.VaultContext context) => new ").Append(runtimeName).Append('(')
                        .Append(string.Join(", ", overrides.Select(o => o.collection ? "ReadOnly(" + o.field + ".Value)" : o.field + ".Value"))).Append(");\n\n");
                }
                if (!writesOverrides)
                {
                    body.Append("        /// <summary>Generated: the inherited value with every enabled override applied.</summary>\n");
                    body.Append("        protected override ").Append(runtimeName).Append(" ApplyOverrides(").Append(runtimeName).Append(" inherited, global::Threadforge.Threadlink.VaultContext context) => new ").Append(runtimeName).Append('(')
                        .Append(string.Join(", ", overrides.Select(o => o.collection
                            ? o.field + ".Enabled ? ReadOnly(" + o.field + ".Value) : inherited." + o.member.Property
                            : o.field + ".Resolve(inherited." + o.member.Property + ")"))).Append(");\n");
                }
                EmitPartial(output, type, body.ToString());
            }
        }

        private static (bool variant, ITypeSymbol runtime) Definition(INamedTypeSymbol type)
        {
            for (var current = type.BaseType; current != null; current = current.BaseType)
            {
                if (!current.IsGenericType)
                    continue;
                string original = current.OriginalDefinition.ToDisplayString();
                if (original == VariantBase)
                    return (true, current.TypeArguments[0]);
                if (original == AssetBase)
                    return (false, current.TypeArguments[0]);
            }
            return (false, null);
        }

        private static bool Declares(INamedTypeSymbol type, string method) => type.GetMembers(method).Any(member => member is IMethodSymbol);

        private static bool IsPartial(INamedTypeSymbol type)
        {
            for (var current = type; current != null; current = current.ContainingType)
                if (!current.DeclaringSyntaxReferences.Any(reference => reference.GetSyntax() is TypeDeclarationSyntax syntax && syntax.Modifiers.Any(SyntaxKind.PartialKeyword)))
                    return false;
            return true;
        }

        /// <summary>An empty partial class or struct in this compilation: no members, no constructors of its own.</summary>
        private static bool IsGeneratedRuntime(ITypeSymbol runtime) =>
            runtime is INamedTypeSymbol named
            && (named.TypeKind == TypeKind.Class || named.TypeKind == TypeKind.Struct)
            && !named.IsGenericType
            && named.DeclaringSyntaxReferences.Length > 0
            && IsPartial(named)
            && named.GetMembers().All(member => member.IsImplicitlyDeclared || member.IsStatic || member is INamedTypeSymbol);

        private static List<Member> Shape(ITypeSymbol runtime, Dictionary<ISymbol, List<Member>> shapes, out string problem)
        {
            problem = null;
            if (shapes.TryGetValue(runtime, out var generated))
                return generated;
            if (IsGeneratedRuntime(runtime))
            {
                problem = $"{runtime.Name} is empty; declare one partial VaultAsset<{runtime.Name}> class whose fields define it, or give it members";
                return null;
            }
            if (!(runtime is INamedTypeSymbol named))
            {
                problem = $"{runtime.ToDisplayString()} has no constructor to call";
                return null;
            }
            var properties = new List<IPropertySymbol>();
            for (var current = named; current != null; current = current.BaseType)
                properties.AddRange(current.GetMembers().OfType<IPropertySymbol>().Where(property => !property.IsStatic && property.GetMethod != null && !property.IsIndexer));
            IMethodSymbol best = null;
            List<IPropertySymbol> matched = null;
            foreach (var constructor in named.InstanceConstructors.Where(c => c.DeclaredAccessibility == Accessibility.Public || c.DeclaredAccessibility == Accessibility.Internal))
            {
                if (constructor.Parameters.Length == 0 || (best != null && constructor.Parameters.Length <= best.Parameters.Length))
                    continue;
                var match = constructor.Parameters.Select(parameter => properties.FirstOrDefault(property => Normalize(property.Name) == Normalize(parameter.Name) && SymbolEqualityComparer.Default.Equals(property.Type, parameter.Type))).ToList();
                if (match.Any(property => property == null))
                    continue;
                best = constructor;
                matched = match;
            }
            if (best == null)
            {
                problem = $"{runtime.Name} needs a constructor whose parameters are named after its properties";
                return null;
            }
            return matched.Select(property => Describe(property.Name, property.Type, null, null)).ToList();
        }

        private static List<IFieldSymbol> AuthoredFields(INamedTypeSymbol type) =>
            type.GetMembers()
                .OfType<IFieldSymbol>()
                .Where(field =>
                    !field.IsStatic
                    && !field.IsConst
                    && !field.IsReadOnly
                    && !field.IsImplicitlyDeclared
                    && !HasAttribute(field, "System.NonSerializedAttribute")
                    && (field.DeclaredAccessibility == Accessibility.Public || HasAttribute(field, "UnityEngine.SerializeField"))
                    && !(field.Type is INamedTypeSymbol named && named.IsGenericType && named.OriginalDefinition.ToDisplayString() == OverrideType)
                )
                .ToList();

        private static List<Member> AuthoredMembers(INamedTypeSymbol owner) =>
            AuthoredFields(owner)
                .Select(field =>
                {
                    var element = ElementOf(field.Type);
                    var type = element == null ? field.Type : null;
                    string tooltip = field
                        .GetAttributes()
                        .Where(attribute => attribute.AttributeClass?.ToDisplayString() == "UnityEngine.TooltipAttribute" && attribute.ConstructorArguments.Length > 0)
                        .Select(attribute => attribute.ConstructorArguments[0].Value as string)
                        .FirstOrDefault();
                    var member = Describe(Pascal(field.Name), type, element, tooltip);
                    if (element != null)
                        member.AuthoredCollection = Name(field.Type);
                    return member;
                })
                .ToList();

        private static Member Describe(string property, ITypeSymbol type, ITypeSymbol element, string tooltip)
        {
            if (type != null && element == null && type is INamedTypeSymbol named && named.IsGenericType && named.OriginalDefinition.ToDisplayString() == ReadOnlyList)
                element = named.TypeArguments[0];
            string propertyType = element != null ? "global::System.Collections.Generic.IReadOnlyList<" + Name(element) + ">" : Name(type);
            return new Member
            {
                Property = property,
                PropertyType = propertyType,
                Parameter = Parameter(property),
                Tooltip = tooltip,
                Element = element,
                Type = type,
            };
        }

        /// <summary>The element of an authored collection (E[] or List&lt;E&gt;), or null.</summary>
        private static ITypeSymbol ElementOf(ITypeSymbol type) =>
            type is IArrayTypeSymbol array && array.Rank == 1 ? array.ElementType
            : type is INamedTypeSymbol named && named.IsGenericType && named.OriginalDefinition.ToDisplayString() == List ? named.TypeArguments[0]
            : null;

        private static bool HasAttribute(ISymbol symbol, string name) => symbol.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == name);

        private static void EmitRuntime(SourceProductionContext output, INamedTypeSymbol runtime, List<Member> members)
        {
            var body = new StringBuilder();
            foreach (var member in members)
            {
                body.Append("        /// <summary>").Append(Xml(string.IsNullOrEmpty(member.Tooltip) ? member.Property + "." : member.Tooltip)).Append("</summary>\n");
                body.Append("        public ").Append(member.PropertyType).Append(' ').Append(member.Property).Append(" { get; }\n\n");
            }
            if (members.Count > 0)
            {
                body.Append("        /// <summary>Generated: creates the value. Vault definitions call this.</summary>\n");
                body.Append("        public ").Append(runtime.Name).Append('(').Append(string.Join(", ", members.Select(member => member.PropertyType + " " + member.Parameter))).Append(")\n        {\n");
                foreach (var member in members)
                    body.Append("            ").Append(member.Property).Append(" = ").Append(member.Parameter).Append(";\n");
                body.Append("        }\n");
            }
            EmitPartial(output, runtime, body.ToString(), "VaultRuntime.");
        }

        private static void EmitPartial(SourceProductionContext output, INamedTypeSymbol type, string body, string prefix = "VaultDefinition.")
        {
            var chain = new List<INamedTypeSymbol>();
            for (var current = type; current != null; current = current.ContainingType)
                chain.Insert(0, current);
            var code = new StringBuilder();
            code.Append("// <auto-generated>Threadlink Vault definition mapping.</auto-generated>\n");
            // CS0649: Unity assigns the generated override fields when it deserializes the asset.
            code.Append("#pragma warning disable CS0649, CS1591\n");
            string space = type.ContainingNamespace == null || type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString();
            if (space != null)
                code.Append("namespace ").Append(space).Append("\n{\n");
            int outer = space == null ? 0 : 1;
            int depth = outer;
            foreach (var part in chain)
            {
                code.Append(' ', depth * 4).Append("partial ").Append(Keyword(part)).Append(' ').Append(part.Name).Append('\n');
                code.Append(' ', depth * 4).Append("{\n");
                depth++;
            }
            // Bodies are written two levels deep; re-indent them to their real depth.
            foreach (string line in body.TrimEnd('\n').Split('\n'))
                code.Append(line.Length == 0 ? string.Empty : new string(' ', depth * 4) + (line.StartsWith("        ") ? line.Substring(8) : line)).Append('\n');
            while (depth > outer)
            {
                depth--;
                code.Append(' ', depth * 4).Append("}\n");
            }
            if (space != null)
                code.Append("}\n");
            string hint = type.ToDisplayString().Replace('<', '_').Replace('>', '_');
            output.AddSource(prefix + hint + ".g.cs", code.ToString());
        }

        private static string Keyword(INamedTypeSymbol type)
        {
            var syntax = type.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax()).OfType<TypeDeclarationSyntax>().FirstOrDefault();
            if (syntax is RecordDeclarationSyntax)
                return type.TypeKind == TypeKind.Struct ? "record struct" : "record";
            return type.TypeKind == TypeKind.Struct ? "struct" : type.TypeKind == TypeKind.Interface ? "interface" : "class";
        }

        private static string Name(ITypeSymbol type) => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        private static string Normalize(string name)
        {
            if (name.StartsWith("m_"))
                name = name.Substring(2);
            return name.TrimStart('_').ToLowerInvariant();
        }

        private static string Pascal(string field)
        {
            string name = field.StartsWith("m_") ? field.Substring(2) : field.TrimStart('_');
            return name.Length == 0 ? field : char.ToUpperInvariant(name[0]) + name.Substring(1);
        }

        private static string Camel(string property)
        {
            string name = char.ToLowerInvariant(property[0]) + property.Substring(1);
            return SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None || SyntaxFacts.GetContextualKeywordKind(name) != SyntaxKind.None ? name + "Override" : name;
        }

        private static string Parameter(string property)
        {
            string name = char.ToLowerInvariant(property[0]) + property.Substring(1);
            return SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ? "@" + name : name;
        }

        private static string Id(string name) => SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ? "@" + name : name;

        private static string Xml(string text) => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\n", " ");
    }
}
