# api/routes/frame.py
from fastapi import APIRouter, File, UploadFile, BackgroundTasks, Depends, Query
from fastapi.concurrency import run_in_threadpool
from api.deps import get_liveness_analyzer, get_object_detector
from core.notifier import notify_violation
from models.liveness_analyzer import LivenessAnalyzer
from models.object_detector   import ObjectDetector

router = APIRouter()

@router.post("/analyze/frame")
async def analyze_frame(
    background_tasks: BackgroundTasks,
    student_id: str | None = Query(default=None),
    session_id: int | None = Query(default=1),
    relative_ts: int | None = Query(default=0),
    file: UploadFile = File(...),
    liveness: LivenessAnalyzer = Depends(get_liveness_analyzer),
    detector: ObjectDetector   = Depends(get_object_detector),
):
    contents = await file.read()

    # Паралельний аналіз: міміка/погляд/вуха (MediaPipe) + об'єкти/телефон/люди (YOLOv8)
    face_analysis = await run_in_threadpool(liveness.analyze_continuous, contents)
    objects       = await run_in_threadpool(detector.detect, contents)

    violations: list[dict] = []

    # 1. Порушення обличчя / погляду / мікронавушників
    violations.extend(face_analysis.get("violations", []))

    # 2. Об'єктні порушення: телефон, додаткова особа тощо
    for obj_v in objects.get("violations", []):
        v_type = obj_v["type"]
        if v_type == "cell phone":
            v_type = "cell_phone"
        elif v_type == "extra_person":
            v_type = "multiple_faces"

        violations.append({
            "type": v_type,
            "confidence": obj_v.get("confidence", 1.0),
            "metadata": {"desc": f"Виявлено заборонений об'єкт: {obj_v['type']}"}
        })

    # Відправка порушень на Core backend з точним таймкодом
    for v in violations:
        background_tasks.add_task(
            notify_violation,
            student_id=0,
            session_id=session_id or 1,
            violation_type=v["type"],
            confidence=v.get("confidence", 1.0),
            relative_ts=relative_ts or 0,
            metadata=v.get("metadata"),
        )

    return {
        "violation":    len(violations) > 0,
        "violations":   violations,
        "person_count": objects.get("person_count", 0),
        "has_face":     face_analysis.get("has_face", False),
        "yaw":          face_analysis.get("yaw", 0.0),
        "pitch":        face_analysis.get("pitch", 0.0),
        "relative_ts":  relative_ts,
        "status":       "processed",
    }