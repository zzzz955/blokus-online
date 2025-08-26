🎯 Unity 씬 스코프 마이그레이션 — 작업 프롬프트 (v3, 실행 지침)
역할

너는 Unity 마이그레이션/구현 어시스턴트다. 아래 요구사항을 만족하도록 코드/프리팹/씬 설정을 생성·수정하라. DI는 사용하지 않는다.

목표

다음 구조와 전환 규칙을 구현/고도화한다:

씬 구조(모두 Additive)

AppPersistent(전역): SceneFlowController, SessionManager, SystemMessageManager, HttpApiClient(대체)

MainScene: 로그인/모드 선택/설정 + UIArchitecture

SingleCore: 싱글 전용 매니저/캐시(StageDataManager, StageProgressManager, UserDataCache, SingleCoreBootstrap)

SingleGameplayScene: 싱글 게임 화면/로직

MultiGameplayScene(Stub): 멀티 진입 포인트(TCP 준비용)

전환 규칙

부팅: AppPersistent → MainScene(additive 활성)

GoSingle: SingleCore(없으면 로드) → SingleGameplayScene 로드 → SingleGameplayScene 활성

ExitSingleToMain: SingleGameplayScene 언로드(코어 유지) → MainScene 활성

GoMulti: SingleGameplayScene 언로드(있다면) → SingleCore 언로드(있다면) → MultiGameplayScene 로드/활성

ExitMultiToMain: MultiGameplayScene 언로드 → MainScene 활성

제약/정책

HTTP 레이어: HttpService/Factory는 폐기, **HttpApiClient**로 대체. 재시도/타임아웃 없음. 에러 시 SystemMessageManager 토스트 노출.

BlockSkin: Phase 1로 enum 매핑 + 내부 리소스 할당(Registry/Resources). Phase 2에서 Addressables 전환(준비만).

UI 애니메이션: Animator 사용, 기본 0.2s / EaseOut, 패널마다 Show/Hide 트리거.

Session: 로그인은 MainScene 로그인 패널에서 처리. 게스트 없음. 멀티 입장 시 캐싱된 ID/PW를 TCP 서버로 전송(서버가 토큰 식별/갱신).

세이브/진행도: REST API 서버 사용.

SystemMessageManager: 최대 3개 스택, 신규 메시지가 아래쪽, 3초 후 자동 소멸.

Loading UX: 입력 잠금, 캔슬 불가, 스피너 인디케이터 사용(회전 아이콘).

네트워킹: 멀티는 C++/Boost.asio TCP와 연동 예정. 모바일은 싱글 우선 → Stub 유지.

프로젝트 경로 규약

씬: Assets/_Project/Scenes/{AppPersistent|MainScene|SingleCore|SingleGameplayScene|MultiGameplayScene}.unity

스크립트 루트: Assets/_Project/Scripts

App/(부팅/플로우/네트워크/세션/UI)

Features/Single/(싱글 전용)

Shared/(공통, 예: UI 메시지, 유틸)

구현 작업 (파일/클래스 생성 가이드 & 수락 기준)
1) SceneFlowController

파일: Assets/_Project/Scripts/App/SceneFlowController.cs
역할: 씬 로딩/언로딩/활성 관리 + 로딩 중 입력 잠금 + 인디케이터 표시.

구현 요구

상수: 씬명 문자열 5개.

코루틴: GoSingle(), ExitSingleToMain(), GoMulti(), ExitMultiToMain()

헬퍼: EnsureLoaded(name), LoadAdditive(name, setActive=false), UnloadIfLoaded(name), SetActive(name)

로딩 프레임: 호출 구간에서 LoadingOverlay.Show() → await → Hide(), InputLocker.Enable/Disable

수락 기준

전환 규칙을 위반하지 않음(코어 생존/언로드 타이밍 정확).

로딩 중 UI 입력 완전 차단.

예외 발생 시 SystemMessageManager.ShowToast("로딩 실패: ...", Priority.Error) 호출.

2) LoadingOverlay & InputLocker

파일:

Assets/_Project/Scripts/App/UI/LoadingOverlay.cs

Assets/_Project/Scripts/App/UI/InputLocker.cs

프리팹: Assets/_Project/Prefabs/UI/LoadingOverlay.prefab(Canvas + 반투명 패널 + 회전 스피너)

요구

LoadingOverlay.Show(string note=null) / Hide() 정적 접근 or 싱글턴(DDOL).

InputLocker.Enable() / Disable() → EventSystem 및 GraphicRaycaster 비활성.

수락 기준

전환 전후 깜빡임/입력 누수 없음.

스피너는 항상 최상위 UI로 표시.

3) SystemMessageManager (토스트)

