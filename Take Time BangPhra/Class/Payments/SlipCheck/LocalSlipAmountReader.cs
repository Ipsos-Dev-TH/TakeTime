using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Hosting;

namespace Take_Time_BangPhra.Payments.SlipCheck
{
    /// <summary>
    /// ระดับที่ 2 — "อ่านค่าตัวเลขโอน" จากรูปสลิปเอง (ฟรี ในเครื่อง) เมื่อโควต้า SlipOK หมด/ใช้ไม่ได้
    ///
    /// เครื่องมือ: Tesseract 5 แบบโปรแกรม (tesseract.exe) เรียกผ่าน Process — ไม่ใช่ NuGet "Tesseract" (P/Invoke)
    /// เหตุผล: (1) native crash ใน leptonica/tesseract จะล้มทั้ง w3wp (จับด้วย try/catch ไม่ได้) — แยก process ปลอดภัยกว่า
    /// (2) ไม่ผูก bitness x86/x64 ของ App Pool และ VC++ runtime กับโปรเจกต์ (3) ตั้ง timeout แล้ว kill ได้จริง
    /// (4) ไม่ต้องเพิ่ม DLL native ใน bin/ — ไม่มีโปรแกรม = ข้ามไปขั้น bypass เอง หน้าไม่พัง
    ///
    /// ── ขั้นตอน (พอร์ต 1:1 จากต้นแบบ tools/slip-ocr-bench/reader.py — แก้ที่นี่ต้องแก้คู่กัน) ──
    ///  1. เตรียมรูป: EXIF หมุนรูป → ตัดเฉพาะการ์ดสลิปสีขาว (รูปถ่ายจอมือถือ: min(R,G,B) สูง, ก้อนใหญ่สุดบนภาพย่อ ~240px)
    ///     → ขาวดำ (BT.601) → โหมดมืด (ค่าเฉลี่ย &lt; 100) กลับสี → ปรับกว้าง ~1500px → ยืด contrast 1–99%
    ///  2. รอบ 1: tesseract tha+eng --psm 6 แบบ TSV (ได้กรอบคำ) → บรรทัด → หาป้าย "จำนวนเงิน/จำนวน/ยอดชำระ/Amount"
    ///     และตัวเลขเงิน ให้คะแนนตามตำแหน่ง (ขวาของป้ายแถวเดียวกัน หรือใต้ป้าย) · ไม่เจอป้าย = รอบ 1b (--psm 11 ย่อ 0.72)
    ///  3. รอบ 2: ตัดกรอบตัวเลข (ROI) ของผู้สมัคร 3 อันดับ + แถบขวา/ใต้ป้าย → ลบคำที่เป็นตัวอักษร (บาท/THB) → ปรับสูง ~48px
    ///     → ต่อเป็นภาพเดียว 2 แบบ (เทา + Otsu) → tesseract eng --psm 6 whitelist "0123456789.,B" (เรียกครั้งเดียว)
    ///     ลบเครื่องหมาย ฿ ด้วยรูปทรง (ก้อนซ้ายสุดที่สูงกว่าตัวเลขทั้งบนและล่าง) กัน "฿15,000.00" → "815,000.00"
    ///  4. โหวต: ค่าที่รอบ 1 และรอบ 2 (หลายแบบ) อ่านตรงกันในบริบทป้ายจำนวนเงิน = มั่นใจสูง; ขัดกัน/กำกวม = ไม่คืนค่า
    ///     (อ่านผิดแย่กว่าอ่านไม่ได้ — อ่านไม่ได้ = ข้ามการตรวจ+ติดธง, อ่านผิด = หยุดพนักงานผิด ๆ หรือล็อกยอดผิด)
    ///
    /// ความมั่นใจ (ใช้กับ SlipVerify_Lock_Min_Confidence ค่าเริ่ม 85): 92 = รอบ 1 + รอบ 2 ≥ 2 แบบ ตรงกัน ในบริบทป้าย ไม่มีคู่แข่ง
    /// · 80 = รอบ 2 ≥ 2 แบบในบริบทป้าย (รอบ 1 ไม่เห็น/฿ แก้จากรอบ 2) · 72 = รอบ 1 + รอบ 2 แบบเดียว · 62 = ไม่เจอป้าย แต่มีตัวเลขเงินตัวเดียว
    /// และทุกรอบตรงกัน · 70/60 = เลือกตามยอดที่ลง (ตัวเลขนั้นต้องอยู่ในบริบทจำนวนเงินจริง) · &lt; SlipVerify_Ocr_Min_Confidence (60) = ไม่คืนค่า
    /// Benchmark (สลิปจำลอง 9 ธนาคาร + สลิปจริง 6 ใบ + augmentation รวม 602 รูป): ดู CLAUDE.md ข้อ 18 / tools/slip-ocr-bench
    ///
    /// ติดตั้ง (ดู CLAUDE.md ข้อ 18): Tesseract 5 for Windows (UB Mannheim) → C:\Program Files\Tesseract-OCR พร้อมภาษา Thai
    /// หรือวาง tha.traineddata + eng.traineddata (tessdata_fast) ไว้ที่ ~/App_Data/tessdata แล้วตั้ง SlipVerify_Tessdata_Dir
    /// </summary>
    public static class LocalSlipAmountReader
    {
        public class OcrText
        {
            public bool Ok;
            public string Text;          // ข้อความรอบ 1 (แสดงผล/เก็บ log)
            public string Error;
            public long ElapsedMs;
            public SlipOcrResult Result; // ข้อเท็จจริงทั้งหมด (ไม่ขึ้นกับยอดที่ลง) — ส่งต่อให้ ExtractAmount(…, result)
        }

        // ── ค่าคงที่ของอัลกอริทึม (ต้องตรงกับต้นแบบ Python) ──────────────────────────────────────
        private const int TargetWidth = 1500;
        private const int RoiTextHeight = 48;
        private const int MontageGap = 28;
        private const double Pass1bScale = 0.72;
        private const decimal MaxAmount = 2000000m;
        private const long MaxPixels = 40000000L;
        // TSV ผ่าน -c (ไม่พึ่งไฟล์ configs/tsv — tessdata ที่วางเองใน App_Data มักไม่มีโฟลเดอร์ configs)
        private const string TsvArgs = "-c tessedit_create_tsv=1 -c tessedit_create_txt=0";

        // ── หาโปรแกรม ───────────────────────────────────────────────────────────

        public static string ResolveTesseractPath()
        {
            string cfg = AppCfg.Get("SlipVerify_Tesseract_Path", "");
            if (!string.IsNullOrWhiteSpace(cfg)) return cfg.Trim().Trim('"');
            var candidates = new List<string>
            {
                @"C:\Program Files\Tesseract-OCR\tesseract.exe",
                @"C:\Program Files (x86)\Tesseract-OCR\tesseract.exe"
            };
            try
            {
                string app = HostingEnvironment.MapPath("~/App_Data/tesseract/tesseract.exe");
                if (!string.IsNullOrEmpty(app)) candidates.Insert(0, app);
            }
            catch { }
            foreach (string c in candidates)
            {
                try { if (File.Exists(c)) return c; } catch { }
            }
            return "tesseract";   // ลองจาก PATH
        }

        public static string ResolveTessdataDir()
        {
            string cfg = AppCfg.Get("SlipVerify_Tessdata_Dir", "");
            try
            {
                if (!string.IsNullOrWhiteSpace(cfg))
                {
                    string p = cfg.Trim();
                    if (p.StartsWith("~/", StringComparison.Ordinal)) p = HostingEnvironment.MapPath(p);
                    return p;
                }
                string app = HostingEnvironment.MapPath("~/App_Data/tessdata");
                if (!string.IsNullOrEmpty(app) && Directory.Exists(app) && Directory.GetFiles(app, "*.traineddata").Length > 0)
                    return app;
            }
            catch { }
            return null;
        }

        /// <summary>สถานะ (หน้าตั้งค่า) — รัน --version สั้น ๆ</summary>
        public static string EngineStatus(out bool ready)
        {
            ready = false;
            string exe = ResolveTesseractPath();
            try
            {
                string outp, err;
                int code = Run(exe, "--version", 8000, out outp, out err);
                string first = ((outp ?? "") + "\n" + (err ?? "")).Trim().Split('\n')[0].Trim();
                ready = code == 0 || first.StartsWith("tesseract", StringComparison.OrdinalIgnoreCase);
                string td = ResolveTessdataDir();
                return (ready ? first : "เรียกไม่สำเร็จ (exit " + code + ")") + " · " + exe
                    + (td != null ? " · tessdata " + td : "");
            }
            catch (Exception ex)
            {
                return "ไม่พบโปรแกรม Tesseract (" + exe + ") — " + ex.GetType().Name;
            }
        }

