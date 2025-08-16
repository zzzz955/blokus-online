// src/lib/board-state-codec.ts

/**
 * Board State Type Definitions
 * - BoardStateExpanded: UI-friendly format with separate arrays for obstacles and preplaced pieces
 * - BoardStateCompact: Legacy JSONB format {obsIdx, pre} (still supported for backward compatibility)
 * - BoardStateDB: New INTEGER[] format for database storage (color_index * 400 + position)
 */

export type BoardStateExpanded = {
  obstacles: { x: number; y: number }[];
  preplaced: { x: number; y: number; color: number }[];
};

export type BoardStateCompact = {
  obsIdx: number[];
  pre: [number, number, number][];
};

export type BoardStateDB = number[]; // New INTEGER[] format for database

const BOARD_SIZE = 20;
const COLOR_MULTIPLIER = 400; // color_index * 400 + position

/**
 * Type Guards
 */
export function isCompactBoardState(v: any): v is BoardStateCompact {
  return v && Array.isArray(v.obsIdx) && Array.isArray(v.pre);
}

export function isExpandedBoardState(v: any): v is BoardStateExpanded {
  return v && Array.isArray(v.obstacles) && Array.isArray(v.preplaced);
}

export function isDBBoardState(v: any): v is BoardStateDB {
  return Array.isArray(v) && v.every(item => typeof item === 'number');
}

/**
 * Legacy functions (maintained for backward compatibility)
 */
export function compressBoardState(exp: BoardStateExpanded): BoardStateCompact {
  const obsIdx = exp.obstacles.map(o => o.y * BOARD_SIZE + o.x);
  const pre: [number, number, number][] = exp.preplaced.map(p => [p.x, p.y, p.color]);
  return { obsIdx, pre };
}

/**
 * New conversion functions for DATABASE INTEGER[] format
 */

/**
 * Convert expanded format to database INTEGER[] format
 * Encoding: color_index * 400 + (y * 20 + x)
 */
export function expandedToDBFormat(exp: BoardStateExpanded): BoardStateDB {
  const result: number[] = [];
  
  // Add obstacles (color = 0)
  for (const obs of exp.obstacles) {
    const position = obs.y * BOARD_SIZE + obs.x;  // y * 20 + x
    const encoded = 0 * COLOR_MULTIPLIER + position; // Black color (0)
    result.push(encoded);
  }
  
  // Add preplaced pieces
  for (const piece of exp.preplaced) {
    const position = piece.y * BOARD_SIZE + piece.x;  // y * 20 + x
    const encoded = piece.color * COLOR_MULTIPLIER + position;
    result.push(encoded);
  }
  
  return result;
}

/**
 * Convert database INTEGER[] format to expanded format
 * Decoding: color_index = value / 400, position = value % 400, y = position / 20, x = position % 20
 */
export function dbFormatToExpanded(dbState: BoardStateDB): BoardStateExpanded {
  const obstacles: { x: number; y: number }[] = [];
  const preplaced: { x: number; y: number; color: number }[] = [];
  
  for (const encoded of dbState) {
    const colorIndex = Math.floor(encoded / COLOR_MULTIPLIER);
    const position = encoded % COLOR_MULTIPLIER;
    const y = Math.floor(position / BOARD_SIZE);  // y = position / 20
    const x = position % BOARD_SIZE;              // x = position % 20
    
    if (colorIndex === 0) {
      // Black color = obstacle
      obstacles.push({ x, y });
    } else {
      // Other colors = preplaced piece
      preplaced.push({ x, y, color: colorIndex });
    }
  }
  
  return { obstacles, preplaced };
}

/**
 * Convert compact JSONB format to database INTEGER[] format
 */
export function compactToDBFormat(compact: BoardStateCompact): BoardStateDB {
  const result: number[] = [];
  
  // Convert obstacles (obsIdx array)
  for (const obsPosition of compact.obsIdx) {
    const encoded = 0 * COLOR_MULTIPLIER + obsPosition; // Black color (0)
    result.push(encoded);
  }
  
  // Convert preplaced pieces (pre array: [x, y, color])
  for (const [x, y, color] of compact.pre) {
    const position = y * BOARD_SIZE + x;  // y * 20 + x
    const encoded = color * COLOR_MULTIPLIER + position;
    result.push(encoded);
  }
  
  return result;
}

/**
 * Convert database INTEGER[] format to compact JSONB format
 */
export function dbFormatToCompact(dbState: BoardStateDB): BoardStateCompact {
  const obsIdx: number[] = [];
  const pre: [number, number, number][] = [];
  
  for (const encoded of dbState) {
    const colorIndex = Math.floor(encoded / COLOR_MULTIPLIER);
    const position = encoded % COLOR_MULTIPLIER;
    
    if (colorIndex === 0) {
      // Black color = obstacle
      obsIdx.push(position);
    } else {
      // Other colors = preplaced piece
      const y = Math.floor(position / BOARD_SIZE);  // y = position / 20
      const x = position % BOARD_SIZE;              // x = position % 20
      pre.push([x, y, colorIndex]);
    }
  }
  
  return { obsIdx, pre };
}

/**
 * Universal function to expand any board state format to expanded format
 * Supports: expanded, compact (legacy JSONB), and database INTEGER[] formats
 */
export function expandBoardState(anyState: any): BoardStateExpanded {
  if (anyState === null || anyState === undefined) {
    return { obstacles: [], preplaced: [] };
  }
  
  // Already expanded format
  if (isExpandedBoardState(anyState)) {
    return anyState;
  }
  
  // Database INTEGER[] format
  if (isDBBoardState(anyState)) {
    return dbFormatToExpanded(anyState);
  }
  
  // Legacy compact JSONB format
  if (isCompactBoardState(anyState)) {
    return {
      obstacles: anyState.obsIdx.map((i: number) => ({ 
        x: i % BOARD_SIZE,           // x = position % 20
        y: Math.floor(i / BOARD_SIZE) // y = position / 20
      })),
      preplaced: anyState.pre.map(([x, y, color]: [number, number, number]) => ({ x, y, color })),
    };
  }
  
  // Fallback for unknown format
  return { obstacles: [], preplaced: [] };
}

/**
 * Convert any board state format to database INTEGER[] format
 * This is the function to use when saving to database
 */
export function toBoardStateDB(anyState: any): BoardStateDB {
  if (anyState === null || anyState === undefined) {
    return [];
  }
  
  // Already database format
  if (isDBBoardState(anyState)) {
    return anyState;
  }
  
  // Convert via expanded format
  const expanded = expandBoardState(anyState);
  return expandedToDBFormat(expanded);
}
