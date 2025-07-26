#include "UserInfoDialog.h"
#include <QApplication>
#include <QDesktopWidget>
#include <QHeaderView>
#include <QMessageBox>
#include <QMouseEvent>
#include <QPainter>
#include <QBrush>
#include <QPen>
#include <QDebug>
#include <QTableWidgetItem>

namespace Blokus {

    UserInfoDialog::UserInfoDialog(const UserInfo& userInfo, QWidget* parent)
        : QDialog(parent)
        , m_userInfo(userInfo)
        , m_isOwnInfo(false)
        , m_currentUsername("")
        , m_mainLayout(nullptr)
        , m_scrollArea(nullptr)
        , m_contentWidget(nullptr)
        , m_basicInfoGroup(nullptr)
        , m_avatarLabel(nullptr)
        , m_usernameLabel(nullptr)
        , m_statusLabel(nullptr)
        , m_statsGroup(nullptr)
        , m_totalGamesLabel(nullptr)
        , m_winsLabel(nullptr)
        , m_lossesLabel(nullptr)
        , m_winRateLabel(nullptr)
        , m_averageScoreLabel(nullptr)
        , m_expProgressBar(nullptr)
        , m_expLabel(nullptr)
        , m_buttonWidget(nullptr)
        , m_addFriendButton(nullptr)
        , m_whisperButton(nullptr)
        , m_refreshButton(nullptr)
        , m_closeButton(nullptr)
        , m_backgroundClickTimer(new QTimer(this))
        , m_parentWidget(parent)
    {
        setWindowTitle(QString::fromUtf8("%1님의 정보").arg(userInfo.username));
        setModal(false); // 비모달로 설정
        setWindowFlags(Qt::Dialog | Qt::CustomizeWindowHint | Qt::WindowTitleHint | Qt::WindowCloseButtonHint);
        
        setupUI();
        setupStyles();
        updateUserInfo(userInfo);
        
        // 크기 설정
        setFixedSize(450, 600);
        
        // 화면 중앙에 배치하되 약간 오른쪽으로 이동
        if (parent) {
            QRect parentGeometry = parent->geometry();
            int x = parentGeometry.x() + parentGeometry.width() - width() - 50;
            int y = parentGeometry.y() + (parentGeometry.height() - height()) / 2;
            move(x, y);
        } else {
            QRect screenGeometry = QApplication::desktop()->screenGeometry();
            int x = (screenGeometry.width() - width()) / 2 + 100;
            int y = (screenGeometry.height() - height()) / 2;
            move(x, y);
        }
        
        // 배경 클릭 감지 설정
        installBackgroundEventFilter();
        
        // 타이머 설정 (100ms마다 체크)
        m_backgroundClickTimer->setInterval(100);
        m_backgroundClickTimer->setSingleShot(true);
    }

    UserInfoDialog::~UserInfoDialog()
    {
        removeBackgroundEventFilter();
    }

    void UserInfoDialog::setupUI()
    {
        m_mainLayout = new QVBoxLayout(this);
        m_mainLayout->setContentsMargins(20, 20, 20, 20);
        m_mainLayout->setSpacing(15);

        // 스크롤 영역 설정
        m_scrollArea = new QScrollArea();
        m_scrollArea->setWidgetResizable(true);
        m_scrollArea->setHorizontalScrollBarPolicy(Qt::ScrollBarAlwaysOff);
        m_scrollArea->setVerticalScrollBarPolicy(Qt::ScrollBarAsNeeded);

        m_contentWidget = new QWidget();
        QVBoxLayout* contentLayout = new QVBoxLayout(m_contentWidget);
        contentLayout->setContentsMargins(10, 10, 10, 10);
        contentLayout->setSpacing(15);

        // UI 섹션들 설정
        setupBasicInfo();
        setupStatsInfo();
        setupActionButtons();

        // 레이아웃에 추가
        contentLayout->addWidget(m_basicInfoGroup);
        contentLayout->addWidget(m_statsGroup);
        contentLayout->addStretch();

        m_scrollArea->setWidget(m_contentWidget);
        m_mainLayout->addWidget(m_scrollArea);
        m_mainLayout->addWidget(m_buttonWidget);
    }

