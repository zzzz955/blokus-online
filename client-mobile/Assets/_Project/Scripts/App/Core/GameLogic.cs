using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Shared.Models;
namespace App.Core{
    /// <summary>
    /// Unity 블로쿠스 게임 로직 (C++ GameLogic.h 포팅)
    /// 서버와 클라이언트가 공유하는 핵심 게임 규칙
    /// </summary>
    public class GameLogic
    {
        // ========================================
        // Private 멤버 변수들 (C++ 포팅)
        // ========================================

        private PlayerColor currentPlayer;
        private PlayerColor[,] board;

        // 사용된 블록 관리
        private Dictionary<PlayerColor, HashSet<BlockType>> usedBlocks;
        private Dictionary<PlayerColor, List<Position>> playerOccupiedCells;
        private Dictionary<PlayerColor, bool> hasPlacedFirstBlock;

        // 성능 최적화를 위한 캐싱
        private Dictionary<PlayerColor, bool> canPlaceAnyBlockCache;
        private bool cacheValid;

        // 영구 캐시: 더 이상 블록을 배치할 수 없는 플레이어 추적
        private Dictionary<PlayerColor, bool> playerBlockedPermanently;

        // 영구 차단 알림 상태 추적 (최초 1번만 알림)
        private Dictionary<PlayerColor, bool> playerBlockedNotified;

        // ========================================
        // 생성자
        // ========================================

        public GameLogic()
        {
            // 2차원 보드 배열 초기화
            board = new PlayerColor[GameConstants.BOARD_SIZE, GameConstants.BOARD_SIZE];

            // 딕셔너리들 초기화
            usedBlocks = new Dictionary<PlayerColor, HashSet<BlockType>>();
            playerOccupiedCells = new Dictionary<PlayerColor, List<Position>>();
            hasPlacedFirstBlock = new Dictionary<PlayerColor, bool>();

            // 캐시 초기화
            canPlaceAnyBlockCache = new Dictionary<PlayerColor, bool>();
            playerBlockedPermanently = new Dictionary<PlayerColor, bool>();
            playerBlockedNotified = new Dictionary<PlayerColor, bool>();

            // 초기 상태 설정
            currentPlayer = PlayerColor.Blue;
            cacheValid = false;

            InitializeBoard();
        }

        // ========================================
        // 보드 관리
        // ========================================

        /// <summary>
        /// 보드 초기화
        /// </summary>
        public void InitializeBoard()
        {
            ClearBoard();

            // 플레이어별 데이터 초기화
            for (int i = 1; i <= GameConstants.MAX_PLAYERS; i++)
            {
                PlayerColor player = (PlayerColor)i;

                usedBlocks[player] = new HashSet<BlockType>();
                playerOccupiedCells[player] = new List<Position>();
                hasPlacedFirstBlock[player] = false;
                canPlaceAnyBlockCache[player] = true;
                playerBlockedPermanently[player] = false;
                playerBlockedNotified[player] = false;
            }

            cacheValid = false;
        }

        /// <summary>
        /// 보드 클리어
        /// </summary>
        public void ClearBoard()
        {
            for (int row = 0; row < GameConstants.BOARD_SIZE; row++)
            {
                for (int col = 0; col < GameConstants.BOARD_SIZE; col++)
                {
                    board[row, col] = PlayerColor.None;
                }
            }

            InvalidateCache();
        }

        // ========================================
        // 셀 상태 확인
        // ========================================

        /// <summary>
        /// 셀 소유자 반환
        /// </summary>
        public PlayerColor GetCellOwner(Position pos)
        {
            if (!IsPositionValid(pos))
                return PlayerColor.None;

            return board[pos.row, pos.col];
        }

        // ========================================
        // 블록 배치 관련
        // ========================================

