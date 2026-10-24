#!/usr/bin/env python3
"""Normalizes a plain pg_dump read from stdin, for diffing: drops the per-run \\restrict/\\unrestrict
token lines and sorts the rows of every COPY block, so physical row order never shows as a difference."""
import sys


def main() -> None:
    out = []
    rows = None
    for line in sys.stdin:
        if line.startswith("\\restrict") or line.startswith("\\unrestrict"):
            continue
        if rows is not None:
            if line == "\\.\n":
                out.extend(sorted(rows))
                out.append(line)
                rows = None
            else:
                rows.append(line)
            continue
        out.append(line)
        if line.startswith("COPY ") and line.rstrip().endswith("FROM stdin;"):
            rows = []
    sys.stdout.writelines(out)


if __name__ == "__main__":
    main()
