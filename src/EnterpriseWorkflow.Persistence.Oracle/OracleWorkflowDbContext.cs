using Microsoft.EntityFrameworkCore;

namespace EnterpriseWorkflow.Persistence.Oracle;

/// <summary>Dedicated EF Core context for the Oracle workflow store.</summary>
public sealed class OracleWorkflowDbContext(DbContextOptions<OracleWorkflowDbContext> options) : DbContext(options)
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
    internal DbSet<ModuleArtifactRow> ModuleArtifacts => Set<ModuleArtifactRow>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.ConfigureEnterpriseWorkflowOracle();
}

/// <summary>Oracle EF Core mapping entry point for embedded application contexts.</summary>
public static class OracleWorkflowModelBuilderExtensions
{
    /// <summary>Adds the provider-owned workflow tables with Oracle 19c-compatible types and names.</summary>
    public static ModelBuilder ConfigureEnterpriseWorkflowOracle(this ModelBuilder modelBuilder)
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
        ConfigureModuleArtifact(modelBuilder);
        return modelBuilder;
    }

    private static void ConfigureDefinition(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<DefinitionRow>();
        entity.ToTable("EW_DEFINITIONS");
        entity.HasKey(row => new { row.DefinitionId, row.Version }).HasName("PK_EW_DEFINITIONS");
        Text(entity.Property(row => row.DefinitionId), "DEFINITION_ID", 128);
        entity.Property(row => row.Version).HasColumnName("VERSION").HasColumnType("NUMBER(10)");
        Text(entity.Property(row => row.Sha256), "SHA256", 64, fixedLength: true);
        entity.Property(row => row.SchemaVersion).HasColumnName("SCHEMA_VERSION").HasColumnType("NUMBER(10)");
        entity.Property(row => row.CanonicalJson).HasColumnName("CANONICAL_JSON").HasColumnType("CLOB");
        Number19(entity.Property(row => row.PublishedAtUnixMilliseconds), "PUBLISHED_AT_MS");
    }

    private static void ConfigureInstance(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<InstanceRow>();
        entity.ToTable("EW_INSTANCES");
        entity.HasKey(row => row.Id).HasName("PK_EW_INSTANCES");
        Text(entity.Property(row => row.Id), "ID", 32, fixedLength: true);
        Text(entity.Property(row => row.DefinitionId), "DEFINITION_ID", 128);
        entity.Property(row => row.DefinitionVersion).HasColumnName("DEFINITION_VERSION").HasColumnType("NUMBER(10)");
        Text(entity.Property(row => row.DefinitionSha256), "DEFINITION_SHA256", 64, fixedLength: true);
        entity.Property(row => row.Status).HasColumnName("STATUS").HasColumnType("NUMBER(10)");
        Number19(entity.Property(row => row.Revision), "REVISION");
        entity.Property(row => row.StateSchemaVersion).HasColumnName("STATE_SCHEMA_VERSION").HasColumnType("NUMBER(10)");
        entity.Property(row => row.StateJson).HasColumnName("STATE_JSON").HasColumnType("CLOB");
        Text(entity.Property(row => row.InitiatorProviderId), "INITIATOR_PROVIDER_ID", 128);
        Text(entity.Property(row => row.InitiatorSubjectId), "INITIATOR_SUBJECT_ID", 512);
        Text(entity.Property(row => row.BusinessKey), "BUSINESS_KEY", 512);
        Text(entity.Property(row => row.CorrelationId), "CORRELATION_ID", 512);
        Number19(entity.Property(row => row.CreatedAtUnixMilliseconds), "CREATED_AT_MS");
        Number19(entity.Property(row => row.UpdatedAtUnixMilliseconds), "UPDATED_AT_MS");
        entity.HasIndex(row => new { row.DefinitionId, row.DefinitionVersion, row.Status })
            .HasDatabaseName("IX_EW_INSTANCE_DEF_STATUS");
        entity.HasOne<DefinitionRow>().WithMany()
            .HasForeignKey(row => new { row.DefinitionId, row.DefinitionVersion })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_EW_INSTANCE_DEFINITION");
    }

    private static void ConfigureActivation(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ActivationRow>();
        entity.ToTable("EW_ACTIVATIONS");
        entity.HasKey(row => row.Id).HasName("PK_EW_ACTIVATIONS");
        Text(entity.Property(row => row.Id), "ID", 32, fixedLength: true);
        Text(entity.Property(row => row.InstanceId), "INSTANCE_ID", 32, fixedLength: true);
        Text(entity.Property(row => row.NodeId), "NODE_ID", 128);
        entity.Property(row => row.Status).HasColumnName("STATUS").HasColumnType("NUMBER(10)");
        entity.Property(row => row.Attempt).HasColumnName("ATTEMPT").HasColumnType("NUMBER(10)");
        Text(entity.Property(row => row.ErrorCode), "ERROR_CODE", 128);
        entity.HasIndex(row => row.InstanceId).HasDatabaseName("IX_EW_ACT_INSTANCE");
        entity.HasOne<InstanceRow>().WithMany().HasForeignKey(row => row.InstanceId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_EW_ACT_INSTANCE");
    }

    private static void ConfigureWorkItem(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<WorkItemRow>();
        entity.ToTable("EW_WORK_ITEMS");
        entity.HasKey(row => row.Id).HasName("PK_EW_WORK_ITEMS");
        Text(entity.Property(row => row.Id), "ID", 32, fixedLength: true);
        Text(entity.Property(row => row.ActivationId), "ACTIVATION_ID", 32, fixedLength: true);
        Text(entity.Property(row => row.InstanceId), "INSTANCE_ID", 32, fixedLength: true);
        Text(entity.Property(row => row.NodeId), "NODE_ID", 128);
        entity.Property(row => row.Status).HasColumnName("STATUS").HasColumnType("NUMBER(10)");
        Number19(entity.Property(row => row.DueAtUnixMilliseconds), "DUE_AT_MS");
        Text(entity.Property(row => row.OwnerId), "OWNER_ID", 128);
        Number19(entity.Property(row => row.Generation), "GENERATION");
        Text(entity.Property(row => row.LeaseToken), "LEASE_TOKEN", 32, fixedLength: true);
        Number19(entity.Property(row => row.LeaseExpiresAtUnixMilliseconds), "LEASE_EXPIRES_AT_MS");
        Number19(entity.Property(row => row.CreatedAtUnixMilliseconds), "CREATED_AT_MS");
        entity.HasIndex(row => row.ActivationId).IsUnique().HasDatabaseName("UX_EW_WORK_ACTIVATION");
        entity.HasIndex(row => new { row.Status, row.DueAtUnixMilliseconds }).HasDatabaseName("IX_EW_WORK_STATUS_DUE");
        entity.HasIndex(row => new { row.Status, row.LeaseExpiresAtUnixMilliseconds })
            .HasDatabaseName("IX_EW_WORK_STATUS_LEASE");
        entity.HasOne<ActivationRow>().WithMany().HasForeignKey(row => row.ActivationId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_EW_WORK_ACTIVATION");
        entity.HasOne<InstanceRow>().WithMany().HasForeignKey(row => row.InstanceId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_EW_WORK_INSTANCE");
    }

    private static void ConfigureReceipt(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<StartReceiptRow>();
        entity.ToTable("EW_START_RECEIPTS");
        entity.HasKey(row => row.ReceiptKey).HasName("PK_EW_START_RECEIPTS");
        Text(entity.Property(row => row.ReceiptKey), "RECEIPT_KEY", 64, fixedLength: true);
        Text(entity.Property(row => row.InstallationId), "INSTALLATION_ID", 128);
        Text(entity.Property(row => row.CommandTypeId), "COMMAND_TYPE_ID", 128);
        Text(entity.Property(row => row.ActorProviderId), "ACTOR_PROVIDER_ID", 128);
        Text(entity.Property(row => row.ActorSubjectId), "ACTOR_SUBJECT_ID", 512);
        Text(entity.Property(row => row.IdempotencyKey), "IDEMPOTENCY_KEY", 128);
        Text(entity.Property(row => row.RequestSha256), "REQUEST_SHA256", 64);
        Text(entity.Property(row => row.InstanceId), "INSTANCE_ID", 32, fixedLength: true);
        Number19(entity.Property(row => row.InstanceRevision), "INSTANCE_REVISION");
        Number19(entity.Property(row => row.CommittedAtUnixMilliseconds), "COMMITTED_AT_MS");
        entity.HasOne<InstanceRow>().WithMany().HasForeignKey(row => row.InstanceId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_EW_RECEIPT_INSTANCE");
    }

    private static void ConfigureAudit(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<AuditRow>();
        entity.ToTable("EW_AUDITS");
        entity.HasKey(row => row.Sequence).HasName("PK_EW_AUDITS");
        entity.Property(row => row.Sequence).HasColumnName("SEQUENCE").HasColumnType("NUMBER(19)")
            .ValueGeneratedOnAdd();
        Text(entity.Property(row => row.InstanceId), "INSTANCE_ID", 32, fixedLength: true);
        Text(entity.Property(row => row.EventType), "EVENT_TYPE", 64);
        Number19(entity.Property(row => row.Revision), "REVISION");
        Number19(entity.Property(row => row.OccurredAtUnixMilliseconds), "OCCURRED_AT_MS");
        Text(entity.Property(row => row.ActorProviderId), "ACTOR_PROVIDER_ID", 128);
        Text(entity.Property(row => row.ActorSubjectId), "ACTOR_SUBJECT_ID", 512);
        entity.HasIndex(row => new { row.InstanceId, row.Sequence }).HasDatabaseName("IX_EW_AUDIT_INSTANCE_SEQ");
        entity.HasOne<InstanceRow>().WithMany().HasForeignKey(row => row.InstanceId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_EW_AUDIT_INSTANCE");
    }

    private static void ConfigureOutbox(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<OutboxRow>();
        entity.ToTable("EW_OUTBOX");
        entity.HasKey(row => row.Id).HasName("PK_EW_OUTBOX");
        Text(entity.Property(row => row.Id), "ID", 32, fixedLength: true);
        Text(entity.Property(row => row.InstanceId), "INSTANCE_ID", 32, fixedLength: true);
        Text(entity.Property(row => row.ActivationId), "ACTIVATION_ID", 32, fixedLength: true);
        Text(entity.Property(row => row.OperationId), "OPERATION_ID", 128);
        Text(entity.Property(row => row.Destination), "DESTINATION", 128);
        Text(entity.Property(row => row.ContentType), "CONTENT_TYPE", 128);
        entity.Property(row => row.PayloadJson).HasColumnName("PAYLOAD_JSON").HasColumnType("CLOB");
        Text(entity.Property(row => row.IdempotencyKey), "IDEMPOTENCY_KEY", 64, fixedLength: true);
        entity.Property(row => row.Status).HasColumnName("STATUS").HasColumnType("NUMBER(10)");
        entity.Property(row => row.Attempt).HasColumnName("ATTEMPT").HasColumnType("NUMBER(10)");
        Number19(entity.Property(row => row.DueAtUnixMilliseconds), "DUE_AT_MS");
        Text(entity.Property(row => row.OwnerId), "OWNER_ID", 128);
        Number19(entity.Property(row => row.Generation), "GENERATION");
        Text(entity.Property(row => row.LeaseToken), "LEASE_TOKEN", 32, fixedLength: true);
        Number19(entity.Property(row => row.LeaseExpiresAtUnixMilliseconds), "LEASE_EXPIRES_AT_MS");
        Text(entity.Property(row => row.LastErrorCode), "LAST_ERROR_CODE", 128);
        Number19(entity.Property(row => row.CreatedAtUnixMilliseconds), "CREATED_AT_MS");
        Number19(entity.Property(row => row.DeliveredAtUnixMilliseconds), "DELIVERED_AT_MS");
        entity.HasIndex(row => new { row.ActivationId, row.OperationId }).IsUnique().HasDatabaseName("UX_EW_OUTBOX_ACT_OP");
        entity.HasIndex(row => new { row.Status, row.DueAtUnixMilliseconds }).HasDatabaseName("IX_EW_OUTBOX_STATUS_DUE");
        entity.HasOne<InstanceRow>().WithMany().HasForeignKey(row => row.InstanceId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_EW_OUTBOX_INSTANCE");
        entity.HasOne<ActivationRow>().WithMany().HasForeignKey(row => row.ActivationId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_EW_OUTBOX_ACTIVATION");
    }

    private static void ConfigureHumanTask(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<HumanTaskRow>();
        entity.ToTable("EW_HUMAN_TASKS");
        entity.HasKey(row => row.Id).HasName("PK_EW_HUMAN_TASKS");
        Text(entity.Property(row => row.Id), "ID", 32, true);
        Text(entity.Property(row => row.ActivationId), "ACTIVATION_ID", 32, true);
        Text(entity.Property(row => row.InstanceId), "INSTANCE_ID", 32, true);
        Text(entity.Property(row => row.NodeId), "NODE_ID", 128);
        entity.Property(row => row.Status).HasColumnName("STATUS").HasColumnType("NUMBER(10)");
        entity.Property(row => row.AssignmentMode).HasColumnName("ASSIGNMENT_MODE").HasColumnType("NUMBER(10)");
        Text(entity.Property(row => row.AssigneeProviderId), "ASSIGNEE_PROVIDER_ID", 128);
        Text(entity.Property(row => row.AssigneeSubjectId), "ASSIGNEE_SUBJECT_ID", 512);
        Number19(entity.Property(row => row.Revision), "REVISION");
        Number19(entity.Property(row => row.CreatedAtUnixMilliseconds), "CREATED_AT_MS");
        Number19(entity.Property(row => row.CompletedAtUnixMilliseconds), "COMPLETED_AT_MS");
        Text(entity.Property(row => row.CompletedActionId), "COMPLETED_ACTION_ID", 128);
        Text(entity.Property(row => row.CompletedByProviderId), "COMPLETED_BY_PROVIDER_ID", 128);
        Text(entity.Property(row => row.CompletedBySubjectId), "COMPLETED_BY_SUBJECT_ID", 512);
        entity.HasIndex(row => row.ActivationId).IsUnique().HasDatabaseName("UX_EW_HT_ACTIVATION");
        entity.HasIndex(row => new { row.Status, row.CreatedAtUnixMilliseconds, row.Id }).HasDatabaseName("IX_EW_HT_STATUS_CREATED");
        entity.HasIndex(row => row.InstanceId).HasDatabaseName("IX_EW_HT_INSTANCE");
        entity.HasOne<ActivationRow>().WithMany().HasForeignKey(row => row.ActivationId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_EW_HT_ACTIVATION");
        entity.HasOne<InstanceRow>().WithMany().HasForeignKey(row => row.InstanceId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_EW_HT_INSTANCE");
    }

    private static void ConfigureHumanTaskReceipt(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<HumanTaskReceiptRow>();
        entity.ToTable("EW_HUMAN_TASK_RECEIPTS");
        entity.HasKey(row => row.ReceiptKey).HasName("PK_EW_HT_RECEIPTS");
        Text(entity.Property(row => row.ReceiptKey), "RECEIPT_KEY", 64, true);
        Text(entity.Property(row => row.TaskId), "TASK_ID", 32, true);
        Text(entity.Property(row => row.IdempotencyKey), "IDEMPOTENCY_KEY", 128);
        Text(entity.Property(row => row.ActorProviderId), "ACTOR_PROVIDER_ID", 128);
        Text(entity.Property(row => row.ActorSubjectId), "ACTOR_SUBJECT_ID", 512);
        Text(entity.Property(row => row.ActionId), "ACTION_ID", 128);
        Text(entity.Property(row => row.RequestSha256), "REQUEST_SHA256", 64, true);
        Number19(entity.Property(row => row.TaskRevision), "TASK_REVISION");
        Number19(entity.Property(row => row.InstanceRevision), "INSTANCE_REVISION");
        Number19(entity.Property(row => row.CommittedAtUnixMilliseconds), "COMMITTED_AT_MS");
        entity.HasIndex(row => new { row.TaskId, row.IdempotencyKey }).IsUnique().HasDatabaseName("UX_EW_HTR_TASK_KEY");
        entity.HasOne<HumanTaskRow>().WithMany().HasForeignKey(row => row.TaskId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_EW_HTR_TASK");
    }

    private static void ConfigureTimer(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<TimerRow>();
        entity.ToTable("EW_TIMERS");
        entity.HasKey(row => row.Id).HasName("PK_EW_TIMERS");
        Text(entity.Property(row => row.Id), "ID", 32, true);
        Text(entity.Property(row => row.ActivationId), "ACTIVATION_ID", 32, true);
        Text(entity.Property(row => row.InstanceId), "INSTANCE_ID", 32, true);
        Text(entity.Property(row => row.NodeId), "NODE_ID", 128);
        Text(entity.Property(row => row.NextNodeId), "NEXT_NODE_ID", 128);
        entity.Property(row => row.Status).HasColumnName("STATUS").HasColumnType("NUMBER(10)");
        Number19(entity.Property(row => row.DueAtUnixMilliseconds), "DUE_AT_MS");
        Number19(entity.Property(row => row.Revision), "REVISION");
        Number19(entity.Property(row => row.CreatedAtUnixMilliseconds), "CREATED_AT_MS");
        Number19(entity.Property(row => row.FiredAtUnixMilliseconds), "FIRED_AT_MS");
        entity.HasIndex(row => row.ActivationId).IsUnique().HasDatabaseName("UX_EW_TIMER_ACTIVATION");
        entity.HasIndex(row => new { row.Status, row.DueAtUnixMilliseconds }).HasDatabaseName("IX_EW_TIMER_STATUS_DUE");
        entity.HasOne<ActivationRow>().WithMany().HasForeignKey(row => row.ActivationId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_EW_TIMER_ACTIVATION");
        entity.HasOne<InstanceRow>().WithMany().HasForeignKey(row => row.InstanceId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_EW_TIMER_INSTANCE");
    }

    private static void ConfigureDesignatedAssignment(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<DesignatedAssignmentRow>();
        entity.ToTable("EW_DESIGNATED_ASSIGNMENTS");
        entity.HasKey(row => new { row.InstanceId, row.NodeId }).HasName("PK_EW_DESIGNATED_ASSIGNMENTS");
        Text(entity.Property(row => row.InstanceId), "INSTANCE_ID", 32, true);
        Text(entity.Property(row => row.NodeId), "NODE_ID", 128);
        Text(entity.Property(row => row.AssigneeProviderId), "ASSIGNEE_PROVIDER_ID", 128);
        Text(entity.Property(row => row.AssigneeSubjectId), "ASSIGNEE_SUBJECT_ID", 512);
        entity.HasOne<InstanceRow>().WithMany().HasForeignKey(row => row.InstanceId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_EW_DA_INSTANCE");
    }

    private static void ConfigureModuleArtifact(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ModuleArtifactRow>();
        entity.ToTable("EW_MODULE_ARTIFACTS");
        entity.HasKey(row => new { row.ModuleId, row.Version }).HasName("PK_EW_MODULE_ARTIFACTS");
        Text(entity.Property(row => row.ModuleId), "MODULE_ID", 128);
        Text(entity.Property(row => row.Version), "MODULE_VERSION", 128);
        Text(entity.Property(row => row.Sha256), "SHA256", 64, true);
        Number19(entity.Property(row => row.InstalledAtUnixMilliseconds), "INSTALLED_AT_MS");
    }

    private static void Text<T>(Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<T> property, string name, int length, bool fixedLength = false)
    {
        property.HasColumnName(name).HasMaxLength(length).IsUnicode(false)
            .HasColumnType(fixedLength ? $"CHAR({length} CHAR)" : $"VARCHAR2({length} CHAR)");
    }

    private static void Number19<T>(Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<T> property, string name) =>
        property.HasColumnName(name).HasColumnType("NUMBER(19)");
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

internal sealed class ModuleArtifactRow
{
    public required string ModuleId { get; set; }
    public required string Version { get; set; }
    public required string Sha256 { get; set; }
    public long InstalledAtUnixMilliseconds { get; set; }
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
