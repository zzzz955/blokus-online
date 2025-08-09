# 🎮 싱글플레이 확장 스키마 제안

## 📊 현재 스키마 분석
- **user_stats**: 멀티플레이어 통계 (승/패/무, 점수)
- **users**: 사용자 기본 정보
- **user_settings**: 환경 설정

## 🎯 최적화 전략: **기존 구조 활용 + 최소 확장**

### 1. user_stats 테이블 확장 (권장)
```sql
ALTER TABLE user_stats ADD COLUMN IF NOT EXISTS single_player_level INTEGER DEFAULT 1;
ALTER TABLE user_stats ADD COLUMN IF NOT EXISTS max_stage_completed INTEGER DEFAULT 0;
ALTER TABLE user_stats ADD COLUMN IF NOT EXISTS total_single_games INTEGER DEFAULT 0;
ALTER TABLE user_stats ADD COLUMN IF NOT EXISTS single_player_score BIGINT DEFAULT 0;
```

**장점:**
- ✅ 기존 멀티플레이어 통계 유지
- ✅ 단일 테이블에서 모든 통계 관리
- ✅ 기존 코드 호환성 유지
- ✅ 레벨 시스템을 싱글/멀티 분리 가능

### 2. stages 테이블 (스테이지 마스터 데이터)
```sql
CREATE TABLE stages (
    stage_id SERIAL PRIMARY KEY,
    stage_number INTEGER NOT NULL UNIQUE,
    difficulty INTEGER CHECK (difficulty >= 1 AND difficulty <= 10),
    
    -- 게임 데이터 (JSONB로 구조화)
    initial_board_state JSONB DEFAULT NULL, -- 미리 배치된 블록들 위치/색상
    available_blocks INTEGER[] NOT NULL,    -- 사용 가능한 블록 타입 ID 배열
    
    -- 점수 시스템
    optimal_score INTEGER NOT NULL,        -- 이론적 최대 점수 (별점 계산 기준)
    
    -- 게임 설정
    time_limit INTEGER DEFAULT NULL,       -- 제한시간(초), NULL이면 무제한
    max_undo_count INTEGER DEFAULT 3,     -- 최대 되돌리기 횟수
    
    -- 메타 데이터
    stage_description TEXT DEFAULT NULL,
    stage_hints TEXT DEFAULT NULL,        -- 힌트 텍스트
    
    -- 운영
    is_active BOOLEAN DEFAULT true,
    is_featured BOOLEAN DEFAULT false,    -- 추천 스테이지
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- 인덱스
CREATE INDEX idx_stages_number ON stages(stage_number);
CREATE INDEX idx_stages_difficulty ON stages(difficulty);
CREATE INDEX idx_stages_featured ON stages(is_featured) WHERE is_featured = true;
```

### 3. user_stage_progress 테이블 (플레이어별 진행도)
```sql
CREATE TABLE user_stage_progress (
    user_id INTEGER REFERENCES users(user_id) ON DELETE CASCADE,
    stage_id INTEGER REFERENCES stages(stage_id) ON DELETE CASCADE,
    
    -- 진행 상태
    is_completed BOOLEAN DEFAULT false,
    stars_earned INTEGER DEFAULT 0 CHECK (stars_earned >= 0 AND stars_earned <= 3),
    
    -- 플레이 기록
    best_score INTEGER DEFAULT 0,
    best_completion_time INTEGER DEFAULT NULL, -- 최단 클리어 시간 (초)
    total_attempts INTEGER DEFAULT 0,
    successful_attempts INTEGER DEFAULT 0,
    
    -- 타임스탬프
    first_played_at TIMESTAMPTZ DEFAULT NOW(),
    first_completed_at TIMESTAMPTZ DEFAULT NULL,
    last_played_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW(),    
    PRIMARY KEY (user_id, stage_id)
);

-- 핵심 인덱스
CREATE INDEX idx_user_stage_progress_user_id ON user_stage_progress(user_id);
CREATE INDEX idx_user_stage_progress_stage_id ON user_stage_progress(stage_id);
CREATE INDEX idx_user_stage_progress_completed ON user_stage_progress(is_completed) WHERE is_completed = true;
CREATE INDEX idx_user_stage_progress_stars ON user_stage_progress(stars_earned) WHERE stars_earned > 0;
```

### 4. 성능 최적화 전략

#### 데이터 생성 전략
```sql
-- 🚀 Lazy Loading 방식 (권장)
-- 유저가 실제로 스테이지에 접근할 때만 레코드 생성
-- 신규 유저당 즉시 1,000개 레코드 생성하지 않음

-- 스테이지 플레이시 자동 생성하는 함수 (언락 체크는 클라이언트에서)
CREATE OR REPLACE FUNCTION ensure_user_stage_progress(p_user_id INTEGER, p_stage_id INTEGER)
RETURNS void AS $$
BEGIN
    INSERT INTO user_stage_progress (user_id, stage_id)
    VALUES (p_user_id, p_stage_id)
    ON CONFLICT (user_id, stage_id) DO NOTHING;
END;
$$ LANGUAGE plpgsql;
```

#### 캐싱 전략
```sql
-- 🗃️ stages 테이블은 Redis 캐싱 (변경 빈도 낮음)
-- Key: "stage:{stage_number}" 
-- Value: JSON serialized stage data
-- TTL: 24시간

-- 유저 진행도는 세션별 캐싱
-- Key: "user_progress:{user_id}"
-- Value: 모든 스테이지 진행도 JSON
-- TTL: 1시간
```

