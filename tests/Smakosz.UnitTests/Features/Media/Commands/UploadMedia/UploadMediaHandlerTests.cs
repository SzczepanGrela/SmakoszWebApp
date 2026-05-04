using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Media.Commands.UploadMedia;
using Smakosz.Domain.Entities;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Media.Commands.UploadMedia;

[Trait("Category", "Handlers")]
public class UploadMediaHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _storage;
    private readonly IPublicConfigProvider _configProvider;
    private readonly UploadMediaHandler _handler;

    private static readonly byte[] JpegHeader = { 0xFF, 0xD8, 0xFF, 0xE0 };

    public UploadMediaHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _sets.Dishes.Add(new Dish { DishId = 1, DishName = "Test" });
        _sets.Restaurants.Add(new Restaurant { RestaurantId = 1 });
        DbContextMockFactory.Refresh(_db, _sets);

        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1);
        _storage = Substitute.For<IFileStorageService>();
        _configProvider = Substitute.For<IPublicConfigProvider>();
        _configProvider.GetIntAsync("upload.max_size_mb", 5, Arg.Any<CancellationToken>()).Returns(5);
        _configProvider.GetValueAsync("upload.allowed_types", Arg.Any<CancellationToken>()).Returns(".jpg,.jpeg,.png,.webp");
        _storage.UploadAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IReadOnlyList<ImageVariant>>(), Arg.Any<CancellationToken>())
            .Returns(new FileUploadResult("uploads/dish/abc.webp", "http://img.jpg", null, null, null, "blurhash", 800, 600));
        _handler = new UploadMediaHandler(_db, _currentUser, _storage, _configProvider, Substitute.For<IBusinessMetrics>(), NullLogger<UploadMediaHandler>.Instance);
    }

    private static MemoryStream MakeJpegStream(int totalSize)
    {
        var buffer = new byte[totalSize];
        Array.Copy(JpegHeader, buffer, JpegHeader.Length);
        return new MemoryStream(buffer);
    }

    [Fact]
    public async Task Handle_ValidUpload_ReturnsAssetInfo()
    {
        using var stream = MakeJpegStream(2048);
        var command = new UploadMediaCommand(stream, "photo.jpg", "Dish", 1);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Url.Should().Be("http://img.jpg");
        _sets.MediaAssets.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_InvalidExtension_ReturnsError()
    {
        using var stream = MakeJpegStream(2048);
        var command = new UploadMediaCommand(stream, "file.exe", "Dish", 1);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("MEDIA_INVALID_FORMAT");
    }

    [Fact]
    public async Task Handle_FileTooLarge_ReturnsError()
    {
        using var stream = MakeJpegStream(6 * 1024 * 1024);
        var command = new UploadMediaCommand(stream, "photo.jpg", "Dish", 1);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("MEDIA_FILE_TOO_LARGE");
    }

    [Fact]
    public async Task Handle_FileTooSmall_ReturnsError()
    {
        using var stream = MakeJpegStream(36);
        var command = new UploadMediaCommand(stream, "photo.jpg", "Dish", 1);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("MEDIA_FILE_TOO_SMALL");
    }

    [Fact]
    public async Task Handle_MagicBytesMismatch_ReturnsInvalidFormat()
    {
        using var stream = new MemoryStream(new byte[2048]);
        var command = new UploadMediaCommand(stream, "photo.jpg", "Dish", 1);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("MEDIA_INVALID_FORMAT");
    }

    [Fact]
    public async Task Handle_NonExistentEntity_ReturnsEntityNotFound()
    {
        using var stream = MakeJpegStream(2048);
        var command = new UploadMediaCommand(stream, "photo.jpg", "Dish", 999);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("MEDIA_ENTITY_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_MissingEntityId_ReturnsEntityNotFound()
    {
        using var stream = MakeJpegStream(2048);
        var command = new UploadMediaCommand(stream, "photo.jpg", "Dish", null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("MEDIA_ENTITY_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_NotAuthenticated_ReturnsError()
    {
        var anonymous = MockExtensions.CreateAnonymousUser();
        var handler = new UploadMediaHandler(_db, anonymous, _storage, _configProvider, Substitute.For<IBusinessMetrics>(), NullLogger<UploadMediaHandler>.Instance);

        using var stream = MakeJpegStream(2048);
        var command = new UploadMediaCommand(stream, "photo.jpg", "Dish", 1);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Handle_EntityTypeUser_ReturnsUseDedicatedEndpoint()
    {
        using var stream = MakeJpegStream(2048);
        var command = new UploadMediaCommand(stream, "photo.jpg", "User", 1);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("MEDIA_USE_DEDICATED_ENDPOINT");
    }

    [Fact]
    public async Task Handle_EntityTypeHero_ReturnsUseDedicatedEndpoint()
    {
        using var stream = MakeJpegStream(2048);
        var command = new UploadMediaCommand(stream, "photo.jpg", "Hero", null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("MEDIA_USE_DEDICATED_ENDPOINT");
    }
}
