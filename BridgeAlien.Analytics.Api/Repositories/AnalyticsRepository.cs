using Dapper;
using Npgsql;
using BridgeAlien.Analytics.Api.Models;

namespace BridgeAlien.Analytics.Api.Repositories;

public class AnalyticsRepository(string connectionString)
{
    private readonly string _connectionString = ToNpgsqlString(connectionString);

    private static string ToNpgsqlString(string cs)
    {
        if (!cs.StartsWith("postgresql://") && !cs.StartsWith("postgres://"))
            return cs;

        var uri = new Uri(cs);
        var userInfo = uri.UserInfo.Split(':');
        return new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = uri.AbsolutePath.TrimStart('/'),
            Username = userInfo[0],
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "",
            SslMode = SslMode.Require,
            TrustServerCertificate = true
        }.ConnectionString;
    }

    public async Task<SummaryDto> GetSummaryAsync(DateTime from, DateTime to)
    {
        await using var conn = new NpgsqlConnection(_connectionString);

        var row = await conn.QuerySingleAsync("""
            SELECT
                (SELECT COUNT(DISTINCT player_id) FROM analytics_events
                 WHERE created_at >= @From AND created_at < @To) AS total_players,

                (SELECT COUNT(DISTINCT session_id) FROM analytics_events
                 WHERE event_name = 'session_start'
                   AND created_at >= @From AND created_at < @To) AS total_sessions,

                (SELECT COALESCE(AVG((payload_json->>'duration_sec')::numeric), 0)
                 FROM analytics_events
                 WHERE event_name = 'session_end'
                   AND created_at >= @From AND created_at < @To) AS avg_session_duration_sec
            """,
            new { From = from, To = to });

        return new SummaryDto
        {
            TotalPlayers = Convert.ToInt32(row.total_players),
            TotalSessions = Convert.ToInt32(row.total_sessions),
            AvgSessionDurationSec = Math.Round(Convert.ToDouble(row.avg_session_duration_sec), 1),
            Period = new PeriodDto
            {
                From = from.ToString("yyyy-MM-dd"),
                To = to.AddDays(-1).ToString("yyyy-MM-dd")
            }
        };
    }

    public async Task<IEnumerable<StageDropoffDto>> GetStageDropoffAsync(DateTime from, DateTime to)
    {
        await using var conn = new NpgsqlConnection(_connectionString);

        var rows = await conn.QueryAsync("""
            SELECT
                stage_id,
                COUNT(*) FILTER (WHERE event_name = 'stage_enter') AS enter_count,
                COUNT(*) FILTER (WHERE event_name = 'stage_clear') AS clear_count,
                COUNT(*) FILTER (WHERE event_name = 'stage_fail')  AS fail_count,
                COALESCE(AVG((payload_json->>'score')::numeric)
                    FILTER (WHERE event_name IN ('stage_clear','stage_fail')), 0) AS avg_score,
                COALESCE(AVG((payload_json->>'duration_sec')::numeric)
                    FILTER (WHERE event_name IN ('stage_clear','stage_fail')), 0) AS avg_duration_sec
            FROM analytics_events
            WHERE event_name IN ('stage_enter','stage_clear','stage_fail')
              AND stage_id IS NOT NULL
              AND created_at >= @From AND created_at < @To
            GROUP BY stage_id
            ORDER BY stage_id
            """,
            new { From = from, To = to });

        return rows.Select(r =>
        {
            int enter = Convert.ToInt32(r.enter_count);
            int clear = Convert.ToInt32(r.clear_count);
            return new StageDropoffDto
            {
                StageId = r.stage_id,
                EnterCount = enter,
                ClearCount = clear,
                FailCount = Convert.ToInt32(r.fail_count),
                ClearRate = enter > 0 ? Math.Round((double)clear / enter, 3) : 0,
                AvgScore = Math.Round(Convert.ToDouble(r.avg_score), 1),
                AvgDurationSec = Math.Round(Convert.ToDouble(r.avg_duration_sec), 1)
            };
        });
    }

    public async Task<IEnumerable<MinigameSummaryDto>> GetMinigameSummaryAsync(DateTime from, DateTime to)
    {
        await using var conn = new NpgsqlConnection(_connectionString);

        var rows = await conn.QueryAsync("""
            SELECT
                payload_json->>'minigame_type' AS minigame_type,
                COUNT(*) AS play_count,
                COUNT(*) FILTER (WHERE (payload_json->>'success')::boolean) AS success_count,
                COALESCE(AVG((payload_json->>'score')::numeric), 0) AS avg_score,
                COALESCE(AVG((payload_json->>'duration_sec')::numeric), 0) AS avg_duration_sec,
                COALESCE(AVG((payload_json->>'combo_count')::numeric), 0) AS avg_combo_count
            FROM analytics_events
            WHERE event_name = 'minigame_result'
              AND created_at >= @From AND created_at < @To
            GROUP BY payload_json->>'minigame_type'
            ORDER BY payload_json->>'minigame_type'
            """,
            new { From = from, To = to });

        return rows.Select(r =>
        {
            int play = Convert.ToInt32(r.play_count);
            int success = Convert.ToInt32(r.success_count);
            return new MinigameSummaryDto
            {
                MinigameType = r.minigame_type ?? "",
                PlayCount = play,
                SuccessCount = success,
                SuccessRate = play > 0 ? Math.Round((double)success / play, 3) : 0,
                AvgScore = Math.Round(Convert.ToDouble(r.avg_score), 1),
                AvgDurationSec = Math.Round(Convert.ToDouble(r.avg_duration_sec), 1),
                AvgComboCount = Math.Round(Convert.ToDouble(r.avg_combo_count), 1)
            };
        });
    }

    public async Task<IEnumerable<DailyNewPlayersDto>> GetDailyNewPlayersAsync(DateTime from, DateTime to)
    {
        await using var conn = new NpgsqlConnection(_connectionString);

        var rows = await conn.QueryAsync("""
            SELECT
                date_trunc('day', first_seen) AS day,
                COUNT(*) AS new_players
            FROM (
                SELECT player_id, MIN(created_at) AS first_seen
                FROM analytics_events
                WHERE event_name = 'session_start'
                  AND created_at >= @From AND created_at < @To
                GROUP BY player_id
            ) sub
            GROUP BY date_trunc('day', first_seen)
            ORDER BY day
            """,
            new { From = from, To = to });

        return rows.Select(r => new DailyNewPlayersDto
        {
            Day = ((DateTime)r.day).ToString("yyyy-MM-dd"),
            NewPlayers = Convert.ToInt32(r.new_players)
        });
    }

    public async Task<IEnumerable<StageDetailDto>> GetStageDetailAsync(DateTime from, DateTime to)
    {
        await using var conn = new NpgsqlConnection(_connectionString);

        var rows = await conn.QueryAsync("""
            SELECT
                stage_id,
                COUNT(*) FILTER (WHERE event_name = 'stage_clear' AND (payload_json->>'star_count')::int = 1) AS star1,
                COUNT(*) FILTER (WHERE event_name = 'stage_clear' AND (payload_json->>'star_count')::int = 2) AS star2,
                COUNT(*) FILTER (WHERE event_name = 'stage_clear' AND (payload_json->>'star_count')::int = 3) AS star3,
                COALESCE(AVG((payload_json->>'duration_sec')::numeric) FILTER (WHERE event_name = 'stage_clear'), 0) AS avg_clear_duration,
                COALESCE(AVG((payload_json->>'duration_sec')::numeric) FILTER (WHERE event_name = 'stage_fail'), 0) AS avg_fail_duration
            FROM analytics_events
            WHERE event_name IN ('stage_clear', 'stage_fail')
              AND stage_id IS NOT NULL
              AND created_at >= @From AND created_at < @To
            GROUP BY stage_id
            ORDER BY stage_id
            """,
            new { From = from, To = to });

        return rows.Select(r => new StageDetailDto
        {
            StageId = r.stage_id,
            Star1Count = Convert.ToInt32(r.star1),
            Star2Count = Convert.ToInt32(r.star2),
            Star3Count = Convert.ToInt32(r.star3),
            AvgClearDurationSec = Math.Round(Convert.ToDouble(r.avg_clear_duration), 1),
            AvgFailDurationSec = Math.Round(Convert.ToDouble(r.avg_fail_duration), 1)
        });
    }

    public async Task<IEnumerable<RetentionBucketDto>> GetRetentionAsync(DateTime from, DateTime to)
    {
        await using var conn = new NpgsqlConnection(_connectionString);

        var rows = await conn.QueryAsync("""
            SELECT
                session_count,
                COUNT(*) AS player_count
            FROM (
                SELECT player_id, COUNT(DISTINCT session_id) AS session_count
                FROM analytics_events
                WHERE event_name = 'session_start'
                  AND created_at >= @From AND created_at < @To
                GROUP BY player_id
            ) sub
            GROUP BY session_count
            ORDER BY session_count
            """,
            new { From = from, To = to });

        return rows.Select(r => new RetentionBucketDto
        {
            SessionCount = Convert.ToInt32(r.session_count),
            PlayerCount = Convert.ToInt32(r.player_count)
        });
    }

    public async Task<IEnumerable<TaskSummaryDto>> GetTaskSummaryAsync(DateTime from, DateTime to)
    {
        await using var conn = new NpgsqlConnection(_connectionString);

        var rows = await conn.QueryAsync("""
            WITH typed_events AS (
                SELECT
                    payload_json->>'task_id' AS task_id,
                    payload_json->>'task_name' AS task_name,
                    payload_json->>'task_type' AS task_type,
                    CASE
                        WHEN jsonb_typeof(payload_json->'required_gold') = 'number' THEN (payload_json->>'required_gold')::int
                        WHEN COALESCE(payload_json->>'required_gold', '') ~ '^-?\d+$' THEN (payload_json->>'required_gold')::int
                        ELSE 0
                    END AS required_gold,
                    CASE
                        WHEN jsonb_typeof(payload_json->'actual_luck_delta') = 'number' THEN (payload_json->>'actual_luck_delta')::numeric
                        WHEN COALESCE(payload_json->>'actual_luck_delta', '') ~ '^-?\d+(\.\d+)?$' THEN (payload_json->>'actual_luck_delta')::numeric
                        ELSE 0
                    END AS actual_luck_delta
                FROM analytics_events
                WHERE event_name = 'task_execute'
                  AND created_at >= @From AND created_at < @To
            )
            SELECT
                task_id,
                task_name,
                task_type,
                COUNT(*) AS execute_count,
                COALESCE(SUM(required_gold), 0) AS total_gold_spent,
                COALESCE(AVG(actual_luck_delta), 0) AS avg_luck_delta
            FROM typed_events
            GROUP BY task_id, task_name, task_type
            ORDER BY execute_count DESC, task_id
            """,
            new { From = from, To = to });

        return rows.Select(r => new TaskSummaryDto
        {
            TaskId = r.task_id ?? "",
            TaskName = r.task_name ?? "",
            TaskType = r.task_type ?? "",
            ExecuteCount = Convert.ToInt32(r.execute_count),
            TotalGoldSpent = Convert.ToInt32(r.total_gold_spent),
            AvgLuckDelta = Math.Round(Convert.ToDouble(r.avg_luck_delta), 1)
        });
    }

    public async Task<IEnumerable<SkillUsageSummaryDto>> GetSkillUsageAsync(DateTime from, DateTime to)
    {
        await using var conn = new NpgsqlConnection(_connectionString);

        var rows = await conn.QueryAsync("""
            WITH typed_events AS (
                SELECT
                    payload_json->>'skill_type' AS skill_type,
                    CASE
                        WHEN LOWER(COALESCE(payload_json->>'success', '')) IN ('true', 'false')
                            THEN (payload_json->>'success')::boolean
                        ELSE false
                    END AS success,
                    CASE
                        WHEN jsonb_typeof(payload_json->'used_at_stage_sec') = 'number' THEN (payload_json->>'used_at_stage_sec')::numeric
                        WHEN COALESCE(payload_json->>'used_at_stage_sec', '') ~ '^-?\d+(\.\d+)?$' THEN (payload_json->>'used_at_stage_sec')::numeric
                        ELSE 0
                    END AS used_at_stage_sec
                FROM analytics_events
                WHERE event_name = 'skill_use'
                  AND created_at >= @From AND created_at < @To
            )
            SELECT
                skill_type,
                COUNT(*) AS use_count,
                COUNT(*) FILTER (WHERE success) AS success_count,
                COALESCE(AVG(used_at_stage_sec), 0) AS avg_used_at_stage_sec
            FROM typed_events
            GROUP BY skill_type
            ORDER BY use_count DESC, skill_type
            """,
            new { From = from, To = to });

        return rows.Select(r =>
        {
            int useCount = Convert.ToInt32(r.use_count);
            int successCount = Convert.ToInt32(r.success_count);
            return new SkillUsageSummaryDto
            {
                SkillType = r.skill_type ?? "",
                UseCount = useCount,
                SuccessCount = successCount,
                SuccessRate = useCount > 0 ? Math.Round((double)successCount / useCount, 3) : 0,
                AvgUsedAtStageSec = Math.Round(Convert.ToDouble(r.avg_used_at_stage_sec), 1)
            };
        });
    }

    public async Task<IEnumerable<SkillUpgradeSummaryDto>> GetSkillUpgradeSummaryAsync(DateTime from, DateTime to)
    {
        await using var conn = new NpgsqlConnection(_connectionString);

        var rows = await conn.QueryAsync("""
            WITH typed_events AS (
                SELECT
                    payload_json->>'skill_type' AS skill_type,
                    CASE
                        WHEN LOWER(COALESCE(payload_json->>'success', '')) IN ('true', 'false')
                            THEN (payload_json->>'success')::boolean
                        ELSE false
                    END AS success,
                    CASE
                        WHEN jsonb_typeof(payload_json->'upgrade_cost') = 'number' THEN (payload_json->>'upgrade_cost')::numeric
                        WHEN COALESCE(payload_json->>'upgrade_cost', '') ~ '^-?\d+(\.\d+)?$' THEN (payload_json->>'upgrade_cost')::numeric
                        ELSE 0
                    END AS upgrade_cost,
                    CASE
                        WHEN jsonb_typeof(payload_json->'new_level') = 'number' THEN (payload_json->>'new_level')::int
                        WHEN COALESCE(payload_json->>'new_level', '') ~ '^-?\d+$' THEN (payload_json->>'new_level')::int
                        ELSE 0
                    END AS new_level
                FROM analytics_events
                WHERE event_name = 'skill_upgrade'
                  AND created_at >= @From AND created_at < @To
            )
            SELECT
                skill_type,
                COUNT(*) FILTER (WHERE success) AS success_count,
                COUNT(*) FILTER (WHERE NOT success) AS fail_count,
                COALESCE(AVG(upgrade_cost) FILTER (WHERE success), 0) AS avg_upgrade_cost,
                COALESCE(MAX(new_level) FILTER (WHERE success), 0) AS max_level_reached
            FROM typed_events
            GROUP BY skill_type
            ORDER BY success_count DESC, skill_type
            """,
            new { From = from, To = to });

        return rows.Select(r => new SkillUpgradeSummaryDto
        {
            SkillType = r.skill_type ?? "",
            SuccessCount = Convert.ToInt32(r.success_count),
            FailCount = Convert.ToInt32(r.fail_count),
            AvgUpgradeCost = Math.Round(Convert.ToDouble(r.avg_upgrade_cost), 1),
            MaxLevelReached = Convert.ToInt32(r.max_level_reached)
        });
    }

    public async Task<EconomySummaryDto> GetEconomySummaryAsync(DateTime from, DateTime to)
    {
        await using var conn = new NpgsqlConnection(_connectionString);

        var totalRow = await conn.QuerySingleAsync("""
            WITH typed_events AS (
                SELECT
                    CASE
                        WHEN jsonb_typeof(payload_json->'delta') = 'number' THEN (payload_json->>'delta')::int
                        WHEN COALESCE(payload_json->>'delta', '') ~ '^-?\d+$' THEN (payload_json->>'delta')::int
                        ELSE 0
                    END AS delta
                FROM analytics_events
                WHERE event_name = 'gold_change'
                  AND created_at >= @From AND created_at < @To
            )
            SELECT
                COALESCE(SUM(CASE WHEN delta > 0 THEN delta ELSE 0 END), 0) AS total_earned,
                COALESCE(SUM(CASE WHEN delta < 0 THEN ABS(delta) ELSE 0 END), 0) AS total_spent
            FROM typed_events
            """,
            new { From = from, To = to });

        var breakdownRows = await conn.QueryAsync("""
            WITH typed_events AS (
                SELECT
                    COALESCE(payload_json->>'reason', 'unknown') AS reason,
                    CASE
                        WHEN jsonb_typeof(payload_json->'delta') = 'number' THEN (payload_json->>'delta')::int
                        WHEN COALESCE(payload_json->>'delta', '') ~ '^-?\d+$' THEN (payload_json->>'delta')::int
                        ELSE 0
                    END AS delta
                FROM analytics_events
                WHERE event_name = 'gold_change'
                  AND created_at >= @From AND created_at < @To
            )
            SELECT
                reason,
                COUNT(*) AS event_count,
                COALESCE(SUM(CASE WHEN delta > 0 THEN delta ELSE 0 END), 0) AS total_earned,
                COALESCE(SUM(CASE WHEN delta < 0 THEN ABS(delta) ELSE 0 END), 0) AS total_spent
            FROM typed_events
            GROUP BY reason
            ORDER BY event_count DESC, reason
            """,
            new { From = from, To = to });

        int totalEarned = Convert.ToInt32(totalRow.total_earned);
        int totalSpent = Convert.ToInt32(totalRow.total_spent);

        return new EconomySummaryDto
        {
            TotalGoldEarned = totalEarned,
            TotalGoldSpent = totalSpent,
            NetGold = totalEarned - totalSpent,
            Breakdown = breakdownRows.Select(r =>
            {
                int earned = Convert.ToInt32(r.total_earned);
                int spent = Convert.ToInt32(r.total_spent);
                return new EconomyBreakdownDto
                {
                    Reason = r.reason ?? "unknown",
                    TotalEarned = earned,
                    TotalSpent = spent,
                    NetGold = earned - spent,
                    EventCount = Convert.ToInt32(r.event_count)
                };
            }).ToList()
        };
    }

    public async Task<IEnumerable<PlayerEconomyDto>> GetPlayerEconomyAsync(DateTime from, DateTime to)
    {
        await using var conn = new NpgsqlConnection(_connectionString);

        var rows = await conn.QueryAsync("""
            WITH gold_events_in_range AS (
                SELECT
                    id,
                    player_id,
                    created_at,
                    CASE
                        WHEN jsonb_typeof(payload_json->'delta') = 'number' THEN (payload_json->>'delta')::int
                        WHEN COALESCE(payload_json->>'delta', '') ~ '^-?\d+$' THEN (payload_json->>'delta')::int
                        ELSE 0
                    END AS delta
                FROM analytics_events
                WHERE event_name = 'gold_change'
                  AND created_at >= @From AND created_at < @To
            ),
            player_gold AS (
                SELECT
                    player_id,
                    COALESCE(SUM(CASE WHEN delta > 0 THEN delta ELSE 0 END), 0) AS total_earned,
                    COALESCE(SUM(CASE WHEN delta < 0 THEN ABS(delta) ELSE 0 END), 0) AS total_spent
                FROM gold_events_in_range
                GROUP BY player_id
            ),
            latest_balance AS (
                SELECT DISTINCT ON (player_id)
                    player_id,
                    CASE
                        WHEN jsonb_typeof(payload_json->'balance_after') = 'number' THEN (payload_json->>'balance_after')::int
                        WHEN COALESCE(payload_json->>'balance_after', '') ~ '^-?\d+$' THEN (payload_json->>'balance_after')::int
                        ELSE 0
                    END AS balance_after
                FROM analytics_events
                WHERE event_name = 'gold_change'
                ORDER BY player_id, created_at DESC, id DESC
            )
            SELECT
                pg.player_id,
                pg.total_earned,
                pg.total_spent,
                (pg.total_earned - pg.total_spent) AS net_gold,
                COALESCE(lb.balance_after, 0) AS last_balance
            FROM player_gold pg
            LEFT JOIN latest_balance lb ON lb.player_id = pg.player_id
            ORDER BY pg.total_spent DESC, pg.total_earned DESC, pg.player_id
            """,
            new { From = from, To = to });

        return rows.Select(r => new PlayerEconomyDto
        {
            PlayerId = r.player_id,
            TotalGoldEarned = Convert.ToInt32(r.total_earned),
            TotalGoldSpent = Convert.ToInt32(r.total_spent),
            NetGold = Convert.ToInt32(r.net_gold),
            LastKnownBalance = Convert.ToInt32(r.last_balance)
        });
    }

    public async Task<IEnumerable<StageTimelinePointDto>> GetStageTimelineAsync(DateTime from, DateTime to)
    {
        await using var conn = new NpgsqlConnection(_connectionString);

        var rows = await conn.QueryAsync("""
            WITH skill_events AS (
                SELECT
                    stage_id,
                    ROUND(
                        CASE
                            WHEN jsonb_typeof(payload_json->'used_at_stage_sec') = 'number' THEN (payload_json->>'used_at_stage_sec')::numeric
                            WHEN COALESCE(payload_json->>'used_at_stage_sec', '') ~ '^-?\d+(\.\d+)?$' THEN (payload_json->>'used_at_stage_sec')::numeric
                            ELSE 0
                        END, 1
                    ) AS timeline_sec,
                    'skill_use' AS event_kind,
                    COALESCE(payload_json->>'skill_type', 'unknown') AS label,
                    CASE
                        WHEN jsonb_typeof(payload_json->'score') = 'number' THEN (payload_json->>'score')::numeric
                        WHEN COALESCE(payload_json->>'score', '') ~ '^-?\d+(\.\d+)?$' THEN (payload_json->>'score')::numeric
                        ELSE 0
                    END AS score
                FROM analytics_events
                WHERE event_name = 'skill_use'
                  AND stage_id IS NOT NULL
                  AND created_at >= @From AND created_at < @To
            ),
            result_events AS (
                SELECT
                    stage_id,
                    ROUND(
                        CASE
                            WHEN jsonb_typeof(payload_json->'duration_sec') = 'number' THEN (payload_json->>'duration_sec')::numeric
                            WHEN COALESCE(payload_json->>'duration_sec', '') ~ '^-?\d+(\.\d+)?$' THEN (payload_json->>'duration_sec')::numeric
                            ELSE 0
                        END, 1
                    ) AS timeline_sec,
                    'stage_result' AS event_kind,
                    CASE WHEN event_name = 'stage_clear' THEN 'clear' ELSE 'fail' END AS label,
                    CASE
                        WHEN jsonb_typeof(payload_json->'score') = 'number' THEN (payload_json->>'score')::numeric
                        WHEN COALESCE(payload_json->>'score', '') ~ '^-?\d+(\.\d+)?$' THEN (payload_json->>'score')::numeric
                        ELSE 0
                    END AS score
                FROM analytics_events
                WHERE event_name IN ('stage_clear', 'stage_fail')
                  AND stage_id IS NOT NULL
                  AND created_at >= @From AND created_at < @To
            ),
            timeline_points AS (
                SELECT * FROM skill_events
                UNION ALL
                SELECT * FROM result_events
            )
            SELECT
                stage_id,
                timeline_sec,
                event_kind,
                label,
                COUNT(*) AS event_count,
                COALESCE(AVG(score), 0) AS avg_score
            FROM timeline_points
            GROUP BY stage_id, timeline_sec, event_kind, label
            ORDER BY stage_id, timeline_sec, event_kind, label
            """,
            new { From = from, To = to });

        return rows.Select(r => new StageTimelinePointDto
        {
            StageId = r.stage_id ?? "",
            TimelineSec = Convert.ToDouble(r.timeline_sec),
            EventKind = r.event_kind ?? "",
            Label = r.label ?? "",
            EventCount = Convert.ToInt32(r.event_count),
            AvgScore = Math.Round(Convert.ToDouble(r.avg_score), 1)
        });
    }

    public async Task SaveEventsAsync(IEnumerable<AnalyticsEventDto> events)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        foreach (var e in events)
        {
            await conn.ExecuteAsync("""
                INSERT INTO players (player_id, platform, last_seen_at)
                VALUES (@PlayerId, @Platform, @CreatedAt)
                ON CONFLICT (player_id)
                DO UPDATE SET platform = EXCLUDED.platform,
                              last_seen_at = EXCLUDED.last_seen_at
                """,
                new { e.PlayerId, e.Platform, e.CreatedAt },
                tx);

            var payloadJson = e.Payload?.ToJsonString();
            await conn.ExecuteAsync("""
                INSERT INTO analytics_events
                    (player_id, session_id, event_name, stage_id, payload_json, created_at)
                VALUES
                    (@PlayerId, @SessionId, @EventName, @StageId, @PayloadJson::jsonb, @CreatedAt)
                """,
                new
                {
                    e.PlayerId,
                    e.SessionId,
                    e.EventName,
                    e.StageId,
                    PayloadJson = payloadJson,
                    e.CreatedAt
                },
                tx);
        }

        await tx.CommitAsync();
    }
}
