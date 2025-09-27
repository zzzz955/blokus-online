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

/**  단일 색 최적해 계산(개선된 휴리스틱 + 점진적 딥닝) */
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

  // 블록 전처리: 셀 수/모양, 중복 모양 제거 + 우선순위 정보 추가
  const idToCells = new Map<number, number>();
  const idToShapes = new Map<number, Array<Array<{dx:number;dy:number}>>>();
  const idToPriority = new Map<number, number>(); // 블록 배치 우선순위

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

    // 우선순위: 큰 블록 + 제약이 많은 블록 우선
    const cellCount = d.cells;
    const shapeVariations = uniq.length;
    const priority = cellCount * 100 + (10 - Math.min(shapeVariations, 10)); // 큰 블록, 변형 적은 것 우선
    idToPriority.set(d.id, priority);
  }

  const used = new Set<number>();
  const totalCells = availableBlockIds.reduce((a,id)=>a+(idToCells.get(id)||0), 0);

  const canStart = () => firstPlaced || CORNERS.some(({x,y}) => board[y][x]===0);

  // 개선된 상계: 연결 가능성과 모양 호환성 고려
  const advancedUpperBound = (score:number) => {
    const usedSum = Array.from(used).reduce((s,id)=>s+(idToCells.get(id)||0),0);
    const remainingCells = totalCells - usedSum;
    const empty = countEmpty(board);
    const basicBound = Math.min(remainingCells, empty);

    // 연결 가능한 영역 분석
    const frontier = firstPlaced ? frontierCells(board, targetColor) : new Set(CORNERS.filter(c => board[c.y][c.x] === 0).map(c => c.y * BOARD_SIZE + c.x));

    if (frontier.size === 0) return score;

    // 각 연결 가능한 영역에서 배치 가능한 최대 블록 수 추정
    let connectivityBonus = 0;
    const remainingBlocks = availableBlockIds.filter(id => !used.has(id));
    const maxRemainingBlockSize = remainingBlocks.length > 0 ? Math.max(...remainingBlocks.map(id => idToCells.get(id) || 0)) : 0;

    // 프론티어 밀도 기반 보정
    connectivityBonus = Math.min(frontier.size * 2, maxRemainingBlockSize);

    return score + Math.min(basicBound + connectivityBonus, remainingCells);
  };

  let bestScore = 0;
  let bestPlacements: Placement[] = [];
  const placedSeq: Placement[] = [];
  let iterations = 0;
  let timedOut = false;

  // 개선된 전이 테이블 (더 효율적인 키 생성)
  const TT = new Map<string, {score: number, depth: number}>();
  function compactStateKey(): string {
    // 비트 압축된 보드 상태
    let boardHash = 0;
    let shift = 0;
    for (let y=0; y<BOARD_SIZE; y++) {
      for (let x=0; x<BOARD_SIZE; x++) {
        if (board[y][x] === targetColor) {
          boardHash ^= (x + y * BOARD_SIZE + 1) << (shift % 30);
        }
        shift++;
      }
    }
    const usedKey = Array.from(used).sort((a,b)=>a-b).join(',');
    return `${firstPlaced ? 1 : 0}|${usedKey}|${boardHash.toString(36)}`;
  }

  // 개선된 블록 배치 점수 계산
  function evaluatePlacementQuality(placement: Placement & {shapeCells:Array<{dx:number;dy:number}>}): number {
    let score = 0;

    // 1. 블록 크기 가중치 (큰 블록 우선)
    const blockSize = idToCells.get(placement.blockId) || 0;
    score += blockSize * 10;

    // 2. 연결성 보너스 (많은 프론티어 생성)
    const newFrontierCells = new Set<number>();
    for (const {dx, dy} of placement.shapeCells) {
      const x = placement.x + dx, y = placement.y + dy;
      for (const [cx, cy] of DIAGS) {
        const nx = x + cx, ny = y + cy;
        if (inBoard(nx, ny) && board[ny][nx] === 0) {
          newFrontierCells.add(ny * BOARD_SIZE + nx);
        }
      }
    }
    score += newFrontierCells.size * 2;

    // 3. 중앙성 보너스 (보드 중앙 근처 배치 선호)
    const centerX = BOARD_SIZE / 2, centerY = BOARD_SIZE / 2;
    const avgX = placement.shapeCells.reduce((sum, c) => sum + placement.x + c.dx, 0) / placement.shapeCells.length;
    const avgY = placement.shapeCells.reduce((sum, c) => sum + placement.y + c.dy, 0) / placement.shapeCells.length;
    const distFromCenter = Math.sqrt((avgX - centerX) ** 2 + (avgY - centerY) ** 2);
    score += Math.max(0, 20 - distFromCenter);

    // 4. 코너 보너스 (corner에 가까울수록 향후 확장성 좋음)
    if (!firstPlaced) {
      const touchesCorner = placement.shapeCells.some(c => {
        const x = placement.x + c.dx, y = placement.y + c.dy;
        return CORNERS.some(corner => corner.x === x && corner.y === y);
      });
      if (touchesCorner) score += 50;
    }

    return score;
  }

  function enumeratePlacementsAtAnchor(ax:number, ay:number) {
    const out: Array<Placement & {shapeCells:Array<{dx:number;dy:number}>, quality: number}> = [];
    const ids = availableBlockIds.filter(id=>!used.has(id));

    // 우선순위 기반 정렬
    ids.sort((a,b)=>(idToPriority.get(b)||0)-(idToPriority.get(a)||0));
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

          const placement = { blockId:id, color:targetColor, x:ox, y:oy, shapeIndex:s, shapeCells };
          const quality = evaluatePlacementQuality(placement);
          out.push({ ...placement, quality });
        }
      }
    }

    // 품질 기반 정렬 (높은 품질 우선)
    out.sort((a, b) => b.quality - a.quality);
    return out;
  }

  // 개선된 이동 생성: 품질과 제약도 기반 정렬
  function generateMoves(){
    const anchors = firstPlaced
      ? Array.from(frontierCells(board, targetColor)).map(v => ({x: v%BOARD_SIZE, y: Math.floor(v/BOARD_SIZE)}))
      : CORNERS.filter(({x,y}) => board[y][x]===0);

    if (!anchors.length) return [];

    const aggregated: Array<Placement & {shapeCells:Array<{dx:number;dy:number}>, anchorDeg:number, quality:number}> = [];
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

    // 개선된 정렬: MRV + 품질 조합
    aggregated.sort((a,b)=>{
      // 1차: 제약도 (MRV - Most Constraining Variable)
      if (a.anchorDeg !== b.anchorDeg) return a.anchorDeg - b.anchorDeg;

      // 2차: 품질 점수 (높은 품질 우선)
      if (Math.abs(a.quality - b.quality) > 0.1) return b.quality - a.quality;

      // 3차: 블록 크기 (큰 블록 우선)
      const cellDiff = (idToCells.get(b.blockId)||0) - (idToCells.get(a.blockId)||0);
      if (cellDiff !== 0) return cellDiff;

      return 0;
    });
    return aggregated;
  }

  function remainingSizes(): number[] {
    const sizes:number[] = [];
    for (const id of availableBlockIds) if (!used.has(id)) sizes.push(idToCells.get(id)!);
    return sizes;
  }

  // 점진적 딥닝을 위한 다단계 탐색
  function progressiveDeepening() {
    const phases = [
      { name: "quick", timeRatio: 0.1, maxMoves: 10 },    // 빠른 탐색
      { name: "medium", timeRatio: 0.3, maxMoves: 50 },   // 중간 탐색
      { name: "deep", timeRatio: 0.6, maxMoves: -1 }      // 깊은 탐색
    ];

    let phaseStartTime = Date.now();

    for (const phase of phases) {
      const phaseTimeLimit = timeLimit * phase.timeRatio;
      const phaseEndTime = t0 + (Date.now() - t0) + phaseTimeLimit;

      // 현재 단계에서 탐색
      dfs(0, phase.maxMoves, phaseEndTime);

      if (timedOut || Date.now() - t0 > timeLimit * 0.9) break;

      // 다음 단계로 넘어가기 전 현재 결과 보존
      if (bestScore > 0) {
        console.log(`Phase ${phase.name}: ${bestScore} points, ${iterations} iterations`);
      }
    }
  }

  function dfs(score:number, maxMovesToExplore = -1, phaseTimeLimit = timeLimit){
    iterations++;
    if (Date.now() > phaseTimeLimit || Date.now()-t0 > timeLimit) {
      timedOut = true;
      return;
    }
    if (!canStart() && !firstPlaced) return;

    // 🔒 안전 가지치기: 컴포넌트 불가능성 검사
    if (earlyInfeasible(board, remainingSizes())) return;

    // 개선된 상계 검사
    if (advancedUpperBound(score) <= bestScore) return;

    const sk = compactStateKey();
    const prev = TT.get(sk);
    const currentDepth = placedSeq.length;
    if (prev && prev.score >= score && prev.depth <= currentDepth) return;
    TT.set(sk, {score, depth: currentDepth});

    const moves = generateMoves();
    if (!moves.length) {
      if (score > bestScore) {
        bestScore = score;
        bestPlacements = placedSeq.slice();
      }
      return;
    }

    // 이동 제한 적용 (빠른 단계에서는 일부만 탐색)
    const movesToExplore = maxMovesToExplore > 0 ? Math.min(moves.length, maxMovesToExplore) : moves.length;

    for (let i = 0; i < movesToExplore && !timedOut; i++) {
      const m = moves[i];

      place(board, targetColor, (m as any).shapeCells, m.x, m.y);
      placedSeq.push(m);
      const wasFirst = !firstPlaced; if (wasFirst) firstPlaced = true;
      used.add(m.blockId);

      dfs(score + (idToCells.get(m.blockId)||0), maxMovesToExplore, phaseTimeLimit);

      used.delete(m.blockId);
      if (wasFirst) firstPlaced = false;
      placedSeq.pop();
      unplace(board, (m as any).shapeCells, m.x, m.y);
    }
  }

  // 점진적 딥닝 실행
  progressiveDeepening();

  return {
    score: bestScore,
    placements: bestPlacements,
    timedOut,
    iterations
  };
}
