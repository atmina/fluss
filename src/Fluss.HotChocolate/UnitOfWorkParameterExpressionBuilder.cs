using System.Linq.Expressions;
using System.Reflection;
using HotChocolate.Internal;
using HotChocolate.Resolvers;

namespace Fluss.HotChocolate;

public class UnitOfWorkParameterExpressionBuilder : IParameterExpressionBuilder
{
    public const string PrefillUnitOfWorkVersion = nameof(AddExtensionMiddleware) + ".prefillUnitOfWorkVersion";

    private static readonly MethodInfo GetOrSetGlobalStateUnitOfWorkMethod =
        typeof(ResolverContextExtensions).GetMethods()
            .First(m => m.Name == nameof(ResolverContextExtensions.GetOrSetGlobalState))
            .MakeGenericMethod(typeof(UnitOfWork));

    private static readonly MethodInfo GetGlobalStateOrDefaultLongMethod =
        typeof(ResolverContextExtensions).GetMethods()
            .First(m => m.Name == nameof(ResolverContextExtensions.GetGlobalStateOrDefault))
            .MakeGenericMethod(typeof(long?));

    private static readonly MethodInfo ServiceUnitOfWorkMethod =
        typeof(IPureResolverContext).GetMethods().First(
            method => method.Name == nameof(IPureResolverContext.Service) &&
                      method.IsGenericMethod)
            .MakeGenericMethod(typeof(UnitOfWork));

    private static readonly MethodInfo GetValueOrDefaultMethod =
        typeof(CollectionExtensions).GetMethods().First(m => m.Name == nameof(CollectionExtensions.GetValueOrDefault) && m.GetParameters().Length == 2);

    private static readonly MethodInfo WithPrefilledVersionMethod =
        typeof(UnitOfWork).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .First(m => m.Name == nameof(UnitOfWork.WithPrefilledVersion));

    private static readonly PropertyInfo ContextData =
        typeof(IHasContextData).GetProperty(
            nameof(IHasContextData.ContextData))!;

    public bool CanHandle(ParameterInfo parameter) => typeof(UnitOfWork) == parameter.ParameterType
        || typeof(IUnitOfWork) == parameter.ParameterType;

    /*
     * Produces something like this: context.GetOrSetGlobalState(
     *      nameof(UnitOfWork.UnitOfWork),
     *      _ =>
     *          context
     *              .Service<UnitOfWork.UnitOfWork>()
     *              .WithPrefilledVersion(
     *                  context.GetGlobalState<long>(PrefillUnitOfWorkVersion)
     *              ))!;
     */
    public Expression Build(ParameterExpressionBuilderContext builderContext)
    {
        var context = builderContext.ResolverContext;
        var getNewUnitOfWork = Expression.Call(
            Expression.Call(context, ServiceUnitOfWorkMethod),
            WithPrefilledVersionMethod,
            Expression.Call(
                null,
                GetGlobalStateOrDefaultLongMethod,
                context,
                Expression.Constant(PrefillUnitOfWorkVersion)));

        return Expression.Call(null, GetOrSetGlobalStateUnitOfWorkMethod, context, Expression.Constant(nameof(UnitOfWork)),
            Expression.Lambda<Func<string, UnitOfWork>>(
                getNewUnitOfWork,
                Expression.Parameter(typeof(string))));
    }

    public ArgumentKind Kind => ArgumentKind.Custom;
    public bool IsPure => true;
    public bool IsDefaultHandler => false;
}
