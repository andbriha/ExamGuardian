# ExamGuardian

Self-hosted exam attempts with identity checks, saved answers, camera/screen evidence and human review. The academic score is calculated from answers; the integrity verdict is a separate examiner decision.

This is a pilot implementation. Model accuracy, resistance to recorded-video attacks, device compatibility and classroom-scale capacity require measured validation. See [PROJECT_ANALYSIS.md](PROJECT_ANALYSIS.md) for the original audit and [WORK_PROGRESS.md](WORK_PROGRESS.md) for implemented fixes and verification.

## Components and authority

- **Qt 6.10 / C++:** student login, exam selection, environment checks, camera/screen recording, microphone analysis and persistent delivery queues.
- **ASP.NET Core 10 / PostgreSQL 16:** owner checks, signed admission and attempt capabilities, frozen questions, deadlines, answers, scores, evidence storage and review history.
- **FastAPI:** identity matching, pose challenge, periodic identity checks, face/object observations and speech activity. Models share one bounded analysis slot. A SQLite outbox retries event delivery.
- **Angular:** examiner monitoring by REST polling, authorized preview images, camera/screen playback and review.
- **Nginx:** one origin for `/api/`, `/verify/`, `/analyze/`, panel and student portal. Qt sends images/audio through this origin directly to AI; AI validates the active attempt with Core.

An observation is a reason to inspect context, not a calibrated probability of cheating. The system does not implement SignalR/WebSocket video, automatic integrity scoring, micro-earpiece recognition, speaker attribution, immutable PDF signing or LMS/CSV export. The printable report uses the browser print dialog.

## Documentation

The documentation site separates system overview (`/`), launch instructions (`/start`), examiner workflow (`/guide`), architecture (`/dev`), and a reference map (`/docs`). Start development below to serve it locally. Source: [web-site/README.md](web-site/README.md).

## Development and production

```powershell
./run.ps1                 # isolated development, reload and Astro documentation
./run.ps1 --check         # validate configuration without changing containers
./run.ps1 --status
./run.ps1 --stop          # preserve development volumes
./run.ps1 --production    # explicit production configuration and HTTPS certificate required
./scripts/Start-Client.ps1
```

Development initializes `.env.dev` with independent random secrets; examiner credentials are stored there. The application defaults to `http://127.0.0.1:8081`. The docs port is selected on first initialization (starting at 4321) and printed at launch. Copy `.env.production.example` to `.env.production` and replace all placeholders before selecting production. The production alias `--продакшн` is supported. Production failures never launch development instead.

The native DEV client opens local controls at startup (`Ctrl+Shift+D` reopens them). Switches disable VM/process/display/virtual-device checks, keyboard restrictions or focus restrictions. Identity verification, recording, AI, API, panel and examination data use the same code as production. No sample data is seeded. The landing is shared; only the served installer differs. Production compilation removes the local controls; `Client/build-release.ps1` defaults to production. Use `-Flavor development` for the DEV installer.

The existing root `docker-compose.yml` and `.env` remain a legacy installation path. They are not the new dev/prod profiles and are preserved for deliberate migration.

## Legacy local environment

Run from the repository root with Docker Compose and PowerShell:

```powershell
./scripts/Initialize-Development.ps1
docker compose up -d --build
docker compose ps
```

The initializer generates random secrets and first examiner credentials in `.env`, and preserves an existing `.env`. Read that file locally to sign in. For an existing installation, change published/default credentials and keys deliberately before external deployment. Database migrations are tracked and applied on startup; preserve a checkpoint before upgrading an existing database.

- Student portal: `http://localhost/landing/`
- Examiner panel: `http://localhost/login`
- AI development docs: `http://127.0.0.1:8000/docs` (loopback only).

Create students, enroll a JPEG reference photograph, then create an exam with group, scheduled start, duration, attempt limit, passing score and permitted speech/objects. The student selects an exam and completes a fresh identity/pose challenge. Resume does not reset the original deadline.

## Client build

Install Qt 6.10.1 MinGW, matching compiler, CMake and Ninja. Build a fresh release from source:

```powershell
./Client/build-release.ps1
# Optional installer:
./Client/build-release.ps1 -InnoCompiler 'C:/Program Files (x86)/Inno Setup 6/ISCC.exe'
```

Output goes to a new `Client/releases/` directory with a manifest of hashes and source state. Copy a verified installer to the portal deliberately; existing portal binaries are not assumed to match this working tree. The login screen configures the server origin. `EXAMGUARDIAN_URL` overrides it. Use the same origin for all services.