        /// <summary>
        /// 블록 배치 가능 여부 확인
        /// </summary>
        public bool CanPlaceBlock(BlockPlacement placement)
        {
            // 기본 유효성 검사
            if (!IsPositionValid(placement.position))
                return false;

            // 이미 사용된 블록인지 확인
            if (IsBlockUsed(placement.player, placement.type))
                return false;

            // 충돌 검사
            if (HasCollision(placement))
                return false;

            // 첫 번째 블록인지 확인
            if (!HasPlayerPlacedFirstBlock(placement.player))
            {
                return IsFirstBlockValid(placement);
            }

            // 모서리 인접성 검사 (같은 색 블록과 모서리로 연결되어야 함)
            if (!IsCornerAdjacencyValid(placement))
                return false;

            // 변 인접성 검사 (같은 색 블록과 변으로 인접하면 안됨)
            if (!HasNoEdgeAdjacency(placement))
                return false;

            return true;
        }

        /// <summary>
        /// 블록 배치
        /// </summary>
        public bool PlaceBlock(BlockPlacement placement)
        {
            if (!CanPlaceBlock(placement))
                return false;

            // 블록 모양 가져오기
            List<Position> blockShape = GetBlockShape(placement);

            // 보드에 블록 배치
            foreach (Position pos in blockShape)
            {
                board[pos.row, pos.col] = placement.player;
                playerOccupiedCells[placement.player].Add(pos);
            }

            // 사용된 블록으로 표시
            SetPlayerBlockUsed(placement.player, placement.type);

            // 첫 블록 플래그 설정
            if (!hasPlacedFirstBlock[placement.player])
            {
                hasPlacedFirstBlock[placement.player] = true;
            }

            // 캐시 무효화
            InvalidateCache();

            return true;
        }

        /// <summary>
        /// 블록 제거 (실행취소용)
        /// </summary>
        public bool RemoveBlock(Position position)
        {
            if (!IsPositionValid(position))
                return false;

            PlayerColor owner = GetCellOwner(position);
            if (owner == PlayerColor.None)
                return false;

            // TODO: 전체 블록을 찾아서 제거하는 로직 구현
            // 현재는 단순히 해당 셀만 제거
            board[position.row, position.col] = PlayerColor.None;

            InvalidateCache();
            return true;
        }

        // ========================================
        // 게임 상태 관리
        // ========================================

        /// <summary>
        /// 현재 플레이어 반환
        /// </summary>
        public PlayerColor GetCurrentPlayer()
        {
            return currentPlayer;
        }

        /// <summary>
        /// 현재 플레이어 설정
        /// </summary>
        public void SetCurrentPlayer(PlayerColor player)
        {
            currentPlayer = player;
        }

        /// <summary>
        /// 다음 플레이어 반환
        /// </summary>
        public PlayerColor GetNextPlayer()
        {
            int current = (int)currentPlayer;
            int next = (current % GameConstants.MAX_PLAYERS) + 1;
            return (PlayerColor)next;
        }

        // ========================================
        // 블록 사용 관리
        // ========================================

        /// <summary>
        /// 플레이어 블록을 사용됨으로 표시
        /// </summary>
        public void SetPlayerBlockUsed(PlayerColor player, BlockType blockType)
        {
            if (!usedBlocks.ContainsKey(player))
                usedBlocks[player] = new HashSet<BlockType>();

            usedBlocks[player].Add(blockType);
            InvalidateCache();
        }

        /// <summary>
        /// 블록이 이미 사용되었는지 확인
        /// </summary>
        public bool IsBlockUsed(PlayerColor player, BlockType blockType)
        {
            if (!usedBlocks.ContainsKey(player))
                return false;

            return usedBlocks[player].Contains(blockType);
        }

        /// <summary>
        /// 사용된 블록 리스트 반환
        /// </summary>
        public List<BlockType> GetUsedBlocks(PlayerColor player)
        {
            if (!usedBlocks.ContainsKey(player))
                return new List<BlockType>();

            return usedBlocks[player].ToList();
        }

        /// <summary>
        /// 사용 가능한 블록 리스트 반환
        /// </summary>
        public List<BlockType> GetAvailableBlocks(PlayerColor player)
        {
            List<BlockType> available = new List<BlockType>();
            HashSet<BlockType> used = usedBlocks.ContainsKey(player) ?
                usedBlocks[player] : new HashSet<BlockType>();

            // 모든 블록 타입을 확인하여 사용되지 않은 것만 추가
            for (int i = 1; i <= 21; i++) // BlockType.Single(1) ~ BlockType.Pento_Z(21)
            {
                BlockType blockType = (BlockType)(byte)i;
                if (!used.Contains(blockType))
                {
                    available.Add(blockType);
                }
            }

            return available;
        }

