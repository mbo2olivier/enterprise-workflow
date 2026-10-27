using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Persistence;
using EnterpriseWorkflow.Security;

namespace EnterpriseWorkflow.Presentation;

public sealed record WorkflowInstanceDetail(WorkflowRequestView Instance, CanonicalJson VisibleState,
    IReadOnlyDictionary<string, string> Labels, ActorIdentity? Initiator = null, IReadOnlyList<WorkflowInstanceStep>? Steps = null, IReadOnlyList<WorkflowInstanceHistoryEntry>? RecentHistory = null, IReadOnlyList<WorkflowCommentGroup>? CommentGroups = null);
public sealed record WorkflowCommentGroup(string Label, IReadOnlyList<WorkflowCommentDisplay> Comments);
public sealed record WorkflowCommentDisplay(string? Text, string? Stage, string? ActorId, string? ActorName, string? Date);
public sealed record WorkflowSearchRequest(WorkflowInstanceStatus? Status = null,
    DateTimeOffset? CreatedFromUtc = null, DateTimeOffset? CreatedUntilUtc = null,
    string? ProcessId = null, int? ProcessVersion = null, Guid? InstanceId = null,
    string? BusinessKey = null, IdentityReference? Initiator = null, string? Cursor = null, WorkflowInstanceStatus[]? Statuses = null, string? PublicReference = null);
public sealed record WorkflowSearchPage(IReadOnlyList<WorkflowRequestView> Items, string? NextCursor, string? ErrorCode = null);

public sealed partial class WorkflowPortalService
{
    private readonly byte[] _cursorKey = RandomNumberGenerator.GetBytes(32);

