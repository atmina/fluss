using System.Collections.ObjectModel;
using Fluss.Events;
using Moq;

namespace Fluss.UnitTest.Core.Events;

public class InMemoryCacheTest
{
    private readonly Mock<IBaseEventRepository> _baseRepository;
    private readonly InMemoryEventCache _cache;

    public InMemoryCacheTest()
    {
        _cache = new InMemoryEventCache(20);
        _baseRepository = new Mock<IBaseEventRepository>();

        _cache.Next = _baseRepository.Object;
    }

    [Fact]
    public async Task CallsBaseRepository()
    {
        _baseRepository.Setup(repository => repository.GetEvents(-1, 9))
            .Returns(ValueTask.FromResult(GetMockEnvelopes(0, 9)));

        var events = (await _cache.GetEvents(-1, 9)).ToFlatEventList();

        _baseRepository.Verify(repository => repository.GetEvents(-1, 9), Times.Once);

        Assert.Equal(10, events.Count);

        for (var i = 0; i < events.Count; i++)
        {
            Assert.Equal(i, events[i].Version);
        }
    }

    [Fact]
    public async Task DoesNotCallBaseRepositoryTwice()
    {
        _baseRepository.Setup(repository => repository.GetEvents(-1, 4))
            .Returns(ValueTask.FromResult(GetMockEnvelopes(0, 4)));

        await _cache.GetEvents(-1, 4);
        await _cache.GetEvents(-1, 4);

        _baseRepository.Verify(repository => repository.GetEvents(-1, 4), Times.Once);
    }

    [Fact]
    public async Task DoesNotCallForPublishedEvents()
    {
        var events = GetMockEnvelopes(0, 4).ToFlatEventList();

        await _cache.Publish(events);
        await _cache.GetEvents(-1, 4);

        _baseRepository.Verify(repository => repository.Publish(events), Times.Once);
        _baseRepository.Verify(repository => repository.GetEvents(-1, 4), Times.Never);
    }

    [Fact]
    public async Task CanRequestSecondCacheItem()
    {
        _baseRepository.Setup(repository => repository.GetEvents(-1, 39))
            .Returns(ValueTask.FromResult(GetMockEnvelopes(0, 39)));

        await _cache.GetEvents(-1, 39);
        var events = (await _cache.GetEvents(19, 39)).ToFlatEventList();

        _baseRepository.Verify(repository => repository.GetEvents(-1, 39), Times.Once);

        Assert.Equal(20, events.Count);

        for (var i = 0; i < events.Count; i++)
        {
            Assert.Equal(i + 20, events[i].Version);
        }
    }


    [Fact]
    public async Task CanRequestAcrossMultipleCacheItems()
    {
        _baseRepository.Setup(repository => repository.GetEvents(-1, 59))
            .Returns(ValueTask.FromResult(GetMockEnvelopes(0, 59)));

        await _cache.GetEvents(-1, 59);
        var events = (await _cache.GetEvents(15, 45)).ToFlatEventList();

        _baseRepository.Verify(repository => repository.GetEvents(-1, 59), Times.Once);

        Assert.Equal(30, events.Count);

        for (var i = 0; i < events.Count; i++)
        {
            Assert.Equal(i + 16, events[i].Version);
        }
    }

    private ReadOnlyCollection<ReadOnlyMemory<EventEnvelope>> GetMockEnvelopes(int from, int to)
    {
        return Enumerable.Range(from, to - from + 1).Select(version =>
                new EventEnvelope { At = DateTimeOffset.Now, By = null, Version = version, Event = new MockEvent() })
            .ToList().ToPagedMemory();
    }

    private class MockEvent : Event { }
}
