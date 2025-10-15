'use client';

import { useState, useCallback, useRef, useEffect } from 'react';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import { Prism as SyntaxHighlighter } from 'react-syntax-highlighter';
import { vscDarkPlus } from 'react-syntax-highlighter/dist/esm/styles/prism';
import {
  uploadImage,
  extractImageFromClipboard,
  extractImageFromDrop,
  validateImageFile,
} from '@/lib/utils/image-upload';

interface MarkdownEditorProps {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  height?: number;
  className?: string;
  disabled?: boolean;
  showPreview?: boolean;
  autoSaveKey?: string; // LocalStorage 저장 키
  enableAutoSave?: boolean;
  enableImageUpload?: boolean;
  enableTemplates?: boolean;
}

export default function MarkdownEditor({
  value,
  onChange,
  placeholder = '마크다운 형식으로 내용을 입력해주세요...',
  height = 300,
  className = '',
  disabled = false,
  showPreview = true,
  autoSaveKey,
  enableAutoSave = true,
  enableImageUpload = true,
  enableTemplates = true,
}: MarkdownEditorProps) {
  const [previewMode, setPreviewMode] = useState<'edit' | 'preview' | 'split'>('edit');
  const [isDragging, setIsDragging] = useState(false);
  const [isUploading, setIsUploading] = useState(false);
  const [showTemplates, setShowTemplates] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const textareaRef = useRef<HTMLTextAreaElement>(null);
  const autoSaveTimeoutRef = useRef<NodeJS.Timeout>();

  // 자동 저장 기능
  useEffect(() => {
    if (!enableAutoSave || !autoSaveKey || disabled) return;

    // 컴포넌트 마운트 시 저장된 내용 복원
    const saved = localStorage.getItem(`autosave_${autoSaveKey}`);
    if (saved && !value) {
      try {
        const data = JSON.parse(saved);
        if (data.content && Date.now() - data.timestamp < 24 * 60 * 60 * 1000) {
          // 24시간 이내 저장 데이터만 복원
          onChange(data.content);
        }
      } catch (error) {
        console.error('Auto-save restore error:', error);
      }
    }

    return () => {
      if (autoSaveTimeoutRef.current) {
        clearTimeout(autoSaveTimeoutRef.current);
      }
    };
  }, []);

  // 내용 변경 시 자동 저장
  useEffect(() => {
    if (!enableAutoSave || !autoSaveKey || disabled || !value) return;

    if (autoSaveTimeoutRef.current) {
      clearTimeout(autoSaveTimeoutRef.current);
    }

    autoSaveTimeoutRef.current = setTimeout(() => {
      try {
        const data = {
          content: value,
          timestamp: Date.now(),
        };
        localStorage.setItem(`autosave_${autoSaveKey}`, JSON.stringify(data));
      } catch (error) {
        console.error('Auto-save error:', error);
      }
    }, 2000); // 2초 후 자동 저장
  }, [value, autoSaveKey, enableAutoSave, disabled]);

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

  // 마크다운 템플릿
  const templates = [
    {
      name: '테이블',
      icon: '📊',
      content: `| 헤더1 | 헤더2 | 헤더3 |
|-------|-------|-------|
| 내용1 | 내용2 | 내용3 |
| 내용4 | 내용5 | 내용6 |`,
    },
    {
      name: '체크리스트',
      icon: '☑️',
      content: `- [ ] 할 일 1
- [ ] 할 일 2
- [x] 완료된 할 일`,
    },
    {
      name: '코드 블록',
      icon: '💻',
      content: '```javascript\nfunction example() {\n  console.log("Hello, World!");\n}\n```',
    },
    {
      name: '접기/펼치기',
      icon: '📁',
      content: `<details>
<summary>클릭하여 펼치기</summary>

여기에 숨길 내용을 작성하세요.

</details>`,
    },
  ];

  // 템플릿 삽입
  const insertTemplate = (template: string) => {
    const textarea = textareaRef.current;
    if (!textarea) return;

    const start = textarea.selectionStart;
    const end = textarea.selectionEnd;
    const newText = value.substring(0, start) + '\n' + template + '\n' + value.substring(end);

    onChange(newText);
    setShowTemplates(false);

    setTimeout(() => {
      textarea.focus();
      textarea.selectionStart = textarea.selectionEnd = start + template.length + 2;
    }, 0);
  };

  // 이미지 업로드 핸들러
  const handleImageUpload = async (files: File[]) => {
    if (!enableImageUpload || files.length === 0) return;

    setIsUploading(true);

    try {
      for (const file of files) {
        const validation = validateImageFile(file);
        if (!validation.valid) {
          alert(validation.error);
          continue;
        }

        const uploaded = await uploadImage(file);
        const markdown = `![${file.name}](${uploaded.url})`;

        const textarea = textareaRef.current;
        if (textarea) {
          const start = textarea.selectionStart;
          const newText = value.substring(0, start) + '\n' + markdown + '\n' + value.substring(start);
          onChange(newText);
        }
      }
    } catch (error) {
      console.error('Image upload error:', error);
      alert('이미지 업로드에 실패했습니다.');
    } finally {
      setIsUploading(false);
    }
  };

  // 파일 선택 핸들러
  const handleFileSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
    const files = Array.from(e.target.files || []);
    handleImageUpload(files);
    // Reset input
    if (fileInputRef.current) {
      fileInputRef.current.value = '';
    }
  };

  // 드래그앤드롭 핸들러
  const handleDragEnter = (e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    setIsDragging(true);
  };

  const handleDragLeave = (e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    setIsDragging(false);
  };

  const handleDragOver = (e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
  };

  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    setIsDragging(false);

    if (!enableImageUpload) return;

    const files = extractImageFromDrop(e.nativeEvent);
    handleImageUpload(files);
  };

  // 클립보드 붙여넣기 핸들러
  const handlePaste = async (e: React.ClipboardEvent) => {
    if (!enableImageUpload) return;

    const file = extractImageFromClipboard(e.nativeEvent);
    if (file) {
      e.preventDefault();
      handleImageUpload([file]);
    }
  };

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
          {enableImageUpload && (
            <button
              type="button"
              onClick={() => fileInputRef.current?.click()}
              className="p-2 hover:bg-gray-200 rounded"
              title="이미지 업로드"
              disabled={disabled || isUploading}
            >
              {isUploading ? '⏳' : '🖼️'}
            </button>
          )}
        </div>

        <div className="flex gap-1 border-r border-gray-300 pr-2">
          <button
            type="button"
            onClick={handleHr}
            className="p-2 hover:bg-gray-200 rounded"
            title="구분선"
            disabled={disabled}
          >
            ─
          </button>
          {enableTemplates && (
            <button
              type="button"
              onClick={() => setShowTemplates(!showTemplates)}
              className="p-2 hover:bg-gray-200 rounded"
              title="템플릿"
              disabled={disabled}
            >
              📝
            </button>
          )}
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

      {/* 템플릿 팝업 */}
      {showTemplates && (
        <div className="relative z-10 mb-2">
          <div className="absolute top-0 left-0 right-0 bg-white border border-gray-300 rounded-lg shadow-lg p-4">
            <div className="flex justify-between items-center mb-3">
              <h4 className="font-medium text-gray-900">템플릿 선택</h4>
              <button
                type="button"
                onClick={() => setShowTemplates(false)}
                className="text-gray-400 hover:text-gray-600"
              >
                ✕
              </button>
            </div>
            <div className="grid grid-cols-2 gap-2">
              {templates.map((template) => (
                <button
                  key={template.name}
                  type="button"
                  onClick={() => insertTemplate(template.content)}
                  className="flex items-center gap-2 p-3 border border-gray-200 rounded hover:bg-gray-50 hover:border-blue-500 transition-colors text-left"
                  disabled={disabled}
                >
                  <span className="text-2xl">{template.icon}</span>
                  <span className="text-sm font-medium text-gray-700">{template.name}</span>
                </button>
              ))}
            </div>
          </div>
        </div>
      )}

      {/* 숨겨진 파일 입력 */}
      <input
        ref={fileInputRef}
        type="file"
        accept="image/*"
        multiple
        onChange={handleFileSelect}
        className="hidden"
      />

      {/* 에디터 영역 */}
      <div
        className={`markdown-content border border-t-0 border-gray-300 rounded-b-lg overflow-hidden relative ${
          isDragging ? 'border-blue-500 bg-blue-50' : ''
        }`}
        onDragEnter={handleDragEnter}
        onDragLeave={handleDragLeave}
        onDragOver={handleDragOver}
        onDrop={handleDrop}
      >
        {/* 드래그 오버레이 */}
        {isDragging && (
          <div className="absolute inset-0 bg-blue-500 bg-opacity-10 flex items-center justify-center z-10 pointer-events-none">
            <div className="bg-white rounded-lg p-4 shadow-lg">
              <p className="text-blue-600 font-medium">이미지를 여기에 드롭하세요</p>
            </div>
          </div>
        )}

        <div className={`flex ${previewMode === 'split' ? 'divide-x divide-gray-300' : ''}`}>
          {/* 편집 영역 */}
          {(previewMode === 'edit' || previewMode === 'split') && (
            <div className={previewMode === 'split' ? 'w-1/2' : 'w-full'}>
              <textarea
                ref={textareaRef}
                className="markdown-textarea w-full p-4 font-mono text-sm resize-none focus:outline-none focus:ring-2 focus:ring-blue-500 bg-white text-gray-900"
                style={{ height: `${height}px` }}
                value={value}
                onChange={(e) => onChange(e.target.value)}
                onKeyDown={handleKeyDown}
                onPaste={handlePaste}
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
                  components={{
                    code({ node, inline, className, children, ...props }: any) {
                      const match = /language-(\w+)/.exec(className || '');
                      return !inline && match ? (
                        <SyntaxHighlighter
                          style={vscDarkPlus}
                          language={match[1]}
                          PreTag="div"
                          {...props}
                        >
                          {String(children).replace(/\n$/, '')}
                        </SyntaxHighlighter>
                      ) : (
                        <code className={className} {...props}>
                          {children}
                        </code>
                      );
                    },
                  }}
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

      {/* 마크다운 가이드 & 상태 */}
      <div className="mt-2 flex items-center justify-between text-xs text-gray-500">
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
            <p><code>```언어\n코드\n```</code> - 코드 블록 (문법 강조 지원)</p>
          </div>
        </details>

        <div className="flex items-center gap-3">
          {enableImageUpload && (
            <span className="text-gray-400">💡 이미지를 드래그하거나 붙여넣기하세요</span>
          )}
          {enableAutoSave && autoSaveKey && (
            <span className="text-green-600">✓ 자동 저장 활성화</span>
          )}
        </div>
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
