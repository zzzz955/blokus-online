#include <QApplication>
#include <QWidget>
#include <QVBoxLayout>
#include <QLabel>
#include <QPushButton>
#include <QMessageBox>

int main(int argc, char* argv[])
{
    QApplication app(argc, argv);

    // �⺻ â ����
    QWidget window;
    window.setWindowTitle("Blokus Online - Development Test");
    window.resize(800, 600);

    // ���̾ƿ� ����
    QVBoxLayout* layout = new QVBoxLayout(&window);

    // ���� ��
    QLabel* titleLabel = new QLabel("Blokus Online", &window);
    titleLabel->setAlignment(Qt::AlignCenter);
    titleLabel->setStyleSheet("font-size: 24px; font-weight: bold; margin: 20px;");

    // ���� ��
    QLabel* statusLabel = new QLabel("Qt5 Build Success!", &window);
    statusLabel->setAlignment(Qt::AlignCenter);
    statusLabel->setStyleSheet("font-size: 16px; color: green; margin: 10px;");

    // ���� ����
    QLabel* versionLabel = new QLabel(
        QString("Qt Version: %1\nBuild Date: %2 %3")
        .arg(QT_VERSION_STR)
        .arg(__DATE__)
        .arg(__TIME__), &window);
    versionLabel->setAlignment(Qt::AlignCenter);
    versionLabel->setStyleSheet("font-size: 12px; color: gray; margin: 10px;");

    // �׽�Ʈ ��ư
    QPushButton* testButton = new QPushButton("Start Game Development", &window);
    testButton->setStyleSheet("font-size: 14px; padding: 10px; margin: 20px;");

    // ��ư Ŭ�� �̺�Ʈ
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

    // ���̾ƿ��� ���� �߰�
    layout->addWidget(titleLabel);
    layout->addWidget(statusLabel);
    layout->addWidget(versionLabel);
    layout->addStretch(); // ������ ����
    layout->addWidget(testButton);
    layout->addStretch(); // ������ ����

    // â ǥ��
    window.show();

    return app.exec();
}