// src/lib/blokus/calc.ts
import type { BoardState } from './solver';
import { solveOptimalScoreSingleColor } from './solver';
import { BLOCK_DEFINITIONS } from './blocks';

/** 관리자 UI에서 사용하는 단일색 최적 점수 계산 */
export async function calculateOptimalScoreExact(
  boardState: BoardState,
  availableBlockIds: number[],
  timeLimitMs = 15000,
  targetColor = 1 // 기본 1번 색
): Promise<number> {
  const { score } = solveOptimalScoreSingleColor(
    boardState,
    availableBlockIds,
    BLOCK_DEFINITIONS,
    { timeLimitMs },
    targetColor
  );
  return score;
}
