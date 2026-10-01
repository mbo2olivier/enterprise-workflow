using System.Reflection;

namespace EnterpriseWorkflow.Core;

/// <summary>
/// Provides a stable reference to the assembly that will contain the canonical workflow model.
/// </summary>
public static class CoreAssembly
{
    /// <summary>
    /// Gets the core assembly.
    /// </summary>
    public static Assembly Reference { get; } = typeof(CoreAssembly).Assembly;
}

