#include "UserSettingsDialog.h"

#include <QApplication>
#include <QMessageBox>
#include <QKeyEvent>
#include <QCloseEvent>
#include <QDesktopWidget>
#include <QScreen>
#include <QDebug>

namespace Blokus {

    UserSettingsDialog::UserSettingsDialog(QWidget* parent)
        : QDialog(parent)
        , m_originalSettings()
        , m_currentSettings()
        , m_hasUnsavedChanges(false)
        , m_mainLayout(nullptr)
        , m_uiGroup(nullptr)
        , m_notificationGroup(nullptr)
        , m_audioGroup(nullptr)
        , m_buttonFrame(nullptr)
        , m_previewTimer(nullptr)
    {
        setupUI();
        setupStyles();
        
        // 디바운싱 타이머 초기화
        m_previewTimer = new QTimer(this);
        m_previewTimer->setSingleShot(true);
        m_previewTimer->setInterval(PREVIEW_DELAY_MS);
        connect(m_previewTimer, &QTimer::timeout, this, &UserSettingsDialog::onPreviewTimer);
        
        // 기본 설정으로 초기화
        m_originalSettings = UserSettings::getDefaults();
        m_currentSettings = m_originalSettings;
        updateUIFromSettings(m_currentSettings);
        
        qDebug() << "UserSettingsDialog created";
    }

    UserSettingsDialog::~UserSettingsDialog()
    {
        qDebug() << "UserSettingsDialog destroyed";
    }

    // ========================================
    // 공개 인터페이스
    // ========================================

    void UserSettingsDialog::setCurrentSettings(const UserSettings& settings)
    {
        m_originalSettings = settings;
        m_currentSettings = settings;
        m_hasUnsavedChanges = false;
        
        updateUIFromSettings(settings);
        qDebug() << "Settings loaded:" << settings.getThemeString() << settings.bgmVolume;
    }

    UserSettings UserSettingsDialog::getCurrentSettings() const
    {
        return m_currentSettings;
    }

    bool UserSettingsDialog::hasChanges() const
    {
        return m_hasUnsavedChanges || (m_originalSettings != m_currentSettings);
    }

    void UserSettingsDialog::resetToDefaults()
    {
        UserSettings defaults = UserSettings::getDefaults();
        setCurrentSettings(defaults);
        emit settingsChanged(defaults);
    }

    // ========================================
    // UI 구성 함수들
    // ========================================

    void UserSettingsDialog::setupUI()
    {
        setWindowTitle("환경 설정");
        setModal(false);  // 비모달 다이얼로그
        setWindowFlags(Qt::Dialog | Qt::WindowCloseButtonHint | Qt::WindowTitleHint);
        resize(400, 500);
        
        // 화면 중앙에 배치
        if (parent()) {
            QWidget* parentWidget = qobject_cast<QWidget*>(parent());
            if (parentWidget) {
                move(parentWidget->geometry().center() - rect().center());
            }
        } else {
            QScreen* screen = QApplication::primaryScreen();
            if (screen) {
                move(screen->geometry().center() - rect().center());
            }
        }

        // 메인 레이아웃
        m_mainLayout = new QVBoxLayout(this);
        m_mainLayout->setSpacing(15);
        m_mainLayout->setContentsMargins(20, 20, 20, 20);

        // 각 그룹 설정
        setupUIGroup();
        setupNotificationGroup();
        setupAudioGroup();
        setupButtonGroup();

        setLayout(m_mainLayout);
    }

