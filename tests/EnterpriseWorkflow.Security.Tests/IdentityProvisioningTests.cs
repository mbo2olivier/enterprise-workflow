using Xunit;

namespace EnterpriseWorkflow.Security.Tests;

/// <summary>Contract example; consuming hosts can reuse these scenarios for their durable implementation.</summary>
public sealed class IdentityProvisioningTests
{
    [Fact]
    public async Task ApprovalNeedsPermissionAnotherActorCurrentRevisionAndAnUnchangedSource()
    {
        var provider = new DemonstrationProvider();
        var permission = new PermissionGate();
        var administration = new IdentityProvisioningAdministration(permission);
        var maker = new IdentityReference("local", "maker");
        var checker = new IdentityReference("local", "checker");
        var submitted = await administration.SubmitAsync(provider, new("employee", ["reviewers"], maker), TestContext.Current.CancellationToken);
        Assert.Equal(ProviderOutcome.Succeeded, submitted.Outcome);
        Assert.Equal(IdentityAdmissionStatus.PendingApproval, (await provider.GetAdmissionAsync(new("demo", "employee"), TestContext.Current.CancellationToken)).Value);
        Assert.Equal(ProviderOutcome.Conflict, (await administration.SubmitAsync(provider, new("employee", ["reviewers"], maker), TestContext.Current.CancellationToken)).Outcome);
        var request = submitted.Value!;
        Assert.Equal(ProviderOutcome.Forbidden, (await administration.DecideAsync(provider, new(request.Id, request.Revision, maker), true, TestContext.Current.CancellationToken)).Outcome);
        Assert.Equal(ProviderOutcome.Conflict, (await administration.DecideAsync(provider, new(request.Id, request.Revision + 1, checker), true, TestContext.Current.CancellationToken)).Outcome);
        permission.Allowed = false;
        Assert.Equal(ProviderOutcome.Forbidden, (await administration.DecideAsync(provider, new(request.Id, request.Revision, checker), true, TestContext.Current.CancellationToken)).Outcome);
        permission.Allowed = true;
        provider.SourceName = "Changed after review";
        Assert.Equal(ProviderOutcome.Conflict, (await administration.DecideAsync(provider, new(request.Id, request.Revision, checker), true, TestContext.Current.CancellationToken)).Outcome);
        Assert.Empty(provider.ActiveProfiles);
        provider.SourceName = "Employee";
        var decisions = await Task.WhenAll(
            administration.DecideAsync(provider, new(request.Id, request.Revision, checker), true, TestContext.Current.CancellationToken).AsTask(),
            administration.DecideAsync(provider, new(request.Id, request.Revision, checker), true, TestContext.Current.CancellationToken).AsTask());
        Assert.Single(decisions, decision => decision.Outcome == ProviderOutcome.Succeeded);
        Assert.Equal(["reviewers"], provider.ActiveProfiles);
        Assert.Equal(IdentityAdmissionStatus.Active, (await provider.GetAdmissionAsync(new("demo", "employee"), TestContext.Current.CancellationToken)).Value);
        Assert.Equal(2, provider.AuditCount);
    }

    private sealed class PermissionGate : IAuthorizationProvider
    {
        public bool Allowed { get; set; } = true;
        public ValueTask<AuthorizationDecision> AuthorizeAsync(AuthorizationRequest request, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new AuthorizationDecision(Allowed, "test.permission"));
    }

    /// <summary>In-memory contract double only: not a provider shipped in the default kernel.</summary>
    private sealed class DemonstrationProvider : IIdentityProvisioningProvider
    {
        private readonly object _gate = new();
        private IdentityProvisioningRequest? _request;
        public string ProviderId => "demo";
        public string SourceName { get; set; } = "Employee";
        public IReadOnlyList<string> ActiveProfiles { get; private set; } = [];
        public int AuditCount { get; private set; }
        public ValueTask<ProviderResult<DirectoryIdentity>> ResolveForImportAsync(string identifier, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new ProviderResult<DirectoryIdentity>(ProviderOutcome.Succeeded, new(new(ProviderId, identifier), SourceName,
                "employee@example.test", new HashSet<string>(StringComparer.Ordinal), DirectoryAccountStatus.Enabled)));
        public async ValueTask<ProviderResult<IdentityProvisioningRequest>> SubmitAsync(IdentityProvisioningSubmission submission, CancellationToken cancellationToken)
        {
            var found = await ResolveForImportAsync(submission.Identifier, cancellationToken);
            lock (_gate)
            {
                if (_request is not null) return new(ProviderOutcome.Conflict);
                _request = new("request", found.Value!, submission.ProfileIds.ToArray(), submission.Maker, IdentityAdmissionStatus.PendingApproval, 1);
                AuditCount++;
                return new(ProviderOutcome.Succeeded, _request);
            }
        }
        public ValueTask<ProviderResult<IdentityProvisioningRequest>> FindRequestAsync(string requestId, CancellationToken cancellationToken)
        { lock (_gate) return ValueTask.FromResult(new ProviderResult<IdentityProvisioningRequest>(_request is null ? ProviderOutcome.NotFound : ProviderOutcome.Succeeded, _request)); }
        public ValueTask<ProviderResult<IReadOnlyList<IdentityProvisioningRequest>>> ListPendingAsync(int maximumResults, CancellationToken cancellationToken)
        { lock (_gate) return ValueTask.FromResult(new ProviderResult<IReadOnlyList<IdentityProvisioningRequest>>(ProviderOutcome.Succeeded, _request is { Status: IdentityAdmissionStatus.PendingApproval } ? [_request] : [])); }
        public ValueTask<ProviderResult<IdentityAdmissionStatus>> GetAdmissionAsync(IdentityReference identity, CancellationToken cancellationToken)
        { lock (_gate) return ValueTask.FromResult(new ProviderResult<IdentityAdmissionStatus>(_request?.Identity.Identity == identity ? ProviderOutcome.Succeeded : ProviderOutcome.NotFound, _request?.Status ?? IdentityAdmissionStatus.PendingApproval)); }
        public ValueTask<ProviderResult<IdentityProvisioningRequest>> ApproveAsync(IdentityProvisioningDecision decision, CancellationToken cancellationToken) => Decide(decision, true);
        public ValueTask<ProviderResult<IdentityProvisioningRequest>> RejectAsync(IdentityProvisioningDecision decision, CancellationToken cancellationToken) => Decide(decision, false);
        private ValueTask<ProviderResult<IdentityProvisioningRequest>> Decide(IdentityProvisioningDecision decision, bool approve)
        {
            lock (_gate)
            {
                if (_request is null || _request.Id != decision.RequestId) return ValueTask.FromResult(new ProviderResult<IdentityProvisioningRequest>(ProviderOutcome.NotFound));
                if (_request.Maker == decision.Checker) return ValueTask.FromResult(new ProviderResult<IdentityProvisioningRequest>(ProviderOutcome.Forbidden));
                if (_request.Revision != decision.ExpectedRevision || _request.Status != IdentityAdmissionStatus.PendingApproval || _request.Identity.DisplayName != SourceName)
                    return ValueTask.FromResult(new ProviderResult<IdentityProvisioningRequest>(ProviderOutcome.Conflict));
                _request = _request with { Status = approve ? IdentityAdmissionStatus.Active : IdentityAdmissionStatus.Rejected, Revision = _request.Revision + 1, Checker = decision.Checker };
                ActiveProfiles = approve ? _request.ProfileIds : [];
                AuditCount++;
                return ValueTask.FromResult(new ProviderResult<IdentityProvisioningRequest>(ProviderOutcome.Succeeded, _request));
            }
        }
    }
}
