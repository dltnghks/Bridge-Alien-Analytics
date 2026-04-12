# Bridge-Alien Analytics Backend

Unity 게임 **Bridge-Alien**의 플레이어 행동 통계를 수집·저장·조회하는 백엔드 API.  
포트폴리오 목적으로 제작. 스테이지 이탈률, 미니게임 성과 등 행동 데이터를 분석할 수 있다.

- **대시보드**: `https://<your-domain>/dashboard.html`
- **API 문서**: `https://<your-domain>/scalar/v1`
- **기술 스택**: ASP.NET Core 9 / PostgreSQL / Dapper / Railway

---

## 아키텍처

```
Unity Client
  └─ AnalyticsService.Track(event)
       └─ AnalyticsQueue (인메모리 큐)
            └─ [씬 전환 or 30초 주기] 배치 전송
                 └─ POST /analytics/events
                      └─ AnalyticsController
                           └─ AnalyticsRepository (Dapper)
                                └─ PostgreSQL
                                     ├─ players
                                     └─ analytics_events

조회
  └─ GET /analytics/summary
  └─ GET /analytics/stages/dropoff
  └─ GET /analytics/stages/detail
  └─ GET /analytics/minigames/summary
  └─ GET /analytics/daily/players
  └─ GET /analytics/retention
       └─ Scalar UI (/scalar/v1)
       └─ Dashboard (/dashboard.html)
```

---

## ERD

```
players
─────────────────────────────
player_id    TEXT  PK
platform     TEXT
created_at   TIMESTAMPTZ
last_seen_at TIMESTAMPTZ

analytics_events
─────────────────────────────
id           BIGSERIAL  PK
player_id    TEXT       FK → players.player_id
session_id   TEXT
event_name   TEXT
stage_id     TEXT       (stage_* 이벤트에만 값)
payload_json JSONB      (이벤트별 가변 필드)
created_at   TIMESTAMPTZ
```

---

## 수집 이벤트

| 이벤트명 | 발생 시점 | 주요 payload 필드 |
|---|---|---|
| `session_start` | 앱 최초 실행 | `platform` |
| `session_end` | 앱 종료 / 백그라운드 전환 | `duration_sec` |
| `stage_enter` | 스테이지 진입 | — |
| `stage_clear` | 스테이지 클리어 | `score`, `star_count`, `duration_sec` |
| `stage_fail` | 스테이지 실패 | `score`, `duration_sec` |
| `minigame_result` | 미니게임 종료 | `minigame_type`, `score`, `duration_sec`, `success`, `combo_count` |

### 공통 필드 (모든 이벤트)

| 필드 | 타입 | 설명 |
|---|---|---|
| `player_id` | string | 클라이언트 최초 실행 시 생성·저장하는 GUID |
| `session_id` | string | 세션 시작 시 생성하는 GUID |
| `event_name` | string | 이벤트명 |
| `client_version` | string | `Application.version` |
| `platform` | string | `Application.platform` |
| `created_at` | datetime | UTC 기준 이벤트 발생 시각 |

---

## API

### POST `/analytics/events`

이벤트 배열을 배치 전송한다.

**Request**
```json
[
  {
    "player_id": "550e8400-e29b-41d4-a716-446655440000",
    "session_id": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
    "event_name": "stage_clear",
    "stage_id": "CH1",
    "payload": {
      "score": 1200,
      "star_count": 2,
      "duration_sec": 87.4
    },
    "client_version": "0.9.1",
    "platform": "WindowsPlayer",
    "created_at": "2026-04-12T10:23:00Z"
  }
]
```

**Response** `200 OK`
```json
{ "saved": 1 }
```

---

### GET `/analytics/summary?from=&to=`

전체 플레이어 수, 세션 수, 평균 세션 시간.  
`from`, `to` 생략 시 기본값: 최근 30일.

**Response**
```json
{
  "totalPlayers": 142,
  "totalSessions": 389,
  "avgSessionDurationSec": 312.5,
  "period": { "from": "2026-04-01", "to": "2026-04-13" }
}
```

---

### GET `/analytics/stages/dropoff?from=&to=`

스테이지별 진입·클리어·실패 통계.

**Response**
```json
[
  {
    "stageId": "CH1",
    "enterCount": 98,
    "clearCount": 91,
    "failCount": 7,
    "clearRate": 0.928,
    "avgScore": 1150,
    "avgDurationSec": 74.2
  },
  {
    "stageId": "CH3",
    "enterCount": 54,
    "clearCount": 31,
    "failCount": 23,
    "clearRate": 0.574,
    "avgScore": 870,
    "avgDurationSec": 95.8
  }
]
```

