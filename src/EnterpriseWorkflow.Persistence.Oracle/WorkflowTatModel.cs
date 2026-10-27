using Microsoft.EntityFrameworkCore;

namespace EnterpriseWorkflow.Persistence.Oracle;

internal static class TatModelBuilder
{
    internal static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TatInstanceRow>(entity =>
        {
            entity.ToTable("EW_TAT_INSTANCES");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasMaxLength(32);
            entity.Property(row => row.ProcessId).HasMaxLength(128);
            entity.Property(row => row.ProcessVersion).HasColumnType("NUMBER(10)");
            entity.Property(row => row.InitiatorProviderId).HasMaxLength(128);
            entity.Property(row => row.InitiatorSubjectId).HasMaxLength(512);
            entity.Property(row => row.StartedAtMs).HasColumnType("NUMBER(19)");
            entity.Property(row => row.EndedAtMs).HasColumnType("NUMBER(19)");
            entity.Property(row => row.Status).HasColumnType("NUMBER(10)");
            entity.Property(row => row.SourceRevision).HasColumnType("NUMBER(19)");
            entity.Property(row => row.Coverage).HasColumnType("NUMBER(10)");
        });
        modelBuilder.Entity<TatStepRow>(entity =>
        {
            entity.ToTable("EW_TAT_STEPS");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasMaxLength(32);
            entity.Property(row => row.InstanceId).HasMaxLength(32);
            entity.Property(row => row.NodeId).HasMaxLength(128);
            entity.Property(row => row.StartedAtMs).HasColumnType("NUMBER(19)");
            entity.Property(row => row.EndedAtMs).HasColumnType("NUMBER(19)");
            entity.Property(row => row.Status).HasColumnType("NUMBER(10)");
            entity.Property(row => row.Attempt).HasColumnType("NUMBER(10)");
            entity.Property(row => row.Coverage).HasColumnType("NUMBER(10)");
            entity.HasOne<TatInstanceRow>().WithMany().HasForeignKey(row => row.InstanceId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<TatAssignmentRow>(entity =>
        {
            entity.ToTable("EW_TAT_ASSIGNMENTS");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasMaxLength(128);
            entity.Property(row => row.InstanceId).HasMaxLength(32);
            entity.Property(row => row.StepId).HasMaxLength(32);
            entity.Property(row => row.TaskId).HasMaxLength(32);
            entity.Property(row => row.ActorProviderId).HasMaxLength(128);
            entity.Property(row => row.ActorSubjectId).HasMaxLength(512);
            entity.Property(row => row.StartedAtMs).HasColumnType("NUMBER(19)");
            entity.Property(row => row.EndedAtMs).HasColumnType("NUMBER(19)");
            entity.Property(row => row.EndReason).HasMaxLength(64);
            entity.Property(row => row.SourceRevision).HasColumnType("NUMBER(19)");
            entity.Property(row => row.Coverage).HasColumnType("NUMBER(10)");
            entity.HasOne<TatInstanceRow>().WithMany().HasForeignKey(row => row.InstanceId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<TatEventRow>(entity =>
        {
            entity.ToTable("EW_TAT_EVENTS");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasMaxLength(64);
            entity.Property(row => row.InstanceId).HasMaxLength(32);
            entity.Property(row => row.SourceId).HasMaxLength(32);
            entity.Property(row => row.SourceKind).HasMaxLength(32);
            entity.Property(row => row.EventType).HasMaxLength(64);
            entity.Property(row => row.AtMs).HasColumnType("NUMBER(19)");
            entity.Property(row => row.SourceRevision).HasColumnType("NUMBER(19)");
            entity.Property(row => row.Status).HasColumnType("NUMBER(10)");
            entity.Property(row => row.ActorProviderId).HasMaxLength(128);
            entity.Property(row => row.ActorSubjectId).HasMaxLength(512);
            entity.HasOne<TatInstanceRow>().WithMany().HasForeignKey(row => row.InstanceId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<TatInstanceRow>().HasIndex(row => new { row.ProcessId, row.ProcessVersion, row.StartedAtMs });
        modelBuilder.Entity<TatStepRow>().HasIndex(row => new { row.InstanceId, row.NodeId });
        modelBuilder.Entity<TatAssignmentRow>().HasIndex(row => new { row.ActorProviderId, row.ActorSubjectId, row.StartedAtMs });
        modelBuilder.Entity<TatAssignmentRow>().HasIndex(row => new { row.TaskId, row.EndedAtMs });
        modelBuilder.Entity<TatEventRow>().HasIndex(row => new { row.InstanceId, row.AtMs });
    }
}

internal sealed class TatInstanceRow
{
    public required string Id { get; set; }
    public required string ProcessId { get; set; }
    public int ProcessVersion { get; set; }
    public required string InitiatorProviderId { get; set; }
    public required string InitiatorSubjectId { get; set; }
    public long StartedAtMs { get; set; }
    public long? EndedAtMs { get; set; }
    public int Status { get; set; }
    public long SourceRevision { get; set; }
    public int Coverage { get; set; }
}

internal sealed class TatStepRow
{
    public required string Id { get; set; }
    public required string InstanceId { get; set; }
    public required string NodeId { get; set; }
    public long StartedAtMs { get; set; }
    public long? EndedAtMs { get; set; }
    public int Status { get; set; }
    public int Attempt { get; set; }
    public int Coverage { get; set; }
}

internal sealed class TatAssignmentRow
{
    public required string Id { get; set; }
    public required string InstanceId { get; set; }
    public required string StepId { get; set; }
    public required string TaskId { get; set; }
    public string? ActorProviderId { get; set; }
    public string? ActorSubjectId { get; set; }
    public long StartedAtMs { get; set; }
    public long? EndedAtMs { get; set; }
    public string? EndReason { get; set; }
    public long SourceRevision { get; set; }
    public int Coverage { get; set; }
}

internal sealed class TatEventRow
{
    public required string Id { get; set; }
    public required string InstanceId { get; set; }
    public required string SourceId { get; set; }
    public required string SourceKind { get; set; }
    public required string EventType { get; set; }
    public long AtMs { get; set; }
    public long SourceRevision { get; set; }
    public int Status { get; set; }
    public string? ActorProviderId { get; set; }
    public string? ActorSubjectId { get; set; }
}
