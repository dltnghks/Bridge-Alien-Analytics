CREATE TABLE IF NOT EXISTS players (
    player_id    TEXT        PRIMARY KEY,
    platform     TEXT,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT now(),
    last_seen_at TIMESTAMPTZ
);

CREATE TABLE IF NOT EXISTS analytics_events (
    id           BIGSERIAL   PRIMARY KEY,
    player_id    TEXT        NOT NULL REFERENCES players(player_id),
    session_id   TEXT        NOT NULL,
    event_name   TEXT        NOT NULL,
    stage_id     TEXT,
    payload_json JSONB,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_events_event_name ON analytics_events(event_name);
CREATE INDEX IF NOT EXISTS idx_events_stage_id   ON analytics_events(stage_id);
CREATE INDEX IF NOT EXISTS idx_events_created_at ON analytics_events(created_at);
