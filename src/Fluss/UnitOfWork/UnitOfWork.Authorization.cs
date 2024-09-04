using Fluss.Authentication;
using Fluss.Events;
using Fluss.ReadModel;

namespace Fluss;

public partial class UnitOfWork
{
    private async ValueTask<bool> AuthorizeUsage(EventEnvelope envelope)
    {
        var ac = new AuthContext(this, CurrentUserId());
#if DEBUG
        var all =
await Task.WhenAll(_policies.Select(async policy => (policy, await policy.AuthenticateEvent(envelope, ac))).ToList());

        var rejected = all.Where(a => !a.Item2).ToList();
        var accepted = all.Where(a => a.Item2).ToList();

        return accepted.Count > 0;
#else
        return await AnyAsync(_policies.Select(p => p.AuthenticateEvent(envelope, ac)));
#endif
    }

    private async ValueTask<bool> AuthorizeUsage(IReadModel eventListener)
    {
        var ac = new AuthContext(this, CurrentUserId());
#if DEBUG
        var all =
await Task.WhenAll(_policies.Select(async policy => (policy, await policy.AuthenticateReadModel(eventListener, ac))).ToList());

        var rejected = all.Where(a => !a.Item2).ToList();
        var accepted = all.Where(a => a.Item2).ToList();

        return accepted.Count > 0;
#else
        return await AnyAsync(_policies.Select(p => p.AuthenticateReadModel(eventListener, ac)));
#endif
    }

    private static async ValueTask<bool> AnyAsync(IEnumerable<ValueTask<bool>> valueTasks)
    {
        foreach (var valueTask in valueTasks)
        {
            if (await valueTask)
            {
                return true;
            }
        }
        return false;
    }
}