        // ── โครงสร้างผล ──────────────────────────────────────────────────────────

        public struct Box
        {
            public int L, T, R, B;
            public Box(int l, int t, int r, int b) { L = l; T = t; R = r; B = b; }
            public override string ToString() { return L + "," + T + "-" + R + "," + B; }
        }

        public class Word
        {
            public string Text;
            public int L, T, W, H;
        }

        public class Line
        {
            public List<Word> Words;
            public string Text;
            public List<int[]> Spans;   // [start,end) ของแต่ละคำใน Text
            public Box Box;
            public int HMed;
        }

        public class Label
        {
            public int Line;
            public Box Box;
            public bool Strong;
        }

        public class Cand
        {
            public decimal Value;
            public int Score;
            public int Line;
            public string Raw;
            public Box Box;
            public string Ctx = "";      // "right" | "below" | ""
            public int H;
            public bool Baht;
        }

        public class Roi
        {
            public string Kind;          // cand | right | below
            public Cand Cand;
            public int LabelLine;
            public Box Source;           // กรอบในรูปรอบ 1
            public bool BahtRemoved;
            public bool BahtSuspect;
            internal Gray Img;
        }

        public class Vote
        {
            public Roi Roi;
            public string Ver;           // g = เทา, b = Otsu
            public decimal Value;
            public string Raw;
            public bool BahtPrefix;
        }

        /// <summary>ข้อเท็จจริงจากรูป (ไม่ขึ้นกับยอดที่ลง) — cache ไปกับ SlipInspection แล้วตัดสินซ้ำได้ทุกยอด</summary>
        public class SlipOcrResult
        {
            public string Pass1Text = "";
            public string Pass1bText;
            public string Pass2Text;
            public bool Dark;
            public bool ThaiMissing;
            public Box? Crop;
            public int PageWidth, PageHeight;
            public List<Label> Labels = new List<Label>();
            public List<Cand> Cands = new List<Cand>();
            public List<Cand> Top = new List<Cand>();
            public List<Roi> Rois = new List<Roi>();
            public List<Vote> Votes = new List<Vote>();
            public List<string> Notes = new List<string>();
            public byte[] MontagePng;    // ภาพที่ส่งรอบ 2 (หน้าตั้งค่า "ทดสอบด้วยสลิป")
            public long ElapsedMs;

            /// <summary>รายละเอียดสำหรับแอดมิน: ข้อความแต่ละรอบ, ป้าย, ผู้สมัคร, ROI, โหวต</summary>
            public string Diagnostics(decimal? expected)
            {
                var sb = new StringBuilder();
                int conf; string why;
                decimal? v = Decide(this, expected, out conf, out why);
                sb.Append("ผล: ").Append(v.HasValue ? v.Value.ToString("N2", CultureInfo.InvariantCulture) : "อ่านไม่ได้")
                  .Append(" · ความมั่นใจ ").Append(conf).Append(" · ").Append(why).Append('\n');
                sb.Append("รูปรอบ 1 ").Append(PageWidth).Append('x').Append(PageHeight)
                  .Append(Crop.HasValue ? " · ตัดการ์ด " + Crop.Value : " · ไม่ตัดการ์ด")
                  .Append(Dark ? " · โหมดมืด (กลับสี)" : "").Append(ThaiMissing ? " · ไม่มีภาษาไทย (อ่านอังกฤษ)" : "")
                  .Append(" · ").Append(ElapsedMs).Append(" ms\n");
                foreach (string n in Notes) sb.Append("หมายเหตุ: ").Append(n).Append('\n');
                sb.Append("ป้าย: ").Append(Labels.Count == 0 ? "-" : string.Join(" | ", Labels.Select(l => "บรรทัด " + (l.Line + 1) + " [" + l.Box + "]" + (l.Strong ? "" : " (อ่อน)")))).Append('\n');
                sb.Append("ผู้สมัคร (รอบ 1):\n");
                foreach (Cand c in Cands)
                    sb.Append("  ").Append(c.Value.ToString("N2", CultureInfo.InvariantCulture)).Append(" คะแนน ").Append(c.Score)
                      .Append(string.IsNullOrEmpty(c.Ctx) ? "" : " " + c.Ctx).Append(" \"").Append(c.Raw).Append("\" [").Append(c.Box).Append("]")
                      .Append(Top.Contains(c) ? " ★" : "").Append('\n');
                sb.Append("ROI (รอบ 2):\n");
                for (int i = 0; i < Rois.Count; i++)
                {
                    Roi r = Rois[i];
                    sb.Append("  #").Append(i + 1).Append(' ').Append(r.Kind).Append(" [").Append(r.Source).Append("]")
                      .Append(r.BahtRemoved ? " ลบ ฿" : "").Append(r.BahtSuspect ? " สงสัย ฿" : "").Append(" → ");
                    var vs = Votes.Where(x => x.Roi == r).Select(x => x.Ver + ":" + x.Raw).ToList();
                    sb.Append(vs.Count == 0 ? "-" : string.Join(" ", vs)).Append('\n');
                }
                sb.Append("──────── รอบ 1 (tha+eng psm 6) ────────\n").Append(Pass1Text).Append('\n');
                if (Pass1bText != null) sb.Append("──────── รอบ 1b (psm 11) ────────\n").Append(Pass1bText).Append('\n');
                if (Pass2Text != null) sb.Append("──────── รอบ 2 (ตัวเลขใน ROI) ────────\n").Append(Pass2Text).Append('\n');
                return sb.ToString();
            }
        }

        // ── อ่านรูป ─────────────────────────────────────────────────────────────

        public static OcrText ReadText(byte[] imageBytes)
        {
            var sw = Stopwatch.StartNew();
            var res = new OcrText();
            try
            {
                if (!AppCfg.GetBool("SlipVerify_Local_Ocr", true)) { res.Error = "ปิด OCR ในเครื่องไว้ (SlipVerify_Local_Ocr=0)"; return res; }
                string err;
                SlipOcrResult r = Analyze(imageBytes, out err);
                if (r == null) { res.Error = err; return res; }
                res.Ok = true;
                res.Result = r;
                res.Text = r.Pass1Text ?? "";
            }
            catch (Exception ex)
            {
                res.Error = "OCR ในเครื่องใช้ไม่ได้: " + ex.GetType().Name + " " + Trunc(ex.Message, 160);
            }
            finally
            {
                res.ElapsedMs = sw.ElapsedMilliseconds;
                if (res.Result != null) res.Result.ElapsedMs = res.ElapsedMs;
            }
            return res;
        }

