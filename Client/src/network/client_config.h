#pragma once
#include <QSettings>
#include <QString>
#include "server_connection_policy.h"

inline QString clientServerUrl() {
    const QString environmentUrl = qEnvironmentVariable("EXAMGUARDIAN_URL");
    QString url = environmentUrl.isEmpty() ? QSettings().value("serverUrl",
#ifdef EXAMGUARDIAN_DEVELOPMENT
"http://127.0.0.1:8081"
#else
"https://localhost"
#endif
).toString() : environmentUrl;
    while (url.endsWith('/')) url.chop(1);
    return allowedClientOrigin(QUrl(url)) ? url : QStringLiteral("blocked://invalid-server");
}
