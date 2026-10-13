namespace ReliefLink.Api.Auth;

/// <summary>Role names as strings, for use in [Authorize(Roles = ...)].</summary>
public static class Roles
{
    public const string Citizen = "Citizen";
    public const string Volunteer = "Volunteer";
    public const string Coordinator = "Coordinator";
    public const string Admin = "Admin";

    /// <summary>Staff who manage relief operations.</summary>
    public const string Staff = Coordinator + "," + Admin;
}
