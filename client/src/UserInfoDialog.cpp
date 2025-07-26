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
        , m_levelLabel(nullptr)
        , m_statusLabel(nullptr)
        , m_onlineTimeLabel(nullptr)
        , m_statsGroup(nullptr)
        , m_totalGamesLabel(nullptr)
        , m_winsLabel(nullptr)
        , m_lossesLabel(nullptr)
        , m_winRateLabel(nullptr)
        , m_averageScoreLabel(nullptr)
        , m_rankLabel(nullptr)
        , m_expProgressBar(nullptr)
        , m_expLabel(nullptr)
        , m_historyGroup(nullptr)
        , m_historyTable(nullptr)
        , m_recentStatsLabel(nullptr)
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
        setupGameHistory();
        setupActionButtons();

        // 레이아웃에 추가
        contentLayout->addWidget(m_basicInfoGroup);
        contentLayout->addWidget(m_statsGroup);
        contentLayout->addWidget(m_historyGroup);
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

        // 사용자명
        m_usernameLabel = new QLabel();
        m_usernameLabel->setStyleSheet("font-size: 18px; font-weight: bold; color: #2c3e50;");

        // 레벨
        m_levelLabel = new QLabel();
        m_levelLabel->setStyleSheet("font-size: 14px; color: #27ae60; font-weight: bold;");

        // 상태
        m_statusLabel = new QLabel();
        m_statusLabel->setStyleSheet("font-size: 13px; color: #7f8c8d;");

        // 온라인 시간 (향후 구현용)
        m_onlineTimeLabel = new QLabel();
        m_onlineTimeLabel->setStyleSheet("font-size: 12px; color: #95a5a6;");

        // 레이아웃 배치
        layout->addWidget(m_avatarLabel, 0, 0, 3, 1);
        layout->addWidget(m_usernameLabel, 0, 1);
        layout->addWidget(m_levelLabel, 1, 1);
        layout->addWidget(m_statusLabel, 2, 1);
        layout->addWidget(m_onlineTimeLabel, 3, 0, 1, 2);

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

        QLabel* rankTitle = new QLabel(QString::fromUtf8("등급:"));
        rankTitle->setStyleSheet("font-weight: bold; color: #34495e;");
        m_rankLabel = new QLabel();

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
        layout->addWidget(rankTitle, 2, 2);
        layout->addWidget(m_rankLabel, 2, 3);

        layout->addWidget(expTitle, 3, 0);
        layout->addWidget(m_expProgressBar, 3, 1, 1, 2);
        layout->addWidget(m_expLabel, 3, 3);

        layout->setColumnStretch(1, 1);
        layout->setColumnStretch(3, 1);
    }

    void UserInfoDialog::setupGameHistory()
    {
        m_historyGroup = new QGroupBox(QString::fromUtf8("최근 게임 기록"));
        QVBoxLayout* layout = new QVBoxLayout(m_historyGroup);
        layout->setContentsMargins(15, 20, 15, 15);

        // 최근 통계 요약
        m_recentStatsLabel = new QLabel();
        m_recentStatsLabel->setStyleSheet("color: #7f8c8d; font-size: 12px; margin-bottom: 10px;");
        layout->addWidget(m_recentStatsLabel);

        // 게임 기록 테이블 (향후 서버에서 데이터 받을 때 활용)
        m_historyTable = new QTableWidget();
        m_historyTable->setColumnCount(3);
        m_historyTable->setHorizontalHeaderLabels({
            QString::fromUtf8("날짜"), QString::fromUtf8("결과"), QString::fromUtf8("점수")
        });
        m_historyTable->horizontalHeader()->setStretchLastSection(true);
        m_historyTable->verticalHeader()->setVisible(false);
        m_historyTable->setAlternatingRowColors(true);
        m_historyTable->setSelectionBehavior(QAbstractItemView::SelectRows);
        m_historyTable->setMaximumHeight(150);
        m_historyTable->setStyleSheet(
            "QTableWidget { gridline-color: #ddd; border: 1px solid #ddd; }"
            "QHeaderView::section { background-color: #34495e; color: white; font-weight: bold; padding: 5px; }"
        );

        layout->addWidget(m_historyTable);
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
        updateGameHistoryDisplay();
        
        // 자신의 정보인지 확인하여 버튼 표시 조정
        m_isOwnInfo = (userInfo.username == m_currentUsername);
        m_addFriendButton->setVisible(!m_isOwnInfo);
        m_whisperButton->setVisible(!m_isOwnInfo);
    }

    void UserInfoDialog::updateBasicInfoDisplay()
    {
        // 아바타에 첫 글자 표시
        if (!m_userInfo.username.isEmpty()) {
            m_avatarLabel->setText(m_userInfo.username.at(0).toUpper());
        }
        
        m_usernameLabel->setText(m_userInfo.username);
        m_levelLabel->setText(QString::fromUtf8("레벨 %1").arg(m_userInfo.level));
        
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
        
        // 온라인 시간 (향후 구현)
        m_onlineTimeLabel->setText(QString::fromUtf8("접속 시간: 정보 없음"));
    }

    void UserInfoDialog::updateStatsDisplay()
    {
        m_totalGamesLabel->setText(QString::number(m_userInfo.totalGames));
        m_winsLabel->setText(QString::number(m_userInfo.wins));
        m_lossesLabel->setText(QString::number(m_userInfo.losses));
        m_winRateLabel->setText(formatWinRate(m_userInfo.getWinRate()));
        m_averageScoreLabel->setText(QString::number(m_userInfo.averageScore));
        
        // 등급 표시
        QString rankText = getRankText(m_userInfo.getWinRate());
        QColor rankColor = getRankColor(m_userInfo.getWinRate());
        m_rankLabel->setText(rankText);
        m_rankLabel->setStyleSheet(QString("color: %1; font-weight: bold;").arg(rankColor.name()));
        
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

    void UserInfoDialog::updateGameHistoryDisplay()
    {
        // 최근 통계 요약
        int recentGames = std::min(m_userInfo.totalGames, 10);
        QString recentText = QString::fromUtf8("최근 %1게임 기록 (실제 데이터는 서버에서 받아올 예정)")
            .arg(recentGames);
        m_recentStatsLabel->setText(recentText);
        
        // 더미 데이터로 테이블 채우기 (실제로는 서버에서 받아올 예정)
        m_historyTable->setRowCount(3);
        
        QStringList dummyData = {
            QString::fromUtf8("2024-01-20|승리|89점"),
            QString::fromUtf8("2024-01-19|패배|45점"),
            QString::fromUtf8("2024-01-18|승리|92점")
        };
        
        for (int i = 0; i < dummyData.size(); ++i) {
            QStringList parts = dummyData[i].split("|");
            if (parts.size() == 3) {
                m_historyTable->setItem(i, 0, new QTableWidgetItem(parts[0]));
                
                QTableWidgetItem* resultItem = new QTableWidgetItem(parts[1]);
                if (parts[1] == QString::fromUtf8("승리")) {
                    resultItem->setForeground(QBrush(QColor("#27ae60")));
                } else {
                    resultItem->setForeground(QBrush(QColor("#e74c3c")));
                }
                m_historyTable->setItem(i, 1, resultItem);
                
                m_historyTable->setItem(i, 2, new QTableWidgetItem(parts[2]));
            }
        }
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

    QString UserInfoDialog::getRankText(double winRate) const
    {
        if (winRate >= 80.0) return QString::fromUtf8("🏆 마스터");
        else if (winRate >= 70.0) return QString::fromUtf8("💎 다이아몬드");
        else if (winRate >= 60.0) return QString::fromUtf8("🥇 골드");
        else if (winRate >= 50.0) return QString::fromUtf8("🥈 실버");
        else if (winRate >= 40.0) return QString::fromUtf8("🥉 브론즈");
        else return QString::fromUtf8("🪨 아이언");
    }

    QColor UserInfoDialog::getRankColor(double winRate) const
    {
        if (winRate >= 80.0) return QColor("#e74c3c");      // 빨강 (마스터)
        else if (winRate >= 70.0) return QColor("#9b59b6"); // 보라 (다이아)
        else if (winRate >= 60.0) return QColor("#f39c12"); // 금색 (골드)
        else if (winRate >= 50.0) return QColor("#95a5a6"); // 은색 (실버)
        else if (winRate >= 40.0) return QColor("#d35400"); // 구리색 (브론즈)
        else return QColor("#34495e");                      // 회색 (아이언)
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
        QTimer::singleShot(1000, [this]() {
            m_refreshButton->setText(QString::fromUtf8("새로고침"));
            m_refreshButton->setEnabled(true);
        });
    }

    void UserInfoDialog::onCloseClicked()
    {
        close();
    }

} // namespace Blokus

#include "UserInfoDialog.moc"