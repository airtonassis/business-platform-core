"""Local journal/transport tests; mocks do not prove PostgreSQL ACL or RLS."""
import importlib.util
import json
import http.server
from pathlib import Path
import shutil
import ssl
import subprocess
import tempfile
import threading
import unittest
from unittest.mock import patch
import uuid

MODULE = Path(__file__).resolve().parents[2] / 'scripts/provision_local.py'


class ProvisionLocalTests(unittest.TestCase):
    def setUp(self):
        spec = importlib.util.spec_from_file_location('provision_local', MODULE)
        self.module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(self.module)
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.path = Path(self.temp.name) / 'operation.json'
        self.request = dict(base_url='https://localhost:55443', actor={'issuer': 'https://idp.invalid', 'subject': 'operator'},
                            user_id=str(uuid.uuid4()), reference='A' * 43, name='Synthetic', key='operation-1',
                            justification='Approved synthetic onboarding', approver='local-project-owner')
        self.ids = dict(organizationId=str(uuid.uuid4()), accountId=str(uuid.uuid4()), membershipId=str(uuid.uuid4()))
        self.calls = []

    def run_operation(self, replies, reconcile=False):
        def transport(path, body, key):
            self.calls.append((path, body, key))
            reply = replies.pop(0)
            if isinstance(reply, Exception):
                raise reply
            return reply
        return self.module.execute(self.path, self.request, transport, reconcile=reconcile)

    def test_legitimate_write_and_authorized_receipt_replay(self):
        first = self.run_operation([(200, {}), (201, dict(self.ids, replayed=False))])
        self.assertEqual(first['outcome'], 'CONFIRMED_SUCCESS')
        replay = self.run_operation([(200, dict(self.ids, replayed=True))], reconcile=True)
        self.assertEqual(replay['result'], dict(self.ids, replayed=True))
        self.assertEqual(len(self.calls), 3)
        self.assertNotIn(self.request['reference'], self.path.read_text())
        self.assertEqual(json.loads(self.path.read_text())['justification'], self.request['justification'])
        self.assertEqual(json.loads(self.path.read_text())['correlation']['operationId'], 'c55ed98b-adf7-26fb-bc87-3dba532c99f9')

    def test_timeout_is_unknown_and_reconciliation_keeps_exact_key_and_payload(self):
        self.assertEqual(self.run_operation([(200, {}), TimeoutError()])['outcome'], 'UNKNOWN')
        result = self.run_operation([(200, dict(self.ids, replayed=True))], reconcile=True)
        self.assertEqual(result['outcome'], 'CONFIRMED_SUCCESS')
        self.assertEqual(self.calls[-1][1:], self.calls[-2][1:])

    def test_expired_reference_or_denied_reconciliation_cannot_prove_no_commit(self):
        for status in [401, 403, 404, 409, 503]:
            with self.subTest(status=status):
                self.path.unlink(missing_ok=True)
                self.run_operation([(200, {}), OSError()])
                result = self.run_operation([(status, {})], reconcile=True)
                self.assertEqual(result['outcome'], 'UNKNOWN')
                self.assertNotIn('result', result)

    def test_unauthorized_preview_never_calls_write(self):
        for status in [401, 403, 404]:
            self.path.unlink(missing_ok=True)
            self.calls.clear()
            self.assertEqual(self.run_operation([(status, {})])['outcome'], 'CONFIRMED_FAILURE')
            self.assertEqual(len(self.calls), 1)

    def test_unknown_requires_explicit_reconciliation(self):
        self.run_operation([(200, {}), OSError()])
        with self.assertRaises(ValueError):
            self.run_operation([])

    def test_previously_confirmed_success_is_not_disclosed_after_authorization_loss(self):
        self.run_operation([(200, {}), (201, dict(self.ids, replayed=False))])
        result = self.run_operation([(403, {})], reconcile=True)
        self.assertEqual(result['outcome'], 'UNKNOWN')
        self.assertNotIn('result', result)
        journal = json.loads(self.path.read_text())
        self.assertEqual(journal['events'][-1]['httpStatus'], 403)
        self.assertTrue(any(e['outcome'] == 'CONFIRMED_SUCCESS' for e in journal['events']))

    def test_first_confirmed_rejection_is_distinct_from_unavailable_storage(self):
        self.assertEqual(self.run_operation([(200, {}), (409, {})])['outcome'], 'CONFIRMED_FAILURE')
        self.path.unlink()
        self.assertEqual(self.run_operation([(200, {}), (503, {})])['outcome'], 'UNKNOWN')

    def test_payload_actor_key_or_environment_change_rejected_before_transport(self):
        self.run_operation([(200, {}), TimeoutError()])
        for field, value in [('name', 'Changed'), ('key', 'other-key'), ('base_url', 'https://localhost:55444'),
                             ('actor', {'issuer': 'https://other.invalid', 'subject': 'operator'})]:
            original = self.request[field]
            self.request[field] = value
            with self.assertRaises(ValueError):
                self.run_operation([], reconcile=True)
            self.request[field] = original

    def test_missing_justification_and_nonlocal_targets_rejected(self):
        for changes in [dict(justification=''), dict(approver=''), dict(base_url='https://example.com'),
                        dict(base_url='http://localhost:55443'), dict(base_url='https://localhost:55443/other'),
                        dict(base_url='https://user@localhost:55443')]:
            with self.subTest(changes=changes):
                original = dict(self.request)
                self.request.update(changes)
                with self.assertRaises(ValueError):
                    self.run_operation([])
                self.request = original

    def test_invalid_success_is_unknown_not_confirmed(self):
        self.assertEqual(self.run_operation([(200, {}), (201, {'message': 'internal'})])['outcome'], 'UNKNOWN')

    def test_concurrent_or_stale_lock_fails_closed(self):
        lock = self.path.with_suffix('.json.lock')
        lock.write_text('held')
        with self.assertRaises(FileExistsError):
            self.run_operation([])
        self.assertTrue(lock.exists())

    def test_journal_persisted_before_transport_and_no_secret_in_failure(self):
        def transport(path, body, key):
            journal = json.loads(self.path.read_text())
            self.assertTrue(journal['events'])
            if path.endswith('/organizations'):
                self.assertEqual(journal['events'][-1]['outcome'], 'UNKNOWN')
                raise OSError('sensitive-token-must-not-be-logged')
            return 200, {}
        result = self.module.execute(self.path, self.request, transport)
        self.assertEqual(result['outcome'], 'UNKNOWN')
        self.assertNotIn('sensitive-token', self.path.read_text())

    def test_partial_response_is_unknown_without_disclosing_response_bytes(self):
        def transport(path, body, key):
            if path.endswith('/organizations'):
                import http.client
                raise http.client.IncompleteRead(b'sensitive-partial-response', 42)
            return 200, {}
        result = self.module.execute(self.path, self.request, transport)
        self.assertEqual(result['outcome'], 'UNKNOWN')
        self.assertNotIn('sensitive-partial-response', self.path.read_text())

    def test_cli_refuses_nonlocal_issuer_before_transport(self):
        import base64
        import io
        from contextlib import redirect_stdout
        request = {k: v for k, v in self.request.items() if k != 'reference'}
        config = Path(self.temp.name) / 'request.json'
        config.write_text(json.dumps(request))
        claims = base64.urlsafe_b64encode(json.dumps({'iss': 'https://idp.invalid', 'sub': 'operator'}).encode()).decode().rstrip('=')
        secret = 'header.' + claims + '.synthetic-secret'
        argv = ['provision_local.py', '--execute-local', '--request', str(config), '--journal', str(self.path),
                '--ca-file', 'unused-local-ca', '--request-timeout', '3']
        output = io.StringIO()
        with patch('sys.argv', argv), patch.dict('os.environ', BPC_LOCAL_PLATFORM_TOKEN=secret, BPC_LOCAL_OWNER_REFERENCE=self.request['reference']), \
                patch.object(self.module, 'https_transport') as send, redirect_stdout(output):
            self.assertEqual(self.module.main(), 2)
            send.assert_not_called()
        self.assertNotIn(secret, output.getvalue())
        self.assertFalse(self.path.exists())


