// ========================================
// 내가 작성한 콘텐츠 조회 API 엔드포인트
// ========================================
// GET /api/user/my-content?type=posts|testimonials|tickets - 내가 작성한 콘텐츠 조회
// ========================================

import { NextRequest, NextResponse } from 'next/server';
import { getServerSession } from 'next-auth/next';
import { prisma } from '@/lib/prisma';
import { ApiResponse } from '@/types';
import { authOptions } from '@/lib/auth';

interface MyPost {
  id: number;
  title: string;
  category: string;
  view_count: number;
  is_hidden: boolean;
  created_at: Date;
  updated_at: Date;
}

interface MyTestimonial {
  id: number;
  rating: number;
  comment?: string;
  is_pinned: boolean;
  is_published: boolean;
  created_at: Date;
}

interface MySupportTicket {
  id: number;
  subject: string;
  status: string;
  admin_reply?: string;
  created_at: Date;
  replied_at?: Date;
}

type ContentType = 'posts' | 'testimonials' | 'tickets';

// GET /api/user/my-content?type=posts|testimonials|tickets
export async function GET(request: NextRequest) {
  try {
    const session = await getServerSession(authOptions);
    
    if (!session?.user?.email) {
      const response: ApiResponse<null> = {
        success: false,
        error: '로그인이 필요합니다.'
      };
      return NextResponse.json(response, { status: 401 });
    }

    const { searchParams } = new URL(request.url);
    const type = searchParams.get('type') as ContentType;

    if (!type || !['posts', 'testimonials', 'tickets'].includes(type)) {
      const response: ApiResponse<null> = {
        success: false,
        error: '유효한 콘텐츠 타입을 지정해주세요. (posts, testimonials, tickets)'
      };
      return NextResponse.json(response, { status: 400 });
    }

    // 사용자 확인
    const user = await prisma.user.findUnique({
      where: { email: session.user.email },
      select: { user_id: true }
    });

    if (!user) {
      const response: ApiResponse<null> = {
        success: false,
        error: '사용자 정보를 찾을 수 없습니다.'
      };
      return NextResponse.json(response, { status: 404 });
    }

    let result: any = [];

    switch (type) {
      case 'posts':
        const posts = await prisma.post.findMany({
          where: {
            author_id: user.user_id,
            is_deleted: false
          },
          select: {
            id: true,
            title: true,
            category: true,
            view_count: true,
            is_hidden: true,
            created_at: true,
            updated_at: true
          },
          orderBy: { created_at: 'desc' }
        });
        
        result = posts.map(post => ({
          ...post,
          category: post.category.toLowerCase()
        }));
        break;

      case 'testimonials':
        result = await prisma.testimonial.findMany({
          where: {
            userId: user.user_id
          },
          select: {
            id: true,
            rating: true,
            comment: true,
            is_pinned: true,
            is_published: true,
            created_at: true
          },
          orderBy: { created_at: 'desc' }
        });
        break;

      case 'tickets':
        result = await prisma.supportTicket.findMany({
          where: {
            userId: user.user_id
          },
          select: {
            id: true,
            subject: true,
            status: true,
            admin_reply: true,
            created_at: true,
            replied_at: true
          },
          orderBy: { created_at: 'desc' }
        });
        break;
    }

    const response: ApiResponse<MyPost[] | MyTestimonial[] | MySupportTicket[]> = {
      success: true,
      data: result
    };
    
    return NextResponse.json(response);
  } catch (error) {
    console.error('내 콘텐츠 조회 오류:', error);
    
    const response: ApiResponse<null> = {
      success: false,
      error: '콘텐츠를 불러오는데 실패했습니다.'
    };
    
    return NextResponse.json(response, { status: 500 });
  }
}