    void UserSettingsDialog::setupUIGroup()
    {
        m_uiGroup = new QGroupBox("UI 설정", this);
        m_uiLayout = new QGridLayout(m_uiGroup);
        
        // 테마 설정
        m_themeLabel = new QLabel("테마:", m_uiGroup);
        m_themeCombo = new QComboBox(m_uiGroup);
        m_themeCombo->addItem("라이트 테마", static_cast<int>(ThemeType::Light));
        m_themeCombo->addItem("다크 테마", static_cast<int>(ThemeType::Dark));
        connect(m_themeCombo, QOverload<int>::of(&QComboBox::currentIndexChanged),
                this, &UserSettingsDialog::onThemeChanged);
        
        // 언어 설정
        m_languageLabel = new QLabel("언어:", m_uiGroup);
        m_languageCombo = new QComboBox(m_uiGroup);
        m_languageCombo->addItem("한국어", static_cast<int>(LanguageType::Korean));
        m_languageCombo->addItem("English (향후 지원)", static_cast<int>(LanguageType::English));
        m_languageCombo->setItemData(1, QVariant(0), Qt::UserRole - 1); // 비활성화
        connect(m_languageCombo, QOverload<int>::of(&QComboBox::currentIndexChanged),
                this, &UserSettingsDialog::onLanguageChanged);
        
        // 레이아웃 배치
        m_uiLayout->addWidget(m_themeLabel, 0, 0);
        m_uiLayout->addWidget(m_themeCombo, 0, 1);
        m_uiLayout->addWidget(m_languageLabel, 1, 0);
        m_uiLayout->addWidget(m_languageCombo, 1, 1);
        
        m_mainLayout->addWidget(m_uiGroup);
    }

    void UserSettingsDialog::setupNotificationGroup()
    {
        m_notificationGroup = new QGroupBox("알림 설정", this);
        m_notificationLayout = new QVBoxLayout(m_notificationGroup);
        
        // 현재 비활성화 안내 레이블
        m_notificationDisabledLabel = new QLabel(
            "※ 알림 기능은 향후 업데이트에서 지원 예정입니다.", m_notificationGroup);
        m_notificationDisabledLabel->setStyleSheet("color: #888; font-style: italic;");
        
        // 알림 체크박스들 (비활성화 상태)
        m_inviteNotificationCheck = new QCheckBox("게임 초대 알림 허용", m_notificationGroup);
        m_friendNotificationCheck = new QCheckBox("친구 접속 알림 허용", m_notificationGroup);
        m_systemNotificationCheck = new QCheckBox("시스템 메시지 허용", m_notificationGroup);
        
        // 비활성화 처리
        enableNotificationControls(false);
        
        // 레이아웃 배치
        m_notificationLayout->addWidget(m_notificationDisabledLabel);
        m_notificationLayout->addWidget(m_inviteNotificationCheck);
        m_notificationLayout->addWidget(m_friendNotificationCheck);
        m_notificationLayout->addWidget(m_systemNotificationCheck);
        
        m_mainLayout->addWidget(m_notificationGroup);
    }

    void UserSettingsDialog::setupAudioGroup()
    {
        m_audioGroup = new QGroupBox("오디오 설정", this);
        m_audioLayout = new QGridLayout(m_audioGroup);
        
        // BGM 설정
        m_bgmMuteCheck = new QCheckBox("배경음 음소거", m_audioGroup);
        connect(m_bgmMuteCheck, &QCheckBox::toggled, this, &UserSettingsDialog::onBGMSettingChanged);
        
        m_bgmVolumeLabel = new QLabel("배경음 음량:", m_audioGroup);
        m_bgmVolumeSlider = new QSlider(Qt::Horizontal, m_audioGroup);
        m_bgmVolumeSlider->setRange(0, 100);
        m_bgmVolumeSlider->setValue(50);
        connect(m_bgmVolumeSlider, &QSlider::valueChanged, this, &UserSettingsDialog::onVolumeSliderChanged);
        
        m_bgmVolumeValue = new QLabel("50%", m_audioGroup);
        m_bgmVolumeValue->setMinimumWidth(40);
        
        // 효과음 설정
        m_sfxMuteCheck = new QCheckBox("효과음 음소거", m_audioGroup);
        connect(m_sfxMuteCheck, &QCheckBox::toggled, this, &UserSettingsDialog::onSFXSettingChanged);
        
        m_sfxVolumeLabel = new QLabel("효과음 음량:", m_audioGroup);
        m_sfxVolumeSlider = new QSlider(Qt::Horizontal, m_audioGroup);
        m_sfxVolumeSlider->setRange(0, 100);
        m_sfxVolumeSlider->setValue(50);
        connect(m_sfxVolumeSlider, &QSlider::valueChanged, this, &UserSettingsDialog::onVolumeSliderChanged);
        
        m_sfxVolumeValue = new QLabel("50%", m_audioGroup);
        m_sfxVolumeValue->setMinimumWidth(40);
        
        // 레이아웃 배치
        m_audioLayout->addWidget(m_bgmMuteCheck, 0, 0, 1, 3);
        m_audioLayout->addWidget(m_bgmVolumeLabel, 1, 0);
        m_audioLayout->addWidget(m_bgmVolumeSlider, 1, 1);
        m_audioLayout->addWidget(m_bgmVolumeValue, 1, 2);
        
        m_audioLayout->addWidget(m_sfxMuteCheck, 2, 0, 1, 3);
        m_audioLayout->addWidget(m_sfxVolumeLabel, 3, 0);
        m_audioLayout->addWidget(m_sfxVolumeSlider, 3, 1);
        m_audioLayout->addWidget(m_sfxVolumeValue, 3, 2);
        
        m_mainLayout->addWidget(m_audioGroup);
    }

