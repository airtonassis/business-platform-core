"""Offline safety checks supplement real PostgreSQL tests; never prove RLS by text alone."""
import unittest
from pathlib import Path

class MigrationPackage(unittest.TestCase):
    def test_versioned_product_migration_exists_and_is_not_test_harness(self):
        path = Path(__file__).resolve().parents[2] / 'src/Business.Platform.Core.Infrastructure/Persistence/Migrations/001_enterprise.sql'
        self.assertTrue(path.is_file())
        sql = path.read_text(encoding='utf-8')
        self.assertNotIn('TEST-ONLY', sql)
        self.assertIn('FORCE ROW LEVEL SECURITY', sql)
        self.assertNotIn('BYPASSRLS;', sql.replace('NOBYPASSRLS;', ''))
        self.assertNotIn('GRANT ALL', sql)

if __name__ == '__main__': unittest.main()
