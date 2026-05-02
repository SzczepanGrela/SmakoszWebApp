using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Me.Commands.UploadAvatar;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Me.Commands.UploadAvatar;

[Trait("Category", "Handlers")]
public class UploadAvatarHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _storage;
    private readonly IImageProcessingService _imageProcessor;
    private readonly IPublicConfigProvider _configProvider;
    private readonly UploadAvatarHandler _handler;

    public UploadAvatarHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1);
        _storage = Substitute.For<IFileStorageService>();
        _imageProcessor = Substitute.For<IImageProcessingService>();
        _configProvider = Substitute.For<IPublicConfigProvider>();
        _configProvider.GetIntAsync("upload.avatar_max_size_mb", 1, Arg.Any<CancellationToken>()).Returns(1);
        _imageProcessor.IdentifyDimensionsAsync(Arg.Any<Stream>()).Returns((512, 512));
        _storage.UploadAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IReadOnlyList<ImageVariant>>(), Arg.Any<CancellationToken>())
            .Returns(new FileUploadResult("key", "https://cdn.smakosz.test/uploads/user/abc.webp", null, "tinyUrl", null, "blurhash", 512, 512));
        _handler = new UploadAvatarHandler(_db, _currentUser, _storage, _imageProcessor, _configProvider, Substitute.For<IBusinessMetrics>());
    }

    [Fact]
    public async Task Handle_NewAvatar_NoPrevious_PersistsAndReturnsUrl()
    {
        _sets.Users.Add(new UserBuilder().WithId(1).Build());
        DbContextMockFactory.Refresh(_db, _sets);
        using var stream = new MemoryStream(new byte[1024]);

        var result = await _handler.Handle(new UploadAvatarCommand(stream, "avatar.jpg"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Url.Should().Contain("uploads/user");
        _sets.Users.Single().AvatarUrl.Should().Be(result.Value.Url);
        _sets.Users.Single().AvatarBlurhash.Should().Be("blurhash");
        _sets.MediaAssets.Should().ContainSingle(a => a.EntityType == MediaEntityType.User && a.EntityId == 1 && a.ModerationStatus == ContentModerationStatus.Approved);
        _sets.FilesToDelete.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ReplaceAvatar_EnqueuesOldR2KeyAndUpdatesUser()
    {
        _sets.Users.Add(new UserBuilder().WithId(1).WithAvatarUrl("https://cdn.smakosz.test/uploads/user/old.webp").Build());
        _sets.MediaAssets.Add(new Smakosz.Domain.Entities.MediaAsset
        {
            AssetId = 99,
            EntityType = MediaEntityType.User,
            EntityId = 1,
            Url = "https://cdn.smakosz.test/uploads/user/old.webp",
            ModerationStatus = ContentModerationStatus.Approved,
            UploadedBy = 1
        });
        DbContextMockFactory.Refresh(_db, _sets);
        using var stream = new MemoryStream(new byte[1024]);

        var result = await _handler.Handle(new UploadAvatarCommand(stream, "avatar.jpg"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.FilesToDelete.Should().ContainSingle(f => f.R2Key == "uploads/user/old.webp" && f.Reason == "avatar_replaced");
        _sets.Users.Single().AvatarUrl.Should().NotBe("https://cdn.smakosz.test/uploads/user/old.webp");
    }

    [Fact]
    public async Task Handle_OversizeFile_ReturnsValidationError()
    {
        _sets.Users.Add(new UserBuilder().WithId(1).Build());
        DbContextMockFactory.Refresh(_db, _sets);
        using var stream = new MemoryStream(new byte[2 * 1024 * 1024]);

        var result = await _handler.Handle(new UploadAvatarCommand(stream, "avatar.jpg"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("MEDIA_FILE_TOO_LARGE");
    }

    [Fact]
    public async Task Handle_WrongRatio_ReturnsValidationError()
    {
        _sets.Users.Add(new UserBuilder().WithId(1).Build());
        DbContextMockFactory.Refresh(_db, _sets);
        _imageProcessor.IdentifyDimensionsAsync(Arg.Any<Stream>()).Returns((1920, 1080));
        using var stream = new MemoryStream(new byte[1024]);

        var result = await _handler.Handle(new UploadAvatarCommand(stream, "avatar.jpg"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("MEDIA_WRONG_RATIO");
    }

    [Fact]
    public async Task Handle_UnsupportedFormat_ReturnsValidationError()
    {
        _sets.Users.Add(new UserBuilder().WithId(1).Build());
        DbContextMockFactory.Refresh(_db, _sets);
        using var stream = new MemoryStream(new byte[1024]);

        var result = await _handler.Handle(new UploadAvatarCommand(stream, "avatar.gif"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("MEDIA_INVALID_FORMAT");
    }

    [Fact]
    public async Task Handle_RegularUser_CreatesSystemTicketForPostModeration()
    {
        _sets.Users.Add(new UserBuilder().WithId(1).Build());
        DbContextMockFactory.Refresh(_db, _sets);
        using var stream = new MemoryStream(new byte[1024]);

        var result = await _handler.Handle(new UploadAvatarCommand(stream, "avatar.jpg"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.SystemTickets.Should().ContainSingle(t => t.TicketType == TicketType.Photo);
    }

    [Fact]
    public async Task Handle_AdminUploads_NoSystemTicketCreated()
    {
        var adminUser = MockExtensions.CreateAdminUser(userId: 1);
        var handler = new UploadAvatarHandler(_db, adminUser, _storage, _imageProcessor, _configProvider, Substitute.For<IBusinessMetrics>());
        _sets.Users.Add(new UserBuilder().WithId(1).WithRole(UserRole.Admin).Build());
        DbContextMockFactory.Refresh(_db, _sets);
        using var stream = new MemoryStream(new byte[1024]);

        var result = await handler.Handle(new UploadAvatarCommand(stream, "avatar.jpg"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.SystemTickets.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_NotAuthenticated_ReturnsUnauthorized()
    {
        var anonymous = MockExtensions.CreateAnonymousUser();
        var handler = new UploadAvatarHandler(_db, anonymous, _storage, _imageProcessor, _configProvider, Substitute.For<IBusinessMetrics>());
        using var stream = new MemoryStream(new byte[1024]);

        var result = await handler.Handle(new UploadAvatarCommand(stream, "avatar.jpg"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_CREDENTIALS");
    }
}
