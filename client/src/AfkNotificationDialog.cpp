#include "AfkNotificationDialog.h"
#include <QCloseEvent>
#include <QKeyEvent>
#include <QFont>
#include <QPixmap>
#include <QIcon>
#include <QApplication>
#include <QDesktopWidget>
#include <QJsonDocument>
#include <QJsonObject>
#include <QTimer>

namespace Blokus {

    AfkNotificationDialog::AfkNotificationDialog(QWidget* parent)
        : QDialog(parent)
        , m_mainLayout(nullptr)
        , m_titleLabel(nullptr)
        , m_messageLabel(nullptr)
        , m_infoLabel(nullptr)
        , m_buttonLayout(nullptr)
        , m_continueButton(nullptr)
        , m_leaveButton(nullptr)
        , m_reason("timeout")
        , m_timeoutCount(3)
        , m_maxCount(3)
        , m_gameEnded(false)
    {
        setupUI();
        setupConnections();
        
        // 🔥 FIX: 비모달 설정으로 변경 (게임 종료 이벤트 수신 가능)
        setModal(false);
        setWindowFlags(Qt::Dialog | Qt::WindowTitleHint | Qt::CustomizeWindowHint | Qt::WindowStaysOnTopHint);
        setAttribute(Qt::WA_DeleteOnClose, false); // 수동으로 삭제 관리
        
        // 중앙 정렬
        if (parent) {
            move(parent->geometry().center() - rect().center());
        }
    }

    AfkNotificationDialog::~AfkNotificationDialog()
    {
        // Qt의 parent-child 관계로 자동 정리됨
    }

    void AfkNotificationDialog::setupUI()
    {
        setWindowTitle("AFK 모드 알림");
        setFixedSize(400, 250);
        
        // 메인 레이아웃
        m_mainLayout = new QVBoxLayout(this);
        m_mainLayout->setSpacing(15);
        m_mainLayout->setContentsMargins(20, 20, 20, 20);
        
        // 제목 라벨
        m_titleLabel = new QLabel("⚠️ AFK 모드로 전환되었습니다", this);
        QFont titleFont = m_titleLabel->font();
        titleFont.setPointSize(14);
        titleFont.setBold(true);
        m_titleLabel->setFont(titleFont);
        m_titleLabel->setAlignment(Qt::AlignCenter);
        m_titleLabel->setStyleSheet("color: #d32f2f; margin-bottom: 10px;");
        
        // 메시지 라벨
        m_messageLabel = new QLabel(this);
        m_messageLabel->setWordWrap(true);
        m_messageLabel->setAlignment(Qt::AlignCenter);
        m_messageLabel->setStyleSheet("color: #333; line-height: 1.4;");
        
        // 정보 라벨
        m_infoLabel = new QLabel(this);
        m_infoLabel->setWordWrap(true);
        m_infoLabel->setAlignment(Qt::AlignCenter);
        m_infoLabel->setStyleSheet("color: #666; font-size: 11px; margin-top: 10px;");
        
        // 버튼 레이아웃
        m_buttonLayout = new QHBoxLayout();
        m_buttonLayout->setSpacing(10);
        
        // 게임 계속하기 버튼
        m_continueButton = new QPushButton("🎮 게임 계속하기", this);
        m_continueButton->setMinimumHeight(40);
        m_continueButton->setStyleSheet(
            "QPushButton {"
            "    background-color: #4caf50;"
            "    color: white;"
            "    border: none;"
            "    border-radius: 6px;"
            "    font-weight: bold;"
            "    font-size: 13px;"
            "}"
            "QPushButton:hover {"
            "    background-color: #45a049;"
            "}"
            "QPushButton:pressed {"
            "    background-color: #3d8b40;"
            "}"
        );
        
        // 나가기 버튼
        m_leaveButton = new QPushButton("🚪 게임 나가기", this);
        m_leaveButton->setMinimumHeight(40);
        m_leaveButton->setStyleSheet(
            "QPushButton {"
            "    background-color: #f44336;"
            "    color: white;"
            "    border: none;"
            "    border-radius: 6px;"
            "    font-weight: bold;"
            "    font-size: 13px;"
            "}"
            "QPushButton:hover {"
            "    background-color: #da190b;"
            "}"
            "QPushButton:pressed {"
            "    background-color: #c1170c;"
            "}"
        );
        
        // 레이아웃에 추가
        m_buttonLayout->addWidget(m_continueButton);
        m_buttonLayout->addWidget(m_leaveButton);
        
        m_mainLayout->addWidget(m_titleLabel);
        m_mainLayout->addWidget(m_messageLabel);
        m_mainLayout->addWidget(m_infoLabel);
        m_mainLayout->addStretch();
        m_mainLayout->addLayout(m_buttonLayout);
        
        // 초기 메시지 업데이트
        updateMessage();
    }