파일: Assets/_Project/Scripts/App/UI/SystemMessageManager.cs
참고 enum: Assets/_Project/Scripts/Shared/UI/MessageData.cs(Priority 존재)

요구

API: ShowToast(string message, MessagePriority priority, float duration=3f)

스택 3개 제한, 새 메시지는 아래로 추가.

각 항목은 Animator(0.2s EaseOut)로 등장/퇴장.

같은 메시지의 연속 노출은 합치지 않음(정책 단순화).

수락 기준

4번째 메시지 도착 시 가장 오래된(맨 위) 즉시 닫고 밀림.

3초 경과 후 자동 종료 + 레이아웃 재정렬.

에러 레벨일 때 색/아이콘 차등(간단 스타일).

4) UIArchitecture (패널 규격)

파일:

인터페이스: Assets/_Project/Scripts/Shared/UI/IPanel.cs

기본 구현: Assets/_Project/Scripts/Shared/UI/PanelBase.cs

요구

IPanel { void Show(); void Hide(); bool IsVisible { get; } }

PanelBase는 Animator와 트리거(Show, Hide)를 강제.

기본 전환 시간 0.2s, EaseOut.

MainScene/싱글 오버레이 패널들은 PanelBase 상속으로 통일.

수락 기준

임의 패널 프리팹에 PanelBase 붙이면 추가 코드 없이 Show/Hide 동작.

5) SessionManager & 로그인 패널

파일:

Assets/_Project/Scripts/App/Session/SessionManager.cs

Assets/_Project/Scripts/App/Session/LoginPanelController.cs

요구

로그인 로직은 MainScene의 패널에서 동작.

게스트 금지, ID/PW 입력 → REST 로그인 성공 시 메모리 캐시 저장.

멀티 진입 시 캐시된 ID/PW를 TCP 서버로 전송(토큰은 서버에서 관리/갱신).

실패 시 SystemMessageManager로 토스트.

수락 기준

플레이모드 재시작 없이 씬 전환 반복해도 캐시/상태 일관.

로그인 실패/네트워크 예외 시 사용자 피드백 명확.

6) HttpApiClient (대체)

파일: Assets/_Project/Scripts/App/Network/HttpApiClient.cs

요구

간단한 래퍼: Task<T> Get<T>(string path), Task<T> Post<T>(string path, object body)

재시도/타임아웃 없음, 예외 시 SystemMessageManager.ShowToast("네트워크 오류: ...", Error) 호출 후 예외 재던짐.

JSON 직렬화/역직렬화(Newtonsoft 또는 UnityWebRequest + JsonUtility) 중 하나 선택, 프로젝트 표준에 맞춤.

수락 기준

4xx/5xx 시 본문 메시지 파싱해 사용자에게 노출.

MainThread 컨텍스트에서 토스트 호출 안정성 확보.

7) SingleCore 구성

파일:

Assets/_Project/Scripts/Features/Single/SingleCoreBootstrap.cs

Assets/_Project/Scripts/Features/Single/StageDataManager.cs

Assets/_Project/Scripts/Features/Single/StageProgressManager.cs

Assets/_Project/Scripts/Features/Single/UserDataCache.cs

요구

SingleCoreBootstrap가 위 매니저 초기화 및 의존 관계 연결.

메인 복귀 시 유지, 멀티 진입 전 언로드.

수락 기준

싱글 → 메인 → 싱글 반복에서 진행도/캐시 유지.

멀티로 갈 때 메모리 릭/핸들 잔류 없음.

8) BlockSkin (Phase 1: Enum/Registry)

파일:

Assets/_Project/Scripts/Features/Single/Gameplay/Skins/BlockSkin.cs(SO 또는 데이터 클래스로 유지)

Assets/_Project/Scripts/Features/Single/Gameplay/Skins/BlockSkinId.cs(enum)

Assets/_Project/Scripts/Features/Single/Gameplay/Skins/BlockSkinRegistry.cs

요구

DB에서 상수값(enum) 수신 → BlockSkinId 매핑 → 내부 리소스 할당(텍스처/머티리얼/프리팹 경로).

리소스 로드: 우선 Resources 또는 사전 캐시.

Phase 2 준비: Addressables로 전환 가능한 구조(IBlockSkinProvider 인터페이스 등).

수락 기준

잘못된 enum 수신 시 디폴트 스킨 + 오류 토스트.

런타임 교체 시 깜빡임 최소화.

9) MultiGameplayScene (Stub)

파일:

Assets/_Project/Scripts/Features/Multi/MultiBootstrap.cs

요구

씬 진입/이탈 훅만 구현(로딩/입력 잠금/토스트).

TCP 접속/로비/룸 로직은 TODO로 명시.

