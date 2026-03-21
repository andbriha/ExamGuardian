#include <QGuiApplication>
#include <QQmlApplicationEngine>
#include <QQmlContext>
#include "proctoring_module.h"

int main(int argc, char *argv[])
{
    QGuiApplication app(argc, argv);

    ProctoringModule proctoring;

    QQmlApplicationEngine engine;

    engine.rootContext()->setContextProperty("Proctoring", &proctoring);

    QObject::connect(
        &engine,
        &QQmlApplicationEngine::objectCreationFailed,
        &app,
        []() { QCoreApplication::exit(-1); },
        Qt::QueuedConnection);

    engine.loadFromModule("Client", "Main");

    return app.exec();
}
