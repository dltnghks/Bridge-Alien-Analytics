using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BridgeAlien.Analytics.Api.Models;
using BridgeAlien.Analytics.Api.Repositories;

namespace BridgeAlien.Analytics.Api.Controllers;

[ApiController]
[Authorize]
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
    public async Task<IActionResult> GetSummary([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var f = from ?? DateTime.UtcNow.AddDays(-30);
        var t = to ?? DateTime.UtcNow.AddDays(1);
        var result = await repo.GetSummaryAsync(f, t);
        return Ok(result);
    }

    [HttpGet("stages/dropoff")]
    public async Task<IActionResult> GetStageDropoff([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var f = from ?? DateTime.UtcNow.AddDays(-30);
        var t = to ?? DateTime.UtcNow.AddDays(1);
        var result = await repo.GetStageDropoffAsync(f, t);
        return Ok(result);
    }

    [HttpGet("minigames/summary")]
    public async Task<IActionResult> GetMinigameSummary([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var f = from ?? DateTime.UtcNow.AddDays(-30);
        var t = to ?? DateTime.UtcNow.AddDays(1);
        var result = await repo.GetMinigameSummaryAsync(f, t);
        return Ok(result);
    }

    [HttpGet("daily/players")]
    public async Task<IActionResult> GetDailyNewPlayers([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var f = from ?? DateTime.UtcNow.AddDays(-30);
        var t = to ?? DateTime.UtcNow.AddDays(1);
        var result = await repo.GetDailyNewPlayersAsync(f, t);
        return Ok(result);
    }

    [HttpGet("stages/detail")]
    public async Task<IActionResult> GetStageDetail([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var f = from ?? DateTime.UtcNow.AddDays(-30);
        var t = to ?? DateTime.UtcNow.AddDays(1);
        var result = await repo.GetStageDetailAsync(f, t);
        return Ok(result);
    }

    [HttpGet("retention")]
    public async Task<IActionResult> GetRetention([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var f = from ?? DateTime.UtcNow.AddDays(-30);
        var t = to ?? DateTime.UtcNow.AddDays(1);
        var result = await repo.GetRetentionAsync(f, t);
        return Ok(result);
    }

    [HttpGet("tasks/summary")]
    public async Task<IActionResult> GetTaskSummary([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var f = from ?? DateTime.UtcNow.AddDays(-30);
        var t = to ?? DateTime.UtcNow.AddDays(1);
        var result = await repo.GetTaskSummaryAsync(f, t);
        return Ok(result);
    }

    [HttpGet("skills/usage")]
    public async Task<IActionResult> GetSkillUsage([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var f = from ?? DateTime.UtcNow.AddDays(-30);
        var t = to ?? DateTime.UtcNow.AddDays(1);
        var result = await repo.GetSkillUsageAsync(f, t);
        return Ok(result);
    }

    [HttpGet("skills/upgrades")]
    public async Task<IActionResult> GetSkillUpgrades([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var f = from ?? DateTime.UtcNow.AddDays(-30);
        var t = to ?? DateTime.UtcNow.AddDays(1);
        var result = await repo.GetSkillUpgradeSummaryAsync(f, t);
        return Ok(result);
    }

    [HttpGet("economy/summary")]
    public async Task<IActionResult> GetEconomySummary([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var f = from ?? DateTime.UtcNow.AddDays(-30);
        var t = to ?? DateTime.UtcNow.AddDays(1);
        var result = await repo.GetEconomySummaryAsync(f, t);
        return Ok(result);
    }

    [HttpGet("economy/players")]
    public async Task<IActionResult> GetPlayerEconomy([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var f = from ?? DateTime.UtcNow.AddDays(-30);
        var t = to ?? DateTime.UtcNow.AddDays(1);
        var result = await repo.GetPlayerEconomyAsync(f, t);
        return Ok(result);
    }
}
