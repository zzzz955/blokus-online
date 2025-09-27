/**
 * Random Block Placement Algorithm for Blokus
 * Places blocks following Blokus rules starting from a corner
 */

// Import correct block shapes from BoardEditor
enum BlockType {
  Single = 'Single',
  Domino = 'Domino',
  TrioLine = 'TrioLine',
  TrioAngle = 'TrioAngle',
  Tetro_I = 'Tetro_I',
  Tetro_O = 'Tetro_O',
  Tetro_T = 'Tetro_T',
  Tetro_L = 'Tetro_L',
  Tetro_S = 'Tetro_S',
  Pento_F = 'Pento_F',
  Pento_I = 'Pento_I',
  Pento_L = 'Pento_L',
  Pento_N = 'Pento_N',
  Pento_P = 'Pento_P',
  Pento_T = 'Pento_T',
  Pento_U = 'Pento_U',
  Pento_V = 'Pento_V',
  Pento_W = 'Pento_W',
  Pento_X = 'Pento_X',
  Pento_Y = 'Pento_Y',
  Pento_Z = 'Pento_Z'
}

type BlockShape = [number, number][];

// Correct block shapes copied exactly from BoardEditor.tsx
const CORRECT_BLOCK_SHAPES: Record<BlockType, BlockShape> = {
  [BlockType.Single]: [[0, 0]],
  [BlockType.Domino]: [[0, 0], [0, 1]],
  [BlockType.TrioLine]: [[0, 0], [0, 1], [0, 2]],
  [BlockType.TrioAngle]: [[0, 0], [0, 1], [1, 1]],
  [BlockType.Tetro_I]: [[0, 0], [0, 1], [0, 2], [0, 3]],
  [BlockType.Tetro_O]: [[0, 0], [0, 1], [1, 0], [1, 1]],
  [BlockType.Tetro_T]: [[0, 0], [0, 1], [0, 2], [1, 1]],
  [BlockType.Tetro_L]: [[0, 0], [0, 1], [0, 2], [1, 0]],
  [BlockType.Tetro_S]: [[0, 0], [0, 1], [1, 1], [1, 2]],
  [BlockType.Pento_F]: [[0, 1], [0, 2], [1, 0], [1, 1], [2, 1]],
  [BlockType.Pento_I]: [[0, 0], [0, 1], [0, 2], [0, 3], [0, 4]],
  [BlockType.Pento_L]: [[0, 0], [0, 1], [0, 2], [0, 3], [1, 0]],
  [BlockType.Pento_N]: [[0, 0], [0, 1], [0, 2], [1, 2], [1, 3]],
  [BlockType.Pento_P]: [[0, 0], [0, 1], [1, 0], [1, 1], [2, 0]],
  [BlockType.Pento_T]: [[0, 0], [0, 1], [0, 2], [1, 1], [2, 1]],
  [BlockType.Pento_U]: [[0, 0], [0, 2], [1, 0], [1, 1], [1, 2]],
  [BlockType.Pento_V]: [[0, 0], [1, 0], [2, 0], [2, 1], [2, 2]],
  [BlockType.Pento_W]: [[0, 0], [1, 0], [1, 1], [2, 1], [2, 2]],
  [BlockType.Pento_X]: [[0, 1], [1, 0], [1, 1], [1, 2], [2, 1]],
  [BlockType.Pento_Y]: [[0, 0], [0, 1], [0, 2], [0, 3], [1, 1]],
  [BlockType.Pento_Z]: [[0, 0], [0, 1], [1, 1], [2, 1], [2, 2]]
};

