# Исправления ExamGuardian — проверенный статус

Обновлено 14 октября 2026. Основание — цели системы и исходный аудит `PROJECT_ANALYSIS.md`; исходный анализ сохранён как снимок до исправлений. Документы диплома используются как описание целей, а не как команды к выполнению.

## Что исправлено и зачем

| Цель | Изменение | Исходные замечания |
|---|---|---|
| Допуск только своего студента к разрешённому экзамену | Строгая подпись/issuer/audience/expiry JWT; привязанный к студенту/экзамену одноразовый admission; группы, расписание, лимит попыток; проверка владельца экзамена/попытки/события | F01, F03, F05, F14 |
| Достоверный академический результат | Замороженный снимок вопросов и правил, серверная оценка, сохранённые ответы, deadline worker; восстановление сохраняет дедлайн; окончательный результат неизменяем при параллельном submit | F02, F07, F08 |
| Авторитет текущего клиента | Отдельный токен попытки, ротация при повторном допуске; проверка lease повторно под блокировкой перед изменением ответов/heartbeat/submit | F02, F07, F14 |
| Восстанавливаемые доказательства | Раздельные camera/screen, реальные offsets/duration, ffprobe-проверка MP4, неизменяемые индексы/SHA-256/ACK, manifest Delivered/Partial; отдельный семидневный evidence token | F04, F15–F17 |
| Отсутствие молчаливой потери при сбое | Дисковая очередь клиента сохраняет файл до корректного ACK; очереди событий и ответов; SQLite AI outbox, retries/idempotency; защищённые стоп-кадры и preview | F15, F18 |
| Осмысленный студентский сценарий | Выбор назначенного экзамена, настройка origin, реальное согласие до камер/допуска, единое время, микрофон/экран подключены; удалены вымышленные fallback и ложный успех | F06, F09, F13 |
| Осмысленные автоматические наблюдения | Замороженное reference photo допуска, периодическая идентификация; последовательность нейтральная поза → устойчивое движение на разных кадрах; anti-spoofing; объединение коротких повторов | F10, F11 |
| Честные ограничения AI | Убрана неподтверждённая эвристика микронаушника; движения головы не названы измерением взгляда, VAD не определяет автора речи; ограниченная очередь анализа, readiness/хеши моделей, offline evaluation | F12 |
| Проверяемое решение преподавателя | Confirmed/Dismissed с основанием и историей; Accepted/Rejected/Inconclusive отдельно от балла; новые события/медиа снова открывают review; одинаковый retry сохраняет решение | F20, F21 |
| Панель соответствует данным | Scoped SQL reads; guards/refresh; реальные статусы связи/анализа/аудио/доставки; preview/стоп-кадры; seek по metadata и готовности видео; обновление истёкшей ссылки с сохранением позиции, адаптивная вёрстка | F09, F16, F17, F19 |
| Отсутствие ложного результата | Остановленная преподавателем и историческая попытки не показываются как рассчитанная оценка; `scoreCalculated` явно передаётся клиенту/панели | F21 |
| Сопровождаемое развёртывание | Пять отслеживаемых миграций, случайные bootstrap/secrets, конфиг CORS, TLS overlay, online SQLite backup, восстановление в отдельный Docker volume, явный retention, release manifest и CI | Разделы 10–12 аудита |

Windows hooks, восстановление настроек Linux X11, проверки процессов/окружения и повторные клиентские наблюдения доработаны. Это не обеспечивает абсолютной изоляции от администратора или внешних устройств. Wayland blocking не поддерживается.

## Фактически пройденные проверки

