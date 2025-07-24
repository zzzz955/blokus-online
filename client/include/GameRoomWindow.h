#pragma once

#include <QMainWindow>
#include <QWidget>
#include <QVBoxLayout>
#include <QHBoxLayout>
#include <QGridLayout>
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
#include <QSet>

#include "GameBoard.h"
#include "ClientLogic.h"
#include "ClientTypes.h"

namespace Blokus {

    // 전방 선언
    class MyBlockPalette;

    // 내 블록 팔레트 클래스
    class MyBlockPalette : public QWidget
    {
        Q_OBJECT

    public:
        explicit MyBlockPalette(QWidget* parent = nullptr);

        void setPlayer(PlayerColor player);
        void removeBlock(BlockType blockType);
        void resetAllBlocks();
        void setEnabled(bool enabled);
        void clearSelection();

    signals:
        void blockSelected(const Block& block);

    private slots:
        void onBlockButtonClicked();

    private:
        void setupUI();
        void updateBlockButtons();
        void clearBlockButtons();
        QColor getPlayerColor() const;

    private:
        PlayerColor m_player;
        QVBoxLayout* m_mainLayout;
        QScrollArea* m_scrollArea;
        QWidget* m_blockContainer;
        QGridLayout* m_blockGrid;
        std::vector<Block> m_availableBlocks;
        std::map<BlockType, QPushButton*> m_blockButtons;
        Block m_selectedBlock;
        bool m_hasSelection;
        QPushButton* m_selectedButton;
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
        void updateReadyState(bool isReady);
        void setCurrentTurn(bool isCurrentTurn);

    signals:
        void kickPlayerRequested(PlayerColor color);

    private slots:
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
        QLabel* m_scoreLabel;
        QLabel* m_remainingBlocksLabel;
        QPushButton* m_actionButton;
        QWidget* m_hostIndicator;
        QLabel* m_readyIndicator;
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
        void updatePlayerReadyState(const QString& username, bool ready);

        // 게임 상태 업데이트
        void startGame();
        void endGame(const std::map<PlayerColor, int>& finalScores);
        void updateGameState(const GameStateManager& gameManager);

        // 채팅 메시지 추가
        void addChatMessage(const QString& username, const QString& message, bool isSystem = false);
        void addSystemMessage(const QString& message);
        
        // 서버 통신
        void sendBlockPlacementToServer(BlockType blockType, PlayerColor playerColor, int row, int col, int rotation, int flip);

        // 호스트 권한 확인
        bool isHost() const;
        
        // 내 준비 상태 설정
        void setMyReadyState(bool ready);
        
        // 턴 전환 알림
        void showTurnChangeNotification(const QString& playerName, bool isMyTurn);
        
        // 게임 리셋 (대기 상태로 복원)
        void resetGameToWaitingState();

    signals:
        // 룸 관리 시그널
        void leaveRoomRequested();
        void gameStartRequested();
        void kickPlayerRequested(PlayerColor color);

        // 게임 플레이 시그널
        void blockPlacedRequested(const Block& block, const Position& position);
        void blockPlacementRequested(const QString& gameMessage);
        void turnSkipRequested();

        // 채팅 시그널
        void chatMessageSent(const QString& message);
        
        // 준비 상태 시그널
        void playerReadyChanged(bool ready);

    public slots:
        // UI 이벤트 핸들러
        void onLeaveRoomClicked();
        
        // 게임 상태 동기화 슬롯
        void onGameStateUpdated(const QString& gameStateJson);
        void onBlockPlaced(const QString& playerName, int blockType, int row, int col, int rotation, int flip, int playerColor, int scoreGained);
        void onTurnChanged(const QString& newPlayerName, int playerColor, int turnNumber);

    private slots:
        void onGameStartClicked();
        void onReadyToggleClicked();
        void onChatSendClicked();
        void onChatReturnPressed();

        // 플레이어 슬롯 이벤트
        void onKickPlayerRequested(PlayerColor color);

        // 게임보드 이벤트
        void onCellClicked(int row, int col);
        void onCellHovered(int row, int col);
        void onBlockPlacedSuccessfully(BlockType blockType, PlayerColor player, int row, int col, int rotation, int flip);
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
        void setupRoomInfoPanel();
        void setupPlayerSlotsPanel();
        void setupGameArea();
        void setupChatPanel();
        void setupControlsPanel();
        void setupStyles();

        // UI 업데이트 함수들
        void updateRoomInfoDisplay();
        void updatePlayerSlotsDisplay();
        void updateGameControlsState();
        void updateReadyStates();

        // 게임 상태 관리
        void enableGameControls(bool enabled);
        void showGameResults(const std::map<PlayerColor, int>& scores);
        void showFinalResults();
        void resetGameToWaitingState();
        void checkGameEndConditions();
        void checkAndSkipPlayerTurn();

        // 플레이어 상태 업데이트
        void updatePlayerScore(PlayerColor player, int scoreToAdd);
        void updatePlayerRemainingBlocks(PlayerColor player, int change);
        void setPlayerScore(PlayerColor player, int score);
        void setPlayerRemainingBlocks(PlayerColor player, int remainingBlocks);

        // 권한 확인
        bool canStartGame() const;
        bool canKickPlayer(PlayerColor color);
        bool areAllPlayersReady() const;

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
        QPushButton* m_leaveRoomButton;

        // 플레이어 슬롯들
        QWidget* m_playerSlotsPanel;
        QHBoxLayout* m_slotsLayout;
        QList<PlayerSlotWidget*> m_playerSlotWidgets;

        // 게임 영역
        QWidget* m_gameArea;
        GameBoard* m_gameBoard;
        MyBlockPalette* m_myBlockPalette;

        // 채팅 패널
        QWidget* m_chatPanel;
        QTextEdit* m_chatDisplay;
        QLineEdit* m_chatInput;
        QPushButton* m_chatSendButton;

        // 컨트롤 패널
        QWidget* m_controlsPanel;
        QPushButton* m_gameStartButton;
        QLabel* m_gameStatusLabel;
        QLabel* m_coordinateLabel;

        // 상태 관리
        bool m_isGameStarted;
        bool m_isReady;              // 내 준비 상태
        PlayerColor m_previousTurn;  // 이전 턴 추적용
        QTimer* m_turnTimer;
        QTimer* m_readyButtonTimeout; // 준비 버튼 타임아웃
        QList<QString> m_chatHistory;
    };

} // namespace Blokus