#include <QGuiApplication>
#include <QDebug>
#include <QStandardPaths>
#include <QJsonDocument>
#include <QJsonObject>
#include <QTimer>
#include <QUuid>
#include "exam_viewmodel.h"
#include "client_config.h"

// Real Qt -> Core wire contract, without creating QML/capture sessions or installing hooks.
int main(int argc, char **argv) {
    QGuiApplication app(argc,argv);
    QCoreApplication::setOrganizationName("ExamGuardianTests");
    QCoreApplication::setApplicationName("NativeContract-"+QUuid::createUuid().toString(QUuid::WithoutBraces));
    QStandardPaths::setTestModeEnabled(true);
    const auto url=qEnvironmentVariable("EXAMGUARDIAN_TEST_URL");
    if ((url!="http://127.0.0.1:55111" && url!="http://host.docker.internal:55111") || clientServerUrl()!=url) return 2;
    const auto login=qEnvironmentVariable("EXAMGUARDIAN_TEST_STUDENT_LOGIN");
    if (!login.startsWith("test_")) return 2;
    const auto answers=QJsonDocument::fromJson(qgetenv("EXAMGUARDIAN_TEST_ANSWERS")).object().toVariantMap();
    const int expectedDuration=qEnvironmentVariableIntValue("EXAMGUARDIAN_TEST_DURATION");
    const int expectedScore=qEnvironmentVariableIntValue("EXAMGUARDIAN_TEST_SCORE");
    if (answers.isEmpty() || expectedDuration<=0) return 2;
    ExamViewModel vm;
    int score=-1;
    QObject::connect(&vm,&ExamViewModel::loginFailed,&app,[&](const QString &message) { qWarning().noquote()<<message; app.exit(1); });
    QObject::connect(&vm,&ExamViewModel::examLoadFailed,&app,[&](const QString &message) { qWarning().noquote()<<message; app.exit(1); });
    QObject::connect(&vm,&ExamViewModel::loginSucceeded,&app,[&] {
        vm.selectExam(qEnvironmentVariableIntValue("EXAMGUARDIAN_TEST_EXAM_ID"));
        vm.setAdmissionToken(qEnvironmentVariable("EXAMGUARDIAN_TEST_ADMISSION"));
        vm.loadExam();
    });
    QObject::connect(&vm,&ExamViewModel::examLoaded,&app,[&] {
        // .NET emits seven fractional digits. The client must use server time and parse the actual contract.
        if (vm.sessionId()<=0 || vm.questions().isEmpty() || vm.durationSeconds()<=expectedDuration-5 || vm.durationSeconds()>expectedDuration) { app.exit(1); return; }
        vm.saveAnswers(answers);
    });
    QObject::connect(&vm,&ExamViewModel::answersSaved,&app,[&](bool success) {
        if (!success) { app.exit(1); return; }
        vm.submitAnswers(answers);
    });
    QObject::connect(&vm,&ExamViewModel::examScoreReceived,&app,[&](int value,int) { score=value; });
    QObject::connect(&vm,&ExamViewModel::submitFinished,&app,[&](bool success,const QString &) { app.exit(success && score==expectedScore ? 0 : 1); });
    QTimer::singleShot(30000,&app,[&] { qWarning()<<"Native Core contract timed out"; app.exit(1); });
    vm.login(login,qEnvironmentVariable("EXAMGUARDIAN_TEST_STUDENT_PASSWORD"));
    return app.exec();
}
