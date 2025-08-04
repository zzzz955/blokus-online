'use client';

import { useState } from 'react';
import { X, Star, Send, Loader2 } from 'lucide-react';
import Button from '@/components/ui/Button';
import { api } from '@/utils/api';
import { TestimonialForm } from '@/types';

interface TestimonialModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
}

export default function TestimonialModal({ isOpen, onClose, onSuccess }: TestimonialModalProps) {
  const [formData, setFormData] = useState<TestimonialForm>({
    name: '',
    rating: 5,
    comment: '',
  });
  const [hoverRating, setHoverRating] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError(null);

    try {
      const submitData = {
        ...formData,
        comment: formData.comment?.trim() || undefined,
      };

      await api.post('/api/testimonials', submitData);
      onSuccess();
      handleClose();
    } catch (error: any) {
      setError(error.message || '후기 등록에 실패했습니다.');
    } finally {
      setLoading(false);
    }
  };

  const handleClose = () => {
    setFormData({
      name: '',
      rating: 5,
      comment: '',
    });
    setHoverRating(0);
    setError(null);
    onClose();
  };

  const handleRatingClick = (rating: number) => {
    setFormData(prev => ({ ...prev, rating }));
  };

  if (!isOpen) return null;

  return (
    <>
      {/* Backdrop */}
      <div 
        className="fixed inset-0 bg-black/50 z-50"
        onClick={handleClose}
      />
      
      {/* Modal */}
      <div className="fixed inset-0 flex items-center justify-center z-50 p-4">
        <div className="bg-gray-800 rounded-lg border border-gray-700 w-full max-w-md">
          {/* Header */}
          <div className="flex items-center justify-between p-6 border-b border-gray-700">
            <h2 className="text-xl font-semibold text-white">후기 작성하기</h2>
            <button
              onClick={handleClose}
              className="text-gray-400 hover:text-white transition-colors"
              disabled={loading}
            >
              <X className="w-5 h-5" />
            </button>
          </div>

          {/* Content */}
          <form onSubmit={handleSubmit} className="p-6 space-y-4">
            {/* 이름 입력 */}
            <div>
              <label className="block text-sm font-medium text-gray-300 mb-2">
                이름 <span className="text-red-400">*</span>
              </label>
              <input
                type="text"
                value={formData.name}
                onChange={(e) => setFormData(prev => ({ ...prev, name: e.target.value }))}
                placeholder="익명 가능"
                className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded-md text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-primary-500 focus:border-transparent"
                required
                maxLength={50}
                disabled={loading}
              />
            </div>

            {/* 별점 선택 */}
            <div>
              <label className="block text-sm font-medium text-gray-300 mb-2">
                별점 <span className="text-red-400">*</span>
              </label>
              <div className="flex items-center space-x-1">
                {[1, 2, 3, 4, 5].map((star) => (
                  <button
                    key={star}
                    type="button"
                    onClick={() => handleRatingClick(star)}
                    onMouseEnter={() => setHoverRating(star)}
                    onMouseLeave={() => setHoverRating(0)}
                    className="p-1 transition-colors"
                    disabled={loading}
                  >
                    <Star
                      className={`w-6 h-6 ${
                        star <= (hoverRating || formData.rating)
                          ? 'text-yellow-400 fill-current'
                          : 'text-gray-600'
                      }`}
                    />
                  </button>
                ))}
                <span className="ml-2 text-sm text-gray-400">
                  {formData.rating}점
                </span>
              </div>
            </div>

            {/* 후기 내용 */}
            <div>
              <label className="block text-sm font-medium text-gray-300 mb-2">
                후기 내용 (선택사항)
              </label>
              <textarea
                value={formData.comment}
                onChange={(e) => setFormData(prev => ({ ...prev, comment: e.target.value }))}
                placeholder="게임에 대한 솔직한 후기를 남겨주세요"
                rows={4}
                className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded-md text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-primary-500 focus:border-transparent resize-none"
                maxLength={500}
                disabled={loading}
              />
              <div className="text-right text-xs text-gray-500 mt-1">
                {formData.comment?.length || 0}/500
              </div>
            </div>

            {/* 에러 메시지 */}
            {error && (
              <div className="text-red-400 text-sm bg-red-900/20 border border-red-500/30 rounded-md p-3">
                {error}
              </div>
            )}

            {/* 액션 버튼 */}
            <div className="flex space-x-3 pt-4">
              <Button
                type="button"
                variant="outline"
                onClick={handleClose}
                disabled={loading}
                className="flex-1"
              >
                취소
              </Button>
              <Button
                type="submit"
                disabled={loading || !formData.name.trim()}
                className="flex-1 flex items-center justify-center space-x-2"
              >
                {loading ? (
                  <>
                    <Loader2 className="w-4 h-4 animate-spin" />
                    <span>등록 중...</span>
                  </>
                ) : (
                  <>
                    <Send className="w-4 h-4" />
                    <span>후기 등록</span>
                  </>
                )}
              </Button>
            </div>
          </form>
        </div>
      </div>
    </>
  );
}