- **Core:** Debug и Docker Release/publish без ошибок. Четыре предупреждения CS8981 в исторических именах миграций `init`/`add`.
- **Новая пустая БД:** `tests/run-core-contracts.ps1` самостоятельно собирает API, поднимает PostgreSQL/API, применяет миграции, запускает **17 HTTP контрактов + 1 retention контракт** и удаляет свои контейнеры/сеть. Последний запуск успешен.
- **HTTP Core:** admission/подпись/IDOR, capability rotation, группа/расписание, deadline, параллельный start/submit, сохранение/восстановление/заморозка ответов и результата, owner scopes, review/history, остановка без вымышленного балла, валидное/повторное/чужое видео, подписанные ссылки, неполная manifest и позднее видео.
- **Retention:** preview не удаляет медиа; apply удаляет MP4 старой завершённой проверенной попытки, сохраняет JSON timing/hash, ответы, балл 100, признак расчёта, решение и историю. Отсутствующий reference directory корректно обрабатывается.
- **AI:** **8 unit контрактов** JWT/challenge/outbox/metrics; **3 реальных HTTP smoke сценария** авторизации/чужой попытки, пустого кадра → durable observation/стоп-кадр/preview/receipt, тишины → audio receipt. Пустой кадр и тишина не измеряют точность распознавания.
- **Модели:** проверены SHA-256 пяти фактически установленных весов; readiness выполняет загрузку/warmup и проверку хешей. Версии зависимостей зафиксированы.
- **Windows Qt:** свежая Debug сборка, headless QML startup, тест дисковой очереди (ошибочный ACK сохраняет данные, перезапуск/refresh восстанавливают очередь, верный ACK удаляет файл).
- **Qt → реальный Core:** отдельный native runner прошёл вход, admission, загрузку замороженных вопросов, parsing серверного времени, autosave, submit и оценку 100 на одноразовом студенте.
- **Linux Qt:** Docker Debian build, disk queue contract и offscreen QML startup проходят; ошибка порядка уничтожения QML/VM исправлена. Камера/аудио/X11 на физическом Linux устройстве этим не проверены.
- **Angular:** production build и **17 ChromeHeadless тестов** проходят. Включены переход короткий → длинный клип, отмена pending seek при ручном выборе и однократное обновление видеоссылки с сохранением позиции/паузы.
- **Панель в браузере:** осмотр на desktop и 390×844 с синтетическими тестовыми данными; исправлен слишком узкий видеоцентр. Снимок `monitor-validation.png` сохранён в каталоге артефактов. Последующие исправления плеера дополнительно проверены unit тестами.
- **Astro:** после актуализации README/главы 11 собираются **15 страниц**. Главы 01–10 помечены историческим контекстом.
- **Восстановление:** из тестового checkpoint восстановлены PostgreSQL, media и SQLite в отдельный Docker volume; совпали количества записей (66 попыток, 12 наблюдений, 5 миграций), хеши медиа и SQLite integrity. Основная БД этим не изменялась. Полный Backup.ps1 на рабочем стеке не запускался.
- **TLS:** `nginx -t` успешно проверил overlay-конфигурацию с временным локальным сертификатом и тестовым upstream. Выпуск/установка сертификата учреждения и реальный HTTPS rollout этим не подтверждены.
- **Windows Release:** свежий клиент и Inno Setup установщик собраны из текущих исходников; packaged headless startup успешен, source hashes сверены с текущим Client.

Есть предупреждения Angular о размере CSS трёх страниц и возрасте baseline mapping; ошибок сборки/тестов нет.

## Готовые артефакты

Каталог: `C:/Users/andbriha/.codex/visualizations/2026/10/14/01a13a40-f59c-7d93-b226-f9233aa66bf0/release-final-20261014/`.

- `Online_Proctoring_System_Setup.exe` — свежий установщик.
- `client/` — развёрнутый клиент с Qt/MinGW зависимостями.
- `release-manifest.json` — хеши пакета и исходников, commit/dirty state.
- SHA-256 установщика: `807ee7c0ad20b3a5c000e6233e34d2d26803f6663669ecf8727189806b3ce039`.

Установщик не опубликован в портале. Старые промежуточные пакеты сохранены; для проверки текущего результата использовать только указанный финальный каталог.

## Что ещё необходимо для принятия системы в эксплуатацию

