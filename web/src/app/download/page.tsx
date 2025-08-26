'use client';

import { useState, useEffect } from 'react';
import Layout from '@/components/layout/Layout';

// 플랫폼 타입
type PlatformType = 'desktop' | 'mobile';

// 플랫폼별 클라이언트 정보
interface PlatformClientInfo {
  available: boolean;
  filename: string;
  downloadUrl: string;
  fileSize: number;
  source: string;
  platform: PlatformType;
}

// 다중 플랫폼 릴리즈 정보
interface MultiPlatformReleaseInfo {
  available: boolean;
  version: string;
  releaseDate: string;
  changelog: string[];
  platforms: {
    desktop: PlatformClientInfo | null;
    mobile: PlatformClientInfo | null;
  };
}

// 시스템 요구사항
interface SystemRequirements {
  os: string;
  memory: string;
  storage: string;
  network: string;
}

// 플랫폼별 시스템 요구사항
const SYSTEM_REQUIREMENTS: Record<PlatformType, { minimum: SystemRequirements; recommended: SystemRequirements }> = {
  desktop: {
    minimum: {
      os: "Windows 7 64bit 이상",
      memory: "512MB RAM",
      storage: "50MB 이상",
      network: "인터넷 연결 필수"
    },
    recommended: {
      os: "Windows 10 64bit",
      memory: "1GB RAM",
      storage: "100MB 이상",
      network: "안정적인 인터넷"
    }
  },
  mobile: {
    minimum: {
      os: "Android 5.0 (API 21) 이상",
      memory: "2GB RAM",
      storage: "100MB 이상",
      network: "인터넷 연결 필수"
    },
    recommended: {
      os: "Android 8.0 이상",
      memory: "4GB RAM",
      storage: "200MB 이상",
      network: "Wi-Fi 권장"
    }
  }
};

// 플랫폼 정보
const PLATFORM_INFO = {
  desktop: {
    name: 'Windows 데스크톱',
    icon: '💻',
    description: 'Windows PC용 클라이언트',
    installGuide: 'BlokusClient.exe를 실행하세요. Windows Defender에서 경고가 나타날 수 있지만 안전한 파일입니다.'
  },
  mobile: {
    name: 'Android 모바일',
    icon: '📱', 
    description: 'Android 스마트폰/태블릿용 클라이언트',
    installGuide: 'APK 파일을 다운로드하여 설치하세요. 알 수 없는 출처 설치 허용이 필요할 수 있습니다.'
  }
};

// 사용자 플랫폼 자동 감지
function detectUserPlatform(): PlatformType {
  if (typeof window === 'undefined') return 'desktop';
  
  const userAgent = navigator.userAgent.toLowerCase();
  const isMobile = /android|iphone|ipad|ipod|blackberry|iemobile|opera mini/i.test(userAgent);
  
  // Android 기기인 경우 mobile, 그 외에는 desktop
  return isMobile && /android/i.test(userAgent) ? 'mobile' : 'desktop';
}

