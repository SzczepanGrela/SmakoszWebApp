using System.Text.Json;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.DeleteForbiddenWord;

public record DeleteForbiddenWordCommand(int WordId) : IRequest<ErrorOr<Success>>;

public class DeleteForbiddenWordValidator : AbstractValidator<DeleteForbiddenWordCommand>
{
    public DeleteForbiddenWordValidator()
    {
        RuleFor(x => x.WordId).GreaterThan(0).WithMessage("Nieprawidłowe ID");
    }
}

public class DeleteForbiddenWordHandler : IRequestHandler<DeleteForbiddenWordCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IForbiddenWordService _forbiddenWords;

    public DeleteForbiddenWordHandler(ISmakoszDbContext db, ICurrentUserService currentUser, IForbiddenWordService forbiddenWords)
    {
        _db = db;
        _currentUser = currentUser;
        _forbiddenWords = forbiddenWords;
    }

    public async Task<ErrorOr<Success>> Handle(DeleteForbiddenWordCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var word = await _db.ForbiddenWords
            .FirstOrDefaultAsync(w => w.WordId == request.WordId, cancellationToken);

        if (word is null)
            return DomainErrors.ForbiddenWord.NotFound;

        _db.AuditLogs.Add(new AuditLog
        {
            TableName = "forbidden_words",
            RecordId = word.WordId,
            Operation = AuditOperation.Delete,
            ChangedBy = _currentUser.UserId?.ToString() ?? "system",
            ChangedAt = DateTime.UtcNow,
            OldValues = JsonSerializer.Serialize(new { word.Word, Category = word.Category.ToString(), word.IsRegex })
        });

        _db.ForbiddenWords.Remove(word);
        await _db.SaveChangesAsync(cancellationToken);

        _forbiddenWords.InvalidateCache();

        return Result.Success;
    }
}
