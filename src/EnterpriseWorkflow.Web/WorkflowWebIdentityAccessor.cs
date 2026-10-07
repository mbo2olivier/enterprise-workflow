using EnterpriseWorkflow.Security;
using Microsoft.AspNetCore.Components.Authorization;

namespace EnterpriseWorkflow.Web;

public sealed class WorkflowWebIdentityAccessor(AuthenticationStateProvider authenticationStateProvider)
{
    public async ValueTask<IdentityReference> GetRequiredAsync()
    {
        var user = (await authenticationStateProvider.GetAuthenticationStateAsync().ConfigureAwait(false)).User;
        var providerId = user.FindFirst(WorkflowSessionAuthenticationHandler.ProviderClaim)?.Value;
        var subjectId = user.FindFirst(WorkflowSessionAuthenticationHandler.SubjectClaim)?.Value;
        if (providerId is null || subjectId is null)
            throw new InvalidOperationException("An authenticated workflow identity is required.");
        return new(providerId, subjectId);
    }
}
