"""Regression checks for CI coverage guards; no ERP or database execution."""
import copy
import contextlib
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import backend_tests as ci


class CoverageTests(unittest.TestCase):
    def setUp(self):
        self.groups = ci.load_groups()
        self.classes = [c for g in self.groups if g["project"] == ci.INFRA for c in g["classes"]]
        self.tests = [c + ".Test" for c in self.classes if c != ci.GOLDEN]
        self.tests += [ci.GOLDEN + f".Golden{i}" for i in range(6)]

    def validate_manifest(self, groups):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "groups.json"
            path.write_text(json.dumps({"groups": groups}), encoding="utf-8")
            with patch.object(ci, "MANIFEST", path):
                return ci.load_groups()

    def test_manifest_covers_each_project_and_class_once(self):
        ci.verify_inventory(self.tests, self.groups)
        self.assertEqual(len(self.classes), len(set(self.classes)))
        self.assertEqual(len(self.groups), 13)

    def test_new_class_requires_explicit_assignment(self):
        with self.assertRaisesRegex(ValueError, "unassigned"):
            ci.verify_inventory(self.tests + [ci.INFRA + ".NewTests.Test"], self.groups)

    def test_removed_class_rejects_stale_manifest(self):
        with self.assertRaisesRegex(ValueError, "obsolete"):
            ci.verify_inventory(self.tests[1:], self.groups)

    def test_golden_count_must_be_six(self):
        with self.assertRaisesRegex(ValueError, "exactly six"):
            ci.verify_inventory(self.tests[:-1], self.groups)

    def test_duplicate_assignment_is_rejected(self):
        groups = copy.deepcopy(self.groups)
        groups[-2]["classes"].append(groups[-1]["classes"][0])
        with self.assertRaises(ValueError):
            self.validate_manifest(groups)

    def test_missing_project_is_rejected(self):
        with self.assertRaisesRegex(ValueError, "projects"):
            self.validate_manifest(self.groups[1:])

    def test_unfiltered_project_cannot_have_an_extra_job(self):
        groups = copy.deepcopy(self.groups)
        groups.append(dict(groups[0], id="domain-copy"))
        with self.assertRaisesRegex(ValueError, "unfiltered exactly once"):
            self.validate_manifest(groups)

    def test_golden_cannot_run_on_linux(self):
        groups = copy.deepcopy(self.groups)
        groups[-1]["runner"] = "ubuntu-latest"
        with self.assertRaisesRegex(ValueError, "Wrong runner"):
            self.validate_manifest(groups)

    def test_filter_uses_class_boundary(self):
        self.assertEqual(ci.test_filter({"classes": ["A.B.Class", "A.B.Other"]}),
                         "FullyQualifiedName~A.B.Class.|FullyQualifiedName~A.B.Other.")

    def test_discovery_rejects_empty_and_custom_display_names(self):
        for output in ["", "The following Tests are available:\n", "The following Tests are available:\n custom"]:
            with self.assertRaises(ValueError):
                ci.parse_discovery(output)

    def test_discovery_preserves_theory_cases(self):
        names = ["ERP.Tests.Class.Method(value: 1)", "ERP.Tests.Class.Method(value: 2)"]
        self.assertEqual(ci.parse_discovery("The following Tests are available:\n    " +
                                          "\n    ".join(names)), names)

    def test_random_member_data_does_not_change_method_identity(self):
        self.assertEqual(ci.method_name('ERP.Tests.Class.Test(id: "first-guid")'),
                         ci.method_name('ERP.Tests.Class.Test(id: "second-guid")'))


class ReportTests(unittest.TestCase):
    def setUp(self):
        env = patch.dict(ci.os.environ, {"GITHUB_STEP_SUMMARY": ""})
        env.start()
        self.addCleanup(env.stop)
        quiet = contextlib.redirect_stdout(io.StringIO())
        quiet.__enter__()
        self.addCleanup(quiet.__exit__, None, None, None)

    def results(self, path, outcomes, expected):
        (path / "inventory.json").write_text(json.dumps({"group": "example", "selected": expected}), encoding="utf-8")
        (path / "timings.json").write_text('{"test": 2.5}', encoding="utf-8")
        (path / "run-state.json").write_text('{"completed": true}', encoding="utf-8")
        rows = "".join(f'<UnitTestResult testId="{i}" testName="ERP.Tests.Class.Test" duration="00:00:01" outcome="{o}" />'
                       for i, o in enumerate(outcomes))
        definitions = "".join(f'<UnitTest id="{i}"><TestMethod className="ERP.Tests.Class" name="Test" /></UnitTest>'
                              for i in range(len(outcomes)))
        (path / "tests.trx").write_text('<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">'
                                       f'<Results>{rows}</Results><TestDefinitions>{definitions}</TestDefinitions></TestRun>', encoding="utf-8")

    def test_missing_trx_is_failure_and_produces_summary(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp)
            (path / "timings.json").write_text("{}", encoding="utf-8")
            with self.assertRaises(ValueError):
                ci.report({"id": "example"}, path)
            self.assertIn("INCOMPLETE", (path / "summary.md").read_text(encoding="utf-8"))

    def test_partial_theory_execution_is_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp)
            self.results(path, ["Passed"], ["ERP.Tests.Class.Test(value: 1)", "ERP.Tests.Class.Test(value: 2)"])
            with self.assertRaisesRegex(ValueError, "coverage"):
                ci.read_results(path)

    def test_failed_or_skipped_results_never_pass(self):
        for outcome in ["Failed", "NotExecuted"]:
            with self.subTest(outcome=outcome), tempfile.TemporaryDirectory() as tmp:
                path = Path(tmp)
                self.results(path, [outcome], ["ERP.Tests.Class.Test"])
                with self.assertRaisesRegex(ValueError, "Failed, skipped"):
                    ci.report({"id": "example"}, path)

    def test_complete_passing_results_publish_timings(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp)
            self.results(path, ["Passed"], ["ERP.Tests.Class.Test"])
            ci.report({"id": "example"}, path)
            self.assertIn("2.500", (path / "summary.md").read_text(encoding="utf-8"))

    def test_stale_passing_trx_cannot_hide_an_interrupted_run(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp)
            self.results(path, ["Passed"], ["ERP.Tests.Class.Test"])
            (path / "run-state.json").write_text('{"completed": false}', encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "stale TRX"):
                ci.report({"id": "example"}, path)


if __name__ == "__main__":
    unittest.main()
