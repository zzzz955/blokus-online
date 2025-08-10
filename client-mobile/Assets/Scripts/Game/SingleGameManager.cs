// Assets/Scripts/Game/SingleGameManager.cs
using System.Collections.Generic;
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

        // ===== Runtime =====
        private GameLogic logic;
        private StagePayload payload;
        // 현재 세션에서 사용 가능한 블록 목록(팔레트 재생성용)
        private List<BlockType> initialBlocks;
        // 배치 히스토리(Undo용) — 최신 배치가 리스트 끝
        private readonly List<BlockPlacement> placements = new();
        private Block _currentSelectedBlock; // 현재 선택된 블록
        public int RemainingUndo { get; private set; }
        public System.Action<int> OnUndoCountChanged;
        public System.Action<int /*score*/> OnGameFinished;
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
            // 테스트용으로 자동 초기화
            if (!IsInitialized)
            {
                Debug.Log("[SingleGameManager] 자동 초기화 시작");
                var testPayload = new StagePayload
                {
                    StageName = "Test Stage",
                    BoardSize = 20,
                    AvailableBlocks = DefaultBlockSet()
                };
                Init(testPayload);
            }
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
        public void Init(StagePayload p)
        {
            payload = p ?? new StagePayload();
            logic = new GameLogic();
            placements.Clear();

            RemainingUndo = Mathf.Max(0, maxUndo);
            OnUndoCountChanged?.Invoke(RemainingUndo);

            // GameBoard 세팅
            gameBoard.SetGameLogic(logic);
            gameBoard.ClearBoard();

            // 블록 세팅
            var blocks = (payload.AvailableBlocks != null && payload.AvailableBlocks.Length > 0)
                ? new List<BlockType>(payload.AvailableBlocks)
                : new List<BlockType>(DefaultBlockSet());
            initialBlocks = blocks;

            blockPalette.InitializePalette(
                blocks,
                PlayerColor.Blue
            );

            // 이벤트 연결
            blockPalette.OnBlockSelected += OnBlockSelectedFromPalette;
            gameBoard.OnBlockPlaced += OnBlockPlacedToBoard;
            gameBoard.OnCellClicked += OnBoardCellClicked;

            Debug.Log($"[SingleGame] Start - Stage: {payload.StageName ?? "Unknown"}, Board: {payload.BoardSize}");
            startTimeRealtime = Time.realtimeSinceStartup;
            IsInitialized = true;
        }

        public void OnExitRequested()
        {
            Debug.Log($"[SingleGame] ExitRequested - elapsed={ElapsedSeconds}s");
            // TODO: 여기서 네트워크 업로드/세이브 등 필요한 후처리
        }

        // 기존 BlockControlUI 의존 제거 후, Undo는 별도 패널/버튼에서 호출
        public void OnUndoMove()
        {
            if (RemainingUndo <= 0)
            {
                Debug.Log("[SingleGame] Undo 불가 - 남은 횟수 없음");
                return;
            }
            if (placements.Count == 0)
            {
                Debug.Log("[SingleGame] Undo 불가 - 히스토리 없음");
                return;
            }

            // 1개 pop
            placements.RemoveAt(placements.Count - 1);

            // 게임 로직/보드 재빌드
            RebuildFromPlacements();

            RemainingUndo--;
            OnUndoCountChanged?.Invoke(RemainingUndo);
        }

        // ===== Handlers =====
        private void OnBlockSelectedFromPalette(Block block)
        {
            Debug.Log($"[SingleGameManager] 블록 선택됨: {block.Type}");
            _currentSelectedBlock = block; // 선택된 블록 저장
        }

        private void OnBoardCellClicked(Position pos)
        {
            Debug.Log($"[SingleGameManager] 보드 클릭: ({pos.row}, {pos.col})");
            
            if (_currentSelectedBlock != null)
            {
                Debug.Log($"[SingleGameManager] 블록 미리보기 설정: {_currentSelectedBlock.Type} at ({pos.row}, {pos.col})");
                gameBoard.SetTouchPreview(_currentSelectedBlock, pos);
            }
            else
            {
                Debug.LogWarning("[SingleGameManager] 선택된 블록이 없습니다. 먼저 팔레트에서 블록을 선택하세요.");
                // 블록이 선택되지 않았을 때도 기존 미리보기 클리어
                gameBoard.ClearTouchPreview();
            }
        }

        private void OnBlockPlacedToBoard(Block block, Position pos)
        {
            // 배치 기록
            var placement = new BlockPlacement(
                block.Type,
                pos,
                block.CurrentRotation,
                block.CurrentFlipState,
                block.Player
            );
            placements.Add(placement);

            // 팔레트 사용 표시
            blockPalette.MarkBlockAsUsed(block.Type);

            // 종료 체크
            if (logic.IsGameFinished())
            {
                var scores = logic.CalculateScores();
                int myScore = scores.ContainsKey(PlayerColor.Blue) ? scores[PlayerColor.Blue] : 0;
                Debug.Log($"[SingleGame] Finished. Score={myScore}");
                OnGameFinished?.Invoke(myScore);
            }
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
                PlayerColor.Blue
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
    }
}
