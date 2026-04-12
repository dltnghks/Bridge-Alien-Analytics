# 배포 트러블슈팅

> 작성일: 2026-04-12  
> 환경: ASP.NET Core 9 / Railway / Nixpacks → Dockerfile

---

## 이슈 1 — Railway Nixpacks 빌드 실패

### 증상
```
ERROR: failed to build: failed to solve: process "/bin/bash -ol pipefail -c dotnet restore" did not complete successfully: exit code: 1
```

### 원인
`railway.toml`에 빌드 명령을 지정하지 않으면 Nixpacks가 프로젝트 루트에서 `dotnet restore`를 실행한다. 프로젝트가 서브디렉터리(`BridgeAlien.Analytics.Api/`)에 있어서 `.csproj`를 찾지 못해 실패했다.

### 시도한 해결 (실패)
`railway.toml`에 `buildCommand`와 `startCommand`를 명시했으나, Nixpacks가 자체 Dockerfile을 생성하면서 동일한 문제가 반복됐다.

```toml
# 효과 없었던 설정
[build]
builder = "nixpacks"
buildCommand = "dotnet publish BridgeAlien.Analytics.Api/BridgeAlien.Analytics.Api.csproj -c Release -o out"

[deploy]
startCommand = "dotnet out/BridgeAlien.Analytics.Api.dll"
```

### 최종 해결
Nixpacks 대신 직접 Dockerfile을 작성해 빌드를 완전히 제어했다.

```toml
# railway.toml
[build]
builder = "dockerfile"

[deploy]
healthcheckPath = "/health"
```

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY BridgeAlien.Analytics.Api/BridgeAlien.Analytics.Api.csproj ./BridgeAlien.Analytics.Api/
RUN dotnet restore ./BridgeAlien.Analytics.Api/BridgeAlien.Analytics.Api.csproj

COPY BridgeAlien.Analytics.Api/ ./BridgeAlien.Analytics.Api/
RUN dotnet publish ./BridgeAlien.Analytics.Api/BridgeAlien.Analytics.Api.csproj -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /out .
ENTRYPOINT ["dotnet", "BridgeAlien.Analytics.Api.dll"]
```

---

## 이슈 2 — 헬스체크 service unavailable

### 증상
```
Attempt #1 failed with service unavailable. Continuing to retry for 4m49s
Attempt #2 failed with service unavailable. Continuing to retry for 4m38s
...
Attempt #11 failed with service unavailable. Continuing to retry for 8s
```

### 원인
앱이 시작 직후 `TypeLoadException`으로 즉시 종료되어 Railway 헬스체크 요청에 응답하지 못했다. (이슈 3 참고)

초기에는 헬스체크 경로가 `/openapi/v1.json`으로 설정되어 있어서 응답이 느릴 수 있다고 판단했으나, 실제 원인은 앱 자체의 크래시였다.

### 해결
- `/health` 전용 엔드포인트 추가 (앱 크래시 원인 해결 후 정상 동작)
- `railway.toml` 헬스체크 경로를 `/openapi/v1.json` → `/health`로 변경

```csharp
// Program.cs
app.MapGet("/health", () => Results.Ok("healthy"));
```

---

## 이슈 3 — Microsoft.OpenApi 버전 충돌

### 증상
```
System.TypeLoadException: Could not load type 'Microsoft.OpenApi.Models.OpenApiDocument'
from assembly 'Microsoft.OpenApi, Version=2.4.1.0'
```

앱 시작 시 `MapControllers()` 호출 시점에 발생하여 즉시 종료됐다.

### 원인
ASP.NET Core 9의 내장 `AddOpenApi()` / `MapOpenApi()`는 `Microsoft.OpenApi` **2.x**를 사용한다. 반면 `Swashbuckle.AspNetCore`는 `Microsoft.OpenApi` **1.x**를 요구한다. 두 패키지가 동시에 설치되면서 버전이 충돌했다.

### 해결
`Swashbuckle.AspNetCore`를 제거하고 .NET 9에 호환되는 **Scalar.AspNetCore**로 교체했다.

```bash
dotnet remove package Swashbuckle.AspNetCore
dotnet add package Scalar.AspNetCore
```

```csharp
// Program.cs — 변경 전
app.UseSwaggerUI(c => c.SwaggerEndpoint("/openapi/v1.json", "Bridge-Alien Analytics API"));

