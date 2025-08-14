import fs from 'fs/promises';
import path from 'path';

// ✅ MIME → 확장자 매핑
const MIME_TO_EXT: Record<string, string> = {
  'image/png': 'png',
  'image/svg+xml': 'svg',
  'image/jpeg': 'jpg',
};

// (선택) monorepo일 때 안전하게:
// APP_ROOT_DIR=/절대/경로/…/web (Next 앱 루트) 로 지정해두면 public 경로가 정확해져요.
const DEFAULT_CONFIG: StorageConfig = {
  baseDir: process.env.APP_ROOT_DIR || process.cwd(),
  thumbnailDir: 'public/stage-thumbnails',
  publicPath: process.env.THUMBNAIL_PUBLIC_PATH || '/stage-thumbnails'
};

export interface StorageConfig {
  baseDir: string;
  thumbnailDir: string;
  publicPath: string;
}

export class FileStorage {
  private config: StorageConfig;

  constructor(config: Partial<StorageConfig> = {}) {
    this.config = { ...DEFAULT_CONFIG, ...config };
  }

  /**
   * Ensure thumbnail directory exists
   */
  async ensureThumbnailDir(): Promise<void> {
    const fullPath = path.join(this.config.baseDir, this.config.thumbnailDir);

    try {
      await fs.access(fullPath);
    } catch (error) {
      // Directory doesn't exist, create it
      await fs.mkdir(fullPath, { recursive: true });
    }
  }

  /**
   * Save thumbnail from data URL
   */
  async saveThumbnail(dataUrl: string, filename: string): Promise<string> {
    await this.ensureThumbnailDir();

    // ✅ SVG/PNG/JPEG 모두 지원
    const match = dataUrl.match(/^data:(image\/[a-zA-Z0-9.+-]+);base64,([\s\S]*)$/);
    if (!match) throw new Error('Unsupported data URL format for thumbnail');

    const mime = match[1];
    const base64Data = match[2];
    const ext = MIME_TO_EXT[mime] ?? 'bin';

    // 확장자 동기화
    const parsed = path.parse(filename);
    const finalName = `${parsed.name}.${ext}`;

    const buffer = Buffer.from(base64Data, 'base64');
    const fullPath = path.join(this.config.baseDir, this.config.thumbnailDir, finalName);
    await fs.writeFile(fullPath, buffer);

    return `${this.config.publicPath}/${finalName}`;
  }

  /**
   * Delete thumbnail file
   */
  async deleteThumbnail(filename: string): Promise<void> {
    const fullPath = path.join(this.config.baseDir, this.config.thumbnailDir, filename);

    try {
      await fs.unlink(fullPath);
    } catch (error) {
      // File might not exist, ignore error
      console.warn(`Failed to delete thumbnail ${filename}:`, error);
    }
  }

  /**
   * Check if thumbnail exists
   */
  async thumbnailExists(filename: string): Promise<boolean> {
    const fullPath = path.join(this.config.baseDir, this.config.thumbnailDir, filename);

    try {
      await fs.access(fullPath);
      return true;
    } catch (error) {
      return false;
    }
  }

  /**
   * Get thumbnail URL if exists, null otherwise
   */
  async getThumbnailUrl(filename: string): Promise<string | null> {
    const exists = await this.thumbnailExists(filename);
    return exists ? `${this.config.publicPath}/${filename}` : null;
  }

  /**
   * Generate filename for stage thumbnail
   */
  generateThumbnailFilename(stageNumber: number): string {
    const timestamp = Date.now();
    return `stage-${stageNumber}-${timestamp}.png`;
  }

  /**
   * Clean up old thumbnails for a stage
   */
  async cleanupOldThumbnails(stageNumber: number): Promise<void> {
    await this.ensureThumbnailDir();

    const dirPath = path.join(this.config.baseDir, this.config.thumbnailDir);
    const files = await fs.readdir(dirPath);

    // Find files matching the stage pattern
    const stagePrefix = `stage-${stageNumber}-`;
    const oldFiles = files.filter(file => file.startsWith(stagePrefix));

    // Delete old files
    for (const file of oldFiles) {
      await this.deleteThumbnail(file);
    }
  }

  async deleteOldThumbnailsForStage(stageNumber: number): Promise<void> {
    return this.cleanupOldThumbnails(stageNumber);
  }
}

// Export default instance
export default new FileStorage();