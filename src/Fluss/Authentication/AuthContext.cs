using Fluss.Events;
using Fluss.ReadModel;
using Microsoft.Extensions.ObjectPool;

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
    private static readonly ObjectPool<AuthContext> Pool = new DefaultObjectPool<AuthContext>(new DefaultPooledObjectPolicy<AuthContext>());

    internal static AuthContext Get(IUnitOfWork unitOfWork, Guid userId)
    {
        var authContext = Pool.Get();
        authContext._unitOfWork = unitOfWork;
        authContext._userId = userId;
        return authContext;
    }

    private IUnitOfWork _unitOfWork = null!;
    private readonly Dictionary<string, object> _data = new();
    private Guid? _userId;
    public Guid UserId
    {
        get
        {
            EnsureInitialized();
            return _userId!.Value;
        }
    }

    public async ValueTask<T> CacheAndGet<T>(string key, Func<Task<T>> func)
    {
        EnsureInitialized();
        var o = _data.GetValueOrDefault(key);

        switch (o)
        {
            case Task<T> task:
                return await task;
            case T t:
                return t;
        }

        var newTask = func();
        _data[key] = newTask;
        var result = await newTask;
        _data[key] = result!;

        return result;
    }

    public ValueTask<TReadModel> GetReadModel<TReadModel>()
        where TReadModel : EventListener, IRootEventListener, IReadModel, new()
    {
        EnsureInitialized();
        return _unitOfWork.UnsafeGetReadModelWithoutAuthorization<TReadModel>();
    }

    public ValueTask<TReadModel> GetReadModel<TReadModel, TKey>(TKey key)
        where TReadModel : EventListener, IEventListenerWithKey<TKey>, IReadModel, new()
    {
        EnsureInitialized();
        return _unitOfWork.UnsafeGetReadModelWithoutAuthorization<TReadModel, TKey>(key);
    }

    public ValueTask<IReadOnlyList<TReadModel>>
        GetMultipleReadModels<TReadModel, TKey>(IEnumerable<TKey> keys) where TKey : notnull
        where TReadModel : EventListener, IReadModel, IEventListenerWithKey<TKey>, new()
    {
        EnsureInitialized();
        return _unitOfWork.UnsafeGetMultipleReadModelsWithoutAuthorization<TReadModel, TKey>(keys);
    }

    private void EnsureInitialized()
    {
        if (_unitOfWork == null || _userId == null)
        {
            throw new InvalidOperationException("AuthContext is uninitialized");
        }
    }

    internal void Return()
    {
        _userId = null;
        _unitOfWork = null!;
        _data.Clear();

        Pool.Return(this);
    }
}
