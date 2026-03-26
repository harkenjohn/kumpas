using System.ComponentModel.DataAnnotations;

namespace Kumpas.AdminWeb.ViewModels;

public class ManageAccountsViewModel
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    public string? UserType { get; set; }
    public IReadOnlyList<AccountRowViewModel> Accounts { get; set; } = [];
}

public class AccountRowViewModel
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = "No email";
    public string UserType { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? LastSignInAt { get; set; }
}

public class UpdateUserPasswordViewModel
{
    [Required]
    public Guid UserId { get; set; }

    [Required, StringLength(100, MinimumLength = 8), DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Required, Compare(nameof(NewPassword)), DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;
}
