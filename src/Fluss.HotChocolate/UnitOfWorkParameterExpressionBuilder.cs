using HotChocolate.Internal;
using HotChocolate.Resolvers;

namespace Fluss.HotChocolate;

public class UnitOfWorkParameterExpressionBuilder() :
    CustomParameterExpressionBuilder<IUnitOfWork>(ctx => CreateIUnitOfWork(ctx))
{
    private static IUnitOfWork CreateIUnitOfWork(IResolverContext context)
    {
        var unitOfWork = context.GetOrSetGlobalState(
           nameof(IUnitOfWork),
           _ =>
           {
               var createdUnitOfWork = context
                   .Service<IUnitOfWork>()
                   .WithPrefilledVersion(
                       context.GetGlobalState<long>(PrefillUnitOfWorkVersion)
                   );
               
               ((IMiddlewareContext)context).RegisterForCleanup(createdUnitOfWork.Return);

               return createdUnitOfWork;
           });

        return unitOfWork;
    }
    
    public const string PrefillUnitOfWorkVersion = nameof(AddExtensionMiddleware) + ".prefillUnitOfWorkVersion";

    public ArgumentKind Kind => ArgumentKind.Custom;
    public bool IsPure => false;
}