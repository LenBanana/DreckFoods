using FoodDbAPI.DTOs;
using FoodDbAPI.Extensions;
using FoodDbAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodDbAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FoodController(IFoodService foodService, ILogger<FoodController> logger) : ControllerBase
{
    [HttpGet("search")]
    public async Task<ActionResult<FoodSearchResponse>> SearchFoods(
        [FromQuery] string query,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest(new { message = "Query parameter is required" });
            }

            if (pageSize > 100)
            {
                pageSize = 100; // Limit maximum page size
            }

            var result = await foodService.SearchFoodsAsync(query, page, pageSize);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching foods with query: {Query}", query);
            return StatusCode(500, new { message = "An error occurred while searching foods" });
        }
    }

    [HttpGet("{foodId}")]
    public async Task<ActionResult<FoodSearchDto>> GetFood(int foodId)
    {
        try
        {
            var food = await foodService.GetFoodByIdAsync(foodId);
            if (food == null)
            {
                return NotFound(new { message = "Food not found" });
            }
            return Ok(food);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting food with ID: {FoodId}", foodId);
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    [HttpPost("entries")]
    public async Task<ActionResult<FoodEntryDto>> AddFoodEntry(CreateFoodEntryRequest request)
    {
        try
        {
            var userId = User.GetUserId();
            var entry = await foodService.AddFoodEntryAsync(userId, request);
            return Ok(entry);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding food entry");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    [HttpGet("entries")]
    public async Task<ActionResult<List<FoodEntryDto>>> GetFoodEntries([FromQuery] DateTime? date = null)
    {
        try
        {
            var userId = User.GetUserId();
            var entries = await foodService.GetFoodEntriesAsync(userId, date);
            return Ok(entries);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting food entries");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }

    [HttpDelete("entries/{entryId}")]
    public async Task<ActionResult> DeleteFoodEntry(int entryId)
    {
        try
        {
            var userId = User.GetUserId();
            await foodService.DeleteFoodEntryAsync(userId, entryId);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting food entry");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }
}