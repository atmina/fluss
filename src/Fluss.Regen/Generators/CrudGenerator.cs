using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Fluss.Regen.FileBuilders;
using Fluss.Regen.Helpers;
using Fluss.Regen.Inspectors;
using Fluss.Regen.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Fluss.Regen.Generators;

// TODO: Add way to extend the when method of aggregate / readmodel

public class CrudGenerator : ISyntaxGenerator
{
    public void Generate(SourceProductionContext context, Compilation compilation, ImmutableArray<SyntaxInfo> syntaxInfos)
    {
        using var fileBuilder = new CrudFileBuilder();
        fileBuilder.WriteHeader();

        foreach (var crudInfo in syntaxInfos.OfType<CrudInfo>())
        {
            if (!crudInfo.Diagnostics.IsEmpty)
            {
                continue;
            }

            fileBuilder.WriteCrudClassStart(crudInfo.Namespace, crudInfo.Name);

            if (crudInfo.IdProperty == null)
            {
                fileBuilder.WriteAggregateRootClassStart();
            }
            else
            {
                fileBuilder.WriteAggregateRootWithKeyClassStart(GetKeyTypeName(crudInfo.IdProperty));
            }

            WriteProperties(fileBuilder, crudInfo, true);

            if (crudInfo.IdProperty != null)
            {
                fileBuilder.WriteCreateMethod(crudInfo.Name, crudInfo.IdProperty, crudInfo.Properties);
            }

            fileBuilder.WriteChangeMethod(crudInfo.Name, crudInfo.IdProperty, crudInfo.Properties);

            WriteWhenMethod(fileBuilder, crudInfo, true);
            fileBuilder.WriteAggregateOrReadModelEnd();

            if (crudInfo.IdProperty == null)
            {
                fileBuilder.WriteReadModelClassStart();
            }
            else
            {
                fileBuilder.WriteReadModelWithKeyClassStart(GetKeyTypeName(crudInfo.IdProperty));
            }

            WriteProperties(fileBuilder, crudInfo, false);
            WriteWhenMethod(fileBuilder, crudInfo, false);
            fileBuilder.WriteAggregateOrReadModelEnd();

            WriteIdsReadModel(fileBuilder, crudInfo);
            WriteSelectors(fileBuilder, crudInfo);
            WriteCommands(fileBuilder, crudInfo);
            WriteEvents(fileBuilder, crudInfo);

            fileBuilder.WriteCrudClassEnd();
        }

        context.AddSource("Crud.g.cs", fileBuilder.ToSourceText());
    }

    private static string GetKeyTypeName(IPropertySymbol idProperty)
    {
        return idProperty.Type.ToFullyQualified();
    }

    private void WriteProperties(CrudFileBuilder fileBuilder, CrudInfo crudInfo, bool isAggregate)
    {
        foreach (var property in crudInfo.Properties)
        {
            var initializer = property.DeclaringSyntaxReferences.Select(dsr => (dsr.GetSyntax() as PropertyDeclarationSyntax)?.Initializer?.Value.ToString()).FirstOrDefault();
            fileBuilder.WriteProperty(property.Name, property.Type.ToFullyQualified(), initializer);
        }

        if (crudInfo.IdProperty != null && !isAggregate)
        {
            fileBuilder.WriteProperty("Exists", "bool");
        }
    }

    private void WriteWhenMethod(CrudFileBuilder fileBuilder, CrudInfo crudInfo, bool isAggregate)
    {
        bool withCallToExtendWhen;
        if (isAggregate)
        {
            withCallToExtendWhen = crudInfo.ClassSymbol.GetMembers("Aggregate").Any(member =>
                member is ITypeSymbol typeSymbol && typeSymbol.GetMembers("ExtendWhen").Length > 0);
            fileBuilder.WriteAggregateWhenMethodStart(withCallToExtendWhen);
        }
        else
        {
            withCallToExtendWhen = crudInfo.ClassSymbol.GetMembers("ReadModel").Any(member =>
                member is ITypeSymbol typeSymbol && typeSymbol.GetMembers("ExtendWhen").Length > 0);
            fileBuilder.WriteReadModelWhenMethodStart(withCallToExtendWhen);
        }

        foreach (var property in crudInfo.Properties)
        {
            fileBuilder.WriteWhenPropertyChanged(crudInfo.Name, property.Name, crudInfo.IdProperty?.Name);
        }

        if (crudInfo.IdProperty != null)
        {
            fileBuilder.WriteWhenCreated(crudInfo.Name, crudInfo.IdProperty.Name, crudInfo.Properties.Select(p => p.Name));
            fileBuilder.WriteWhenDeleted(crudInfo.Name, crudInfo.IdProperty.Name);
        }
        fileBuilder.WriteWhenMethodEnd(withCallToExtendWhen);
    }

