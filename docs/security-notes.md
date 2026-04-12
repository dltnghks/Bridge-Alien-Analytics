# 보안 고려사항

> 작성일: 2026-04-12  
> 범위: Bridge-Alien Analytics Backend 포트폴리오 배포 기준

---

## 1. 인증 없는 쓰기 엔드포인트

**대상 API:** `POST /analytics/events`

**문제**  
인증 수단이 없어 URL을 아는 누구든 임의의 데이터를 삽입할 수 있다.  
Railway 무료 티어 PostgreSQL은 용량 제한(500MB)이 있어 스팸 삽입 시 서비스가 중단될 수 있다.

**현재 결정**  
포트폴리오 범위에서 미적용. 실서비스 전환 시 아래 중 하나를 적용한다.

- Unity 클라이언트에 고정 API Key를 심고 `X-Api-Key` 헤더로 검증
- JWT 발급 후 Bearer 토큰으로 검증

---

## 2. TrustServerCertificate 설정

**위치:** `AnalyticsRepository.cs` — `Normalize()` 메서드

**문제**  
Railway `DATABASE_URL`을 Npgsql 연결 문자열로 변환할 때 `TrustServerCertificate = true`를 설정하면 서버 인증서 검증을 건너뛴다. 중간자 공격(MITM)에 이론적으로 취약해진다.

**현재 결정**  
Railway 내부 네트워크 환경에서는 실질적 위험이 낮아 현상 유지.  
실서비스 전환 시 `TrustServerCertificate`를 제거하고 Railway가 제공하는 CA 인증서를 명시적으로 신뢰하도록 변경한다.

---

## 3. 배치 요청 크기 제한 없음

**대상 API:** `POST /analytics/events`

**문제**  
단일 요청에 포함할 수 있는 이벤트 수와 페이로드 크기에 제한이 없다.  
대형 요청으로 서버 메모리를 압박하거나 DB 처리 시간을 늘릴 수 있다.

**현재 결정**  
포트폴리오 범위에서 미적용.  
실서비스 전환 시 다음을 적용한다.

- 이벤트 배열 최대 100개 제한 (컨트롤러 유효성 검사)
- ASP.NET Core `MaxRequestBodySize` 설정으로 요청 크기 상한 설정

---

## 4. Swagger UI 프로덕션 노출

**문제**  
현재 `app.MapOpenApi()`가 환경 구분 없이 항상 실행된다.  
프로덕션 배포 후에도 `/swagger`에서 API 스펙이 외부에 공개된다.

**현재 결정**  
포트폴리오 특성상 Swagger를 통한 API 확인이 목적이므로 의도적으로 유지.  
실서비스 전환 시 `IsDevelopment()` 조건으로 제한하거나 별도 인증을 적용한다.

---

## 5. player_id 검증 없음

**문제**  
`player_id`는 클라이언트가 생성한 GUID를 그대로 신뢰한다.  
악의적 클라이언트가 타 플레이어의 `player_id`를 사용해 통계를 오염시킬 수 있다.

**현재 결정**  
통계 수집 목적의 포트폴리오이므로 허용.  
실서비스 전환 시 서버가 `player_id`를 발급하고 세션 토큰으로 검증하는 구조로 변경한다.

---

## 6. Rate Limiting 없음

**문제**  
동일 IP에서 반복 요청을 제한하는 수단이 없다.

**현재 결정**  
Railway 무료 티어의 트래픽 제한으로 대체.  
실서비스 전환 시 ASP.NET Core `RateLimiter` 미들웨어를 적용한다.

---

## 요약

| # | 항목 | 현재 상태 | 실서비스 전환 시 |
|---|---|---|---|
| 1 | API 인증 | 미적용 | API Key 또는 JWT |
| 2 | TrustServerCertificate | true | 제거 후 CA 명시 |
| 3 | 배치 크기 제한 | 미적용 | 최대 100개 + Body 크기 제한 |
| 4 | Swagger 노출 | 항상 공개 | Development 환경 한정 |
| 5 | player_id 검증 | 미적용 | 서버 발급 + 세션 토큰 |
| 6 | Rate Limiting | 미적용 | ASP.NET Core RateLimiter |
