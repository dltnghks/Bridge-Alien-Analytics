# Bridge-Alien Analytics Backend — 구현 계획서

> 작성일: 2026-04-12  
> 목적: 포트폴리오용 플레이어 행동 통계 백엔드 구현  
> 범위: 통계 수집·저장·조회만 (로그인, 업적, 클라우드 저장 제외)

---

## 1. 프로젝트 개요

Unity 게임 Bridge-Alien에서 발생하는 플레이 이벤트를 백엔드로 수집하고,
스테이지 이탈률·미니게임 성과 등 행동 통계를 조회할 수 있는 API를 제공한다.

### 기술 스택

| 영역 | 선택 | 이유 |
|---|---|---|
| 서버 | ASP.NET Core 8 Web API | Unity/C#와 동일 언어권, 포트폴리오 연결성 |
| DB | PostgreSQL | 집계 쿼리 성능, 무료 배포 옵션 다수 |
| ORM | Dapper | 집계 SQL을 직접 작성해야 하는 구조에 적합 |
| 배포 | Railway | .NET 지원, 무료 티어, PostgreSQL 플러그인 내장 |
| 문서 | Swagger (Swashbuckle) | API 응답 샘플 시각화, 별도 문서 없이 확인 가능 |

---

## 2. 수집 이벤트 정의

총 6개. 과하지 않게 핵심만.

| 이벤트명 | 발생 지점 (Unity 코드) | 주요 payload 필드 |
|---|---|---|
| `session_start` | `Managers.cs` — `Init()` 최초 호출 시 | `platform`, `client_version` |
| `session_end` | `Application.OnApplicationQuit` / `OnApplicationPause` | `duration_sec` |
| `stage_enter` | `StageManager.StartStage()` — 씬 전환 직전 | `stage_id` |
| `stage_clear` | `StageManager.EndStage()` — `starCount > 0` 분기 | `stage_id`, `score`, `star_count`, `duration_sec` |
| `stage_fail` | `StageManager.EndStage()` — `starCount == 0` 분기 | `stage_id`, `score`, `duration_sec` |
| `minigame_result` | `MiniGameManager.EndGame()` 이후 결과 확정 시점 | `minigame_type`, `score`, `duration_sec`, `success`, `combo_count` |

### 공통 필드 (모든 이벤트에 포함)

```
player_id       string    클라이언트 최초 실행 시 생성·저장하는 GUID
session_id      string    세션 시작 시 생성하는 GUID, 세션 종료까지 유지
event_name      string    위 표의 이벤트명
created_at      datetime  UTC 기준 이벤트 발생 시각
client_version  string    Application.version
platform        string    Application.platform (Android / WindowsPlayer 등)
```

---

## 3. 아키텍처 흐름

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

관리자 / 확인
  └─ GET /analytics/summary
  └─ GET /analytics/stages/dropoff
  └─ GET /analytics/minigames/summary
       └─ Swagger UI
