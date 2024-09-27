using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Fluss.Regen.FileBuilders;
using Fluss.Regen.Inspectors;
using Fluss.Regen.Models;
using Microsoft.CodeAnalysis;

namespace Fluss.Regen.Generators;

public class SelectorGenerator : ISyntaxGenerator
{
    public void Generate(SourceProductionContext context, Compilation compilation, ImmutableArray<SyntaxInfo> syntaxInfos)
    {
        var selectors = new List<SelectorInfo>();

        foreach (var syntaxInfo in syntaxInfos)
        {
            if (syntaxInfo is not SelectorInfo selector)
            {
                continue;
            }

            selectors.Add(selector);
        }

        using var generator = new SelectorFileBuilder();
        generator.WriteHeader();
        generator.WriteClassHeader();

        foreach (var selector in selectors)
        {
            var parametersWithoutUnitOfWork = new List<IParameterSymbol>();

            foreach (var parameter in selector.MethodSymbol.Parameters)
            {
                if (ToTypeNameNoGenerics(parameter.Type) != "Fluss.IUnitOfWork")
                {
                    parametersWithoutUnitOfWork.Add(parameter);
                }
            }

            var isAsync = ToTypeNameNoGenerics(selector.MethodSymbol.ReturnType) == typeof(ValueTask).FullName ||
                          ToTypeNameNoGenerics(selector.MethodSymbol.ReturnType) == typeof(Task).FullName;
            var hasUnitOfWorkParameter = selector.MethodSymbol.Parameters.Length != parametersWithoutUnitOfWork.Count;

            var returnType = ExtractValueType(selector.MethodSymbol.ReturnType);

            generator.WriteMethodSignatureStart(selector.Name, returnType, parametersWithoutUnitOfWork.Count == 0);

            for (var index = 0; index < parametersWithoutUnitOfWork.Count; index++)
            {
                var parameter = parametersWithoutUnitOfWork[index];
                generator.WriteMethodSignatureParameter(parameter.Type, parameter.Name, parametersWithoutUnitOfWork.Count - 1 == index);
            }

            generator.WriteMethodSignatureEnd();

            if (hasUnitOfWorkParameter)
            {
                generator.WriteRecordingUnitOfWork();
            }

            generator.WriteKeyStart(selector.ContainingType, selector.Name, parametersWithoutUnitOfWork.Count == 0);

            for (var index = 0; index < parametersWithoutUnitOfWork.Count; index++)
            {
                var parameter = parametersWithoutUnitOfWork[index];
                generator.WriteKeyParameter(parameter.Name, index == parametersWithoutUnitOfWork.Count - 1);
            }

            generator.WriteKeyEnd();
            generator.WriteMethodCacheHit(returnType);

            generator.WriteMethodCall(selector.ContainingType, selector.Name, isAsync);
            for (var index = 0; index < selector.MethodSymbol.Parameters.Length; index++)
            {
                var parameter = selector.MethodSymbol.Parameters[index];

                if (ToTypeNameNoGenerics(parameter.Type) == "Fluss.IUnitOfWork")
                {
                    generator.WriteMethodCallParameter("recordingUnitOfWork", index == selector.MethodSymbol.Parameters.Length - 1);
                }
                else
                {
                    generator.WriteMethodCallParameter(parameter.Name, index == selector.MethodSymbol.Parameters.Length - 1);
                }
            }

            generator.WriteMethodCallEnd(isAsync);

            generator.WriteMethodCacheMiss(returnType);

            generator.WriteMethodEnd();
        }

        generator.WriteEndNamespace();

        context.AddSource("Selectors.g.cs", generator.ToSourceText());
    }
    
    
    private static ITypeSymbol ExtractValueType(ITypeSymbol returnType)
    {
        if (returnType is INamedTypeSymbol namedTypeSymbol && (ToTypeNameNoGenerics(returnType) == typeof(ValueTask).FullName ||
                                                               ToTypeNameNoGenerics(returnType) == typeof(Task).FullName))
        {
            return namedTypeSymbol.TypeArguments[0];
        }

        return returnType;
    }

    private static string ToTypeNameNoGenerics(ITypeSymbol typeSymbol)
        => $"{typeSymbol.ContainingNamespace}.{typeSymbol.Name}";
}