namespace Htmx.Components.AuthStatus;

public class AuthStatusViewModel
{
    public bool IsAuthenticated { get; set; }
    public string? UserName { get; set; }
    public string? ProfileImageUrl { get; set; }
    public string? LoginUrl { get; set; }
}