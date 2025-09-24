import type { BlockShape } from './blocks';

export type Cell = -1 | 0 | 1 | 2 | 3 | 4;
export type BoardState = number[];

export type SolveOptions = { timeLimitMs?: number };
export type Placement = { blockId:number;color:number;x:number;y:number;shapeIndex:number };
export type SolveResult = { score:number; placements:Placement[]; timedOut?: boolean; iterations?: number };

const BOARD_SIZE = 20 as const;
const DIAGS = [[-1,-1],[1,-1],[-1,1],[1,1]] as const;
const ORTHO = [[0,-1],[0,1],[-1,0],[1,0]] as const;
const CORNERS = [{x:0,y:0},{x:19,y:0},{x:19,y:19},{x:0,y:19}];

type Grid = Cell[][];
const inBoard = (x:number,y:number)=> x>=0 && y>=0 && x<BOARD_SIZE && y<BOARD_SIZE;

const OBSTACLE_COLOR_INDEX = 5;
const COLOR_MULTIPLIER = 400;

const cellsFromShape = (shape:number[][]) => {
  const out:Array<{dx:number;dy:number}> = [];
  for (let dy=0; dy<shape.length; dy++) {
    for (let dx=0; dx<shape[0].length; dx++) {
      if (shape[dy][dx]) out.push({dx,dy});
    }
  }
  return out;
};

