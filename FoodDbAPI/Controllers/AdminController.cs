using FoodDbAPI.Data;
using FoodDbAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodDbAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = AppRoles.Admin)]
public class AdminController(FoodDbContext db) : ControllerBase
{
    [HttpPut("users/{id}/role")]
    public async Task<IActionResult> SetRole(int id, [FromBody] AppRole newRole)
    {
        var user = await db.Users.FindAsync(id);
        if (user == null) return NotFound();
        user.Role = newRole;
        await db.SaveChangesAsync();
        return NoContent();
    }
}