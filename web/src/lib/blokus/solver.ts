// src/lib/blokus/solver.ts (하단에 추가)
import type { BlockShape } from './blocks';
import { toLegacyBoardState, type BoardState as IntBoardState } from '../board-state-codec';

export type Cell = -1 | 0 | 1 | 2 | 3 | 4;

// Use the new int[] BoardState format as primary
export type BoardState = IntBoardState;

// Legacy interface for backward compatibility
export interface LegacyBoardState {
  obstacles: Array<{ x: number; y: number }>;
  preplaced: Array<{ x: number; y: number; color: number }>;
}
export type SolveOptions = { timeLimitMs?: number };
export type Placement = { blockId:number;color:number;x:number;y:number;shapeIndex:number };
export type SolveResult = { score:number; placements:Placement[] };

const BOARD_SIZE = 20 as const;
const DIAGS = [[-1,-1],[1,-1],[-1,1],[1,1]] as const;
const ORTHO = [[0,-1],[0,1],[-1,0],[1,0]] as const;
const CORNERS = [{x:0,y:0},{x:0,y:19},{x:19,y:0},{x:19,y:19}];

type Grid = Cell[][];
const inBoard = (x:number,y:number)=> x>=0 && y>=0 && x<BOARD_SIZE && y<BOARD_SIZE;

const cellsFromShape = (shape:number[][]) => {
  const out:Array<{dx:number;dy:number}> = [];
  for (let dy=0; dy<shape.length; dy++) {
    for (let dx=0; dx<shape[0].length; dx++) {
      if (shape[dy][dx]) out.push({dx,dy});
    }
  }
  return out;
};

const OBSTACLE_COLOR_INDEX = 5;
const COLOR_MULTIPLIER = 400;

function makeBoardSingle(state: BoardState, targetColor: number): Grid {
  const b:Grid = Array.from({length:BOARD_SIZE},()=>Array<Cell>(BOARD_SIZE).fill(0));
  
  // Process int[] format directly
  for (const encoded of state) {
    const colorIndex = Math.floor(encoded / COLOR_MULTIPLIER);
    const position = encoded % COLOR_MULTIPLIER;
    const y = Math.floor(position / BOARD_SIZE);
    const x = position % BOARD_SIZE;
    
    if (!inBoard(x, y)) continue;
    
    if (colorIndex === OBSTACLE_COLOR_INDEX) {
      // Obstacle
      b[y][x] = -1;
    } else if (colorIndex >= 1 && colorIndex <= 4) {
      // Preplaced piece
      if (colorIndex === targetColor) {
        b[y][x] = targetColor as Cell;  // 같은 색은 연결/앵커로 사용
      } else {
        b[y][x] = -1;                   // 다른 색은 장애물로 간주
      }
    }
  }
  
  return b;
}

function frontierCells(board: Grid, color: number): Set<number> {
  const s = new Set<number>();
  for (let y=0;y<BOARD_SIZE;y++) for (let x=0;x<BOARD_SIZE;x++) {
    if (board[y][x] !== color) continue;
    for (const [dx,dy] of DIAGS) {
      const nx=x+dx, ny=y+dy;
      if (!inBoard(nx,ny)) continue;
      if (board[ny][nx] !== 0) continue;
      // 면접촉 금지 사전 필터
      let bad=false;
      for (const [ox,oy] of ORTHO) {
        const tx=nx+ox, ty=ny+oy;
        if (inBoard(tx,ty) && board[ty][tx]===color) { bad=true; break; }
      }
      if (!bad) s.add(ny*BOARD_SIZE + nx);
    }
  }
  return s;
}

function isLegalPlacement(
  board: Grid,
  color: number,
  shapeCells: Array<{dx:number;dy:number}>,
  x: number,
  y: number,
  cornerStart: boolean
): boolean {
  let hasCornerTouch = false;
  let touchesStartCorner = false;

  for (const {dx,dy} of shapeCells) {
    const ax = x+dx, ay = y+dy;
    if (!inBoard(ax,ay)) return false;
    if (board[ay][ax] !== 0) return false;

    // 같은 색 면접촉 금지
    for (const [ox,oy] of ORTHO) {
      const nx=ax+ox, ny=ay+oy;
      if (inBoard(nx,ny) && board[ny][nx]===color) return false;
    }
    // 같은 색 코너 접촉 필요(첫 블록 제외)
    for (const [cx,cy] of DIAGS) {
      const nx=ax+cx, ny=ay+cy;
      if (inBoard(nx,ny) && board[ny][nx]===color) hasCornerTouch = true;
    }
    // 첫 블록이면 코너 칸 포함
    if (cornerStart) {
      for (const c of CORNERS) if (ax===c.x && ay===c.y) touchesStartCorner = true;
    }
  }
  return cornerStart ? touchesStartCorner : hasCornerTouch;
}

