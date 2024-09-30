using Fluss.Aggregates;
using Fluss.Events;

namespace Fluss;

public interface IWriteUnitOfWork : IUnitOfWork
{
    ValueTask<TAggregate> GetAggregate<TAggregate, TKey>(TKey key)
        where TAggregate : AggregateRoot<TKey>, new();
    
    ValueTask<TAggregate> GetAggregate<TAggregate>()
        where TAggregate : AggregateRoot, new();

    ValueTask Publish(Event @event, AggregateRoot? aggregate = null);
}
