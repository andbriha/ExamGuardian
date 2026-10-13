from fastapi import APIRouter, File, UploadFile, BackgroundTasks, Depends, Query
from fastapi.concurrency import run_in_threadpool
from api.deps import get_audio_analyzer
from core.notifier import notify_violation
from models.audio_analyzer import AudioAnalyzer

router = APIRouter()

@router.post("/analyze/audio")
async def analyze_audio(
    background_tasks: BackgroundTasks,
    student_id:  str | None = Query(default=None),
    session_id:  int | None = Query(default=1),
    relative_ts: int | None = Query(default=0),
    sample_rate: int = Query(default=16000),
    file: UploadFile = File(...),
    analyzer: AudioAnalyzer = Depends(get_audio_analyzer),
):
    pcm_bytes = await file.read()
    result    = await run_in_threadpool(analyzer.analyze, pcm_bytes, sample_rate)

    if result.get("violation"):
        background_tasks.add_task(
            notify_violation,
            student_id=0,
            session_id=session_id or 1,
            violation_type="voice_activity",
            confidence=result.get("confidence", 1.0),
            relative_ts=relative_ts or 0,
            metadata={"speech_seconds": result.get("speech_seconds", 0), "desc": "Виявлено сторонній голос або розмову під час іспиту"},
        )

    return result