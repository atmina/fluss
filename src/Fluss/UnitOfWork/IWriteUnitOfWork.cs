using System.Collections.Concurrent;
using Fluss.Aggregates;
using Fluss.Events;
using Fluss.ReadModel;

namespace Fluss;

public interface IWriteUnitOfWork : IUnitOfWork
{
    ValueTask<TAggregate> GetAggregate<TAggregate, TKey>(TKey key)
        where TAggregate : AggregateRoot<TKey>, new();

    ValueTask Publish(Event @event, AggregateRoot? aggregate = null);
}
