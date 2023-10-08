using Fluss.Events;
using Fluss.ReadModel;

namespace Fluss.Authentication;

public interface Policy
{
    public ValueTask<bool> AuthenticateEvent(EventEnvelope envelope, IAuthContext authContext)
    {
        return ValueTask.FromResult(false);
    }

    public ValueTask<bool> AuthenticateReadModel(IReadModel readModel, IAuthContext authContext)
    {
        return ValueTask.FromResult(false);
    }
}
