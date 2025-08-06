'use client';

import { useState } from 'react';
import { useSession, signOut } from 'next-auth/react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { ArrowLeft, Lock, Eye, EyeOff, AlertTriangle, CheckCircle } from 'lucide-react';

export default function PasswordResetPage() {
  const { data: session, status } = useSession();
  const router = useRouter();
  const [formData, setFormData] = useState({
    new_password: '',
    confirm_password: ''
  });
  const [showPassword, setShowPassword] = useState({
    new_password: false,
    confirm_password: false
  });
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);

  // OAuth 사용자가 아닌 경우에만 접근 가능하도록 제한할 수 있지만, 
  // OAuth 사용자도 비밀번호를 설정했을 수 있으므로 모든 로그인 사용자에게 허용
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
          <h2 className="text-2xl font-bold text-white mb-4">비밀번호 변경 완료</h2>
          <p className="text-gray-300 mb-6">
            비밀번호가 성공적으로 변경되었습니다.<br />
            보안을 위해 다시 로그인해주세요.
          </p>
          <button
            onClick={() => signOut({ callbackUrl: '/auth/signin' })}
            className="btn-primary w-full"
          >
            다시 로그인하기
          </button>
        </div>
      </div>
    );
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (!formData.new_password || !formData.confirm_password) {
      setError('모든 필드를 입력해주세요.');
      return;
    }

    if (formData.new_password !== formData.confirm_password) {
      setError('새 비밀번호와 비밀번호 확인이 일치하지 않습니다.');
      return;
    }

    if (formData.new_password.length < 8) {
      setError('비밀번호는 최소 8자 이상이어야 합니다.');
      return;
    }

    const passwordRegex = /^(?=.*[a-zA-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]/;
    if (!passwordRegex.test(formData.new_password)) {
      setError('비밀번호는 영문, 숫자, 특수문자를 모두 포함해야 합니다.');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const response = await fetch('/api/user/password-reset', {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          new_password: formData.new_password,
          confirm_password: formData.confirm_password
        })
      });

      const data = await response.json();

      if (data.success) {
        setSuccess(true);
      } else {
        setError(data.error || '비밀번호 변경에 실패했습니다.');
      }
    } catch (err) {
      setError('네트워크 오류가 발생했습니다.');
    } finally {
      setLoading(false);
    }
  };

  const handleInputChange = (field: string, value: string) => {
    setFormData(prev => ({ ...prev, [field]: value }));
    if (error) setError(null);
  };

  const togglePasswordVisibility = (field: 'new_password' | 'confirm_password') => {
    setShowPassword(prev => ({ ...prev, [field]: !prev[field] }));
  };

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
          <h1 className="text-3xl font-bold text-white mb-2">비밀번호 변경</h1>
          <p className="text-gray-400">새로운 비밀번호를 설정하세요.</p>
        </div>

        {/* 경고 메시지 */}
        <div className="bg-yellow-500/10 border border-yellow-500/30 rounded-lg p-4 mb-6">
          <div className="flex items-start space-x-3">
            <AlertTriangle size={20} className="text-yellow-400 mt-0.5 flex-shrink-0" />
            <div>
              <h3 className="text-yellow-400 font-medium mb-1">보안 안내</h3>
              <p className="text-yellow-200 text-sm">
                비밀번호 변경 후 보안을 위해 자동으로 로그아웃됩니다.<br />
                새 비밀번호로 다시 로그인해주세요.
              </p>
            </div>
          </div>
        </div>

        {/* 비밀번호 변경 폼 */}
        <div className="bg-dark-card border border-dark-border rounded-lg p-6">
          <form onSubmit={handleSubmit} className="space-y-6">
            {error && (
              <div className="bg-red-500/10 border border-red-500/30 rounded-lg p-3">
                <p className="text-red-400 text-sm">{error}</p>
              </div>
            )}

            {/* 새 비밀번호 */}
            <div>
              <label htmlFor="new_password" className="block text-sm font-medium text-gray-300 mb-2">
                새 비밀번호
              </label>
              <div className="relative">
                <input
                  type={showPassword.new_password ? 'text' : 'password'}
                  id="new_password"
                  value={formData.new_password}
                  onChange={(e) => handleInputChange('new_password', e.target.value)}
                  className="w-full bg-dark-bg border border-dark-border rounded-lg px-4 py-3 text-white placeholder-gray-500 focus:border-primary-500 focus:outline-none"
                  placeholder="새 비밀번호를 입력하세요"
                  disabled={loading}
                />
                <button
                  type="button"
                  onClick={() => togglePasswordVisibility('new_password')}
                  className="absolute right-3 top-1/2 transform -translate-y-1/2 text-gray-400 hover:text-white"
                >
                  {showPassword.new_password ? <EyeOff size={20} /> : <Eye size={20} />}
                </button>
              </div>
              <p className="text-gray-500 text-xs mt-1">
                영문, 숫자, 특수문자를 포함하여 8자 이상
              </p>
            </div>

            {/* 비밀번호 확인 */}
            <div>
              <label htmlFor="confirm_password" className="block text-sm font-medium text-gray-300 mb-2">
                비밀번호 확인
              </label>
              <div className="relative">
                <input
                  type={showPassword.confirm_password ? 'text' : 'password'}
                  id="confirm_password"
                  value={formData.confirm_password}
                  onChange={(e) => handleInputChange('confirm_password', e.target.value)}
                  className="w-full bg-dark-bg border border-dark-border rounded-lg px-4 py-3 text-white placeholder-gray-500 focus:border-primary-500 focus:outline-none"
                  placeholder="비밀번호를 다시 입력하세요"
                  disabled={loading}
                />
                <button
                  type="button"
                  onClick={() => togglePasswordVisibility('confirm_password')}
                  className="absolute right-3 top-1/2 transform -translate-y-1/2 text-gray-400 hover:text-white"
                >
                  {showPassword.confirm_password ? <EyeOff size={20} /> : <Eye size={20} />}
                </button>
              </div>
            </div>

            {/* 비밀번호 강도 표시 */}
            {formData.new_password && (
              <div className="space-y-2">
                <p className="text-sm text-gray-400">비밀번호 강도:</p>
                <div className="space-y-1 text-xs">
                  <div className={`flex items-center space-x-2 ${
                    formData.new_password.length >= 8 ? 'text-green-400' : 'text-gray-500'
                  }`}>
                    <div className={`w-2 h-2 rounded-full ${
                      formData.new_password.length >= 8 ? 'bg-green-400' : 'bg-gray-600'
                    }`}></div>
                    <span>8자 이상</span>
                  </div>
                  <div className={`flex items-center space-x-2 ${
                    /[a-zA-Z]/.test(formData.new_password) ? 'text-green-400' : 'text-gray-500'
                  }`}>
                    <div className={`w-2 h-2 rounded-full ${
                      /[a-zA-Z]/.test(formData.new_password) ? 'bg-green-400' : 'bg-gray-600'
                    }`}></div>
                    <span>영문 포함</span>
                  </div>
                  <div className={`flex items-center space-x-2 ${
                    /\d/.test(formData.new_password) ? 'text-green-400' : 'text-gray-500'
                  }`}>
                    <div className={`w-2 h-2 rounded-full ${
                      /\d/.test(formData.new_password) ? 'bg-green-400' : 'bg-gray-600'
                    }`}></div>
                    <span>숫자 포함</span>
                  </div>
                  <div className={`flex items-center space-x-2 ${
                    /[@$!%*?&]/.test(formData.new_password) ? 'text-green-400' : 'text-gray-500'
                  }`}>
                    <div className={`w-2 h-2 rounded-full ${
                      /[@$!%*?&]/.test(formData.new_password) ? 'bg-green-400' : 'bg-gray-600'
                    }`}></div>
                    <span>특수문자 포함</span>
                  </div>
                </div>
              </div>
            )}

            {/* 제출 버튼 */}
            <button
              type="submit"
              disabled={loading || !formData.new_password || !formData.confirm_password}
              className={`w-full flex items-center justify-center space-x-2 py-3 px-4 rounded-lg font-medium transition-colors ${
                loading || !formData.new_password || !formData.confirm_password
                  ? 'bg-gray-600 text-gray-400 cursor-not-allowed'
                  : 'bg-primary-600 hover:bg-primary-700 text-white'
              }`}
            >
              {loading ? (
                <>
                  <div className="animate-spin rounded-full h-4 w-4 border-b-2 border-white"></div>
                  <span>변경 중...</span>
                </>
              ) : (
                <>
                  <Lock size={16} />
                  <span>비밀번호 변경</span>
                </>
              )}
            </button>
          </form>
        </div>
      </div>
    </div>
  );
}