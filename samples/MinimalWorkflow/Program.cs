using EnterpriseWorkflow.Abstractions;
using EnterpriseWorkflow.Core;
using EnterpriseWorkflow.Sdk;

Console.WriteLine("Enterprise Workflow L1 reference topology:");
Console.WriteLine($"- {AbstractionsAssembly.Reference.GetName().Name}");
Console.WriteLine($"- {CoreAssembly.Reference.GetName().Name}");
Console.WriteLine($"- {SdkAssembly.Reference.GetName().Name}");
Console.WriteLine("Workflow authoring is introduced in L2.");

