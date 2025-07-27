#include "ResponsiveUI.h"
#include <QScreen>
#include <QGuiApplication>
#include <QWidget>
#include <QResizeEvent>
#include <QDebug>
#include <qmath.h>

namespace Blokus {

    // Static 멤버 초기화
    qreal ResponsiveLayoutManager::s_scaleFactor = 1.0;
    ScreenSize ResponsiveLayoutManager::s_currentScreenSize = ScreenSize::Medium;

    // ===============================================
    // ResponsiveLayoutManager 구현
    // ===============================================
    
    ResponsiveLayoutManager::ResponsiveLayoutManager(QObject* parent)
        : QObject(parent)
    {
        // 화면 변경 감지 연결
        connect(QGuiApplication::primaryScreen(), &QScreen::geometryChanged,
                this, &ResponsiveLayoutManager::onScreenChanged);
        
        // 초기 설정
        updateScaleFactor();
        detectScreenSize();
    }

    ScreenSize ResponsiveLayoutManager::getCurrentScreenSize() {
        QSize size = getScreenSize();
        int width = size.width();
        
        if (width < 800) return ScreenSize::XSmall;
        else if (width < 1024) return ScreenSize::Small;
        else if (width < 1440) return ScreenSize::Medium;
        else if (width < 1920) return ScreenSize::Large;
        else return ScreenSize::XLarge;
    }

    QSize ResponsiveLayoutManager::getScreenSize() {
        return QGuiApplication::primaryScreen()->availableSize();
    }

    qreal ResponsiveLayoutManager::getScaleFactor() {
        return s_scaleFactor;
    }

    int ResponsiveLayoutManager::getResponsiveWidth(int baseWidth) {
        return qRound(baseWidth * s_scaleFactor);
    }

    int ResponsiveLayoutManager::getResponsiveHeight(int baseHeight) {
        return qRound(baseHeight * s_scaleFactor);
    }

    QMargins ResponsiveLayoutManager::getResponsiveMargins(int base) {
        int margin = qRound(base * s_scaleFactor);
        
        // 최소/최대 마진 제한
        margin = qMax(8, qMin(margin, 40));
        
        return QMargins(margin, margin, margin, margin);
    }

    int ResponsiveLayoutManager::getResponsiveSpacing(int base) {
        int spacing = qRound(base * s_scaleFactor);
        
        // 최소/최대 스페이싱 제한
        return qMax(4, qMin(spacing, 20));
    }

    QFont ResponsiveLayoutManager::getResponsiveFont(const QString& family, int baseSize, QFont::Weight weight) {
        QFont font(family);
        
        // 스케일링된 폰트 크기 계산
        int scaledSize = qRound(baseSize * s_scaleFactor);
        
        // 해상도별 최소/최대 폰트 크기 제한
        switch (s_currentScreenSize) {
            case ScreenSize::XSmall:
                scaledSize = qMax(9, qMin(scaledSize, 18));
                break;
            case ScreenSize::Small:
                scaledSize = qMax(10, qMin(scaledSize, 20));
                break;
            case ScreenSize::Medium:
                scaledSize = qMax(11, qMin(scaledSize, 24));
                break;
            case ScreenSize::Large:
                scaledSize = qMax(12, qMin(scaledSize, 28));
                break;
            case ScreenSize::XLarge:
                scaledSize = qMax(14, qMin(scaledSize, 32));
                break;
        }
        
        font.setPointSize(scaledSize);
        font.setWeight(weight);
        return font;
    }

    QFont ResponsiveLayoutManager::getTitleFont(int baseSize) {
        return getResponsiveFont("맑은 고딕", baseSize, QFont::Bold);
    }

    QFont ResponsiveLayoutManager::getHeaderFont(int baseSize) {
        return getResponsiveFont("맑은 고딕", baseSize, QFont::DemiBold);
    }

    QFont ResponsiveLayoutManager::getBodyFont(int baseSize) {
        return getResponsiveFont("맑은 고딕", baseSize, QFont::Normal);
    }

