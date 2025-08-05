'use client';

import { useState } from 'react';
import { useSession, signOut } from 'next-auth/react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { ArrowLeft, AlertTriangle, Trash2, CheckCircle } from 'lucide-react';

const DEACTIVATION_REASONS = [
  '더 이상 게임을 하지 않아요',
  '개인정보 보호를 위해서요',
  '다른 계정을 사용할 예정이에요',
  '서비스에 불만이 있어요',
  '기타'
];

export default function DeactivateAccountPage() {
  const { data: session, status } = useSession();
  const router = useRouter();
  const [confirmationText, setConfirmationText] = useState('');
  const [selectedReason, setSelectedReason] = useState('');
  const [customReason, setCustomReason] = useState('');
  const [showConfirmation, setShowConfirmation] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);

  if (status === 'loading') {
    return (
      <div className="min-h-screen bg-dark-bg flex items-center justify-center">
        <div className="animate-spin rounded-full h-32 w-32 border-b-2 border-primary-500"></div>
      </div>
    );
  }

  if (!session) {
    router.push('/auth/signin');
    return null;
  }

  if (success) {
    return (
      <div className="min-h-screen bg-dark-bg flex items-center justify-center">
        <div className="max-w-md w-full bg-dark-card border border-dark-border rounded-lg p-8 text-center">
          <CheckCircle size={64} className="text-green-400 mx-auto mb-4" />
          <h2 className="text-2xl font-bold text-white mb-4">계정 비활성화 완료</h2>
          <p className="text-gray-300 mb-6">
            계정이 성공적으로 비활성화되었습니다.<br />
            {session.user.oauth_provider && (
              <>언제든 {session.user.oauth_provider} 계정으로 다시 로그인하여 복구할 수 있습니다.</>
            )}
          </p>
          <button
            onClick={() => signOut({ callbackUrl: '/' })}
            className="btn-primary w-full"
          >
            홈으로 이동
          </button>
        </div>
      </div>
    );
  }

  const handleReasonChange = (reason: string) => {
    setSelectedReason(reason);
    if (reason !== '기타') {
      setCustomReason('');
    }
  };

  const handleDeactivate = async () => {
    if (confirmationText !== '계정 삭제') {
      setError('확인 텍스트가 올바르지 않습니다.');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const reason = selectedReason === '기타' ? customReason : selectedReason;
      
      const response = await fetch('/api/user/deactivate', {
        method: 'DELETE',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          confirmation_text: confirmationText,
          reason: reason || undefined
        })
      });

      const data = await response.json();

      if (data.success) {
        setSuccess(true);
      } else {
        setError(data.error || '계정 비활성화에 실패했습니다.');
      }
    } catch (err) {
      setError('네트워크 오류가 발생했습니다.');
    } finally {
      setLoading(false);
    }
  };

  if (showConfirmation) {
    return (
      <div className="min-h-screen bg-dark-bg">
        <div className="max-w-md mx-auto px-4 py-8">
          <div className="mb-8">
            <button
              onClick={() => setShowConfirmation(false)}
              className="inline-flex items-center text-gray-400 hover:text-white mb-4"
            >
              <ArrowLeft size={20} className="mr-2" />
              이전으로
            </button>
            <h1 className="text-3xl font-bold text-white mb-2">최종 확인</h1>
            <p className="text-gray-400">정말로 계정을 비활성화하시겠습니까?</p>
          </div>

          <div className="bg-red-500/10 border border-red-500/30 rounded-lg p-6 mb-6">
            <div className="flex items-start space-x-3">
              <AlertTriangle size={24} className="text-red-400 flex-shrink-0 mt-1" />
              <div>
                <h3 className="text-red-400 font-medium mb-2">계정 비활성화 안내</h3>
                <ul className="text-red-200 text-sm space-y-1">
                  <li>• 계정이 즉시 비활성화되어 로그인할 수 없습니다</li>
                  <li>• 작성한 게시글과 후기가 숨겨집니다</li>
                  <li>• 게임 통계와 데이터는 보존됩니다</li>
                  {session.user.oauth_provider ? (
                    <li>• {session.user.oauth_provider} 계정으로 언제든 복구 가능합니다</li>
                  ) : (
                    <li>• 계정 복구는 관리자에게 문의해주세요</li>
                  )}
                </ul>
              </div>
            </div>
          </div>

          <div className="bg-dark-card border border-dark-border rounded-lg p-6">
            {error && (
              <div className="bg-red-500/10 border border-red-500/30 rounded-lg p-3 mb-4">
                <p className="text-red-400 text-sm">{error}</p>
              </div>
            )}

            <div className="mb-6">
              <label htmlFor="confirmation" className="block text-sm font-medium text-gray-300 mb-2">
                확인을 위해 <span className="text-red-400 font-bold">"계정 삭제"</span>라고 입력해주세요
              </label>
              <input
                type="text"
                id="confirmation"
                value={confirmationText}
                onChange={(e) => setConfirmationText(e.target.value)}
                className="w-full bg-dark-bg border border-dark-border rounded-lg px-4 py-3 text-white placeholder-gray-500 focus:border-red-500 focus:outline-none"
                placeholder="계정 삭제"
                disabled={loading}
              />
            </div>

            <div className="flex space-x-3">
              <button
                onClick={() => setShowConfirmation(false)}
                className="flex-1 bg-gray-600 hover:bg-gray-700 text-white py-3 px-4 rounded-lg font-medium transition-colors"
                disabled={loading}
              >
                취소
              </button>
              <button
                onClick={handleDeactivate}
                disabled={loading || confirmationText !== '계정 삭제'}
                className={`flex-1 flex items-center justify-center space-x-2 py-3 px-4 rounded-lg font-medium transition-colors ${
                  loading || confirmationText !== '계정 삭제'
                    ? 'bg-gray-600 text-gray-400 cursor-not-allowed'
                    : 'bg-red-600 hover:bg-red-700 text-white'
                }`}
              >
                {loading ? (
                  <>
                    <div className="animate-spin rounded-full h-4 w-4 border-b-2 border-white"></div>
                    <span>처리 중...</span>
                  </>
                ) : (
                  <>
                    <Trash2 size={16} />
                    <span>계정 비활성화</span>
                  </>
                )}
              </button>
            </div>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-dark-bg">
      <div className="max-w-md mx-auto px-4 py-8">
        {/* 헤더 */}
        <div className="mb-8">
          <Link 
            href="/profile"
            className="inline-flex items-center text-gray-400 hover:text-white mb-4"
          >
            <ArrowLeft size={20} className="mr-2" />
            내 정보로 돌아가기
          </Link>
          <h1 className="text-3xl font-bold text-white mb-2">회원 탈퇴</h1>
          <p className="text-gray-400">계정을 비활성화합니다.</p>
        </div>

        {/* 경고 메시지 */}
        <div className="bg-yellow-500/10 border border-yellow-500/30 rounded-lg p-4 mb-6">
          <div className="flex items-start space-x-3">
            <AlertTriangle size={20} className="text-yellow-400 mt-0.5 flex-shrink-0" />
            <div>
              <h3 className="text-yellow-400 font-medium mb-1">탈퇴 전 확인사항</h3>
              <ul className="text-yellow-200 text-sm space-y-1">
                <li>• 계정은 완전히 삭제되지 않고 비활성화됩니다</li>
                <li>• 게임 통계와 기록은 보존됩니다</li>
                {session.user.oauth_provider && (
                  <li>• {session.user.oauth_provider} 계정으로 언제든 복구 가능합니다</li>
                )}
              </ul>
            </div>
          </div>
        </div>

        {/* 탈퇴 사유 */}
        <div className="bg-dark-card border border-dark-border rounded-lg p-6 mb-6">
          <h3 className="text-lg font-semibold text-white mb-4">탈퇴 사유를 알려주세요 (선택사항)</h3>
          <div className="space-y-3">
            {DEACTIVATION_REASONS.map((reason) => (
              <label key={reason} className="flex items-center space-x-3 cursor-pointer">
                <input
                  type="radio"
                  name="reason"
                  value={reason}
                  checked={selectedReason === reason}
                  onChange={(e) => handleReasonChange(e.target.value)}
                  className="w-4 h-4 text-primary-600 bg-dark-bg border-dark-border focus:ring-primary-500"
                />
                <span className="text-gray-300">{reason}</span>
              </label>
            ))}
          </div>

          {selectedReason === '기타' && (
            <div className="mt-4">
              <textarea
                value={customReason}
                onChange={(e) => setCustomReason(e.target.value)}
                className="w-full bg-dark-bg border border-dark-border rounded-lg px-4 py-3 text-white placeholder-gray-500 focus:border-primary-500 focus:outline-none resize-none"
                rows={3}
                placeholder="자세한 사유를 알려주세요..."
                maxLength={500}
              />
              <p className="text-gray-500 text-xs mt-1">
                {customReason.length}/500자
              </p>
            </div>
          )}
        </div>

        {/* 진행 버튼 */}
        <div className="space-y-3">
          <button
            onClick={() => setShowConfirmation(true)}
            className="w-full bg-red-600 hover:bg-red-700 text-white py-3 px-4 rounded-lg font-medium transition-colors flex items-center justify-center space-x-2"
          >
            <Trash2 size={16} />
            <span>계정 비활성화 진행</span>
          </button>

          <Link
            href="/profile"
            className="block w-full bg-gray-600 hover:bg-gray-700 text-white py-3 px-4 rounded-lg font-medium transition-colors text-center"
          >
            취소
          </Link>
        </div>
      </div>
    </div>
  );
}