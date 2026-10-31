"""Offline accuracy benchmark for the local slip amount reader (reader.py == LocalSlipAmountReader.cs).

    python bench.py --images DIR --truth truth.csv [--out results.csv] [--par 4]

truth.csv: header row with at least `file` and `amount` (optional `bank`, `variant`). `file` may be a bare file name or a
path such as `~/Upload/Slip/2026/10/x.jpg` — it is matched by file name anywhere under --images (recursive).
Each slip is read once (OCR does not depend on the staff amount); the decision is then evaluated three ways:
  none  : no staff amount          -> exact / FALSE (wrong number returned) / unreadable
  true  : staff typed the right amount -> VERIFIED vs false MISMATCH stop
  wrong : staff typed a wrong amount (half, or +100 for small amounts) -> MISMATCH detected vs false VERIFIED
plus the share of reads at confidence >= 85 (UI auto-fill + lock threshold) and how many of those were wrong (F@85).
"""
import argparse
import csv
import os
import sys
from multiprocessing import Pool

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import reader  # noqa: E402
import score  # noqa: E402


def wrong_amount(truth):
    return round(truth / 2, 2) if truth >= 200 else truth + 100


def one(job):
    name, path, truth = job
    try:
        st = reader.ocr_state(path)
    except Exception as e:  # never crash the run
        return [name, "%.2f" % truth, "0", "", "0", "", "0", "", "0", "ERROR " + repr(e)[:120]]
    cells = [name, "%.2f" % truth, str(st["ms"])]
    whys = []
    for exp in (None, truth, wrong_amount(truth)):
        v, c, w = reader.decide(st, exp)
        cells += ["" if v is None else "%.2f" % v, str(c)]
        whys.append(w)
    return cells + [whys[0]]


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--images", required=True)
    ap.add_argument("--truth", required=True)
    ap.add_argument("--out", default="results.csv")
    ap.add_argument("--par", type=int, default=max(1, (os.cpu_count() or 2) // 2))
    a = ap.parse_args()

    index = {}
    for root, _, files in os.walk(a.images):
        for f in files:
            index.setdefault(f.lower(), os.path.join(root, f))
    jobs, meta, missing = [], [], 0
    with open(a.truth, encoding="utf-8-sig") as fh:
        for r in csv.DictReader(fh):
            base = os.path.basename((r.get("file") or "").replace("\\", "/")).lower()
            p = index.get(base)
            if not p or not (r.get("amount") or "").strip():
                missing += 1
                continue
            truth = float(r["amount"].replace(",", ""))
            jobs.append((base, p, truth))
            meta.append({"file": base, "amount": "%.2f" % truth, "bank": r.get("bank") or "-",
                         "variant": r.get("variant") or "clean"})
    if not jobs:
        raise SystemExit("no images matched (missing=%d)" % missing)
    print("reading %d slips (%d rows without a matching image skipped), %d parallel ..." % (len(jobs), missing, a.par))
    with Pool(a.par) as pool:
        rows = pool.map(one, jobs, chunksize=1)
    with open(a.out, "w", newline="", encoding="utf-8") as fh:
        w = csv.writer(fh)
        w.writerow(["file", "truth", "ms", "v_none", "c_none", "v_true", "c_true", "v_wrong", "c_wrong", "why"])
        w.writerows(rows)
    score.report(rows, meta)
    print("\nper-slip results: %s  (why = decision trace; run `python reader.py <slip>` for every pass)" % a.out)


if __name__ == "__main__":
    main()
