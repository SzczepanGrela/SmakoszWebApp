namespace Smakosz.Application.Features.Worker.DTOs;

public class CompleteJobRequest
{
    public string Result { get; set; } = string.Empty;
    public int ProcessingTimeMs { get; set; }
}
