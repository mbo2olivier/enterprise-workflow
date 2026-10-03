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

public sealed class ActiveDirectoryOptions
{
    public required string ProviderId { get; init; }
    public required string Host { get; init; }
    public int Port { get; init; } = 636;
    public required string BaseDn { get; init; }
    public required string UserNameAttribute { get; init; }
    public string SubjectAttribute { get; init; } = "objectGUID";
    public string DisplayNameAttribute { get; init; } = "displayName";
    public string EmailAttribute { get; init; } = "mail";
    public string GroupAttribute { get; init; } = "memberOf";
    public string GroupIdAttribute { get; init; } = "objectGUID";
    public int MaximumGroups { get; init; } = 100;
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);

    public void Validate()
    {
        Required(ProviderId, nameof(ProviderId));
        Required(Host, nameof(Host));
        Required(BaseDn, nameof(BaseDn));
        Required(UserNameAttribute, nameof(UserNameAttribute));
        Required(SubjectAttribute, nameof(SubjectAttribute));
        Required(GroupIdAttribute, nameof(GroupIdAttribute));
        if (Port is < 1 or > 65535) throw new InvalidOperationException("Port must be between 1 and 65535.");
        if (Timeout <= TimeSpan.Zero) throw new InvalidOperationException("Timeout must be positive.");
        if (MaximumGroups is < 0 or > 1000) throw new InvalidOperationException("MaximumGroups must be between 0 and 1000.");
    }

    private static void Required(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException($"{name} is required.");
    }
}

public interface IActiveDirectoryServiceCredentialProvider
{
    ValueTask<System.Net.NetworkCredential> GetCredentialAsync(CancellationToken cancellationToken);
}
