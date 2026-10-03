namespace EnterpriseWorkflow.Security;

public sealed class SecurityPolicyOptions
{
    public TimeSpan IdleSessionTimeout { get; set; } = TimeSpan.FromMinutes(30);
    public TimeSpan AbsoluteSessionLifetime { get; set; } = TimeSpan.FromHours(8);
    public TimeSpan RemoteStatusRevalidationInterval { get; set; } = TimeSpan.FromMinutes(5);
    public int MaximumFailedAccessAttempts { get; set; } = 5;
    public TimeSpan LockoutDuration { get; set; } = TimeSpan.FromMinutes(15);
    public int LoginRateLimitPermitCount { get; set; } = 10;
    public TimeSpan LoginRateLimitWindow { get; set; } = TimeSpan.FromMinutes(1);
    public int PasswordMinimumLength { get; set; } = 12;
    public int PasswordMaximumLength { get; set; } = 1024;
    public long MaximumRequestBodyBytes { get; set; } = 65_536;

    public void Validate()
    {
        Positive(IdleSessionTimeout, nameof(IdleSessionTimeout));
        Positive(AbsoluteSessionLifetime, nameof(AbsoluteSessionLifetime));
        Positive(RemoteStatusRevalidationInterval, nameof(RemoteStatusRevalidationInterval));
        Positive(LockoutDuration, nameof(LockoutDuration));
        Positive(LoginRateLimitWindow, nameof(LoginRateLimitWindow));
        if (IdleSessionTimeout > AbsoluteSessionLifetime)
            throw new InvalidOperationException("IdleSessionTimeout cannot exceed AbsoluteSessionLifetime.");
        if (RemoteStatusRevalidationInterval > AbsoluteSessionLifetime)
            throw new InvalidOperationException("RemoteStatusRevalidationInterval cannot exceed AbsoluteSessionLifetime.");
        if (MaximumFailedAccessAttempts < 1)
            throw new InvalidOperationException("MaximumFailedAccessAttempts must be positive.");
        if (LoginRateLimitPermitCount < 1) throw new InvalidOperationException("LoginRateLimitPermitCount must be positive.");
        if (PasswordMinimumLength < 1 || PasswordMaximumLength < PasswordMinimumLength)
            throw new InvalidOperationException("Password length bounds are invalid.");
        if (MaximumRequestBodyBytes is < 1024 or > 10_485_760)
            throw new InvalidOperationException("MaximumRequestBodyBytes must be between 1 KiB and 10 MiB.");
    }

    public void ValidateNewPassword(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        if (password.Length < PasswordMinimumLength || password.Length > PasswordMaximumLength)
            throw new ArgumentException($"Password length must be between {PasswordMinimumLength} and {PasswordMaximumLength} characters.", nameof(password));
    }

    private static void Positive(TimeSpan value, string name)
    {
        if (value <= TimeSpan.Zero || value.TotalMilliseconds != Math.Truncate(value.TotalMilliseconds))
            throw new InvalidOperationException($"{name} must be a whole positive number of milliseconds.");
    }
}

public enum LdapTransportMode
{
    Ldaps,
    PlainText,
}

public enum LdapAttributeValueCodec
{
    Utf8String,
    GuidLittleEndian,
    Hexadecimal,
    Base64,
}

public enum LdapGroupResolutionStrategy
{
    None,
    UserAttribute,
    GroupSearch,
}

public sealed class LdapDirectoryOptions
{
    public required string ProviderId { get; init; }
    public required string Host { get; init; }
    public int Port { get; init; } = 636;
    public LdapTransportMode Transport { get; init; } = LdapTransportMode.Ldaps;
    public int PlainTextPort { get; init; } = 389;
    public required string BaseDn { get; init; }
    public string UserObjectFilter { get; init; } = "(objectClass=inetOrgPerson)";
    public string LoginAttribute { get; init; } = "uid";
    public string SubjectAttribute { get; init; } = "entryUUID";
    public LdapAttributeValueCodec SubjectIdCodec { get; init; } = LdapAttributeValueCodec.Utf8String;
    public string DisplayNameAttribute { get; init; } = "cn";
    public string? EmailAttribute { get; init; } = "mail";
    public string? AccountStatusAttribute { get; init; }
    public IReadOnlySet<string> EnabledAccountStatusValues { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public LdapGroupResolutionStrategy GroupResolution { get; init; }
    public string UserGroupAttribute { get; init; } = "memberOf";
    public string? GroupBaseDn { get; init; }
    public string GroupObjectFilter { get; init; } = "(objectClass=groupOfNames)";
    public string GroupMemberAttribute { get; init; } = "member";
    public string GroupIdAttribute { get; init; } = "entryUUID";
    public LdapAttributeValueCodec GroupIdCodec { get; init; } = LdapAttributeValueCodec.Utf8String;
    public int MaximumGroups { get; init; } = 100;
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);

