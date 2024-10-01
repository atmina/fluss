using System;
using System.Collections.Immutable;
using System.Linq;
using Fluss.Regen.Inspectors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Fluss.Regen.Models;

public sealed class CrudInfo : SyntaxInfo
{
    public INamedTypeSymbol ClassSymbol { get; }
    private ClassDeclarationSyntax ClassSyntax { get; }
    public string Name { get; }
    public string Namespace { get; }

    public readonly ImmutableArray<IPropertySymbol> AllProperties;

    public readonly IPropertySymbol? IdProperty;

    public readonly ImmutableArray<IPropertySymbol> Properties;

    public CrudInfo(INamedTypeSymbol classSymbol,
        ClassDeclarationSyntax classSyntax)
    {
        ClassSymbol = classSymbol;
        ClassSyntax = classSyntax;
        Name = classSymbol.Name;
        Namespace = classSymbol.ContainingNamespace.ToDisplayString();

        var allProperties = classSymbol.GetMembers().OfType<IPropertySymbol>().ToImmutableArray();

        AllProperties = allProperties;
        IdProperty = allProperties.FirstOrDefault(m => m.Name == "Id");
        Properties = [.. allProperties.Where(m => m.Name != "Id")];
    }

    public bool Equals(CrudInfo? other)
    {
        if (ReferenceEquals(null, other))
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return ClassSyntax.Equals(other.ClassSyntax);
    }

    public override bool Equals(SyntaxInfo other)
    {
        if (ReferenceEquals(null, other))
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return other is CrudInfo info && Equals(info);
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj)
               || obj is CrudInfo other && Equals(other);
    }

    public override int GetHashCode()
    {
        return ClassSyntax.GetHashCode();
    }
}