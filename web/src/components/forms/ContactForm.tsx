'use client';

import { useState } from 'react';
import { useSession } from 'next-auth/react';
import { useForm } from 'react-hook-form';
import Button from '@/components/ui/Button';
import { api } from '@/utils/api';
import { ContactForm as ContactFormType } from '@/types';

interface ContactFormProps {
  onSuccess?: () => void;
}

export default function ContactForm({ onSuccess }: ContactFormProps) {
  const { data: session } = useSession();
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [submitMessage, setSubmitMessage] = useState<{ type: 'success' | 'error'; message: string } | null>(null);

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<ContactFormType>();

  const onSubmit = async (data: ContactFormType) => {
    if (!session?.user) {
      setSubmitMessage({
        type: 'error',
        message: '로그인이 필요합니다.',
      });
      return;
    }

    setIsSubmitting(true);
    setSubmitMessage(null);

    try {
      // Include user email from session
      const ticketData = {
        ...data,
        email: session.user.email,
      };

      await api.post('/api/support', ticketData);
      setSubmitMessage({
        type: 'success',
        message: '문의가 성공적으로 접수되었습니다. 빠른 시일 내에 답변드리겠습니다.',
      });
      reset();
      onSuccess?.();
    } catch (error: any) {
      setSubmitMessage({
        type: 'error',
        message: error.message || '문의 접수에 실패했습니다. 잠시 후 다시 시도해주세요.',
      });
    } finally {
      setIsSubmitting(false);
    }
  };

  if (!session?.user) {
    return (
      <div className="text-center py-8">
        <p className="text-gray-400 mb-4">문의를 작성하려면 로그인이 필요합니다.</p>
        <Button onClick={() => window.location.href = '/auth/signin?callbackUrl=/support'}>
          로그인하기
        </Button>
      </div>
    );
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-6">
      {/* 문의자 정보 표시 */}
      <div className="bg-dark-card border border-dark-border rounded-lg p-4">
        <h4 className="text-sm font-medium text-gray-300 mb-2">문의자 정보</h4>
        <p className="text-white font-medium">{session.user.name || session.user.email}</p>
        <p className="text-gray-400 text-sm">{session.user.email}</p>
      </div>

      {/* 제목 */}
      <div>
        <label htmlFor="subject" className="block text-sm font-medium text-gray-300 mb-2">
          제목 *
        </label>
        <input
          type="text"
          id="subject"
          {...register('subject', {
            required: '제목을 입력해주세요.',
            maxLength: {
              value: 200,
              message: '제목은 200자 이하로 입력해주세요.',
            },
          })}
          className="w-full px-4 py-3 bg-dark-card border border-dark-border rounded-lg text-white placeholder-gray-400 focus:outline-none focus:border-primary-500 focus:ring-1 focus:ring-primary-500"
          placeholder="문의 제목을 입력해주세요"
        />
        {errors.subject && (
          <p className="mt-1 text-sm text-red-400">{errors.subject.message}</p>
        )}
      </div>

      {/* 메시지 */}
      <div>
        <label htmlFor="message" className="block text-sm font-medium text-gray-300 mb-2">
          문의 내용 *
        </label>
        <textarea
          id="message"
          rows={6}
          {...register('message', {
            required: '문의 내용을 입력해주세요.',
            maxLength: {
              value: 2000,
              message: '메시지는 2000자 이하로 입력해주세요.',
            },
          })}
          className="w-full px-4 py-3 bg-dark-card border border-dark-border rounded-lg text-white placeholder-gray-400 focus:outline-none focus:border-primary-500 focus:ring-1 focus:ring-primary-500 resize-none"
          placeholder="문의하실 내용을 자세히 작성해주세요..."
        />
        {errors.message && (
          <p className="mt-1 text-sm text-red-400">{errors.message.message}</p>
        )}
      </div>

      {/* 제출 메시지 */}
      {submitMessage && (
        <div
          className={`p-4 rounded-lg ${
            submitMessage.type === 'success'
              ? 'bg-green-900/20 border border-green-500/30 text-green-400'
              : 'bg-red-900/20 border border-red-500/30 text-red-400'
          }`}
        >
          {submitMessage.message}
        </div>
      )}

      {/* 제출 버튼 */}
      <Button
        type="submit"
        loading={isSubmitting}
        className="w-full"
        disabled={isSubmitting}
      >
        문의 접수하기
      </Button>

      <p className="text-sm text-gray-400 text-center">
        문의하신 내용은 관리자 검토 후 등록하신 이메일로 답변드립니다.
      </p>
    </form>
  );
}