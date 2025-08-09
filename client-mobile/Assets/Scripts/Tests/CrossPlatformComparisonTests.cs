using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using BlokusUnity.Common;

namespace BlokusUnity.Tests
{
    /// <summary>
    /// 크로스 플랫폼 비교 테스트
    /// C++ Common 라이브러리와 Unity C# 포팅 결과를 비교 검증
    /// 
    /// 주의: 이 테스트들은 C++ 서버와 실제 결과를 비교하기 위한 것입니다.
    /// 실제 C++ 라이브러리가 없는 환경에서는 예상 결과값을 하드코딩하여 테스트합니다.
    /// </summary>
    [TestFixture]
    public class CrossPlatformComparisonTests
    {
        // ========================================
        // C++ 구현과 동일한 결과를 확인하는 테스트들
        // ========================================
        
        [Test]
        public void GameLogic_BlokusRules_MatchesCppImplementation()
        {
            // 이 테스트는 C++ 서버에서 실행된 동일한 시나리오와 결과를 비교
            // C++ 코드에서 검증된 게임 시나리오를 Unity에서 재현
            
            GameLogic gameLogic = new GameLogic();
            
            // Scenario 1: Blue player의 첫 번째 블록 배치 (corner rule)
            BlockPlacement blueFirstBlock = new BlockPlacement(
                BlockType.Single, new Position(0, 0), Rotation.Degree_0, FlipState.Normal, PlayerColor.Blue);
            
            Assert.IsTrue(gameLogic.CanPlaceBlock(blueFirstBlock), "Blue player should be able to place first block at corner");
            Assert.IsTrue(gameLogic.PlaceBlock(blueFirstBlock), "Block placement should succeed");
            
            // Scenario 2: Yellow player의 첫 번째 블록 배치 (different corner)
            BlockPlacement yellowFirstBlock = new BlockPlacement(
                BlockType.Single, new Position(0, 19), Rotation.Degree_0, FlipState.Normal, PlayerColor.Yellow);
            
            Assert.IsTrue(gameLogic.CanPlaceBlock(yellowFirstBlock), "Yellow player should be able to place first block at corner");
            Assert.IsTrue(gameLogic.PlaceBlock(yellowFirstBlock), "Yellow block placement should succeed");
            
            // Scenario 3: Blue player의 두 번째 블록 (diagonal connection required)
            BlockPlacement blueSecondBlock = new BlockPlacement(
                BlockType.Domino, new Position(1, 1), Rotation.Degree_0, FlipState.Normal, PlayerColor.Blue);
            
            Assert.IsTrue(gameLogic.CanPlaceBlock(blueSecondBlock), "Blue should be able to place block diagonally");
            Assert.IsTrue(gameLogic.PlaceBlock(blueSecondBlock), "Blue second block placement should succeed");
            
            // Scenario 4: Invalid placement (edge adjacency)
            BlockPlacement invalidBlock = new BlockPlacement(
                BlockType.Single, new Position(0, 1), Rotation.Degree_0, FlipState.Normal, PlayerColor.Blue);
            
            Assert.IsFalse(gameLogic.CanPlaceBlock(invalidBlock), "Edge adjacency should be prohibited");
            
            // Score verification (matches C++ calculation)
            Dictionary<PlayerColor, int> scores = gameLogic.CalculateScores();
            Assert.AreEqual(3, scores[PlayerColor.Blue], "Blue score should be Single(1) + Domino(2) = 3");
            Assert.AreEqual(1, scores[PlayerColor.Yellow], "Yellow score should be Single(1) = 1");
        }
        
