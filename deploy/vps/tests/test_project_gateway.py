import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location("gateway", Path(__file__).resolve().parents[1] / "standardize-project-gateway.py")
gateway = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gateway)


class ProjectGatewayTests(unittest.TestCase):
    def test_port_replacement_requires_the_expected_layout_and_keeps_other_projects(self):
        source = "proxy_pass http://127.0.0.1:3110; proxy_pass http://127.0.0.1:3080;"
        result = gateway.replace_required(source, "127.0.0.1:3110", "127.0.0.1:9004")
        self.assertIn("127.0.0.1:3080", result)
        self.assertNotIn(":3110", result)
        with self.assertRaises(RuntimeError):
            gateway.replace_required(source, "127.0.0.1:3105", "127.0.0.1:9005")

    def test_game_guard_counts_connections_inside_the_container_namespace(self):
        sockets = "0 0 172.20.0.2:3001 172.20.0.1:43111\n0 0 172.20.0.2:41111 172.20.0.3:5432\n"
        with patch.object(gateway, "run", return_value=sockets) as run:
            self.assertEqual(1, gateway.game_connections({"State": {"Pid": 123}}))
            self.assertEqual(["nsenter", "-t", "123", "-n", "ss", "-H", "-nt", "state", "established"], run.call_args.args[0])

    def test_baseline_does_not_store_environment_arguments_or_logs(self):
        value = {"Name": "/project", "Id": "id", "Image": "digest", "RestartCount": 0,
                 "State": {"StartedAt": "UTC", "Status": "running"},
                 "Config": {"Env": ["TOKEN=private-test-value"], "Cmd": ["private-command"]}}
        with patch.object(gateway, "containers", return_value=[value]):
            result = json.dumps(gateway.baseline())
            self.assertNotIn("private-test-value", result)
            self.assertNotIn("private-command", result)

    def test_rollback_restores_originals_and_removes_only_new_files(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            original, added, unrelated = [root / name for name in ("original.conf", "added.conf", "other.conf")]
            original.write_text("changed")
            added.write_text("new")
            unrelated.write_text("untouched")
            gateway.write_changes({str(original): "previous", str(added): None})
            self.assertEqual("previous", original.read_text())
            self.assertFalse(added.exists())
            self.assertEqual("untouched", unrelated.read_text())


if __name__ == "__main__":
    unittest.main()
