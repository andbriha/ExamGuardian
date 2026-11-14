import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch
from core import outbox
from core.episodes import advance, observe, face_observations
from core.audio_window import append_audio


class Episodes(unittest.TestCase):
    def candidate(self, kind='cell_phone'):
        return {kind: {'type': kind, 'confidence': .8}}

    def test_normal_frames_break_accumulation(self):
        state = {}
        for tick in range(7):
            self.assertEqual(advance(state, self.candidate() if tick % 2 == 0 else {}, {'cell_phone'}, tick), [])

    def test_sparse_samples_and_closure_keep_one_event(self):
        state = {}
        self.assertEqual(advance(state, self.candidate(), {'cell_phone'}, 0), [])
        self.assertEqual(advance(state, self.candidate(), {'cell_phone'}, 4), [])
        first = advance(state, self.candidate(), {'cell_phone'}, 8)[0]
        self.assertEqual(first['metadata']['episode']['samples'], 3)
        for tick in (12, 16, 20):
            self.assertEqual(advance(state, self.candidate(), {'cell_phone'}, tick), [])
        update = advance(state, self.candidate(), {'cell_phone'}, 24)[0]
        closed = advance(state, {}, {'cell_phone'}, 25)[0]
        self.assertEqual(first['event_id'], update['event_id'])
        self.assertEqual(first['event_id'], closed['event_id'])
        self.assertEqual(closed['metadata']['episode']['status'], 'ended')
        self.assertGreater(closed['metadata']['episode']['revision'], update['metadata']['episode']['revision'])

    def test_quality_and_gaps_are_unknown_not_behavior(self):
        bad = face_observations({'quality_reason':'too_dark', 'violations':[{'type':'face_missing'}]})
        self.assertEqual([x['type'] for x in bad], ['analysis_unavailable'])
        state = {}
        for tick in (0,1): advance(state,self.candidate(),{'cell_phone'},tick)
        advance(state,{},set(),2,{'cell_phone'})
        self.assertEqual(advance(state,self.candidate(),{'cell_phone'},3), [])
        self.assertEqual(advance(state,self.candidate(),{'cell_phone'},20), [])

    def test_duplicate_and_out_of_order_frames_do_not_restart_event(self):
        state = {}
        for tick in (0,1,2): advance(state,self.candidate(),{'cell_phone'},tick)
        advance(state,{}, {'cell_phone'},3)
        self.assertEqual(advance(state,self.candidate(),{'cell_phone'},2), [])
        self.assertNotIn('cell_phone', state)

    def test_technical_quality_needs_ten_seconds(self):
        state={}
        for tick in range(10):
            self.assertEqual(advance(state,self.candidate('analysis_unavailable'),{'analysis_unavailable'},tick),[])
        self.assertEqual(len(advance(state,self.candidate('analysis_unavailable'),{'analysis_unavailable'},10)),1)

    def test_state_and_delivery_are_atomic_and_coalesced(self):
        with tempfile.TemporaryDirectory() as folder, patch.object(outbox,'STATE_PATH',Path(folder)/'state.db'):
            for tick in (0,1): observe(1,list(self.candidate().values()),tick,{'cell_phone'})
            def fail(_): raise OSError('disk full')
            with self.assertRaises(OSError): observe(1,list(self.candidate().values()),2,{'cell_phone'},evidence_writer=fail)
            event=observe(1,list(self.candidate().values()),2,{'cell_phone'})[0]
            observe(1,[],3,{'cell_phone'})
            with outbox.connect() as db:
                rows=db.execute('SELECT payload FROM outbox').fetchall()
            self.assertEqual(len(rows),1)
            value=json.loads(rows[0][0])
            self.assertEqual(value['eventId'],event['event_id'])
            self.assertEqual(json.loads(value['metadata'])['episode']['status'],'ended')


class AudioWindow(unittest.TestCase):
    def test_boundaries_gaps_overlap_and_student_isolation(self):
        with tempfile.TemporaryDirectory() as folder, patch.object(outbox,'STATE_PATH',Path(folder)/'state.db'):
            pcm=b'\x01\x00'*32000
            self.assertEqual(len(append_audio(1,pcm,16000,0)[0]),64000)
            merged=append_audio(1,pcm,16000,2)
            self.assertEqual(len(merged[0]),128000) # 2 + 2 seconds exceed the 3s speech threshold
            self.assertTrue(merged[3])
            self.assertIsNone(append_audio(1,pcm,16000,2))
            self.assertEqual(len(append_audio(2,pcm,16000,2)[0]),64000)
            after_gap=append_audio(1,pcm,16000,8)
            self.assertFalse(after_gap[3]); self.assertEqual(len(after_gap[0]),64000)
            for tick in range(10,26,2): result=append_audio(1,pcm,16000,tick)
            self.assertEqual(len(result[0]),320000)
