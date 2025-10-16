/**
 * Content Format Detection and Utilities
 * HTML과 Markdown 형식을 자동으로 판별하는 유틸리티
 */

export type ContentFormat = 'html' | 'markdown' | 'plain';

/**
 * 콘텐츠 형식을 자동으로 판별합니다
 * @param content - 판별할 콘텐츠 문자열
 * @returns 'html' | 'markdown' | 'plain'
 */
export function detectContentFormat(content: string): ContentFormat {
  if (!content || content.trim() === '') {
    return 'plain';
  }

  const trimmed = content.trim();

  // HTML 형식 감지
  // Quill 에디터의 출력 형식: <p>, <strong>, <em>, <ul>, <ol>, <h1> 등
  const htmlPatterns = [
    /<p[^>]*>/i,              // <p> 태그
    /<div[^>]*>/i,            // <div> 태그
    /<br\s*\/?>/i,            // <br> 태그
    /<strong>/i,              // <strong> 태그
    /<em>/i,                  // <em> 태그
    /<span[^>]*style=/i,      // style 속성을 가진 span
    /<ul>/i,                  // <ul> 태그
    /<ol>/i,                  // <ol> 태그
    /<li>/i,                  // <li> 태그
    /<h[1-6]>/i,              // <h1>~<h6> 태그
    /<a[^>]*href=/i,          // href 속성을 가진 a 태그
    /<img[^>]*src=/i,         // src 속성을 가진 img 태그
    /<blockquote>/i,          // <blockquote> 태그
    /<pre>/i,                 // <pre> 태그
  ];

  // HTML 패턴 중 하나라도 매칭되면 HTML로 판정
  const hasHtmlTags = htmlPatterns.some(pattern => pattern.test(trimmed));
  if (hasHtmlTags) {
    return 'html';
  }

  // Markdown 형식 감지
  const markdownPatterns = [
    /^#{1,6}\s+/m,            // 헤더 (# ## ###)
    /\*\*[^*]+\*\*/,          // 굵게 (**text**)
    /\*[^*]+\*/,              // 기울임 (*text*)
    /~~[^~]+~~/,              // 취소선 (~~text~~)
    /^\s*[-*+]\s+/m,          // 목록 (- item, * item, + item)
    /^\s*\d+\.\s+/m,          // 순서 목록 (1. item)
    /\[([^\]]+)\]\(([^)]+)\)/,// 링크 ([text](url))
    /!\[([^\]]*)\]\(([^)]+)\)/,// 이미지 (![alt](url))
    /^>\s+/m,                 // 인용 (> quote)
    /`[^`]+`/,                // 인라인 코드 (`code`)
    /```[\s\S]*?```/,         // 코드 블록 (```code```)
    /^\s*\|.+\|/m,            // 테이블 (| col1 | col2 |)
    /^\s*[-=]{3,}\s*$/m,      // 수평선 (---, ===)
    /\n\n/,                   // 연속된 줄바꿈 (마크다운의 특징)
  ];

  // Markdown 패턴 중 2개 이상 매칭되면 Markdown으로 판정
  const markdownMatchCount = markdownPatterns.filter(pattern => pattern.test(trimmed)).length;
  if (markdownMatchCount >= 2) {
    return 'markdown';
  }

  // 특별한 패턴이 없으면 일반 텍스트
  return 'plain';
}

/**
 * HTML 콘텐츠인지 확인
 */
export function isHtmlContent(content: string): boolean {
  return detectContentFormat(content) === 'html';
}

/**
 * Markdown 콘텐츠인지 확인
 */
export function isMarkdownContent(content: string): boolean {
  return detectContentFormat(content) === 'markdown';
}

/**
 * 콘텐츠 형식 정보 반환
 */
export function getContentFormatInfo(content: string) {
  const format = detectContentFormat(content);

  return {
    format,
    isHtml: format === 'html',
    isMarkdown: format === 'markdown',
    isPlain: format === 'plain',
    length: content.length,
    isEmpty: !content || content.trim() === '',
  };
}

/**
 * 콘텐츠 미리보기 텍스트 생성 (HTML 태그 제거)
 */
export function getPlainTextPreview(content: string, maxLength: number = 200): string {
  if (!content) return '';

  // HTML 태그 제거
  let plain = content.replace(/<[^>]+>/g, '');

  // Markdown 링크 형식을 텍스트로 변환 [text](url) -> text
  plain = plain.replace(/\[([^\]]+)\]\([^)]+\)/g, '$1');

  // Markdown 이미지 형식 제거 ![alt](url) -> [이미지]
  plain = plain.replace(/!\[([^\]]*)\]\([^)]+\)/g, '[이미지]');

  // Markdown 서식 제거
  plain = plain.replace(/(\*\*|__)(.*?)\1/g, '$2'); // 굵게
  plain = plain.replace(/(\*|_)(.*?)\1/g, '$2');     // 기울임
  plain = plain.replace(/~~(.*?)~~/g, '$1');         // 취소선
  plain = plain.replace(/`([^`]+)`/g, '$1');         // 인라인 코드
  plain = plain.replace(/^#{1,6}\s+/gm, '');         // 헤더

  // 여러 공백을 하나로
  plain = plain.replace(/\s+/g, ' ').trim();

  // 길이 제한
  if (plain.length > maxLength) {
    return plain.substring(0, maxLength) + '...';
  }

  return plain;
}