Windows and Linux builds, headless QML startup and persistent upload-queue tests pass. Windows Qt login/admission/autosave/submission was checked against the disposable Core API. Linux has `/proc`, DMI/CPUID checks and X11 controls; real device and desktop behavior still need practical testing. Wayland environment blocking is unsupported. Neither platform provides an absolute sandbox against an administrator or external hardware.

## TLS

For a reachable institutional deployment, configure `.env` with `ASPNETCORE_ENVIRONMENT=Production`, `TLS_DIRECTORY` containing `fullchain.pem` and `privkey.pem`, and a valid host certificate. Then:

```powershell
docker compose -f docker-compose.yml -f docker-compose.tls.yml up -d --build
```

The TLS overlay redirects HTTP to HTTPS and adds the HTTPS port. Use an HTTPS origin in the student client. Refresh cookies are Secure outside Development.

## Evidence and recovery

Answers have a server autosave and a local pending copy. Camera and screen clips have immutable indices, verified MP4 tracks, actual durations, SHA-256 acknowledgements and a disk queue. Interrupted clips that cannot be decoded remain available for inspection; they do not become successful uploads. Delivery authorization lasts seven days; reopening the client with the same account and origin resumes queued deliveries during that period.

The final evidence manifest distinguishes Delivered, Partial, Pending and Unknown. Delivered refers to file/time coverage, not proof of useful picture quality. Accepted integrity decisions require complete delivered evidence and dismissed observations. Rejected decisions require a confirmed observation. Inconclusive records insufficient evidence. New late observations or newly received media return the attempt to review without changing its academic score.

## Backup, restore and retention

The following scripts use the **legacy default Compose project**. They do not select the new dev/production profiles automatically; do not assume they protect those environments unchanged. A profile-aware procedure must explicitly select the project, configuration, and database identifiers.

For the legacy installation, use a quiet checkpoint after active attempts and pending deliveries drain:

```powershell
./scripts/Backup.ps1 -Destination ./backups/checkpoint-01
./scripts/Restore-Checkpoint.ps1 -BackupDirectory ./backups/checkpoint-01 -Destination ./restored/checkpoint-01
```

The backup includes PostgreSQL, camera/screen files, photos/evidence and online SQLite snapshots with checksums. Restore validates checksums and restores into a new isolated PostgreSQL container; it preserves the existing installation. Verify restored records/media before switching any deployment to it.

Retention is disabled by default. The administrator must set `Retention__Days` (at least seven). Preview first with `dotnet Api.dll --retention-preview`; use `--retention-apply` only after reviewing the plan and preserving a backup. Commands run in the API storage environment with its database configuration. Only finished, already reviewed attempts older than the policy are eligible. Media expires; scores, answers, timing/hash sidecars and review history remain. Reference photographs have a separate enrollment lifecycle and are not removed by this command.

## Verification

```powershell
dotnet build Core/Core.sln --configuration Release
npm.cmd --prefix monitor ci
npm.cmd --prefix monitor run build
npm.cmd --prefix monitor test -- --watch=false --browsers=ChromeHeadless
```

`Inference/tests` exercises authentication, challenge transitions, durable delivery and evaluation metrics without downloading models. Install `Inference/requirements-test.txt` and run `python -m unittest discover -s tests -v` from `Inference/`.

Run `./tests/run-core-contracts.ps1` to build the API and create a disposable PostgreSQL environment, apply migrations, verify Core/retention contracts, and clean up its containers. Pass `-PythonExecutable` when Python is not on PATH.

`tests/test_native_contract.py` checks the native Qt wire protocol when `EXAMGUARDIAN_NATIVE_CLIENT` points to the `native_core_contract` target built with `EXAMGUARDIAN_BUILD_TESTS=ON`.

`tests/test_core_contract.py` is deliberately restricted to a disposable API on `127.0.0.1:55111`; configure its explicit environment variables. It creates test participants/attempts and must not target a working database. `tests/test_ai_http_contract.py` adds real-model HTTP smoke checks on `127.0.0.1:55112`; blank frames and silence establish wiring, not accuracy.

For labelled local samples, run `python evaluate.py dataset.jsonl --output evaluation-results/report.json` from `Inference/`. The report records confusion counts, false positives/negatives, latency, model/dependency versions and sample/weight hashes. Dataset files are not copied into the report. No accuracy or sub-100-ms capacity claim is established by a startup smoke test.