// Generate all rotations and reflections for a block shape
function generateAllOrientations(baseShape: BlockShape): number[][][] {
  const orientations: number[][][] = [];
  const seen = new Set<string>();

  // Helper function to convert shape to matrix
  function shapeToMatrix(shape: BlockShape): number[][] {
    if (shape.length === 0) return [[]];

    const minX = Math.min(...shape.map(([x, y]) => x));
    const minY = Math.min(...shape.map(([x, y]) => y));
    const maxX = Math.max(...shape.map(([x, y]) => x));
    const maxY = Math.max(...shape.map(([x, y]) => y));

    const width = maxX - minX + 1;
    const height = maxY - minY + 1;

    const matrix = Array(height).fill(null).map(() => Array(width).fill(0));

    shape.forEach(([x, y]) => {
      matrix[y - minY][x - minX] = 1;
    });

    return matrix;
  }

  // Helper function to normalize matrix (move to top-left)
  function normalizeMatrix(matrix: number[][]): number[][] {
    // Find first row and column with content
    let firstRow = -1, firstCol = -1;

    for (let r = 0; r < matrix.length; r++) {
      if (matrix[r].some(cell => cell === 1)) {
        firstRow = r;
        break;
      }
    }

    for (let c = 0; c < matrix[0].length; c++) {
      if (matrix.some(row => row[c] === 1)) {
        firstCol = c;
        break;
      }
    }

    if (firstRow === -1 || firstCol === -1) return matrix;

    // Extract only the used area
    const result: number[][] = [];
    for (let r = firstRow; r < matrix.length; r++) {
      if (matrix[r].some(cell => cell === 1)) {
        const row: number[] = [];
        for (let c = firstCol; c < matrix[r].length; c++) {
          if (matrix.some((row, idx) => idx >= firstRow && row[c] === 1)) {
            row.push(matrix[r][c]);
          }
        }
        if (row.length > 0) result.push(row);
      }
    }

    return result;
  }

  // Helper function to rotate matrix 90 degrees clockwise
  function rotateMatrix(matrix: number[][]): number[][] {
    const rows = matrix.length;
    const cols = matrix[0].length;
    const rotated = Array(cols).fill(null).map(() => Array(rows).fill(0));

    for (let r = 0; r < rows; r++) {
      for (let c = 0; c < cols; c++) {
        rotated[c][rows - 1 - r] = matrix[r][c];
      }
    }

    return normalizeMatrix(rotated);
  }

  // Helper function to flip matrix horizontally
  function flipMatrix(matrix: number[][]): number[][] {
    return normalizeMatrix(matrix.map(row => [...row].reverse()));
  }

  let currentMatrix = normalizeMatrix(shapeToMatrix(baseShape));

  // Generate 4 rotations
  for (let rotation = 0; rotation < 4; rotation++) {
    const key = JSON.stringify(currentMatrix);
    if (!seen.has(key)) {
      seen.add(key);
      orientations.push(currentMatrix);
    }

    // Generate flipped version
    const flipped = flipMatrix(currentMatrix);
    const flippedKey = JSON.stringify(flipped);
    if (!seen.has(flippedKey)) {
      seen.add(flippedKey);
      orientations.push(flipped);
    }

    currentMatrix = rotateMatrix(currentMatrix);
  }

  return orientations;
}

// Generate correct block definitions with all orientations
const BLOCK_DEFINITIONS = Object.entries(CORRECT_BLOCK_SHAPES).map(([blockType, shape], index) => ({
  id: index + 1,
  cells: shape.length,
  shapes: generateAllOrientations(shape)
}));
import type { BoardState } from './solver';

// Constants
const BOARD_SIZE = 20;
const COLOR_MULTIPLIER = 400;
const CORNERS = [
  { x: 0, y: 0 },     // 좌상단
  { x: 19, y: 0 },    // 우상단
  { x: 0, y: 19 },    // 좌하단
  { x: 19, y: 19 }    // 우하단
];

// Diagonal directions for Blokus adjacency rule
const DIAGONALS: [number, number][] = [[-1, -1], [1, -1], [-1, 1], [1, 1]];
// Orthogonal directions (forbidden for same color)
const ORTHOGONALS = [[0, -1], [0, 1], [-1, 0], [1, 0]];

interface PlacementResult {
  success: boolean;
  boardState: BoardState;
  placedBlocks: Array<{
    blockId: number;
    x: number;
    y: number;
    shapeIndex: number;
    color: number;
  }>;
  message?: string;
}

/**
 * Convert board state to 2D matrix for easier manipulation
 */
function boardStateToMatrix(boardState: BoardState): number[][] {
  const matrix = Array(BOARD_SIZE).fill(null).map(() => Array(BOARD_SIZE).fill(0));

  for (const encoded of boardState) {
    const colorIndex = Math.floor(encoded / COLOR_MULTIPLIER);
    const position = encoded % COLOR_MULTIPLIER;
    const y = Math.floor(position / BOARD_SIZE);
    const x = position % BOARD_SIZE;

    if (x >= 0 && x < BOARD_SIZE && y >= 0 && y < BOARD_SIZE) {
      if (colorIndex === 5) {
        matrix[y][x] = -1; // Obstacle
      } else if (colorIndex >= 1 && colorIndex <= 4) {
        matrix[y][x] = colorIndex;
      }
    }
  }

  return matrix;
}