    void UserSettingsDialog::setupButtonGroup()
    {
        m_buttonFrame = new QFrame(this);
        m_buttonLayout = new QHBoxLayout(m_buttonFrame);
        
        // 버튼 생성
        m_resetButton = new QPushButton("기본값", m_buttonFrame);
        m_cancelButton = new QPushButton("취소", m_buttonFrame);
        m_okButton = new QPushButton("확인", m_buttonFrame);
        
        // 기본 버튼 설정
        m_okButton->setDefault(true);
        m_okButton->setAutoDefault(true);
        
        // 버튼 연결
        connect(m_resetButton, &QPushButton::clicked, this, &UserSettingsDialog::onResetClicked);
        connect(m_cancelButton, &QPushButton::clicked, this, &UserSettingsDialog::onCancelClicked);
        connect(m_okButton, &QPushButton::clicked, this, &UserSettingsDialog::onOkClicked);
        
        // 레이아웃 배치
        m_buttonSpacer = new QSpacerItem(40, 20, QSizePolicy::Expanding, QSizePolicy::Minimum);
        m_buttonLayout->addItem(m_buttonSpacer);
        m_buttonLayout->addWidget(m_resetButton);
        m_buttonLayout->addWidget(m_cancelButton);
        m_buttonLayout->addWidget(m_okButton);
        
        m_mainLayout->addWidget(m_buttonFrame);
    }

    void UserSettingsDialog::setupStyles()
    {
        // 기본 스타일 적용 (향후 테마 시스템 연동)
        setStyleSheet(R"(
            QGroupBox {
                font-weight: bold;
                border: 2px solid #cccccc;
                border-radius: 5px;
                margin-top: 10px;
                padding-top: 10px;
            }
            
            QGroupBox::title {
                subcontrol-origin: margin;
                left: 10px;
                padding: 0 5px 0 5px;
            }
            
            QPushButton {
                padding: 5px 15px;
                border: 1px solid #cccccc;
                border-radius: 3px;
                background-color: #f0f0f0;
            }
            
            QPushButton:hover {
                background-color: #e0e0e0;
            }
            
            QPushButton:pressed {
                background-color: #d0d0d0;
            }
            
            QPushButton:default {
                border-color: #0078d4;
                background-color: #0078d4;
                color: white;
            }
            
            QPushButton:default:hover {
                background-color: #106ebe;
            }
        )");
    }

    // ========================================
    // UI 업데이트 함수들
    // ========================================

    void UserSettingsDialog::updateUIFromSettings(const UserSettings& settings)
    {
        // 시그널 차단 (무한 루프 방지)
        const bool oldState = blockSignals(true);
        
        // UI 설정 업데이트
        m_themeCombo->setCurrentIndex(static_cast<int>(settings.theme));
        m_languageCombo->setCurrentIndex(static_cast<int>(settings.language));
        
        // 알림 설정 업데이트
        m_inviteNotificationCheck->setChecked(settings.gameInviteNotifications);
        m_friendNotificationCheck->setChecked(settings.friendOnlineNotifications);
        m_systemNotificationCheck->setChecked(settings.systemNotifications);
        
        // 오디오 설정 업데이트
        m_bgmMuteCheck->setChecked(settings.bgmMute);
        m_bgmVolumeSlider->setValue(settings.bgmVolume);
        m_bgmVolumeValue->setText(formatVolumeText(settings.bgmVolume));
        
        m_sfxMuteCheck->setChecked(settings.effectMute);
        m_sfxVolumeSlider->setValue(settings.effectVolume);
        m_sfxVolumeValue->setText(formatVolumeText(settings.effectVolume));
        
        updateAudioControls();
        
        blockSignals(oldState);
        m_currentSettings = settings;
    }