---

### GET `/analytics/minigames/summary?from=&to=`

미니게임 타입별 성과 집계.

**Response**
```json
[
  {
    "minigameType": "Unload",
    "playCount": 389,
    "successCount": 312,
    "successRate": 0.802,
    "avgScore": 1043,
    "avgDurationSec": 83.1,
    "avgComboCount": 6.4
  }
]
```

---

### GET `/analytics/daily/players?from=&to=`

날짜별 신규 플레이어 수.

**Response**
```json
[
  { "day": "2026-04-10", "newPlayers": 3 },
  { "day": "2026-04-11", "newPlayers": 7 },
  { "day": "2026-04-12", "newPlayers": 12 }
]
```

---

### GET `/analytics/stages/detail?from=&to=`

스테이지별 별점 분포 및 클리어/실패 평균 시간.

**Response**
```json
[
  {
    "stageId": "CH1",
    "star1Count": 5,
    "star2Count": 38,
    "star3Count": 48,
    "avgClearDurationSec": 74.2,
    "avgFailDurationSec": 41.8
  }
]
```

---

### GET `/analytics/retention?from=&to=`

플레이어별 세션 횟수 분포.

**Response**
```json
[
  { "sessionCount": 1, "playerCount": 54 },
  { "sessionCount": 2, "playerCount": 31 },
  { "sessionCount": 3, "playerCount": 18 }
]
```

---

## 대시보드

`/dashboard.html`에서 수집된 데이터를 시각화해 확인할 수 있다.

| 섹션 | 내용 |
|---|---|
| Overview | 총 플레이어/세션 수, 평균 세션 시간, 일별 신규 플레이어 |
| Stage Funnel | 스테이지별 진입·클리어·실패 바차트 + 클리어율·이탈률 테이블 |
| Stage Score | 별점 분포 누적 바차트, 클리어 vs 실패 평균 시간 비교 |
| Minigame | 미니게임별 성과 테이블 + 평균 점수 차트 |
| Retention | 세션 횟수 분포 바차트, 재방문 플레이어 비율 |

---

## 데이터 인사이트 예시

실제 수집 데이터를 통해 다음과 같은 인사이트를 도출할 수 있다.

### 스테이지 이탈률 분석

```
스테이지   진입   클리어   이탈률
────────   ────   ──────   ──────
CH1        98     91       7.1%   ← 튜토리얼, 이탈 낮음
CH2        87     74       14.9%
CH3        54     31       42.6%  ← 난이도 급상승 구간
CH4        28     11       60.7%  ← 이탈이 가장 높은 구간
```

CH3~CH4 구간에서 이탈률이 급격히 올라간다면 해당 스테이지의 난이도 조정을 검토할 수 있다.

### 미니게임 성과 분석

```
미니게임   플레이   성공률   평균 콤보
────────   ──────   ──────   ────────
Unload     389      80.2%    6.4
```

성공률이 낮거나 평균 콤보가 기대치보다 낮으면 미니게임 밸런스 조정의 근거로 활용할 수 있다.

### 세션 시간 분포

평균 세션 시간을 기준으로 짧은 세션(5분 미만)이 많다면 초반 이탈이 높다는 신호이고,
긴 세션(30분 이상)이 많다면 핵심 콘텐츠에서 충분한 몰입이 일어나고 있다는 의미다.

---

## 알려진 한계

| 항목 | 내용 |
|---|---|
| `session_end` 신뢰성 | 강제 종료 시 전송 보장 불가 (best-effort) |
| 인증 없음 | 포트폴리오 범위. 실서비스라면 API Key 또는 JWT 필요 |
| `player_id` 검증 없음 | 클라이언트 GUID 신뢰. 통계 목적이므로 허용 |
| Rate limiting 없음 | Railway 무료 티어 트래픽 제한으로 대체 |

보안 관련 상세 내용은 [docs/security-notes.md](docs/security-notes.md) 참고.

---

## 로컬 실행

```bash
# 환경변수 설정
export DATABASE_URL="postgresql://user:pass@host:port/db"

# 실행
cd BridgeAlien.Analytics.Api
dotnet run
```

## Railway 배포

1. GitHub 레포 연결
2. PostgreSQL 플러그인 추가
3. Variables → `DATABASE_URL = ${{Postgres.DATABASE_URL}}` 설정
4. PostgreSQL Query 탭에서 `sql/migration.sql` 실행

배포 중 발생한 트러블슈팅은 [docs/troubleshooting.md](docs/troubleshooting.md) 참고.
