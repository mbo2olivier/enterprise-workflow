using System.DirectoryServices.Protocols;
using System.Globalization;
using System.Net;
using System.Text;

namespace EnterpriseWorkflow.Security;

public sealed class LdapDirectoryProvider(
    LdapDirectoryOptions options,
    ILdapServiceCredentialProvider serviceCredentials) : IAuthenticationProvider, IIdentityDirectory
{
    public string ProviderId => options.ProviderId;

    public DirectoryCapabilities Capabilities => DirectoryCapabilities.Search |
        (options.GroupResolution is LdapGroupResolutionStrategy.None ? DirectoryCapabilities.None : DirectoryCapabilities.Groups) |
        (options.AccountStatusAttribute is null ? DirectoryCapabilities.None : DirectoryCapabilities.AccountStatus);

    public async ValueTask<AuthenticationResult> AuthenticateAsync(PasswordCredential credential, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credential);
        options.Validate();
        if (string.IsNullOrWhiteSpace(credential.UserName) || string.IsNullOrEmpty(credential.Password))
            return new(AuthenticationStatus.Rejected, ErrorCode: "security.invalid-credentials");

        var found = await SearchOneAsync(credential.UserName, includeGroups: false, cancellationToken).ConfigureAwait(false);
        if (found.Outcome is ProviderOutcome.Unavailable)
            return new(AuthenticationStatus.Unavailable, ErrorCode: found.ErrorCode);
        if (found.Outcome is not ProviderOutcome.Succeeded || found.Value is null)
            return new(AuthenticationStatus.Rejected, ErrorCode: "security.invalid-credentials");
        if (found.Value.Identity.AccountStatus is DirectoryAccountStatus.Disabled)
            return new(AuthenticationStatus.Rejected, ErrorCode: "security.account-disabled");

        try
        {
            using var connection = CreateConnection(new NetworkCredential(found.Value.DistinguishedName, credential.Password), found.Value.Endpoint);
            connection.Bind();
            return new(AuthenticationStatus.Succeeded, found.Value.Identity.Identity);
        }
        catch (LdapException exception) when (exception.ErrorCode == 49)
        {
            return new(AuthenticationStatus.Rejected, ErrorCode: "security.invalid-credentials");
        }
        catch (LdapException)
        {
            return new(AuthenticationStatus.Unavailable, ErrorCode: "security.ldap-unavailable");
        }
    }

    public async ValueTask<ProviderResult<DirectoryIdentity>> FindAsync(
        IdentityReference identity,
        bool includeGroups,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(identity.ProviderId, ProviderId, StringComparison.Ordinal))
            return new(ProviderOutcome.NotFound, ErrorCode: "security.provider-mismatch");
        if (includeGroups && !Capabilities.HasFlag(DirectoryCapabilities.Groups))
            return new(ProviderOutcome.Conflict, ErrorCode: "security.directory-groups-unsupported");

        string filter;
        try { filter = SubjectFilter(identity.SubjectId); }
        catch (Exception exception) when (exception is FormatException or InvalidOperationException)
        {
            return new(ProviderOutcome.Conflict, ErrorCode: "security.invalid-subject-id");
        }
        var result = await ExecuteSearchAsync(filter, includeGroups, 2, cancellationToken).ConfigureAwait(false);
        return result switch
        {
            { Outcome: ProviderOutcome.Succeeded, Value.Count: 1 } => new(ProviderOutcome.Succeeded, result.Value[0].Identity),
            { Outcome: ProviderOutcome.Succeeded, Value.Count: 0 } => new(ProviderOutcome.NotFound, ErrorCode: "security.identity-not-found"),
            { Outcome: ProviderOutcome.Succeeded } => new(ProviderOutcome.Conflict, ErrorCode: "security.identity-ambiguous"),
            _ => new(result.Outcome, ErrorCode: result.ErrorCode),
        };
    }

    public async ValueTask<ProviderResult<IReadOnlyList<DirectoryIdentity>>> SearchAsync(
        string query,
        int maximumResults,
        CancellationToken cancellationToken)
    {
        if (!Capabilities.HasFlag(DirectoryCapabilities.Search))
            return new(ProviderOutcome.Conflict, ErrorCode: "security.directory-search-unsupported");
        if (string.IsNullOrWhiteSpace(query) || query.Length > 256 || maximumResults is < 1 or > 100)
            return new(ProviderOutcome.Conflict, ErrorCode: "security.invalid-directory-search");
        var value = EscapeFilter(query.Trim());
        var filter = Combine(options.UserObjectFilter, $"(|({options.LoginAttribute}=*{value}*)({options.DisplayNameAttribute}=*{value}*))");
        var result = await ExecuteSearchAsync(filter, includeGroups: false, maximumResults, cancellationToken).ConfigureAwait(false);
        return result.Outcome is ProviderOutcome.Succeeded
            ? new(ProviderOutcome.Succeeded, result.Value!.Select(item => item.Identity).ToArray())
            : new(result.Outcome, ErrorCode: result.ErrorCode);
    }

    private ValueTask<ProviderResult<DirectoryRecord>> SearchOneAsync(string userName, bool includeGroups, CancellationToken cancellationToken) =>
        ExecuteSearchAsync(Combine(options.UserObjectFilter, $"({options.LoginAttribute}={EscapeFilter(userName.Trim())})"), includeGroups, 2, cancellationToken).MapSingle();

    private async ValueTask<ProviderResult<IReadOnlyList<DirectoryRecord>>> ExecuteSearchAsync(
        string filter,
        bool includeGroups,
        int maximumResults,
        CancellationToken cancellationToken)
    {
        options.Validate();
        try
        {
            var credential = await serviceCredentials.GetCredentialAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return await Task.Run(() => SearchAcrossConfiguredEndpoints(credential, filter, includeGroups, maximumResults), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (LdapException)
        {
            return new(ProviderOutcome.Unavailable, ErrorCode: "security.ldap-unavailable");
        }
        catch (InvalidOperationException)
        {
            return new(ProviderOutcome.Unavailable, ErrorCode: "security.ldap-invalid-response");
        }
    }

    private ProviderResult<IReadOnlyList<DirectoryRecord>> SearchAcrossConfiguredEndpoints(
        NetworkCredential credential,
        string filter,
        bool includeGroups,
        int maximumResults)
    {
        LdapEndpoint endpoint = options.Transport is LdapTransportMode.Ldaps
            ? new(options.Port, UseSsl: true)
            : new(options.PlainTextPort, UseSsl: false);
        using var connection = CreateConnection(credential, endpoint);
        connection.Bind();
        var response = SendSearch(connection, options.BaseDn, filter, maximumResults, UserAttributes(includeGroups));
        var records = response.Entries.Cast<SearchResultEntry>()
            .Select(entry => ToRecord(connection, endpoint, entry, includeGroups))
            .ToArray();
        return new(ProviderOutcome.Succeeded, records);
    }

    private LdapConnection CreateConnection(NetworkCredential credential, LdapEndpoint endpoint)
    {
        var connection = new LdapConnection(new LdapDirectoryIdentifier(options.Host, endpoint.Port), credential, AuthType.Basic)
        {
            Timeout = options.Timeout,
        };
        connection.SessionOptions.ProtocolVersion = 3;
        connection.SessionOptions.SecureSocketLayer = endpoint.UseSsl;
        if (endpoint.UseSsl && options.CertificateDirectory is not null && OperatingSystem.IsLinux())
        {
            connection.SessionOptions.TrustedCertificatesDirectory = options.CertificateDirectory;
            connection.SessionOptions.StartNewTlsSessionContext();
        }
        connection.SessionOptions.ReferralChasing = ReferralChasingOptions.None;
        return connection;
    }

    private DirectoryRecord ToRecord(LdapConnection connection, LdapEndpoint endpoint, SearchResultEntry entry, bool includeGroups)
    {
        var subject = Decode(entry, options.SubjectAttribute, options.SubjectIdCodec);
        var groups = includeGroups ? ResolveGroups(connection, entry) : new HashSet<string>(StringComparer.Ordinal);
        var status = options.AccountStatusAttribute is null
            ? DirectoryAccountStatus.Unknown
            : options.EnabledAccountStatusValues.Contains(OptionalString(entry, options.AccountStatusAttribute) ?? string.Empty)
                ? DirectoryAccountStatus.Enabled
                : DirectoryAccountStatus.Disabled;
        return new(entry.DistinguishedName, endpoint, new DirectoryIdentity(
            new IdentityReference(ProviderId, subject),
            RequiredString(entry, options.DisplayNameAttribute),
            options.EmailAttribute is null ? null : OptionalString(entry, options.EmailAttribute),
            groups,
            status));
    }

    private HashSet<string> ResolveGroups(LdapConnection connection, SearchResultEntry user)
    {
        if (options.GroupResolution is LdapGroupResolutionStrategy.None)
            return new(StringComparer.Ordinal);
        if (options.GroupResolution is LdapGroupResolutionStrategy.UserAttribute)
        {
            var values = Values(user, options.UserGroupAttribute).Take(options.MaximumGroups + 1).ToArray();
            if (values.Length > options.MaximumGroups) throw new InvalidOperationException("The configured LDAP group membership limit was exceeded.");
            return values.Select(value => Decode(value, options.GroupIdCodec)).ToHashSet(StringComparer.Ordinal);
        }

        var filter = Combine(options.GroupObjectFilter, $"({options.GroupMemberAttribute}={EscapeFilter(user.DistinguishedName)})");
        var response = SendSearch(connection, options.GroupBaseDn!, filter, options.MaximumGroups + 1, options.GroupIdAttribute);
        if (response.Entries.Count > options.MaximumGroups) throw new InvalidOperationException("The configured LDAP group membership limit was exceeded.");
        return response.Entries.Cast<SearchResultEntry>()
            .Select(group => Decode(group, options.GroupIdAttribute, options.GroupIdCodec))
            .ToHashSet(StringComparer.Ordinal);
    }

    private string[] UserAttributes(bool includeGroups)
    {
        var attributes = new List<string> { options.SubjectAttribute, options.DisplayNameAttribute };
        if (options.EmailAttribute is not null) attributes.Add(options.EmailAttribute);
        if (options.AccountStatusAttribute is not null) attributes.Add(options.AccountStatusAttribute);
        if (includeGroups && options.GroupResolution is LdapGroupResolutionStrategy.UserAttribute) attributes.Add(options.UserGroupAttribute);
        return attributes.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private string SubjectFilter(string subjectId) => Combine(options.UserObjectFilter,
        $"({options.SubjectAttribute}={EncodeFilterValue(subjectId, options.SubjectIdCodec)})");

    private static SearchResponse SendSearch(LdapConnection connection, string baseDn, string filter, int sizeLimit, params string[] attributes) =>
        (SearchResponse)connection.SendRequest(new SearchRequest(baseDn, filter, SearchScope.Subtree, attributes) { SizeLimit = sizeLimit });

    private static string Combine(string left, string right) => $"(&{left}{right})";

    private static string RequiredString(SearchResultEntry entry, string name) =>
        OptionalString(entry, name) ?? throw new InvalidOperationException($"Required LDAP attribute '{name}' is absent.");

    private static string? OptionalString(SearchResultEntry entry, string name) =>
        entry.Attributes[name] is { Count: > 0 } value ? Decode(value[0], LdapAttributeValueCodec.Utf8String) : null;

    private static IEnumerable<object> Values(SearchResultEntry entry, string name) =>
        entry.Attributes[name] is { } attribute ? attribute.Cast<object>() : [];

    private static string Decode(SearchResultEntry entry, string attribute, LdapAttributeValueCodec codec)
    {
        if (entry.Attributes[attribute] is not { Count: > 0 } value)
            throw new InvalidOperationException($"Required LDAP attribute '{attribute}' is absent.");
        return Decode(value[0], codec);
    }

    internal static string Decode(object value, LdapAttributeValueCodec codec) => codec switch
    {
        LdapAttributeValueCodec.Utf8String when value is byte[] bytes => Encoding.UTF8.GetString(bytes),
        LdapAttributeValueCodec.Utf8String => Convert.ToString(value, CultureInfo.InvariantCulture) ?? throw new InvalidOperationException("LDAP string value is null."),
        LdapAttributeValueCodec.GuidLittleEndian when value is byte[] { Length: 16 } bytes => new Guid(bytes).ToString("N"),
        LdapAttributeValueCodec.Hexadecimal when value is byte[] bytes => Convert.ToHexStringLower(bytes),
        LdapAttributeValueCodec.Base64 when value is byte[] bytes => Convert.ToBase64String(bytes),
        _ => throw new InvalidOperationException($"LDAP value is incompatible with codec {codec}."),
    };

    internal static string EncodeFilterValue(string value, LdapAttributeValueCodec codec) => codec switch
    {
        LdapAttributeValueCodec.Utf8String => EscapeFilter(value),
        LdapAttributeValueCodec.GuidLittleEndian when Guid.TryParse(value, out var guid) => EscapeBinary(guid.ToByteArray()),
        LdapAttributeValueCodec.Hexadecimal => EscapeBinary(Convert.FromHexString(value)),
        LdapAttributeValueCodec.Base64 => EscapeBinary(Convert.FromBase64String(value)),
        _ => throw new InvalidOperationException($"Subject identifier is incompatible with codec {codec}."),
    };

    internal static string EscapeFilter(string value) => value
        .Replace("\\", "\\5c", StringComparison.Ordinal)
        .Replace("*", "\\2a", StringComparison.Ordinal)
        .Replace("(", "\\28", StringComparison.Ordinal)
        .Replace(")", "\\29", StringComparison.Ordinal)
        .Replace("\0", "\\00", StringComparison.Ordinal);

    private static string EscapeBinary(IEnumerable<byte> value) => string.Concat(value.Select(item => $"\\{item:x2}"));

    private sealed record DirectoryRecord(string DistinguishedName, LdapEndpoint Endpoint, DirectoryIdentity Identity);
    private readonly record struct LdapEndpoint(int Port, bool UseSsl);
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
