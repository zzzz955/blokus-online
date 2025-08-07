import Link from 'next/link';
import { Github, Mail, MessageCircle } from 'lucide-react';

export default function Footer() {
  const currentYear = new Date().getFullYear();

  return (
    <footer className="bg-dark-bg border-t border-dark-border">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-12">
        <div className="grid grid-cols-1 md:grid-cols-4 gap-8">
          {/* 회사 정보 */}
          <div className="col-span-1 md:col-span-2">
            <div className="flex items-center space-x-2 mb-4">
              <div className="w-8 h-8 bg-gradient-to-br from-primary-500 to-secondary-500 rounded-lg"></div>
              <span className="font-sans text-xl text-white">Blokus-online</span>
            </div>
            <p className="text-gray-400 text-sm mb-4">
              전략적 사고와 창의성이 만나는 온라인 블로커스 게임입니다. 
              친구들과 함께 즐기는 멀티플레이어 보드게임의 새로운 경험을 제공합니다.
            </p>
            <div className="flex space-x-4">
              <a
                href="https://github.com/zzzz955/blokus-online"
                className="text-gray-400 hover:text-white transition-colors"
                aria-label="GitHub"
              >
                <Github size={20} />
              </a>
              <a
                href="mailto:zzzzz955@gmail.com"
                className="text-gray-400 hover:text-white transition-colors"
                aria-label="이메일"
              >
                <Mail size={20} />
              </a>
              <a
                href="/support"
                className="text-gray-400 hover:text-white transition-colors"
                aria-label="고객지원"
              >
                <MessageCircle size={20} />
              </a>
            </div>
          </div>

          {/* 빠른 링크 */}
          <div>
            <h3 className="text-white font-semibold mb-4">빠른 링크</h3>
            <ul className="space-y-2">
              <li>
                <Link href="/guide" className="text-gray-400 hover:text-white text-sm transition-colors">
                  게임 가이드
                </Link>
              </li>
              <li>
                <Link href="/announcements" className="text-gray-400 hover:text-white text-sm transition-colors">
                  공지사항
                </Link>
              </li>
              <li>
                <Link href="/patch-notes" className="text-gray-400 hover:text-white text-sm transition-colors">
                  패치 노트
                </Link>
              </li>
              <li>
                <Link href="/support" className="text-gray-400 hover:text-white text-sm transition-colors">
                  고객지원
                </Link>
              </li>
            </ul>
          </div>

          {/* 다운로드 */}
          <div>
            <h3 className="text-white font-semibold mb-4">다운로드</h3>
            <ul className="space-y-2">
              <li>
                <Link href="/download" className="text-gray-400 hover:text-white text-sm transition-colors">
                  Windows 버전
                </Link>
              </li>
              <li>
                <a href="#" className="text-gray-400 hover:text-white text-sm transition-colors">
                  시스템 요구사항
                </a>
              </li>
              <li>
                <a href="#" className="text-gray-400 hover:text-white text-sm transition-colors">
                  설치 가이드
                </a>
              </li>
              <li>
                <a href="#" className="text-gray-400 hover:text-white text-sm transition-colors">
                  문제 해결
                </a>
              </li>
            </ul>
          </div>
        </div>

        <div className="border-t border-dark-border mt-8 pt-8">
          <div className="flex flex-col md:flex-row justify-between items-center">
            <p className="text-gray-400 text-sm">
              © {currentYear} 블로커스 온라인. All rights reserved.
            </p>
            <div className="flex space-x-6 mt-4 md:mt-0">
              <a href="#" className="text-gray-400 hover:text-white text-sm transition-colors">
                개인정보처리방침
              </a>
              <a href="#" className="text-gray-400 hover:text-white text-sm transition-colors">
                이용약관
              </a>
              <Link href="/admin/login" className="text-gray-400 hover:text-white text-sm transition-colors">
                관리자
              </Link>
            </div>
          </div>
        </div>
      </div>
    </footer>
  );
}