        // ========================================
        // 첫 블록 관리
        // ========================================

        /// <summary>
        /// 플레이어가 첫 블록을 배치했는지 확인
        /// </summary>
        public bool HasPlayerPlacedFirstBlock(PlayerColor player)
        {
            return hasPlacedFirstBlock.ContainsKey(player) && hasPlacedFirstBlock[player];
        }

        // ========================================
        // 게임 진행 상태
        // ========================================

        /// <summary>
        /// 플레이어가 배치할 수 있는 블록이 있는지 확인
        /// </summary>
        public bool CanPlayerPlaceAnyBlock(PlayerColor player)
        {
            if (cacheValid && canPlaceAnyBlockCache.ContainsKey(player))
            {
                return canPlaceAnyBlockCache[player];
            }

            // 사용 가능한 블록들 중에서 배치 가능한 것이 있는지 확인
            List<BlockType> availableBlocks = GetAvailableBlocks(player);

            foreach (BlockType blockType in availableBlocks)
            {
                // 보드의 모든 위치에 대해 배치 가능한지 확인
                for (int row = 0; row < GameConstants.BOARD_SIZE; row++)
                {
                    for (int col = 0; col < GameConstants.BOARD_SIZE; col++)
                    {
                        Position pos = new Position(row, col);

                        // 4가지 회전과 2가지 뒤집기를 모두 시도
                        for (int rot = 0; rot < 4; rot++)
                        {
                            for (int flip = 0; flip < 2; flip++)
                            {
                                BlockPlacement placement = new BlockPlacement(
                                    blockType, pos, (Rotation)rot,
                                    (FlipState)flip, player);

                                if (CanPlaceBlock(placement))
                                {
                                    canPlaceAnyBlockCache[player] = true;
                                    return true;
                                }
                            }
                        }
                    }
                }
            }

            canPlaceAnyBlockCache[player] = false;
            playerBlockedPermanently[player] = true;
            return false;
        }

        /// <summary>
        /// 최적화된 버전 (동일한 결과)
        /// </summary>
        public bool CanPlayerPlaceAnyBlockOptimized(PlayerColor player)
        {
            return CanPlayerPlaceAnyBlock(player);
        }

        /// <summary>
        /// 게임이 종료되었는지 확인
        /// </summary>
        public bool IsGameFinished()
        {
            // 모든 플레이어가 더 이상 블록을 배치할 수 없으면 게임 종료
            for (int i = 1; i <= GameConstants.MAX_PLAYERS; i++)
            {
                PlayerColor player = (PlayerColor)i;
                if (CanPlayerPlaceAnyBlock(player))
                {
                    return false; // 한 명이라도 배치 가능하면 게임 계속
                }
            }
            return true;
        }

        /// <summary>
        /// 점수 계산
        /// </summary>
        public Dictionary<PlayerColor, int> CalculateScores()
        {
            Dictionary<PlayerColor, int> scores = new Dictionary<PlayerColor, int>();

            for (int i = 1; i <= GameConstants.MAX_PLAYERS; i++)
            {
                PlayerColor player = (PlayerColor)i;
                int score = 0;

                // 배치된 블록들의 점수 합산
                HashSet<BlockType> used = usedBlocks.ContainsKey(player) ?
                    usedBlocks[player] : new HashSet<BlockType>();

                foreach (BlockType blockType in used)
                {
                    score += GetBlockScore(blockType);
                }

                // 보너스 점수 (모든 블록을 사용한 경우)
                if (used.Count == 21) // 모든 블록 타입 사용
                {
                    score += 15; // 보너스 점수
                }

                // 마지막 블록이 Single인 경우 추가 보너스
                if (used.Count == 21 && used.Contains(BlockType.Single))
                {
                    score += 5; // 추가 보너스
                }

                scores[player] = score;
            }

            return scores;
        }

        // ========================================
        // 영구 차단 알림 관리
        // ========================================

