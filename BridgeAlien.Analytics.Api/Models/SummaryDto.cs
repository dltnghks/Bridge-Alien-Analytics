namespace BridgeAlien.Analytics.Api.Models;

public class SummaryDto
{
    public int TotalPlayers { get; set; }
    public int TotalSessions { get; set; }
    public double AvgSessionDurationSec { get; set; }
    public PeriodDto Period { get; set; } = new();
}

public class PeriodDto
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
}

public class StageDropoffDto
{
    public string StageId { get; set; } = "";
    public int EnterCount { get; set; }
    public int ClearCount { get; set; }
    public int FailCount { get; set; }
    public double ClearRate { get; set; }
    public double AvgScore { get; set; }
    public double AvgDurationSec { get; set; }
}

public class MinigameSummaryDto
{
    public string MinigameType { get; set; } = "";
    public int PlayCount { get; set; }
    public int SuccessCount { get; set; }
    public double SuccessRate { get; set; }
    public double AvgScore { get; set; }
    public double AvgDurationSec { get; set; }
    public double AvgComboCount { get; set; }
}

public class DailyNewPlayersDto
{
    public string Day { get; set; } = "";
    public int NewPlayers { get; set; }
}

public class StageDetailDto
{
    public string StageId { get; set; } = "";
    public int Star1Count { get; set; }
    public int Star2Count { get; set; }
    public int Star3Count { get; set; }
    public double AvgClearDurationSec { get; set; }
    public double AvgFailDurationSec { get; set; }
}

public class RetentionBucketDto
{
    public int SessionCount { get; set; }
    public int PlayerCount { get; set; }
}

public class TaskSummaryDto
{
    public string TaskId { get; set; } = "";
    public string TaskName { get; set; } = "";
    public string TaskType { get; set; } = "";
    public int ExecuteCount { get; set; }
    public int TotalGoldSpent { get; set; }
    public double AvgLuckDelta { get; set; }
}

public class SkillUsageSummaryDto
{
    public string SkillType { get; set; } = "";
    public int UseCount { get; set; }
    public int SuccessCount { get; set; }
    public double SuccessRate { get; set; }
    public double AvgUsedAtStageSec { get; set; }
}

public class SkillUpgradeSummaryDto
{
    public string SkillType { get; set; } = "";
    public int SuccessCount { get; set; }
    public int FailCount { get; set; }
    public double AvgUpgradeCost { get; set; }
    public int MaxLevelReached { get; set; }
}

public class EconomySummaryDto
{
    public int TotalGoldEarned { get; set; }
    public int TotalGoldSpent { get; set; }
    public int NetGold { get; set; }
    public List<EconomyBreakdownDto> Breakdown { get; set; } = new();
}

public class EconomyBreakdownDto
{
    public string Reason { get; set; } = "";
    public int TotalEarned { get; set; }
    public int TotalSpent { get; set; }
    public int NetGold { get; set; }
    public int EventCount { get; set; }
}

public class PlayerEconomyDto
{
    public string PlayerId { get; set; } = "";
    public int TotalGoldEarned { get; set; }
    public int TotalGoldSpent { get; set; }
    public int NetGold { get; set; }
    public int LastKnownBalance { get; set; }
}

public class StageTimelinePointDto
{
    public string StageId { get; set; } = "";
    public double TimelineSec { get; set; }
    public string EventKind { get; set; } = "";
    public string Label { get; set; } = "";
    public int EventCount { get; set; }
    public double AvgScore { get; set; }
}
