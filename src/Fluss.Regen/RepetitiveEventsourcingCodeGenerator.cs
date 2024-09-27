using System;
using System.Collections.Immutable;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Fluss.Regen.Attributes;
using Fluss.Regen.Generators;
using Fluss.Regen.Inspectors;
using Fluss.Regen.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;


namespace Fluss.Regen;

[Generator]
public class RepetitiveEventsourcingCodeGenerator : IIncrementalGenerator
{
    private static readonly ISyntaxInspector[] Inspectors =
    [
        new AggregateValidatorInspector(),
        new EventValidatorInspector(),
        new PolicyInspector(),
        new SelectorInspector(),
        new SideEffectInspector(),
        new UpcasterInspector(),
    ];

    private static readonly ISyntaxGenerator[] Generators =
    [
        new SelectorGenerator(),
        new RegistrationGenerator()
    ];
    
    private static readonly IRegenAttribute[] Attributes =
    [
        new SelectorAttribute()
    ];

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(ctx =>
        {
            foreach (var attribute in Attributes)
            {
                ctx.AddSource(
                    attribute.FileName,
                    SourceText.From(attribute.SourceCode, Encoding.UTF8)
                );
            }
        });

        var syntaxInfos = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsRelevant(s),
                transform: TryGetModuleOrType)
            .Where(static t => t is not null)!
            .WithComparer(SyntaxInfoComparer.Default)
            .Collect();

        var valueProvider = context.CompilationProvider.Combine(syntaxInfos);

        context.RegisterSourceOutput(
            valueProvider,
            static (context, source) => Execute(context, source.Left, source.Right));
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

    private static SyntaxInfo? TryGetModuleOrType(
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

    private static void Execute(SourceProductionContext context,
        Compilation compilation,
        ImmutableArray<SyntaxInfo> syntaxInfos)
    {
        foreach (var syntaxInfo in syntaxInfos.AsSpan())
        {
            foreach (var diagnostic in syntaxInfo.Diagnostics.AsSpan())
            {
                context.ReportDiagnostic(diagnostic);
            }
        }

        foreach (var generator in Generators.AsSpan())
        {
            generator.Generate(context, compilation, syntaxInfos);
        }
    }
}
