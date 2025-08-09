# 🧪 Blokus Single Player API - 테스트 가이드

Unity 모바일 클라이언트를 위한 싱글플래이 API 서버 테스트 방법과 결과입니다.

## 🚀 빠른 테스트

### 1. Docker로 전체 환경 테스트

```bash
# 루트 디렉터리에서 전체 서비스 시작
cd blokus-online
docker-compose up -d

# API 서버만 테스트 (PostgreSQL, Redis 포함)
cd single-player-api
docker-compose up -d

# 헬스체크 확인
curl http://localhost:8080/api/health
```

### 2. 로컬 개발 환경 테스트

```bash
cd single-player-api

# 개발 환경 자동 설정 및 시작
npm run setup

# 또는 수동으로
npm install
npm run dev
```

### 3. 자동화된 API 테스트

```bash
# Node.js 기반 통합 테스트
npm run test:api

# Shell 스크립트 기반 테스트
npm run test:integration
# 또는
./scripts/test.sh
```

## 📋 테스트 체크리스트

### ✅ 기본 엔드포인트 테스트

| 엔드포인트 | 메서드 | 상태 | 설명 |
|-----------|--------|------|------|
| `/api/health` | GET | ✅ | 서버 상태 확인 |
| `/api/health/live` | GET | ✅ | Kubernetes Liveness |
| `/api/health/ready` | GET | ✅ | Kubernetes Readiness |
| `/api/` | GET | ✅ | API 문서 |

### 🔐 인증 테스트 (JWT 토큰 필요)

| 엔드포인트 | 메서드 | 상태 | 설명 |
|-----------|--------|------|------|
| `/api/auth/validate` | POST | 🔑 | JWT 토큰 검증 |
| `/api/auth/info` | GET | 🔑 | 토큰 정보 조회 |

### 🎮 게임 관련 API 테스트

| 엔드포인트 | 메서드 | 상태 | 설명 |
|-----------|--------|------|------|
| `/api/stages/1` | GET | 🔑 | 스테이지 데이터 조회 |
| `/api/stages/1/progress` | GET | 🔑 | 진행도 조회 |
| `/api/stages/complete` | POST | 🔑 | 스테이지 완료 보고 |
| `/api/user/profile` | GET | 🔑 | 사용자 프로필 |
| `/api/user/stats` | GET | 🔑 | 상세 통계 |

### 🛡️ 보안 테스트

| 테스트 항목 | 상태 | 결과 |
|------------|------|------|
| 무인증 접근 차단 | ✅ | 401 Unauthorized |
| 잘못된 엔드포인트 | ✅ | 404 Not Found |
| Rate Limiting | ✅ | 429 Too Many Requests |
| CORS 설정 | ✅ | `*` (Unity 호환) |

## 🔧 테스트 도구 사용법

### 1. cURL을 이용한 수동 테스트

```bash
# 헬스체크
curl -i http://localhost:8080/api/health

# 무인증 접근 테스트 (401 응답 예상)
curl -i http://localhost:8080/api/stages/1

# JWT 토큰을 사용한 인증된 요청
export JWT_TOKEN="your-jwt-token-here"
curl -i -H "Authorization: Bearer $JWT_TOKEN" \
     http://localhost:8080/api/stages/1

# 스테이지 완료 보고 (POST)
curl -i -X POST \
     -H "Authorization: Bearer $JWT_TOKEN" \
     -H "Content-Type: application/json" \
     -d '{"stage_number":1,"score":85,"completion_time":240,"completed":true}' \
     http://localhost:8080/api/stages/complete
```

### 2. Unity에서 테스트용 HTTP 요청

```csharp
// Unity C# 코드 예시
public class ApiTester : MonoBehaviour 
{
    private string apiBaseUrl = "http://localhost:8080/api";
    private string jwtToken = "your-jwt-token-here";
    
    private async void Start() 
    {
        await TestHealthCheck();
        await TestStageData();
    }
    
    private async Task TestHealthCheck() 
    {
        using (var client = new HttpClient()) 
        {
            var response = await client.GetAsync($"{apiBaseUrl}/health");
            var content = await response.Content.ReadAsStringAsync();
            Debug.Log($"Health Check: {response.StatusCode} - {content}");
        }
    }
    
    private async Task TestStageData() 
    {
        using (var client = new HttpClient()) 
        {
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {jwtToken}");
            var response = await client.GetAsync($"{apiBaseUrl}/stages/1");
            var content = await response.Content.ReadAsStringAsync();
            Debug.Log($"Stage Data: {response.StatusCode} - {content}");
        }
    }
}
```

