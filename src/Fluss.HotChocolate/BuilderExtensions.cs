using System.Runtime.CompilerServices;
using HotChocolate.Execution.Configuration;
using HotChocolate.Internal;
using Microsoft.Extensions.DependencyInjection;

[assembly: InternalsVisibleTo("Fluss.HotChocolate.IntegrationTest")]

namespace Fluss.HotChocolate;

public static class BuilderExtensions
{
    public static IRequestExecutorBuilder AddLiveEventSourcing(this IRequestExecutorBuilder reb)
    {
        reb.UseRequest<AddExtensionMiddleware>()
            .RegisterService<UnitOfWorkFactory>(ServiceKind.Synchronized);

        reb.Services
            .AddSingleton<NewEventNotifier, NewEventNotifier>()
            .AddSingleton<IParameterExpressionBuilder, UnitOfWorkParameterExpressionBuilder>()
            .AddSingleton<NewTransientEventNotifier, NewTransientEventNotifier>();

        return reb;
    }
}
