"""Offline evaluation on explicitly labelled local samples; never invent accuracy.

Each JSONL row: {"id":"anon-01", "frame":"frame.jpg", "expected":["face_missing"],
"reference":"reference.jpg", "same_person":false, "audio":"mono16.wav", "speech":false}.
Paths resolve against the manifest directory. Optional fields select subtests.
This measures frame classifiers, not the server's temporal incident aggregation.
"""
import argparse
import hashlib
import importlib.metadata
import json
import math
import platform
import time
import wave
from collections import defaultdict
from pathlib import Path


def metrics(pairs):
    tp=sum(actual and expected for actual, expected in pairs)
    fp=sum(actual and not expected for actual, expected in pairs)
    fn=sum(not actual and expected for actual, expected in pairs)
    tn=sum(not actual and not expected for actual, expected in pairs)
    return {"samples":len(pairs), "tp":tp, "fp":fp, "fn":fn, "tn":tn,
        "precision":tp/(tp+fp) if tp+fp else None,
        "recall":tp/(tp+fn) if tp+fn else None,
        "false_positive_rate":fp/(fp+tn) if fp+tn else None,
        "false_negative_rate":fn/(fn+tp) if fn+tp else None}


def percentile(values, fraction):
    return sorted(values)[max(0, math.ceil(len(values)*fraction)-1)] if values else None


def run(manifest, output):
    from api.deps import get_liveness_analyzer, get_identity_analyzer, get_object_detector, get_audio_analyzer
    from core.config import settings
    liveness, identity, objects, audio = get_liveness_analyzer(), get_identity_analyzer(), get_object_detector(), get_audio_analyzer()
    observations=defaultdict(list); timings=defaultdict(list); samples=[]
    rows=[json.loads(line) for line in manifest.read_text(encoding="utf-8-sig").splitlines() if line.strip()]
    if not rows: raise ValueError("A labelled dataset is required")
    hashes={}; identifiers=set()
    def read_file(name):
        path=(manifest.parent/name).resolve()
        data=path.read_bytes(); hashes[name]=hashlib.sha256(data).hexdigest(); return data
    try:
        for row in rows:
            sample_id=row["id"]
            if sample_id in identifiers: raise ValueError("Duplicate anonymous sample identifier")
            identifiers.add(sample_id)
            result={"id":sample_id}
            if "frame" in row:
                frame=read_file(row["frame"])
                if "expected" in row:
                    start=time.perf_counter(); pose=liveness.analyze_continuous(frame); detected=objects.detect(frame)
                    timings["frame"].append((time.perf_counter()-start)*1000)
                    predicted={v["type"] for v in pose.get("violations",[])} | {v["type"] for v in detected.get("violations",[])}
                    predicted={ {"cell phone":"cell_phone", "extra_person":"multiple_faces"}.get(v,v) for v in predicted }
                    expected=set(row["expected"])
                    for label in {"face_missing","multiple_faces","looking_away_left","looking_away_right","looking_down","cell_phone","book","laptop"} | expected | predicted:
                        observations[label].append((label in predicted,label in expected))
                    result.update(expected=sorted(expected),predicted=sorted(predicted))
                if "same_person" in row:
                    if type(row["same_person"]) is not bool: raise ValueError("same_person must be a boolean")
                    start=time.perf_counter(); comparison=identity.compare(read_file(row["reference"]),frame)
                    timings["identity"].append((time.perf_counter()-start)*1000)
                    observations["identity_match"].append((bool(comparison["match"]),row["same_person"]))
                    result["identity"]=comparison
            if "audio" in row:
                read_file(row["audio"])
                with wave.open(str(manifest.parent/row["audio"]),"rb") as recording:
                    if recording.getnchannels()!=1 or recording.getsampwidth()!=2 or recording.getframerate()!=16000:
                        raise ValueError("Audio samples must be mono PCM16 at 16000 Hz")
                    pcm=recording.readframes(recording.getnframes())
                start=time.perf_counter(); vad=audio.analyze(pcm); timings["audio"].append((time.perf_counter()-start)*1000)
                if type(row.get("speech")) is not bool: raise ValueError("speech must be labelled as a boolean")
                observations["speech"].append((vad["speech_detected"],row["speech"]))
                result["audio"]=vad
            samples.append(result)
    finally:
        liveness._detector.close()
    weights={}
    for directory in [Path("."),Path.home()/".deepface/weights"]:
        for pattern in ["*.pt","*.pth","*.h5","*.task"]:
            for weight in directory.glob(pattern):
                with weight.open("rb") as f: weights[weight.name]=hashlib.file_digest(f,"sha256").hexdigest()
    report={"dataset_sha256":hashlib.sha256(manifest.read_bytes()).hexdigest(),"sample_hashes":hashes,
        "settings":{k:getattr(settings,k) for k in ["identity_threshold","yaw_threshold","pitch_threshold","yolo_confidence","vad_aggressiveness","vad_threshold_seconds"]},
        "environment":{"python":platform.python_version(),"platform":platform.platform(),"packages":{p:importlib.metadata.version(p) for p in ["deepface","mediapipe","ultralytics","tensorflow"]}},
        "weights":weights,"metrics":{label:metrics(pairs) for label,pairs in observations.items()},
        "latency_ms":{kind:{"count":len(values),"p50":percentile(values,.5),"p95":percentile(values,.95),"max":max(values)} for kind,values in timings.items()},
        "samples":samples,"scope":"Offline frame classification; not full liveness attack resistance or concurrent HTTP performance."}
    output.parent.mkdir(parents=True,exist_ok=True); output.write_text(json.dumps(report,indent=2,ensure_ascii=False),encoding="utf-8")


if __name__ == "__main__":
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("manifest",type=Path); parser.add_argument("--output",type=Path,required=True)
    args=parser.parse_args(); run(args.manifest.resolve(),args.output.resolve())
