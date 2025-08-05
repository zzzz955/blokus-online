'use client';

import { useState, useEffect } from 'react';
import { useSession } from 'next-auth/react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { 
  User, 
  Mail, 
  Calendar, 
  Trophy, 
  Target, 
  TrendingUp, 
  Award,
  Edit,
  FileText,
  Star,
  HelpCircle,
  Save,
  X
} from 'lucide-react';

interface UserProfile {
  user_id: number;
  username: string;
  email?: string;
  oauth_provider?: string;
  display_name?: string;
  avatar_url?: string;
  created_at: string;
  last_login_at?: string;
  stats: {
    total_games: number;
    wins: number;
    losses: number;
    draws: number;
    win_rate: number;
    best_score: number;
    total_score: number;
    average_score: number;
    longest_win_streak: number;
    current_win_streak: number;
    level: number;
    experience_points: number;
    last_played?: string;
  };
  activity_stats: {
    posts_count: number;
    testimonials_count: number;
    support_tickets_count: number;
  };
}

export default function ProfilePage() {
  const { data: session, status } = useSession();
  const router = useRouter();
  const [profile, setProfile] = useState<UserProfile | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [isEditing, setIsEditing] = useState(false);
  const [editingDisplayName, setEditingDisplayName] = useState('');
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (status === 'loading') return;
    
    if (!session) {
      router.push('/auth/signin');
      return;
    }

    fetchProfile();
  }, [session, status, router]);

  const fetchProfile = async () => {
    try {
      const response = await fetch('/api/user/profile');
      const data = await response.json();
      
      if (data.success) {
        setProfile(data.data);
        setEditingDisplayName(data.data.display_name || '');
      } else {
        setError(data.error || '프로필 정보를 불러올 수 없습니다.');
      }
    } catch (err) {
      setError('네트워크 오류가 발생했습니다.');
    } finally {
      setLoading(false);
    }
  };

  const handleSaveProfile = async () => {
    setSaving(true);
    try {
      const response = await fetch('/api/user/profile', {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          display_name: editingDisplayName || undefined
        })
      });
      
      const data = await response.json();
      
      if (data.success) {
        setProfile(prev => prev ? {
          ...prev,
          display_name: editingDisplayName || undefined
        } : null);
        setIsEditing(false);
      } else {
        setError(data.error || '프로필 수정에 실패했습니다.');
      }
    } catch (err) {
      setError('네트워크 오류가 발생했습니다.');
    } finally {
      setSaving(false);
    }
  };

  const cancelEdit = () => {
    setEditingDisplayName(profile?.display_name || '');
    setIsEditing(false);
  };

  if (loading) {
    return (
      <div className="min-h-screen bg-dark-bg">
        <div className="max-w-4xl mx-auto px-4 py-8">
          <div className="animate-pulse">
            <div className="h-8 bg-gray-700 rounded mb-4"></div>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              <div className="h-64 bg-gray-700 rounded-lg"></div>
              <div className="h-64 bg-gray-700 rounded-lg"></div>
            </div>
          </div>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="min-h-screen bg-dark-bg flex items-center justify-center">
        <div className="text-center">
          <div className="text-red-400 text-xl mb-4">{error}</div>
          <button 
            onClick={fetchProfile}
            className="btn-primary"
          >
            다시 시도
          </button>
        </div>
      </div>
    );
  }

  if (!profile) return null;

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleDateString('ko-KR', {
      year: 'numeric',
      month: 'long',
      day: 'numeric'
    });
  };

  return (
    <div className="min-h-screen bg-dark-bg">
      <div className="max-w-4xl mx-auto px-4 py-8">
        {/* 헤더 */}
        <div className="mb-8">
          <h1 className="text-3xl font-bold text-white mb-2">내 정보</h1>
          <p className="text-gray-400">프로필 정보와 게임 통계를 확인하고 관리하세요.</p>
        </div>

        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          {/* 프로필 정보 */}
          <div className="lg:col-span-1">
            <div className="bg-dark-card border border-dark-border rounded-lg p-6">
              <div className="flex items-center justify-between mb-6">
                <h2 className="text-xl font-semibold text-white">프로필</h2>
                {!isEditing && (
                  <button
                    onClick={() => setIsEditing(true)}
                    className="text-gray-400 hover:text-white p-2 rounded"
                  >
                    <Edit size={18} />
                  </button>
                )}
              </div>

              <div className="space-y-4">
                <div className="flex items-center space-x-3">
                  <User className="text-gray-400" size={20} />
                  <div>
                    <div className="text-gray-400 text-sm">사용자명</div>
                    <div className="text-white font-medium">{profile.username}</div>
                  </div>
                </div>

                <div className="flex items-center space-x-3">
                  <Award className="text-gray-400" size={20} />
                  <div className="flex-1">
                    <div className="text-gray-400 text-sm">표시명</div>
                    {isEditing ? (
                      <div className="flex items-center space-x-2 mt-1">
                        <input
                          type="text"
                          value={editingDisplayName}
                          onChange={(e) => setEditingDisplayName(e.target.value)}
                          className="bg-dark-bg border border-dark-border rounded px-3 py-1 text-white text-sm flex-1"
                          placeholder="표시명을 입력하세요"
                          maxLength={30}
                        />
                        <button
                          onClick={handleSaveProfile}
                          disabled={saving}
                          className="text-green-400 hover:text-green-300 p-1"
                        >
                          <Save size={16} />
                        </button>
                        <button
                          onClick={cancelEdit}
                          className="text-gray-400 hover:text-gray-300 p-1"
                        >
                          <X size={16} />
                        </button>
                      </div>
                    ) : (
                      <div className="text-white font-medium">
                        {profile.display_name || '설정되지 않음'}
                      </div>
                    )}
                  </div>
                </div>

                {profile.email && (
                  <div className="flex items-center space-x-3">
                    <Mail className="text-gray-400" size={20} />
                    <div>
                      <div className="text-gray-400 text-sm">이메일</div>
                      <div className="text-white font-medium">{profile.email}</div>
                    </div>
                  </div>
                )}

                {profile.oauth_provider && (
                  <div className="flex items-center space-x-3">
                    <div className="w-5 h-5 bg-gradient-to-br from-primary-500 to-secondary-500 rounded"></div>
                    <div>
                      <div className="text-gray-400 text-sm">로그인 방식</div>
                      <div className="text-white font-medium capitalize">{profile.oauth_provider}</div>
                    </div>
                  </div>
                )}

                <div className="flex items-center space-x-3">
                  <Calendar className="text-gray-400" size={20} />
                  <div>
                    <div className="text-gray-400 text-sm">가입일</div>
                    <div className="text-white font-medium">{formatDate(profile.created_at)}</div>
                  </div>
                </div>

                {profile.last_login_at && (
                  <div className="flex items-center space-x-3">
                    <Calendar className="text-gray-400" size={20} />
                    <div>
                      <div className="text-gray-400 text-sm">마지막 로그인</div>
                      <div className="text-white font-medium">{formatDate(profile.last_login_at)}</div>
                    </div>
                  </div>
                )}
              </div>
            </div>
          </div>

          {/* 게임 통계 */}
          <div className="lg:col-span-2">
            <div className="bg-dark-card border border-dark-border rounded-lg p-6 mb-6">
              <h2 className="text-xl font-semibold text-white mb-6">게임 통계</h2>
              
              <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-6">
                <div className="text-center">
                  <div className="text-2xl font-bold text-primary-400">{profile.stats.level}</div>
                  <div className="text-gray-400 text-sm">레벨</div>
                </div>
                <div className="text-center">
                  <div className="text-2xl font-bold text-green-400">{profile.stats.wins}</div>
                  <div className="text-gray-400 text-sm">승리</div>
                </div>
                <div className="text-center">
                  <div className="text-2xl font-bold text-red-400">{profile.stats.losses}</div>
                  <div className="text-gray-400 text-sm">패배</div>
                </div>
                <div className="text-center">
                  <div className="text-2xl font-bold text-blue-400">{profile.stats.win_rate.toFixed(1)}%</div>
                  <div className="text-gray-400 text-sm">승률</div>
                </div>
              </div>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div className="space-y-3">
                  <div className="flex justify-between">
                    <span className="text-gray-400">총 게임</span>
                    <span className="text-white">{profile.stats.total_games}게임</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-gray-400">무승부</span>
                    <span className="text-white">{profile.stats.draws}게임</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-gray-400">최고 점수</span>
                    <span className="text-white">{profile.stats.best_score}점</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-gray-400">평균 점수</span>
                    <span className="text-white">{profile.stats.average_score.toFixed(1)}점</span>
                  </div>
                </div>
                <div className="space-y-3">
                  <div className="flex justify-between">
                    <span className="text-gray-400">총 점수</span>
                    <span className="text-white">{profile.stats.total_score.toLocaleString()}점</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-gray-400">최장 연승</span>
                    <span className="text-white">{profile.stats.longest_win_streak}연승</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-gray-400">현재 연승</span>
                    <span className="text-white">{profile.stats.current_win_streak}연승</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-gray-400">경험치</span>
                    <span className="text-white">{profile.stats.experience_points.toLocaleString()}XP</span>
                  </div>
                </div>
              </div>

              {profile.stats.last_played && (
                <div className="mt-4 pt-4 border-t border-dark-border">
                  <div className="flex justify-between">
                    <span className="text-gray-400">마지막 플레이</span>
                    <span className="text-white">{formatDate(profile.stats.last_played)}</span>
                  </div>
                </div>
              )}
            </div>

            {/* 활동 통계 */}
            <div className="bg-dark-card border border-dark-border rounded-lg p-6">
              <h2 className="text-xl font-semibold text-white mb-6">내 활동</h2>
              
              <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                <Link 
                  href="/profile/posts"
                  className="bg-dark-bg border border-dark-border rounded-lg p-4 hover:border-primary-500 transition-colors"
                >
                  <div className="flex items-center space-x-3">
                    <FileText className="text-primary-400" size={24} />
                    <div>
                      <div className="text-white font-medium">내 게시글</div>
                      <div className="text-gray-400 text-sm">{profile.activity_stats.posts_count}개</div>
                    </div>
                  </div>
                </Link>

                <Link 
                  href="/profile/testimonials"
                  className="bg-dark-bg border border-dark-border rounded-lg p-4 hover:border-primary-500 transition-colors"
                >
                  <div className="flex items-center space-x-3">
                    <Star className="text-yellow-400" size={24} />
                    <div>
                      <div className="text-white font-medium">내 후기</div>
                      <div className="text-gray-400 text-sm">{profile.activity_stats.testimonials_count}개</div>
                    </div>
                  </div>
                </Link>

                <Link 
                  href="/profile/support"
                  className="bg-dark-bg border border-dark-border rounded-lg p-4 hover:border-primary-500 transition-colors"
                >
                  <div className="flex items-center space-x-3">
                    <HelpCircle className="text-blue-400" size={24} />
                    <div>
                      <div className="text-white font-medium">내 문의</div>
                      <div className="text-gray-400 text-sm">{profile.activity_stats.support_tickets_count}개</div>
                    </div>
                  </div>
                </Link>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}