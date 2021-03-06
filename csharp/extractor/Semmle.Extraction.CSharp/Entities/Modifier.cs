using Microsoft.CodeAnalysis;
using System;
using System.IO;
using System.Reflection;

namespace Semmle.Extraction.CSharp.Entities
{
    /// <summary>
    /// Provide a "Key" object to allow modifiers to exist as entities in the extractor
    /// hash map. (Raw strings would work as keys but might clash with other types).
    /// </summary>
    class ModifierKey : Object
    {
        public readonly string name;

        public ModifierKey(string m)
        {
            name = m;
        }

        public override bool Equals(Object obj)
        {
            return obj.GetType() == GetType() && name == ((ModifierKey)obj).name;
        }

        public override int GetHashCode() => 13 * name.GetHashCode();
    }

    class Modifier : Extraction.CachedEntity<ModifierKey>
    {
        Modifier(Context cx, ModifierKey init)
            : base(cx, init) { }

        public override Microsoft.CodeAnalysis.Location ReportingLocation => null;

        public override void WriteId(TextWriter trapFile)
        {
            trapFile.Write(symbol.name);
            trapFile.Write(";modifier");
        }

        public override bool NeedsPopulation => true;

        public override void Populate(TextWriter trapFile)
        {
            trapFile.modifiers(Label, symbol.name);
        }

        public static string AccessbilityModifier(Accessibility access)
        {
            switch (access)
            {
                case Accessibility.Private:
                    return "private";
                case Accessibility.Protected:
                    return "protected";
                case Accessibility.Public:
                    return "public";
                case Accessibility.Internal:
                    return "internal";
                default:
                    throw new InternalError("Unavailable modifier combination");
            }
        }

        public static void HasAccessibility(Context cx, TextWriter trapFile, IEntity type, Accessibility access)
        {
            switch (access)
            {
                case Accessibility.Private:
                case Accessibility.Public:
                case Accessibility.Protected:
                case Accessibility.Internal:
                    HasModifier(cx, trapFile, type, Modifier.AccessbilityModifier(access));
                    break;
                case Accessibility.NotApplicable:
                    break;
                case Accessibility.ProtectedOrInternal:
                    HasModifier(cx, trapFile, type, "protected");
                    HasModifier(cx, trapFile, type, "internal");
                    break;
                case Accessibility.ProtectedAndInternal:
                    HasModifier(cx, trapFile, type, "protected");
                    HasModifier(cx, trapFile, type, "private");
                    break;
                default:
                    throw new InternalError($"Unhandled Microsoft.CodeAnalysis.Accessibility value: {access}");
            }
        }

        public static void HasModifier(Context cx, TextWriter trapFile, IEntity target, string modifier)
        {
            trapFile.has_modifiers(target, Modifier.Create(cx, modifier));
        }

        public static void ExtractModifiers(Context cx, TextWriter trapFile, IEntity key, ISymbol symbol)
        {
            bool interfaceDefinition = symbol.ContainingType != null
                && symbol.ContainingType.Kind == SymbolKind.NamedType
                && symbol.ContainingType.TypeKind == TypeKind.Interface;

            HasAccessibility(cx, trapFile, key, symbol.DeclaredAccessibility);
            if (symbol.Kind == SymbolKind.ErrorType)
                trapFile.has_modifiers(key, Modifier.Create(cx, Accessibility.Public));

            if (symbol.IsAbstract && (symbol.Kind != SymbolKind.NamedType || ((INamedTypeSymbol)symbol).TypeKind != TypeKind.Interface) && !interfaceDefinition)
                HasModifier(cx, trapFile, key, "abstract");

            if (symbol.IsSealed)
                HasModifier(cx, trapFile, key, "sealed");

            bool fromSource = symbol.DeclaringSyntaxReferences.Length > 0;

            if (symbol.IsStatic && !(symbol.Kind == SymbolKind.Field && ((IFieldSymbol)symbol).IsConst && !fromSource))
                HasModifier(cx, trapFile, key, "static");

            if (symbol.IsVirtual)
                HasModifier(cx, trapFile, key, "virtual");

            // For some reason, method in interfaces are "virtual", not "abstract"
            if (symbol.IsAbstract && interfaceDefinition)
                HasModifier(cx, trapFile, key, "virtual");

            if (symbol.Kind == SymbolKind.Field && ((IFieldSymbol)symbol).IsReadOnly)
                HasModifier(cx, trapFile, key, "readonly");

            if (symbol.IsOverride)
                HasModifier(cx, trapFile, key, "override");

            if (symbol.Kind == SymbolKind.Method && ((IMethodSymbol)symbol).IsAsync)
                HasModifier(cx, trapFile, key, "async");

            if (symbol.IsExtern)
                HasModifier(cx, trapFile, key, "extern");

            foreach (var modifier in symbol.GetSourceLevelModifiers())
                HasModifier(cx, trapFile, key, modifier);

            if (symbol.Kind == SymbolKind.NamedType)
            {
                INamedTypeSymbol nt = symbol as INamedTypeSymbol;
                if (nt is null)
                    throw new InternalError(symbol, "Symbol kind is inconsistent with its type");

                if (nt.TypeKind == TypeKind.Struct)
                {
                    if (nt.IsReadOnly)
                        HasModifier(cx, trapFile, key, "readonly");
                    if (nt.IsRefLikeType)
                        HasModifier(cx, trapFile, key, "ref");
                }
            }
        }

        public static Modifier Create(Context cx, string modifier) =>
            ModifierFactory.Instance.CreateEntity(cx, new ModifierKey(modifier));

        public static Modifier Create(Context cx, Accessibility access) =>
            ModifierFactory.Instance.CreateEntity(cx, new ModifierKey(AccessbilityModifier(access)));

        class ModifierFactory : ICachedEntityFactory<ModifierKey, Modifier>
        {
            public static readonly ModifierFactory Instance = new ModifierFactory();

            public Modifier Create(Context cx, ModifierKey init) => new Modifier(cx, init);
        }
        public override TrapStackBehaviour TrapStackBehaviour => TrapStackBehaviour.OptionalLabel;
    }
}
