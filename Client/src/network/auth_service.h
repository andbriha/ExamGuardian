#pragma once
#include <QObject>
#include <QNetworkAccessManager>
#include <QNetworkReply>
#include <QString>

struct UserCredentials {
    QString accessToken;
    QString login;
    QString role;
};

class AuthService : public QObject {
    Q_OBJECT
public:
    explicit AuthService(QObject *parent = nullptr);
    void login(const QString &login, const QString &password, const QString &baseUrl);
    void refresh(const QString &baseUrl);

signals:
    void loginSucceeded(const UserCredentials &credentials);
    void loginFailed(const QString &error);

private:
    QNetworkAccessManager *m_manager;
};
