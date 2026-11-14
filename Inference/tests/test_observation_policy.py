import unittest
from core.observation_policy import apply_policy


class Policy(unittest.TestCase):
    def test_allowed_objects_are_not_incidents(self):
        values=[{'type':t} for t in ('book','laptop','cell_phone','multiple_faces','looking_down')]
        self.assertEqual(apply_policy(values,{}),values)
        result=apply_policy(values,{'allowedObjects':['book','laptop'],'flagHeadMovement':False})
        self.assertEqual([v['type'] for v in result],['cell_phone','multiple_faces'])
        self.assertIn({'type':'multiple_faces'},apply_policy(values,{'allowedObjects':['multiple_faces']}))
