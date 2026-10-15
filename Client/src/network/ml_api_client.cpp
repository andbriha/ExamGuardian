#include "client_config.h"
#include "ml_api_client.h"
#include <QHttpMultiPart>
#include <QBuffer>
#include <QJsonDocument>
#include <QJsonObject>
#include <QUrlQuery>
#include <QDebug>
#include <QStringList>
#include <QElapsedTimer>

MlApiClient::MlApiClient(QObject *parent) : QObject(parent) {
    m_manager = new QNetworkAccessManager(this);
    m_manager->setTransferTimeout(15000);
}

void MlApiClient::setAuthToken(const QString &token) {
    m_authToken = token;
}

void MlApiClient::cancelVerification() {
    ++m_verificationEpoch;
    m_challengeId.clear();
    m_verificationPending = false;
    m_verificationAudioPending = false;
    m_verificationFailures = 0;
}

void MlApiClient::fetchChallenge() {
    cancelVerification();
    const auto epoch = m_verificationEpoch;
    QNetworkRequest request(QUrl(clientServerUrl() + QString("/verify/challenge?client_protocol=3&exam_id=%1").arg(m_examId)));

    if (!m_authToken.isEmpty()) {
        request.setRawHeader("X-Session-Token", m_attemptToken.toUtf8());
    request.setRawHeader("Authorization", ("Bearer " + m_authToken).toUtf8());
    }

    QNetworkReply *reply = m_manager->get(request);

    connect(reply, &QNetworkReply::finished, this, [this, reply, epoch]() {
        if (epoch != m_verificationEpoch) { reply->deleteLater(); return; }
        if (reply->error() == QNetworkReply::NoError) {
            QJsonDocument doc = QJsonDocument::fromJson(reply->readAll());
            m_challengeId = doc.object()["challenge_id"].toString();
            QString task = doc.object()["task"].toString();
            if (doc.object()["protocol_version"].toInt() != 3 || m_challengeId.isEmpty() || (!QStringList{"turn_left", "turn_right", "nod", "look_up", "smile", "open_mouth", "close_eyes"}.contains(task))) emit verificationError(QString("Версії перевірки не збігаються: клієнт 3, сервер %1 (%2). Перезапустіть AI-сервіс та оновіть клієнт.").arg(doc.object()["protocol_version"].toInt()).arg(clientServerUrl()));
            else emit challengeReceived(task);
        } else {
            emit verificationError("Не вдалося отримати завдання. Перевірте з’єднання та спробуйте повторно.");
        }
        reply->deleteLater();
    });
}

