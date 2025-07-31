import Layout from '@/components/layout/Layout';
import Card, { CardHeader, CardTitle, CardContent } from '@/components/ui/Card';
import { Users, Target, Trophy, Lightbulb, GamepadIcon, Shield } from 'lucide-react';

export default function GuidePage() {
  const gameRules = [
    {
      title: '게임 목표',
      description: '자신의 21개 블록을 모두 보드에 배치하는 것이 목표입니다.',
      icon: <Target className="w-6 h-6 text-primary-400" />,
    },
    {
      title: '배치 규칙',
      description: '첫 번째 블록은 자신의 코너에서 시작하며, 이후 블록들은 모서리끼리만 연결됩니다.',
      icon: <GamepadIcon className="w-6 h-6 text-secondary-400" />,
    },
    {
      title: '승리 조건',
      description: '모든 블록을 배치하거나, 더 이상 배치할 수 없을 때 남은 블록이 가장 적은 플레이어가 승리합니다.',
      icon: <Trophy className="w-6 h-6 text-yellow-400" />,
    },
  ];

  const strategies = [
    {
      title: '초보자 전략',
      tips: [
        '큰 블록부터 우선적으로 배치하세요',
        '코너 지역을 먼저 확보하는 것이 유리합니다',
        '상대방의 확장 경로를 차단해보세요',
        '작은 블록들로 틈새를 메우는 연습을 하세요',
      ],
    },
    {
      title: '중급자 전략',
      tips: [
        '여러 방향으로 확장 가능한 형태를 만드세요',
        '상대방의 블록 배치 패턴을 관찰하세요',
        '막다른 길을 만들지 않도록 주의하세요',
        '블록의 연결점을 최대한 활용하세요',
      ],
    },
    {
      title: '고급자 전략',
      tips: [
        '심리전을 활용해 상대를 오판하게 만드세요',
        '블록의 우선순위를 상황에 맞게 조정하세요',
        '상대방의 승부수를 미리 예측하고 대비하세요',
        '엔드게임에서의 블록 최적화를 계산하세요',
      ],
    },
  ];

  const gameFlow = [
    { step: 1, title: '게임 시작', description: '각 플레이어는 21개의 블록을 받습니다.' },
    { step: 2, title: '첫 배치', description: '첫 번째 블록을 자신의 시작 코너에 배치합니다.' },
    { step: 3, title: '턴 진행', description: '시계방향으로 돌아가며 블록을 배치합니다.' },
    { step: 4, title: '게임 종료', description: '더 이상 배치할 블록이 없으면 게임이 종료됩니다.' },
  ];

  return (
    <Layout>
      <div className="py-12">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          {/* Header */}
          <div className="text-center mb-16">
            <h1 className="text-4xl md:text-5xl font-bold text-white mb-6">
              게임 가이드
            </h1>
            <p className="text-xl text-gray-400 max-w-3xl mx-auto">
              블로커스의 규칙부터 고급 전략까지, 승리를 위한 모든 것을 알려드립니다.
            </p>
          </div>

          {/* 게임 규칙 */}
          <section className="mb-20">
            <h2 className="text-3xl font-bold text-white mb-8 text-center">기본 규칙</h2>
            <div className="grid grid-cols-1 md:grid-cols-3 gap-8">
              {gameRules.map((rule, index) => (
                <Card key={index} hover>
                  <CardContent className="text-center">
                    <div className="flex justify-center mb-4">
                      {rule.icon}
                    </div>
                    <h3 className="text-xl font-semibold text-white mb-3">
                      {rule.title}
                    </h3>
                    <p className="text-gray-300">
                      {rule.description}
                    </p>
                  </CardContent>
                </Card>
              ))}
            </div>
          </section>

          {/* 게임 진행 */}
          <section className="mb-20">
            <h2 className="text-3xl font-bold text-white mb-8 text-center">게임 진행</h2>
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
              {gameFlow.map((flow, index) => (
                <Card key={index}>
                  <CardContent className="text-center">
                    <div className="w-12 h-12 bg-primary-600 rounded-full flex items-center justify-center mx-auto mb-4">
                      <span className="text-white font-bold text-lg">{flow.step}</span>
                    </div>
                    <h3 className="text-lg font-semibold text-white mb-2">
                      {flow.title}
                    </h3>
                    <p className="text-gray-300 text-sm">
                      {flow.description}
                    </p>
                  </CardContent>
                </Card>
              ))}
            </div>
          </section>

          {/* 전략 가이드 */}
          <section className="mb-20">
            <h2 className="text-3xl font-bold text-white mb-8 text-center">전략 가이드</h2>
            <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
              {strategies.map((strategy, index) => (
                <Card key={index}>
                  <CardHeader>
                    <CardTitle className="flex items-center space-x-2">
                      <Lightbulb className="w-5 h-5 text-yellow-400" />
                      <span>{strategy.title}</span>
                    </CardTitle>
                  </CardHeader>
                  <CardContent>
                    <ul className="space-y-3">
                      {strategy.tips.map((tip, tipIndex) => (
                        <li key={tipIndex} className="flex items-start space-x-2">
                          <div className="w-2 h-2 bg-primary-400 rounded-full mt-2 flex-shrink-0"></div>
                          <span className="text-gray-300 text-sm">{tip}</span>
                        </li>
                      ))}
                    </ul>
                  </CardContent>
                </Card>
              ))}
            </div>
          </section>

          {/* 팁 & 주의사항 */}
          <section>
            <div className="grid grid-cols-1 lg:grid-cols-2 gap-8">
              <Card className="bg-green-900/20 border-green-500/30">
                <CardHeader>
                  <CardTitle className="text-green-400 flex items-center space-x-2">
                    <Shield className="w-5 h-5" />
                    <span>도움이 되는 팁</span>
                  </CardTitle>
                </CardHeader>
                <CardContent>
                  <ul className="space-y-2 text-green-300">
                    <li>• 블록의 모양을 미리 파악하고 계획을 세우세요</li>
                    <li>• 상대방의 움직임을 관찰하고 대응하세요</li>
                    <li>• 여러 경로를 열어두어 선택의 폭을 넓히세요</li>
                    <li>• 작은 블록들을 마지막에 활용할 계획을 세우세요</li>
                  </ul>
                </CardContent>
              </Card>

              <Card className="bg-red-900/20 border-red-500/30">
                <CardHeader>
                  <CardTitle className="text-red-400 flex items-center space-x-2">
                    <Users className="w-5 h-5" />
                    <span>주의사항</span>
                  </CardTitle>
                </CardHeader>
                <CardContent>
                  <ul className="space-y-2 text-red-300">
                    <li>• 블록은 면끼리 맞닿으면 안 됩니다</li>
                    <li>• 자신의 블록과만 모서리로 연결할 수 있습니다</li>
                    <li>• 시간 제한이 있으니 신중하되 빠르게 결정하세요</li>
                    <li>• 패스는 한 번만 가능하니 신중히 사용하세요</li>
                  </ul>
                </CardContent>
              </Card>
            </div>
          </section>
        </div>
      </div>
    </Layout>
  );
}