class ProvisionTransportTests(unittest.TestCase):
    """Real HTTPS transport only; this server is not an authentication provider/API."""
    @classmethod
    def setUpClass(cls):
        spec = importlib.util.spec_from_file_location('provision_local', MODULE)
        cls.module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(cls.module)
        cls.temp = tempfile.TemporaryDirectory()
        cls.addClassCleanup(cls.temp.cleanup)
        folder = Path(cls.temp.name)
        cls.cert, key = folder / 'cert.pem', folder / 'key.pem'
        openssl = shutil.which('openssl') or r'C:\Program Files\Git\usr\bin\openssl.exe'
        subprocess.run([openssl, 'req', '-x509', '-newkey', 'rsa:2048', '-nodes', '-keyout', str(key),
                        '-out', str(cls.cert), '-days', '1', '-subj', '/CN=localhost',
                        '-addext', 'subjectAltName=DNS:localhost'], capture_output=True, check=True)
        cls.received = []
        class Handler(http.server.BaseHTTPRequestHandler):
            def log_message(self, *args):
                pass

            def do_POST(self):
                cls.received.append((self.path, self.headers.get('Authorization')))
                self.rfile.read(int(self.headers.get('Content-Length', 0)))
                if self.path == '/redirect':
                    self.send_response(307)
                    self.send_header('Location', '/must-not-follow')
                    self.end_headers()
                elif self.path == '/drop':
                    self.close_connection = True
                else:
                    self.send_response(200)
                    self.end_headers()
                    self.wfile.write(b'{"ok":true}')
        cls.server = http.server.ThreadingHTTPServer(('127.0.0.1', 0), Handler)
        context = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
        context.load_cert_chain(cls.cert, key)
        cls.server.socket = context.wrap_socket(cls.server.socket, server_side=True)
        cls.thread = threading.Thread(target=cls.server.serve_forever, daemon=True)
        cls.thread.start()
        cls.addClassCleanup(cls.server.server_close)
        cls.addClassCleanup(cls.server.shutdown)
        cls.origin = 'https://localhost:' + str(cls.server.server_port)

    def test_verified_https_delivers_bearer_without_redirect_following(self):
        send = self.module.https_transport(self.origin, 'synthetic-only', self.cert, 3)
        self.assertEqual(send('/ok', {}, None), (200, {'ok': True}))
        self.assertEqual(send('/redirect', {}, None), (307, {}))
        self.assertFalse(any(path == '/must-not-follow' for path, _ in self.received))

    def test_untrusted_certificate_rejected(self):
        send = self.module.https_transport(self.origin, 'synthetic-only', None, 3)
        with self.assertRaises(OSError):
            send('/ok', {}, None)

    def test_connection_lost_after_dispatch_remains_unknown(self):
        send = self.module.https_transport(self.origin, 'synthetic-only', self.cert, 3)
        with self.assertRaises(OSError):
            send('/drop', {}, 'same-key')


if __name__ == '__main__':
    unittest.main()
