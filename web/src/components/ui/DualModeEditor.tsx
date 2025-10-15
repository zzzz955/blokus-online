'use client';

import { useState, useEffect } from 'react';
import dynamic from 'next/dynamic';
import MarkdownEditor from './MarkdownEditor';
import { htmlToMarkdown, markdownToHtml } from '@/lib/utils/format-converter';

// RichTextEditor 동적 import (SSR 방지)
const RichTextEditor = dynamic(() => import('./RichTextEditor'), {
  ssr: false,
  loading: () => <p>에디터를 로드 중...</p>,
});

export type EditorMode = 'rich' | 'markdown';

interface DualModeEditorProps {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  height?: number;
  className?: string;
  disabled?: boolean;
  defaultMode?: EditorMode;
  showModeSwitch?: boolean;
}

export default function DualModeEditor({
  value,
  onChange,
  placeholder = '내용을 입력해주세요...',
  height = 300,
  className = '',
  disabled = false,
  defaultMode = 'rich',
  showModeSwitch = true,
}: DualModeEditorProps) {
  const [mode, setMode] = useState<EditorMode>(defaultMode);
  const [richContent, setRichContent] = useState('');
  const [markdownContent, setMarkdownContent] = useState('');
  const [isConverting, setIsConverting] = useState(false);

  // 초기 값 설정
  useEffect(() => {
    if (value) {
      // HTML 형식이면 리치텍스트 기본값으로
      if (value.includes('<') && value.includes('>')) {
        setRichContent(value);
        setMarkdownContent(htmlToMarkdown(value));
      } else {
        // 마크다운 형식이면 마크다운 기본값으로
        setMarkdownContent(value);
        setRichContent(markdownToHtml(value));
      }
    }
  }, []);

  // 모드 전환 핸들러
  const handleModeChange = (newMode: EditorMode) => {
    if (isConverting || disabled) return;

    setIsConverting(true);

    try {
      if (newMode === 'markdown' && mode === 'rich') {
        // 리치텍스트 → 마크다운 변환
        const converted = htmlToMarkdown(richContent);
        setMarkdownContent(converted);
        onChange(converted);
      } else if (newMode === 'rich' && mode === 'markdown') {
        // 마크다운 → 리치텍스트 변환
        const converted = markdownToHtml(markdownContent);
        setRichContent(converted);
        onChange(converted);
      }

      setMode(newMode);
    } catch (error) {
      console.error('Mode conversion error:', error);
      alert('형식 변환 중 오류가 발생했습니다.');
    } finally {
      setIsConverting(false);
    }
  };

  // 리치텍스트 변경 핸들러
  const handleRichChange = (content: string) => {
    setRichContent(content);
    onChange(content);
  };

  // 마크다운 변경 핸들러
  const handleMarkdownChange = (content: string) => {
    setMarkdownContent(content);
    onChange(content);
  };

  return (
    <div className={`dual-mode-editor ${className}`}>
      {/* 모드 전환 탭 */}
      {showModeSwitch && (
        <div className="mode-switch-tabs flex border-b border-gray-300 bg-gray-50 rounded-t-lg">
          <button
            type="button"
            onClick={() => handleModeChange('rich')}
            disabled={disabled || isConverting}
            className={`flex-1 px-4 py-3 text-sm font-medium transition-colors ${
              mode === 'rich'
                ? 'bg-white border-b-2 border-blue-600 text-blue-600'
                : 'text-gray-600 hover:text-gray-900 hover:bg-gray-100'
            } ${disabled || isConverting ? 'opacity-50 cursor-not-allowed' : 'cursor-pointer'}`}
          >
            <span className="flex items-center justify-center gap-2">
              <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
              </svg>
              리치텍스트 에디터
            </span>
          </button>
          <button
            type="button"
            onClick={() => handleModeChange('markdown')}
            disabled={disabled || isConverting}
            className={`flex-1 px-4 py-3 text-sm font-medium transition-colors ${
              mode === 'markdown'
                ? 'bg-white border-b-2 border-blue-600 text-blue-600'
                : 'text-gray-600 hover:text-gray-900 hover:bg-gray-100'
            } ${disabled || isConverting ? 'opacity-50 cursor-not-allowed' : 'cursor-pointer'}`}
          >
            <span className="flex items-center justify-center gap-2">
              <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M10 20l4-16m4 4l4 4-4 4M6 16l-4-4 4-4" />
              </svg>
              마크다운 에디터
            </span>
          </button>
        </div>
      )}

      {/* 변환 중 오버레이 */}
      {isConverting && (
        <div className="conversion-overlay absolute inset-0 bg-white bg-opacity-75 flex items-center justify-center z-10 rounded-lg">
          <div className="text-center">
            <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600 mx-auto mb-2"></div>
            <p className="text-sm text-gray-600">형식 변환 중...</p>
          </div>
        </div>
      )}

      {/* 에디터 영역 */}
      <div className="editor-content relative">
        {mode === 'rich' ? (
          <RichTextEditor
            value={richContent}
            onChange={handleRichChange}
            placeholder={placeholder}
            height={height}
            disabled={disabled}
          />
        ) : (
          <MarkdownEditor
            value={markdownContent}
            onChange={handleMarkdownChange}
            placeholder={placeholder}
            height={height}
            disabled={disabled}
            showPreview={true}
          />
        )}
      </div>

      {/* 안내 메시지 */}
      <div className="editor-info mt-2 text-xs text-gray-500">
        <div className="flex items-start gap-2">
          <svg className="w-4 h-4 text-blue-500 flex-shrink-0 mt-0.5" fill="currentColor" viewBox="0 0 20 20">
            <path fillRule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7-4a1 1 0 11-2 0 1 1 0 012 0zM9 9a1 1 0 000 2v3a1 1 0 001 1h1a1 1 0 100-2v-3a1 1 0 00-1-1H9z" clipRule="evenodd" />
          </svg>
          <div>
            <p className="font-medium text-gray-700">에디터 모드 전환 안내</p>
            <p className="mt-1">
              {mode === 'rich'
                ? '리치텍스트 에디터: 워드프로세서처럼 직관적인 편집이 가능합니다. 마크다운으로 전환하면 현재 내용이 마크다운 형식으로 변환됩니다.'
                : '마크다운 에디터: 마크다운 문법을 사용하여 빠르게 서식을 지정할 수 있습니다. 리치텍스트로 전환하면 현재 내용이 HTML 형식으로 변환됩니다.'
              }
            </p>
            {mode === 'markdown' && (
              <p className="mt-1 text-gray-600">
                💡 팁: 툴바 버튼을 사용하거나 <kbd className="px-1 py-0.5 bg-gray-200 rounded">Ctrl+B</kbd> (굵게),
                <kbd className="px-1 py-0.5 bg-gray-200 rounded ml-1">Ctrl+I</kbd> (기울임),
                <kbd className="px-1 py-0.5 bg-gray-200 rounded ml-1">Ctrl+K</kbd> (링크) 단축키를 사용할 수 있습니다.
              </p>
            )}
          </div>
        </div>
      </div>

      <style jsx>{`
        .dual-mode-editor {
          position: relative;
        }
        .mode-switch-tabs button {
          position: relative;
          z-index: 1;
        }
        kbd {
          font-family: monospace;
          font-size: 0.875em;
        }
      `}</style>
    </div>
  );
}
