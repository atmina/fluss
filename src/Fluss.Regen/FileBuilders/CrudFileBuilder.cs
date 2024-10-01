using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fluss.Regen.Generators;
using Fluss.Regen.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Fluss.Regen.FileBuilders;

public sealed class CrudFileBuilder : IDisposable
{
    private StringBuilder _sb;
    private CodeWriter _writer;
    private bool _disposed;

    public CrudFileBuilder()
    {
        _sb = StringBuilderPool.Get();
        _writer = new CodeWriter(_sb);
    }

    public void WriteHeader()
    {
        _writer.WriteFileHeader();
    }

    public void WriteCrudClassStart(string @namespace, string className)
    {
        _writer.WriteLine();
        _writer.WriteIndentedLine($"namespace {@namespace}");
        _writer.WriteIndentedLine("{");
        _writer.IncreaseIndent();
        _writer.WriteIndentedLine($"public partial class {className}");
        _writer.WriteIndentedLine("{");
        _writer.IncreaseIndent();
    }

    public void WriteAggregateRootClassStart()
    {
        _writer.WriteIndentedLine("public partial record Aggregate : global::Fluss.Aggregates.AggregateRoot");
        _writer.WriteIndentedLine("{");
        _writer.IncreaseIndent();
    }

    public void WriteAggregateRootWithKeyClassStart(string keyTypeName)
    {
        _writer.WriteIndentedLine("public partial record Aggregate : global::Fluss.Aggregates.AggregateRoot<{0}>", keyTypeName);
        _writer.WriteIndentedLine("{");
        _writer.IncreaseIndent();
    }

    public void WriteReadModelClassStart()
    {
        _writer.WriteLine();
        _writer.WriteIndentedLine("public partial record ReadModel : global::Fluss.ReadModel.RootReadModel");
        _writer.WriteIndentedLine("{");
        _writer.IncreaseIndent();
    }

    public void WriteReadModelWithKeyClassStart(string keyTypeName)
    {
        _writer.WriteLine();
        _writer.WriteIndentedLine("public partial record ReadModel : global::Fluss.ReadModel.ReadModelWithKey<{0}>", keyTypeName);
        _writer.WriteIndentedLine("{");
        _writer.IncreaseIndent();
    }

    public void WriteProperty(string propertyName, string typeName, string? initializer = null)
    {
        if (initializer != null)
        {
            _writer.WriteIndentedLine("public {0} {1} {{ get; init; }} = {2};", typeName, propertyName, initializer);
        }
        else
        {
            _writer.WriteIndentedLine("public {0} {1} {{ get; init; }}", typeName, propertyName);
        }
    }

    public void WriteCreateMethod(string crudName, IPropertySymbol idProperty, IEnumerable<IPropertySymbol> properties)
    {
        _writer.WriteLine();
        _writer.WriteIndentedLine($"public static async global::System.Threading.Tasks.Task<Aggregate> Create(global::Fluss.IWriteUnitOfWork unitOfWork, Commands.{crudName}Create command)");
        _writer.WriteIndentedLine("{");
        _writer.IncreaseIndent();
        _writer.WriteIndentedLine("var id = {0};", GetTypeInitializer(idProperty.Type));
        _writer.WriteIndentedLine($"var aggregate = await unitOfWork.GetAggregate<Aggregate, {idProperty.Type.ToFullyQualified()}>(id);");
        _writer.WriteIndentedLine($"await aggregate.Apply(new Events.{crudName}Created(");

        using (var commaSeparated = _writer.CommaSeparatedIndented())
        {
            commaSeparated.Write("id");

            foreach (var property in properties)
            {
                commaSeparated.Write($"command.{property.Name}");
            }
        }

        _writer.WriteIndentedLine("));");
        _writer.WriteIndentedLine("return aggregate;");
        _writer.DecreaseIndent();
        _writer.WriteIndentedLine("}");
    }

    public void WriteChangeMethod(string crudName, IPropertySymbol? idProperty, IEnumerable<IPropertySymbol> properties)
    {
        _writer.WriteLine();
        _writer.WriteIndentedLine($"public async global::System.Threading.Tasks.Task Change(Commands.{crudName}Change command)");
        _writer.WriteIndentedLine("{");
        _writer.IncreaseIndent();
        foreach (var property in properties)
        {
            _writer.WriteIndentedLine($"if (this.{property.Name} != command.{property.Name})");
            _writer.WriteIndentedLine("{");
            _writer.IncreaseIndent();
            if (idProperty != null)
            {
                _writer.WriteIndentedLine($"await Apply(new Events.{crudName}{property.Name}Changed({idProperty.Name}, command.{property.Name}));");
            }
            else
            {
                _writer.WriteIndentedLine($"await Apply(new Events.{crudName}{property.Name}Changed(command.{property.Name}));");
            }
            _writer.DecreaseIndent();
            _writer.WriteIndentedLine("}");
        }
        _writer.DecreaseIndent();
        _writer.WriteIndentedLine("}");
    }

