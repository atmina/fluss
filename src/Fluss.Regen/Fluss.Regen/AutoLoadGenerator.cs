using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Fluss.Regen.Attributes;
using Fluss.Regen.Generators;
using Fluss.Regen.Helpers;
using Fluss.Regen.Inspectors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;


namespace Fluss.Regen;

[Generator]
public class AutoLoadGenerator : IIncrementalGenerator
{
    private static readonly ISyntaxInspector[] Inspectors =
    [
        new SelectorInspector(),
    ];

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(ctx => ctx.AddSource(
            "SelectorAttribute.g.cs",
            SourceText.From(SelectorAttribute.AttributeSourceCode, Encoding.UTF8)));
        
        var modulesAndTypes = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsRelevant(s),
                transform: TryGetModuleOrType)
            .Where(static t => t is not null)!
            .WithComparer(SyntaxInfoComparer.Default);

        var valueProvider = context.CompilationProvider.Combine(modulesAndTypes.Collect());
        
        context.RegisterSourceOutput(
            valueProvider,
            static (context, source) => Execute(context, source.Right));
    }

    private static bool IsRelevant(SyntaxNode node)
        => IsTypeWithAttribute(node) ||
           IsClassWithBaseClass(node) ||
           IsMethodWithAttribute(node);

    private static bool IsClassWithBaseClass(SyntaxNode node)
        => node is ClassDeclarationSyntax { BaseList.Types.Count: > 0, };

    private static bool IsTypeWithAttribute(SyntaxNode node)
        => node is BaseTypeDeclarationSyntax { AttributeLists.Count: > 0, };

    private static bool IsMethodWithAttribute(SyntaxNode node)
        => node is MethodDeclarationSyntax { AttributeLists.Count: > 0, };

    private static ISyntaxInfo? TryGetModuleOrType(
        GeneratorSyntaxContext context,
        CancellationToken cancellationToken)
    {
        foreach (var inspector in Inspectors)
        {
            if (inspector.TryHandle(context, out var syntaxInfo))
            {
                return syntaxInfo;
            }
        }

        return null;
    }

    private static void Execute(
        SourceProductionContext context,
        ImmutableArray<ISyntaxInfo> syntaxInfos)
    {
        if (syntaxInfos.IsEmpty)
        {
            return;
        }

        var syntaxInfoList = syntaxInfos.ToList();
        // WriteSelectorInterfaces(context, syntaxInfoList);
        WriteSelectorMethods(context, syntaxInfoList);
    }

    private static void WriteSelectorInterfaces(SourceProductionContext context, List<ISyntaxInfo> syntaxInfos)
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

        using var generator = new SelectorSyntaxGenerator();
        generator.WriteHeader();
        generator.WriteInterfaceHeader();
        
        foreach (var selector in selectors)
        {
            generator.WriteStartSelectorSelectInterfaceMethod(selector.Name, ExtractValueType(selector.MethodSymbol.ReturnType));

            for (var index = 0; index < selector.MethodSymbol.Parameters.Length; index++)
            {
                var parameter = selector.MethodSymbol.Parameters[index];
                generator.WriteSelectorMethodParameter(parameter.Type, parameter.Name, index == selector.MethodSymbol.Parameters.Length - 1);
            }

            generator.WriteSelectorInterfaceEnd();
        }
        
        generator.WriteEndNamespace();

        context.AddSource("SelectorInterfaces.g.cs", generator.ToSourceText());
    }

    
    private static void WriteSelectorMethods(SourceProductionContext context, List<ISyntaxInfo> syntaxInfos)
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

        using var generator = new SelectorSyntaxGenerator();
        generator.WriteHeader();
        generator.WriteClassHeader();
        
        foreach (var selector in selectors)
        {
            var isAsync = ToTypeNameNoGenerics(selector.MethodSymbol.ReturnType) == typeof(ValueTask).FullName ||
                          ToTypeNameNoGenerics(selector.MethodSymbol.ReturnType) == typeof(Task).FullName;
            
            generator.WriteStartSelectorSelectMethod(selector.Name, ExtractValueType(selector.MethodSymbol.ReturnType));

            for (var index = 0; index < selector.MethodSymbol.Parameters.Length; index++)
            {
                var parameter = selector.MethodSymbol.Parameters[index];
                generator.WriteSelectorMethodParameter(parameter.Type, parameter.Name, index == selector.MethodSymbol.Parameters.Length - 1);
            }

            generator.WriteSelectorMethodCall(selector.ContainingType, selector.Name, isAsync);
            for (var index = 0; index < selector.MethodSymbol.Parameters.Length; index++)
            {
                var parameter = selector.MethodSymbol.Parameters[index];
                generator.WriteSelectorMethodCallParameter(parameter.Name, index == selector.MethodSymbol.Parameters.Length - 1);
            }
            
            generator.WriteSelectorMethodEnd(isAsync);
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