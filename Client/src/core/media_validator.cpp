#include "media_validator.h"
#include <QTimer>
#include <QVideoFrame>
#include <QDebug>

MediaValidator::MediaValidator(QObject *parent) : QObject(parent) {
    m_verificationTimer = new QTimer(this);
    m_verificationTimer->setInterval(125);
    connect(m_verificationTimer, &QTimer::timeout, this, &MediaValidator::captureVerificationFrame);
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
    if (task == "look_up") return "Повільно підніміть підборіддя ВГОРУ";
    if (task == "smile") return "Усміхніться та утримайте усмішку";
    if (task == "open_mouth") return "Відкрийте рот та утримайте положення";
    if (task == "close_eyes") return "Заплющте очі на дві секунди, потім відкрийте";
    if (task.startsWith("say_phrase:")) return "Чітко скажіть: «" + task.mid(11) + "»";
    return task;
}

void MediaValidator::startVerificationSession() {
    qDebug() << "[VALIDATOR] Requesting challenge from ML server...";
    stopVerificationSession();
    emit requestChallenge();
}

void MediaValidator::stopVerificationSession() {
    m_verificationTimer->stop();
    m_currentTask.clear();
}

void MediaValidator::onChallengeReceived(const QString &task) {
    m_currentTask = task;
    m_verificationTimer->start();
    emit instructionReceived(translateTask(m_currentTask));
    emit verificationPhaseChanged("prepare");
}

void MediaValidator::captureVerificationFrame() {
    if (!m_videoSink || !m_frameClock.isValid() || m_frameClock.elapsed() > 3000) {
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
        stopVerificationSession();
        qDebug() << "[VALIDATOR] Verification passed successfully!";
        emit progressUpdated("✅ Верифікацію успішно пройдено!");
#ifdef EXAMGUARDIAN_DEVELOPMENT
        if (stage == "development" && reason == "identity_skipped") {
            emit checkResult(true, "DEV: перевірку особи пропущено. Це непідтверджена тестова спроба.");
            return;
        }
#endif
        emit checkResult(true, "Верифікацію пройдено");
        return;
    }

    qDebug() << "[VALIDATOR] Verification in progress. Stage:" << stage << "Reason:" << reason << "Dist:" << distance;

    if(stage=="identity" || stage=="quality" || reason=="look_forward_first" || reason=="next_action") emit verificationPhaseChanged("prepare");
    else if(reason=="return_neutral" || reason=="confirm_live_face") emit verificationPhaseChanged("return");
    else if(reason=="perform_action" || reason=="wrong_pose" || reason=="hold_pose" || reason=="say_phrase") emit verificationPhaseChanged("perform");
    QString hint;
    if (stage == "quality") {
        if (reason == "too_dark") hint = "Додайте світло перед обличчям, а не позаду.";
        else if (reason == "too_bright") hint = "Зменште яскраве світло перед обличчям.";
        else if (reason == "too_blurry") hint = "Протріть камеру та зачекайте фокусування.";
        else if (reason == "multiple_faces") hint = "У кадрі має бути лише одна людина.";
        else if (reason == "face_too_small") hint = "Підсуньтесь ближче до камери.";
        else hint = "Розташуйте обличчя в центрі кадру.";
    } else if (stage == "identity") {
        if (reason == "spoof_suspected") {
            hint = "Кадр не пройшов перевірку захисту від фото/відеопідміни. Дивіться прямо в камеру та дочекайтеся повторної перевірки.";
        } else if (reason == "no_photo_on_server") {
            hint = "Еталонне фото відсутнє на сервері";
        } else if (reason == "no_face_in_frame" || reason == "no_face_detected") {
            hint = "Обличчя не видно. Будь ласка, подивіться прямо в камеру";
        } else {
            hint = "Подивіться прямо в камеру для підтвердження особи";
        }
    } else if (stage == "liveness") {
        if (reason == "quality_paused") {
            hint = "Кадр нечіткий або змінилося освітлення. Зачекайте мить — прогрес дії збережено.";
        } else if (reason == "confirm_live_face") {
            hint = "Рух виконано. Дивіться прямо в камеру: підтверджуємо живу присутність повторно.";
        } else if (reason == "server_busy") {
            hint = "Сервер зайнятий. Автоматично повторюємо перевірку…";
        } else if (reason == "return_neutral") {
            hint = "Поверніться до вихідного положення: дивіться прямо, відкрийте очі та розслабте обличчя.";
        } else if (reason == "fresh_frame_required") {
            hint = "Очікуємо новий кадр камери.";
        } else if (reason == "next_action") {
            hint = "Дію зараховано! Наступне завдання…";
        } else if (reason == "look_forward_first") {
            hint = "Спочатку подивіться прямо в камеру.";
        } else if (reason == "hold_pose") {
            hint = "Утримайте дію ще мить, потім дочекайтеся підказки.";
        } else if (reason == "wrong_pose" || reason == "perform_action" || reason == "say_phrase") {
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
    if (m_videoSink) disconnect(m_videoSink,nullptr,this,nullptr);
    m_videoSink = sink;
    if (sink) connect(sink,&QVideoSink::videoFrameChanged,this,[this](const QVideoFrame &){m_frameClock.restart();});
}

void MediaValidator::startContinuousProctoring(int elapsedSeconds) {
    m_startOffset = elapsedSeconds; m_sessionClock.start();
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
    if (!m_videoSink || !m_frameClock.isValid() || m_frameClock.elapsed() > 3000) { emit analysisUnavailable("Свіжий кадр камери не отримано. Перевірте пристрій."); return; }
    QVideoFrame frame = m_videoSink->videoFrame();
    if (frame.isValid()) {
        int relSec = 0;
        if (m_sessionStartTime.isValid()) {
            relSec = m_startOffset + m_sessionClock.elapsed()/1000;
        }
        emit frameCaptured(frame.toImage(), relSec);
    }
}
