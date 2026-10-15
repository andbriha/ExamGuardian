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
proctoring retains its existing pitch units; `gesture_pitch` is for calibration
and challenge transitions. Left/right refer to the participant, using unmirrored
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

Protocol 3 clients receive a pose, an expression, and a random three-word
Ukrainian phrase when the speech model is available. Protocol 2 keeps three
visual gestures. Missing ASR falls back to visual verification at challenge
creation; an issued speech task never passes when recognition is unavailable.

Install `requirements.txt` and run `python download_speech_model.py` from
`Inference` before starting locally or using the development bind mount. The
Docker build provisions the same model automatically. `SPEECH_MODEL_PATH` can
override its location. The model is Vosk's Apache-2.0 Ukrainian small model:
https://alphacephei.com/vosk/models . No audio is sent to a cloud ASR service.

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

Speech verification checks exact word order and minimum word confidence, with
a bounded six-second PCM window to bridge microphone chunks. That window is
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
