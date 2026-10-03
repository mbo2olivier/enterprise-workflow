using Microsoft.EntityFrameworkCore;

namespace EnterpriseWorkflow.Security.Persistence;

public sealed class RelationalSecurityStore(Func<SecurityDbContext> createContext, TimeProvider timeProvider) : ISecurityProfileStore, ILocalAccountStore, ISecuritySessionStore, ILocalSecurityProvisioningStore
{
    public async ValueTask<ProviderResult<SecurityProfile>> UpsertProfileAsync(SecurityProfile profile, IdentityReference actor, CancellationToken cancellationToken)
    {
        await using var db = createContext();
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var row = await db.Profiles.FindAsync([profile.Id], cancellationToken);
        if (row is null) db.Profiles.Add(new ProfileRow { Id = profile.Id, DisplayName = profile.DisplayName, GrantsAdministrativeAccess = profile.GrantsAdministrativeAccess });
        else { row.DisplayName = profile.DisplayName; row.GrantsAdministrativeAccess = profile.GrantsAdministrativeAccess; }
        await db.Permissions.Where(x => x.ProfileId == profile.Id).ExecuteDeleteAsync(cancellationToken);
        db.Permissions.AddRange(profile.Permissions.Select(x => new PermissionRow { ProfileId = profile.Id, PermissionId = x.Value }));
        AddAudit(db, actor, "security.profile.upsert", profile.Id, "succeeded");
        await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken);
        return new(ProviderOutcome.Succeeded, profile);
    }

    public async ValueTask<ProviderResult<bool>> DeleteProfileAsync(string profileId, IdentityReference actor, CancellationToken cancellationToken)
    {
        await using var db = createContext(); await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var profile = await db.Profiles.FindAsync([profileId], cancellationToken);
        if (profile is null) return new(ProviderOutcome.NotFound, ErrorCode: "security.profile-not-found");
        if (profile.GrantsAdministrativeAccess && !await db.IdentityProfiles.AnyAsync(x => x.ProfileId != profileId && db.Profiles.Any(p => p.Id == x.ProfileId && p.GrantsAdministrativeAccess) && (x.ProviderId != SecurityProviderIds.Local || db.LocalAccounts.Any(a => a.SubjectId == x.SubjectId && a.Enabled)), cancellationToken))
            return new(ProviderOutcome.Conflict, ErrorCode: "security.last-administrator");
        await db.Permissions.Where(x => x.ProfileId == profileId).ExecuteDeleteAsync(cancellationToken);
        await db.IdentityProfiles.Where(x => x.ProfileId == profileId).ExecuteDeleteAsync(cancellationToken);
        await db.GroupProfiles.Where(x => x.ProfileId == profileId).ExecuteDeleteAsync(cancellationToken);
        db.Profiles.Remove(profile); AddAudit(db, actor, "security.profile.delete", profileId, "succeeded");
        await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken); return new(ProviderOutcome.Succeeded, true);
    }

    public ValueTask<ProviderResult<bool>> AssignIdentityAsync(IdentityReference identity, string profileId, IdentityReference actor, CancellationToken cancellationToken) =>
        ChangeIdentity(identity, profileId, actor, add: true, cancellationToken);
    public ValueTask<ProviderResult<bool>> UnassignIdentityAsync(IdentityReference identity, string profileId, IdentityReference actor, CancellationToken cancellationToken) =>
        ChangeIdentity(identity, profileId, actor, add: false, cancellationToken);
    public ValueTask<ProviderResult<bool>> MapGroupAsync(GroupReference group, string profileId, IdentityReference actor, CancellationToken cancellationToken) => ChangeGroup(group, profileId, actor, true, cancellationToken);
    public ValueTask<ProviderResult<bool>> UnmapGroupAsync(GroupReference group, string profileId, IdentityReference actor, CancellationToken cancellationToken) => ChangeGroup(group, profileId, actor, false, cancellationToken);

    public async ValueTask<ProviderResult<IReadOnlySet<PermissionId>>> ResolvePermissionsAsync(IdentityReference identity, IReadOnlySet<string>? currentGroupIds, CancellationToken cancellationToken)
    {
        await using var db = createContext();
        var direct = db.IdentityProfiles.Where(x => x.ProviderId == identity.ProviderId && x.SubjectId == identity.SubjectId).Select(x => x.ProfileId);
        var profiles = await direct.ToListAsync(cancellationToken);
        if (currentGroupIds is not null)
            profiles.AddRange(await db.GroupProfiles.Where(x => x.ProviderId == identity.ProviderId && currentGroupIds.Contains(x.GroupId)).Select(x => x.ProfileId).ToListAsync(cancellationToken));
        var values = await db.Permissions.Where(x => profiles.Contains(x.ProfileId)).Select(x => x.PermissionId).Distinct().ToListAsync(cancellationToken);
        return new(ProviderOutcome.Succeeded, values.Select(x => new PermissionId(x)).ToHashSet());
    }

    public async ValueTask<ProviderResult<int>> CountAdministrativeIdentitiesAsync(CancellationToken cancellationToken)
    {
        await using var db = createContext();
        var count = await db.IdentityProfiles.Where(x => db.Profiles.Any(p => p.Id == x.ProfileId && p.GrantsAdministrativeAccess) && (x.ProviderId != SecurityProviderIds.Local || db.LocalAccounts.Any(a => a.SubjectId == x.SubjectId && a.Enabled))).Select(x => new { x.ProviderId, x.SubjectId }).Distinct().CountAsync(cancellationToken);
        return new(ProviderOutcome.Succeeded, count);
    }

    public async ValueTask<IReadOnlyList<SecurityAuditEntry>> ReadAuditAsync(int maximumResults, CancellationToken cancellationToken)
    {
        await using var db = createContext();
        return await db.Audit.OrderByDescending(x => x.OccurredAtUnixMilliseconds).Take(Math.Clamp(maximumResults, 1, 1000))
            .Select(x => new SecurityAuditEntry(DateTimeOffset.FromUnixTimeMilliseconds(x.OccurredAtUnixMilliseconds), new IdentityReference(x.ActorProviderId, x.ActorSubjectId), x.Action, x.Target, x.Outcome)).ToListAsync(cancellationToken);
    }

    public async ValueTask<ProviderResult<LocalAccount>> FindByNormalizedNameAsync(string normalizedUserName, CancellationToken cancellationToken) { await using var db = createContext(); var row = await db.LocalAccounts.SingleOrDefaultAsync(x => x.NormalizedUserName == normalizedUserName, cancellationToken); return row is null ? new(ProviderOutcome.NotFound) : new(ProviderOutcome.Succeeded, ToAccount(row)); }
    public async ValueTask<ProviderResult<LocalAccount>> FindByIdentityAsync(IdentityReference identity, CancellationToken cancellationToken) { if (identity.ProviderId != SecurityProviderIds.Local) return new(ProviderOutcome.NotFound); await using var db = createContext(); var row = await db.LocalAccounts.FindAsync([identity.SubjectId], cancellationToken); return row is null ? new(ProviderOutcome.NotFound) : new(ProviderOutcome.Succeeded, ToAccount(row)); }
    public async ValueTask<ProviderResult<LocalAccount>> CreateAsync(LocalAccount account, IdentityReference actor, CancellationToken cancellationToken) { await using var db = createContext(); if (await db.LocalAccounts.AnyAsync(x => x.SubjectId == account.Identity.SubjectId || x.NormalizedUserName == account.NormalizedUserName, cancellationToken)) return new(ProviderOutcome.Conflict, ErrorCode: "security.account-exists"); db.LocalAccounts.Add(ToRow(account)); AddAudit(db, actor, "security.account.create", account.Identity.SubjectId, "succeeded"); await db.SaveChangesAsync(cancellationToken); return new(ProviderOutcome.Succeeded, account); }
    public async ValueTask<ProviderResult<LocalAccount>> UpdateAsync(LocalAccount account, CancellationToken cancellationToken) { await using var db = createContext(); var row = await db.LocalAccounts.FindAsync([account.Identity.SubjectId], cancellationToken); if (row is null) return new(ProviderOutcome.NotFound); if (row.Revision != account.Revision) return new(ProviderOutcome.Conflict, ErrorCode: "security.stale-account"); row.PasswordHash = account.PasswordHash; row.Enabled = account.Enabled; row.FailedAccessCount = account.FailedAccessCount; row.LockoutEndUnixMilliseconds = account.LockoutEndUtc?.ToUnixTimeMilliseconds(); row.Revision++; await db.SaveChangesAsync(cancellationToken); return new(ProviderOutcome.Succeeded, ToAccount(row)); }
    public async ValueTask<ProviderResult<LocalAccount>> RecordAuthenticationFailureAsync(IdentityReference identity, int maximumAttempts, DateTimeOffset lockoutEndUtc, CancellationToken cancellationToken) { await using var db = createContext(); await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken); var row = await db.LocalAccounts.FindAsync([identity.SubjectId], cancellationToken); if (row is null) return new(ProviderOutcome.NotFound); row.FailedAccessCount = checked(row.FailedAccessCount + 1); if (row.FailedAccessCount >= maximumAttempts) row.LockoutEndUnixMilliseconds = lockoutEndUtc.ToUnixTimeMilliseconds(); row.Revision++; await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken); return new(ProviderOutcome.Succeeded, ToAccount(row)); }
    public async ValueTask<ProviderResult<LocalAccount>> RecordAuthenticationSuccessAsync(IdentityReference identity, string? upgradedPasswordHash, CancellationToken cancellationToken) { await using var db = createContext(); await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken); var row = await db.LocalAccounts.FindAsync([identity.SubjectId], cancellationToken); if (row is null) return new(ProviderOutcome.NotFound); if (upgradedPasswordHash is not null) row.PasswordHash = upgradedPasswordHash; row.FailedAccessCount = 0; row.LockoutEndUnixMilliseconds = null; row.Revision++; await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken); return new(ProviderOutcome.Succeeded, ToAccount(row)); }

    public async ValueTask<ProviderResult<bool>> BootstrapAsync(LocalAccount account, SecurityProfile administratorProfile, CancellationToken cancellationToken)
    {
        if (!administratorProfile.GrantsAdministrativeAccess || !administratorProfile.Permissions.Contains(WorkflowPermissions.ManageAccess)) return new(ProviderOutcome.Conflict, ErrorCode: "security.invalid-bootstrap-profile");
        await using var db = createContext(); await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        if (await db.IdentityProfiles.AnyAsync(x => db.Profiles.Any(p => p.Id == x.ProfileId && p.GrantsAdministrativeAccess), cancellationToken)) return new(ProviderOutcome.Conflict, ErrorCode: "security.already-bootstrapped");
        if (await db.LocalAccounts.AnyAsync(x => x.SubjectId == account.Identity.SubjectId || x.NormalizedUserName == account.NormalizedUserName, cancellationToken)) return new(ProviderOutcome.Conflict, ErrorCode: "security.account-exists");
        db.LocalAccounts.Add(ToRow(account));
        db.Profiles.Add(new() { Id = administratorProfile.Id, DisplayName = administratorProfile.DisplayName, GrantsAdministrativeAccess = true });
        db.Permissions.AddRange(administratorProfile.Permissions.Select(x => new PermissionRow { ProfileId = administratorProfile.Id, PermissionId = x.Value }));
        db.IdentityProfiles.Add(new() { ProviderId = account.Identity.ProviderId, SubjectId = account.Identity.SubjectId, ProfileId = administratorProfile.Id });
        AddAudit(db, account.Identity, "security.bootstrap", account.Identity.SubjectId, "succeeded"); await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken); return new(ProviderOutcome.Succeeded, true);
    }

    public async ValueTask<ProviderResult<bool>> RecoverAdministrativeAccessAsync(IdentityReference identity, string administratorProfileId, CancellationToken cancellationToken)
    {
        await using var db = createContext(); await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        if (await db.IdentityProfiles.AnyAsync(x => db.Profiles.Any(p => p.Id == x.ProfileId && p.GrantsAdministrativeAccess) && (x.ProviderId != SecurityProviderIds.Local || db.LocalAccounts.Any(a => a.SubjectId == x.SubjectId && a.Enabled)), cancellationToken)) return new(ProviderOutcome.Conflict, ErrorCode: "security.administrator-still-present");
        var profile = await db.Profiles.FindAsync([administratorProfileId], cancellationToken); if (profile is null || !profile.GrantsAdministrativeAccess) return new(ProviderOutcome.NotFound, ErrorCode: "security.administrator-profile-not-found");
        if (identity.ProviderId == SecurityProviderIds.Local) { var account = await db.LocalAccounts.FindAsync([identity.SubjectId], cancellationToken); if (account is null) return new(ProviderOutcome.NotFound, ErrorCode: "security.account-not-found"); account.Enabled = true; account.FailedAccessCount = 0; account.LockoutEndUnixMilliseconds = null; account.Revision++; }
        if (!await db.IdentityProfiles.AnyAsync(x => x.ProviderId == identity.ProviderId && x.SubjectId == identity.SubjectId && x.ProfileId == administratorProfileId, cancellationToken)) db.IdentityProfiles.Add(new() { ProviderId = identity.ProviderId, SubjectId = identity.SubjectId, ProfileId = administratorProfileId }); AddAudit(db, identity, "security.recovery", identity.SubjectId, "succeeded"); await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken); return new(ProviderOutcome.Succeeded, true);
    }

    public async ValueTask<ProviderResult<LocalAccount>> ChangeLocalAccountAsync(IdentityReference identity, bool enabled, string? passwordHash, IdentityReference actor, CancellationToken cancellationToken)
    {
        if (identity.ProviderId != SecurityProviderIds.Local) return new(ProviderOutcome.NotFound);
        await using var db = createContext(); await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var row = await db.LocalAccounts.FindAsync([identity.SubjectId], cancellationToken); if (row is null) return new(ProviderOutcome.NotFound);
        if (!enabled && row.Enabled && !await db.IdentityProfiles.AnyAsync(x => (x.ProviderId != identity.ProviderId || x.SubjectId != identity.SubjectId) && db.Profiles.Any(p => p.Id == x.ProfileId && p.GrantsAdministrativeAccess) && (x.ProviderId != SecurityProviderIds.Local || db.LocalAccounts.Any(a => a.SubjectId == x.SubjectId && a.Enabled)), cancellationToken)) return new(ProviderOutcome.Conflict, ErrorCode: "security.last-administrator");
        row.Enabled = enabled; if (passwordHash is not null) row.PasswordHash = passwordHash; row.FailedAccessCount = 0; row.LockoutEndUnixMilliseconds = null; row.Revision++;
        if (!enabled || passwordHash is not null)
            await db.Sessions.Where(x => x.ProviderId == identity.ProviderId && x.SubjectId == identity.SubjectId && !x.Revoked).ExecuteUpdateAsync(x => x.SetProperty(s => s.Revoked, true).SetProperty(s => s.Revision, s => s.Revision + 1), cancellationToken);
        AddAudit(db, actor, passwordHash is null ? "security.account.status" : "security.account.password-reset", identity.SubjectId, "succeeded"); await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken); return new(ProviderOutcome.Succeeded, ToAccount(row));
    }

    public async ValueTask<ProviderResult<SecuritySession>> CreateSessionAsync(SecuritySession session, CancellationToken cancellationToken) { await using var db = createContext(); db.Sessions.Add(ToRow(session)); await db.SaveChangesAsync(cancellationToken); return new(ProviderOutcome.Succeeded, session); }
    public async ValueTask<ProviderResult<SecuritySession>> FindSessionAsync(string tokenDigest, CancellationToken cancellationToken) { await using var db = createContext(); var row = await db.Sessions.FindAsync([tokenDigest], cancellationToken); return row is null ? new(ProviderOutcome.NotFound) : new(ProviderOutcome.Succeeded, ToSession(row)); }
    public async ValueTask<ProviderResult<SecuritySession>> UpdateSessionAsync(SecuritySession session, CancellationToken cancellationToken) { await using var db = createContext(); var row = await db.Sessions.FindAsync([session.TokenDigest], cancellationToken); if (row is null) return new(ProviderOutcome.NotFound); if (row.Revision != session.Revision) return new(ProviderOutcome.Conflict, ErrorCode: "security.stale-session"); row.LastSeenAtUnixMilliseconds = session.LastSeenAtUtc.ToUnixTimeMilliseconds(); row.RemoteStatusCheckedAtUnixMilliseconds = session.RemoteStatusCheckedAtUtc.ToUnixTimeMilliseconds(); row.Revoked = session.Revoked; row.Revision++; await db.SaveChangesAsync(cancellationToken); return new(ProviderOutcome.Succeeded, ToSession(row)); }
    public async ValueTask<ProviderResult<bool>> RevokeSessionAsync(string tokenDigest, IdentityReference actor, CancellationToken cancellationToken) { await using var db = createContext(); var row = await db.Sessions.FindAsync([tokenDigest], cancellationToken); if (row is null) return new(ProviderOutcome.NotFound); row.Revoked = true; row.Revision++; AddAudit(db, actor, "security.session.revoke", tokenDigest, "succeeded"); await db.SaveChangesAsync(cancellationToken); return new(ProviderOutcome.Succeeded, true); }
    public async ValueTask<ProviderResult<int>> RevokeIdentitySessionsAsync(IdentityReference identity, IdentityReference actor, CancellationToken cancellationToken) { await using var db = createContext(); var count = await db.Sessions.Where(x => x.ProviderId == identity.ProviderId && x.SubjectId == identity.SubjectId && !x.Revoked).ExecuteUpdateAsync(x => x.SetProperty(s => s.Revoked, true).SetProperty(s => s.Revision, s => s.Revision + 1), cancellationToken); AddAudit(db, actor, "security.identity-sessions.revoke", $"{identity.ProviderId}:{identity.SubjectId}", "succeeded"); await db.SaveChangesAsync(cancellationToken); return new(ProviderOutcome.Succeeded, count); }

    private async ValueTask<ProviderResult<bool>> ChangeIdentity(IdentityReference identity, string profileId, IdentityReference actor, bool add, CancellationToken ct) { await using var db = createContext(); await using var tx = await db.Database.BeginTransactionAsync(ct); var profile = await db.Profiles.FindAsync([profileId], ct); if (profile is null) return new(ProviderOutcome.NotFound); var row = await db.IdentityProfiles.FindAsync([identity.ProviderId, identity.SubjectId, profileId], ct); if (add && row is null) db.IdentityProfiles.Add(new() { ProviderId = identity.ProviderId, SubjectId = identity.SubjectId, ProfileId = profileId }); if (!add && row is not null) { if (profile.GrantsAdministrativeAccess && !await HasAdministratorOutsideAsync(db, identity.ProviderId, identity.SubjectId, profileId, ct)) return new(ProviderOutcome.Conflict, ErrorCode: "security.last-administrator"); db.IdentityProfiles.Remove(row); } AddAudit(db, actor, add ? "security.identity.assign" : "security.identity.unassign", $"{identity.ProviderId}:{identity.SubjectId}:{profileId}", "succeeded"); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return new(ProviderOutcome.Succeeded, true); }
    private async ValueTask<ProviderResult<bool>> ChangeGroup(GroupReference group, string profileId, IdentityReference actor, bool add, CancellationToken ct) { await using var db = createContext(); if (!await db.Profiles.AnyAsync(x => x.Id == profileId, ct)) return new(ProviderOutcome.NotFound); var row = await db.GroupProfiles.FindAsync([group.ProviderId, group.GroupId, profileId], ct); if (add && row is null) db.GroupProfiles.Add(new() { ProviderId = group.ProviderId, GroupId = group.GroupId, ProfileId = profileId }); if (!add && row is not null) db.GroupProfiles.Remove(row); AddAudit(db, actor, add ? "security.group.map" : "security.group.unmap", $"{group.ProviderId}:{group.GroupId}:{profileId}", "succeeded"); await db.SaveChangesAsync(ct); return new(ProviderOutcome.Succeeded, true); }
    private static Task<bool> HasAdministratorOutsideAsync(SecurityDbContext db, string? providerId, string? subjectId, string excludedProfileId, CancellationToken ct) => db.IdentityProfiles.AnyAsync(x => db.Profiles.Any(p => p.Id == x.ProfileId && p.GrantsAdministrativeAccess) && (x.ProfileId != excludedProfileId || x.ProviderId != providerId || x.SubjectId != subjectId) && (x.ProviderId != SecurityProviderIds.Local || db.LocalAccounts.Any(a => a.SubjectId == x.SubjectId && a.Enabled)), ct);
    private void AddAudit(SecurityDbContext db, IdentityReference actor, string action, string target, string outcome) => db.Audit.Add(new() { OccurredAtUnixMilliseconds = timeProvider.GetUtcNow().ToUnixTimeMilliseconds(), ActorProviderId = actor.ProviderId, ActorSubjectId = actor.SubjectId, Action = action, Target = target, Outcome = outcome });
    private static LocalAccount ToAccount(LocalAccountRow x) => new(new IdentityReference(SecurityProviderIds.Local, x.SubjectId), x.UserName, x.NormalizedUserName, x.PasswordHash, x.Enabled, x.FailedAccessCount, x.LockoutEndUnixMilliseconds is null ? null : DateTimeOffset.FromUnixTimeMilliseconds(x.LockoutEndUnixMilliseconds.Value), x.Revision);
    private static LocalAccountRow ToRow(LocalAccount x) => new() { SubjectId = x.Identity.SubjectId, UserName = x.UserName, NormalizedUserName = x.NormalizedUserName, PasswordHash = x.PasswordHash, Enabled = x.Enabled, FailedAccessCount = x.FailedAccessCount, LockoutEndUnixMilliseconds = x.LockoutEndUtc?.ToUnixTimeMilliseconds(), Revision = x.Revision };
    private static SecuritySession ToSession(SessionRow x) => new(x.TokenDigest, new(x.ProviderId, x.SubjectId), DateTimeOffset.FromUnixTimeMilliseconds(x.CreatedAtUnixMilliseconds), DateTimeOffset.FromUnixTimeMilliseconds(x.LastSeenAtUnixMilliseconds), DateTimeOffset.FromUnixTimeMilliseconds(x.ExpiresAtUnixMilliseconds), DateTimeOffset.FromUnixTimeMilliseconds(x.RemoteStatusCheckedAtUnixMilliseconds), x.Revoked, x.Revision);
    private static SessionRow ToRow(SecuritySession x) => new() { TokenDigest = x.TokenDigest, ProviderId = x.Identity.ProviderId, SubjectId = x.Identity.SubjectId, CreatedAtUnixMilliseconds = x.CreatedAtUtc.ToUnixTimeMilliseconds(), LastSeenAtUnixMilliseconds = x.LastSeenAtUtc.ToUnixTimeMilliseconds(), ExpiresAtUnixMilliseconds = x.ExpiresAtUtc.ToUnixTimeMilliseconds(), RemoteStatusCheckedAtUnixMilliseconds = x.RemoteStatusCheckedAtUtc.ToUnixTimeMilliseconds(), Revoked = x.Revoked, Revision = x.Revision };
}
