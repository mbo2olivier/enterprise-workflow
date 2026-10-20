using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Persistence;
using EnterpriseWorkflow.Runtime;
using EnterpriseWorkflow.Security;

namespace EnterpriseWorkflow.Presentation;

public sealed class WorkflowSmtpOptions
{
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string? From { get; set; }
    public string BaseUrl { get; set; } = "https://localhost";
    public int PageSize { get; set; } = 50;
    public int TimeoutSeconds { get; set; } = 15;
    public Dictionary<string, string> Addresses { get; set; } = new(StringComparer.Ordinal);
    public bool Enabled => !string.IsNullOrWhiteSpace(Host);
    public void Validate()
    {
        if (!Enabled) return;
        if (Port is < 1 or > 65535 || PageSize is < 1 or > 100 || TimeoutSeconds is < 1 or > 20 ||
            !Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
            throw new InvalidOperationException("Invalid SMTP notification options.");
        _ = new MailAddress(From ?? throw new InvalidOperationException("SMTP From is required."));
    }
}

public interface IWorkflowMailSender
{
    ValueTask SendAsync(string address, string taskUrl, string idempotencyKey, CancellationToken cancellationToken);
}

public sealed class SmtpWorkflowMailSender(WorkflowSmtpOptions options) : IWorkflowMailSender
{
    public async ValueTask SendAsync(string address, string taskUrl, string idempotencyKey, CancellationToken cancellationToken)
    {
        using var client = new SmtpClient(options.Host!, options.Port)
        {
            EnableSsl = options.EnableSsl,
            UseDefaultCredentials = false,
            Timeout = options.TimeoutSeconds * 1000,
        };
        if (!string.IsNullOrWhiteSpace(options.UserName)) client.Credentials = new NetworkCredential(options.UserName, options.Password);
        using var message = new MailMessage(options.From!, address, "Une tâche est disponible", $"Une tâche est disponible dans votre file : {taskUrl}");
        message.Headers.Add("X-Workflow-Idempotency-Key", idempotencyKey);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
        await client.SendMailAsync(message, timeout.Token).ConfigureAwait(false);
    }
}

/// <summary>Fan-out pages and individual emails are separate durable outbox operations.</summary>
public sealed class WorkflowNotificationTransport(
    IWorkflowStore store,
    IWorkflowAuthorizationService authorization,
    ISecurityProfileStore profiles,
    ILocalAccountStore accounts,
    IReadOnlyDictionary<string, IIdentityDirectory> directories,
    WorkflowSmtpOptions options,
    IWorkflowMailSender sender,
    IExternalEffectTransport fallback) : IExternalEffectTransport
{
    public async ValueTask<ExternalEffectDeliveryResult> DeliverAsync(OutboxLease message, CancellationToken cancellationToken)
    {
        if (message.Destination.Value is not ("workflow.pool-notification" or "workflow.pool-mail"))
            return await fallback.DeliverAsync(message, cancellationToken).ConfigureAwait(false);
        if (!options.Enabled) return ExternalEffectDeliveryResult.Success();
        var loaded = await store.GetHumanTaskByActivationAsync(message.ActivationId, cancellationToken).ConfigureAwait(false);
        if (loaded.Outcome != StoreOutcome.Succeeded || loaded.Value is null)
            return ExternalEffectDeliveryResult.Retryable(loaded.ErrorCode ?? "notification.task-unavailable");
        var task = loaded.Value;
        if (task.Status != HumanTaskStatus.Available) return ExternalEffectDeliveryResult.Success();
        if (message.Destination.Value == "workflow.pool-mail")
        {
            var recipient = JsonSerializer.Deserialize<Recipient>(message.PayloadJson)
                ?? throw new FormatException("Invalid email payload.");
            if (!await EligibleAsync(task, recipient.Identity, cancellationToken).ConfigureAwait(false)) return ExternalEffectDeliveryResult.Success();
            var currentEmail = recipient.Email;
            if (recipient.Identity.ProviderId == SecurityProviderIds.Local)
            {
                var account = await accounts.FindByIdentityAsync(recipient.Identity, cancellationToken).ConfigureAwait(false);
                if (account.Outcome == ProviderOutcome.Unavailable) return ExternalEffectDeliveryResult.Retryable("notification.local-directory-unavailable");
                if (account.Value?.Enabled != true) return ExternalEffectDeliveryResult.Success();
            }
            else if (directories.TryGetValue(recipient.Identity.ProviderId, out var directory))
            {
                var current = await directory.FindAsync(recipient.Identity, false, cancellationToken).ConfigureAwait(false);
                if (current.Outcome == ProviderOutcome.Unavailable) return ExternalEffectDeliveryResult.Retryable("notification.directory-unavailable");
                if (current.Value is null || current.Value.AccountStatus == DirectoryAccountStatus.Disabled) return ExternalEffectDeliveryResult.Success();
                currentEmail = current.Value.Email;
            }
            else return ExternalEffectDeliveryResult.PermanentFailure("notification.identity-provider-not-configured");
            var address = options.Addresses.GetValueOrDefault(AddressKey(recipient.Identity)) ?? currentEmail;
            if (string.IsNullOrWhiteSpace(address)) return ExternalEffectDeliveryResult.PermanentFailure("notification.email-missing");
            try
            {
                await sender.SendAsync(address, $"{options.BaseUrl.TrimEnd('/')}/tasks/{task.TaskId.Value:D}", message.IdempotencyKey, cancellationToken).ConfigureAwait(false);
                return ExternalEffectDeliveryResult.Success();
            }
            catch (FormatException) { return ExternalEffectDeliveryResult.PermanentFailure("notification.email-invalid"); }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return ExternalEffectDeliveryResult.Retryable("notification.smtp-timeout"); }
            catch (SmtpException) { return ExternalEffectDeliveryResult.Retryable("notification.smtp-unavailable"); }
        }

        var cursor = JsonSerializer.Deserialize<Cursor>(message.PayloadJson) ?? new();
        IReadOnlyList<DirectoryIdentity> identities;
        Cursor? next;
        if (string.IsNullOrEmpty(cursor.Source))
        {
            var page = await profiles.ListKnownIdentitiesAsync(cursor.Offset, options.PageSize, cancellationToken).ConfigureAwait(false);
            if (page.Outcome != ProviderOutcome.Succeeded || page.Value is null)
                return ExternalEffectDeliveryResult.Retryable(page.ErrorCode ?? "notification.identities-unavailable");
            var known = new List<DirectoryIdentity>();
            foreach (var identity in page.Value)
            {
                if (identity.ProviderId == SecurityProviderIds.Local)
                {
                    var account = await accounts.FindByIdentityAsync(identity, cancellationToken).ConfigureAwait(false);
                    if (account.Outcome == ProviderOutcome.Unavailable) return ExternalEffectDeliveryResult.Retryable("notification.local-directory-unavailable");
                    if (account.Value?.Enabled == true) known.Add(new(identity, account.Value.UserName, null, new HashSet<string>(), DirectoryAccountStatus.Enabled));
                }
                else if (directories.TryGetValue(identity.ProviderId, out var directory))
                {
                    var found = await directory.FindAsync(identity, false, cancellationToken).ConfigureAwait(false);
                    if (found.Outcome == ProviderOutcome.Unavailable) return ExternalEffectDeliveryResult.Retryable("notification.directory-unavailable");
                    if (found.Value is not null) known.Add(found.Value);
                }
                else return ExternalEffectDeliveryResult.PermanentFailure("notification.identity-provider-not-configured");
            }
            identities = known;
            next = page.Value.Count == options.PageSize ? cursor with { Offset = cursor.Offset + options.PageSize } : NextDirectory(null);
        }
        else
        {
            if (!directories.TryGetValue(cursor.Source, out var directory) || directory is not IPagedIdentityDirectory paged)
                return ExternalEffectDeliveryResult.PermanentFailure("notification.directory-enumeration-unsupported");
            var page = await paged.ReadPageAsync(cursor.DirectoryCursor, options.PageSize, cancellationToken).ConfigureAwait(false);
            if (page.Outcome != ProviderOutcome.Succeeded || page.Value is null)
                return ExternalEffectDeliveryResult.Retryable(page.ErrorCode ?? "notification.directory-unavailable");
            identities = page.Value.Identities;
            next = page.Value.Next is null ? NextDirectory(cursor.Source) : cursor with { DirectoryCursor = page.Value.Next };
        }
        var followUps = new List<OutboxWrite>();
        foreach (var identity in identities.DistinctBy(identity => identity.Identity))
        {
            if (identity.AccountStatus == DirectoryAccountStatus.Disabled || !await EligibleAsync(task, identity.Identity, cancellationToken).ConfigureAwait(false)) continue;
            followUps.Add(new(new($"pool-mail-{Hash(AddressKey(identity.Identity))}"), new("workflow.pool-mail"), "application/json",
                JsonSerializer.Serialize(new Recipient(identity.Identity.ProviderId, identity.Identity.SubjectId, identity.Email))));
        }
        if (next is not null)
        {
            var payload = JsonSerializer.Serialize(next);
            followUps.Add(new(new($"pool-page-{Hash(payload)}"), new("workflow.pool-notification"), "application/json", payload));
        }
        return new(ExternalEffectDeliveryDisposition.Delivered, FollowUps: followUps);
    }

