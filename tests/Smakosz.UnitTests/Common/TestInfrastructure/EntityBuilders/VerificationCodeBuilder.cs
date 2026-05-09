using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;

namespace Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

public class VerificationCodeBuilder
{
    private readonly VerificationCode _code = new()
    {
        VerificationCodeId = 1,
        UserId = 1,
        CodeHash = "valid-code",
        Type = VerificationCodeType.Register,
        ExpiresAt = DateTime.UtcNow.AddHours(1),
        AttemptsCount = 0,
        CreatedAt = DateTime.UtcNow,
        User = null!
    };

    public VerificationCodeBuilder WithId(int id) { _code.VerificationCodeId = id; return this; }
    public VerificationCodeBuilder WithUserId(int userId) { _code.UserId = userId; return this; }
    public VerificationCodeBuilder WithUser(User user) { _code.User = user; _code.UserId = user.UserId; return this; }
    public VerificationCodeBuilder WithCode(string code) { _code.CodeHash = code; return this; }
    public VerificationCodeBuilder WithType(VerificationCodeType type) { _code.Type = type; return this; }
    public VerificationCodeBuilder AsExpired() { _code.ExpiresAt = DateTime.UtcNow.AddHours(-1); return this; }
    public VerificationCodeBuilder WithExpiresAt(DateTime expiresAt) { _code.ExpiresAt = expiresAt; return this; }
    public VerificationCodeBuilder WithPayload(string? payload) { _code.Payload = payload; return this; }

    public VerificationCode Build() => _code;
}
