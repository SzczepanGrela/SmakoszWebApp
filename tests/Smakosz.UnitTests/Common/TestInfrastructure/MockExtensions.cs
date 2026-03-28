using NSubstitute;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.UnitTests.Common.TestInfrastructure;

public static class MockExtensions
{
    public static ICurrentUserService CreateAuthenticatedUser(int userId = 1, string role = "User", long sessionId = 0)
    {
        var service = Substitute.For<ICurrentUserService>();
        service.UserId.Returns(userId);
        service.SessionId.Returns(sessionId > 0 ? sessionId : (long?)null);
        service.Role.Returns(role);
        service.IsAdmin.Returns(role == "Admin");
        service.IsAdminOrModerator.Returns(role is "Admin" or "Moderator");
        service.IsAuthenticated.Returns(true);
        return service;
    }

    public static ICurrentUserService CreateAdminUser(int userId = 99, long sessionId = 0)
    {
        var service = Substitute.For<ICurrentUserService>();
        service.UserId.Returns(userId);
        service.SessionId.Returns(sessionId > 0 ? sessionId : (long?)null);
        service.Role.Returns("Admin");
        service.IsAdmin.Returns(true);
        service.IsAdminOrModerator.Returns(true);
        service.IsAuthenticated.Returns(true);
        return service;
    }

    public static ICurrentUserService CreateAnonymousUser()
    {
        var service = Substitute.For<ICurrentUserService>();
        service.UserId.Returns((int?)null);
        service.Role.Returns((string?)null);
        service.IsAdmin.Returns(false);
        service.IsAdminOrModerator.Returns(false);
        service.IsAuthenticated.Returns(false);
        return service;
    }
}
