#pragma once

#include <QDialog>
#include <QWidget>
#include <QVBoxLayout>
#include <QHBoxLayout>
#include <QGridLayout>
#include <QLabel>
#include <QPushButton>
#include <QProgressBar>
#include <QFrame>
#include <QGroupBox>
#include <QTableWidget>
#include <QScrollArea>
#include <QPixmap>
#include <QTimer>

#include "ClientTypes.h"

namespace Blokus {

    class UserInfoDialog : public QDialog
    {
        Q_OBJECT

    public:
        explicit UserInfoDialog(const UserInfo& userInfo, QWidget* parent = nullptr);
        ~UserInfoDialog();

        void updateUserInfo(const UserInfo& userInfo);
        void setCurrentUsername(const QString& currentUsername);

    signals:
        void getUserStatsRequested(const QString& username);
        void addFriendRequested(const QString& username);
        void sendWhisperRequested(const QString& username);

    private slots:
        void onAddFriendClicked();
        void onSendWhisperClicked();
        void onCloseClicked();
        void onRefreshClicked();

    protected:
        void paintEvent(QPaintEvent* event) override;
        void mousePressEvent(QMouseEvent* event) override;
        bool eventFilter(QObject* obj, QEvent* event) override;

    private:
        void setupUI();
        void setupBasicInfo();
        void setupStatsInfo();
        void setupActionButtons();
        void setupStyles();
        void updateBasicInfoDisplay();
        void updateStatsDisplay();
        void installBackgroundEventFilter();
        void removeBackgroundEventFilter();

        QString formatWinRate(double winRate) const;
        QString formatLevel(int level) const;
        QString formatGameCount(int games) const;

    private:
        // 사용자 정보
        UserInfo m_userInfo;
        bool m_isOwnInfo;
        QString m_currentUsername;

        // 메인 레이아웃
        QVBoxLayout* m_mainLayout;
        QScrollArea* m_scrollArea;
        QWidget* m_contentWidget;

        // 기본 정보 섹션
        QGroupBox* m_basicInfoGroup;
        QLabel* m_avatarLabel;
        QLabel* m_usernameLabel;
        QLabel* m_displayNameLabel;
        QLabel* m_statusLabel;

        // 통계 정보 섹션
        QGroupBox* m_statsGroup;
        QLabel* m_totalGamesLabel;
        QLabel* m_winsLabel;
        QLabel* m_lossesLabel;
        QLabel* m_winRateLabel;
        QLabel* m_averageScoreLabel;
        QLabel* m_totalScoreLabel;
        QLabel* m_bestScoreLabel;
        QProgressBar* m_expProgressBar;
        QLabel* m_expLabel;


        // 액션 버튼들
        QWidget* m_buttonWidget;
        QPushButton* m_addFriendButton;
        QPushButton* m_whisperButton;
        QPushButton* m_refreshButton;
        QPushButton* m_closeButton;

        // 배경 클릭 감지를 위한 타이머
        QTimer* m_backgroundClickTimer;
        QWidget* m_parentWidget;
    };

} // namespace Blokus