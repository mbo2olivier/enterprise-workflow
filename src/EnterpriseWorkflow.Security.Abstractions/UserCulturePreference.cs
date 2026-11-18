namespace EnterpriseWorkflow.Security;

/// <summary>Local, provider-independent profile preference. Null means the installation default.</summary>
public interface IUserCulturePreferenceStore
{
    ValueTask<string?> ReadCultureAsync(IdentityReference identity, CancellationToken cancellationToken);
    ValueTask SetCultureAsync(IdentityReference identity, string? culture, CancellationToken cancellationToken);
}
