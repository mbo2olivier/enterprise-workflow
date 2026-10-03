using System.DirectoryServices.Protocols;
using System.Net;
using Xunit;

namespace EnterpriseWorkflow.Security.Tests;

public sealed class LdapDirectoryIntegrationTests
{
    [Fact]
    [Trait("Category", "LdapIntegration")]
    public async Task OpenLdapQualifiesAuthenticationSearchGroupsStatusStableIdentityTlsAndExplicitPlainText()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("LDAP_TEST_ENABLED"), "1", StringComparison.Ordinal)) return;
        var caFile = Environment.GetEnvironmentVariable("LDAP_TEST_CA_FILE");
        var qualifyLdaps = string.Equals(Environment.GetEnvironmentVariable("LDAP_TEST_LDAPS"), "1", StringComparison.Ordinal);
        if (qualifyLdaps) Assert.False(string.IsNullOrWhiteSpace(caFile));
        var cancellationToken = TestContext.Current.CancellationToken;
        var credentials = new StaticCredentialProvider(AdminCredential);
        var provider = new LdapDirectoryProvider(
            Options("localhost", 1636, qualifyLdaps ? LdapTransportMode.Ldaps : LdapTransportMode.PlainText, 1389), credentials);

        Assert.Equal(DirectoryCapabilities.Search | DirectoryCapabilities.Groups | DirectoryCapabilities.AccountStatus, provider.Capabilities);
        var authenticated = await provider.AuthenticateAsync(new("employee1", "changeit"), cancellationToken);
        Assert.Equal(AuthenticationStatus.Succeeded, authenticated.Status);

        var search = await provider.SearchAsync("Employee", 10, cancellationToken);
        var employee = Assert.Single(AssertSuccess(search), item => item.Identity == authenticated.Identity);
        Assert.Equal(DirectoryAccountStatus.Enabled, employee.AccountStatus);

        var withGroups = AssertSuccess(await provider.FindAsync(employee.Identity, includeGroups: true, cancellationToken));
        Assert.Equal(2, withGroups.GroupIds.Count);
        Assert.DoesNotContain(withGroups.GroupIds, string.IsNullOrWhiteSpace);

        var disabled = await provider.AuthenticateAsync(new("guest1", "changeit"), cancellationToken);
        Assert.Equal(AuthenticationStatus.Rejected, disabled.Status);
        Assert.Equal("security.account-disabled", disabled.ErrorCode);

        RenameEmployee();
        var afterRename = AssertSuccess(await provider.FindAsync(employee.Identity, includeGroups: false, cancellationToken));
        Assert.Equal(employee.Identity, afterRename.Identity);
        Assert.Equal("Employee1", afterRename.DisplayName);

        if (qualifyLdaps)
        {
            var badName = new LdapDirectoryProvider(Options("127.0.0.1", 1636), credentials);
            var rejectedCertificate = await badName.AuthenticateAsync(new("employee-renamed", "changeit"), cancellationToken);
            Assert.Equal(AuthenticationStatus.Unavailable, rejectedCertificate.Status);
        }

        var unavailable = new LdapDirectoryProvider(Options("localhost", 65534, timeout: TimeSpan.FromMilliseconds(500)), credentials);
        var outage = await unavailable.AuthenticateAsync(new("employee-renamed", "changeit"), cancellationToken);
        Assert.Equal(AuthenticationStatus.Unavailable, outage.Status);

        var plainText = new LdapDirectoryProvider(Options("localhost", 1636, LdapTransportMode.PlainText, 1389), credentials);
        var explicitPlainTextLogin = await plainText.AuthenticateAsync(new("employee-renamed", "changeit"), cancellationToken);
        Assert.Equal(AuthenticationStatus.Succeeded, explicitPlainTextLogin.Status);
    }

    private static LdapDirectoryOptions Options(
        string host,
        int port,
        LdapTransportMode transport = LdapTransportMode.Ldaps,
        int plainTextPort = 389,
        TimeSpan? timeout = null) => new()
        {
            ProviderId = "openldap",
            Host = host,
            Port = port,
            Transport = transport,
            PlainTextPort = plainTextPort,
            Timeout = timeout ?? TimeSpan.FromSeconds(10),
            BaseDn = "dc=example,dc=test",
            UserObjectFilter = "(objectClass=inetOrgPerson)",
            LoginAttribute = "uid",
            SubjectAttribute = "entryUUID",
            DisplayNameAttribute = "cn",
            EmailAttribute = "mail",
            AccountStatusAttribute = "employeeType",
            EnabledAccountStatusValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "active" },
            GroupResolution = LdapGroupResolutionStrategy.GroupSearch,
            GroupBaseDn = "ou=Groups,dc=example,dc=test",
            GroupObjectFilter = "(objectClass=groupOfUniqueNames)",
            GroupMemberAttribute = "uniqueMember",
            GroupIdAttribute = "entryUUID",
        };

    private static NetworkCredential AdminCredential => new("uid=admin,dc=example,dc=test", "L5a-qualification-only");

    private static void RenameEmployee()
    {
        using var connection = new LdapConnection(new LdapDirectoryIdentifier("localhost", 1389), AdminCredential, AuthType.Basic);
        connection.SessionOptions.ProtocolVersion = 3;
        connection.Bind();
        connection.SendRequest(new ModifyDNRequest(
            "uid=employee1,ou=Internal,ou=Users,dc=example,dc=test",
            "ou=Internal,ou=Users,dc=example,dc=test",
            "uid=employee-renamed")
        { DeleteOldRdn = true });
    }

    private static T AssertSuccess<T>(ProviderResult<T> result)
    {
        Assert.Equal(ProviderOutcome.Succeeded, result.Outcome);
        return result.Value!;
    }

    private sealed class StaticCredentialProvider(NetworkCredential credential) : ILdapServiceCredentialProvider
    {
        public ValueTask<NetworkCredential> GetCredentialAsync(CancellationToken cancellationToken) => ValueTask.FromResult(credential);
    }
}
