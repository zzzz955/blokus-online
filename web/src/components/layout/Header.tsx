'use client';

import Link from 'next/link';
import { useState, useEffect } from 'react';
import { Menu, X, Download, User, LogIn, Settings } from 'lucide-react';
import { useSession, signOut } from 'next-auth/react';
import { usePathname, useRouter } from 'next/navigation';

export default function Header() {
  const [isMenuOpen, setIsMenuOpen] = useState(false);
  const [admin, setAdmin] = useState<any>(null);
  const { data: session, status } = useSession();
  const pathname = usePathname();
  const router = useRouter();

  // 관리자 모드 감지
  const isAdminMode = pathname?.startsWith('/admin');

  useEffect(() => {
    if (isAdminMode) {
      // 관리자 정보 가져오기
      const adminData = localStorage.getItem('admin');
      if (adminData) {
        try {
          setAdmin(JSON.parse(adminData));
        } catch (error) {
          console.error('관리자 정보 파싱 오류:', error);
        }
      }
    }
  }, [isAdminMode]);

  const toggleMenu = () => setIsMenuOpen(!isMenuOpen);
  
  const handleSignOut = async () => {
    await signOut({ callbackUrl: '/' });
  };

  const handleAdminLogout = async () => {
    try {
      await fetch('/api/admin/auth', {
        method: 'DELETE'
      });
      localStorage.removeItem('admin');
      router.push('/admin/login');
    } catch (error) {
      console.error('로그아웃 오류:', error);
      localStorage.removeItem('admin');
      router.push('/admin/login');
    }
  };

  // 일반 사용자 네비게이션
  const userNavigation = [
    { name: '홈', href: '/' },
    { name: '게임 가이드', href: '/guide' },
    { name: '자유 게시판', href: '/posts' },
    { name: '공지사항', href: '/announcements' },
    { name: '패치 노트', href: '/patch-notes' },
    { name: session?.user ? '내 문의' : '고객지원', href: session?.user ? '/support' : '/contact' },
  ];

  // 관리자 네비게이션
  const adminNavigation = [
    { name: '대시보드', href: '/admin/dashboard' },
    { name: '공지', href: '/admin/announcements' },
    { name: '패치', href: '/admin/patch-notes' },
    { name: '스테이지', href: '/admin/stages' },
    { name: '후기', href: '/admin/testimonials' },
    { name: '게시글', href: '/admin/posts' },
    { name: '통계', href: '/admin/stats' },
    { name: '배치', href: '/admin/batch' },
  ];

  const navigation = isAdminMode ? adminNavigation : userNavigation;

  return (
    <header className="bg-dark-bg/95 backdrop-blur-md border-b border-dark-border sticky top-0 z-50">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="flex justify-between items-center h-16">
          {/* 로고 */}
          <div className="flex-shrink-0">
            <Link href={isAdminMode ? '/admin/dashboard' : '/'} className="flex items-center space-x-2">
              <div className="w-8 h-8 bg-gradient-to-br from-primary-500 to-secondary-500 rounded-lg flex items-center justify-center">
                {isAdminMode && <Settings size={16} className="text-white" />}
              </div>
              <span className="font-sans text-xl text-white">
                {isAdminMode ? 'Blokus 관리자' : 'Blokus-online'}
              </span>
            </Link>
          </div>

          {/* 데스크톱 네비게이션 */}
          <nav className="hidden md:flex space-x-4">
            {navigation.map((item) => (
              <Link
                key={item.name}
                href={item.href}
                className={`px-2 py-2 text-sm font-medium transition-colors duration-200 ${
                  pathname === item.href
                    ? 'text-primary-400 border-b-2 border-primary-400'
                    : 'text-gray-300 hover:text-white'
                }`}
              >
                {item.name}
              </Link>
            ))}
          </nav>

          {/* 로그인/사용자 영역 */}
          <div className="hidden md:flex items-center space-x-3">
            {isAdminMode ? (
              // 관리자 모드
              <div className="flex items-center space-x-2">
                <div className="flex items-center space-x-1 text-white">
                  <Settings size={18} />
                  <span className="text-sm">
                    {admin?.username}
                  </span>
                </div>
                <Link
                  href="/"
                  className="text-gray-300 hover:text-white text-sm px-3 py-1 rounded transition-colors"
                >
                  사용자 사이트
                </Link>
                <button
                  onClick={handleAdminLogout}
                  className="text-gray-300 hover:text-white text-sm px-3 py-1 rounded transition-colors"
                >
                  로그아웃
                </button>
              </div>
            ) : (
              // 일반 사용자 모드
              <>
                {status === 'loading' ? (
                  <div className="w-8 h-8 animate-pulse bg-gray-600 rounded-full"></div>
                ) : session?.user ? (
                  <div className="flex items-center space-x-3">
                    <Link
                      href="/profile"
                      className="flex items-center space-x-2 text-white hover:text-primary-400 transition-colors"
                    >
                      <User size={18} />
                      <span className="text-sm">
                        {session.user.name || session.user.email}
                      </span>
                    </Link>
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
                  </div>
                )}
                
                <Link
                  href="/download"
                  className="btn-primary flex items-center space-x-2"
                >
                  <Download size={18} />
                  <span>게임 다운로드</span>
                </Link>
              </>
            )}
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
                {isAdminMode ? (
                  // 관리자 모드
                  <div className="space-y-2">
                    <div className="flex items-center space-x-2 text-white px-3 py-2">
                      <Settings size={16} />
                      <span className="text-sm">
                        {admin?.username} ({admin?.role})
                      </span>
                    </div>
                    <Link
                      href="/"
                      className="text-gray-300 hover:text-white block px-3 py-2 text-base font-medium transition-colors duration-200"
                      onClick={() => setIsMenuOpen(false)}
                    >
                      사용자 사이트
                    </Link>
                    <button
                      onClick={() => {
                        setIsMenuOpen(false);
                        handleAdminLogout();
                      }}
                      className="text-gray-300 hover:text-white block px-3 py-2 text-base font-medium transition-colors duration-200 w-full text-left"
                    >
                      로그아웃
                    </button>
                  </div>
                ) : (
                  // 일반 사용자 모드
                  <>
                    {session?.user ? (
                      <div className="space-y-2">
                        <Link
                          href="/profile"
                          className="flex items-center space-x-2 text-white hover:text-primary-400 px-3 py-2 transition-colors"
                          onClick={() => setIsMenuOpen(false)}
                        >
                          <User size={16} />
                          <span className="text-sm">
                            {session.user.name || session.user.email}
                          </span>
                        </Link>
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
                      </div>
                    )}
                  </>
                )}
              </div>
              
              {!isAdminMode && (
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
              )}
            </div>
          </div>
        )}
      </div>
    </header>
  );
}