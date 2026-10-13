#include <QGuiApplication>
#include <QQmlApplicationEngine>
#include <QQmlContext>
#include "process_viewmodel.h"
#include "media_viewmodel.h"
#include "exam_viewmodel.h"

int main(int argc, char *argv[])
{
    QGuiApplication app(argc, argv);

    QQmlApplicationEngine engine;


    ProcessViewModel processViewModel;
    engine.rootContext()->setContextProperty("ProcessVM", &processViewModel);
    MediaViewModel mediaViewModel;
    engine.rootContext()->setContextProperty("MediaVM", &mediaViewModel);
    ExamViewModel examViewModel;
    engine.rootContext()->setContextProperty("ExamVM", &examViewModel);

    QObject::connect(&examViewModel, &ExamViewModel::loginSucceeded,
                     &mediaViewModel, &MediaViewModel::setAuthToken);
    QObject::connect(&examViewModel, &ExamViewModel::sessionIdChanged,
                     &mediaViewModel, &MediaViewModel::setSessionId);

    QObject::connect(
        &engine,
        &QQmlApplicationEngine::objectCreationFailed,
        &app,
        []() { QCoreApplication::exit(-1); },
        Qt::QueuedConnection);

    engine.loadFromModule("Client", "Main");

    return app.exec();
}
