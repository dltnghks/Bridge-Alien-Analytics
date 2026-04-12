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

## 요약

| # | 이슈 | 원인 | 해결 |
|---|---|---|---|
| 1 | Nixpacks `dotnet restore` 실패 | 서브디렉터리 구조 미인식 | Dockerfile 직접 작성 |
| 2 | 헬스체크 `service unavailable` | 앱 크래시로 응답 불가 | 이슈 3 해결 후 자동 해소 |
| 3 | `TypeLoadException` 앱 크래시 | Swashbuckle ↔ .NET 9 OpenAPI 버전 충돌 | Swashbuckle → Scalar 교체 |
