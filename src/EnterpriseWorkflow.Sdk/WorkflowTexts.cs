using System.Reflection;
using System.Runtime.CompilerServices;
using EnterpriseWorkflow.Abstractions;

namespace EnterpriseWorkflow.Sdk;

/// <summary>Conventional embedded catalogs, scoped to the exact assembly rather than a global module name.</summary>
public static class WorkflowTexts
{
    /// <summary>Call directly from module code. Shared helper libraries should use For&lt;TModule&gt; to name the owner explicitly.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static WorkflowText Text(string key, string fallback) =>
        WorkflowTextCatalog.FromAssembly(Assembly.GetCallingAssembly()).Text(key, fallback);

    public static WorkflowTextCatalog For<TModule>() => WorkflowTextCatalog.FromAssembly(typeof(TModule).Assembly);
}
