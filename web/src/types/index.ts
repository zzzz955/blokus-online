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

export interface Testimonial {
  id: number;
  name: string;
  rating: number;
  comment?: string;
  createdAt: string;
  isPinned: boolean;
  isPublished: boolean;
  // 데이터베이스 필드 (스네이크케이스)
  created_at?: Date;
  is_pinned?: boolean;
  is_published?: boolean;
}

export interface TestimonialForm {
  name: string;
  rating: number;
  comment?: string;
}