## 📊 성능 테스트 결과

### 응답 시간 측정

```bash
# Apache Bench를 이용한 성능 테스트
ab -n 1000 -c 10 http://localhost:8080/api/health

# 예상 결과:
# - 평균 응답시간: < 50ms
# - 초당 요청수: > 200 req/sec
# - 메모리 사용량: < 50MB
```

### Redis 캐시 성능

```bash
# Redis 모니터링
docker-compose exec redis redis-cli monitor

# 캐시 히트율 확인
docker-compose exec redis redis-cli info stats | grep keyspace
```

## 🐛 문제 해결

### 일반적인 문제들

1. **서버가 시작되지 않음**
   ```bash
   # 환경변수 확인
   cat single-player-api/.env
   
   # Docker 로그 확인
   docker-compose logs blokus-single-api
   ```

2. **데이터베이스 연결 실패**
   ```bash
   # PostgreSQL 상태 확인
   docker-compose exec postgres pg_isready -U admin -d blokus_online
   
   # 데이터베이스 연결 테스트
   docker-compose exec postgres psql -U admin -d blokus_online -c "SELECT version();"
   ```

3. **Redis 캐시 문제**
   ```bash
   # Redis 연결 확인
   docker-compose exec redis redis-cli ping
   
   # 캐시 상태 확인
   docker-compose exec redis redis-cli info memory
   ```

4. **JWT 토큰 문제**
   ```bash
   # 토큰이 유효한지 온라인에서 확인
   # https://jwt.io/ 에서 토큰 디코딩
   
   # 또는 서버 로그에서 인증 오류 확인
   docker-compose logs blokus-single-api | grep -i jwt
   ```

## 🎯 Unity 클라이언트 연동 테스트

### 테스트 시나리오

1. **로그인 플로우**
   - TCP 서버에서 로그인
   - JWT 토큰 획득
   - Single Player API로 토큰 검증

2. **게임 플레이 플로우**
   - 스테이지 데이터 조회
   - 게임 시작
   - 게임 완료 보고
   - 진행도 업데이트

3. **오프라인 모드**
   - 네트워크 연결 실패 시 로컬 데이터 사용
   - 온라인 복귀 시 동기화

### Unity 테스트 코드

```csharp
public class SinglePlayerApiClient 
{
    private readonly string baseUrl = "http://localhost:8080/api";
    private readonly HttpClient httpClient;
    private string jwtToken;
    
    public SinglePlayerApiClient(string token) 
    {
        this.jwtToken = token;
        this.httpClient = new HttpClient();
        this.httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
    }
    
    public async Task<StageData> GetStageDataAsync(int stageNumber) 
    {
        var response = await httpClient.GetAsync($"{baseUrl}/stages/{stageNumber}");
        
        if (response.IsSuccessStatusCode) 
        {
            var json = await response.Content.ReadAsStringAsync();
            return JsonUtility.FromJson<StageData>(json);
        }
        
        throw new Exception($"Failed to get stage data: {response.StatusCode}");
    }
}
```

---

## ✅ 테스트 완료 체크

- [ ] Docker 환경에서 전체 서비스 실행 확인
- [ ] 헬스체크 엔드포인트 정상 동작 확인
- [ ] JWT 토큰 인증 정상 동작 확인
- [ ] 스테이지 데이터 조회 API 정상 동작 확인
- [ ] Redis 캐시 정상 동작 확인
- [ ] Unity 클라이언트 연동 테스트 완료
- [ ] 성능 테스트 결과 만족
- [ ] 오류 처리 테스트 완료

**🎉 모든 테스트가 통과하면 프로덕션 배포 준비 완료!**