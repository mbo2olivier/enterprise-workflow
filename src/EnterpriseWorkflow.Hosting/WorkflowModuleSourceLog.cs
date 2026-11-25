using Microsoft.Extensions.Logging;

namespace EnterpriseWorkflow.Hosting;

internal static partial class WorkflowModuleSourceLog
{
    [LoggerMessage(EventId = 7064, Level = LogLevel.Information, Message = "{Code}: {Message} Path: {Path}")]
    internal static partial void Report(ILogger logger, string code, string message, string path);
}