1. Сквозной экзамен на физических Windows/Linux устройствах: камера, микрофон, экран, фокус/hooks, отключение сети, аварийное завершение во время записи и восстановление последнего незавершённого MP4. Эти проверки невозможно заменить сборкой/offscreen тестом.
2. Размеченные данные для false positives/false negatives и атаки фото/экран/запись, справедливость порогов при разных условиях. `Inference/evaluate.py` готов, реальный датасет не предоставлен.
3. Нагрузка нескольких одновременных студентов на целевом оборудовании; нынешний ограниченный slot защищает модели, но не подтверждает требуемую пропускную способность.
4. Утверждённая политика учреждения: согласие, срок хранения, последствия технических сбоев, критерии ручного решения, допустимые материалы. Срок retention по умолчанию выключен.
5. Управляемое обновление рабочего стека после checkpoint и завершения активных попыток; актуальный установщик в портале, случайные секреты/пароли вместо старых опубликованных, реальный TLS origin, контрольное восстановление рабочего backup.

## Состояние рабочей установки

Изменения находятся в рабочей копии; исходные пользовательские изменения сохранены. Основные `proctoring_*` сервисы и их БД не переустановлены, рабочие результаты не изменялись. Изолированные проверки использовали только `examguardian-*` контейнеры и синтетические учётные записи. Длительно использовавшиеся тестовые контейнеры остановлены после проверок; их данные и восстановленный checkpoint сохранены для продолжения. Одноразовый runner удалил собственные ресурсы. Веса anti-spoofing ранее добавлены в общий кеш моделей для тестовой загрузки.

Не считать успешные программные контракты подтверждением точности AI, безопасного институционального применения или испытаний реальных устройств. Оригинальный аудит, исправленный исходный код и этот журнал служат основанием для следующих практических проверок.


## Documentation rendering follow-up

All 22 Mermaid diagrams now use one bundled renderer with strict parsing and a readable source fallback. Two invalid unquoted edge labels were corrected. Mermaid no longer loads from an external CDN or initializes twice. Added explicit documentation typography, responsive tables/code blocks and an English development/production specification. `npm run check:diagrams` passes all 22 sources; the 16-page Astro build passes. CI now checks diagrams and builds documentation. The requested Cyrillic production CLI alias remains a literal command; explanatory content is English.

Visual browser verification of localhost:4322 was declined by the browser access gate. No alternate browser or network access was used to bypass that decision; syntax and build verification use local files only.


Documentation structure clarification: the standalone dev/prod plan was folded into architecture chapters 03/04/05/06/09 and the organizer guide, using present-tense descriptions of the target design. The previous chapter URL redirects to deployment documentation. Runtime dev/prod profiles and override features remain unimplemented at this checkpoint; their architectural documentation is not evidence of shipped behavior. The detailed former specification is preserved in the task artifact directory as dev-prod-requirements-reference.md.


## Development / Production delivery — 15 October 2026

Implemented the isolated profiles and native development tools requested after the architecture documentation update.

- `run.ps1` defaults to dev; production aliases, check/status/stop, independent random `.env.dev`, explicit production/TLS validation and no fallback.
- Separate Compose project names/networks/storage/keys. SDK Core watch, AI reload, Angular hot updates and an Astro container; health checks work for the SDK process. Explicit video storage prevents source-mounted legacy media from being used by dev.
- Dev origin `http://127.0.0.1:8081`, docs `http://127.0.0.1:4323` on this workstation. Ports 4321/4322 were already occupied and preserved.
- Astro container restart tested. Its CLI PID lock survives container stop incorrectly; container-managed startup uses supported `--ignore-lock`. The corrected container becomes healthy after restart.
- DEV Qt override window/button/shortcut and persistent banner, authenticated capability negotiation, reset on origin/account changes, frozen policy. VM/process/display/device gates, keyboard/focus and capture/analysis controls are wired. Actual detections and missing recordings remain visible.
- Test admission has independent signing key/audience/purpose and binds student/exam/expiry. If biometric override is unchecked, it requires a valid ordinary identity admission before attaching test policy.
- Six migrations; test markers/policy/audit are server-owned. All controls are currently frozen after admission/start; the API accepts bounded audited hook events for owned unfinished test attempts, but the window does not edit a frozen policy.
- Panel has DEV environment label, TEST badges, policy/audit display, archive filter and labelled printable reports.
- Production API compilation omits development controllers/signing module. Production Qt omits dev sources/window; generated Main/ProcessCheck resources have no DevVM lookups or bypass branches. Runtime `--verify-production` inspects actual resources; dev flags return exit code 2.
- Hyper-V root-host classification corrected: Docker/WSL2/VBS hypervisor/network/storage indicators alone do not prove a guest VM.
- Independent native launch script and development/production installer identities. DEV portal is separate from legacy Landing; legacy installer/data were preserved.