```

---

## 4. DB 설계

### 4-1. `players`

```sql
CREATE TABLE players (
    player_id    TEXT        PRIMARY KEY,  -- 클라이언트 생성 GUID
    platform     TEXT,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT now(),
    last_seen_at TIMESTAMPTZ
);
```

### 4-2. `analytics_events`

```sql
CREATE TABLE analytics_events (
    id           BIGSERIAL   PRIMARY KEY,
    player_id    TEXT        NOT NULL REFERENCES players(player_id),
    session_id   TEXT        NOT NULL,
    event_name   TEXT        NOT NULL,
    stage_id     TEXT,                    -- stage_enter / stage_clear / stage_fail 에만 값
    payload_json JSONB,                   -- 이벤트별 추가 필드 (score, duration_sec 등)
    created_at   TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_events_event_name   ON analytics_events(event_name);
CREATE INDEX idx_events_stage_id     ON analytics_events(stage_id);
CREATE INDEX idx_events_created_at   ON analytics_events(created_at);
```

> 처음부터 정규화하지 않는다. 공통 컬럼은 고정 컬럼으로, 이벤트별 가변 필드는 `payload_json`으로 처리.

---

## 5. API 설계

### POST `/analytics/events`

Unity가 이벤트를 배열로 배치 전송.

**Request body**
```json
[
  {
    "player_id": "uuid",
    "session_id": "uuid",
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

**Response** `200 OK` / `400 Bad Request`

---

### GET `/analytics/summary?from=&to=`

총 플레이어 수, 총 세션 수, 평균 세션 시간.

**Response**
```json
{
  "total_players": 142,
  "total_sessions": 389,
  "avg_session_duration_sec": 312.5,
  "period": { "from": "2026-04-01", "to": "2026-04-12" }
}
```

---

### GET `/analytics/stages/dropoff?from=&to=`

스테이지 진입 대비 클리어/실패 비율.

**Response**
```json
[
  {
    "stage_id": "CH1",
    "enter_count": 98,
    "clear_count": 91,
    "fail_count": 7,
    "clear_rate": 0.928,
    "avg_score": 1150,
    "avg_duration_sec": 74.2
  },
  {
    "stage_id": "CH3",
    "enter_count": 54,
    "clear_count": 31,
    "fail_count": 23,
    "clear_rate": 0.574,
    "avg_score": 870,
    "avg_duration_sec": 95.8
  }
]
```

---

### GET `/analytics/minigames/summary?from=&to=`

미니게임 타입별 성과 집계. (현재 `Unload` 1종, 이후 확장 대비)

**Response**
```json
[
  {
    "minigame_type": "Unload",
    "play_count": 389,
    "success_count": 312,
    "success_rate": 0.802,
    "avg_score": 1043,
    "avg_duration_sec": 83.1,
    "avg_combo_count": 6.4
  }
]
```

---

## 6. Unity 연동 계획

### 추가 파일

```
Assets/03.Scripts/Managers/Backend/
  ├─ AnalyticsEvent.cs       -- 이벤트 데이터 구조체
  ├─ AnalyticsService.cs     -- Track() 진입점, player_id/session_id 관리
  └─ AnalyticsHttpClient.cs  -- 배치 HTTP 전송, 실패 시 재시도
```

### 이벤트 주입 위치

| 이벤트 | 파일 | 메서드 | 비고 |
|---|---|---|---|
| `session_start` | `Managers.cs` | `Init()` | `AnalyticsService` 초기화 시 함께 전송 |
| `session_end` | `Managers.cs` | `OnApplicationQuit` / `OnApplicationPause` | best-effort 전송 |
| `stage_enter` | `StageManager.cs` | `StartStage()` — 씬 전환 직전 | `_currentStageType` 사용 |
| `stage_clear` | `StageManager.cs` | `EndStage()` — `starCount > 0` 분기 | `starCount`, 소요 시간 포함 |
| `stage_fail` | `StageManager.cs` | `EndStage()` — `starCount == 0` 분기 | |
| `minigame_result` | `MiniGameManager.cs` | `EndGame()` 이후 | 점수·콤보·소요 시간 포함 |

### 전송 방식

- 이벤트 발생 시 즉시 전송하지 않고 인메모리 큐에 적재
- 씬 전환 시 또는 30초 주기로 배치 전송
- 전송 실패 시 큐에 유지 후 다음 기회에 재전송
- `session_end`만 `OnApplicationQuit`에서 즉시 전송 시도

---

## 7. 구현 단계

### Phase 1 — 백엔드 기반

- [ ] ASP.NET Core Web API 프로젝트 생성
- [ ] Railway PostgreSQL 연결 및 마이그레이션 (테이블 2개)
- [ ] `POST /analytics/events` 구현 (Dapper, upsert players + insert events)
- [ ] Swagger 연결 및 동작 확인

### Phase 2 — Unity 연동

- [ ] `AnalyticsEvent.cs` — 이벤트 DTO 정의
- [ ] `AnalyticsService.cs` — `player_id` GUID 생성/저장, `session_id` 관리, `Track()` 구현
- [ ] `AnalyticsHttpClient.cs` — 배치 전송, 재시도 로직
- [ ] `StageManager` / `MiniGameManager` 이벤트 주입 (3개 먼저: `stage_enter`, `stage_clear`, `minigame_result`)
- [ ] 나머지 이벤트 주입 (`session_start`, `session_end`, `stage_fail`)

### Phase 3 — 조회 API

- [ ] `GET /analytics/summary`
- [ ] `GET /analytics/stages/dropoff`
- [ ] `GET /analytics/minigames/summary`
- [ ] 날짜 범위 필터(`from`, `to`) 적용

### Phase 4 — 문서화

- [ ] README — 아키텍처 다이어그램, 이벤트 흐름 설명
- [ ] ERD 이미지 추가
- [ ] 샘플 요청/응답 스니펫
- [ ] 실제 수집 데이터 기반 인사이트 예시 (스테이지 이탈률 등)

---

## 8. 알려진 한계 및 설계 결정

| 항목 | 결정 | 이유 |
|---|---|---|
| `session_end` 신뢰성 | best-effort 전송 | 강제 종료 시 전송 보장 불가. README에 명시 |
| 인증 없음 | API key 미적용 | 포트폴리오 범위. 실서비스라면 API key 또는 JWT 필요 |
| `player_id` 검증 없음 | 클라이언트 GUID 신뢰 | 변조 가능하나 통계 목적이므로 허용 |
| 미니게임 1종 | `minigame_type` 컬럼 유지 | `Delivery` 타입 추가 대비 |
| Rate limiting 없음 | 미적용 | Railway 무료 티어 트래픽 제한으로 대체 |
