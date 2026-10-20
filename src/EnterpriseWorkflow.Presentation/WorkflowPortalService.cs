using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Extensions;
using EnterpriseWorkflow.Persistence;
using EnterpriseWorkflow.Runtime;
using EnterpriseWorkflow.Security;
using Microsoft.Extensions.Options;

namespace EnterpriseWorkflow.Presentation;

public sealed record WorkflowProcessCard(
    string Id,
    int Version,
    string Title,
    string Description,
    WorkflowFormDefinition StartForm,
    IReadOnlyList<TechnicalId> DesignatedAssignmentNodes,
    string? IconResourcePath);

public sealed record WorkflowStartRequest(
    string ProcessId,
    int ProcessVersion,
    string SubmissionJson,
    TechnicalId IdempotencyKey,
    IReadOnlyDictionary<TechnicalId, IdentityReference> DesignatedAssignments,
    string? BusinessKey = null,
    string? CorrelationId = null);

public sealed record WorkflowPortalStartResult(
    bool Succeeded,
    WorkflowInstanceId? InstanceId,
    bool WasReplay,
    IReadOnlyList<WorkflowFormError> Errors,
    string? ErrorCode = null);

public sealed record WorkflowTaskActionRequest(
    HumanTaskId TaskId,
    long ExpectedRevision,
    WorkflowActionId Action,
    TechnicalId IdempotencyKey,
    string SubmissionJson,
    IReadOnlyDictionary<TechnicalId, IdentityReference>? DesignatedAssignments = null);

public sealed record WorkflowPortalTaskActionResult(
    bool Succeeded,
    bool WasReplay,
    IReadOnlyList<WorkflowFormError> Errors,
    string? ErrorCode = null);

public sealed record WorkflowTaskView(
    HumanTaskId TaskId,
    WorkflowInstanceId InstanceId,
    string ProcessTitle,
    string NodeLabel,
    HumanTaskStatus Status,
    HumanTaskAssignmentMode AssignmentMode,
    long Revision,
    WorkflowFormDefinition Form,
    Type? CustomComponentType,
    CanonicalJson VisibleData,
    IReadOnlyList<HumanTaskAction> Actions,
    DateTimeOffset CreatedAtUtc,
    WorkflowStagePolicy? Policy = null,
    CanonicalJson? Summary = null,
    string? ProcessId = null,
    int ProcessVersion = 0,
    bool CanAssign = false,
    IReadOnlyDictionary<string, string>? SummaryLabels = null,
    IReadOnlyDictionary<string, string>? AssignmentLabels = null);

