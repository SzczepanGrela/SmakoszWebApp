using ErrorOr;
using MediatR;
using Smakosz.Application.Features.Admin.Dtos;

namespace Smakosz.Application.Features.Admin.Queries.GetTicketsSummary;

public record GetTicketsSummaryQuery : IRequest<ErrorOr<List<TicketSummaryDto>>>;
