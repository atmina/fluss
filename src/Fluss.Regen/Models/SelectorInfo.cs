using Fluss.Regen.Inspectors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Fluss.Regen.Models;

public sealed class SelectorInfo(
    AttributeSyntax attributeSyntax,
    IMethodSymbol methodSymbol,
    MethodDeclarationSyntax methodSyntax)
    : SyntaxInfo
{
    private AttributeSyntax AttributeSyntax { get; } = attributeSyntax;
    public IMethodSymbol MethodSymbol { get; } = methodSymbol;
    private MethodDeclarationSyntax MethodSyntax { get; } = methodSyntax;
    public string Name { get; } = methodSymbol.Name;
    public string Namespace { get; } = methodSymbol.ContainingNamespace.ToDisplayString();
    public string ContainingType { get; } = methodSymbol.ContainingType.ToDisplayString();

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

        return other is SelectorInfo info && Equals(info);
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj)
               || obj is SelectorInfo other && Equals(other);
    }

    public override int GetHashCode()
    {
        return 31 * AttributeSyntax.GetHashCode() + MethodSyntax.GetHashCode();
    }
}