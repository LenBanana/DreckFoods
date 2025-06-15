using FoodDbAPI.DTOs;
using FoodDbAPI.Extensions;
using FoodDbAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodDbAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MealController(IMealService mealService) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<List<MealResponseDto>>> GetUserMeals()
    {
        try
        {
            var userId = User.GetUserId();
            var meals = await mealService.GetUserMealsAsync(userId);
            return Ok(meals);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<MealResponseDto>> GetMealById(int id)
    {
        try
        {
            var userId = User.GetUserId();
            var meal = await mealService.GetMealByIdAsync(id, userId);
            return Ok(meal);
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Meal not found");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<MealResponseDto>> CreateMeal([FromBody] CreateMealDto createMealDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = User.GetUserId();
            var meal = await mealService.CreateMealAsync(userId, createMealDto);
            return CreatedAtAction(nameof(GetMealById), new { id = meal.Id }, meal);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
    
    // Duplicate meals 
    [HttpPost("duplicate/{id}")]
    [Authorize]
    public async Task<ActionResult<MealResponseDto>> DuplicateMeal(int id)
    {
        try
        {
            var userId = User.GetUserId();
            var meal = await mealService.DuplicateMealAsync(id, userId);
            return CreatedAtAction(nameof(GetMealById), new { id = meal.Id }, meal);
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Meal not found");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    [HttpPut("{id}")]
    [Authorize]
    public async Task<ActionResult<MealResponseDto>> UpdateMeal(int id, [FromBody] UpdateMealDto updateMealDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = User.GetUserId();
            var meal = await mealService.UpdateMealAsync(id, userId, updateMealDto);
            return Ok(meal);
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Meal not found");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<ActionResult> DeleteMeal(int id)
    {
        try
        {
            var userId = User.GetUserId();
            var result = await mealService.DeleteMealAsync(id, userId);

            if (result)
            {
                return NoContent();
            }

            return NotFound("Meal not found");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    [HttpPost("portion")]
    [Authorize]
    public async Task<ActionResult<List<FoodEntryDto>>> AddMealPortion([FromBody] AddMealPortionDto addMealPortionDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = User.GetUserId();
            var foodEntries = await mealService.AddMealPortionAsync(userId, addMealPortionDto);
            return Ok(foodEntries);
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Meal not found");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    [HttpGet("{mealId}/share")]
    [Authorize]
    public ActionResult<string> GetMealShareId(int mealId)
    {
        try
        {
            var userId = User.GetUserId();
            var shareId = mealService.GetMealShareId(mealId, userId);
            return Ok(shareId);
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Meal not found");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    [HttpPost("{shareId}/share")]
    [Authorize]
    public async Task<ActionResult<MealResponseDto>> AddMealByShareId(string shareId)
    {
        try
        {
            var userId = User.GetUserId();
            var meal = await mealService.AddMealByShareIdAsync(shareId, userId);
            return CreatedAtAction(nameof(GetMealById), new { id = meal.Id }, meal);
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Shared meal not found");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
}