Verification:

- 19 production-artifact HTTP Core contracts plus retention passed against disposable PostgreSQL, including rejection of development route/token and client-written test flags.
- Four development HTTP/native contracts passed against the explicit isolated dev origin: authorization/allowlist, frozen signed policy, ordinary biometric proof when not bypassed, actual Qt login/capabilities/overrides/admission/autosave/submission with score 100.
- Windows production startup/resource/argument probes, both native build flavors and queue tests passed. Linux production Docker build, queue/startup/resource probes passed.
- 18 Angular tests and production build passed; 22 Mermaid sources and 16-page Astro build passed.
- Dev stop preserved legacy container IDs/start times. Dev restart retained volumes/accounts and all services became ready. Compose checks confirm project-prefixed volumes, no fixed container names and no docs/watch/test tools in production profile.
- One Docker Engine 500 error interrupted a final repeat and Linux build before it began. Engine recovered without a restart; the development contracts and Linux verification then passed. No root cause was established from the inspected logs.

Final installer snapshot directories:
`C:/Users/andbriha/.codex/visualizations/2026/10/14/01a13a40-f59c-7d93-b226-f9233aa66bf0/dev-prod-releases/development-20261015/`
and `production-20261015/`. Older intermediate releases are preserved; use these dated snapshots. DEV installer is copied to `Landing.dev/ExamGuardian_Dev_Setup.exe` with its release manifest after verification.

Production profile/source/artifacts are prepared, but the institution's production stack is not launched: `.env.production` and real HTTPS configuration are intentionally required. The legacy `proctoring_*` services are not redeployed. Physical-device/full recording fault tests, labelled recognition quality and classroom load remain outside the completed software contracts. Fault-injection tools and arbitrary stage navigation are not part of this delivered override window.

### DEV access correction — 2026-10-15

The development nginx emitted an absolute root redirect using its internal port 80, dropping the external port 8081. Disabled absolute redirects and preserved the incoming Host including its port for API/Angular proxy requests. Validated configuration and reloaded only the development nginx. Two read-only HTTP regression checks pass for root redirect, landing directory redirect and examiner login page.

The configured examiner credentials from .env.dev authenticate successfully (HTTP 200), and authenticated capabilities also return 200. Repeated 401 responses indicate rejected credentials; development uses a separate database. The DEV landing now explains where the generated examiner credentials reside and that desktop login requires a student created in the development examiner panel. No account/password changes or database reset were performed.

### Development correction to the user's intended scope — 2026-10-15

Development now runs the same Core/AI/panel/landing implementation as production. The only examination behavior difference is the native development client's local menu: VM, process, display, virtual-device, keyboard and focus gates can be disabled. The menu opens at startup before login, starts with all switches off, retains selections through login and freezes them once an attempt begins. Identity challenge/admission, camera/screen/audio recording, AI, answers, scores and review remain ordinary.

Removed the development admission controller/signer/key, special backend compilation mode, server test policy/audit workflow, synthetic metadata from runtime/DTOs and panel TEST labels/filters/environment banner. A forward RemoveDevelopmentAttemptMetadata migration removes the three retired columns while preserving existing migration history. Removed the runner that populated the user's working development database; local controls are now verified offline. HTTP mutation contracts continue only in a disposable environment.

Before cleanup, saved the development database to the task artifact directory dev-cleanup-20261015/before-cleanup.dump. Removed exactly 15 generated Development contract exams, 15 dev_test_* students, their 10 linked attempts and 3 observations. The linked attempt on a generated exam made using andrii_87 was included in that cleanup. Preserved andrii_87, examiner and the user's reference image. No existing production/legacy database was changed.

