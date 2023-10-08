using Fluss.Events;
using Fluss.Extensions;
using Fluss.ReadModel;
using Xunit;

namespace Fluss.Testing;

public class ReadModelTestBed : EventTestBed
{
    public ReadModelTestBed ResultsIn<TReadModel>(TReadModel readModel) where TReadModel : RootReadModel, new()
    {
        var eventSourced = EventListenerFactory
            .UpdateTo(new TReadModel(), EventRepository.GetLatestVersion().GetResult()).GetResult();

        Assert.Equal(readModel with { Tag = eventSourced.Tag }, eventSourced);

        AssertReadModelDoesNotReactToCanary(eventSourced);

        return this;
    }

    public ReadModelTestBed ResultsIn<TReadModel, TKey>(TReadModel readModel)
        where TReadModel : ReadModelWithKey<TKey>, new()
    {
        var eventSourced = EventListenerFactory
            .UpdateTo(new TReadModel { Id = readModel.Id }, EventRepository.GetLatestVersion().GetResult()).GetResult();

        Assert.Equal(readModel with { Tag = eventSourced.Tag }, eventSourced);

        AssertReadModelDoesNotReactToCanary(eventSourced);

        return this;
    }

    private void AssertReadModelDoesNotReactToCanary(EventListener readModel)
    {
        Assert.True(readModel == readModel.WhenInt(new EventEnvelope
        {
            At = DateTimeOffset.Now,
            Event = new CanaryEvent(),
            Version = EventRepository.GetLatestVersion().GetResult() + 1
        }), "Read model should not react to arbitrary events");
    }

    public override ReadModelTestBed WithEvents(params Event[] events)
    {
        base.WithEvents(events);
        return this;
    }

    public override ReadModelTestBed WithEventEnvelopes(params EventEnvelope[] eventEnvelopes)
    {
        base.WithEventEnvelopes(eventEnvelopes);
        return this;
    }
}
