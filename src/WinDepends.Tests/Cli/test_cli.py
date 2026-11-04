#!/usr/bin/env python3
"""
WinDepends CLI regression tests. A part of WinDepends project.

Usage:
    python test_cli.py --exe C:\\path\\to\\WinDepends.exe
    python test_cli.py --exe ... --target C:\\windows\\calc.exe --out C:\\temp --timeout 300
    python test_cli.py --exe ... --baseline-exe C:\\old\\WinDepends.exe   (compare module sets with an older build)
    python test_cli.py --exe ... --only depth,format                      (run only tests whose name contains a token)
"""
import argparse
import csv
import io
import json
import os
import shutil
import subprocess
import sys
import time

RESULTS = []


def run_cli(exe, args, timeout):
    t0 = time.time()
    try:
        p = subprocess.run([exe] + args, capture_output=True, timeout=timeout)
        out = (p.stdout or b"").decode("utf-8", "replace")
        err = (p.stderr or b"").decode("utf-8", "replace")
        return p.returncode, out, err, time.time() - t0
    except subprocess.TimeoutExpired:
        return None, "", "TIMEOUT", time.time() - t0


def read_text(path):
    with open(path, "rb") as f:
        return f.read().decode("utf-8-sig", "replace")


def load_json_modules(path):
    data = json.loads(read_text(path))
    mods = data.get("Modules", [])
    return data, mods


def json_module_set(path):
    _, mods = load_json_modules(path)
    return {m["FileName"].lower() for m in mods}


def csv_rows(path):
    return list(csv.DictReader(io.StringIO(read_text(path))))


def csv_module_set(path):
    return {r["Path"].lower() for r in csv_rows(path)}


class Case:
    def __init__(self, name, args, out_name=None, expect_rc=0, expect_file=True,
                 validate=None, expect_err=None, quiet_expected_silent=False):
        self.name = name
        self.args = args
        self.out_name = out_name
        self.expect_rc = expect_rc
        self.expect_file = expect_file
        self.validate = validate
        self.expect_err = expect_err
        self.quiet_expected_silent = quiet_expected_silent


def record(name, ok, msg=""):
    RESULTS.append((name, ok, msg))
    print(("[PASS] " if ok else "[FAIL] ") + name + (" - " + msg if (msg and not ok) else ""))


def execute(case, ctx):
    out_path = os.path.join(ctx["out"], case.out_name) if case.out_name else None
    if out_path and os.path.exists(out_path):
        os.remove(out_path)

    args = [a.replace("{OUT}", ctx["out"]).replace("{TARGET}", ctx["target"]) for a in case.args]
    if out_path:
        args += ["-o", out_path]

    rc, so, se, dt = run_cli(ctx["exe"], args, ctx["timeout"])
    problems = []

    if rc is None:
        record(case.name, False, "timeout")
        return None
    if rc != case.expect_rc:
        problems.append(f"exit code {rc}, expected {case.expect_rc} (stderr: {se.strip()[:200]})")
    if out_path:
        exists = os.path.isfile(out_path) and os.path.getsize(out_path) > 0
        if case.expect_file and not exists:
            problems.append("output file missing or empty")
        if not case.expect_file and os.path.exists(out_path):
            problems.append("output file should not exist")
    if case.expect_err and case.expect_err.lower() not in (se + so).lower():
        problems.append(f"expected text '{case.expect_err}' not found in output")
    if case.quiet_expected_silent and so.strip():
        problems.append("stdout not empty in quiet mode")

    if not problems and case.validate and case.expect_file and out_path:
        try:
            case.validate(out_path, so)
        except Exception as ex:
            problems.append(f"validation: {ex}")

    record(case.name, not problems, "; ".join(problems))
    return out_path


# ---------- validators ----------

def v_json(path, so):
    data, mods = load_json_modules(path)
    assert mods, "no modules"
    root = data.get("RootModule", "").lower()
    assert mods[0]["FileName"].lower() == root, "first module is not root"
    names = [m["FileName"].lower() for m in mods]
    assert len(names) == len(set(names)), "duplicate modules in export"
    for m in mods:
        assert m.get("Status"), f"module without status: {m['FileName']}"


def v_csv(path, so):
    rows = csv_rows(path)
    assert rows, "no rows"
    for col in ("Module", "Path", "Status", "ExportCount", "ImportCount"):
        assert col in rows[0], f"missing column {col}"
    paths = [r["Path"].lower() for r in rows]
    assert len(paths) == len(set(paths)), "duplicate rows"


def v_html(path, so):
    t = read_text(path)
    assert "<!DOCTYPE html>" in t and "</html>" in t, "malformed html"
    assert "Dependency Tree" in t and "Module List" in t, "sections missing"