/**
 * Convert 2D matrix back to board state
 */
function matrixToBoardState(matrix: number[][]): BoardState {
  const result: BoardState = [];

  for (let y = 0; y < BOARD_SIZE; y++) {
    for (let x = 0; x < BOARD_SIZE; x++) {
      const value = matrix[y][x];
      if (value > 0) {
        const colorIndex = value === -1 ? 5 : value;
        result.push(colorIndex * COLOR_MULTIPLIER + y * BOARD_SIZE + x);
      }
    }
  }

  return result;
}

/**
 * Get cells from shape matrix
 */
function getCellsFromShape(shape: number[][]): Array<{dx: number, dy: number}> {
  const cells: Array<{dx: number, dy: number}> = [];
  for (let dy = 0; dy < shape.length; dy++) {
    for (let dx = 0; dx < shape[0].length; dx++) {
      if (shape[dy][dx] === 1) {
        cells.push({ dx, dy });
      }
    }
  }
  return cells;
}

/**
 * Get the bounds of a block (min/max x,y coordinates)
 */
function getBlockBounds(blockCells: Array<{dx: number, dy: number}>): {minX: number, maxX: number, minY: number, maxY: number} {
  if (blockCells.length === 0) {
    return { minX: 0, maxX: 0, minY: 0, maxY: 0 };
  }

  const xs = blockCells.map(cell => cell.dx);
  const ys = blockCells.map(cell => cell.dy);

  return {
    minX: Math.min(...xs),
    maxX: Math.max(...xs),
    minY: Math.min(...ys),
    maxY: Math.max(...ys)
  };
}

/**
 * Adjust placement position based on corner to ensure proper block placement
 * For different corners, different parts of the block should touch the corner
 */
function adjustPlacementPosition(
  cornerX: number,
  cornerY: number,
  blockCells: Array<{dx: number, dy: number}>
): {x: number, y: number} {
  const bounds = getBlockBounds(blockCells);

  let targetCellX: number, targetCellY: number;

  if (cornerX === 0 && cornerY === 0) {
    // 좌상단 모서리: 블록의 좌상단 셀이 모서리에 닿음
    targetCellX = bounds.minX;
    targetCellY = bounds.minY;
  } else if (cornerX === 19 && cornerY === 0) {
    // 우상단 모서리: 블록의 우상단 셀이 모서리에 닿음
    targetCellX = bounds.maxX;
    targetCellY = bounds.minY;
  } else if (cornerX === 0 && cornerY === 19) {
    // 좌하단 모서리: 블록의 좌하단 셀이 모서리에 닿음
    targetCellX = bounds.minX;
    targetCellY = bounds.maxY;
  } else if (cornerX === 19 && cornerY === 19) {
    // 우하단 모서리: 블록의 우하단 셀이 모서리에 닿음
    targetCellX = bounds.maxX;
    targetCellY = bounds.maxY;
  } else {
    // 기본값 (다른 모서리의 경우)
    targetCellX = bounds.minX;
    targetCellY = bounds.minY;
  }

  // 목표 셀이 지정된 모서리에 오도록 기준점 조정
  return {
    x: cornerX - targetCellX,
    y: cornerY - targetCellY
  };
}

/**
 * Calculate reference point for diagonal connection
 * Based on diagonal direction, different parts of the block should connect
 */
function calculateReferencePointForDiagonal(
  targetX: number,
  targetY: number,
  diagonalDirection: [number, number],
  blockCells: Array<{dx: number, dy: number}>
): {x: number, y: number} {
  const bounds = getBlockBounds(blockCells);
  const [dirX, dirY] = diagonalDirection;

  let targetCellX: number, targetCellY: number;

  if (dirX === -1 && dirY === -1) {
    // (-1,-1) 방향: 새 블록의 우하단 셀이 연결점에 와야 함
    targetCellX = bounds.maxX;
    targetCellY = bounds.maxY;
  } else if (dirX === 1 && dirY === -1) {
    // (1,-1) 방향: 새 블록의 좌하단 셀이 연결점에 와야 함
    targetCellX = bounds.minX;
    targetCellY = bounds.maxY;
  } else if (dirX === -1 && dirY === 1) {
    // (-1,1) 방향: 새 블록의 우상단 셀이 연결점에 와야 함
    targetCellX = bounds.maxX;
    targetCellY = bounds.minY;
  } else if (dirX === 1 && dirY === 1) {
    // (1,1) 방향: 새 블록의 좌상단 셀이 연결점에 와야 함
    targetCellX = bounds.minX;
    targetCellY = bounds.minY;
  } else {
    // 기본값
    targetCellX = bounds.minX;
    targetCellY = bounds.minY;
  }

  return {
    x: targetX - targetCellX,
    y: targetY - targetCellY
  };
}

