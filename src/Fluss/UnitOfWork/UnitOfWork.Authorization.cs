using Fluss.Authentication;
using Fluss.Events;
using Fluss.ReadModel;
#if !DEBUG
using Fluss.Extensions;
#endif

namespace Fluss.UnitOfWork;

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
        return await _policies.Select(p => p.AuthenticateEvent(envelope, ac)).AnyAsync();
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
        return await _policies.Select(p => p.AuthenticateReadModel(eventListener, ac)).AnyAsync();
#endif
    }
}
