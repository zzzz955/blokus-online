#pragma once

#include <QObject>
#include <QWidget>
#include <QApplication>
#include <QScreen>
#include <QFont>
#include <QFontMetrics>
#include <QSizePolicy>
#include <QLayout>
#include <QVBoxLayout>
#include <QHBoxLayout>
#include <QGridLayout>
#include <QMargins>
#include <QColor>
#include <QPalette>
#include <QString>
#include <QMap>
#include <QPushButton>
#include <QLineEdit>
#include <QLabel>
#include <QResizeEvent>

namespace Blokus {

    // 해상도 분류 시스템
    enum class ScreenSize {
        XSmall,  // < 800px width
        Small,   // 800-1024px
        Medium,  // 1024-1440px
        Large,   // 1440-1920px
        XLarge   // > 1920px
    };

    // 현대적 파스텔 색상 팔레트
    class ModernPastelTheme {
    public:
        // 메인 게임 색상 (파스텔 톤)
        static QColor getPrimaryBlue() { return QColor(135, 206, 235, 200); }    // Sky Blue
        static QColor getPrimaryYellow() { return QColor(255, 228, 181, 200); }  // Moccasin
        static QColor getPrimaryRed() { return QColor(255, 182, 193, 200); }     // Light Pink
        static QColor getPrimaryGreen() { return QColor(152, 251, 152, 200); }   // Pale Green
        
        // UI 배경 색상 (부드러운 그라데이션)
        static QColor getBackgroundPrimary() { return QColor(248, 249, 250); }   // Almost White
        static QColor getBackgroundSecondary() { return QColor(241, 243, 244); } // Light Gray
        static QColor getCardBackground() { return QColor(255, 255, 255, 250); } // Pure White with alpha
        
        // 텍스트 색상 (가독성 최적화)
        static QColor getTextPrimary() { return QColor(47, 54, 64); }     // Dark Blue Gray
        static QColor getTextSecondary() { return QColor(99, 110, 114); } // Medium Gray
        static QColor getTextMuted() { return QColor(149, 165, 166); }    // Light Gray
        
        // 액센트 색상 (버튼, 하이라이트 등)
        static QColor getAccentBlue() { return QColor(116, 185, 255); }    // Bright Blue
        static QColor getAccentGreen() { return QColor(85, 239, 196); }    // Mint Green
        static QColor getAccentOrange() { return QColor(255, 177, 66); }   // Peach
        static QColor getAccentPurple() { return QColor(162, 155, 254); }  // Lavender
        
        // 상태 색상 (성공, 경고, 오류)
        static QColor getSuccessColor() { return QColor(46, 213, 115); }
        static QColor getWarningColor() { return QColor(255, 195, 18); }
        static QColor getErrorColor() { return QColor(255, 118, 117); }
        
        // 게임 전용 색상 (블록 하이라이트 등)
        static QColor getBlockHighlight() { return QColor(255, 255, 255, 100); }
        static QColor getValidPlacement() { return QColor(46, 213, 115, 150); }
        static QColor getInvalidPlacement() { return QColor(255, 118, 117, 150); }
        
        // 그림자 및 테두리
        static QColor getShadowColor() { return QColor(0, 0, 0, 30); }
        static QColor getBorderColor() { return QColor(220, 221, 225); }
        static QColor getFocusBorderColor() { return QColor(116, 185, 255); }
    };

    // 반응형 레이아웃 관리자
    class ResponsiveLayoutManager : public QObject {
        Q_OBJECT

    public:
        explicit ResponsiveLayoutManager(QObject* parent = nullptr);
        
        // 화면 크기 감지
        static ScreenSize getCurrentScreenSize();
        static QSize getScreenSize();
        static qreal getScaleFactor();
        
        // 반응형 크기 계산
        static int getResponsiveWidth(int baseWidth);
        static int getResponsiveHeight(int baseHeight);
        static QMargins getResponsiveMargins(int base = 16);
        static int getResponsiveSpacing(int base = 8);
        