        [Test]
        public void Block_TransformationsMatchCppResults()
        {
            // C++ Block 클래스와 동일한 변환 결과를 검증
            
            // Test 1: L-shaped tetromino rotation
            Block tetroL = new Block(BlockType.Tetro_L, PlayerColor.Blue);
            List<Position> originalShape = tetroL.GetCurrentShape();
            
            // Original L-tetromino normalized shape: (0,0), (0,1), (0,2), (1,0)
            Assert.AreEqual(4, originalShape.Count);
            Assert.Contains(new Position(0, 0), originalShape);
            Assert.Contains(new Position(0, 1), originalShape);
            Assert.Contains(new Position(0, 2), originalShape);
            Assert.Contains(new Position(1, 0), originalShape);
            
            // 90도 회전 후 정규화된 결과
            tetroL.SetRotation(Rotation.Degree_90);
            List<Position> rotated90Shape = tetroL.GetCurrentShape();
            Assert.AreEqual(4, rotated90Shape.Count);
            
            // 180도 회전 후 정규화된 결과
            tetroL.SetRotation(Rotation.Degree_180);
            List<Position> rotated180Shape = tetroL.GetCurrentShape();
            Assert.AreEqual(4, rotated180Shape.Count);
            
            // 270도 회전 후 정규화된 결과
            tetroL.SetRotation(Rotation.Degree_270);
            List<Position> rotated270Shape = tetroL.GetCurrentShape();
            Assert.AreEqual(4, rotated270Shape.Count);
            
            // 360도 회전 (= 0도)는 원래와 같아야 함
            tetroL.SetRotation(Rotation.Degree_0);
            List<Position> fullRotationShape = tetroL.GetCurrentShape();
            CollectionAssert.AreEquivalent(originalShape, fullRotationShape);
        }
        
        [Test]
        public void Block_FlipTransformationsMatchCppResults()
        {
            // Test horizontal flip of Pento_F (비대칭 블록)
            Block pentoF = new Block(BlockType.Pento_F, PlayerColor.Red);
            List<Position> originalShape = pentoF.GetCurrentShape();
            
            // 수평 뒤집기
            pentoF.SetFlipState(FlipState.Horizontal);
            List<Position> horizontalFlipShape = pentoF.GetCurrentShape();
            
            // 원래와 다른 모양이어야 함 (비대칭이므로)
            CollectionAssert.AreNotEquivalent(originalShape, horizontalFlipShape);
            
            // 다시 수평 뒤집기 하면 원래대로
            pentoF.SetFlipState(FlipState.Normal);
            List<Position> backToOriginalShape = pentoF.GetCurrentShape();
            CollectionAssert.AreEquivalent(originalShape, backToOriginalShape);
        }
        
