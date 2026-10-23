#ifdef EXAMGUARDIAN_DEVELOPMENT
#include "development_viewmodel.h"
#endif
#include "client_config.h"
#include <QUrl>
#include <QSettings>
#include <QStandardPaths>
#include <QDir>
#include <QFile>
#include <QSaveFile>
#include <QCryptographicHash>
#include "exam_viewmodel.h"
#include <QNetworkRequest>
#include <QNetworkReply>
#include <QJsonDocument>
#include <QJsonArray>
#include <QJsonObject>
#include <QDebug>

ExamViewModel::ExamViewModel(QObject *parent) : QObject(parent) {
    m_net.setTransferTimeout(15000);
    m_refreshTimer.setSingleShot(true);
    connect(&m_refreshTimer,&QTimer::timeout,this,[this]{m_auth.refresh(clientServerUrl());});
    connect(&m_auth, &AuthService::loginSucceeded,
            this, [this](const UserCredentials &creds) {
                if (creds.role != "Student") { emit loginFailed("Для складання іспиту використайте обліковий запис студента."); return; }
                const bool initialLogin = m_token.isEmpty();
                m_login = creds.login;
                m_role  = creds.role;
                m_token = creds.accessToken;
                emit userChanged();
                emit authTokenChanged(m_token);
                if (initialLogin) emit loginSucceeded(m_token);
                if (creds.expiresAt.isValid()) m_refreshTimer.start(qMax(1000,int(QDateTime::currentDateTimeUtc().msecsTo(creds.expiresAt)-60000)));
            });

    connect(&m_auth, &AuthService::loginFailed,
            this, &ExamViewModel::loginFailed);
}

bool ExamViewModel::setKeyBlock(bool enable) {
#ifdef EXAMGUARDIAN_DEVELOPMENT
    if(enable && DevelopmentViewModel::current && DevelopmentViewModel::current->bypass("keyboardHooks")) enable=false;
#endif
    const bool installed = m_inputBlocker.setKeyBlock(enable);
    if (!installed) emit examLoadFailed("Не вдалося встановити блокування середовища. Linux потребує X11.");
    return installed;
}

void ExamViewModel::login(const QString &login, const QString &password) {
    m_auth.login(login, password, clientServerUrl());
}


