#include "exam_viewmodel.h"
#include <QNetworkRequest>
#include <QNetworkReply>
#include <QJsonDocument>
#include <QJsonArray>
#include <QJsonObject>
#include <QDebug>

ExamViewModel::ExamViewModel(QObject *parent) : QObject(parent) {
    connect(&m_auth, &AuthService::loginSucceeded,
            this, [this](const UserCredentials &creds) {
                m_login = creds.login;
                m_role  = creds.role;
                m_token = creds.accessToken;
                emit userChanged();
                emit loginSucceeded(m_token);
            });

    connect(&m_auth, &AuthService::loginFailed,
            this, &ExamViewModel::loginFailed);
}

void ExamViewModel::setKeyBlock(bool enable) {
    m_inputBlocker.setKeyBlock(enable);
}

void ExamViewModel::login(const QString &login, const QString &password) {
    m_auth.login(login, password, m_baseUrl);
}

void ExamViewModel::setupFallbackExam() {
    m_examTitle = "Тестовий іспит: Комп'ютерні мережі та безпека";
    m_durationSeconds = 1800;
    m_questions.clear();

    QVariantList q1Options;
    q1Options.append(QVariantMap{{"text", "Application layer"}, {"orderIndex", 0}});
    q1Options.append(QVariantMap{{"text", "Transport layer"}, {"orderIndex", 1}});
    q1Options.append(QVariantMap{{"text", "Network layer"}, {"orderIndex", 2}});
    q1Options.append(QVariantMap{{"text", "Data Link layer"}, {"orderIndex", 3}});

    m_questions.append(QVariantMap{
        {"id", 1},
        {"text", "На якому рівні моделі OSI працює протокол TLS/SSL?"},
        {"options", q1Options}
    });

    QVariantList q2Options;
    q2Options.append(QVariantMap{{"text", "Симетричне шифрування"}, {"orderIndex", 0}});
    q2Options.append(QVariantMap{{"text", "Асиметричне шифрування"}, {"orderIndex", 1}});
    q2Options.append(QVariantMap{{"text", "Одностороннє гешування"}, {"orderIndex", 2}});
    q2Options.append(QVariantMap{{"text", "Стеганографія"}, {"orderIndex", 3}});

    m_questions.append(QVariantMap{
        {"id", 2},
        {"text", "Який тип алгоритму використовує алгоритм RSA?"},
        {"options", q2Options}
    });

    emit examLoaded();
}

