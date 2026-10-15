#include <QUrl>
#include <QMediaPlayer>
#include <QFileInfo>
#include <QStandardPaths>
#include <QDir>
#include <QSaveFile>
#include <QUuid>
#include <QCryptographicHash>
#include "client_config.h"
#include "core_api_client.h"
#include <QHttpMultiPart>
#include <QFile>
#include <QNetworkReply>
#include <QJsonObject>
#include <QJsonDocument>
#include <QDebug>

CoreApiClient::CoreApiClient(QObject *parent) : QObject(parent) {
    m_manager = new QNetworkAccessManager(this);
    m_manager->setTransferTimeout(120000);
    m_retryTimer.setSingleShot(true);
    connect(&m_retryTimer, &QTimer::timeout, this, [this] { pumpUploads(); });
}

void CoreApiClient::reportViolation(int sessionId, const QString &violationType, double confidence, int relativeTimestampSeconds, const QString &metadata) {
    QJsonObject json;
    json["examSessionId"] = sessionId;
    json["violationType"] = violationType;
    json["confidence"] = confidence;
    json["relativeTimestampSeconds"] = relativeTimestampSeconds;
    json["metadata"] = metadata;

    json["eventId"] = QUuid::createUuid().toString(QUuid::WithoutBraces);
    m_queue.append(QJsonObject{{"kind","event"},{"sessionId",QString::number(sessionId)},{"payload",json}});
    if (persistQueue()) pumpUploads();
}


