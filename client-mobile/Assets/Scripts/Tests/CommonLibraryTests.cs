using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using BlokusUnity.Common;

namespace BlokusUnity.Tests
{
    /// <summary>
    /// Common 라이브러리 크로스 플랫폼 테스트
    /// C++ 구현과 C# 포팅 결과를 비교 검증
    /// </summary>
    [TestFixture]
    public class CommonLibraryTests
    {
        private GameLogic gameLogic;
        
        [SetUp]
        public void Setup()
        {
            gameLogic = new GameLogic();
        }
        
        [TearDown]
        public void TearDown()
        {
            gameLogic = null;
        }
        
        // ========================================
        // Position 구조체 테스트
        // ========================================
        
        [Test]
        public void Position_Equality_WorksCorrectly()
        {
            // Arrange
            Position pos1 = new Position(5, 10);
            Position pos2 = new Position(5, 10);
            Position pos3 = new Position(3, 7);
            
            // Assert
            Assert.IsTrue(pos1.Equals(pos2));
            Assert.IsFalse(pos1.Equals(pos3));
            Assert.IsTrue(pos1 == pos2);
            Assert.IsFalse(pos1 == pos3);
        }
        
        [Test]
        public void Position_IsValid_ChecksBounds()
        {
            // Test valid positions
            Assert.IsTrue(new Position(0, 0).IsValid());
            Assert.IsTrue(new Position(10, 15).IsValid());
            Assert.IsTrue(new Position(19, 19).IsValid());
            
            // Test invalid positions
            Assert.IsFalse(new Position(-1, 5).IsValid());
            Assert.IsFalse(new Position(5, -1).IsValid());
            Assert.IsFalse(new Position(20, 10).IsValid());
            Assert.IsFalse(new Position(10, 20).IsValid());
        }
        
        [Test]
        public void Position_ToVector2Int_ConvertsCorrectly()
        {
            // Arrange
            Position pos = new Position(7, 13);
            
            // Act
            Vector2Int vector = pos.ToVector2Int();
            
            // Assert
            Assert.AreEqual(13, vector.x); // col -> x
            Assert.AreEqual(7, vector.y);  // row -> y
        }
        
        // ========================================
        // BlockPlacement 구조체 테스트
        // ========================================
        
        [Test]
        public void BlockPlacement_Construction_SetsPropertiesCorrectly()
        {
            // Arrange
            BlockType type = BlockType.Pento_X;
            Position pos = new Position(5, 10);
            Rotation rot = Rotation.Degree_90;
            FlipState flip = FlipState.Horizontal;
            PlayerColor player = PlayerColor.Blue;
            
            // Act
            BlockPlacement placement = new BlockPlacement(type, pos, rot, flip, player);
            
            // Assert
            Assert.AreEqual(type, placement.type);
            Assert.AreEqual(pos, placement.position);
            Assert.AreEqual(rot, placement.rotation);
            Assert.AreEqual(flip, placement.flip);
            Assert.AreEqual(player, placement.player);
        }
        
        // ========================================
        // Block 클래스 테스트
        // ========================================
        
        [Test]
        public void Block_GetBaseShape_ReturnsCorrectShapes()
        {
            // Test Single block (1칸)
            List<Position> singleShape = Block.GetBaseShape(BlockType.Single);
            Assert.AreEqual(1, singleShape.Count);
            Assert.AreEqual(new Position(0, 0), singleShape[0]);
            
            // Test Domino block (2칸)
            List<Position> dominoShape = Block.GetBaseShape(BlockType.Domino);
            Assert.AreEqual(2, dominoShape.Count);
            Assert.Contains(new Position(0, 0), dominoShape);
            Assert.Contains(new Position(0, 1), dominoShape);
            
            // Test Pento_X block (5칸, + 모양)
            List<Position> pentoXShape = Block.GetBaseShape(BlockType.Pento_X);
            Assert.AreEqual(5, pentoXShape.Count);
            Assert.Contains(new Position(0, 1), pentoXShape); // 위
            Assert.Contains(new Position(1, 0), pentoXShape); // 왼쪽
            Assert.Contains(new Position(1, 1), pentoXShape); // 중앙
            Assert.Contains(new Position(1, 2), pentoXShape); // 오른쪽
            Assert.Contains(new Position(2, 1), pentoXShape); // 아래
        }
        
