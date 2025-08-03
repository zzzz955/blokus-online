import { NextRequest, NextResponse } from 'next/server';
import { prisma } from '@/lib/prisma';
import { ApiResponse, PatchNote } from '@/types';

// Force dynamic rendering for this API route
export const dynamic = 'force-dynamic';

export async function GET(
  request: NextRequest,
  { params }: { params: { version: string } }
) {
  try {
    const version = decodeURIComponent(params.version);
    
    const patchNote = await prisma.patchNote.findUnique({
      where: { version }
    });

    if (!patchNote) {
      return NextResponse.json({
        success: false,
        error: '패치 노트를 찾을 수 없습니다.'
      }, { status: 404 });
    }

    const response: ApiResponse<PatchNote> = {
      success: true,
      data: {
        ...patchNote,
        releaseDate: patchNote.release_date.toISOString(),
        createdAt: patchNote.created_at.toISOString(),
        downloadUrl: patchNote.download_url || undefined,
      },
    };

    return NextResponse.json(response);
  } catch (error) {
    console.error('Get patch note by version error:', error);
    const response: ApiResponse = {
      success: false,
      error: '패치 노트를 불러오는데 실패했습니다.',
    };
    return NextResponse.json(response, { status: 500 });
  }
}