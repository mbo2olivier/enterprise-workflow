using Microsoft.EntityFrameworkCore;

namespace EnterpriseWorkflow.Security.Persistence;

public sealed class SecurityDbContext(DbContextOptions<SecurityDbContext> options) : DbContext(options)
{
    internal DbSet<ProfileRow> Profiles => Set<ProfileRow>();
    internal DbSet<PermissionRow> Permissions => Set<PermissionRow>();
    internal DbSet<IdentityProfileRow> IdentityProfiles => Set<IdentityProfileRow>();
    internal DbSet<GroupProfileRow> GroupProfiles => Set<GroupProfileRow>();
    internal DbSet<LocalAccountRow> LocalAccounts => Set<LocalAccountRow>();
    internal DbSet<AuditRow> Audit => Set<AuditRow>();
    internal DbSet<SessionRow> Sessions => Set<SessionRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProfileRow>(entity => { entity.ToTable("EwSecurityProfiles"); entity.HasKey(x => x.Id); entity.Property(x => x.Id).HasMaxLength(160); entity.Property(x => x.DisplayName).HasMaxLength(256); });
        modelBuilder.Entity<PermissionRow>(entity => { entity.ToTable("EwSecurityPermissions"); entity.HasKey(x => new { x.ProfileId, x.PermissionId }); entity.Property(x => x.ProfileId).HasMaxLength(160); entity.Property(x => x.PermissionId).HasMaxLength(160); });
        modelBuilder.Entity<IdentityProfileRow>(entity => { entity.ToTable("EwSecurityIdentityProfiles"); entity.HasKey(x => new { x.ProviderId, x.SubjectId, x.ProfileId }); entity.Property(x => x.ProviderId).HasMaxLength(256); entity.Property(x => x.SubjectId).HasMaxLength(256); entity.Property(x => x.ProfileId).HasMaxLength(160); });
        modelBuilder.Entity<GroupProfileRow>(entity => { entity.ToTable("EwSecurityGroupProfiles"); entity.HasKey(x => new { x.ProviderId, x.GroupId, x.ProfileId }); entity.Property(x => x.ProviderId).HasMaxLength(256); entity.Property(x => x.GroupId).HasMaxLength(512); entity.Property(x => x.ProfileId).HasMaxLength(160); });
        modelBuilder.Entity<LocalAccountRow>(entity => { entity.ToTable("EwSecurityLocalAccounts"); entity.HasKey(x => x.SubjectId); entity.Property(x => x.SubjectId).HasMaxLength(256); entity.Property(x => x.UserName).HasMaxLength(256); entity.Property(x => x.NormalizedUserName).HasMaxLength(256); entity.Property(x => x.PasswordHash).HasMaxLength(2048); entity.HasIndex(x => x.NormalizedUserName).IsUnique(); });
        modelBuilder.Entity<AuditRow>(entity => { entity.ToTable("EwSecurityAudit"); entity.HasKey(x => x.Id); entity.Property(x => x.Id).HasMaxLength(32); entity.Property(x => x.ActorProviderId).HasMaxLength(256); entity.Property(x => x.ActorSubjectId).HasMaxLength(256); entity.Property(x => x.Action).HasMaxLength(160); entity.Property(x => x.Target).HasMaxLength(1024); entity.Property(x => x.Outcome).HasMaxLength(64); entity.HasIndex(x => x.OccurredAtUnixMilliseconds); });
        modelBuilder.Entity<SessionRow>(entity => { entity.ToTable("EwSecuritySessions"); entity.HasKey(x => x.TokenDigest); entity.Property(x => x.TokenDigest).HasMaxLength(64); entity.Property(x => x.ProviderId).HasMaxLength(256); entity.Property(x => x.SubjectId).HasMaxLength(256); entity.HasIndex(x => new { x.ProviderId, x.SubjectId }); entity.HasIndex(x => x.ExpiresAtUnixMilliseconds); });
    }
}

internal sealed class ProfileRow { public required string Id { get; set; } public required string DisplayName { get; set; } public bool GrantsAdministrativeAccess { get; set; } }
internal sealed class PermissionRow { public required string ProfileId { get; set; } public required string PermissionId { get; set; } }
internal sealed class IdentityProfileRow { public required string ProviderId { get; set; } public required string SubjectId { get; set; } public required string ProfileId { get; set; } }
internal sealed class GroupProfileRow { public required string ProviderId { get; set; } public required string GroupId { get; set; } public required string ProfileId { get; set; } }
internal sealed class LocalAccountRow { public required string SubjectId { get; set; } public required string UserName { get; set; } public required string NormalizedUserName { get; set; } public required string PasswordHash { get; set; } public bool Enabled { get; set; } public int FailedAccessCount { get; set; } public long? LockoutEndUnixMilliseconds { get; set; } public long Revision { get; set; } }
internal sealed class AuditRow { public string Id { get; set; } = Guid.NewGuid().ToString("N"); public long OccurredAtUnixMilliseconds { get; set; } public required string ActorProviderId { get; set; } public required string ActorSubjectId { get; set; } public required string Action { get; set; } public required string Target { get; set; } public required string Outcome { get; set; } }
internal sealed class SessionRow { public required string TokenDigest { get; set; } public required string ProviderId { get; set; } public required string SubjectId { get; set; } public long CreatedAtUnixMilliseconds { get; set; } public long LastSeenAtUnixMilliseconds { get; set; } public long ExpiresAtUnixMilliseconds { get; set; } public long RemoteStatusCheckedAtUnixMilliseconds { get; set; } public bool Revoked { get; set; } public long Revision { get; set; } }
