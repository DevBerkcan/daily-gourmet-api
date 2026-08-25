using DailyGourmet.Api.Handlers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.Recipes;
using DailyGourmet.Api.Services.Pdf;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DailyGourmet.Api.Controllers;

[ApiController]
[Route("api/recipes")]
[Authorize]
public class RecipesController(RecipeHandler handler) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<RecipeDto>>>> List(
        [FromQuery] string? search, [FromQuery] Guid? category, [FromQuery] bool? active,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        var result = await handler.ListAsync(search, category, active, page, pageSize, ct);
        return Ok(ApiResponse<PagedResult<RecipeDto>>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<RecipeDto>>> GetById(Guid id, CancellationToken ct)
    {
        var result = await handler.GetByIdAsync(id, ct);
        return Ok(ApiResponse<RecipeDto>.Ok(result));
    }

    [HttpPost]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN")]
    public async Task<ActionResult<ApiResponse<RecipeDto>>> Create([FromBody] SaveRecipeDto dto, CancellationToken ct)
    {
        var result = await handler.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, ApiResponse<RecipeDto>.Ok(result));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN")]
    public async Task<ActionResult<ApiResponse<RecipeDto>>> Update(Guid id, [FromBody] SaveRecipeDto dto, CancellationToken ct)
    {
        var result = await handler.UpdateAsync(id, dto, ct);
        return Ok(ApiResponse<RecipeDto>.Ok(result));
    }

    [HttpPost("{id:guid}/duplicate")]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN")]
    public async Task<ActionResult<ApiResponse<RecipeDto>>> Duplicate(Guid id, CancellationToken ct)
    {
        var result = await handler.DuplicateAsync(id, ct);
        return Ok(ApiResponse<RecipeDto>.Ok(result));
    }

    [HttpPost("{id:guid}/archive")]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN")]
    public async Task<ActionResult<ApiResponse>> Archive(Guid id, CancellationToken ct)
    {
        await handler.ArchiveAsync(id, ct);
        return Ok(ApiResponse.Ok());
    }

    [HttpGet("{id:guid}/scale")]
    public async Task<ActionResult<ApiResponse<RecipeScaleResultDto>>> Scale(Guid id, [FromQuery] int portions, CancellationToken ct)
    {
        var result = await handler.ScaleAsync(id, portions, ct);
        return Ok(ApiResponse<RecipeScaleResultDto>.Ok(result));
    }

    [HttpGet("{id:guid}/nutrition-detail")]
    public async Task<ActionResult<ApiResponse<RecipeNutritionDetailDto>>> NutritionDetail(Guid id, CancellationToken ct)
    {
        var result = await handler.GetNutritionDetailAsync(id, ct);
        return Ok(ApiResponse<RecipeNutritionDetailDto>.Ok(result));
    }

    [HttpGet("{id:guid}/label")]
    public async Task<IActionResult> Label(
        Guid id,
        [FromQuery] string orientierung = "Quer",
        [FromQuery] string inhalt = "Vollstaendig",
        [FromQuery] bool proPortion = false,
        [FromQuery] decimal? portionsgroesseG = null,
        [FromQuery] string? mindestensHaltbarBis = null,
        CancellationToken ct = default)
    {
        var o = Enum.TryParse<EtikettOrientierung>(orientierung, true, out var parsedOrientierung) ? parsedOrientierung : EtikettOrientierung.Quer;
        var i = Enum.TryParse<EtikettInhalt>(inhalt, true, out var parsedInhalt) ? parsedInhalt : EtikettInhalt.Vollstaendig;
        var bytes = await handler.RenderLabelAsync(id, o, i, proPortion, portionsgroesseG, mindestensHaltbarBis, ct);
        return File(bytes, "application/pdf", $"etikett-{id}.pdf");
    }

    [HttpPost("import")]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN")]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<ApiResponse<RecipeImportResultDto>>> Import(IFormFile zutatenMengenFile, IFormFile artikeldatenFile, IFormFile? allergeneListeFile, CancellationToken ct)
    {
        if (zutatenMengenFile.Length == 0 || artikeldatenFile.Length == 0)
            throw new DailyGourmet.Api.Helpers.ValidationException("Beide Dateien (Zutaten-Mengen und Artikeldaten) werden benötigt.");
        await using var zutatenStream = zutatenMengenFile.OpenReadStream();
        await using var artikelStream = artikeldatenFile.OpenReadStream();
        await using var allergeneStream = allergeneListeFile is { Length: > 0 } ? allergeneListeFile.OpenReadStream() : null;
        var result = await handler.ImportFromRezeptrechnerAsync(zutatenStream, artikelStream, allergeneStream, ct);
        return Ok(ApiResponse<RecipeImportResultDto>.Ok(result));
    }
}
