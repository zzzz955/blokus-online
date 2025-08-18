// Assets/Scripts/Game/SingleGameManager.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BlokusUnity.Common;
using BlokusUnity.Game;

namespace BlokusUnity.Game
{
    /// <summary>
    /// 싱글플레이 게임 세션 관리 + Undo 지원
    /// </summary>
    public class SingleGameManager : MonoBehaviour
    {
        // ===== Singleton & Legacy statics =====
        public static SingleGameManager Instance { get; private set; }
        public static int CurrentStage { get; private set; } = 0;
        public static BlokusUnity.Data.StageDataManager StageManager { get; private set; }
        public static void SetStageContext(int stageNumber, BlokusUnity.Data.StageDataManager stageManager)
        {
            CurrentStage = stageNumber;
            StageManager = stageManager;
        }

        // ===== Inspector refs =====
        [Header("References")]
        [SerializeField] private GameBoard gameBoard;
        [SerializeField] private BlockPalette blockPalette;

        [Header("Undo Settings")]
        [SerializeField] private int maxUndo = 3;

        [Header("Player Settings")]
        [SerializeField] private PlayerColor playerColor = PlayerColor.Blue;

        // ===== Runtime =====
        private GameLogic logic;
        private StagePayload payload;
        // 현재 세션에서 사용 가능한 블록 목록(팔레트 재생성용)
        private List<BlockType> initialBlocks;
        private bool _undoInProgress = false;
        // 배치 히스토리(Undo용) — 최신 배치가 리스트 끝
        private readonly List<BlockPlacement> placements = new();
        private Block _currentSelectedBlock; // 현재 선택된 블록
        public int RemainingUndo { get; private set; }
        public System.Action<int> OnUndoCountChanged;
        public System.Action<int /*score*/> OnGameFinished;
        public System.Action<int /*scoreChange*/, string /*reason*/> OnScoreChanged;
        private float startTimeRealtime;
        public bool IsInitialized { get; private set; }
        public int ElapsedSeconds
        {
            get
            {
                if (!IsInitialized) return 0;
                return Mathf.Max(0, Mathf.FloorToInt(Time.realtimeSinceStartup - startTimeRealtime));
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (gameBoard == null) gameBoard = FindObjectOfType<GameBoard>();
            if (blockPalette == null) blockPalette = FindObjectOfType<BlockPalette>();

            if (StageManager == null)
                StageManager = FindObjectOfType<BlokusUnity.Data.StageDataManager>();
        }

        private void Start()
        {
            // 실제 스테이지 데이터로 초기화
            if (!IsInitialized)
            {
                Debug.Log("[SingleGameManager] 스테이지 데이터 기반 초기화 시작");
                
                // StageDataManager에서 현재 스테이지 데이터 가져오기
                if (StageManager != null)
                {
                    var stageData = StageManager.GetCurrentStageData();
                    if (stageData != null)
                    {
                        Debug.Log($"[SingleGameManager] 스테이지 {stageData.stage_number} 데이터로 초기화");
                        var stagePayload = ConvertStageDataToPayload(stageData);
                        Init(stagePayload);
                    }
                    else
                    {
                        Debug.LogWarning("[SingleGameManager] 현재 스테이지 데이터가 없음 - 테스트 모드로 초기화");
                        InitializeWithTestData();
                    }
                }
                else
                {
                    Debug.LogWarning("[SingleGameManager] StageDataManager가 없음 - 테스트 모드로 초기화");
                    InitializeWithTestData();
                }
            }
        }
        
        /// <summary>
        /// 테스트용 초기화 (폴백)
        /// </summary>
        private void InitializeWithTestData()
        {
            Debug.Log("[SingleGameManager] 테스트 데이터로 자동 초기화");
            var testPayload = new StagePayload
            {
                StageName = "Test Stage",
                StageNumber = 1,
                BoardSize = 20,
                Difficulty = 1,
                TimeLimit = 0,
                MaxUndoCount = 3,
                AvailableBlocks = GetMinimalBlockSet()
            };
            Init(testPayload);
        }
        
        /// <summary>
        /// StageData를 StagePayload로 변환
        /// </summary>
        private StagePayload ConvertStageDataToPayload(BlokusUnity.Data.StageData stageData)
        {
            var payload = new StagePayload
            {
                StageNumber = stageData.stage_number,
                StageName = !string.IsNullOrEmpty(stageData.stage_description) ? 
                    stageData.stage_description : $"스테이지 {stageData.stage_number}",
                BoardSize = 20, // 기본 보드 크기
                Difficulty = stageData.difficulty,
                TimeLimit = stageData.time_limit,
                MaxUndoCount = stageData.max_undo_count > 0 ? stageData.max_undo_count : 3,
                ParScore = stageData.optimal_score
            };
            
            // 사용 가능한 블록 설정
            if (stageData.available_blocks != null && stageData.available_blocks.Length > 0)
            {
                var blockTypes = new List<BlockType>();
                foreach (var blockInt in stageData.available_blocks)
                {
                    if (blockInt >= 1 && blockInt <= 21 && System.Enum.IsDefined(typeof(BlockType), (byte)blockInt))
                    {
                        blockTypes.Add((BlockType)(byte)blockInt);
                    }
                }
                payload.AvailableBlocks = blockTypes.ToArray();
                Debug.Log($"[SingleGameManager] 스테이지 {stageData.stage_number} 전용 블록 {blockTypes.Count}개 설정: [{string.Join(", ", blockTypes)}]");
            }
            else
            {
                // ⚠️ 수정: 스테이지 데이터에 available_blocks가 없으면 기본 튜토리얼 블록만 사용
                // 모든 21개 블록 대신 제한된 블록 세트 사용
                payload.AvailableBlocks = GetMinimalBlockSet();
                Debug.LogWarning($"[SingleGameManager] 스테이지 {stageData.stage_number}에 available_blocks 데이터가 없음 - 최소 블록 세트 사용: {payload.AvailableBlocks.Length}개");
            }
            
            // 초기 보드 상태 설정
            if (stageData.initial_board_state != null)
            {
                // StageData.InitialBoardState를 StagePayload.InitialBoardData로 변환
                payload.InitialBoard = ConvertInitialBoardState(stageData.initial_board_state);
            }
            
            Debug.Log($"[SingleGameManager] StagePayload 생성 완료: " +
                     $"스테이지={payload.StageNumber}, 난이도={payload.Difficulty}, " +
                     $"제한시간={payload.TimeLimit}, 최대언두={payload.MaxUndoCount}, " +
                     $"블록수={payload.AvailableBlocks?.Length ?? 0}");
            
            return payload;
        }
        
        /// <summary>
        /// StageData.InitialBoardState를 StagePayload.InitialBoardData로 변환
        /// </summary>
        private BlokusUnity.Common.InitialBoardData ConvertInitialBoardState(BlokusUnity.Data.InitialBoardState boardState)
        {
            if (boardState == null)
                return null;
            
            var placements = boardState.GetPlacements();
            if (placements == null || placements.Length == 0)
                return null;
            
            var result = new BlokusUnity.Common.InitialBoardData
            {
                obstacles = new System.Collections.Generic.List<Position>(),
                preplaced = new System.Collections.Generic.List<BlockPlacement>()
            };
            
            // 배치 데이터를 BlockPlacement로 변환
            foreach (var placement in placements)
            {
                var blockPlacement = new BlockPlacement(
                    (BlockType)(byte)placement.block_type,
                    new Position(placement.row, placement.col),
                    (Rotation)(byte)placement.rotation,
                    (FlipState)(byte)placement.flip_state,
                    (PlayerColor)(byte)placement.color
                );
                result.preplaced.Add(blockPlacement);
            }
            
            Debug.Log($"[SingleGameManager] 초기 보드 상태 변환 완료: 사전배치 블록 {result.preplaced.Count}개");
            return result;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            if (blockPalette != null) blockPalette.OnBlockSelected -= OnBlockSelectedFromPalette;
            if (gameBoard != null)
            {
                gameBoard.OnBlockPlaced -= OnBlockPlacedToBoard;
                gameBoard.OnCellClicked -= OnBoardCellClicked;
            }
        }

        // ===== Public API =====
        /// <summary>
        /// 플레이어 색상 변경 (런타임)
        /// </summary>
        public void SetPlayerColor(PlayerColor color)
        {
            playerColor = color;

            // 팔레트가 이미 초기화되었다면 재생성
            if (IsInitialized && blockPalette != null && initialBlocks != null)
            {
                blockPalette.InitializePalette(
                    new List<BlockType>(initialBlocks),
                    playerColor
                );

                Debug.Log($"[SingleGameManager] 플레이어 색상 변경: {playerColor}");
            }
        }

        /// <summary>
        /// 현재 플레이어 색상 반환
        /// </summary>
        public PlayerColor GetPlayerColor() => playerColor;

        public void Init(StagePayload p)
        {
            payload = p ?? new StagePayload();
            logic = new GameLogic();
            placements.Clear();

            // API 데이터가 있으면 최대 언두 횟수 적용
            RemainingUndo = Mathf.Max(0, payload.MaxUndoCount > 0 ? payload.MaxUndoCount : maxUndo);
            OnUndoCountChanged?.Invoke(RemainingUndo);

            // GameBoard 세팅
            gameBoard.SetGameLogic(logic);
            gameBoard.ClearBoard();

            // 초기 보드 상태 적용 (장애물, 사전 배치된 블록 등)
            ApplyInitialBoardState(payload.InitialBoard);

            // 블록 세팅
            var blocks = (payload.AvailableBlocks != null && payload.AvailableBlocks.Length > 0)
                ? new List<BlockType>(payload.AvailableBlocks)
                : new List<BlockType>(GetMinimalBlockSet());
            initialBlocks = blocks;

            blockPalette.InitializePalette(
                blocks,
                playerColor
            );

            // 이벤트 연결
            blockPalette.OnBlockSelected += OnBlockSelectedFromPalette;
            gameBoard.OnBlockPlaced += OnBlockPlacedToBoard;
            gameBoard.OnCellClicked += OnBoardCellClicked;

            Debug.Log($"[SingleGame] Start - Stage: {payload.StageName ?? "Unknown"} (#{payload.StageNumber}), " +
                     $"Board: {payload.BoardSize}, Difficulty: {payload.Difficulty}, " +
                     $"TimeLimit: {(payload.TimeLimit > 0 ? payload.TimeLimit + "s" : "무제한")}, " +
                     $"MaxUndo: {RemainingUndo}");
            
            startTimeRealtime = Time.realtimeSinceStartup;
            IsInitialized = true;
        }
        
        /// <summary>
        /// 초기 보드 상태 적용 (API에서 받은 장애물과 사전 배치 블록들)
        /// </summary>
        private void ApplyInitialBoardState(InitialBoardData initialBoard)
        {
            if (initialBoard == null)
            {
                Debug.Log("[SingleGame] 초기 보드 상태 데이터가 없습니다. 빈 보드로 시작합니다.");
                return;
            }
            
            // 장애물 적용
            if (initialBoard.obstacles != null && initialBoard.obstacles.Count > 0)
            {
                foreach (var obstaclePos in initialBoard.obstacles)
                {
                    // TODO: GameLogic에 장애물 설정 메서드가 있다면 호출
                    // logic.SetObstacle(obstaclePos);
                    // Debug.Log($"[SingleGame] 장애물 설정: ({obstaclePos.row}, {obstaclePos.col})");
                }
                Debug.Log($"[SingleGame] 장애물 {initialBoard.obstacles.Count}개 적용됨");
            }
            
            // 사전 배치된 블록들 적용
            if (initialBoard.preplaced != null && initialBoard.preplaced.Count > 0)
            {
                foreach (var placement in initialBoard.preplaced)
                {
                    // 게임 로직에 블록 배치
                    bool placed = logic.PlaceBlock(placement);
                    if (placed)
                    {
                        // 배치 히스토리에 추가 (Undo 대상에서 제외하려면 별도 리스트 관리 필요)
                        placements.Add(placement);
                        // Debug.Log($"[SingleGame] 사전 배치 블록: {placement.type} at ({placement.position.row}, {placement.position.col})");
                    }
                    else
                    {
                        // Debug.LogWarning($"[SingleGame] 사전 배치 블록 실패: {placement.type} at ({placement.position.row}, {placement.position.col})");
                    }
                }
                Debug.Log($"[SingleGame] 사전 배치 블록 {initialBoard.preplaced.Count}개 중 {placements.Count}개 적용됨");
            }
        }

        public void OnExitRequested()
        {
            Debug.Log($"[SingleGame] ExitRequested - elapsed={ElapsedSeconds}s");
            
            // 현재 점수 계산
            var scores = logic?.CalculateScores();
            int currentScore = scores?.ContainsKey(playerColor) == true ? scores[playerColor] : 0;
            
            // 스테이지 완료 보고 (완료되지 않은 상태로)
            ReportStageCompletion(currentScore, false);
        }
        
        /// <summary>
        /// 스테이지 완료 보고 (결과를 user_stage_progress에 저장)
        /// </summary>
        private void ReportStageCompletion(int score, bool completed)
        {
            // 현재 스테이지 번호 확인
            int stageNumber = CurrentStage;
            if (stageNumber <= 0)
            {
                Debug.LogWarning("[SingleGame] 스테이지 번호를 확인할 수 없어 완료 보고를 건너뜁니다.");
                return;
            }
            
            // 별점 계산 (완료된 경우에만)
            int stars = 0;
            if (completed && payload != null && payload.ParScore > 0)
            {
                stars = BlokusUnity.Utils.ApiDataConverter.CalculateStars(score, payload.ParScore);
                Debug.Log($"[SingleGame] 별점 계산: {score}/{payload.ParScore} = {stars}별");
            }
            
            // StageDataManager를 통한 직접 완료 보고
            if (StageManager != null)
            {
                if (completed)
                {
                    StageManager.CompleteStage(stageNumber, score, stars, ElapsedSeconds);
                    Debug.Log($"[SingleGame] ✅ 스테이지 {stageNumber} 완료 보고: " +
                             $"점수={score}, 별점={stars}, 시간={ElapsedSeconds}s");
                }
                else
                {
                    StageManager.FailStage(stageNumber);
                    Debug.Log($"[SingleGame] ❌ 스테이지 {stageNumber} 포기/실패 보고: " +
                             $"점수={score}, 시간={ElapsedSeconds}s");
                }
            }
            else
            {
                Debug.LogWarning("[SingleGame] StageDataManager를 찾을 수 없어 완료 보고를 건너뜁니다.");
            }
            
        }

        /// <summary>
        /// Undo가 가능한지 확인
        /// </summary>
        public bool CanUndo()
        {
            return RemainingUndo > 0 && placements.Count > 0;
        }

        // 기존 BlockControlUI 의존 제거 후, Undo는 별도 패널/버튼에서 호출
        public void OnUndoMove()
        {
            if (_undoInProgress) { Debug.LogWarning("[SingleGame] Undo 진행 중입니다. 중복 호출 무시."); return; }
            _undoInProgress = true;
            try
            {
                if (!CanUndo()) { Debug.LogWarning("[SingleGame] Undo 불가능한 상태"); return; }

                var lastPlacement = placements[placements.Count - 1];
                int removedBlockScore = logic?.GetBlockScore(lastPlacement.type) ?? 0;

                placements.RemoveAt(placements.Count - 1);
                RebuildBoardOnlyFromPlacements();       // 팔레트 초기화 금지
                blockPalette?.RestoreBlock(lastPlacement.type);

                if (removedBlockScore > 0)
                {
                    OnScoreChanged?.Invoke(-removedBlockScore, $"Undo {lastPlacement.type}");
                    Debug.Log($"[SingleGame] 점수 감소: -{removedBlockScore} (Undo {lastPlacement.type})");
                }

                RemainingUndo--;
                OnUndoCountChanged?.Invoke(RemainingUndo);
                Debug.Log($"[SingleGame] Undo 완료 - 남은 횟수: {RemainingUndo}");
            }
            finally
            {
                _undoInProgress = false;
                
                // 간단한 UI 업데이트 (이미 RefreshBoard가 호출되므로)
                UnityEngine.Canvas.ForceUpdateCanvases();
            }
        }

        // ===== Handlers =====
        private void OnBlockSelectedFromPalette(Block block)
        {
            if (block != null)
            {
                // Debug.Log($"[SingleGameManager] 블록 선택됨: {block.Type}");
                _currentSelectedBlock = block; // 선택된 블록 저장
            }
            else
            {
                // Undo 진행 중이면 ClearTouchPreview 호출 안함 (UI 충돌 방지)
                if (!_undoInProgress)
                {
                    // Debug.Log("[SingleGameManager] 블록 선택 해제됨");
                    _currentSelectedBlock = null;
                    // 기존 미리보기와 ActionButtonPanel 클리어
                    gameBoard?.ClearTouchPreview();
                }
            }
        }

        private void OnBoardCellClicked(Position pos)
        {
            // Debug.Log($"[SingleGameManager] 보드 클릭: ({pos.row}, {pos.col})");
            // Debug.Log($"[SingleGameManager] 현재 선택된 블록: {(_currentSelectedBlock?.Type.ToString() ?? "없음")}");

            if (_currentSelectedBlock != null)
            {
                // Debug.Log($"[SingleGameManager] 블록 미리보기 설정 시도: {_currentSelectedBlock.Type} at ({pos.row}, {pos.col})");

                if (gameBoard != null)
                {
                    // Debug.Log("[SingleGameManager] GameBoard.SetTouchPreview 호출");
                    gameBoard.SetTouchPreview(_currentSelectedBlock, pos);
                }
                else
                {
                    Debug.LogError("[SingleGameManager] GameBoard가 null입니다!");
                }
            }
            else
            {
                Debug.LogWarning("[SingleGameManager] 선택된 블록이 없습니다. 먼저 팔레트에서 블록을 선택하세요.");
                // 블록이 선택되지 않았을 때도 기존 미리보기 클리어
                gameBoard?.ClearTouchPreview();
            }
        }

        private void OnBlockPlacedToBoard(Block block, Position pos)
        {
            // 배치 기록 (중복 방지 체크)
            var placement = new BlockPlacement(
                block.Type,
                pos,
                block.CurrentRotation,
                block.CurrentFlipState,
                block.Player
            );
            
            // 중복 배치 방지 - 같은 위치에 같은 블록이 이미 있는지 확인
            var isDuplicate = placements.Any(p => 
                p.type == placement.type && 
                p.position.row == placement.position.row && 
                p.position.col == placement.position.col
            );
            
            if (!isDuplicate)
            {
                placements.Add(placement);
                Debug.Log($"[SingleGame] 블록 배치 기록 추가: {placement.type} at ({pos.row}, {pos.col}) - 총 {placements.Count}개");
            }
            else
            {
                Debug.LogWarning($"[SingleGame] 중복 배치 방지: {placement.type} at ({pos.row}, {pos.col}) 이미 존재");
            }

            // 팔레트 사용 표시
            blockPalette.MarkBlockAsUsed(block.Type);

            // 게임 종료 조건 체크 (블록 팔레트 기반)
            CheckGameEndConditions();
        }

        /// <summary>
        /// 게임 종료 조건 체크 (사용자 요구사항 기반)
        /// 1. 블록 팔레트에 블록이 존재하지 않는 경우
        /// 2. 블록 팔레트에 블록이 존재하나 모든 블록이 배치 불가능한 경우
        /// </summary>
        private void CheckGameEndConditions()
        {
            if (blockPalette == null || logic == null)
            {
                return;
            }

            // 조건 1: 사용 가능한 블록이 없는 경우
            if (!blockPalette.HasAvailableBlocks())
            {
                Debug.Log("[SingleGame] 게임 종료 - 사용 가능한 블록이 없음");
                EndGame("모든 블록 사용 완료");
                return;
            }

            // 조건 2: 남은 블록들이 모두 배치 불가능한 경우
            var availableBlocks = blockPalette.GetAvailableBlocks();
            if (!logic.CanPlaceAnyBlock(playerColor, availableBlocks))
            {
                Debug.Log($"[SingleGame] 게임 종료 - 남은 {availableBlocks.Count}개 블록 모두 배치 불가능");
                EndGame("더 이상 블록을 배치할 수 없음");
                return;
            }

            // 게임 계속 진행
            Debug.Log($"[SingleGame] 게임 계속 - 사용 가능한 블록: {blockPalette.GetAvailableBlockCount()}개");
        }

        /// <summary>
        /// 게임 종료 처리
        /// </summary>
        private void EndGame(string reason)
        {
            var scores = logic.CalculateScores();
            int myScore = scores.ContainsKey(playerColor) ? scores[playerColor] : 0;
            
            Debug.Log($"[SingleGame] 게임 종료: {reason}, 최종 점수: {myScore}");
            
            // 스테이지 완료 보고 (성공)
            ReportStageCompletion(myScore, true);
            
            // 게임 종료 이벤트 발생
            OnGameFinished?.Invoke(myScore);
        }

        // ===== Helpers =====
        private void RebuildFromPlacements()
        {
            // 로직/보드 리셋
            logic = new GameLogic();
            gameBoard.SetGameLogic(logic);
            gameBoard.ClearBoard();

            // 팔레트 리셋 후, 사용 처리 다시 반영
            blockPalette.InitializePalette(
                new List<BlockType>(initialBlocks),
                playerColor
            );

            // 히스토리 재적용
            foreach (var p in placements)
            {
                // 로직 배치 → 보드 반영
                logic.PlaceBlock(p);
            }
            gameBoard.RefreshBoard();

            // 사용된 블록 표시
            foreach (var p in placements)
            {
                blockPalette.MarkBlockAsUsed(p.type);
            }
        }


        private static BlockType[] DefaultBlockSet()
        {
            return new[]
            {
                BlockType.Single, BlockType.Domino,
                BlockType.TrioLine, BlockType.TrioAngle,
                BlockType.Tetro_I, BlockType.Tetro_O, BlockType.Tetro_T, BlockType.Tetro_L, BlockType.Tetro_S,
                BlockType.Pento_F, BlockType.Pento_I, BlockType.Pento_L, BlockType.Pento_N,
                BlockType.Pento_P, BlockType.Pento_T, BlockType.Pento_U, BlockType.Pento_V,
                BlockType.Pento_W, BlockType.Pento_X, BlockType.Pento_Y, BlockType.Pento_Z
            };
        }
        
        /// <summary>
        /// 스테이지 데이터에 available_blocks가 없을 때 사용할 최소 블록 세트
        /// 튜토리얼이나 기본 스테이지에 적합한 제한된 블록들만 포함
        /// </summary>
        private static BlockType[] GetMinimalBlockSet()
        {
            return new[]
            {
                // 기본 블록들 (1-4칸)
                BlockType.Single,
                BlockType.Domino,
                BlockType.TrioLine,
                BlockType.TrioAngle,
                BlockType.Tetro_I,
                BlockType.Tetro_O,
                BlockType.Tetro_T,
                BlockType.Tetro_L,
                // 몇 개의 기본 펜토미노만 포함 (복잡도 제한)
                BlockType.Pento_I,
                BlockType.Pento_L,
                BlockType.Pento_T
            };
        }

        private void RebuildBoardOnlyFromPlacements()
        {
            // Debug.Log($"[SingleGame] Undo 보드 재구성 시작 - placements 개수: {placements.Count}");
            
            // 로직 리셋 (UI 업데이트 없이)
            logic = new BlokusUnity.Common.GameLogic();
            gameBoard.SetGameLogic(logic);

            // 히스토리 재적용 (보드 상태만)
            foreach (var p in placements)
            {
                logic.PlaceBlock(p);
            }

            // ★ 블록 재적용 후 UI 업데이트 필수
            gameBoard.RefreshBoard();
            // Debug.Log("[SingleGame] Undo 보드 재구성 완료");
        }

    }
}
