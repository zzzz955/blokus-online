/**
 * Image Upload Utilities
 * 이미지 업로드 및 처리 유틸리티
 */

export interface UploadedImage {
  url: string;
  filename: string;
  size: number;
  type: string;
}

/**
 * 이미지 파일 유효성 검사
 */
export function validateImageFile(file: File): { valid: boolean; error?: string } {
  // 파일 타입 체크
  const allowedTypes = ['image/jpeg', 'image/jpg', 'image/png', 'image/gif', 'image/webp'];
  if (!allowedTypes.includes(file.type)) {
    return {
      valid: false,
      error: '지원하지 않는 이미지 형식입니다. (JPG, PNG, GIF, WEBP만 가능)',
    };
  }

  // 파일 크기 체크 (5MB 제한)
  const maxSize = 5 * 1024 * 1024; // 5MB
  if (file.size > maxSize) {
    return {
      valid: false,
      error: '이미지 크기는 5MB를 초과할 수 없습니다.',
    };
  }

  return { valid: true };
}

/**
 * 이미지를 Base64로 변환
 */
export function imageToBase64(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => {
      if (typeof reader.result === 'string') {
        resolve(reader.result);
      } else {
        reject(new Error('Failed to read image as base64'));
      }
    };
    reader.onerror = reject;
    reader.readAsDataURL(file);
  });
}

/**
 * 이미지를 Data URL로 변환 (미리보기용)
 */
export function createImagePreview(file: File): Promise<string> {
  return imageToBase64(file);
}

/**
 * 클립보드에서 이미지 추출
 */
export function extractImageFromClipboard(event: ClipboardEvent): File | null {
  const items = event.clipboardData?.items;
  if (!items) return null;

  for (let i = 0; i < items.length; i++) {
    const item = items[i];
    if (item.type.indexOf('image') !== -1) {
      const file = item.getAsFile();
      return file;
    }
  }

  return null;
}

/**
 * 드래그앤드롭에서 이미지 추출
 */
export function extractImageFromDrop(event: DragEvent): File[] {
  const files: File[] = [];
  const items = event.dataTransfer?.items;

  if (items) {
    for (let i = 0; i < items.length; i++) {
      const item = items[i];
      if (item.kind === 'file' && item.type.indexOf('image') !== -1) {
        const file = item.getAsFile();
        if (file) files.push(file);
      }
    }
  } else if (event.dataTransfer?.files) {
    // Fallback for older browsers
    const fileList = event.dataTransfer.files;
    for (let i = 0; i < fileList.length; i++) {
      const file = fileList[i];
      if (file.type.indexOf('image') !== -1) {
        files.push(file);
      }
    }
  }

  return files;
}

/**
 * 이미지 업로드 (서버 또는 Base64)
 * TODO: 실제 서버 업로드 API 연동 필요
 */
export async function uploadImage(file: File): Promise<UploadedImage> {
  // 유효성 검사
  const validation = validateImageFile(file);
  if (!validation.valid) {
    throw new Error(validation.error);
  }

  try {
    // 임시: Base64로 변환하여 반환 (실제 서버 업로드로 교체 필요)
    const base64 = await imageToBase64(file);

    return {
      url: base64,
      filename: file.name,
      size: file.size,
      type: file.type,
    };

    // TODO: 실제 서버 업로드 구현
    /*
    const formData = new FormData();
    formData.append('image', file);

    const response = await fetch('/api/upload/image', {
      method: 'POST',
      body: formData,
    });

    if (!response.ok) {
      throw new Error('이미지 업로드에 실패했습니다.');
    }

    const data = await response.json();
    return {
      url: data.url,
      filename: file.name,
      size: file.size,
      type: file.type,
    };
    */
  } catch (error) {
    console.error('Image upload error:', error);
    throw error;
  }
}

/**
 * 여러 이미지를 순차적으로 업로드
 */
export async function uploadMultipleImages(files: File[]): Promise<UploadedImage[]> {
  const results: UploadedImage[] = [];

  for (const file of files) {
    try {
      const uploaded = await uploadImage(file);
      results.push(uploaded);
    } catch (error) {
      console.error(`Failed to upload ${file.name}:`, error);
      // 실패한 파일은 건너뛰고 계속 진행
    }
  }

  return results;
}
