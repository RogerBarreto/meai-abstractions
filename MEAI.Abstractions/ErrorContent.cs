using Microsoft.Extensions.AI;

namespace MEAI.Abstractions;

public class ErrorContent : AIContent
{
    public required string Message { get; set; }
    public string? Code { get; set; }
    public string? Details { get; set; }
}
