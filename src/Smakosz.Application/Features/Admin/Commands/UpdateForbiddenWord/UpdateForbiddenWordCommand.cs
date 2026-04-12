using System.Text.Json;
using System.Text.RegularExpressions;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.UpdateForbiddenWord;

public record UpdateForbiddenWordCommand(int WordId, string? Word, ForbiddenWordCategory? Category, bool? IsRegex)
    : IRequest<ErrorOr<Success>>;

public class UpdateForbiddenWordHandler : IRequestHandler<UpdateForbiddenWordCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IForbiddenWordService _forbiddenWords;

    public UpdateForbiddenWordHandler(ISmakoszDbContext db, ICurrentUserService currentUser, IForbiddenWordService forbiddenWords)
    {
        _db = db;
        _currentUser = currentUser;
        _forbiddenWords = forbiddenWords;
    }

    public async Task<ErrorOr<Success>> Handle(UpdateForbiddenWordCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var word = await _db.ForbiddenWords
            .FirstOrDefaultAsync(w => w.WordId == request.WordId, cancellationToken);

        if (word is null)
            return DomainErrors.ForbiddenWord.NotFound;

        if (request.Word is not null && request.Word.ToLower() != word.Word.ToLower())
        {
            var duplicate = await _db.ForbiddenWords
                .AnyAsync(w => w.Word.ToLower() == request.Word.ToLower() && w.WordId != request.WordId, cancellationToken);

            if (duplicate)
                return DomainErrors.ForbiddenWord.AlreadyExists;
        }

        var isRegex = request.IsRegex ?? word.IsRegex;
        var newWord = request.Word ?? word.Word;

        if (isRegex && request.Word is not null)
        {
            try { _ = new Regex(newWord, RegexOptions.None, TimeSpan.FromSeconds(1)); }
            catch (RegexParseException) { return DomainErrors.ForbiddenWord.InvalidRegex; }
        }

        var oldValues = JsonSerializer.Serialize(new { word.Word, Category = word.Category.ToString(), word.IsRegex });

        if (request.Word is not null) word.Word = request.Word;
        if (request.Category.HasValue) word.Category = request.Category.Value;
        if (request.IsRegex.HasValue) word.IsRegex = request.IsRegex.Value;

        _db.AuditLogs.Add(new AuditLog
        {
            TableName = "forbidden_words",
            RecordId = word.WordId,
            Operation = AuditOperation.Update,
            ChangedBy = _currentUser.UserId?.ToString() ?? "system",
            ChangedAt = DateTime.UtcNow,
            OldValues = oldValues,
            NewValues = JsonSerializer.Serialize(new { word.Word, Category = word.Category.ToString(), word.IsRegex })
        });

        await _db.SaveChangesAsync(cancellationToken);

        _forbiddenWords.InvalidateCache();

        return Result.Success;
    }
}
