'use client';

import Layout from '@/components/layout/Layout';
import Card, { CardHeader, CardTitle, CardContent } from '@/components/ui/Card';
import ContactForm from '@/components/forms/ContactForm';
import { MessageCircle, Clock, Shield, HelpCircle, Mail, Phone } from 'lucide-react';

export default function SupportPage() {
  const faqItems = [
    {
      question: '게임이 실행되지 않아요',
      answer: 'Windows 10 이상에서 실행 가능하며, Visual C++ 재배포 패키지가 필요할 수 있습니다. 공식 홈페이지에서 최신 클라이언트를 다운로드해 주세요.',
    },
    {
      question: '온라인 연결이 안 돼요',
      answer: '방화벽 설정을 확인하고, 안정적인 인터넷 연결 상태인지 확인해 주세요. 서버 점검 중일 수도 있으니 공지사항을 확인해 보세요.',
    },
    {
      question: '게임 규칙을 모르겠어요',
      answer: '게임 가이드 페이지에서 상세한 규칙과 전략을 확인할 수 있습니다. 초보자도 쉽게 따라할 수 있는 단계별 가이드를 제공합니다.',
    },
    {
      question: '랭킹이 제대로 반영되지 않아요',
      answer: '게임 종료 후 약간의 지연이 있을 수 있습니다. 지속적으로 문제가 발생한다면 아래 문의 양식을 통해 연락 주세요.',
    },
    {
      question: '계정 관련 문제가 있어요',
      answer: '게임 내에서 직접 계정 관리가 가능합니다. 비밀번호 변경이나 기타 계정 문제는 게임 클라이언트 내 설정에서 해결할 수 있습니다.',
    },
    {
      question: '버그를 발견했어요',
      answer: '버그 제보는 언제나 환영합니다! 아래 문의 양식에 상세한 상황 설명과 함께 제보해 주시면 빠르게 수정하겠습니다.',
    },
  ];

  const supportInfo = [
    {
      icon: <Clock className="w-6 h-6 text-primary-400" />,
      title: '응답 시간',
      description: '평균 24시간 이내 답변',
    },
    {
      icon: <Shield className="w-6 h-6 text-green-400" />,
      title: '개인정보 보호',
      description: '문의 내용은 안전하게 보호됩니다',
    },
    {
      icon: <MessageCircle className="w-6 h-6 text-secondary-400" />,
      title: '친절한 지원',
      description: '전문 지원팀이 도움을 드립니다',
    },
  ];

  return (
    <Layout>
      <div className="py-12">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          {/* Header */}
          <div className="text-center mb-16">
            <h1 className="text-4xl md:text-5xl font-bold text-white mb-6">
              고객지원
            </h1>
            <p className="text-xl text-gray-400 max-w-3xl mx-auto">
              문제가 발생했거나 궁금한 점이 있으신가요? 언제든지 도움을 요청해 주세요.
            </p>
          </div>

          <div className="grid grid-cols-1 lg:grid-cols-3 gap-8 mb-16">
            {/* 지원 정보 */}
            <div className="lg:col-span-1">
              <Card className="mb-8">
                <CardHeader>
                  <CardTitle className="flex items-center space-x-2">
                    <HelpCircle className="w-5 h-5 text-primary-400" />
                    <span>지원 안내</span>
                  </CardTitle>
                </CardHeader>
                <CardContent>
                  <div className="space-y-6">
                    {supportInfo.map((info, index) => (
                      <div key={index} className="flex items-start space-x-3">
                        <div className="flex-shrink-0">
                          {info.icon}
                        </div>
                        <div>
                          <h3 className="text-white font-semibold mb-1">
                            {info.title}
                          </h3>
                          <p className="text-gray-300 text-sm">
                            {info.description}
                          </p>
                        </div>
                      </div>
                    ))}
                  </div>
                </CardContent>
              </Card>

              {/* 연락처 정보 */}
              <Card>
                <CardHeader>
                  <CardTitle>다른 연락 방법</CardTitle>
                </CardHeader>
                <CardContent>
                  <div className="space-y-4">
                    <div className="flex items-center space-x-3">
                      <Mail className="w-5 h-5 text-primary-400" />
                      <div>
                        <p className="text-white font-medium">이메일</p>
                        <p className="text-gray-300 text-sm">support@blokus-online.com</p>
                      </div>
                    </div>
                    <div className="flex items-center space-x-3">
                      <MessageCircle className="w-5 h-5 text-secondary-400" />
                      <div>
                        <p className="text-white font-medium">Discord</p>
                        <p className="text-gray-300 text-sm">공식 Discord 서버 참여</p>
                      </div>
                    </div>
                  </div>
                </CardContent>
              </Card>
            </div>

            {/* 문의 양식 */}
            <div className="lg:col-span-2">
              <Card>
                <CardHeader>
                  <CardTitle>문의하기</CardTitle>
                </CardHeader>
                <CardContent>
                  <ContactForm onSuccess={() => {
                    // 성공 후 처리 (예: 성공 메시지 표시)
                  }} />
                </CardContent>
              </Card>
            </div>
          </div>

          {/* FAQ 섹션 */}
          <section>
            <div className="text-center mb-12">
              <h2 className="text-3xl font-bold text-white mb-4">
                자주 묻는 질문
              </h2>
              <p className="text-gray-400">
                일반적인 문제들에 대한 해답을 찾아보세요.
              </p>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              {faqItems.map((faq, index) => (
                <Card key={index}>
                  <CardContent>
                    <h3 className="text-lg font-semibold text-white mb-3">
                      Q. {faq.question}
                    </h3>
                    <p className="text-gray-300 leading-relaxed">
                      A. {faq.answer}
                    </p>
                  </CardContent>
                </Card>
              ))}
            </div>
          </section>

          {/* 추가 도움말 */}
          <section className="mt-16">
            <Card className="bg-gradient-to-r from-primary-900/20 to-secondary-900/20 border-primary-500/30">
              <CardContent className="text-center py-12">
                <h3 className="text-2xl font-bold text-white mb-4">
                  더 많은 도움이 필요하신가요?
                </h3>
                <p className="text-gray-300 mb-6 max-w-2xl mx-auto">
                  게임 가이드에서 상세한 플레이 방법을 확인하거나, 
                  공지사항에서 최신 업데이트 정보를 살펴보세요.
                </p>
                <div className="flex flex-col sm:flex-row gap-4 justify-center">
                  <a
                    href="/guide"
                    className="btn-primary inline-flex items-center justify-center"
                  >
                    게임 가이드 보기
                  </a>
                  <a
                    href="/announcements"
                    className="btn-secondary inline-flex items-center justify-center"
                  >
                    공지사항 확인
                  </a>
                </div>
              </CardContent>
            </Card>
          </section>
        </div>
      </div>
    </Layout>
  );
}