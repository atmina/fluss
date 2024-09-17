using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Fluss.Regen.Inspectors;

public sealed class PolicyInfo(
    INamedTypeSymbol classSymbol,
    ClassDeclarationSyntax classSyntax)
    : ISyntaxInfo
{
    public INamedTypeSymbol Type { get; } = classSymbol;
    private ClassDeclarationSyntax ClassSyntax { get; } = classSyntax;

    public bool Equals(PolicyInfo? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return ClassSyntax.Equals(other.ClassSyntax);
    }

    public bool Equals(ISyntaxInfo? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return other is PolicyInfo info && Equals(info);
    }

    public override bool Equals(object? obj) =>
        ReferenceEquals(this, obj) || (obj is PolicyInfo other && Equals(other));

    public override int GetHashCode() => ClassSyntax.GetHashCode();
}