// Program.cs — 변경 후
app.MapScalarApiReference();
```

API 문서는 `/scalar/v1`에서 확인 가능하다.

---

---

## 이슈 4 — DB 연결 문자열 미사용 (빈 문자열 우선 적용)

### 증상
```
System.InvalidOperationException: The ConnectionString property has not been initialized.
```

### 원인
`appsettings.json`에 `"Postgres": ""`가 빈 문자열로 설정되어 있었다.  
`GetConnectionString("Postgres")`가 빈 문자열을 반환하고, `??` 연산자는 빈 문자열을 null로 취급하지 않아 `DATABASE_URL` 환경변수까지 도달하지 못했다.

### 해결
`appsettings.json`에서 빈 `Postgres` 키를 제거하고, 코드에서 빈 문자열도 null처럼 처리했다.

```csharp
var pgFromConfig = builder.Configuration.GetConnectionString("Postgres");
var pgFromEnv    = Environment.GetEnvironmentVariable("DATABASE_URL");
var connectionString =
    (!string.IsNullOrWhiteSpace(pgFromConfig) ? pgFromConfig : null)
    ?? (!string.IsNullOrWhiteSpace(pgFromEnv) ? pgFromEnv : null)
    ?? throw new InvalidOperationException("PostgreSQL 연결 문자열이 설정되지 않았습니다.");
```

---

## 이슈 5 — top-level statements 빌드 에러 (로컬/Railway 환경 차이)

### 증상
```
error CS8803: Top-level statements must precede namespace and type declarations.
```

로컬 빌드는 성공했으나 Railway Docker 빌드에서만 발생했다.

### 원인
`Program.cs`에 `static class StringExtensions`를 top-level statements 앞에 선언하면서 발생했다. 이어서 `static string? NullIfEmpty` 로컬 함수를 top-level statements 중간에 선언하는 방식도 일부 빌드 환경에서 문제가 됐다.

### 해결
확장 메서드나 로컬 함수 대신 인라인 조건식으로 처리했다.

```csharp
// 해결 후
var pgFromConfig = builder.Configuration.GetConnectionString("Postgres");
var pgFromEnv    = Environment.GetEnvironmentVariable("DATABASE_URL");
var connectionString =
    (!string.IsNullOrWhiteSpace(pgFromConfig) ? pgFromConfig : null)
    ?? (!string.IsNullOrWhiteSpace(pgFromEnv) ? pgFromEnv : null)
    ?? throw new ...;
```

---

## 이슈 6 — Railway DATABASE_URL URI 형식 파싱 실패

### 증상
```
System.ArgumentException: Format of the initialization string does not conform to specification starting at index 0.
   at Npgsql.NpgsqlConnectionStringBuilder..ctor(String connectionString)
```

### 원인
Railway가 제공하는 `DATABASE_URL`은 `postgresql://user:pass@host:port/db` URI 형식이다.  
Npgsql 10의 `NpgsqlConnection` 생성자와 `NpgsqlConnectionStringBuilder` 생성자는 이 URI 형식을 직접 받지 못한다.

### 해결
`System.Uri`로 직접 파싱해 Npgsql 키-값 형식 연결 문자열로 변환했다.

```csharp
private static string ToNpgsqlString(string cs)
{
    if (!cs.StartsWith("postgresql://") && !cs.StartsWith("postgres://"))
        return cs;

    var uri      = new Uri(cs);
    var userInfo = uri.UserInfo.Split(':');
    return new NpgsqlConnectionStringBuilder
    {
        Host                   = uri.Host,
        Port                   = uri.Port > 0 ? uri.Port : 5432,
        Database               = uri.AbsolutePath.TrimStart('/'),
        Username               = userInfo[0],
        Password               = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "",
        SslMode                = SslMode.Require,
        TrustServerCertificate = true
    }.ConnectionString;
}
```

---

## 이슈 7 — 조회 API 기본 날짜 범위에서 이벤트 누락

### 증상
`GET /analytics/summary` 파라미터 없이 호출 시 데이터가 있음에도 0 반환.

