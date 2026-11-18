namespace EnterpriseWorkflow.Core.Model;

/// <summary>A deliberately user-facing validation message. Field refers to a submitted form field, never implicitly to state.</summary>
public sealed record WorkflowValidationError
{
    public WorkflowValidationError(string Code, string Message, string? Field = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Code);
        ArgumentException.ThrowIfNullOrWhiteSpace(Message);
        if (Code.Length > 128 || Message.Length > 1024 || Field?.Length > 128) throw new ArgumentOutOfRangeException(nameof(Message));
        this.Code = Code; this.Message = Message; this.Field = Field;
    }
    public WorkflowValidationError(string code, EnterpriseWorkflow.Abstractions.WorkflowText message, string? field = null)
        : this(code, message.Fallback, field) => LocalizedMessage = message;
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public EnterpriseWorkflow.Abstractions.WorkflowText? LocalizedMessage { get; init; }
    public string Code { get; }
    public string Message { get; }
    public string? Field { get; }
}
