export interface Announcement {
  id: number;
  title: string;
  content: string;
  author: string;
  createdAt: string;
  updatedAt: string;
  isPinned: boolean;
  isPublished: boolean;
  // 데이터베이스 필드 (스네이크케이스)
  created_at?: Date;
  updated_at?: Date;
  is_pinned?: boolean;
  is_published?: boolean;
}

export interface PatchNote {
  id: number;
  version: string;
  title: string;
  content: string;
  releaseDate: string;
  downloadUrl?: string;
  createdAt: string;
  // 데이터베이스 필드 (스네이크케이스)
  release_date?: Date;
  download_url?: string | null;
  created_at?: Date;
}

export interface SupportTicket {
  id: number;
  email: string;
  subject: string;
  message: string;
  status: 'PENDING' | 'ANSWERED' | 'CLOSED';
  adminReply?: string;
  createdAt: string;
  repliedAt?: string;
}

export interface AdminUser {
  id: number;
  username: string;
  role: 'ADMIN' | 'SUPER_ADMIN';
  createdAt: string;
}

export interface ApiResponse<T = any> {
  success: boolean;
  data?: T;
  error?: string;
  message?: string;
}

export interface PaginatedResponse<T> extends ApiResponse<T[]> {
  pagination: {
    page: number;
    limit: number;
    total: number;
    totalPages: number;
  };
}

export interface ContactForm {
  email: string;
  subject: string;
  message: string;
}

export interface AdminLoginForm {
  username: string;
  password: string;
}

export interface GameUserStats {
  username: string;
  level: number;
  totalGames: number;
  wins: number;
  totalScore: number;
  bestScore: number;
  winRate: number;
}

export interface Testimonial {
  id: number;
  name: string;
  rating: number;
  comment?: string;
  createdAt: string;
  isPinned: boolean;
  isPublished: boolean;
  user?: GameUserStats | null;
  // 데이터베이스 필드 (스네이크케이스)
  created_at?: Date;
  is_pinned?: boolean;
  is_published?: boolean;
}

export interface TestimonialForm {
  rating: number;
  comment?: string;
}

// ========================================
// 게시판 시스템 타입
// ========================================

export type PostCategory = 'QUESTION' | 'GUIDE' | 'GENERAL';
export type post_category = PostCategory; // DB 호환성을 위한 별칭

export interface Post {
  id: number;
  title: string;
  content: string;
  category: PostCategory;
  authorId: number;
  author: {
    username: string;
    displayName?: string;
    level: number;
  };
  isHidden: boolean;
  isDeleted: boolean;
  viewCount: number;
  createdAt: string;
  updatedAt: string;
  // 데이터베이스 필드 (스네이크케이스)
  author_id?: number;
  is_hidden?: boolean;
  is_deleted?: boolean;
  view_count?: number;
  created_at?: Date;
  updated_at?: Date;
}

export interface PostForm {
  title: string;
  content: string;
  category: PostCategory;
}

export interface PostListQuery {
  category?: PostCategory;
  page?: number;
  limit?: number;
  search?: string;
}

export interface PostStats {
  totalPosts: number;
  questionCount: number;
  guideCount: number;
  generalCount: number;
}