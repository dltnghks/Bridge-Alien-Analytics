using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;

namespace BridgeAlien.Analytics.Api.Models;

public class AnalyticsEventDto
{
    [Required]
    [JsonPropertyName("player_id")]
    public string PlayerId { get; set; } = "";

    [Required]
    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = "";

    [Required]
    [JsonPropertyName("event_name")]
    public string EventName { get; set; } = "";

    [JsonPropertyName("stage_id")]
    public string? StageId { get; set; }

    [JsonPropertyName("payload")]
    public JsonObject? Payload { get; set; }

    [Required]
    [JsonPropertyName("client_version")]
    public string ClientVersion { get; set; } = "";

    [Required]
    [JsonPropertyName("platform")]
    public string Platform { get; set; } = "";

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
