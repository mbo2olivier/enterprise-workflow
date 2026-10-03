using System.DirectoryServices.Protocols;
using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace EnterpriseWorkflow.Security;

public sealed class ActiveDirectoryProvider(
    ActiveDirectoryOptions options,
    IActiveDirectoryServiceCredentialProvider serviceCredentials) : IAuthenticationProvider, IIdentityDirectory
{
    public string ProviderId => options.ProviderId;

    public async ValueTask<AuthenticationResult> AuthenticateAsync(PasswordCredential credential, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credential);
        options.Validate();
        if (string.IsNullOrWhiteSpace(credential.UserName) || string.IsNullOrEmpty(credential.Password))
            return new AuthenticationResult(AuthenticationStatus.Rejected, ErrorCode: "security.invalid-credentials");

        var found = await SearchOneAsync(credential.UserName, includeGroups: false, cancellationToken).ConfigureAwait(false);
        if (found.Outcome is ProviderOutcome.Unavailable)
            return new AuthenticationResult(AuthenticationStatus.Unavailable, ErrorCode: found.ErrorCode);
        if (found.Outcome is not ProviderOutcome.Succeeded || found.Value is null)
            return new AuthenticationResult(AuthenticationStatus.Rejected, ErrorCode: "security.invalid-credentials");

        try
        {
            using var connection = CreateConnection(new NetworkCredential(found.Value.DistinguishedName, credential.Password));
            connection.Bind();
            return new AuthenticationResult(AuthenticationStatus.Succeeded, found.Value.Identity.Identity);
        }
        catch (LdapException exception) when (exception.ErrorCode == 49)
        {
            return new AuthenticationResult(AuthenticationStatus.Rejected, ErrorCode: "security.invalid-credentials");
        }
        catch (LdapException)
        {
            return new AuthenticationResult(AuthenticationStatus.Unavailable, ErrorCode: "security.ad-unavailable");
        }
    }

    public async ValueTask<ProviderResult<DirectoryIdentity>> FindAsync(
        IdentityReference identity,
        bool includeGroups,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(identity.ProviderId, ProviderId, StringComparison.Ordinal))
            return new ProviderResult<DirectoryIdentity>(ProviderOutcome.NotFound, ErrorCode: "security.provider-mismatch");
        return (await ExecuteSearchAsync(SubjectFilter(identity.SubjectId), includeGroups, 2, cancellationToken).ConfigureAwait(false)) switch
        {
            { Outcome: ProviderOutcome.Succeeded, Value.Count: 1 } result => new(ProviderOutcome.Succeeded, result.Value[0].Identity),
            { Outcome: ProviderOutcome.Succeeded, Value.Count: 0 } => new(ProviderOutcome.NotFound, ErrorCode: "security.identity-not-found"),
            { Outcome: ProviderOutcome.Succeeded } => new(ProviderOutcome.Conflict, ErrorCode: "security.identity-ambiguous"),
            var result => new(result.Outcome, ErrorCode: result.ErrorCode),
        };
    }

    public async ValueTask<ProviderResult<IReadOnlyList<DirectoryIdentity>>> SearchAsync(
        string query,
        int maximumResults,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length > 256 || maximumResults is < 1 or > 100)
            return new(ProviderOutcome.Conflict, ErrorCode: "security.invalid-directory-search");
        var filter = $"(|({options.UserNameAttribute}=*{EscapeFilter(query.Trim())}*)({options.DisplayNameAttribute}=*{EscapeFilter(query.Trim())}*))";
        var result = await ExecuteSearchAsync(filter, includeGroups: false, maximumResults, cancellationToken).ConfigureAwait(false);
        return result.Outcome is ProviderOutcome.Succeeded
            ? new(ProviderOutcome.Succeeded, result.Value!.Select(item => item.Identity).ToArray())
            : new(result.Outcome, ErrorCode: result.ErrorCode);
    }

    private ValueTask<ProviderResult<DirectoryRecord>> SearchOneAsync(string userName, bool includeGroups, CancellationToken cancellationToken) =>
        SearchByAttributeAsync(options.UserNameAttribute, userName.Trim(), includeGroups, 2, cancellationToken).MapSingle();

    private ValueTask<ProviderResult<IReadOnlyList<DirectoryRecord>>> SearchByAttributeAsync(
        string attribute, string value, bool includeGroups, int maximumResults, CancellationToken cancellationToken) =>
        ExecuteSearchAsync($"({attribute}={EscapeFilter(value)})", includeGroups, maximumResults, cancellationToken);

    private async ValueTask<ProviderResult<IReadOnlyList<DirectoryRecord>>> ExecuteSearchAsync(
        string filter, bool includeGroups, int maximumResults, CancellationToken cancellationToken)
    {
        options.Validate();
        try
        {
            var credential = await serviceCredentials.GetCredentialAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return await Task.Run(() =>
            {
                using var connection = CreateConnection(credential);
                connection.Bind();
                var attributes = includeGroups
                    ? new[] { options.SubjectAttribute, options.DisplayNameAttribute, options.EmailAttribute, options.GroupAttribute }
                    : new[] { options.SubjectAttribute, options.DisplayNameAttribute, options.EmailAttribute };
                var request = new SearchRequest(options.BaseDn, filter, SearchScope.Subtree, attributes) { SizeLimit = maximumResults };
                var response = (SearchResponse)connection.SendRequest(request);
                var records = response.Entries.Cast<SearchResultEntry>().Select(entry => ToRecord(connection, entry, includeGroups)).ToArray();
                return new ProviderResult<IReadOnlyList<DirectoryRecord>>(ProviderOutcome.Succeeded, records);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (LdapException)
        {
            return new(ProviderOutcome.Unavailable, ErrorCode: "security.ad-unavailable");
        }
    }

    private LdapConnection CreateConnection(NetworkCredential credential)
    {
        var connection = new LdapConnection(new LdapDirectoryIdentifier(options.Host, options.Port), credential, AuthType.Basic)
        {
            Timeout = options.Timeout,
        };
        connection.SessionOptions.ProtocolVersion = 3;
        connection.SessionOptions.SecureSocketLayer = true;
        connection.SessionOptions.VerifyServerCertificate = static (_, certificate) =>
            certificate is not null && new X509Chain().Build(new X509Certificate2(certificate));
        return connection;
    }

    private DirectoryRecord ToRecord(LdapConnection connection, SearchResultEntry entry, bool includeGroups)
    {
        var subject = AttributeValue(entry, options.SubjectAttribute);
        if (entry.Attributes[options.SubjectAttribute]?[0] is byte[] bytes) subject = new Guid(bytes).ToString("N");
        var groupDns = includeGroups && entry.Attributes[options.GroupAttribute] is DirectoryAttribute attribute
            ? attribute.GetValues(typeof(string)).Cast<string>().Take(options.MaximumGroups + 1).ToArray()
            : [];
        if (groupDns.Length > options.MaximumGroups) throw new LdapException("The configured group membership limit was exceeded.");
        var groups = groupDns.Select(groupDn => ResolveGroupId(connection, groupDn)).ToHashSet(StringComparer.Ordinal);
        return new(entry.DistinguishedName, new DirectoryIdentity(
            new IdentityReference(ProviderId, subject),
            AttributeValue(entry, options.DisplayNameAttribute),
            OptionalAttributeValue(entry, options.EmailAttribute),
            groups));
    }

    private static string AttributeValue(SearchResultEntry entry, string name) =>
        OptionalAttributeValue(entry, name) ?? throw new LdapException($"Required AD attribute '{name}' is absent.");

    private static string? OptionalAttributeValue(SearchResultEntry entry, string name) =>
        entry.Attributes[name] is { Count: > 0 } value ? Convert.ToString(value[0], System.Globalization.CultureInfo.InvariantCulture) : null;

    private string ResolveGroupId(LdapConnection connection, string distinguishedName)
    {
        var response = (SearchResponse)connection.SendRequest(new SearchRequest(distinguishedName, "(objectClass=*)", SearchScope.Base, options.GroupIdAttribute) { SizeLimit = 1 });
        if (response.Entries.Count != 1) throw new LdapException("An AD group could not be resolved to a stable identifier.");
        var entry = response.Entries[0];
        if (entry.Attributes[options.GroupIdAttribute]?[0] is byte[] bytes) return new Guid(bytes).ToString("N");
        return AttributeValue(entry, options.GroupIdAttribute);
    }

    private string SubjectFilter(string subjectId)
    {
        if (string.Equals(options.SubjectAttribute, "objectGUID", StringComparison.OrdinalIgnoreCase) && Guid.TryParse(subjectId, out var guid))
            return $"({options.SubjectAttribute}={string.Concat(guid.ToByteArray().Select(value => $"\\{value:x2}"))})";
        return $"({options.SubjectAttribute}={EscapeFilter(subjectId)})";
    }

    internal static string EscapeFilter(string value) => value
        .Replace("\\", "\\5c", StringComparison.Ordinal)
        .Replace("*", "\\2a", StringComparison.Ordinal)
        .Replace("(", "\\28", StringComparison.Ordinal)
        .Replace(")", "\\29", StringComparison.Ordinal)
        .Replace("\0", "\\00", StringComparison.Ordinal);

    private sealed record DirectoryRecord(string DistinguishedName, DirectoryIdentity Identity);
}

internal static class DirectoryResultExtensions
{
    public static async ValueTask<ProviderResult<T>> MapSingle<T>(this ValueTask<ProviderResult<IReadOnlyList<T>>> pending)
    {
        var result = await pending.ConfigureAwait(false);
        return result switch
        {
            { Outcome: ProviderOutcome.Succeeded, Value.Count: 1 } => new(ProviderOutcome.Succeeded, result.Value[0]),
            { Outcome: ProviderOutcome.Succeeded, Value.Count: 0 } => new(ProviderOutcome.NotFound, ErrorCode: "security.identity-not-found"),
            { Outcome: ProviderOutcome.Succeeded } => new(ProviderOutcome.Conflict, ErrorCode: "security.identity-ambiguous"),
            _ => new(result.Outcome, ErrorCode: result.ErrorCode),
        };
    }
}
