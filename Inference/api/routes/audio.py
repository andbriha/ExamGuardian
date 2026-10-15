from fastapi import Header
from api.auth import acknowledge_analysis
from fastapi import HTTPException
from core.outbox import should_emit
from api.deps import audio_slot
from api.auth import get_current_user, require_active_session
from fastapi import APIRouter, File, UploadFile, BackgroundTasks, Depends, Query
from fastapi.concurrency import run_in_threadpool
from api.deps import get_audio_analyzer
from core.notifier import notify_violation
from models.audio_analyzer import AudioAnalyzer
from models.speech_analyzer import get_speech_analyzer
import logging

router = APIRouter()

@router.post("/analyze/audio")
async def analyze_audio(
    background_tasks: BackgroundTasks,
    session_token: str = Header(...,alias="X-Session-Token"),
    user_info: dict = Depends(get_current_user),
    _slot=Depends(audio_slot),
    student_id:  str | None = Query(default=None),
    session_id:  int = Query(..., gt=0),
    relative_ts: int | None = Query(default=0),
    sample_rate: int = Query(default=16000),
    file: UploadFile = File(...),
    analyzer: AudioAnalyzer = Depends(get_audio_analyzer),
):
    session = await require_active_session(user_info, session_id, session_token)
    if sample_rate not in (8000,16000,32000,48000) or relative_ts is None or relative_ts < 0 or relative_ts > 86400: raise HTTPException(400, "Invalid audio format or time")
    pcm_bytes = await file.read(2 * 1024 * 1024 + 1)
    if len(pcm_bytes) > sample_rate*2*10 or len(pcm_bytes) % 2: raise HTTPException(400, "Invalid PCM payload")
    result    = await run_in_threadpool(analyzer.analyze, pcm_bytes, sample_rate)

    if session.get("allowSpeech",False): result["violation"] = False
    if result.get("violation") and should_emit(session_id, "voice_activity"):
        metadata = {"speech_seconds": result.get("speech_seconds", 0),
                    "review_required": True, "speaker_identified": False,
                    "duration_seconds": len(pcm_bytes)/(sample_rate*2),
                    "desc": "Виявлено мовлення. Це не доказ підказки чи стороннього голосу: викладач має прослухати запис і перевірити контекст."}
        speech = await run_in_threadpool(get_speech_analyzer)
        if speech.available:
            try:
                recognized = await run_in_threadpool(speech.transcribe, pcm_bytes, sample_rate)
                metadata.update(transcript=recognized["transcript"], transcription_confidence=recognized["confidence"])
            except Exception:
                logging.getLogger(__name__).exception("Incident transcription unavailable")
                metadata["transcription_unavailable"] = True
        await notify_violation(
            student_id=0,
            session_id=session_id,
            violation_type="voice_activity",
            confidence=0.0,  # VAD supplies no probability of misconduct.
            relative_ts=relative_ts or 0,
            metadata=metadata,
        )

    await acknowledge_analysis(user_info,session_id,"audio")
    return result
