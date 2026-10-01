using EnterpriseWorkflow.Sdk;

var result = WorkflowBuilder.Create("sample.leave-request", 1)
    .WithDisplayLabel("Leave request")
    .Start("start")
    .Service("submit", "leave.submit", configurationJson: "{\"form\":\"leave-request-v1\"}")
    .Decision("approval", "leave.evaluate-approval", ["approved", "rejected"])
    .Service("notify", "leave.notify")
    .End("approved-end", displayLabel: "Approved")
    .End("rejected-end", displayLabel: "Rejected")
    .Then("start", "submit")
    .Then("submit", "approval")
    .On("approval", "approved", "notify")
    .On("approval", "rejected", "rejected-end")
    .Then("notify", "approved-end")
    .Validate();

if (!result.IsPublishable)
{
    foreach (var diagnostic in result.Diagnostics)
    {
        Console.Error.WriteLine($"{diagnostic.Code} {diagnostic.Location}: {diagnostic.Message}");
    }

    return 1;
}

Console.WriteLine($"Definition: {result.Definition!.Id} v{result.Definition.Version}");
Console.WriteLine($"Nodes: {result.Definition.Nodes.Length}");
Console.WriteLine($"SHA-256: {result.Definition.Sha256}");
return 0;
