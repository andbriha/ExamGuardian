import unittest
from core.challenges import TASKS, advance, new_sequence


class Gestures(unittest.TestCase):
    def neutral(self):
        return dict(yaw=0., gesture_pitch=0., smile=0., mouth=0., eyes_closed=0.)

    def calibrated(self):
        state = {}
        for now in (0., .5, 1.):
            self.assertFalse(advance(state, "smile", self.neutral(), now)[0])
        return state

    def test_every_task_requires_hold_and_return(self):
        targets = {"turn_left": ("yaw", -.15), "turn_right": ("yaw", .15),
                   "nod": ("gesture_pitch", .1), "look_up": ("gesture_pitch", -.1),
                   "smile": ("smile", .8), "open_mouth": ("mouth", .8),
                   "close_eyes": ("eyes_closed", .9)}
        for task, (key, value) in targets.items():
            with self.subTest(task=task):
                state = self.calibrated()
                target = {**self.neutral(), key: value}
                self.assertFalse(advance(state, task, target, 1.5)[0])
                self.assertEqual(advance(state, task, target, 2.), (False, "return_neutral"))
                self.assertFalse(advance(state, task, target, 2.5)[0])
                self.assertFalse(advance(state, task, self.neutral(), 3.)[0])
                self.assertTrue(advance(state, task, self.neutral(), 3.5)[0])

    def test_neutral_and_wrong_action_never_complete(self):
        state = self.calibrated()
        for now in range(2, 30):
            self.assertFalse(advance(state, "turn_left", {**self.neutral(), "yaw": .2}, now)[0])
        self.assertNotEqual(state.get("phase"), "return")

    def test_natural_short_turn_is_captured_at_eight_fps(self):
        state = {}
        for now in (0., .125, .250):
            advance(state, "turn_left", self.neutral(), now)
        target = {**self.neutral(), "yaw": -.15}
        for now in (.375, .5, .625, .75):
            complete, reason = advance(state, "turn_left", target, now)
            self.assertFalse(complete)
        self.assertEqual(reason, "return_neutral")
        for now in (.875, 1., 1.125):
            complete, _ = advance(state, "turn_left", self.neutral(), now)
        self.assertTrue(complete)

    def test_interrupted_target_requires_new_hold(self):
        state = self.calibrated()
        target = {**self.neutral(), "smile": .8}
        advance(state, "smile", target, 1.5)
        advance(state, "smile", self.neutral(), 2.)
        self.assertEqual(advance(state, "smile", target, 2.5)[1], "hold_pose")

    def test_long_gap_and_invalid_features_clear_progress(self):
        state = self.calibrated()
        advance(state, "smile", self.neutral(), 20.)
        self.assertNotIn("baseline", state)
        for value in (None, float("nan"), float("inf")):
            state = self.calibrated()
            self.assertEqual(advance(state, "smile", {**self.neutral(), "smile": value}, 2.), (False, "no_face"))
            self.assertEqual(state, {})

    def test_sequence_has_pose_expression_and_no_duplicates(self):
        for _ in range(100):
            sequence = new_sequence()
            self.assertEqual(len(set(sequence)), 3)
            self.assertTrue(set(sequence) & set(TASKS[:4]))
            self.assertTrue(set(sequence) & set(TASKS[4:]))


if __name__ == "__main__":
    unittest.main()
