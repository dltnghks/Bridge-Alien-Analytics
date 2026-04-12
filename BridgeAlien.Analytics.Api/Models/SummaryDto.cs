namespace BridgeAlien.Analytics.Api.Models;

public class SummaryDto
{
    public int    TotalPlayers          { get; set; }
    public int    TotalSessions         { get; set; }
    public double AvgSessionDurationSec { get; set; }
    public PeriodDto Period             { get; set; } = new();
}

public class PeriodDto
{
    public string From { get; set; } = "";
    public string To   { get; set; } = "";
}

public class StageDropoffDto
{
    public string StageId        { get; set; } = "";
    public int    EnterCount     { get; set; }
    public int    ClearCount     { get; set; }
    public int    FailCount      { get; set; }
    public double ClearRate      { get; set; }
    public double AvgScore       { get; set; }
    public double AvgDurationSec { get; set; }
}

public class MinigameSummaryDto
{
    public string MinigameType   { get; set; } = "";
    public int    PlayCount      { get; set; }
    public int    SuccessCount   { get; set; }
    public double SuccessRate    { get; set; }
    public double AvgScore       { get; set; }
    public double AvgDurationSec { get; set; }
    public double AvgComboCount  { get; set; }
}

public class DailyNewPlayersDto
{
    public string Day        { get; set; } = "";
    public int    NewPlayers { get; set; }
}

public class StageDetailDto
{
    public string StageId            { get; set; } = "";
    public int    Star1Count         { get; set; }
    public int    Star2Count         { get; set; }
    public int    Star3Count         { get; set; }
    public double AvgClearDurationSec { get; set; }
    public double AvgFailDurationSec  { get; set; }
}

public class RetentionBucketDto
{
    public int SessionCount { get; set; }
    public int PlayerCount  { get; set; }
}
