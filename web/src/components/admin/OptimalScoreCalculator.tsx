// src/components/admin/OptimalScoreCalculator.tsx
// 경고: 실제 탐색 로직은 lib/blokus/solver 로 이관되었습니다.
// 이 파일은 하위 호환을 위한 래퍼입니다.

import type { BoardState } from '@/lib/blokus/solver';
import { calculateOptimalScoreExact } from '@/lib/blokus/calc';

export class OptimalScoreCalculator {
  async calculateGreedy(boardState: BoardState, availableBlockIds: number[]): Promise<number> {
    // 정확 탐색 결과 객체에서 score만 반환
    const { score /*, timedOut, iterations */ } =
      await calculateOptimalScoreExact(boardState, availableBlockIds, 15000);
    return score;
  }
}

// 과거에 쓰던 헬퍼 이름을 유지
// export const calculateOptimalScore = async (
//   boardState: BoardState,
//   availableBlockIds: number[]
// ): Promise<number> => {
//   return await calculateOptimalScoreExact(boardState, availableBlockIds, 15000);
// };