    void UserInfoDialog::setupBasicInfo()
    {
        m_basicInfoGroup = new QGroupBox(QString::fromUtf8("기본 정보"));
        QGridLayout* layout = new QGridLayout(m_basicInfoGroup);
        layout->setContentsMargins(15, 20, 15, 15);
        layout->setSpacing(10);

        // 아바타 (향후 구현용)
        m_avatarLabel = new QLabel();
        m_avatarLabel->setFixedSize(80, 80);
        m_avatarLabel->setStyleSheet(
            "QLabel { "
            "border: 3px solid #3498db; "
            "border-radius: 40px; "
            "background-color: #ecf0f1; "
            "font-size: 24px; "
            "font-weight: bold; "
            "color: #3498db; "
            "}"
        );
        m_avatarLabel->setAlignment(Qt::AlignCenter);
        m_avatarLabel->setText("👤");

        // 사용자명 (레벨 포함)
        m_usernameLabel = new QLabel();
        m_usernameLabel->setStyleSheet("font-size: 16px; font-weight: bold; color: #2c3e50;");

        // 상태
        m_statusLabel = new QLabel();
        m_statusLabel->setStyleSheet("font-size: 13px; color: #7f8c8d;");

        // 레이아웃 배치
        layout->addWidget(m_avatarLabel, 0, 0, 3, 1);
        layout->addWidget(m_usernameLabel, 0, 1);
        layout->addWidget(m_statusLabel, 1, 1);

        layout->setColumnStretch(1, 1);
    }

    void UserInfoDialog::setupStatsInfo()
    {
        m_statsGroup = new QGroupBox(QString::fromUtf8("게임 통계"));
        QGridLayout* layout = new QGridLayout(m_statsGroup);
        layout->setContentsMargins(15, 20, 15, 15);
        layout->setSpacing(10);

        // 통계 라벨들
        QLabel* totalGamesTitle = new QLabel(QString::fromUtf8("총 게임 수:"));
        totalGamesTitle->setStyleSheet("font-weight: bold; color: #34495e;");
        m_totalGamesLabel = new QLabel();
        m_totalGamesLabel->setStyleSheet("color: #2c3e50;");

        QLabel* winsTitle = new QLabel(QString::fromUtf8("승리:"));
        winsTitle->setStyleSheet("font-weight: bold; color: #34495e;");
        m_winsLabel = new QLabel();
        m_winsLabel->setStyleSheet("color: #27ae60; font-weight: bold;");

        QLabel* lossesTitle = new QLabel(QString::fromUtf8("패배:"));
        lossesTitle->setStyleSheet("font-weight: bold; color: #34495e;");
        m_lossesLabel = new QLabel();
        m_lossesLabel->setStyleSheet("color: #e74c3c; font-weight: bold;");

        QLabel* winRateTitle = new QLabel(QString::fromUtf8("승률:"));
        winRateTitle->setStyleSheet("font-weight: bold; color: #34495e;");
        m_winRateLabel = new QLabel();
        m_winRateLabel->setStyleSheet("color: #3498db; font-weight: bold; font-size: 14px;");

        QLabel* averageScoreTitle = new QLabel(QString::fromUtf8("평균 점수:"));
        averageScoreTitle->setStyleSheet("font-weight: bold; color: #34495e;");
        m_averageScoreLabel = new QLabel();
        m_averageScoreLabel->setStyleSheet("color: #f39c12; font-weight: bold;");

        QLabel* totalScoreTitle = new QLabel(QString::fromUtf8("누적 점수:"));
        totalScoreTitle->setStyleSheet("font-weight: bold; color: #34495e;");
        m_totalScoreLabel = new QLabel();
        m_totalScoreLabel->setStyleSheet("color: #9b59b6; font-weight: bold;");

        QLabel* bestScoreTitle = new QLabel(QString::fromUtf8("최고 점수:"));
        bestScoreTitle->setStyleSheet("font-weight: bold; color: #34495e;");
        m_bestScoreLabel = new QLabel();
        m_bestScoreLabel->setStyleSheet("color: #e67e22; font-weight: bold; font-size: 14px;");

        // 경험치 바
        QLabel* expTitle = new QLabel(QString::fromUtf8("경험치:"));
        expTitle->setStyleSheet("font-weight: bold; color: #34495e;");
        
        m_expProgressBar = new QProgressBar();
        m_expProgressBar->setFixedHeight(20);
        m_expProgressBar->setStyleSheet(
            "QProgressBar {"
            "    border: 2px solid #bdc3c7;"
            "    border-radius: 10px;"
            "    background-color: #ecf0f1;"
            "    text-align: center;"
            "    font-size: 11px;"
            "    color: #2c3e50;"
            "}"
            "QProgressBar::chunk {"
            "    background: qlineargradient(x1:0, y1:0, x2:1, y2:0, stop:0 #3498db, stop:1 #2980b9);"
            "    border-radius: 8px;"
            "}"
        );

        m_expLabel = new QLabel();
        m_expLabel->setStyleSheet("color: #7f8c8d; font-size: 11px;");

        // 레이아웃 배치 (2열)
        layout->addWidget(totalGamesTitle, 0, 0);
        layout->addWidget(m_totalGamesLabel, 0, 1);
        layout->addWidget(winsTitle, 0, 2);
        layout->addWidget(m_winsLabel, 0, 3);

        layout->addWidget(lossesTitle, 1, 0);
        layout->addWidget(m_lossesLabel, 1, 1);
        layout->addWidget(winRateTitle, 1, 2);
        layout->addWidget(m_winRateLabel, 1, 3);

        layout->addWidget(averageScoreTitle, 2, 0);
        layout->addWidget(m_averageScoreLabel, 2, 1);
        layout->addWidget(totalScoreTitle, 2, 2);
        layout->addWidget(m_totalScoreLabel, 2, 3);

        layout->addWidget(bestScoreTitle, 3, 0);
        layout->addWidget(m_bestScoreLabel, 3, 1);

        layout->addWidget(expTitle, 4, 0);
        layout->addWidget(m_expProgressBar, 4, 1, 1, 2);
        layout->addWidget(m_expLabel, 4, 3);

        layout->setColumnStretch(1, 1);
        layout->setColumnStretch(3, 1);
    }


