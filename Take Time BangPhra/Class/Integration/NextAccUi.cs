using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Take_Time_BangPhra.Integration
{
    // ══════════════════════════════════════════════════════════════════════════════════════════
    //  คำไทย + ป้ายสถานะของงาน NextAcc ที่ทุกหน้าใช้ร่วมกัน (CLAUDE.md ข้อ 23)
    //
    //  ที่เดียวที่แปลรหัสภายใน (สถานะคิว, Action_Type, Entity_Type, Error_Class) เป็นคำที่พนักงานอ่านรู้เรื่อง
    //  + กติกา "ลองใหม่ทั้งหมด" ที่ปลอดภัย + ลิงก์แก้ของตรวจสุขภาพ + ข้อความเงินเดือน/Telegram.
    //  โค้ดบริสุทธิ์ (ไม่แตะ DB/HttpContext) — เทสต์ด้วย mono ได้. ข้อความที่มาจาก NextAcc/คิว ผ่าน H() ทุกครั้ง.
    // ══════════════════════════════════════════════════════════════════════════════════════════
    public static class NextAccUi
    {
        /// <summary>หน้าคิว (root-relative) — ลิงก์ทุกที่ชี้ที่นี่</summary>
        public const string QueuePagePath = "/Admin/Settings/AccountingIntegration";

        /// <summary>HTML-encode (null = "")</summary>
        public static string H(string s) { return WebUtility.HtmlEncode(s ?? ""); }

        // ── สถานะ / งาน / ชนิดรายการ ─────────────────────────────────────────────────────

        public static string StatusTh(string status)
        {
            switch ((status ?? "").Trim().ToUpperInvariant())
            {
                case "PENDING": return "รอส่ง";
                case "PROCESSING": return "กำลังส่ง";
                case "COMPLETED": return "สำเร็จ";
                case "FAILED": return "ล้มเหลว";
                case "AWAITING_SLIP": return "รอตรวจสลิป";
                case "SUPERSEDED": return "ถูกแทนด้วยรายการใหม่";
                case "CANCELLED": return "ยกเลิกรายการคิวแล้ว";
                case "": return "-";
                default: return status;
            }
        }

        private static readonly Dictionary<string, string> ActionLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "CREATE_RECEIPT_DOCUMENT", "ส่งใบเสร็จ/ใบกำกับ" },
            { "CREATE_OTA_SALES_DOCUMENT", "ส่งเอกสารรับเงิน OTA" },
            { "CREATE_VOUCHER_JOURNAL", "ส่งใบสำคัญจ่าย" },
            { "PAY_CREDIT_VOUCHER", "บันทึกจ่ายชำระใบซื้อเชื่อ" },
            { "CREATE_PAYROLL_ENTRY", "ลงบัญชีเงินเดือนรายคน" },
            { "SYNC_PAYROLL_RUN", "ส่งงวดเงินเดือน (NextAcc คำนวณเอง)" },
            { "IMPORT_PAYROLL_RUN", "ส่งงวดเงินเดือน (ยอดจาก TakeTime)" },
            { "STOCK_QTY_PUSH", "ส่งจำนวนสต๊อก" },
            { "SYNC_CUSTOMER_CONTACT", "ส่งข้อมูลผู้ติดต่อ" },
            { "VOID_RECEIPT", "ยกเลิก (void) ใบเสร็จบน NextAcc" },
            { "VOID_VOUCHER", "ยกเลิก (void) ใบสำคัญจ่ายบน NextAcc" },
            { "VOID_PAYROLL", "ยกเลิก (void) เงินเดือนบน NextAcc" },
            { "CLEAR_DEPOSIT_AT_CHECKOUT", "ตัดมัดจำตอนเช็คเอาท์" },
            { "REFUND_DEPOSIT", "คืนเงินมัดจำ" },
            { "FORFEIT_DEPOSIT", "ริบมัดจำ" },
            { "STOCK_IN", "รับสินค้าเข้า" },
            { "STOCK_OUT_COGS", "ตัดต้นทุนขาย" },
            { "STOCK_OUT_COGS_REVERSE", "กลับรายการต้นทุนขาย" },
            { "STOCK_ADJUSTMENT", "ปรับยอดสต๊อก" },
            { "STOCK_WRITEOFF", "ตัดจำหน่ายสต๊อก" },
            { "PURCHASE_RETURN", "คืนสินค้าผู้ขาย" },
            { "PURCHASE_RETURN_REVERSE", "กลับรายการคืนสินค้า (ได้ใบลดหนี้แล้ว)" },
            { "PURCHASE_RETURN_COSTDIFF", "ส่วนต่างต้นทุนคืนสินค้า" },
            { "VENDOR_BALANCE_JOURNAL", "ยอดกับผู้ขาย (ใบลดหนี้/เพิ่มหนี้)" },
            { "STOCK_OUT_REASON_JOURNAL", "จ่ายออกสต๊อกตามเหตุผล" },
            { "STOCK_OUT_REASON_REVERSE", "กลับรายการจ่ายออกสต๊อก" },
            { "SYNC_PRODUCT_MASTER", "ส่งข้อมูลสินค้า" },
            { "ASSET_RECLASSIFICATION", "โอนค่าใช้จ่ายเป็นสินทรัพย์" },
            { "OTA_CASH_RECLASS", "ย้ายเงินสดของใบ OTA" },
            { "SECURITY_DEPOSIT_JOURNAL", "เงินประกันความเสียหาย" },
            { "CREATE_CREDIT_NOTE", "ส่งใบลดหนี้" },
            { "RECORD_CN_REFUND", "บันทึกคืนเงินตามใบลดหนี้" },
            { "VOID_CREDIT_NOTE", "ยกเลิก (void) ใบลดหนี้บน NextAcc" }
        };

        private static readonly HashSet<string> DeprecatedActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CREATE_DEPOSIT_JOURNAL", "CREATE_PAYMENT_JOURNAL", "CREATE_CHECKOUT_JOURNAL", "CREATE_REFUND_JOURNAL",
            "CREATE_ROOM_CHARGE_JOURNAL", "CREATE_STOCK_IN_JOURNAL", "SYNC_PRODUCT", "CREATE_CREDIT_NOTE_DOCUMENT",
            "CREATE_PAYROLL_JOURNAL", "CREATE_CANCEL_NO_REFUND_JOURNAL", "CREATE_POS_SALE_JOURNAL",
            "CREATE_POSTPONE_PRICE_DIFF_JOURNAL", "CREATE_PARTIAL_REFUND_JOURNAL", "CREATE_DAMAGE_CHARGE_JOURNAL"
        };

        public static string ActionTh(string action)
        {
            string a = (action ?? "").Trim();
            if (a.Length == 0) return "-";
            string t;
            if (ActionLabels.TryGetValue(a, out t)) return t;
            if (DeprecatedActions.Contains(a)) return "รายการรุ่นเก่า (ไม่ใช้แล้ว)";
            return a;
        }

        private static readonly Dictionary<string, string> EntityLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "RECEIPT", "ใบเสร็จ" }, { "VOUCHER", "ใบสำคัญจ่าย" }, { "PAYROLL", "เงินเดือน" }, { "RESERVATION", "การจอง" },
            { "STOCK", "สต๊อก" }, { "JOURNAL", "สมุดรายวัน" }, { "SECURITY_DEPOSIT", "เงินประกัน" },
            { "PURCHASE_RETURN", "คืนสินค้าผู้ขาย" }, { "OTA_RECLASS", "ใบ OTA" }, { "OTA", "OTA" }, { "CUSTOMER", "ลูกค้า" },
            { "ASSET", "สินทรัพย์" }, { "PRODUCT", "สินค้า" }, { "PAYMENT", "การชำระ" }, { "DEPOSIT", "มัดจำ" },
            { "CREDIT_NOTE", "ใบลดหนี้" }, { "EMPLOYEE", "พนักงาน" }
        };

        public static string EntityTh(string entity)
        {
            string e = (entity ?? "").Trim();
            if (e.Length == 0) return "-";
            string t;
            if (EntityLabels.TryGetValue(e, out t)) return t;
            return ActionLabels.TryGetValue(e, out t) ? t : e;
        }

        // ── ชนิดปัญหา (Error_Class) ─────────────────────────────────────────────────────

        public static string ClassIcon(string cls)
        {
            switch ((cls ?? "").Trim().ToUpperInvariant())
            {
                case "DATA": return "🟧";
                case "CONFIG": return "🟪";
                case "WAIT": return "🟦";
                case "POLICY": return "🟥";
                case "DUPLICATE": return "⬛";
                case "TRANSIENT": return "⬜";
                case "UNKNOWN": return "❔";
                default: return "";
            }
        }

        /// <summary>ป้ายไทยของชนิดปัญหา (ตัวเดียวกับหน้าคิว/Telegram) — ไม่รู้จัก = ""</summary>
        public static string ClassLabelTh(string cls)
        {
            var p = QueueErrors.Parse(cls);
            return p == null ? "" : QueueErrors.BadgeTh(p.Value);
        }

        /// <summary>บอกว่าต้องทำอะไรต่อ (ใช้ใน tooltip/ข้อความ)</summary>
        public static string ClassHintTh(string cls)
        {
            switch ((cls ?? "").Trim().ToUpperInvariant())
            {
                case "DATA": return "แก้ข้อมูลในใบแล้วบันทึก — ระบบเข้าคิวใหม่เอง";
                case "CONFIG": return "แก้การตั้งค่า/ผูกบัญชีให้ครบ — ระบบคืนเข้าคิวเองเมื่อบันทึก";
                case "WAIT": return "รอรายการอื่นให้เสร็จก่อน — ระบบลองใหม่เอง";
                case "POLICY": return "แก้ตรงไม่ได้แล้ว (งวดปิด/ยื่นภาษี/e-Tax ถึงกรมฯ) — ต้องออกใบลดหนี้";
                case "DUPLICATE": return "อาจสร้างเอกสารบน NextAcc ไปแล้ว — ตรวจบน NextAcc ก่อนลองใหม่";
                case "TRANSIENT": return "NextAcc/เครือข่ายขัดข้องชั่วคราว — ระบบลองใหม่เอง";
                case "UNKNOWN": return "ไม่ทราบสาเหตุ — ดู Log ของรายการ";
                default: return "";
            }
        }

        // ── ลองใหม่ทั้งหมด: ชนิดที่ปลอดภัย ──────────────────────────────────────────────

        /// <summary>ชนิดที่ "ลองใหม่ทั้งหมด" ต้องข้าม: DUPLICATE (อาจสร้างเอกสารไปแล้ว — ยิงซ้ำ = ซ้ำ), POLICY (ต้องใบลดหนี้),
        /// CONFIG (คืนเข้าคิวเองเมื่อแก้ตั้งค่า), DATA (ตัวตรวจก่อนส่งพักไว้ — ต้องแก้ใบ/ตรวจซ้ำ)</summary>
        public static readonly string[] RetryAllBlockedClasses = { "DUPLICATE", "POLICY", "CONFIG", "DATA" };

        /// <summary>ลองใหม่ทั้งหมดได้เฉพาะ TRANSIENT / UNKNOWN / WAIT (FAILED+WAIT = รอเกินกำหนดแล้ว) และแถวที่ยังไม่จำแนก</summary>
        public static bool IsRetryAllSafe(string errorClass)
        {
            string c = (errorClass ?? "").Trim().ToUpperInvariant();
            return Array.IndexOf(RetryAllBlockedClasses, c) < 0;
        }

        /// <summary>เงื่อนไข SQL ต่อท้าย WHERE ของ "ลองใหม่ทั้งหมด" (ต้องมีคอลัมน์ Error_Class)</summary>
        public static string SqlRetryAllClassFilter()
        {
            return " AND (Error_Class IS NULL OR Error_Class NOT IN ("
                   + string.Join(",", RetryAllBlockedClasses.Select(c => "'" + c + "'")) + "))";
        }

        /// <summary>เหตุผลที่ข้าม (ไทย) — ชนิดที่ลองใหม่ได้ = null</summary>
        public static string RetrySkipReasonTh(string errorClass)
        {
            switch ((errorClass ?? "").Trim().ToUpperInvariant())
            {
                case "DUPLICATE": return "อาจสร้างเอกสารบน NextAcc ไปแล้ว — ตรวจบน NextAcc แล้วปิดรายการ หรือให้เจ้าของระบบลองใหม่รายแถวพร้อมเหตุผล";
                case "POLICY": return "ติดนโยบายบัญชี/ภาษี — ลองใหม่ไม่ช่วย ต้องออกใบลดหนี้หรือเปิดงวด";
                case "CONFIG": return "ตั้งค่า/ผูกบัญชีไม่ครบ — แก้แล้วระบบคืนเข้าคิวเอง";
                case "DATA": return "ข้อมูลในใบผิด — แก้ใบแล้วกด \"ตรวจซ้ำ\"";
                default: return null;
            }
        }

        /// <summary>ลองใหม่รายแถวที่ต้องมีเจ้าของระบบ + เหตุผล (บันทึก log)</summary>
        public static bool RetryNeedsOwnerReason(string errorClass)
        {
            return string.Equals((errorClass ?? "").Trim(), "DUPLICATE", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>เหตุผลยืนยันข้ามพอใช้ได้ไหม (≥ 10 ตัวอักษรหลังตัดช่องว่าง)</summary>
        public static bool IsReasonAcceptable(string reason)
        {
            return (reason ?? "").Trim().Length >= 10;
        }

        private static string ClassKey(string cls)
        {
            string c = (cls ?? "").Trim().ToUpperInvariant();
            return c.Length == 0 ? "" : (QueueErrors.Parse(c) == null ? "UNKNOWN" : c);
        }

        private static string ClassName(string key)
        {
            return key.Length == 0 ? "ยังไม่จำแนกชนิด" : (ClassIcon(key) + " " + ClassLabelTh(key)).Trim();
        }

        /// <summary>ข้อความยืนยันก่อน "ลองใหม่ทั้งหมด" — นับตามชนิด แยกจะลองใหม่/จะข้าม</summary>
        public static string RetryAllConfirmText(IDictionary<string, int> countsByClass)
        {
            int retry, skip;
            string body = RetryAllBreakdown(countsByClass, out retry, out skip);
            if (retry == 0 && skip == 0) return "ไม่มีรายการที่ล้มเหลวจนหมดรอบลองใหม่";
            var sb = new StringBuilder();
            sb.AppendLine("ลองใหม่รายการที่ล้มเหลวจนหมดรอบ?");
            sb.AppendLine();
            sb.Append(body);
            if (retry == 0) { sb.AppendLine(); sb.Append("ไม่มีรายการที่ลองใหม่ได้อย่างปลอดภัย — ไม่มีอะไรถูกส่งซ้ำ"); }
            return sb.ToString().TrimEnd();
        }

        /// <summary>ผลหลังสั่ง "ลองใหม่ทั้งหมด" — retried = จำนวนแถวที่รีเซ็ตจริง</summary>
        public static string RetryAllResultText(IDictionary<string, int> countsByClass, int retried)
        {
            int retry, skip;
            string body = RetryAllBreakdown(countsByClass, out retry, out skip);
            var sb = new StringBuilder();
            sb.Append("คืนเข้าคิว ").Append(retried).Append(" รายการ");
            if (skip > 0) sb.Append(" · ข้าม ").Append(skip).Append(" รายการ (ลองใหม่ไม่ปลอดภัย/ไม่ช่วย)");
            sb.AppendLine();
            if (body.Length > 0) { sb.AppendLine(); sb.Append(body); }
            return sb.ToString().TrimEnd();
        }

        private static string RetryAllBreakdown(IDictionary<string, int> countsByClass, out int retry, out int skip)
        {
            retry = 0; skip = 0;
            var merged = new Dictionary<string, int>();
            if (countsByClass != null)
                foreach (var kv in countsByClass)
                {
                    if (kv.Value <= 0) continue;
                    string k = ClassKey(kv.Key);
                    int n; merged.TryGetValue(k, out n);
                    merged[k] = n + kv.Value;
                }
            var ok = new List<string>();
            var no = new List<string>();
            foreach (var kv in merged.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                if (IsRetryAllSafe(kv.Key)) { retry += kv.Value; ok.Add("  • " + ClassName(kv.Key) + " " + kv.Value); }
                else { skip += kv.Value; no.Add("  • " + ClassName(kv.Key) + " " + kv.Value + " — " + RetrySkipReasonTh(kv.Key)); }
            }
            var sb = new StringBuilder();
            if (ok.Count > 0) { sb.AppendLine("จะลองใหม่ " + retry + " รายการ:"); foreach (var l in ok) sb.AppendLine(l); }
            if (no.Count > 0) { sb.AppendLine("จะข้าม " + skip + " รายการ:"); foreach (var l in no) sb.AppendLine(l); }
            return sb.ToString();
        }

        // ── ลิงก์คิว ───────────────────────────────────────────────────────────────────

        /// <summary>URL หน้าคิวที่กรองแล้ว. siteBase = "" → root-relative; ใส่โดเมน = URL เต็ม (Telegram)</summary>
        public static string QueueUrl(string siteBase, string cls = null, string status = null, string q = null)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(cls)) parts.Add("cls=" + Uri.EscapeDataString(cls.Trim().ToUpperInvariant()));
            if (!string.IsNullOrWhiteSpace(status)) parts.Add("status=" + Uri.EscapeDataString(status.Trim().ToUpperInvariant()));
            if (!string.IsNullOrWhiteSpace(q)) parts.Add("q=" + Uri.EscapeDataString(q.Trim()));
            parts.Add("focus=queue");
            return (siteBase ?? "").TrimEnd('/') + QueuePagePath + "?" + string.Join("&", parts) + "#queueStats";
        }

        /// <summary>ลิงก์ในแอปที่ปลอดภัย: ขึ้นต้น "/ตัวอักษร" (ไม่ใช่ //host, javascript:)</summary>
        public static bool IsSafeAppPath(string url)
        {
            return !string.IsNullOrEmpty(url) && Regex.IsMatch(url, "^/[A-Za-z]") && url.IndexOf("//", StringComparison.Ordinal) < 0
                   && url.IndexOfAny(new[] { '<', '>', '"', '\'', ' ' }) < 0;
        }

        // ── ตรวจสุขภาพ: ลิงก์ "แก้ที่นี่" + ซ่อนรหัส migration ─────────────────────────────

        private static readonly Regex PhaseCode = new Regex(@"(?:Database/)?PHASE\d+(?:_Migration)?_\d+[A-Za-z0-9_]*(?:\.sql)?", RegexOptions.Compiled);

        /// <summary>แทนรหัส migration (PHASE19_33, Database/PHASE19_Migration_33_x.sql) ด้วย "การอัปเดตฐานข้อมูล"
        /// แล้วเก็บรหัสจริงไว้ใน codes (แสดงเป็น "รายละเอียดสำหรับผู้ดูแล")</summary>
        public static string HideInternalCodes(string text, ICollection<string> codes)
        {
            if (string.IsNullOrEmpty(text)) return text ?? "";
            string r = PhaseCode.Replace(text, m =>
            {
                if (codes != null && !codes.Contains(m.Value)) codes.Add(m.Value);
                return "การอัปเดตฐานข้อมูล";
            });
            r = r.Replace("migration การอัปเดตฐานข้อมูล", "การอัปเดตฐานข้อมูล")
                 .Replace("รัน การอัปเดตฐานข้อมูล", "รันการอัปเดตฐานข้อมูล");
            return r;
        }

        private static readonly Regex MappingKeyRx = new Regex(@"\b([A-Z][A-Z0-9]*(?:_[A-Z0-9]+)+)\b\s*(?:→|->)", RegexOptions.Compiled);
        // "ยังไม่ผูกบัญชี INVENTORY" / "ยังไม่ผูก mapping ROOM_AR"
        private static readonly Regex TitleKeyRx = new Regex(@"ผูก(?:บัญชี)?\s*(?:mapping\s+)?([A-Z][A-Z0-9]{2,}(?:_[A-Z0-9]+)*)\b", RegexOptions.Compiled);
        // "ROOM_AR ชี้รหัสบัญชีที่ NextAcc ไม่มี"
        private static readonly Regex TitlePointsRx = new Regex(@"\b([A-Z][A-Z0-9]*_[A-Z0-9_]+)\s+ชี้รหัส", RegexOptions.Compiled);

        /// <summary>ลิงก์ "แก้ที่นี่" ของรายการตรวจสุขภาพ — "?focus=…" = ส่วนในหน้าตั้งค่านี้, "/…" = หน้าอื่น. คืนค่าเสมอ</summary>
        public static string HealthFixUrl(string title, string detail)
        {
            string t = title ?? "";
            string all = t + "\n" + (detail ?? "");
            Func<string, bool> has = s => all.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0;
            Func<string, bool> tHas = s => t.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0;

            if (tHas("NextAcc ไม่พร้อมใช้งาน") || tHas("ตั้งค่า NextAcc") || tHas("Integration Key") || tHas("Company ID"))
                return "?focus=api";
            // ผูกบัญชีรายคีย์ (ROOM_AR, INVENTORY, VENDOR_CREDIT_RECEIVABLE …) → แถว mapping นั้น
            if (has("VENDOR_CREDIT_RECEIVABLE")) return "?focus=mapping&key=VENDOR_CREDIT_RECEIVABLE";
            if (tHas("เหตุผลการเบิก")) return "?focus=mapping&key=OWNER_DRAWINGS";
            if (tHas("21913") || has("OUTPUT_VAT_DEFERRED") && tHas("พัก VAT")) return "?focus=mapping&key=OUTPUT_VAT_DEFERRED";
            var tk = TitleKeyRx.Match(t);
            if (!tk.Success) tk = TitlePointsRx.Match(t);
            if (tk.Success) return "?focus=mapping&key=" + Uri.EscapeDataString(tk.Groups[1].Value);
            if (tHas("mapping"))
            {
                var m = MappingKeyRx.Match(detail ?? "");
                return m.Success ? "?focus=mapping&key=" + Uri.EscapeDataString(m.Groups[1].Value) : "?focus=mapping";
            }
            if (tHas("ผังบัญชี")) return "?focus=api";          // ดึง/อายุผังบัญชี — ปุ่ม "Sync บัญชี" อยู่การ์ดเชื่อมต่อ
            if (tHas("รอใบลดหนี้") || tHas("คืนสินค้า")) return "/Product/PurchaseReturn";
            if (tHas("ผู้ขาย")) return "/Account/CreditPurchasePayment";
            if (tHas("ใบลดหนี้") && tHas("แหล่งเงิน")) return "?focus=paidHow";
            if (tHas("ใบลดหนี้") || tHas("คืนเงิน")) return "/Account/CreditNotes";
            if (tHas("เกตเวย์") || tHas("ช่องทางชำระ")) return "?focus=gateway";
            if (tHas("OTA")) return "?focus=ota";
            if (tHas("แหล่งเงิน") || tHas("กระเป๋าเงิน") || tHas("เงินประกัน")) return "?focus=paidHow";
            if (tHas("ไม่ตรงกับใบเสร็จ") || tHas("ตัวตรวจเทียบ")) return "/ReserveTable";
            if (tHas("อีเมลจอง") || tHas("อ่านอีเมล") || tHas("หน้าต่างลองใหม่")) return "?focus=email";
            if (tHas("คิวพักเพราะข้อมูล")) return "?focus=queue&cls=DATA";
            if (tHas("คิวพักเพราะการตั้งค่า")) return "?focus=queue&cls=CONFIG";
            if (tHas("คิวติดนโยบาย")) return "?focus=queue&cls=POLICY";
            if (tHas("ล้มเหลว") || tHas("ยกเลิกเอกสาร")) return "?focus=queue&status=FAILED";
            if (tHas("คิว") || tHas("PROCESSING") || tHas("ยังไม่ขึ้น NextAcc") || tHas("งานเบื้องหลัง") || tHas("ตัวตรวจก่อนส่ง"))
                return "?focus=queue";
            return "?focus=sync";
        }

        /// <summary>ป้ายปุ่มของลิงก์แก้ (ไทย)</summary>
        public static string HealthFixLabel(string fixUrl)
        {
            string u = fixUrl ?? "";
            if (u.StartsWith("/Account/CreditNotes", StringComparison.OrdinalIgnoreCase)) return "ไปหน้าใบลดหนี้";
            if (u.StartsWith("/Product/PurchaseReturn", StringComparison.OrdinalIgnoreCase)) return "ไปหน้าคืนสินค้าผู้ขาย";
            if (u.StartsWith("/ReserveTable", StringComparison.OrdinalIgnoreCase)) return "ไปตารางจอง";
            if (u.StartsWith("/Account/CreditPurchasePayment", StringComparison.OrdinalIgnoreCase)) return "ไปหน้าซื้อเชื่อ/ยอดกับผู้ขาย";
            return "แก้ที่นี่";
        }

        // ── ป้ายสถานะในหน้าเอกสาร (CheckDocument_New / CheckPayment_New) ────────────────

        public sealed class SyncBadge
        {
            public string Html;
            public bool ShowRetry;
            public string RetryText;
        }

        /// <summary>ป้ายของแถวคิวที่ยังไม่สำเร็จ (COMPLETED หน้าเอกสารทำเองเพราะมีลิงก์เปิดเอกสาร).
        /// errorMessage ถูก encode เสมอ. fixUrlResolved/queueLinkResolved = URL ที่ resolve แล้ว (ว่าง = ไม่แสดง)</summary>
        public static SyncBadge DocQueueBadge(string status, string errorClass, string queueId, string errorMessage,
                                              string fixUrlResolved, string queueLinkResolved)
        {
            string st = (status ?? "").Trim().ToUpperInvariant();
            string cls = (errorClass ?? "").Trim().ToUpperInvariant();
            string qid = H(queueId);
            var b = new SyncBadge { ShowRetry = false, RetryText = "🔄 ส่งใหม่" };
            string queueLink = string.IsNullOrEmpty(queueLinkResolved) ? ""
                : "<a href='" + H(queueLinkResolved) + "' target='_blank' style='font-size:10px;margin-left:4px;'>ดูในคิว</a>";

            switch (st)
            {
                case "PENDING":
                    b.Html = cls == "WAIT"
                        ? "<span class='sync-badge pending' title='" + H(ClassHintTh("WAIT") + (string.IsNullOrEmpty(errorMessage) ? "" : " · " + Trunc(errorMessage, 300))) + "'>🟦 รอรายการอื่น #" + qid + "</span>"
                        : "<span class='sync-badge pending'>" + StatusTh(st) + " #" + qid + "</span>";
                    return b;
                case "PROCESSING":
                    b.Html = "<span class='sync-badge pending'>" + StatusTh(st) + " #" + qid + "</span>";
                    return b;
                case "AWAITING_SLIP":
                    b.Html = "<span class='sync-badge pending' title='ระบบพักไว้จนกว่าสลิปจะผ่าน/อนุมัติที่หน้าตรวจสลิปโอน'>🧾 " + StatusTh(st) + " #" + qid + "</span>";
                    return b;
                case "FAILED":
                {
                    string label = ClassLabelTh(cls);
                    string title = (cls.Length > 0 ? ClassHintTh(cls) + " · " : "") + Trunc(errorMessage, 300);
                    b.Html = "<span class='sync-badge failed' title='" + H(title) + "'>"
                             + (label.Length > 0 ? ClassIcon(cls) + " " + H(label) : "ล้มเหลว") + " #" + qid + "</span>";
                    if (IsSafeAppPathResolved(fixUrlResolved))
                        b.Html += "<a href='" + H(fixUrlResolved) + "' target='_blank' style='font-size:10px;margin-left:4px;'>🛠 แก้ที่นี่</a>";
                    else if (cls == "CONFIG" || cls == "POLICY" || cls == "DUPLICATE")
                        b.Html += queueLink;
                    // ตั้งค่า (กลับเข้าคิวเอง) / นโยบาย (ต้องใบลดหนี้) / อาจซ้ำ (ตรวจบน NextAcc ก่อน) — ส่งใหม่จากหน้านี้ไม่ช่วยหรือเสี่ยงซ้ำ
                    b.ShowRetry = !(cls == "CONFIG" || cls == "POLICY" || cls == "DUPLICATE");
                    if (cls == "DATA") b.RetryText = "🔄 ส่งใหม่ (หลังแก้ใบ)";
                    return b;
                }
                case "SUPERSEDED":
                case "CANCELLED":
                    b.Html = "<span class='sync-badge none' title='" + H(st) + "'>" + StatusTh(st) + " #" + qid + "</span>";
                    return b;
                default:
                    b.Html = "<span class='sync-badge pending'>" + H(StatusTh(st)) + " #" + qid + "</span>";
                    return b;
            }
        }

        private static bool IsSafeAppPathResolved(string url)
        {
            // หลัง ResolveUrl อาจมี virtual dir นำหน้า ("/app/Account/…") — กติกาเดียวกับ IsSafeAppPath
            return IsSafeAppPath(url);
        }

        private static string Trunc(string s, int max)
        {
            s = s ?? "";
            return s.Length > max ? s.Substring(0, max) + "…" : s;
        }

        /// <summary>แถบเตือนเมื่อดึงรายการจาก NextAcc ไม่ทัน — รายการอาจไม่ครบ อย่ากดส่งซ้ำ</summary>
        public static string FetchWarningHtml(int seconds, string extra = null)
        {
            return "<div style='margin:8px 0;padding:10px 12px;border-radius:6px;background:#FFF8E1;border:1px solid #FFE082;color:#7a5200;font-size:13px;'>"
                 + "⚠ <b>ดึงข้อมูลจาก NextAcc ไม่ทันใน " + seconds + " วินาที</b> — ตารางด้านล่างแสดงเฉพาะข้อมูลในระบบ (อาจไม่ครบ/สถานะ NextAcc ยังไม่อัปเดต). "
                 + "<b>อย่าเพิ่งกด \"Sync\"/\"ส่งใหม่\" ซ้ำ</b> — รอสักครู่แล้วกดค้นหาอีกครั้ง"
                 + (string.IsNullOrEmpty(extra) ? "" : "<br/><span style='font-size:12px;'>" + H(extra) + "</span>")
                 + "</div>";
        }

        // ── เงินเดือน ─────────────────────────────────────────────────────────────────

        /// <summary>ข้อความ (HTML) หลังอนุมัติ/ทำจ่ายเงินเดือน ตามโหมด — ไม่โชว์รหัสโหมด, มีลิงก์ไปคิว.
        /// mode = DOCUMENT_IMPORT | DOCUMENT | JOURNAL_ONLY | LOCAL. queueId &lt;= 0 = เข้าคิวไม่สำเร็จ (ยกเว้น JOURNAL_ONLY/LOCAL)</summary>
        public static string PayrollSyncNoteHtml(string mode, long queueId, string queueUrlResolved)
        {
            string m = (mode ?? "").Trim().ToUpperInvariant();
            string link = string.IsNullOrEmpty(queueUrlResolved) ? ""
                : " <a href='" + H(queueUrlResolved) + "' target='_blank'>ดูสถานะการส่ง</a>";
            switch (m)
            {
                case "DOCUMENT_IMPORT":
                    return queueId > 0
                        ? "<br/>📤 ส่งงวดเงินเดือนขึ้น NextAcc แล้ว (รอคิว) — NextAcc จะออกสลิปเงินเดือน ภ.ง.ด.1 สปส.1-10 และ 50 ทวิ จากยอดที่ระบบนี้คำนวณ." + link
                        : "<br/>⚠ ส่งงวดเงินเดือนขึ้น NextAcc ไม่สำเร็จ — ตรวจการตั้งค่า NextAcc แล้วกดอีกครั้ง" + link;
                case "DOCUMENT":
                    return queueId > 0
                        ? "<br/>📤 ส่งงวดเงินเดือนให้ NextAcc คำนวณและออกเอกสารแล้ว (รอคิว) — ยอดบน NextAcc อาจต่างจากที่ระบบนี้คำนวณ." + link
                        : "<br/>⚠ ส่งงวดเงินเดือนขึ้น NextAcc ไม่สำเร็จ — ตรวจการตั้งค่า NextAcc แล้วกดอีกครั้ง" + link;
                case "JOURNAL_ONLY":
                    return "<br/>📒 ลงบัญชีเงินเดือนเข้า NextAcc เป็นรายการบัญชีรายคนตอนทำจ่าย." + link
                         + "<br/>" + PayrollStatutoryWarningHtml();
                default:
                    return "";
            }
        }

        /// <summary>คำเตือนโหมดลงบัญชีอย่างเดียว — แบบภาษี/ประกันสังคมต้องยื่นเองจากข้อมูลระบบนี้</summary>
        public static string PayrollStatutoryWarningHtml()
        {
            return "<b>⚠ โหมดนี้ NextAcc ไม่ออก ภ.ง.ด.1 / สปส.1-10 / 50 ทวิ ให้</b> — ต้องยื่นแบบจากข้อมูลในระบบนี้เอง "
                 + "(ปุ่ม \"Export ภ.ง.ด.1\" และ \"Export ประกันสังคม\") ภายในกำหนดของกรมสรรพากร/สำนักงานประกันสังคม";
        }

        // ── Telegram ──────────────────────────────────────────────────────────────────

        /// <summary>บรรทัดลิงก์ Telegram (parse_mode HTML) — 1 ลิงก์ต่อชนิดปัญหาที่ค้าง + ลิงก์คิวทั้งหมด</summary>
        public static string TelegramQueueLinks(string siteBase, IDictionary<string, int> countsByClass)
        {
            var sb = new StringBuilder();
            var links = new List<string>();
            if (countsByClass != null)
                foreach (string k in new[] { "DATA", "CONFIG", "POLICY", "DUPLICATE", "WAIT", "TRANSIENT", "UNKNOWN" })
                {
                    int n;
                    if (countsByClass.TryGetValue(k, out n) && n > 0)
                        links.Add("<a href=\"" + H(QueueUrl(siteBase, cls: k)) + "\">" + ClassIcon(k) + " " + H(ClassLabelTh(k)) + " " + n + "</a>");
                }
            if (links.Count > 0) sb.AppendLine("🔗 เปิดคิวตามชนิด: " + string.Join(" · ", links));
            sb.Append("🔗 <a href=\"" + H(QueueUrl(siteBase, status: "FAILED")) + "\">เปิดคิวที่ล้มเหลว</a>");
            return sb.ToString();
        }

        /// <summary>สรุปประจำวัน — ไม่มีอะไรค้าง = "ปกติดี" (บอกว่าระบบเตือนยังทำงาน)</summary>
        public static string DailySummaryHtml(DateTime day, int completedYesterday, int failedOpen, int waitOpen, int pendingStale, bool serverDown, string siteBase)
        {
            var sb = new StringBuilder();
            bool ok = failedOpen == 0 && pendingStale == 0 && !serverDown;
            sb.AppendLine(ok ? "<b>✅ คิวบัญชี NextAcc ปกติดี</b>" : "<b>🟠 สรุปคิวบัญชี NextAcc ประจำวัน</b>");
            sb.AppendLine(day.ToString("dd/MM/yyyy") + " · เมื่อวานส่งสำเร็จ " + completedYesterday + " รายการ");
            if (serverDown) sb.AppendLine("🔴 NextAcc ไม่พร้อมใช้งานตอนนี้ — ระบบพักการส่งและจะส่งต่อเอง");
            if (failedOpen > 0) sb.AppendLine("❌ ล้มเหลวรอคนแก้: " + failedOpen + " รายการ");
            if (waitOpen > 0) sb.AppendLine("🟦 รอรายการอื่น: " + waitOpen + " รายการ");
            if (pendingStale > 0) sb.AppendLine("🕐 ค้างเกิน 6 ชม.: " + pendingStale + " รายการ");
            if (ok) sb.AppendLine("ไม่มีรายการค้าง — ข้อความนี้ส่งวันละครั้งเพื่อยืนยันว่าระบบแจ้งเตือนยังทำงาน");
            sb.Append("🔗 <a href=\"" + H(QueueUrl(siteBase, status: ok ? null : "FAILED")) + "\">เปิดคิว</a>");
            return sb.ToString();
        }
    }
}