    QFont ResponsiveLayoutManager::getCaptionFont(int baseSize) {
        return getResponsiveFont("맑은 고딕", baseSize, QFont::Light);
    }

    void ResponsiveLayoutManager::setResponsivePolicy(QWidget* widget, QSizePolicy::Policy horizontal, QSizePolicy::Policy vertical) {
        if (!widget) return;
        
        QSizePolicy policy(horizontal, vertical);
        policy.setHorizontalStretch(1);
        policy.setVerticalStretch(0);
        
        widget->setSizePolicy(policy);
    }

    void ResponsiveLayoutManager::setMinimumResponsiveSize(QWidget* widget, int baseWidth, int baseHeight) {
        if (!widget) return;
        
        widget->setMinimumSize(getResponsiveWidth(baseWidth), getResponsiveHeight(baseHeight));
    }

    void ResponsiveLayoutManager::setMaximumResponsiveSize(QWidget* widget, int baseWidth, int baseHeight) {
        if (!widget) return;
        
        widget->setMaximumSize(getResponsiveWidth(baseWidth), getResponsiveHeight(baseHeight));
    }

    QVBoxLayout* ResponsiveLayoutManager::createResponsiveVLayout(QWidget* parent, int spacing) {
        QVBoxLayout* layout = new QVBoxLayout(parent);
        
        layout->setContentsMargins(getResponsiveMargins());
        layout->setSpacing(spacing == -1 ? getResponsiveSpacing() : getResponsiveSpacing(spacing));
        
        return layout;
    }

    QHBoxLayout* ResponsiveLayoutManager::createResponsiveHLayout(QWidget* parent, int spacing) {
        QHBoxLayout* layout = new QHBoxLayout(parent);
        
        layout->setContentsMargins(getResponsiveMargins());
        layout->setSpacing(spacing == -1 ? getResponsiveSpacing() : getResponsiveSpacing(spacing));
        
        return layout;
    }

    QGridLayout* ResponsiveLayoutManager::createResponsiveGridLayout(QWidget* parent, int spacing) {
        QGridLayout* layout = new QGridLayout(parent);
        
        layout->setContentsMargins(getResponsiveMargins());
        layout->setSpacing(spacing == -1 ? getResponsiveSpacing() : getResponsiveSpacing(spacing));
        
        return layout;
    }

    QString ResponsiveLayoutManager::getButtonStyle(const QColor& bgColor, const QColor& hoverColor, const QColor& textColor) {
        int padding = getResponsiveSpacing(8);
        int borderRadius = getResponsiveSpacing(6);
        int fontSize = qRound(12 * s_scaleFactor);
        
        return QString(
            "QPushButton {"
            "    background-color: rgba(%1, %2, %3, %4);"
            "    color: rgba(%5, %6, %7, 255);"
            "    border: 2px solid rgba(%8, %9, %10, 100);"
            "    border-radius: %11px;"
            "    padding: %12px %13px;"
            "    font-size: %14px;"
            "    font-weight: 500;"
            "    font-family: '맑은 고딕';"
            "} "
            "QPushButton:hover {"
            "    background-color: rgba(%15, %16, %17, %18);"
            "    border-color: rgba(%19, %20, %21, 150);"
            "    transform: translateY(-1px);"
            "} "
            "QPushButton:pressed {"
            "    background-color: rgba(%22, %23, %24, %25);"
            "    transform: translateY(1px);"
            "} "
            "QPushButton:disabled {"
            "    background-color: rgba(149, 165, 166, 100);"
            "    color: rgba(149, 165, 166, 150);"
            "    border-color: rgba(149, 165, 166, 50);"
            "}"
        ).arg(bgColor.red()).arg(bgColor.green()).arg(bgColor.blue()).arg(bgColor.alpha())
         .arg(textColor.red()).arg(textColor.green()).arg(textColor.blue())
         .arg(ModernPastelTheme::getBorderColor().red())
         .arg(ModernPastelTheme::getBorderColor().green())
         .arg(ModernPastelTheme::getBorderColor().blue())
         .arg(borderRadius)
         .arg(padding).arg(padding * 2)
         .arg(fontSize)
         .arg(hoverColor.red()).arg(hoverColor.green()).arg(hoverColor.blue()).arg(hoverColor.alpha())
         .arg(ModernPastelTheme::getFocusBorderColor().red())
         .arg(ModernPastelTheme::getFocusBorderColor().green())
         .arg(ModernPastelTheme::getFocusBorderColor().blue())
         .arg(bgColor.darker(110).red()).arg(bgColor.darker(110).green())
         .arg(bgColor.darker(110).blue()).arg(bgColor.alpha());
    }

