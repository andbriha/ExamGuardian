#include "vm_windows.h"
#include "virtualization_runtime.h"
#include <QByteArray>
#ifdef Q_OS_WIN
#include <windows.h>
#include <setupapi.h>
#include <tlhelp32.h>
#include <vector>

namespace {
bool runningService(SC_HANDLE manager, const wchar_t *name) {
    SC_HANDLE service = OpenServiceW(manager, name, SERVICE_QUERY_STATUS);
    if (!service) return false;
    SERVICE_STATUS_PROCESS status{};
    DWORD needed = 0;
    const bool running = QueryServiceStatusEx(service, SC_STATUS_PROCESS_INFO,
        reinterpret_cast<BYTE *>(&status), sizeof(status), &needed) && status.dwCurrentState == SERVICE_RUNNING;
    CloseServiceHandle(service);
    return running;
}
}
#endif

WindowsVmEvidence collectWindowsVmEvidence() {
    WindowsVmEvidence result;
#ifdef Q_OS_WIN
    // Read firmware directly as well as registry mirrors. Never log SMBIOS IDs.
    constexpr DWORD rawSmbios = 0x52534D42; // 'RSMB'
    const UINT size = GetSystemFirmwareTable(rawSmbios, 0, nullptr, 0);
    if (size > 8 && size <= 1024 * 1024) {
        QByteArray data(size, '\0');
        if (GetSystemFirmwareTable(rawSmbios, 0, data.data(), size) == size) {
            data = data.toLower();
            for (const auto *vendor : {"vmware", "virtualbox", "innotek", "qemu", "bochs", "parallels", "virtual machine", "xen"}) {
                if (data.contains(vendor)) {
                    result.firmware = true;
                    result.details << QString("[SMBIOS guest marker: %1]").arg(vendor);
                    break;
                }
            }
        } else result.details << "[SMBIOS probe unavailable]";
    } else result.details << "[SMBIOS probe unavailable]";

    // Enumerate present hardware IDs, rather than display names or stale devices.
    HDEVINFO devices = SetupDiGetClassDevsW(nullptr, nullptr, nullptr, DIGCF_ALLCLASSES | DIGCF_PRESENT);
    if (devices != INVALID_HANDLE_VALUE) {
        SP_DEVINFO_DATA device{}; device.cbSize = sizeof(device);
        for (DWORD index = 0; SetupDiEnumDeviceInfo(devices, index, &device); ++index) {
            DWORD bytes = 0, type = 0;
            SetupDiGetDeviceRegistryPropertyW(devices, &device, SPDRP_HARDWAREID, &type, nullptr, 0, &bytes);
            if (!bytes || bytes > 65536) continue;
            std::vector<wchar_t> buffer(bytes / sizeof(wchar_t) + 2, L'\0');
            if (!SetupDiGetDeviceRegistryPropertyW(devices, &device, SPDRP_HARDWAREID, &type,
                    reinterpret_cast<BYTE *>(buffer.data()), bytes, nullptr) || type != REG_MULTI_SZ) continue;
            const QString ids = QString::fromWCharArray(buffer.data(), bytes / sizeof(wchar_t)).toUpper();
            for (const auto *vendor : {"PCI\\VEN_15AD&", "PCI\\VEN_80EE&", "PCI\\VEN_1AF4&", "PCI\\VEN_5853&", "PCI\\VEN_1AB8&"}) {
                if (ids.contains(QLatin1String(vendor))) {
                    result.devices = true;
                    const auto detail = QString("[Present guest PCI vendor: %1]").arg(vendor);
                    if (!result.details.contains(detail)) result.details << detail;
                }
            }
        }
        SetupDiDestroyDeviceInfoList(devices);
    } else result.details << "[Hardware enumeration unavailable]";

    HANDLE snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
    if (snapshot == INVALID_HANDLE_VALUE) {
        result.runtimeProbeFailed = true;
        result.details << "[Runtime process enumeration unavailable]";
    } else {
        PROCESSENTRY32W entry{}; entry.dwSize = sizeof(entry);
        if (Process32FirstW(snapshot, &entry)) {
            do {
                const QString name = QString::fromWCharArray(entry.szExeFile);
                if (isVirtualizationRuntimeProcess(name)) {
                    result.activeRuntime = true;
                    const auto detail = QString("[Active virtualization/container process: %1]").arg(name);
                    if (!result.details.contains(detail)) result.details << detail;
                }
            } while (Process32NextW(snapshot, &entry));
            if (GetLastError() != ERROR_NO_MORE_FILES) result.runtimeProbeFailed = true;
        } else result.runtimeProbeFailed = true;
        CloseHandle(snapshot);
    }

    SC_HANDLE manager = OpenSCManagerW(nullptr, nullptr, SC_MANAGER_CONNECT | SC_MANAGER_ENUMERATE_SERVICE);
    if (manager) {
        for (const auto *name : {L"VBoxGuest", L"VBoxSF", L"vmmouse", L"vmhgfs", L"xenevtchn", L"xenbus"}) {
            if (runningService(manager, name)) {
                result.drivers = true;
                result.details << QString("[Running guest driver: %1]").arg(QString::fromWCharArray(name));
            }
        }
        for (const auto *name : {L"VBoxService", L"VMTools", L"qemu-ga", L"xensvc", L"prl_tools"}) {
            if (runningService(manager, name)) {
                result.services = true;
                result.details << QString("[Running guest service: %1]").arg(QString::fromWCharArray(name));
            }
        }
        DWORD needed = 0, count = 0, resume = 0;
        // Include per-user WSL services; their suffix changes between logons.
        std::vector<BYTE> buffer(64 * 1024);
        bool more = false;
        do {
            const BOOL ok = EnumServicesStatusExW(manager, SC_ENUM_PROCESS_INFO, SERVICE_WIN32,
                SERVICE_ACTIVE, buffer.data(), DWORD(buffer.size()), &needed, &count, &resume, nullptr);
            const DWORD error = ok ? ERROR_SUCCESS : GetLastError();
            if (!ok && error != ERROR_MORE_DATA) { result.runtimeProbeFailed = true; break; }
            const auto *entries = reinterpret_cast<const ENUM_SERVICE_STATUS_PROCESSW *>(buffer.data());
            for (DWORD i = 0; i < count; ++i) {
                const auto name = QString::fromWCharArray(entries[i].lpServiceName);
                if (isVirtualizationRuntimeService(name)) {
                    result.activeRuntime = true;
                    result.details << QString("[Active virtualization/container service: %1]").arg(name);
                }
            }
            more = !ok;
            if (more && count == 0) {
                if (buffer.size() >= 256 * 1024) { result.runtimeProbeFailed = true; break; }
                buffer.resize(256 * 1024);
            }
        } while (more);
        CloseServiceHandle(manager);
    } else { result.runtimeProbeFailed = true; result.details << "[Service probe unavailable]"; }
    if (result.runtimeProbeFailed) result.details << "[Cannot confirm absence of active virtualization; admission blocked]";
#endif
    return result;
}
