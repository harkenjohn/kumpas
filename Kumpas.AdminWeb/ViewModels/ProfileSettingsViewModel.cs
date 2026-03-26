using System.ComponentModel.DataAnnotations;

namespace Kumpas.AdminWeb.ViewModels;

public class ProfileSettingsViewModel
{
    public Guid UserId { get; set; }

    [Required, StringLength(150)]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(150)]
    public string LastName { get; set; } = string.Empty;

    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [StringLength(100, MinimumLength = 8), DataType(DataType.Password)]
    public string? NewPassword { get; set; }

    [Compare(nameof(NewPassword)), DataType(DataType.Password)]
    public string? ConfirmPassword { get; set; }
}
