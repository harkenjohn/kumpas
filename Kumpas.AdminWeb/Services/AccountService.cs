using Kumpas.AdminWeb.Data;
using Kumpas.AdminWeb.Models;
using Kumpas.AdminWeb.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Kumpas.AdminWeb.Services;

public class AccountService(KumpasDbContext dbContext)
{
    public async Task<ManageAccountsViewModel> GetAccountsAsync(string? search, string? status, string? userType, CancellationToken cancellationToken = default)
    {
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

        return new ManageAccountsViewModel
        {
            Search = search,
            Status = status,
            UserType = userType,
            Accounts = await query.OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken)
        };
    }

    public async Task<Profile?> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await dbContext.Profiles.Include(x => x.AuthUser).FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

    public async Task<bool> SetActiveStatusAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default)
    {
        var profile = await dbContext.Profiles.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (profile is null)
        {
            return false;
        }

        profile.IsActive = isActive;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
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
