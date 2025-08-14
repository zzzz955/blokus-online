// src/lib/board-state-codec.ts
export type BoardStateExpanded = {
  obstacles: { x: number; y: number }[];
  preplaced: { x: number; y: number; color: number }[];
};

export type BoardStateCompact = {
  obsIdx: number[];
  pre: [number, number, number][];
};

const BOARD_SIZE = 20;

export function isCompactBoardState(v: any): v is BoardStateCompact {
  return v && Array.isArray(v.obsIdx) && Array.isArray(v.pre);
}
export function isExpandedBoardState(v: any): v is BoardStateExpanded {
  return v && Array.isArray(v.obstacles) && Array.isArray(v.preplaced);
}

export function compressBoardState(exp: BoardStateExpanded): BoardStateCompact {
  const obsIdx = exp.obstacles.map(o => o.y * BOARD_SIZE + o.x);
  const pre: [number, number, number][] = exp.preplaced.map(p => [p.x, p.y, p.color]);
  return { obsIdx, pre };
}

export function expandBoardState(anyState: any): BoardStateExpanded {
  if (isExpandedBoardState(anyState)) return anyState;
  if (isCompactBoardState(anyState)) {
    return {
      obstacles: anyState.obsIdx.map((i: number) => ({ x: i % BOARD_SIZE, y: Math.floor(i / BOARD_SIZE) })),
      preplaced: anyState.pre.map(([x, y, color]: [number, number, number]) => ({ x, y, color })),
    };
  }
  return { obstacles: [], preplaced: [] };
}
