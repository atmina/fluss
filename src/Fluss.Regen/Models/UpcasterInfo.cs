using Fluss.Regen.Inspectors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Fluss.Regen.Models;

public sealed class UpcasterInfo(
    INamedTypeSymbol classSymbol,
    ClassDeclarationSyntax classSyntax)
    : SyntaxInfo
{
    public INamedTypeSymbol Type { get; } = classSymbol;
    private ClassDeclarationSyntax ClassSyntax { get; } = classSyntax;

    public bool Equals(UpcasterInfo? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return ClassSyntax.Equals(other.ClassSyntax);
    }

    public override bool Equals(SyntaxInfo? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return other is UpcasterInfo info && Equals(info);
    }

    public override bool Equals(object? obj) =>
        ReferenceEquals(this, obj) || (obj is UpcasterInfo other && Equals(other));

    public override int GetHashCode() => ClassSyntax.GetHashCode();
}