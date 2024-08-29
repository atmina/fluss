using Fluss.Authentication;
using Fluss.Events;
using Fluss.ReadModel;
using Moq;

namespace Fluss.UnitTest.Core.Authentication;

public class AuthContextTest
{
    private readonly Mock<IUnitOfWork> _unitOfWork;
    private readonly Guid _userId;
    private readonly AuthContext _authContext;

    public AuthContextTest()
    {
        _unitOfWork = new Mock<IUnitOfWork>();
        _userId = Guid.NewGuid();
        _authContext = new AuthContext(_unitOfWork.Object, _userId);
    }

    [Fact]
    public async Task CanCacheValues()
    {
        var result1 = await _authContext.CacheAndGet("test", () => Task.FromResult("foo"));
        var result2 = await _authContext.CacheAndGet("test", () => Task.FromResult("bar"));

        Assert.Equal("foo", result1);
        Assert.Equal("foo", result2);
    }

    [Fact]
    public async Task ForwardsGetReadModel()
    {
        var testReadModel = new TestReadModel();
        _unitOfWork
            .Setup(uow => uow.UnsafeGetReadModelWithoutAuthorization<TestReadModel>(null))
            .Returns(ValueTask.FromResult(testReadModel));

        Assert.Equal(testReadModel, await _authContext.GetReadModel<TestReadModel>());
    }

    [Fact]
    public async Task ForwardsGetReadModelWithKey()
    {
        var testReadModel = new TestReadModelWithKey { Id = 0 };
        _unitOfWork
            .Setup(uow => uow.UnsafeGetReadModelWithoutAuthorization<TestReadModelWithKey, int>(0, null))
            .Returns(ValueTask.FromResult(testReadModel));

        Assert.Equal(testReadModel, await _authContext.GetReadModel<TestReadModelWithKey, int>(0));
    }

    [Fact]
    public async Task ForwardsGetMultipleReadModels()
    {
        var testReadModel = new TestReadModelWithKey { Id = 0 };
        _unitOfWork
            .Setup(uow => uow.UnsafeGetMultipleReadModelsWithoutAuthorization<TestReadModelWithKey, int>(new[] { 0, 1 }, null))
            .Returns(ValueTask.FromResult((IReadOnlyList<TestReadModelWithKey>)new List<TestReadModelWithKey> { testReadModel }.AsReadOnly()));

        Assert.Equal(new List<TestReadModelWithKey> { testReadModel }, await _authContext.GetMultipleReadModels<TestReadModelWithKey, int>(new[] { 0, 1 }));
    }

    public record TestReadModel : RootReadModel
    {
        protected override EventListener When(EventEnvelope envelope)
        {
            throw new NotImplementedException();
        }
    }

    public record TestReadModelWithKey : ReadModelWithKey<int>
    {
        protected override EventListener When(EventEnvelope envelope)
        {
            throw new NotImplementedException();
        }
    }
}
