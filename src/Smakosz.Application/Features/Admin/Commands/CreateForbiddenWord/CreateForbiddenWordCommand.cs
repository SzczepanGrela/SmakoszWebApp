using System.Text.Json;
using System.Text.RegularExpressions;
using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.CreateForbiddenWord;

public record CreateForbiddenWordCommand(string Word, ForbiddenWordCategory Category, bool IsRegex)
    : IRequest<ErrorOr<int>>;

public class CreateForbiddenWordValidator : AbstractValidator<CreateForbiddenWordCommand>
{
    public CreateForbiddenWordValidator()
    {
        RuleFor(x => x.Word)
            .NotEmpty().WithMessage("Slowo jest wymagane")
            .MaximumLength(100).WithMessage("Slowo moze miec maksymalnie 100 znakow");
    }
}

public class CreateForbiddenWordHandler : IRequestHandler<CreateForbiddenWordCommand, ErrorOr<int>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IForbiddenWordService _forbiddenWords;

    public CreateForbiddenWordHandler(ISmakoszDbContext db, ICurrentUserService currentUser, IForbiddenWordService forbiddenWords)
    {
        _db = db;
        _currentUser = currentUser;
        _forbiddenWords = forbiddenWords;
    }

    public async Task<ErrorOr<int>> Handle(CreateForbiddenWordCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var exists = await _db.ForbiddenWords
            .AnyAsync(w => w.Word.ToLower() == request.Word.ToLower(), cancellationToken);

        if (exists)
            return DomainErrors.ForbiddenWord.AlreadyExists;

        if (request.IsRegex)
        {
            try { _ = new Regex(request.Word, RegexOptions.None, TimeSpan.FromSeconds(1)); }
            catch (RegexParseException) { return DomainErrors.ForbiddenWord.InvalidRegex; }
        }

        var word = new ForbiddenWord
        {
            Word = request.Word,
            Category = request.Category,
            IsRegex = request.IsRegex,
            AddedBy = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow
        };

        _db.ForbiddenWords.Add(word);
        await _db.SaveChangesAsync(cancellationToken);

        _db.AuditLogs.Add(new AuditLog
        {
            TableName = "forbidden_words",
            RecordId = word.WordId,
            Operation = AuditOperation.Insert,
            ChangedBy = _currentUser.UserId?.ToString() ?? "system",
            ChangedAt = DateTime.UtcNow,
            NewValues = JsonSerializer.Serialize(new { request.Word, Category = request.Category.ToString(), request.IsRegex })
        });
        await _db.SaveChangesAsync(cancellationToken);

        _forbiddenWords.InvalidateCache();

        return word.WordId;
    }
}
