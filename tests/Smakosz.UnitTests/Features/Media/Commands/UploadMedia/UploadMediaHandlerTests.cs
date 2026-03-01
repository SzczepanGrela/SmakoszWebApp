using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Media.Commands.UploadMedia;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Media.Commands.UploadMedia;

[Trait("Category", "Handlers")]
public class UploadMediaHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _storage;
    private readonly UploadMediaHandler _handler;

    public UploadMediaHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1);
        _storage = Substitute.For<IFileStorageService>();
        _storage.UploadAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IReadOnlyList<ImageVariant>>(), Arg.Any<CancellationToken>())
            .Returns(new FileUploadResult("key", "http://img.jpg", null, null, null, "blurhash", 800, 600));
        _handler = new UploadMediaHandler(_db, _currentUser, _storage);
    }

    [Fact]
    public async Task Handle_ValidUpload_ReturnsAssetInfo()
    {
        using var stream = new MemoryStream(new byte[1024]);
        var command = new UploadMediaCommand(stream, "photo.jpg", "Dish", 1);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Url.Should().Be("http://img.jpg");
        _sets.MediaAssets.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_InvalidExtension_ReturnsError()
    {
        using var stream = new MemoryStream(new byte[1024]);
        var command = new UploadMediaCommand(stream, "file.exe", "Dish", 1);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("MEDIA_INVALID_FORMAT");
    }

    [Fact]
    public async Task Handle_FileTooLarge_ReturnsError()
    {
        using var stream = new MemoryStream(new byte[6 * 1024 * 1024]);
        var command = new UploadMediaCommand(stream, "photo.jpg", "Dish", 1);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("MEDIA_FILE_TOO_LARGE");
    }

    [Fact]
    public async Task Handle_NotAuthenticated_ReturnsError()
    {
        var anonymous = MockExtensions.CreateAnonymousUser();
        var handler = new UploadMediaHandler(_db, anonymous, _storage);

        using var stream = new MemoryStream(new byte[1024]);
        var command = new UploadMediaCommand(stream, "photo.jpg", "Dish", 1);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_CREDENTIALS");
    }
}