    public void Validate()
    {
        if (!Enum.IsDefined(Transport)) throw new InvalidOperationException("Transport is invalid.");
        if (!Enum.IsDefined(SubjectIdCodec)) throw new InvalidOperationException("SubjectIdCodec is invalid.");
        if (!Enum.IsDefined(GroupResolution)) throw new InvalidOperationException("GroupResolution is invalid.");
        if (!Enum.IsDefined(GroupIdCodec)) throw new InvalidOperationException("GroupIdCodec is invalid.");
        Required(ProviderId, nameof(ProviderId));
        Required(Host, nameof(Host));
        Required(BaseDn, nameof(BaseDn));
        Filter(UserObjectFilter, nameof(UserObjectFilter));
        Attribute(LoginAttribute, nameof(LoginAttribute));
        Attribute(SubjectAttribute, nameof(SubjectAttribute));
        Attribute(DisplayNameAttribute, nameof(DisplayNameAttribute));
        if (EmailAttribute is not null) Attribute(EmailAttribute, nameof(EmailAttribute));
        if (AccountStatusAttribute is not null)
        {
            Attribute(AccountStatusAttribute, nameof(AccountStatusAttribute));
            if (EnabledAccountStatusValues.Count == 0)
                throw new InvalidOperationException("EnabledAccountStatusValues is required when AccountStatusAttribute is configured.");
        }
        if (Transport is LdapTransportMode.PlainText && PlainTextPort is < 1 or > 65535)
            throw new InvalidOperationException("PlainTextPort must be between 1 and 65535 when clear-text LDAP is explicitly enabled.");
        if (GroupResolution is LdapGroupResolutionStrategy.UserAttribute)
            Attribute(UserGroupAttribute, nameof(UserGroupAttribute));
        if (GroupResolution is LdapGroupResolutionStrategy.GroupSearch)
        {
            Required(GroupBaseDn ?? string.Empty, nameof(GroupBaseDn));
            Filter(GroupObjectFilter, nameof(GroupObjectFilter));
            Attribute(GroupMemberAttribute, nameof(GroupMemberAttribute));
            Attribute(GroupIdAttribute, nameof(GroupIdAttribute));
        }
        if (Port is < 1 or > 65535) throw new InvalidOperationException("Port must be between 1 and 65535.");
        if (Timeout <= TimeSpan.Zero || Timeout > TimeSpan.FromMinutes(5) || Timeout.TotalMilliseconds != Math.Truncate(Timeout.TotalMilliseconds))
            throw new InvalidOperationException("Timeout must be a whole positive number of milliseconds no greater than five minutes.");
        if (MaximumGroups is < 0 or > 1000) throw new InvalidOperationException("MaximumGroups must be between 0 and 1000.");
    }

    private static void Required(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException($"{name} is required.");
    }

    private static void Attribute(string value, string name)
    {
        Required(value, name);
        if (!value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '.' or ';'))
            throw new InvalidOperationException($"{name} is not a valid LDAP attribute description.");
    }

    private static void Filter(string value, string name)
    {
        Required(value, name);
        var depth = 0;
        foreach (var character in value)
        {
            if (character == '(') depth++;
            else if (character == ')' && --depth < 0) throw new InvalidOperationException($"{name} is not a balanced LDAP filter.");
        }
        if (depth != 0 || value[0] != '(' || value[^1] != ')')
            throw new InvalidOperationException($"{name} is not a balanced LDAP filter.");
    }
}

public interface ILdapServiceCredentialProvider
{
    ValueTask<System.Net.NetworkCredential> GetCredentialAsync(CancellationToken cancellationToken);
}