    QString ResponsiveLayoutManager::getInputStyle() {
        int padding = getResponsiveSpacing(8);
        int borderRadius = getResponsiveSpacing(4);
        int fontSize = qRound(12 * s_scaleFactor);
        
        return QString(
            "QLineEdit {"
            "    background-color: rgba(255, 255, 255, 250);"
            "    color: rgba(47, 54, 64, 255);"
            "    border: 2px solid rgba(220, 221, 225, 255);"
            "    border-radius: %1px;"
            "    padding: %2px;"
            "    font-size: %3px;"
            "    font-family: '맑은 고딕';"
            "} "
            "QLineEdit:focus {"
            "    border-color: rgba(116, 185, 255, 255);"
            "    background-color: rgba(255, 255, 255, 255);"
            "} "
            "QLineEdit:disabled {"
            "    background-color: rgba(241, 243, 244, 255);"
            "    color: rgba(149, 165, 166, 255);"
            "}"
        ).arg(borderRadius).arg(padding).arg(fontSize);
    }

    QString ResponsiveLayoutManager::getCardStyle() {
        int borderRadius = getResponsiveSpacing(8);
        
        return QString(
            "QWidget {"
            "    background-color: rgba(255, 255, 255, 250);"
            "    border: 1px solid rgba(220, 221, 225, 255);"
            "    border-radius: %1px;"
            "}"
        ).arg(borderRadius);
    }

    QString ResponsiveLayoutManager::getLabelStyle(const QColor& textColor) {
        int fontSize = qRound(12 * s_scaleFactor);
        
        return QString(
            "QLabel {"
            "    color: rgba(%1, %2, %3, 255);"
            "    font-size: %4px;"
            "    font-family: '맑은 고딕';"
            "}"
        ).arg(textColor.red()).arg(textColor.green()).arg(textColor.blue()).arg(fontSize);
    }

    void ResponsiveLayoutManager::onScreenChanged() {
        updateScaleFactor();
        detectScreenSize();
        
        emit screenSizeChanged(s_currentScreenSize);
        emit scaleFactorChanged(s_scaleFactor);
    }

    void ResponsiveLayoutManager::updateScaleFactor() {
        QSize size = getScreenSize();
        int baseWidth = 1920; // 기준 해상도
        
        // 스케일 팩터 계산 (0.7 ~ 1.5 범위로 제한)
        s_scaleFactor = qBound(0.7, static_cast<qreal>(size.width()) / baseWidth, 1.5);
    }

    void ResponsiveLayoutManager::detectScreenSize() {
        s_currentScreenSize = getCurrentScreenSize();
    }

    // ===============================================
    // ResponsiveWidget 구현
    // ===============================================

    ResponsiveWidget::ResponsiveWidget(QWidget* parent)
        : QWidget(parent)
        , m_layoutManager(new ResponsiveLayoutManager(this))
    {
        connect(m_layoutManager, &ResponsiveLayoutManager::screenSizeChanged,
                this, &ResponsiveWidget::onResponsiveUpdate);
    }

    void ResponsiveWidget::resizeEvent(QResizeEvent* event) {
        QWidget::resizeEvent(event);
        updateResponsiveLayout();
    }

    void ResponsiveWidget::onResponsiveUpdate() {
        updateResponsiveLayout();
    }

    // ===============================================
    // ResponsiveButton 구현
    // ===============================================

