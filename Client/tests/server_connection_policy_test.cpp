#include <QtTest>
#include "server_connection_policy.h"
#include "local_token_protection.h"
class ConnectionPolicyTest : public QObject {
    Q_OBJECT
private slots:
    void tokenProtection() {
        const QByteArray token("a-test-capability-never-use-in-production");
        const auto stored = protectLocalTokens(token);
        QVERIFY(!stored.isEmpty()); QCOMPARE(unprotectLocalTokens(stored), token);
#ifdef Q_OS_WIN
        QVERIFY(!stored.contains(token)); QVERIFY(unprotectLocalTokens(token).isEmpty());
        auto damaged = stored; damaged[damaged.size()-1] ^= 1; QVERIFY(unprotectLocalTokens(damaged).isEmpty());
#endif
    }
    void origins() {
        QVERIFY(allowedServerOrigin(QUrl("https://school.example:443")));
        QVERIFY(!allowedServerOrigin(QUrl("http://school.example")));
        QVERIFY(!allowedServerOrigin(QUrl("http://127.0.0.1")));
        QVERIFY(allowedServerOrigin(QUrl("http://127.0.0.1:8081"), true));
        QVERIFY(allowedServerOrigin(QUrl("http://[::1]:8081"), true));
        QVERIFY(!allowedServerOrigin(QUrl("http://school.example"), true));
        for (const auto &value : {"https://user:password@school.example", "https://school.example/path", "https://school.example?token=1", "https://school.example#x", "file:///etc/passwd"})
            QVERIFY(!allowedServerOrigin(QUrl(value), true));
    }
};
QTEST_GUILESS_MAIN(ConnectionPolicyTest)
#include "server_connection_policy_test.moc"
