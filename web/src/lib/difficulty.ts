/**
 * 스테이지 난이도 관련 유틸리티
 * 웹과 모바일 클라이언트 간의 난이도 표시 동기화를 위한 표준 매핑
 */

export interface DifficultyInfo {
  level: number;
  label: string;
  color: string;
  cssClass: string;
}

/**
 * 난이도 레벨별 표준 매핑 (1-10)
 * StageEditor.tsx의 매핑을 기준으로 색상 정보 추가
 */
export const DIFFICULTY_MAPPING: Record<number, DifficultyInfo> = {
  1: { level: 1, label: '매우 쉬움', color: '#22c55e', cssClass: 'text-green-400' },
  2: { level: 2, label: '쉬움', color: '#84cc16', cssClass: 'text-lime-400' },
  3: { level: 3, label: '보통', color: '#eab308', cssClass: 'text-yellow-400' },
  4: { level: 4, label: '어려움', color: '#f97316', cssClass: 'text-orange-400' },
  5: { level: 5, label: '매우 어려움', color: '#ef4444', cssClass: 'text-red-400' },
  6: { level: 6, label: '극한', color: '#dc2626', cssClass: 'text-red-500' },
  7: { level: 7, label: '악몽', color: '#b91c1c', cssClass: 'text-red-600' },
  8: { level: 8, label: '지옥', color: '#991b1b', cssClass: 'text-red-700' },
  9: { level: 9, label: '전설', color: '#7c2d12', cssClass: 'text-red-800' },
  10: { level: 10, label: '신화', color: '#451a03', cssClass: 'text-red-900' }
};

/**
 * 난이도 레벨에 따른 라벨 반환
 */
export function getDifficultyLabel(difficulty: number): string {
  return DIFFICULTY_MAPPING[difficulty]?.label || '알 수 없음';
}

/**
 * 난이도 레벨에 따른 색상 반환 (Hex)
 */
export function getDifficultyColor(difficulty: number): string {
  return DIFFICULTY_MAPPING[difficulty]?.color || '#6b7280';
}

/**
 * 난이도 레벨에 따른 CSS 클래스 반환
 */
export function getDifficultyCssClass(difficulty: number): string {
  return DIFFICULTY_MAPPING[difficulty]?.cssClass || 'text-gray-400';
}

/**
 * 난이도 정보 전체 반환
 */
export function getDifficultyInfo(difficulty: number): DifficultyInfo | null {
  return DIFFICULTY_MAPPING[difficulty] || null;
}

/**
 * 모든 난이도 레벨 반환 (1-10)
 */
export function getAllDifficultyLevels(): DifficultyInfo[] {
  return Object.values(DIFFICULTY_MAPPING);
}

/**
 * 난이도가 유효한 범위인지 확인
 */
export function isValidDifficultyLevel(difficulty: number): boolean {
  return difficulty >= 1 && difficulty <= 10 && DIFFICULTY_MAPPING[difficulty] !== undefined;
}