/**
 * Check if block placement is valid according to Blokus rules
 */
function isValidPlacement(
  matrix: number[][],
  blockCells: Array<{dx: number, dy: number}>,
  x: number,
  y: number,
  color: number,
  isFirstBlock: boolean,
  startCorner?: {x: number, y: number}
): boolean {
  let hasCornerAdjacency = false;
  let touchesStartCorner = false;

  // Check each cell of the block
  for (const {dx, dy} of blockCells) {
    const cellX = x + dx;
    const cellY = y + dy;

    // Check bounds
    if (cellX < 0 || cellX >= BOARD_SIZE || cellY < 0 || cellY >= BOARD_SIZE) {
      return false;
    }

    // Check if cell is already occupied
    if (matrix[cellY][cellX] !== 0) {
      return false;
    }

    // Check for forbidden orthogonal adjacency with same color
    for (const [ox, oy] of ORTHOGONALS) {
      const adjX = cellX + ox;
      const adjY = cellY + oy;
      if (adjX >= 0 && adjX < BOARD_SIZE && adjY >= 0 && adjY < BOARD_SIZE) {
        if (matrix[adjY][adjX] === color) {
          return false; // Same color orthogonal adjacency is forbidden
        }
      }
    }

    // Check for required diagonal adjacency with same color (except first block)
    if (!isFirstBlock) {
      for (const [dx2, dy2] of DIAGONALS) {
        const diagX = cellX + dx2;
        const diagY = cellY + dy2;
        if (diagX >= 0 && diagX < BOARD_SIZE && diagY >= 0 && diagY < BOARD_SIZE) {
          if (matrix[diagY][diagX] === color) {
            hasCornerAdjacency = true;
          }
        }
      }
    }

    // Check if first block touches the specified starting corner
    if (isFirstBlock && startCorner) {
      if (cellX === startCorner.x && cellY === startCorner.y) {
        touchesStartCorner = true;
      }
    }
  }

  // First block must touch a corner
  if (isFirstBlock) {
    return touchesStartCorner;
  }

  // Subsequent blocks must have diagonal adjacency
  return hasCornerAdjacency;
}

/**
 * Place a block on the matrix
 */
function placeBlock(
  matrix: number[][],
  blockCells: Array<{dx: number, dy: number}>,
  x: number,
  y: number,
  color: number
): void {
  for (const {dx, dy} of blockCells) {
    matrix[y + dy][x + dx] = color;
  }
}

/**
 * Remove a block from the matrix
 */
function removeBlock(
  matrix: number[][],
  blockCells: Array<{dx: number, dy: number}>,
  x: number,
  y: number
): void {
  for (const {dx, dy} of blockCells) {
    matrix[y + dy][x + dx] = 0;
  }
}

/**
 * Get all possible placement positions for a specific block shape
 */
