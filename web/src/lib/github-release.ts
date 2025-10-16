// GitHub Release API 공통 유틸리티
import { env } from './env';

/**
 * 릴리즈 노트에서 실제 변경사항만 추출
 * - 변경사항 섹션 또는 이모지로 시작하는 항목만 포함
 * - 메타 정보 섹션 제외 (기능 설명, 다운로드, 설치, 시스템 요구사항, 알려진 이슈, 커뮤니티, 감사 등)
 */
function extractChangelog(body: string): string[] {
  const lines = body.split('\n').map(line => line.trim());
  const changelog: string[] = [];

  // 변경사항으로 인정할 섹션 헤더 패턴
  const changelogSectionPatterns = [
    /^#{1,6}\s*(변경|변경사항|업데이트|update|change|what'?s new|새로운 기능|new feature)/i,
    /^#{1,6}\s*(개선|improvement|enhancement)/i,
    /^#{1,6}\s*(수정|fix|bug)/i,
  ];

  // 제외할 섹션 헤더 패턴
  const excludeSectionPatterns = [
    /^#{1,6}\s*(소개|개요|overview|about)/i,
    /^#{1,6}\s*(주요 기능|key feature|feature|기능)/i,
    /^#{1,6}\s*(플랫폼|platform)/i,
    /^#{1,6}\s*(시작|getting start|how to|사용법)/i,
    /^#{1,6}\s*(다운로드|download)/i,
    /^#{1,6}\s*(설치|installation|install)/i,
    /^#{1,6}\s*(시스템 요구사항|system requirements|requirements)/i,
    /^#{1,6}\s*(알려진 이슈|known issues|issues)/i,
    /^#{1,6}\s*(커뮤니티|community|support|지원)/i,
    /^#{1,6}\s*(문의|contact)/i,
    /^#{1,6}\s*(개발 현황|development|roadmap|진행|status)/i,
    /^#{1,6}\s*(감사|thanks|acknowledgments)/i,
    /^#{1,6}\s*(기여|contributors|contribution)/i,
    /^#{1,6}\s*(라이선스|license)/i,
    /^#{1,6}\s*(링크|link)/i,
  ];

  let inChangelogSection = false;
  let inExcludedSection = false;

  for (const line of lines) {
    // 헤더 라인 체크 (## 형태)
    if (line.match(/^#{1,6}\s+/)) {
      // 변경사항 섹션인지 확인
      const isChangelogSection = changelogSectionPatterns.some(pattern => pattern.test(line));
      // 제외할 섹션인지 확인
      const isExcludedSection = excludeSectionPatterns.some(pattern => pattern.test(line));

      if (isChangelogSection) {
        inChangelogSection = true;
        inExcludedSection = false;
      } else if (isExcludedSection) {
        inChangelogSection = false;
        inExcludedSection = true;
      } else {
        // 기타 헤더는 변경사항으로 간주하지 않음
        inChangelogSection = false;
        inExcludedSection = false;
      }
      continue;
    }

    // 제외 섹션 내부는 스킵
    if (inExcludedSection) {
      continue;
    }

    // 변경사항 라인 추출 (- 로 시작)
    if (line.startsWith('- ')) {
      const content = line.slice(2).trim();

      // 변경사항 섹션 내부이거나, 이모지로 시작하는 항목만 포함
      if (inChangelogSection || /^[\u{1F300}-\u{1F9FF}]/u.test(content)) {
        changelog.push(content);
      }
    }
  }

  return changelog;
}

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
    asset.name.includes('BlobloClient') && asset.name.endsWith('.zip')
  );

  // 변경사항 추출 (메타 섹션 제외)
  const changelog = extractChangelog(release.body || '');

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
      return getDefaultVersion(env.CLIENT_VERSION);
    }
  } catch (error) {
    console.error('GitHub 릴리즈 정보 조회 실패:', error);
    return getDefaultVersion(env.CLIENT_VERSION);
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

  // 데스크톱 클라이언트 에셋 찾기 (BlobloClient-Desktop 또는 BlobloClient.zip)
  const desktopAsset = release.assets?.find(asset =>
    (asset.name.includes('Desktop') || asset.name.includes('BlobloClient')) &&
    asset.name.endsWith('.zip')
  );

  // 모바일 클라이언트 에셋 찾기 (bloblo.apk 또는 BlobloClient.apk 또는 Mobile 포함 .apk)
  const mobileAsset = release.assets?.find(asset =>
    asset.name.endsWith('.apk') && (
      asset.name.toLowerCase().includes('bloblo') ||
      asset.name.includes('Mobile') ||
      asset.name.includes('BlobloClient')
    )
  );

  // 변경사항 추출 (메타 섹션 제외)
  const changelog = extractChangelog(release.body || '');

  return {
    version,
    releaseDate: release.published_at,
    changelog: changelog.length > 0 ? changelog : [release.name || '최신 버전 릴리즈'],
    platforms: {
      desktop: desktopAsset ? transformAssetToPlatform(desktopAsset, 'desktop') : null,
      mobile: mobileAsset ? transformAssetToPlatform(mobileAsset, 'mobile') : {
        platform: 'mobile' as const,
        filename: `bloblo-v${version}.apk`,
        downloadUrl: `https://github.com/zzzz955/blokus-online/releases/download/v${version}/bloblo-v${version}.apk`,
        size: 45000000,
        available: false, // APK 파일이 실제로 없으므로 false로 설정
        systemRequirements: SYSTEM_REQUIREMENTS.mobile
      }
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
      return getDefaultMultiPlatformVersion(env.CLIENT_VERSION);
    }
  } catch (error) {
    console.error('GitHub 다중 플랫폼 릴리즈 정보 조회 실패:', error);
    return getDefaultMultiPlatformVersion(env.CLIENT_VERSION);
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
        filename: `BlobloClient-Desktop-v${version}.zip`,
        downloadUrl: "/api/download/client/desktop",
        size: 15670931,
        available: true,
        systemRequirements: SYSTEM_REQUIREMENTS.desktop
      },
      mobile: {
        platform: 'mobile',
        filename: `bloblo-v${version}.apk`,
        downloadUrl: "/api/download/client/mobile",
        size: 45000000, // 약 45MB 예상
        available: false, // APK 파일이 없을 경우를 대비하여 기본값을 false로 설정
        systemRequirements: SYSTEM_REQUIREMENTS.mobile
      }
    }
  };
}