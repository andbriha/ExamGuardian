#include <QGuiApplication>
#include <QQmlApplicationEngine>
#include <QQmlContext>
#include <QStandardPaths>
#include <QUrl>
#include "exam_viewmodel.h"
#include "client_config.h"

class CatalogMedia : public QObject {
    Q_OBJECT
public:
    Q_INVOKABLE void setExamId(int) {}
};

// A real catalog screen + HTTP viewmodel, without capture or OS restrictions.
// Test-only executable: authentication uses an explicitly supplied local fixture account.
int main(int argc, char **argv) {
    QGuiApplication app(argc, argv);
    QCoreApplication::setOrganizationName("ExamGuardianTests");
    QCoreApplication::setApplicationName("CatalogPreview");
    QStandardPaths::setTestModeEnabled(true);
    const QUrl origin(clientServerUrl());
    const auto login = qEnvironmentVariable("EXAMGUARDIAN_TEST_STUDENT_LOGIN");
    if (origin.scheme() != "http" || origin.host() != "127.0.0.1" || !login.startsWith("ui_test_")) return 2;
    ExamViewModel vm;
    CatalogMedia media;
    QQmlApplicationEngine engine;
    engine.rootContext()->setContextProperty("ExamVM", &vm);
    engine.rootContext()->setContextProperty("MediaVM", &media);
    QObject::connect(&vm, &ExamViewModel::loginFailed, &app, [&](const QString &message) { qWarning() << message; app.exit(1); });
    QObject::connect(&vm, &ExamViewModel::loginSucceeded, &app, [&] {
        engine.loadData(R"(import QtQuick
import QtQuick.Controls
ApplicationWindow {
    visible: true; width: 1024; height: 768; title: "ExamGuardian — Catalog UI check"
    ExamSelectionScreen { anchors.fill: parent; onExamSelected: console.log("Catalog selection confirmed") }
})", QUrl::fromLocalFile(QStringLiteral(CATALOG_SOURCE_DIR) + "/CatalogPreview.qml"));
        if (engine.rootObjects().isEmpty()) app.exit(1);
    });
    vm.login(login, qEnvironmentVariable("EXAMGUARDIAN_TEST_STUDENT_PASSWORD"));
    return app.exec();
}

#include "catalog_preview.moc"
