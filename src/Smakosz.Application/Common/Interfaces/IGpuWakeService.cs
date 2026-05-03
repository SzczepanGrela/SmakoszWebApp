namespace Smakosz.Application.Common.Interfaces;

public enum GpuWakeStatus
{
    Sent,
    AlreadyOnline,
    RateLimited,
    GatewayFailed,
    GpuNodeNotFound
}

public record GpuWakeResult(GpuWakeStatus Status, string? Message = null);

public interface IGpuWakeService
{
    Task<GpuWakeResult> WakeAsync(CancellationToken ct);
}