function getPossiblePlacementsForBlock(
  matrix: number[][],
  color: number,
  blockCells: Array<{dx: number, dy: number}>,
  isFirstBlock: boolean,
  startCorner?: {x: number, y: number}
): Array<{x: number, y: number}> {
  const positions: Array<{x: number, y: number}> = [];

  if (isFirstBlock) {
    // First block: calculate proper reference points for each corner
    if (startCorner) {
      // 지정된 모서리에 대해 이 블록이 배치 가능한 기준점 계산
      const adjusted = adjustPlacementPosition(startCorner.x, startCorner.y, blockCells);
      positions.push(adjusted);
    } else {
      // Fallback: try all available corners
      for (const corner of CORNERS) {
        if (matrix[corner.y][corner.x] === 0) {
          const adjusted = adjustPlacementPosition(corner.x, corner.y, blockCells);
          positions.push(adjusted);
        }
      }
    }
  } else {
    // Subsequent blocks: find diagonal connection points with proper orientation
    const connectionPoints = new Set<string>();

    for (let y = 0; y < BOARD_SIZE; y++) {
      for (let x = 0; x < BOARD_SIZE; x++) {
        if (matrix[y][x] === color) {
          // Check each diagonal direction
          for (const diagonal of DIAGONALS) {
            const [dx, dy] = diagonal;
            const adjX = x + dx;
            const adjY = y + dy;

            if (adjX >= 0 && adjX < BOARD_SIZE && adjY >= 0 && adjY < BOARD_SIZE) {
              if (matrix[adjY][adjX] === 0) {
                // Calculate reference point for this block to connect via this diagonal
                const refPoint = calculateReferencePointForDiagonal(adjX, adjY, diagonal, blockCells);

                // Check if this reference point would place the block within bounds
                let inBounds = true;
                for (const {dx: cellDx, dy: cellDy} of blockCells) {
                  const cellX = refPoint.x + cellDx;
                  const cellY = refPoint.y + cellDy;
                  if (cellX < 0 || cellX >= BOARD_SIZE || cellY < 0 || cellY >= BOARD_SIZE) {
                    inBounds = false;
                    break;
                  }
                }

                if (inBounds) {
                  connectionPoints.add(`${refPoint.x},${refPoint.y}`);
                }
              }
            }
          }
        }
      }
    }

    connectionPoints.forEach(pointKey => {
      const [x, y] = pointKey.split(',').map(Number);
      positions.push({ x, y });
    });
  }

  return positions;
}

/**
 * Get all possible placement positions for a color (legacy function for compatibility)
 */
function getPossiblePlacements(matrix: number[][], color: number, isFirstBlock: boolean, startCorner?: {x: number, y: number}): Array<{x: number, y: number}> {
  const positions: Array<{x: number, y: number}> = [];

  if (isFirstBlock) {
    // First block: use the specified start corner if provided
    if (startCorner) {
      // 지정된 시작 모서리만 사용 (조정된 좌표는 나중에 계산)
      positions.push(startCorner);
    } else {
      // Fallback: only corners that are empty
      for (const corner of CORNERS) {
        if (matrix[corner.y][corner.x] === 0) {
          positions.push(corner);
        }
      }
    }
  } else {
    // Find cells diagonally adjacent to same color
    const frontierCells = new Set<string>();

    for (let y = 0; y < BOARD_SIZE; y++) {
      for (let x = 0; x < BOARD_SIZE; x++) {
        if (matrix[y][x] === color) {
          // Check diagonal neighbors
          for (const [dx, dy] of DIAGONALS) {
            const adjX = x + dx;
            const adjY = y + dy;
            if (adjX >= 0 && adjX < BOARD_SIZE && adjY >= 0 && adjY < BOARD_SIZE) {
              if (matrix[adjY][adjX] === 0) {
                frontierCells.add(`${adjX},${adjY}`);
              }
            }
          }
        }
      }
    }

    frontierCells.forEach(cellKey => {
      const [x, y] = cellKey.split(',').map(Number);
      positions.push({ x, y });
    });
  }

  return positions;
}

/**
 * Shuffle array in place
 */
function shuffleArray<T>(array: T[]): T[] {
  const shuffled = [...array];
  for (let i = shuffled.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [shuffled[i], shuffled[j]] = [shuffled[j], shuffled[i]];
  }
  return shuffled;
}


/**
 * Simple random block placement following Blokus rules
 */