        [Test]
        public void GameLogic_ComplexScenario_MatchesCppResults()
        {
            // C++에서 검증된 복잡한 게임 시나리오를 Unity에서 재현
            GameLogic gameLogic = new GameLogic();
            
            // Scenario: 4명 플레이어가 각각 첫 블록을 코너에 배치
            
            // Blue at (0, 0)
            Assert.IsTrue(gameLogic.PlaceBlock(new BlockPlacement(
                BlockType.Single, new Position(0, 0), Rotation.Degree_0, FlipState.Normal, PlayerColor.Blue)));
            
            // Yellow at (0, 19)
            Assert.IsTrue(gameLogic.PlaceBlock(new BlockPlacement(
                BlockType.Single, new Position(0, 19), Rotation.Degree_0, FlipState.Normal, PlayerColor.Yellow)));
            
            // Red at (19, 19)
            Assert.IsTrue(gameLogic.PlaceBlock(new BlockPlacement(
                BlockType.Single, new Position(19, 19), Rotation.Degree_0, FlipState.Normal, PlayerColor.Red)));
            
            // Green at (19, 0)
            Assert.IsTrue(gameLogic.PlaceBlock(new BlockPlacement(
                BlockType.Single, new Position(19, 0), Rotation.Degree_0, FlipState.Normal, PlayerColor.Green)));
            
            // 각 플레이어가 두 번째 블록 배치 (대각선 연결)
            Assert.IsTrue(gameLogic.PlaceBlock(new BlockPlacement(
                BlockType.Domino, new Position(1, 1), Rotation.Degree_0, FlipState.Normal, PlayerColor.Blue)));
            
            Assert.IsTrue(gameLogic.PlaceBlock(new BlockPlacement(
                BlockType.Domino, new Position(1, 17), Rotation.Degree_90, FlipState.Normal, PlayerColor.Yellow)));
            
            Assert.IsTrue(gameLogic.PlaceBlock(new BlockPlacement(
                BlockType.TrioLine, new Position(16, 17), Rotation.Degree_180, FlipState.Normal, PlayerColor.Red)));
            
            Assert.IsTrue(gameLogic.PlaceBlock(new BlockPlacement(
                BlockType.TrioAngle, new Position(17, 1), Rotation.Degree_270, FlipState.Normal, PlayerColor.Green)));
            
            // 점수 계산 (C++과 동일한 결과여야 함)
            Dictionary<PlayerColor, int> finalScores = gameLogic.CalculateScores();
            Assert.AreEqual(3, finalScores[PlayerColor.Blue]);    // Single(1) + Domino(2) = 3
            Assert.AreEqual(3, finalScores[PlayerColor.Yellow]);  // Single(1) + Domino(2) = 3
            Assert.AreEqual(4, finalScores[PlayerColor.Red]);     // Single(1) + TrioLine(3) = 4
            Assert.AreEqual(4, finalScores[PlayerColor.Green]);   // Single(1) + TrioAngle(3) = 4
            
            // 사용 가능한 블록 수 확인
            Assert.AreEqual(19, gameLogic.GetAvailableBlocks(PlayerColor.Blue).Count);  // 21 - 2 = 19
            Assert.AreEqual(19, gameLogic.GetAvailableBlocks(PlayerColor.Yellow).Count);
            Assert.AreEqual(19, gameLogic.GetAvailableBlocks(PlayerColor.Red).Count);
            Assert.AreEqual(19, gameLogic.GetAvailableBlocks(PlayerColor.Green).Count);
            
            // 게임이 아직 끝나지 않아야 함
            Assert.IsFalse(gameLogic.IsGameFinished());
        }
        
        [Test]
        public void BlockShapes_Match21StandardPiecesExactly()
        {
            // 블로쿠스 표준 21개 피스가 정확히 구현되었는지 확인
            
            // 1칸: 1개
            List<BlockType> size1Blocks = BlockFactory.GetBlocksBySize(1);
            Assert.AreEqual(1, size1Blocks.Count);
            
            // 2칸: 1개
            List<BlockType> size2Blocks = BlockFactory.GetBlocksBySize(2);
            Assert.AreEqual(1, size2Blocks.Count);
            
            // 3칸: 2개
            List<BlockType> size3Blocks = BlockFactory.GetBlocksBySize(3);
            Assert.AreEqual(2, size3Blocks.Count);
            
            // 4칸: 5개 (테트로미노)
            List<BlockType> size4Blocks = BlockFactory.GetBlocksBySize(4);
            Assert.AreEqual(5, size4Blocks.Count);
            
            // 5칸: 12개 (펜토미노)
            List<BlockType> size5Blocks = BlockFactory.GetBlocksBySize(5);
            Assert.AreEqual(12, size5Blocks.Count);
            
            // 총 21개
            List<BlockType> allBlocks = BlockFactory.GetAllBlockTypes();
            Assert.AreEqual(21, allBlocks.Count);
            
            // 중복 없이 21개 고유한 블록 타입
            HashSet<BlockType> uniqueBlocks = new HashSet<BlockType>(allBlocks);
            Assert.AreEqual(21, uniqueBlocks.Count);
        }
        
