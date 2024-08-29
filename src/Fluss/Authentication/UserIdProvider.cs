namespace Fluss.Authentication;

public class UserIdProvider(Func<IServiceProvider, Guid> func, IServiceProvider serviceProvider)
{
    public Guid Get()
    {
        return func(serviceProvider);
    }
}