    void UserInfoDialog::setupActionButtons()
    {
        m_buttonWidget = new QWidget();
        QHBoxLayout* layout = new QHBoxLayout(m_buttonWidget);
        layout->setContentsMargins(0, 10, 0, 0);

        m_addFriendButton = new QPushButton(QString::fromUtf8("친구 추가"));
        m_whisperButton = new QPushButton(QString::fromUtf8("귓속말"));
        m_refreshButton = new QPushButton(QString::fromUtf8("새로고침"));
        m_closeButton = new QPushButton(QString::fromUtf8("닫기"));

        m_addFriendButton->setMinimumHeight(35);
        m_whisperButton->setMinimumHeight(35);
        m_refreshButton->setMinimumHeight(35);
        m_closeButton->setMinimumHeight(35);

        layout->addWidget(m_addFriendButton);
        layout->addWidget(m_whisperButton);
        layout->addStretch();
        layout->addWidget(m_refreshButton);
        layout->addWidget(m_closeButton);

        // 시그널 연결
        connect(m_addFriendButton, &QPushButton::clicked, this, &UserInfoDialog::onAddFriendClicked);
        connect(m_whisperButton, &QPushButton::clicked, this, &UserInfoDialog::onSendWhisperClicked);
        connect(m_refreshButton, &QPushButton::clicked, this, &UserInfoDialog::onRefreshClicked);
        connect(m_closeButton, &QPushButton::clicked, this, &UserInfoDialog::onCloseClicked);
    }