        /// <summary>
        /// 차단 알림이 필요한지 확인
        /// </summary>
        public bool NeedsBlockedNotification(PlayerColor player)
        {
            if (playerBlockedPermanently.ContainsKey(player) &&
                playerBlockedPermanently[player])
            {
                if (!playerBlockedNotified.ContainsKey(player) ||
                    !playerBlockedNotified[player])
                {
                    playerBlockedNotified[player] = true;
                    return true;
                }
            }
            return false;
        }

        // ========================================
        // 디버깅
        // ========================================

        /// <summary>
        /// 배치된 블록 개수 반환
        /// </summary>
        public int GetPlacedBlockCount(PlayerColor player)
        {
            if (!playerOccupiedCells.ContainsKey(player))
                return 0;

            return playerOccupiedCells[player].Count;
        }

        // ========================================
        // 내부 헬퍼 함수들
        // ========================================

        /// <summary>
        /// 위치가 유효한지 확인
        /// </summary>
        private bool IsPositionValid(Position pos)
        {
            return ValidationUtility.IsValidPosition(pos);
        }

        /// <summary>
        /// 블록 배치시 충돌이 있는지 확인
        /// </summary>
        private bool HasCollision(BlockPlacement placement)
        {
            List<Position> blockShape = GetBlockShape(placement);

            foreach (Position pos in blockShape)
            {
                if (!IsPositionValid(pos) || IsCellOccupied(pos))
                {
                    return true; // 충돌 발생
                }
            }

            return false; // 충돌 없음
        }

