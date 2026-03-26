namespace Kumpas.AdminWeb.Models;

public class AuthUser
{
    public Guid Id { get; set; }
    public string? Email { get; set; }
    public string? EncryptedPassword { get; set; }

    public Profile? Profile { get; set; }
}
