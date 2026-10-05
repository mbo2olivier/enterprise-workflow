using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EnterpriseWorkflow.Extensions;

/// <summary>Maps immutable, manifest-authorized module resources; it never exposes a directory provider.</summary>
public static class WorkflowModuleResourceEndpointRouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapEnterpriseWorkflowModuleResources(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        return endpoints.MapGet("/modules/{moduleId}/{version}/{artifactSha256}/assets/{**path}",
            (HttpContext context, WorkflowModuleCatalog catalog, string moduleId, string version,
                string artifactSha256, string path) =>
            {
                if (!catalog.TryOpenResource(moduleId, version, artifactSha256, path, out var resource, out var content))
                    return Results.NotFound();
                context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
                context.Response.Headers.ETag = $"\"sha256-{resource!.Sha256}\"";
                return Results.Stream(content!, resource.ContentType, enableRangeProcessing: false);
            });
    }
}
