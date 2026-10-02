using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace Take_Time_BangPhra.Services
{
    /// <summary>
    /// อ่านค่าจากอีเมลจอง OTA แบบ "หลายวิธีแล้วให้คะแนน" แทนการผูกกับ regex ตัวเดียว
    ///
    /// ═══ ทำไมต้องมี ═══
    /// เดิมแต่ละฟิลด์มี regex ตัวเดียวตายตัว ผูกกับเทมเพลตที่เคยเห็น พอ OTA เปลี่ยน
    /// เทมเพลต (ซึ่งเปลี่ยนได้ตลอดโดยไม่บอกใคร) regex ก็ match ไม่ได้หรือได้ค่าผิด
    /// แล้วระบบเดินต่อเงียบ ๆ ⇒ ต้องไล่แก้ทีละตัวทุกครั้ง ไม่มีวันจบ
    ///
    /// ═══ แนวคิด ═══
    /// คนอ่านอีเมลไม่ได้จำ regex — คนมองหา "ป้ายชื่อ" แล้วอ่านค่าที่อยู่ถัดไป
    /// คลาสนี้ทำแบบเดียวกัน: หาป้าย (รู้จักหลายคำพ้อง) แล้วเก็บค่าที่อยู่ถัดไป
    /// จากหลาย ๆ ทาง (ข้อความ / โครงสร้าง DOM / ช่องตาราง / regex) ได้มาหลายคำตอบ
    /// แล้วให้คะแนนแต่ละคำตอบด้วยหลักฐาน:
    ///   · ค่าผ่านตัวตรวจชนิดข้อมูลไหม (เงินเป็นตัวเลข > 0, วันที่ parse ได้, ฯลฯ)
    ///   · มีชื่อฟิลด์อื่นปนมาไหม (= regex กวาดข้ามฟิลด์ → ตัดคะแนนหนัก)
    ///   · วิธีอื่นได้ค่าเดียวกันไหม (ยืนยันกันเอง → บวกคะแนน)
    ///   · วิธีนี้เคยถูกกับเทมเพลตนี้มาก่อนไหม (บทเรียนที่สะสมไว้ → บวกคะแนน)
    /// เลือกคำตอบคะแนนสูงสุด · คะแนนต่ำกว่าเกณฑ์ = ไม่เดา ให้คนตัดสิน
    ///
    /// ⚠ ไม่ได้แปลว่า "รองรับทุกเทมเพลต" — แปลว่าเทมเพลตใหม่มีโอกาสอ่านออกเอง
    ///   และถ้าอ่านไม่ออกจะ "รู้ตัวว่าไม่แน่ใจ" แทนที่จะมั่นใจผิด ๆ
    /// </summary>
    public class OtaFieldReader
    {
        // ── ชนิดข้อมูลของฟิลด์ (ใช้ตรวจว่าค่าที่ได้สมเหตุสมผลไหม) ──
        public enum FieldKind { Text, Money, Date, Id, Phone }

        public class FieldSpec
        {
            public string Name;                 // ชื่อฟิลด์ (ใช้เป็น key ตอนเรียนรู้)
            public FieldKind Kind = FieldKind.Text;
            /// <summary>ป้ายชื่อที่เคยเห็น/น่าจะเจอ — ยิ่งใส่หลายคำพ้อง ยิ่งทนต่อเทมเพลตใหม่</summary>
            public string[] Labels = new string[0];
            /// <summary>regex เฉพาะทางที่รู้อยู่แล้ว (ถ้ามี) — ยังใช้เป็นวิธีหนึ่ง ไม่ใช่วิธีเดียว</summary>
            public string[] Patterns = new string[0];
            /// <summary>ค่ายาวสุดที่ยอมรับ (กันกวาดยาว)</summary>
            public int MaxLength = 120;
        }

        public class Candidate
        {
            public string Value;
            public string Strategy;             // วิธีที่ได้ค่านี้มา (ใช้จำว่าอะไรเวิร์กกับเทมเพลตไหน)
            public int Score;
            public string Why;                  // เหตุผลของคะแนน (ไว้แสดงตอนต้องให้คนตัดสิน)
            public override string ToString() { return Strategy + "=" + Value + " (" + Score + ")"; }
        }

        public class ReadResult
        {
            public string Value;
            public int Confidence;              // 0-100
            public string Strategy;
            public List<Candidate> All = new List<Candidate>();
            public bool IsConfident(int threshold) { return !string.IsNullOrWhiteSpace(Value) && Confidence >= threshold; }

            /// <summary>สรุปให้คนอ่านตอนคะแนนต่ำ — เห็นว่าแต่ละวิธีได้อะไรมา</summary>
            public string Explain(int max = 4)
            {
                if (All.Count == 0) return "ไม่พบค่าจากวิธีใดเลย";
                return string.Join(" · ", All.OrderByDescending(c => c.Score).Take(max)
                    .Select(c => c.Strategy + " → \"" + Trim(c.Value, 40) + "\" (" + c.Score + ")"));
            }
        }

        // ── ป้ายชื่อฟิลด์อื่น ที่ "ห้ามโผล่ในค่า" — โผล่เมื่อไหร่แปลว่ากวาดข้ามฟิลด์ ──
        private static readonly string[] ForeignLabels =
        {
            "channel name", "bookings status", "booking id", "booking reference",
            "payment type", "refsell_amt", "room type", "check-in", "check-out",
            "no of rooms", "no of nights", "guest name", "total (all inclusive)",
            "cancellation", "commission", "special request", "adults", "children"
        };

        private readonly HtmlDocument _doc;
        private readonly string _text;

        public OtaFieldReader(string html)
        {
            _doc = new HtmlDocument();
            if (!string.IsNullOrEmpty(html)) _doc.LoadHtml(html);
            _text = Norm(_doc.DocumentNode == null ? "" : _doc.DocumentNode.InnerText);
        }

        // ══ ลายนิ้วมือเทมเพลต ═══════════════════════════════════════════════
        // ใช้จำว่า "อีเมลหน้าตาแบบนี้ เคยอ่านด้วยวิธีไหนแล้วถูก"
        // ทำจาก "ป้ายชื่อที่ปรากฏในอีเมล" ไม่ใช่เนื้อหา ⇒ อีเมลคนละใบของเทมเพลตเดียวกัน
        // ได้ลายนิ้วมือเดียวกัน แต่พอ OTA เปลี่ยนโครงสร้างจะได้ลายใหม่ทันที
        public string TemplateKey()
        {
            var marks = new List<string>();
            foreach (string lbl in ForeignLabels)
                if (_text.IndexOf(lbl, StringComparison.OrdinalIgnoreCase) >= 0) marks.Add(lbl);
            // นับตารางด้วย — เทมเพลตที่จัดหน้าไม่เหมือนกันจะต่างกันตรงนี้
            int tables = 0;
            try { var t = _doc.DocumentNode.SelectNodes("//table"); tables = t == null ? 0 : t.Count; } catch { }
            string raw = string.Join("|", marks) + "#t" + Math.Min(tables, 30);
            return Hash(raw);
        }

        private static string Hash(string s)
        {
            unchecked
            {
                // FNV-1a — สั้น อ่านง่าย พอสำหรับใช้เป็น key (ไม่ใช่งานความปลอดภัย)
                ulong h = 14695981039346656037UL;
                foreach (char c in s ?? "") { h ^= c; h *= 1099511628211UL; }
                return h.ToString("x16");
            }
        }

        // ══ อ่านค่าหนึ่งฟิลด์ ════════════════════════════════════════════════

        /// <param name="learnedBonus">
        /// ฟังก์ชันให้คะแนนพิเศษตามบทเรียน: รับ (fieldName, strategy) คืน 0-25
        /// (ส่ง null ได้ = ยังไม่มีบทเรียน)
        /// </param>
        public ReadResult Read(FieldSpec spec, Func<string, string, int> learnedBonus = null)
        {
            var cands = new List<Candidate>();

            // ── วิธีที่ 1: regex ที่รู้อยู่แล้ว ──
            for (int i = 0; i < spec.Patterns.Length; i++)
            {
                string v = RxFirst(_text, spec.Patterns[i]);
                if (!string.IsNullOrWhiteSpace(v)) Add(cands, v, "regex#" + (i + 1), spec);
            }

            // ── วิธีที่ 2: หาป้ายในข้อความ แล้วอ่านค่าที่อยู่ถัดไป ──
            // นี่คือวิธีที่ทำให้ "เทมเพลตใหม่" มีโอกาสอ่านออกเอง เพราะไม่ผูกกับโครงสร้าง
            foreach (string label in spec.Labels)
            {
                string v = AfterLabelInText(label, spec);
                if (!string.IsNullOrWhiteSpace(v)) Add(cands, v, "label-text:" + label, spec);
            }

            // ── วิธีที่ 3: หาป้ายใน DOM แล้วอ่านโหนดถัดไป/ช่องตารางที่คู่กัน ──
            foreach (string label in spec.Labels)
            {
                foreach (var v in AfterLabelInDom(label, spec))
                    if (!string.IsNullOrWhiteSpace(v.Item1))
                        Add(cands, v.Item1, "dom-" + v.Item2 + ":" + label, spec);
            }

            // ── ให้คะแนน ──
            foreach (var c in cands)
            {
                int score = BaseScore(c.Strategy);
                var reasons = new List<string>();

                int typeScore = TypeScore(c.Value, spec.Kind);
                score += typeScore;
                if (typeScore < 0) reasons.Add("ผิดชนิดข้อมูล");

                if (LooksSwept(c.Value)) { score -= 45; reasons.Add("มีชื่อฟิลด์อื่นปนมา"); }
                if (c.Value.Length > spec.MaxLength) { score -= 25; reasons.Add("ยาวเกินไป"); }

                // วิธีอื่นได้ค่าเดียวกัน = ยืนยันกันเอง
                int agree = cands.Count(x => x != c && SameValue(x.Value, c.Value, spec.Kind));
                if (agree > 0) { score += Math.Min(20, agree * 10); reasons.Add("ตรงกับอีก " + agree + " วิธี"); }

                if (learnedBonus != null)
                {
                    int bonus = learnedBonus(spec.Name, c.Strategy);
                    if (bonus != 0) { score += bonus; reasons.Add("เคยถูกกับเทมเพลตนี้ +" + bonus); }
                }

                c.Score = Math.Max(0, Math.Min(100, score));
                c.Why = string.Join(", ", reasons);
            }

            var best = cands.OrderByDescending(c => c.Score).FirstOrDefault();
            return new ReadResult
            {
                Value = best == null ? "" : best.Value,
                Confidence = best == null ? 0 : best.Score,
                Strategy = best == null ? "" : best.Strategy,
                All = cands
            };
        }

        /// <summary>
        /// ให้คะแนนค่าที่มาจากภายนอก (เช่น AI แนะนำมา) ด้วยเกณฑ์เดียวกับวิธีอื่น
        ///
        /// ⚠ สำคัญ: AI ไม่ได้รับสิทธิ์พิเศษ — ต้องผ่านตัวตรวจชนิดข้อมูลและตัวจับ
        /// "มีชื่อฟิลด์อื่นปนมา" เหมือนกัน ⇒ AI แนะนำมั่วก็ได้คะแนนต่ำ ไม่หลุดเข้าระบบ
        /// ให้ฐานคะแนนต่ำกว่าวิธีที่อ่านจากโครงสร้างจริง เพราะ AI ไม่มีหลักฐานว่าอ่านจากไหน
        /// </summary>
        public static ReadResult ScoreExternal(string value, FieldSpec spec, string strategy)
        {
            var r = new ReadResult { Strategy = strategy, Value = "", Confidence = 0 };
            string v = Cut(Norm(value), Math.Max(spec.MaxLength, 20));
            if (string.IsNullOrWhiteSpace(v)) return r;

            int score = 42;                       // ฐานของ AI — ต่ำกว่า regex/DOM ที่มีหลักฐาน
            var why = new List<string>();

            int t = TypeScore(v, spec.Kind);
            score += t;
            if (t < 0) why.Add("ผิดชนิดข้อมูล");
            if (LooksSwept(v)) { score -= 45; why.Add("มีชื่อฟิลด์อื่นปนมา"); }
            if (v.Length > spec.MaxLength) { score -= 25; why.Add("ยาวเกินไป"); }

            r.Value = v;
            r.Confidence = Math.Max(0, Math.Min(100, score));
            r.All.Add(new Candidate { Value = v, Strategy = strategy, Score = r.Confidence, Why = string.Join(", ", why) });
            return r;
        }

        private static int BaseScore(string strategy)
        {
            if (strategy.StartsWith("regex#1", StringComparison.Ordinal)) return 55;  // ตัวหลักที่เคยใช้ได้
            if (strategy.StartsWith("regex", StringComparison.Ordinal)) return 45;
            if (strategy.StartsWith("dom-cell", StringComparison.Ordinal)) return 50; // โครงสร้างชัดเจน
            if (strategy.StartsWith("dom-sibling", StringComparison.Ordinal)) return 48;
            if (strategy.StartsWith("label-text", StringComparison.Ordinal)) return 40;
            return 35;
        }

        /// <summary>ค่าที่ได้เข้ากับชนิดข้อมูลที่คาดไว้ไหม (+ = ใช่, - = ไม่ใช่)</summary>
        private static int TypeScore(string v, FieldKind kind)
        {
            string s = (v ?? "").Trim();
            if (s.Length == 0) return -50;

            switch (kind)
            {
                case FieldKind.Money:
                    double m;
                    if (!TryMoney(s, out m)) return -40;
                    return m > 0 ? 25 : -30;          // 0 บาท = อ่านผิดแน่ ๆ

                case FieldKind.Date:
                    return LooksLikeDate(s) ? 25 : -40;

                case FieldKind.Id:
                    // เลขจอง OTA: ตัวเลข/ขีด ยาวพอประมาณ ไม่มีช่องว่าง
                    if (Regex.IsMatch(s, @"^[0-9][0-9A-Za-z\-]{4,30}$")) return 25;
                    if (Regex.IsMatch(s, @"^\S{5,30}$")) return 5;
                    return -30;

                case FieldKind.Phone:
                    string d = Regex.Replace(s, @"[^\d]", "");
                    return d.Length >= 8 && d.Length <= 15 ? 20 : -25;

                default:
                    // ข้อความทั่วไป: สั้นเกิน/ยาวเกินน่าสงสัย
                    if (s.Length < 2) return -20;
                    if (s.Length > 120) return -15;
                    return 10;
            }
        }

        private static bool SameValue(string a, string b, FieldKind kind)
        {
            if (kind == FieldKind.Money)
            {
                double x, y;
                return TryMoney(a, out x) && TryMoney(b, out y) && Math.Abs(x - y) < 0.01;
            }
            return string.Equals(Norm(a), Norm(b), StringComparison.OrdinalIgnoreCase);
        }

        // ── หาป้ายในข้อความล้วน ──
        private string AfterLabelInText(string label, FieldSpec spec)
        {
            // ป้ายอาจตามด้วย ":" หรือไม่มีก็ได้ (บางเทมเพลตวางค่าไว้บรรทัดถัดไปเฉย ๆ)
            string pat = Regex.Escape(label).Replace("\\ ", "\\s*") + @"\s*:?\s*(.{1,"
                       + Math.Max(10, spec.MaxLength) + @"}?)\s*(?=$|" + ForeignLabelAlternation() + ")";
            return RxFirst(_text, pat);
        }

        private static string _foreignAlt;
        private static string ForeignLabelAlternation()
        {
            if (_foreignAlt != null) return _foreignAlt;
            _foreignAlt = string.Join("|", ForeignLabels.Select(l => Regex.Escape(l).Replace("\\ ", "\\s*")));
            return _foreignAlt;
        }

        // ── หาป้ายใน DOM ──
        private IEnumerable<Tuple<string, string>> AfterLabelInDom(string label, FieldSpec spec)
        {
            var found = new List<Tuple<string, string>>();
            if (_doc.DocumentNode == null) return found;

            HtmlNodeCollection nodes = null;
            try
            {
                // โหนดที่ "ข้อความของมันเอง" คือป้ายนี้ (ไม่เอาโหนดแม่ที่ครอบทั้งหน้า)
                nodes = _doc.DocumentNode.SelectNodes(
                    "//*[not(self::script) and not(self::style)][contains(translate(text(),"
                    + "'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'), '"
                    + label.ToLowerInvariant().Replace("'", "") + "')]");
            }
            catch { }
            if (nodes == null) return found;

            foreach (var n in nodes.Take(6))
            {
                // (ก) โหนดพี่น้องถัดไป — เทมเพลตแบบ <span>ป้าย</span><span>ค่า</span>
                var sib = n.NextSibling;
                int hop = 0;
                while (sib != null && hop++ < 4)
                {
                    string t = Norm(sib.InnerText);
                    if (!string.IsNullOrWhiteSpace(t) && !IsLabelOnly(t))
                    { found.Add(Tuple.Create(Cut(t, spec.MaxLength), "sibling")); break; }
                    sib = sib.NextSibling;
                }

                // (ข) ช่องตารางที่คู่กัน — ป้ายอยู่ใน td/th แล้วค่าอยู่ช่องถัดไปหรือแถวล่าง
                var cell = Ancestor(n, "td", "th");
                if (cell != null)
                {
                    var next = cell.NextSibling;
                    int h2 = 0;
                    while (next != null && h2++ < 4)
                    {
                        if (next.Name == "td" || next.Name == "th")
                        {
                            string t = Norm(next.InnerText);
                            if (!string.IsNullOrWhiteSpace(t) && !IsLabelOnly(t))
                            { found.Add(Tuple.Create(Cut(t, spec.MaxLength), "cell-right")); break; }
                        }
                        next = next.NextSibling;
                    }

                    // ค่าอยู่แถวล่างในคอลัมน์เดียวกัน (หัวตารางแนวตั้ง)
                    var row = Ancestor(cell, "tr");
                    if (row != null && row.NextSibling != null)
                    {
                        int idx = IndexOfCell(row, cell);
                        var below = CellAt(NextElement(row, "tr"), idx);
                        if (below != null)
                        {
                            string t = Norm(below.InnerText);
                            if (!string.IsNullOrWhiteSpace(t) && !IsLabelOnly(t))
                                found.Add(Tuple.Create(Cut(t, spec.MaxLength), "cell-below"));
                        }
                    }
                }

                // (ค) ค่าติดอยู่ในข้อความเดียวกับป้าย — "Payment Type: Hotel Collect"
                string own = Norm(n.InnerText);
                int p = own.IndexOf(label, StringComparison.OrdinalIgnoreCase);
                if (p >= 0)
                {
                    string tail = own.Substring(p + label.Length).TrimStart(':', ' ', '\t');
                    tail = StopAtForeignLabel(tail);
                    if (!string.IsNullOrWhiteSpace(tail))
                        found.Add(Tuple.Create(Cut(tail, spec.MaxLength), "inline"));
                }
            }
            return found;
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static void Add(List<Candidate> list, string value, string strategy, FieldSpec spec)
        {
            string v = Cut(Norm(value), Math.Max(spec.MaxLength, 20));
            if (string.IsNullOrWhiteSpace(v)) return;
            if (list.Any(x => x.Strategy == strategy && x.Value == v)) return;
            list.Add(new Candidate { Value = v, Strategy = strategy });
        }

        public static bool LooksSwept(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            string v = value.ToLowerInvariant();
            return ForeignLabels.Any(l => v.IndexOf(l, StringComparison.Ordinal) >= 0);
        }

        private static string StopAtForeignLabel(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            int cut = -1;
            foreach (string l in ForeignLabels)
            {
                int i = s.IndexOf(l, StringComparison.OrdinalIgnoreCase);
                if (i > 0 && (cut < 0 || i < cut)) cut = i;
            }
            return cut > 0 ? s.Substring(0, cut).Trim() : s.Trim();
        }

        /// <summary>ข้อความนี้เป็นแค่ป้ายชื่อ (ไม่ใช่ค่า) ใช่ไหม</summary>
        private static bool IsLabelOnly(string t)
        {
            string v = (t ?? "").Trim().TrimEnd(':').ToLowerInvariant();
            return v.Length == 0 || ForeignLabels.Any(l => v == l);
        }

        private static HtmlNode Ancestor(HtmlNode n, params string[] names)
        {
            var cur = n;
            int hop = 0;
            while (cur != null && hop++ < 8)
            {
                if (names.Contains(cur.Name)) return cur;
                cur = cur.ParentNode;
            }
            return null;
        }

        private static HtmlNode NextElement(HtmlNode n, string name)
        {
            var s = n == null ? null : n.NextSibling;
            while (s != null) { if (s.Name == name) return s; s = s.NextSibling; }
            return null;
        }

        private static int IndexOfCell(HtmlNode row, HtmlNode cell)
        {
            int i = 0;
            foreach (var c in row.ChildNodes)
            {
                if (c.Name != "td" && c.Name != "th") continue;
                if (c == cell) return i;
                i++;
            }
            return -1;
        }

        private static HtmlNode CellAt(HtmlNode row, int index)
        {
            if (row == null || index < 0) return null;
            int i = 0;
            foreach (var c in row.ChildNodes)
            {
                if (c.Name != "td" && c.Name != "th") continue;
                if (i == index) return c;
                i++;
            }
            return null;
        }

        private static string RxFirst(string s, string pattern)
        {
            try
            {
                var m = Regex.Match(s ?? "", pattern, RegexOptions.IgnoreCase);
                return m.Success && m.Groups.Count > 1 ? m.Groups[1].Value.Trim() : "";
            }
            catch { return ""; }
        }

        private static string Norm(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return Regex.Replace(s.Replace(' ', ' '), @"\s+", " ").Trim();
        }

        private static string Cut(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Length <= max ? s : s.Substring(0, max).Trim();
        }

        private static string Trim(string s, int max)
        {
            return string.IsNullOrEmpty(s) || s.Length <= max ? s : s.Substring(0, max) + "…";
        }

        public static bool TryMoney(string s, out double value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(s)) return false;
            var m = Regex.Match(s, @"[\d][\d,\.]*");
            if (!m.Success) return false;
            return double.TryParse(m.Value.Replace(",", ""), NumberStyles.Any,
                CultureInfo.InvariantCulture, out value);
        }

        private static readonly string[] DateFormats =
        {
            "dd MMM yyyy", "d MMM yyyy", "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd",
            "MMM dd, yyyy", "MMMM dd, yyyy", "dd-MMM-yyyy", "dd.MM.yyyy"
        };

        public static bool TryDate(string s, out DateTime value)
        {
            value = default(DateTime);
            string t = (s ?? "").Trim();
            if (t.Length == 0) return false;
            if (DateTime.TryParseExact(t, DateFormats, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out value)) return true;
            return DateTime.TryParse(t, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
        }

        private static bool LooksLikeDate(string s)
        {
            DateTime d;
            return TryDate(s, out d) && d.Year >= 2000 && d.Year <= 2100;
        }
    }
}
