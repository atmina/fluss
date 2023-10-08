namespace Fluss.Authentication;

public class UserIdProvider
{
    private readonly Func<IServiceProvider, Guid> _func;
    private readonly IServiceProvider _serviceProvider;

    public UserIdProvider(Func<IServiceProvider, Guid> func, IServiceProvider serviceProvider)
    {
        _func = func;
        _serviceProvider = serviceProvider;
    }

    public Guid Get()
    {
        return _func(_serviceProvider);
    }
}
