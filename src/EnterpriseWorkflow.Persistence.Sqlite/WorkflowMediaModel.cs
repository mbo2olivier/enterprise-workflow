using Microsoft.EntityFrameworkCore;
namespace EnterpriseWorkflow.Persistence.Sqlite;
internal static class WorkflowMediaModel { internal static void Configure(ModelBuilder modelBuilder) {
modelBuilder.Entity<WorkflowMediaRow>(entity => { entity.ToTable("EW_MEDIA"); entity.HasKey(row => row.Key); entity.Property(row => row.Status).IsConcurrencyToken(); entity.HasIndex(row => new { row.InstanceId, row.Status }); entity.HasIndex(row => new { row.MediaId, row.Revision }).IsUnique();
entity.Property(row => row.Key).HasMaxLength(64);
entity.Property(row => row.MediaId).HasMaxLength(32);
entity.Property(row => row.Name).HasMaxLength(256);
entity.Property(row => row.ContentType).HasMaxLength(128);
entity.Property(row => row.Sha256).HasMaxLength(64);
entity.Property(row => row.ObjectKey).HasMaxLength(128);
entity.Property(row => row.OwnerProviderId).HasMaxLength(128);
entity.Property(row => row.OwnerSubjectId).HasMaxLength(512);
entity.Property(row => row.InstanceId).HasMaxLength(32);
entity.Property(row => row.ActivationId).HasMaxLength(32);
entity.Property(row => row.SubmissionKey).HasMaxLength(128);
entity.Property(row => row.SourceField).HasMaxLength(128);
entity.Property(row => row.PermissionGroupsJson).HasMaxLength(4000);
}); } }
internal sealed class WorkflowMediaRow {
public required string Key { get; set; }
public required string MediaId { get; set; }
public int Revision { get; set; }
public required string Name { get; set; }
public required string ContentType { get; set; }
public long Length { get; set; }
public required string Sha256 { get; set; }
public required string ObjectKey { get; set; }
public required string OwnerProviderId { get; set; }
public required string OwnerSubjectId { get; set; }
public string? InstanceId { get; set; }
public string? ActivationId { get; set; }
public string? SubmissionKey { get; set; }
public string? SourceField { get; set; }
public int Hidden { get; set; }
public required string PermissionGroupsJson { get; set; }
public int Status { get; set; }
public long CreatedAtMs { get; set; }
}