#### 파티셔닝 전략 (미래 대비)
```sql
-- user_id 기반 Hash 파티셔닝 (1000만 레코드 이상시)
CREATE TABLE user_stage_progress_partitioned (
    LIKE user_stage_progress INCLUDING ALL
) PARTITION BY HASH (user_id);

-- 4개 파티션으로 분할
CREATE TABLE user_stage_progress_p0 PARTITION OF user_stage_progress_partitioned
    FOR VALUES WITH (modulus 4, remainder 0);
-- ... p1, p2, p3
```

## 🔄 기존 vs 새로운 접근

### 기존 멀티플레이어 데이터 (유지)
```sql
-- user_stats 기존 컬럼들
total_games, wins, losses, draws    -- 멀티플레이어 게임
best_score, total_score            -- 멀티플레이어 점수
level, experience_points           -- 통합 레벨 (또는 멀티 전용)
```

### 새로운 싱글플레이어 데이터 (추가)
```sql
-- user_stats 새 컬럼들
single_player_level               -- 싱글플레이어 전용 레벨
max_stage_completed              -- 최고 클리어 스테이지
total_single_games               -- 싱글플레이어 게임 수
single_player_score              -- 싱글플레이어 총 점수
single_best_stage_score          -- 단일 스테이지 최고 점수

-- single_stage_progress (완전 새로운 테이블)
개별 스테이지별 진행 상황
```

## 💡 레벨 시스템 설계 옵션

### 옵션 1: 통합 레벨 시스템 (단순)
- `level`: 싱글+멀티 통합 경험치 기반
- `experience_points`: 싱글(스테이지 클리어) + 멀티(게임 승리) 합산

### 옵션 2: 분리 레벨 시스템 (권장)
- `level`: 멀티플레이어 레벨 (승률 기반)
- `single_player_level`: 싱글플레이어 레벨 (스테이지 진행 기반)

## 🎮 Unity 클라이언트 연동

### StageManager ScriptableObject
```csharp
[System.Serializable]
public class StageData 
{
    public int stageNumber;
    public string stageName;
    public int difficulty;
    public int optimalScore;
    public int threeStar, twoStar, oneStar;
    public int timeLimit; // 0 = 무제한
    public List<BlockType> availableBlocks;
    public string stageDescription;
}
```

### 서버 통신 메시지 확장
```
// 싱글플레이어 전용 메시지들
SINGLE_STAGE_PROGRESS_REQUEST:stage_number
SINGLE_STAGE_COMPLETE:stage_number:score:stars:time
SINGLE_STATS_UPDATE:level:max_stage:total_games:total_score
```

## 🔧 Migration 스크립트
```sql
-- 1단계: user_stats 확장
ALTER TABLE user_stats 
ADD COLUMN single_player_level INTEGER DEFAULT 1,
ADD COLUMN max_stage_completed INTEGER DEFAULT 0,
ADD COLUMN total_single_games INTEGER DEFAULT 0,
ADD COLUMN single_player_score BIGINT DEFAULT 0,

-- 2단계: 제약조건 추가
ALTER TABLE user_stats 
ADD CONSTRAINT check_single_player_level CHECK (single_player_level >= 1 AND single_player_level <= 100),
ADD CONSTRAINT check_max_stage_completed CHECK (max_stage_completed >= 0),
ADD CONSTRAINT check_total_single_games CHECK (total_single_games >= 0),
ADD CONSTRAINT check_single_player_score CHECK (single_player_score >= 0)

-- 3단계: 새 테이블들 생성
-- (위의 CREATE TABLE 문들)
```

## 📈 최종 권장 사항

### 🎯 **RDBMS 선택 이유**
1. **현재 규모 적합**: 1,000만 레코드 × 70 bytes = 700MB (충분히 처리 가능)
2. **기존 인프라 활용**: PostgreSQL + 기존 백업/복구 시스템
3. **데이터 일관성**: ACID 보장으로 게임 진행도 정확성 확보
4. **복합 쿼리 지원**: 유저 랭킹, 통계, 분석 쿼리 용이
5. **JSONB 지원**: 반구조화 데이터 (보드 상태, 설정) 효율 저장

### 🏗️ **스키마 설계 전략**
1. **stages 테이블**: 스테이지 마스터 데이터 (JSONB로 복잡한 구조 저장)
2. **user_stage_progress**: 플레이어별 진행도 (복합 PK로 관계 관리)
3. **Lazy Loading**: 실제 접근시에만 레코드 생성 (성능 최적화)
4. **캐싱 전략**: Redis로 자주 조회되는 데이터 캐싱
5. **분리된 레벨 시스템**: 싱글/멀티 독립적 성장

### 💡 **핵심 이점**
✅ **최소한의 변경**: 기존 멀티플레이어 시스템 완전 호환  
✅ **확장성**: 파티셔닝으로 대용량 확장 가능  
✅ **운영 편의성**: 기존 DBA 스킬셋과 도구 활용  
✅ **개발 생산성**: 팀이 익숙한 SQL과 ORM 활용  
✅ **비용 효율성**: 새로운 인프라 구축 불필요  

**결론: PostgreSQL + JSONB + 적절한 인덱싱으로 충분히 처리 가능하며, 가장 안전하고 효율적인 선택입니다!** 🚀