using Kumpas.AdminWeb.Data;
using Kumpas.AdminWeb.Models;
using Kumpas.AdminWeb.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Kumpas.AdminWeb.Services;

public class AccountService(KumpasDbContext dbContext)
{
    public async Task<ManageAccountsViewModel> GetAccountsAsync(string? search, string? status, string? userType, int pageNumber = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(pageNumber, 1);
        pageSize = 10;

        var query =
            from profile in dbContext.Profiles.AsNoTracking()
            join authUser in dbContext.AuthUsers.AsNoTracking() on profile.Id equals authUser.Id into authGroup
            from authUser in authGroup.DefaultIfEmpty()
            select new AccountRowViewModel
            {
                Id = profile.Id,
                FullName = ((profile.FirstName ?? string.Empty) + " " + (profile.LastName ?? string.Empty)).Trim(),
                Email = authUser != null && !string.IsNullOrWhiteSpace(authUser.Email) ? authUser.Email! : "No email",
                UserType = profile.UserType ?? string.Empty,
                IsActive = profile.IsActive,
                CreatedAt = profile.CreatedAt,
                LastSignInAt = null
            };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x =>
                x.FullName.ToLower().Contains(term) ||
                x.Email.ToLower().Contains(term) ||
                x.UserType.ToLower().Contains(term));
        }

        if (status is "active")
        {
            query = query.Where(x => x.IsActive);
        }
        else if (status is "inactive")
        {
            query = query.Where(x => !x.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(userType))
        {
            query = query.Where(x => x.UserType == userType);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        return new ManageAccountsViewModel
        {
            Search = search,
            Status = status,
            UserType = userType,
            Pagination = new PaginationViewModel
            {
                Action = "Index",
                Controller = "Accounts",
                ItemLabel = "records",
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount,
                RouteValues = new Dictionary<string, string>
                {
                    ["search"] = search ?? string.Empty,
                    ["status"] = status ?? string.Empty,
                    ["userType"] = userType ?? string.Empty
                }
            },
            Accounts = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken)
        };
    }

    public async Task<Profile?> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await dbContext.Profiles.Include(x => x.AuthUser).FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

    public async Task<AccountDetailsViewModel?> GetAccountDetailsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var account = await (
            from profile in dbContext.Profiles.AsNoTracking()
            join authUser in dbContext.AuthUsers.AsNoTracking() on profile.Id equals authUser.Id into authGroup
            from authUser in authGroup.DefaultIfEmpty()
            where profile.Id == userId
            select new AccountDetailsViewModel
            {
                Id = profile.Id,
                FirstName = profile.FirstName ?? string.Empty,
                LastName = profile.LastName ?? string.Empty,
                FullName = ((profile.FirstName ?? string.Empty) + " " + (profile.LastName ?? string.Empty)).Trim(),
                Email = authUser != null && !string.IsNullOrWhiteSpace(authUser.Email) ? authUser.Email! : "No email",
                UserType = profile.UserType ?? string.Empty,
                IsActive = profile.IsActive,
                CreatedAt = profile.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (account is null)
        {
            return null;
        }

        account.TotalConversations = await dbContext.ChatSessions
            .AsNoTracking()
            .CountAsync(x => x.User1Id == userId || x.User2Id == userId, cancellationToken);

        account.TotalMessages = await dbContext.ChatMessages
            .AsNoTracking()
            .CountAsync(x => x.SenderId == userId, cancellationToken);

        account.LastConversationAt = await dbContext.ChatSessions
            .AsNoTracking()
            .Where(x => x.User1Id == userId || x.User2Id == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        account.RecentConversations = await dbContext.ChatSessions
            .AsNoTracking()
            .Include(x => x.User1)
            .Include(x => x.User2)
            .Include(x => x.Messages)
            .Where(x => x.User1Id == userId || x.User2Id == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(5)
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

        return account;
    }

    public async Task<bool> SetActiveStatusAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default)
    {
        var updatedRows = await dbContext.Profiles
            .Where(x => x.Id == userId)
            .ExecuteUpdateAsync(
                updates => updates.SetProperty(profile => profile.IsActive, isActive),
                cancellationToken);

        return updatedRows > 0;
    }

    public async Task<bool> UpdateProfileAsync(ProfileSettingsViewModel model, CancellationToken cancellationToken = default)
    {
        var profile = await dbContext.Profiles.FirstOrDefaultAsync(x => x.Id == model.UserId, cancellationToken);
        if (profile is null)
        {
            return false;
        }

        profile.FirstName = model.FirstName.Trim();
        profile.LastName = model.LastName.Trim();
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await dbContext.Profiles.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (profile is null)
        {
            return false;
        }

        dbContext.Profiles.Remove(profile);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
