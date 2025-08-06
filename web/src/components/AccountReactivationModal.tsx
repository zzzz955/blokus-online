'use client';

import { useState } from 'react';
import { useSession, signOut } from 'next-auth/react';
import { UserPlus, Calendar, AlertTriangle, X } from 'lucide-react';

interface AccountReactivationModalProps {
  isOpen: boolean;
  onClose: () => void;
  deactivatedAccount: {
    username: string;
    display_name?: string;
    member_since: Date;
  };
  email: string;
  oauth_provider: string;
}

export default function AccountReactivationModal({
  isOpen,
  onClose,
  deactivatedAccount,
  email,
  oauth_provider
}: AccountReactivationModalProps) {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);

  if (!isOpen) return null;

  const formatDate = (dateString: Date | string) => {
    return new Date(dateString).toLocaleDateString('ko-KR', {
      year: 'numeric',
      month: 'long',
      day: 'numeric'
    });
  };

  const handleReactivate = async () => {
    setLoading(true);
    setError(null);

    try {
      const response = await fetch('/api/user/reactivate', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          email,
          oauth_provider,
          confirm_reactivation: true
        })
      });

      const data = await response.json();

      if (data.success) {
        setSuccess(true);
        // 3초 후 자동으로 페이지 새로고침하여 정상 로그인 처리
        setTimeout(() => {
          window.location.reload();
        }, 3000);
      } else {
        setError(data.error || '계정 복구에 실패했습니다.');
      }
    } catch (err) {
      setError('네트워크 오류가 발생했습니다.');
    } finally {
      setLoading(false);
    }
  };

  const handleReject = async () => {
    // 세션 종료하고 홈으로 이동
    await signOut({ callbackUrl: '/' });
  };

  if (success) {
    return (
      <div className="fixed inset-0 bg-black/80 backdrop-blur-sm flex items-center justify-center z-50 p-4">
        <div className="bg-dark-card border border-dark-border rounded-lg p-8 max-w-md w-full text-center">
          <div className="w-16 h-16 bg-green-500/20 rounded-full flex items-center justify-center mx-auto mb-4">
            <UserPlus size={32} className="text-green-400" />
          </div>
          <h2 className="text-2xl font-bold text-white mb-4">계정 복구 완료!</h2>
          <p className="text-gray-300 mb-6">
            계정이 성공적으로 복구되었습니다.<br />
            다시 돌아오신 것을 환영합니다!
          </p>
          <div className="flex items-center justify-center space-x-2 text-sm text-gray-400">
            <div className="animate-spin rounded-full h-4 w-4 border-b-2 border-primary-500"></div>
            <span>페이지를 새로고침하는 중...</span>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="fixed inset-0 bg-black/80 backdrop-blur-sm flex items-center justify-center z-50 p-4">
      <div className="bg-dark-card border border-dark-border rounded-lg p-6 max-w-md w-full">
        {/* 헤더 */}
        <div className="flex items-center justify-between mb-6">
          <h2 className="text-xl font-bold text-white">계정 복구</h2>
          <button
            onClick={onClose}
            className="text-gray-400 hover:text-white transition-colors"
          >
            <X size={20} />
          </button>
        </div>

        {/* 계정 정보 */}
        <div className="bg-dark-bg border border-dark-border rounded-lg p-4 mb-6">
          <div className="flex items-start space-x-3">
            <div className="w-12 h-12 bg-gradient-to-br from-primary-500 to-secondary-500 rounded-full flex items-center justify-center flex-shrink-0">
              <UserPlus size={20} className="text-white" />
            </div>
            <div className="flex-1">
              <h3 className="text-white font-medium mb-1">
                {deactivatedAccount.display_name || deactivatedAccount.username}
              </h3>
              <p className="text-gray-400 text-sm mb-2">@{deactivatedAccount.username}</p>
              <div className="flex items-center space-x-1 text-xs text-gray-500">
                <Calendar size={12} />
                <span>{formatDate(deactivatedAccount.member_since)}부터 회원</span>
              </div>
            </div>
          </div>
        </div>

        {/* 안내 메시지 */}
        <div className="bg-blue-500/10 border border-blue-500/30 rounded-lg p-4 mb-6">
          <div className="flex items-start space-x-3">
            <AlertTriangle size={20} className="text-blue-400 mt-0.5 flex-shrink-0" />
            <div>
              <h4 className="text-blue-400 font-medium mb-1">비활성화된 계정을 발견했습니다</h4>
              <p className="text-blue-200 text-sm">
                이전에 사용하시던 계정이 비활성화 상태입니다.<br />
                계정을 복구하여 기존 데이터와 통계를 그대로 사용하실 수 있습니다.
              </p>
            </div>
          </div>
        </div>

        {/* 복구될 내용 안내 */}
        <div className="mb-6">
          <h4 className="text-white font-medium mb-3">복구되는 내용:</h4>
          <ul className="text-gray-300 text-sm space-y-1">
            <li>• 게임 통계 및 기록</li>
            <li>• 작성한 게시글 및 후기</li>
            <li>• 문의 내역</li>
            <li>• 계정 설정 및 프로필</li>
          </ul>
        </div>

        {/* 에러 메시지 */}
        {error && (
          <div className="bg-red-500/10 border border-red-500/30 rounded-lg p-3 mb-4">
            <p className="text-red-400 text-sm">{error}</p>
          </div>
        )}

        {/* 액션 버튼 */}
        <div className="flex space-x-3">
          <button
            onClick={handleReject}
            className="flex-1 bg-gray-600 hover:bg-gray-700 text-white py-3 px-4 rounded-lg font-medium transition-colors"
            disabled={loading}
          >
            취소
          </button>
          <button
            onClick={handleReactivate}
            disabled={loading}
            className={`flex-1 flex items-center justify-center space-x-2 py-3 px-4 rounded-lg font-medium transition-colors ${
              loading
                ? 'bg-gray-600 text-gray-400 cursor-not-allowed'
                : 'bg-primary-600 hover:bg-primary-700 text-white'
            }`}
          >
            {loading ? (
              <>
                <div className="animate-spin rounded-full h-4 w-4 border-b-2 border-white"></div>
                <span>복구 중...</span>
              </>
            ) : (
              <>
                <UserPlus size={16} />
                <span>계정 복구</span>
              </>
            )}
          </button>
        </div>

        {/* 추가 안내 */}
        <p className="text-gray-500 text-xs text-center mt-4">
          취소하시면 새로운 계정으로 가입하실 수 있습니다.
        </p>
      </div>
    </div>
  );
}