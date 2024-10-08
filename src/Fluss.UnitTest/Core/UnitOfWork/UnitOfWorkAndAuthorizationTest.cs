using Fluss.Authentication;
using Fluss.Events;
using Fluss.ReadModel;

namespace Fluss.UnitTest.Core.UnitOfWork;

public partial class UnitOfWorkTest
{
    [Fact]
    public async Task DoesNotReuseCacheWhenNewEventIsAdded()
    {
        _policies.Add(new AllowReadAfterEventPolicy());

        var unitOfWork = GetUnitOfWork();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            await unitOfWork.GetReadModel<TestReadModel, int>(1);
        });

        await unitOfWork.Publish(new AllowEvent());
        await unitOfWork.GetReadModel<TestReadModel, int>(1);
    }

    private record AllowEvent : Event;

    private record HasAllowEventReadModel : RootReadModel
    {
        public bool HasAllowEvent { get; private init; }

        protected override EventListener When(EventEnvelope envelope)
        {
            return envelope.Event switch
            {
                AllowEvent => this with { HasAllowEvent = true },
                _ => this
            };
        }
    }

    private class AllowReadAfterEventPolicy : Policy
    {
        public ValueTask<bool> AuthenticateEvent(EventEnvelope envelope, IAuthContext authContext)
        {
            return ValueTask.FromResult(true);
        }

        public async ValueTask<bool> AuthenticateReadModel(IReadModel readModel, IAuthContext authContext)
        {
            return (await authContext.GetReadModel<HasAllowEventReadModel>()).HasAllowEvent;
        }
    }

    [Fact]
    public async Task AnEmptyPolicyDoesNotAllowAnything()
    {
        Policy emptyPolicy = new EmptyPolicy();

        Assert.False(await emptyPolicy.AuthenticateEvent(null!, null!));
        Assert.False(await emptyPolicy.AuthenticateReadModel(null!, null!));
    }

    private class EmptyPolicy : Policy;
}
