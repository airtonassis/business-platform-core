"""Coordinator rules only; real process/SQL proofs belong to the disposable harness."""
import importlib.util
from pathlib import Path
import unittest


class HmacRotationTests(unittest.TestCase):
    def setUp(self):
        spec = importlib.util.spec_from_file_location('rotation', Path(__file__).resolve().parents[2] / 'scripts/hmac_rotation_local.py')
        self.module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(self.module)
        self.now = 100.0
        self.plan = self.module.RotationPlan('new-key', ['one', 'two'], clock=lambda: self.now)

    def test_deadline_is_five_minutes_for_whole_preparation_to_cutover(self):
        self.now = 399.99
        self.plan.confirm('one', 'new-key', True)
        self.plan.confirm('two', 'new-key', True)
        self.plan.cutover()

    def test_exact_deadline_cannot_be_extended_or_reset_by_confirmations(self):
        self.now = 250
        self.plan.confirm('one', 'new-key', True)
        self.now = 400
        with self.assertRaises(self.module.RotationBlocked):
            self.plan.confirm('two', 'new-key', True)

    def test_blocking_operation_budget_is_remaining_time_not_a_fresh_timeout(self):
        self.now = 399.5
        self.assertEqual(self.plan.remaining(), 0.5)
        self.now = 400.001
        with self.assertRaises(self.module.RotationBlocked):
            self.plan.remaining()

    def test_partial_distribution_cannot_retire_old_key(self):
        self.plan.confirm('one', 'new-key', True)
        with self.assertRaises(self.module.RotationBlocked):
            self.plan.cutover()

    def test_wrong_kid_or_failed_read_cannot_confirm_instance(self):
        for kid, success in [('old-key', True), ('new-key', False)]:
            with self.assertRaises(self.module.RotationBlocked):
                self.plan.confirm('one', kid, success)
        self.assertEqual(self.plan.confirmed, set())

    def test_unknown_instance_cannot_replace_a_required_confirmation(self):
        with self.assertRaises(self.module.RotationBlocked):
            self.plan.confirm('unlisted', 'new-key', True)

    def test_compromised_key_never_allowed_for_rollback_even_after_restore(self):
        self.plan.compromise('old-key')
        for observed_state in ['Active', 'Previous', 'Revoked']:
            with self.assertRaises(self.module.RotationBlocked):
                self.plan.rollback_allowed('old-key', observed_state)

    def test_trusted_rollback_requires_confirmed_database_active_state(self):
        self.plan.rollback_allowed('old-key', 'Active')
        for state in ['Previous', 'Revoked', None]:
            with self.assertRaises(self.module.RotationBlocked):
                self.plan.rollback_allowed('old-key', state)

    def test_uncertain_transition_requires_exact_metadata_reconciliation(self):
        self.plan.reconcile('new-key', 'Active', {'kid': 'new-key', 'state': 'Active'})
        for observed in [None, {'kid': 'old-key', 'state': 'Active'}, {'kid': 'new-key', 'state': 'Revoked'}]:
            with self.assertRaises(self.module.RotationBlocked):
                self.plan.reconcile('new-key', 'Active', observed)

    def test_invalid_inventory_rejected_before_work(self):
        for kid, instances in [('bad.kid', ['one', 'two']), ('new-key', ['one']), ('new-key', ['one', 'one'])]:
            with self.assertRaises(self.module.RotationBlocked):
                self.module.RotationPlan(kid, instances)


if __name__ == '__main__':
    unittest.main()