void MlApiClient::sendVerificationFrame(const QImage &currentFrame, const QString &task) {
    if (currentFrame.isNull() || m_verificationPending || m_challengeId.isEmpty()) return;
    m_verificationPending = true;
    const auto epoch = m_verificationEpoch;
    QElapsedTimer requestClock;
    requestClock.start();

    QByteArray frameBytes;
    QBuffer buffer(&frameBytes);
    buffer.open(QIODevice::WriteOnly);
    const QImage upload = currentFrame.width() > 960 || currentFrame.height() > 960
        ? currentFrame.scaled(960, 960, Qt::KeepAspectRatio, Qt::SmoothTransformation) : currentFrame;
    upload.save(&buffer, "JPG", 85);

    QHttpMultiPart *multiPart = new QHttpMultiPart(QHttpMultiPart::FormDataType);

    QHttpPart curPart;
    curPart.setHeader(QNetworkRequest::ContentTypeHeader, QVariant("image/jpeg"));
    curPart.setHeader(QNetworkRequest::ContentDispositionHeader, QVariant("form-data; name=\"current\"; filename=\"frame.jpg\""));
    curPart.setBody(frameBytes);
    multiPart->append(curPart);

    QUrl url(clientServerUrl() + "/verify/identity-and-liveness");
    QUrlQuery query;
    query.addQueryItem("task", task.startsWith("say_phrase:") ? "say_phrase" : task);
    query.addQueryItem("challenge_id", m_challengeId);
    url.setQuery(query);

    QNetworkRequest request(url);

    if (!m_authToken.isEmpty()) {
        request.setRawHeader("X-Session-Token", m_attemptToken.toUtf8());
    request.setRawHeader("Authorization", ("Bearer " + m_authToken).toUtf8());
    }

    QNetworkReply *reply = m_manager->post(request, multiPart);
    multiPart->setParent(reply);

    connect(reply, &QNetworkReply::finished, this, [this, reply, epoch, requestClock]() {
        if (epoch != m_verificationEpoch) { reply->deleteLater(); return; }
        m_verificationPending = false;
        if (reply->error() == QNetworkReply::NoError) {
            m_verificationFailures = 0;
            QJsonDocument doc = QJsonDocument::fromJson(reply->readAll());
            QJsonObject obj = doc.object();
            qInfo() << "[VERIFICATION] roundtrip_ms=" << requestClock.elapsed()
                    << "processing_ms=" << obj["processing_ms"].toDouble()
                    << "checkpoint=" << obj["identity_checkpoint"].toBool()
                    << "reason=" << obj["reason"].toString();
            if (obj["verified"].toBool()) emit admissionReceived(obj["admission_token"].toString());
            const QString nextTask = obj["next_task"].toString();
            emit verificationFinished(
                obj["verified"].toBool(),
                obj["stage"].toString(),
                obj["reason"].toString(),
                obj["id_distance"].toDouble()
                );
            if (!nextTask.isEmpty()) emit challengeReceived(nextTask == "say_phrase" ? "say_phrase:" + obj["phrase"].toString() : nextTask);
        } else if ((reply->attribute(QNetworkRequest::HttpStatusCodeAttribute).toInt() == 429 || reply->error() == QNetworkReply::TimeoutError || reply->error() == QNetworkReply::TemporaryNetworkFailureError) && ++m_verificationFailures <= 3) {
            emit verificationFinished(false, "liveness", "server_busy", 0);
        } else {
            emit verificationError("Перевірку не підтверджено або завдання закінчилося. Запросіть нове завдання.");
        }
        reply->deleteLater();
    });
}

void MlApiClient::sendFrameForContinuousAnalysis(const QImage &image, int relativeSec) {
    if (image.isNull() || m_sessionId <= 0 || m_analysisPending) return;
    m_analysisPending = true;

    QByteArray imageBytes;
    QBuffer buffer(&imageBytes);
    buffer.open(QIODevice::WriteOnly);
    image.save(&buffer, "JPG");

    QHttpMultiPart *multiPart = new QHttpMultiPart(QHttpMultiPart::FormDataType);
    QHttpPart imagePart;
    imagePart.setHeader(QNetworkRequest::ContentTypeHeader, QVariant("image/jpeg"));
    imagePart.setHeader(QNetworkRequest::ContentDispositionHeader, QVariant("form-data; name=\"file\"; filename=\"frame.jpg\""));
    imagePart.setBody(imageBytes);
    multiPart->append(imagePart);

    QUrl url(clientServerUrl() + "/analyze/frame");
    QUrlQuery query;
    if (m_sessionId > 0) {
        query.addQueryItem("session_id", QString::number(m_sessionId));
    }
    query.addQueryItem("relative_ts", QString::number(relativeSec));
    url.setQuery(query);

    QNetworkRequest request(url);

    if (!m_authToken.isEmpty()) {
        request.setRawHeader("X-Session-Token", m_attemptToken.toUtf8());
    request.setRawHeader("Authorization", ("Bearer " + m_authToken).toUtf8());
    }

    QNetworkReply *reply = m_manager->post(request, multiPart);
    multiPart->setParent(reply);

    connect(reply, &QNetworkReply::finished, this, [this, reply]() {
        if (reply->error() == QNetworkReply::NoError) {
            m_analysisPending = false;
            QJsonDocument jsonDoc = QJsonDocument::fromJson(reply->readAll());
            QJsonObject root = jsonDoc.object();

            if (root["violation"].toBool()) {
                emit violationDetected("anomaly_detected");
                emit continuousAnalysisFinished(false, "Знайдено порушення!");
            } else {
                emit continuousAnalysisFinished(true, "Кадр проаналізовано. Автоматичних сигналів немає.");
            }
        } else {
            m_analysisPending = false;
            emit continuousAnalysisFinished(false, "Аналіз недоступний. Дані очікують перевірки.");
        }
        reply->deleteLater();
    });
}

