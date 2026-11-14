from fastapi import Header
from api.auth import acknowledge_analysis
import os
import cv2
import numpy as np
from pathlib import Path
from core.outbox import should_emit
from core.episodes import observe, face_observations, VIDEO_TYPES
from core.challenges import frontal
from fastapi import HTTPException
from api.deps import get_identity_analyzer
from api.deps import analysis_slot
from api.auth import get_current_user, require_active_session
# api/routes/frame.py
from fastapi import APIRouter, File, UploadFile, BackgroundTasks, Depends, Query
from fastapi.concurrency import run_in_threadpool
from api.deps import get_liveness_analyzer, get_object_detector
from core.observation_policy import apply_policy
from models.liveness_analyzer import LivenessAnalyzer
from models.object_detector   import ObjectDetector

router = APIRouter()

@router.post("/analyze/frame")
async def analyze_frame(
    background_tasks: BackgroundTasks,
    session_token: str = Header(...,alias="X-Session-Token"),
    user_info: dict = Depends(get_current_user),
    _slot=Depends(analysis_slot),
    student_id: str | None = Query(default=None),
    session_id: int = Query(..., gt=0),
    relative_ts: int | None = Query(default=0),
    file: UploadFile = File(...),
    liveness: LivenessAnalyzer = Depends(get_liveness_analyzer),
    detector: ObjectDetector   = Depends(get_object_detector),
    identity=Depends(get_identity_analyzer),
):
    session = await require_active_session(user_info, session_id, session_token)
    if relative_ts is None or relative_ts < 0 or relative_ts > 86400: raise HTTPException(400, "Invalid capture time")
    contents = await file.read(4 * 1024 * 1024 + 1)
    if len(contents) > 4 * 1024 * 1024: raise HTTPException(413, "Frame too large")
    try: decoded = cv2.imdecode(np.frombuffer(contents, np.uint8), cv2.IMREAD_COLOR)
    except cv2.error: raise HTTPException(400, "Invalid image dimensions")
    if decoded is None or max(decoded.shape[:2])>4096: raise HTTPException(400, "Invalid image")

    # Shared models execute sequentially; observations are not misconduct verdicts.
    face_analysis = await run_in_threadpool(liveness.analyze_continuous, contents)
    objects       = await run_in_threadpool(detector.detect, contents)

    violations: list[dict] = []

    # 1. Порушення обличчя / погляду / мікронавушників
    violations.extend(face_observations(face_analysis))

    # 2. Об'єктні порушення: телефон, додаткова особа тощо
    for obj_v in objects.get("violations", []):
        v_type = obj_v["type"]
        if v_type == "cell phone":
            v_type = "cell_phone"

        violations.append({
            "type": v_type,
            "confidence": obj_v.get("confidence", 1.0),
            "metadata": {"desc": f"Виявлено об'єкт: {obj_v['type']}; потрібна перевірка контексту",
                         "bbox": obj_v.get('bbox'), "source": "object_detector",
                         "rule": "object_not_allowed" if v_type in ('cell_phone', 'book', 'laptop') else "review_context"}
        })

    violations = apply_policy(violations, session)
    # Periodic identity matching is separate from face presence.
    reference = Path(os.environ.get("INFERENCE_IMAGES_PATH", "images")) / "references" / ((session.get("referenceId") or "missing") + ".jpg")
    evaluated = set(VIDEO_TYPES)
    unknown = set()
    if face_analysis.get('quality_reason') not in (None, 'no_face', 'multiple_faces'):
        unknown = {'face_missing', 'multiple_faces', 'looking_down', 'looking_away_left', 'looking_away_right', 'identity_uncertain'}
        evaluated -= unknown
    if not face_analysis.get('quality_reason') and face_analysis.get('has_face') and frontal(face_analysis) and should_emit(session_id,"identity_check"):
        evaluated.add('identity_uncertain')
        match = await run_in_threadpool(identity.compare, reference.read_bytes(), contents) if reference.is_file() else {"match":False,"reason":"reference_missing"}
        if not match.get("match"):
            violations.append({"type": "identity_uncertain", "confidence": 0.0, "metadata": {"distance": match.get("distance"), "reason": match.get("reason"), "score_kind": "uncalibrated"}})
    previews = Path(os.environ.get("INFERENCE_IMAGES_PATH","images")) / "previews"
    previews.mkdir(parents=True,exist_ok=True)
    target_preview = previews / f"{session_id}.jpg"
    temporary_preview = target_preview.with_suffix(".upload")
    temporary_preview.write_bytes(cv2.imencode(".jpg",decoded)[1].tobytes()); temporary_preview.replace(target_preview)
    # Persist frame and enqueue observations before acknowledging this frame.

    def evidence_writer(v):
        event_id = v['event_id']
        evidence_dir = Path(os.environ.get("INFERENCE_IMAGES_PATH", "images")) / "evidence"
        evidence_dir.mkdir(parents=True, exist_ok=True)
        encoded = cv2.imencode(".jpg", decoded)[1].tobytes()
        target = evidence_dir / (event_id + ".jpg")
        temporary = target.with_suffix(".upload")
        if not target.exists():
            temporary.write_bytes(encoded); temporary.replace(target)
        return f"/api/violations/evidence/{event_id}"
    episodes = await run_in_threadpool(observe, session_id, violations, relative_ts, evaluated, unknown, evidence_writer)

    await acknowledge_analysis(user_info,session_id,"camera")
    return {
        "violation":    len(episodes) > 0,
        "violations":   episodes,
        "person_count": objects.get("person_count", 0),
        "has_face":     face_analysis.get("has_face", False),
        "yaw":          face_analysis.get("yaw", 0.0),
        "pitch":        face_analysis.get("pitch", 0.0),
        "relative_ts":  relative_ts,
        "status":       "processed",
    }
