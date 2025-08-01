'use client';

import { useState, useEffect } from 'react';
import Layout from '@/components/layout/Layout';

interface ClientInfo {
  available: boolean;
  filename: string;
  size: number;
  lastModified: string;
  downloadUrl: string;
}

interface VersionInfo {
  version: string;
  releaseDate: string;
  downloadUrl: string;
  fileSize: number;
  changelog: string[];
}

export default function DownloadPage() {
  const [clientInfo, setClientInfo] = useState<ClientInfo | null>(null);
  const [versionInfo, setVersionInfo] = useState<VersionInfo | null>(null);
  const [isDownloading, setIsDownloading] = useState(false);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    Promise.all([
      fetch('/api/download/client', { method: 'POST' }).then(res => res.json()),
      fetch('/api/download/version').then(res => res.json())
    ]).then(([clientData, versionData]) => {
      setClientInfo(clientData);
      setVersionInfo(versionData.data);
      setLoading(false);
    }).catch(error => {
      console.error('Failed to fetch download info:', error);
      setLoading(false);
    });
  }, []);

  const handleDownload = () => {
    if (!clientInfo?.available) return;
    
    setIsDownloading(true);
    
    // 다운로드 시작
    window.location.href = '/api/download/client';
    
    // 3초 후 로딩 상태 해제
    setTimeout(() => setIsDownloading(false), 3000);
  };

  const formatFileSize = (bytes: number) => {
    const mb = bytes / (1024 * 1024);
    return `${mb.toFixed(1)} MB`;
  };

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleDateString('ko-KR', {
      year: 'numeric',
      month: 'long',
      day: 'numeric'
    });
  };

  if (loading) {
    return (
      <Layout>
        <div className="min-h-screen bg-gradient-to-br from-blue-900 via-purple-900 to-indigo-900 flex items-center justify-center">
          <div className="text-white text-xl">로딩 중...</div>
        </div>
      </Layout>
    );
  }

  return (
    <Layout>
      <div className="min-h-screen bg-gradient-to-br from-blue-900 via-purple-900 to-indigo-900 py-12 px-4">
        <div className="max-w-4xl mx-auto">
          {/* 헤더 */}
          <div className="text-center mb-12">
            <h1 className="text-4xl md:text-6xl font-bold text-white mb-4">
              게임 다운로드
            </h1>
            <p className="text-xl text-blue-200 max-w-2xl mx-auto">
              블로커스 온라인 클라이언트를 다운로드하고 친구들과 함께 게임을 즐겨보세요!
            </p>
          </div>

          {/* 다운로드 섹션 */}
          <div className="bg-white/10 backdrop-blur-lg rounded-2xl p-8 mb-8">
            <div className="text-center mb-8">
              <h2 className="text-2xl font-bold text-white mb-4">
                최신 버전 다운로드
              </h2>
              
              {versionInfo && (
                <div className="bg-white/10 rounded-lg p-4 mb-6">
                  <div className="flex flex-col md:flex-row justify-between items-center gap-4">
                    <div className="text-left">
                      <div className="text-white font-semibold text-lg">
                        버전 {versionInfo.version}
                      </div>
                      <div className="text-blue-200 text-sm">
                        {formatDate(versionInfo.releaseDate)}
                      </div>
                    </div>
                    <div className="text-right">
                      <div className="text-white font-semibold">
                        {formatFileSize(versionInfo.fileSize)}
                      </div>
                      <div className="text-blue-200 text-sm">
                        Windows 64bit
                      </div>
                    </div>
                  </div>
                </div>
              )}

              <button
                onClick={handleDownload}
                disabled={isDownloading || !clientInfo?.available}
                className={`
                  px-8 py-4 rounded-lg font-bold text-lg transition-all duration-300 transform
                  ${isDownloading || !clientInfo?.available
                    ? 'bg-gray-500 cursor-not-allowed'
                    : 'bg-gradient-to-r from-blue-500 to-purple-600 hover:from-blue-600 hover:to-purple-700 hover:scale-105 shadow-lg hover:shadow-xl'
                  }
                  text-white
                `}
              >
                {isDownloading ? '다운로드 중...' : '게임 다운로드'}
              </button>

              {!clientInfo?.available && (
                <p className="text-red-300 mt-4">
                  현재 다운로드할 수 없습니다. 잠시 후 다시 시도해주세요.
                </p>
              )}
            </div>
          </div>

          {/* 변경사항 */}
          {versionInfo?.changelog && (
            <div className="bg-white/10 backdrop-blur-lg rounded-2xl p-8 mb-8">
              <h3 className="text-2xl font-bold text-white mb-6">
                최신 업데이트 내용
              </h3>
              <ul className="space-y-2">
                {versionInfo.changelog.map((change, index) => (
                  <li key={index} className="text-blue-200 flex items-start">
                    <span className="text-blue-400 mr-2">•</span>
                    {change}
                  </li>
                ))}
              </ul>
            </div>
          )}

          {/* 시스템 요구사항 */}
          <div className="bg-white/10 backdrop-blur-lg rounded-2xl p-8">
            <h3 className="text-2xl font-bold text-white mb-6">
              시스템 요구사항
            </h3>
            <div className="grid md:grid-cols-2 gap-6">
              <div>
                <h4 className="text-lg font-semibold text-white mb-3">최소 사양</h4>
                <ul className="space-y-2 text-blue-200">
                  <li>• Windows 7 64bit 이상</li>
                  <li>• 메모리: 512MB RAM</li>
                  <li>• 저장공간: 50MB 이상</li>
                  <li>• 네트워크: 인터넷 연결 필수</li>
                </ul>
              </div>
              <div>
                <h4 className="text-lg font-semibold text-white mb-3">권장 사양</h4>
                <ul className="space-y-2 text-blue-200">
                  <li>• Windows 10 64bit</li>
                  <li>• 메모리: 1GB RAM</li>
                  <li>• 저장공간: 100MB 이상</li>
                  <li>• 네트워크: 안정적인 인터넷</li>
                </ul>
              </div>
            </div>

            <div className="mt-6 p-4 bg-yellow-500/20 rounded-lg border border-yellow-500/30">
              <p className="text-yellow-200 font-semibold">
                ⚠️ 설치 안내
              </p>
              <p className="text-yellow-100 text-sm mt-1">
                다운로드 후 압축을 해제하고 BlokusClient.exe를 실행하세요. 
                Windows Defender에서 경고가 나타날 수 있지만 안전한 파일입니다.
              </p>
            </div>
          </div>
        </div>
      </div>
    </Layout>
  );
}