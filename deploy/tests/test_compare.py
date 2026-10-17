import json, os, subprocess, sys, tempfile, textwrap, unittest

HERE = os.path.dirname(__file__)
COMPARE = os.path.join(HERE, "..", "compare.py")

def write(path, rows):
    with open(path, "w") as f:
        for r in rows:
            f.write(json.dumps(r) + "\n")

def run(a, b, allow="changes: []\n"):
    d = tempfile.mkdtemp()
    pa, pb, pl = (os.path.join(d, n) for n in ("a.jsonl", "b.jsonl", "allow.yml"))
    write(pa, a); write(pb, b)
    open(pl, "w").write(textwrap.dedent(allow))
    p = subprocess.run([sys.executable, COMPARE, pa, pb, pl], capture_output=True, text=True)
    return p.returncode, p.stdout

def row(table, pk, **cols):
    return {"table": table, "pk": pk, "row": cols}

class CompareTests(unittest.TestCase):
    def test_identical_snapshots_pass(self):
        rows = [row("Users", "1", Name="a")]
        self.assertEqual(run(rows, rows)[0], 0)

    def test_lost_row_fails(self):
        code, out = run([row("Users", "1", Name="a"), row("Users", "2", Name="b")], [row("Users", "1", Name="a")])
        self.assertEqual(code, 1); self.assertIn("Users", out); self.assertIn("lost", out)

    def test_altered_value_fails_and_names_the_column(self):
        code, out = run([row("Users", "1", Name="a", Age="3")], [row("Users", "1", Name="z", Age="3")])
        self.assertEqual(code, 1); self.assertIn("Name", out); self.assertNotIn("Age", out)

    def test_value_changed_to_null(self):
        code, _ = run([row("Users", "1", Name="a")], [row("Users", "1", Name=None)])
        self.assertEqual(code, 1)

    def test_new_column_new_row_new_table_are_ignored(self):
        a = [row("Users", "1", Name="a")]
        b = [row("Users", "1", Name="a", Extra="x"), row("Users", "2", Name="b"), row("NewTable", "1", X="y")]
        self.assertEqual(run(a, b)[0], 0)

    def test_lost_row_in_table_without_pk(self):
        code, _ = run([row("Log", None, Msg="a"), row("Log", None, Msg="a")], [row("Log", None, Msg="a")])
        self.assertEqual(code, 1)

    def test_allowlisted_alteration_passes(self):
        allow = """
        changes:
          - { table: Users, columns: [Name], change: altered, reason: rename }
        """
        self.assertEqual(run([row("Users", "1", Name="a")], [row("Users", "1", Name="z")], allow)[0], 0)

    def test_allowlist_for_other_column_does_not_cover(self):
        allow = """
        changes:
          - { table: Users, columns: [Age], change: altered, reason: x }
        """
        self.assertEqual(run([row("Users", "1", Name="a")], [row("Users", "1", Name="z")], allow)[0], 1)

    def test_allowlisted_loss_with_star_passes(self):
        allow = """
        changes:
          - { table: Users, columns: "*", change: lost, reason: purge }
        """
        self.assertEqual(run([row("Users", "1", Name="a")], [], allow)[0], 0)

    def test_type_drift_is_not_a_finding(self):
        self.assertEqual(run([row("Users", "1", N=1)], [row("Users", "1", N="1")])[0], 0)

    def test_dropped_column_counts_as_altered(self):
        code, out = run([row("Users", "1", Name="a", Age="3")], [row("Users", "1", Name="a")])
        self.assertEqual(code, 1); self.assertIn("Age", out)
        allow = """
        changes:
          - { table: Users, columns: [Age], change: altered, reason: drop }
        """
        self.assertEqual(run([row("Users", "1", Name="a", Age="3")], [row("Users", "1", Name="a")], allow)[0], 0)

    def test_duplicate_key_is_infrastructure_error(self):
        dup = [row("Users", "1", Name="a"), row("Users", "1", Name="b")]
        self.assertEqual(run(dup, [row("Users", "1", Name="a")])[0], 2)
        self.assertEqual(run([row("Users", "1", Name="a")], dup)[0], 2)

    def test_empty_snapshot_a_is_infrastructure_error(self):
        self.assertEqual(run([], [row("Users", "1", Name="a")])[0], 2)

    def test_pkless_new_column_in_b_is_ignored(self):
        a = [row("Log", None, Msg="a")]
        b = [row("Log", None, Msg="a", Extra="x")]
        self.assertEqual(run(a, b)[0], 0)

    def test_malformed_allowlist_fails_closed(self):
        base = [row("Users", "1", Name="a")]
        for bad in ("{ table: Users, columns: Name, change: altered, reason: x }",
                    "{ table: Users, columns: [Name], change: removed, reason: x }",
                    "{ table: Users, columns: [1], change: altered, reason: x }"):
            code, out = run(base, base, "changes:\n  - " + bad + "\n")
            self.assertEqual(code, 2, bad)

if __name__ == "__main__":
    unittest.main()
