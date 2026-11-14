import unittest
from evaluate import metrics, percentile

class MetricsTests(unittest.TestCase):
    def test_false_alarms_and_missed_events_are_separate(self):
        measured=metrics([(True,True),(True,False),(False,True),(False,False)])
        self.assertEqual(measured['precision'],.5)
        self.assertEqual(measured['recall'],.5)
        self.assertEqual(measured['false_positive_rate'],.5)
        self.assertEqual(measured['false_negative_rate'],.5)

    def test_missing_denominator_is_unknown(self):
        self.assertIsNone(metrics([])['precision'])
        self.assertIsNone(metrics([(False,False)])['recall'])
        self.assertEqual(percentile([90,5,40],.95),90)

if __name__ == '__main__': unittest.main()
