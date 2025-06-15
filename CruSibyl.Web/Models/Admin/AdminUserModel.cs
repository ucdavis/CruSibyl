namespace CruSibyl.Web.Models.Admin;

public class AdminUserModel
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Kerberos { get; set; } = "";
    public bool IsSystemAdmin { get; set; }
}