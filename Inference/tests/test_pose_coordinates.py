import unittest
from core.pose_coordinates import anatomical_yaw, relative_pitch

class PoseDirection(unittest.TestCase):
    def test_pitch_is_invariant_to_face_size_and_position(self):
        value = relative_pitch(.55, .3, .7)
        self.assertAlmostEqual(value, relative_pitch(.275, .15, .35))
        self.assertAlmostEqual(value, relative_pitch(.65, .4, .8))

    def test_directions_are_from_the_subject_not_the_observer(self):
        self.assertLess(anatomical_yaw(0.56,0.3,0.7),-0.1) # own left, nose moves image-right
        self.assertGreater(anatomical_yaw(0.44,0.3,0.7),0.1) # own right, nose moves image-left
        self.assertAlmostEqual(anatomical_yaw(0.5,0.3,0.7),0)

if __name__=='__main__': unittest.main()
