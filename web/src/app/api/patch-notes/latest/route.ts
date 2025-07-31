import { NextResponse } from 'next/server';
import { prisma } from '@/lib/prisma';
import { ApiResponse, PatchNote } from '@/types';

export async function GET() {
  try {
    const latestPatchNote = await prisma.patchNote.findFirst({
      orderBy: { releaseDate: 'desc' },
    });

    if (!latestPatchNote) {
      const response: ApiResponse = {
        success: false,
        error: '패치 노트를 찾을 수 없습니다.',
      };
      return NextResponse.json(response, { status: 404 });
    }

    const response: ApiResponse<PatchNote> = {
      success: true,
      data: latestPatchNote,
    };

    return NextResponse.json(response);
  } catch (error) {
    console.error('Get latest patch note error:', error);
    const response: ApiResponse = {
      success: false,
      error: '최신 패치 노트를 불러오는데 실패했습니다.',
    };
    return NextResponse.json(response, { status: 500 });
  }
}