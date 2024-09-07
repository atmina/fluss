using Fluss.Authentication;
using Fluss.Events;
using Fluss.ReadModel;

namespace Fluss;

public partial class UnitOfWork
{
    private async ValueTask<bool> AuthorizeUsage(EventEnvelope envelope)
    {
        EnsureInstantiated();

        var ac = AuthContext.Get(this, CurrentUserId());

        try
        {
            foreach (var policy in _policies!)
            {
                if (await policy.AuthenticateEvent(envelope, ac))
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            ac.Return();
        }
    }

    private async ValueTask<bool> AuthorizeUsage(IReadModel eventListener)
    {
        EnsureInstantiated();

        var ac = AuthContext.Get(this, CurrentUserId());

        try
        {
            foreach (var policy in _policies!)
            {
                if (await policy.AuthenticateReadModel(eventListener, ac))
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            ac.Return();
        }
    }
}
