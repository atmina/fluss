using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Fluss.Regen.Inspectors;

public sealed class SelectorInfo : ISyntaxInfo
{
    private AttributeSyntax AttributeSyntax { get; }
    public IMethodSymbol MethodSymbol { get; }
    private MethodDeclarationSyntax MethodSyntax { get; }
    public string Name { get; }
    public string Namespace { get; }
    public string ContainingType { get; }

    public SelectorInfo(
        AttributeSyntax attributeSyntax,
        IMethodSymbol attributeSymbol,
        IMethodSymbol methodSymbol,
        MethodDeclarationSyntax methodSyntax
        )
    {
        AttributeSyntax = attributeSyntax;
        MethodSymbol = methodSymbol;
        MethodSyntax = methodSyntax;

        Name = methodSymbol.Name;
        Namespace = methodSymbol.ContainingNamespace.ToDisplayString();
        ContainingType = methodSymbol.ContainingType.ToDisplayString();
    }

    public bool Equals(SelectorInfo? other)
    {
        if (ReferenceEquals(null, other))
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return AttributeSyntax.Equals(other.AttributeSyntax) &&
               MethodSyntax.Equals(other.MethodSyntax);
    }

    public bool Equals(ISyntaxInfo other)
    {
        if (ReferenceEquals(null, other))
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return other is SelectorInfo info && Equals(info);
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj)
               || obj is SelectorInfo other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = AttributeSyntax.GetHashCode();
            hashCode = (hashCode * 397) ^ MethodSyntax.GetHashCode();
            return hashCode;
        }
    }

}