import importlib.util
import io
import json
from pathlib import Path
import subprocess
from types import SimpleNamespace
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location("collector", Path(__file__).resolve().parents[1] / "collect-host-status.py")
collector = importlib.util.module_from_spec(spec)
spec.loader.exec_module(collector)


class HostStatusTests(unittest.TestCase):
    def test_ipv6_loopback_and_wildcard_sockets_without_command_arguments(self):
        result = collector.socket_rows('tcp LISTEN 0 128 [::1]:3180 [::]:* users:(("nginx",pid=1,fd=2))\n'
                                       'udp UNCONN 0 0 0.0.0.0:50000 0.0.0.0:*\n'
                                       'tcp LISTEN 0 128 *:8443 *:*\n')
        self.assertEqual(3, len(result))
        self.assertIn({"address": "::1", "port": 3180, "protocol": "tcp", "process": "nginx"}, result)
        self.assertTrue(any(row["port"] == 8443 for row in result))
        self.assertNotIn("pid", str(result))

    def test_exposed_only_is_distinct_from_multiple_published_addresses(self):
        result = collector.port_bindings({"8080/tcp": None, "8443/tcp": [
            {"HostIp": "127.0.0.1", "HostPort": "3180"}, {"HostIp": "::1", "HostPort": "3180"}], "bad/tcp": None})
        self.assertEqual(3, len(result))
        self.assertIsNone(result[0]["hostPort"])
        self.assertEqual(3180, result[1]["hostPort"])
        self.assertEqual("::1", result[2]["hostIp"])

    def test_parse_size_str(self):
        for value, expected in (("16.81GB", 16_810_000_000), ("128MB", 128_000_000),
                                ("5.583kB", 5583), ("1 GiB", 1024**3), ("128MiB", 128 * 1024**2),
                                ("512B", 512), ("", 0), (None, 0), (".GB", 0), ("1XB", 0),
                                ("1GB invalid", 0), ("-1GB", 0)):
            with self.subTest(value=value):
                self.assertEqual(expected, collector.parse_size_str(value))

    @staticmethod
    def inspected(project, directory):
        return {"Labels": {"com.docker.compose.project": project,
                           "com.docker.compose.project.working_dir": directory}}

    def test_compose_discovers_missing_and_new_projects_and_finds_git_root(self):
        inspected = [self.inspected("mot-me-banh", "/opt/apps/bep-du-banh/repo"),
                     self.inspected("new-project", "/srv/new-project/deploy")]
        directories = {"/opt/apps/bep-du-banh/repo", "/srv/new-project/deploy", "/srv/new-project"}
        warnings = []
        with patch.object(collector.os.path, "realpath", side_effect=lambda value: value), \
                patch.object(collector.os.path, "isdir", side_effect=lambda value: value in directories), \
                patch.object(collector.os.path, "exists", side_effect=lambda value: value == "/srv/new-project/.git"):
            targets = collector.storage_targets(inspected, warnings)
        self.assertIn(("Mọt Mê Bánh", ["/opt/apps/bep-du-banh/repo"], "repo"), targets)
        self.assertIn(("new-project", ["/srv/new-project"], "repo"), targets)
        self.assertEqual([], warnings)

    def test_active_home_checkouts_and_old_copies_are_both_measured(self):
        directories = {"/home/ubuntu/exxplore-kittens", "/var/www/exxplore-kittens", "/home/ubuntu/livekit"}
        inspected = [self.inspected("exxplore-kittens", "/home/ubuntu/exxplore-kittens"),
                     self.inspected("livekit", "/home/ubuntu/livekit")]
        with patch.object(collector.os.path, "realpath", side_effect=lambda value: value), \
                patch.object(collector.os.path, "isdir", side_effect=lambda value: value in directories), \
                patch.object(collector.os.path, "exists", return_value=False):
            targets = {name: paths for name, paths, _ in collector.storage_targets(inspected, [])}
        self.assertEqual(directories - {"/home/ubuntu/livekit"}, set(targets["Kitchen Explore"]))
        self.assertEqual(["/home/ubuntu/livekit"], targets["LiveKit"])

    def test_same_checkout_and_nested_release_are_not_counted_twice(self):
        inspected = [self.inspected("sentinellan-prod", "/opt/sentinellan/releases/current/deploy/vps")] * 2
        with patch.object(collector.os.path, "realpath", side_effect=lambda value: value), \
                patch.object(collector.os.path, "isdir", side_effect=lambda value: value.startswith("/opt/sentinellan")), \
                patch.object(collector.os.path, "exists", return_value=False):
            targets = collector.storage_targets(inspected, [])
        self.assertEqual([("SentinelLAN", ["/opt/sentinellan"], "repo")], targets)

    def test_aliased_directories_are_not_counted_twice(self):
        with patch.object(collector.os.path, "isdir", side_effect=lambda value: value in
                          {"/var/www/exxplore-kittens", "/home/ubuntu/exxplore-kittens"}), \
                patch.object(collector.os.path, "realpath", return_value="/home/ubuntu/exxplore-kittens"):
            targets = collector.storage_targets([], [])
        self.assertEqual([("Kitchen Explore", ["/home/ubuntu/exxplore-kittens"], "repo")], targets)

    def test_unapproved_or_inaccessible_compose_paths_warn_without_being_scanned(self):
        inspected = [self.inspected("private", "/etc"), self.inspected("missing", "/srv/missing"),
                     self.inspected("relative", "--exclude=/"),
                     self.inspected("escaping-symlink", "/srv/link")]
        warnings = []
        with patch.object(collector.os.path, "realpath", side_effect=lambda value:
                          "/etc" if value == "/srv/link" else value), \
                patch.object(collector.os.path, "isdir", return_value=False):
            self.assertEqual([], collector.storage_targets(inspected, warnings))
        self.assertEqual(4, len(warnings))

    def test_docker_units_and_reclaimable_values_are_preserved_in_ranked_storage(self):
        output = "\n".join(json.dumps(row) for row in (
            {"Type": "Build Cache", "Size": "17.98GB", "Reclaimable": "17.98GB"},
            {"Type": "Local Volumes", "Size": "127.8MB", "Reclaimable": "0B (0%)"}))
        with patch.object(collector, "run", return_value=(0, output)), \
                patch.object(collector, "storage_targets", return_value=[]):
            items = collector.collect_storage()
        self.assertEqual(17_980_000_000, items[0]["sizeBytes"])
        self.assertEqual("17.98GB", items[0]["reclaimable"])
        self.assertEqual(127_800_000, items[1]["sizeBytes"])
        self.assertIsNone(items[1]["reclaimable"])

    def test_docker_storage_timeout_or_failure_does_not_block_repository_measurements(self):
        for failure in (subprocess.TimeoutExpired("docker", 6), OSError("unavailable"), (1, "")):
            with self.subTest(failure=failure):
                warnings = []
                with patch.object(collector, "storage_targets", return_value=[("Repo", ["/srv/repo"], "repo")]), \
                        patch.object(collector, "run", side_effect=[failure, (0, "1024\t/srv/repo\n")]):
                    items = collector.collect_storage(warnings=warnings)
                self.assertEqual(["Repo"], [item["name"] for item in items])
                self.assertEqual(1024, items[0]["sizeBytes"])
                self.assertEqual(1, len(warnings))

    def test_directory_timeout_or_failure_does_not_block_other_repositories(self):
        for failure in (subprocess.TimeoutExpired("du", 4), OSError("unavailable"), (1, "")):
            with self.subTest(failure=failure):
                warnings = []
                targets = [("Slow repo", ["/srv/slow"], "repo"), ("Repo", ["/srv/repo"], "repo")]
                with patch.object(collector, "storage_targets", return_value=targets), \
                        patch.object(collector, "run", side_effect=[(0, ""), failure, (0, "2048\t/srv/repo\n")]):
                    items = collector.collect_storage(warnings=warnings)
                self.assertEqual(["Repo"], [item["name"] for item in items])
                self.assertEqual(1, len(warnings))

    def test_malformed_docker_row_does_not_discard_valid_storage_rows(self):
        warnings = []
        output = '[]\n{bad json\n' + json.dumps({"Type": "Images", "Size": "1GB"})
        with patch.object(collector, "run", return_value=(0, output)), \
                patch.object(collector, "storage_targets", return_value=[]):
            items = collector.collect_storage(warnings=warnings)
        self.assertEqual(1_000_000_000, items[0]["sizeBytes"])
        self.assertEqual(2, len(warnings))

    def test_storage_time_budget_keeps_already_collected_metrics(self):
        warnings = []
        output = json.dumps({"Type": "Images", "Size": "1GB"})
        with patch.object(collector.time, "monotonic", side_effect=[0, 21]), \
                patch.object(collector, "run", return_value=(0, output)), \
                patch.object(collector, "storage_targets", return_value=[("Repo", ["/srv/repo"], "repo")]):
            items = collector.collect_storage(warnings=warnings)
        self.assertEqual(["Docker Images"], [item["name"] for item in items])
        self.assertEqual(1, len(warnings))

    def test_storage_timeout_still_returns_a_fresh_host_snapshot(self):
        def run(args, timeout=8):
            if "df" in args:
                raise subprocess.TimeoutExpired(args, timeout)
            return (0, "") if "ps" in args or "/usr/bin/ss" in args else (0, "active\n")

        with patch.object(collector, "run", side_effect=run), \
                patch.object(collector, "cpu_times", side_effect=[(100, 30), (200, 40)]), \
                patch.object(collector.platform, "platform", return_value="Linux lab-host"), \
                patch.object(collector.time, "sleep"), \
                patch.object(collector.shutil, "disk_usage", return_value=SimpleNamespace(total=1000, used=400)), \
                patch.object(collector.os.path, "isdir", return_value=False), \
                patch("builtins.open", side_effect=[io.StringIO("MemTotal: 1024 kB\nMemAvailable: 256 kB\n"),
                                                    io.StringIO("3600 0")]):
            snapshot = collector.collect("lab-host", "192.0.2.1")
        self.assertEqual(90, snapshot["cpuPercent"])
        self.assertEqual(75, snapshot["ramPercent"])
        self.assertEqual(40, snapshot["diskPercent"])
        self.assertTrue(snapshot["runtime"]["dockerAvailable"])
        self.assertEqual([], snapshot["runtime"]["storageBreakdown"])
        self.assertIn("Docker", snapshot["warnings"][0])
        self.assertTrue(snapshot["capturedAtUtc"])



if __name__ == "__main__":
    unittest.main()
