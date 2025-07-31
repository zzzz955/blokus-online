export interface Announcement {
  id: number;
  title: string;
  content: string;
  author: string;
  createdAt: string;
  updatedAt: string;
  isPinned: boolean;
  isPublished: boolean;
}

export interface PatchNote {
  id: number;
  version: string;
  title: string;
  content: string;
  releaseDate: string;
  downloadUrl?: string;
  createdAt: string;
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