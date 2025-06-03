using FoodDbAPI.DTOs;
using FoodDbAPI.Extensions;
using FoodDbAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodDbAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TimelineController(IFoodService foodService, ILogger<TimelineController> logger)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<TimelineResponse>> GetTimeline(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate)
    {
        try
        {
            if (startDate == default || endDate == default)
            {
                return BadRequest(new { message = "Start date and end date are required" });
            }

            if (startDate > endDate)
            {
                return BadRequest(new { message = "Start date must be before end date" });
            }

            var userId = User.GetUserId();
            var timeline = await foodService.GetTimelineAsync(userId, startDate, endDate);

            var response = new TimelineResponse
            {
                Days = timeline,
                TotalDays = timeline.Count
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting timeline");
            return StatusCode(500, new { message = "An error occurred" });
        }
    }
}