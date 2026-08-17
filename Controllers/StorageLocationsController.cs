using DailyGourmet.Api.Handlers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.Production;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DailyGourmet.Api.Controllers;

[ApiController]
[Route("api/storage-locations")]
[Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN")]
public class StorageLocationsController(StorageLocationHandler handler) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<StorageLocationDto>>>> List(CancellationToken ct) =>
        Ok(ApiResponse<List<StorageLocationDto>>.Ok(await handler.ListAsync(ct)));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<StorageLocationDto>>> Create([FromBody] SaveStorageLocationDto dto, CancellationToken ct) =>
        Ok(ApiResponse<StorageLocationDto>.Ok(await handler.CreateAsync(dto, ct)));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<StorageLocationDto>>> Update(Guid id, [FromBody] SaveStorageLocationDto dto, CancellationToken ct) =>
        Ok(ApiResponse<StorageLocationDto>.Ok(await handler.UpdateAsync(id, dto, ct)));
}
