'use client';

import { useEffect, useMemo, useRef } from 'react';
import dynamic from 'next/dynamic';

// ReactQuill을 동적 import로 로드 (SSR 문제 해결)
const ReactQuill = dynamic(() => import('react-quill'), { 
  ssr: false,
  loading: () => <p>에디터를 로드 중...</p>
});

// Quill 스타일 import
import 'react-quill/dist/quill.snow.css';

interface RichTextEditorProps {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  height?: number;
  className?: string;
  disabled?: boolean;
}

export default function RichTextEditor({
  value,
  onChange,
  placeholder = '내용을 입력해주세요...',
  height = 300,
  className = '',
  disabled = false
}: RichTextEditorProps) {
  const quillRef = useRef<any>(null);

  // Quill 에디터 설정
  const modules = useMemo(() => ({
    toolbar: {
      container: [
        [{ 'header': [1, 2, 3, false] }],
        ['bold', 'italic', 'underline', 'strike'],
        [{ 'color': [] }, { 'background': [] }],
        [{ 'list': 'ordered'}, { 'list': 'bullet' }],
        [{ 'indent': '-1'}, { 'indent': '+1' }],
        [{ 'align': [] }],
        ['link', 'image'],
        ['blockquote', 'code-block'],
        ['clean']
      ]
    },
    clipboard: {
      // 붙여넣기 시 스타일 유지
      matchVisual: false,
    }
  }), []);

  const formats = useMemo(() => [
    'header',
    'bold', 'italic', 'underline', 'strike',
    'color', 'background',
    'list', 'bullet', 'indent',
    'align',
    'link', 'image',
    'blockquote', 'code-block'
  ], []);

  // 에디터 높이 설정
  useEffect(() => {
    if (quillRef.current) {
      const editor = quillRef.current.getEditor();
      const toolbar = quillRef.current.getEditingArea();
      if (toolbar) {
        toolbar.style.height = `${height}px`;
      }
    }
  }, [height]);

  return (
    <div className={`rich-text-editor ${className}`}>
      <style jsx global>{`
        .ql-editor {
          min-height: ${height}px;
          font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
          line-height: 1.6;
          color: #111827;
          background-color: #ffffff;
        }
        .ql-toolbar {
          border-top: 1px solid #e5e7eb;
          border-left: 1px solid #e5e7eb;
          border-right: 1px solid #e5e7eb;
          border-bottom: none;
          border-top-left-radius: 0.5rem;
          border-top-right-radius: 0.5rem;
        }
        .ql-container {
          border-bottom: 1px solid #e5e7eb;
          border-left: 1px solid #e5e7eb;
          border-right: 1px solid #e5e7eb;
          border-top: none;
          border-bottom-left-radius: 0.5rem;
          border-bottom-right-radius: 0.5rem;
        }
        .ql-editor.ql-blank::before {
          font-style: normal;
          color: #9ca3af;
        }
        .ql-toolbar .ql-formats {
          margin-right: 8px;
        }
        .ql-toolbar button {
          width: 28px;
          height: 28px;
        }
        .ql-toolbar button:hover {
          color: #3b82f6;
        }
        .ql-toolbar .ql-active {
          color: #3b82f6;
        }
        .dark .ql-toolbar {
          background-color: #374151;
          border-color: #4b5563;
        }
        .dark .ql-container {
          background-color: #1f2937;
          border-color: #4b5563;
        }
        .dark .ql-editor {
          color: #f9fafb;
        }
        .dark .ql-toolbar button {
          color: #d1d5db;
        }
        .dark .ql-toolbar button:hover {
          color: #60a5fa;
        }
        .dark .ql-toolbar .ql-active {
          color: #60a5fa;
        }
      `}</style>
      
      <ReactQuill
        theme="snow"
        value={value}
        onChange={(content) => {
          onChange(content);
          // Store reference for manual access if needed
          if (typeof window !== 'undefined') {
            const editor = document.querySelector('.ql-editor');
            if (editor && quillRef.current !== editor) {
              quillRef.current = { getEditor: () => editor, getEditingArea: () => editor };
            }
          }
        }}
        modules={modules}
        formats={formats}
        placeholder={placeholder}
        readOnly={disabled}
        style={{ 
          background: 'white',
          borderRadius: '0.5rem'
        }}
      />
    </div>
  );
}