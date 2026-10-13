#pragma once
#include <QObject>
#include <QString>
#include <QVariantList>
#include <QVariantMap>
#include <QNetworkAccessManager>
#include <QDateTime>
#include "input_blocker.h"
#include "auth_service.h"

class ExamViewModel : public QObject {
    Q_OBJECT

    Q_PROPERTY(QString userLogin READ userLogin NOTIFY userChanged)
    Q_PROPERTY(QString userRole  READ userRole  NOTIFY userChanged)
    Q_PROPERTY(QString authToken READ authToken NOTIFY userChanged)
    Q_PROPERTY(QVariantList questions READ questions NOTIFY examLoaded)
    Q_PROPERTY(int durationSeconds READ durationSeconds NOTIFY examLoaded)
    Q_PROPERTY(QString examTitle READ examTitle NOTIFY examLoaded)
    Q_PROPERTY(int sessionId READ sessionId NOTIFY sessionIdChanged)

public:
    explicit ExamViewModel(QObject *parent = nullptr);

    Q_INVOKABLE void setKeyBlock(bool enable);
    Q_INVOKABLE void login(const QString &login, const QString &password);
    Q_INVOKABLE void loadExam();
    Q_INVOKABLE void submitAnswers(const QVariantMap &answers);
    Q_INVOKABLE void reportProhibitedProcess(const QString &processName);

    QString userLogin() const { return m_login; }
    QString userRole()  const { return m_role;  }
    QString authToken() const { return m_token; }

    QVariantList questions() const { return m_questions; }
    int durationSeconds() const { return m_durationSeconds; }
    QString examTitle() const { return m_examTitle; }
    int sessionId() const { return m_sessionId; }

signals:
    void loginFailed(const QString &error);
    void userChanged();
    void loginSucceeded(QString token);
    void examLoaded();
    void sessionIdChanged(int sessionId);
    void examScoreReceived(int score, int totalQuestions);
    void submitFinished(bool success, const QString &message);
    void prohibitedProcessAlert(const QString &processName);

private:
    void setupFallbackExam();

    InputBlocker m_inputBlocker;
    AuthService  m_auth;
    QNetworkAccessManager m_net;

    QString m_login;
    QString m_role;
    QString m_token;

    QVariantList m_questions;
    int m_durationSeconds = 3600;
    QString m_examTitle = "Завантаження іспиту...";
    int m_examId = 1;
    int m_sessionId = 1;
    QDateTime m_examStartTime;

    const QString m_baseUrl = "http://localhost";
};