        [Test]
        public void BlockFactory_Names_MatchKoreanStandard()
        {
            // 한국어 블록 이름이 표준과 일치하는지 확인
            
            Assert.AreEqual("단일", BlockFactory.GetBlockName(BlockType.Single));
            Assert.AreEqual("도미노", BlockFactory.GetBlockName(BlockType.Domino));
            Assert.AreEqual("3직선", BlockFactory.GetBlockName(BlockType.TrioLine));
            Assert.AreEqual("3꺾임", BlockFactory.GetBlockName(BlockType.TrioAngle));
            Assert.AreEqual("I자", BlockFactory.GetBlockName(BlockType.Tetro_I));
            Assert.AreEqual("O자", BlockFactory.GetBlockName(BlockType.Tetro_O));
            Assert.AreEqual("T자", BlockFactory.GetBlockName(BlockType.Tetro_T));
            Assert.AreEqual("L자", BlockFactory.GetBlockName(BlockType.Tetro_L));
            Assert.AreEqual("S자", BlockFactory.GetBlockName(BlockType.Tetro_S));
            Assert.AreEqual("X자", BlockFactory.GetBlockName(BlockType.Pento_X));
            
            // 점수도 함께 확인
            Assert.AreEqual("단일 (1칸)", BlockFactory.GetBlockDescription(BlockType.Single));
            Assert.AreEqual("도미노 (2칸)", BlockFactory.GetBlockDescription(BlockType.Domino));
            Assert.AreEqual("X자 (5칸)", BlockFactory.GetBlockDescription(BlockType.Pento_X));
        }
        
        [Test]
        public void GameConstants_MatchServerImplementation()
        {
            // 게임 상수들이 C++ 서버와 일치하는지 확인
            
            Assert.AreEqual(20, GameConstants.BOARD_SIZE);
            Assert.AreEqual(4, GameConstants.MAX_PLAYERS);
            
            // 각 플레이어의 시작 코너 위치 확인
            Assert.IsTrue(ValidationUtility.IsValidPosition(new Position(0, 0)));     // Blue corner
            Assert.IsTrue(ValidationUtility.IsValidPosition(new Position(0, 19)));    // Yellow corner
            Assert.IsTrue(ValidationUtility.IsValidPosition(new Position(19, 19)));   // Red corner
            Assert.IsTrue(ValidationUtility.IsValidPosition(new Position(19, 0)));    // Green corner
        }
        
        [Test]
        public void ScoreCalculation_IncludesBonusesCorrectly()
        {
            // 점수 계산이 C++ 구현과 동일한지 확인 (보너스 포함)
            
            GameLogic gameLogic = new GameLogic();
            
            // 플레이어가 모든 블록을 사용한 시나리오 시뮬레이션
            // (실제로는 불가능하지만, 점수 계산 로직 테스트용)
            
            // 모든 블록 타입을 사용됨으로 표시
            List<BlockType> allBlockTypes = BlockFactory.GetAllBlockTypes();
            foreach (BlockType blockType in allBlockTypes)
            {
                gameLogic.SetPlayerBlockUsed(PlayerColor.Blue, blockType);
            }
            
            Dictionary<PlayerColor, int> scores = gameLogic.CalculateScores();
            
            // 기본 점수: 1+2+3+3+4+4+4+4+4+5*12 = 89
            // 모든 블록 사용 보너스: +15
            // Single이 마지막 블록인 경우 추가 보너스: +5
            // 총 예상 점수: 89 + 15 + 5 = 109
            Assert.AreEqual(109, scores[PlayerColor.Blue]);
        }
        