QString ExamViewModel::serverUrl() const { return clientServerUrl(); }
bool ExamViewModel::configureServer(const QString &value) {
    QUrl url(value.trimmed());
    if (!url.isValid() || url.host().isEmpty() || (url.scheme() != "http" && url.scheme() != "https") || !url.userInfo().isEmpty() || url.hasQuery() || url.hasFragment()) return false;
    QSettings().setValue("serverUrl", url.toString());
    emit userChanged(); return true;
}
void ExamViewModel::loadAvailableExams() {
    if (m_catalogLoading || m_token.isEmpty()) return;
    m_catalogLoading = true;
    emit catalogLoadingChanged();
    QNetworkRequest req(QUrl(clientServerUrl() + "/api/Exams"));
    req.setRawHeader("Authorization", ("Bearer " + m_token).toUtf8());
    auto *reply = m_net.get(req);
    connect(reply, &QNetworkReply::finished, this, [this,reply]() {
        m_catalogLoading = false;
        emit catalogLoadingChanged();
        QJsonParseError error;
        const auto document = QJsonDocument::fromJson(reply->readAll(), &error);
        if (reply->error() == QNetworkReply::NoError && error.error == QJsonParseError::NoError && document.isArray()) {
            auto exams = document.array().toVariantList();
            if (!exams.isEmpty()) {
                const auto serverTime = QDateTime::fromString(exams.first().toMap()["serverTimeUtc"].toString(), Qt::ISODateWithMs);
                if (serverTime.isValid()) { m_catalogServerTime = serverTime.toMSecsSinceEpoch(); m_catalogClock.start(); }
            }
            // Server time changes on every poll; do not rebuild unchanged cards or lose keyboard focus.
            for (auto &value : exams) {
                auto exam = value.toMap();
                exam.remove("serverTimeUtc");
                value = exam;
            }
            if (m_availableExams != exams) {
                m_availableExams = exams;
                emit availableExamsChanged();
                emit selectedExamChanged();
            }
            emit catalogLoaded();
        } else {
            emit catalogLoadFailed(QStringLiteral("Не вдалося оновити список. Перевірте з’єднання із сервером; повторна спроба відбудеться автоматично."));
        }
        reply->deleteLater();
    });
}
void ExamViewModel::loadExam() {
    if (m_token.isEmpty() || m_admissionToken.isEmpty() || m_examId <= 0) { emit examLoadFailed("Спочатку оберіть іспит та пройдіть перевірку особи."); return; }
    QNetworkRequest req(QUrl(clientServerUrl() + "/api/ExamSessions/start"));
    req.setHeader(QNetworkRequest::ContentTypeHeader, "application/json");
    req.setRawHeader("X-Session-Token", m_attemptToken.toUtf8());
    req.setRawHeader("Authorization", ("Bearer " + m_token).toUtf8());
    QJsonObject body{{"examId", m_examId}, {"admissionToken", m_admissionToken}};
    auto *reply = m_net.post(req, QJsonDocument(body).toJson());
    connect(reply, &QNetworkReply::finished, this, [this,reply]() {
        if (reply->error() != QNetworkReply::NoError) {
            auto error = QJsonDocument::fromJson(reply->readAll()).object()["message"].toString(reply->errorString());
            emit examLoadFailed(error); reply->deleteLater(); return;
        }
        auto session = QJsonDocument::fromJson(reply->readAll()).object();
        m_sessionId = session["id"].toInt();
#ifdef EXAMGUARDIAN_DEVELOPMENT
        if(DevelopmentViewModel::current) DevelopmentViewModel::current->started(m_sessionId);
#endif
        m_attemptToken = session["attemptToken"].toString();
        m_evidenceToken = session["evidenceToken"].toString();
        if (m_sessionId <= 0 || m_attemptToken.isEmpty() || m_evidenceToken.isEmpty()) { emit examLoadFailed("Сервер не підтвердив спробу."); reply->deleteLater(); return; }
        m_examStartTime = QDateTime::fromString(session["startTime"].toString(), Qt::ISODateWithMs);
        const auto serverTime = QDateTime::fromString(session["serverTimeUtc"].toString(), Qt::ISODateWithMs);
        const auto deadline = QDateTime::fromString(session["deadlineUtc"].toString(), Qt::ISODateWithMs);
        m_serverElapsed = m_examStartTime.msecsTo(serverTime)/1000.0;
        m_sessionClock.start();
        m_durationSeconds = qMax(0, int(serverTime.secsTo(deadline)));
        m_savedAnswers = session["savedAnswers"].toObject().toVariantMap();
        const auto directory = QStandardPaths::writableLocation(QStandardPaths::AppLocalDataLocation)+"/"+QString::fromLatin1(QCryptographicHash::hash(clientServerUrl().toUtf8(),QCryptographicHash::Sha256).toHex().left(16))+"/"+session["studentId"].toString();
        QDir().mkpath(directory); m_answersPath=directory+QString("/answers_%1.json").arg(m_sessionId);
        QFile pending(m_answersPath);
        if (m_durationSeconds>0 && pending.open(QIODevice::ReadOnly)) m_savedAnswers=QJsonDocument::fromJson(pending.readAll()).object().toVariantMap();
        emit sessionIdChanged(m_sessionId);
        emit attemptReady(m_sessionId,m_attemptToken,m_evidenceToken);
        QNetworkRequest detail(QUrl(clientServerUrl() + QString("/api/Exams/%1").arg(m_examId)));
        detail.setRawHeader("X-Session-Token", m_attemptToken.toUtf8());
        detail.setRawHeader("Authorization", ("Bearer " + m_token).toUtf8());
        auto *detailReply = m_net.get(detail);
        connect(detailReply, &QNetworkReply::finished, this, [this,detailReply]() {
            if (detailReply->error() != QNetworkReply::NoError) { emit examLoadFailed(detailReply->errorString()); detailReply->deleteLater(); return; }
            auto exam = QJsonDocument::fromJson(detailReply->readAll()).object();
            m_examTitle = exam["title"].toString();
            m_questions = exam["questions"].toArray().toVariantList();
            if (m_questions.isEmpty()) emit examLoadFailed("В іспиті немає доступних запитань.");
            else emit examLoaded();
            detailReply->deleteLater();
        });
        reply->deleteLater();
    });
}
void ExamViewModel::saveAnswers(const QVariantMap &answers) {
    m_latestAnswers = answers;
    if (!m_answersPath.isEmpty() && !m_submitted) {
        QSaveFile local(m_answersPath);
        if (!local.open(QIODevice::WriteOnly) || local.write(QJsonDocument(QJsonObject::fromVariantMap(answers)).toJson())<0 || !local.commit()) { emit answersSaved(false); return; }
    }
    if (m_sessionId <= 0 || m_saveInFlight || m_submitted) return;
    m_saveInFlight = true;
    QNetworkRequest req(QUrl(clientServerUrl() + QString("/api/ExamSessions/%1/answers").arg(m_sessionId)));
    req.setHeader(QNetworkRequest::ContentTypeHeader, "application/json");
    req.setRawHeader("X-Session-Token", m_attemptToken.toUtf8());
    req.setRawHeader("Authorization", ("Bearer " + m_token).toUtf8());
    auto *reply = m_net.put(req, QJsonDocument(QJsonObject::fromVariantMap(answers)).toJson());
    connect(reply, &QNetworkReply::finished, this, [this,reply,answers]() {
        m_saveInFlight = false; const bool success = reply->error() == QNetworkReply::NoError;
        if (success && answers == m_latestAnswers) QFile::remove(m_answersPath);
        emit answersSaved(success); reply->deleteLater();
        if (!m_submitted && success && answers != m_latestAnswers) saveAnswers(m_latestAnswers);
    });
}
void ExamViewModel::heartbeat() {
    if (m_sessionId <= 0) return;
    QNetworkRequest req(QUrl(clientServerUrl() + QString("/api/ExamSessions/%1/heartbeat").arg(m_sessionId)));
    req.setRawHeader("X-Session-Token", m_attemptToken.toUtf8());
    req.setRawHeader("Authorization", ("Bearer " + m_token).toUtf8());
    auto *reply = m_net.post(req, QByteArray());
    connect(reply, &QNetworkReply::finished, this, [this,reply]() {
        if (reply->error() == QNetworkReply::NoError && QJsonDocument::fromJson(reply->readAll()).object()["isFinished"].toBool()) emit sessionTerminated();
        reply->deleteLater();
    });
}

