using Microsoft.EntityFrameworkCore;

namespace EnterpriseWorkflow.Persistence.Sqlite;

/// <summary>Dedicated EF Core context for the SQLite workflow store.</summary>
public sealed class SqliteWorkflowDbContext(DbContextOptions<SqliteWorkflowDbContext> options) : DbContext(options)
{
    internal DbSet<DefinitionRow> Definitions => Set<DefinitionRow>();

    internal DbSet<InstanceRow> Instances => Set<InstanceRow>();

    internal DbSet<ActivationRow> Activations => Set<ActivationRow>();

    internal DbSet<WorkItemRow> WorkItems => Set<WorkItemRow>();

    internal DbSet<StartReceiptRow> StartReceipts => Set<StartReceiptRow>();

    internal DbSet<AuditRow> Audits => Set<AuditRow>();

    internal DbSet<OutboxRow> Outbox => Set<OutboxRow>();

    internal DbSet<HumanTaskRow> HumanTasks => Set<HumanTaskRow>();
    internal DbSet<HumanTaskReceiptRow> HumanTaskReceipts => Set<HumanTaskReceiptRow>();
    internal DbSet<TimerRow> Timers => Set<TimerRow>();
    internal DbSet<DesignatedAssignmentRow> DesignatedAssignments => Set<DesignatedAssignmentRow>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ConfigureEnterpriseWorkflowSqlite();
}

/// <summary>EF Core mapping entry point for applications embedding the workflow tables in their own context.</summary>
public static class SqliteWorkflowModelBuilderExtensions
{
    /// <summary>Adds the workflow persistence model with the provider's stable table names.</summary>
    public static ModelBuilder ConfigureEnterpriseWorkflowSqlite(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        ConfigureDefinition(modelBuilder);
        ConfigureInstance(modelBuilder);
        ConfigureActivation(modelBuilder);
        ConfigureWorkItem(modelBuilder);
        ConfigureReceipt(modelBuilder);
        ConfigureAudit(modelBuilder);
        ConfigureOutbox(modelBuilder);
        ConfigureHumanTask(modelBuilder);
        ConfigureHumanTaskReceipt(modelBuilder);
        ConfigureTimer(modelBuilder);
        ConfigureDesignatedAssignment(modelBuilder);
        return modelBuilder;
    }

    private static void ConfigureDefinition(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<DefinitionRow>();
        entity.ToTable("EwDefinitions");
        entity.HasKey(row => new { row.DefinitionId, row.Version });
        entity.Property(row => row.DefinitionId).HasMaxLength(128).UseCollation("BINARY");
        entity.Property(row => row.Sha256).HasMaxLength(64).IsRequired();
        entity.Property(row => row.CanonicalJson).IsRequired();
    }

