using Fluss.Events;
using Fluss.ReadModel;

namespace Fluss.Authentication;

public interface IAuthContext
{
    public ValueTask<T> CacheAndGet<T>(string key, Func<Task<T>> func);

    public ValueTask<TReadModel> GetReadModel<TReadModel>()
        where TReadModel : EventListener, IRootEventListener, IReadModel, new();

    public ValueTask<TReadModel> GetReadModel<TReadModel, TKey>(TKey key)
        where TReadModel : EventListener, IEventListenerWithKey<TKey>, IReadModel, new();

    public ValueTask<IReadOnlyList<TReadModel>>
        GetMultipleReadModels<TReadModel, TKey>(IEnumerable<TKey> keys) where TKey : notnull
        where TReadModel : EventListener, IReadModel, IEventListenerWithKey<TKey>, new();

    public Guid UserId { get; }
}

internal class AuthContext : IAuthContext
{
    private readonly IUnitOfWork _unitOfWork;
    public readonly Dictionary<string, object> Data = new();
    public Guid UserId { get; private set; }

    public AuthContext(IUnitOfWork unitOfWork, Guid userId)
    {
        _unitOfWork = unitOfWork;
        UserId = userId;
    }

    public async ValueTask<T> CacheAndGet<T>(string key, Func<Task<T>> func)
    {
        var o = Data.ContainsKey(key) ? Data[key] : null;

        switch (o)
        {
            case Task<T> task:
                return await task;
            case T t:
                return t;
        }

        var newTask = func();
        Data[key] = newTask;
        var result = await newTask;
        Data[key] = result!;

        return result;
    }

    public ValueTask<TReadModel> GetReadModel<TReadModel>()
        where TReadModel : EventListener, IRootEventListener, IReadModel, new()
    {
        return _unitOfWork.UnsafeGetReadModelWithoutAuthorization<TReadModel>();
    }

    public ValueTask<TReadModel> GetReadModel<TReadModel, TKey>(TKey key)
        where TReadModel : EventListener, IEventListenerWithKey<TKey>, IReadModel, new()
    {
        return _unitOfWork.UnsafeGetReadModelWithoutAuthorization<TReadModel, TKey>(key);
    }

    public ValueTask<IReadOnlyList<TReadModel>>
        GetMultipleReadModels<TReadModel, TKey>(IEnumerable<TKey> keys) where TKey : notnull
        where TReadModel : EventListener, IReadModel, IEventListenerWithKey<TKey>, new()
    {
        return _unitOfWork.UnsafeGetMultipleReadModelsWithoutAuthorization<TReadModel, TKey>(keys);
    }
}
