using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
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
    /// Windows.Media.Ocr ไม่เลือก: ต้องมี language pack OCR ภาษาไทยบนเครื่อง และ WinRT จาก ASP.NET 4.7.2 ต้องใช้
    /// Microsoft.Windows.SDK.Contracts + Windows Server Desktop Experience — ติดตั้งยากกว่าและทดสอบบน Linux ไม่ได้
    ///
    /// ติดตั้ง (ดู CLAUDE.md ข้อ 18): Tesseract 5 for Windows (UB Mannheim) → C:\Program Files\Tesseract-OCR พร้อมภาษา Thai
    /// หรือวาง tha.traineddata + eng.traineddata (tessdata_fast) ไว้ที่ ~/App_Data/tessdata แล้วตั้ง SlipVerify_Tessdata_Dir
    ///
    /// ตัวดึงยอด (<see cref="ExtractAmount"/>) พอร์ตตรงจากต้นแบบ Python ที่ทดสอบกับสลิปจริง SCB (1,000.00) และสลิปจำลอง
    /// KBank/KTB/BBL/PromptPay — ผลอ่าน OCR ไม่ใช่ข้อมูลธนาคาร ใช้เทียบยอดเท่านั้น
    /// </summary>
    public static class LocalSlipAmountReader
    {
        public class OcrText
        {
            public bool Ok;
            public string Text;
            public string Error;
            public long ElapsedMs;
        }

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

        // ── อ่านข้อความ ────────────────────────────────────────────────────────

        public static OcrText ReadText(byte[] imageBytes)
        {
            var sw = Stopwatch.StartNew();
            var res = new OcrText();
            string tmp = null;
            try
            {
                if (!AppCfg.GetBool("SlipVerify_Local_Ocr", true)) { res.Error = "ปิด OCR ในเครื่องไว้ (SlipVerify_Local_Ocr=0)"; return res; }
                tmp = Path.Combine(Path.GetTempPath(), "ttslip_" + Guid.NewGuid().ToString("N") + ".png");
                if (!Preprocess(imageBytes, tmp)) { res.Error = "ไฟล์ไม่ใช่รูปที่อ่านได้ (เช่น PDF)"; return res; }

                string exe = ResolveTesseractPath();
                string td = ResolveTessdataDir();
                string tdArg = string.IsNullOrEmpty(td) ? "" : " --tessdata-dir \"" + td.TrimEnd('\\', '/') + "\"";
                string outp, err;
                int code = Run(exe, "\"" + tmp + "\" stdout -l tha+eng --psm 6" + tdArg, 20000, out outp, out err);
                if (code != 0 && (err ?? "").IndexOf("tha", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // ไม่มีภาษาไทย → อ่านอังกฤษ (ตัวเลขยังอ่านได้ แต่คำว่า "จำนวนเงิน" หาย — ความมั่นใจต่ำลง)
                    code = Run(exe, "\"" + tmp + "\" stdout -l eng --psm 6" + tdArg, 20000, out outp, out err);
                }
                if (code != 0)
                {
                    res.Error = "Tesseract exit " + code + ": " + Trunc(err, 200);
                    return res;
                }
                res.Ok = true;
                res.Text = outp ?? "";
            }
            catch (Exception ex)
            {
                res.Error = "OCR ในเครื่องใช้ไม่ได้: " + ex.GetType().Name + " " + Trunc(ex.Message, 160);
            }
            finally
            {
                res.ElapsedMs = sw.ElapsedMilliseconds;
                if (tmp != null) { try { File.Delete(tmp); } catch { } }
            }
            return res;
        }

        /// <summary>ขยายรูปเล็ก (สลิปที่ถูกย่อ) ให้กว้าง ~1400px แล้วบันทึกเป็น PNG — คืน false ถ้าไม่ใช่รูป</summary>
        private static bool Preprocess(byte[] bytes, string outPath)
        {
            try
            {
                using (var ms = new MemoryStream(bytes))
                using (var img = Image.FromStream(ms, false, false))
                {
                    double f = img.Width < 1000 ? Math.Min(3.0, 1400.0 / Math.Max(1, img.Width)) : 1.0;
                    int w = (int)(img.Width * f), h = (int)(img.Height * f);
                    using (var bmp = new Bitmap(w, h, PixelFormat.Format24bppRgb))
                    {
                        using (var g = Graphics.FromImage(bmp))
                        {
                            g.Clear(Color.White);
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.DrawImage(img, 0, 0, w, h);
                        }
                        bmp.Save(outPath, ImageFormat.Png);
                    }
                }
                return true;
            }
            catch { return false; }
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
                p.OutputDataReceived += (s, e) => { if (e.Data != null) so.AppendLine(e.Data); };
                p.ErrorDataReceived += (s, e) => { if (e.Data != null) se.AppendLine(e.Data); };
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                if (!p.WaitForExit(timeoutMs))
                {
                    try { p.Kill(); } catch { }
                    stdout = so.ToString();
                    stderr = "timeout";
                    return -1;
                }
                p.WaitForExit();   // ให้ event อ่าน stream จนจบ
                stdout = so.ToString();
                stderr = se.ToString();
                return p.ExitCode;
            }
        }

        // ── ดึงยอดเงินจากข้อความ OCR (พอร์ตจาก scratchpad proto/slip_amount.py — แก้ที่นี่ต้องแก้คู่กัน) ─────

        private static readonly string[] AmountKw = { "จำนวนเงิน", "จำนวน", "ยอดเงิน", "ยอดโอน", "ยอดชำระ", "amount", "total", "โอนเงิน" };
        private static readonly string[] ExcludeKw = { "ค่าธรรมเนียม", "fee", "คงเหลือ", "balance", "available", "ใช้ได้",
                                                       "รหัสอ้างอิง", "เลขที่รายการ", "ref", "vat", "ภาษี" };
        private static readonly string[] CurrencyKw = { "บาท", "thb", "฿" };
        private static readonly string[] DateKw = { "ม.ค.", "ก.พ.", "มี.ค.", "เม.ย.", "พ.ค.", "มิ.ย.", "ก.ค.", "ส.ค.", "ก.ย.", "ต.ค.",
                                                    "พ.ย.", "ธ.ค.", "jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep",
                                                    "oct", "nov", "dec" };
        // จำนวนเงิน: 1–3 หลัก + (คั่นหลักพัน , . ' หรือช่องว่าง + 3 หลัก)* หรือเลขติดกัน + จุด/คอมมา + 2 หลัก
        private static readonly Regex MoneyRe = new Regex(@"(?<![\d.,])(\d{1,3}(?:[,.' ]\d{3})+|\d+)[.,](\d{2})(?!\d)", RegexOptions.Compiled);
        private static readonly Regex TimeRe = new Regex(@"\b\d{1,2}[:.]\d{2}\s*น", RegexOptions.Compiled);
        private static readonly Regex HhmmRe = new Regex(@"\b\d{1,2}:\d{2}\b", RegexOptions.Compiled);
        private static readonly Regex GroupRe = new Regex(@"\d[,.' ]\d{3}", RegexOptions.Compiled);

        public static string Normalize(string text)
        {
            string t = (text ?? "").Normalize(NormalizationForm.FormC);
            t = t.Replace("\u0E4D\u0E32", "\u0E33").Replace("\u200B", "").Replace("\uFEFF", "");
            var sb = new StringBuilder(t.Length);
            foreach (char c in t) sb.Append(c >= '\u0E50' && c <= '\u0E59' ? (char)('0' + (c - '\u0E50')) : c);
            t = sb.ToString();
            t = Regex.Replace(t, @"(?<=[\d,.])[Oo](?=[\d,.])", "0");
            t = Regex.Replace(t, @"(?<=[\d,.])[lI|](?=[\d,.])", "1");
            return t;
        }

        private static decimal? ParseToken(Match m)
        {
            string whole = Regex.Replace(m.Groups[1].Value, @"[,.' ]", "");
            decimal v;
            return decimal.TryParse(whole + "." + m.Groups[2].Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out v)
                ? Math.Round(v, 2) : (decimal?)null;
        }

        private struct Cand { public int Score; public int Line; public decimal Value; public string Raw; }

        /// <summary>
        /// หา "ยอดโอน" จากข้อความ OCR — คะแนนต่อตัวเลข: +10 อยู่บรรทัดคำว่า จำนวนเงิน/Amount, +6 อยู่ 1–2 บรรทัดถัดจากบรรทัดคำนั้น,
        /// +4 มี บาท/THB/฿, +1 มีคั่นหลักพัน, −20 บรรทัดค่าธรรมเนียม/คงเหลือ/อ้างอิง, −8 บรรทัดวันที่/เวลา
        /// expected (ถ้ามี) ใช้แค่ "ซ่อม" กรณีตัวเลขคะแนนดีตัวอื่นตรงยอด หรือ ฿ ถูกอ่านเป็น 8 (฿35,000.00 → 835,000.00)
        /// </summary>
        public static decimal? ExtractAmount(string text, decimal? expected, out int confidence, out string why)
        {
            confidence = 0;
            why = null;
            var lines = Normalize(text).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                       .Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
            var cands = new List<Cand>();
            int kwLine = -99;
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                string low = line.ToLowerInvariant();
                bool hasKw = AmountKw.Any(k => low.Contains(k));
                bool excl = ExcludeKw.Any(k => low.Contains(k));
                bool cur = CurrencyKw.Any(k => low.Contains(k));
                bool datey = DateKw.Any(k => low.Contains(k)) || TimeRe.IsMatch(line) || HhmmRe.IsMatch(line);
                int tokens = 0;
                foreach (Match m in MoneyRe.Matches(line))
                {
                    decimal? v = ParseToken(m);
                    if (!v.HasValue || v.Value <= 0m) continue;
                    tokens++;
                    int s = 0;
                    if (hasKw) s += 10;
                    else if (i - kwLine >= 1 && i - kwLine <= 2) s += 6;
                    if (cur) s += 4;
                    if (GroupRe.IsMatch(m.Groups[1].Value)) s += 1;
                    if (excl) s -= 20;
                    if (datey) s -= 8;
                    cands.Add(new Cand { Score = s, Line = i, Value = v.Value, Raw = m.Value });
                }
                if (hasKw && tokens == 0 && !excl) kwLine = i;
            }
            if (cands.Count == 0) { why = "ไม่พบตัวเลขจำนวนเงิน"; return null; }

            var sorted = cands.OrderByDescending(c => c.Score).ThenBy(c => c.Line).ToList();
            Cand best = sorted[0];
            if (expected.HasValue && best.Score >= 4 && Math.Abs(best.Value - expected.Value) > 0.005m)
            {
                foreach (var c in sorted)
                {
                    if (c.Score < 4) break;
                    if (Math.Abs(c.Value - expected.Value) <= 0.005m)
                    {
                        confidence = 75; why = "ตรงยอดที่ลง (คะแนน " + c.Score + ")"; return c.Value;
                    }
                    decimal? fixedV = StripCurrencyMisread(c.Raw);
                    if (fixedV.HasValue && Math.Abs(fixedV.Value - expected.Value) <= 0.005m)
                    {
                        confidence = 60; why = "ซ่อม ฿ ที่ถูกอ่านเป็น 8"; return fixedV.Value;
                    }
                }
            }
            if (best.Score >= 4)
            {
                confidence = best.Score >= 10 ? 90 : 70;
                why = "คะแนน " + best.Score + " บรรทัด " + (best.Line + 1);
                return best.Value;
            }
            var distinct = cands.Where(c => c.Score > -8).Select(c => c.Value).Distinct().ToList();
            if (distinct.Count == 1)
            {
                confidence = 50; why = "มีตัวเลขเงินตัวเดียว"; return distinct[0];
            }
            why = "กำกวม (" + string.Join(", ", distinct.Select(d => d.ToString("N2"))) + ")";
            return null;
        }

        private static decimal? StripCurrencyMisread(string raw)
        {
            if (string.IsNullOrEmpty(raw) || raw.Length <= 4 || raw[0] != '8') return null;
            Match m = MoneyRe.Match(raw.Substring(1));
            return m.Success && m.Index == 0 && m.Length == raw.Length - 1 ? ParseToken(m) : null;
        }

        private static string Trunc(string s, int n) => string.IsNullOrEmpty(s) ? "" : (s.Length > n ? s.Substring(0, n) : s).Trim();
    }
}