void ExamViewModel::submitAnswers(const QVariantMap &answers) {
    if (m_token.isEmpty()) {
        emit submitFinished(false, "Немає авторизації. Відповіді не передано.");
        return;
    }

    QUrl url(clientServerUrl() + QString("/api/Exams/%1/submit").arg(m_examId));
    QNetworkRequest request(url);
    request.setHeader(QNetworkRequest::ContentTypeHeader, "application/json");
    request.setRawHeader("X-Session-Token", m_attemptToken.toUtf8());
    request.setRawHeader("Authorization", ("Bearer " + m_token).toUtf8());

    QJsonObject answersJson;
    for (auto it = answers.begin(); it != answers.end(); ++it) {
        answersJson.insert(it.key(), it.value().toInt());
    }

    QJsonObject body;
    body["sessionId"] = m_sessionId;
    body["answers"] = answersJson;

    QNetworkReply *reply = m_net.post(request, QJsonDocument(body).toJson());
    connect(reply, &QNetworkReply::finished, this, [this, reply]() {
        if (reply->error() == QNetworkReply::NoError) {
            QJsonDocument doc = QJsonDocument::fromJson(reply->readAll());
            if (doc.object()["session"].toObject()["scoreCalculated"].isBool() && !doc.object()["session"].toObject()["scoreCalculated"].toBool()) {
                m_submitted=true;
                emit submitFinished(true,"Спробу завершено без розрахованого бала. Результат потребує розбору викладача.");
                reply->deleteLater(); return;
            }
            int score = doc.object()["score"].toInt();
            int total = m_questions.size();
            m_submitted = true;
            QFile::remove(m_answersPath);
            emit examScoreReceived(score, total);
            emit submitFinished(true, QString("Відповіді прийнято. Результат: %1 / 100. Висновок прокторингу надасть викладач.").arg(score));
        } else {
            emit submitFinished(false, reply->errorString());
        }
        reply->deleteLater();
    });
}

void ExamViewModel::reportEnvironmentObservation(const QString &type, const QString &details) {
    if (m_sessionId <= 0 || m_submitted) return;
    const double seconds=elapsedSeconds(); const auto key=type+":"+details;
    if (m_lastObservation.contains(key) && seconds-m_lastObservation[key]<15) return;
    m_lastObservation[key]=seconds;
    emit observationCaptured(m_sessionId,type,int(seconds),QString::fromUtf8(QJsonDocument(QJsonObject{{"details",details},{"source","client"}}).toJson(QJsonDocument::Compact)));
}
void ExamViewModel::reportProhibitedProcess(const QString &processName) {
    reportEnvironmentObservation("prohibited_app",processName);
    emit prohibitedProcessAlert(processName);
}
void ExamViewModel::reportFocusLoss() { reportEnvironmentObservation("app_switched","Application lost focus"); }
