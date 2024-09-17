using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Fluss.Regen.Inspectors;

public sealed class AggregateValidatorInspector : ISyntaxInspector
{
    public bool TryHandle(
        GeneratorSyntaxContext context,
        [NotNullWhen(true)] out ISyntaxInfo? syntaxInfo)
    {
        if (context.Node is ClassDeclarationSyntax classSyntax)
        {
            var symbol = context.SemanticModel.GetDeclaredSymbol(classSyntax);
            if (symbol is INamedTypeSymbol classSymbol &&
                classSymbol.AllInterfaces.Any(i => i.ToDisplayString().StartsWith("Fluss.Validation.AggregateValidator")))
            {
                syntaxInfo = new AggregateValidatorInfo(classSymbol, classSyntax);
                return true;
            }
        }

        syntaxInfo = null;
        return false;
    }
}