using System.ComponentModel.DataAnnotations;

namespace Kumpas.AdminWeb.ViewModels;

public class ProfileSettingsViewModel
{
    public Guid UserId { get; set; }

    [Display(Name = "First Name")]
    [Required, StringLength(150)]
    public string FirstName { get; set; } = string.Empty;

    [Display(Name = "Last Name")]
    [Required, StringLength(150)]
    public string LastName { get; set; } = string.Empty;

    [Display(Name = "Email")]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "New Password")]
    [StringLength(100, MinimumLength = 8), DataType(DataType.Password)]
    public string? NewPassword { get; set; }

    [Display(Name = "Confirm Password")]
    [Compare(nameof(NewPassword)), DataType(DataType.Password)]
    public string? ConfirmPassword { get; set; }
}
