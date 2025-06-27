#pragma once

#include <QMainWindow>
#include <QWidget>
#include <QVBoxLayout>
#include <QHBoxLayout>
#include <QGridLayout>
#include <QSplitter>
#include <QLabel>
#include <QLineEdit>
#include <QPushButton>
#include <QListWidget>
#include <QTextEdit>
#include <QComboBox>
#include <QSpinBox>
#include <QProgressBar>
#include <QTimer>
#include <QGroupBox>
#include <QFrame>
#include <QScrollArea>
#include <QCloseEvent>
#include <QResizeEvent>
#include <QDebug>
#include <QList>

#include "ui/GameBoard.h"
#include "ui/ImprovedBlockPalette.h"
#include "game/GameLogic.h"
#include "common/Types.h"

namespace Blokus {

    // 플레이어 슬롯 정보
    struct PlayerSlot {
        PlayerColor color;          // 플레이어 색상
        QString username;           // 플레이어 이름 (빈 슬롯일 경우 "")
        bool isAI;                  // AI 플레이어 여부
        int aiDifficulty;           // AI 난이도 (1-3)
        bool isHost;                // 호스트 여부
        bool isReady;               // 준비 상태
        int score;                  // 현재 점수
        int remainingBlocks;        // 남은 블록 수

        PlayerSlot()
            : color(PlayerColor::None)
            , username("")
            , isAI(false)
            , aiDifficulty(2)
            , isHost(false)
            , isReady(false)
            , score(0)
            , remainingBlocks(BLOCKS_PER_PLAYER)
        {
        }

        bool isEmpty() const {
            return username.isEmpty() && !isAI;
        }

        QString getDisplayName() const {
            if (isEmpty()) {
                return QString::fromUtf8("빈 슬롯");
            }
            else if (isAI) {
                return QString::fromUtf8("AI (레벨 %1)").arg(aiDifficulty);
            }
            else {
                return username;
            }
        }
    };

    // 게임 룸 정보
    struct GameRoomInfo {
        int roomId;
        QString roomName;
        QString hostUsername;       // 현재 호스트
        PlayerColor hostColor;      // 호스트의 색상
        int maxPlayers;
        QString gameMode;
        bool isPlaying;             // 게임 진행 중 여부
        QList<PlayerSlot> playerSlots;  // 4개 슬롯 (파-노-빨-초)

        GameRoomInfo()
            : roomId(0)
            , roomName(QString::fromUtf8("새 방"))
            , hostUsername("")
            , hostColor(PlayerColor::Blue)
            , maxPlayers(4)
            , gameMode(QString::fromUtf8("클래식"))
            , isPlaying(false)
        {
            // 4개 색상 슬롯 초기화
            for (int i = 0; i < 4; ++i)
                playerSlots.append(PlayerSlot());

            playerSlots[0].color = PlayerColor::Blue;
            playerSlots[1].color = PlayerColor::Yellow;
            playerSlots[2].color = PlayerColor::Red;
            playerSlots[3].color = PlayerColor::Green;
        }

        int getCurrentPlayerCount() const {
            int count = 0;
            for (const auto& slot : playerSlots) {
                if (!slot.isEmpty()) count++;
            }
            return count;
        }

        PlayerColor getMyColor(const QString& username) const {
            for (const auto& slot : playerSlots) {
                if (slot.username == username) {
                    return slot.color;
                }
            }
            return PlayerColor::None;
        }

        bool isMyTurn(const QString& username, PlayerColor currentTurn) const {
            return getMyColor(username) == currentTurn;
        }
    };

    // 개별 플레이어 슬롯 위젯
    class PlayerSlotWidget : public QWidget
    {
        Q_OBJECT

    public:
        explicit PlayerSlotWidget(PlayerColor color, QWidget* parent = nullptr);

        void updatePlayerSlot(const PlayerSlot& slot);
        void setMySlot(bool isMySlot);
        PlayerColor getColor() const { return m_color; }
        void updateActionButton();

    signals:
        void addAIRequested(PlayerColor color, int difficulty);
        void removePlayerRequested(PlayerColor color);
        void kickPlayerRequested(PlayerColor color);

    private slots:
        void onAddAIClicked();
        void onRemoveClicked();
        void onKickClicked();

    private:
        void setupUI();
        void setupStyles();
        QString getColorName() const;
        QColor getPlayerColor() const;

    private:
        PlayerColor m_color;
        PlayerSlot m_currentSlot;
        bool m_isMySlot;

        // UI 컴포넌트
        QVBoxLayout* m_mainLayout;
        QFrame* m_colorFrame;
        QLabel* m_colorLabel;
        QLabel* m_usernameLabel;
        QLabel* m_statusLabel;
        QLabel* m_scoreLabel;
        QPushButton* m_actionButton;   // 상황에 따라 "AI 추가", "제거", "강퇴" 등
        QWidget* m_hostIndicator;      // 호스트 표시
    };

