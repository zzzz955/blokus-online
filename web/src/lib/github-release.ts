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