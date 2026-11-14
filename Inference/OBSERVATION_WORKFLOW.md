# Quiet exam observations — 2026-11-14

Continuous camera analysis uses capture-time episodes (`core/episodes.py`).
Three fresh positive samples over at least two seconds open an episode; a normal
frame resets a candidate or closes an existing episode. Gaps over six seconds and
unusable facial images are unknown coverage, not confirmation of behavior.
Persistent poor facial quality needs ten seconds before a technical observation.
Objects remain independent of facial quality. Identity comparisons run only on
usable frontal frames; two failed checkpoints are needed for an identity episode.
These heuristic defaults need real-camera validation, not interpretation as
calibrated probabilities or proof of misconduct.

Each episode has one event UUID, capture interval, sample count and revision.
Updates are coalesced in the durable outbox, normally at most once per 15 seconds
plus closure. Core updates the same card, ignores stale revisions and reopens a
review when new episode evidence arrives. A last-observed timestamp does not
prove continuity between samples. If capture ends without another frame, the
episode retains its last observation rather than inventing an end time.

Exam audio uses a maximum ten-second per-attempt PCM window. The configured VAD
threshold means **total speech in that window**, not necessarily uninterrupted
speech. Capture gaps, overlaps and sample-rate changes cannot join unrelated
audio; windows are pruned after 60 seconds of inactivity by the delivery worker.
Qt performs filtered streaming resampling and retains up to two waiting five-
second audio batches; overflow/failures remain visible as timestamp gaps.

Student-facing proctoring alerts are disabled. Camera disconnection produces a
delayed actionable message; recording/save errors remain visible. Rules are
shown before admission. The examiner distinguishes observations, warnings and
intervention needs, separately from four review outcomes: detection error,
no violation, confirmed violation, insufficient evidence. No-violation outcomes
permit acceptance/export; insufficient evidence does not count as clearance.

Deploy Core and the examiner panel together before deploying the new Inference
episode producer; old Core versions ignore event updates. No schema migration is
needed for the new review strings or metadata. New SQLite tables are created on
use. Rebuild Qt to obtain the audio and student UI changes. Existing installers
are not changed by editing source or building a development executable.

Validation includes reducer/SQLite tests, synthetic audio resampling tests,
Qt screen fixtures, examiner UI tests and isolated Core HTTP contracts. These
establish behavior, not population-level recognition accuracy. Personal pose
calibration, enrollment quality checks and model comparisons remain subsequent
experiments requiring representative recordings.