    private Cursor? NextDirectory(string? previous)
    {
        var next = directories.Keys.Order(StringComparer.Ordinal).FirstOrDefault(id => previous is null || string.CompareOrdinal(id, previous) > 0);
        return next is null ? null : new Cursor { Source = next };
    }
    private async ValueTask<bool> EligibleAsync(HumanTaskSnapshot task, IdentityReference identity, CancellationToken cancellationToken)
    {
        var node = task.Definition.Nodes.Single(node => node.Id == task.NodeId);
        var actor = new ActorIdentity(new(identity.ProviderId), identity.SubjectId);
        if ((!node.HumanTask!.AllowInitiator && actor == task.Initiator) ||
            node.HumanTask.DistinctFrom.Any(constraint => task.CompletionHistory.Any(history => history.NodeId == constraint.NodeId && history.ActionId == constraint.ActionId && history.Actor == actor))) return false;
        foreach (var action in node.HumanTask.Actions)
        {
            var allowed = await authorization.AuthorizeAsync(new(identity, new(task.Definition.Id.Value, task.Definition.Version, task.NodeId.Value, new(action.ActionId.Value))), cancellationToken).ConfigureAwait(false);
            if (allowed.ProviderUnavailable) throw new InvalidOperationException("Notification authorization unavailable.");
            if (allowed.Allowed) return true;
        }
        return false;
    }
    public static string AddressKey(IdentityReference identity) => $"{Uri.EscapeDataString(identity.ProviderId)}|{Uri.EscapeDataString(identity.SubjectId)}";
    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private sealed record Cursor
    {
        public string? Source { get; init; }
        public int Offset { get; init; }
        public string? DirectoryCursor { get; init; }
    }
    private sealed record Recipient(string ProviderId, string SubjectId, string? Email)
    {
        [System.Text.Json.Serialization.JsonIgnore]
        public IdentityReference Identity => new(ProviderId, SubjectId);
    }
}
