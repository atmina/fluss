using Fluss.HotChocolate;
using HotChocolate.Internal;
using HotChocolate.Resolvers;
using Microsoft.CodeAnalysis.Operations;
using Moq;
using ArgumentKind = HotChocolate.Internal.ArgumentKind;

namespace Fluss.UnitTest.HotChocolate;

public class UnitOfWorkParameterExpressionBuilderTest
{
    private readonly UnitOfWorkParameterExpressionBuilder _builder = new();

    [Fact]
    public void CanHandle_ShouldReturnFalseForUnitOfWorkParameter()
    {
        var parameter = typeof(TestClass).GetMethod(nameof(TestClass.MethodWithUnitOfWork))!.GetParameters()[0];
        Assert.False(_builder.CanHandle(parameter));
    }

    [Fact]
    public void CanHandle_ShouldReturnTrueForIUnitOfWorkParameter()
    {
        var parameter = typeof(TestClass).GetMethod(nameof(TestClass.MethodWithIUnitOfWork))!.GetParameters()[0];
        Assert.True(_builder.CanHandle(parameter));
    }

    [Fact]
    public void CanHandle_ShouldReturnFalseForOtherParameters()
    {
        var parameter = typeof(TestClass).GetMethod(nameof(TestClass.MethodWithOtherParameter))!.GetParameters()[0];
        Assert.False(_builder.CanHandle(parameter));
    }

    [Fact]
    public void Kind_ShouldReturnCustom()
    {
        Assert.Equal(ArgumentKind.Custom, _builder.Kind);
    }

    [Fact]
    public void IsPure_ShouldReturnFalse()
    {
        Assert.False(_builder.IsPure);
    }

    [Fact]
    public void IsDefaultHandler_ShouldReturnFalse()
    {
        Assert.False(_builder.IsDefaultHandler);
    }

    private class TestClass
    {
        public void MethodWithUnitOfWork(UnitOfWork uow) { }
        public void MethodWithIUnitOfWork(IUnitOfWork uow) { }
        public void MethodWithOtherParameter(string param) { }
    }
}