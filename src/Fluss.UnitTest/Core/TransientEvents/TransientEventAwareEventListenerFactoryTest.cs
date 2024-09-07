using Fluss.Events;
using Fluss.Events.TransientEvents;
using Fluss.ReadModel;

namespace Fluss.UnitTest.Core.TransientEvents;

public class TransientEventAwareEventListenerFactoryTest
{
    private readonly TransientEventAwareEventListenerFactory _transientFactory;
    private readonly TransientEventAwareEventRepository _transientRepository;

    public TransientEventAwareEventListenerFactoryTest()
    {
        _transientRepository = new TransientEventAwareEventRepository
        {
            Next = new InMemoryEventRepository()
        };

        _transientFactory =
            new TransientEventAwareEventListenerFactory(_transientRepository)
            {
                Next = new EventListenerFactory(_transientRepository),
            };
    }

    [Fact]
    public async Task AppliesTransientEvents()
    {
        var transientEventEnvelope = new TransientEventEnvelope
        {
            Event = new ExampleTransientEvent(),
            Version = 0,
            At = new DateTimeOffset(),
            ExpiresAt = new DateTimeOffset().AddMinutes(1)
        };

        var readModel = new ExampleReadModel();
        await _transientRepository.Publish([transientEventEnvelope]);

        var updatedReadModel = await _transientFactory.UpdateTo(readModel, -1);

        Assert.Equal(0, updatedReadModel.LastSeenTransientEvent);
    }
}

internal record ExampleReadModel : RootReadModel
{
    protected override EventListener When(EventEnvelope envelope) => this;
}

internal class ExampleTransientEvent : TransientEvent;
