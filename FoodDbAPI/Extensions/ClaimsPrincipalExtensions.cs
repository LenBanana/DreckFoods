using System.Security.Claims;

namespace FoodDbAPI.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static int GetUserId(this ClaimsPrincipal user)
    {
        if(user?.Identity?.IsAuthenticated != true)
            throw new UnauthorizedAccessException("User is not authenticated");

        var idClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if(!int.TryParse(idClaim, out var id))
            throw new UnauthorizedAccessException("Invalid user id claim");

        return id;
    }
}