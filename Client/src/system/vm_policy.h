#pragma once

// Strict exam policy: active hypervisors and runtimes block independently.
struct VmEvidence {
    bool hypervisor = false;
    bool microsoftHypervisor = false;
    bool knownGuestHypervisor = false;
    bool firmware = false;
    bool devices = false;
    bool runningGuestDrivers = false;
    bool runningGuestService = false;
    bool driverFiles = false;
    bool network = false;
    bool disk = false;
    bool activeRuntime = false;
    bool runtimeProbeFailed = false;
};

inline int vmEvidenceScore(const VmEvidence &e) {
    if (e.hypervisor || e.activeRuntime || e.runtimeProbeFailed || e.devices ||
        e.runningGuestDrivers || e.runningGuestService) return 100;
    int score = 0;
    score += e.firmware ? 55 : 0;
    score += e.devices ? 45 : 0;
    score += e.runningGuestDrivers ? 35 : 0;
    score += e.runningGuestService ? 30 : 0;
    score += e.driverFiles ? 5 : 0;
    score += e.network ? 5 : 0;
    score += e.disk ? 15 : 0;
    // Old files and network/disk markers alone do not prove active virtualization.
    return e.firmware ? score : (score > 20 ? 20 : score);
}
