using System.Diagnostics.CodeAnalysis;
using Fluss.Regen.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Fluss.Regen.Inspectors;

public sealed class UpcasterInspector : ISyntaxInspector
{
    public bool TryHandle(
        GeneratorSyntaxContext context,
        [NotNullWhen(true)] out SyntaxInfo? syntaxInfo)
    {
        if (context.Node is ClassDeclarationSyntax classSyntax)
        {
            var symbol = context.SemanticModel.GetDeclaredSymbol(classSyntax);
            if (symbol is INamedTypeSymbol classSymbol &&
                classSymbol.BaseType?.ToDisplayString() == "Fluss.Upcasting.Upcaster")
            {
                syntaxInfo = new UpcasterInfo(classSymbol, classSyntax);
                return true;
            }
        }

        syntaxInfo = null;
        return false;
    }
}