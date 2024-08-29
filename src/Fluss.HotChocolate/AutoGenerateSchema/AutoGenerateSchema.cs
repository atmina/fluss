using System.ComponentModel;
using System.Reflection;
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
            $"Could not find Type converter for strongly typed IDs with backing type {backingType.Name}");
    }
}

public abstract class StronglyTypedIdType<TId, TScalarType, TCLRType, TNodeType>(TScalarType scalarType)
    : ScalarType<TId, TNodeType>(typeof(TId).Name)
    where TId : struct
    where TScalarType : ScalarType<TCLRType, TNodeType>
    where TNodeType : IValueNode
{
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

public class StronglyTypedGuidIdType<TId>()
    : StronglyTypedIdType<TId, UuidType, Guid, StringValueNode>(new UuidType('D'))
    where TId : struct;

public class StronglyTypedLongIdType<TId>() : StronglyTypedIdType<TId, LongType, long, IntValueNode>(new LongType())
    where TId : struct;
