using Fluss.Events;
using Moq;

namespace Fluss.UnitTest.Core.Events;

public class InMemoryListenerCacheTest
{
    private readonly Mock<IEventListenerFactory> _baseEventListenerFactory;
    private readonly InMemoryEventListenerCache _listenerCache;

    public InMemoryListenerCacheTest()
    {
        _baseEventListenerFactory = new Mock<IEventListenerFactory>();
        _listenerCache = new InMemoryEventListenerCache
        {
            Next = _baseEventListenerFactory.Object,
        };
    }

    // ReSharper disable ReturnValueOfPureMethodIsNotUsed

    [Fact]
    public async Task PassesUpdatesToNext()
    {
        _baseEventListenerFactory.Setup(f => f.UpdateTo(It.IsAny<TestEventListener>(), 100))
            .Returns(ValueTask.FromResult(new TestEventListener()));

        _ = await _listenerCache.UpdateTo(new TestEventListener(), 100);

        _baseEventListenerFactory.Verify(
            f => f.UpdateTo(
                It.IsAny<TestEventListener>(),
                100
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task ReturnsCachedEventListener()
    {
        _baseEventListenerFactory.Setup(f => f.UpdateTo(It.IsAny<TestEventListener>(), 100))
            .Returns(ValueTask.FromResult(new TestEventListener { Tag = new EventListenerVersionTag(100) }));

        _ = await _listenerCache.UpdateTo(new TestEventListener(), 100);
        _ = await _listenerCache.UpdateTo(new TestEventListener(), 100);

        _baseEventListenerFactory.Verify(
            f => f.UpdateTo(
                It.IsAny<TestEventListener>(),
                100
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task ReturnsCachedKeyedEventListener()
    {
        _baseEventListenerFactory.Setup(f => f.UpdateTo(It.IsAny<KeyedTestEventListener>(), 100))
            .Returns(ValueTask.FromResult(new KeyedTestEventListener { Id = 1, Tag = new EventListenerVersionTag(100) }));

        await _listenerCache.UpdateTo(new KeyedTestEventListener { Id = 1 }, 100);
        await _listenerCache.UpdateTo(new KeyedTestEventListener { Id = 1 }, 100);

        _baseEventListenerFactory.Verify(
            f => f.UpdateTo(
                It.IsAny<KeyedTestEventListener>(),
                100
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task ForwardsIfCacheContainsNewer()
    {
        _baseEventListenerFactory.Setup(f => f.UpdateTo(It.IsAny<TestEventListener>(), 100))
            .Returns(ValueTask.FromResult(new TestEventListener { Tag = new EventListenerVersionTag(100) }));
        _baseEventListenerFactory.Setup(f => f.UpdateTo(It.IsAny<TestEventListener>(), 90))
            .Returns(ValueTask.FromResult(new TestEventListener { Tag = new EventListenerVersionTag(90) }));

        await _listenerCache.UpdateTo(new TestEventListener(), 100);
        await _listenerCache.UpdateTo(new TestEventListener(), 90);

        _baseEventListenerFactory.Verify(
            f => f.UpdateTo(
                It.IsAny<TestEventListener>(),
                100
            ),
            Times.Once
        );
        _baseEventListenerFactory.Verify(
            f => f.UpdateTo(
                It.IsAny<TestEventListener>(),
                90
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task UpdatesStoreWithNewerVersion()
    {
        var testEventListener = new TestEventListener();
        var otherTestEventList = new TestEventListener();

        _baseEventListenerFactory.Setup(f => f.UpdateTo(It.IsAny<TestEventListener>(), 100))
            .Returns(ValueTask.FromResult(new TestEventListener { Tag = new EventListenerVersionTag(100) }));
        _baseEventListenerFactory.Setup(f => f.UpdateTo(It.IsAny<TestEventListener>(), 110))
            .Returns(ValueTask.FromResult(new TestEventListener { Tag = new EventListenerVersionTag(110) }));

        await _listenerCache.UpdateTo(testEventListener, 100);

        await _listenerCache.UpdateTo(otherTestEventList, 110);
        await _listenerCache.UpdateTo(otherTestEventList, 110);

        _baseEventListenerFactory.Verify(
            f => f.UpdateTo(
                It.IsAny<TestEventListener>(),
                110
            ),
            Times.Once
        );
    }

    [Fact]
    public async Task ForwardsAgainIfCleaned()
    {
        _baseEventListenerFactory.Setup(f => f.UpdateTo(It.IsAny<TestEventListener>(), 100))
            .Returns(ValueTask.FromResult(new TestEventListener { Tag = new EventListenerVersionTag(100) }));

        await _listenerCache.UpdateTo(new TestEventListener(), 100);
        _listenerCache.Clean();
        await _listenerCache.UpdateTo(new TestEventListener(), 100);


        _baseEventListenerFactory.Verify(
            f => f.UpdateTo(
                It.IsAny<TestEventListener>(),
                100
            ),
            Times.Exactly(2)
        );
    }

    private record TestEventListener : EventListener
    {
        protected override TestEventListener When(EventEnvelope envelope)
        {
            return this;
        }
    }

    private record KeyedTestEventListener : EventListener, IEventListenerWithKey<int>
    {
        public int Id { get; init; }

        protected override KeyedTestEventListener When(EventEnvelope envelope)
        {
            return this;
        }
    }
}
