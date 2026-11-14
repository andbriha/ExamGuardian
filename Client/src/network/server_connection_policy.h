#pragma once
#include <QUrl>
#include <QHostAddress>

inline bool allowedServerOrigin(const QUrl &url, bool allowLocalHttp = false) {
    if (!url.isValid() || url.host().isEmpty() || !url.userInfo().isEmpty() || url.hasQuery() || url.hasFragment()
        || (!url.path().isEmpty() && url.path() != "/")) return false;
    if (url.scheme() == "https") return true;
    const auto host = url.host().toLower();
    return allowLocalHttp && url.scheme() == "http" &&
        (host == "localhost" || QHostAddress(host).isLoopback());
}

inline bool allowedClientOrigin(const QUrl &url) {
#if defined(EXAMGUARDIAN_DEVELOPMENT) || defined(EXAMGUARDIAN_TEST_LOOPBACK)
    return allowedServerOrigin(url, true);
#else
    return allowedServerOrigin(url);
#endif
}
