import importlib.util
import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('easypanel_mcp', ROOT / 'scripts/easypanel_mcp.py')
mcp = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mcp)


class McpRedactionTests(unittest.TestCase):
    def test_unstructured_errors_and_secret_urls_are_never_printed(self):
        marker = 'synthetic-secret-never-print'
        value = {'content': [{'type': 'text', 'text': 'Error Password=' + marker}],
                 'apiKey': marker, 'connectionUrl': 'postgres://u:' + marker + '@host/db',
                 'env': 'Password=' + marker, 'deploymentUrl': 'http://host/' + marker}
        self.assertNotIn(marker, json.dumps(mcp.redact(value)))

    def test_embedded_mcp_json_is_sanitized_recursively(self):
        value = {'content': [{'type': 'text', 'text': json.dumps({'password': 'synthetic-marker',
                                                               'name': 'core-api'})}]}
        result = mcp.redact(value)
        self.assertNotIn('synthetic-marker', json.dumps(result))
        self.assertIn('core-api', json.dumps(result))

    def test_sensitive_parent_redacts_nested_generic_values(self):
        value = {'credentials': {'value': 'synthetic-nested-marker'},
                 'environment': [{'name': 'ANY', 'value': 'synthetic-nested-marker'}]}
        self.assertNotIn('synthetic-nested-marker', json.dumps(mcp.redact(value)))
