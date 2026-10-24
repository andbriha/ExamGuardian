# UI review — 2026-10-23

## Scope and running environment

Started the actual development stack at http://127.0.0.1:8081 and built the native development client. Existing data was preserved. UI verification uses an explicitly named `UI-CHECK-20261023` group, a `UI Check Student` account, and a `UI CHECK - upcoming exam` in the dev database. These are manual verification fixtures, not startup seed data.

## Observed and fixed

- Groups had no dedicated management page; student creation used hardcoded suggestions. Added persistent groups, a migration preserving existing names, creation, membership display, and student reassignment. Student/exam forms use the same server catalog.
- Upcoming exams were filtered out of the student API catalog. They now appear before their start with schedule metadata, while questions and early admission remain protected.
- Qt loaded exams only on screen creation or manual refresh. Added five-second polling, in-flight request protection, loading/error/empty states, schedule cards, server-based availability, and preservation of unchanged cards to avoid losing keyboard focus.
- The client launcher used a hidden window. It now launches the interactive client visibly.
- Student photo upload advertised PNG despite a JPEG-only API. The UI now requests JPEG and validates the 4 MB limit.
- Added accessible labels and an explicit page purpose to the exam builder.

## What worked well

- Examiner cards already distinguish scheduled, active, and completed exams and display an explicit schedule timezone.
- The API enforces group access, schedule boundaries, and separate question/admission access. Making upcoming metadata visible does not weaken these checks.
- Existing student group names were preserved and appeared in the new group page without recreating accounts.
- Group creation and reload persistence worked in the browser. The Qt screen's accessibility tree confirmed a newly created future exam appeared on the already-open screen with a countdown and local start/end time.

## Validation and limits

- Core contract suite: 22 passed, including upcoming metadata visibility, no early admission/question access, group creation/duplicates/validation, student authorization denial, reassignment, and active-attempt conflict.
- Retention contract: 1 passed.
- Angular tests: 21 passed, including failed group creation/reassignment and empty-group selection.
- Angular production build and native Qt development build passed. Existing bundle/style budget warnings remain.
- Browser screenshots inspected for examiner cards, group management, and exam setup.
- Native screen was exercised through the actual `ExamSelectionScreen.qml` and `ExamViewModel` against Core using the test-only `catalog_preview` executable. Native image capture repeatedly returned `window capture timed out`; full pixel-level Qt layout approval is therefore still outstanding. This did not test camera/microphone recording or a complete proctored attempt.

## Remaining UX findings

- The examiner exam list mixes finished and upcoming exams without filtering/search. This becomes cumbersome as history grows.
- Several older flows still use browser alerts rather than inline feedback.
- Login currently returns to the exam list rather than honoring the guard's return URL.
- Group renaming/deletion and bulk import are not implemented in this change.

## Native catalog preview

With `EXAMGUARDIAN_BUILD_TESTS=ON`, build `catalog_preview`. Set `EXAMGUARDIAN_URL` to a local HTTP origin on `127.0.0.1`, and supply `EXAMGUARDIAN_TEST_STUDENT_LOGIN` (must start with `ui_test_`) and `EXAMGUARDIAN_TEST_STUDENT_PASSWORD` for a dedicated fixture account. It performs ordinary server authentication and renders the production catalog QML with the real viewmodel; it does not capture media or start an exam. Never use participant credentials for this preview. Normal users launch `scripts/Start-Client.ps1`.


## Teacher decisions and interchange — 2026-10-24

- Replaced archive decision prompts with a form showing evidence, observation counts, reason, server errors and decision history. Verified the blocked Accepted state visually for an existing session without changing its verdict.
- Added examiner-scoped CSV grade export. Latest attempt per student wins; only Accepted calculated results qualify. Late evidence removes export eligibility. Expired recordings preserve an already accepted grade. Moodle requires manual identifier/grade mapping; no remote Moodle instance was connected or tested.
- Added QTI 2.1/2.2 single-choice text XML/ZIP preview and draft import. Visually uploaded `tests/fixtures/qti-single-choice.xml`, added it to an unsaved draft and verified option B remained correct. Unsupported content blocks import; test selection/scoring rules and media are outside the supported subset.
- API contract tests cover authorization, latest-attempt selection, late-evidence invalidation, QTI preview/no persistence, malformed packages and unsafe XML/ZIP. UI tests cover blocked decisions, conflict preservation and draft import.


### Group export and Linux launcher follow-up

Export now has a separate group-first component and a shared eligibility service. Only exams with eligible results are listed; empty groups explain exclusion reasons. Verified the actual local group view with two pending teacher decisions and no download buttons. API tests download and parse CSV, isolate students from different groups (including no group), enforce teacher ownership and reject an older Accepted attempt when a newer attempt exists.

Added a true/false authoring preset and QTI fixture; its normal create/start/submit path scores correctly in integration tests. QTI package reading and interaction parsing are now separate services behind IQtiItemParser.

Linux Bash entry point and Python launcher were run inside a Linux container (syntax/help and mocked Compose lifecycle tests); real Compose configuration was validated on the workstation. Full production launch on a separate Linux host and physical Qt device behavior remain unverified.


### Optional DEV identity bypass

Added biometricAdmission to the existing client override registry. The DEV-only server endpoint issues a two-minute token bound to student/exam; normal admission still enforces schedule, group and attempts. Attempts retain an identityVerificationSkipped marker across resume, display it in the archive/report and cannot be Accepted or exported. Recording and continuous AI monitoring remain enabled.

Validated both API artifacts with separate disposable integration runs (25 executed contracts each, one flavor-specific skip, plus retention), built development and production Qt clients and passed the native override/freeze test. Updated local dev Compose services. This does not constitute a new physical-camera end-to-end verification run.