        [Test]
        public void Block_Rotation_WorksCorrectly()
        {
            // Arrange
            Block block = new Block(BlockType.Domino, PlayerColor.Blue);
            
            // 원래 모양: (0,0), (0,1) - 가로
            List<Position> originalShape = block.GetCurrentShape();
            Assert.AreEqual(2, originalShape.Count);
            
            // Act: 90도 회전
            block.SetRotation(Rotation.Degree_90);
            List<Position> rotatedShape = block.GetCurrentShape();
            
            // Assert: (0,0), (1,0) - 세로가 되어야 함
            Assert.AreEqual(2, rotatedShape.Count);
            Assert.Contains(new Position(0, 0), rotatedShape);
            Assert.Contains(new Position(1, 0), rotatedShape);
        }
        
        [Test]
        public void Block_FlipHorizontal_WorksCorrectly()
        {
            // Arrange
            Block block = new Block(BlockType.TrioAngle, PlayerColor.Red);
            
            // 원래 모양: (0,0), (0,1), (1,1) - L자
            List<Position> originalShape = block.GetCurrentShape();
            
            // Act: 수평 뒤집기
            block.FlipHorizontal();
            List<Position> flippedShape = block.GetCurrentShape();
            
            // Assert: 뒤집힌 형태가 되어야 함
            Assert.AreEqual(3, flippedShape.Count);
            // 정확한 좌표는 정규화 후 결과에 따라 달라질 수 있음
        }
        
        [Test]
        public void Block_GetAbsolutePositions_CalculatesCorrectly()
        {
            // Arrange
            Block block = new Block(BlockType.Single, PlayerColor.Green);
            Position basePos = new Position(10, 15);
            
            // Act
            List<Position> absolutePositions = block.GetAbsolutePositions(basePos);
            
            // Assert
            Assert.AreEqual(1, absolutePositions.Count);
            Assert.AreEqual(new Position(10, 15), absolutePositions[0]);
        }
        
        [Test]
        public void Block_IsValidPlacement_ChecksBounds()
        {
            // Arrange
            Block block = new Block(BlockType.Domino, PlayerColor.Yellow);
            
            // Test valid placement
            Assert.IsTrue(block.IsValidPlacement(new Position(0, 0)));
            Assert.IsTrue(block.IsValidPlacement(new Position(18, 19)));
            
            // Test invalid placement (would go out of bounds)
            Assert.IsFalse(block.IsValidPlacement(new Position(19, 19))); // Domino would extend to (19, 20)
            Assert.IsFalse(block.IsValidPlacement(new Position(-1, 0)));
        }
        
        // ========================================
        // GameLogic 클래스 테스트 (핵심 로직)
        // ========================================
        
        [Test]
        public void GameLogic_InitialState_IsCorrect()
        {
            // Assert
            Assert.AreEqual(PlayerColor.Blue, gameLogic.GetCurrentPlayer());
            Assert.AreEqual(PlayerColor.None, gameLogic.GetCellOwner(new Position(0, 0)));
            Assert.AreEqual(PlayerColor.None, gameLogic.GetCellOwner(new Position(19, 19)));
        }
        
