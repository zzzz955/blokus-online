import type { Metadata } from 'next';
import { Inter } from 'next/font/google';
import './globals.css';
import { Providers } from './providers';

// Environment variable validation for production (server-side only)
if (typeof window === 'undefined' && process.env.NODE_ENV === 'production') {
  const requiredVars = ['NEXTAUTH_SECRET', 'JWT_SECRET', 'DATABASE_URL'];
  const missing = requiredVars.filter(varName => !process.env[varName]);
  
  if (missing.length > 0) {
    throw new Error(`Missing required environment variables: ${missing.join(', ')}`);
  }
}

const inter = Inter({ subsets: ['latin'] });

export const metadata: Metadata = {
  title: '블로커스 온라인 - 공식 웹사이트',
  description: '블로커스 온라인 게임의 공식 웹사이트입니다. 최신 클라이언트 다운로드, 게임 가이드, 공지사항을 확인하세요.',
  keywords: '블로커스, 온라인게임, 보드게임, 전략게임',
  openGraph: {
    title: '블로커스 온라인 - 공식 웹사이트',
    description: '블로커스 온라인 게임의 공식 웹사이트입니다.',
    type: 'website',
    locale: 'ko_KR',
  },
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="ko">
      <body className={inter.className}>
        <Providers>
          {children}
        </Providers>
      </body>
    </html>
  );
}