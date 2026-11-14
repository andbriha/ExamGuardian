#include <QtTest>
#include <QQmlEngine>
#include <QQmlComponent>
#include <QQmlContext>
#include <QQuickItem>
#include <QQuickWindow>
#include <QScopedPointer>
#include <QFontDatabase>

class QuietExamTest : public QObject {
    Q_OBJECT
private slots:
    void screensRemainUsableWithoutProctoringAlerts() {
#ifdef Q_OS_WIN
        QFontDatabase::addApplicationFont(qEnvironmentVariable("SystemRoot")+"/Fonts/segoeui.ttf");
        QGuiApplication::setFont(QFont("Segoe UI"));
#endif
        QQmlEngine engine;
        QQmlComponent mock(&engine, QUrl::fromLocalFile(QStringLiteral(QUIET_QML_DIR)+"/../../tests/quiet_exam_model.qml"));
        QScopedPointer<QObject> model(mock.create());
        QVERIFY2(model, qPrintable(mock.errorString()));
        for (const auto *name : {"ExamVM", "ProcessVM", "mainWindow"}) engine.rootContext()->setContextProperty(name,model.data());
        QQuickWindow window; window.resize(1024,768); window.show();
        for (const auto *screen : {"ExamScreen", "AgreementScreen"}) {
            QQmlComponent component(&engine,QUrl::fromLocalFile(QStringLiteral(QUIET_QML_DIR)+"/"+screen+".qml"));
            QScopedPointer<QObject> object(component.create());
            QVERIFY2(object, qPrintable(component.errorString()));
            auto *item=qobject_cast<QQuickItem*>(object.data()); QVERIFY(item);
            item->setParentItem(window.contentItem()); item->setSize(QSizeF(1024,768));
            if (QString(screen)=="ExamScreen") {
                QVERIFY(QMetaObject::invokeMethod(model.data(),"examLoaded"));
                QVERIFY(QMetaObject::invokeMethod(model.data(),"prohibitedProcessAlert",Q_ARG(QString,QString("Discord"))));
                QVERIFY(!object->property("prohibitedAppAlert").isValid());
                QVERIFY(item->isEnabled());
            }
            QTest::qWait(250);
            const auto capture=window.grabWindow();
            QVERIFY(!capture.isNull());
            QVERIFY(capture.save(QString(screen)+"-quiet.png"));
        }
    }
};
QTEST_MAIN(QuietExamTest)
#include "quiet_exam_test.moc"
