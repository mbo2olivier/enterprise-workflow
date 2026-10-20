using System.Net.Mail;
using System.Reflection;
using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Core.Model;
using EnterpriseWorkflow.Persistence;
using EnterpriseWorkflow.Presentation;
using EnterpriseWorkflow.Runtime;
using EnterpriseWorkflow.Security;
using EnterpriseWorkflow.Sdk;
using Xunit;

namespace EnterpriseWorkflow.Presentation.Tests;

public sealed class WorkflowNotificationTests
{
    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2012:Use ValueTasks correctly", Justification = "DispatchProxy boxes completed ValueTasks; each interface call consumes its newly created result once.")]
    public async Task FanOutIsPagedAndMailRetryKeepsItsIdempotencyKey()
    {
        var token = TestContext.Current.CancellationToken;
        var definition = WorkflowBuilder.Create("notifications", 1).Start("start")
            .HumanTask("review", HumanTaskKind.Approval, HumanTaskAssignmentMode.EligiblePool, "form", 1, "handler", [new("approve", "approved")])
            .End("end").Then("start", "review").On("review", "approved", "end").Validate().Definition!;
        var task = new HumanTaskSnapshot(new(Guid.NewGuid()), new(Guid.NewGuid()), new(Guid.NewGuid()), new("review"),
            HumanTaskStatus.Available, HumanTaskAssignmentMode.EligiblePool, null, 0, 1, new(new("local"), "requester"),
            definition, WorkflowState.Create("{}", 1), DateTimeOffset.UtcNow, []);
        var identity = new IdentityReference("local", "reviewer");
        var store = Fake<IWorkflowStore>((method, _) => method.Name == "GetHumanTaskByActivationAsync"
            ? ValueTask.FromResult(StoreResults.Succeeded(task)) : throw new NotSupportedException(method.Name));
        var profiles = Fake<ISecurityProfileStore>((method, args) => method.Name == "ListKnownIdentitiesAsync"
            ? ValueTask.FromResult(new ProviderResult<IReadOnlyList<IdentityReference>>(ProviderOutcome.Succeeded, (int)args![0]! == 0 ? [identity] : []))
            : throw new NotSupportedException(method.Name));
        var accounts = Fake<ILocalAccountStore>((method, _) => method.Name == "FindByIdentityAsync"
            ? ValueTask.FromResult(new ProviderResult<LocalAccount>(ProviderOutcome.Succeeded, new(identity, "Reviewer", "REVIEWER", "unused", true, 0, null, 0)))
            : throw new NotSupportedException(method.Name));
        var sender = new Sender();
        var authorization = new Authorization();
        var smtp = new WorkflowSmtpOptions { Host = "smtp.invalid", From = "workflow@example.org", PageSize = 1 };
        smtp.Addresses[WorkflowNotificationTransport.AddressKey(identity)] = "reviewer@example.org";
        var transport = new WorkflowNotificationTransport(store, authorization, profiles, accounts,
            new Dictionary<string, IIdentityDirectory>(), smtp, sender, new Fallback());
        var lease = new OutboxLease(new(Guid.NewGuid()), task.InstanceId, task.ActivationId, new("pool-notification"),
            new("workflow.pool-notification"), "application/json", "{}", "stable-key", 1, 1, new(Guid.NewGuid()), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(1));
        var page = await transport.DeliverAsync(lease, token);
        Assert.Equal(ExternalEffectDeliveryDisposition.Delivered, page.Disposition);
        Assert.Equal(2, page.FollowUps!.Count);
        var mail = Assert.Single(page.FollowUps, followUp => followUp.Destination.Value == "workflow.pool-mail");
        var mailLease = lease with { Destination = mail.Destination, OperationId = mail.OperationId, PayloadJson = mail.PayloadJson };
        Assert.Equal(ExternalEffectDeliveryDisposition.RetryableFailure, (await transport.DeliverAsync(mailLease, token)).Disposition);
        Assert.Equal(ExternalEffectDeliveryDisposition.Delivered, (await transport.DeliverAsync(mailLease, token)).Disposition);
        Assert.Equal(["stable-key", "stable-key"], sender.Keys);
        authorization.Allowed = false;
        Assert.Equal(ExternalEffectDeliveryDisposition.Delivered, (await transport.DeliverAsync(mailLease, token)).Disposition);
        Assert.Equal(2, sender.Keys.Count);
    }

    [Fact]
    public async Task SmtpAbsentDoesNotRequireDirectoryOrStoreAccess()
    {
        var transport = new WorkflowNotificationTransport(null!, null!, null!, null!, null!, new(), null!, new Fallback());
        var lease = new OutboxLease(new(Guid.NewGuid()), new(Guid.NewGuid()), new(Guid.NewGuid()), new("notify"),
            new("workflow.pool-notification"), "application/json", "{}", "key", 1, 1, new(Guid.NewGuid()), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(1));
        Assert.Equal(ExternalEffectDeliveryDisposition.Delivered,
            (await transport.DeliverAsync(lease, TestContext.Current.CancellationToken)).Disposition);
    }

    private static T Fake<T>(Func<MethodInfo, object?[]?, object?> invoke) where T : class
    {
        var proxy = DispatchProxy.Create<T, TestProxy>();
        ((TestProxy)(object)proxy).Implementation = invoke;
        return proxy;
    }
    public class TestProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?> Implementation { get; set; } = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Implementation(targetMethod!, args);
    }
    private sealed class Sender : IWorkflowMailSender
    {
        public List<string> Keys { get; } = [];
        public ValueTask SendAsync(string address, string taskUrl, string idempotencyKey, CancellationToken cancellationToken)
        {
            Keys.Add(idempotencyKey);
            if (Keys.Count == 1) throw new SmtpException("Simulated outage");
            return ValueTask.CompletedTask;
        }
    }
    private sealed class Authorization : IWorkflowAuthorizationService
    {
        public bool Allowed { get; set; } = true;
        public ValueTask<AuthorizationDecision> AuthorizeAsync(WorkflowAuthorizationRequest request, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new AuthorizationDecision(Allowed, "test"));
    }
    private sealed class Fallback : IExternalEffectTransport
    {
        public ValueTask<ExternalEffectDeliveryResult> DeliverAsync(OutboxLease message, CancellationToken cancellationToken) =>
            ValueTask.FromResult(ExternalEffectDeliveryResult.PermanentFailure("not-configured"));
    }
}
