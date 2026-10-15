#include <QTimer>
#include <QElapsedTimer>
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
    Q_PROPERTY(QVariantList availableExams READ availableExams NOTIFY availableExamsChanged)
    Q_PROPERTY(QVariantMap savedAnswers READ savedAnswers NOTIFY examLoaded)
    Q_PROPERTY(QVariantMap selectedExam READ selectedExam NOTIFY selectedExamChanged)
    Q_PROPERTY(QString serverUrl READ serverUrl NOTIFY userChanged)
    Q_PROPERTY(int sessionId READ sessionId NOTIFY sessionIdChanged)

public:
    explicit ExamViewModel(QObject *parent = nullptr);

    Q_INVOKABLE bool setKeyBlock(bool enable);
    Q_INVOKABLE void reportFocusLoss();
    Q_INVOKABLE void reportEnvironmentObservation(const QString &type, const QString &details);
    Q_INVOKABLE void login(const QString &login, const QString &password);
    Q_INVOKABLE void loadExam();
    Q_INVOKABLE void loadAvailableExams();
    Q_INVOKABLE void selectExam(int id) { m_examId = id; m_admissionToken.clear(); emit selectedExamChanged(); }
    Q_INVOKABLE void setAdmissionToken(const QString &token) { m_admissionToken = token; }
    Q_INVOKABLE bool configureServer(const QString &url);
    Q_INVOKABLE void saveAnswers(const QVariantMap &answers);
    Q_INVOKABLE void heartbeat();
    Q_INVOKABLE double elapsedSeconds() const { return m_serverElapsed + (m_sessionClock.isValid() ? m_sessionClock.elapsed()/1000.0 : 0); }
    QVariantList availableExams() const { return m_availableExams; }
    QVariantMap savedAnswers() const { return m_savedAnswers; }
    QString serverUrl() const;
    QVariantMap selectedExam() const { for (const auto &value : m_availableExams) { const auto exam=value.toMap(); if (exam["id"].toInt()==m_examId) return exam; } return {}; }

    Q_INVOKABLE void submitAnswers(const QVariantMap &answers);
    Q_INVOKABLE void reportProhibitedProcess(const QString &processName);

    QString userLogin() const { return m_login; }
    QString userRole()  const { return m_role;  }
    QString authToken() const { return m_token; }

    QVariantList questions() const { return m_questions; }
    int durationSeconds() const { return qMax(0, m_durationSeconds - (m_sessionClock.isValid() ? int(m_sessionClock.elapsed()/1000) : 0)); }
    QString examTitle() const { return m_examTitle; }
    int sessionId() const { return m_sessionId; }

signals:
    void loginFailed(const QString &error);
    void userChanged();
    void loginSucceeded(QString token);
    void authTokenChanged(QString token);
    void examLoaded();
    void availableExamsChanged();
    void selectedExamChanged();
    void examLoadFailed(const QString &message);
    void sessionTerminated();
    void answersSaved(bool success);
    void sessionIdChanged(int sessionId);
    void attemptReady(int sessionId, const QString &attemptToken, const QString &evidenceToken);
    void examScoreReceived(int score, int totalQuestions);
    void submitFinished(bool success, const QString &message);
    void prohibitedProcessAlert(const QString &processName);
    void observationCaptured(int sessionId, const QString &type, int relativeSeconds, const QString &metadata);

private:


    InputBlocker m_inputBlocker;
    AuthService  m_auth;
    QTimer m_refreshTimer;
    QNetworkAccessManager m_net;

    QString m_login;
    QString m_role;
    QString m_token;

    QVariantList m_questions;
    int m_durationSeconds = 3600;
    QString m_examTitle = "Завантаження іспиту...";
    QVariantList m_availableExams;
    QVariantMap m_savedAnswers;
    QVariantMap m_latestAnswers;
    bool m_saveInFlight = false;
    bool m_submitted = false;
    QString m_admissionToken;
    QHash<QString,double> m_lastObservation;
    QString m_answersPath;
    QString m_attemptToken;
    QString m_evidenceToken;
    int m_examId = 0;
    int m_sessionId = 0;
    QDateTime m_examStartTime;
    QElapsedTimer m_sessionClock;
    double m_serverElapsed = 0;

};
