using Fluss.Events;
using Fluss.ReadModel;
using Moq;

namespace Fluss.UnitTest.Core.UnitOfWork;

public class UnitOfWorkRecordingProxyTest
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly UnitOfWorkRecordingProxy _proxy;

    public UnitOfWorkRecordingProxyTest()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _proxy = new UnitOfWorkRecordingProxy(_mockUnitOfWork.Object);
    }

    [Fact]
    public async Task ConsistentVersion_ShouldCallImplementation()
    {
        _mockUnitOfWork.Setup(u => u.ConsistentVersion()).ReturnsAsync(10);

        var result = await _proxy.ConsistentVersion();

        Assert.Equal(10, result);
        _mockUnitOfWork.Verify(u => u.ConsistentVersion(), Times.Once);
    }

    [Fact]
    public void ReadModels_ShouldReturnImplementationReadModels()
    {
        var mockReadModels = new List<EventListener> { new Mock<EventListener>().Object };
        _mockUnitOfWork.Setup(u => u.ReadModels).Returns(mockReadModels);

        var result = _proxy.ReadModels;

        Assert.Same(mockReadModels, result);
    }

    [Fact]
    public async Task GetReadModel_Generic_ShouldRecordListener()
    {
        var mockReadModel = new Mock<TestReadModel>().Object;
        _mockUnitOfWork.Setup(u => u.GetReadModel<TestReadModel>(It.IsAny<long?>()))
            .ReturnsAsync(mockReadModel);

        var result = await _proxy.GetReadModel<TestReadModel>();

        Assert.Same(mockReadModel, result);
        Assert.Single(_proxy.RecordedListeners);
        Assert.Same(mockReadModel, _proxy.RecordedListeners[0]);
    }

    [Fact]
    public async Task GetReadModel_GenericWithKey_ShouldRecordListener()
    {
        var key = "testKey";
        var mockReadModel = new Mock<TestReadModelWithKey>().Object;
        _mockUnitOfWork.Setup(u => u.GetReadModel<TestReadModelWithKey, string>(key, It.IsAny<long?>()))
            .ReturnsAsync(mockReadModel);

        var result = await _proxy.GetReadModel<TestReadModelWithKey, string>(key);

        Assert.Same(mockReadModel, result);
        Assert.Single(_proxy.RecordedListeners);
        Assert.Same(mockReadModel, _proxy.RecordedListeners[0]);
    }

    [Fact]
    public async Task GetReadModel_WithTypeAndKey_ShouldCallImplementation()
    {
        var mockReadModel = new Mock<IReadModel>().Object;
        var type = typeof(TestReadModel);
        var key = "testKey";
        _mockUnitOfWork.Setup(u => u.GetReadModel(type, key, It.IsAny<long?>()))
            .ReturnsAsync(mockReadModel);

        var result = await _proxy.GetReadModel(type, key);

        Assert.Same(mockReadModel, result);
        _mockUnitOfWork.Verify(u => u.GetReadModel(type, key, It.IsAny<long?>()), Times.Once);
    }

    [Fact]
    public async Task UnsafeGetReadModelWithoutAuthorization_Generic_ShouldRecordListener()
    {
        var mockReadModel = new Mock<TestReadModel>().Object;
        _mockUnitOfWork.Setup(u => u.UnsafeGetReadModelWithoutAuthorization<TestReadModel>(It.IsAny<long?>()))
            .ReturnsAsync(mockReadModel);

        var result = await _proxy.UnsafeGetReadModelWithoutAuthorization<TestReadModel>();

        Assert.Same(mockReadModel, result);
        Assert.Single(_proxy.RecordedListeners);
        Assert.Same(mockReadModel, _proxy.RecordedListeners[0]);
    }

    [Fact]
    public async Task UnsafeGetReadModelWithoutAuthorization_GenericWithKey_ShouldRecordListener()
    {
        var key = "testKey";
        var mockReadModel = new Mock<TestReadModelWithKey>().Object;
        _mockUnitOfWork.Setup(u => u.UnsafeGetReadModelWithoutAuthorization<TestReadModelWithKey, string>(key, It.IsAny<long?>()))
            .ReturnsAsync(mockReadModel);

        var result = await _proxy.UnsafeGetReadModelWithoutAuthorization<TestReadModelWithKey, string>(key);

        Assert.Same(mockReadModel, result);
        Assert.Single(_proxy.RecordedListeners);
        Assert.Same(mockReadModel, _proxy.RecordedListeners[0]);
    }

    [Fact]
    public async Task GetMultipleReadModels_ShouldRecordListeners()
    {
        var keys = new[] { "key1", "key2" };
        var mockReadModels = new List<TestReadModelWithKey>
        {
            new Mock<TestReadModelWithKey>().Object,
            new Mock<TestReadModelWithKey>().Object
        };
        _mockUnitOfWork.Setup(u => u.GetMultipleReadModels<TestReadModelWithKey, string>(keys, It.IsAny<long?>()))
            .ReturnsAsync(mockReadModels);

        var result = await _proxy.GetMultipleReadModels<TestReadModelWithKey, string>(keys);

        Assert.Same(mockReadModels, result);
        Assert.Equal(2, _proxy.RecordedListeners.Count);
        Assert.Same(mockReadModels[0], _proxy.RecordedListeners[0]);
        Assert.Same(mockReadModels[1], _proxy.RecordedListeners[1]);
    }

    [Fact]
    public async Task UnsafeGetMultipleReadModelsWithoutAuthorization_ShouldRecordListeners()
    {
        var keys = new[] { "key1", "key2" };
        var mockReadModels = new List<TestReadModelWithKey>
        {
            new Mock<TestReadModelWithKey>().Object,
            new Mock<TestReadModelWithKey>().Object
        };
        _mockUnitOfWork.Setup(u => u.UnsafeGetMultipleReadModelsWithoutAuthorization<TestReadModelWithKey, string>(keys, It.IsAny<long?>()))
            .ReturnsAsync(mockReadModels);

        var result = await _proxy.UnsafeGetMultipleReadModelsWithoutAuthorization<TestReadModelWithKey, string>(keys);

        Assert.Same(mockReadModels, result);
        Assert.Equal(2, _proxy.RecordedListeners.Count);
        Assert.Same(mockReadModels[0], _proxy.RecordedListeners[0]);
        Assert.Same(mockReadModels[1], _proxy.RecordedListeners[1]);
    }

    [Fact]
    public void WithPrefilledVersion_ShouldCallImplementation()
    {
        var version = 5L;
        var mockResult = new Mock<IUnitOfWork>().Object;
        _mockUnitOfWork.Setup(u => u.WithPrefilledVersion(version)).Returns(mockResult);

        var result = _proxy.WithPrefilledVersion(version);

        Assert.Same(mockResult, result);
        _mockUnitOfWork.Verify(u => u.WithPrefilledVersion(version), Times.Once);
    }

    [Fact]
    public void GetRecordedListeners_ShouldReturnCorrectInformation()
    {
        var readModel1 = new TestReadModel()
        {
            LastAcceptedEvent = 5
        };
        _mockUnitOfWork.Setup(u => u.GetReadModel<TestReadModel>(5)).ReturnsAsync(readModel1);

        var readModel2 = new TestReadModelWithKey
        {
            Id = "testKey",
            LastAcceptedEvent = 10
        };
        _mockUnitOfWork.Setup(u => u.GetReadModel<TestReadModelWithKey, string>("testKey", 10)).ReturnsAsync(readModel2);

        _proxy.GetReadModel<TestReadModel>(5);
        _proxy.GetReadModel<TestReadModelWithKey, string>("testKey", 10);

        var result = _proxy.GetRecordedListeners();

        Assert.Equal(2, result.Count);
        Assert.Equal(typeof(TestReadModel), result[0].Type);
        Assert.Null(result[0].Key);
        Assert.Equal(5, result[0].Version);
        Assert.Equal(typeof(TestReadModelWithKey), result[1].Type);
        Assert.Equal("testKey", result[1].Key);
        Assert.Equal(10, result[1].Version);
    }

    [Fact]
    public async Task IsStillUpToDate_ShouldReturnCorrectResult()
    {
        var type = typeof(TestReadModel);
        var key = "testKey";
        var version = 10L;
        var eventListenerTypeWithKeyAndVersion = new UnitOfWorkRecordingProxy.EventListenerTypeWithKeyAndVersion(type, key, version);

        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var readModel = new TestReadModel { LastAcceptedEvent = 8 };
        mockUnitOfWork.Setup(u => u.GetReadModel(type, key, null)).ReturnsAsync(readModel);

        var result = await eventListenerTypeWithKeyAndVersion.IsStillUpToDate(mockUnitOfWork.Object);
        Assert.True(result);

        readModel.LastAcceptedEvent = 12;

        result = await eventListenerTypeWithKeyAndVersion.IsStillUpToDate(mockUnitOfWork.Object);
        Assert.False(result);

        mockUnitOfWork.Verify(u => u.GetReadModel(type, key, null), Times.Exactly(2));
    }

    public record TestReadModel : RootReadModel
    {
        protected override EventListener When(EventEnvelope envelope)
        {
            return this;
        }
    }

    public record TestReadModelWithKey : ReadModelWithKey<string>
    {
        protected override EventListener When(EventEnvelope envelope)
        {
            return this;
        }
    }
}