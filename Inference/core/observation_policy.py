"""Apply the exam's frozen policy to candidate observations."""
OBJECT_TYPES = frozenset({'book', 'laptop', 'cell_phone'})


def apply_policy(observations, session):
    allowed = OBJECT_TYPES.intersection(session.get('allowedObjects') or [])
    return [v for v in observations if v['type'] not in allowed
            and (session.get('flagHeadMovement', True) or not v['type'].startswith('looking_'))]
