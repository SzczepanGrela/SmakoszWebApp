namespace Smakosz.Application.Common.Interfaces;

public interface IPrimaryPhotoSyncer
{
    Task SyncToEntityAsync(long assetId, CancellationToken ct);
}