    public void WriteAggregateWhenMethodStart(bool withCallToExtendWhen)
    {
        _writer.WriteLine();
        _writer.WriteIndentedLine("protected override Aggregate When(global::Fluss.Events.EventEnvelope envelope)");
        _writer.WriteIndentedLine("{");
        _writer.IncreaseIndent();

        if (withCallToExtendWhen)
        {
            _writer.WriteIndentedLine("return (envelope.Event switch");
        }
        else
        {
            _writer.WriteIndentedLine("return envelope.Event switch");
        }

        _writer.WriteIndentedLine("{");
        _writer.IncreaseIndent();
    }

    public void WriteReadModelWhenMethodStart(bool withCallToExtendWhen)
    {
        _writer.WriteLine();
        _writer.WriteIndentedLine("protected override ReadModel When(global::Fluss.Events.EventEnvelope envelope)");
        _writer.WriteIndentedLine("{");
        _writer.IncreaseIndent();

        if (withCallToExtendWhen)
        {
            _writer.WriteIndentedLine("return (envelope.Event switch");
        }
        else
        {
            _writer.WriteIndentedLine("return envelope.Event switch");
        }

        _writer.WriteIndentedLine("{");
        _writer.IncreaseIndent();
    }

    public void WriteWhenPropertyChanged(string crudName, string propertyName, string? idPropertyName)
    {
        var eventName = $"{crudName}{propertyName}Changed";
        var recordName = char.ToLower(eventName[0]) + eventName.Substring(1);

        if (idPropertyName != null)
        {
            _writer.WriteIndentedLine($"Events.{eventName} {recordName} when {recordName}.{idPropertyName} == {idPropertyName} => this with {{ {propertyName} = {recordName}.{propertyName} }},");
        }
        else
        {
            _writer.WriteIndentedLine($"Events.{eventName} {recordName} => this with {{ {propertyName} = {recordName}.{propertyName} }},");
        }
    }

    public void WriteWhenCreated(string crudName, string idPropertyName, IEnumerable<string> propertyNames)
    {
        var createEventName = $"{crudName}Created";
        _writer.WriteIndentedLine($"Events.{createEventName} created when created.{idPropertyName} == {idPropertyName} => this with {{");

        using (var commaSeparated = _writer.CommaSeparatedIndented())
        {
            foreach (var propertyName in propertyNames)
            {
                commaSeparated.Write($"{propertyName} = created.{propertyName}");
            }

            commaSeparated.Write("Exists = true");
        }

        _writer.WriteIndentedLine("},");
    }

    public void WriteWhenDeleted(string crudName, string idPropertyName)
    {
        var deleteEventName = $"{crudName}Deleted";
        _writer.WriteIndentedLine($"Events.{deleteEventName} deleted when deleted.{idPropertyName} == {idPropertyName} => this with {{ Exists = false }},");
    }

    public void WriteWhenMethodEnd(bool withCallToExtendWhen)
    {
        _writer.WriteIndentedLine("_ => this,");
        _writer.DecreaseIndent();

        if (withCallToExtendWhen)
        {
            _writer.WriteIndentedLine("}).ExtendWhen(envelope);");
        }
        else
        {
            _writer.WriteIndentedLine("};");
        }

        _writer.DecreaseIndent();
        _writer.WriteIndentedLine("}");
    }

    public void WriteAggregateOrReadModelEnd()
    {
        _writer.DecreaseIndent();
        _writer.WriteIndentedLine("}");
    }

    public void WriteIdsReadModelStart(string idTypeName)
    {
        _writer.WriteLine();
        _writer.WriteIndentedLine("public partial record AllIds : global::Fluss.ReadModel.RootReadModel");
        _writer.WriteIndentedLine("{");
        _writer.IncreaseIndent();
        _writer.WriteIndentedLine($"public global::System.Collections.Immutable.ImmutableHashSet<{idTypeName}> Ids {{ get; init; }} = global::System.Collections.Immutable.ImmutableHashSet<{idTypeName}>.Empty;");
        _writer.WriteLine();
    }

    public void WriteIdsReadModelWhenMethod(string crudName)
    {
        _writer.WriteIndentedLine("protected override AllIds When(global::Fluss.Events.EventEnvelope envelope)");
        _writer.WriteIndentedLine("{");
        _writer.IncreaseIndent();
        _writer.WriteIndentedLine("return envelope.Event switch");
        _writer.WriteIndentedLine("{");

        using (var commaSeparated = _writer.CommaSeparatedIndented())
        {
            commaSeparated.Write($"Events.{crudName}Created created => this with {{ Ids = Ids.Add(created.Id) }}");
            commaSeparated.Write($"Events.{crudName}Deleted deleted => this with {{ Ids = Ids.Remove(deleted.Id) }}");
            commaSeparated.Write("_ => this");
        }

        _writer.DecreaseIndent();
        _writer.WriteIndentedLine("};");
        _writer.DecreaseIndent();
        _writer.WriteIndentedLine("}");
    }

