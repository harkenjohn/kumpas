using Kumpas.AdminWeb.Data;
using Kumpas.AdminWeb.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Kumpas.AdminWeb.Services;

public class ConversationService(KumpasDbContext dbContext)
{
    public async Task<ConversationHistoryViewModel> GetSessionsAsync(string? search, DateTime? fromDate, DateTime? toDate, int pageNumber = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(pageNumber, 1);
        pageSize = 10;

        var query = dbContext.ChatSessions
            .AsNoTracking()
            .Include(x => x.User1)
            .Include(x => x.User2)
            .Include(x => x.Messages)
            .AsQueryable();

        if (fromDate.HasValue)
        {
            var from = new DateTimeOffset(DateTime.SpecifyKind(fromDate.Value.Date, DateTimeKind.Utc));
            query = query.Where(x => x.CreatedAt >= from);
        }

        if (toDate.HasValue)
        {
            var to = new DateTimeOffset(DateTime.SpecifyKind(toDate.Value.Date.AddDays(1), DateTimeKind.Utc));
            query = query.Where(x => x.CreatedAt <= to);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x =>
                (x.RoomCode ?? string.Empty).ToLower().Contains(term) ||
                ((((x.User1!.FirstName ?? string.Empty) + " " + (x.User1.LastName ?? string.Empty)).Trim()).ToLower().Contains(term)) ||
                ((((x.User2!.FirstName ?? string.Empty) + " " + (x.User2.LastName ?? string.Empty)).Trim()).ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var sessions = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ConversationSessionRowViewModel
            {
                Id = x.Id,
                RoomCode = x.RoomCode ?? "N/A",
                ParticipantOne = ((x.User1!.FirstName ?? string.Empty) + " " + (x.User1.LastName ?? string.Empty)).Trim(),
                ParticipantTwo = ((x.User2!.FirstName ?? string.Empty) + " " + (x.User2.LastName ?? string.Empty)).Trim(),
                MessageCount = x.Messages.Count,
                CreatedAt = x.CreatedAt,
                LastMessageAt = x.Messages.OrderByDescending(m => m.CreatedAt).Select(m => m.CreatedAt).FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return new ConversationHistoryViewModel
        {
            Search = search,
            FromDate = fromDate,
            ToDate = toDate,
            Pagination = new PaginationViewModel
            {
                Action = "Index",
                Controller = "Conversations",
                ItemLabel = "sessions",
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount,
                RouteValues = new Dictionary<string, string>
                {
                    ["search"] = search ?? string.Empty,
                    ["fromDate"] = fromDate?.ToString("yyyy-MM-dd") ?? string.Empty,
                    ["toDate"] = toDate?.ToString("yyyy-MM-dd") ?? string.Empty
                }
            },
            Sessions = sessions
        };
    }

    public async Task<ConversationDetailsViewModel?> GetSessionDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var session = await dbContext.ChatSessions
            .AsNoTracking()
            .Include(x => x.User1)
            .Include(x => x.User2)
            .Include(x => x.Messages)
                .ThenInclude(x => x.Sender)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (session is null)
        {
            return null;
        }

        return new ConversationDetailsViewModel
        {
            Id = session.Id,
            RoomCode = session.RoomCode ?? "N/A",
            ParticipantOne = ((session.User1!.FirstName ?? string.Empty) + " " + (session.User1.LastName ?? string.Empty)).Trim(),
            ParticipantTwo = ((session.User2!.FirstName ?? string.Empty) + " " + (session.User2.LastName ?? string.Empty)).Trim(),
            CreatedAt = session.CreatedAt,
            Messages = session.Messages
                .OrderBy(x => x.CreatedAt)
                .Select(x => new ConversationMessageRowViewModel
                {
                    Id = x.Id,
                    SenderName = ((x.Sender!.FirstName ?? string.Empty) + " " + (x.Sender.LastName ?? string.Empty)).Trim(),
                    MessageType = x.GestureId.HasValue ? "Gesture" : "Text",
                    MessageContent = x.MessageContent ?? string.Empty,
                    CreatedAt = x.CreatedAt
                })
                .ToList()
        };
    }

    public async Task<bool> DeleteSessionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var session = await dbContext.ChatSessions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (session is null)
        {
            return false;
        }

        var messages = await dbContext.ChatMessages.Where(x => x.SessionId == id).ToListAsync(cancellationToken);
        dbContext.ChatMessages.RemoveRange(messages);
        dbContext.ChatSessions.Remove(session);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