수락 기준

GoMulti/ExitMultiToMain 플로우가 정확히 동작.

Addressables(Phase 2) — 준비만

지금은 Addressables 미도입. 다음을 주석/TODO로 명시:

AddressableAssetSettings 생성, Skins(Local) 그룹, Label: BlockSkin

BlockSkinRegistry를 Addressables 기반 제공자로 교체 가능하도록 인터페이스 분리

카탈로그 로컬 우선(원격/버전업은 추후)

에디터/빌드 설정

Build Settings에 5개 씬 등록(순서 무관, 전부 Additive 로드 전제).

Enter Play Mode Options: 기본값 유지(도메인/씬 리로드 끔 옵션 미사용) 권장. AppPersistent의 이중 생성 방지 체크.

Scripting Define Symbols: 현재 필요 없음. (멀티/모바일 플래그는 추후)

테스트 시나리오 (수락 테스트)

부팅→메인 진입(토스트/로딩 표시 정상, 입력 잠금 정상)

메인→싱글 3회 반복(코어 유지, 진행도/캐시 유지, 누수 없음)

싱글→메인→멀티(싱글 언로드, 코어 언로드, 멀티 활성)

멀티→메인 복귀

네트워크 실패(REST 500/타임아웃 가정) 시 토스트 출력 & 예외 전파

토스트 4개 연속 호출 시 스택 3개 유지(가장 오래된 것 제거), 신규가 아래 배치

패널 애니메이션 0.2s EaseOut로 Show/Hide 일괄 동작

산출물 체크리스트

 SceneFlowController 코루틴/헬퍼 완비, 씬 전환 전후 LoadingOverlay & InputLocker 연동

 SystemMessageManager 토스트 스택(3개, 아래에 신규) + 3초 소멸 + 애니메이션

 PanelBase/Animator 트리거 규약(Show/Hide) 문서화 및 샘플 패널 1개

 SessionManager + LoginPanel 연동, 캐시 저장/활용 흐름, 실패 토스트

 HttpApiClient 교체 적용(컴파일 에러 없이 전역 참조 업데이트)

 SingleCoreBootstrap에서 매니저 초기화/해제 명확

 BlockSkin Enum/Registry 경로 매핑 & 잘못된 값 방어

 MultiGameplayScene Stub 진입/이탈 훅

 Build Settings 씬 등록, AppPersistent DDOL 중복 방지

코드 스니펫(핵심 인터페이스/시그니처만)
// SceneFlowController.cs (요약)
public class SceneFlowController : MonoBehaviour {
  public IEnumerator GoSingle();
  public IEnumerator ExitSingleToMain();
  public IEnumerator GoMulti();
  public IEnumerator ExitMultiToMain();
  IEnumerator EnsureLoaded(string name);
  IEnumerator LoadAdditive(string name, bool setActive=false);
  IEnumerator UnloadIfLoaded(string name);
  void SetActive(string name);
}

// SystemMessageManager.cs (요약)
public enum MessagePriority { Info, Warning, Error }
public class SystemMessageManager : MonoBehaviour {
  public static void ShowToast(string message, MessagePriority priority, float duration = 3f);
  // 내부: 최대 3개 스택, 신규 아래, 3초 후 자동 Hide
}

// IPanel/PanelBase (요약)
public interface IPanel { void Show(); void Hide(); bool IsVisible { get; } }
public abstract class PanelBase : MonoBehaviour, IPanel {
  protected Animator animator; // triggers: "Show","Hide"
}

// HttpApiClient.cs (요약)
public class HttpApiClient {
  public async Task<T> Get<T>(string path);
  public async Task<T> Post<T>(string path, object body);
  // catch(Exception ex) { SystemMessageManager.ShowToast(..., Error); throw; }
}

// SessionManager.cs (요약)
public class SessionManager : MonoBehaviour {
  public bool IsLoggedIn { get; }
  public string CachedId { get; }
  public string CachedPassword { get; }
  public Task<bool> Login(string id, string pw); // REST
  public (string id, string pw) GetCredentialsForTcp();
}

// BlockSkin (요약)
public enum BlockSkinId { Default = 0, /* ... */ }
public static class BlockSkinRegistry {
  public static BlockSkin Get(BlockSkinId id); // 내부 리소스/Resources 매핑
}

마이그레이션 메모

기존 HttpService/Factory 참조는 전부 HttpApiClient로 교체. 컴파일 에러를 체크하고 최소한의 어댑터 제공 가능(필요 시).

Addressables는 Phase 2에서만: 지금은 코드에 인터페이스 훅만 남겨라.

멀티는 Stub이므로 UI/흐름만 보장. 실제 TCP는 나중에.