    public void WriteSingleItemSelector(string crudName, string idType, string idPropertyName)
    {
        _writer.WriteLine();
        _writer.WriteIndentedLine("[global::Fluss.Regen.Selector]");
        _writer.WriteIndentedLine($"public static global::System.Threading.Tasks.ValueTask<ReadModel> Get{crudName}(global::Fluss.IUnitOfWork unitOfWork, {idType} {idPropertyName.ToLowerInvariant()})");
        _writer.WriteIndentedLine("{");
        _writer.IncreaseIndent();
        _writer.WriteIndentedLine($"return unitOfWork.GetReadModel<ReadModel, {idType}>({idPropertyName.ToLowerInvariant()});");
        _writer.DecreaseIndent();
        _writer.WriteIndentedLine("}");
    }

    public void WriteAllItemsSelector(string crudName, string idType)
    {
        _writer.WriteLine();
        _writer.WriteIndentedLine("[global::Fluss.Regen.Selector]");
        _writer.WriteIndentedLine($"public static async global::System.Threading.Tasks.ValueTask<global::System.Collections.Generic.IReadOnlyList<ReadModel>> GetAll{crudName}s(global::Fluss.IUnitOfWork unitOfWork)");
        _writer.WriteIndentedLine("{");
        _writer.IncreaseIndent();
        _writer.WriteIndentedLine("var allIds = await unitOfWork.GetReadModel<AllIds>();");
        _writer.WriteIndentedLine($"return await unitOfWork.GetMultipleReadModels<ReadModel, {idType}>(allIds.Ids);");
        _writer.DecreaseIndent();
        _writer.WriteIndentedLine("}");
    }

    public void WriteGlobalSelector(string crudName)
    {
        _writer.WriteLine();
        _writer.WriteIndentedLine($"[global::Fluss.Regen.Selector]");
        _writer.WriteIndentedLine($"public static global::System.Threading.Tasks.ValueTask<ReadModel> Get{crudName}(global::Fluss.IUnitOfWork unitOfWork)");
        _writer.WriteIndentedLine("{");
        _writer.IncreaseIndent();
        _writer.WriteIndentedLine($"return unitOfWork.GetReadModel<ReadModel>();");
        _writer.DecreaseIndent();
        _writer.WriteIndentedLine("}");
    }

    public void WriteCrudClassEnd()
    {
        _writer.DecreaseIndent();
        _writer.WriteIndentedLine("}");
        _writer.DecreaseIndent();
        _writer.WriteIndentedLine("}");
    }

    public void WriteCommandsClassStart()
    {
        _writer.WriteLine();
        _writer.WriteIndentedLine("public static partial class Commands");
        _writer.WriteIndentedLine("{");
        _writer.IncreaseIndent();
    }

    public void WriteCommandRecord(string name, List<(string Type, string Name)> properties)
    {
        _writer.WriteIndentedLine($"public record {name}(");

        using (var commaSeparated = _writer.CommaSeparatedIndented())
        {
            foreach (var property in properties)
            {
                commaSeparated.Write($"{property.Type} {property.Name}");
            }
        }

        _writer.WriteIndentedLine(");");
    }

    public void WriteCommandsClassEnd()
    {
        _writer.DecreaseIndent();
        _writer.WriteIndentedLine("}");
    }

    public void WriteEventsClassStart()
    {
        _writer.WriteLine();
        _writer.WriteIndentedLine("public static partial class Events");
        _writer.WriteIndentedLine("{");
        _writer.IncreaseIndent();
    }

    public void WriteEventRecord(string name, List<(string Type, string Name)> properties)
    {
        _writer.WriteIndentedLine($"public record {name}(");

        using (var commaSeparated = _writer.CommaSeparatedIndented())
        {
            foreach (var property in properties)
            {
                commaSeparated.Write($"{property.Type} {property.Name}");
            }
        }

        _writer.WriteIndentedLine(") : global::Fluss.Events.Event;");
    }

    public void WriteEventsClassEnd()
    {
        _writer.DecreaseIndent();
        _writer.WriteIndentedLine("}");
    }

    public override string ToString()
        => _sb.ToString();

    public SourceText ToSourceText()
        => SourceText.From(ToString(), Encoding.UTF8);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        StringBuilderPool.Return(_sb);
        _sb = default!;
        _writer = default!;
        _disposed = true;
    }

    public static string? GetTypeInitializer(ITypeSymbol idPropertyType)
    {
        if (idPropertyType.ToFullyQualified() == "global::System.Guid")
        {
            return "global::System.Guid.NewGuid()";
        }

        // Supports StronglyTypedIds
        if (idPropertyType.TypeKind == TypeKind.Struct)
        {
            return $"new {idPropertyType.ToFullyQualified()}(global::System.Guid.NewGuid())";
        }

        return null;
    }
}
