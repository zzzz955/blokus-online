// GitHub Release API 공통 유틸리티
export interface GitHubRelease {
  tag_name: string;
  name: string;
  published_at: string;
  body?: string;
  assets: Array<{
    name: string;
    browser_download_url: string;
    size: number;
  }>;
}

export interface ClientVersion {
  version: string;
  releaseDate: string;
  downloadUrl: string;
  fileSize: number;
  changelog?: string[];
}

// 새로운 다중 플랫폼 지원 인터페이스
export type PlatformType = 'desktop' | 'mobile';

export interface SystemRequirements {
  os: string;
  memory: string;
  storage: string;
  network: string;
}

export interface PlatformAsset {
  platform: PlatformType;
  filename: string;
  downloadUrl: string;
  size: number;
  available: boolean;
  systemRequirements: {
    minimum: SystemRequirements;
    recommended: SystemRequirements;
  };
}

export interface MultiPlatformRelease {
  version: string;
  releaseDate: string;
  changelog: string[];
  platforms: {
    desktop: PlatformAsset | null;
    mobile: PlatformAsset | null;
  };
}

/**
 * GitHub Release API를 통해 최신 릴리즈 정보 가져오기
 * 버전 API와 다운로드 API에서 공통으로 사용
 */
export async function fetchGitHubRelease(version?: string): Promise<GitHubRelease | null> {
  try {
    const repoUrl = 'https://api.github.com/repos/zzzz955/blokus-online/releases';
    const url = version ? `${repoUrl}/tags/${version}` : `${repoUrl}/latest`;
    
    const response = await fetch(url, {
      headers: {
        'Accept': 'application/vnd.github+json',
        'User-Agent': 'blokus-web-service'
      }
    });
    
    if (!response.ok) {
      console.warn(`GitHub API 호출 실패: ${response.status} ${response.statusText}`);
      return null;
    }
    
    return await response.json();
  } catch (error) {
    console.error('GitHub API 호출 오류:', error);
    return null;
  }
}

/**
 * GitHub Release 데이터를 ClientVersion 형태로 변환
 */
export function transformToClientVersion(release: GitHubRelease): ClientVersion {
  const version = release.tag_name.startsWith('v') ? release.tag_name.slice(1) : release.tag_name;
  const downloadAsset = release.assets?.find(asset => 
    asset.name.includes('BlokusClient') && asset.name.endsWith('.zip')
  );
  
  const bodyLines: string[] = (release.body || '').split('\n');
  const changelog = bodyLines
    .map(line => line.trim())
    .filter(line => line.startsWith('- '))
    .map(line => line.slice(2));

  return {
    version,
    releaseDate: release.published_at,
    downloadUrl: downloadAsset?.browser_download_url || '',
    fileSize: downloadAsset?.size || 0,
    changelog: changelog.length > 0 ? changelog : [release.name || '최신 버전 릴리즈']
  };
}

/**
 * 기본 버전 정보 반환 (GitHub API 실패시)
 */
export function getDefaultVersion(envVersion?: string): ClientVersion {
  const version = envVersion?.replace('v', '') || '1.0.0';
  
  return {
    version,
    releaseDate: new Date().toISOString(),
    downloadUrl: "/api/download/client",
    fileSize: 15670931,
    changelog: [
      "초기 릴리즈",
      "멀티플레이어 블로쿠스 게임 지원",
      "실시간 채팅 기능",
      "사용자 통계 및 순위 시스템"
    ]
  };
}

/**
 * 통합된 버전 정보 가져오기 함수
 */
export async function getVersionInfo(): Promise<ClientVersion> {
  try {
    const release = await fetchGitHubRelease();
    
    if (release) {
      console.log(`GitHub 최신 릴리즈 확인됨: ${release.tag_name}`);
      return transformToClientVersion(release);
    } else {
      console.warn('GitHub Releases API 오류, 기본 버전 사용');
      return getDefaultVersion(process.env.CLIENT_VERSION);
    }
  } catch (error) {
    console.error('GitHub 릴리즈 정보 조회 실패:', error);
    return getDefaultVersion(process.env.CLIENT_VERSION);
  }
}