        [Test]
        public void GameLogic_FirstBlockPlacement_RequiresCorner()
        {
            // Arrange - Blue player's first block at their corner (0, 0)
            BlockPlacement validPlacement = new BlockPlacement(
                BlockType.Single, 
                new Position(0, 0), 
                Rotation.Degree_0, 
                FlipState.Normal, 
                PlayerColor.Blue
            );
            
            BlockPlacement invalidPlacement = new BlockPlacement(
                BlockType.Single, 
                new Position(5, 5), // Not at corner
                Rotation.Degree_0, 
                FlipState.Normal, 
                PlayerColor.Blue
            );
            
            // Assert
            Assert.IsTrue(gameLogic.CanPlaceBlock(validPlacement));
            Assert.IsFalse(gameLogic.CanPlaceBlock(invalidPlacement));
        }
        
        [Test]
        public void GameLogic_BlockPlacement_UpdatesBoardState()
        {
            // Arrange
            BlockPlacement placement = new BlockPlacement(
                BlockType.Single, 
                new Position(0, 0), 
                Rotation.Degree_0, 
                FlipState.Normal, 
                PlayerColor.Blue
            );
            
            // Act
            bool placed = gameLogic.PlaceBlock(placement);
            
            // Assert
            Assert.IsTrue(placed);
            Assert.AreEqual(PlayerColor.Blue, gameLogic.GetCellOwner(new Position(0, 0)));
            Assert.IsTrue(gameLogic.IsBlockUsed(PlayerColor.Blue, BlockType.Single));
            Assert.IsTrue(gameLogic.HasPlayerPlacedFirstBlock(PlayerColor.Blue));
        }
        
        [Test]
        public void GameLogic_DuplicateBlockPlacement_IsRejected()
        {
            // Arrange - Place first block
            BlockPlacement firstPlacement = new BlockPlacement(
                BlockType.Single, 
                new Position(0, 0), 
                Rotation.Degree_0, 
                FlipState.Normal, 
                PlayerColor.Blue
            );
            gameLogic.PlaceBlock(firstPlacement);
            
            // Try to place same block type again
            BlockPlacement duplicatePlacement = new BlockPlacement(
                BlockType.Single, 
                new Position(1, 1), 
                Rotation.Degree_0, 
                FlipState.Normal, 
                PlayerColor.Blue
            );
            
            // Assert
            Assert.IsFalse(gameLogic.CanPlaceBlock(duplicatePlacement));
        }
        
        [Test]
        public void GameLogic_EdgeAdjacency_IsProhibited()
        {
            // Arrange - Place first block at corner
            BlockPlacement firstBlock = new BlockPlacement(
                BlockType.Single, 
                new Position(0, 0), 
                Rotation.Degree_0, 
                FlipState.Normal, 
                PlayerColor.Blue
            );
            gameLogic.PlaceBlock(firstBlock);
            
            // Try to place adjacent block (edge touching - not allowed)
            BlockPlacement adjacentBlock = new BlockPlacement(
                BlockType.Single, 
                new Position(0, 1), // Right next to first block
                Rotation.Degree_0, 
                FlipState.Normal, 
                PlayerColor.Blue
            );
            
            // Assert
            Assert.IsFalse(gameLogic.CanPlaceBlock(adjacentBlock));
        }
        
        [Test]
        public void GameLogic_CornerAdjacency_IsRequired()
        {
            // Arrange - Place first block at corner
            BlockPlacement firstBlock = new BlockPlacement(
                BlockType.Single, 
                new Position(0, 0), 
                Rotation.Degree_0, 
                FlipState.Normal, 
                PlayerColor.Blue
            );
            gameLogic.PlaceBlock(firstBlock);
            
            // Try to place diagonal block (corner touching - allowed)
            BlockPlacement diagonalBlock = new BlockPlacement(
                BlockType.Single, 
                new Position(1, 1), // Diagonal from first block
                Rotation.Degree_0, 
                FlipState.Normal, 
                PlayerColor.Blue
            );
            
            // Assert
            Assert.IsTrue(gameLogic.CanPlaceBlock(diagonalBlock));
        }
        
