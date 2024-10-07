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
            .MakeGenericMethod(typeof(IUnitOfWork));

    private static readonly MethodInfo GetGlobalStateOrDefaultLongMethod =
        typeof(ResolverContextExtensions).GetMethods()
            .First(m => m.Name == nameof(ResolverContextExtensions.GetGlobalStateOrDefault))
            .MakeGenericMethod(typeof(long?));

    private static readonly MethodInfo ServiceUnitOfWorkMethod =
        typeof(IPureResolverContext).GetMethods().First(
                method => method is { Name: nameof(IPureResolverContext.Service), IsGenericMethod: true })
            .MakeGenericMethod(typeof(IUnitOfWork));

    private static readonly MethodInfo WithPrefilledVersionMethod =
        typeof(IUnitOfWork).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .First(m => m.Name == nameof(IUnitOfWork.WithPrefilledVersion));

    public bool CanHandle(ParameterInfo parameter) =>
        typeof(IUnitOfWork) == parameter.ParameterType;

    /*
     * Produces something like this: context.GetOrSetGlobalState(
     *      nameof(IUnitOfWork),
     *      _ =>
     *          context
     *              .Service<IUnitOfWork>()
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

        return Expression.Call(null, GetOrSetGlobalStateUnitOfWorkMethod, context,
            Expression.Constant(nameof(IUnitOfWork)),
            Expression.Lambda<Func<string, IUnitOfWork>>(
                getNewUnitOfWork,
                Expression.Parameter(typeof(string))));
    }

    public ArgumentKind Kind => ArgumentKind.Custom;
    public bool IsPure => false;
    public bool IsDefaultHandler => false;
}