    void UserInfoDialog::setupStyles()
    {
        setStyleSheet(
            "QDialog { background-color: #f8f9fa; } "
            
            "QGroupBox { "
            "    font-weight: bold; "
            "    font-size: 14px; "
            "    color: #2c3e50; "
            "    border: 2px solid #bdc3c7; "
            "    border-radius: 8px; "
            "    margin-top: 10px; "
            "    padding-top: 10px; "
            "    background-color: white; "
            "} "
            "QGroupBox::title { "
            "    subcontrol-origin: margin; "
            "    left: 10px; "
            "    padding: 0 8px 0 8px; "
            "    background-color: #f8f9fa; "
            "} "
            
            "QPushButton { "
            "    border: none; "
            "    border-radius: 6px; "
            "    font-weight: bold; "
            "    font-size: 13px; "
            "    padding: 8px 15px; "
            "    min-width: 80px; "
            "} "
            "QPushButton[text*='친구'] { background-color: #27ae60; color: white; } "
            "QPushButton[text*='친구']:hover { background-color: #229954; } "
            "QPushButton[text*='귓속말'] { background-color: #f39c12; color: white; } "
            "QPushButton[text*='귓속말']:hover { background-color: #e67e22; } "
            "QPushButton[text*='새로고침'] { background-color: #3498db; color: white; } "
            "QPushButton[text*='새로고침']:hover { background-color: #2980b9; } "
            "QPushButton[text*='닫기'] { background-color: #95a5a6; color: white; } "
            "QPushButton[text*='닫기']:hover { background-color: #7f8c8d; } "
            
            "QScrollArea { "
            "    border: none; "
            "    background-color: transparent; "
            "} "
        );
    }

    void UserInfoDialog::updateUserInfo(const UserInfo& userInfo)
    {
        m_userInfo = userInfo;
        updateBasicInfoDisplay();
        updateStatsDisplay();
        
        // 자신의 정보인지 확인하여 버튼 표시 조정
        m_isOwnInfo = (userInfo.username == m_currentUsername);
        m_addFriendButton->setVisible(!m_isOwnInfo);
        m_whisperButton->setVisible(!m_isOwnInfo);
    }

    void UserInfoDialog::setCurrentUsername(const QString& currentUsername)
    {
        m_currentUsername = currentUsername;
        
        // 버튼 표시 업데이트
        m_isOwnInfo = (m_userInfo.username == m_currentUsername);
        m_addFriendButton->setVisible(!m_isOwnInfo);
        m_whisperButton->setVisible(!m_isOwnInfo);
    }

    void UserInfoDialog::updateBasicInfoDisplay()
    {
        qDebug() << QString::fromUtf8("UserInfoDialog::updateBasicInfoDisplay() - 사용자명: '%1', 레벨: %2")
            .arg(m_userInfo.username).arg(m_userInfo.level);
            
        // 아바타에 첫 글자 표시
        if (!m_userInfo.username.isEmpty()) {
            m_avatarLabel->setText(m_userInfo.username.at(0).toUpper());
        }
        
        m_usernameLabel->setText(QString::fromUtf8("%1 (레벨 %2)").arg(m_userInfo.username).arg(m_userInfo.level));
        
        // 상태 표시 (아이콘과 함께)
        QString statusText;
        if (m_userInfo.isOnline) {
            if (m_userInfo.status == QString::fromUtf8("게임중")) {
                statusText = QString::fromUtf8("🎮 게임중");
            } else if (m_userInfo.status == QString::fromUtf8("자리비움")) {
                statusText = QString::fromUtf8("💤 자리비움");
            } else {
                statusText = QString::fromUtf8("🟢 온라인");
            }
        } else {
            statusText = QString::fromUtf8("⚫ 오프라인");
        }
        m_statusLabel->setText(statusText);
    }

    void UserInfoDialog::updateStatsDisplay()
    {
        m_totalGamesLabel->setText(QString::number(m_userInfo.totalGames));
        m_winsLabel->setText(QString::number(m_userInfo.wins));
        m_lossesLabel->setText(QString::number(m_userInfo.losses));
        m_winRateLabel->setText(formatWinRate(m_userInfo.getWinRate()));
        m_averageScoreLabel->setText(QString::number(m_userInfo.averageScore, 'f', 1));
        m_totalScoreLabel->setText(QString::number(m_userInfo.totalScore));
        m_bestScoreLabel->setText(QString::number(m_userInfo.bestScore));
        
        // 경험치 바 업데이트
        if (m_userInfo.requiredExp > 0) {
            m_expProgressBar->setMaximum(m_userInfo.requiredExp);
            m_expProgressBar->setValue(m_userInfo.experience);
            double progress = static_cast<double>(m_userInfo.experience) / m_userInfo.requiredExp * 100.0;
            m_expProgressBar->setFormat(QString("%1%").arg(QString::number(progress, 'f', 1)));
        } else {
            m_expProgressBar->setMaximum(100);
            m_expProgressBar->setValue(100);
            m_expProgressBar->setFormat(QString::fromUtf8("MAX"));
        }
        
        m_expLabel->setText(QString::fromUtf8("%1/%2")
            .arg(m_userInfo.experience)
            .arg(m_userInfo.requiredExp));
    }