function makeBoardSingle(state: BoardState, targetColor: number): Grid {
  const b:Grid = Array.from({length:BOARD_SIZE},()=>Array<Cell>(BOARD_SIZE).fill(0));
  for (const encoded of state) {
    const colorIndex = Math.floor(encoded / COLOR_MULTIPLIER);
    const position = encoded % COLOR_MULTIPLIER;
    const y = Math.floor(position / BOARD_SIZE);
    const x = position % BOARD_SIZE;
    if (!inBoard(x, y)) continue;
    if (colorIndex === OBSTACLE_COLOR_INDEX) {
      b[y][x] = -1;
    } else if (colorIndex >= 1 && colorIndex <= 4) {
      b[y][x] = (colorIndex === targetColor) ? (targetColor as Cell) : -1;
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
      s.add(ny*BOARD_SIZE + nx);
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
    // 첫 블록이면 시작 코너 포함
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

/* ---------- 모양 정규화/중복 제거 ---------- */
function normalizeCells(cells: Array<{dx:number;dy:number}>) {
  let minx = Infinity, miny = Infinity;
  for (const c of cells) { if (c.dx < minx) minx=c.dx; if (c.dy < miny) miny=c.dy; }
  const norm = cells.map(c=>({dx:c.dx-minx, dy:c.dy-miny}));
  norm.sort((a,b)=> a.dy-b.dy || a.dx-b.dx);
  return norm;
}
function keyOf(cells: Array<{dx:number;dy:number}>) {
  return normalizeCells(cells).map(c=>`${c.dx},${c.dy}`).join(';');
}

/* ---------- 컴포넌트 기반 안전 가지치기 ---------- */
function gcd(a:number,b:number){ while(b){ const t=a%b; a=b; b=t; } return Math.abs(a); }
function gcdArray(arr:number[]){ return arr.reduce((g,v)=>gcd(g,v),0); }

function emptyComponents(board:Grid): number[] {
  const seen:boolean[][] = Array.from({length:BOARD_SIZE},()=>Array(BOARD_SIZE).fill(false));
  const sizes:number[] = [];
  const q:[number,number][] = [];
  for (let y=0;y<BOARD_SIZE;y++) for (let x=0;x<BOARD_SIZE;x++) {
    if (board[y][x]!==0 || seen[y][x]) continue;
    seen[y][x]=true; q.push([x,y]);
    let sz=0;
    while(q.length){
      const [cx,cy]=q.pop()!;
      sz++;
      for (const [dx,dy] of ORTHO) {
        const nx=cx+dx, ny=cy+dy;
        if (!inBoard(nx,ny)) continue;
        if (seen[ny][nx]) continue;
        if (board[ny][nx]!==0) continue;
        seen[ny][nx]=true; q.push([nx,ny]);
      }
    }
    sizes.push(sz);
  }
  return sizes;
}

function earlyInfeasible(board:Grid, remainingSizes:number[]): boolean {
  if (remainingSizes.length===0) return false;
  const comps = emptyComponents(board);
  const minSz = Math.min(...remainingSizes);
  const g = gcdArray(remainingSizes);
  for (const c of comps) {
    if (c < minSz) return true;        // 너무 작은 섬 존재 → 불가능
    if (g>1 && (c % g)!==0) return true; // gcd 조건 위반 → 불가능
  }
  return false;
}

/**  단일 색 최적해 계산(완전성 유지 + 안전 가지치기) */
export function solveOptimalScoreSingleColor(
  boardState: BoardState,
  availableBlockIds: number[],
  BLOCK_DEFS: BlockShape[],
  options: SolveOptions = {},
  targetColor = 1
): SolveResult {
  const t0 = Date.now();
  const timeLimit = options.timeLimitMs ?? 120_000;

  const board = makeBoardSingle(boardState, targetColor);
  const firstPlacedInitially = (() => {
    for (let y=0;y<BOARD_SIZE;y++) for (let x=0;x<BOARD_SIZE;x++) if (board[y][x]===targetColor) return true;
    return false;
  })();
  let firstPlaced = firstPlacedInitially;

  // 블록 전처리: 셀 수/모양, 중복 모양 제거
  const idToCells = new Map<number, number>();
  const idToShapes = new Map<number, Array<Array<{dx:number;dy:number}>>>();
  for (const d of BLOCK_DEFS) {
    idToCells.set(d.id, d.cells);
    const seen = new Set<string>();
    const uniq: Array<Array<{dx:number;dy:number}>> = [];
    for (const mat of d.shapes) {
      const cells = cellsFromShape(mat);
      const k = keyOf(cells);
      if (!seen.has(k)) { seen.add(k); uniq.push(normalizeCells(cells)); }
    }
    idToShapes.set(d.id, uniq);
  }

  const used = new Set<number>();
  const totalCells = availableBlockIds.reduce((a,id)=>a+(idToCells.get(id)||0), 0);

  const canStart = () => firstPlaced || CORNERS.some(({x,y}) => board[y][x]===0);

  // 상계: 빈칸/남은블록셀수 중 최소
  const upperBound = (score:number) => {
    const usedSum = Array.from(used).reduce((s,id)=>s+(idToCells.get(id)||0),0);
    const remainingCells = totalCells - usedSum;
    const empty = countEmpty(board);
    return score + Math.min(remainingCells, empty);
  };

  let bestScore = 0;
  let bestPlacements: Placement[] = [];
  const placedSeq: Placement[] = [];
  let iterations = 0;
  let timedOut = false;

  // 전이 테이블(동일상태 재방문 컷)
  const TT = new Map<string, number>();
  function stateKey(): string {
    let bits = '';
    for (let y=0; y<BOARD_SIZE; y++) {
      for (let x=0; x<BOARD_SIZE; x++) {
        bits += (board[y][x]===targetColor ? '1' : '0');
      }
    }
    const usedKey = Array.from(used).sort((a,b)=>a-b).join(',');
    return (firstPlaced ? '1' : '0') + '|' + usedKey + '|' + bits;
  }

  function enumeratePlacementsAtAnchor(ax:number, ay:number) {
    const out: Array<Placement & {shapeCells:Array<{dx:number;dy:number}>, anchorDeg?:number}> = [];
    const ids = availableBlockIds.filter(id=>!used.has(id));
    ids.sort((a,b)=>(idToCells.get(b)||0)-(idToCells.get(a)||0)); // 큰 블록 우선
    const dedup = new Set<string>();

    for (const id of ids) {
      const shapes = idToShapes.get(id) || [];
      for (let s=0; s<shapes.length; s++) {
        const shapeCells = shapes[s];
        for (const c of shapeCells) {
          const ox = ax - c.dx, oy = ay - c.dy;
          if (!inBoard(ox,oy)) continue;
          const key = `${id}@${ox},${oy}@${s}`;
          if (dedup.has(key)) continue;
          dedup.add(key);
          if (!isLegalPlacement(board, targetColor, shapeCells, ox, oy, !firstPlaced)) continue;
          out.push({ blockId:id, color:targetColor, x:ox, y:oy, shapeIndex:s, shapeCells });
        }
      }
    }
    return out;
  }

  // 모든 앵커의 move를 모아 “앵커 degree 오름차순”으로 정렬(완전성 유지)
  function generateMoves(){
    const anchors = firstPlaced
      ? Array.from(frontierCells(board, targetColor)).map(v => ({x: v%BOARD_SIZE, y: Math.floor(v/BOARD_SIZE)}))
      : CORNERS.filter(({x,y}) => board[y][x]===0);

    if (!anchors.length) return [];

    const aggregated: Array<Placement & {shapeCells:Array<{dx:number;dy:number}>, anchorDeg:number}> = [];
    let anyPositive = false;

    for (const a of anchors) {
      const list = enumeratePlacementsAtAnchor(a.x, a.y);
      const deg = list.length;
      if (deg > 0) {
        anyPositive = true;
        for (const m of list) aggregated.push({ ...m, anchorDeg: deg });
      }
    }
    if (!anyPositive) return []; // 모든 앵커가 degree==0이면 종료

    aggregated.sort((a,b)=>{
      if (a.anchorDeg !== b.anchorDeg) return a.anchorDeg - b.anchorDeg;       // MRV
      const ca = (idToCells.get(b.blockId)||0) - (idToCells.get(a.blockId)||0); // 큰 블록 우선
      if (ca !== 0) return ca;
      return 0;
    });
    return aggregated;
  }

  function remainingSizes(): number[] {
    const sizes:number[] = [];
    for (const id of availableBlockIds) if (!used.has(id)) sizes.push(idToCells.get(id)!);
    return sizes;
  }

  function dfs(score:number){
    iterations++;
    if (Date.now()-t0 > timeLimit) { timedOut = true; return; }
    if (!canStart() && !firstPlaced) return;

    // 🔒 안전 가지치기: 컴포넌트 불가능성 검사
    if (earlyInfeasible(board, remainingSizes())) return;

    if (upperBound(score) <= bestScore) return;

    const sk = stateKey();
    const prev = TT.get(sk);
    if (prev !== undefined && prev >= score) return;
    TT.set(sk, score);

    const moves = generateMoves();
    if (!moves.length) {
      if (score > bestScore) {
        bestScore = score;
        bestPlacements = placedSeq.slice();
      }
      return;
    }

    for (let i = 0; i < moves.length && !timedOut; i++) {
      const m = moves[i];

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

  return {
    score: bestScore,
    placements: bestPlacements,
    timedOut,
    iterations
  };
}
