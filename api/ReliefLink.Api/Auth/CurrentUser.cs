using System.Security.Claims;

namespace ReliefLink.Api.Auth;

public static class CurrentUser
{
    /// <summary>Returns the authenticated user's id, or null when the principal has no valid id claim.</summary>
    public static Guid? GetUserId(this ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
