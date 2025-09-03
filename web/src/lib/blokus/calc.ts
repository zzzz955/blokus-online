// src/lib/blokus/calc.ts
import type { BoardState } from './solver';
import { solveOptimalScoreSingleColor } from './solver';
import { BLOCK_DEFINITIONS } from './blocks';

/**
 * 관리자 UI에서 사용하는 단일색 최적 점수 계산 - 최적화 버전
 * - 기본 타임리밋을 120초로 상향(싱글 플레이 스테이지용)
 */
export async function calculateOptimalScoreExact(
  boardState: BoardState,
  availableBlockIds: number[],
  timeLimitMs = 120_000,
  targetColor = 1 // 기본 1번 색
): Promise<{ score: number; timedOut?: boolean; iterations?: number }> {
  const result = solveOptimalScoreSingleColor(
    boardState,
    availableBlockIds,
    BLOCK_DEFINITIONS,
    { timeLimitMs },
    targetColor
  );
  return {
    score: result.score,
    timedOut: result.timedOut,
    iterations: result.iterations
  };
}
