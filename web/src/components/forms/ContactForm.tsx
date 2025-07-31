'use client';

import { useState } from 'react';
import { useForm } from 'react-hook-form';
import Button from '@/components/ui/Button';
import { api } from '@/utils/api';
import { ContactForm as ContactFormType } from '@/types';

interface ContactFormProps {
  onSuccess?: () => void;
}

export default function ContactForm({ onSuccess }: ContactFormProps) {
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [submitMessage, setSubmitMessage] = useState<{ type: 'success' | 'error'; message: string } | null>(null);

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<ContactFormType>();

  const onSubmit = async (data: ContactFormType) => {
    setIsSubmitting(true);
    setSubmitMessage(null);

    try {
      await api.post('/api/support', data);
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

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-6">
      {/* 이메일 */}
      <div>
        <label htmlFor="email" className="block text-sm font-medium text-gray-300 mb-2">
          이메일 주소 *
        </label>
        <input
          type="email"
          id="email"
          {...register('email', {
            required: '이메일 주소를 입력해주세요.',
            pattern: {
              value: /^[^\s@]+@[^\s@]+\.[^\s@]+$/,
              message: '올바른 이메일 형식이 아닙니다.',
            },
          })}
          className="input-field bg-dark-card border-dark-border text-white"
          placeholder="your@email.com"
        />
        {errors.email && (
          <p className="mt-1 text-sm text-red-400">{errors.email.message}</p>
        )}
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
          className="input-field bg-dark-card border-dark-border text-white"
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
          className="input-field bg-dark-card border-dark-border text-white resize-none"
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
        개인정보 보호를 위해 이메일 주소는 답변 목적으로만 사용됩니다.
      </p>
    </form>
  );
}