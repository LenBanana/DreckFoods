using FoodDbAPI.Data;
using FoodDbAPI.DTOs;
using FoodDbAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodDbAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = $"{AppRoles.DataEditor},{AppRoles.Admin}")]
public class FddbEditorController(IFddbEditorService editorService) : ControllerBase
{
    [HttpGet("{id}")]
    public async Task<IActionResult> GetFoodById(int id)
    {
        var food = await editorService.GetFoodByIdAsync(id);
        if (food == null)
            return NotFound($"Food with ID {id} not found");
            
        return Ok(food);
    }

    [HttpPut("{id}/info")]
    public async Task<IActionResult> UpdateFoodInfo(int id, [FromBody] FddbFoodUpdateDTO? updateDto)
    {
        if (updateDto == null)
            return BadRequest("Update data cannot be null");
            
        var result = await editorService.UpdateFoodInfoAsync(id, updateDto);
        if (!result)
            return NotFound($"Food with ID {id} not found");
            
        return Ok(new { Success = true, Message = "Food information updated successfully" });
    }

    [HttpPut("{id}/nutrition")]
    public async Task<IActionResult> UpdateFoodNutrition(int id, [FromBody] FddbFoodNutritionUpdateDTO? updateDto)
    {
        if (updateDto == null)
            return BadRequest("Nutrition update data cannot be null");
            
        var result = await editorService.UpdateFoodNutritionAsync(id, updateDto);
        if (!result)
            return NotFound($"Food nutrition with food ID {id} not found");
            
        return Ok(new { Success = true, Message = "Food nutrition updated successfully" });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateFoodComplete(int id, [FromBody] FddbFoodCompleteUpdateDTO? updateDto)
    {
        if (updateDto == null || (updateDto.FoodInfo == null && updateDto.Nutrition == null))
            return BadRequest("Update data cannot be null and must contain food info or nutrition data");
            
        var result = await editorService.UpdateFoodCompleteAsync(id, updateDto);
        if (!result)
            return NotFound($"Food with ID {id} not found");
            
        return Ok(new { Success = true, Message = "Food information and nutrition updated successfully" });
    }
}
