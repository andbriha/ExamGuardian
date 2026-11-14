"""Model-independent temporal gesture checks. Coordinates are subject-relative."""
import math
import secrets

TASKS = ("turn_left", "turn_right", "nod", "look_up", "smile", "open_mouth", "close_eyes")
FEATURES = ("yaw", "gesture_pitch", "smile", "mouth", "eyes_closed")


def frontal(observation, task=None):
    """A natural resting face need not have a zero smile blendshape."""
    limits = {"yaw": .16, "eyes_closed": .5, "mouth": .4}
    if task == "smile": limits["smile"] = .5
    return all(isinstance(observation.get(k), (int, float)) and math.isfinite(observation[k])
               and abs(observation[k]) < limit for k, limit in limits.items())


def new_sequence():
    # At least one pose and one expression; no duplicate task within a challenge.
    rng = secrets.SystemRandom()
    tasks = [rng.choice(TASKS[:4]), rng.choice(TASKS[4:])]
    tasks.append(rng.choice([task for task in TASKS if task not in tasks]))
    rng.shuffle(tasks)
    return tasks


def advance(state, task, observation, now):
    """Mutates JSON state; completion requires neutral -> sustained target -> neutral."""
    values = {key: observation.get(key) for key in FEATURES}
    if any(not isinstance(v, (int, float)) or not math.isfinite(v) for v in values.values()):
        state.clear()
        return False, "no_face"
    if now - state.get("last_seen", now) > 5:
        state.clear()
    state["last_seen"] = now
    baseline = state.get("baseline")
    if baseline is None:
        neutral = frontal(values, task)
        if not neutral:
            state.pop("samples", None)
            return False, "look_forward_first"
        samples = state.setdefault("samples", [])
        samples.append(values)
        if len(samples) < 3:
            return False, "look_forward_first"
        # Blendshape scores fluctuate more than pose even in a relaxed face.
        tolerances = {"yaw": .08, "gesture_pitch": .06, "smile": .18, "mouth": .15, "eyes_closed": .18}
        relevant = ("yaw", "gesture_pitch", {"smile": "smile", "open_mouth": "mouth", "close_eyes": "eyes_closed"}.get(task, "yaw"))
        if any(max(s[k] for s in samples) - min(s[k] for s in samples) > tolerances[k] for k in relevant):
            state["samples"] = [values]
            return False, "look_forward_first"
        state["baseline"] = {k: sum(s[k] for s in samples) / len(samples) for k in FEATURES}
        state.pop("samples", None)
        return False, "perform_action"
    delta = {k: values[k] - baseline[k] for k in FEATURES}
    targets = {
        "turn_left": delta["yaw"] < -.10,
        "turn_right": delta["yaw"] > .10,
        "nod": delta["gesture_pitch"] > .07,
        "look_up": delta["gesture_pitch"] < -.07,
        "smile": values["smile"] > .5 and delta["smile"] > .25,
        "open_mouth": values["mouth"] > .4 and delta["mouth"] > .25,
        "close_eyes": values["eyes_closed"] > .65 and delta["eyes_closed"] > .4,
    }
    if state.get("phase") != "return":
        if not targets.get(task, False):
            # One noisy sample must not erase a sustained movement.
            if now - state.get("target_last_seen", -10) > .2:
                state.pop("target_at", None)
                state["target_count"] = 0
            opposite = (task == "turn_left" and delta["yaw"] > .1) or (task == "turn_right" and delta["yaw"] < -.1)
            return False, "opposite_direction" if opposite else "perform_action"
        state.setdefault("target_at", now)
        state["target_last_seen"] = now
        state["target_count"] = state.get("target_count", 0) + 1
        if state["target_count"] >= 2 and now - state["target_at"] >= .25:
            state["phase"] = "return"
        return False, "return_neutral" if state.get("phase") == "return" else "hold_pose"
    pitch_return = .05 if task in ("nod", "look_up") else .08
    neutral = (abs(delta["yaw"]) < .09 and abs(delta["gesture_pitch"]) < pitch_return
               and values["eyes_closed"] < max(.4, baseline["eyes_closed"] + .15)
               and values["mouth"] < max(.3, baseline["mouth"] + .15))
    if task == "smile": neutral = neutral and values["smile"] < max(.4, baseline["smile"] + .15)
    if not neutral:
        state.pop("return_at", None)
        state["return_count"] = 0
        return False, "return_neutral"
    state.setdefault("return_at", now)
    state["return_count"] = state.get("return_count", 0) + 1
    return state["return_count"] >= 2 and now - state["return_at"] >= .25, "return_neutral"
