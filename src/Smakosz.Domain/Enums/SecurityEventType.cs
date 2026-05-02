namespace Smakosz.Domain.Enums;

public enum SecurityEventType
{
    FailedLogin,
    BlockedIp,
    SuspiciousActivity,
    PasswordReset,
    BannedRegistration,
    PasswordChanged,
    AccountDeleted,
    TwoFactorEnabled,
    TwoFactorDisabled,
    TwoFactorDisabledByAdmin,
    PasswordResetByAdmin,
    AccountInvited,
    RoleChanged
}
