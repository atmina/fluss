using System.ComponentModel;
using System.Reflection;
using HotChocolate.Data.Filters;
using HotChocolate.Execution.Configuration;
using HotChocolate.Language;
using Microsoft.Extensions.DependencyInjection;

namespace Fluss.HotChocolate.AutoGenerateSchema;

public static class AutoGenerateSchema
{
    public static IRequestExecutorBuilder AutoGenerateStronglyTypedIds(
        this IRequestExecutorBuilder requestExecutorBuilder, Assembly assembly)
    {
        var typesToGenerateFor = assembly.GetTypes().Where(t =>
            t.IsValueType && t.CustomAttributes.Any(a =>
                a.AttributeType == typeof(TypeConverterAttribute)));

        foreach (var type in typesToGenerateFor)
        {
            var converterType = GetStronglyTypedIdTypeForType(type);
            requestExecutorBuilder.BindRuntimeType(type, converterType.MakeGenericType(type));
        }

        return requestExecutorBuilder;
    }

    private static Type GetBackingType(Type type)
    {
        return type.GetProperty("Value")?.PropertyType ??
               throw new ArgumentException($"Could not determine backing field type for type {type.Name}");
    }

    private static Type GetStronglyTypedIdTypeForType(Type type)
    {
        var backingType = GetBackingType(type);
        if (backingType == typeof(long))
        {
            return typeof(StronglyTypedLongIdType<>);
        }

        if (backingType == typeof(Guid))
        {
            return typeof(StronglyTypedGuidIdType<>);
        }

        throw new ArgumentException(
            $"Could not find Type converter for strongly typed IDs with backing type {backingType!.Name}");
    }
}

public abstract class StronglyTypedIdType<TId, TScalarType, TCLRType, TNodeType> : ScalarType<TId, TNodeType>
    where TId : struct where TScalarType : ScalarType<TCLRType, TNodeType> where TNodeType : IValueNode
{
    private readonly TScalarType scalarType;

    protected StronglyTypedIdType(TScalarType scalarType) : base(typeof(TId).Name)
    {
        this.scalarType = scalarType;
    }

    protected override TId ParseLiteral(TNodeType valueSyntax)
    {
        var guid = (TCLRType)scalarType.ParseLiteral(valueSyntax)!;

        return (TId)Activator.CreateInstance(typeof(TId), guid)!;
    }

    protected override TNodeType ParseValue(TId runtimeValue)
    {
        return (TNodeType)scalarType.ParseValue(GetInternalValue(runtimeValue));
    }

    public override IValueNode ParseResult(object? resultValue)
    {
        if (resultValue is TId id)
        {
            resultValue = GetInternalValue(id);
        }

        return scalarType.ParseResult(resultValue);
    }

    private TCLRType GetInternalValue(TId obj)
    {
        return (TCLRType)typeof(TId).GetProperty("Value")?.GetMethod?.Invoke(obj, null)!;
    }

    public override bool TrySerialize(object? runtimeValue, out object? resultValue)
    {
        if (runtimeValue is TId id)
        {
            resultValue = GetInternalValue(id);
            return true;
        }

        return base.TrySerialize(runtimeValue, out resultValue);
    }
}

public class StronglyTypedGuidIdType<TId> : StronglyTypedIdType<TId, UuidType, Guid, StringValueNode> where TId : struct
{
    public StronglyTypedGuidIdType() : base(new UuidType('D')) { }
}

public class StronglyTypedLongIdType<TId> : StronglyTypedIdType<TId, LongType, long, IntValueNode> where TId : struct
{
    public StronglyTypedLongIdType() : base(new LongType()) { }
}

public class StronglyTypedIdFilterConventionExtension<TAssemblyReference> : FilterConventionExtension
{
    protected override void Configure(IFilterConventionDescriptor descriptor)
    {
        base.Configure(descriptor);

        var typesToGenerateFor = typeof(TAssemblyReference).Assembly.GetTypes().Where(t =>
            t.IsValueType && t.CustomAttributes.Any(a =>
                a.AttributeType == typeof(TypeConverterAttribute)));


        foreach (var type in typesToGenerateFor)
        {
            var filterInputType = typeof(StronglyTypedGuidIdFilterInput<>).MakeGenericType(type);
            var nullableType = typeof(Nullable<>).MakeGenericType(type);
            descriptor.BindRuntimeType(type, filterInputType);
            descriptor.BindRuntimeType(nullableType, filterInputType);
        }
    }
}

public class StronglyTypedGuidIdFilterInput<TId> : StringOperationFilterInputType
{
    /*public override bool TrySerialize(object? runtimeValue, out object? resultValue) {
        if (runtimeValue is TId id) {
            resultValue = id.ToString();
            return true;
        }

        resultValue = null;
        return false;
    }

    public override bool TryDeserialize(object? resultValue, out object? runtimeValue) {
        var canParseGuid = Guid.TryParse(resultValue?.ToString(), out var parsedGuid);
        if (!canParseGuid) {
            runtimeValue = null;
            return false;
        }

        var tId = Activator.CreateInstance(typeof(TId), parsedGuid);
        runtimeValue = tId;
        return true;
    }*/
}
