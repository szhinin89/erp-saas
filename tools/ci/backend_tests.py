"""Backend CI groups, discovery coverage and TRX reporting (standard library only)."""
import argparse
from collections import Counter
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import time
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
MANIFEST = Path(__file__).with_name("backend-test-groups.json")
INFRA = "ERP.Infrastructure.Tests"
GOLDEN = INFRA + ".Ride.RideGoldenFileTests"


def load_groups():
    groups = json.loads(MANIFEST.read_text(encoding="utf-8"))["groups"]
    ids = [g["id"] for g in groups]
    if len(ids) != len(set(ids)):
        raise ValueError("Duplicate group IDs")
    solution = ET.parse(ROOT / "backend/src/ERP.slnx")
    projects = {Path(p.attrib["Path"]).stem for p in solution.iter("Project")
                if Path(p.attrib["Path"]).stem.endswith(".Tests")}
    if projects != {g["project"] for g in groups}:
        raise ValueError("Test projects in solution and manifest differ")
    for project in projects - {INFRA}:
        matches = [g for g in groups if g["project"] == project]
        if len(matches) != 1 or "classes" in matches[0]:
            raise ValueError(f"{project} must run unfiltered exactly once")
    assigned = []
    for g in groups:
        if not re.fullmatch(r"[a-z0-9-]+", g["id"]):
            raise ValueError("Invalid group ID")
        if g["project"] == INFRA:
            if not g.get("classes"):
                raise ValueError("Empty Infrastructure group")
            for cls in g["classes"]:
                if not re.fullmatch(r"ERP\.Infrastructure\.Tests\.[A-Za-z0-9_.]+", cls):
                    raise ValueError(f"Invalid class: {cls}")
            assigned.extend(g["classes"])
        expected_os = "windows-latest" if GOLDEN in g.get("classes", []) else "ubuntu-latest"
        if g["runner"] != expected_os:
            raise ValueError(f"Wrong runner for {g['id']}")
    if len(assigned) != len(set(assigned)):
        raise ValueError("A class belongs to multiple groups")
    if [g["classes"] for g in groups if g["runner"] == "windows-latest"] != [[GOLDEN]]:
        raise ValueError("Windows must execute only the golden class")
    return groups


def method_name(display_name):
    return display_name.split("(", 1)[0]


def test_class(display_name):
    return method_name(display_name).rsplit(".", 1)[0]


def parse_discovery(output):
    marker = "The following Tests are available:"
    if marker not in output:
        raise ValueError("Missing VSTest discovery header")
    tests = [line.strip() for line in output.split(marker, 1)[1].splitlines() if line.strip()]
    if not tests or any(not re.match(r"^ERP\.[A-Za-z0-9_.]+\.", t) for t in tests):
        raise ValueError("Empty or unrecognized VSTest discovery output")
    return tests


def verify_inventory(tests, groups):
    discovered = {test_class(t) for t in tests}
    assigned = {c for g in groups if g["project"] == INFRA for c in g["classes"]}
    if discovered != assigned:
        raise ValueError(f"Infrastructure inventory mismatch: unassigned={sorted(discovered-assigned)}, "
                         f"obsolete={sorted(assigned-discovered)}")
    if sum(test_class(t) == GOLDEN for t in tests) != 6:
        raise ValueError("Expected exactly six discovered golden tests")


def test_filter(group):
    # The trailing dot prevents ClassA from selecting ClassAB.
    return "|".join("FullyQualifiedName~" + cls + "." for cls in group.get("classes", []))


def command(args, output, stage, timings):
    print("+ " + " ".join(args), flush=True)
    start = time.monotonic()
    try:
        with (output / f"{stage}.log").open("w", encoding="utf-8") as log:
            process = subprocess.Popen(args, cwd=ROOT, stdout=subprocess.PIPE,
                                       stderr=subprocess.STDOUT, text=True,
                                       encoding="utf-8", errors="replace",
                                       env={**os.environ, "DOTNET_CLI_UI_LANGUAGE": "en"})
            lines = []
            for line in process.stdout:
                log.write(line)
                log.flush()
                if not stage.endswith("discovery"):
                    print(line, end="", flush=True)
                lines.append(line)
            if process.wait():
                raise RuntimeError(f"{stage} exited {process.returncode}")
            return "".join(lines)
    finally:
        timings[stage] = round(time.monotonic() - start, 3)
        (output / "timings.json").write_text(json.dumps(timings, indent=2), encoding="utf-8")


