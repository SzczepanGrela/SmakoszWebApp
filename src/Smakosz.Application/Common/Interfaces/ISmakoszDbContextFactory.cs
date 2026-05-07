namespace Smakosz.Application.Common.Interfaces;

public interface ISmakoszDbContextFactory
{
    Task<ISmakoszDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default);
}
