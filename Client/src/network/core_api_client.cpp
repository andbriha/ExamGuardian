#include "core_api_client.h"
#include <QHttpMultiPart>
#include <QFile>
#include <QNetworkReply>
#include <QJsonObject>
#include <QJsonDocument>
#include <QDebug>

CoreApiClient::CoreApiClient(QObject *parent) : QObject(parent) {
    m_manager = new QNetworkAccessManager(this);
}

void CoreApiClient::reportViolation(int sessionId, const QString &violationType, double confidence, int relativeTimestampSeconds, const QString &metadata) {
    QJsonObject json;
    json["examSessionId"] = sessionId;
    json["violationType"] = violationType;
    json["confidence"] = confidence;
    json["relativeTimestampSeconds"] = relativeTimestampSeconds;
    json["metadata"] = metadata;

    QNetworkRequest request(QUrl(m_baseUrl + "/api/Violations/report"));
    request.setHeader(QNetworkRequest::ContentTypeHeader, "application/json");
    if (!m_authToken.isEmpty()) {
        request.setRawHeader("Authorization", ("Bearer " + m_authToken).toUtf8());
    }

    QNetworkReply *reply = m_manager->post(request, QJsonDocument(json).toJson());
    connect(reply, &QNetworkReply::finished, this, [reply, violationType]() {
        if (reply->error() == QNetworkReply::NoError) {
            qDebug() << "[CoreApiClient] Violation reported successfully:" << violationType;
        } else {
            qDebug() << "[CoreApiClient] Failed to report violation:" << reply->errorString();
        }
        reply->deleteLater();
    });
}

void CoreApiClient::setAuthToken(const QString &token) {
    m_authToken = token;
}

void CoreApiClient::uploadVideoChunk(const QString &sessionId, int chunkIndex, const QString &filePath) {
    QFile *file = new QFile(filePath);
    if (!file->open(QIODevice::ReadOnly)) {
        qDebug() << "Cannot open video file for upload:" << filePath;
        delete file;
        return;
    }

    QHttpMultiPart *multiPart = new QHttpMultiPart(QHttpMultiPart::FormDataType);

    QHttpPart sessionPart;
    sessionPart.setHeader(QNetworkRequest::ContentDispositionHeader, QVariant("form-data; name=\"sessionId\""));
    sessionPart.setBody(sessionId.toUtf8());
    multiPart->append(sessionPart);

    QHttpPart chunkPart;
    chunkPart.setHeader(QNetworkRequest::ContentDispositionHeader, QVariant("form-data; name=\"chunkIndex\""));
    chunkPart.setBody(QByteArray::number(chunkIndex));
    multiPart->append(chunkPart);

    QHttpPart videoPart;
    videoPart.setHeader(QNetworkRequest::ContentTypeHeader, QVariant("video/mp4"));
    videoPart.setHeader(QNetworkRequest::ContentDispositionHeader, QVariant("form-data; name=\"file\"; filename=\"chunk.mp4\""));
    videoPart.setBodyDevice(file);
    file->setParent(multiPart);
    multiPart->append(videoPart);

    QNetworkRequest request(QUrl(m_baseUrl + "/api/video/upload-chunk"));
    if (!m_authToken.isEmpty()) {
        request.setRawHeader("Authorization", ("Bearer " + m_authToken).toUtf8());
    }

    QNetworkReply *reply = m_manager->post(request, multiPart);
    multiPart->setParent(reply);

    connect(reply, &QNetworkReply::finished, this, [this, reply, filePath]() {
        if (reply->error() == QNetworkReply::NoError) {
            emit videoUploadedSuccessfully(filePath);
            QFile::remove(filePath); // Видаляємо локально після успішного завантаження
        } else {
            emit videoUploadFailed(reply->errorString());
        }
        reply->deleteLater();
    });
}