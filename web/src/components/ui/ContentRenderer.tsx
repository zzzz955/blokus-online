'use client';

import React from 'react';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import { Prism as SyntaxHighlighter } from 'react-syntax-highlighter';
import { vscDarkPlus } from 'react-syntax-highlighter/dist/esm/styles/prism';
import { detectContentFormat, type ContentFormat } from '@/lib/utils/content-format';

interface ContentRendererProps {
  content: string;
  className?: string;
}

/**
 * ContentRenderer Component
 *
 * 콘텐츠 형식을 자동으로 감지하여 적절한 방식으로 렌더링합니다.
 * - HTML: Quill 에디터 출력 → dangerouslySetInnerHTML
 * - Markdown: 마크다운 형식 → ReactMarkdown with syntax highlighting
 * - Plain: 일반 텍스트 → 줄바꿈 처리
 */
export default function ContentRenderer({ content, className = '' }: ContentRendererProps) {
  if (!content || content.trim() === '') {
    return null;
  }

  const format: ContentFormat = detectContentFormat(content);

  // HTML 콘텐츠 렌더링 (Quill 에디터 출력)
  if (format === 'html') {
    return (
      <div
        className={`prose prose-slate dark:prose-invert max-w-none ${className}`}
        dangerouslySetInnerHTML={{ __html: content }}
      />
    );
  }

  // Markdown 콘텐츠 렌더링
  if (format === 'markdown') {
    return (
      <div className={`markdown-content prose prose-slate dark:prose-invert max-w-none ${className}`}>
        <ReactMarkdown
          remarkPlugins={[remarkGfm]}
          components={{
            code({ node, inline, className, children, ...props }: any) {
              const match = /language-(\w+)/.exec(className || '');
              const language = match ? match[1] : '';

              return !inline && language ? (
                <SyntaxHighlighter
                  style={vscDarkPlus}
                  language={language}
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
            // 링크는 새 탭에서 열기
            a({ node, children, href, ...props }: any) {
              return (
                <a
                  href={href}
                  target="_blank"
                  rel="noopener noreferrer"
                  {...props}
                >
                  {children}
                </a>
              );
            },
            // 이미지는 최대 너비 제한
            img({ node, src, alt, ...props }: any) {
              return (
                <img
                  src={src}
                  alt={alt}
                  className="max-w-full h-auto rounded-lg"
                  loading="lazy"
                  {...props}
                />
              );
            },
          }}
        >
          {content}
        </ReactMarkdown>

        <style jsx global>{`
          .markdown-content {
            color: #1f2937;
          }

          .markdown-content h1,
          .markdown-content h2,
          .markdown-content h3,
          .markdown-content h4,
          .markdown-content h5,
          .markdown-content h6 {
            color: #111827;
            font-weight: 600;
            margin-top: 1.5em;
            margin-bottom: 0.5em;
          }

          .markdown-content h1 {
            font-size: 2em;
            border-bottom: 2px solid #e5e7eb;
            padding-bottom: 0.3em;
          }

          .markdown-content h2 {
            font-size: 1.5em;
            border-bottom: 1px solid #e5e7eb;
            padding-bottom: 0.3em;
          }

          .markdown-content h3 {
            font-size: 1.25em;
          }

          .markdown-content p,
          .markdown-content ul,
          .markdown-content ol,
          .markdown-content li {
            color: #374151;
            line-height: 1.7;
          }

          .markdown-content p {
            margin-bottom: 1em;
          }

          .markdown-content ul,
          .markdown-content ol {
            margin-left: 1.5em;
            margin-bottom: 1em;
          }

          .markdown-content li {
            margin-bottom: 0.5em;
          }

          .markdown-content code {
            background-color: #f3f4f6;
            color: #dc2626;
            padding: 0.2em 0.4em;
            border-radius: 3px;
            font-size: 0.875em;
            font-family: 'Courier New', Courier, monospace;
          }

          .markdown-content pre {
            background-color: #1e293b;
            border-radius: 8px;
            padding: 1em;
            overflow-x: auto;
            margin-bottom: 1em;
          }

          .markdown-content pre code {
            background-color: transparent;
            color: inherit;
            padding: 0;
            font-size: 0.875em;
          }

          .markdown-content blockquote {
            border-left: 4px solid #3b82f6;
            background-color: #f0f9ff;
            padding: 0.5em 1em;
            margin: 1em 0;
            color: #1e40af;
          }

          .markdown-content table {
            border-collapse: collapse;
            width: 100%;
            margin-bottom: 1em;
          }

          .markdown-content th,
          .markdown-content td {
            border: 1px solid #e5e7eb;
            padding: 0.5em 1em;
            text-align: left;
          }

          .markdown-content th {
            background-color: #f9fafb;
            font-weight: 600;
            color: #111827;
          }

          .markdown-content a {
            color: #3b82f6;
            text-decoration: underline;
          }

          .markdown-content a:hover {
            color: #2563eb;
          }

          .markdown-content hr {
            border: none;
            border-top: 2px solid #e5e7eb;
            margin: 2em 0;
          }

          .markdown-content strong {
            font-weight: 600;
            color: #111827;
          }

          .markdown-content em {
            font-style: italic;
          }

          .markdown-content del {
            text-decoration: line-through;
            color: #6b7280;
          }

          /* 체크박스 스타일 */
          .markdown-content input[type='checkbox'] {
            margin-right: 0.5em;
          }

          /* 코드 블록 스크롤바 스타일 */
          .markdown-content pre::-webkit-scrollbar {
            height: 8px;
          }

          .markdown-content pre::-webkit-scrollbar-track {
            background: #334155;
            border-radius: 4px;
          }

          .markdown-content pre::-webkit-scrollbar-thumb {
            background: #64748b;
            border-radius: 4px;
          }

          .markdown-content pre::-webkit-scrollbar-thumb:hover {
            background: #94a3b8;
          }
        `}</style>
      </div>
    );
  }

  // Plain 텍스트 렌더링
  return (
    <div className={`whitespace-pre-wrap text-gray-700 dark:text-gray-300 ${className}`}>
      {content}
    </div>
  );
}
