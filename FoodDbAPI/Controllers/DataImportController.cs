using FoodDbAPI.Data;
using FoodDbAPI.DTOs;
using FoodDbAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodDbAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AppPolicies.CanImportData)]
public class DataImportController(IDataImportService import) : ControllerBase
{
    [HttpPost("import")]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> ImportFoods([FromBody] List<FddbFoodImportDto> foods)
    {
        await import.ImportFoodDataAsync(foods);
        return Ok(new { imported = foods.Count });
    }

    [HttpGet("count")]
    public async Task<ActionResult<int>> GetCount()
    {
        var count = await import.GetFoodCountAsync();
        return Ok(count);
    }
    
    [HttpPost("cleanup")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> CleanupData()
    {
        var cleanUpEntries =await import.CleanupDataAsync();
        return Ok(new { message = $"Cleaned up {cleanUpEntries} entries." });
    }
}