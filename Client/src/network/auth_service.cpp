#include "auth_service.h"
#include <QNetworkRequest>
#include <QJsonDocument>
#include <QJsonObject>
#include <QDebug>

AuthService::AuthService(QObject *parent) : QObject(parent) {
    m_manager = new QNetworkAccessManager(this);
}

void AuthService::login(const QString &login, const QString &password, const QString &baseUrl) {
    QNetworkRequest request(QUrl(baseUrl + "/api/Auth/login"));
    request.setHeader(QNetworkRequest::ContentTypeHeader, "application/json");

    QJsonObject body;
    body["login"]    = login;
    body["password"] = password;

    QNetworkReply *reply = m_manager->post(request, QJsonDocument(body).toJson());

    connect(reply, &QNetworkReply::finished, this, [this, reply]() {
        if (reply->error() != QNetworkReply::NoError) {
            emit loginFailed(reply->errorString());
            reply->deleteLater();
            return;
        }

        QJsonDocument doc  = QJsonDocument::fromJson(reply->readAll());
        QJsonObject   root = doc.object();

        if (!root["succeeded"].toBool()) {
            emit loginFailed("Невірний логін або пароль");
            reply->deleteLater();
            return;
        }

        QJsonObject resp = root["response"].toObject();
        UserCredentials creds;
        creds.accessToken = resp["accessToken"].toString();
        creds.login       = resp["login"].toString();
        creds.role        = resp["role"].toString();

        emit loginSucceeded(creds);
        reply->deleteLater();
    });
}

void AuthService::refresh(const QString &baseUrl) {
    QNetworkRequest request(QUrl(baseUrl + "/api/Auth/refresh"));
    request.setHeader(QNetworkRequest::ContentTypeHeader, "application/json");

    // refresh token береться з HttpOnly cookie — браузер відправляє автоматично
    // Qt не зберігає cookies за замовчуванням, тому треба QNetworkCookieJar
    QNetworkReply *reply = m_manager->post(request, QByteArray());

    connect(reply, &QNetworkReply::finished, this, [this, reply]() {
        if (reply->error() != QNetworkReply::NoError) {
            emit loginFailed("Сесія закінчилась, увійдіть знову");
            reply->deleteLater();
            return;
        }

        QJsonDocument doc  = QJsonDocument::fromJson(reply->readAll());
        QJsonObject   root = doc.object();

        if (!root["succeeded"].toBool()) {
            emit loginFailed("Токен недійсний");
            reply->deleteLater();
            return;
        }

        QJsonObject resp = root["response"].toObject();
        UserCredentials creds;
        creds.accessToken = resp["accessToken"].toString();
        creds.login       = resp["login"].toString();
        creds.role        = resp["role"].toString();

        emit loginSucceeded(creds);
        reply->deleteLater();
    });
}
