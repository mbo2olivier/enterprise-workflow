using EnterpriseWorkflow.Security;

namespace EnterpriseWorkflow.Presentation;

/// <summary>Server-owned identity label; never accepts a label from form data.</summary>
public sealed class IdentityDisplayNameResolver(ILocalAccountStore? accounts = null, IReadOnlyDictionary<string, IIdentityDirectory>? directories = null) : IIdentityDisplayNameResolver
{
    public async ValueTask<string> ResolveAsync(IdentityReference actor, CancellationToken token)
    {
        if (actor.ProviderId == SecurityProviderIds.Local && accounts is not null)
            return Bound((await accounts.FindByIdentityAsync(actor, token).ConfigureAwait(false)).Value?.UserName ?? actor.SubjectId);
        if (directories?.TryGetValue(actor.ProviderId, out var directory) == true)
            return Bound((await directory.FindAsync(actor, false, token).ConfigureAwait(false)).Value?.DisplayName ?? actor.SubjectId);
        return Bound(actor.SubjectId);
    }
    private static string Bound(string value) => value.Length <= 512 ? value : value[..512];
}