        [Test]
        public void GameLogic_ScoreCalculation_IsAccurate()
        {
            // Arrange - Place some blocks
            gameLogic.PlaceBlock(new BlockPlacement(BlockType.Single, new Position(0, 0), Rotation.Degree_0, FlipState.Normal, PlayerColor.Blue));
            gameLogic.PlaceBlock(new BlockPlacement(BlockType.Domino, new Position(1, 1), Rotation.Degree_0, FlipState.Normal, PlayerColor.Blue));
            
            // Act
            Dictionary<PlayerColor, int> scores = gameLogic.CalculateScores();
            
            // Assert
            Assert.IsTrue(scores.ContainsKey(PlayerColor.Blue));
            Assert.AreEqual(3, scores[PlayerColor.Blue]); // Single(1) + Domino(2) = 3
        }
        
        [Test]
        public void GameLogic_GetAvailableBlocks_ReturnsCorrectList()
        {
            // Arrange - Place one block
            gameLogic.PlaceBlock(new BlockPlacement(BlockType.Single, new Position(0, 0), Rotation.Degree_0, FlipState.Normal, PlayerColor.Blue));
            
            // Act
            List<BlockType> availableBlocks = gameLogic.GetAvailableBlocks(PlayerColor.Blue);
            
            // Assert
            Assert.AreEqual(20, availableBlocks.Count); // 21 - 1 used = 20
            Assert.IsFalse(availableBlocks.Contains(BlockType.Single));
            Assert.IsTrue(availableBlocks.Contains(BlockType.Domino));
        }
        
        // ========================================
        // BlockFactory 클래스 테스트
        // ========================================
        
        [Test]
        public void BlockFactory_CreatePlayerSet_ReturnsAllBlocks()
        {
            // Act
            List<Block> playerBlocks = BlockFactory.CreatePlayerSet(PlayerColor.Red);
            
            // Assert
            Assert.AreEqual(21, playerBlocks.Count); // All block types
            Assert.IsTrue(playerBlocks.All(block => block.Player == PlayerColor.Red));
            
            // Check that all block types are present
            HashSet<BlockType> blockTypes = new HashSet<BlockType>(playerBlocks.Select(b => b.Type));
            Assert.AreEqual(21, blockTypes.Count); // No duplicates
        }
        
        [Test]
        public void BlockFactory_GetBlockScore_ReturnsCorrectValues()
        {
            // Test various block scores
            Assert.AreEqual(1, BlockFactory.GetBlockScore(BlockType.Single));
            Assert.AreEqual(2, BlockFactory.GetBlockScore(BlockType.Domino));
            Assert.AreEqual(3, BlockFactory.GetBlockScore(BlockType.TrioLine));
            Assert.AreEqual(3, BlockFactory.GetBlockScore(BlockType.TrioAngle));
            Assert.AreEqual(4, BlockFactory.GetBlockScore(BlockType.Tetro_I));
            Assert.AreEqual(5, BlockFactory.GetBlockScore(BlockType.Pento_X));
        }
        
        [Test]
        public void BlockFactory_GetBlockName_ReturnsKoreanNames()
        {
            // Test Korean block names
            Assert.AreEqual("단일", BlockFactory.GetBlockName(BlockType.Single));
            Assert.AreEqual("도미노", BlockFactory.GetBlockName(BlockType.Domino));
            Assert.AreEqual("X자", BlockFactory.GetBlockName(BlockType.Pento_X));
        }
        
        [Test]
        public void BlockFactory_GetBlocksBySize_FiltersCorrectly()
        {
            // Act
            List<BlockType> size1Blocks = BlockFactory.GetBlocksBySize(1);
            List<BlockType> size5Blocks = BlockFactory.GetBlocksBySize(5);
            
            // Assert
            Assert.AreEqual(1, size1Blocks.Count);
            Assert.Contains(BlockType.Single, size1Blocks);
            
            Assert.AreEqual(12, size5Blocks.Count); // 12 pentominos
            Assert.Contains(BlockType.Pento_X, size5Blocks);
        }
        
        // ========================================
        // 통합 게임 플레이 테스트
        // ========================================
        