        /// <summary>
        /// 첫 번째 블록 배치 유효성 확인
        /// </summary>
        private bool IsFirstBlockValid(BlockPlacement placement)
        {
            List<Position> blockShape = GetBlockShape(placement);

            int N = GameConstants.BOARD_SIZE - 1;
            var c1 = new Position(0, 0);
            var c2 = new Position(0, N);
            var c3 = new Position(N, 0);
            var c4 = new Position(N, N);

            // 블록이 4개 코너 중 하나라도 덮으면 첫 수로 유효
            foreach (var p in blockShape)
            {
                if (p.Equals(c1) || p.Equals(c2) || p.Equals(c3) || p.Equals(c4))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 모서리 인접성 확인 (같은 색 블록과 대각선으로 연결되어야 함)
        /// </summary>
        private bool IsCornerAdjacencyValid(BlockPlacement placement)
        {
            List<Position> blockShape = GetBlockShape(placement);

            foreach (Position pos in blockShape)
            {
                List<Position> diagonalCells = GetDiagonalCells(pos);

                foreach (Position diagonal in diagonalCells)
                {
                    if (IsPositionValid(diagonal) &&
                        GetCellOwner(diagonal) == placement.player)
                    {
                        return true; // 대각선으로 연결됨
                    }
                }
            }

            return false; // 대각선 연결 없음
        }

        /// <summary>
        /// 변 인접성 확인 (같은 색 블록과 변으로 인접하면 안됨)
        /// </summary>
        private bool HasNoEdgeAdjacency(BlockPlacement placement)
        {
            List<Position> blockShape = GetBlockShape(placement);

            foreach (Position pos in blockShape)
            {
                List<Position> adjacentCells = GetAdjacentCells(pos);

                foreach (Position adjacent in adjacentCells)
                {
                    if (IsPositionValid(adjacent) &&
                        GetCellOwner(adjacent) == placement.player)
                    {
                        return false; // 변으로 인접함 (규칙 위반)
                    }
                }
            }

            return true; // 변 인접 없음 (규칙 준수)
        }

        /// <summary>
        /// 인접한 셀들 반환 (상하좌우)
        /// </summary>
        private List<Position> GetAdjacentCells(Position pos)
        {
            List<Position> adjacent = new List<Position>();

            // 상하좌우
            adjacent.Add(new Position(pos.row - 1, pos.col));
            adjacent.Add(new Position(pos.row + 1, pos.col));
            adjacent.Add(new Position(pos.row, pos.col - 1));
            adjacent.Add(new Position(pos.row, pos.col + 1));

            return adjacent;
        }

        /// <summary>
        /// 대각선 셀들 반환
        /// </summary>
        private List<Position> GetDiagonalCells(Position pos)
        {
            List<Position> diagonal = new List<Position>();

            // 대각선 4방향
            diagonal.Add(new Position(pos.row - 1, pos.col - 1));
            diagonal.Add(new Position(pos.row - 1, pos.col + 1));
            diagonal.Add(new Position(pos.row + 1, pos.col - 1));
            diagonal.Add(new Position(pos.row + 1, pos.col + 1));

            return diagonal;
        }

        /// <summary>
        /// 블록 모양 가져오기 (변환 적용)
        /// </summary>
        private List<Position> GetBlockShape(BlockPlacement placement)
        {
            // Block 클래스 사용하여 변환된 모양 가져오기
            Block block = new Block(placement.type, placement.player);
            block.SetRotation(placement.rotation);
            block.SetFlipState(placement.flip);

            return block.GetAbsolutePositions(placement.position);
        }

        /// <summary>
        /// 캐시 무효화
        /// </summary>
        private void InvalidateCache()
        {
            cacheValid = false;
            canPlaceAnyBlockCache.Clear();
        }

        /// <summary>
        /// 모양 정규화 (좌상단 기준)
        /// </summary>
        private List<Position> NormalizeShape(List<Position> shape)
        {
            if (shape.Count == 0) return shape;

            int minRow = shape.Min(p => p.row);
            int minCol = shape.Min(p => p.col);

            return shape.Select(p => new Position(p.row - minRow, p.col - minCol)).ToList();
        }

        /// <summary>
        /// 블록 점수 계산
        /// </summary>
        public int GetBlockScore(BlockType blockType)
        {
            return blockType switch
            {
                BlockType.Single => 1,
                BlockType.Domino => 2,
                BlockType.TrioLine => 3,
                BlockType.TrioAngle => 3,
                BlockType.Tetro_I => 4,
                BlockType.Tetro_O => 4,
                BlockType.Tetro_T => 4,
                BlockType.Tetro_L => 4,
                BlockType.Tetro_S => 4,
                _ => 5 // 펜토미노는 모두 5점
            };
        }

        // ========================================
        // Block 객체 기반 메서드들 (Unity용 편의 메서드)
        // ========================================

        /// <summary>
        /// 블록 배치 가능성 확인 (Block 객체 사용)
        /// </summary>
        public bool CanPlaceBlock(Block block, Position position)
        {
            var placement = new BlockPlacement(
                block.Type,
                position,
                block.CurrentRotation,
                block.CurrentFlipState,
                block.Player
            );
            return CanPlaceBlock(placement);
        }

        /// <summary>
        /// 블록 배치 (Block 객체 사용)
        /// </summary>
        public bool PlaceBlock(Block block, Position position)
        {
            var placement = new BlockPlacement(
                block.Type,
                position,
                block.CurrentRotation,
                block.CurrentFlipState,
                block.Player
            );
            return PlaceBlock(placement);
        }

        /// <summary>
        /// 셀 색상 가져오기
        /// </summary>
        public PlayerColor GetCellColor(Position position)
        {
            if (!ValidationUtility.IsValidPosition(position))
                return PlayerColor.None;

            return board[position.row, position.col];
        }
        
        /// <summary>
        /// 보드 셀 색상 가져오기 (행, 열 인덱스 사용)
        /// </summary>
        public PlayerColor GetBoardCell(int row, int col)
        {
            if (row < 0 || row >= GameConstants.BOARD_SIZE || col < 0 || col >= GameConstants.BOARD_SIZE)
                return PlayerColor.None;
            
            return board[row, col];
        }
        
        /// <summary>
        /// 초기 보드 상태 설정 (initial_board_state 적용용)
        /// Format: color_index * 400 + (row * 20 + col)
        /// Colors: 검정(0: 0-399), 파랑(1: 400-799), 노랑(2: 800-1199), 빨강(3: 1200-1599), 초록(4: 1600-1999)
        /// </summary>
        public void SetInitialBoardState(int[] boardPositions)
        {
            if (boardPositions == null || boardPositions.Length == 0)
                return;
                
            Debug.Log($"[GameLogic] 초기 보드 상태 설정: {boardPositions.Length}개 위치");
            
            foreach (int encoded in boardPositions)
            {
                // Format: color_index * 400 + (row * 20 + col)
                int colorIndex = encoded / 400;
                int position = encoded % 400;
                int col = position % 20;
                int row = position / 20;
                
                // Debug.Log($"[GameLogic] encoded={encoded}, colorIndex={colorIndex}, position={position}, row={row}, col={col}");
                
                // 유효한 범위 체크
                if (row >= 0 && row < GameConstants.BOARD_SIZE && col >= 0 && col < GameConstants.BOARD_SIZE)
                {
                    // 색상 인덱스를 PlayerColor로 변환
                    PlayerColor color = colorIndex switch
                    {
                        0 => PlayerColor.None,     // 검정 → 빈 칸 (서버에서 변경될 예정)
                        1 => PlayerColor.Blue,     // 파랑
                        2 => PlayerColor.Yellow,   // 노랑  
                        3 => PlayerColor.Red,      // 빨강
                        4 => PlayerColor.Green,    // 초록
                        5 => PlayerColor.Obstacle, // 장애물 (어두운 회색)
                        _ => PlayerColor.Obstacle  // 기타 값은 장애물로 처리
                    };
                    
                    board[row, col] = color;
                    // Debug.Log($"[GameLogic] 초기 보드 설정: ({row},{col}) = {color} (encoded: {encoded}, colorIndex: {colorIndex})");
                }
                else
                {
                    Debug.LogWarning($"[GameLogic] 잘못된 보드 위치: row={row}, col={col} (encoded: {encoded})");
                }
            }
            
            // 캐시 무효화
            InvalidateCache();
        }
        
        /// <summary>
        /// 특정 위치의 셀이 점유되어 있는지 확인
        /// </summary>
        private bool IsCellOccupied(Position position)
        {
            if (!ValidationUtility.IsValidPosition(position))
                return true; // 보드 밖은 점유된 것으로 간주
                
            return board[position.row, position.col] != PlayerColor.None;
        }

        /// <summary>
        /// 특정 블록이 보드의 어디든 배치 가능한지 확인 (게임 종료 조건 체크용)
        /// 모든 위치, 모든 회전, 모든 플립 상태를 시도해서 하나라도 가능하면 true
        /// </summary>
        public bool CanPlaceAnywhereOnBoard(BlockType blockType, PlayerColor player)
        {
            // 이미 사용된 블록은 배치할 수 없음
            if (usedBlocks.ContainsKey(player) && usedBlocks[player].Contains(blockType))
            {
                return false;
            }

            // 모든 회전과 플립 상태 시도
            var rotations = new[] { Rotation.Degree_0, Rotation.Degree_90, Rotation.Degree_180, Rotation.Degree_270 };
            var flipStates = new[] { FlipState.Normal, FlipState.Horizontal, FlipState.Vertical, FlipState.Both };

            for (int row = 0; row < GameConstants.BOARD_SIZE; row++)
            {
                for (int col = 0; col < GameConstants.BOARD_SIZE; col++)
                {
                    var position = new Position(row, col);

                    foreach (var rotation in rotations)
                    {
                        foreach (var flipState in flipStates)
                        {
                            var placement = new BlockPlacement(blockType, position, rotation, flipState, player);
                            
                            // 하나라도 배치 가능하면 true 반환
                            if (CanPlaceBlock(placement))
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            // 모든 위치와 상태를 시도했지만 배치할 수 없음
            return false;
        }

        /// <summary>
        /// 플레이어가 사용 가능한 블록 리스트 중 하나라도 배치 가능한지 확인
        /// </summary>
        public bool CanPlaceAnyBlock(PlayerColor player, List<BlockType> availableBlocks)
        {
            if (availableBlocks == null || availableBlocks.Count == 0)
            {
                return false;
            }

            foreach (var blockType in availableBlocks)
            {
                if (CanPlaceAnywhereOnBoard(blockType, player))
                {
                    return true;
                }
            }

            return false;
        }
    }
}