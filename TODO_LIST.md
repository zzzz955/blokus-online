# Unity 모바일 클라이언트 멀티플레이 기능 구현 TODO

## 프로젝트 개요
Unity 기반 모바일 클라이언트에서 stub으로 구현된 멀티플레이 기능을 완전히 구현하여 TCP 소켓 서버와 연동하는 프로젝트

## 🔍 현재 상황 분석

### ✅ 확인된 현재 상태
- **로그인 시스템**: OIDC 기반 인증 완료 구현됨
- **멀티플레이 버튼**: `ModeSelectionPanel`에 존재하지만 `interactable = false`로 비활성화
- **네트워킹 스텁들**: `NetworkClient`, `MessageHandler`, `NetworkManager` 모두 주석처리된 완전한 스텁
- **MultiGameplaySceneStub**: 기본 틀만 있고 3초 후 메인씬 복귀
- **서버 프로토콜**: 텍스트 기반 콜론(:) 구분자, 잘 정의된 메시지 규격

### 📋 참조 구현
- **Qt 클라이언트**: `client/src/LobbyWindow.cpp`, `client/src/GameRoomWindow.cpp` 
- **서버 프로토콜**: `prompt.txt` 파일에 상세 분석 완료
- **싱글플레이 구현**: 완전히 구현된 SingleCore/SingleGameplayScene 구조

---

## 🚀 구현 단계별 TODO 리스트

### 📌 1단계: 기초 네트워킹 인프라
**목표**: TCP 소켓 연결과 기본 메시지 핸들링 구현

#### 1.1 NetworkClient 구현 복구 및 수정
- [✔️] **NetworkClient.cs 주석 해제 및 Unity 호환성 수정**
  - `System.Net.Sockets.TcpClient` 사용
  - UTF-8 + 개행문자(\n) 프레이밍 구현
  - Thread 기반 수신처리를 Unity 메인스레드 디스패처로 수정
  - 연결 상태 관리 및 재연결 로직 구현

#### 1.2 MessageHandler 구현 복구 및 서버 프로토콜 매핑
- [ ] **MessageHandler.cs 주석 해제 및 프로토콜 구현**
  - 콜론(:) 구분자 기반 메시지 파싱
  - 클라이언트→서버: 소문자 명령 (예: `auth:username:password`)
  - 서버→클라이언트: 대문자/스네이크케이스 (예: `AUTH_SUCCESS:...`)
  - JSON 페이로드 처리 (GAME_STATE_UPDATE, BLOCK_PLACED 등)

#### 1.3 NetworkManager 파사드 패턴 구현
- [✔️] **NetworkManager.cs 통합 관리 클래스 구현**
  - NetworkClient와 MessageHandler 통합 관리
  - UI 레이어에서 쉽게 사용할 수 있는 API 제공
  - 자동 재연결 및 하트비트 시스템
  - 연결 상태 이벤트 시스템

### 📌 2단계: 인증 시스템 연동
**목표**: OIDC 인증과 TCP 서버 연결 연동

#### 2.1 멀티플레이 버튼 활성화 및 연결 플로우
- [✔️] **ModeSelectionPanel.cs 수정**
  - `multiPlayerButton.interactable = true`로 변경
  - 멀티플레이 버튼 클릭 시 TCP 서버 연결 시도

#### 2.2 토큰 기반 인증 구현
- [✔️] **TCP 인증 연동 구현**
  - `SessionManager`에서 refreshToken 획득
  - refreshToken을 JWT로 서버에 전송: `auth:<JWT>`
  - fallback: ID/PW 기반 인증 `auth:<username>:<password>`
  - `AUTH_SUCCESS` 응답 처리 및 세션 토큰 저장

#### 2.3 버전 체크 구현
- [ ] **버전 호환성 체크**
  - 연결 직후 `version:check:<clientVersion>` 전송
  - `version:ok` 또는 `version:mismatch:<downloadUrl>` 응답 처리
  - 버전 불일치 시 업데이트 안내 UI