### 원인
두 가지 원인이 복합적으로 작용했다.

1. **summary 쿼리 구조 문제**: `FROM analytics_events WHERE event_name = 'session_end'`를 외부 FROM으로 사용해 `session_end` 이벤트가 없으면 전체 쿼리가 0행을 반환했다.
2. **기본 `to` 값 문제**: 기본값이 `DateTime.UtcNow`라서 Unity 클라이언트가 보내는 이벤트의 `created_at`이 서버 현재 시각보다 미래이면 BETWEEN 범위에서 누락됐다.

### 해결

**1. summary 쿼리 — 항상 1행 반환하도록 재작성**

```sql
-- 변경 전: session_end가 없으면 0행
SELECT ..., AVG(...)
FROM analytics_events WHERE event_name = 'session_end' AND ...

-- 변경 후: 스칼라 서브쿼리로 분리해 항상 1행
SELECT
    (SELECT COUNT(DISTINCT player_id) FROM analytics_events WHERE ...) AS total_players,
    (SELECT COUNT(DISTINCT session_id) FROM analytics_events WHERE event_name = 'session_start' AND ...) AS total_sessions,
    (SELECT COALESCE(AVG((payload_json->>'duration_sec')::numeric), 0) FROM analytics_events WHERE event_name = 'session_end' AND ...) AS avg_session_duration_sec
```

**2. 기본 `to` 값을 내일로 변경**

```csharp
// 변경 전
var t = to ?? DateTime.UtcNow;

// 변경 후
var t = to ?? DateTime.UtcNow.AddDays(1);
```

---

---

## 이슈 8 — DateOnly / DateTime 타입 불일치

### 증상
```
Microsoft.CSharp.RuntimeBinder.RuntimeBinderException: Cannot convert type 'System.DateOnly' to 'System.DateTime'
   at AnalyticsRepository.GetDailyNewPlayersAsync
```

`GET /analytics/daily/players` 호출 시 500 에러 발생. 다른 엔드포인트는 정상.

### 원인
`date_trunc('day', ...)::date`로 캐스팅하면 Npgsql이 PostgreSQL `date` 타입을 C# `DateOnly`로 매핑한다.  
코드에서 `(DateTime)r.day`로 캐스팅하려 하자 런타임 바인딩 오류가 발생했다.

### 해결
SQL에서 `::date` 캐스팅을 제거해 `TIMESTAMPTZ`로 반환하도록 변경했다.  
Npgsql은 `TIMESTAMPTZ`를 `DateTime`으로 매핑하므로 기존 코드와 호환된다.

```sql
-- 변경 전
date_trunc('day', first_seen)::date AS day
GROUP BY date_trunc('day', first_seen)::date

-- 변경 후
date_trunc('day', first_seen) AS day
GROUP BY date_trunc('day', first_seen)
```

---

## 요약

| # | 이슈 | 원인 | 해결 |
|---|---|---|---|
| 1 | Nixpacks `dotnet restore` 실패 | 서브디렉터리 구조 미인식 | Dockerfile 직접 작성 |
| 2 | 헬스체크 `service unavailable` | 앱 크래시로 응답 불가 | 이슈 3 해결 후 자동 해소 |
| 3 | `TypeLoadException` 앱 크래시 | Swashbuckle ↔ .NET 9 OpenAPI 버전 충돌 | Swashbuckle → Scalar 교체 |
| 4 | DB 연결 문자열 미사용 | 빈 문자열이 `??` 통과 | 빈 문자열 명시적 처리 |
| 5 | top-level statements 빌드 에러 | static 선언 위치 문제 | 인라인 조건식으로 대체 |
| 6 | DATABASE_URL URI 파싱 실패 | Npgsql이 URI 형식 미지원 | `System.Uri`로 직접 변환 |
| 7 | 조회 API 이벤트 누락 | 쿼리 구조 + 날짜 범위 문제 | 쿼리 재작성 + 기본 to 조정 |
| 8 | `DateOnly` → `DateTime` 타입 오류 | `::date` 캐스팅으로 타입 불일치 | `::date` 제거해 TIMESTAMPTZ 유지 |
