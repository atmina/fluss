using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Fluss.Regen.FileBuilders;
using Fluss.Regen.Helpers;
using Fluss.Regen.Inspectors;
using Fluss.Regen.Models;
using Microsoft.CodeAnalysis;

namespace Fluss.Regen.Generators;

public class SelectorGenerator : ISyntaxGenerator
{
    public void Generate(SourceProductionContext context, Compilation compilation, ImmutableArray<SyntaxInfo> syntaxInfos)
    {
        using var generator = new SelectorFileBuilder();
        generator.WriteHeader();
        generator.WriteClassHeader();

        foreach (var selector in syntaxInfos.OfType<SelectorInfo>())
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

            generator.WriteMethodSignatureStart(selector.Name, returnType.ToFullyQualified(), parametersWithoutUnitOfWork.Count == 0);

            for (var index = 0; index < parametersWithoutUnitOfWork.Count; index++)
            {
                var parameter = parametersWithoutUnitOfWork[index];
                generator.WriteMethodSignatureParameter(parameter.Type.ToFullyQualified(), parameter.Name, parametersWithoutUnitOfWork.Count - 1 == index);
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
            generator.WriteMethodCacheHit(returnType.ToFullyQualified());

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

            generator.WriteMethodCacheMiss(returnType.ToFullyQualified());

            generator.WriteMethodEnd();
        }

        foreach (var crudInfo in syntaxInfos.OfType<CrudInfo>())
        {
            if (crudInfo.IdProperty != null)
            {
                // Generate single item selector
                var singleSelectorName = $"Get{crudInfo.Name}";
                var idType = crudInfo.IdProperty.Type.ToFullyQualified();
                var idName = crudInfo.IdProperty.Name.ToLowerInvariant();

                generator.WriteMethodSignatureStart(crudInfo.Name, crudInfo.ClassSymbol.ToFullyQualified() + ".ReadModel", false);
                generator.WriteMethodSignatureParameter(idType, idName, true);
                generator.WriteMethodSignatureEnd();

                generator.WriteRecordingUnitOfWork();
                generator.WriteKeyStart(crudInfo.ClassSymbol.ToFullyQualified(), singleSelectorName, false);
                generator.WriteKeyParameter(idName, true);
                generator.WriteKeyEnd();
                generator.WriteMethodCacheHit(crudInfo.ClassSymbol.ToFullyQualified() + ".ReadModel");

                generator.WriteMethodCall(crudInfo.ClassSymbol.ToFullyQualified(), singleSelectorName, true);
                generator.WriteMethodCallParameter("recordingUnitOfWork", false);
                generator.WriteMethodCallParameter(idName, true);
                generator.WriteMethodCallEnd(true);

                generator.WriteMethodCacheMiss(crudInfo.ClassSymbol.ToFullyQualified() + ".ReadModel");
                generator.WriteMethodEnd();

                // Generate all items selector
                var allSelectorName = $"GetAll{crudInfo.Name}s";

                generator.WriteMethodSignatureStart($"All{crudInfo.Name}s", $"global::System.Collections.Generic.IReadOnlyList<{crudInfo.ClassSymbol.ToFullyQualified()}.ReadModel>", true);
                generator.WriteMethodSignatureEnd();

                generator.WriteRecordingUnitOfWork();
                generator.WriteKeyStart(crudInfo.ClassSymbol.ToFullyQualified(), allSelectorName, true);
                generator.WriteKeyEnd();
                generator.WriteMethodCacheHit($"global::System.Collections.Generic.IReadOnlyList<{crudInfo.ClassSymbol.ToFullyQualified()}.ReadModel>");

                generator.WriteMethodCall(crudInfo.ClassSymbol.ToFullyQualified(), allSelectorName, true);
                generator.WriteMethodCallParameter("recordingUnitOfWork", true);
                generator.WriteMethodCallEnd(true);

                generator.WriteMethodCacheMiss($"global::System.Collections.Generic.IReadOnlyList<{crudInfo.ClassSymbol.ToFullyQualified()}.ReadModel>");
                generator.WriteMethodEnd();
            }
            else
            {
                // Generate global selector
                var globalSelectorName = $"Get{crudInfo.Name}";

                generator.WriteMethodSignatureStart(crudInfo.Name, crudInfo.ClassSymbol.ToFullyQualified() + ".ReadModel", true);
                generator.WriteMethodSignatureEnd();

                generator.WriteRecordingUnitOfWork();
                generator.WriteKeyStart(crudInfo.ClassSymbol.ToFullyQualified(), globalSelectorName, true);
                generator.WriteKeyEnd();
                generator.WriteMethodCacheHit(crudInfo.ClassSymbol.ToFullyQualified() + ".ReadModel");

                generator.WriteMethodCall(crudInfo.ClassSymbol.ToFullyQualified(), globalSelectorName, true);
                generator.WriteMethodCallParameter("recordingUnitOfWork", true);
                generator.WriteMethodCallEnd(true);

                generator.WriteMethodCacheMiss(crudInfo.ClassSymbol.ToFullyQualified() + ".ReadModel");
                generator.WriteMethodEnd();
            }
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