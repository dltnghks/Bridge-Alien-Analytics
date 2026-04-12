using Microsoft.AspNetCore.Mvc;
using BridgeAlien.Analytics.Api.Models;
using BridgeAlien.Analytics.Api.Repositories;

namespace BridgeAlien.Analytics.Api.Controllers;

[ApiController]
[Route("analytics")]
public class AnalyticsController(AnalyticsRepository repo) : ControllerBase
{
    [HttpPost("events")]
    public async Task<IActionResult> PostEvents([FromBody] List<AnalyticsEventDto> events)
    {
        if (events == null || events.Count == 0)
            return BadRequest("이벤트 목록이 비어 있습니다.");

        await repo.SaveEventsAsync(events);
        return Ok(new { saved = events.Count });
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var f = from ?? DateTime.UtcNow.AddDays(-30);
        var t = to   ?? DateTime.UtcNow;
        var result = await repo.GetSummaryAsync(f, t);
        return Ok(result);
    }

    [HttpGet("stages/dropoff")]
    public async Task<IActionResult> GetStageDropoff(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var f = from ?? DateTime.UtcNow.AddDays(-30);
        var t = to   ?? DateTime.UtcNow;
        var result = await repo.GetStageDropoffAsync(f, t);
        return Ok(result);
    }

    [HttpGet("minigames/summary")]
    public async Task<IActionResult> GetMinigameSummary(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var f = from ?? DateTime.UtcNow.AddDays(-30);
        var t = to   ?? DateTime.UtcNow;
        var result = await repo.GetMinigameSummaryAsync(f, t);
        return Ok(result);
    }
}
