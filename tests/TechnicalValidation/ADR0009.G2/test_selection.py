import unittest
from unittest.mock import patch
from pathlib import Path
from prepare_g2 import generate, ROOT

class SelectionTests(unittest.TestCase):
    def test_only_g2_selected(self):
        source, selection=generate()
        self.assertEqual(31,len(selection['selected']))
        self.assertFalse(any(x['id'].startswith('K') or x['id'] in {'V09','V11','V12','V13','V14'} for x in selection['selected']))
        self.assertNotIn('await ConcurrencyProofs();',source)
        self.assertIn('args[0] != "--execute-g2"',source)
        self.assertIn('../ADR0009.G2/evidence/runs',source)
        self.assertTrue(any(x['id']=='Q11' for x in selection['selected']))
        self.assertTrue(any(x['id']=='Q12' for x in selection['selected']))

    def test_other_gate_injection_fails_closed(self):
        read=Path.read_text
        def injected(path,*args,**kwargs):
            value=read(path,*args,**kwargs)
            return value+'\nawait Proof("K01","unauthorized",async()=>{});' if path==ROOT/'runtime/G2Proofs.cs' else value
        with patch.object(Path,'read_text',injected):
            with self.assertRaises(AssertionError):generate()

if __name__=='__main__':unittest.main()
