using System.Reflection;

namespace EnterpriseWorkflow.Abstractions;

/// <summary>
/// Provides a stable reference to the assembly that contains the pure workflow contracts.
/// </summary>
public static class AbstractionsAssembly
{
    /// <summary>
    /// Gets the contracts assembly.
    /// </summary>
    public static Assembly Reference { get; } = typeof(AbstractionsAssembly).Assembly;
}

