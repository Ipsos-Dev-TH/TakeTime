"""Accuracy table for bench.py results (also usable standalone: python score.py results.csv truth.csv)."""
import csv
import sys
from collections import defaultdict

TOL = 1.00          # SlipVerify_Amount_Tolerance
LOCK = 85           # SlipVerify_Lock_Min_Confidence


def _f(x):
    return float(x) if x not in ("", None) else None


def _wrong(t):
    return round(t / 2, 2) if t >= 200 else t + 100


def stats(rs):
    n = len(rs)
    ok = fa = un = hi = hifa = tv = tm = wd = wv = 0
    ms = sorted(int(r[2] or 0) for r in rs)
    for r in rs:
        t = float(r[1])
        v = _f(r[3])
        if v is None:
            un += 1
        elif abs(v - t) < 0.005:
            ok += 1
        else:
            fa += 1
        if v is not None and int(r[4] or 0) >= LOCK:
            hi += 1
            if abs(v - t) >= 0.005:
                hifa += 1
        v = _f(r[5])
        if v is not None and abs(v - t) <= TOL:
            tv += 1
        elif v is not None:
            tm += 1
        v = _f(r[7])
        if v is not None and abs(v - _wrong(t)) <= TOL:
            wv += 1
        elif v is not None:
            wd += 1

    def p(k):
        return "%5.1f%%" % (100.0 * k / n) if n else "-"
    return [n, p(ok), p(fa), p(un), p(hi), str(hifa), p(tv), p(tm), p(wd), p(wv),
            str(sum(ms) // max(1, n)), str(ms[n // 2] if n else 0), str(ms[min(n - 1, int(n * 0.95))] if n else 0)]


HDR = ["group", "n", "exact", "FALSE", "unread", ">=85", "F@85", "E:verif", "E:fStop", "W:detect", "W:fVerif",
       "ms avg", "p50", "p95"]


def report(rows, meta):
    by = {m["file"]: m for m in meta}
    groups = defaultdict(list)
    aug = defaultdict(list)
    for r in rows:
        m = by.get(r[0], {"bank": "-", "variant": "clean"})
        groups["ALL"].append(r)
        groups[m["bank"]].append(r)
        aug[m["variant"].split(";")[-1].split("%")[0].rstrip("+-0123456789.")].append(r)
    print("  ".join("%-9s" % h for h in HDR))
    for g in ["ALL"] + sorted(k for k in groups if k != "ALL"):
        print("  ".join("%-9s" % x for x in [g] + stats(groups[g])))
    if len(aug) > 1:
        print("-- by variant --")
        for g in sorted(aug):
            print("  ".join("%-9s" % x for x in [g] + stats(aug[g])))
    print("\nexact = returned the right amount (no staff amount) · FALSE = returned a WRONG amount (worst case) ·"
          " unread = bypass with flag\n>=85 = share auto-filled+locked · F@85 = wrong among those (must be 0) ·"
          " E:/W: = staff typed the right / a wrong amount")


if __name__ == "__main__":
    rows = list(csv.reader(open(sys.argv[1], encoding="utf-8")))[1:]
    meta = []
    with open(sys.argv[2], encoding="utf-8-sig") as fh:
        for r in csv.DictReader(fh):
            meta.append({"file": r["file"].lower(), "bank": r.get("bank") or "-", "variant": r.get("variant") or "clean"})
    report(rows, meta)