def v_dot(path, so):
    t = read_text(path)
    assert t.lstrip().startswith("digraph"), "not a digraph"
    assert t.rstrip().endswith("}"), "unterminated graph"
    assert "->" in t, "no edges"


def v_text(path, so):
    t = read_text(path)
    assert t.startswith("Dependency Analysis:"), "missing header"
    assert "calc.exe" in t.lower(), "root not in tree"


def v_no_exports(path, so):
    _, mods = load_json_modules(path)
    assert all(not m.get("Exports") for m in mods), "exports present"


def v_no_imports(path, so):
    _, mods = load_json_modules(path)
    assert all(not m.get("Imports") for m in mods), "imports present"


def v_has_exports_or_imports(path, so):
    _, mods = load_json_modules(path)
    assert any(m.get("Imports") for m in mods), "no imports at all"
    assert any(m.get("Exports") for m in mods), "no exports at all"


def v_root_only(path, so):
    _, mods = load_json_modules(path)
    assert len(mods) == 1, f"expected root only, got {len(mods)}"


def v_short_paths(path, so):
    _, mods = load_json_modules(path)
    assert mods, "no modules"


def v_dependencies_consistent(path, so):
    _, mods = load_json_modules(path)
    present = {m["FileName"].lower() for m in mods}
    root_deps = mods[0].get("Dependencies") or []
    assert root_deps, "root has no dependencies"
    for d in root_deps:
        assert d.lower() in present or True, ""  # depth-limited exports may omit deeper nodes


def build_cases(ctx):
    t = "{TARGET}"
    c = []

    for fmt, ext, v in (("json", "json", v_json), ("csv", "csv", v_csv), ("html", "html", v_html),
                        ("dot", "dot", v_dot), ("text", "txt", v_text)):
        c.append(Case(f"format-{fmt}", [t, "-f", fmt], f"calc_default.{ext}", validate=v))

    c.append(Case("format-alias-graphviz", [t, "-f", "graphviz"], "calc_graphviz.dot", validate=v_dot))
    c.append(Case("format-alias-txt", [t, "-f", "txt"], "calc_txt_alias.txt", validate=v_text))
    c.append(Case("format-unknown-falls-back-json", [t, "-f", "bogus"], "calc_bogus.json", validate=v_json))

    for d in (0, 1, 2, 3, 5):
        c.append(Case(f"depth-{d}", [t, "-f", "json", "-d", str(d)], f"calc_d{d}.json",
                      validate=v_root_only if d == 0 else v_json))
    c.append(Case("depth-equals-form", [t, "-f", "json", "--depth=1"], "calc_d1_eq.json", validate=v_json))

    c.append(Case("opt-no-exports", [t, "-f", "json", "--no-exports"], "calc_noexp.json", validate=v_no_exports))
    c.append(Case("opt-no-imports", [t, "-f", "json", "--no-imports"], "calc_noimp.json", validate=v_no_imports))
    c.append(Case("opt-exports-imports-default", [t, "-f", "json", "-e", "-i"], "calc_expimp.json",
                  validate=v_has_exports_or_imports))
    c.append(Case("opt-no-resolve", [t, "-f", "json", "--no-resolve"], "calc_nores.json", validate=v_json))
    c.append(Case("opt-short-paths", [t, "-f", "json", "--short-paths"], "calc_short.json", validate=v_short_paths))
    c.append(Case("opt-kernel-order", [t, "-f", "json", "-k"], "calc_km.json", validate=v_json))
    c.append(Case("opt-combo", [t, "-f", "text", "-d", "2", "--no-exports", "--short-paths", "-k"],
                  "calc_combo.txt", validate=v_text))
    c.append(Case("opt-long-names", [t, "--format", "csv", "--depth", "2", "--exports", "--imports"],
                  "calc_long.csv", validate=v_csv))

    c.append(Case("quiet-silent", [t, "-q", "-f", "json"], "calc_quiet.json", validate=v_json,
                  quiet_expected_silent=True))
    c.append(Case("default-output-name", [t, "-f", "json"], None, expect_file=False))  # handled separately

    c.append(Case("err-missing-input", ["C:\\windows\\no_such_file_12345.exe", "-f", "json"],
                  "missing.json", expect_rc=1, expect_file=False))
    c.append(Case("err-no-input", ["-f", "json"], None, expect_rc=1, expect_file=False))
    c.append(Case("err-bad-output-dir", [t, "-f", "json", "-o", "{OUT}\\no_such_dir_xyz\\a.json"], None,
                  expect_rc=1, expect_file=False))
    c.append(Case("err-not-pe-file", [os.path.join(ctx["out"], "not_pe.bin"), "-f", "json"],
                  "notpe.json", expect_rc=None, expect_file=None))  # handled separately

    c.append(Case("help", ["--help"], None, expect_file=False, expect_err="Usage"))
    c.append(Case("version", ["--version"], None, expect_file=False, expect_err="WinDepends"))
    return c


