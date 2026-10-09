import importlib.util
from pathlib import Path
import unittest

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
        self.assertEqual(collector.parse_size_str("16.81GB"), int(16.81 * 1024**3))
        self.assertEqual(collector.parse_size_str("128MB"), 128 * 1024**2)
        self.assertEqual(collector.parse_size_str("512B"), 512)
        self.assertEqual(collector.parse_size_str(""), 0)



if __name__ == "__main__":
    unittest.main()