    void AfkNotificationDialog::setupConnections()
    {
        connect(m_continueButton, &QPushButton::clicked, this, &AfkNotificationDialog::onContinueGameClicked);
        connect(m_leaveButton, &QPushButton::clicked, this, &AfkNotificationDialog::onLeaveGameClicked);
    }

    void AfkNotificationDialog::setAfkInfo(const QJsonObject& jsonData)
    {
        m_reason = jsonData["reason"].toString("timeout");
        m_timeoutCount = jsonData["timeoutCount"].toInt(3);
        m_maxCount = jsonData["maxCount"].toInt(3);
        
        updateMessage();
    }

    void AfkNotificationDialog::setAfkInfo(int timeoutCount, int maxCount)
    {
        m_reason = "timeout";
        m_timeoutCount = timeoutCount;
        m_maxCount = maxCount;
        
        updateMessage();
    }

    void AfkNotificationDialog::updateMessage()
    {
        if (!m_messageLabel || !m_infoLabel) {
            return;
        }
        
        QString message;
        QString info;
        
        if (m_reason == "timeout") {
            message = QString("연속으로 %1회 시간 초과가 발생하여<br>"
                            "<strong>자동 턴 스킵 모드</strong>로 전환되었습니다.<br><br>"
                            "게임을 계속하시려면 아래 버튼을 클릭해주세요.")
                            .arg(m_timeoutCount);
            
            info = QString("• 현재 %1/%2회 타임아웃 발생<br>"
                         "• 게임을 계속하면 타임아웃 카운터가 초기화됩니다<br>"
                         "• 게임당 최대 2회까지 AFK 해제가 가능합니다")
                         .arg(m_timeoutCount).arg(m_maxCount);
        } else {
            message = "AFK 모드로 전환되었습니다.<br>게임을 계속하시겠습니까?";
            info = "게임을 계속하려면 아래 버튼을 클릭해주세요.";
        }
        
        m_messageLabel->setText(message);
        m_infoLabel->setText(info);
    }

    void AfkNotificationDialog::onContinueGameClicked()
    {
        // 🔥 CRITICAL: 게임 종료 상태에서는 AFK 해제 요청 차단
        if (m_gameEnded) {
            qDebug() << "게임이 이미 종료되어 AFK 해제 요청을 차단합니다.";
            accept(); // 그냥 다이얼로그만 닫기
            return;
        }
        
        // AFK 해제 요청 시그널 발생
        emit afkUnblockRequested();
        
        // 대화상자 닫기
        accept();
    }

    void AfkNotificationDialog::onLeaveGameClicked()
    {
        // 게임 나가기 (reject로 처리)
        reject();
    }

    void AfkNotificationDialog::closeEvent(QCloseEvent* event)
    {
        // ESC나 X 버튼으로 닫기 차단 (명시적 선택 강제)
        event->ignore();
    }

    void AfkNotificationDialog::keyPressEvent(QKeyEvent* event)
    {
        // ESC 키 차단
        if (event->key() == Qt::Key_Escape) {
            event->ignore();
            return;
        }
        
        // Enter 키는 게임 계속하기로 처리
        if (event->key() == Qt::Key_Return || event->key() == Qt::Key_Enter) {
            onContinueGameClicked();
            return;
        }
        
        QDialog::keyPressEvent(event);
    }

    void AfkNotificationDialog::onGameEnded()
    {
        // 게임이 종료되었음을 표시
        m_gameEnded = true;
        
        // UI 업데이트
        m_titleLabel->setText("게임 종료됨");
        m_messageLabel->setText("게임이 종료되었습니다.");
        m_infoLabel->setText("3초 후 자동으로 닫힙니다.");
        
        // 계속하기 버튼 비활성화
        m_continueButton->setEnabled(false);
        m_continueButton->setText("게임 종료됨");
        
        // 나가기만 활성화
        m_leaveButton->setText("확인");
        m_leaveButton->setFocus();
        
        // 🔥 CRITICAL: 즉시 닫기 (사용자 실수 클릭 방지)
        QTimer::singleShot(1000, this, [this]() {
            this->accept();
        });
    }

    void AfkNotificationDialog::onAfkUnblockError(const QString& reason, const QString& message)
    {
        if (reason == "game_not_active") {
            // 게임이 비활성 상태면 게임 종료로 처리
            onGameEnded();
        } else {
            // 기타 에러 처리
            m_messageLabel->setText(QString("오류: %1").arg(message));
            m_continueButton->setEnabled(false);
        }
    }

} // namespace Blokus