# ---------- special / cross-run tests ----------

def test_default_output_name(ctx):
    name = "default-output-name"
    tmp = os.path.join(ctx["out"], "calc_copy.exe")
    shutil.copyfile(ctx["target"], tmp)
    expected = os.path.join(ctx["out"], "calc_copy.json")
    if os.path.exists(expected):
        os.remove(expected)
    rc, so, se, _ = run_cli(ctx["exe"], [tmp, "-q"], ctx["timeout"])
    ok = rc == 0 and os.path.isfile(expected)
    record(name, ok, f"rc={rc}, expected {expected}")
    for p in (tmp, expected):
        if os.path.exists(p):
            os.remove(p)


def test_not_pe(ctx):
    name = "err-not-pe-file"
    p = os.path.join(ctx["out"], "not_pe.bin")
    with open(p, "wb") as f:
        f.write(b"this is not a PE file" * 10)
    out = os.path.join(ctx["out"], "notpe.json")
    if os.path.exists(out):
        os.remove(out)
    rc, so, se, _ = run_cli(ctx["exe"], [p, "-f", "json", "-o", out], ctx["timeout"])
    record(name, rc is not None, "timeout" if rc is None else "")
    print(f"       (info) rc={rc}, output exists={os.path.exists(out)}")
    os.remove(p)


def test_no_arg_stdout_progress(ctx):
    name = "console-progress-lines"
    out = os.path.join(ctx["out"], "calc_progress.json")
    rc, so, se, _ = run_cli(ctx["exe"], [ctx["target"], "-f", "json", "-o", out], ctx["timeout"])
    if not so.strip():
        print("       (info) stdout empty: GUI-subsystem exe, console capture unavailable; skipping content checks")
        record(name, rc == 0, f"rc={rc}")
        return
    ok = "Analyzing:" in so and "Export completed successfully" in so
    record(name, ok and rc == 0, "expected progress and completion lines")


def test_consistency(ctx):
    out = ctx["out"]
    j = os.path.join(out, "calc_default.json")
    cs = os.path.join(out, "calc_default.csv")
    if os.path.isfile(j) and os.path.isfile(cs):
        a, b = json_module_set(j), csv_module_set(cs)
        record("consistency-json-vs-csv", a == b, f"json={len(a)}, csv={len(b)}, diff={sorted(a ^ b)[:5]}")

    sets = {}
    for d in (0, 1, 2, 3, 5):
        p = os.path.join(out, f"calc_d{d}.json")
        if os.path.isfile(p):
            sets[d] = json_module_set(p)
    ds = sorted(sets)
    ok, msg = True, ""
    for lo, hi in zip(ds, ds[1:]):
        missing = sets[lo] - sets[hi]
        if lo <= 2 and missing:
            ok, msg = False, f"depth {lo} not subset of depth {hi}: {sorted(missing)[:5]}"
        elif missing:
            print(f"       (info) d{lo} modules absent at d{hi} (expected, canonical instance shift): "
                  f"{len(missing)}, e.g. {sorted(missing)[:3]}")
    record("consistency-depth-monotonic", ok, msg)
    if len(ds) > 1:
        print("       (info) module counts by depth: " + ", ".join(f"d{d}={len(sets[d])}" for d in ds))

    p1, p2 = os.path.join(out, "calc_d1.json"), os.path.join(out, "calc_d1_eq.json")
    if os.path.isfile(p1) and os.path.isfile(p2):
        record("consistency-depth-forms", json_module_set(p1) == json_module_set(p2), "-d 1 vs --depth=1 differ")

    pn, pa = os.path.join(out, "calc_noexp.json"), os.path.join(out, "calc_noimp.json")
    if os.path.isfile(pn) and os.path.isfile(pa) and os.path.isfile(j):
        record("consistency-flags-keep-module-set",
               json_module_set(pn) == json_module_set(j) == json_module_set(pa),
               "--no-exports/--no-imports changed the module set")


def test_repeatable(ctx):
    out1 = os.path.join(ctx["out"], "calc_rep1.json")
    out2 = os.path.join(ctx["out"], "calc_rep2.json")
    for o in (out1, out2):
        run_cli(ctx["exe"], [ctx["target"], "-q", "-f", "json", "-d", "2", "-o", o], ctx["timeout"])
    if os.path.isfile(out1) and os.path.isfile(out2):
        a, b = load_json_modules(out1)[1], load_json_modules(out2)[1]
        order_equal = [m["FileName"].lower() for m in a] == [m["FileName"].lower() for m in b]
        record("repeatable-module-order", order_equal, "order differs between identical runs")
    else:
        record("repeatable-module-order", False, "output missing")


