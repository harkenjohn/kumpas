namespace Kumpas.AdminWeb.Services;

public interface ISupabaseAuthService
{
    Task<SupabaseLoginResult> SignInAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<SupabaseOperationResult> UpdatePasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken = default);
    Task<SupabaseOperationResult> DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default);
}

public sealed record SupabaseLoginResult(bool Succeeded, Guid? UserId = null, string? AccessToken = null, string? ErrorMessage = null);
public sealed record SupabaseOperationResult(bool Succeeded, string? ErrorMessage = null);
