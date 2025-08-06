'use client';

import { useEffect, useState } from 'react';
import { useSession } from 'next-auth/react';
import AccountReactivationModal from './AccountReactivationModal';

export default function AccountReactivationProvider({
  children
}: {
  children: React.ReactNode;
}) {
  const { data: session, status } = useSession();
  const [showModal, setShowModal] = useState(false);

  useEffect(() => {
    // 세션이 로드되고 계정 복구가 필요한 경우 모달 표시
    if (
      status === 'authenticated' && 
      session?.user?.needs_reactivation && 
      session?.user?.deactivated_account &&
      session?.user?.email &&
      session?.user?.oauth_provider
    ) {
      setShowModal(true);
    }
  }, [session, status]);

  const handleCloseModal = () => {
    setShowModal(false);
  };

  return (
    <>
      {children}
      {showModal && 
       session?.user?.deactivated_account && 
       session?.user?.email && 
       session?.user?.oauth_provider && (
        <AccountReactivationModal
          isOpen={showModal}
          onClose={handleCloseModal}
          deactivatedAccount={session.user.deactivated_account}
          email={session.user.email}
          oauth_provider={session.user.oauth_provider}
        />
      )}
    </>
  );
}