        private static SlipOcrResult Analyze(byte[] bytes, out string error)
        {
            error = null;
            var sw = Stopwatch.StartNew();
            int timeout = Math.Max(2000, AppCfg.GetInt("SlipVerify_Ocr_Timeout_Ms", 15000));
            int budget = Math.Max(timeout, AppCfg.GetInt("SlipVerify_Ocr_Budget_Ms", 20000));

            Gray g, mn;
            if (!Decode(bytes, out g, out mn, out error)) return null;
            var st = new SlipOcrResult();

            Box? cb = CardBox(mn);
            if (cb.HasValue)
            {
                g = g.Crop(cb.Value.L, cb.Value.T, cb.Value.R, cb.Value.B);
                st.Crop = cb;
            }
            if (g.Mean() < 100.0)
            {
                g.Invert();
                st.Dark = true;
            }
            double f = Math.Max(0.5, Math.Min(3.0, TargetWidth / (double)g.W));
            if (Math.Abs(f - 1) > 0.05) g = g.Resize(PyRound(g.W * f), PyRound(g.H * f));
            g = Stretch(g);
            st.PageWidth = g.W;
            st.PageHeight = g.H;

            string exe = ResolveTesseractPath();
            string td = ResolveTessdataDir();
            string tdArg = string.IsNullOrEmpty(td) ? "" : " --tessdata-dir \"" + td.TrimEnd('\\', '/') + "\"";
            string lang = "tha+eng";
            string tsv, terr;
            int code = RunTess(exe, g, "-l " + lang + " --psm 6" + tdArg + " " + TsvArgs, Remaining(sw, budget, timeout), out tsv, out terr);
            if (code != 0 && (terr ?? "").IndexOf("tha", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // ไม่มีภาษาไทย → อ่านอังกฤษ (ตัวเลข/"Amount" ยังอ่านได้ แต่ป้ายไทยหาย — มั่นใจน้อยลงเอง)
                lang = "eng";
                st.ThaiMissing = true;
                code = RunTess(exe, g, "-l eng --psm 6" + tdArg + " " + TsvArgs, Remaining(sw, budget, timeout), out tsv, out terr);
            }
            if (code != 0)
            {
                error = "Tesseract exit " + code + ": " + Trunc(terr, 200);
                return null;
            }
            List<Line> lines = ParseTsv(tsv, 1.0);
            st.Pass1Text = string.Join("\n", lines.Select(l => l.Text));
            FindCandidates(lines, g.W, st.Labels, st.Cands);

            if (st.Labels.Count == 0 && sw.ElapsedMilliseconds < budget / 2)
            {
                // ไม่เจอป้ายจำนวนเงิน (ลายพื้น/แสงสะท้อน/ฟอนต์แปลก) → อ่านทั้งหน้าอีกรอบที่ขนาดต่างไป โหมดข้อความกระจาย
                Gray gb = g.Resize(PyRound(g.W * Pass1bScale), PyRound(g.H * Pass1bScale));
                string tsvB, errB;
                int codeB = RunTess(exe, gb, "-l " + lang + " --psm 11" + tdArg + " " + TsvArgs, Remaining(sw, budget, timeout), out tsvB, out errB);
                List<Line> linesB = codeB == 0 ? ParseTsv(tsvB, 1.0 / Pass1bScale) : new List<Line>();
                st.Pass1bText = string.Join("\n", linesB.Select(l => l.Text));
                var labB = new List<Label>();
                var candB = new List<Cand>();
                FindCandidates(linesB, g.W, labB, candB);
                if (labB.Count > 0)
                {
                    // รวม: ป้ายจากรอบ 1b ให้บริบทกับตัวเลขของรอบ 1 ได้ (พิกัดเดียวกัน)
                    lines = lines.Concat(linesB).ToList();
                    lines = lines.OrderBy(l => (l.Box.T + l.Box.B) / 2.0).ToList();
                    st.Labels.Clear();
                    st.Cands.Clear();
                    FindCandidates(lines, g.W, st.Labels, st.Cands);
                }
            }

            BuildRois(BlankWords(g, lines, st.Cands), st);
            List<int[]> spans;
            Gray can = Montage(st.Rois, out spans);
            if (can != null)
            {
                if (sw.ElapsedMilliseconds >= budget)
                    st.Notes.Add("หมดเวลารอบ 2 (" + budget + " ms)");
                else
                {
                    try { st.MontagePng = can.ToPng(); } catch { }
                    string tsv2, err2;
                    int code2 = RunTess(exe, can, "-l eng --psm 6" + tdArg + " -c tessedit_char_whitelist=0123456789.,B " + TsvArgs,
                        Remaining(sw, budget, timeout), out tsv2, out err2);
                    List<Line> l2 = code2 == 0 ? ParseTsv(tsv2, 1.0) : new List<Line>();
                    if (code2 != 0) st.Notes.Add("รอบ 2 ล้ม: exit " + code2 + " " + Trunc(err2, 120));
                    st.Pass2Text = string.Join("\n", l2.Select(l => l.Text));
                    foreach (Line L in l2)
                    {
                        double cy = (L.Box.T + L.Box.B) / 2.0;
                        foreach (int[] sp in spans)
                        {
                            if (cy >= sp[0] - 6 && cy <= sp[1] + 6)
                            {
                                Roi r = st.Rois[sp[2]];
                                string ver = sp[3] == 0 ? "g" : "b";
                                foreach (Match m in MoneyRe.Matches(L.Text))
                                {
                                    decimal? v = ParseValue(m);
                                    if (!v.HasValue) continue;
                                    st.Votes.Add(new Vote
                                    {
                                        Roi = r, Ver = ver, Value = v.Value, Raw = m.Value,
                                        BahtPrefix = BahtTailRe.IsMatch(L.Text.Substring(0, m.Index))
                                    });
                                }
                                break;
                            }
                        }
                    }
                }
            }
            foreach (Roi r in st.Rois) r.Img = null;   // ไม่เก็บภาพใน cache
            return st;
        }

        private static int Remaining(Stopwatch sw, int budget, int timeout)
        {
            long left = budget - sw.ElapsedMilliseconds;
            return (int)Math.Max(1500, Math.Min(timeout, left));
        }

        // ── ตัดสิน ──────────────────────────────────────────────────────────────

        private class Tally
        {
            public decimal Value;
            public int W;
            public List<string> Src = new List<string>();
            public bool Ctx, P1, Baht, BahtGlyph, BahtP2Only, BahtSuspect, Contra;
            public int P2;
            public decimal? Conflict;
        }

        private static string F2(decimal v) { return v.ToString("0.00", CultureInfo.InvariantCulture); }

        /// <summary>เลขของ vLong = เลขนำหน้า 1 ตัว + เลขของ vShort (฿ ถูกอ่านเป็น 8/6/3…)</summary>
        private static bool StripLead(decimal vLong, decimal vShort)
        {
            string a = F2(vLong).Replace(".", "");
            string b = F2(vShort).Replace(".", "");
            return a.Length == b.Length + 1 && a.Substring(1) == b && "8635B9".IndexOf(a[0]) >= 0;
        }

        /// <summary>รวมโหวตรอบ 1 + รอบ 2 → ยอด/ความมั่นใจ (expected ใช้เลือกได้เฉพาะตัวเลขที่อ่านได้ในบริบทจำนวนเงินจริง)</summary>
        public static decimal? Decide(SlipOcrResult st, decimal? expected, out int confidence, out string why)
        {
            confidence = 0;
            var tally = new List<Tally>();
            Func<decimal, int, string, bool, Tally> add = (v, w, src, ctx) =>
            {
                Tally e = tally.FirstOrDefault(x => x.Value == v);
                if (e == null) { e = new Tally { Value = v }; tally.Add(e); }
                e.W += w;
                e.Src.Add(src);
                e.Ctx = e.Ctx || ctx;
                return e;
            };
            foreach (Cand c in st.Top)
            {
                Tally e = add(c.Value, c.Ctx.Length > 0 ? 2 : 1, "p1:" + (c.Ctx.Length > 0 ? c.Ctx : "-") + c.Score, c.Ctx.Length > 0);
                e.P1 = true;
            }
            foreach (Vote v in st.Votes)
            {
                Roi r = v.Roi;
                Tally e;
                if (r.Kind == "cand")
                {
                    bool ctx = r.Cand.Ctx.Length > 0;
                    e = add(v.Value, ctx ? 2 : 1, "p2c:" + v.Ver + (r.BahtRemoved ? "$" : ""), ctx);
                    if (r.BahtRemoved || v.BahtPrefix) e.Baht = true;
                    if (r.BahtRemoved) e.BahtGlyph = true;
                    if (r.BahtSuspect && "86".IndexOf(F2(v.Value)[0]) >= 0) e.BahtSuspect = true;
                    decimal cv = r.Cand.Value;
                    if (StripLead(cv, v.Value) && r.Cand.Raw.Length > 0 && "86".IndexOf(r.Cand.Raw[0]) >= 0)
                    {
                        e.Baht = true;          // รอบ 2 บนกรอบเดียวกันบอกว่า "8" ตัวหน้าของรอบ 1 คือ ฿
                        e.BahtP2Only = true;
                    }
                    if (Math.Abs(cv - v.Value) > 0.005m && !StripLead(cv, v.Value) && !StripLead(v.Value, cv))
                        e.Contra = true;        // รอบ 2 อ่านจุดเดียวกันได้คนละค่ากับรอบ 1
                }
                else
                {
                    e = add(v.Value, 1, "p2" + r.Kind[0] + ":" + v.Ver, true);
                    if (v.BahtPrefix) e.Baht = true;
                }
                e.P2++;
            }
            if (tally.Count == 0) { why = "ไม่พบตัวเลขจำนวนเงิน"; return null; }

            // ฿ ถูกอ่านเป็นเลขนำหน้า: รวม "8"+X เข้ากับ X เมื่อมีหลักฐานว่าเป็นเครื่องหมายบาท
            foreach (Tally el0 in tally.ToList())
            {
                foreach (Tally es0 in tally.ToList())
                {
                    if (!tally.Contains(el0) || !tally.Contains(es0) || el0 == es0 || !StripLead(el0.Value, es0.Value)) continue;
                    if (es0.Baht)
                    {
                        tally.Remove(el0);
                        es0.W += el0.W;
                        es0.Src.AddRange(el0.Src.Select(s => "~" + s));
                        es0.Ctx = es0.Ctx || el0.Ctx;
                        es0.P1 = es0.P1 || el0.P1;
                        es0.P2 += el0.P2;
                        es0.Contra = es0.Contra || el0.Contra;
                    }
                    else
                    {
                        el0.Conflict = es0.Value;
                        es0.Conflict = el0.Value;
                    }
                }
            }
            List<Tally> ranked = tally.OrderByDescending(x => x.W).ToList();   // stable
            Tally e1 = ranked[0];
            int w2 = ranked.Count > 1 ? ranked[1].W : 0;
            int margin = e1.W - w2;
            int conf;
            if (e1.Ctx && e1.P1 && e1.P2 >= 2 && margin >= 3) conf = 92;
            else if (e1.Ctx && e1.P2 >= 2 && margin >= 2) conf = 80;
            else if (e1.Ctx && e1.P1 && e1.P2 >= 1 && margin >= 2) conf = 72;
            else if (!e1.Ctx && e1.P1 && e1.P2 >= 2 && ranked.Count == 1) conf = 62;   // ไม่เจอป้าย แต่มีตัวเลขเงินตัวเดียวและทุกรอบตรงกัน
            else conf = 30;
            if (e1.Conflict.HasValue) conf = Math.Min(conf, 40);
            if (e1.BahtP2Only && !e1.BahtGlyph) conf = Math.Min(conf, 80);
            if (!e1.P1 && "86".IndexOf(F2(e1.Value)[0]) >= 0 && !e1.BahtGlyph) conf = Math.Min(conf, 40);   // รอบ 2 เห็นคนเดียว + ขึ้นต้น 8/6 = อาจเป็น ฿
            if (e1.Contra && !e1.P1 && !e1.BahtGlyph) conf = Math.Min(conf, 40);
            if (e1.BahtSuspect && !e1.BahtGlyph) conf = Math.Min(conf, 40);              // ก้อนหน้าคล้าย ฿ แต่ไม่ชัด: 8xxx หรือ ฿xxx ตัดสินไม่ได้
            string reason = e1.Value.ToString("N2", CultureInfo.InvariantCulture) + " ← " + string.Join(" ", e1.Src) + " (คู่แข่ง " + w2 + ")";

            if (expected.HasValue && Math.Abs(e1.Value - expected.Value) > 0.005m && conf < 80)
            {
                foreach (Tally e in ranked)
                {
                    if (Math.Abs(e.Value - expected.Value) <= 0.005m && e.Ctx && e.W >= 3)
                    {
                        confidence = 70;
                        why = "ตรงยอดที่ลง: " + e.Value.ToString("N2", CultureInfo.InvariantCulture) + " ← " + string.Join(" ", e.Src);
                        return e.Value;
                    }
                    if (e.Conflict.HasValue && Math.Abs(e.Conflict.Value - expected.Value) <= 0.005m && e.Ctx)
                    {
                        confidence = 60;
                        why = "ซ่อม ฿ ที่ถูกอ่านเป็นตัวเลข (ตรงยอดที่ลง)";
                        return e.Conflict.Value;
                    }
                }
            }
            confidence = conf;
            int min = AppCfg.GetInt("SlipVerify_Ocr_Min_Confidence", 60);
            if (conf < min) { why = "กำกวม: " + reason; return null; }
            why = reason;
            return e1.Value;
        }

        // ── ข้อความ / คำสำคัญ ───────────────────────────────────────────────────

        private static readonly string[] AmountKw = { "จำนวนเงิน", "จำนวน", "ยอดเงิน", "ยอดโอน", "ยอดชำระ", "ยอดรวม", "amount", "total" };
        private static readonly string[] StrongKw = { "จำนวนเงิน", "จำนวน", "ยอดเงิน", "ยอดโอน", "ยอดชำระ", "amount" };
        private static readonly string[] ExcludeKw = { "ค่าธรรมเนียม", "fee", "คงเหลือ", "balance", "available", "ใช้ได้", "อ้างอิง", "เลขที่",
                                                       "ref", "vat", "ภาษี", "transaction", "บัญชี", "kb", "รายการ" };
        private static readonly string[] CurrencyKw = { "บาท", "thb", "฿" };
        private static readonly string[] DateKw = { "ม.ค.", "ก.พ.", "มี.ค.", "เม.ย.", "พ.ค.", "มิ.ย.", "ก.ค.", "ส.ค.", "ก.ย.", "ต.ค.",
                                                    "พ.ย.", "ธ.ค.", "jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep",
                                                    "oct", "nov", "dec", "วันที่", "เวลา", "date" };

        private const RegexOptions Ro = RegexOptions.Compiled | RegexOptions.CultureInvariant;
        // จำนวนเงิน: 1–3 หลัก + (คั่นหลักพัน , . ' หรือช่องว่าง + 3 หลัก)* หรือเลขติดกัน + จุด/คอมมา + 2 หลักพอดี
        private static readonly Regex MoneyRe = new Regex(@"(?<![\d.,])(\d{1,3}(?:[,.' ]\d{3})+|\d+)[.,](\d{2})(?!\d)", Ro);
        private static readonly Regex TimeRe = new Regex(@"\d{1,2}[:.]\d{2}\s*น", Ro);
        private static readonly Regex HhmmRe = new Regex(@"\b\d{1,2}:\d{2}\b", Ro);
        private static readonly Regex DateRe = new Regex(@"\b\d{1,2}/\d{1,2}/\d{2,4}\b", Ro);
        private static readonly Regex GroupRe = new Regex(@"\d[,.' ]\d{3}", Ro);
        private static readonly Regex BahtPrefixRe = new Regex(@"(?:^|[^\w])[B฿]\s?$", Ro);
        private static readonly Regex BahtTailRe = new Regex(@"[B฿]\s?$", Ro);
        private static readonly Regex DigitishRe = new Regex(@"^[0-9.,'฿BOolI|S]+$", Ro);
        private static readonly Regex NonDigitWordRe = new Regex(@"^[^0-9]*[A-Za-z\u0E01-\u0E4F][^0-9]*$", Ro);
        private static readonly Regex ThaiMarksRe = new Regex(@"[\u0E47-\u0E4E]", Ro);
        private static readonly Regex OInNum1 = new Regex(@"(?<=[\d,.])[Oo](?=[\d,.])", Ro);
        private static readonly Regex OInNum2 = new Regex(@"(?<=[.,]\d)[Oo](?!\w)", Ro);
        private static readonly Regex LInNum = new Regex(@"(?<=[\d,.])[lI|](?=[\d,.])", Ro);
        private static readonly Regex SInNum = new Regex(@"(?<=\d)S(?=\d)", Ro);
        private static readonly Regex SepRe = new Regex(@"[,.' ]", Ro);
        // ต้องประกาศหลัง ThaiMarksRe (static initializer รันตามลำดับในไฟล์)
        private static readonly string[] KAmount = AmountKw.Select(KwText).ToArray();
        private static readonly string[] KStrong = StrongKw.Select(KwText).ToArray();
        private static readonly string[] KExclude = ExcludeKw.Select(KwText).ToArray();
        private static readonly string[] KCurrency = CurrencyKw.Select(KwText).ToArray();
        private static readonly string[] KDate = DateKw.Select(KwText).ToArray();

        public static string Normalize(string text)
        {
            string t = (text ?? "").Normalize(NormalizationForm.FormC);
            t = t.Replace("\u0E4D\u0E32", "\u0E33").Replace("\u200B", "").Replace("\uFEFF", "");
            var sb = new StringBuilder(t.Length);
            foreach (char c in t) sb.Append(c >= '\u0E50' && c <= '\u0E59' ? (char)('0' + (c - '\u0E50')) : c);
            t = sb.ToString();
            t = OInNum1.Replace(t, "0");
            t = OInNum2.Replace(t, "0");
            t = LInNum.Replace(t, "1");
            t = SInNum.Replace(t, "5");
            return t;
        }

        /// <summary>รูปแบบสำหรับเทียบคำ: ตัวเล็ก ไม่มีช่องว่าง ตัดวรรณยุกต์/เครื่องหมายบน (OCR เติม/หาย: "จําน่วน" = "จำนวน") สระอำ → อา</summary>
        private static string KwText(string s)
        {
            s = (s ?? "").ToLowerInvariant().Replace(" ", "").Replace("\u0E33", "\u0E32");
            return ThaiMarksRe.Replace(s, "");
        }

        private static bool KwIn(string low, string[] kws)
        {
            foreach (string k in kws) if (low.IndexOf(k, StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        private static decimal? ParseValue(Match m)
        {
            string whole = SepRe.Replace(m.Groups[1].Value, "");
            decimal v;
            if (!decimal.TryParse(whole + "." + m.Groups[2].Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out v)) return null;
            v = Math.Round(v, 2);
            return v > 0m && v <= MaxAmount ? v : (decimal?)null;
        }

        private static List<Line> ParseTsv(string tsv, double scale)
        {
            var groups = new Dictionary<string, List<Word>>();
            var order = new List<string>();
            string[] rows = (tsv ?? "").Replace("\r", "").Split('\n');
            for (int i = 1; i < rows.Length; i++)
            {
                string[] c = rows[i].Split('\t');
                if (c.Length < 12 || c[0] != "5" || c[11].Trim().Length == 0) continue;
                int l, t, w, h;
                if (!int.TryParse(c[6], out l) || !int.TryParse(c[7], out t) || !int.TryParse(c[8], out w) || !int.TryParse(c[9], out h)) continue;
                string key = c[1] + "/" + c[2] + "/" + c[3] + "/" + c[4];
                List<Word> list;
                if (!groups.TryGetValue(key, out list)) { list = new List<Word>(); groups[key] = list; order.Add(key); }
                list.Add(new Word { Text = c[11], L = PyRound(l * scale), T = PyRound(t * scale), W = PyRound(w * scale), H = PyRound(h * scale) });
            }
            var output = new List<Line>();
            foreach (string k in order)
            {
                List<Word> ws = groups[k].OrderBy(x => x.L).ToList();
                var L = new Line { Words = ws, Spans = new List<int[]>() };
                List<int> hs = ws.Select(x => x.H).OrderBy(x => x).ToList();
                L.HMed = hs[hs.Count / 2];
                var sb = new StringBuilder();
                int? prevR = null;
                foreach (Word w in ws)
                {
                    if (prevR.HasValue && (w.L - prevR.Value) > Math.Max(4, L.HMed * 0.35)) sb.Append(' ');
                    string nt = Normalize(w.Text);
                    L.Spans.Add(new[] { sb.Length, sb.Length + nt.Length });
                    sb.Append(nt);
                    prevR = w.L + w.W;
                }
                L.Text = sb.ToString();
                L.Box = new Box(ws.Min(x => x.L), ws.Min(x => x.T), ws.Max(x => x.L + x.W), ws.Max(x => x.T + x.H));
                output.Add(L);
            }
            return output.OrderBy(x => (x.Box.T + x.Box.B) / 2.0).ToList();
        }

        private static Box? LabelBox(Line L, out bool strong)
        {
            strong = false;
            var s = new StringBuilder();
            var owner = new List<int>();
            for (int i = 0; i < L.Words.Count; i++)
            {
                string nt = KwText(Normalize(L.Words[i].Text));
                s.Append(nt);
                for (int k = 0; k < nt.Length; k++) owner.Add(i);
            }
            string str = s.ToString();
            for (int ki = 0; ki < AmountKw.Length; ki++)
            {
                string k = KAmount[ki];
                int j = str.IndexOf(k, StringComparison.Ordinal);
                if (j < 0) continue;
                var idx = new SortedSet<int>();
                for (int p = j; p < j + k.Length; p++) idx.Add(owner[p]);
                List<Word> ws = idx.Select(i => L.Words[i]).ToList();
                strong = StrongKw.Contains(AmountKw[ki]);
                return new Box(ws.Min(x => x.L), ws.Min(x => x.T), ws.Max(x => x.L + x.W), ws.Max(x => x.T + x.H));
            }
            return null;
        }

        private static Box? TokenBox(Line L, int a, int b, out string prefix)
        {
            prefix = "";
            var idx = new List<int>();
            for (int i = 0; i < L.Spans.Count; i++) if (L.Spans[i][1] > a && L.Spans[i][0] < b) idx.Add(i);
            if (idx.Count == 0) return null;
            int i0 = idx.Min(), i1 = idx.Max();
            // รวมคำติดกันที่เป็นตัวเลข/฿ (เช่น "B1" + "5,000.00" ของ ฿15,000.00)
            while (i0 > 0 && DigitishRe.IsMatch(L.Words[i0 - 1].Text)
                   && L.Words[i0].L - (L.Words[i0 - 1].L + L.Words[i0 - 1].W) < L.HMed * 1.2)
                i0--;
            List<Word> ws = L.Words.GetRange(i0, i1 - i0 + 1);
            int ps = L.Spans[i0][0];
            prefix = a > ps ? L.Text.Substring(ps, a - ps) : "";
            return new Box(ws.Min(x => x.L), ws.Min(x => x.T), ws.Max(x => x.L + x.W), ws.Max(x => x.T + x.H));
        }

        private static void FindCandidates(List<Line> lines, int W, List<Label> labels, List<Cand> cands)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                string low = KwText(lines[i].Text);
                if (KwIn(low, KAmount) && !KwIn(low, KExclude))
                {
                    bool strong;
                    Box? lb = LabelBox(lines[i], out strong);
                    if (lb.HasValue) labels.Add(new Label { Line = i, Box = lb.Value, Strong = strong });
                }
            }
            for (int i = 0; i < lines.Count; i++)
            {
                Line L = lines[i];
                string low = KwText(L.Text);
                bool excl = KwIn(low, KExclude);
                if (!excl && i > 0)
                {
                    // ค่าที่พิมพ์ใต้ป้ายของมัน (KBank "ค่าธรรมเนียม:" / บรรทัดถัดไป "0.00 บาท")
                    Line P = lines[i - 1];
                    int ph = P.Box.B - P.Box.T;
                    string pl = KwText(P.Text);
                    if (KwIn(pl, KExclude) && !MoneyRe.IsMatch(P.Text) && !KwIn(pl, KStrong)
                        && L.Box.T - P.Box.B >= 0 && L.Box.T - P.Box.B < Math.Max(ph, 10) * 2.5)
                        excl = true;
                }
                bool cur = KwIn(low, KCurrency);
                bool datey = KwIn(low, KDate) || TimeRe.IsMatch(L.Text) || HhmmRe.IsMatch(L.Text) || DateRe.IsMatch(L.Text);
                foreach (Match m in MoneyRe.Matches(L.Text))
                {
                    decimal? v = ParseValue(m);
                    if (!v.HasValue) continue;
                    string prefix;
                    Box? bx = TokenBox(L, m.Index, m.Index + m.Length, out prefix);
                    if (!bx.HasValue) continue;
                    Box box = bx.Value;
                    int s = 0;
                    string ctx = "";
                    double cy = (box.T + box.B) / 2.0;
                    int ch = box.B - box.T;
                    foreach (Label lab in labels)
                    {
                        Box lb = lab.Box;
                        int lh = lb.B - lb.T;
                        double lcy = (lb.T + lb.B) / 2.0;
                        if (Math.Abs(cy - lcy) < Math.Max(lh, ch) * 0.7 + 0.06 * Math.Max(0, box.L - lb.R) && box.L > lb.R - 5)
                        {
                            int sc = lab.Strong ? 12 : 8;
                            if (s < sc) { s = sc; ctx = "right"; }
                        }
                        else if (lab.Line < i && box.T - lb.B > 0 && box.T - lb.B < Math.Max(lh, ch) * 3.2
                                 && box.L < lb.L + W * 0.6 && box.R > lb.L - W * 0.25)
                        {
                            bool between = false;
                            for (int j = lab.Line + 1; j < i; j++)
                                if (MoneyRe.IsMatch(lines[j].Text) && lines[j].Box.B < box.T - 2) { between = true; break; }
                            int sc = lab.Strong ? 10 : 6;
                            if (!between && s < sc) { s = sc; ctx = "below"; }
                        }
                    }
                    bool baht = BahtPrefixRe.IsMatch(L.Text.Substring(0, m.Index)) || prefix.Contains("฿") || prefix.Contains("B");
                    if (cur || baht) s += 3;
                    if (GroupRe.IsMatch(m.Groups[1].Value)) s += 1;
                    if (excl) s -= 20;
                    if (datey) s -= 8;
                    cands.Add(new Cand
                    {
                        Value = v.Value, Score = s, Line = i, Raw = m.Value, Box = box, Ctx = ctx, H = ch,
                        Baht = baht || L.Text.Contains("฿") || "86".IndexOf(m.Value[0]) >= 0
                    });
                }
            }
        }

        // ── รอบ 2 ──────────────────────────────────────────────────────────────

        private static Gray BlankWords(Gray g, List<Line> lines, List<Cand> cands)
        {
            int[] hist = g.Histogram();
            int bg = 0;
            for (int i = 1; i < 256; i++) if (hist[i] > hist[bg]) bg = i;
            Gray o = g.Clone();
            foreach (Line L in lines)
                foreach (Word w in L.Words)
                    if (NonDigitWordRe.IsMatch(w.Text))
                    {
                        int pd = Math.Max(2, (int)(w.H * 0.15));
                        o.Fill(w.L - pd, w.T - pd, w.L + w.W + pd, w.T + w.H + pd, (byte)bg);
                    }
            foreach (Cand c in cands)
                o.CopyFrom(g, c.Box.L - 1, c.Box.T - 1, c.Box.R + 1, c.Box.B + 1);
            return o;
        }

        private static Gray CropF(Gray g, double l, double t, double r, double b, out Box box)
        {
            int il = Math.Max(0, PyRound(l)), it = Math.Max(0, PyRound(t)), ir = Math.Min(g.W, PyRound(r)), ib = Math.Min(g.H, PyRound(b));
            box = new Box(il, it, ir, ib);
            if (ir - il < 4 || ib - it < 4) return null;
            return g.Crop(il, it, ir, ib);
        }

        private static Gray NormHeight(Gray img, double textH)
        {
            double f = RoiTextHeight / Math.Max(8.0, textH);
            f = Math.Max(0.5, Math.Min(4.0, f));
            return img.Resize((int)(img.W * f), (int)(img.H * f));
        }

        private static void BuildRois(Gray g, SlipOcrResult st)
        {
            foreach (Cand c in st.Cands.Where(x => x.Score >= 0).OrderByDescending(x => x.Score).ThenBy(x => x.Line))
                if (st.Top.Count < 3 && st.Top.All(t => Math.Abs(c.Value - t.Value) > 0.005m)) st.Top.Add(c);   // ค่าไม่ซ้ำ
            foreach (Cand c in st.Top)
            {
                double th = c.H;
                Box src;
                Gray im = CropF(g, c.Box.L - th * 0.6, c.Box.T - th * 0.35, c.Box.R + th * 0.3, c.Box.B + th * 0.35, out src);
                if (im != null) st.Rois.Add(new Roi { Kind = "cand", Cand = c, Source = src, Img = NormHeight(im, th) });
            }
            int W = g.W;
            foreach (Label lab in st.Labels.Take(2))
            {
                Box lb = lab.Box;
                int lh = lb.B - lb.T;
                double bl = lb.R + lh * 0.5, br = W - 2;
                Box src;
                if (br - bl > lh * 2)
                {
                    Gray im = CropF(g, bl, lb.T - lh * 0.45, br, lb.B + lh * 0.45, out src);
                    if (im != null) st.Rois.Add(new Roi { Kind = "right", LabelLine = lab.Line, Source = src, Img = NormHeight(im, lh) });
                }
                Gray im2 = CropF(g, lb.L - W * 0.25, lb.B + lh * 0.1, lb.L + W * 0.75, lb.B + lh * 3.4, out src);
                if (im2 != null) st.Rois.Add(new Roi { Kind = "below", LabelLine = lab.Line, Source = src, Img = NormHeight(im2, lh * 1.25) });
            }
        }

        /// <summary>ต่อ ROI ทั้งหมด (เทา + Otsu) เป็นภาพเดียว — spans: [y0, y1, roiIndex, ver(0=g,1=b)]</summary>
        private static Gray Montage(List<Roi> rois, out List<int[]> spans)
        {
            spans = new List<int[]>();
            var items = new List<Tuple<int, int, Gray>>();
            for (int i = 0; i < rois.Count; i++)
            {
                Roi r = rois[i];
                Gray gi = Stretch(r.Img);
                Gray b = Binarize(r.Img);
                if (r.Kind == "cand" && r.Cand.Baht)
                {
                    int[] lab;
                    int k = FindBahtGlyph(b, out lab);
                    r.BahtSuspect = k == -1;
                    if (k > 0)
                    {
                        bool[] er = Dilate(lab, k, b.W, b.H, 2);
                        for (int p = 0; p < er.Length; p++)
                            if (er[p]) { b.Px[p] = 255; gi.Px[p] = 255; }
                        r.BahtRemoved = true;
                    }
                }
                items.Add(Tuple.Create(i, 0, gi));
                items.Add(Tuple.Create(i, 1, b));
            }
            if (items.Count == 0) return null;
            int W = items.Max(x => x.Item3.W) + 2 * MontageGap;
            int H = items.Sum(x => x.Item3.H + MontageGap) + MontageGap;
            var can = new Gray(W, H);
            for (int p = 0; p < can.Px.Length; p++) can.Px[p] = 255;
            int y = MontageGap;
            foreach (var it in items)
            {
                Gray im = it.Item3;
                for (int yy = 0; yy < im.H; yy++)
                    Buffer.BlockCopy(im.Px, yy * im.W, can.Px, (y + yy) * W + MontageGap, im.W);
                spans.Add(new[] { y, y + im.H, it.Item1, it.Item2 });
                y += im.H + MontageGap;
            }
            return can;
        }

        /// <summary>
        /// ฿ = ก้อนซ้ายสุดที่ขีดตั้งยื่นทั้งบนและล่างของแถวตัวเลข (เทียบ 2 ก้อนถัดไป — ทนรูปเอียงเล็กน้อย)
        /// คืน label ที่ต้องลบ / -1 = น่าสงสัย (อย่าเชื่อเลข 8/6 ตัวหน้า) / 0 = ไม่ใช่
        /// </summary>
        private static int FindBahtGlyph(Gray b, out int[] lab)
        {
            var mask = new bool[b.Px.Length];
            for (int p = 0; p < mask.Length; p++) mask[p] = b.Px[p] == 0;
            List<int[]> comps;
            lab = Components(mask, b.W, b.H, out comps);   // comps: [root, minx, miny, maxx, maxy, count] เรียงตาม root
            int h = b.H;
            var big = comps.Where(c => (c[4] - c[2] + 1) >= h * 0.25 && c[5] >= 12).OrderBy(c => c[1]).ToList();
            if (big.Count < 4) return 0;
            int[] first = big[0];
            var rest = big.Skip(1).Where(c => (c[4] - c[2] + 1) >= h * 0.4).ToList();   // เฉพาะตัวเลข (ข้าม , .)
            if (rest.Count < 3) return 0;
            List<int> hs = rest.Select(c => c[4] - c[2] + 1).OrderBy(x => x).ToList();
            int med = hs[hs.Count / 2];
            double nt = (rest[0][2] + rest[1][2]) / 2.0;
            double nb = (rest[0][4] + rest[1][4]) / 2.0;
            int fh = first[4] - first[2] + 1;
            double protrude = Math.Max(1.5, med * 0.035);
            if (fh >= med * 1.07 && first[2] <= nt - protrude && first[4] >= nb + protrude) return first[0];
            if (fh >= med * 1.08 || (fh >= med * 1.03 && first[2] < nt && first[4] > nb)) return -1;
            return 0;
        }

        private static bool[] Dilate(int[] lab, int k, int w, int h, int r)
        {
            var o = new bool[lab.Length];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (lab[y * w + x] != k) continue;
                    for (int dy = -r; dy <= r; dy++)
                    {
                        int yy = y + dy;
                        if (yy < 0 || yy >= h) continue;
                        for (int dx = -r; dx <= r; dx++)
                        {
                            int xx = x + dx;
                            if (xx >= 0 && xx < w) o[yy * w + xx] = true;
                        }
                    }
                }
            return o;
        }

        /// <summary>4-connected components (union-find, root = label เล็กสุด) — comps: [root, minx, miny, maxx, maxy, count]</summary>
        private static int[] Components(bool[] mask, int w, int h, out List<int[]> comps)
        {
            var lab = new int[w * h];
            var parent = new List<int> { 0 };
            Func<int, int> find = null;
            find = x =>
            {
                while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; }
                return x;
            };
            int next = 1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int p = y * w + x;
                    if (!mask[p]) continue;
                    int up = y > 0 ? lab[p - w] : 0;
                    int left = x > 0 ? lab[p - 1] : 0;
                    if (up != 0 && left != 0)
                    {
                        int a = find(up), b = find(left);
                        lab[p] = Math.Min(a, b);
                        if (a != b) parent[Math.Max(a, b)] = Math.Min(a, b);
                    }
                    else if (up != 0 || left != 0) lab[p] = up != 0 ? up : left;
                    else { parent.Add(next); lab[p] = next; next++; }
                }
            var roots = new int[next];
            for (int i = 0; i < next; i++) roots[i] = find(i);
            var boxes = new SortedDictionary<int, int[]>();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int p = y * w + x;
                    if (lab[p] == 0) continue;
                    int k = roots[lab[p]];
                    lab[p] = k;
                    int[] c;
                    if (!boxes.TryGetValue(k, out c)) { boxes[k] = new[] { k, x, y, x, y, 1 }; continue; }
                    if (x < c[1]) c[1] = x;
                    if (y < c[2]) c[2] = y;
                    if (x > c[3]) c[3] = x;
                    if (y > c[4]) c[4] = y;
                    c[5]++;
                }
            comps = boxes.Values.ToList();
            return lab;
        }

        // ── ภาพขาวดำ ────────────────────────────────────────────────────────────

        internal sealed class Gray
        {
            public readonly int W, H;
            public readonly byte[] Px;
            public Gray(int w, int h) { W = w; H = h; Px = new byte[w * h]; }

            public Gray Clone() { var g = new Gray(W, H); Buffer.BlockCopy(Px, 0, g.Px, 0, Px.Length); return g; }

            public double Mean()
            {
                long s = 0;
                foreach (byte b in Px) s += b;
                return Px.Length == 0 ? 0 : s / (double)Px.Length;
            }

            public void Invert() { for (int i = 0; i < Px.Length; i++) Px[i] = (byte)(255 - Px[i]); }

            public int[] Histogram()
            {
                var h = new int[256];
                foreach (byte b in Px) h[b]++;
                return h;
            }

            public Gray Crop(int l, int t, int r, int b)
            {
                l = Math.Max(0, l); t = Math.Max(0, t); r = Math.Min(W, r); b = Math.Min(H, b);
                var g = new Gray(Math.Max(1, r - l), Math.Max(1, b - t));
                for (int y = 0; y < g.H; y++) Buffer.BlockCopy(Px, (t + y) * W + l, g.Px, y * g.W, Math.Min(g.W, W - l));
                return g;
            }

            public void Fill(int l, int t, int r, int b, byte v)
            {
                l = Math.Max(0, l); t = Math.Max(0, t); r = Math.Min(W, r); b = Math.Min(H, b);
                for (int y = t; y < b; y++) for (int x = l; x < r; x++) Px[y * W + x] = v;
            }

            public void CopyFrom(Gray src, int l, int t, int r, int b)
            {
                l = Math.Max(0, l); t = Math.Max(0, t); r = Math.Min(W, r); b = Math.Min(H, b);
                for (int y = t; y < b; y++) for (int x = l; x < r; x++) Px[y * W + x] = src.Px[y * W + x];
            }

            public Bitmap ToBitmap()
            {
                var bmp = new Bitmap(W, H, PixelFormat.Format24bppRgb);
                BitmapData d = bmp.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
                try
                {
                    var row = new byte[d.Stride];
                    for (int y = 0; y < H; y++)
                    {
                        for (int x = 0; x < W; x++) { byte v = Px[y * W + x]; row[x * 3] = v; row[x * 3 + 1] = v; row[x * 3 + 2] = v; }
                        Marshal.Copy(row, 0, d.Scan0 + y * d.Stride, d.Stride);
                    }
                }
                finally { bmp.UnlockBits(d); }
                return bmp;
            }

            public static void FromBitmap(Bitmap bmp, out Gray gray, out Gray minRgb)
            {
                int w = bmp.Width, h = bmp.Height;
                gray = new Gray(w, h);
                minRgb = new Gray(w, h);
                BitmapData d = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                try
                {
                    var row = new byte[Math.Abs(d.Stride)];
                    for (int y = 0; y < h; y++)
                    {
                        Marshal.Copy(d.Scan0 + y * d.Stride, row, 0, row.Length);
                        for (int x = 0; x < w; x++)
                        {
                            int b = row[x * 3], g = row[x * 3 + 1], r = row[x * 3 + 2];
                            gray.Px[y * w + x] = (byte)((r * 299 + g * 587 + b * 114) / 1000);
                            minRgb.Px[y * w + x] = (byte)Math.Min(r, Math.Min(g, b));
                        }
                    }
                }
                finally { bmp.UnlockBits(d); }
            }

            /// <summary>ย่อ/ขยายแบบ bicubic (GDI+ HighQualityBicubic, ขอบแบบ TileFlipXY กันขอบดำ)</summary>
            public Gray Resize(int w, int h)
            {
                w = Math.Max(1, w); h = Math.Max(1, h);
                if (w == W && h == H) return Clone();
                using (Bitmap src = ToBitmap())
                using (var dst = new Bitmap(w, h, PixelFormat.Format24bppRgb))
                {
                    using (Graphics gr = Graphics.FromImage(dst))
                    using (var ia = new ImageAttributes())
                    {
                        gr.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        gr.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        gr.CompositingQuality = CompositingQuality.HighQuality;
                        ia.SetWrapMode(WrapMode.TileFlipXY);
                        gr.DrawImage(src, new Rectangle(0, 0, w, h), 0, 0, W, H, GraphicsUnit.Pixel, ia);
                    }
                    Gray g, unused;
                    FromBitmap(dst, out g, out unused);
                    return g;
                }
            }

            public void SavePng(string path)
            {
                using (Bitmap b = ToBitmap()) b.Save(path, ImageFormat.Png);
            }

            public byte[] ToPng()
            {
                using (Bitmap b = ToBitmap())
                using (var ms = new MemoryStream())
                {
                    b.Save(ms, ImageFormat.Png);
                    return ms.ToArray();
                }
            }
        }

        /// <summary>ถอดรูป (หมุนตาม EXIF ของรูปถ่าย) → ขาวดำ + min(R,G,B)</summary>
        private static bool Decode(byte[] bytes, out Gray gray, out Gray minRgb, out string error)
        {
            gray = null; minRgb = null; error = null;
            try
            {
                using (var ms = new MemoryStream(bytes ?? new byte[0]))
                using (var img = Image.FromStream(ms, false, false))
                {
                    if ((long)img.Width * img.Height > MaxPixels) { error = "รูปใหญ่เกินไป (" + img.Width + "x" + img.Height + ")"; return false; }
                    using (var bmp = new Bitmap(img.Width, img.Height, PixelFormat.Format24bppRgb))
                    {
                        using (Graphics g = Graphics.FromImage(bmp))
                        {
                            g.Clear(Color.White);
                            g.DrawImage(img, new Rectangle(0, 0, img.Width, img.Height), 0, 0, img.Width, img.Height, GraphicsUnit.Pixel);
                        }
                        RotateFlipType rf = ExifRotation(img);
                        if (rf != RotateFlipType.RotateNoneFlipNone) bmp.RotateFlip(rf);
                        Gray.FromBitmap(bmp, out gray, out minRgb);
                    }
                }
                return true;
            }
            catch
            {
                error = "ไฟล์ไม่ใช่รูปที่อ่านได้ (เช่น PDF)";
                return false;
            }
        }

        private static RotateFlipType ExifRotation(Image img)
        {
            try
            {
                if (Array.IndexOf(img.PropertyIdList, 0x0112) < 0) return RotateFlipType.RotateNoneFlipNone;
                PropertyItem pi = img.GetPropertyItem(0x0112);
                int o = pi.Value != null && pi.Value.Length >= 2 ? BitConverter.ToUInt16(pi.Value, 0) : 1;
                switch (o)
                {
                    case 2: return RotateFlipType.RotateNoneFlipX;
                    case 3: return RotateFlipType.Rotate180FlipNone;
                    case 4: return RotateFlipType.Rotate180FlipX;
                    case 5: return RotateFlipType.Rotate90FlipX;
                    case 6: return RotateFlipType.Rotate90FlipNone;
                    case 7: return RotateFlipType.Rotate270FlipX;
                    case 8: return RotateFlipType.Rotate270FlipNone;
                    default: return RotateFlipType.RotateNoneFlipNone;
                }
            }
            catch { return RotateFlipType.RotateNoneFlipNone; }
        }

        /// <summary>
        /// รูปถ่ายจอมือถือ: หาการ์ดสลิปสีขาวกลาง ๆ (min(R,G,B) สูง — พื้นสีเบจ/ผิว/โต๊ะมีน้ำเงินต่ำ) = ก้อนสว่างใหญ่สุดบนภาพย่อ ~240px
        /// คืน null ถ้าไม่ชัด (ภาพหน้าจอทั้งใบสว่าง = ไม่ตัด)
        /// </summary>
        private static Box? CardBox(Gray mn)
        {
            int w = mn.W, h = mn.H;
            int k = Math.Max(1, w / 240);
            int sh = h / k, sw = w / k;
            if (sh < 20 || sw < 20) return null;
            var small = new Gray(sw, sh);
            for (int y = 0; y < sh; y++)
                for (int x = 0; x < sw; x++)
                {
                    int s = 0;
                    for (int yy = 0; yy < k; yy++)
                    {
                        int o = (y * k + yy) * w + x * k;
                        for (int xx = 0; xx < k; xx++) s += mn.Px[o + xx];
                    }
                    small.Px[y * sw + x] = (byte)(s / (k * k));
                }
            int thr = Math.Max(150, Otsu(small.Histogram(), small.Px.Length));
            var mask = new bool[small.Px.Length];
            for (int p = 0; p < mask.Length; p++) mask[p] = small.Px[p] > thr;
            List<int[]> comps;
            Components(mask, sw, sh, out comps);
            if (comps.Count == 0) return null;
            int[] best = comps[0];
            foreach (int[] x in comps) if (x[5] > best[5]) best = x;
            if (best[5] < sh * sw * 0.12) return null;
            int[] c = (int[])best.Clone();
            // แสงสะท้อน/ลายมัวเร/แถบแจ้งเตือนอาจแบ่งการ์ดเป็นหลายก้อน: รวมก้อนสว่างที่ซ้อนบน/ล่างคอลัมน์เดียวกัน (ช่องว่างแคบ)
            // กันการตัดภาพจนบรรทัดยอดเงินหาย
            var used = new HashSet<int> { best[0] };
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (int[] o in comps)
                {
                    if (used.Contains(o[0]) || o[5] < sh * sw * 0.01) continue;
                    int ov = Math.Min(c[3], o[3]) - Math.Max(c[1], o[1]) + 1;
                    int gap = Math.Max(o[2] - c[4], c[2] - o[4]);
                    if (ov >= 0.6 * Math.Min(c[3] - c[1] + 1, o[3] - o[1] + 1) && gap <= sh * 0.12)
                    {
                        c = new[] { c[0], Math.Min(c[1], o[1]), Math.Min(c[2], o[2]), Math.Max(c[3], o[3]), Math.Max(c[4], o[4]), c[5] + o[5] };
                        used.Add(o[0]);
                        changed = true;
                    }
                }
            }
            int bw = c[3] - c[1] + 1, bh = c[4] - c[2] + 1;
            if (bw * bh > sh * sw * 0.85 || bw < sw * 0.3) return null;
            int m = (int)(Math.Max(bw, bh) * 0.02) + 1;
            return new Box(Math.Max(0, (c[1] - m) * k), Math.Max(0, (c[2] - m) * k), Math.Min(w, (c[3] + 1 + m) * k), Math.Min(h, (c[4] + 1 + m) * k));
        }

        private static Gray Stretch(Gray g)
        {
            int[] hist = g.Histogram();
            long n = g.Px.Length;
            long cum = 0;
            int lo = -1, hi = -1;
            for (int i = 0; i < 256; i++)
            {
                cum += hist[i];
                if (lo < 0 && cum >= n * 0.01) lo = i;
                if (hi < 0 && cum >= n * 0.99) { hi = i; break; }
            }
            if (lo < 0 || hi < 0 || hi - lo < 30) return g.Clone();
            var o = new Gray(g.W, g.H);
            for (int i = 0; i < g.Px.Length; i++)
            {
                int v = (g.Px[i] - lo) * 255 / (hi - lo);
                o.Px[i] = (byte)(v < 0 ? 0 : v > 255 ? 255 : v);
            }
            return o;
        }

        private static int Otsu(int[] hist, int total)
        {
            double sumAll = 0;
            for (int t = 0; t < 256; t++) sumAll += t * (double)hist[t];
            double wb = 0, sb = 0, best = -1;
            int thr = 127;
            for (int t = 0; t < 256; t++)
            {
                wb += hist[t];
                if (wb == 0) continue;
                double wf = total - wb;
                if (wf == 0) break;
                sb += t * (double)hist[t];
                double mb = sb / wb, mf = (sumAll - sb) / wf;
                double between = wb * wf * (mb - mf) * (mb - mf);
                if (between > best) { best = between; thr = t; }
            }
            return thr;
        }

        private static Gray Binarize(Gray g)
        {
            int t = Otsu(g.Histogram(), g.Px.Length);
            var b = new Gray(g.W, g.H);
            int dark = 0;
            for (int i = 0; i < g.Px.Length; i++)
            {
                b.Px[i] = g.Px[i] > t ? (byte)255 : (byte)0;
                if (b.Px[i] == 0) dark++;
            }
            if (dark > g.Px.Length * 0.5) b.Invert();   // ตัวอักษรต้องเข้มบนพื้นสว่าง
            return b;
        }

        /// <summary>ปัดแบบ Python round() (banker's) ให้พิกัดตรงต้นแบบ</summary>
        private static int PyRound(double v) { return (int)Math.Round(v, MidpointRounding.ToEven); }

        // ── เรียกโปรแกรม ────────────────────────────────────────────────────────

        private static int RunTess(string exe, Gray img, string args, int timeoutMs, out string stdout, out string stderr)
        {
            string tmp = Path.Combine(Path.GetTempPath(), "ttslip_" + Guid.NewGuid().ToString("N") + ".png");
            try
            {
                img.SavePng(tmp);
                return Run(exe, "\"" + tmp + "\" stdout " + args, timeoutMs, out stdout, out stderr);
            }
            finally
            {
                try { File.Delete(tmp); } catch { }
            }
        }

        private static int Run(string exe, string args, int timeoutMs, out string stdout, out string stderr)
        {
            var psi = new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            using (var p = new Process { StartInfo = psi })
            {
                var so = new StringBuilder();
                var se = new StringBuilder();
                p.OutputDataReceived += (s, e) => { if (e.Data != null) lock (so) so.AppendLine(e.Data); };
                p.ErrorDataReceived += (s, e) => { if (e.Data != null) lock (se) se.AppendLine(e.Data); };
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                if (!p.WaitForExit(timeoutMs))
                {
                    try { p.Kill(); } catch { }
                    try { p.WaitForExit(2000); } catch { }
                    lock (so) stdout = so.ToString();
                    stderr = "timeout";
                    return -1;
                }
                p.WaitForExit();   // ให้ event อ่าน stream จนจบ
                lock (so) stdout = so.ToString();
                lock (se) stderr = se.ToString();
                return p.ExitCode;
            }
        }

        // ── ทางเดิม: ข้อความอย่างเดียว (ไม่มีผลรอบ 2) ───────────────────────────────

        private struct TextCand { public int Score; public int Line; public decimal Value; public string Raw; }

        /// <summary>
        /// หา "ยอดโอน" — มี result (จาก ReadText) = ตัดสินด้วยโหวตรอบ 1 + รอบ 2 (<see cref="Decide"/>)
        /// ไม่มี (ข้อความอย่างเดียว เช่น cache เก่า) = heuristic เดิม แต่ความมั่นใจสูงสุด 70 (ไม่ถึงเกณฑ์ล็อกยอด)
        /// </summary>
        public static decimal? ExtractAmount(string text, decimal? expected, out int confidence, out string why, SlipOcrResult result = null)
        {
            if (result != null) return Decide(result, expected, out confidence, out why);
            confidence = 0;
            why = null;
            var lines = Normalize(text).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                       .Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
            var cands = new List<TextCand>();
            int kwLine = -99;
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                string low = KwText(line);
                bool hasKw = KwIn(low, KAmount);
                bool excl = KwIn(low, KExclude);
                bool cur = KwIn(low, KCurrency);
                bool datey = KwIn(low, KDate) || TimeRe.IsMatch(line) || HhmmRe.IsMatch(line);
                int tokens = 0;
                foreach (Match m in MoneyRe.Matches(line))
                {
                    decimal? v = ParseValue(m);
                    if (!v.HasValue) continue;
                    tokens++;
                    int s = 0;
                    if (hasKw) s += 10;
                    else if (i - kwLine >= 1 && i - kwLine <= 2) s += 6;
                    if (cur) s += 4;
                    if (GroupRe.IsMatch(m.Groups[1].Value)) s += 1;
                    if (excl) s -= 20;
                    if (datey) s -= 8;
                    cands.Add(new TextCand { Score = s, Line = i, Value = v.Value, Raw = m.Value });
                }
                if (hasKw && tokens == 0 && !excl) kwLine = i;
            }
            if (cands.Count == 0) { why = "ไม่พบตัวเลขจำนวนเงิน"; return null; }

            var sorted = cands.OrderByDescending(c => c.Score).ThenBy(c => c.Line).ToList();
            TextCand best = sorted[0];
            if (best.Score < 4) { why = "กำกวม (ไม่พบป้ายจำนวนเงิน)"; return null; }
            if (best.Raw.Length > 4 && best.Raw[0] == '8')
            {
                // "฿35,000.00" มักถูกอ่านเป็น "835,000.00" — ข้อความอย่างเดียวแยกไม่ออก: ใช้ยอดที่ลงตัดสินเท่านั้น
                Match m2 = MoneyRe.Match(best.Raw.Substring(1));
                decimal? stripped = m2.Success && m2.Index == 0 && m2.Length == best.Raw.Length - 1 ? ParseValue(m2) : null;
                if (stripped.HasValue && expected.HasValue && Math.Abs(stripped.Value - expected.Value) <= 0.005m)
                {
                    confidence = 60; why = "ซ่อม ฿ ที่ถูกอ่านเป็น 8 (ตรงยอดที่ลง)"; return stripped.Value;
                }
                if (stripped.HasValue) { why = "กำกวม (" + best.Raw + " อาจเป็น ฿" + m2.Value + ")"; return null; }
            }
            confidence = best.Score >= 10 ? 70 : 60;
            why = "ข้อความอย่างเดียว คะแนน " + best.Score + " บรรทัด " + (best.Line + 1);
            return best.Value;
        }

        private static string Trunc(string s, int n) => string.IsNullOrEmpty(s) ? "" : (s.Length > n ? s.Substring(0, n) : s).Trim();
    }
}
