# Verification engine

The Qt client and inference service must be updated together (challenge protocol 3;
the server retains protocol 2 for older clients).
`GET /verify/capabilities` exposes the protocol and optional features without
authentication. `scripts/Start-Client.ps1` checks this endpoint before launch.
The service selects three distinct actions using the system random generator, with
at least one head movement and one facial expression. Supported actions: own-left,
own-right, nod down, chin up, smile, open mouth, close and reopen both eyes.

Each action starts with three stable neutral samples, requires a sustained target
on at least two different frames, and ends with two neutral samples. The next
action is revealed only after completion. Admission is issued after all three.
The sequence expires after four minutes. Identity and anti-spoof checks remain
required at frontal checkpoints. Brief uncertainty pauses or preserves motion as
described below; a gap over five seconds requires recalibration. Completed actions persist.

- `core/challenges.py`: pure temporal state machine and randomized selection.
- `core/pose_coordinates.py`: anatomical yaw and scale-normalized gesture pitch.
- `models/frame_quality.py`: exposure/blur feedback and bounded luminance correction.
- `models/liveness_analyzer.py`: MediaPipe adapter, face count, blendshape scores.
- `api/routes/verify_session.py`: authenticated, persistent challenge lifecycle.

Raw camera frames remain the input to identity/anti-spoofing. Luminance correction
is only for landmarks; extreme darkness/overexposure is reported to the user.
Quality is measured on the face crop when one face is detected. Continuous
proctoring and `gesture_pitch` both use face-height-normalized pitch.
`PITCH_THRESHOLD` (default 0.17) is now in face-height units, not image-height
units; reassess any deployment-specific override against representative footage. Left/right refer to the participant, using unmirrored
camera uploads. A C++ timer attempts a frame every 125 ms with one request in flight;
actual sampling speed depends on inference latency.
There is no manual frame-submission button. Capture stops on success, terminal
error, navigation or cancellation; stale network callbacks cannot restart a previous
challenge. Three temporary network/capacity failures are retried automatically.

During verification the microphone sends one-second PCM16/16kHz mono chunks to
`POST /verify/audio`, bound to the authenticated student's unexpired challenge.
The endpoint returns voice activity and, during a speech task, phrase recognition;
it neither grants admission by itself nor reports an exam violation. A bounded
temporary PCM window is stored with the speech challenge. The client
shows microphone/voice status separately from gesture instructions. Once the exam
starts, the existing continuous audio analysis and recording workflow takes over.

SQLite adds sequence, step and motion columns in place and keeps existing rows.
Frame digests are retained for the challenge lifetime to reject exact repeats.
This does not establish resistance to re-encoded replay or generated video.
Thresholds require evaluation on labelled real-camera sessions across lighting,
camera distance, glasses, skin tones and facial mobility. State-machine tests
verify protocol behavior, not biometric accuracy. Speech recognition verifies
phrase content but cannot bind that voice to the visible face. Audiovisual replay
resistance needs separate evaluation; this is not voice biometrics.

Run lightweight protocol tests from `Inference` with:

```powershell
.\venv\Scripts\python.exe -m unittest discover -s tests -v
```

They use mocked biometric outputs and do not need model downloads. Dependencies
for a separate test environment are in `requirements-test.txt`.
## Verification and speech update (2026-10-15)

Protocol 4 clients receive a pose, an expression, and a five-syllable sequence
from MA /ma/, MI /mi/, MU /mu/ when the phoneme model is available.
There are 42 sequences: all three sounds appear, and neighbors differ.
These are sounds, not letter names or translated words. The Qt client includes
fixed pronunciation samples and a Listen button. Microphone capture and uploads
pause during playback and for 500 ms afterwards; the next upload resets the
server audio window. Protocols 2/3 keep three visual gestures.
Missing phoneme recognition falls back to visual verification at challenge
creation; an issued speech task never passes when recognition is unavailable.