    void UserSettingsDialog::updateSettingsFromUI()
    {
        // UI에서 현재 설정 읽기
        m_currentSettings.theme = static_cast<ThemeType>(m_themeCombo->currentData().toInt());
        m_currentSettings.language = static_cast<LanguageType>(m_languageCombo->currentData().toInt());
        
        m_currentSettings.gameInviteNotifications = m_inviteNotificationCheck->isChecked();
        m_currentSettings.friendOnlineNotifications = m_friendNotificationCheck->isChecked();
        m_currentSettings.systemNotifications = m_systemNotificationCheck->isChecked();
        
        m_currentSettings.bgmMute = m_bgmMuteCheck->isChecked();
        m_currentSettings.bgmVolume = m_bgmVolumeSlider->value();
        m_currentSettings.effectMute = m_sfxMuteCheck->isChecked();
        m_currentSettings.effectVolume = m_sfxVolumeSlider->value();
    }

    void UserSettingsDialog::updateAudioControls()
    {
        // BGM 컨트롤 활성화/비활성화
        bool bgmEnabled = !m_bgmMuteCheck->isChecked();
        m_bgmVolumeLabel->setEnabled(bgmEnabled);
        m_bgmVolumeSlider->setEnabled(bgmEnabled);
        m_bgmVolumeValue->setEnabled(bgmEnabled);
        
        // 효과음 컨트롤 활성화/비활성화
        bool sfxEnabled = !m_sfxMuteCheck->isChecked();
        m_sfxVolumeLabel->setEnabled(sfxEnabled);
        m_sfxVolumeSlider->setEnabled(sfxEnabled);
        m_sfxVolumeValue->setEnabled(sfxEnabled);
    }

    // ========================================
    // 슬롯 함수들
    // ========================================

    void UserSettingsDialog::onThemeChanged()
    {
        updateSettingsFromUI();
        m_hasUnsavedChanges = true;
        
        // 실시간 테마 미리보기
        applyThemePreview();
        emit settingsChanged(m_currentSettings);
        
        qDebug() << "Theme changed to:" << m_currentSettings.getThemeString();
    }

    void UserSettingsDialog::onLanguageChanged()
    {
        updateSettingsFromUI();
        m_hasUnsavedChanges = true;
        emit settingsChanged(m_currentSettings);
    }

    void UserSettingsDialog::onNotificationSettingChanged()
    {
        updateSettingsFromUI();
        m_hasUnsavedChanges = true;
        emit settingsChanged(m_currentSettings);
    }

    void UserSettingsDialog::onBGMSettingChanged()
    {
        updateSettingsFromUI();
        updateAudioControls();
        m_hasUnsavedChanges = true;
        
        // 실시간 오디오 적용
        startPreviewTimer();
        emit settingsChanged(m_currentSettings);
    }

    void UserSettingsDialog::onSFXSettingChanged()
    {
        updateSettingsFromUI();
        updateAudioControls();
        m_hasUnsavedChanges = true;
        
        // 실시간 오디오 적용
        startPreviewTimer();
        emit settingsChanged(m_currentSettings);
    }

    void UserSettingsDialog::onVolumeSliderChanged()
    {
        QSlider* slider = qobject_cast<QSlider*>(sender());
        if (slider == m_bgmVolumeSlider) {
            m_bgmVolumeValue->setText(formatVolumeText(slider->value()));
        } else if (slider == m_sfxVolumeSlider) {
            m_sfxVolumeValue->setText(formatVolumeText(slider->value()));
        }
        
        updateSettingsFromUI();
        m_hasUnsavedChanges = true;
        
        // 디바운싱된 실시간 오디오 적용
        startPreviewTimer();
        emit settingsChanged(m_currentSettings);
    }

    void UserSettingsDialog::onOkClicked()
    {
        if (hasChanges()) {
            updateSettingsFromUI();
            emit settingsUpdateRequested(m_currentSettings);
            qDebug() << "Settings update requested";
        }
        accept();
    }

