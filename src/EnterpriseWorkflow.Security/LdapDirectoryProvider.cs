using System.DirectoryServices.Protocols;
using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Text;

namespace EnterpriseWorkflow.Security;

public sealed class LdapDirectoryProvider(
    LdapDirectoryOptions options,
    ILdapServiceCredentialProvider serviceCredentials) : IAuthenticationProvider, IPagedIdentityDirectory
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

    public async ValueTask<ProviderResult<DirectoryIdentityPage>> ReadPageAsync(string? cursor, int pageSize, CancellationToken cancellationToken)
    {
        if (pageSize is < 1 or > 100 || !TryDecodeDirectoryCursor(cursor, out var offset))
            return new(ProviderOutcome.Conflict, ErrorCode: "security.invalid-directory-page");
        options.Validate();
        try
        {
            var credential = await serviceCredentials.GetCredentialAsync(cancellationToken).ConfigureAwait(false);
            return await Task.Run(() =>
            {
                var endpoint = options.Transport == LdapTransportMode.Ldaps ? new LdapEndpoint(options.Port, true) : new LdapEndpoint(options.PlainTextPort, false);
                using var connection = CreateConnection(credential, endpoint);
                connection.Bind();
                byte[] cookie = [];
                var remaining = offset;
                while (remaining > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var replay = SendPage(connection, Math.Min(remaining, 100), cookie);
                    if (replay.Control is null)
                        return new ProviderResult<DirectoryIdentityPage>(ProviderOutcome.Unavailable, ErrorCode: "security.ldap-paging-unsupported");
                    if (replay.Response.Entries.Count == 0)
                        return new ProviderResult<DirectoryIdentityPage>(ProviderOutcome.Succeeded, new([], null));
                    remaining -= replay.Response.Entries.Count;
                    cookie = replay.Control.Cookie;
                    if (cookie.Length == 0 && remaining > 0)
                        return new ProviderResult<DirectoryIdentityPage>(ProviderOutcome.Succeeded, new([], null));
                }
                if (offset > 0 && cookie.Length == 0)
                    return new ProviderResult<DirectoryIdentityPage>(ProviderOutcome.Succeeded, new([], null));

                cancellationToken.ThrowIfCancellationRequested();
                var page = SendPage(connection, pageSize, cookie);
                if (page.Control is null)
                    return new ProviderResult<DirectoryIdentityPage>(ProviderOutcome.Unavailable, ErrorCode: "security.ldap-paging-unsupported");
                var response = page.Response;
                var identities = response.Entries.Cast<SearchResultEntry>().Select(entry => ToRecord(connection, endpoint, entry, false).Identity).ToArray();
                if (identities.Length == 0 && page.Control.Cookie.Length > 0)
                    return new ProviderResult<DirectoryIdentityPage>(ProviderOutcome.Unavailable, ErrorCode: "security.ldap-invalid-page");
                return new ProviderResult<DirectoryIdentityPage>(ProviderOutcome.Succeeded,
                    new(identities, page.Control.Cookie.Length == 0 ? null : EncodeDirectoryCursor(checked(offset + identities.Length))));
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) when (exception is LdapException or DirectoryOperationException or FormatException or InvalidOperationException)
        {
            return new(ProviderOutcome.Unavailable, ErrorCode: "security.ldap-enumeration-unavailable");
        }
    }

    private (SearchResponse Response, PageResultResponseControl? Control) SendPage(
        LdapConnection connection,
        int pageSize,
        byte[] cookie)
    {
        var request = new SearchRequest(options.BaseDn, options.UserObjectFilter, SearchScope.Subtree, UserAttributes(false));
        request.Controls.Add(new PageResultRequestControl(pageSize) { Cookie = cookie, IsCritical = true });
        var response = (SearchResponse)connection.SendRequest(request);
        return (response, response.Controls.OfType<PageResultResponseControl>().SingleOrDefault());
    }

    internal static string EncodeDirectoryCursor(int offset)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(offset, 1);
        Span<byte> value = stackalloc byte[5];
        value[0] = 1;
        BinaryPrimitives.WriteInt32BigEndian(value[1..], offset);
        return Convert.ToBase64String(value);
    }

    internal static bool TryDecodeDirectoryCursor(string? cursor, out int offset)
    {
        offset = 0;
        if (cursor is null) return true;
        Span<byte> value = stackalloc byte[5];
        if (!Convert.TryFromBase64String(cursor, value, out var written) || written != value.Length || value[0] != 1)
            return false;
        offset = BinaryPrimitives.ReadInt32BigEndian(value[1..]);
        return offset > 0;
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
