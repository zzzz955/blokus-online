import { NextRequest, NextResponse } from 'next/server';
import { PrismaClient } from '@prisma/client';
import { getAdminFromRequest, requireAdmin } from '@/lib/server/admin-auth';

const prisma = new PrismaClient();

// API 라우트가 빌드 타임에 실행되지 않도록 설정
export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

/**
 * 관리자 대시보드 통계 조회
 */
export async function GET(request: NextRequest) {
  try {
    const admin = getAdminFromRequest(request);
    if (!requireAdmin()(admin)) {
      return NextResponse.json({
        success: false,
        error: '관리자 권한이 필요합니다.'
      }, { status: 401 });
    }

    // 오늘 날짜 범위 설정 (자정부터 다음 자정까지)
    const today = new Date();
    const startOfDay = new Date(today.getFullYear(), today.getMonth(), today.getDate());
    const endOfDay = new Date(today.getFullYear(), today.getMonth(), today.getDate() + 1);

    // 병렬로 모든 통계 조회
    const [
      // 공지사항 통계
      totalAnnouncements,
      publishedAnnouncements,
      pinnedAnnouncements,
      
      // 패치노트 통계
      totalPatchNotes,
      latestPatchNote,
      
      // 지원 티켓 통계 (아직 구현되지 않아서 기본값)
      
      // 후기 통계
      totalTestimonials,
      todayTestimonials,
      
      // 게시글 통계
      totalPosts,
      todayPosts,
      hiddenPosts,
      deletedPosts,
      
      // 댓글 통계 (오늘)
      todayComments,
      todayReplies
    ] = await Promise.all([
      // 공지사항
      prisma.announcement.count(),
      prisma.announcement.count({ where: { is_published: true } }),
      prisma.announcement.count({ where: { is_pinned: true } }),
      
      // 패치노트
      prisma.patchNote.count(),
      prisma.patchNote.findFirst({
        orderBy: { release_date: 'desc' },
        select: { version: true }
      }),
      
      // 후기
      prisma.testimonial.count(),
      prisma.testimonial.count({
        where: {
          created_at: {
            gte: startOfDay,
            lt: endOfDay
          }
        }
      }),
      
      // 게시글
      prisma.post.count({ where: { is_deleted: false } }),
      prisma.post.count({
        where: {
          is_deleted: false,
          created_at: {
            gte: startOfDay,
            lt: endOfDay
          }
        }
      }),
      prisma.post.count({ where: { is_hidden: true, is_deleted: false } }),
      prisma.post.count({ where: { is_deleted: true } }),
      
      // 댓글 (오늘)
      prisma.comment.count({
        where: {
          created_at: {
            gte: startOfDay,
            lt: endOfDay
          }
        }
      }),
      prisma.reply.count({
        where: {
          created_at: {
            gte: startOfDay,
            lt: endOfDay
          }
        }
      })
    ]);

    const stats = {
      announcements: {
        total: totalAnnouncements,
        published: publishedAnnouncements,
        pinned: pinnedAnnouncements
      },
      patchNotes: {
        total: totalPatchNotes,
        latest: latestPatchNote?.version || '없음'
      },
      supportTickets: {
        pending: 0, // 아직 구현되지 않음
        answered: 0, // 아직 구현되지 않음
        total: 0 // 아직 구현되지 않음
      },
      testimonials: {
        total: totalTestimonials,
        today: todayTestimonials
      },
      posts: {
        total: totalPosts,
        today: todayPosts,
        hidden: hiddenPosts,
        deleted: deletedPosts
      },
      comments: {
        today: todayComments + todayReplies // 댓글 + 대댓글
      }
    };

    return NextResponse.json({
      success: true,
      data: stats
    });

  } catch (error) {
    console.error('대시보드 통계 조회 오류:', error);
    return NextResponse.json({
      success: false,
      error: '서버 오류가 발생했습니다.'
    }, { status: 500 });
  }
}