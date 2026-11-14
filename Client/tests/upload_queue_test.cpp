#include <QtTest>
#include <QTcpServer>
#include <QTcpSocket>
#include <QJsonDocument>
#include <QJsonObject>
#include <QCryptographicHash>
#include <QFile>
#include <QFileInfo>
#include <QStandardPaths>
#include <QRegularExpression>
#include <QUrl>
#include <QUuid>
#include "core_api_client.h"

class UploadQueueTest : public QObject {
    Q_OBJECT
private slots:
    void wrongAcknowledgementPreservesFileAndRestartRecovers() {
        QCoreApplication::setApplicationName("QueueContract-"+QUuid::createUuid().toString(QUuid::WithoutBraces));
        QStandardPaths::setTestModeEnabled(true);
        QTcpServer server;
        QVERIFY(server.listen(QHostAddress::LocalHost));
        qputenv("EXAMGUARDIAN_URL",("http://127.0.0.1:"+QString::number(server.serverPort())).toUtf8());
        const QByteArray media="test-media-content";
        const QString hash=QString::fromLatin1(QCryptographicHash::hash(media,QCryptographicHash::Sha256).toHex().toUpper());
        int requests=0; bool correctAck=false; bool sawEvidenceToken=false;
        connect(&server,&QTcpServer::newConnection,this,[&] {
            auto *socket=server.nextPendingConnection();
            connect(socket,&QTcpSocket::readyRead,socket,[&,socket] {
                auto bytes=socket->property("received").toByteArray()+socket->readAll();
                socket->setProperty("received",bytes);
                const int end=bytes.indexOf("\r\n\r\n"); if (end<0) return;
                const auto header=bytes.left(end);
                const QRegularExpression length("Content-Length: (\\d+)",QRegularExpression::CaseInsensitiveOption);
                const auto match=length.match(QString::fromLatin1(header));
                if (!match.hasMatch() || bytes.size()<end+4+match.captured(1).toInt()) return;
                ++requests;
                if(header.startsWith("POST /api/video/41/finalize ")) {
                    socket->write("HTTP/1.1 404 Not Found\r\nContent-Length: 2\r\nConnection: close\r\n\r\n{}");socket->disconnectFromHost();return;
                }
                sawEvidenceToken=header.toLower().contains("x-evidence-token: delivery-capability");
                const auto json=QJsonDocument(QJsonObject{{"sessionId",42},{"chunkIndex",1},{"stream","camera"},{"sha256",correctAck ? hash : "incorrect"}}).toJson(QJsonDocument::Compact);
                socket->write("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: "+QByteArray::number(json.size())+"\r\nConnection: close\r\n\r\n"+json);
                socket->disconnectFromHost();
            });
            connect(socket,&QTcpSocket::disconnected,socket,&QObject::deleteLater);
        });
        const auto claims=QJsonDocument(QJsonObject{{"sub",QUuid::createUuid().toString(QUuid::WithoutBraces)}}).toJson(QJsonDocument::Compact).toBase64(QByteArray::Base64UrlEncoding|QByteArray::OmitTrailingEquals);
        const QString token="header."+QString::fromLatin1(claims)+".signature";
        QString path;
        {
            CoreApiClient client;
            client.setAuthToken(token);
            client.finalizeEvidence(40,0,0); // another orphan without a capability
            client.setAttemptTokens(41,"old-attempt","old-delivery");
            client.finalizeEvidence(41,0,0); // deleted previous attempt returns 404
            client.setAttemptTokens(42,"attempt-capability","delivery-capability");
            path=QUrl(client.beginRecording(42,"camera",1,0)).toLocalFile();
            QVERIFY(!path.isEmpty());
            QCOMPARE(client.pendingUploads(),3); // intent is durable before capture begins
            QFile file(path); QVERIFY(file.open(QIODevice::WriteOnly)); QCOMPARE(file.write(media),media.size()); file.close();
            QSignalSpy failure(&client,&CoreApiClient::videoUploadFailed);
            client.uploadVideoChunk("42",1,path,"camera",0,1);
            QTRY_VERIFY_WITH_TIMEOUT(failure.count()>=2 && requests>=2,5000);
            QVERIFY(QFile::exists(path)); QCOMPARE(client.pendingUploads(),3);
            client.setAuthToken(token); // refresh must not reload/reclassify a running recording
            QVERIFY(QFile::exists(path));
        }
        correctAck=true;
        {
            CoreApiClient restarted;
            QSignalSpy delivered(&restarted,&CoreApiClient::videoUploadedSuccessfully);
            restarted.setAuthToken(token); // reloads both disk queue and delivery capability
            QTRY_COMPARE_WITH_TIMEOUT(delivered.count(),1,5000);
            QVERIFY(!QFile::exists(path)); QCOMPARE(restarted.pendingUploads(),2);
        }
        QVERIFY(sawEvidenceToken); QVERIFY(requests>=2);
        QFile::remove(QFileInfo(path).absolutePath()+"/uploads.json");
        QFile::remove(QFileInfo(path).absolutePath()+"/evidence-tokens.json");
    }
};

QTEST_GUILESS_MAIN(UploadQueueTest)
#include "upload_queue_test.moc"
