namespace EnterpriseWorkflow.Security;

/// <summary>Server-side permission and actor checks shared by UI and custom host endpoints.</summary>
public sealed class IdentityProvisioningAdministration(IAuthorizationProvider authorization)
{
    public async ValueTask<ProviderResult<IdentityProvisioningRequest>> SubmitAsync(IIdentityProvisioningProvider provider,
        IdentityProvisioningSubmission submission, CancellationToken cancellationToken)
    {
        var allowed = await authorization.AuthorizeAsync(new(submission.Maker, WorkflowPermissions.SubmitIdentity), cancellationToken).ConfigureAwait(false);
        if (!allowed.Allowed) return new(ProviderOutcome.Forbidden, ErrorCode: allowed.ReasonCode);
        if (string.IsNullOrWhiteSpace(submission.Identifier) || submission.Identifier.Length > 256 || submission.ProfileIds.Count is 0 or > 100 ||
            submission.ProfileIds.Any(id => string.IsNullOrWhiteSpace(id) || id.Length > 160) || submission.ProfileIds.Distinct(StringComparer.Ordinal).Count() != submission.ProfileIds.Count)
            throw new ArgumentException("Invalid identity provisioning submission.", nameof(submission));
        return await provider.SubmitAsync(submission, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<ProviderResult<IdentityProvisioningRequest>> DecideAsync(IIdentityProvisioningProvider provider,
        IdentityProvisioningDecision decision, bool approve, CancellationToken cancellationToken)
    {
        var allowed = await authorization.AuthorizeAsync(new(decision.Checker, WorkflowPermissions.ApproveIdentity), cancellationToken).ConfigureAwait(false);
        if (!allowed.Allowed) return new(ProviderOutcome.Forbidden, ErrorCode: allowed.ReasonCode);
        if (string.IsNullOrWhiteSpace(decision.RequestId) || decision.RequestId.Length > 256 || decision.ExpectedRevision < 0 || decision.Reason?.Length > 1024)
            throw new ArgumentException("Invalid identity provisioning decision.", nameof(decision));
        var found = await provider.FindRequestAsync(decision.RequestId, cancellationToken).ConfigureAwait(false);
        if (found.Outcome is not ProviderOutcome.Succeeded || found.Value is null) return found;
        if (found.Value.Maker == decision.Checker) return new(ProviderOutcome.Forbidden, ErrorCode: "security.provisioning-self-approval");
        if (found.Value.Revision != decision.ExpectedRevision || found.Value.Status is not IdentityAdmissionStatus.PendingApproval)
            return new(ProviderOutcome.Conflict, ErrorCode: "security.provisioning-stale-request");
        // The provider must repeat these invariants atomically with source verification and activation.
        return approve ? await provider.ApproveAsync(decision, cancellationToken).ConfigureAwait(false)
            : await provider.RejectAsync(decision, cancellationToken).ConfigureAwait(false);
    }
}