function place(b:Grid, color:number, shapeCells:Array<{dx:number;dy:number}>, x:number,y:number){
  for (const c of shapeCells) b[y+c.dy][x+c.dx] = color as Cell;
}
function unplace(b:Grid, shapeCells:Array<{dx:number;dy:number}>, x:number,y:number){
  for (const c of shapeCells) b[y+c.dy][x+c.dx] = 0;
}
const countEmpty = (b:Grid)=>{ let n=0; for (let y=0;y<BOARD_SIZE;y++) for (let x=0;x<BOARD_SIZE;x++) if (b[y][x]===0) n++; return n; };

/** ✅ 단일 색(세트 1벌) 최적해 계산 */
export function solveOptimalScoreSingleColor(
  boardState: BoardState,
  availableBlockIds: number[],
  BLOCK_DEFS: BlockShape[],
  options: SolveOptions = {},
  targetColor = 1
): SolveResult {
  const t0 = Date.now();
  const timeLimit = options.timeLimitMs ?? Infinity;

  const board = makeBoardSingle(boardState, targetColor);
  const firstPlacedInitially = (() => {
    for (let y=0;y<BOARD_SIZE;y++) for (let x=0;x<BOARD_SIZE;x++) if (board[y][x]===targetColor) return true;
    return false;
  })();
  let firstPlaced = firstPlacedInitially;

  const idToCells = new Map<number, number>();
  const idToShapes = new Map<number, Array<Array<{dx:number;dy:number}>>>();
  for (const d of BLOCK_DEFS) {
    idToCells.set(d.id, d.cells);
    idToShapes.set(d.id, d.shapes.map(cellsFromShape));
  }

  const used = new Set<number>();
  const totalCells = availableBlockIds.reduce((a,id)=>a+(idToCells.get(id)||0), 0);

  const canStart = () => firstPlaced || CORNERS.some(({x,y}) => board[y][x]===0);
  const upperBound = (score:number) => score + Math.min(countEmpty(board), totalCells - Array.from(used).reduce((s,id)=>s+(idToCells.get(id)||0),0));

  let bestScore = 0;
  let bestPlacements: Placement[] = [];
  const placedSeq: Placement[] = [];

  function generateMoves(){
    const anchors = firstPlaced
      ? Array.from(frontierCells(board, targetColor)).map(v => ({x: v%BOARD_SIZE, y: Math.floor(v/BOARD_SIZE)}))
      : CORNERS.filter(({x,y}) => board[y][x]===0);
    if (anchors.length === 0) return [];

    const ids = availableBlockIds.filter(id=>!used.has(id))
      .sort((a,b)=>(idToCells.get(b)||0)-(idToCells.get(a)||0));

    const moves: Array<Placement & {shapeCells:Array<{dx:number;dy:number}>}> = [];
    const dedup = new Set<string>();

    for (const id of ids) {
      const shapes = idToShapes.get(id) || [];
      for (let s=0; s<shapes.length; s++) {
        const shapeCells = shapes[s];
        for (const {x:ax,y:ay} of anchors) {
          for (const c of shapeCells) {
            const ox = ax - c.dx, oy = ay - c.dy;
            if (!inBoard(ox,oy)) continue;
            const key = `${id}@${ox},${oy}@${s}`;
            if (dedup.has(key)) continue;
            dedup.add(key);
            if (!isLegalPlacement(board, targetColor, shapeCells, ox, oy, !firstPlaced)) continue;
            moves.push({ blockId:id, color:targetColor, x:ox, y:oy, shapeIndex:s, shapeCells });
          }
        }
      }
    }
    // 큰 블록 우선
    moves.sort((a,b)=>(idToCells.get(b.blockId)||0)-(idToCells.get(a.blockId)||0));
    return moves;
  }

  function dfs(score:number){
    if (Date.now()-t0 > timeLimit) return;
    if (!canStart() && !firstPlaced) return;      // 시작 불가면 중단
    if (upperBound(score) <= bestScore) return;   // 가지치기

    const moves = generateMoves();
    if (!moves.length) {
      if (score > bestScore) { bestScore=score; bestPlacements=placedSeq.slice(); }
      return;
    }

    for (const m of moves) {
      place(board, targetColor, (m as any).shapeCells, m.x, m.y);
      placedSeq.push(m);
      const wasFirst = !firstPlaced; if (wasFirst) firstPlaced = true;
      used.add(m.blockId);

      dfs(score + (idToCells.get(m.blockId)||0));

      used.delete(m.blockId);
      if (wasFirst) firstPlaced = false;
      placedSeq.pop();
      unplace(board, (m as any).shapeCells, m.x, m.y);
    }
  }

  dfs(0);
  return { score: bestScore, placements: bestPlacements };
}