public sealed record WorkflowRequestView(
    WorkflowInstanceId InstanceId,
    string ProcessTitle,
    int ProcessVersion,
    WorkflowInstanceStatus Status,
    long Revision,
    string? BusinessKey,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record WorkflowCandidate(
    IdentityReference Identity,
    string DisplayName,
    string? Email,
    DirectoryAccountStatus AccountStatus);

public interface IWorkflowPortalService
{
    ValueTask<IReadOnlyList<WorkflowProcessCard>> ListStartableProcessesAsync(
        IdentityReference actor, CancellationToken cancellationToken);
    ValueTask<WorkflowPortalStartResult> StartAsync(
        IdentityReference actor, WorkflowStartRequest request, CancellationToken cancellationToken);
    ValueTask<HumanTaskInboxPage> ReadInboxAsync(
        IdentityReference actor, HumanTaskCursor? after, CancellationToken cancellationToken);
    ValueTask<IReadOnlyList<WorkflowRequestView>> ListMyRequestsAsync(
        IdentityReference actor, int maximumResults, CancellationToken cancellationToken);
    ValueTask<IReadOnlyList<WorkflowCandidate>> SearchAssignmentCandidatesAsync(
        IdentityReference actor, string processId, int processVersion, TechnicalId nodeId,
        string query, int maximumResults, CancellationToken cancellationToken);
    ValueTask<IReadOnlyList<WorkflowCandidate>> SearchTaskAssignmentCandidatesAsync(
        IdentityReference actor, HumanTaskId taskId, WorkflowActionId action, string query, int maximumResults, CancellationToken cancellationToken) =>
        ValueTask.FromResult<IReadOnlyList<WorkflowCandidate>>([]);
    ValueTask<WorkflowTaskView?> ReadTaskAsync(
        IdentityReference actor, HumanTaskId taskId, CancellationToken cancellationToken);
    ValueTask<WorkflowPortalTaskActionResult> AssignTaskAsync(IdentityReference actor, HumanTaskId taskId, long expectedRevision,
        IdentityReference assignee, CancellationToken cancellationToken) =>
        ValueTask.FromResult(new WorkflowPortalTaskActionResult(false, false, [], "presentation.assignment-not-supported"));
    ValueTask<IReadOnlyList<WorkflowCandidate>> SearchRecoveryCandidatesAsync(IdentityReference actor, HumanTaskId taskId, string query,
        int maximumResults, CancellationToken cancellationToken) => ValueTask.FromResult<IReadOnlyList<WorkflowCandidate>>([]);
    ValueTask<WorkflowPortalTaskActionResult> ClaimTaskAsync(
        IdentityReference actor, HumanTaskId taskId, long expectedRevision, CancellationToken cancellationToken);
    ValueTask<WorkflowPortalTaskActionResult> ReleaseTaskAsync(
        IdentityReference actor, HumanTaskId taskId, long expectedRevision, CancellationToken cancellationToken);
    ValueTask<WorkflowPortalTaskActionResult> CompleteTaskAsync(
        IdentityReference actor, WorkflowTaskActionRequest request, CancellationToken cancellationToken);
}

public sealed class WorkflowPortalOptions
{
    public string InstallationId { get; set; } = "enterprise-workflow";

    public void Validate()
    {
        if (!TechnicalId.IsValid(InstallationId))
            throw new InvalidOperationException("Presentation installation id must be a valid technical identifier.");
    }
}

public sealed class WorkflowPortalService(
    IWorkflowPresentationCatalog catalog,
    IWorkflowAuthorizationService authorization,
    IWorkflowFormService forms,
    IWorkflowStore store,
    IWorkflowStartService starts,
    IHumanTaskService humanTasks,
    IAuthorizationProvider generalAuthorization,
    ILocalAccountStore localAccounts,
    IReadOnlyDictionary<string, IIdentityDirectory> directories,
    IOptions<WorkflowPortalOptions> configuredOptions) : IWorkflowPortalService
{
    private readonly WorkflowPortalOptions _options = Validate(configuredOptions.Value);

    public async ValueTask<IReadOnlyList<WorkflowProcessCard>> ListStartableProcessesAsync(
        IdentityReference actor,
        CancellationToken cancellationToken)
    {
        var accepted = new List<WorkflowProcessCard>();
        foreach (var process in catalog.Processes)
        {
            var decision = await authorization.AuthorizeAsync(new(actor,
                new(process.Definition.Id.Value, process.Definition.Version, null, WorkflowActions.Start)),
                cancellationToken).ConfigureAwait(false);
            if (!decision.Allowed || !catalog.TryResolveForm(process.Definition, process.StartForm, out var registration) ||
                registration is null)
                continue;
            accepted.Add(new(process.Definition.Id.Value, process.Definition.Version, process.Title,
                process.Description, registration.Form, process.DesignatedAssignmentNodes, process.IconResourcePath));
        }
        return accepted;
    }

    public async ValueTask<WorkflowPortalStartResult> StartAsync(
        IdentityReference actor,
        WorkflowStartRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!catalog.TryGetProcess(request.ProcessId, request.ProcessVersion, out var process) || process is null)
            return Failed("presentation.process-not-found");
        if (!catalog.TryResolveForm(process.Definition, process.StartForm, out var registration) || registration is null)
            return Failed("presentation.start-form-not-found");
        var validation = forms.Validate(registration.Form, request.SubmissionJson);
        if (!validation.Succeeded)
            return new(false, null, false, validation.Errors);
        var requiredAssignments = process.DesignatedAssignmentNodes.ToHashSet();
        if (!requiredAssignments.SetEquals(request.DesignatedAssignments.Keys))
            return Failed("presentation.designated-assignments-incomplete");

        var published = await store.PublishDefinitionAsync(new(process.Definition), cancellationToken).ConfigureAwait(false);
        if (published.Outcome is not (StoreOutcome.Succeeded or StoreOutcome.Idempotent) || published.Value is null)
            return Failed(published.ErrorCode ?? "presentation.definition-publication-failed");
        var assignments = request.DesignatedAssignments.Select(item => new DesignatedTaskAssignment(
            item.Key, new(new TechnicalId(item.Value.ProviderId), item.Value.SubjectId))).ToArray();
        var scope = new StartCommandScope(new(_options.InstallationId), new("workflow.start"),
            new(new TechnicalId(actor.ProviderId), actor.SubjectId));
        var submission = validation.Submission!;
        var command = new StartInstanceCommand(scope, request.IdempotencyKey,
            RequestHash(process.Definition, submission, assignments), published.Value,
            WorkflowState.Create(submission.CanonicalText, process.InitialStateSchemaVersion),
            request.BusinessKey, request.CorrelationId, MillisecondUtcNow(), assignments);
        var started = await starts.StartAsync(process.Definition, command, cancellationToken).ConfigureAwait(false);
        return started.Succeeded
            ? new(true, started.InstanceId, started.WasReplay, [])
            : Failed(started.ErrorCode ?? "presentation.workflow-start-failed");
    }

    public ValueTask<HumanTaskInboxPage> ReadInboxAsync(
        IdentityReference actor,
        HumanTaskCursor? after,
        CancellationToken cancellationToken) => humanTasks.ReadInboxAsync(actor, after, cancellationToken);

    public async ValueTask<IReadOnlyList<WorkflowRequestView>> ListMyRequestsAsync(
        IdentityReference actor,
        int maximumResults,
        CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(maximumResults, 1, 200);
        var result = await store.ReadWorkflowInstancesAsync(new(
            new(new TechnicalId(actor.ProviderId), actor.SubjectId), limit), cancellationToken).ConfigureAwait(false);
        if (result.Outcome is not StoreOutcome.Succeeded || result.Value is null) return [];
        var accepted = new List<WorkflowRequestView>(result.Value.Count);
        foreach (var instance in result.Value)
        {
            var decision = await authorization.AuthorizeAsync(new(actor,
                new(instance.DefinitionId.Value, instance.DefinitionVersion, null, WorkflowActions.ReadInstance)),
                cancellationToken).ConfigureAwait(false);
            if (!decision.Allowed) continue;
            var title = catalog.TryGetProcess(instance.DefinitionId.Value, instance.DefinitionVersion, out var process) && process is not null
                ? process.Title
                : instance.DefinitionId.Value;
            accepted.Add(new(instance.InstanceId, title, instance.DefinitionVersion, instance.Status,
                instance.Revision, instance.BusinessKey, instance.CreatedAtUtc, instance.UpdatedAtUtc));
        }
        return accepted;
    }

    public ValueTask<IReadOnlyList<WorkflowCandidate>> SearchAssignmentCandidatesAsync(
        IdentityReference actor, string processId, int processVersion, TechnicalId nodeId,
        string query, int maximumResults, CancellationToken cancellationToken) =>
        SearchCandidatesAsync(actor, processId, processVersion, nodeId, query, maximumResults, false, cancellationToken);

    public async ValueTask<IReadOnlyList<WorkflowCandidate>> SearchTaskAssignmentCandidatesAsync(
        IdentityReference actor, HumanTaskId taskId, WorkflowActionId action, string query, int maximumResults, CancellationToken cancellationToken)
    {
        var loaded = await humanTasks.ReadAsync(taskId, actor, cancellationToken).ConfigureAwait(false);
        if (loaded.Snapshot is not { } snapshot || !loaded.Succeeded || snapshot.Status is not (HumanTaskStatus.Assigned or HumanTaskStatus.Claimed)) return [];
        var source = snapshot.Definition.Nodes.Single(node => node.Id == snapshot.NodeId);
        var policy = WorkflowStagePolicy.Read(source.Configuration);
        if (policy is null || !policy.Actions.TryGetValue(action.Value, out var actionPolicy) || actionPolicy.AssigneeTarget is null) return [];
        var allowed = await authorization.AuthorizeAsync(new(actor, new(snapshot.Definition.Id.Value, snapshot.Definition.Version, source.Id.Value, action)), cancellationToken).ConfigureAwait(false);
        if (!allowed.Allowed || snapshot.Assignee != new ActorIdentity(new(actor.ProviderId), actor.SubjectId)) return [];
        var candidates = await SearchCandidatesAsync(actor, snapshot.Definition.Id.Value, snapshot.Definition.Version,
            new(actionPolicy.AssigneeTarget), query, maximumResults, true, cancellationToken).ConfigureAwait(false);
        var target = snapshot.Definition.Nodes.Single(node => node.Id.Value == actionPolicy.AssigneeTarget).HumanTask!;
        return candidates.Where(candidate =>
        {
            var identity = new ActorIdentity(new(candidate.Identity.ProviderId), candidate.Identity.SubjectId);
            return (target.AllowInitiator || identity != snapshot.Initiator) &&
                !target.DistinctFrom.Any(constraint =>
                    (constraint.NodeId == source.Id && constraint.ActionId.Value == action.Value && identity == snapshot.Assignee) ||
                    snapshot.CompletionHistory.Any(history => history.NodeId == constraint.NodeId && history.ActionId == constraint.ActionId && history.Actor == identity));
        }).ToArray();
    }

    private async ValueTask<IReadOnlyList<WorkflowCandidate>> SearchCandidatesAsync(
        IdentityReference actor,
        string processId,
        int processVersion,
        TechnicalId nodeId,
        string query,
        int maximumResults,
        bool trustedSource,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2 || query.Length > 256 ||
            !catalog.TryGetProcess(processId, processVersion, out var process) || process is null)
            return [];
        var node = process.Definition.Nodes.SingleOrDefault(candidate => candidate.Id == nodeId);
        if (node?.HumanTask is not { AssignmentMode: HumanTaskAssignmentMode.DesignatedIdentity } task)
            return [];
        var searchDecision = await generalAuthorization.AuthorizeAsync(
            new(actor, WorkflowPermissions.SearchApprovalCandidates), cancellationToken).ConfigureAwait(false);
        if (!trustedSource && !searchDecision.Allowed) return [];

        var take = Math.Clamp(maximumResults, 1, 50);
        var candidates = new Dictionary<IdentityReference, DirectoryIdentity>();
        var accounts = await localAccounts.ListAsync(500, cancellationToken).ConfigureAwait(false);
        if (accounts.Outcome is ProviderOutcome.Succeeded && accounts.Value is not null)
        {
            foreach (var account in accounts.Value.Where(item => item.Enabled &&
                         item.UserName.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase)))
                candidates[account.Identity] = new(account.Identity, account.UserName, null,
                    new HashSet<string>(StringComparer.Ordinal), DirectoryAccountStatus.Enabled);
        }
        foreach (var directory in directories.Values.Where(item =>
                     item.Capabilities.HasFlag(DirectoryCapabilities.Search)))
        {
            var found = await directory.SearchAsync(query.Trim(), Math.Min(take * 3, 100), cancellationToken)
                .ConfigureAwait(false);
            if (found.Outcome is not ProviderOutcome.Succeeded || found.Value is null) continue;
            foreach (var candidate in found.Value) candidates[candidate.Identity] = candidate;
        }

        var accepted = new List<WorkflowCandidate>(take);
        foreach (var candidate in candidates.Values.OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            if (candidate.AccountStatus is DirectoryAccountStatus.Disabled) continue;
            var allowed = false;
            foreach (var action in task.Actions)
            {
                var decision = await authorization.AuthorizeAsync(new(candidate.Identity,
                    new(processId, processVersion, nodeId.Value, new(action.ActionId.Value))), cancellationToken)
                    .ConfigureAwait(false);
                if (!decision.Allowed) continue;
                allowed = true;
                break;
            }
            if (!allowed) continue;
            accepted.Add(new(candidate.Identity, candidate.DisplayName, candidate.Email, candidate.AccountStatus));
            if (accepted.Count == take) break;
        }
        return accepted;
    }

    public async ValueTask<WorkflowTaskView?> ReadTaskAsync(
        IdentityReference actor,
        HumanTaskId taskId,
        CancellationToken cancellationToken)
    {
        var result = await humanTasks.ReadAsync(taskId, actor, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded || result.Snapshot is null) return null;
        var snapshot = result.Snapshot;
        var node = snapshot.Definition.Nodes.Single(item => item.Id == snapshot.NodeId);
        var contract = node.HumanTask!;
        if (!catalog.TryResolveForm(snapshot.Definition, contract.Form, out var registration) || registration is null)
            return null;
        var data = forms.Project(registration.Form, snapshot.State.Value, WorkflowFieldAudience.TaskParticipant);
        var policy = WorkflowStagePolicy.Read(node.Configuration);
        var actions = new List<HumanTaskAction>();
        foreach (var action in contract.Actions)
        {
            var allowed = await authorization.AuthorizeAsync(new(actor, new(snapshot.Definition.Id.Value, snapshot.Definition.Version, node.Id.Value, new(action.ActionId.Value))), cancellationToken).ConfigureAwait(false);
            if (allowed.Allowed) actions.Add(action);
        }
        CanonicalJson? summary = null;
        IReadOnlyDictionary<string, string>? summaryLabels = null;
        if (policy is not null && catalog.TryGetProcess(snapshot.Definition.Id.Value, snapshot.Definition.Version, out var process) && process is not null &&
            catalog.TryResolveForm(snapshot.Definition, process.StartForm, out var startRegistration) && startRegistration is not null)
        {
            var summaryForm = startRegistration.Form with { Fields = startRegistration.Form.Fields.Where(field => policy.SummaryFields.Contains(field.Id.Value, StringComparer.Ordinal)).ToImmutableArray() };
            summary = forms.Project(summaryForm, snapshot.State.Value, WorkflowFieldAudience.TaskParticipant);
            summaryLabels = summaryForm.Fields.ToDictionary(field => field.Id.Value, field => field.Label, StringComparer.Ordinal);
        }
        var canAssign = snapshot.Status == HumanTaskStatus.AwaitingAssignment &&
            (await authorization.AuthorizeAsync(new(actor, new(snapshot.Definition.Id.Value, snapshot.Definition.Version, node.Id.Value, WorkflowActions.AssignTask)), cancellationToken).ConfigureAwait(false)).Allowed;
        return new(snapshot.TaskId, snapshot.InstanceId,
            snapshot.Definition.DisplayLabel ?? snapshot.Definition.Id.Value,
            node.DisplayLabel ?? node.Id.Value, snapshot.Status, snapshot.AssignmentMode,
            snapshot.Revision, registration.Form, registration.CustomComponentType, data,
            actions, snapshot.CreatedAtUtc, policy, summary, snapshot.Definition.Id.Value, snapshot.Definition.Version, canAssign, summaryLabels,
            snapshot.Definition.Nodes.Where(candidate => candidate.HumanTask is not null).ToDictionary(candidate => candidate.Id.Value,
                candidate => candidate.DisplayLabel ?? candidate.Id.Value, StringComparer.Ordinal));
    }

    public async ValueTask<WorkflowPortalTaskActionResult> AssignTaskAsync(IdentityReference actor, HumanTaskId taskId, long expectedRevision,
        IdentityReference assignee, CancellationToken cancellationToken) =>
        ToPortalResult(await humanTasks.AssignAsync(taskId, expectedRevision, actor, assignee, cancellationToken).ConfigureAwait(false));

    public async ValueTask<IReadOnlyList<WorkflowCandidate>> SearchRecoveryCandidatesAsync(IdentityReference actor, HumanTaskId taskId,
        string query, int maximumResults, CancellationToken cancellationToken)
    {
        var loaded = await humanTasks.ReadAsync(taskId, actor, cancellationToken).ConfigureAwait(false);
        if (loaded.Snapshot is not { Status: HumanTaskStatus.AwaitingAssignment } snapshot || !loaded.Succeeded) return [];
        var allowed = await authorization.AuthorizeAsync(new(actor, new(snapshot.Definition.Id.Value, snapshot.Definition.Version, snapshot.NodeId.Value, WorkflowActions.AssignTask)), cancellationToken).ConfigureAwait(false);
        if (!allowed.Allowed) return [];
        var candidates = await SearchCandidatesAsync(actor, snapshot.Definition.Id.Value, snapshot.Definition.Version, snapshot.NodeId,
            query, maximumResults, true, cancellationToken).ConfigureAwait(false);
        var node = snapshot.Definition.Nodes.Single(node => node.Id == snapshot.NodeId);
        var policy = WorkflowStagePolicy.Read(node.Configuration);
        return candidates.Where(candidate =>
        {
            var identity = new ActorIdentity(new(candidate.Identity.ProviderId), candidate.Identity.SubjectId);
            return (policy?.AssignedToInitiator != true || identity == snapshot.Initiator) &&
                (node.HumanTask!.AllowInitiator || identity != snapshot.Initiator) &&
                !node.HumanTask.DistinctFrom.Any(constraint => snapshot.CompletionHistory.Any(history => history.NodeId == constraint.NodeId && history.ActionId == constraint.ActionId && history.Actor == identity));
        }).ToArray();
    }

    public async ValueTask<WorkflowPortalTaskActionResult> ClaimTaskAsync(
        IdentityReference actor,
        HumanTaskId taskId,
        long expectedRevision,
        CancellationToken cancellationToken) =>
        ToPortalResult(await humanTasks.ClaimAsync(taskId, expectedRevision, actor, cancellationToken)
            .ConfigureAwait(false));

    public async ValueTask<WorkflowPortalTaskActionResult> ReleaseTaskAsync(
        IdentityReference actor,
        HumanTaskId taskId,
        long expectedRevision,
        CancellationToken cancellationToken) =>
        ToPortalResult(await humanTasks.ReleaseAsync(taskId, expectedRevision, actor, cancellationToken)
            .ConfigureAwait(false));

    public async ValueTask<WorkflowPortalTaskActionResult> CompleteTaskAsync(
        IdentityReference actor,
        WorkflowTaskActionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var task = await ReadTaskAsync(actor, request.TaskId, cancellationToken).ConfigureAwait(false);
        if (task is null) return FailedTask("presentation.task-not-found");
        if (!task.Actions.Any(candidate => candidate.ActionId.Value == request.Action.Value))
            return FailedTask("presentation.task-action-not-declared");
        var validation = forms.Validate(task.Form, request.SubmissionJson);
        if (!validation.Succeeded)
            return new(false, false, validation.Errors);
        return ToPortalResult(await humanTasks.CompleteAsync(
            request.TaskId,
            request.ExpectedRevision,
            actor,
            request.Action,
            request.IdempotencyKey,
            HumanTaskSubmission.WithAssignments(validation.Submission!, (request.DesignatedAssignments ?? new Dictionary<TechnicalId, IdentityReference>())
                .Select(item => new DesignatedTaskAssignment(item.Key, new(new(item.Value.ProviderId), item.Value.SubjectId)))),
            cancellationToken).ConfigureAwait(false));
    }

    private static string RequestHash(
        WorkflowDefinition definition,
        CanonicalJson submission,
        IReadOnlyList<DesignatedTaskAssignment> assignments)
    {
        var material = new StringBuilder(definition.Sha256).Append('\n').Append(submission.CanonicalText);
        foreach (var assignment in assignments.OrderBy(item => item.NodeId.Value, StringComparer.Ordinal))
            material.Append('\n').Append(assignment.NodeId.Value).Append('|')
                .Append(assignment.Assignee.ProviderId.Value).Append('|').Append(assignment.Assignee.SubjectId);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(material.ToString())));
    }

    private static DateTimeOffset MillisecondUtcNow() =>
        DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

    private static WorkflowPortalStartResult Failed(string code) => new(false, null, false, [], code);
    private static WorkflowPortalTaskActionResult FailedTask(string code) => new(false, false, [], code);
    private static WorkflowPortalTaskActionResult ToPortalResult(HumanTaskCommandResult result) =>
        result.Succeeded
            ? new(true, result.WasReplay, [])
            : FailedTask(result.ErrorCode ?? "presentation.task-action-failed");

    private static WorkflowPortalOptions Validate(WorkflowPortalOptions options)
    {
        options.Validate();
        return options;
    }
}
