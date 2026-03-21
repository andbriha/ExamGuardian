#include "media_validator.h"
#include <QTimer>
#include <QDebug>

MediaValidator::MediaValidator(QObject *parent) : QObject(parent) {}

bool MediaValidator::isVirtualCamera(const QString &cameraName) {
    QString name = cameraName.toLower();
    // Базовий список популярних віртуальних камер
    QStringList blacklist = {"obs", "virtual", "manycam", "splitcam", "snap camera", "xsplit"};

    for (const QString &badName : blacklist) {
        if (name.contains(badName)) {
            return true; // Знайдено віртуальну камеру
        }
    }
    return false;
}

void MediaValidator::startLivenessSession() {
    qDebug() << "[NETWORK] Starting video stream to server...";

    // Імітуємо затримку мережі (сервер аналізує відео і генерує завдання)
    QTimer::singleShot(1500, this, [this]() {
        emit instructionReceived("Будь ласка, повільно поверніть голову вліво, потім вправо.");
    });
}

void MediaValidator::simulateUserActionCompleted() {
    qDebug() << "[NETWORK] Sending action frames to server for analysis...";

    // Імітуємо роботу нейромережі на сервері
    QTimer::singleShot(2500, this, [this]() {
        emit checkResult(true, "Перевірка успішно пройдена!");
    });
}
