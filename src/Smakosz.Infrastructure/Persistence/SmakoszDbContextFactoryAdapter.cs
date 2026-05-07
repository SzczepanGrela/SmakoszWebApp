using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Infrastructure.Persistence;

public class SmakoszDbContextFactoryAdapter : ISmakoszDbContextFactory
{
    private readonly IDbContextFactory<SmakoszDbContext> _inner;

    public SmakoszDbContextFactoryAdapter(IDbContextFactory<SmakoszDbContext> inner)
    {
        _inner = inner;
    }

    public async Task<ISmakoszDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        return await _inner.CreateDbContextAsync(cancellationToken);
    }
}
