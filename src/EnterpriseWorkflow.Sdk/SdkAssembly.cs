using System.Reflection;

namespace EnterpriseWorkflow.Sdk;

/// <summary>
/// Provides a stable reference to the C# authoring SDK assembly.
/// </summary>
public static class SdkAssembly
{
    /// <summary>
    /// Gets the SDK assembly.
    /// </summary>
    public static Assembly Reference { get; } = typeof(SdkAssembly).Assembly;
}