        [Test]
        public void ValidationUtility_BoundaryChecking_IsStrict()
        {
            // 경계 검사가 정확한지 확인
            
            // 유효한 경계 위치들
            Assert.IsTrue(ValidationUtility.IsValidPosition(new Position(0, 0)));
            Assert.IsTrue(ValidationUtility.IsValidPosition(new Position(0, 19)));
            Assert.IsTrue(ValidationUtility.IsValidPosition(new Position(19, 0)));
            Assert.IsTrue(ValidationUtility.IsValidPosition(new Position(19, 19)));
            Assert.IsTrue(ValidationUtility.IsValidPosition(new Position(10, 10)));
            
            // 무효한 위치들
            Assert.IsFalse(ValidationUtility.IsValidPosition(new Position(-1, 0)));
            Assert.IsFalse(ValidationUtility.IsValidPosition(new Position(0, -1)));
            Assert.IsFalse(ValidationUtility.IsValidPosition(new Position(20, 19)));
            Assert.IsFalse(ValidationUtility.IsValidPosition(new Position(19, 20)));
            Assert.IsFalse(ValidationUtility.IsValidPosition(new Position(-1, -1)));
            Assert.IsFalse(ValidationUtility.IsValidPosition(new Position(20, 20)));
        }
        
        [Test]
        public void PerformanceComparison_WithCppBenchmarks()
        {
            // C++ 구현과 성능을 비교하는 기준 테스트
            
            GameLogic gameLogic = new GameLogic();
            
            // 첫 블록 배치
            gameLogic.PlaceBlock(new BlockPlacement(
                BlockType.Single, new Position(0, 0), Rotation.Degree_0, FlipState.Normal, PlayerColor.Blue));
            
            // CanPlayerPlaceAnyBlock 성능 측정
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            
            bool canPlace = gameLogic.CanPlayerPlaceAnyBlock(PlayerColor.Blue);
            
            sw.Stop();
            
            Assert.IsTrue(canPlace);
            
            // C++ 구현에서 일반적으로 50ms 이하로 실행되므로, Unity에서도 비슷한 성능을 기대
            Assert.Less(sw.ElapsedMilliseconds, 200, $"CanPlayerPlaceAnyBlock took {sw.ElapsedMilliseconds}ms, expected < 200ms");
            
            Debug.Log($"CanPlayerPlaceAnyBlock 성능: {sw.ElapsedMilliseconds}ms");
        }
        
        [Test]
        public void EndGameCondition_MatchesCppLogic()
        {
            // 게임 종료 조건이 C++ 구현과 동일한지 확인
            
            GameLogic gameLogic = new GameLogic();
            
            // 게임 시작 시에는 종료되지 않아야 함
            Assert.IsFalse(gameLogic.IsGameFinished());
            
            // 모든 플레이어가 블록을 배치할 수 없는 상황을 시뮬레이션
            // (실제로는 복잡하지만, 테스트를 위해 가정)
            
            // 각 플레이어의 첫 블록 배치
            gameLogic.PlaceBlock(new BlockPlacement(BlockType.Single, new Position(0, 0), Rotation.Degree_0, FlipState.Normal, PlayerColor.Blue));
            gameLogic.PlaceBlock(new BlockPlacement(BlockType.Single, new Position(0, 19), Rotation.Degree_0, FlipState.Normal, PlayerColor.Yellow));
            gameLogic.PlaceBlock(new BlockPlacement(BlockType.Single, new Position(19, 19), Rotation.Degree_0, FlipState.Normal, PlayerColor.Red));
            gameLogic.PlaceBlock(new BlockPlacement(BlockType.Single, new Position(19, 0), Rotation.Degree_0, FlipState.Normal, PlayerColor.Green));
            
            // 아직 많은 블록이 남아있으므로 게임이 계속되어야 함
            Assert.IsFalse(gameLogic.IsGameFinished());
            
            // 각 플레이어가 블록을 배치할 수 있어야 함
            Assert.IsTrue(gameLogic.CanPlayerPlaceAnyBlock(PlayerColor.Blue));
            Assert.IsTrue(gameLogic.CanPlayerPlaceAnyBlock(PlayerColor.Yellow));
            Assert.IsTrue(gameLogic.CanPlayerPlaceAnyBlock(PlayerColor.Red));
            Assert.IsTrue(gameLogic.CanPlayerPlaceAnyBlock(PlayerColor.Green));
        }
    }
}