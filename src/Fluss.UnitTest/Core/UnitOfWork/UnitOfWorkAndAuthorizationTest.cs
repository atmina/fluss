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

        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            await _unitOfWork.GetReadModel<UnitOfWorkTest.TestReadModel, int>(1);
        });

        await _unitOfWork.Publish(new AllowEvent());
        await _unitOfWork.GetReadModel<UnitOfWorkTest.TestReadModel, int>(1);
    }

    private record AllowEvent : Event;

    private record HasAllowEventReadModel : RootReadModel
    {
        public bool HasAllowEvent { get; init; } = false;

        protected override EventListener When(EventEnvelope envelope)
        {
            return envelope.Event switch
            {
                AllowEvent => this with { HasAllowEvent = true },
                _ => this
            };
        }
    };

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
}
