using DailyGourmet.Api.Handlers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.Logistics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DailyGourmet.Api.Controllers;

[ApiController]
[Route("api/drivers")]
[Authorize]
public class DriversController(DriverHandler handler) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN,KITCHEN_MANAGER,KITCHEN_STAFF,DRIVER")]
    public async Task<ActionResult<ApiResponse<PagedResult<DriverDto>>>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        Ok(ApiResponse<PagedResult<DriverDto>>.Ok(await handler.ListAsync(page, pageSize, ct)));

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN")]
    public async Task<ActionResult<ApiResponse<DriverDto>>> GetById(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<DriverDto>.Ok(await handler.GetByIdAsync(id, ct)));

    [HttpPost]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN")]
    public async Task<ActionResult<ApiResponse<DriverDto>>> Create([FromBody] SaveDriverDto dto, CancellationToken ct) =>
        Ok(ApiResponse<DriverDto>.Ok(await handler.CreateAsync(dto, ct)));

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN")]
    public async Task<ActionResult<ApiResponse<DriverDto>>> Update(Guid id, [FromBody] SaveDriverDto dto, CancellationToken ct) =>
        Ok(ApiResponse<DriverDto>.Ok(await handler.UpdateAsync(id, dto, ct)));
}
