import Link from 'next/link';
import { Download, Users, Trophy, Shield, Gamepad2, Star } from 'lucide-react';
import Layout from '@/components/layout/Layout';
import Card, { CardHeader, CardTitle, CardContent } from '@/components/ui/Card';
import Button from '@/components/ui/Button';

export default function HomePage() {
  const features = [
    {
      icon: <Users className="w-8 h-8 text-primary-400" />,
      title: '멀티플레이어',
      description: '최대 4명까지 함께 즐기는 온라인 대전',
    },
    {
      icon: <Trophy className="w-8 h-8 text-secondary-400" />,
      title: '랭킹 시스템',
      description: '실력을 겨루고 순위를 올려보세요',
    },
    {
      icon: <Shield className="w-8 h-8 text-green-400" />,
      title: '안정적인 서버',
      description: '끊김없는 게임 환경을 제공합니다',
    },
    {
      icon: <Gamepad2 className="w-8 h-8 text-purple-400" />,
      title: '직관적인 UI',
      description: '누구나 쉽게 배우고 즐길 수 있습니다',
    },
  ];

  const testimonials = [
    {
      name: '김민수',
      rating: 5,
      comment: '정말 재미있는 게임입니다! 친구들과 함께 하면 시간 가는 줄 모르네요.',
    },
    {
      name: '이서연',
      rating: 5,
      comment: '전략적 사고가 필요한 게임이라 더욱 흥미롭습니다. UI도 깔끔하고 좋아요.',
    },
    {
      name: '박지훈',
      rating: 4,
      comment: '온라인으로 블로커스를 즐길 수 있어서 좋습니다. 랭킹 시스템도 재미있어요.',
    },
  ];

  return (
    <Layout>
      {/* Hero Section */}
      <section className="hero-gradient relative overflow-hidden">
        <div className="absolute inset-0 bg-black/20"></div>
        <div className="relative max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-24 lg:py-32">
          <div className="text-center">
            <h1 className="text-4xl md:text-6xl font-bold text-white mb-6">
              <span className="block">전략적 사고와</span>
              <span className="block gaming-text">창의성이 만나는</span>
              <span className="block">블로커스 온라인</span>
            </h1>
            <p className="text-xl text-gray-200 mb-8 max-w-3xl mx-auto">
              친구들과 함께 즐기는 온라인 블로커스 게임입니다. 
              전략적 사고력을 기르고 창의적인 플레이를 통해 승리를 쟁취하세요!
            </p>
            <div className="flex flex-col sm:flex-row gap-4 justify-center">
              <Link href="/download">
                <Button size="lg" className="flex items-center space-x-2">
                  <Download size={20} />
                  <span>무료 다운로드</span>
                </Button>
              </Link>
              <Link href="/guide">
                <Button variant="outline" size="lg">
                  게임 가이드 보기
                </Button>
              </Link>
            </div>
          </div>
        </div>
      </section>

      {/* Features Section */}
      <section className="py-20 bg-gray-900">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="text-center mb-16">
            <h2 className="text-3xl md:text-4xl font-bold text-white mb-4">
              게임 특징
            </h2>
            <p className="text-xl text-gray-400 max-w-3xl mx-auto">
              블로커스 온라인만의 특별한 기능들을 만나보세요
            </p>
          </div>
          
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-8">
            {features.map((feature, index) => (
              <Card key={index} hover className="text-center">
                <CardContent>
                  <div className="flex justify-center mb-4">
                    {feature.icon}
                  </div>
                  <h3 className="text-xl font-semibold text-white mb-2">
                    {feature.title}
                  </h3>
                  <p className="text-gray-400">
                    {feature.description}
                  </p>
                </CardContent>
              </Card>
            ))}
          </div>
        </div>
      </section>

      {/* Testimonials Section */}
      <section className="py-20">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="text-center mb-16">
            <h2 className="text-3xl md:text-4xl font-bold text-white mb-4">
              플레이어 후기
            </h2>
            <p className="text-xl text-gray-400">
              블로커스 온라인을 즐기고 있는 플레이어들의 생생한 후기
            </p>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-3 gap-8">
            {testimonials.map((testimonial, index) => (
              <Card key={index}>
                <CardContent>
                  <div className="flex items-center mb-4">
                    {[...Array(5)].map((_, i) => (
                      <Star
                        key={i}
                        size={16}
                        className={`${
                          i < testimonial.rating
                            ? 'text-yellow-400 fill-current'
                            : 'text-gray-600'
                        }`}
                      />
                    ))}
                  </div>
                  <p className="text-gray-300 mb-4">{testimonial.comment}</p>
                  <p className="text-white font-semibold">- {testimonial.name}</p>
                </CardContent>
              </Card>
            ))}
          </div>
        </div>
      </section>

      {/* CTA Section */}
      <section className="py-20 bg-gradient-to-r from-primary-600 to-secondary-600">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 text-center">
          <h2 className="text-3xl md:text-4xl font-bold text-white mb-4">
            지금 바로 시작하세요!
          </h2>
          <p className="text-xl text-gray-200 mb-8 max-w-2xl mx-auto">
            무료로 다운로드하고 친구들과 함께 블로커스 온라인의 재미를 경험해보세요.
          </p>
          <Link href="/download">
            <Button size="lg" variant="secondary" className="text-primary-700">
              <Download size={20} className="mr-2" />
              게임 다운로드
            </Button>
          </Link>
        </div>
      </section>
    </Layout>
  );
}