    ResponsiveButton::ResponsiveButton(const QString& text, QWidget* parent)
        : QPushButton(text, parent)
        , m_bgColor(ModernPastelTheme::getAccentBlue())
        , m_hoverColor(ModernPastelTheme::getAccentBlue().lighter(110))
    {
        setupDefaultStyle();
    }

    ResponsiveButton::ResponsiveButton(QWidget* parent)
        : QPushButton(parent)
        , m_bgColor(ModernPastelTheme::getAccentBlue())
        , m_hoverColor(ModernPastelTheme::getAccentBlue().lighter(110))
    {
        setupDefaultStyle();
    }

    void ResponsiveButton::setColorScheme(const QColor& bgColor, const QColor& hoverColor) {
        m_bgColor = bgColor;
        m_hoverColor = hoverColor;
        updateResponsiveStyle();
    }

    void ResponsiveButton::updateResponsiveStyle() {
        setStyleSheet(ResponsiveLayoutManager::getButtonStyle(m_bgColor, m_hoverColor));
        setFont(ResponsiveLayoutManager::getBodyFont());
        
        // 최소 크기 설정
        ResponsiveLayoutManager::setMinimumResponsiveSize(this, 80, 32);
    }

    void ResponsiveButton::resizeEvent(QResizeEvent* event) {
        QPushButton::resizeEvent(event);
        updateResponsiveStyle();
    }

    void ResponsiveButton::setupDefaultStyle() {
        updateResponsiveStyle();
        setCursor(Qt::PointingHandCursor);
    }

    // ===============================================
    // ResponsiveLineEdit 구현
    // ===============================================

    ResponsiveLineEdit::ResponsiveLineEdit(QWidget* parent)
        : QLineEdit(parent)
    {
        setupDefaultStyle();
    }

    ResponsiveLineEdit::ResponsiveLineEdit(const QString& text, QWidget* parent)
        : QLineEdit(text, parent)
    {
        setupDefaultStyle();
    }

    void ResponsiveLineEdit::updateResponsiveStyle() {
        setStyleSheet(ResponsiveLayoutManager::getInputStyle());
        setFont(ResponsiveLayoutManager::getBodyFont());
        
        // 최소 크기 설정
        ResponsiveLayoutManager::setMinimumResponsiveSize(this, 150, 32);
    }

    void ResponsiveLineEdit::resizeEvent(QResizeEvent* event) {
        QLineEdit::resizeEvent(event);
        updateResponsiveStyle();
    }

    void ResponsiveLineEdit::setupDefaultStyle() {
        updateResponsiveStyle();
    }

    // ===============================================
    // ResponsiveLabel 구현
    // ===============================================

    ResponsiveLabel::ResponsiveLabel(QWidget* parent)
        : QLabel(parent)
        , m_textLevel("body")
    {
        setupDefaultStyle();
    }

    ResponsiveLabel::ResponsiveLabel(const QString& text, QWidget* parent)
        : QLabel(text, parent)
        , m_textLevel("body")
    {
        setupDefaultStyle();
    }

    void ResponsiveLabel::setTextLevel(const QString& level) {
        m_textLevel = level;
        updateFont();
        updateResponsiveStyle();
    }

    void ResponsiveLabel::updateResponsiveStyle() {
        setStyleSheet(ResponsiveLayoutManager::getLabelStyle());
        updateFont();
    }

    void ResponsiveLabel::resizeEvent(QResizeEvent* event) {
        QLabel::resizeEvent(event);
        updateResponsiveStyle();
    }

    void ResponsiveLabel::setupDefaultStyle() {
        updateResponsiveStyle();
        setWordWrap(true);
    }

    void ResponsiveLabel::updateFont() {
        if (m_textLevel == "title") {
            setFont(ResponsiveLayoutManager::getTitleFont(18));
        } else if (m_textLevel == "header") {
            setFont(ResponsiveLayoutManager::getHeaderFont(16));
        } else if (m_textLevel == "caption") {
            setFont(ResponsiveLayoutManager::getCaptionFont(10));
        } else {
            setFont(ResponsiveLayoutManager::getBodyFont(12));
        }
    }

} // namespace Blokus