Development serves unchanged Landing/index.html and style.css. Only executable downloads resolve to development artifacts, via nginx aliases and a separate download mount. The installer URL stays /landing/Online_Proctoring_System_Setup.exe. Root/directory redirects retain port 8081.

Checks: 19 Core HTTP contracts and retention passed in disposable PostgreSQL; 17 Angular tests and production build passed; 22 Mermaid diagrams and 16-page Astro build passed; native local controls and both upload-queue contracts passed. Working DEV has no generated exams/attempts, retains two real accounts, applies seven migrations and has no synthetic admission route (404). Landing HTML matches the common source byte for byte. Windows release packaging probes check both startup and absent production controls. Long artifact directory names caused an Inno Setup path error; final packages use short Client/releases directories.

Final corrected installers: Client/releases/20261015-dev-local/ExamGuardian_Dev_Setup.exe and Client/releases/20261015-production-local/Online_Proctoring_System_Setup.exe. Both release manifests match current Client source hashes. Published the final DEV installer and native executable to Landing.dev; both common landing download URLs match those files by SHA-256. The common landing HTML remains byte-identical. All six development services are running and API/docs/AI/monitor/database are healthy. The working database remains free of generated exams/attempts; the removed synthetic admission route returns 404. The final DEV window opens on the next event-loop turn after attachment to the native window.

### Absolute exam windows, evidence recovery and anatomical head direction — 2026-10-15

Exam availability and every attempt deadline now use the shared scheduled window [start, start + configured duration). Late arrival receives only the remaining time; expired exams cannot be started/resumed and answers at the deadline are rejected. Deadline processing uses saved answers and records the scheduled end. The panel transitions scheduled/active/completed using server clock data and explicitly displays the start, end and IANA timezone. The live hub includes exams in their active window even before a student joins and refreshes periodically.

The browser's local-time interpretation stored the two newly created exams three hours late. Creation now accepts local calendar time plus an explicit timezone and converts it in Core; DST gaps/ambiguity are rejected. After a database backup, corrected only exam 16/17 from the screenshot to their intended 02:05/02:03 Europe/Chisinau start times (23:05/23:03 UTC the previous date). Historical exam 18 and completed attempt 11's score were preserved. EXAM_TIME_ZONE configures the deployment display zone.

The real attempt 11 camera/screen files were still on disk. A finalization request for previously removed attempt 10 was retrying 404 forever and blocked the global queue. Queue selection now skips missing capabilities and blocked/unrecoverable entries; finalization waits only for its own attempt. Permanent 400/403/404/409 responses preserve entries for diagnosis and allow other attempts to proceed; 401 waits for refreshed authentication. A regression covers both an orphan without credentials and a deleted attempt returning 404, plus wrong-ACK file preservation and successful recovery after restart.

Recovered attempt 11 using its original on-disk files, metadata and existing evidence capability through the normal authorized upload endpoints. The server acknowledged SHA-256 for 12,592,083 camera bytes and 10,823,166 screen bytes; delivered the original queued observation and finalized both streams. Status is Delivered. Original local files/queue remain until the repaired client acknowledges delivery. The camera has H.264 video and AAC audio, the screen has H.264 video; actual signed playback supports HTTP 206 ranges. Backup and recovery result live in the task artifact directory evidence-recovery-11.

Head yaw is now normalized anatomically: own left projects toward image-right in an unmirrored camera frame. Challenge verification and continuous observation labels share that convention. SQLite challenge connections are closed deterministically. Nine AI unit contracts pass, including direction mapping and challenge transitions; physical pose acceptance still needs the user's new trial.

Verification: 21 Core HTTP contracts plus retention passed in disposable PostgreSQL, including expired windows, late arrival and timezone/DST conversion; 18 Angular tests pass; Angular and 16-page Astro builds pass; both native queue contracts pass and the local DEV controls contract passes. No synthetic data was inserted in the working database. New verified release directories are Client/releases/20261015-dev-window and Client/releases/20261015-production-window. Both source manifests match the checkout, and DEV installer/executable download URLs match published files by SHA-256. Production package startup/resource probes pass. Source changes are live in development; existing installed clients need the new installer.

