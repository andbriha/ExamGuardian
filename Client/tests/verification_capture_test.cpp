#include <QtTest>
#include <QVideoFrame>
#include "media_validator.h"
#include "pronunciation_player.h"
#include "../src/network/verification_audio_buffer.h"

class VerificationCaptureTest : public QObject {
    Q_OBJECT
private slots:
    void pronunciationSuspendsBeforePlaybackAndCancellationCannotResumeIt() {
        PronunciationPlayer player;
        QSignalSpy changes(&player, &PronunciationPlayer::activeChanged);
        player.play("../../wrong");
        QCOMPARE(changes.count(), 0);
        player.play("MA MI MU");
        QCOMPARE(changes.count(), 1);
        QCOMPARE(changes.first().at(0).toBool(), true);
        player.stop();
        QCOMPARE(changes.count(), 2);
        QCOMPARE(changes.last().at(0).toBool(), false);
        QTest::qWait(700);
        QCOMPARE(changes.count(), 2);
    }
    void audioKeepsOrderAcrossSlowRequestsAndResetsAfterOverflow() {
        VerificationAudioBuffer buffer;
        QVERIFY(buffer.append(QByteArray(32000, 'a')));
        QVERIFY(buffer.append(QByteArray(32000, 'b')));
        QVERIFY(buffer.append(QByteArray(32000, 'c')));
        QVERIFY(buffer.takeReset());
        QCOMPARE(buffer.take(), QByteArray(32000,'a') + QByteArray(32000,'b'));
        QCOMPARE(buffer.take(), QByteArray(32000,'c'));
        QVERIFY(buffer.empty());
        QVERIFY(!buffer.takeReset());
        QVERIFY(buffer.append(QByteArray(320000,'d')));
        QVERIFY(!buffer.append(QByteArray(32000,'e')));
        QVERIFY(buffer.takeReset());
        QCOMPARE(buffer.take(),QByteArray(32000,'e'));
        buffer.append(QByteArray(32000,'x')); buffer.clear();
        QVERIFY(buffer.empty()); QVERIFY(buffer.takeReset());
    }
    void phasesDoNotCreateExtraActions() {
        MediaValidator validator;
        QSignalSpy tasks(&validator,&MediaValidator::instructionReceived);
        QSignalSpy phases(&validator,&MediaValidator::verificationPhaseChanged);
        validator.onChallengeReceived("turn_right");
        QCOMPARE(tasks.count(),1);
        QCOMPARE(phases.last().at(0).toString(),QString("prepare"));
        validator.onVerificationFinished(false,"liveness","perform_action",.2);
        QCOMPARE(phases.last().at(0).toString(),QString("perform"));
        validator.onVerificationFinished(false,"liveness","confirm_live_face",.2);
        QCOMPARE(phases.last().at(0).toString(),QString("return"));
        QCOMPARE(tasks.count(),1);
    }
    void speechAndBriefQualityPauseKeepTheCurrentTask() {
        MediaValidator validator;
        QSignalSpy tasks(&validator,&MediaValidator::instructionReceived);
        QSignalSpy phases(&validator,&MediaValidator::verificationPhaseChanged);
        validator.onChallengeReceived(QString::fromUtf8("say_phrase:MA MI MU"));
        QVERIFY(tasks.last().at(0).toString().contains(QString::fromUtf8("MA MI MU")));
        validator.onVerificationFinished(false,"liveness","say_phrase",.2);
        QCOMPARE(phases.last().at(0).toString(),QString("perform"));
        const auto count = phases.count();
        validator.onVerificationFinished(false,"liveness","quality_paused",.2);
        QCOMPARE(phases.count(),count);
        QCOMPARE(tasks.count(),1);
    }
    void automaticallyCapturesAndStops() {
        MediaValidator validator;
        QVideoSink sink;
        validator.setVideoSink(&sink);
        QImage image(160, 120, QImage::Format_RGB32); image.fill(Qt::gray);
        sink.setVideoFrame(QVideoFrame(image));
        QSignalSpy frames(&validator, &MediaValidator::verifyIdentityAndLiveness);
        validator.onChallengeReceived("smile");
        QTRY_VERIFY_WITH_TIMEOUT(frames.count() > 0, 1500);
        QCOMPARE(frames.first().at(1).toString(), QString("smile"));
        validator.stopVerificationSession();
        const auto count = frames.count();
        QTest::qWait(550);
        QCOMPARE(frames.count(), count);
    }
    void noFramesBeforeChallengeOrAfterSuccess() {
        MediaValidator validator;
        QVideoSink sink;
        validator.setVideoSink(&sink);
        QImage image(160, 120, QImage::Format_RGB32); image.fill(Qt::gray);
        sink.setVideoFrame(QVideoFrame(image));
        QSignalSpy frames(&validator, &MediaValidator::verifyIdentityAndLiveness);
        validator.startVerificationSession();
        QTest::qWait(550);
        QCOMPARE(frames.count(), 0);
        validator.onChallengeReceived("nod");
        QTRY_VERIFY_WITH_TIMEOUT(frames.count() > 0, 1500);
        validator.onVerificationFinished(true, "ok", "Passed", .2);
        const auto count = frames.count();
        QTest::qWait(550);
        QCOMPARE(frames.count(), count);
    }
};
QTEST_GUILESS_MAIN(VerificationCaptureTest)
#include "verification_capture_test.moc"