def run(group, groups, output, configuration, no_build, verify_only):
    state = output / "run-state.json"
    state.write_text('{"completed": false}', encoding="utf-8")
    timings = {}
    project = f"backend/src/{group['project']}/{group['project']}.csproj"
    if not no_build:
        command(["dotnet", "restore", project], output, "restore", timings)
        command(["dotnet", "build", project, "-c", configuration, "--no-restore", "--nologo"],
                output, "build", timings)
    base = ["dotnet", "test", project, "-c", configuration, "--no-build", "--no-restore", "--nologo"]
    tests = parse_discovery(command(base + ["--list-tests"], output, "discovery", timings))
    if group["project"] == INFRA:
        verify_inventory(tests, groups)
        selected = [t for t in tests if test_class(t) in group["classes"]]
        filtered = parse_discovery(command(base + ["--list-tests", "--filter", test_filter(group)],
                                            output, "filtered-discovery", timings))
        # MemberData can generate fresh GUIDs; compare method identities and case counts,
        # not the argument values embedded in display names from separate discoveries.
        if Counter(map(method_name, filtered)) != Counter(map(method_name, selected)):
            raise ValueError("Actual VSTest filter differs from assigned inventory")
    else:
        selected = tests
    (output / "inventory.json").write_text(json.dumps({"group": group["id"],
        "discovered": len(tests), "selected": selected}, indent=2), encoding="utf-8")
    if verify_only:
        print(f"Discovery coverage PASS: {group['id']}, {len(selected)} cases; no tests executed")
        return
    if (group["runner"] == "windows-latest") != (sys.platform == "win32"):
        raise ValueError("Execution OS differs from the group's declared runner")
    args = base + ["--logger", "trx;LogFileName=tests.trx", "--logger", "console;verbosity=normal",
                   "--results-directory", str(output)]
    if test_filter(group):
        args += ["--filter", test_filter(group)]
    command(args, output, "test", timings)
    state.write_text('{"completed": true}', encoding="utf-8")


def read_results(output):
    ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
    root = ET.parse(output / "tests.trx").getroot()
    results = root.findall(".//t:UnitTestResult", ns)
    if not results:
        raise ValueError("TRX has no test results")
    inventory = json.loads((output / "inventory.json").read_text(encoding="utf-8"))
    definitions = {}
    for definition in root.findall(".//t:UnitTest", ns):
        method = definition.find("t:TestMethod", ns)
        definitions[definition.attrib["id"]] = method.attrib["className"] + "." + method.attrib["name"]
    actual = Counter(method_name(definitions[r.attrib["testId"]]) for r in results)
    expected = Counter(method_name(t) for t in inventory["selected"])
    # Runtime-expanded theories may add cases, but no discovered case/method can disappear.
    if set(actual) != set(expected) or any(actual[m] < count for m, count in expected.items()):
        raise ValueError("TRX method/case coverage differs from discovery")
    if inventory["group"] == "ride-golden" and len(results) != 6:
        raise ValueError("Expected exactly six golden results")
    return results


def report(group, output):
    lines = [f"## Backend: {group['id']}", ""]
    error = None
    try:
        timings = json.loads((output / "timings.json").read_text(encoding="utf-8"))
        lines += ["| Stage | Seconds |", "|---|---:|"]
        lines += [f"| {stage} | {seconds:.3f} |" for stage, seconds in timings.items()]
        results = read_results(output)
        outcomes = Counter(r.attrib["outcome"] for r in results)
        lines += ["", f"TRX results: {dict(outcomes)}", "", "### Slowest 20 tests", "",
                  "| Test | Duration | Outcome |", "|---|---:|---|"]
        for r in sorted(results, key=lambda r: r.attrib.get("duration", ""), reverse=True)[:20]:
            name = r.attrib["testName"].replace("|", "\\|").replace("\n", " ")
            lines.append(f"| {name} | {r.attrib.get('duration', '')} | {r.attrib['outcome']} |")
        if any(r.attrib["outcome"] != "Passed" for r in results):
            raise ValueError("Failed, skipped or incomplete tests: see TRX")
        if not json.loads((output / "run-state.json").read_text(encoding="utf-8"))["completed"]:
            raise ValueError("Run did not complete; stale TRX cannot establish PASS")
    except (OSError, ValueError, KeyError, ET.ParseError) as exc:
        error = str(exc)
        lines += ["", f"**INCOMPLETE / FAIL:** {error}"]
    text = "\n".join(lines) + "\n"
    (output / "summary.md").write_text(text, encoding="utf-8")
    if os.environ.get("GITHUB_STEP_SUMMARY"):
        with open(os.environ["GITHUB_STEP_SUMMARY"], "a", encoding="utf-8") as summary:
            summary.write(text)
    print(text)
    if error:
        raise ValueError(error)


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--matrix", action="store_true")
    parser.add_argument("--group")
    parser.add_argument("--results", type=Path)
    parser.add_argument("--configuration", default="Release")
    parser.add_argument("--no-build", action="store_true")
    parser.add_argument("--verify-only", action="store_true")
    parser.add_argument("--report", action="store_true")
    args = parser.parse_args()
    groups = load_groups()
    if args.matrix:
        matrix = json.dumps({"include": [{"group": g["id"], "runner": g["runner"]} for g in groups]})
        print(matrix)
        if os.environ.get("GITHUB_OUTPUT"):
            with open(os.environ["GITHUB_OUTPUT"], "a", encoding="utf-8") as stream:
                stream.write("matrix=" + matrix + "\n")
        return
    group = next(g for g in groups if g["id"] == args.group)
    output = (args.results or ROOT / ".local/backend-ci" / group["id"]).resolve()
    output.mkdir(parents=True, exist_ok=True)
    os.environ["DOTNET_CLI_UI_LANGUAGE"] = "en"
    if args.report:
        report(group, output)
    else:
        run(group, groups, output, args.configuration, args.no_build, args.verify_only)


if __name__ == "__main__":
    try:
        main()
    except Exception as exc:
        print(f"Backend CI ERROR: {exc}", file=sys.stderr)
        sys.exit(1)
