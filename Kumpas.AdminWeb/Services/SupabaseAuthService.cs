using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Kumpas.AdminWeb.Services;

public class SupabaseAuthService(HttpClient httpClient, IOptions<SupabaseOptions> options) : ISupabaseAuthService
{
    private readonly SupabaseOptions _options = options.Value;

    public async Task<SupabaseLoginResult> SignInAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Url) || string.IsNullOrWhiteSpace(_options.AnonKey))
        {
            return new SupabaseLoginResult(false, ErrorMessage: "Supabase login is not configured.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.Url.TrimEnd('/')}/auth/v1/token?grant_type=password");
        request.Headers.Add("apikey", _options.AnonKey);
        request.Content = JsonContent.Create(new { email, password });

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new SupabaseLoginResult(false, ErrorMessage: "Invalid credentials.");
        }

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(contentStream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("user", out var userNode) ||
            !userNode.TryGetProperty("id", out var idNode) ||
            !Guid.TryParse(idNode.GetString(), out var userId))
        {
            return new SupabaseLoginResult(false, ErrorMessage: "Supabase user payload is invalid.");
        }

        var accessToken = document.RootElement.TryGetProperty("access_token", out var tokenNode)
            ? tokenNode.GetString()
            : null;

        return new SupabaseLoginResult(true, userId, accessToken);
    }

    public async Task<SupabaseOperationResult> UpdatePasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken = default)
    {
        if (!HasServiceRole())
        {
            return new SupabaseOperationResult(false, "Supabase service role key is missing.");
        }

        using var request = CreateAdminRequest(HttpMethod.Put, userId);
        request.Content = JsonContent.Create(new { password = newPassword });

        using var response = await httpClient.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode
            ? new SupabaseOperationResult(true)
            : new SupabaseOperationResult(false, "Password update failed.");
    }

    public async Task<SupabaseOperationResult> DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (!HasServiceRole())
        {
            return new SupabaseOperationResult(false, "Supabase service role key is missing.");
        }

        using var request = CreateAdminRequest(HttpMethod.Delete, userId);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        return response.IsSuccessStatusCode
            ? new SupabaseOperationResult(true)
            : new SupabaseOperationResult(false, "User deletion failed.");
    }

    private bool HasServiceRole() =>
        !string.IsNullOrWhiteSpace(_options.Url) && !string.IsNullOrWhiteSpace(_options.ServiceRoleKey);

    private HttpRequestMessage CreateAdminRequest(HttpMethod method, Guid userId)
    {
        var request = new HttpRequestMessage(method, $"{_options.Url.TrimEnd('/')}/auth/v1/admin/users/{userId}");
        request.Headers.Add("apikey", _options.ServiceRoleKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ServiceRoleKey);
        return request;
    }
}
