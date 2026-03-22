using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;

namespace Smakosz.Infrastructure.Services;

public class VerificationCodeService : IVerificationCodeService
{
    private readonly ISmakoszDbContext _db;
    private readonly ICodeHasher _codeHasher;

    public VerificationCodeService(ISmakoszDbContext db, ICodeHasher codeHasher)
    {
        _db = db;
        _codeHasher = codeHasher;
    }

    public async Task<string> CreateCodeAsync(int userId, VerificationCodeType type, CancellationToken ct)
    {
        var ttl = await GetTtlMinutesAsync(ct);
        var code = Random.Shared.Next(100000, 999999).ToString();

        _db.VerificationCodes.Add(new VerificationCode
        {
            UserId = userId,
            CodeHash = _codeHasher.Hash(code),
            Type = type,
            ExpiresAt = DateTime.UtcNow.AddMinutes(ttl)
        });

        await _db.SaveChangesAsync(ct);
        return code;
    }

    private async Task<int> GetTtlMinutesAsync(CancellationToken ct)
    {
        var config = await _db.SystemConfigs
            .FirstOrDefaultAsync(c => c.Key == "auth.verify_code_ttl_min", ct);

        if (config is not null && int.TryParse(config.Value, out var value) && value > 0)
            return value;

        return 15;
    }
}
