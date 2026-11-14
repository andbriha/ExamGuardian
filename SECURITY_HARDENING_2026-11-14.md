# Security and architecture: completed changes

The previous phase (AI, a calm student interface, teacher decisions) was pushed to `origin/feature/active-liveness`, commit `344b67e`; CI fixes were pushed in commit `2294415` (both push/PR workflows succeeded). This phase is on `feature/security-architecture-hardening`. The changes have not been deployed to the live server.

## What changed

- Core issues short-lived access JWT (15 minutes by default) using RS256. Inference receives only public keys through a fixed JWKS URL. Admission, attempt and media-link keys are separated by purpose; the shared HMAC access JWT is no longer accepted.
- Login sessions are stored in PostgreSQL. Refresh tokens are stored as hashes and rotated atomically; reuse revokes the family. Logout and password changes revoke access. Inference checks session validity with Core.
- In the browser access JWT is held in memory, the refresh token is in an HttpOnly/SameSite cookie. Origin checks and login rate limits were added. Old tokens in localStorage are removed.
- Teachers can access students, photos and credential exports only for assigned groups. The bootstrap teacher becomes an administrator; the administrator manages assignments on the groups page. Exam creation checks group access.
- Observation and final integrity decisions use the version of the reviewed evidence. A stale decision receives 409; the interface allows refreshing evidence while preserving the explanation. New evidence changes the version.
- The production Qt client accepts only an HTTPS origin and prevents credential-bearing redirects to a different origin. HTTP is allowed only for loopback in DEV/tests. Windows protects saved queue tokens using DPAPI; other operating systems rely on file permissions; a system key store is not yet integrated.
- Nginx: trusted proxy address, HTTPS/HSTS, security headers, logging without query parameters. CSP is currently Report-Only. API/AI run without root and unnecessary capabilities. DEV and production use different default subnets.
- Migrations for production are a separate explicit step; startup fails with an outdated schema. Reusable Inference HTTP clients are closed during application shutdown.
- Dependencies were updated and vulnerability checks added to CI. Fixed loading of the IPA dictionary on Windows: UTF-8 is used explicitly instead of the system encoding.

## Checks and their limits

- Core Release builds; the monitor builds and passes 54 tests. Bundle/CSS size warnings remain in the monitor.
- Inference: 56 unit tests pass after dependency updates. They do not measure accuracy on students.
- After restoring Docker 29 Core contracts passed again (one DEV scenario is skipped in production), 3 group scenarios, 6 security scenarios and retention. Separately verified refusal to start production without a migration, explicit migration and real integration of Inference with JWKS/session revocation in Core. All 6 Qt tests passed on Linux; 56 tests for Inference passed in a clean Python 3.11 Linux environment. Two local HTTPS Nginx checks also passed.
- The audit of pinned Python dependencies and production dependencies in the monitor found no known vulnerabilities at the time of checking; the site audit was also clean. The monitor’s dev dependencies still have moderate advisories. This does not replace checking images and native libraries.
- The real phoneme model passes a smoke test of all test sequences; incorrect order, silence and noise are rejected. These are synthetic examples, not a measurement of accuracy on people.
- Real domain certificates, Windows installer signing, integration of AI↔Core on the live server have not been confirmed. Full Linux images of AI and the monitor built successfully; `pip check` in the AI image found no conflicts.

The complete isolated Docker stack passed `tests/check_docker_stack.py`: production migration, loading five verified model artifacts and the phoneme recognizer, UID 1654 for API/AI, HTTPS monitor, Secure/HttpOnly/SameSite cookie, refresh through trusted Nginx, real AI challenge and rejection of a revoked JWT. To run, first build the images `examguardian-core-contract:local`, `examguardian-ai-hardening:local`, `examguardian-monitor-hardening:local`. The test creates and removes only its own environment; live volumes are not used. This is a smoke test, not a load test or a measurement of recognition accuracy.

## Upgrade procedure

1. Finish active exams. Back up PostgreSQL, evidence, credential keys and configuration; verify restoration. The upgrade invalidates old JWT and refresh tokens and changes capability keys — it must not be performed during an exam.
2. Check `.env.production` against the example: independent random secrets of at least 32 characters, a real HTTPS origin and certificates `fullchain.pem`/`privkey.pem`. `JWT__KEY` is now the root capability secret, rather than an RSA access signing key.
3. Set unique `APP_NETWORK_SUBNET`/`PROXY_IP` that do not overlap with LAN/VPN or another environment. Trust only the address of Nginx. Do not expose ports for Core, AI and PostgreSQL externally.
4. Preserve the key volume: Core creates a private RSA PEM inside the directory for credential keys. Do not put it in git or share it with AI. Losing the file invalidates issued access JWT. Automatic rotation with overlapping keys is not yet implemented.
5. Check permissions of existing bind mounts/volumes after switching to user UID 1654: API must write evidence/credential keys, AI must write into its own outbox and `/home/app/.deepface`. Do not recursively run chown on an arbitrary directory; identify the actual mounted paths and adjust only those. Move the old root model cache to the new mount if necessary.
6. Stop request-serving instances before migration. Run `./scripts/Start-Environment.ps1 -Flavor production -Action migrate`, then `-Action start`. Test the migration on a restored database copy first.
7. Sign in again as the bootstrap administrator and assign groups to other teachers. Check login/logout, token refresh, access for different teachers, evidence upload and rejection of stale decisions. Update Qt clients; old Windows queue tokens will require a new login.
8. For rollback, use consistent backups of the code, database and keys. Simply reverting to an old image with the new schema/secrets is not a verified rollback.

## Next architecture phase

Keep Core as a modular monolith: separate authorization, roster, review and evidence boundaries through interfaces instead of splitting the server into microservices prematurely. Evidence storage should be separated from the file system by an adapter with an S3 option; make uploads and background processing idempotent. Inference currently has local state/SQLite and needs a separate queue and job-ownership design before horizontal scaling.

Multiple schools require a real SchoolId in all related resources, grants and queries; current groups do not provide tenant isolation. A separate administrative audit log, queue/error observability, automatic key rotation, installer signing and measurement of precision/recall on labeled real recordings with participant consent are needed. Concurrent cookie refresh in different tabs can currently revoke the refresh token family; coordination between tabs is required before broad rollout.

During an exam, show students only issues that require action and affect submission. AI observations remain with the teacher; an object, noise or process alone is not proof of a violation. Thresholds should change after measurement, not to make a test pass.
