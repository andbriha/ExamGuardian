#include "ml_api_client.h"
#include <QHttpMultiPart>
#include <QBuffer>
#include <QJsonDocument>
#include <QJsonObject>
#include <QUrlQuery>
#include <QDebug>

MlApiClient::MlApiClient(QObject *parent) : QObject(parent) {
    m_manager = new QNetworkAccessManager(this);
}

void MlApiClient::setAuthToken(const QString &token) {
    m_authToken = token;
}

void MlApiClient::fetchChallenge() {
    QNetworkRequest request(QUrl(m_baseUrl + "/verify/challenge"));

    if (!m_authToken.isEmpty()) {
        request.setRawHeader("Authorization", ("Bearer " + m_authToken).toUtf8());
    }

    QNetworkReply *reply = m_manager->get(request);

    connect(reply, &QNetworkReply::finished, this, [this, reply]() {
        if (reply->error() == QNetworkReply::NoError) {
            QJsonDocument doc = QJsonDocument::fromJson(reply->readAll());
            QString task = doc.object()["task"].toString();
            emit challengeReceived(task);
        } else {
            qDebug() << "[ML SERVER ERROR] Fetch Challenge:" << reply->errorString();
        }
        reply->deleteLater();
    });
}

void MlApiClient::sendVerificationFrame(const QImage &currentFrame, const QString &task) {
    if (currentFrame.isNull()) return;

    QByteArray frameBytes;
    QBuffer buffer(&frameBytes);
    buffer.open(QIODevice::WriteOnly);
    currentFrame.save(&buffer, "JPG");

    QHttpMultiPart *multiPart = new QHttpMultiPart(QHttpMultiPart::FormDataType);

    QHttpPart curPart;
    curPart.setHeader(QNetworkRequest::ContentTypeHeader, QVariant("image/jpeg"));
    curPart.setHeader(QNetworkRequest::ContentDispositionHeader, QVariant("form-data; name=\"current\"; filename=\"frame.jpg\""));
    curPart.setBody(frameBytes);
    multiPart->append(curPart);

    QUrl url(m_baseUrl + "/verify/identity-and-liveness");
    QUrlQuery query;
    query.addQueryItem("task", task);
    url.setQuery(query);

    QNetworkRequest request(url);

    if (!m_authToken.isEmpty()) {
        request.setRawHeader("Authorization", ("Bearer " + m_authToken).toUtf8());
    }

    QNetworkReply *reply = m_manager->post(request, multiPart);
    multiPart->setParent(reply);

    connect(reply, &QNetworkReply::finished, this, [this, reply]() {
        if (reply->error() == QNetworkReply::NoError) {
            QJsonDocument doc = QJsonDocument::fromJson(reply->readAll());
            QJsonObject obj = doc.object();
            emit verificationFinished(
                obj["verified"].toBool(),
                obj["stage"].toString(),
                obj["reason"].toString(),
                obj["id_distance"].toDouble()
                );
        } else {
            emit verificationFinished(false, "network", reply->errorString(), -1.0);
        }
        reply->deleteLater();
    });
}

void MlApiClient::sendFrameForContinuousAnalysis(const QImage &image, int relativeSec) {
    if (image.isNull()) return;

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

    QUrl url(m_baseUrl + "/analyze/frame");
    QUrlQuery query;
    if (m_sessionId > 0) {
        query.addQueryItem("session_id", QString::number(m_sessionId));
    }
    query.addQueryItem("relative_ts", QString::number(relativeSec));
    url.setQuery(query);

    QNetworkRequest request(url);

    if (!m_authToken.isEmpty()) {
        request.setRawHeader("Authorization", ("Bearer " + m_authToken).toUtf8());
    }

    QNetworkReply *reply = m_manager->post(request, multiPart);
    multiPart->setParent(reply);

    connect(reply, &QNetworkReply::finished, this, [this, reply]() {
        if (reply->error() == QNetworkReply::NoError) {
            QJsonDocument jsonDoc = QJsonDocument::fromJson(reply->readAll());
            QJsonObject root = jsonDoc.object();

            if (root["violation"].toBool()) {
                emit violationDetected("anomaly_detected");
                emit continuousAnalysisFinished(false, "Знайдено порушення!");
            } else {
                emit continuousAnalysisFinished(true, "Все чисто");
            }
        }
        reply->deleteLater();
    });
}