void CoreApiClient::setAuthToken(const QString &token) {
    if (m_uploadInFlight) { m_authToken = token; return; }
    m_authToken = token;
    auto parts = token.split('.');
    if (parts.size() != 3) return;
    const auto claims = QJsonDocument::fromJson(QByteArray::fromBase64(parts[1].toLatin1(), QByteArray::Base64UrlEncoding)).object();
    const auto userId = claims["sub"].toString();
    if (QUuid(userId).isNull()) return;
    const auto directory = QStandardPaths::writableLocation(QStandardPaths::AppLocalDataLocation) + "/" + QString::fromLatin1(QCryptographicHash::hash(clientServerUrl().toUtf8(),QCryptographicHash::Sha256).toHex().left(16)) + "/" + userId;
    QDir().mkpath(directory);
    const auto queuePath = directory + "/uploads.json";
    if (queuePath == m_queuePath) { pumpUploads(); return; }
    m_queuePath = queuePath;
    QFile tokenFile(directory + "/evidence-tokens.json");
    if (tokenFile.open(QIODevice::ReadOnly)) m_evidenceTokens = QJsonDocument::fromJson(tokenFile.readAll()).object();
    QFile manifest(m_queuePath);
    if (manifest.open(QIODevice::ReadOnly)) m_queue = QJsonDocument::fromJson(manifest.readAll()).array();
    emit pendingUploadsChanged(m_queue.size());
    for (const auto &value : m_queue) {
        const auto entry=value.toObject();
        if (!entry.contains("ready") || entry["ready"].toBool() || entry["kind"].toString()=="finalize") continue;
        auto *player=new QMediaPlayer(this);
        connect(player,&QMediaPlayer::durationChanged,this,[this,player,entry](qint64 duration) {
            if (duration <= 0) return;
            uploadVideoChunk(entry["sessionId"].toString(),entry["chunkIndex"].toInt(),entry["path"].toString(),entry["stream"].toString(),entry["startOffsetSeconds"].toDouble(),duration/1000.0);
            player->deleteLater();
        });
        connect(player,&QMediaPlayer::errorOccurred,this,[this,player,entry](QMediaPlayer::Error,const QString &) { markUnrecoverable(entry["path"].toString(),"Незавершений запис потребує перевірки. Його збережено на цьому ПК."); player->deleteLater(); });
        player->setSource(QUrl::fromLocalFile(entry["path"].toString()));
    }
    pumpUploads();
}
void CoreApiClient::setAttemptTokens(int sessionId, const QString &attempt, const QString &evidence) {
    m_attemptToken=attempt;
    m_evidenceTokens[QString::number(sessionId)]=evidence;
    QSaveFile file(QFileInfo(m_queuePath).absolutePath()+"/evidence-tokens.json");
    if (!file.open(QIODevice::WriteOnly)) { emit videoUploadFailed("Не вдалося зберегти дозвіл доставки запису."); return; }
    file.setPermissions(QFileDevice::ReadOwner|QFileDevice::WriteOwner);
    if (file.write(QJsonDocument(m_evidenceTokens).toJson())<0 || !file.commit()) emit videoUploadFailed("Не вдалося зберегти дозвіл доставки запису.");
    pumpUploads();
}
bool CoreApiClient::persistQueue() {
    if (m_queuePath.isEmpty()) return false;
    QSaveFile file(m_queuePath);
    if (!file.open(QIODevice::WriteOnly) || file.write(QJsonDocument(m_queue).toJson()) < 0 || !file.commit())
        { emit videoUploadFailed("Не вдалося зберегти чергу відео на диску."); return false; }
    emit pendingUploadsChanged(m_queue.size()); return true;
}
void CoreApiClient::uploadVideoChunk(const QString &sessionId, int chunkIndex, const QString &filePath, const QString &stream, double startOffsetSeconds, double durationSeconds) {
    if (sessionId.toInt() <= 0 || chunkIndex <= 0 || m_queuePath.isEmpty()) {
        emit videoUploadFailed("Немає підтвердженої сесії для запису."); return;
    }
    if (durationSeconds <= 0) { markUnrecoverable(filePath,"Запис порожній або незавершений. Потрібна перевірка матеріалів."); return; }
    for (int i=0; i<m_queue.size(); ++i) {
        auto entry = m_queue[i].toObject();
        if (entry["path"].toString() != filePath) continue;
        entry["ready"] = true; entry["durationSeconds"] = durationSeconds; entry["startOffsetSeconds"] = startOffsetSeconds; m_queue[i] = entry;
        if (persistQueue()) pumpUploads(); return;
    }
    m_queue.append(QJsonObject{{"sessionId",sessionId},{"chunkIndex",chunkIndex},{"path",filePath},{"ready",true},{"stream",stream},{"startOffsetSeconds",startOffsetSeconds},{"durationSeconds",durationSeconds}});
    if (persistQueue()) pumpUploads();
}
QString CoreApiClient::beginRecording(int sessionId, const QString &stream, int index, double offset) {
    if (m_queuePath.isEmpty() || sessionId <= 0) return {};
    const auto path = QFileInfo(m_queuePath).absolutePath() + QString("/%1_%2_%3_%4.mp4").arg(stream).arg(sessionId).arg(index).arg(QUuid::createUuid().toString(QUuid::WithoutBraces));
    m_queue.append(QJsonObject{{"sessionId",QString::number(sessionId)},{"chunkIndex",index},{"path",path},{"ready",false},{"stream",stream},{"startOffsetSeconds",offset}});
    if (!persistQueue()) { m_queue.removeLast(); return {}; }
    return QUrl::fromLocalFile(path).toString();
}
void CoreApiClient::finalizeEvidence(int sessionId, int cameraLast, int screenLast) {
    m_queue.append(QJsonObject{{"kind","finalize"},{"sessionId",QString::number(sessionId)},{"camera",cameraLast},{"screen",screenLast}});
    if (persistQueue()) pumpUploads();
}
void CoreApiClient::fetchNextChunkIndex(int sessionId) {
    QNetworkRequest req(QUrl(clientServerUrl() + QString("/api/video/%1/chunks").arg(sessionId)));
    req.setRawHeader("Authorization", ("Bearer " + m_authToken).toUtf8());
    req.setRawHeader("X-Evidence-Token", m_evidenceTokens[QString::number(sessionId)].toString().toUtf8());
    auto *reply = m_manager->get(req);
    connect(reply, &QNetworkReply::finished, this, [this,reply,sessionId]() {
        if (reply->error() != QNetworkReply::NoError) { emit videoUploadFailed("Не вдалося отримати стан відеозапису."); reply->deleteLater(); return; }
        int last = 0;
        for (const auto &value : QJsonDocument::fromJson(reply->readAll()).array()) last = qMax(last, value.toObject()["chunkIndex"].toInt());
        for (const auto &value : m_queue) { const auto entry = value.toObject(); if (entry["sessionId"].toString().toInt() == sessionId) last = qMax(last, entry["chunkIndex"].toInt()); }
        emit recordingReady(last + 1);
        reply->deleteLater();
    });
}
void CoreApiClient::pumpUploads() {
    if (m_uploadInFlight || m_queue.isEmpty() || m_authToken.isEmpty()) return;
    int queueIndex=0;
    for(;queueIndex<m_queue.size();++queueIndex) {
        const auto candidate=m_queue[queueIndex].toObject();
        if(candidate["unrecoverable"].toBool() || candidate["blocked"].toBool()
            || m_evidenceTokens[candidate["sessionId"].toString()].toString().isEmpty()
            || (candidate.contains("ready") && !candidate["ready"].toBool())) continue;
        bool waiting=false;
        if(candidate["kind"].toString()=="finalize") {
            for(int i=0;i<queueIndex;++i) {
                const auto prior=m_queue[i].toObject();
                if(prior["sessionId"]==candidate["sessionId"] && !prior["unrecoverable"].toBool() && !prior["blocked"].toBool()) {waiting=true;break;}
            }
        }
        if(!waiting)break;
    }
    if(queueIndex==m_queue.size())return;
    const auto entry = m_queue[queueIndex].toObject();
    const auto evidenceToken = m_evidenceTokens[entry["sessionId"].toString()].toString().toUtf8();
    if (evidenceToken.isEmpty()) return;
    if (entry["kind"].toString() == "event") {
        QNetworkRequest req(QUrl(clientServerUrl()+"/api/Violations/report"));
        req.setHeader(QNetworkRequest::ContentTypeHeader,"application/json");
        req.setRawHeader("Authorization",("Bearer "+m_authToken).toUtf8());
        req.setRawHeader("X-Evidence-Token",evidenceToken);
        m_uploadInFlight=true;
        auto *reply=m_manager->post(req,QJsonDocument(entry["payload"].toObject()).toJson());
        connect(reply,&QNetworkReply::finished,this,[this,reply,entry,queueIndex] {
            m_uploadInFlight=false;
            const auto ack=QJsonDocument::fromJson(reply->readAll()).object();
            if (reply->error()==QNetworkReply::NoError && ack["eventId"].toString()==entry["payload"].toObject()["eventId"].toString()) {
                m_queue.removeAt(queueIndex); if (!persistQueue()) m_queue.insert(queueIndex,entry);
                else QTimer::singleShot(0,this,[this]{pumpUploads();});
            } else {
                const int status=reply->attribute(QNetworkRequest::HttpStatusCodeAttribute).toInt();
                emit videoUploadFailed("Спостереження збережено локально; доставка не підтверджена.");
                if(status==400 || status==403 || status==404 || status==409) {auto blocked=entry;blocked["blocked"]=true;m_queue[queueIndex]=blocked;persistQueue();QTimer::singleShot(0,this,[this]{pumpUploads();});}
                else if(status!=401)m_retryTimer.start(15000);
            }
            reply->deleteLater();
        }); return;
    }
    if (entry["kind"].toString() == "finalize") {
        QNetworkRequest req(QUrl(clientServerUrl() + QString("/api/video/%1/finalize").arg(entry["sessionId"].toString())));
        req.setRawHeader("Authorization", ("Bearer " + m_authToken).toUtf8()); req.setHeader(QNetworkRequest::ContentTypeHeader,"application/json");
        req.setRawHeader("X-Evidence-Token", evidenceToken);
        m_uploadInFlight=true;
        auto *reply = m_manager->post(req,QJsonDocument(QJsonObject{{"camera",entry["camera"]},{"screen",entry["screen"]}}).toJson());
        connect(reply,&QNetworkReply::finished,this,[this,reply,entry,queueIndex] {
            m_uploadInFlight=false;
            if (reply->error()==QNetworkReply::NoError) { const auto ack=QJsonDocument::fromJson(reply->readAll()).object(); m_queue.removeAt(queueIndex); if (!persistQueue()) m_queue.insert(queueIndex,entry); else { emit evidenceFinalized(ack["status"].toString()); QTimer::singleShot(0,this,[this]{pumpUploads();}); } }
            else {
                const int status=reply->attribute(QNetworkRequest::HttpStatusCodeAttribute).toInt();
                emit videoUploadFailed("Стан доставки доказів не підтверджено (HTTP "+QString::number(status)+").");
                if(status==400 || status==403 || status==404 || status==409) {auto blocked=entry;blocked["blocked"]=true;m_queue[queueIndex]=blocked;persistQueue();QTimer::singleShot(0,this,[this]{pumpUploads();});}
                else if(status!=401)m_retryTimer.start(15000);
            }
            reply->deleteLater();
        }); return;
    }
    if (entry.contains("ready") && !entry["ready"].toBool()) return;
    const auto filePath = entry["path"].toString();
    auto *file = new QFile(filePath);
    if (!file->open(QIODevice::ReadOnly)) { delete file; markUnrecoverable(filePath,"Запис із черги недоступний. Потрібна перевірка матеріалів."); return; }
    QCryptographicHash digest(QCryptographicHash::Sha256);
    digest.addData(file); const auto expectedHash = digest.result().toHex().toUpper(); file->seek(0);
    auto *multiPart = new QHttpMultiPart(QHttpMultiPart::FormDataType);
    for (const auto &name : {QString("sessionId"), QString("chunkIndex"), QString("stream"), QString("startOffsetSeconds"), QString("durationSeconds")}) {
        QHttpPart part; part.setHeader(QNetworkRequest::ContentDispositionHeader, "form-data; name=\"" + name + "\"");
        part.setBody(entry[name].toVariant().toString().toUtf8()); multiPart->append(part);
    }
    QHttpPart video; video.setHeader(QNetworkRequest::ContentTypeHeader, "video/mp4");
    video.setHeader(QNetworkRequest::ContentDispositionHeader, "form-data; name=\"file\"; filename=\"chunk.mp4\"");
    video.setBodyDevice(file); file->setParent(multiPart); multiPart->append(video);
    QNetworkRequest req(QUrl(clientServerUrl() + "/api/video/upload-chunk"));
    req.setRawHeader("Authorization", ("Bearer " + m_authToken).toUtf8());
    req.setRawHeader("X-Evidence-Token", evidenceToken);
    m_uploadInFlight = true;
    auto *reply = m_manager->post(req, multiPart); multiPart->setParent(reply);
    connect(reply, &QNetworkReply::finished, this, [this,reply,entry,filePath,expectedHash,file,queueIndex]() {
        m_uploadInFlight = false;
        const auto ack = QJsonDocument::fromJson(reply->readAll()).object();
        const bool confirmed = ack["stream"].toString() == entry["stream"].toString() && reply->error() == QNetworkReply::NoError && ack["sha256"].toString().toLatin1() == expectedHash && ack["chunkIndex"].toInt() == entry["chunkIndex"].toInt() && ack["sessionId"].toInt() == entry["sessionId"].toString().toInt();
        if (confirmed) {
            m_queue.removeAt(queueIndex);
            if (!persistQueue()) { m_queue.insert(queueIndex,entry); reply->deleteLater(); return; }
            file->close(); QFile::remove(filePath); m_retrySeconds = 1;
            emit videoUploadedSuccessfully(filePath);
            QTimer::singleShot(0, this, [this] { pumpUploads(); });
        } else {
            emit videoUploadFailed("Передачу відео не підтверджено. Файл збережено для повторної спроби.");
            const int status = reply->attribute(QNetworkRequest::HttpStatusCodeAttribute).toInt();
            // Authentication and immutable-content conflicts need attention, not an endless retry loop.
            if(status==400 || status==403 || status==404 || status==409) {
                auto blocked=entry;blocked["blocked"]=true;m_queue[queueIndex]=blocked;persistQueue();QTimer::singleShot(0,this,[this]{pumpUploads();});
            } else if (status != 401) {
                m_retryTimer.start(m_retrySeconds * 1000); m_retrySeconds = qMin(60, m_retrySeconds * 2);
            }
        }
        reply->deleteLater();
    });
}

void CoreApiClient::markUnrecoverable(const QString &path, const QString &message) {
    for (int i=0;i<m_queue.size();++i) { auto entry=m_queue[i].toObject(); if (entry["path"].toString()==path) { entry["unrecoverable"]=true; m_queue[i]=entry; break; } }
    persistQueue(); emit videoUploadFailed(message); QTimer::singleShot(0,this,[this]{pumpUploads();});
}
