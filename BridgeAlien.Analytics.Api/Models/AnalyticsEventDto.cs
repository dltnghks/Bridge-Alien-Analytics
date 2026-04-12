using System.Text.Json.Nodes;
using System.ComponentModel.DataAnnotations;

namespace BridgeAlien.Analytics.Api.Models;

public class AnalyticsEventDto
{
    [Required] public string PlayerId    { get; set; } = "";
    [Required] public string SessionId   { get; set; } = "";
    [Required] public string EventName   { get; set; } = "";
    public string?           StageId     { get; set; }
    public JsonObject?       Payload     { get; set; }
    [Required] public string ClientVersion { get; set; } = "";
    [Required] public string Platform    { get; set; } = "";
    public DateTime          CreatedAt   { get; set; } = DateTime.UtcNow;
}