### 📌 3단계: MultiCore 씬 및 데이터 캐싱
**목표**: 멀티플레이 전용 사용자 데이터 관리 시스템

#### 3.1 MultiCore 씬 구조 설계
- [ ] **MultiCore 씬 생성 및 기본 구조**
  - SingleCore와 유사한 구조로 MultiCore 씬 설계
  - 멀티플레이 전용 데이터 매니저들 구현
  - Scene 로딩 및 Additive 방식 구현

#### 3.2 멀티플레이 유저 데이터 캐싱
- [ ] **MultiUserDataCache 구현**
  - `AUTH_SUCCESS` 응답에서 사용자 정보 캐싱
  - username, sessionToken, displayName, level, totalGames, wins, losses, totalScore, bestScore, experiencePoints
  - SingleCore의 UserDataCache와 구조 통일

#### 3.3 UIManager에 MultiCore 연동
- [ ] **UIManager.cs OnMultiModeSelected() 수정**
  - TCP 서버 연결 완료 후 MultiCore 씬 로드
  - 연결 실패 시 에러 처리 및 ModeSelection 패널 유지
  - 로딩 상태 UI 표시

### 📌 4단계: 로비 시스템 구현
**목표**: LobbyPanel 구현 및 방 관리 기능

#### 4.1 LobbyPanel UI 구현
- [ ] **LobbyPanel.cs 생성**
  - `client/src/LobbyWindow.cpp` 참조하여 UI 구성 요소 설계
  - 방 목록 테이블 (RoomId, RoomName, 현재인원/최대인원, 진행상태)
  - 로비 채팅 시스템
  - 방 생성 버튼 및 모달
  - 유저 목록 표시

#### 4.2 로비 네트워크 메시지 처리
- [ ] **로비 메시지 핸들링 구현**
  - `lobby:enter` → `LOBBY_ENTER_SUCCESS` 처리
  - `lobby:list` → `LOBBY_USER_LIST` 사용자 목록 업데이트
  - `room:list` → `ROOM_LIST` 방 목록 업데이트
  - `chat:<message>` → `CHAT:<username>:<displayName>:<message>` 채팅

#### 4.3 방 생성 및 참가 기능
- [ ] **방 관리 기능 구현**
  - 방 생성: `room:create:<name>:<private>[:password]`
  - 방 참가: `room:join:<roomId>[:password]`
  - 방 목록 새로고침 자동/수동 업데이트
  - 더블클릭으로 방 참가 기능

### 📌 5단계: 게임룸 시스템 구현
**목표**: GameRoomPanel 구현 및 게임 준비 단계

#### 5.1 GameRoomPanel UI 구현
- [ ] **GameRoomPanel.cs 생성**
  - `client/src/GameRoomWindow.cpp` 참조하여 UI 설계
  - 플레이어 슬롯 (최대 4명, 색상, 준비상태)
  - 룸 채팅 시스템
  - 준비/시작 버튼 (호스트만 시작 가능)
  - 방 나가기 버튼

#### 5.2 게임룸 네트워크 메시지 처리
- [ ] **게임룸 메시지 핸들링 구현**
  - `ROOM_JOIN_SUCCESS` → 방 참가 성공 시 GameRoomPanel 활성화
  - `ROOM_INFO` → 방 정보 및 플레이어 목록 업데이트
  - `PLAYER_JOINED/LEFT` → 플레이어 입장/퇴장 처리
  - `PLAYER_READY` → 준비 상태 토글
  - `HOST_CHANGED` → 방장 변경 처리

#### 5.3 게임 시작 프로세스
- [ ] **게임 시작 처리**
  - 방장의 `room:start` 명령 전송
  - `GAME_START_SUCCESS` 수신 시 실제 게임플레이로 전환
  - 모든 플레이어 준비 완료 상태 검증

### 📌 6단계: 실제 게임플레이 구현
**목표**: 멀티플레이어 블로쿠스 게임 로직 구현

