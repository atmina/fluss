using Microsoft.CodeAnalysis;

namespace Fluss.Regen.Helpers;

public static class SymbolExtensions
{
    public static string ToFullyQualified(this ITypeSymbol typeSymbol)
        => typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
}
