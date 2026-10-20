using System.Text.Json;
using System.Text.Json.Nodes;
using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Persistence;

namespace EnterpriseWorkflow.Runtime;

/// <summary>Transport envelope included in the idempotency hash, never exposed as a business field.</summary>
public static class HumanTaskSubmission
{
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
