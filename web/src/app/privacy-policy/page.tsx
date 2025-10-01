import { Metadata } from 'next';

export const metadata: Metadata = {
  title: '개인정보처리방침 | 블로블로',
  description: '블로블로 게임의 개인정보처리방침을 확인하세요.',
};

export default function PrivacyPolicyPage() {
  return (
    <div className="min-h-screen bg-dark-bg py-12">
      <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="bg-dark-card border border-dark-border rounded-lg p-8">
          <h1 className="text-3xl font-bold text-white mb-8">개인정보처리방침</h1>

          <div className="space-y-8 text-gray-300">
            {/* 제1조 */}
            <section>
              <h2 className="text-xl font-semibold text-white mb-4">제1조 (목적)</h2>
              <p className="leading-relaxed">
                블로블로(이하 &quot;회사&quot;)는 이용자의 개인정보를 중요시하며, 「개인정보 보호법」, 「정보통신망 이용촉진 및 정보보호 등에 관한 법률」 등 관련 법령을 준수하고 있습니다.
                회사는 본 개인정보처리방침을 통하여 이용자가 제공하는 개인정보가 어떠한 용도와 방식으로 이용되고 있으며, 개인정보 보호를 위해 어떠한 조치가 취해지고 있는지 알려드립니다.
              </p>
            </section>

            {/* 제2조 */}
            <section>
              <h2 className="text-xl font-semibold text-white mb-4">제2조 (수집하는 개인정보의 항목 및 수집방법)</h2>

              <div className="space-y-4">
                <div>
                  <h3 className="text-lg font-medium text-white mb-2">1. 수집하는 개인정보 항목</h3>
                  <p className="mb-2">회사는 서비스 제공을 위해 다음과 같은 개인정보를 수집하고 있습니다:</p>
                  <ul className="list-disc list-inside ml-4 space-y-1">
                    <li><strong>필수 항목:</strong> 이메일 주소, 사용자명(닉네임), 비밀번호</li>
                    <li><strong>선택 항목:</strong> 프로필 이미지</li>
                    <li><strong>자동 수집 항목:</strong> IP 주소, 쿠키, 서비스 이용 기록, 접속 로그, 기기 정보</li>
                  </ul>
                </div>

                <div>
                  <h3 className="text-lg font-medium text-white mb-2">2. 개인정보 수집방법</h3>
                  <ul className="list-disc list-inside ml-4 space-y-1">
                    <li>웹사이트 회원가입 및 서비스 이용 과정에서 이용자가 직접 입력</li>
                    <li>게임 플레이 및 커뮤니티 활동 과정에서 자동 수집</li>
                    <li>로그 분석 프로그램을 통한 자동 생성 정보 수집</li>
                  </ul>
                </div>
              </div>
            </section>

            {/* 제3조 */}
            <section>
              <h2 className="text-xl font-semibold text-white mb-4">제3조 (개인정보의 수집 및 이용목적)</h2>
              <p className="mb-2">회사는 수집한 개인정보를 다음의 목적을 위해 활용합니다:</p>
              <ul className="list-disc list-inside ml-4 space-y-1">
                <li><strong>회원 관리:</strong> 회원제 서비스 이용에 따른 본인확인, 개인 식별, 불량회원의 부정 이용 방지와 비인가 사용 방지, 가입 의사 확인, 분쟁 조정을 위한 기록보존</li>
                <li><strong>서비스 제공:</strong> 게임 서비스 제공, 게임 기록 저장 및 관리, 랭킹 시스템 운영, 커뮤니티 서비스 제공</li>
                <li><strong>서비스 개선:</strong> 신규 서비스 개발 및 맞춤 서비스 제공, 통계학적 특성에 따른 서비스 제공 및 광고 게재, 서비스의 유효성 확인, 이벤트 및 광고성 정보 제공</li>
                <li><strong>고객 지원:</strong> 문의사항 및 불만 처리, 공지사항 전달</li>
              </ul>
            </section>

            {/* 제4조 */}
            <section>
              <h2 className="text-xl font-semibold text-white mb-4">제4조 (개인정보의 보유 및 이용기간)</h2>
              <div className="space-y-2">
                <p>
                  회사는 이용자의 개인정보를 원칙적으로 개인정보의 수집 및 이용목적이 달성되면 지체 없이 파기합니다.
                  단, 다음의 정보에 대해서는 아래의 이유로 명시한 기간 동안 보존합니다.
                </p>

                <div className="ml-4 space-y-2">
                  <div>
                    <h4 className="font-medium text-white">회원 탈퇴 시</h4>
                    <ul className="list-disc list-inside ml-4">
                      <li>보존 항목: 이메일, 사용자명, 서비스 이용 기록</li>
                      <li>보존 근거: 부정 이용 방지 및 서비스 품질 개선</li>
                      <li>보존 기간: 탈퇴일로부터 30일</li>
                    </ul>
                  </div>

                  <div>
                    <h4 className="font-medium text-white">관련 법령에 따른 보존</h4>
                    <ul className="list-disc list-inside ml-4">
                      <li>계약 또는 청약철회 등에 관한 기록: 5년 (전자상거래 등에서의 소비자보호에 관한 법률)</li>
                      <li>대금결제 및 재화 등의 공급에 관한 기록: 5년 (전자상거래 등에서의 소비자보호에 관한 법률)</li>
                      <li>소비자의 불만 또는 분쟁처리에 관한 기록: 3년 (전자상거래 등에서의 소비자보호에 관한 법률)</li>
                      <li>웹사이트 방문 기록: 3개월 (통신비밀보호법)</li>
                    </ul>
                  </div>
                </div>
              </div>
            </section>

            {/* 제5조 */}
            <section>
              <h2 className="text-xl font-semibold text-white mb-4">제5조 (개인정보의 파기절차 및 방법)</h2>
              <div className="space-y-2">
                <p>회사는 원칙적으로 개인정보 수집 및 이용목적이 달성된 후에는 해당 정보를 지체 없이 파기합니다.</p>

                <div className="ml-4 space-y-2">
                  <div>
                    <h4 className="font-medium text-white">파기절차</h4>
                    <p>
                      이용자가 서비스 이용 등을 위해 입력한 정보는 목적이 달성된 후 별도의 DB로 옮겨져 내부 방침 및 기타 관련 법령에 의한 정보보호 사유에 따라
                      일정 기간 저장된 후 파기됩니다.
                    </p>
                  </div>

                  <div>
                    <h4 className="font-medium text-white">파기방법</h4>
                    <ul className="list-disc list-inside ml-4">
                      <li>전자적 파일 형태의 정보: 기록을 재생할 수 없는 기술적 방법을 사용하여 삭제</li>
                      <li>종이에 출력된 개인정보: 분쇄기로 분쇄하거나 소각</li>
                    </ul>
                  </div>
                </div>
              </div>
            </section>

            {/* 제6조 */}
            <section>
              <h2 className="text-xl font-semibold text-white mb-4">제6조 (개인정보의 제3자 제공)</h2>
              <p>
                회사는 이용자의 개인정보를 원칙적으로 외부에 제공하지 않습니다.
                다만, 아래의 경우에는 예외로 합니다:
              </p>
              <ul className="list-disc list-inside ml-4 space-y-1">
                <li>이용자가 사전에 동의한 경우</li>
                <li>법령의 규정에 의거하거나, 수사 목적으로 법령에 정해진 절차와 방법에 따라 수사기관의 요구가 있는 경우</li>
              </ul>
            </section>

            {/* 제7조 */}
            <section>
              <h2 className="text-xl font-semibold text-white mb-4">제7조 (개인정보의 처리위탁)</h2>
              <p>
                회사는 현재 개인정보 처리업무를 외부 업체에 위탁하고 있지 않습니다.
                향후 개인정보 처리업무를 위탁하게 될 경우 관련 내용을 본 방침에 명시하고,
                위탁계약 시 개인정보 보호를 위한 적절한 조치를 취하겠습니다.
              </p>
            </section>

            {/* 제8조 */}
            <section>
              <h2 className="text-xl font-semibold text-white mb-4">제8조 (이용자 및 법정대리인의 권리와 그 행사방법)</h2>
              <div className="space-y-2">
                <p>이용자 및 법정대리인은 언제든지 다음과 같은 개인정보 보호 관련 권리를 행사할 수 있습니다:</p>
                <ul className="list-disc list-inside ml-4 space-y-1">
                  <li>개인정보 열람 요구</li>
                  <li>개인정보에 오류가 있는 경우 정정 요구</li>
                  <li>개인정보 삭제 요구</li>
                  <li>개인정보 처리 정지 요구</li>
                </ul>
                <p className="mt-2">
                  위 권리 행사는 회사에 대해 서면, 전자우편을 통하여 하실 수 있으며,
                  회사는 이에 대해 지체 없이 조치하겠습니다.
                </p>
              </div>
            </section>

            {/* 제9조 */}
            <section>
              <h2 className="text-xl font-semibold text-white mb-4">제9조 (개인정보 자동 수집 장치의 설치·운영 및 거부에 관한 사항)</h2>
              <div className="space-y-2">
                <p>
                  회사는 이용자에게 개별적인 맞춤서비스를 제공하기 위해 이용 정보를 저장하고 수시로 불러오는
                  &apos;쿠키(cookie)&apos;를 사용합니다.
                </p>

                <div className="ml-4 space-y-2">
                  <div>
                    <h4 className="font-medium text-white">쿠키의 사용 목적</h4>
                    <ul className="list-disc list-inside ml-4">
                      <li>이용자의 로그인 상태 유지</li>
                      <li>이용자의 서비스 이용 패턴 분석</li>
                      <li>맞춤형 서비스 제공</li>
                    </ul>
                  </div>

                  <div>
                    <h4 className="font-medium text-white">쿠키 설정 거부 방법</h4>
                    <p>
                      이용자는 웹 브라우저의 옵션을 설정함으로써 모든 쿠키를 허용하거나,
                      쿠키가 저장될 때마다 확인을 거치거나, 모든 쿠키의 저장을 거부할 수 있습니다.
                      다만, 쿠키 설치를 거부할 경우 서비스 이용에 어려움이 있을 수 있습니다.
                    </p>
                  </div>
                </div>
              </div>
            </section>

            {/* 제10조 */}
            <section>
              <h2 className="text-xl font-semibold text-white mb-4">제10조 (개인정보 보호책임자)</h2>
              <div className="space-y-2">
                <p>
                  회사는 개인정보 처리에 관한 업무를 총괄해서 책임지고,
                  개인정보 처리와 관련한 이용자의 불만처리 및 피해구제 등을 위하여 아래와 같이 개인정보 보호책임자를 지정하고 있습니다.
                </p>

                <div className="bg-dark-bg p-4 rounded border border-dark-border mt-4">
                  <h4 className="font-medium text-white mb-2">개인정보 보호책임자</h4>
                  <ul className="space-y-1">
                    <li>이메일: zzzzz955@gmail.com</li>
                    <li>문의: GitHub Issues (https://github.com/zzzz955/blokus-online/issues)</li>
                  </ul>
                </div>

                <p className="mt-4">
                  이용자는 회사의 서비스를 이용하시면서 발생한 모든 개인정보 보호 관련 문의, 불만처리, 피해구제 등에 관한 사항을
                  개인정보 보호책임자에게 문의하실 수 있습니다. 회사는 이용자의 문의에 대해 지체 없이 답변 및 처리해드릴 것입니다.
                </p>
              </div>
            </section>

            {/* 제11조 */}
            <section>
              <h2 className="text-xl font-semibold text-white mb-4">제11조 (개인정보 처리방침 변경)</h2>
              <p>
                이 개인정보처리방침은 시행일로부터 적용되며, 법령 및 방침에 따른 변경내용의 추가, 삭제 및 정정이 있는 경우에는
                변경사항의 시행 7일 전부터 공지사항을 통하여 고지할 것입니다.
                다만, 개인정보의 수집 및 활용, 제3자 제공 등과 같이 이용자 권리의 중요한 변경이 있을 경우에는
                최소 30일 전에 고지합니다.
              </p>
            </section>

            {/* 부칙 */}
            <section className="border-t border-dark-border pt-6">
              <h2 className="text-xl font-semibold text-white mb-4">부칙</h2>
              <p>본 방침은 2025년 1월 1일부터 시행됩니다.</p>
            </section>
          </div>
        </div>
      </div>
    </div>
  );
}
