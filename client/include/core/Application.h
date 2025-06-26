#pragma once

#include <QApplication>
#include <QTranslator>
#include <memory>

namespace Blokus {

    class MainWindow;
    class NetworkManager;
    class Logger;

    class Application : public QApplication {
        Q_OBJECT

    public:
        explicit Application(int& argc, char** argv);
        ~Application() override;

        // 싱글톤 접근
        static Application* instance();

        // 초기화 및 실행
        bool initialize();
        int run();

        // 네트워크 매니저 접근
        NetworkManager* networkManager() const { return m_networkManager.get(); }

        // 설정 관련
        void loadSettings();
        void saveSettings();

        // 언어 설정
        void setLanguage(const QString& languageCode);

    public slots:
        void onNetworkError(const QString& error);
        void onConnectionEstablished();
        void onConnectionLost();

    private slots:
        void onAboutToQuit();

    private:
        void setupLogging();
        void setupStyle();
        void createMainWindow();
        void connectSignals();

    private:
        std::unique_ptr<MainWindow> m_mainWindow;
        std::unique_ptr<NetworkManager> m_networkManager;
        std::unique_ptr<Logger> m_logger;
        std::unique_ptr<QTranslator> m_translator;

        static Application* s_instance;

        // 설정 값들
        QString m_serverAddress;
        quint16 m_serverPort;
        QString m_language;
        bool m_soundEnabled;
        bool m_animationsEnabled;
    };

} // namespace Blokus