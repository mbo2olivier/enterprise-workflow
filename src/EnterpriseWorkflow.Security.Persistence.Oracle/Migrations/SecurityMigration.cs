using Microsoft.EntityFrameworkCore.Migrations;

namespace EnterpriseWorkflow.Security.Persistence.Oracle.Migrations;

internal static class SecurityMigration
{
    private static readonly string[] IdentityColumns = ["ProviderId", "SubjectId"];
    internal static void BuildUp(MigrationBuilder m)
    {
        m.CreateTable("EwSecurityProfiles", c => new { Id = c.Column<string>(maxLength:160), DisplayName = c.Column<string>(maxLength:256), GrantsAdministrativeAccess = c.Column<bool>(), }, constraints: t => t.PrimaryKey("PK_EwSecProfiles", x => x.Id));
        m.CreateTable("EwSecurityPermissions", c => new { ProfileId = c.Column<string>(maxLength:160), PermissionId = c.Column<string>(maxLength:160) }, constraints: t => t.PrimaryKey("PK_EwSecPermissions", x => new { x.ProfileId, x.PermissionId }));
        m.CreateTable("EwSecurityIdentityProfiles", c => new { ProviderId = c.Column<string>(maxLength:256), SubjectId = c.Column<string>(maxLength:256), ProfileId = c.Column<string>(maxLength:160) }, constraints: t => t.PrimaryKey("PK_EwSecIdentityProfiles", x => new { x.ProviderId, x.SubjectId, x.ProfileId }));
        m.CreateTable("EwSecurityGroupProfiles", c => new { ProviderId = c.Column<string>(maxLength:256), GroupId = c.Column<string>(maxLength:512), ProfileId = c.Column<string>(maxLength:160) }, constraints: t => t.PrimaryKey("PK_EwSecGroupProfiles", x => new { x.ProviderId, x.GroupId, x.ProfileId }));
        m.CreateTable("EwSecurityLocalAccounts", c => new { SubjectId = c.Column<string>(maxLength:256), UserName = c.Column<string>(maxLength:256), NormalizedUserName = c.Column<string>(maxLength:256), PasswordHash = c.Column<string>(maxLength:2048), Enabled = c.Column<bool>(), FailedAccessCount = c.Column<int>(), LockoutEndUnixMilliseconds = c.Column<long>(nullable:true), Revision = c.Column<long>() }, constraints: t => t.PrimaryKey("PK_EwSecLocalAccounts", x => x.SubjectId));
        m.CreateTable("EwSecurityAudit", c => new { Id = c.Column<string>(maxLength:32), OccurredAtUnixMilliseconds = c.Column<long>(), ActorProviderId = c.Column<string>(maxLength:256), ActorSubjectId = c.Column<string>(maxLength:256), Action = c.Column<string>(maxLength:160), Target = c.Column<string>(maxLength:1024), Outcome = c.Column<string>(maxLength:64) }, constraints: t => t.PrimaryKey("PK_EwSecAudit", x => x.Id));
        m.CreateTable("EwSecuritySessions", c => new { TokenDigest = c.Column<string>(maxLength:64), ProviderId = c.Column<string>(maxLength:256), SubjectId = c.Column<string>(maxLength:256), CreatedAtUnixMilliseconds = c.Column<long>(), LastSeenAtUnixMilliseconds = c.Column<long>(), ExpiresAtUnixMilliseconds = c.Column<long>(), RemoteStatusCheckedAtUnixMilliseconds = c.Column<long>(), Revoked = c.Column<bool>(), Revision = c.Column<long>() }, constraints: t => t.PrimaryKey("PK_EwSecSessions", x => x.TokenDigest));
        m.CreateIndex("UX_EwSecLocalAccounts_Name", "EwSecurityLocalAccounts", "NormalizedUserName", unique:true);
        m.CreateIndex("IX_EwSecAudit_OccurredAt", "EwSecurityAudit", "OccurredAtUnixMilliseconds");
        m.CreateIndex("IX_EwSecSessions_Identity", "EwSecuritySessions", IdentityColumns);
        m.CreateIndex("IX_EwSecSessions_ExpiresAt", "EwSecuritySessions", "ExpiresAtUnixMilliseconds");
    }
    internal static void BuildDown(MigrationBuilder m) { m.DropTable("EwSecuritySessions"); m.DropTable("EwSecurityAudit"); m.DropTable("EwSecurityGroupProfiles"); m.DropTable("EwSecurityIdentityProfiles"); m.DropTable("EwSecurityLocalAccounts"); m.DropTable("EwSecurityPermissions"); m.DropTable("EwSecurityProfiles"); }
}
