'use client';

import { useState, useCallback } from 'react';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';

interface MarkdownEditorProps {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  height?: number;
  className?: string;
  disabled?: boolean;
  showPreview?: boolean;
}

export default function MarkdownEditor({
  value,
  onChange,
  placeholder = '마크다운 형식으로 내용을 입력해주세요...',
  height = 300,
  className = '',
  disabled = false,
  showPreview = true,
}: MarkdownEditorProps) {
  const [previewMode, setPreviewMode] = useState<'edit' | 'preview' | 'split'>('edit');

  // 마크다운 단축키 삽입
  const insertMarkdown = useCallback((before: string, after: string = '') => {
    const textarea = document.querySelector('.markdown-textarea') as HTMLTextAreaElement;
    if (!textarea) return;

    const start = textarea.selectionStart;
    const end = textarea.selectionEnd;
    const selectedText = value.substring(start, end);
    const newText = value.substring(0, start) + before + selectedText + after + value.substring(end);

    onChange(newText);

    // 커서 위치 조정
    setTimeout(() => {
      textarea.focus();
      textarea.selectionStart = textarea.selectionEnd = start + before.length + selectedText.length;
    }, 0);
  }, [value, onChange]);

  // 툴바 버튼 핸들러
  const handleBold = () => insertMarkdown('**', '**');
  const handleItalic = () => insertMarkdown('*', '*');
  const handleStrikethrough = () => insertMarkdown('~~', '~~');
  const handleCode = () => insertMarkdown('`', '`');
  const handleCodeBlock = () => insertMarkdown('\n```\n', '\n```\n');
  const handleLink = () => insertMarkdown('[', '](url)');
  const handleImage = () => insertMarkdown('![alt](', ')');
  const handleHeading = (level: number) => insertMarkdown('#'.repeat(level) + ' ');
  const handleList = () => insertMarkdown('\n- ');
  const handleOrderedList = () => insertMarkdown('\n1. ');
  const handleQuote = () => insertMarkdown('\n> ');
  const handleHr = () => insertMarkdown('\n---\n');

  // 키보드 단축키
  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.ctrlKey || e.metaKey) {
      switch (e.key) {
        case 'b':
          e.preventDefault();
          handleBold();
          break;
        case 'i':
          e.preventDefault();
          handleItalic();
          break;
        case 'k':
          e.preventDefault();
          handleLink();
          break;
      }
    }
  };

  return (
    <div className={`markdown-editor ${className}`}>
      {/* 툴바 */}
      <div className="markdown-toolbar border border-gray-300 rounded-t-lg bg-gray-50 p-2 flex flex-wrap gap-1">
        <div className="flex gap-1 border-r border-gray-300 pr-2">
          <button
            type="button"
            onClick={() => handleHeading(1)}
            className="p-2 hover:bg-gray-200 rounded text-sm font-semibold"
            title="제목 1"
            disabled={disabled}
          >
            H1
          </button>
          <button
            type="button"
            onClick={() => handleHeading(2)}
            className="p-2 hover:bg-gray-200 rounded text-sm font-semibold"
            title="제목 2"
            disabled={disabled}
          >
            H2
          </button>
          <button
            type="button"
            onClick={() => handleHeading(3)}
            className="p-2 hover:bg-gray-200 rounded text-sm font-semibold"
            title="제목 3"
            disabled={disabled}
          >
            H3
          </button>
        </div>

        <div className="flex gap-1 border-r border-gray-300 pr-2">
          <button
            type="button"
            onClick={handleBold}
            className="p-2 hover:bg-gray-200 rounded font-bold"
            title="굵게 (Ctrl+B)"
            disabled={disabled}
          >
            B
          </button>
          <button
            type="button"
            onClick={handleItalic}
            className="p-2 hover:bg-gray-200 rounded italic"
            title="기울임 (Ctrl+I)"
            disabled={disabled}
          >
            I
          </button>
          <button
            type="button"
            onClick={handleStrikethrough}
            className="p-2 hover:bg-gray-200 rounded line-through"
            title="취소선"
            disabled={disabled}
          >
            S
          </button>
        </div>

        <div className="flex gap-1 border-r border-gray-300 pr-2">
          <button
            type="button"
            onClick={handleList}
            className="p-2 hover:bg-gray-200 rounded"
            title="목록"
            disabled={disabled}
          >
            •
          </button>
          <button
            type="button"
            onClick={handleOrderedList}
            className="p-2 hover:bg-gray-200 rounded"
            title="순서 목록"
            disabled={disabled}
          >
            1.
          </button>
          <button
            type="button"
            onClick={handleQuote}
            className="p-2 hover:bg-gray-200 rounded"
            title="인용"
            disabled={disabled}
          >
            &quot;
          </button>
        </div>

        <div className="flex gap-1 border-r border-gray-300 pr-2">
          <button
            type="button"
            onClick={handleCode}
            className="p-2 hover:bg-gray-200 rounded text-xs font-mono"
            title="인라인 코드"
            disabled={disabled}
          >
            {'<>'}
          </button>
          <button
            type="button"
            onClick={handleCodeBlock}
            className="p-2 hover:bg-gray-200 rounded text-xs font-mono"
            title="코드 블록"
            disabled={disabled}
          >
            {'</>'}
          </button>
        </div>

        <div className="flex gap-1 border-r border-gray-300 pr-2">
          <button
            type="button"
            onClick={handleLink}
            className="p-2 hover:bg-gray-200 rounded"
            title="링크 (Ctrl+K)"
            disabled={disabled}
          >
            🔗
          </button>
          <button
            type="button"
            onClick={handleImage}
            className="p-2 hover:bg-gray-200 rounded"
            title="이미지"
            disabled={disabled}
          >
            🖼️
          </button>
        </div>

        <div className="flex gap-1">
          <button
            type="button"
            onClick={handleHr}
            className="p-2 hover:bg-gray-200 rounded"
            title="구분선"
            disabled={disabled}
          >
            ─
          </button>
        </div>

        {/* 프리뷰 모드 전환 */}
        {showPreview && (
          <div className="ml-auto flex gap-1 border-l border-gray-300 pl-2">
            <button
              type="button"
              onClick={() => setPreviewMode('edit')}
              className={`px-3 py-1 rounded text-sm ${
                previewMode === 'edit' ? 'bg-blue-600 text-white' : 'hover:bg-gray-200'
              }`}
              disabled={disabled}
            >
              편집
            </button>
            <button
              type="button"
              onClick={() => setPreviewMode('split')}
              className={`px-3 py-1 rounded text-sm ${
                previewMode === 'split' ? 'bg-blue-600 text-white' : 'hover:bg-gray-200'
              }`}
              disabled={disabled}
            >
              분할
            </button>
            <button
              type="button"
              onClick={() => setPreviewMode('preview')}
              className={`px-3 py-1 rounded text-sm ${
                previewMode === 'preview' ? 'bg-blue-600 text-white' : 'hover:bg-gray-200'
              }`}
              disabled={disabled}
            >
              미리보기
            </button>
          </div>
        )}
      </div>

      {/* 에디터 영역 */}
      <div className="markdown-content border border-t-0 border-gray-300 rounded-b-lg overflow-hidden">
        <div className={`flex ${previewMode === 'split' ? 'divide-x divide-gray-300' : ''}`}>
          {/* 편집 영역 */}
          {(previewMode === 'edit' || previewMode === 'split') && (
            <div className={previewMode === 'split' ? 'w-1/2' : 'w-full'}>
              <textarea
                className="markdown-textarea w-full p-4 font-mono text-sm resize-none focus:outline-none focus:ring-2 focus:ring-blue-500"
                style={{ height: `${height}px` }}
                value={value}
                onChange={(e) => onChange(e.target.value)}
                onKeyDown={handleKeyDown}
                placeholder={placeholder}
                disabled={disabled}
              />
            </div>
          )}

          {/* 미리보기 영역 */}
          {(previewMode === 'preview' || previewMode === 'split') && (
            <div
              className={`${previewMode === 'split' ? 'w-1/2' : 'w-full'} p-4 overflow-y-auto bg-white`}
              style={{ height: `${height}px` }}
            >
              {value.trim() ? (
                <ReactMarkdown
                  remarkPlugins={[remarkGfm]}
                  className="markdown-preview prose prose-sm max-w-none"
                >
                  {value}
                </ReactMarkdown>
              ) : (
                <p className="text-gray-400 italic">미리보기가 여기에 표시됩니다...</p>
              )}
            </div>
          )}
        </div>
      </div>

      {/* 마크다운 가이드 */}
      <div className="mt-2 text-xs text-gray-500">
        <details>
          <summary className="cursor-pointer hover:text-gray-700">마크다운 문법 가이드</summary>
          <div className="mt-2 space-y-1 pl-4">
            <p><code># 제목</code> - 제목 (H1~H6)</p>
            <p><code>**굵게**</code> - 굵은 텍스트</p>
            <p><code>*기울임*</code> - 기울임 텍스트</p>
            <p><code>~~취소선~~</code> - 취소선</p>
            <p><code>[링크](url)</code> - 링크</p>
            <p><code>![이미지](url)</code> - 이미지</p>
            <p><code>- 항목</code> - 목록</p>
            <p><code>1. 항목</code> - 순서 목록</p>
            <p><code>&gt; 인용</code> - 인용구</p>
            <p><code>`코드`</code> - 인라인 코드</p>
            <p><code>```코드 블록```</code> - 코드 블록</p>
          </div>
        </details>
      </div>

      {/* 스타일 */}
      <style jsx global>{`
        .markdown-preview h1 {
          font-size: 2em;
          font-weight: bold;
          margin-top: 0.67em;
          margin-bottom: 0.67em;
        }
        .markdown-preview h2 {
          font-size: 1.5em;
          font-weight: bold;
          margin-top: 0.83em;
          margin-bottom: 0.83em;
        }
        .markdown-preview h3 {
          font-size: 1.17em;
          font-weight: bold;
          margin-top: 1em;
          margin-bottom: 1em;
        }
        .markdown-preview p {
          margin: 1em 0;
        }
        .markdown-preview ul, .markdown-preview ol {
          margin: 1em 0;
          padding-left: 2em;
        }
        .markdown-preview blockquote {
          border-left: 4px solid #e5e7eb;
          padding-left: 1em;
          margin: 1em 0;
          color: #6b7280;
        }
        .markdown-preview code {
          background-color: #f3f4f6;
          padding: 0.2em 0.4em;
          border-radius: 3px;
          font-family: monospace;
          font-size: 0.9em;
        }
        .markdown-preview pre {
          background-color: #1f2937;
          color: #f9fafb;
          padding: 1em;
          border-radius: 0.5rem;
          overflow-x: auto;
        }
        .markdown-preview pre code {
          background-color: transparent;
          padding: 0;
          color: inherit;
        }
        .markdown-preview a {
          color: #3b82f6;
          text-decoration: underline;
        }
        .markdown-preview img {
          max-width: 100%;
          height: auto;
        }
        .markdown-preview hr {
          border: none;
          border-top: 2px solid #e5e7eb;
          margin: 2em 0;
        }
        .markdown-preview table {
          border-collapse: collapse;
          width: 100%;
          margin: 1em 0;
        }
        .markdown-preview th,
        .markdown-preview td {
          border: 1px solid #e5e7eb;
          padding: 0.5em;
          text-align: left;
        }
        .markdown-preview th {
          background-color: #f3f4f6;
          font-weight: bold;
        }
      `}</style>
    </div>
  );
}
