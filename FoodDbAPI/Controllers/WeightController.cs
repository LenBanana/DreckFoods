using FoodDbAPI.DTOs;
using FoodDbAPI.Extensions;
using FoodDbAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodDbAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WeightController(IWeightService weightService, ILogger<WeightController> logger)
    : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<WeightEntryDto>> AddWeightEntry(CreateWeightEntryRequest request)
    {
        try
        {
            var userId = User.GetUserId();
            var entry = await weightService.AddWeightEntryAsync(userId, request);
            return Ok(entry);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding weight entry");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    [HttpGet]
    public async Task<ActionResult<List<WeightEntryDto>>> GetWeightHistory(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var userId = User.GetUserId();
            var entries = await weightService.GetWeightHistoryAsync(userId, startDate, endDate);
            return Ok(entries);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting weight history");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    [HttpDelete("{entryId}")]
    public async Task<ActionResult> DeleteWeightEntry(int entryId)
    {
        try
        {
            var userId = User.GetUserId();
            await weightService.DeleteWeightEntryAsync(userId, entryId);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting weight entry");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }
}