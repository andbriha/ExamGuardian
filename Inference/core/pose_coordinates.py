"""Head direction in the subject's frame, from an unmirrored camera image."""
def anatomical_yaw(nose_x: float, edge_a_x: float, edge_b_x: float) -> float:
    # Subject-left projects toward image-right: negative yaw is anatomical left.
    return ((edge_a_x + edge_b_x) / 2.0 - nose_x) / max(abs(edge_a_x - edge_b_x), 0.05)


def relative_pitch(nose_y: float, forehead_y: float, chin_y: float) -> float:
    """Face-height units: moving towards the camera does not mimic a nod."""
    return (nose_y - (forehead_y + chin_y) / 2) / max(abs(chin_y - forehead_y), .05)