void ExamViewModel::loadExam() {
    if (m_token.isEmpty()) {
        setupFallbackExam();
        return;
    }

    QUrl url(m_baseUrl + "/api/Exams");
    QNetworkRequest request(url);
    request.setRawHeader("Authorization", ("Bearer " + m_token).toUtf8());

    QNetworkReply *reply = m_net.get(request);
    connect(reply, &QNetworkReply::finished, this, [this, reply]() {
        if (reply->error() != QNetworkReply::NoError) {
            qDebug() << "[ExamViewModel] Failed to fetch exams:" << reply->errorString();
            setupFallbackExam();
            reply->deleteLater();
            return;
        }

        QJsonDocument doc = QJsonDocument::fromJson(reply->readAll());
        QJsonArray exams = doc.array();
        if (exams.isEmpty()) {
            setupFallbackExam();
            reply->deleteLater();
            return;
        }

        QJsonObject firstExam = exams.first().toObject();
        int examId = firstExam["id"].toInt();
        m_examId = examId;

        QUrl detailUrl(m_baseUrl + QString("/api/Exams/%1").arg(examId));
        QNetworkRequest detailReq(detailUrl);
        detailReq.setRawHeader("Authorization", ("Bearer " + m_token).toUtf8());

        QNetworkReply *detailReply = m_net.get(detailReq);
        connect(detailReply, &QNetworkReply::finished, this, [this, detailReply]() {
            if (detailReply->error() == QNetworkReply::NoError) {
                QJsonDocument detailDoc = QJsonDocument::fromJson(detailReply->readAll());
                QJsonObject examObj = detailDoc.object();
                m_examTitle = examObj["title"].toString("Іспит");
                int durationMins = examObj["durationMinutes"].toInt(60);
                m_durationSeconds = durationMins * 60;

                QJsonArray questionsArray = examObj["questions"].toArray();
                m_questions.clear();

                for (const auto &qVal : questionsArray) {
                    QJsonObject qObj = qVal.toObject();
                    QVariantList optsList;
                    QJsonArray optsArr = qObj["options"].toArray();
                    for (const auto &oVal : optsArr) {
                        QJsonObject oObj = oVal.toObject();
                        optsList.append(QVariantMap{
                            {"text", oObj["text"].toString()},
                            {"orderIndex", oObj["orderIndex"].toInt()}
                        });
                    }

                    m_questions.append(QVariantMap{
                        {"id", qObj["id"].toInt()},
                        {"text", qObj["text"].toString()},
                        {"options", optsList}
                    });
                }

                if (m_questions.isEmpty()) {
                    setupFallbackExam();
                } else {
                    // Start or resume real session on backend
                    QUrl startUrl(m_baseUrl + "/api/ExamSessions/start");
                    QNetworkRequest startReq(startUrl);
                    startReq.setHeader(QNetworkRequest::ContentTypeHeader, "application/json");
                    startReq.setRawHeader("Authorization", ("Bearer " + m_token).toUtf8());

                    QJsonObject startBody;
                    startBody["examId"] = m_examId;
                    startBody["studentId"] = "00000000-0000-0000-0000-000000000000";

                    QNetworkReply *startReply = m_net.post(startReq, QJsonDocument(startBody).toJson());
                    connect(startReply, &QNetworkReply::finished, this, [this, startReply]() {
                        if (startReply->error() == QNetworkReply::NoError) {
                            QJsonDocument sDoc = QJsonDocument::fromJson(startReply->readAll());
                            int sid = sDoc.object()["id"].toInt();
                            if (sid > 0) {
                                m_sessionId = sid;
                                m_examStartTime = QDateTime::currentDateTime();
                                emit sessionIdChanged(m_sessionId);
                                qDebug() << "[ExamViewModel] Exam session active. ID:" << m_sessionId;
                            }
                        } else {
                            qDebug() << "[ExamViewModel] Could not start session:" << startReply->errorString();
                        }
                        emit examLoaded();
                        startReply->deleteLater();
                    });
                }
            } else {
                setupFallbackExam();
            }
            detailReply->deleteLater();
        });

        reply->deleteLater();
    });
}

void ExamViewModel::submitAnswers(const QVariantMap &answers) {
    if (m_token.isEmpty()) {
        emit submitFinished(true, "Збережено локально");
        return;
    }

    QUrl url(m_baseUrl + QString("/api/Exams/%1/submit").arg(m_examId));
    QNetworkRequest request(url);
    request.setHeader(QNetworkRequest::ContentTypeHeader, "application/json");
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
            int score = doc.object()["score"].toInt();
            int total = m_questions.size();
            emit examScoreReceived(score, total);
            emit submitFinished(true, QString("Іспит успішно складено! Ваш бал: %1 з %2.").arg(score).arg(total));
        } else {
            emit submitFinished(false, reply->errorString());
        }
        reply->deleteLater();
    });
}

void ExamViewModel::reportProhibitedProcess(const QString &processName) {
    if (m_sessionId <= 0) return;

    int elapsedSec = 0;
    if (m_examStartTime.isValid()) {
        elapsedSec = m_examStartTime.secsTo(QDateTime::currentDateTime());
    }

    QUrl url(m_baseUrl + "/api/Violations/report");
    QNetworkRequest request(url);
    request.setHeader(QNetworkRequest::ContentTypeHeader, "application/json");
    if (!m_token.isEmpty()) {
        request.setRawHeader("Authorization", ("Bearer " + m_token).toUtf8());
    }

    QJsonObject body;
    body["examSessionId"] = m_sessionId;
    body["violationType"] = "prohibited_app";
    body["confidence"] = 1.0;
    body["relativeTimestampSeconds"] = elapsedSec;
    body["metadata"] = QString("{\"app\":\"%1\"}").arg(processName);

    QNetworkReply *reply = m_net.post(request, QJsonDocument(body).toJson());
    connect(reply, &QNetworkReply::finished, this, [this, reply, processName]() {
        if (reply->error() == QNetworkReply::NoError) {
            qDebug() << "[ExamViewModel] Prohibited app violation logged on server:" << processName;
        } else {
            qDebug() << "[ExamViewModel] Error logging prohibited app:" << reply->errorString();
        }
        reply->deleteLater();
    });

    emit prohibitedProcessAlert(processName);
}