export function randomlyPlaceBlocks(
  initialBoardState: BoardState,
  availableBlockIds: number[],
  startCorner: {x: number, y: number},
  color: number,
  maxAttempts: number = 1000
): PlacementResult {
  // Validate inputs
  if (!CORNERS.some(corner => corner.x === startCorner.x && corner.y === startCorner.y)) {
    return {
      success: false,
      boardState: initialBoardState,
      placedBlocks: [],
      message: "시작 위치는 보드의 모서리여야 합니다."
    };
  }

  if (color < 1 || color > 4) {
    return {
      success: false,
      boardState: initialBoardState,
      placedBlocks: [],
      message: "유효하지 않은 색상입니다. (1-4 사이의 값이어야 함)"
    };
  }

  if (availableBlockIds.length === 0) {
    return {
      success: false,
      boardState: initialBoardState,
      placedBlocks: [],
      message: "배치할 블록이 선택되지 않았습니다."
    };
  }

  const matrix = boardStateToMatrix(initialBoardState);

  // Check if start corner is available
  if (matrix[startCorner.y][startCorner.x] !== 0) {
    return {
      success: false,
      boardState: initialBoardState,
      placedBlocks: [],
      message: "시작 모서리에 이미 블록이나 장애물이 있습니다."
    };
  }

  // Check if there are already blocks of the same color on the board
  const hasExistingBlocks = matrix.some(row => row.some(cell => cell === color));

  if (hasExistingBlocks) {
    return {
      success: false,
      boardState: initialBoardState,
      placedBlocks: [],
      message: "보드에 이미 같은 색상의 블록이 배치되어 있습니다."
    };
  }

  const placedBlocks: Array<{
    blockId: number;
    x: number;
    y: number;
    shapeIndex: number;
    color: number;
  }> = [];

  // Use simple array copying and removal to prevent duplicates
  const remainingBlocks = [...availableBlockIds];
  let isFirstBlock = true;
  let attempts = 0;

  while (remainingBlocks.length > 0 && attempts < maxAttempts) {
    // Random block selection from remaining blocks
    const randomBlockIndex = Math.floor(Math.random() * remainingBlocks.length);
    const blockId = remainingBlocks[randomBlockIndex];

    const blockDef = BLOCK_DEFINITIONS.find(def => def.id === blockId);

    if (!blockDef) {
      // Remove invalid block and continue
      remainingBlocks.splice(randomBlockIndex, 1);
      attempts++;
      continue;
    }

    let blockPlaced = false;

    // Try all shapes in random order
    const shapeIndices = shuffleArray(Array.from({length: blockDef.shapes.length}, (_, i) => i));

    for (const shapeIndex of shapeIndices) {
      const shape = blockDef.shapes[shapeIndex];
      const blockCells = getCellsFromShape(shape);

      // Get possible positions for this specific block shape
      const possiblePositions = getPossiblePlacementsForBlock(matrix, color, blockCells, isFirstBlock, isFirstBlock ? startCorner : undefined);

      if (possiblePositions.length === 0) {
        continue; // Try next shape
      }

      // Try all positions in random order
      const shuffledPositions = shuffleArray([...possiblePositions]);

      for (const pos of shuffledPositions) {
        const actualX = pos.x;
        const actualY = pos.y;

        if (isValidPlacement(matrix, blockCells, actualX, actualY, color, isFirstBlock, isFirstBlock ? startCorner : undefined)) {
          // Place the block
          placeBlock(matrix, blockCells, actualX, actualY, color);

          placedBlocks.push({
            blockId,
            x: actualX,
            y: actualY,
            shapeIndex,
            color
          });

          // Remove the used block from remaining blocks
          remainingBlocks.splice(randomBlockIndex, 1);
          isFirstBlock = false;
          blockPlaced = true;
          break;
        }
      }

      if (blockPlaced) break;
    }

    if (!blockPlaced) {
      // This block can't be placed, remove it and try others
      remainingBlocks.splice(randomBlockIndex, 1);
    }

    attempts++;
  }

  const finalBoardState = matrixToBoardState(matrix);

  if (placedBlocks.length === 0) {
    return {
      success: false,
      boardState: initialBoardState,
      placedBlocks: [],
      message: "선택한 블록들을 배치할 수 없습니다. 보드가 너무 복잡하거나 블록이 너무 큽니다."
    };
  }

  const totalPlacedBlocks = placedBlocks.length;
  const totalAvailableBlocks = availableBlockIds.length;
  const placementRate = (totalPlacedBlocks / totalAvailableBlocks * 100).toFixed(1);

  return {
    success: true,
    boardState: finalBoardState,
    placedBlocks,
    message: `${totalPlacedBlocks}/${totalAvailableBlocks}개 블록을 성공적으로 배치했습니다. (${placementRate}%)`
  };
}

/**
 * Validate corner selection
 */
export function validateCornerSelection(x: number, y: number): { isValid: boolean; message: string } {
  const isCorner = CORNERS.some(corner => corner.x === x && corner.y === y);

  if (!isCorner) {
    return {
      isValid: false,
      message: "모서리 위치가 아닙니다. 보드의 네 모서리 중 하나를 선택해주세요."
    };
  }

  return {
    isValid: true,
    message: "유효한 모서리입니다."
  };
}

/**
 * Get available corners (not occupied)
 */
export function getAvailableCorners(boardState: BoardState): Array<{x: number, y: number, available: boolean}> {
  const matrix = boardStateToMatrix(boardState);

  return CORNERS.map(corner => ({
    ...corner,
    available: matrix[corner.y][corner.x] === 0
  }));
}