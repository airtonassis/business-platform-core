"""Safety checks for the destructive disposable harness; no Docker/SQL invocation here."""
import contextlib
import importlib.util
import io
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

ROOT=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('audit_local',ROOT/'scripts/validate_tenant_audit_local.py')
module=importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class LocalAuditHarnessSafety(unittest.TestCase):
    def test_membership_list_modes_cannot_fall_through_to_historical_suites(self):
        for flags in (['--membership-list-probe-only'],['--membership-list-only','--d1-only'],['--membership-list-only','--demo-only']):
            with patch.object(sys,'argv',['audit','--execute-local','--output','unused-proof',*flags]),patch.object(module.subprocess,'run') as subprocess_run,contextlib.redirect_stderr(io.StringIO()):
                with self.assertRaises(SystemExit) as failure:module.main()
                self.assertEqual(failure.exception.code,2);subprocess_run.assert_not_called()
    def test_psql_receives_explicit_database_variable_before_any_sql(self):
        calls=[]
        def run(argv,**kwargs):
            calls.append((argv,kwargs))
            if 'context' in argv: return subprocess.CompletedProcess(argv,0,'npipe:////./pipe/dockerDesktopLinuxEngine','')
            if 'inspect' in argv:
                if '--format' in argv and '.Config.Image' in argv[argv.index('--format')+1]:
                    return subprocess.CompletedProcess(argv,0,'a'*64+'|postgres:17-alpine|e202a','')
                return subprocess.CompletedProcess(argv,1,'','not found')
            if 'run' in argv: return subprocess.CompletedProcess(argv,0,'a'*64,'')
            if 'pg_isready' in argv: return subprocess.CompletedProcess(argv,0,'','')
            if 'psql' in argv: return subprocess.CompletedProcess(argv,0,'f','')  # Stop at version check; no database is contacted.
            self.fail('Unexpected subprocess invocation')
        with tempfile.TemporaryDirectory() as temp:
            with patch.object(sys,'argv',['audit','--execute-local','--output',str(Path(temp)/'proof')]),patch.object(module.subprocess,'run',run),contextlib.redirect_stdout(io.StringIO()):
                with self.assertRaises(RuntimeError): module.main()
        psql=[argv for argv,_ in calls if 'psql' in argv]
        self.assertEqual(len(psql),1)
        position=psql[0].index('DBNAME=bpc_audit_local')
        self.assertEqual(psql[0][position-1],'-v')
        self.assertEqual(psql[0][psql[0].index('-d')+1],'bpc_audit_local')
        readiness=[argv for argv,_ in calls if 'pg_isready' in argv][0]
        self.assertEqual(readiness[readiness.index('-h')+1],'127.0.0.1')
        self.assertTrue(all(kwargs.get('encoding')=='utf-8' for _,kwargs in calls))

    def test_provisioning_guards_precede_mutations_and_preserve_psql_quoting(self):
        sql=(ROOT/'scripts/provision_tenant_audit_login.sql').read_text(encoding='utf-8')
        prefix=sql.split('BEGIN;',1)[0]
        self.assertIn('\\set ON_ERROR_STOP on',prefix)
        self.assertIn('\\if :{?DBNAME}',prefix)
        self.assertIn('SELECT 1/0 AS missing_dbname;',prefix)
        self.assertIn("SELECT :'DBNAME' = 'bpc_audit_local' AND current_database() = 'bpc_audit_local'",prefix)
        self.assertIn('SELECT 1/0 AS unexpected_database;',prefix)
        self.assertIn('GRANT CONNECT ON DATABASE :"DBNAME" TO :"audit_login";',sql)
        self.assertNotIn('CREATE ROLE',prefix)
        self.assertNotIn('GRANT ',prefix)

    def test_remote_daemon_is_refused_before_container_creation(self):
        calls=[]
        def run(argv,**kwargs):
            calls.append(argv)
            if 'context' in argv: return subprocess.CompletedProcess(argv,0,'ssh://remote.invalid','')
            if 'inspect' in argv: return subprocess.CompletedProcess(argv,1,'','not found')
            self.fail('Unexpected subprocess invocation')
        with tempfile.TemporaryDirectory() as temp:
            output=Path(temp)/'proof'
            with patch.object(sys,'argv',['audit','--execute-local','--output',str(output)]),patch.object(module.subprocess,'run',run),contextlib.redirect_stdout(io.StringIO()):
                with self.assertRaises(RuntimeError): module.main()
            self.assertFalse(any('run' in c or 'rm' in c for c in calls))
            summary=json.loads((output/'summary.json').read_text())
            self.assertEqual(summary['checks'][0]['status'],'FAIL')

    def test_readonly_storage_is_blocked_without_sql_or_unrelated_cleanup(self):
        calls=[]
        def run(argv,**kwargs):
            calls.append(argv)
            if 'context' in argv: return subprocess.CompletedProcess(argv,0,'npipe:////./pipe/dockerDesktopLinuxEngine','')
            if 'run' in argv: return subprocess.CompletedProcess(argv,1,'','read-only file system')
            if 'inspect' in argv: return subprocess.CompletedProcess(argv,1,'','not found')
            self.fail('Unexpected subprocess invocation')
        with tempfile.TemporaryDirectory() as temp:
            output=Path(temp)/'proof'
            with patch.object(sys,'argv',['audit','--execute-local','--output',str(output)]),patch.object(module.subprocess,'run',run),contextlib.redirect_stdout(io.StringIO()):
                with self.assertRaises(RuntimeError): module.main()
            self.assertFalse(any('exec' in c or 'rm' in c for c in calls))
            summary=json.loads((output/'summary.json').read_text())
            self.assertEqual(summary['status'],'BLOCKED')
            self.assertTrue(summary['cleaned'])
            self.assertEqual(json.loads((output/'queries.json').read_text()),[])