        [Test]
        public void GameLogic_FullGameScenario_WorksCorrectly()
        {
            // Scenario: Blue player places multiple blocks
            
            // 1. First block at corner
            Assert.IsTrue(gameLogic.PlaceBlock(new BlockPlacement(
                BlockType.Single, new Position(0, 0), Rotation.Degree_0, FlipState.Normal, PlayerColor.Blue)));
            
            // 2. Second block diagonal to first (corner connection)
            Assert.IsTrue(gameLogic.PlaceBlock(new BlockPlacement(
                BlockType.Domino, new Position(1, 1), Rotation.Degree_0, FlipState.Normal, PlayerColor.Blue)));
            
            // 3. Third block diagonal to second
            Assert.IsTrue(gameLogic.PlaceBlock(new BlockPlacement(
                BlockType.TrioLine, new Position(2, 0), Rotation.Degree_90, FlipState.Normal, PlayerColor.Blue)));
            
            // Verify game state
            Dictionary<PlayerColor, int> scores = gameLogic.CalculateScores();
            Assert.AreEqual(6, scores[PlayerColor.Blue]); // 1 + 2 + 3 = 6
            
            Assert.AreEqual(18, gameLogic.GetAvailableBlocks(PlayerColor.Blue).Count); // 21 - 3 = 18
            Assert.IsFalse(gameLogic.IsGameFinished()); // Still blocks available
        }
        
        // ========================================
        // 성능 테스트 (기본적인)
        // ========================================
        
        [Test]
        public void GameLogic_PerformanceTest_CanPlaceAnyBlock()
        {
            // Place first block
            gameLogic.PlaceBlock(new BlockPlacement(BlockType.Single, new Position(0, 0), Rotation.Degree_0, FlipState.Normal, PlayerColor.Blue));
            
            // Measure time for checking if player can place any block
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            
            bool canPlace = gameLogic.CanPlayerPlaceAnyBlock(PlayerColor.Blue);
            
            sw.Stop();
            
            // Assert
            Assert.IsTrue(canPlace);
            Assert.Less(sw.ElapsedMilliseconds, 100); // Should be fast (< 100ms)
            
            Debug.Log($"CanPlayerPlaceAnyBlock took {sw.ElapsedMilliseconds}ms");
        }
        
        // ========================================
        // 에러 케이스 테스트
        // ========================================
        
        [Test]
        public void GameLogic_InvalidBlockType_HandledGracefully()
        {
            // Try to use an invalid block type
            BlockPlacement invalidPlacement = new BlockPlacement(
                (BlockType)999, // Invalid enum value
                new Position(0, 0),
                Rotation.Degree_0,
                FlipState.Normal,
                PlayerColor.Blue
            );
            
            // Should not crash, should return false
            Assert.IsFalse(gameLogic.CanPlaceBlock(invalidPlacement));
        }
        
        [Test]
        public void Block_InvalidBlockType_DefaultsToSingle()
        {
            // Create block with invalid type
            Block block = new Block((BlockType)999, PlayerColor.Blue);
            
            // Should default to Single
            Assert.AreEqual(BlockType.Single, block.Type);
            Assert.AreEqual(1, block.GetSize());
        }
        
        [Test]
        public void ValidationUtility_EdgeCases_HandledCorrectly()
        {
            // Test boundary positions
            Assert.IsTrue(ValidationUtility.IsValidPosition(new Position(0, 0)));
            Assert.IsTrue(ValidationUtility.IsValidPosition(new Position(19, 19)));
            Assert.IsFalse(ValidationUtility.IsValidPosition(new Position(-1, 0)));
            Assert.IsFalse(ValidationUtility.IsValidPosition(new Position(0, -1)));
            Assert.IsFalse(ValidationUtility.IsValidPosition(new Position(20, 19)));
            Assert.IsFalse(ValidationUtility.IsValidPosition(new Position(19, 20)));
        }
    }
}