void MlApiClient::sendAudioForAnalysis(const QByteArray &pcm, int relativeSeconds) {
    if (m_sessionId <= 0 || m_audioPending || m_authToken.isEmpty()) return;
    auto *parts=new QHttpMultiPart(QHttpMultiPart::FormDataType);
    QHttpPart audio; audio.setHeader(QNetworkRequest::ContentTypeHeader,"application/octet-stream");
    audio.setHeader(QNetworkRequest::ContentDispositionHeader,"form-data; name=\"file\"; filename=\"audio.pcm\""); audio.setBody(pcm); parts->append(audio);
    QNetworkRequest req(QUrl(clientServerUrl()+QString("/analyze/audio?session_id=%1&relative_ts=%2&sample_rate=16000").arg(m_sessionId).arg(relativeSeconds)));
    req.setRawHeader("X-Session-Token", m_attemptToken.toUtf8());
    req.setRawHeader("Authorization",("Bearer "+m_authToken).toUtf8());
    m_audioPending=true;
    auto *reply=m_manager->post(req,parts); parts->setParent(reply);
    connect(reply,&QNetworkReply::finished,this,[this,reply] {
        m_audioPending=false;
        if (reply->error()!=QNetworkReply::NoError) emit continuousAnalysisFinished(false,"Аудіоаналіз недоступний. Запис залишається матеріалом для перевірки.");
        reply->deleteLater();
    });
}

void MlApiClient::sendVerificationAudio(const QByteArray &pcm) {
    if (m_challengeId.isEmpty() || m_verificationAudioPending || pcm.isEmpty()) return;
    const auto epoch = m_verificationEpoch;
    auto *parts = new QHttpMultiPart(QHttpMultiPart::FormDataType);
    QHttpPart audio;
    audio.setHeader(QNetworkRequest::ContentTypeHeader, "application/octet-stream");
    audio.setHeader(QNetworkRequest::ContentDispositionHeader, "form-data; name=\"file\"; filename=\"audio.pcm\"");
    audio.setBody(pcm); parts->append(audio);
    QUrl url(clientServerUrl() + "/verify/audio");
    QUrlQuery query; query.addQueryItem("challenge_id", m_challengeId); url.setQuery(query);
    QNetworkRequest request(url);
    request.setRawHeader("Authorization", ("Bearer " + m_authToken).toUtf8());
    request.setTransferTimeout(15000);
    m_verificationAudioPending = true;
    auto *reply = m_manager->post(request, parts); parts->setParent(reply);
    connect(reply, &QNetworkReply::finished, this, [this, reply, epoch] {
        if (epoch != m_verificationEpoch) { reply->deleteLater(); return; }
        m_verificationAudioPending = false;
        if (reply->error() == QNetworkReply::NoError) {
            const auto result = QJsonDocument::fromJson(reply->readAll()).object();
            if (result.contains("speech_ready")) {
                if (!result["speech_ready"].toBool()) emit verificationAudioStatus("Спочатку дивіться прямо. Фразу скажіть після підказки.");
                else if (result["phrase_matched"].toBool()) emit verificationAudioStatus("Фразу підтверджено. Дивіться прямо в камеру.");
                else emit verificationAudioStatus("Розпізнано: «" + result["transcript"].toString() + "». Повторіть усю фразу після короткої паузи.");
                reply->deleteLater();
                return;
            }
            emit verificationAudioStatus(result["speech_detected"].toBool()
                ? "Мікрофон: мовлення виявлено (зміст фрази не перевіряється)"
                : "Мікрофон працює. Мовлення не виявлено.");
        } else emit verificationAudioStatus("Аналіз мікрофона недоступний. Перевірте підключення.");
        reply->deleteLater();
    });
}
