using DailyGourmet.Api.Handlers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.Ingredients;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DailyGourmet.Api.Controllers;

[ApiController]
[Authorize]
public class LookupsController(LookupHandler handler) : ControllerBase
{
    [HttpGet("api/ingredient-categories")]
    public async Task<ActionResult<ApiResponse<List<LookupDto>>>> IngredientCategories(CancellationToken ct) =>
        Ok(ApiResponse<List<LookupDto>>.Ok(await handler.IngredientCategoriesAsync(ct)));

    [HttpGet("api/allergens")]
    public async Task<ActionResult<ApiResponse<List<LookupDto>>>> Allergens(CancellationToken ct) =>
        Ok(ApiResponse<List<LookupDto>>.Ok(await handler.AllergensAsync(ct)));

    [HttpGet("api/recipe-categories")]
    public async Task<ActionResult<ApiResponse<List<LookupDto>>>> RecipeCategories(CancellationToken ct) =>
        Ok(ApiResponse<List<LookupDto>>.Ok(await handler.RecipeCategoriesAsync(ct)));

    [HttpGet("api/target-audience-groups")]
    public async Task<ActionResult<ApiResponse<List<LookupDto>>>> TargetAudienceGroups(CancellationToken ct) =>
        Ok(ApiResponse<List<LookupDto>>.Ok(await handler.TargetAudienceGroupsAsync(ct)));
}
