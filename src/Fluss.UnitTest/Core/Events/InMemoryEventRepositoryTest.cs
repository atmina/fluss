using Fluss.Events;
using Fluss.Testing;

namespace Fluss.UnitTest.Core.Events;

public class InMemoryEventRepositoryTest : EventRepositoryTestBase<InMemoryEventRepository>
{
    protected sealed override InMemoryEventRepository Repository { get; set; } = new();

    [Fact]
    public async Task ThrowsOnWrongGetEventsUsage()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Repository.GetEvents(2, 0));
    }
}
