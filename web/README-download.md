# 클라이언트 다운로드 시스템

## 개요
Blokus Online 웹사이트에서 클라이언트 최신 버전을 다운로드할 수 있는 시스템입니다.

## 디렉토리 구조
```
web/
├── public/
│   └── downloads/
│       ├── BlokusClient-latest.zip    # 클라이언트 파일
│       ├── version.json               # 버전 정보 (자동 생성)
│       ├── download-stats.json        # 다운로드 통계 (자동 생성)
│       └── download-logs.json         # 다운로드 로그 (자동 생성)
└── src/app/api/download/
    ├── client/route.ts               # 클라이언트 다운로드 API
    ├── version/route.ts              # 버전 정보 API
    └── stats/route.ts                # 다운로드 통계 API
```

## API 엔드포인트

### 1. 클라이언트 다운로드
- **GET** `/api/download/client`
  - 클라이언트 zip 파일 직접 다운로드
  - 자동으로 다운로드 로깅 및 통계 업데이트

- **POST** `/api/download/client`
  - 클라이언트 파일 정보 조회
  - 응답: 파일 존재 여부, 크기, 수정일 등

### 2. 버전 정보
- **GET** `/api/download/version`
  - 현재 클라이언트 버전 정보 조회
  - 응답: 버전, 릴리즈 날짜, 변경사항 등

- **POST** `/api/download/version`
  - 관리자용 버전 정보 업데이트
  - 환경변수 `ADMIN_API_KEY` 필요

### 3. 다운로드 통계
- **GET** `/api/download/stats`
  - 다운로드 통계 조회
  - 전체/오늘 다운로드 수, 인기 시간대 등

- **POST** `/api/download/stats`
  - 내부용 통계 업데이트 API
  - 헤더 `x-internal-api-key: internal-stats-update` 필요

- **PUT** `/api/download/stats`
  - 관리자용 상세 로그 조회
  - 환경변수 `ADMIN_API_KEY` 필요

## 사용 방법

### 클라이언트 업데이트 절차
1. 새 클라이언트 빌드 완료
2. `web/public/downloads/BlokusClient-latest.zip` 파일 교체
3. (선택) 버전 정보 업데이트:
   ```bash
   curl -X POST http://your-domain/api/download/version \
     -H "Content-Type: application/json" \
     -d '{
       "version": "1.1.0",
       "changelog": ["새로운 기능 추가", "버그 수정"],
       "adminKey": "your-admin-key"
     }'
   ```

### 환경변수 설정
```env
# .env.local 파일에 추가
ADMIN_API_KEY=your-secure-admin-key
INTERNAL_API_KEY=internal-stats-update
NEXTAUTH_URL=http://localhost:3000  # 프로덕션에서는 실제 도메인
```

### 프론트엔드에서 다운로드 버튼 구현 예시
```typescript
// 다운로드 버튼 컴포넌트
const DownloadButton = () => {
  const [isDownloading, setIsDownloading] = useState(false);
  const [clientInfo, setClientInfo] = useState(null);

  useEffect(() => {
    // 클라이언트 정보 조회
    fetch('/api/download/client', { method: 'POST' })
      .then(res => res.json())
      .then(setClientInfo);
  }, []);

  const handleDownload = () => {
    setIsDownloading(true);
    
    // 다운로드 링크로 이동
    window.location.href = '/api/download/client';
    
    // 잠시 후 로딩 상태 해제
    setTimeout(() => setIsDownloading(false), 2000);
  };

  return (
    <button 
      onClick={handleDownload}
      disabled={isDownloading || !clientInfo?.available}
    >
      {isDownloading ? '다운로드 중...' : '클라이언트 다운로드'}
    </button>
  );
};
```

## 보안 고려사항
- 관리자 API는 강력한 인증 키 사용
- 다운로드 로그에서 민감한 정보 제외
- 파일 크기 제한 (현재 제한 없음)
- Rate limiting 고려 (현재 미구현)

## 모니터링
- 다운로드 통계는 `/api/download/stats`에서 확인
- 서버 로그에 모든 다운로드 기록
- 로그 파일은 최근 1000개 항목으로 제한

## 확장 가능성
- 다중 버전 지원 (베타, 스테이블 등)
- 델타 업데이트 시스템
- 토렌트 기반 다운로드
- CDN 연동