    public async ValueTask<WorkflowInstanceDetail?> ReadInstanceAsync(IdentityReference actor, WorkflowInstanceId instanceId, CancellationToken cancellationToken)
    {
        var result = await store.ReadInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false);
        if (result.Outcome != StoreOutcome.Succeeded || result.Value is not { } snapshot) return null;
        var definition = snapshot.Definition;
        if (!(await authorization.AuthorizeAsync(new(actor, new(definition.Id.Value, definition.Version, null, WorkflowActions.ReadInstance)), cancellationToken)).Allowed) return null;
        var contract = stateValidation?.GetContract(definition, snapshot.State.SchemaVersion) ?? WorkflowStateContract.From(definition);
        if (contract is null || contract.Version != snapshot.State.SchemaVersion) return null;
        CanonicalJson visible;
        IReadOnlyDictionary<string, string> labels;
        {
            var permissions = new HashSet<string>(StringComparer.Ordinal);
            foreach (var permission in AllPermissions(contract.Root).Distinct(StringComparer.Ordinal))
                if ((await generalAuthorization.AuthorizeAsync(new(actor, new(permission)), cancellationToken)).Allowed) permissions.Add(permission);
            visible = contract.Project(snapshot.State.Value, permissions);
            labels = contract.Root.Fields.Where(field => !field.Hidden && field.PermissionGroups.All(group => group.Any(permissions.Contains)))
                .ToDictionary(field => field.Name, field => field.Label, StringComparer.Ordinal);
        }
        var groups = new List<WorkflowCommentGroup>();
        await CollectCommentsAsync(contract.Root, visible.ToJsonElement(), groups, cancellationToken).ConfigureAwait(false);
        groups = groups.Select(group => group with { Comments = group.Comments.Select(comment => comment with
        { Stage = snapshot.Definition.Nodes.FirstOrDefault(node => node.Id.Value == comment.Stage)?.DisplayLabel ?? comment.Stage }).ToArray() }).ToList();
        var table = (contract with { Root = TableFields(contract.Root) }).Project(visible, new HashSet<string>(AllPermissions(contract.Root), StringComparer.Ordinal));
        return new(await ViewAsync(snapshot.Summary, cancellationToken, snapshot.Definition).ConfigureAwait(false), table, labels, snapshot.Initiator, snapshot.Steps, snapshot.RecentHistory, groups);
    }

    public async ValueTask<WorkflowSearchPage> SearchInstancesAsync(IdentityReference actor, WorkflowSearchRequest request, CancellationToken cancellationToken)
    {
        if (request.PublicReference is { } reference && !WorkflowPublicReference.IsValid(reference) || request.ProcessId?.Length > 128 || request.BusinessKey?.Length > 256 || request.ProcessVersion is <= 0 ||
            request.Status is { } status && !Enum.IsDefined(status) || request.Statuses is { } statuses && (statuses.Length > 16 || statuses.Any(item => !Enum.IsDefined(item))) ||
            request.CreatedFromUtc is { } from && request.CreatedUntilUtc is { } until && from >= until)
            return new([], null, "presentation.search-invalid");
        if (request.Initiator is not null && !(await generalAuthorization.AuthorizeAsync(new(actor, WorkflowPermissions.SearchByInitiator), cancellationToken)).Allowed)
            return new([], null, "security.search-initiator-denied");
        var binding = JsonSerializer.Serialize(new { actor, filters = request with { Cursor = null } });
        WorkflowInstanceCursor? cursor = null;
        if (request.Cursor is not null)
        {
            try { cursor = DecodeCursor(request.Cursor, binding); }
            catch (Exception exception) when (exception is CryptographicException or FormatException or JsonException or ArgumentException)
            { return new([], null, "presentation.search-cursor-invalid"); }
        }
        var items = new List<WorkflowRequestView>();
        // Bound each request independently of the number of denied candidates. Continue after last examined row.
        var scanned = 0;
        while (scanned < 500 && items.Count < 25)
        {
            var result = await store.SearchInstancesAsync(new(50, cursor, request.Status, request.CreatedFromUtc, request.CreatedUntilUtc,
                request.ProcessId, request.ProcessVersion, request.InstanceId is { } id ? new(id) : null, request.BusinessKey,
                request.Initiator is { } initiator ? new(new(initiator.ProviderId), initiator.SubjectId) : null, request.Statuses, request.PublicReference), cancellationToken).ConfigureAwait(false);
            if (result.Outcome != StoreOutcome.Succeeded || result.Value is null) return new([], null, "presentation.search-unavailable");
            if (result.Value.Count == 0) return new(items, null);
            foreach (var found in result.Value)
            {
                var candidate = found.Summary;
                scanned++;
                cursor = new(candidate.CreatedAtUtc.ToUnixTimeMilliseconds(), candidate.InstanceId.Value.ToString("N"));
                if ((await authorization.AuthorizeAsync(new(actor, new(candidate.DefinitionId.Value, candidate.DefinitionVersion, null, WorkflowActions.ReadInstance)), cancellationToken)).Allowed)
                    items.Add(await ViewAsync(candidate, cancellationToken).ConfigureAwait(false));
                if (items.Count == 25 || scanned == 500) break;
            }
            if (items.Count < 25 && result.Value.Count < 50) return new(items, null);
        }
        return new(items, cursor is null ? null : EncodeCursor(cursor, binding));
    }

    private async ValueTask<WorkflowRequestView> ViewAsync(WorkflowInstanceSummary instance, CancellationToken token, WorkflowDefinition? definition = null)
    {
        catalog.TryGetProcess(instance.DefinitionId.Value, instance.DefinitionVersion, out var process);
        var nodes = (definition ?? process?.Definition)?.Nodes;
        var positions = instance.Positions ?? [];
        var current = positions.Where(position => instance.Status == WorkflowInstanceStatus.Completed
            ? position.Status == NodeExecutionStatus.Succeeded && nodes?.Any(node => node.Id.Value == position.NodeId && node.Role == WorkflowNodeRole.End) == true
            : position.Status is NodeExecutionStatus.Pending or NodeExecutionStatus.Running or NodeExecutionStatus.Waiting || instance.Status == WorkflowInstanceStatus.Failed && position.Status == NodeExecutionStatus.Failed).ToArray();
        var outcomes = current.Select(position => nodes?.FirstOrDefault(node => node.Id.Value == position.NodeId))
            .Where(node => node?.Role == WorkflowNodeRole.End).Select(node => WorkflowStagePolicy.Read(node!.Configuration)?.CompletionOutcome).Distinct().ToArray();
        return new(instance.InstanceId, process?.Title ?? instance.DefinitionId.Value,
            instance.DefinitionVersion, instance.Status, instance.Revision, instance.BusinessKey, instance.CreatedAtUtc, instance.UpdatedAtUtc,
            instance.PublicReference, instance.Initiator, instance.Initiator is { } actor ? await DisplayNameAsync(actor, token).ConfigureAwait(false) : null,
            current.Length == 0 ? null : string.Join(",", current.Select(position => position.NodeId)),
            current.Length == 0 ? null : string.Join(" · ", current.Select(position => nodes?.FirstOrDefault(node => node.Id.Value == position.NodeId)?.DisplayLabel ?? position.NodeId)),
            instance.Status == WorkflowInstanceStatus.Completed && outcomes.Length == 1 ? outcomes[0] : null);
    }

    private async ValueTask<WorkflowRequestView?> ReadRequestAsync(WorkflowInstanceId id, CancellationToken token)
    {
        var read = await store.ReadInstanceAsync(id, token).ConfigureAwait(false);
        return read.Value is { } value ? await ViewAsync(value.Summary, token).ConfigureAwait(false) : null;
    }
    private ValueTask<string> DisplayNameAsync(ActorIdentity actor, CancellationToken token) =>
        new IdentityDisplayNameResolver(localAccounts, directories).ResolveAsync(new(actor.ProviderId.Value, actor.SubjectId), token);
    private static WorkflowStateField TableFields(WorkflowStateField field) => field with
    {
        Fields = field.Fields.Where(child => child.PresentationRole is not ("comments" or "media")).Select(TableFields).ToArray(),
        Item = field.Item is null ? null : TableFields(field.Item)
    };
    private async ValueTask<CanonicalJson> ProjectStateAsync(WorkflowDefinition definition, WorkflowState state, IdentityReference actor, CancellationToken token)
    {
        var contract = stateValidation?.GetContract(definition, state.SchemaVersion) ?? WorkflowStateContract.From(definition)!;
        var allowed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var permission in AllPermissions(contract.Root).Distinct(StringComparer.Ordinal))
            if ((await generalAuthorization.AuthorizeAsync(new(actor, new(permission)), token).ConfigureAwait(false)).Allowed) allowed.Add(permission);
        return contract.Project(state.Value, allowed);
    }
    private async ValueTask CollectCommentsAsync(WorkflowStateField field, JsonElement value, List<WorkflowCommentGroup> groups, CancellationToken token)
    {
        if (field.PresentationRole == "comments" && value.ValueKind == JsonValueKind.Array)
        {
            var rows = new List<WorkflowCommentDisplay>();
            foreach (var item in value.EnumerateArray())
            {
                string? Get(string name) => item.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() : null;
                var provider = Get("actorProviderId"); var subject = Get("actorSubjectId");
                rows.Add(new(Get("text"), Get("stageId"), subject is null ? null : $"{provider} / {subject}",
                    Get("actorDisplayName") ?? (item.TryGetProperty("actorDisplayName", out _) && provider is not null && subject is not null ? await DisplayNameAsync(new(new(provider), subject), token).ConfigureAwait(false) : null), Get("createdAtUtc")));
            }
            groups.Add(new(field.Label, rows));
        }
        else if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (var child in field.Fields)
                if (value.TryGetProperty(child.Name, out var property)) await CollectCommentsAsync(child, property, groups, token).ConfigureAwait(false);
        }
        else if (value.ValueKind == JsonValueKind.Array && field.Item is not null)
            foreach (var item in value.EnumerateArray()) await CollectCommentsAsync(field.Item, item, groups, token).ConfigureAwait(false);
    }

    private static IEnumerable<string> AllPermissions(WorkflowStateField field) => field.PermissionGroups.SelectMany(group => group)
        .Concat(field.Fields.SelectMany(AllPermissions)).Concat(field.Item is null ? [] : AllPermissions(field.Item));

    private string EncodeCursor(WorkflowInstanceCursor cursor, string binding)
    {
        var data = JsonSerializer.SerializeToUtf8Bytes(cursor);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var cipher = new byte[data.Length];
        using var aes = new AesGcm(_cursorKey, 16);
        aes.Encrypt(nonce, data, cipher, tag, Encoding.UTF8.GetBytes(binding));
        return Convert.ToBase64String(nonce.Concat(tag).Concat(cipher).ToArray());
    }

    private WorkflowInstanceCursor DecodeCursor(string token, string binding)
    {
        if (token.Length > 1024) throw new FormatException("Invalid cursor.");
        var data = Convert.FromBase64String(token);
        if (data.Length < 29) throw new FormatException("Invalid cursor.");
        var plain = new byte[data.Length - 28];
        using var aes = new AesGcm(_cursorKey, 16);
        aes.Decrypt(data.AsSpan(0, 12), data.AsSpan(28), data.AsSpan(12, 16), plain, Encoding.UTF8.GetBytes(binding));
        return JsonSerializer.Deserialize<WorkflowInstanceCursor>(plain) ?? throw new FormatException("Invalid cursor.");
    }
}
