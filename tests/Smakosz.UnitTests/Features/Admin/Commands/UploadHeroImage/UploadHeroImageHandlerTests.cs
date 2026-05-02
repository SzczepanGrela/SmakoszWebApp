using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Commands.UploadHeroImage;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Commands.UploadHeroImage;

[Trait("Category", "Handlers")]
public class UploadHeroImageHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly IFileStorageService _storage;
    private readonly IImageProcessingService _imageProcessor;
    private readonly IPublicConfigProvider _configProvider;

    public UploadHeroImageHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _storage = Substitute.For<IFileStorageService>();
        _imageProcessor = Substitute.For<IImageProcessingService>();
        _configProvider = Substitute.For<IPublicConfigProvider>();
        _configProvider.GetIntAsync("upload.hero_max_size_mb", 5, Arg.Any<CancellationToken>()).Returns(5);
        _imageProcessor.IdentifyDimensionsAsync(Arg.Any<Stream>()).Returns((2100, 900));
        _storage.UploadAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IReadOnlyList<ImageVariant>>(), Arg.Any<CancellationToken>())
            .Returns(new FileUploadResult("key", "https://cdn.smakosz.test/uploads/hero/abc.webp", null, null, null, "blurhash", 2100, 900));
    }

    private UploadHeroImageHandler CreateHandler(ICurrentUserService user)
        => new(_db, user, _storage, _imageProcessor, _configProvider, Substitute.For<IBusinessMetrics>());

    [Fact]
    public async Task Handle_AdminUpload_ApprovedAndReturnsResult()
    {
        var admin = MockExtensions.CreateAdminUser(userId: 99);
        var handler = CreateHandler(admin);
        using var stream = new MemoryStream(new byte[2 * 1024 * 1024]);

        var result = await handler.Handle(new UploadHeroImageCommand(stream, "hero.jpg", "Photo by Admin"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Url.Should().Contain("uploads/hero");
        _sets.MediaAssets.Should().ContainSingle(a =>
            a.EntityType == MediaEntityType.Hero &&
            a.ModerationStatus == ContentModerationStatus.Approved &&
            a.CreditText == "Photo by Admin");
    }

    [Fact]
    public async Task Handle_ModeratorUpload_Approved()
    {
        var moderator = MockExtensions.CreateAuthenticatedUser(userId: 7, role: "Moderator");
        var handler = CreateHandler(moderator);
        using var stream = new MemoryStream(new byte[1024]);

        var result = await handler.Handle(new UploadHeroImageCommand(stream, "hero.jpg", null), CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.MediaAssets.Should().ContainSingle(a => a.ModerationStatus == ContentModerationStatus.Approved);
    }

    [Fact]
    public async Task Handle_RegularUser_ReturnsForbidden()
    {
        var regular = MockExtensions.CreateAuthenticatedUser(userId: 5, role: "User");
        var handler = CreateHandler(regular);
        using var stream = new MemoryStream(new byte[1024]);

        var result = await handler.Handle(new UploadHeroImageCommand(stream, "hero.jpg", null), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }

    [Fact]
    public async Task Handle_WrongRatio_ReturnsValidationError()
    {
        var admin = MockExtensions.CreateAdminUser(userId: 99);
        var handler = CreateHandler(admin);
        _imageProcessor.IdentifyDimensionsAsync(Arg.Any<Stream>()).Returns((1024, 1024));
        using var stream = new MemoryStream(new byte[1024]);

        var result = await handler.Handle(new UploadHeroImageCommand(stream, "hero.jpg", null), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("MEDIA_WRONG_RATIO");
    }

    [Fact]
    public async Task Handle_OversizeFile_ReturnsValidationError()
    {
        var admin = MockExtensions.CreateAdminUser(userId: 99);
        var handler = CreateHandler(admin);
        using var stream = new MemoryStream(new byte[6 * 1024 * 1024]);

        var result = await handler.Handle(new UploadHeroImageCommand(stream, "hero.jpg", null), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("MEDIA_FILE_TOO_LARGE");
    }
}
