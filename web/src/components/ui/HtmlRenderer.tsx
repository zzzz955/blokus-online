'use client';

import { useMemo } from 'react';
import DOMPurify from 'isomorphic-dompurify';

interface HtmlRendererProps {
  content: string;
  className?: string;
}

export default function HtmlRenderer({ content, className = '' }: HtmlRendererProps) {
  // HTML 콘텐츠를 안전하게 정제
  const sanitizedContent = useMemo(() => {
    if (!content) return '';
    
    // DOMPurify로 XSS 공격 방지
    const cleanHtml = DOMPurify.sanitize(content, {
      ALLOWED_TAGS: [
        'p', 'br', 'strong', 'b', 'em', 'i', 'u', 's', 'strike',
        'h1', 'h2', 'h3', 'h4', 'h5', 'h6',
        'ul', 'ol', 'li',
        'blockquote', 'pre', 'code',
        'a', 'img',
        'span', 'div'
      ],
      ALLOWED_ATTR: [
        'href', 'target', 'rel',
        'src', 'alt', 'title',
        'style', 'class'
      ],
      ALLOWED_URI_REGEXP: /^(?:(?:(?:f|ht)tps?|mailto|tel|callto|cid|xmpp):|[^a-z]|[a-z+.\-]+(?:[^a-z+.\-:]|$))/i
    });
    
    return cleanHtml;
  }, [content]);

  return (
    <div 
      className={`prose prose-slate max-w-none rich-content ${className}`}
      dangerouslySetInnerHTML={{ __html: sanitizedContent }}
      style={{
        // HTML 렌더링 시 기본 스타일 설정
        color: '#f9fafb', // 기본 흰색 텍스트
        lineHeight: '1.6'
      }}
    />
  );
}