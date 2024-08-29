using Fluss.UnitOfWork;

namespace Fluss.Authentication;

public static class SystemUser
{
    public static readonly Guid SystemUserGuid = Guid.Parse("9c4749e6-1b58-4ef3-8573-930cd617b181");

    public static UnitOfWorkFactory GetSystemUserUnitOfWorkFactory(this IServiceProvider serviceProvider)
    {
        return serviceProvider.GetUserUnitOfWorkFactory(SystemUserGuid);
    }

    public static IUnitOfWork GetSystemUserUnitOfWork(this IServiceProvider serviceProvider)
    {
        return serviceProvider.GetUserUnitOfWork(SystemUserGuid);
    }

    public static bool IsSystemUser(this IAuthContext authContext)
    {
        return authContext.UserId == SystemUserGuid;
    }
}
