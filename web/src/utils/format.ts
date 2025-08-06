export function formatDate(date: string | Date): string {
  const d = new Date(date);
  return d.toLocaleDateString('ko-KR', {
    year: 'numeric',
    month: 'long',
    day: 'numeric',
  });
}

export function formatDateTime(date: string | Date): string {
  const d = new Date(date);
  return d.toLocaleString('ko-KR', {
    year: 'numeric',
    month: 'long',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

export function formatRelativeTime(date: string | Date): string {
  const d = new Date(date);
  const now = new Date();
  const diffInSeconds = Math.floor((now.getTime() - d.getTime()) / 1000);

  if (diffInSeconds < 60) {
    return '방금 전';
  }

  const diffInMinutes = Math.floor(diffInSeconds / 60);
  if (diffInMinutes < 60) {
    return `${diffInMinutes}분 전`;
  }

  const diffInHours = Math.floor(diffInMinutes / 60);
  if (diffInHours < 24) {
    return `${diffInHours}시간 전`;
  }

  const diffInDays = Math.floor(diffInHours / 24);
  if (diffInDays < 7) {
    return `${diffInDays}일 전`;
  }

  return formatDate(date);
}

export function formatPostDate(date: string | Date): string {
  const d = new Date(date);
  const now = new Date();
  
  // 오늘인지 확인 (같은 날짜인지)
  const isToday = d.toDateString() === now.toDateString();
  
  if (isToday) {
    // 오늘이면 24시간 형식 HH:MM
    return d.toLocaleTimeString('ko-KR', { 
      hour: '2-digit', 
      minute: '2-digit',
      hour12: false
    });
  } else {
    // 오늘이 아니면 MM.DD 형식 (마지막 점 제거)
    const month = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${month}.${day}`;
  }
}

export function isPostModified(createdAt: string, updatedAt: string): boolean {
  const created = new Date(createdAt);
  const updated = new Date(updatedAt);
  
  // updated_at이 created_at보다 나중이고, 차이가 5초 이상인 경우에만 수정된 것으로 간주
  // 5초 임계값으로 설정하여 작성 시 미세한 시간 차이는 무시하고 실제 수정만 감지
  return updated.getTime() > created.getTime() && (updated.getTime() - created.getTime()) >= 5000;
}

export function truncateText(text: string, maxLength: number): string {
  if (text.length <= maxLength) return text;
  return text.substring(0, maxLength) + '...';
}