    // 메인 게임 룸 윈도우
    class GameRoomWindow : public QMainWindow
    {
        Q_OBJECT

    public:
        explicit GameRoomWindow(const GameRoomInfo& roomInfo, const QString& myUsername, QWidget* parent = nullptr);
        ~GameRoomWindow();

        // 룸 정보 업데이트
        void updateRoomInfo(const GameRoomInfo& roomInfo);
        void updatePlayerSlot(PlayerColor color, const PlayerSlot& slot);

        // 게임 상태 업데이트
        void startGame();
        void endGame(const std::map<PlayerColor, int>& finalScores);
        void updateGameState(const GameStateManager& gameManager);

        // 채팅 메시지 추가
        void addChatMessage(const QString& username, const QString& message, bool isSystem = false);
        void addSystemMessage(const QString& message);

    signals:
        // 룸 관리 시그널
        void leaveRoomRequested();
        void gameStartRequested();
        void addAIPlayerRequested(PlayerColor color, int difficulty);
        void removePlayerRequested(PlayerColor color);
        void kickPlayerRequested(PlayerColor color);

        // 게임 플레이 시그널
        void blockPlacedRequested(const Block& block, const Position& position);
        void turnSkipRequested();
        void gameResetRequested();

        // 채팅 시그널
        void chatMessageSent(const QString& message);

    private slots:
        // UI 이벤트 핸들러
        void onLeaveRoomClicked();
        void onGameStartClicked();
        void onGameResetClicked();
        void onChatSendClicked();
        void onChatReturnPressed();

        // 플레이어 슬롯 이벤트
        void onAddAIRequested(PlayerColor color, int difficulty);
        void onRemovePlayerRequested(PlayerColor color);
        void onKickPlayerRequested(PlayerColor color);

        // 게임보드 이벤트
        void onCellClicked(int row, int col);
        void onCellHovered(int row, int col);
        void onBlockPlacedSuccessfully(BlockType blockType, PlayerColor player);
        void onBlockSelected(const Block& block);

    protected:
        void closeEvent(QCloseEvent* event) override;
        void resizeEvent(QResizeEvent* event) override;

    private:
        // UI 구성 함수들
        void setupUI();
        void setupMenuBar();
        void setupStatusBar();
        void setupMainLayout();
        void setupRoomInfoPanel();       // 상단 룸 정보
        void setupPlayerSlotsPanel();    // 플레이어 슬롯들
        void setupGameArea();            // 게임 보드 + 팔레트
        void setupChatPanel();           // 우측 채팅
        void setupControlsPanel();       // 하단 컨트롤
        void setupStyles();

        // UI 업데이트 함수들
        void updateRoomInfoDisplay();
        void updatePlayerSlotsDisplay();
        void updateGameControlsState();
        void updateMyTurnIndicator();

        // 게임 상태 관리
        void enableGameControls(bool enabled);
        void showGameResults(const std::map<PlayerColor, int>& scores);

        // 호스트 권한 확인
        bool isHost() const;
        bool canStartGame() const;
        bool canAddAI() const;
        bool canKickPlayer(PlayerColor color);

        // 유틸리티 함수들
        void scrollChatToBottom();
        QString formatChatMessage(const QString& username, const QString& message, bool isSystem = false);
        PlayerSlot* findPlayerSlot(PlayerColor color);
        PlayerSlot* findPlayerSlot(const QString& username);
        PlayerColor getNextAvailableColor() const;

    private:
        // 기본 정보
        QString m_myUsername;
        GameRoomInfo m_roomInfo;
        GameStateManager* m_gameManager;

        // UI 컴포넌트들
        QWidget* m_centralWidget;
        QVBoxLayout* m_mainLayout;

        // 상단 룸 정보 패널
        QWidget* m_roomInfoPanel;
        QLabel* m_roomNameLabel;
        QLabel* m_roomStatusLabel;
        QLabel* m_currentTurnLabel;

        // 플레이어 슬롯들
        QWidget* m_playerSlotsPanel;
        QHBoxLayout* m_slotsLayout;
        QList<PlayerSlotWidget*> m_playerSlotWidgets;

        // 게임 영역
        QWidget* m_gameArea;
        QSplitter* m_gameSplitter;
        GameBoard* m_gameBoard;
        ImprovedGamePalette* m_blockPalette;

        // 채팅 패널
        QWidget* m_chatPanel;
        QTextEdit* m_chatDisplay;
        QLineEdit* m_chatInput;
        QPushButton* m_chatSendButton;

        // 컨트롤 패널
        QWidget* m_controlsPanel;
        QPushButton* m_leaveRoomButton;
        QPushButton* m_gameStartButton;
        QPushButton* m_gameResetButton;
        QLabel* m_gameStatusLabel;
        QLabel* m_coordinateLabel;

        // 상태 관리
        bool m_isGameStarted;
        QTimer* m_turnTimer;
        QList<QString> m_chatHistory;
    };

} // namespace Blokus