/**
 * 플랫폼별 시스템 요구사항 상수
 */
export const SYSTEM_REQUIREMENTS: Record<PlatformType, { minimum: SystemRequirements; recommended: SystemRequirements }> = {
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

/**
 * GitHub Release 에셋을 플랫폼별로 분류하고 변환
 */
function transformAssetToPlatform(asset: GitHubRelease['assets'][0], platform: PlatformType): PlatformAsset {
  return {
    platform,
    filename: asset.name,
    downloadUrl: asset.browser_download_url,
    size: asset.size,
    available: true,
    systemRequirements: SYSTEM_REQUIREMENTS[platform]
  };
}

/**
 * GitHub Release 데이터를 다중 플랫폼 릴리즈로 변환
 */
export function transformToMultiPlatformRelease(release: GitHubRelease): MultiPlatformRelease {
  const version = release.tag_name.startsWith('v') ? release.tag_name.slice(1) : release.tag_name;
  
  // 데스크톱 클라이언트 에셋 찾기 (BlokusClient-Desktop 또는 BlokusClient.zip)
  const desktopAsset = release.assets?.find(asset => 
    (asset.name.includes('Desktop') || asset.name.includes('BlokusClient')) && 
    asset.name.endsWith('.zip')
  );
  
  // 모바일 클라이언트 에셋 찾기 (BlokusClient-Mobile.apk)
  const mobileAsset = release.assets?.find(asset => 
    asset.name.includes('Mobile') && asset.name.endsWith('.apk')
  );
  
  // 변경사항 파싱
  const bodyLines: string[] = (release.body || '').split('\n');
  const changelog = bodyLines
    .map(line => line.trim())
    .filter(line => line.startsWith('- '))
    .map(line => line.slice(2));

  return {
    version,
    releaseDate: release.published_at,
    changelog: changelog.length > 0 ? changelog : [release.name || '최신 버전 릴리즈'],
    platforms: {
      desktop: desktopAsset ? transformAssetToPlatform(desktopAsset, 'desktop') : null,
      mobile: mobileAsset ? transformAssetToPlatform(mobileAsset, 'mobile') : null
    }
  };
}

/**
 * 다중 플랫폼 릴리즈 정보 가져오기 함수
 */
export async function getMultiPlatformReleaseInfo(): Promise<MultiPlatformRelease> {
  try {
    const release = await fetchGitHubRelease();
    
    if (release) {
      console.log(`GitHub 다중 플랫폼 릴리즈 확인됨: ${release.tag_name}`);
      return transformToMultiPlatformRelease(release);
    } else {
      console.warn('GitHub Releases API 오류, 기본 다중 플랫폼 버전 사용');
      return getDefaultMultiPlatformVersion(process.env.CLIENT_VERSION);
    }
  } catch (error) {
    console.error('GitHub 다중 플랫폼 릴리즈 정보 조회 실패:', error);
    return getDefaultMultiPlatformVersion(process.env.CLIENT_VERSION);
  }
}

/**
 * 기본 다중 플랫폼 버전 정보 반환 (GitHub API 실패시)
 */
export function getDefaultMultiPlatformVersion(envVersion?: string): MultiPlatformRelease {
  const version = envVersion?.replace('v', '') || '1.0.0';
  
  return {
    version,
    releaseDate: new Date().toISOString(),
    changelog: [
      "초기 릴리즈",
      "멀티플레이어 블로쿠스 게임 지원", 
      "실시간 채팅 기능",
      "사용자 통계 및 순위 시스템"
    ],
    platforms: {
      desktop: {
        platform: 'desktop',
        filename: `BlokusClient-Desktop-v${version}.zip`,
        downloadUrl: "/api/download/client/desktop",
        size: 15670931,
        available: true,
        systemRequirements: SYSTEM_REQUIREMENTS.desktop
      },
      mobile: {
        platform: 'mobile', 
        filename: `BlokusClient-Mobile-v${version}.apk`,
        downloadUrl: "/api/download/client/mobile",
        size: 45000000, // 약 45MB 예상
        available: true,
        systemRequirements: SYSTEM_REQUIREMENTS.mobile
      }
    }
  };
}