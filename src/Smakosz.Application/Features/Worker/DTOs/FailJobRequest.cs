namespace Smakosz.Application.Features.Worker.DTOs;

public class FailJobRequest
{
    public string ErrorMessage { get; set; } = string.Empty;
    public string? ErrorLog { get; set; }
    public bool Retryable { get; set; }
}