#### 6.1 게임 상태 동기화
- [ ] **게임 상태 메시지 처리**
  - `GAME_STATE_UPDATE` → JSON 페이로드로 보드/가용블록/점수 동기화
  - `BLOCK_PLACED` → 블록 배치 시 모든 클라이언트에 반영
  - `TURN_CHANGED` → 턴 변경 및 타이머 처리
  - `TURN_TIMEOUT` → 턴 시간초과 처리

#### 6.2 게임 보드 및 블록 시스템
- [ ] **멀티플레이어 게임 보드 구현**
  - SingleGameManager 참조하여 MultiGameManager 구현
  - 4명 플레이어 색상 및 시작 위치 설정
  - 블록 배치 유효성 검사 (로컬 + 서버 검증)
  - 게임 규칙: 첫 수는 코너, 이후 대각선 인접만 허용

#### 6.3 게임 플로우 및 종료
- [ ] **게임 진행 및 종료 처리**
  - `game:move:<blockType>:<x>:<y>:<rotation>:<flip>` 전송
  - `GAME_MOVE_SUCCESS` 또는 `ERROR` 응답 처리
  - `GAME_ENDED` → 게임 종료 및 결과 표시
  - 게임 종료 후 로비로 돌아가기

### 📌 7단계: AFK 및 부가 기능
**목표**: 게임 중 AFK 처리 및 기타 기능들

#### 7.1 AFK(잠수) 시스템
- [ ] **AFK 처리 구현**
  - `AFK_MODE_ACTIVATED` → AFK 모드 진입 알림
  - `AFK_UNBLOCK` → AFK 해제 요청 UI
  - `AFK_UNBLOCK_SUCCESS/ERROR` → 해제 결과 처리
  - AFK 상태 표시 UI

#### 7.2 사용자 설정 및 통계
- [ ] **사용자 기능 구현**
  - `user:settings:request` → 설정 조회
  - `user:settings:<theme>:<language>:<bgmMute>:<bgmVolume>:<sfxMute>:<sfxVolume>` → 설정 업데이트
  - `user:stats:<username>` → 사용자 통계 조회
  - 친구 추가 및 귓속말 기능 (선택사항)

### 📌 8단계: 안정성 및 최적화
**목표**: 프로덕션 레벨 안정성 확보

#### 8.1 에러 처리 및 예외 상황
- [ ] **에러 핸들링 강화**
  - 네트워크 연결 끊김 처리 및 자동 재연결
  - 서버 응답 타임아웃 처리
  - 잘못된 메시지 포맷 처리
  - 게임 중 플레이어 이탈 처리

#### 8.2 UI/UX 개선
- [ ] **사용자 경험 개선**
  - 로딩 인디케이터 및 프로그레스 바
  - 토스트 메시지 시스템 활용
  - 네트워크 상태 표시 UI
  - 오프라인/온라인 상태 전환 처리

#### 8.3 성능 최적화
- [ ] **성능 및 메모리 최적화**
  - 메시지 큐잉 및 배치 처리
  - 메모리 풀링 (메시지, 이벤트 객체)
  - 대역폭 최적화 (불필요한 메시지 최소화)
  - 모바일 배터리 최적화

---

## 🔧 기술적 고려사항

### 네트워킹
- **프로토콜**: 텍스트 기반 콜론(:) 구분자, UTF-8 인코딩
- **연결**: System.Net.Sockets.TcpClient 사용
- **스레딩**: Unity 메인스레드 디스패처 패턴 적용
- **재연결**: 지수 백오프 알고리즘 적용

### 아키텍처
- **씬 관리**: MainScene + MultiCore + MultiGameplayScene (Additive 로딩)
- **데이터 관리**: 싱글톤 패턴 기반 매니저들 (SingleCore 구조와 통일)
- **UI 관리**: 기존 UIManager 확장, PanelBase 상속 구조 활용
- **이벤트 시스템**: C# event 기반 느슨한 결합

### 보안
- **인증**: JWT 기반 서버 인증, 세션 토큰 관리
- **데이터 검증**: 클라이언트 사이드 + 서버 사이드 이중 검증
- **세션 관리**: 토큰 만료 처리 및 자동 갱신

