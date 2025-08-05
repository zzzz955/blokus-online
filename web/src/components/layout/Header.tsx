'use client';

import Link from 'next/link';
import { useState } from 'react';
import { Menu, X, Download, User, LogIn } from 'lucide-react';
import { useSession, signOut } from 'next-auth/react';

export default function Header() {
  const [isMenuOpen, setIsMenuOpen] = useState(false);
  const { data: session, status } = useSession();

  const toggleMenu = () => setIsMenuOpen(!isMenuOpen);
  
  const handleSignOut = async () => {
    await signOut({ callbackUrl: '/' });
  };

  const navigation = [
    { name: '홈', href: '/' },
    { name: '게임 가이드', href: '/guide' },
    { name: '공지사항', href: '/announcements' },
    { name: '패치 노트', href: '/patch-notes' },
    { name: '게임 통계', href: '/stats' },
    { name: session?.user ? '내 문의' : '고객지원', href: session?.user ? '/support' : '/contact' },
  ];

  return (
    <header className="bg-dark-bg/95 backdrop-blur-md border-b border-dark-border sticky top-0 z-50">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="flex justify-between items-center h-16">
          {/* 로고 */}
          <div className="flex-shrink-0">
            <Link href="/" className="flex items-center space-x-2">
              <div className="w-8 h-8 bg-gradient-to-br from-primary-500 to-secondary-500 rounded-lg"></div>
              <span className="font-sans text-xl text-white">Blokus-online</span>
            </Link>
          </div>

          {/* 데스크톱 네비게이션 */}
          <nav className="hidden md:flex space-x-8">
            {navigation.map((item) => (
              <Link
                key={item.name}
                href={item.href}
                className="text-gray-300 hover:text-white px-3 py-2 text-sm font-medium transition-colors duration-200"
              >
                {item.name}
              </Link>
            ))}
          </nav>

          {/* 로그인/사용자 영역 */}
          <div className="hidden md:flex items-center space-x-4">
            {status === 'loading' ? (
              <div className="w-8 h-8 animate-pulse bg-gray-600 rounded-full"></div>
            ) : session?.user ? (
              <div className="flex items-center space-x-3">
                <div className="flex items-center space-x-2 text-white">
                  <User size={18} />
                  <span className="text-sm">
                    {session.user.name || session.user.email}
                  </span>
                </div>
                <button
                  onClick={handleSignOut}
                  className="text-gray-300 hover:text-white text-sm px-3 py-1 rounded transition-colors"
                >
                  로그아웃
                </button>
              </div>
            ) : (
              <div className="flex items-center space-x-3">
                <Link
                  href="/auth/signin"
                  className="text-gray-300 hover:text-white flex items-center space-x-1 px-3 py-2 text-sm font-medium transition-colors duration-200"
                >
                  <LogIn size={16} />
                  <span>회원가입/로그인</span>
                </Link>
                <Link
                  href="/auth/reset-password"
                  className="text-gray-400 hover:text-gray-300 text-xs px-2 py-1 transition-colors"
                >
                  비밀번호 재설정
                </Link>
              </div>
            )}
            
            <Link
              href="/download"
              className="btn-primary flex items-center space-x-2"
            >
              <Download size={18} />
              <span>게임 다운로드</span>
            </Link>
          </div>

          {/* 모바일 메뉴 버튼 */}
          <div className="md:hidden">
            <button
              onClick={toggleMenu}
              className="text-gray-300 hover:text-white p-2"
            >
              {isMenuOpen ? <X size={24} /> : <Menu size={24} />}
            </button>
          </div>
        </div>

        {/* 모바일 메뉴 */}
        {isMenuOpen && (
          <div className="md:hidden">
            <div className="px-2 pt-2 pb-3 space-y-1 bg-dark-card rounded-lg mt-2">
              {navigation.map((item) => (
                <Link
                  key={item.name}
                  href={item.href}
                  className="text-gray-300 hover:text-white block px-3 py-2 text-base font-medium transition-colors duration-200"
                  onClick={() => setIsMenuOpen(false)}
                >
                  {item.name}
                </Link>
              ))}
              
              {/* 모바일 로그인/사용자 영역 */}
              <div className="pt-2 border-t border-gray-600">
                {session?.user ? (
                  <div className="space-y-2">
                    <div className="flex items-center space-x-2 text-white px-3 py-2">
                      <User size={16} />
                      <span className="text-sm">
                        {session.user.name || session.user.email}
                      </span>
                    </div>
                    <button
                      onClick={() => {
                        setIsMenuOpen(false);
                        handleSignOut();
                      }}
                      className="text-gray-300 hover:text-white block px-3 py-2 text-base font-medium transition-colors duration-200 w-full text-left"
                    >
                      로그아웃
                    </button>
                  </div>
                ) : (
                  <div className="space-y-2">
                    <Link
                      href="/auth/signin"
                      className="text-gray-300 hover:text-white flex items-center space-x-2 px-3 py-2 text-base font-medium transition-colors duration-200"
                      onClick={() => setIsMenuOpen(false)}
                    >
                      <LogIn size={16} />
                      <span>회원가입/로그인</span>
                    </Link>
                    <Link
                      href="/auth/reset-password"
                      className="text-gray-400 hover:text-gray-300 block px-3 py-1 text-sm transition-colors"
                      onClick={() => setIsMenuOpen(false)}
                    >
                      비밀번호 재설정
                    </Link>
                  </div>
                )}
              </div>
              
              <div className="pt-2">
                <Link
                  href="/download"
                  className="btn-primary flex items-center justify-center space-x-2 w-full"
                  onClick={() => setIsMenuOpen(false)}
                >
                  <Download size={18} />
                  <span>게임 다운로드</span>
                </Link>
              </div>
            </div>
          </div>
        )}
      </div>
    </header>
  );
}