    private static void ConfigureInstance(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<InstanceRow>();
        entity.ToTable("EwInstances");
        entity.HasKey(row => row.Id);
        entity.Property(row => row.Id).HasMaxLength(32);
        entity.Property(row => row.DefinitionId).HasMaxLength(128).UseCollation("BINARY");
        entity.Property(row => row.DefinitionSha256).HasMaxLength(64);
        entity.Property(row => row.StateJson).IsRequired();
        entity.Property(row => row.InitiatorProviderId).HasMaxLength(128).UseCollation("BINARY");
        entity.Property(row => row.InitiatorSubjectId).HasMaxLength(512).UseCollation("BINARY");
        entity.HasIndex(row => new { row.DefinitionId, row.DefinitionVersion, row.Status });
        entity.HasOne<DefinitionRow>()
            .WithMany()
            .HasForeignKey(row => new { row.DefinitionId, row.DefinitionVersion })
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureActivation(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ActivationRow>();
        entity.ToTable("EwActivations");
        entity.HasKey(row => row.Id);
        entity.Property(row => row.Id).HasMaxLength(32);
        entity.Property(row => row.InstanceId).HasMaxLength(32);
        entity.Property(row => row.NodeId).HasMaxLength(128).UseCollation("BINARY");
        entity.HasIndex(row => row.InstanceId);
        entity.HasOne<InstanceRow>().WithMany().HasForeignKey(row => row.InstanceId).OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureWorkItem(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<WorkItemRow>();
        entity.ToTable("EwWorkItems");
        entity.HasKey(row => row.Id);
        entity.Property(row => row.Id).HasMaxLength(32);
        entity.Property(row => row.ActivationId).HasMaxLength(32);
        entity.Property(row => row.InstanceId).HasMaxLength(32);
        entity.Property(row => row.NodeId).HasMaxLength(128).UseCollation("BINARY");
        entity.Property(row => row.OwnerId).HasMaxLength(128).UseCollation("BINARY");
        entity.Property(row => row.LeaseToken).HasMaxLength(32);
        entity.HasIndex(row => row.ActivationId).IsUnique();
        entity.HasIndex(row => new { row.Status, row.DueAtUnixMilliseconds });
        entity.HasIndex(row => new { row.Status, row.LeaseExpiresAtUnixMilliseconds });
        entity.HasOne<ActivationRow>().WithMany().HasForeignKey(row => row.ActivationId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<InstanceRow>().WithMany().HasForeignKey(row => row.InstanceId).OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureReceipt(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<StartReceiptRow>();
        entity.ToTable("EwStartReceipts");
        entity.HasKey(row => row.ReceiptKey);
        entity.Property(row => row.ReceiptKey).HasMaxLength(64);
        entity.Property(row => row.InstallationId).HasMaxLength(128).UseCollation("BINARY");
        entity.Property(row => row.CommandTypeId).HasMaxLength(128).UseCollation("BINARY");
        entity.Property(row => row.ActorProviderId).HasMaxLength(128).UseCollation("BINARY");
        entity.Property(row => row.ActorSubjectId).HasMaxLength(512).UseCollation("BINARY");
        entity.Property(row => row.IdempotencyKey).HasMaxLength(128).UseCollation("BINARY");
        entity.Property(row => row.RequestSha256).HasMaxLength(64);
        entity.Property(row => row.InstanceId).HasMaxLength(32);
        entity.HasOne<InstanceRow>().WithMany().HasForeignKey(row => row.InstanceId).OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureAudit(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<AuditRow>();
        entity.ToTable("EwAudits");
        entity.HasKey(row => row.Sequence);
        entity.Property(row => row.InstanceId).HasMaxLength(32);
        entity.Property(row => row.EventType).HasMaxLength(64);
        entity.Property(row => row.ActorProviderId).HasMaxLength(128).UseCollation("BINARY");
        entity.Property(row => row.ActorSubjectId).HasMaxLength(512).UseCollation("BINARY");
        entity.HasIndex(row => new { row.InstanceId, row.Sequence });
        entity.HasOne<InstanceRow>().WithMany().HasForeignKey(row => row.InstanceId).OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureOutbox(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<OutboxRow>();
        entity.ToTable("EwOutbox");
        entity.HasKey(row => row.Id);
        entity.Property(row => row.Id).HasMaxLength(32);
        entity.Property(row => row.InstanceId).HasMaxLength(32);
        entity.Property(row => row.ActivationId).HasMaxLength(32);
        entity.Property(row => row.OperationId).HasMaxLength(128).UseCollation("BINARY");
        entity.Property(row => row.Destination).HasMaxLength(128).UseCollation("BINARY");
        entity.Property(row => row.ContentType).HasMaxLength(128);
        entity.Property(row => row.IdempotencyKey).HasMaxLength(64);
        entity.Property(row => row.OwnerId).HasMaxLength(128).UseCollation("BINARY");
        entity.Property(row => row.LeaseToken).HasMaxLength(32);
        entity.Property(row => row.LastErrorCode).HasMaxLength(128);
        entity.HasIndex(row => new { row.ActivationId, row.OperationId }).IsUnique();
        entity.HasIndex(row => new { row.Status, row.DueAtUnixMilliseconds });
        entity.HasOne<InstanceRow>().WithMany().HasForeignKey(row => row.InstanceId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<ActivationRow>().WithMany().HasForeignKey(row => row.ActivationId).OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureHumanTask(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<HumanTaskRow>();
        entity.ToTable("EwHumanTasks");
        entity.HasKey(row => row.Id);
        entity.Property(row => row.Id).HasMaxLength(32);
        entity.Property(row => row.ActivationId).HasMaxLength(32);
        entity.Property(row => row.InstanceId).HasMaxLength(32);
        entity.Property(row => row.NodeId).HasMaxLength(128).UseCollation("BINARY");
        entity.Property(row => row.AssigneeProviderId).HasMaxLength(128).UseCollation("BINARY");
        entity.Property(row => row.AssigneeSubjectId).HasMaxLength(512).UseCollation("BINARY");
        entity.Property(row => row.CompletedActionId).HasMaxLength(128).UseCollation("BINARY");
        entity.Property(row => row.CompletedByProviderId).HasMaxLength(128).UseCollation("BINARY");
        entity.Property(row => row.CompletedBySubjectId).HasMaxLength(512).UseCollation("BINARY");
        entity.HasIndex(row => row.ActivationId).IsUnique();
        entity.HasIndex(row => new { row.Status, row.CreatedAtUnixMilliseconds, row.Id });
        entity.HasIndex(row => row.InstanceId);
        entity.HasOne<ActivationRow>().WithMany().HasForeignKey(row => row.ActivationId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<InstanceRow>().WithMany().HasForeignKey(row => row.InstanceId).OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureHumanTaskReceipt(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<HumanTaskReceiptRow>();
        entity.ToTable("EwHumanTaskReceipts");
        entity.HasKey(row => row.ReceiptKey);
        entity.Property(row => row.ReceiptKey).HasMaxLength(64);
        entity.Property(row => row.TaskId).HasMaxLength(32);
        entity.Property(row => row.IdempotencyKey).HasMaxLength(128).UseCollation("BINARY");
        entity.Property(row => row.ActorProviderId).HasMaxLength(128).UseCollation("BINARY");
        entity.Property(row => row.ActorSubjectId).HasMaxLength(512).UseCollation("BINARY");
        entity.Property(row => row.ActionId).HasMaxLength(128).UseCollation("BINARY");
        entity.Property(row => row.RequestSha256).HasMaxLength(64);
        entity.HasIndex(row => new { row.TaskId, row.IdempotencyKey }).IsUnique();
        entity.HasOne<HumanTaskRow>().WithMany().HasForeignKey(row => row.TaskId).OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureTimer(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<TimerRow>();
        entity.ToTable("EwTimers");
        entity.HasKey(row => row.Id);
        entity.Property(row => row.Id).HasMaxLength(32);
        entity.Property(row => row.ActivationId).HasMaxLength(32);
        entity.Property(row => row.InstanceId).HasMaxLength(32);
        entity.Property(row => row.NodeId).HasMaxLength(128).UseCollation("BINARY");
        entity.Property(row => row.NextNodeId).HasMaxLength(128).UseCollation("BINARY");
        entity.HasIndex(row => row.ActivationId).IsUnique();
        entity.HasIndex(row => new { row.Status, row.DueAtUnixMilliseconds });
        entity.HasOne<ActivationRow>().WithMany().HasForeignKey(row => row.ActivationId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<InstanceRow>().WithMany().HasForeignKey(row => row.InstanceId).OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureDesignatedAssignment(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<DesignatedAssignmentRow>();
        entity.ToTable("EwDesignatedAssignments");
        entity.HasKey(row => new { row.InstanceId, row.NodeId });
        entity.Property(row => row.InstanceId).HasMaxLength(32);
        entity.Property(row => row.NodeId).HasMaxLength(128).UseCollation("BINARY");
        entity.Property(row => row.AssigneeProviderId).HasMaxLength(128).UseCollation("BINARY");
        entity.Property(row => row.AssigneeSubjectId).HasMaxLength(512).UseCollation("BINARY");
        entity.HasOne<InstanceRow>().WithMany().HasForeignKey(row => row.InstanceId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class DefinitionRow
{
    public required string DefinitionId { get; set; }
    public int Version { get; set; }
    public required string Sha256 { get; set; }
    public int SchemaVersion { get; set; }
    public required string CanonicalJson { get; set; }
    public long PublishedAtUnixMilliseconds { get; set; }
}

internal sealed class InstanceRow
{
    public required string Id { get; set; }
    public required string DefinitionId { get; set; }
    public int DefinitionVersion { get; set; }
    public required string DefinitionSha256 { get; set; }
    public int Status { get; set; }
    public long Revision { get; set; }
    public int StateSchemaVersion { get; set; }
    public required string StateJson { get; set; }
    public required string InitiatorProviderId { get; set; }
    public required string InitiatorSubjectId { get; set; }
    public string? BusinessKey { get; set; }
    public string? CorrelationId { get; set; }
    public long CreatedAtUnixMilliseconds { get; set; }
    public long UpdatedAtUnixMilliseconds { get; set; }
}

internal sealed class ActivationRow
{
    public required string Id { get; set; }
    public required string InstanceId { get; set; }
    public required string NodeId { get; set; }
    public int Status { get; set; }
    public int Attempt { get; set; }
    public string? ErrorCode { get; set; }
}

internal sealed class WorkItemRow
{
    public required string Id { get; set; }
    public required string ActivationId { get; set; }
    public required string InstanceId { get; set; }
    public required string NodeId { get; set; }
    public int Status { get; set; }
    public long DueAtUnixMilliseconds { get; set; }
    public string? OwnerId { get; set; }
    public long Generation { get; set; }
    public string? LeaseToken { get; set; }
    public long? LeaseExpiresAtUnixMilliseconds { get; set; }
    public long CreatedAtUnixMilliseconds { get; set; }
}

internal sealed class StartReceiptRow
{
    public required string ReceiptKey { get; set; }
    public required string InstallationId { get; set; }
    public required string CommandTypeId { get; set; }
    public required string ActorProviderId { get; set; }
    public required string ActorSubjectId { get; set; }
    public required string IdempotencyKey { get; set; }
    public required string RequestSha256 { get; set; }
    public required string InstanceId { get; set; }
    public long InstanceRevision { get; set; }
    public long CommittedAtUnixMilliseconds { get; set; }
}

internal sealed class AuditRow
{
    public long Sequence { get; set; }
    public required string InstanceId { get; set; }
    public required string EventType { get; set; }
    public long Revision { get; set; }
    public long OccurredAtUnixMilliseconds { get; set; }
    public string? ActorProviderId { get; set; }
    public string? ActorSubjectId { get; set; }
}

internal sealed class OutboxRow
{
    public required string Id { get; set; }
    public required string InstanceId { get; set; }
    public required string ActivationId { get; set; }
    public required string OperationId { get; set; }
    public required string Destination { get; set; }
    public required string ContentType { get; set; }
    public required string PayloadJson { get; set; }
    public required string IdempotencyKey { get; set; }
    public int Status { get; set; }
    public int Attempt { get; set; }
    public long DueAtUnixMilliseconds { get; set; }
    public string? OwnerId { get; set; }
    public long Generation { get; set; }
    public string? LeaseToken { get; set; }
    public long? LeaseExpiresAtUnixMilliseconds { get; set; }
    public string? LastErrorCode { get; set; }
    public long CreatedAtUnixMilliseconds { get; set; }
    public long? DeliveredAtUnixMilliseconds { get; set; }
}

internal sealed class HumanTaskRow
{
    public required string Id { get; set; }
    public required string ActivationId { get; set; }
    public required string InstanceId { get; set; }
    public required string NodeId { get; set; }
    public int Status { get; set; }
    public int AssignmentMode { get; set; }
    public string? AssigneeProviderId { get; set; }
    public string? AssigneeSubjectId { get; set; }
    public long Revision { get; set; }
    public long CreatedAtUnixMilliseconds { get; set; }
    public long? CompletedAtUnixMilliseconds { get; set; }
    public string? CompletedActionId { get; set; }
    public string? CompletedByProviderId { get; set; }
    public string? CompletedBySubjectId { get; set; }
}

internal sealed class HumanTaskReceiptRow
{
    public required string ReceiptKey { get; set; }
    public required string TaskId { get; set; }
    public required string IdempotencyKey { get; set; }
    public required string ActorProviderId { get; set; }
    public required string ActorSubjectId { get; set; }
    public required string ActionId { get; set; }
    public required string RequestSha256 { get; set; }
    public long TaskRevision { get; set; }
    public long InstanceRevision { get; set; }
    public long CommittedAtUnixMilliseconds { get; set; }
}

internal sealed class TimerRow
{
    public required string Id { get; set; }
    public required string ActivationId { get; set; }
    public required string InstanceId { get; set; }
    public required string NodeId { get; set; }
    public required string NextNodeId { get; set; }
    public int Status { get; set; }
    public long DueAtUnixMilliseconds { get; set; }
    public long Revision { get; set; }
    public long CreatedAtUnixMilliseconds { get; set; }
    public long? FiredAtUnixMilliseconds { get; set; }
}

internal sealed class DesignatedAssignmentRow
{
    public required string InstanceId { get; set; }
    public required string NodeId { get; set; }
    public required string AssigneeProviderId { get; set; }
    public required string AssigneeSubjectId { get; set; }
}