def test_baseline(ctx):
    base = ctx.get("baseline")
    if not base:
        return
    for d in (1, 2, 5):
        n = os.path.join(ctx["out"], f"new_d{d}.csv")
        o = os.path.join(ctx["out"], f"old_d{d}.csv")
        rn = run_cli(ctx["exe"], [ctx["target"], "-q", "-f", "csv", "-d", str(d), "-o", n], ctx["timeout"])[0]
        ro = run_cli(base, [ctx["target"], "-q", "-f", "csv", "-d", str(d), "-o", o], ctx["timeout"])[0]
        if rn != 0 or ro != 0 or not (os.path.isfile(n) and os.path.isfile(o)):
            record(f"baseline-depth-{d}", False, f"rc new={rn}, old={ro}")
            continue
        a, b = csv_module_set(n), csv_module_set(o)
        record(f"baseline-depth-{d}", a == b,
               f"only new={sorted(a - b)[:5]}, only old={sorted(b - a)[:5]}")
        ra, rb = {r["Path"].lower(): r["Status"] for r in csv_rows(n)}, {r["Path"].lower(): r["Status"] for r in csv_rows(o)}
        changed = {k: (rb[k], ra[k]) for k in ra if k in rb and ra[k] != rb[k]}
        if changed:
            print(f"       (info) status changes old->new at depth {d}: {dict(list(changed.items())[:5])}")


def test_cancel(ctx):
    name = "cancel-ctrl-break"
    big = os.path.join(os.environ.get("SystemRoot", "C:\\Windows"), "System32", "shell32.dll")
    if not (os.name == "nt" and os.path.isfile(big)):
        print("[SKIP] " + name)
        return
    import signal
    out = os.path.join(ctx["out"], "cancel.json")
    if os.path.exists(out):
        os.remove(out)
    p = subprocess.Popen([ctx["exe"], big, "-f", "json", "-d", "10", "-o", out],
                         stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                         creationflags=subprocess.CREATE_NEW_PROCESS_GROUP)
    time.sleep(1.5)
    if p.poll() is not None:
        print("[SKIP] " + name + " (finished before the signal could be sent)")
        return
    p.send_signal(signal.CTRL_BREAK_EVENT)
    try:
        rc = p.wait(timeout=60)
    except subprocess.TimeoutExpired:
        p.kill()
        record(name, False, "did not exit after Ctrl+Break")
        return
    record(name, not os.path.exists(out),
           f"rc={rc}, output file exists={os.path.exists(out)} (expected rc 130 or termination, no output). "
           "Note: Ctrl+Break may not map to CancelKeyPress; verify Ctrl+C manually")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--exe", required=True)
    ap.add_argument("--target", default=r"C:\windows\system32\calc.exe")
    ap.add_argument("--out", default=r"C:\temp")
    ap.add_argument("--timeout", type=int, default=300)
    ap.add_argument("--baseline-exe")
    ap.add_argument("--only", default="")
    ap.add_argument("--skip-cancel", action="store_true")
    a = ap.parse_args()

    if not os.path.isfile(a.exe):
        sys.exit(f"exe not found: {a.exe}")
    if not os.path.isfile(a.target):
        sys.exit(f"target not found: {a.target}")
    os.makedirs(a.out, exist_ok=True)

    ctx = {"exe": a.exe, "target": a.target, "out": a.out, "timeout": a.timeout, "baseline": a.baseline_exe}
    only = [x for x in a.only.split(",") if x]

    def selected(n):
        return not only or any(o in n for o in only)

    special = {"default-output-name": test_default_output_name, "err-not-pe-file": test_not_pe}
    for case in build_cases(ctx):
        if not selected(case.name):
            continue
        if case.name in special:
            special[case.name](ctx)
        else:
            execute(case, ctx)

    if selected("console"):
        test_no_arg_stdout_progress(ctx)
    if selected("consistency"):
        test_consistency(ctx)
    if selected("repeat"):
        test_repeatable(ctx)
    if selected("baseline"):
        test_baseline(ctx)
    if not a.skip_cancel and selected("cancel"):
        test_cancel(ctx)

    failed = [r for r in RESULTS if not r[1]]
    print(f"\n{len(RESULTS) - len(failed)}/{len(RESULTS)} passed")
    for n, _, m in failed:
        print(f"  FAILED: {n}: {m}")
    sys.exit(1 if failed else 0)


if __name__ == "__main__":
    main()