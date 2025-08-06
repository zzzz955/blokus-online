import { ReactNode } from 'react';
import Header from './Header';
import Footer from './Footer';
import AccountReactivationProvider from '../AccountReactivationProvider';

interface LayoutProps {
  children: ReactNode;
}

export default function Layout({ children }: LayoutProps) {
  return (
    <AccountReactivationProvider>
      <div className="min-h-screen bg-dark-bg text-white flex flex-col">
        <Header />
        <main className="flex-1">
          {children}
        </main>
        <Footer />
      </div>
    </AccountReactivationProvider>
  );
}