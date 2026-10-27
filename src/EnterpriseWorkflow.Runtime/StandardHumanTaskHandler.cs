using System.Text.Json;
using System.Text.Json.Nodes;
using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Core.Model;

namespace EnterpriseWorkflow.Runtime;

/// <summary>Built-in approval action with declarative, transactional comment mapping.</summary>
public sealed class StandardHumanTaskHandler : IHumanTaskCompletionHandler
{
    public ValueTask<HumanTaskCompletionResult> CompleteAsync(HumanTaskCompletionContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var policy = WorkflowStagePolicy.Read(context.Configuration);
        if (policy is null || !policy.Actions.TryGetValue(context.ActionId.Value, out var action)) return Reject("runtime.action-not-declared");
        if (action.CommentPath is null) return ValueTask.FromResult(HumanTaskCompletionResult.Success());
        var submission = context.Submission.ToJsonElement();
        var comment = submission.TryGetProperty("comment", out var value) ? value : default;
        if (comment.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Null or JsonValueKind.String)) return Reject("runtime.comment-invalid");
        var text = comment.ValueKind == JsonValueKind.String ? comment.GetString()?.Trim() : null;
        if (string.IsNullOrEmpty(text)) return action.RequireComment ? Reject("runtime.comment-required") : ValueTask.FromResult(HumanTaskCompletionResult.Success());
        if (text.Length > 4000 || context.CompletionAtUtc == default) return Reject("runtime.comment-invalid");
        var root = JsonNode.Parse(context.State.Value.CanonicalText)!.AsObject();
        JsonObject target = root;
        foreach (var part in action.CommentPath[..^1])
        {
            if (target[part] is not JsonObject nested) return Reject("runtime.comment-target-invalid");
            target = nested;
        }
        var key = action.CommentPath[^1];
        if (target[key] is not null and not JsonArray) return Reject("runtime.comment-target-invalid");
        var comments = target[key] as JsonArray ?? new JsonArray();
        if (target[key] is null) target[key] = comments;
        var commentNode = JsonSerializer.SerializeToNode(new WorkflowComment(text, context.NodeId.Value, context.ActionId.Value,
            context.Actor.ProviderId.Value, context.Actor.SubjectId, context.CompletionAtUtc) { ActorDisplayName = action.CaptureCommentActorName ? context.Actor.DisplayName : null }, WorkflowJson.Options)!;
        if (!action.CaptureCommentActorName) commentNode.AsObject().Remove("actorDisplayName");
        comments.Add(commentNode);
        return ValueTask.FromResult(HumanTaskCompletionResult.Success(NodeStateUpdate.Replace(JsonSerializer.SerializeToElement(root))));
    }
    private static ValueTask<HumanTaskCompletionResult> Reject(string code) => ValueTask.FromResult(HumanTaskCompletionResult.Rejected(new(code)));
}