    void UserInfoDialog::installBackgroundEventFilter()
    {
        if (m_parentWidget) {
            m_parentWidget->installEventFilter(this);
        }
    }

    void UserInfoDialog::removeBackgroundEventFilter()
    {
        if (m_parentWidget) {
            m_parentWidget->removeEventFilter(this);
        }
    }

    bool UserInfoDialog::eventFilter(QObject* obj, QEvent* event)
    {
        if (obj == m_parentWidget && event->type() == QEvent::MouseButtonPress) {
            QMouseEvent* mouseEvent = static_cast<QMouseEvent*>(event);
            
            // 모달 영역 밖을 클릭했는지 확인
            QPoint globalPos = mouseEvent->globalPos();
            QRect dialogRect = QRect(mapToGlobal(QPoint(0, 0)), size());
            
            if (!dialogRect.contains(globalPos)) {
                // 배경 클릭으로 간주하고 닫기
                close();
                return false; // 이벤트 전파 허용
            }
        }
        
        return QDialog::eventFilter(obj, event);
    }

    void UserInfoDialog::paintEvent(QPaintEvent* event)
    {
        QDialog::paintEvent(event);
        
        // 반투명 배경 효과 (선택사항)
        QPainter painter(this);
        painter.setRenderHint(QPainter::Antialiasing);
        
        // 모서리 둥글게
        QRect rect = this->rect().adjusted(1, 1, -1, -1);
        painter.setPen(QPen(QColor("#bdc3c7"), 2));
        painter.setBrush(QBrush(QColor("#f8f9fa")));
        painter.drawRoundedRect(rect, 10, 10);
    }

    void UserInfoDialog::mousePressEvent(QMouseEvent* event)
    {
        // 다이얼로그 내부 클릭은 정상 처리
        QDialog::mousePressEvent(event);
    }

    QString UserInfoDialog::formatWinRate(double winRate) const
    {
        return QString("%1%").arg(QString::number(winRate, 'f', 1));
    }

    QString UserInfoDialog::formatLevel(int level) const
    {
        return QString::fromUtf8("레벨 %1").arg(level);
    }

    QString UserInfoDialog::formatGameCount(int games) const
    {
        return QString::fromUtf8("%1게임").arg(games);
    }


    // 슬롯 구현
    void UserInfoDialog::onAddFriendClicked()
    {
        emit addFriendRequested(m_userInfo.username);
        QMessageBox::information(this, QString::fromUtf8("친구 추가"), 
            QString::fromUtf8("%1님에게 친구 요청을 보냈습니다. (구현 예정)").arg(m_userInfo.username));
    }

    void UserInfoDialog::onSendWhisperClicked()
    {
        emit sendWhisperRequested(m_userInfo.username);
        // 귓속말 창 열기는 부모 위젯에서 처리
        close();
    }

    void UserInfoDialog::onRefreshClicked()
    {
        emit getUserStatsRequested(m_userInfo.username);
        
        // 새로고침 중 표시
        m_refreshButton->setText(QString::fromUtf8("새로고침 중..."));
        m_refreshButton->setEnabled(false);
        
        // 1초 후 원래 상태로 복구 (실제로는 서버 응답 후 복구)
        QTimer::singleShot(1000, this, [this]() {
            if (m_refreshButton) {
                m_refreshButton->setText(QString::fromUtf8("새로고침"));
                m_refreshButton->setEnabled(true);
            }
        });
    }

    void UserInfoDialog::onCloseClicked()
    {
        close();
    }

} // namespace Blokus

#include "UserInfoDialog.moc"