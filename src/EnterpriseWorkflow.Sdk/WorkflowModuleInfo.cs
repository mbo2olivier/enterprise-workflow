using System.Text.RegularExpressions;
using EnterpriseWorkflow.Abstractions;

namespace EnterpriseWorkflow.Sdk;

/// <summary>Immutable authoring metadata shared by one module release's contracts.</summary>
public sealed partial record WorkflowModuleInfo
{
    /// <summary>Declares the artifact release separately from its default contract revision.</summary>
    public WorkflowModuleInfo(string id, string releaseVersion, int contractRevision)
    {
        Id = new TechnicalId(id);
        ArgumentNullException.ThrowIfNull(releaseVersion);
        if (!ReleasePattern().IsMatch(releaseVersion))
            throw new ArgumentException("A module release must use a semantic version such as 1.2.0.", nameof(releaseVersion));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(contractRevision);
        ReleaseVersion = releaseVersion;
        ContractRevision = contractRevision;
    }

    /// <summary>Gets the globally stable module identifier.</summary>
    public TechnicalId Id { get; }

    /// <summary>Gets the textual artifact release; this is not a state schema version.</summary>
    public string ReleaseVersion { get; }

    /// <summary>Gets the positive revision inherited by newly declared contracts.</summary>
    public int ContractRevision { get; }

    /// <summary>Gets the optional display name.</summary>
    public string? Name { get; private init; }

    /// <summary>Gets the optional display description.</summary>
    public string? Description { get; private init; }

    /// <summary>Returns metadata with a display name, without changing its durable identity.</summary>
    public WorkflowModuleInfo WithName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > 200) throw new ArgumentOutOfRangeException(nameof(name));
        return this with { Name = name };
    }

    /// <summary>Returns metadata with a display description.</summary>
    public WorkflowModuleInfo WithDescription(string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        if (description.Length > 1000) throw new ArgumentOutOfRangeException(nameof(description));
        return this with { Description = description };
    }

    /// <summary>Qualifies a module-owned process, form, handler or permission identifier.</summary>
    /// <remarks>Node, field and action identifiers stay local and must not use this method.</remarks>
    public TechnicalId Qualify(string localId)
    {
        _ = new TechnicalId(localId);
        return new TechnicalId($"{Id.Value}.{localId}");
    }

    [GeneratedRegex("\\A(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)(?:-[0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*)?(?:\\+[0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*)?\\z", RegexOptions.CultureInvariant)]
    private static partial Regex ReleasePattern();
}