### 호환성
- **크로스 플랫폼**: Qt 클라이언트와 완전 호환 프로토콜
- **버전 관리**: 클라이언트-서버 버전 호환성 체크
- **UI 적응**: 모바일 터치 인터페이스 최적화

---

## 📋 검증 체크리스트

각 단계 완료 후 다음 사항들을 검증해야 함:

### 단계별 검증 기준
- [ ] **1단계**: TCP 연결 및 기본 메시지 송수신 성공
- [ ] **2단계**: 토큰 기반 인증 성공, 버전 체크 동작
- [ ] **3단계**: MultiCore 씬 로딩 및 데이터 캐싱 확인
- [ ] **4단계**: 로비 입장, 방 목록 조회, 채팅 기능 동작
- [ ] **5단계**: 방 생성/참가, 플레이어 준비 상태 동기화
- [ ] **6단계**: 4인 멀티플레이 게임 완주 가능
- [ ] **7단계**: AFK 처리 및 부가 기능 정상 동작
- [ ] **8단계**: 장시간 안정성 테스트 통과

### 통합 테스트
- [ ] **Qt 클라이언트와 동시 접속 테스트**
- [ ] **네트워크 불안정 상황 테스트** (연결 끊김, 재연결)
- [ ] **다양한 Android 기기에서 테스트**
- [ ] **메모리 누수 및 성능 프로파일링**

---

## 🚨 주의사항 및 리스크

### 기술적 리스크
1. **Unity 스레딩**: TCP 소켓 수신을 별도 스레드에서 처리 후 메인스레드 디스패치 필요
2. **Android 네트워킹**: Android 9+ 네트워크 보안 정책 고려
3. **메모리 관리**: 장시간 플레이 시 메모리 누수 방지
4. **상태 동기화**: 클라이언트간 게임 상태 불일치 방지

### 개발 복잡도
1. **프로토콜 복잡성**: 텍스트 기반 프로토콜의 파싱 오류 처리
2. **예외 상황**: 네트워크 중단, 플레이어 이탈 등 다양한 엣지 케이스
3. **디버깅 난이도**: 멀티플레이어 버그는 재현과 디버깅이 어려움

### 일정 리스크
- 6단계 (게임플레이)가 가장 복잡하고 시간이 많이 소요될 예상
- 네트워크 관련 버그는 발견과 수정에 시간이 오래 걸릴 수 있음

---

## 📈 우선순위 및 마일스톤

### 🔥 최우선 (필수 기능)
- 1~4단계: 기본 네트워킹부터 로비까지
- 6단계: 핵심 게임플레이 기능

### 🔶 중요 (주요 기능)  
- 5단계: 게임룸 관리
- 8단계: 안정성 개선

### 🔵 낮음 (부가 기능)
- 7단계: AFK 및 사용자 설정 기능

### 마일스톤
- **M1 (2주)**: 1~2단계 완료 - 기본 연결 및 인증
- **M2 (3주)**: 3~4단계 완료 - 로비 시스템
- **M3 (4주)**: 5~6단계 완료 - 게임 플레이
- **M4 (1주)**: 7~8단계 완료 - 최종 마무리

**총 예상 개발 기간: 10주 (2.5개월)**

---

## 💡 구현 시 중요한 포인트

### 주석 작성 지침
- 모든 주석은 **한글**로 작성
- 클래스와 메서드에는 XML 문서화 주석 사용
- 복잡한 로직에는 인라인 주석으로 설명 추가

### 코드 작성 가이드
- 변수명과 메서드명은 영어, 주석과 문자열은 한글
- Unity 코딩 컨벤션 준수
- 에러 메시지는 사용자가 이해하기 쉬운 한글로 작성

---

*이 TODO 리스트는 ChatGPT의 서버 프로토콜 분석과 현재 Unity 클라이언트 stub 구현 상태를 바탕으로 작성되었습니다. 실제 구현 과정에서 세부사항은 조정될 수 있습니다.*