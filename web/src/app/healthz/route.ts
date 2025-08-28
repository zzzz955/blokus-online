// Next.js 13/14 App Router
export const dynamic = 'force-static'; // DB 의존 피하기
export async function GET() {
  return new Response('ok', { status: 200 });
}
