# Environment checks

Windows probes collect independent guest indicators: CPUID vendor, BIOS registry
strings, raw SMBIOS, present PCI hardware IDs, running guest services/drivers,
driver files, disk signatures and virtual NIC prefixes. They do not modify system
configuration, stop services or require elevation. Probe failures are included in
details where available; absence of an indicator is not proof of a physical host.
SMBIOS serial numbers and complete device IDs are not included in diagnostics.

`src/system/vm_windows.cpp` collects Windows evidence; `vm_policy.h` applies the
strict exam policy. Any active hypervisor (including Microsoft Hyper-V and VBS),
present guest PCI device, running guest driver/service, or known container/VM/WSL
process or service blocks independently. Guest firmware also blocks. Docker,
Podman, containerd, WSL (including per-user services), Hyper-V compute/management,
VMware, VirtualBox and QEMU runtimes are covered by explicit name rules.
Service enumeration includes running, paused and transitioning services. Process
or service enumeration failure blocks admission until the check can complete.
Installed inactive files or residual network/disk markers alone are not enough.
The client does not disable Windows security features or stop any services itself.

There is no host exception for Docker/WSL/VBS. This strict policy can require a
separate exam workstation when the development/backend host runs Docker. Firmware
virtualization capability alone (VT-x/AMD-V enabled in BIOS) is not an active
hypervisor and is not a blocker.

The pre-exam gate, the check immediately before entering the exam, and the existing
five-second exam scan use the same detector. On a runtime block, exam questions and
input are hidden/disabled; recording, heartbeat, answer persistence and deadline
continue. The student can submit existing answers. The overlay clears after a clean
scan. A virtualization observation is sent through the existing reporting path.
DEV's explicit `vmGate` override remains a local testing control; production
excludes overrides. A modified client or privileged attacker can still falsify
user-mode evidence. Runtime names can be renamed; this complements firmware,
hardware and hypervisor probes rather than providing remote attestation.

API references:
- [Raw SMBIOS access](https://learn.microsoft.com/en-us/windows/win32/api/sysinfoapi/nf-sysinfoapi-enumsystemfirmwaretables)
- [Hyper-V root and child partitions](https://learn.microsoft.com/en-us/virtualization/hyper-v-on-windows/tlfs/virtualization-host)

Tests: `vm_evidence_policy` covers strict Hyper-V host blocking, standalone runtimes, failed probes,
name matching and combinations with masked CPU/firmware; `automatic_verification_capture` covers timer-driven frame
submission and stopping on success/cancellation. These tests do not certify VM
detection rates or biometric recognition accuracy.
