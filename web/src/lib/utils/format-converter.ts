/**
 * Format Converter Utilities
 * HTML ↔ Markdown 상호 변환 기능
 */

import TurndownService from 'turndown';

/**
 * HTML을 Markdown으로 변환
 * Quill 에디터의 HTML 출력을 Markdown으로 변환
 */
export function htmlToMarkdown(html: string): string {
  if (!html || html.trim() === '' || html === '<p><br></p>') {
    return '';
  }

  const turndownService = new TurndownService({
    headingStyle: 'atx',
    codeBlockStyle: 'fenced',
    bulletListMarker: '-',
    emDelimiter: '*',
    strongDelimiter: '**',
  });

  // Quill의 특수 포맷 처리
  turndownService.addRule('strikethrough', {
    filter: ['s', 'del'] as any,
    replacement: (content) => `~~${content}~~`,
  });

  // Quill의 color, background 스타일 제거 (markdown 미지원)
  turndownService.addRule('removeStyles', {
    filter: (node) => {
      return node.nodeName === 'SPAN' && (
        node.hasAttribute('style') ||
        node.classList.contains('ql-size') ||
        node.classList.contains('ql-font')
      );
    },
    replacement: (content) => content,
  });

  // 빈 paragraph 처리
  turndownService.addRule('emptyParagraph', {
    filter: (node) => {
      return node.nodeName === 'P' && node.textContent?.trim() === '';
    },
    replacement: () => '\n',
  });

  try {
    const markdown = turndownService.turndown(html);
    return markdown.trim();
  } catch (error) {
    console.error('HTML to Markdown conversion error:', error);
    return html; // 변환 실패 시 원본 반환
  }
}

/**
 * Markdown을 HTML로 변환
 * 간단한 Markdown을 Quill이 이해할 수 있는 HTML로 변환
 */
export function markdownToHtml(markdown: string): string {
  if (!markdown || markdown.trim() === '') {
    return '<p><br></p>';
  }

  let html = markdown;

  // 1. 코드 블록 (```)
  html = html.replace(/```(\w+)?\n([\s\S]*?)```/g, (_, lang, code) => {
    return `<pre><code${lang ? ` class="language-${lang}"` : ''}>${escapeHtml(code.trim())}</code></pre>`;
  });

  // 2. 인라인 코드 (`)
  html = html.replace(/`([^`]+)`/g, '<code>$1</code>');

  // 3. 헤더 (# ## ###)
  html = html.replace(/^### (.+)$/gm, '<h3>$1</h3>');
  html = html.replace(/^## (.+)$/gm, '<h2>$1</h2>');
  html = html.replace(/^# (.+)$/gm, '<h1>$1</h1>');

  // 4. 굵게 (**text** or __text__)
  html = html.replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>');
  html = html.replace(/__(.+?)__/g, '<strong>$1</strong>');

  // 5. 기울임 (*text* or _text_)
  html = html.replace(/\*(.+?)\*/g, '<em>$1</em>');
  html = html.replace(/_(.+?)_/g, '<em>$1</em>');

  // 6. 취소선 (~~text~~)
  html = html.replace(/~~(.+?)~~/g, '<s>$1</s>');

  // 7. 링크 ([text](url))
  html = html.replace(/\[([^\]]+)\]\(([^)]+)\)/g, '<a href="$2" target="_blank" rel="noopener noreferrer">$1</a>');

  // 8. 이미지 (![alt](url))
  html = html.replace(/!\[([^\]]*)\]\(([^)]+)\)/g, '<img src="$2" alt="$1" />');

  // 9. 순서 없는 목록 (- item)
  html = html.replace(/^\- (.+)$/gm, '<li>$1</li>');
  html = html.replace(/(<li>.*<\/li>\n?)+/g, '<ul>$&</ul>');

  // 10. 순서 있는 목록 (1. item)
  html = html.replace(/^\d+\. (.+)$/gm, '<li>$1</li>');
  html = html.replace(/(<li>.*<\/li>\n?)+/g, (match) => {
    // ul로 이미 감싸진 것이 아니면 ol로 감싸기
    if (!match.includes('<ul>')) {
      return `<ol>${match}</ol>`;
    }
    return match;
  });

  // 11. 인용구 (> text)
  html = html.replace(/^&gt; (.+)$/gm, '<blockquote>$1</blockquote>');
  html = html.replace(/^> (.+)$/gm, '<blockquote>$1</blockquote>');

  // 12. 수평선 (---, ***, ___)
  html = html.replace(/^(---|===|\*\*\*|___)$/gm, '<hr />');

  // 13. 줄바꿈 처리
  html = html.split('\n\n').map(para => {
    // 이미 HTML 태그로 감싸진 경우 그대로 반환
    if (para.match(/^<(h[1-6]|ul|ol|blockquote|pre|hr)/)) {
      return para;
    }
    // 빈 줄
    if (para.trim() === '') {
      return '<p><br></p>';
    }
    // 일반 텍스트는 p 태그로 감싸기
    return `<p>${para.replace(/\n/g, '<br>')}</p>`;
  }).join('\n');

  return html;
}

/**
 * HTML 특수문자 이스케이프
 */
function escapeHtml(text: string): string {
  const map: Record<string, string> = {
    '&': '&amp;',
    '<': '&lt;',
    '>': '&gt;',
    '"': '&quot;',
    "'": '&#039;',
  };
  return text.replace(/[&<>"']/g, (m) => map[m]);
}

/**
 * Markdown 유효성 검사
 */
export function isValidMarkdown(text: string): boolean {
  if (!text || text.trim() === '') return true;

  // 기본적인 Markdown 문법 패턴 확인
  const markdownPatterns = [
    /^#{1,6}\s/m,           // 헤더
    /\*\*[^*]+\*\*/,        // 굵게
    /\*[^*]+\*/,            // 기울임
    /\[[^\]]+\]\([^)]+\)/,  // 링크
    /^\s*[-*+]\s/m,         // 목록
    /^\s*\d+\.\s/m,         // 순서 목록
    /```[\s\S]*```/,        // 코드 블록
    /`[^`]+`/,              // 인라인 코드
  ];

  // 패턴 중 하나라도 일치하거나 일반 텍스트면 유효
  return markdownPatterns.some(pattern => pattern.test(text)) || true;
}
