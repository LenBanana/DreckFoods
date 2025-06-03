using FoodDbAPI.Data;
using FoodDbAPI.Models.Fddb;
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
    public async Task<IActionResult> ImportFoods([FromBody] List<FddbFood> foods)
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
}