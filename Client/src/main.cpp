#include <QGuiApplication>
#include <QQmlApplicationEngine>
#include <QQmlContext>
#include <QFile>
#include <QJsonDocument>
#include <QJsonObject>
#include <cstdio>
#include "process_viewmodel.h"
#include "media_viewmodel.h"
#include "exam_viewmodel.h"
#ifdef EXAMGUARDIAN_DEVELOPMENT
#include "development_viewmodel.h"
#include <QQmlComponent>
#include <QQuickWindow>
#include <QQuickItem>
#endif

int main(int argc, char *argv[])
{
    QGuiApplication app(argc, argv);
    QCoreApplication::setOrganizationName("ExamGuardian");
    #ifdef EXAMGUARDIAN_DEVELOPMENT
    QCoreApplication::setApplicationName("Client.Dev");
    DevelopmentViewModel development;
#else
    QCoreApplication::setApplicationName("Client");
    if(app.arguments().contains("--verify-production")) {
        if(QFile::exists(":/development/qml/development/DevelopmentWindow.qml")) return 1;
        for(const auto &path : {":/qt/qml/Client/qml/Main.qml",":/qt/qml/Client/qml/screens/ProcessCheckScreen.qml"}) {
            QFile source(path); if(!source.open(QIODevice::ReadOnly)) return 1;
            const auto bytes=source.readAll(); if(bytes.contains("DevVM") || bytes.contains("bypass(")) return 1;
        }
        return 0;
    }
    if(app.arguments().contains("--development") || app.arguments().contains("--dev-tools")) return 2;
#endif

    if (app.arguments().contains("--check-virtualization")) {
        EnvironmentDetector detector;
        const bool blocked = detector.isVirtualMachine();
        const auto report = QJsonDocument(QJsonObject{{"blocked", blocked}, {"details", detector.getVmDetails()}}).toJson(QJsonDocument::Compact);
        std::printf("%s\n", report.constData());
        return 0;
    }

    // Destroy the QML tree before its context objects.
    ProcessViewModel processViewModel;
    MediaViewModel mediaViewModel;
    ExamViewModel examViewModel;
    QQmlApplicationEngine engine;
    engine.rootContext()->setContextProperty("ProcessVM", &processViewModel);
    engine.rootContext()->setContextProperty("MediaVM", &mediaViewModel);
    engine.rootContext()->setContextProperty("ExamVM", &examViewModel);

#ifdef EXAMGUARDIAN_DEVELOPMENT
    engine.rootContext()->setContextProperty("DevVM", &development);
    QObject::connect(&examViewModel,&ExamViewModel::authTokenChanged,&development,&DevelopmentViewModel::authenticate);
    QObject::connect(&examViewModel,&ExamViewModel::userChanged,&development,[&]{development.authenticate(examViewModel.authToken());});
    QObject::connect(&development,&DevelopmentViewModel::changed,&processViewModel,&ProcessViewModel::runFullScan);
#endif
    QObject::connect(&examViewModel, &ExamViewModel::authTokenChanged,
                     &mediaViewModel, &MediaViewModel::setAuthToken);
    QObject::connect(&mediaViewModel, &MediaViewModel::admissionReceived, &examViewModel, &ExamViewModel::setAdmissionToken);
    QObject::connect(&examViewModel, &ExamViewModel::observationCaptured, &mediaViewModel, &MediaViewModel::queueObservation);
    QObject::connect(&examViewModel, &ExamViewModel::attemptReady, &mediaViewModel, &MediaViewModel::setAttemptTokens);
    QObject::connect(&examViewModel, &ExamViewModel::sessionIdChanged,
                     &mediaViewModel, &MediaViewModel::setSessionId);

    QObject::connect(
        &engine,
        &QQmlApplicationEngine::objectCreationFailed,
        &app,
        []() { QCoreApplication::exit(-1); },
        Qt::QueuedConnection);

    engine.loadFromModule("Client", "Main");
#ifdef EXAMGUARDIAN_DEVELOPMENT
    if(!engine.rootObjects().isEmpty()) {
        auto *window=qobject_cast<QQuickWindow*>(engine.rootObjects().first());
        QQmlComponent tools(&engine,QUrl("qrc:/development/qml/development/DevelopmentWindow.qml"));
        auto *item=qobject_cast<QQuickItem*>(tools.create());
        if(!item || !window) {qWarning()<<tools.errors();return 1;}
        item->setParent(window->contentItem());item->setParentItem(window->contentItem());
    }
#endif
    if (app.arguments().contains("--smoke-test")) QTimer::singleShot(1000,&app,[&engine]{ QCoreApplication::exit(engine.rootObjects().isEmpty() ? 1 : 0); });

    return app.exec();
}
