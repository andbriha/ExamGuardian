#pragma once
#include <QByteArray>
#ifdef Q_OS_WIN
#include <windows.h>
#include <dpapi.h>
#endif

inline QByteArray protectLocalTokens(const QByteArray &plain) {
#ifdef Q_OS_WIN
    DATA_BLOB input{static_cast<DWORD>(plain.size()), reinterpret_cast<BYTE *>(const_cast<char *>(plain.constData()))}, output{};
    if (!CryptProtectData(&input, L"ExamGuardian delivery credentials", nullptr, nullptr, nullptr, CRYPTPROTECT_UI_FORBIDDEN, &output)) return {};
    QByteArray protectedBytes("DPAPI1:");
    protectedBytes.append(reinterpret_cast<char *>(output.pbData), output.cbData);
    LocalFree(output.pbData);
    return protectedBytes;
#else
    // Other platforms retain owner-only permissions; OS keychain integration is separate.
    return plain;
#endif
}

inline QByteArray unprotectLocalTokens(const QByteArray &stored) {
#ifdef Q_OS_WIN
    if (!stored.startsWith("DPAPI1:")) return {}; // Never silently restore legacy plaintext capabilities.
    QByteArray encrypted = stored.mid(7);
    DATA_BLOB input{static_cast<DWORD>(encrypted.size()), reinterpret_cast<BYTE *>(encrypted.data())}, output{};
    if (!CryptUnprotectData(&input, nullptr, nullptr, nullptr, nullptr, CRYPTPROTECT_UI_FORBIDDEN, &output)) return {};
    QByteArray plain(reinterpret_cast<char *>(output.pbData), output.cbData);
    LocalFree(output.pbData);
    return plain;
#else
    return stored;
#endif
}
