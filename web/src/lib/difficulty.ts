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
 * 난이도별 그라데이션 색상 생성 함수 (초록->노랑->주황->빨강->보라)
 */
function getGradientColor(difficulty: number): { color: string; cssClass: string } {
  // 난이도를 0-1 범위로 정규화
  const t = (difficulty - 1) / 9;

  // 5단계 색상 포인트 정의 (RGB 0-255)
  const colorPoints = [
    { r: 0, g: 204, b: 0 },     // 밝은 초록
    { r: 127, g: 255, b: 0 },   // 연두
    { r: 255, g: 255, b: 0 },   // 노랑
    { r: 255, g: 127, b: 0 },   // 주황
    { r: 255, g: 0, b: 0 },     // 빨강
    { r: 204, g: 0, b: 204 }    // 보라
  ];

  // t 값에 따라 적절한 색상 구간에서 보간
  const scaledT = t * (colorPoints.length - 1);
  const index = Math.floor(scaledT);
  const localT = scaledT - index;

  // 마지막 색상을 넘지 않도록 제한
  if (index >= colorPoints.length - 1) {
    const lastColor = colorPoints[colorPoints.length - 1];
    return {
      color: `#${lastColor.r.toString(16).padStart(2, '0')}${lastColor.g.toString(16).padStart(2, '0')}${lastColor.b.toString(16).padStart(2, '0')}`,
      cssClass: 'text-purple-400'
    };
  }

  // 두 색상 사이에서 보간
  const color1 = colorPoints[index];
  const color2 = colorPoints[index + 1];

  const r = Math.round(color1.r + (color2.r - color1.r) * localT);
  const g = Math.round(color1.g + (color2.g - color1.g) * localT);
  const b = Math.round(color1.b + (color2.b - color1.b) * localT);

  // CSS 클래스는 대략적인 색상 구간으로 매핑
  let cssClass = 'text-gray-400';
  if (difficulty <= 2) cssClass = 'text-green-400';
  else if (difficulty <= 4) cssClass = 'text-yellow-400';
  else if (difficulty <= 6) cssClass = 'text-orange-400';
  else if (difficulty <= 8) cssClass = 'text-red-400';
  else cssClass = 'text-purple-400';

  return {
    color: `#${r.toString(16).padStart(2, '0')}${g.toString(16).padStart(2, '0')}${b.toString(16).padStart(2, '0')}`,
    cssClass
  };
}

/**
 * 난이도 레벨별 표준 매핑 (1-10)
 * 동적 그라데이션 색상 생성
 */
export const DIFFICULTY_MAPPING: Record<number, DifficultyInfo> = (() => {
  const mapping: Record<number, DifficultyInfo> = {};
  const labels = [
    '매우 쉬움', '쉬움', '보통', '어려움', '매우 어려움',
    '극한', '악몽', '지옥', '전설', '신화'
  ];

  for (let i = 1; i <= 10; i++) {
    const { color, cssClass } = getGradientColor(i);
    mapping[i] = {
      level: i,
      label: labels[i - 1],
      color,
      cssClass
    };
  }

  return mapping;
})();

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