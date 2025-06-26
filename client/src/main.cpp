#include <QApplication>
#include <QWidget>
#include <QVBoxLayout>
#include <QLabel>
#include <QPushButton>
#include <QMessageBox>

int main(int argc, char* argv[])
{
    QApplication app(argc, argv);

    // 기본 창 설정
    QWidget window;
    window.setWindowTitle("Blokus Online - Development Test");
    window.resize(800, 600);

    // 레이아웃 설정
    QVBoxLayout* layout = new QVBoxLayout(&window);

    // 제목 라벨
    QLabel* titleLabel = new QLabel("Blokus Online", &window);
    titleLabel->setAlignment(Qt::AlignCenter);
    titleLabel->setStyleSheet("font-size: 24px; font-weight: bold; margin: 20px;");

    // 상태 라벨
    QLabel* statusLabel = new QLabel("Qt5 Build Success!", &window);
    statusLabel->setAlignment(Qt::AlignCenter);
    statusLabel->setStyleSheet("font-size: 16px; color: green; margin: 10px;");

    // 버전 정보
    QLabel* versionLabel = new QLabel(
        QString("Qt Version: %1\nBuild Date: %2 %3")
        .arg(QT_VERSION_STR)
        .arg(__DATE__)
        .arg(__TIME__), &window);
    versionLabel->setAlignment(Qt::AlignCenter);
    versionLabel->setStyleSheet("font-size: 12px; color: gray; margin: 10px;");

    // 테스트 버튼
    QPushButton* testButton = new QPushButton("Start Game Development", &window);
    testButton->setStyleSheet("font-size: 14px; padding: 10px; margin: 20px;");

    // 버튼 클릭 이벤트
    QObject::connect(testButton, &QPushButton::clicked, [&]() {
        QMessageBox::information(&window,
            "Blokus Online",
            "Development Environment Ready!\n\n"
            "Next Steps:\n"
            "1. Game Board UI Implementation\n"
            "2. Block Rendering System\n"
            "3. Game Logic Implementation\n"
            "4. Network Communication");
        });

    // 레이아웃에 위젯 추가
    layout->addWidget(titleLabel);
    layout->addWidget(statusLabel);
    layout->addWidget(versionLabel);
    layout->addStretch(); // 유연한 공간
    layout->addWidget(testButton);
    layout->addStretch(); // 유연한 공간

    // 창 표시
    window.show();

    return app.exec();
}