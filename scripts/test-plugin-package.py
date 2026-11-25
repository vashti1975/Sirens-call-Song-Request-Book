"""Packaging tests with synthetic DLL bytes; does not claim to compile C#."""
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path
from zipfile import ZipFile

spec = importlib.util.spec_from_file_location("package_plugin", Path(__file__).with_name("package-plugin.py"))
package_plugin = importlib.util.module_from_spec(spec)
spec.loader.exec_module(package_plugin)


class PackageTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.manifest = dict(Name="Siren's call Song Request Book", Author="Siren's Call",
                             Description="Test", Punchline="Test", InternalName="SongbookSync",
                             AssemblyVersion="1.3.4.0", DalamudApiLevel=15)

    def archive(self, overrides=None, omit=()):
        data = {"SongbookSync.dll": b"MZsynthetic-test-fixture", "SongbookSync.json": json.dumps(self.manifest),
                "LICENSE": "Test license", "connection.json": "must not ship", "Dalamud.dll": b"must not ship",
                "SongbookSync.pdb": "local paths must not ship", "song-list.json": "must not ship"}
        data.update(overrides or {})
        path = self.root / "sdk.zip"
        with ZipFile(path, "w") as archive:
            for name, value in data.items():
                if name not in omit:
                    archive.writestr(name, value)
        return path

    def test_private_build_has_no_feed_or_extra_files(self):
        output = self.root / "out"
        package_plugin.prepare(self.archive(), output)
        self.assertFalse((output / "pluginmaster.json").exists())
        with ZipFile(output / "latest.zip") as archive:
            self.assertEqual(set(archive.namelist()), {"SongbookSync.dll", "SongbookSync.json", "LICENSE"})

    def test_feed_matches_packaged_manifest_and_update_links(self):
        output = self.root / "out"
        package_plugin.prepare(self.archive(), output, "https://downloads.example.test/sirens-call/", now=100)
        entry = json.loads((output / "pluginmaster.json").read_text())[0]
        for key, value in self.manifest.items():
            self.assertEqual(entry[key], value)
        self.assertEqual(entry["DownloadLinkInstall"], "https://downloads.example.test/sirens-call/latest.zip")
        self.assertEqual(entry["DownloadLinkInstall"], entry["DownloadLinkUpdate"])
        self.assertEqual(entry["LastUpdate"], 100)
        package_plugin.prepare(self.archive(), output)
        self.assertFalse((output / "pluginmaster.json").exists())

    def test_reject_source_archive_or_wrong_assembly(self):
        for omit in [("SongbookSync.dll",), ("SongbookSync.json",)]:
            with self.subTest(omit=omit), self.assertRaises(ValueError):
                package_plugin.prepare(self.archive(omit=omit), self.root / "out")
        with self.assertRaises(ValueError):
            package_plugin.prepare(self.archive({"SongbookSync.dll": b"source code"}), self.root / "out")
        for key, value in [("InternalName", "Other"), ("AssemblyVersion", "1.0.0.0"), ("DalamudApiLevel", 14)]:
            bad = dict(self.manifest, **{key: value})
            with self.subTest(key=key), self.assertRaises(ValueError):
                package_plugin.prepare(self.archive({"SongbookSync.json": json.dumps(bad)}), self.root / "out")

    def test_public_url_never_embeds_credentials(self):
        for value in ["http://example.test", "https://user:secret@example.test", "https://example.test/?token=secret",
                      "https://example.test/#key", "not-a-url"]:
            with self.subTest(value=value), self.assertRaises(ValueError):
                package_plugin.prepare(self.archive(), self.root / "out", value)


if __name__ == "__main__":
    unittest.main()