        // 폰트 크기 관리
        static QFont getResponsiveFont(const QString& family = "맑은 고딕", int baseSize = 12, QFont::Weight weight = QFont::Normal);
        static QFont getTitleFont(int baseSize = 18);
        static QFont getHeaderFont(int baseSize = 16);
        static QFont getBodyFont(int baseSize = 12);
        static QFont getCaptionFont(int baseSize = 10);
        
        // 위젯 크기 정책 설정
        static void setResponsivePolicy(QWidget* widget, QSizePolicy::Policy horizontal = QSizePolicy::Expanding, QSizePolicy::Policy vertical = QSizePolicy::Preferred);
        static void setMinimumResponsiveSize(QWidget* widget, int baseWidth, int baseHeight);
        static void setMaximumResponsiveSize(QWidget* widget, int baseWidth, int baseHeight);
        
        // 레이아웃 헬퍼
        static QVBoxLayout* createResponsiveVLayout(QWidget* parent = nullptr, int spacing = -1);
        static QHBoxLayout* createResponsiveHLayout(QWidget* parent = nullptr, int spacing = -1);
        static QGridLayout* createResponsiveGridLayout(QWidget* parent = nullptr, int spacing = -1);
        
        // 스타일시트 생성
        static QString getButtonStyle(const QColor& bgColor, const QColor& hoverColor, const QColor& textColor = ModernPastelTheme::getTextPrimary());
        static QString getInputStyle();
        static QString getCardStyle();
        static QString getLabelStyle(const QColor& textColor = ModernPastelTheme::getTextPrimary());
        
    public slots:
        void onScreenChanged();
        
    signals:
        void screenSizeChanged(ScreenSize newSize);
        void scaleFactorChanged(qreal newFactor);
        
    private:
        static qreal s_scaleFactor;
        static ScreenSize s_currentScreenSize;
        
        void updateScaleFactor();
        void detectScreenSize();
    };

    // 반응형 위젯 기본 클래스
    class ResponsiveWidget : public QWidget {
        Q_OBJECT
        
    public:
        explicit ResponsiveWidget(QWidget* parent = nullptr);
        
    protected:
        virtual void updateResponsiveLayout() = 0;
        virtual void resizeEvent(QResizeEvent* event) override;
        
    private slots:
        void onResponsiveUpdate();
        
    private:
        ResponsiveLayoutManager* m_layoutManager;
    };

    // 반응형 버튼 클래스
    class ResponsiveButton : public QPushButton {
        Q_OBJECT
        
    public:
        explicit ResponsiveButton(const QString& text, QWidget* parent = nullptr);
        explicit ResponsiveButton(QWidget* parent = nullptr);
        
        void setColorScheme(const QColor& bgColor, const QColor& hoverColor);
        void updateResponsiveStyle();
        
    protected:
        virtual void resizeEvent(QResizeEvent* event) override;
        
    private:
        QColor m_bgColor;
        QColor m_hoverColor;
        
        void setupDefaultStyle();
    };

    // 반응형 입력 필드 클래스
    class ResponsiveLineEdit : public QLineEdit {
        Q_OBJECT
        
    public:
        explicit ResponsiveLineEdit(QWidget* parent = nullptr);
        explicit ResponsiveLineEdit(const QString& text, QWidget* parent = nullptr);
        
        void updateResponsiveStyle();
        
    protected:
        virtual void resizeEvent(QResizeEvent* event) override;
        
    private:
        void setupDefaultStyle();
    };

    // 반응형 라벨 클래스
    class ResponsiveLabel : public QLabel {
        Q_OBJECT
        
    public:
        explicit ResponsiveLabel(QWidget* parent = nullptr);
        explicit ResponsiveLabel(const QString& text, QWidget* parent = nullptr);
        
        void setTextLevel(const QString& level); // "title", "header", "body", "caption"
        void updateResponsiveStyle();
        
    protected:
        virtual void resizeEvent(QResizeEvent* event) override;
        
    private:
        QString m_textLevel;
        
        void setupDefaultStyle();
        void updateFont();
    };

} // namespace Blokus