    private void WriteIdsReadModel(CrudFileBuilder fileBuilder, CrudInfo crudInfo)
    {
        if (crudInfo.IdProperty == null)
        {
            return;
        }

        fileBuilder.WriteIdsReadModelStart(GetKeyTypeName(crudInfo.IdProperty));
        fileBuilder.WriteIdsReadModelWhenMethod(crudInfo.Name);
        fileBuilder.WriteAggregateOrReadModelEnd();
    }

    private void WriteSelectors(CrudFileBuilder fileBuilder, CrudInfo crudInfo)
    {
        if (crudInfo.IdProperty != null)
        {
            fileBuilder.WriteSingleItemSelector(crudInfo.Name, crudInfo.IdProperty.Type.ToFullyQualified(), crudInfo.IdProperty.Name);
            fileBuilder.WriteAllItemsSelector(crudInfo.Name, crudInfo.IdProperty.Type.ToFullyQualified());
        }
        else
        {
            fileBuilder.WriteGlobalSelector(crudInfo.Name);
        }
    }

    private void WriteCommands(CrudFileBuilder fileBuilder, CrudInfo crudInfo)
    {
        fileBuilder.WriteCommandsClassStart();

        if (crudInfo.IdProperty != null)
        {
            var createProperties = crudInfo.Properties.Select(p => (p.Type.ToFullyQualified(), p.Name)).ToList();
            fileBuilder.WriteCommandRecord($"{crudInfo.Name}Create", createProperties);

            // Replace individual change commands with a single Change command
            var changeProperties = new List<(string, string)> { (crudInfo.IdProperty.Type.ToFullyQualified(), crudInfo.IdProperty.Name) };
            changeProperties.AddRange(crudInfo.Properties.Select(p => (p.Type.ToFullyQualified(), p.Name)));
            fileBuilder.WriteCommandRecord($"{crudInfo.Name}Change", changeProperties);
        }
        else
        {
            // For global entities, we only need a single Change command
            var changeProperties = crudInfo.Properties.Select(p => (p.Type.ToFullyQualified(), p.Name)).ToList();
            fileBuilder.WriteCommandRecord($"{crudInfo.Name}Change", changeProperties);
        }

        fileBuilder.WriteCommandsClassEnd();
    }

    private void WriteEvents(CrudFileBuilder fileBuilder, CrudInfo crudInfo)
    {
        fileBuilder.WriteEventsClassStart();

        if (crudInfo.IdProperty != null)
        {
            var createProperties = crudInfo.AllProperties.Select(p => (p.Type.ToFullyQualified(), p.Name)).ToList();
            fileBuilder.WriteEventRecord($"{crudInfo.Name}Created", createProperties);

            foreach (var property in crudInfo.Properties)
            {
                fileBuilder.WriteEventRecord($"{crudInfo.Name}{property.Name}Changed",
                    new List<(string, string)> { (crudInfo.IdProperty.Type.ToFullyQualified(), crudInfo.IdProperty.Name), (property.Type.ToFullyQualified(), property.Name) });
            }

            fileBuilder.WriteEventRecord($"{crudInfo.Name}Deleted", new List<(string, string)> { (crudInfo.IdProperty.Type.ToFullyQualified(), crudInfo.IdProperty.Name) });
        }
        else
        {
            foreach (var property in crudInfo.Properties)
            {
                fileBuilder.WriteEventRecord($"{crudInfo.Name}{property.Name}Changed",
                    new List<(string, string)> { (property.Type.ToFullyQualified(), property.Name) });
            }
        }

        fileBuilder.WriteCommandsClassEnd();
    }
}