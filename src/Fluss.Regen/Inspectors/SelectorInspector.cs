﻿using System;
using System.Diagnostics.CodeAnalysis;
using Fluss.Regen.Attributes;
using Fluss.Regen.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Fluss.Regen.Inspectors;

public sealed class SelectorInspector : ISyntaxInspector
{
    public bool TryHandle(
        GeneratorSyntaxContext context,
        [NotNullWhen(true)] out SyntaxInfo? syntaxInfo)
    {
        if (context.Node is MethodDeclarationSyntax { AttributeLists.Count: > 0, } methodSyntax)
        {
            foreach (var attributeListSyntax in methodSyntax.AttributeLists)
            {
                foreach (var attributeSyntax in attributeListSyntax.Attributes)
                {
                    var symbol = context.SemanticModel.GetSymbolInfo(attributeSyntax).Symbol;

                    if (symbol is not IMethodSymbol attributeSymbol)
                    {
                        continue;
                    }

                    var attributeContainingTypeSymbol = attributeSymbol.ContainingType;
                    var fullName = attributeContainingTypeSymbol.ToDisplayString();

                    if (fullName.Equals(SelectorAttribute.FullName, StringComparison.Ordinal) &&
                        context.SemanticModel.GetDeclaredSymbol(methodSyntax) is IMethodSymbol methodSymbol)
                    {
                        syntaxInfo = new SelectorInfo(
                            attributeSyntax,
                            methodSymbol,
                            methodSyntax);
                        return true;
                    }
                }
            }
        }

        syntaxInfo = null;
        return false;
    }
}