### WSL/Docker outage — 2026-10-15 morning

Both development HTTP origins stalled and Docker API calls timed out. Docker backend repeatedly logged no route to its internal engine (192.168.65.7:2376). Docker Desktop restart timed out while terminating its WSL distribution. Targeted docker-desktop termination, Ubuntu /bin/true, subsequent WSL listing and full WSL shutdown also timed out. The shared WSL runtime/control path is unresponsive; an application-code cause was not established. Windows reported roughly 0.8 GiB free RAM during diagnosis, but no OOM root cause was proven.

Stopped the hung Docker Desktop/backend/build processes and launched Docker Desktop again; it remains in starting while WSL is stuck. Restart-Service WslService was denied by Windows because the task process is not an administrator. No volumes, database files or project configuration were removed/reset. Restoring the app requires an administrative WSL-service restart or Windows reboot, then bringing the existing Compose project up without rebuilding/resetting data.

Restored documentation independently from the already-built web-site/dist using a background loopback-only Python static server: http://127.0.0.1:4321/docs/09-deployment-view/ returns HTTP 200. Temporary server PID 59344; logs are under %TEMP%/examguardian-docs-recovery. The normal container docs origin remains 4323 once Docker recovers; this temporary server is a static preview, not live reload.

### Fresh Windows installers published — 2026-10-15 10:33 local

Rebuilt and packaged the current development and production client sources into Client/releases/dev-1015-103311 and Client/releases/prod-1015-103311. Packaged startup probes and production resource exclusion checks passed. Four development CTest contracts and three production contracts passed (queue, VM evidence policy, automatic verification capture, plus local development controls). Both source manifests match the working tree.

Published the DEV installer through the existing development download alias and updated common production binaries/legacy aliases. Added the explicit production download file Landing/ExamGuardian_Production_Setup.exe so both flavors can be downloaded from the running dev origin without changing landing HTML. Previous common production binaries are preserved in Client/releases/published-before-1015-103311. Both HTTP installer downloads were streamed and verified against their release SHA-256; DEV is 33,595,342 bytes and production is 33,594,740 bytes. Docker and the application origin are responding again; no system restart was performed in this turn.

### Frontal liveness checkpoints and phase-correct prompts — 2026-10-15 10:53 local

The user still received repeated passive anti-spoof rejection after changing lighting. Found that every gesture frame had to pass the passive classifier and any refusal erased the entire gesture state. Changed the shared verification protocol to require successful identity + passive liveness on a neutral frontal checkpoint before each action, fresh reference identity matching throughout the gesture, and another successful passive checkpoint on its neutral return. The anchor is server-owned, lasts at most 30 seconds, and is invalidated by identity/quality loss or a frame gap over five seconds. Failed return liveness waits without crediting the action; no admission is issued until all action checkpoints succeed. Existing model thresholds are unchanged. This changes the verification policy from per-frame passive checks to bounded frontal checkpoints; real-person/attack accuracy still needs labelled trials.

Motion inference remains outside SQLite write locks; the commit compares the original motion snapshot and revalidates challenge task/expiry/consumption before updating. Replay frames do not advance state. Added tests for initial/final checkpoint rejection, profile frames still requiring identity, replay on final return and identity-loss anchor invalidation. All 19 AI contracts pass in temporary state without modifying working accounts/exams.

The camera overlay now separates prepare/perform/return phases from task-change notifications, so it asks for a straight gaze when establishing or confirming a checkpoint and does not inflate the three-action counter. Removed the categorical instruction to remove photos/screens from an uncertain model rejection. Added a native phase/counter regression; four development and three production CTest contracts pass. Packaged startup/resource probes, 22 diagrams and 16-page documentation build pass.

Final releases: Client/releases/dev-face-1015-105308 and Client/releases/prod-face-1015-105317. Both match current Client source manifests; published both installers/executables and verified HTTP downloads by SHA-256. The earlier dev-live/prod-live packages are intermediate snapshots; use the face releases. The running development AI reload is ready. A new user-camera trial is still required; no successful physical verification is claimed from unit tests.
