using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Application.Common.Helpers;

public static class ModerationPolicyHelper
{
    public static bool IsAutoApproved(ICurrentUserService user)
        => user.Role is "Admin" or "Moderator";
}
