// src/lib/board-state-codec.ts

/**
 * Board State Type Definitions - Refactored for int[] primary format
 * - BoardState: Primary int[] format (colorIndex * 400 + row * 20 + col)
 * - LegacyBoardState: Legacy JSON format (deprecated, for backward compatibility only)
 */

export type BoardState = number[]; // Primary int[] format

// Legacy JSON format - DEPRECATED, for backward compatibility only
export type LegacyBoardState = {
  obstacles: { x: number; y: number }[];
  preplaced: { x: number; y: number; color: number }[];
};

const BOARD_SIZE = 20;
const COLOR_MULTIPLIER = 400; // color_index * 400 + position
const OBSTACLE_COLOR_INDEX = 5; // Changed from 0 to 5

/**
 * Type Guards
 */
export function isBoardState(v: any): v is BoardState {
  return Array.isArray(v) && v.every(item => typeof item === 'number');
}

export function isLegacyBoardState(v: any): v is LegacyBoardState {
  return v && Array.isArray(v.obstacles) && Array.isArray(v.preplaced);
}

/**
 * Core BoardState manipulation functions
 */

/**
 * Create empty board state
 */
export function createEmptyBoardState(): BoardState {
  return [];
}

/**
 * Add obstacle to board state
 * @param boardState - Current board state
 * @param x - X coordinate (0-19)
 * @param y - Y coordinate (0-19)
 */
export function addObstacle(boardState: BoardState, x: number, y: number): BoardState {
  if (x < 0 || x >= BOARD_SIZE || y < 0 || y >= BOARD_SIZE) {
    throw new Error(`Invalid coordinates: (${x}, ${y})`);
  }
  
  const position = y * BOARD_SIZE + x;
  const encoded = OBSTACLE_COLOR_INDEX * COLOR_MULTIPLIER + position;
  
  // Remove existing entry at this position if any
  const filtered = boardState.filter(entry => (entry % COLOR_MULTIPLIER) !== position);
  
  return [...filtered, encoded];
}

/**
 * Add preplaced piece to board state
 * @param boardState - Current board state
 * @param x - X coordinate (0-19)
 * @param y - Y coordinate (0-19)
 * @param color - Player color (1-4)
 */
export function addPreplacedPiece(boardState: BoardState, x: number, y: number, color: number): BoardState {
  if (x < 0 || x >= BOARD_SIZE || y < 0 || y >= BOARD_SIZE) {
    throw new Error(`Invalid coordinates: (${x}, ${y})`);
  }
  if (color < 1 || color > 4) {
    throw new Error(`Invalid color: ${color}. Must be 1-4`);
  }
  
  const position = y * BOARD_SIZE + x;
  const encoded = color * COLOR_MULTIPLIER + position;
  
  // Remove existing entry at this position if any
  const filtered = boardState.filter(entry => (entry % COLOR_MULTIPLIER) !== position);
  
  return [...filtered, encoded];
}

/**
 * Remove piece from board state at position
 * @param boardState - Current board state
 * @param x - X coordinate (0-19)
 * @param y - Y coordinate (0-19)
 */
export function removePiece(boardState: BoardState, x: number, y: number): BoardState {
  if (x < 0 || x >= BOARD_SIZE || y < 0 || y >= BOARD_SIZE) {
    throw new Error(`Invalid coordinates: (${x}, ${y})`);
  }
  
  const position = y * BOARD_SIZE + x;
  return boardState.filter(entry => (entry % COLOR_MULTIPLIER) !== position);
}

/**
 * Get piece at position
 * @param boardState - Current board state
 * @param x - X coordinate (0-19)
 * @param y - Y coordinate (0-19)
 * @returns Color index (5 = obstacle, 1-4 = player colors, 0 = empty)
 */
export function getPieceAt(boardState: BoardState, x: number, y: number): number {
  if (x < 0 || x >= BOARD_SIZE || y < 0 || y >= BOARD_SIZE) {
    return 0; // Out of bounds = empty
  }
  
  // Ensure boardState is array and handle edge cases
  if (!Array.isArray(boardState)) {
    console.warn('getPieceAt: boardState is not an array', boardState);
    return 0;
  }
  
  const position = y * BOARD_SIZE + x;
  const entry = boardState.find(entry => {
    if (typeof entry !== 'number') return false;
    return (entry % COLOR_MULTIPLIER) === position;
  });
  
  if (!entry) return 0; // Empty
  
  return Math.floor(entry / COLOR_MULTIPLIER);
}

/**
 * Legacy conversion functions for backward compatibility
 */

/**
 * Convert legacy JSON format to new BoardState format
 * @param legacyState - Legacy JSON format
 * @returns BoardState in int[] format
 */
export function fromLegacyBoardState(legacyState: LegacyBoardState): BoardState {
  const result: BoardState = [];
  
  // Add obstacles with new color index (5)
  for (const obs of legacyState.obstacles) {
    const position = obs.y * BOARD_SIZE + obs.x;
    const encoded = OBSTACLE_COLOR_INDEX * COLOR_MULTIPLIER + position;
    result.push(encoded);
  }
  
  // Add preplaced pieces
  for (const piece of legacyState.preplaced) {
    const position = piece.y * BOARD_SIZE + piece.x;
    const encoded = piece.color * COLOR_MULTIPLIER + position;
    result.push(encoded);
  }
  
  return result;
}

/**
 * Convert BoardState to legacy JSON format
 * @param boardState - BoardState in int[] format
 * @returns Legacy JSON format
 */
export function toLegacyBoardState(boardState: BoardState): LegacyBoardState {
  const obstacles: { x: number; y: number }[] = [];
  const preplaced: { x: number; y: number; color: number }[] = [];
  
  // Handle edge cases where boardState might not be an array
  if (!Array.isArray(boardState)) {
    console.warn('toLegacyBoardState: boardState is not an array', boardState);
    return { obstacles: [], preplaced: [] };
  }
  
  for (const encoded of boardState) {
    if (typeof encoded !== 'number') {
      console.warn('toLegacyBoardState: invalid entry in boardState', encoded);
      continue;
    }
    
    const colorIndex = Math.floor(encoded / COLOR_MULTIPLIER);
    const position = encoded % COLOR_MULTIPLIER;
    const y = Math.floor(position / BOARD_SIZE);
    const x = position % BOARD_SIZE;
    
    if (colorIndex === OBSTACLE_COLOR_INDEX) {
      obstacles.push({ x, y });
    } else if (colorIndex >= 1 && colorIndex <= 4) {
      preplaced.push({ x, y, color: colorIndex });
    }
  }
  
  return { obstacles, preplaced };
}

/**
 * Universal function to convert any format to BoardState
 * @param anyState - Any board state format
 * @returns BoardState in int[] format
 */
export function normalizeBoardState(anyState: any): BoardState {
  if (anyState === null || anyState === undefined) {
    return [];
  }
  
  // Already int[] format
  if (isBoardState(anyState)) {
    return anyState;
  }
  
  // Legacy JSON format
  if (isLegacyBoardState(anyState)) {
    return fromLegacyBoardState(anyState);
  }
  
  // Fallback for unknown format
  return [];
}
