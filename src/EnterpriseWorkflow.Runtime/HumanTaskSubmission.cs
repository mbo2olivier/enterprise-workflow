using System.Text.Json;
using System.Text.Json.Nodes;
using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Persistence;

namespace EnterpriseWorkflow.Runtime;

/// <summary>Transport envelope included in the idempotency hash, never exposed as a business field.</summary>
public static class HumanTaskSubmission
{
    public const string MediaProperty = "$media";
    public static CanonicalJson WithMedia(CanonicalJson submission, IEnumerable<WorkflowMediaReference> media)
    {
        var root = JsonNode.Parse(submission.CanonicalText)!.AsObject();
        if (root.ContainsKey(MediaProperty)) throw new FormatException("Reserved media field.");
        var references = media.ToArray();
        if (references.Length > 32) throw new FormatException("Too many media references.");
        if (references.Length > 0) root[MediaProperty] = JsonSerializer.SerializeToNode(references);
        return CanonicalJson.CreateObject(root.ToJsonString(), 256 * 1024);
    }
    public static IReadOnlyList<WorkflowMediaReference> ReadMedia(CanonicalJson submission)
    {
        if (!submission.ToJsonElement().TryGetProperty(MediaProperty, out var value)) return [];
        var media = value.Deserialize<WorkflowMediaReference[]>() ?? throw new FormatException("Invalid media references.");
        if (media.Length > 32) throw new FormatException("Too many media references.");
        return media;
    }
    public const string AssignmentsProperty = "$assignments";
    public static CanonicalJson WithAssignments(CanonicalJson submission, IEnumerable<DesignatedTaskAssignment> assignments)
    {
        var root = JsonNode.Parse(submission.CanonicalText)!.AsObject();
        if (root.ContainsKey(AssignmentsProperty)) throw new FormatException("Reserved assignment field.");
        var selections = new JsonObject();
        foreach (var assignment in assignments.OrderBy(item => item.NodeId.Value, StringComparer.Ordinal))
            selections.Add(assignment.NodeId.Value, JsonSerializer.SerializeToNode(new Selection(assignment.Assignee.ProviderId.Value, assignment.Assignee.SubjectId)));
        if (selections.Count > 0) root.Add(AssignmentsProperty, selections);
        return CanonicalJson.CreateObject(root.ToJsonString(), 256 * 1024);
    }

    public static (CanonicalJson Business, IReadOnlyList<DesignatedTaskAssignment> Assignments) Read(CanonicalJson submission)
    {
        var root = JsonNode.Parse(submission.CanonicalText)!.AsObject();
        root.Remove(MediaProperty);
        var selections = new List<DesignatedTaskAssignment>();
        if (root.Remove(AssignmentsProperty, out var element))
        {
            if (element is not JsonObject selectionObject || selectionObject.Count > 1)
                throw new FormatException("Only one directly reached target can be assigned.");
            foreach (var (node, value) in selectionObject)
            {
                var selection = value?.Deserialize<Selection>() ?? throw new FormatException("Invalid selected identity.");
                if (string.IsNullOrWhiteSpace(selection.SubjectId) || selection.SubjectId.Length > 256)
                    throw new FormatException("A selected subject must contain 1 to 256 nonblank characters.");
                selections.Add(new(new TechnicalId(node), new(new TechnicalId(selection.ProviderId), selection.SubjectId)));
            }
        }
        return (CanonicalJson.CreateObject(root.ToJsonString(), 256 * 1024), selections);
    }
    private sealed record Selection(string ProviderId, string SubjectId);
}