    void UserSettingsDialog::onCancelClicked()
    {
        if (hasChanges()) {
            // 원본 설정으로 복원
            updateUIFromSettings(m_originalSettings);
            applyThemePreview();  // 테마도 복원
            applyAudioSettings(); // 오디오도 복원
        }
        reject();
    }

    void UserSettingsDialog::onResetClicked()
    {
        int ret = QMessageBox::question(this, "설정 초기화",
                                       "모든 설정을 기본값으로 초기화하시겠습니까?",
                                       QMessageBox::Yes | QMessageBox::No,
                                       QMessageBox::No);
        if (ret == QMessageBox::Yes) {
            resetToDefaults();
        }
    }

    void UserSettingsDialog::onPreviewTimer()
    {
        // 디바운싱된 오디오 적용
        applyAudioSettings();
    }

    void UserSettingsDialog::onSettingsUpdated(const UserSettings& settings)
    {
        m_originalSettings = settings;
        m_currentSettings = settings;
        m_hasUnsavedChanges = false;
        
        qDebug() << "Settings successfully updated";
    }

    void UserSettingsDialog::onSettingsUpdateFailed(const QString& errorMessage)
    {
        QMessageBox::warning(this, "설정 저장 실패", 
                           "설정을 저장할 수 없습니다:\n" + errorMessage);
    }

    // ========================================
    // 미리보기 및 적용 함수들
    // ========================================

    void UserSettingsDialog::applyThemePreview()
    {
        // 향후 테마 시스템 구현 시 실제 테마 적용
        emit themeChangeRequested(m_currentSettings.theme);
        qDebug() << "Theme preview applied:" << m_currentSettings.getThemeString();
    }

    void UserSettingsDialog::applyAudioSettings()
    {
        // BGMManager에 실시간 적용
        BGMManager& bgmManager = BGMManager::getInstance();
        
        if (bgmManager.isInitialized()) {
            // BGM 설정 적용
            bgmManager.setBGMMuted(m_currentSettings.bgmMute);
            bgmManager.setBGMVolume(m_currentSettings.bgmVolume / 100.0f);
            
            // 효과음 설정 적용
            bgmManager.setSFXMuted(m_currentSettings.effectMute);
            bgmManager.setSFXVolume(m_currentSettings.effectVolume / 100.0f);
            
            qDebug() << "Audio settings applied - BGM:" << m_currentSettings.bgmVolume 
                     << "SFX:" << m_currentSettings.effectVolume;
        }
    }

    void UserSettingsDialog::startPreviewTimer()
    {
        m_previewTimer->start();
    }

    void UserSettingsDialog::stopPreviewTimer()
    {
        m_previewTimer->stop();
    }

    // ========================================
    // 유틸리티 함수들
    // ========================================

    QString UserSettingsDialog::formatVolumeText(int volume) const
    {
        return QString("%1%").arg(volume);
    }

    void UserSettingsDialog::enableNotificationControls(bool enabled)
    {
        m_inviteNotificationCheck->setEnabled(enabled);
        m_friendNotificationCheck->setEnabled(enabled);
        m_systemNotificationCheck->setEnabled(enabled);
        
        if (!enabled) {
            m_inviteNotificationCheck->setStyleSheet("color: #888;");
            m_friendNotificationCheck->setStyleSheet("color: #888;");
            m_systemNotificationCheck->setStyleSheet("color: #888;");
        }
    }

    void UserSettingsDialog::resetUIToDefaults()
    {
        UserSettings defaults = UserSettings::getDefaults();
        updateUIFromSettings(defaults);
    }

    // ========================================
    // 이벤트 핸들러
    // ========================================

    void UserSettingsDialog::closeEvent(QCloseEvent* event)
    {
        if (hasChanges()) {
            onCancelClicked();
        }
        QDialog::closeEvent(event);
    }

    void UserSettingsDialog::keyPressEvent(QKeyEvent* event)
    {
        if (event->key() == Qt::Key_Escape) {
            onCancelClicked();
            return;
        } else if (event->key() == Qt::Key_Return || event->key() == Qt::Key_Enter) {
            if (m_okButton->hasFocus() || m_okButton->isDefault()) {
                onOkClicked();
                return;
            }
        }
        
        QDialog::keyPressEvent(event);
    }

} // namespace Blokus