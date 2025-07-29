#pragma once

#include <QDialog>
#include <QLabel>
#include <QPushButton>
#include <QVBoxLayout>
#include <QHBoxLayout>
#include <QJsonObject>
#include <QJsonDocument>

namespace Blokus {

    /**
     * @brief AFK 모드 전환 알림 대화상자
     * 
     * 서버에서 AFK_MODE_ACTIVATED 메시지를 받았을 때 표시되는 모달 대화상자.
     * 사용자에게 AFK 모드로 전환되었음을 알리고, AFK 해제를 위한 버튼을 제공합니다.
     */
    class AfkNotificationDialog : public QDialog
    {
        Q_OBJECT

    public:
        explicit AfkNotificationDialog(QWidget* parent = nullptr);
        ~AfkNotificationDialog();

        /**
         * @brief AFK 알림 정보 설정
         * @param jsonData 서버에서 받은 AFK_MODE_ACTIVATED 메시지의 JSON 데이터
         */
        void setAfkInfo(const QJsonObject& jsonData);

        /**
         * @brief 간단한 AFK 알림 설정 (기본값 사용)
         * @param timeoutCount 현재 타임아웃 횟수
         * @param maxCount 최대 허용 타임아웃 횟수
         */
        void setAfkInfo(int timeoutCount, int maxCount);

    signals:
        /**
         * @brief AFK 해제 요청 시그널
         * 사용자가 "게임 계속하기" 버튼을 클릭했을 때 발생
         */
        void afkUnblockRequested();

    public slots:
        /**
         * @brief 게임 계속하기 버튼 클릭 핸들러
         */
        void onContinueGameClicked();

        /**
         * @brief 나가기 버튼 클릭 핸들러
         */
        void onLeaveGameClicked();
        
        /**
         * @brief 게임 종료 시 호출되는 슬롯
         * 모달이 열려있을 때 게임이 종료되면 자동으로 상태 변경
         */
        void onGameEnded();
        
        /**
         * @brief AFK 해제 에러 처리 (게임이 이미 종료된 경우)
         */
        void onAfkUnblockError(const QString& reason, const QString& message);

    protected:
        /**
         * @brief ESC 키 등의 닫기 이벤트를 차단
         */
        void closeEvent(QCloseEvent* event) override;

        /**
         * @brief 키보드 이벤트 처리 (ESC 차단)
         */
        void keyPressEvent(QKeyEvent* event) override;

    private:
        void setupUI();
        void setupConnections();
        void updateMessage();

    private:
        // UI 컴포넌트
        QVBoxLayout* m_mainLayout;
        QLabel* m_titleLabel;
        QLabel* m_messageLabel;
        QLabel* m_infoLabel;
        QHBoxLayout* m_buttonLayout;
        QPushButton* m_continueButton;
        QPushButton* m_leaveButton;

        // AFK 정보
        QString m_reason;
        int m_timeoutCount;
        int m_maxCount;
        
        // 게임 상태 추적
        bool m_gameEnded;
    };

} // namespace Blokus