Install dependencies and run `python download_phoneme_model.py` from `Inference`
before starting locally or using the development bind mount. Docker provisions
the model during build. `PHONEME_MODEL_PATH` overrides its directory. Restart AI
after provisioning a previously missing model (availability is cached).
The model is [Meta wav2vec2 espeak CommonVoice](https://huggingface.co/facebook/wav2vec2-lv-60-espeak-cv-ft),
Apache-2.0, pinned revision `ae45363bf3413b374fecd9dc8bc1df0e24c3b7f4`, about
1.3 GB of weights plus runtime memory. Input is PCM16 mono at 16 kHz.
No student audio is sent to a cloud recognizer. Ukrainian Vosk remains separate,
for optional exam transcripts (`python download_speech_model.py`,
`SPEECH_MODEL_PATH`); it is no longer used for admission challenges.

Identity embeddings for reference photos are cached by content (64 entries).
The biometric distance threshold is unchanged. A successful identity/anti-spoof
checkpoint opens a 30-second motion window. Intermediate frames run landmarks
and quality checks without repeating DeepFace. Missing/multiple faces or a gap
over five seconds invalidate the window. A brief exposure/blur pause may last
up to two seconds. Verification, continuous vision, and audio use separate bounded
queues so ASR/YOLO cannot monopolize the gesture queue.
Each completed task still requires a fresh frontal identity and anti-spoof check;
motion alone, speech alone, and replayed frames cannot issue admission.
This is checkpoint identity continuity, not biometric identification on every
frame; substitution between checkpoints remains a limitation to evaluate.
Responses report `processing_ms` and `identity_checkpoint`; the client logs
round-trip latency without storing images. The target rate is 8 fps, not a
guarantee under server load. Camera uploads are bounded to 960 pixels per side.

Speech verification checks phoneme order and grouped phoneme confidence, with
a bounded ten-second PCM window to bridge microphone chunks. That window is
cleared on completion or identity reset; expired challenges are pruned on new
challenge creation. This is phrase recognition, not a voice biometric or proof
that the visible person spoke. Recorded/replayed audio remains a limitation.

During exams, the existing `allowSpeech` policy suppresses speech alerts.
Otherwise sustained voice activity can create a pending review signal, with an
optional transcript and a recording timestamp. Neither VAD nor ASR identifies
an outside speaker or proves coaching. The examiner must listen to the original
recording and confirm or dismiss the signal with context. ASR text can be wrong.

Validation: `venv/Scripts/python -m unittest discover -s tests -v`; Qt target
`verification_capture_test`; Angular `npm run build`. These verify behavior,
not population-level biometric accuracy. Test the deployed camera/microphone
under representative lighting and with genuine/impostor users before making
accuracy or anti-spoofing claims.

## Verification usability update (2026-10-24)

The speech task uses a three-sound alphabet in 42 five-syllable sequences.
`core/syllable_challenges.py` owns the alphabet, generation and matching;
`models/phoneme_analyzer.py` owns recognition. The expected phrase is never fed
into the recognizer. Short/long vowel variants and narrow aliases (ɑ/ɐ → a,
ɪ → i, ʊ → u) are grouped before CTC decoding. Each matched phoneme needs a
model score of at least 0.65. Complete retries are allowed; partial sequences,
reordered consonants and unrelated sounds fail. Scores are not calibrated
probabilities of identity. These sequences are a usability task, not a
high-entropy authentication secret; camera checkpoints remain mandatory.

Run `python -m unittest discover -s tests` for contracts. The optional real-model
check `python -m tests.check_phoneme_model` uses the bundled reference samples:
all six legacy orders and all 42 new sequences, wrong-sequence rejection, silence
and noise. The synthetic samples include 500 ms pauses as in the Qt playback. Set
`PRONUNCIATION_SAMPLES` when Client assets are not beside Inference (e.g. Docker).
For CPU deployments start with `OMP_NUM_THREADS=2` and measure under expected
concurrency. Synthetic checks do not establish accuracy across accents, speech
impairments or microphones. Fixed samples define the intended pronunciation;
Latin spelling alone cannot guarantee identical pronunciation in every language.
This change does not translate the UI or internationalize exam transcription.

The Qt audio queue retains up to ten seconds while an upload is in flight and drains in batches of at most two seconds. Overflow or upload failure marks a discontinuity; the next upload explicitly resets the server PCM buffer instead of joining unrelated fragments. New tasks and cancellation clear the client queue. Recognition results from a previous task are ignored. The UI shows recognized speech separately from the camera instructions.

A natural resting smile no longer blocks head-motion calibration. A target needs at least two fresh samples over 0.25 seconds, followed by a real return. One short noisy sample does not immediately erase a hold. Wrong-direction turns have a specific hint. Calibration, holding, returning and completed-step progress are shown independently of the task text.

The final live-face checkpoint of a completed action also anchors the next action; it is not immediately repeated. The next action still calibrates a new baseline and requires its own final checkpoint. A successful renewal of an expired anchor preserves a continuously observed movement, while missing/multiple faces and failed identity renewal clear it. Server inference time does not count as a gap in incoming camera frames. The biometric comparison and passive anti-spoof thresholds are unchanged.

These fixes are covered by simulated motion, audio queue and admission-contract tests. Actual recognition quality still needs people repeating the syllables and performing gestures on their camera and microphone.


## Inference review and concurrency (2026-10-25)

- **Capacity:** a semaphore with one permit serializes work; it does not reject a second student immediately. Separate analysis/verification/audio queues now bound waiting requests (default 16 per queue), expose active/waiting/rejected counts in `/health/ready`, and return `429` with `Retry-After: 1` on overload. `INFERENCE_QUEUE_LIMIT` (1�256) and `INFERENCE_WAIT_SECONDS` (>0�30, default 5) are available in Compose and native settings. Cancellation and model exceptions release capacity. Model execution remains serialized within each lane: raising concurrency on shared mutable models is not a throughput solution. Queue tuning does not establish classroom capacity. Measure latency on target hardware before choosing worker replicas; multiple processes also duplicate model memory and share challenge/media state.
- **Reference cache:** byte content is the key. Changed enrollment bytes are computed immediately; an older cached entry cannot override the new one. An already issued challenge/attempt intentionally keeps its frozen reference. An explicit cache regression test covers new content without restart.
- **SQLite:** `core/verification_store.py` centralizes connection lifetime, rollback and lock handling. Runtime lock waits are capped at 50 ms so synchronous SQLite cannot stall the async loop for the old default five seconds. Contention returns retryable `503`; short `BEGIN IMMEDIATE` transactions and post-inference state checks remain. Audio revalidation uses the same transaction rather than a second connection. Connections never cross threads, so `check_same_thread=False` is unnecessary. This is bounded contention handling, not distributed database scaling.
- **WAL caveat:** the inspected container links SQLite 3.46.1. WAL is not enabled as part of this patch: [SQLite documents a concurrent WAL-reset corruption bug](https://www.sqlite.org/wal.html#walreset) fixed in 3.51.3 (and selected vendor backports). Upgrade/verify the linked SQLite before considering WAL. WAL still permits only one writer and can return busy errors. Keep the state database on local persistent storage; multi-host replication needs a separate shared-state design.
- **Session authority:** Inference validates the user JWT locally, then Core validates the attempt capability, ownership, active client and deadline. Core is authoritative for revocation/finished attempts. A Core outage returns `503` and never grants analysis access based only on a signature. Tests cover token forwarding, rejection and outages.
- **Model startup:** the optional phoneme model now loads before readiness, moving first-use latency out of a student's challenge request. Availability appears in readiness. Missing weights retain the documented visual fallback for newly issued challenges, not an automatic speech pass. The load lock remains to prevent duplicate large allocations.
- **Object policy:** the detector proposes observations, and `core/observation_policy.py` applies the frozen exam policy. Allowed books/laptops do not become incidents. Defaults still reflect a closed-book exam; select allowances in the builder. Face/identity observations cannot be suppressed through an arbitrary allowed-object name. Detector labels no longer describe every detected object as forbidden.
- **Pitch:** continuous downward-head observations now use face-height normalization like gesture calibration. Tests exercise the actual adapter at two face sizes. Thresholds remain heuristic review signals, not proof of cheating; camera position and individual anatomy need field calibration.
- **Incident identity:** removed unused `student_id=0` arguments. The outbox already stored `examSessionId`, not a zero student ID; Core resolves the student from that attempt.
- **Speech challenge:** expanded from six permutations to 42 varied sequences while retaining the same bundled pronunciation samples and protocol 4. Avoid monotonous adjacent repeats: real-model checks showed poorer recognition on long uniform runs. The recognizer receives no expected phrase and thresholds are unchanged. A short right-context silence pad helps chunk-boundary decoding; missing phonemes are never fabricated. Synthetic full-sequence checks pass, but replayed/spliced speech remains possible; this is not voice biometrics or a substitute for live face checkpoints.

Validation: 45 unit/contract tests; real phoneme model checks of 6 legacy and 42 current sequences, wrong sequences, silence and noise. These do not replace concurrent real-device classroom tests or population-level accuracy measurements.
