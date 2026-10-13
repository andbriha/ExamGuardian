#include "media_validator.h"
#include <QTimer>
#include <QVideoFrame>
#include <QDebug>

MediaValidator::MediaValidator(QObject *parent) : QObject(parent) {
    m_analysisTimer = new QTimer(this);
    m_analysisTimer->setInterval(1000);
    connect(m_analysisTimer, &QTimer::timeout, this, &MediaValidator::grabAndSendFrame);
}

bool MediaValidator::isVirtualCamera(const QString &cameraName) {
    QString name = cameraName.toLower();
    QStringList blacklist = {"obs", "virtual", "manycam", "splitcam", "snap camera", "xsplit", "droidcam"};
    for (const QString &bad : blacklist)
        if (name.contains(bad)) return true;
    return false;
}

QString MediaValidator::translateTask(const QString &task) {
    if (task == "turn_left")  return "Повільно поверніть голову ВЛІВО";
    if (task == "turn_right") return "Повільно поверніть голову ВПРАВО";
    if (task == "nod")        return "Повільно кивніть головою ВНИЗ і вверх";
    return task;
}

void MediaValidator::startVerificationSession() {
    qDebug() << "[VALIDATOR] Requesting challenge from ML server...";
    emit requestChallenge();
}

void MediaValidator::onChallengeReceived(const QString &task) {
    m_currentTask = task;
    emit instructionReceived(translateTask(m_currentTask));
}

void MediaValidator::simulateUserActionCompleted() {
    if (!m_videoSink) {
        emit checkResult(false, "Камера не підключена");
        return;
    }

    QVideoFrame frame = m_videoSink->videoFrame();
    if (!frame.isValid()) {
        emit checkResult(false, "Не вдалося захопити кадр");
        return;
    }

    emit verifyIdentityAndLiveness(frame.toImage(), m_currentTask);
}

void MediaValidator::onVerificationFinished(bool verified, const QString &stage, const QString &reason, double distance) {
    if (verified) {
        qDebug() << "[VALIDATOR] Verification passed successfully!";
        emit progressUpdated("✅ Верифікацію успішно пройдено!");
        emit checkResult(true, "Верифікацію пройдено");
        return;
    }

    qDebug() << "[VALIDATOR] Verification in progress. Stage:" << stage << "Reason:" << reason << "Dist:" << distance;

    QString hint;
    if (stage == "identity") {
        if (reason == "no_photo_on_server") {
            hint = "Еталонне фото відсутнє на сервері";
        } else if (reason == "no_face_in_frame" || reason == "no_face_detected") {
            hint = "Обличчя не видно. Будь ласка, подивіться прямо в камеру";
        } else {
            hint = "Подивіться прямо в камеру для підтвердження особи";
        }
    } else if (stage == "liveness") {
        if (reason == "wrong_pose") {
            hint = "Особу підтверджено! Тепер виконайте завдання: " + translateTask(m_currentTask);
        } else if (reason == "face_too_small") {
            hint = "Підсуньтесь трохи ближче до камери";
        } else if (reason == "no_face") {
            hint = "Обличчя не знайдено. Перевірте освітлення";
        } else {
            hint = "Виконайте завдання: " + translateTask(m_currentTask);
        }
    } else {
        hint = "Спробуйте ще раз";
    }

    emit progressUpdated(hint);
    emit checkResult(false, hint);
}

void MediaValidator::setVideoSink(QVideoSink *sink) {
    m_videoSink = sink;
}

void MediaValidator::startContinuousProctoring() {
    m_sessionStartTime = QDateTime::currentDateTime();
    if (!m_analysisTimer->isActive()) {
        m_analysisTimer->start();
        qDebug() << "[PROCTORING] Started (1 FPS)";
    }
}

void MediaValidator::stopContinuousProctoring() {
    m_analysisTimer->stop();
}

void MediaValidator::grabAndSendFrame() {
    if (!m_videoSink) return;
    QVideoFrame frame = m_videoSink->videoFrame();
    if (frame.isValid()) {
        int relSec = 0;
        if (m_sessionStartTime.isValid()) {
            relSec = m_sessionStartTime.secsTo(QDateTime::currentDateTime());
        }
        emit frameCaptured(frame.toImage(), relSec);
    }
}
