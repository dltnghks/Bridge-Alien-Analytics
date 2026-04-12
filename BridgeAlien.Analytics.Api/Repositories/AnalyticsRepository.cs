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

        var uri      = new Uri(cs);
        var userInfo = uri.UserInfo.Split(':');
        return new NpgsqlConnectionStringBuilder
        {
            Host                 = uri.Host,
            Port                 = uri.Port > 0 ? uri.Port : 5432,
            Database             = uri.AbsolutePath.TrimStart('/'),
            Username             = userInfo[0],
            Password             = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "",
            SslMode              = SslMode.Require,
            TrustServerCertificate = true
        }.ConnectionString;
    }


    public async Task<SummaryDto> GetSummaryAsync(DateTime from, DateTime to)
    {
        await using var conn = new NpgsqlConnection(_connectionString);

        var row = await conn.QuerySingleAsync("""
            SELECT
                (SELECT COUNT(DISTINCT player_id) FROM analytics_events
                 WHERE created_at BETWEEN @From AND @To) AS total_players,

                (SELECT COUNT(DISTINCT session_id) FROM analytics_events
                 WHERE event_name = 'session_start'
                   AND created_at BETWEEN @From AND @To) AS total_sessions,

                COALESCE(
                    AVG((payload_json->>'duration_sec')::numeric)
                , 0) AS avg_session_duration_sec
            FROM analytics_events
            WHERE event_name = 'session_end'
              AND created_at BETWEEN @From AND @To
            """,
            new { From = from, To = to });

        return new SummaryDto
        {
            TotalPlayers          = (int)row.total_players,
            TotalSessions         = (int)row.total_sessions,
            AvgSessionDurationSec = Math.Round((double)row.avg_session_duration_sec, 1),
            Period = new PeriodDto
            {
                From = from.ToString("yyyy-MM-dd"),
                To   = to.ToString("yyyy-MM-dd")
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
              AND created_at BETWEEN @From AND @To
            GROUP BY stage_id
            ORDER BY stage_id
            """,
            new { From = from, To = to });

        return rows.Select(r =>
        {
            int enter = (int)r.enter_count;
            int clear = (int)r.clear_count;
            return new StageDropoffDto
            {
                StageId        = r.stage_id,
                EnterCount     = enter,
                ClearCount     = clear,
                FailCount      = (int)r.fail_count,
                ClearRate      = enter > 0 ? Math.Round((double)clear / enter, 3) : 0,
                AvgScore       = Math.Round((double)r.avg_score, 1),
                AvgDurationSec = Math.Round((double)r.avg_duration_sec, 1)
            };
        });
    }

    public async Task<IEnumerable<MinigameSummaryDto>> GetMinigameSummaryAsync(DateTime from, DateTime to)
    {
        await using var conn = new NpgsqlConnection(_connectionString);

        var rows = await conn.QueryAsync("""
            SELECT
                payload_json->>'minigame_type'                        AS minigame_type,
                COUNT(*)                                               AS play_count,
                COUNT(*) FILTER (WHERE (payload_json->>'success')::boolean) AS success_count,
                COALESCE(AVG((payload_json->>'score')::numeric), 0)   AS avg_score,
                COALESCE(AVG((payload_json->>'duration_sec')::numeric), 0) AS avg_duration_sec,
                COALESCE(AVG((payload_json->>'combo_count')::numeric), 0)  AS avg_combo_count
            FROM analytics_events
            WHERE event_name = 'minigame_result'
              AND created_at BETWEEN @From AND @To
            GROUP BY payload_json->>'minigame_type'
            ORDER BY payload_json->>'minigame_type'
            """,
            new { From = from, To = to });

        return rows.Select(r =>
        {
            int play    = (int)r.play_count;
            int success = (int)r.success_count;
            return new MinigameSummaryDto
            {
                MinigameType   = r.minigame_type ?? "",
                PlayCount      = play,
                SuccessCount   = success,
                SuccessRate    = play > 0 ? Math.Round((double)success / play, 3) : 0,
                AvgScore       = Math.Round((double)r.avg_score, 1),
                AvgDurationSec = Math.Round((double)r.avg_duration_sec, 1),
                AvgComboCount  = Math.Round((double)r.avg_combo_count, 1)
            };
        });
    }


    public async Task SaveEventsAsync(IEnumerable<AnalyticsEventDto> events)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        foreach (var e in events)
        {
            // players upsert
            await conn.ExecuteAsync("""
                INSERT INTO players (player_id, platform, last_seen_at)
                VALUES (@PlayerId, @Platform, @CreatedAt)
                ON CONFLICT (player_id)
                DO UPDATE SET platform = EXCLUDED.platform,
                              last_seen_at = EXCLUDED.last_seen_at
                """,
                new { e.PlayerId, e.Platform, e.CreatedAt },
                tx);

            // analytics_events insert
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
