// src/lib/file-storage.ts
import fs from 'fs/promises';
import path from 'path';
import { env } from './env';

//  MIME → 확장자 매핑 (실제 저장은 항상 png로 통일하지만, 참고용)
const MIME_TO_EXT: Record<string, string> = {
  'image/png': 'png',
  'image/svg+xml': 'svg',
  'image/jpeg': 'jpg',
};

type StorageConfig = {
  baseDir: string;
  thumbnailDir: string;
  publicPath: string;
  isProduction: boolean;
};

const DEFAULT_CONFIG: StorageConfig = {
  baseDir: env.APP_ROOT_DIR,
  thumbnailDir: env.THUMBNAIL_STORAGE_DIR || 'public/stage-thumbnails',
  publicPath: env.THUMBNAIL_PUBLIC_PATH,
  isProduction: env.NODE_ENV === 'production'
};

async function ensureDir(baseDir: string, subDir: string) {
  const full = path.join(baseDir, subDir);
  await fs.mkdir(full, { recursive: true });
  return full;
}

async function toPngBufferFromDataUrl(dataUrl: string): Promise<Buffer> {
  const m = dataUrl.match(/^data:(image\/[a-zA-Z0-9.+-]+);base64,([\s\S]+)$/);
  if (!m) throw new Error('Unsupported data URL format');

  const mime = m[1];
  const buf = Buffer.from(m[2], 'base64');

  // 항상 PNG로 변환 (SVG/JPEG 모두 포함)
  const sharp = (await import('sharp')).default;

  if (mime === 'image/png') {
    // 이미 PNG면 그대로 리턴(필요시 재인코딩 가능)
    return buf;
  }
  // SVG/JPEG 등은 PNG로 래스터화
  return await sharp(buf).png().toBuffer();
}

class FileStorage {
  private config: StorageConfig;

  constructor(config: Partial<StorageConfig> = {}) {
    this.config = { ...DEFAULT_CONFIG, ...config };
  }

  async ensureThumbnailDir() {
    // 환경별 디렉토리 경로 처리
    if (this.config.isProduction) {
      // 배포환경: 절대 경로 또는 컨테이너 내 경로 사용
      const thumbnailPath = path.isAbsolute(this.config.thumbnailDir) 
        ? this.config.thumbnailDir 
        : path.join(this.config.baseDir, this.config.thumbnailDir);
      await fs.mkdir(thumbnailPath, { recursive: true });
      return thumbnailPath;
    } else {
      // 개발환경: 기존 로직 유지
      return ensureDir(this.config.baseDir, this.config.thumbnailDir);
    }
  }

  generateThumbnailFilename(stageNumber: number): string {
    const ts = Date.now();
    // 저장물은 항상 png
    return `stage-${stageNumber}-${ts}.png`;
  }
  async saveThumbnail(dataUrl: string, filename: string): Promise<string> {
    const dir = await this.ensureThumbnailDir();

    // 이름에서 확장자 제거 후 .png로 강제
    const parsed = path.parse(filename);
    const finalName = `${parsed.name}.png`;
    const fullPath = path.join(dir, finalName);

    const pngBuffer = await toPngBufferFromDataUrl(dataUrl);
    await fs.writeFile(fullPath, pngBuffer);

    // 공개 URL 반환
    return path.posix.join(this.config.publicPath, finalName);
  }

  async cleanupOldThumbnails(stageNumber: number): Promise<void> {
    const dir = await this.ensureThumbnailDir();
    const files = await fs.readdir(dir);
    const prefix = `stage-${stageNumber}-`;
    const oldFiles = files.filter(f => f.startsWith(prefix));
    for (const f of oldFiles) {
      await fs.unlink(path.join(dir, f)).catch(() => { });
    }
  }

  async deleteThumbnail(fileName: string): Promise<void> {
    const fullPath = path.join(this.config.baseDir, this.config.thumbnailDir, fileName);
    await fs.unlink(fullPath).catch(() => { });
  }

  async deleteOldThumbnailsForStage(stageNumber: number): Promise<void> {
    return this.cleanupOldThumbnails(stageNumber);
  }
}

export default new FileStorage();
