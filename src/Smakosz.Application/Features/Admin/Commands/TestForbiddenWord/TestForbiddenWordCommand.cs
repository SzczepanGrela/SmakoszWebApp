using ErrorOr;
using MediatR;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.TestForbiddenWord;

public record TestForbiddenWordCommand(string Text, ForbiddenWordCategory[] Categories)
    : IRequest<ErrorOr<bool>>;

public class TestForbiddenWordHandler : IRequestHandler<TestForbiddenWordCommand, ErrorOr<bool>>
{
    private readonly ICurrentUserService _currentUser;
    private readonly IForbiddenWordService _forbiddenWords;

    public TestForbiddenWordHandler(ICurrentUserService currentUser, IForbiddenWordService forbiddenWords)
    {
        _currentUser = currentUser;
        _forbiddenWords = forbiddenWords;
    }

    public async Task<ErrorOr<bool>> Handle(TestForbiddenWordCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var blocked = await _forbiddenWords.ContainsAsync(request.Text, cancellationToken, request.Categories);
        return blocked;
    }
}
