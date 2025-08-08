#pragma once

#include <QDialog>
#include <QWidget>
#include <QVBoxLayout>
#include <QHBoxLayout>
#include <QGridLayout>
#include <QGroupBox>
#include <QLabel>
#include <QComboBox>
#include <QCheckBox>
#include <QSlider>
#include <QPushButton>
#include <QTimer>
#include <QFrame>
#include <QSpacerItem>

#include "ClientTypes.h"
#include "BGMManager.h"

namespace Blokus {

    /**
     * @brief UserSettingsDialog - 사용자 환경 설정 다이얼로그
     * 
     * 🎮 게임 환경 설정을 위한 비모달 다이얼로그
     * - UI 설정: 테마, 언어 선택
     * - 알림 설정: 게임 초대, 친구 접속, 시스템 메시지 (현재 비활성화)
     * - 오디오 설정: BGM/효과음 음소거 및 음량 제어
     * - 실시간 미리보기: 설정 변경 시 즉시 적용
     * - 변경 감지: 실제 변경된 설정만 서버 전송
     * 
     * 🔥 핵심 기능: BGMManager 연동, 테마 시스템 통합
     */
    class UserSettingsDialog : public QDialog
    {
        Q_OBJECT

    public:
        explicit UserSettingsDialog(QWidget* parent = nullptr);
        ~UserSettingsDialog();

        // 설정 데이터 관리
        void setCurrentSettings(const UserSettings& settings);
        UserSettings getCurrentSettings() const;
        
        // 변경 감지
        bool hasChanges() const;
        void resetToDefaults();

    signals:
        // 설정 변경 시그널
        void settingsUpdateRequested(const UserSettings& newSettings);
        void settingsChanged(const UserSettings& newSettings);  // 실시간 변경 알림
        
        // 테마 변경 시그널 (즉시 적용용)
        void themeChangeRequested(ThemeType theme);

    public slots:
        // 외부에서 설정 업데이트
        void onSettingsUpdated(const UserSettings& settings);
        void onSettingsUpdateFailed(const QString& errorMessage);

    private slots:
        // UI 이벤트 핸들러
        void onThemeChanged();
        void onLanguageChanged();
        
        // 알림 설정 (현재 비활성화)
        void onNotificationSettingChanged();
        
        // 오디오 설정
        void onBGMSettingChanged();
        void onSFXSettingChanged();
        void onVolumeSliderChanged();
        
        // 다이얼로그 버튼
        void onOkClicked();
        void onCancelClicked();
        void onResetClicked();
        
        // 실시간 미리보기
        void onPreviewTimer();

    protected:
        // 다이얼로그 이벤트
        void closeEvent(QCloseEvent* event) override;
        void keyPressEvent(QKeyEvent* event) override;

    private:
        // UI 구성 함수들
        void setupUI();
        void setupUIGroup();        // UI 설정 그룹
        void setupNotificationGroup(); // 알림 설정 그룹
        void setupAudioGroup();     // 오디오 설정 그룹
        void setupButtonGroup();    // 확인/취소 버튼
        void setupStyles();         // 스타일 적용
        
        // UI 업데이트 함수들
        void updateUIFromSettings(const UserSettings& settings);
        void updateSettingsFromUI();
        void updateAudioControls();
        void updateNotificationControls();
        
        // 미리보기 및 적용
        void applyThemePreview();
        void applyAudioSettings();
        void startPreviewTimer();
        void stopPreviewTimer();
        
        // 유틸리티 함수들
        QString formatVolumeText(int volume) const;
        void enableNotificationControls(bool enabled);
        void resetUIToDefaults();

    private:
        // 설정 데이터
        UserSettings m_originalSettings;  // 원본 설정 (취소 시 복원용)
        UserSettings m_currentSettings;   // 현재 설정
        bool m_hasUnsavedChanges;        // 변경사항 여부
        
        // 메인 레이아웃
        QVBoxLayout* m_mainLayout;
        
        // UI 설정 그룹
        QGroupBox* m_uiGroup;
        QGridLayout* m_uiLayout;
        QLabel* m_themeLabel;
        QComboBox* m_themeCombo;
        QLabel* m_languageLabel;
        QComboBox* m_languageCombo;
        
        // 알림 설정 그룹 (현재 비활성화)
        QGroupBox* m_notificationGroup;
        QVBoxLayout* m_notificationLayout;
        QCheckBox* m_inviteNotificationCheck;
        QCheckBox* m_friendNotificationCheck;
        QCheckBox* m_systemNotificationCheck;
        QLabel* m_notificationDisabledLabel;
        
        // 오디오 설정 그룹
        QGroupBox* m_audioGroup;
        QGridLayout* m_audioLayout;
        
        // BGM 설정
        QCheckBox* m_bgmMuteCheck;
        QLabel* m_bgmVolumeLabel;
        QSlider* m_bgmVolumeSlider;
        QLabel* m_bgmVolumeValue;
        
        // 효과음 설정
        QCheckBox* m_sfxMuteCheck;
        QLabel* m_sfxVolumeLabel;
        QSlider* m_sfxVolumeSlider;
        QLabel* m_sfxVolumeValue;
        
        // 버튼 그룹
        QFrame* m_buttonFrame;
        QHBoxLayout* m_buttonLayout;
        QPushButton* m_okButton;
        QPushButton* m_cancelButton;
        QPushButton* m_resetButton;
        QSpacerItem* m_buttonSpacer;
        
        // 타이머 (디바운싱용)
        QTimer* m_previewTimer;
        static constexpr int PREVIEW_DELAY_MS = 300;  // 300ms 디바운싱
    };

} // namespace Blokus