export default function DownloadPage() {
  const [releaseInfo, setReleaseInfo] = useState<MultiPlatformReleaseInfo | null>(null);
  const [selectedPlatform, setSelectedPlatform] = useState<PlatformType>('desktop');
  const [isDownloading, setIsDownloading] = useState<Record<PlatformType, boolean>>({
    desktop: false,
    mobile: false
  });
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    // 사용자 플랫폼 자동 감지하여 기본 선택
    const detectedPlatform = detectUserPlatform();
    setSelectedPlatform(detectedPlatform);

    // 다중 플랫폼 릴리즈 정보 가져오기
    fetch('/api/download/client', { method: 'POST' })
      .then(res => res.json())
      .then(data => {
        setReleaseInfo(data);
        setLoading(false);
      })
      .catch(error => {
        console.error('Failed to fetch multi-platform release info:', error);
        setLoading(false);
      });
  }, []);

  const handlePlatformDownload = (platform: PlatformType) => {
    const platformInfo = releaseInfo?.platforms[platform];
    if (!platformInfo?.available) return;
    
    setIsDownloading(prev => ({ ...prev, [platform]: true }));
    
    // 플랫폼별 다운로드 시작
    window.location.href = platformInfo.downloadUrl;
    
    // 3초 후 로딩 상태 해제
    setTimeout(() => {
      setIsDownloading(prev => ({ ...prev, [platform]: false }));
    }, 3000);
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

  // 현재 선택된 플랫폼 정보
  const currentPlatformInfo = releaseInfo?.platforms[selectedPlatform];
  const currentPlatformMeta = PLATFORM_INFO[selectedPlatform];
  const currentSystemReqs = SYSTEM_REQUIREMENTS[selectedPlatform];

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

          {/* 버전 정보 헤더 */}
          {releaseInfo && (
            <div className="bg-white/10 backdrop-blur-lg rounded-2xl p-6 mb-8">
              <div className="text-center">
                <div className="text-white font-semibold text-2xl mb-2">
                  버전 {releaseInfo.version}
                </div>
                <div className="text-blue-200 text-lg">
                  {formatDate(releaseInfo.releaseDate)}
                </div>
              </div>
            </div>
          )}

          {/* 플랫폼 선택 탭 */}
          <div className="bg-white/10 backdrop-blur-lg rounded-2xl p-8 mb-8">
            <div className="flex flex-col md:flex-row gap-4 mb-8">
              {(['desktop', 'mobile'] as PlatformType[]).map((platform) => {
                const platformMeta = PLATFORM_INFO[platform];
                const platformData = releaseInfo?.platforms[platform];
                const isActive = selectedPlatform === platform;
                const isAvailable = platformData?.available ?? false;
                
                return (
                  <button
                    key={platform}
                    onClick={() => setSelectedPlatform(platform)}
                    disabled={!isAvailable}
                    className={`
                      flex-1 p-4 rounded-xl border-2 transition-all duration-300
                      ${isActive 
                        ? 'border-blue-400 bg-blue-500/20' 
                        : isAvailable 
                          ? 'border-white/20 bg-white/5 hover:border-blue-300 hover:bg-blue-500/10' 
                          : 'border-gray-500 bg-gray-500/10 cursor-not-allowed opacity-50'
                      }
                    `}
                  >
                    <div className="flex items-center gap-4">
                      <div className="text-4xl">{platformMeta.icon}</div>
                      <div className="text-left">
                        <div className="text-white font-semibold text-lg">
                          {platformMeta.name}
                        </div>
                        <div className="text-blue-200 text-sm">
                          {platformMeta.description}
                        </div>
                        {!isAvailable && (
                          <div className="text-red-300 text-xs mt-1">
                            현재 사용할 수 없음
                          </div>
                        )}
                      </div>
                    </div>
                  </button>
                );
              })}
            </div>

            {/* 선택된 플랫폼 다운로드 섹션 */}
            {currentPlatformInfo && (
              <div className="text-center">
                <div className="bg-white/10 rounded-lg p-6 mb-6">
                  <div className="flex flex-col md:flex-row justify-between items-center gap-4">
                    <div className="text-left">
                      <div className="text-white font-semibold text-lg">
                        {currentPlatformMeta.name}
                      </div>
                      <div className="text-blue-200 text-sm">
                        {currentPlatformInfo.filename}
                      </div>
                    </div>
                    <div className="text-right">
                      <div className="text-white font-semibold">
                        {formatFileSize(currentPlatformInfo.fileSize)}
                      </div>
                      <div className="text-blue-200 text-sm">
                        {currentPlatformInfo.source === 'local-releases' ? '로컬 서버' : 'GitHub 릴리즈'}
                      </div>
                    </div>
                  </div>
                </div>

                <button
                  onClick={() => handlePlatformDownload(selectedPlatform)}
                  disabled={isDownloading[selectedPlatform] || !currentPlatformInfo.available}
                  className={`
                    px-8 py-4 rounded-lg font-bold text-lg transition-all duration-300 transform
                    ${isDownloading[selectedPlatform] || !currentPlatformInfo.available
                      ? 'bg-gray-500 cursor-not-allowed'
                      : 'bg-gradient-to-r from-blue-500 to-purple-600 hover:from-blue-600 hover:to-purple-700 hover:scale-105 shadow-lg hover:shadow-xl'
                    }
                    text-white
                  `}
                >
                  {isDownloading[selectedPlatform] 
                    ? '다운로드 중...' 
                    : `${currentPlatformMeta.icon} ${currentPlatformMeta.name} 다운로드`
                  }
                </button>

                {!currentPlatformInfo.available && (
                  <p className="text-red-300 mt-4">
                    현재 {currentPlatformMeta.name} 클라이언트를 사용할 수 없습니다.
                  </p>
                )}
              </div>
            )}
          </div>

          {/* 변경사항 */}
          {releaseInfo?.changelog && (
            <div className="bg-white/10 backdrop-blur-lg rounded-2xl p-8 mb-8">
              <h3 className="text-2xl font-bold text-white mb-6">
                최신 업데이트 내용
              </h3>
              <ul className="space-y-2">
                {releaseInfo.changelog.map((change, index) => (
                  <li key={index} className="text-blue-200 flex items-start">
                    <span className="text-blue-400 mr-2">•</span>
                    {change}
                  </li>
                ))}
              </ul>
            </div>
          )}

          {/* 플랫폼별 시스템 요구사항 */}
          <div className="bg-white/10 backdrop-blur-lg rounded-2xl p-8 mb-8">
            <h3 className="text-2xl font-bold text-white mb-6">
              {currentPlatformMeta.name} 시스템 요구사항
            </h3>
            <div className="grid md:grid-cols-2 gap-6">
              <div>
                <h4 className="text-lg font-semibold text-white mb-3">최소 사양</h4>
                <ul className="space-y-2 text-blue-200">
                  <li>• {currentSystemReqs.minimum.os}</li>
                  <li>• 메모리: {currentSystemReqs.minimum.memory}</li>
                  <li>• 저장공간: {currentSystemReqs.minimum.storage}</li>
                  <li>• 네트워크: {currentSystemReqs.minimum.network}</li>
                </ul>
              </div>
              <div>
                <h4 className="text-lg font-semibold text-white mb-3">권장 사양</h4>
                <ul className="space-y-2 text-blue-200">
                  <li>• {currentSystemReqs.recommended.os}</li>
                  <li>• 메모리: {currentSystemReqs.recommended.memory}</li>
                  <li>• 저장공간: {currentSystemReqs.recommended.storage}</li>
                  <li>• 네트워크: {currentSystemReqs.recommended.network}</li>
                </ul>
              </div>
            </div>

            <div className="mt-6 p-4 bg-yellow-500/20 rounded-lg border border-yellow-500/30">
              <p className="text-yellow-200 font-semibold">
                ⚠️ 설치 안내
              </p>
              <p className="text-yellow-100 text-sm mt-1">
                {currentPlatformMeta.installGuide}
              </p>
            </div>
          </div>

          {/* QR 코드 섹션 (데스크톱에서 모바일 다운로드 안내) */}
          {selectedPlatform === 'desktop' && releaseInfo?.platforms.mobile && (
            <div className="bg-white/10 backdrop-blur-lg rounded-2xl p-8">
              <h3 className="text-2xl font-bold text-white mb-4 text-center">
                📱 모바일 버전도 이용해보세요!
              </h3>
              <div className="text-center">
                <p className="text-blue-200 mb-4">
                  Android 기기에서도 블로커스 온라인을 즐길 수 있습니다.
                </p>
                <button
                  onClick={() => setSelectedPlatform('mobile')}
                  className="inline-flex items-center gap-2 px-6 py-3 bg-green-600 hover:bg-green-700 text-white rounded-lg font-medium transition-colors duration-200"
                >
                  📱 모바일 버전 보기
                </button>
              </div>
            </div>
          )}

          {/* 크로스 플랫폼 안내 (모바일에서 데스크톱 안내) */}
          {selectedPlatform === 'mobile' && releaseInfo?.platforms.desktop && (
            <div className="bg-white/10 backdrop-blur-lg rounded-2xl p-8">
              <h3 className="text-2xl font-bold text-white mb-4 text-center">
                💻 더 큰 화면에서 즐기세요!
              </h3>
              <div className="text-center">
                <p className="text-blue-200 mb-4">
                  PC에서는 더 넓은 화면과 편리한 조작으로 게임을 즐길 수 있습니다.
                </p>
                <button
                  onClick={() => setSelectedPlatform('desktop')}
                  className="inline-flex items-center gap-2 px-6 py-3 bg-blue-600 hover:bg-blue-700 text-white rounded-lg font-medium transition-colors duration-200"
                >
                  💻 데스크톱 버전 보기
                </button>
              </div>
            </div>
          )}
        </div>
      </div>
    </Layout>
  );
}