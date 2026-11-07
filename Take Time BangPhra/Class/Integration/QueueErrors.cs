using System;
using System.Text.RegularExpressions;

namespace Take_Time_BangPhra.Integration
{
    // ══════════════════════════════════════════════════════════════════════════════════════════
    //  ชนิดปัญหาของแถวคิว NextAcc (PHASE19_37 — กันคิวค้าง/ล้ม, CLAUDE.md ข้อ 21)
    //
    //  เดิม worker แยกปัญหาจาก "ชนิด exception" (ArgumentException = ตายทันที, Exception = retry 5 ครั้ง ~2.7 ชม.)
    //  ซึ่งไม่ได้บอกว่าต้องแก้ที่ไหน ⇒ ข้อมูลผิด retry เปล่า ๆ 5 ครั้ง, รอใบมัดจำตายทั้งที่อีกชั่วโมงก็ขึ้น,
    //  mapping ผิดแก้แล้วไม่กลับมาเอง. ไฟล์นี้ให้คำศัพท์กลาง: ชนิดปัญหา (Error_Class) + กฎ (Error_Rule) + ลิงก์แก้ (Fix_Url)
    //
    //  ความเข้ากันได้ย้อนหลัง: QueueData/Config/PolicyException สืบจาก ArgumentException → catch เดิม = ตายทันที (ถูกต้องอยู่แล้ว
    //  เพราะ retry ไม่ช่วย). QueueWaitException ไม่ใช่ ArgumentException → ต้องจับก่อน catch (Exception).
    //  QueueErrors.Enabled = false (ยังไม่รัน migration 37 หรือ Nexaacc_Preflight_Mode=OFF) → ตัวช่วยคืน Exception ธรรมดา = พฤติกรรมเดิมเป๊ะ
    // ══════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>ชนิดปัญหาของแถวคิว — กำหนดว่าใครต้องทำอะไร</summary>
    public enum SyncIssueClass { Data, Config, Wait, Transient, Policy, Duplicate, Unknown }

    /// <summary>exception ที่บอกชนิดปัญหาของตัวเองได้</summary>
    public interface IQueueIssue
    {
        SyncIssueClass IssueClass { get; }
        string Rule { get; }
        string FixKind { get; }
        string FixUrl { get; }
    }

    /// <summary>ข้อมูลฝั่ง TakeTime ผิด (ยอด/บรรทัด/ผู้ซื้อ) — retry ไม่ช่วย ต้องแก้ใบแล้วระบบเข้าคิวใหม่เอง</summary>
    public class QueueDataException : ArgumentException, IQueueIssue
    {
        public QueueDataException(string rule, string message, string fixKind = null, string fixUrl = null) : base(message)
        { Rule = rule; FixKind = fixKind ?? "EDIT_RECEIPT"; FixUrl = fixUrl; }
        public SyncIssueClass IssueClass { get { return SyncIssueClass.Data; } }
        public string Rule { get; private set; }
        public string FixKind { get; private set; }
        public string FixUrl { get; private set; }
    }

    /// <summary>ตั้งค่า/ผูกบัญชีไม่ครบ — ไม่บล็อกหน้าร้าน; คิวพักไว้แล้วกลับมาเองเมื่อแอดมินบันทึก mapping</summary>
    public class QueueConfigException : ArgumentException, IQueueIssue
    {
        public QueueConfigException(string rule, string mappingKey, string message, string fixUrl = null) : base(message)
        { Rule = rule; MappingKey = mappingKey; FixUrl = fixUrl; }
        public SyncIssueClass IssueClass { get { return SyncIssueClass.Config; } }
        public string Rule { get; private set; }
        public string MappingKey { get; private set; }
        public string FixKind { get { return "MAPPING"; } }
        public string FixUrl { get; private set; }
    }

    /// <summary>ติดนโยบายบัญชี/ภาษี (งวดปิด, ยื่น ภ.พ.30 แล้ว, e-Tax ถึงกรมฯ, มีใบลดหนี้อ้าง) — ต้องใช้ใบลดหนี้/เปิดงวด</summary>
    public class QueuePolicyException : ArgumentException, IQueueIssue
    {
        public QueuePolicyException(string rule, string message, string fixUrl = null) : base(message)
        { Rule = rule; FixUrl = fixUrl; }
        public SyncIssueClass IssueClass { get { return SyncIssueClass.Policy; } }
        public string Rule { get; private set; }
        public string FixKind { get { return "CREDIT_NOTE"; } }
        public string FixUrl { get; private set; }
    }

    /// <summary>รอรายการอื่นก่อน (ใบมัดจำยังไม่ขึ้น, ใบต้นทางยังรอคิว ฯลฯ) — คง PENDING ไม่นับ retry, ตื่นเมื่อรายการที่รอสำเร็จ</summary>
    public class QueueWaitException : Exception
    {
        public QueueWaitException(string reason, long? dependsOnQueueId = null, int recheckMinutes = 5) : base(reason)
        {
            WaitReason = reason;
            DependsOnQueueId = dependsOnQueueId;
            RecheckMinutes = recheckMinutes < 1 ? 1 : (recheckMinutes > 240 ? 240 : recheckMinutes);
        }
        public string WaitReason { get; private set; }
        public long? DependsOnQueueId { get; private set; }
        public int RecheckMinutes { get; private set; }
    }

    /// <summary>ผลจำแนก error ของ NextAcc / ข้อความ</summary>
    public struct IssueVerdict
    {
        public SyncIssueClass Class;
        public string Rule;
        public string FixKind;
        public IssueVerdict(SyncIssueClass c, string rule, string fixKind) { Class = c; Rule = rule; FixKind = fixKind; }
    }

    public static class QueueErrors
    {
        /// <summary>เปิดการจำแนกชนิดปัญหา — worker ตั้งทุกรอบ (มีคอลัมน์ PHASE19_37 และ Nexaacc_Preflight_Mode ≠ OFF).
        /// ปิด = ตัวช่วยด้านล่างคืน Exception ธรรมดา (พฤติกรรมก่อน PHASE19_37 ทุกประการ)</summary>
        public static volatile bool Enabled;

        public static string Code(SyncIssueClass c)
        {
            switch (c)
            {
                case SyncIssueClass.Data: return "DATA";
                case SyncIssueClass.Config: return "CONFIG";
                case SyncIssueClass.Wait: return "WAIT";
                case SyncIssueClass.Transient: return "TRANSIENT";
                case SyncIssueClass.Policy: return "POLICY";
                case SyncIssueClass.Duplicate: return "DUPLICATE";
                default: return "UNKNOWN";
            }
        }

        public static SyncIssueClass? Parse(string code)
        {
            switch ((code ?? "").Trim().ToUpperInvariant())
            {
                case "DATA": return SyncIssueClass.Data;
                case "CONFIG": return SyncIssueClass.Config;
                case "WAIT": return SyncIssueClass.Wait;
                case "TRANSIENT": return SyncIssueClass.Transient;
                case "POLICY": return SyncIssueClass.Policy;
                case "DUPLICATE": return SyncIssueClass.Duplicate;
                case "UNKNOWN": return SyncIssueClass.Unknown;
                default: return null;
            }
        }

        /// <summary>ป้ายไทยที่หน้าคิว/Telegram ใช้</summary>
        public static string BadgeTh(SyncIssueClass c)
        {
            switch (c)
            {
                case SyncIssueClass.Data: return "แก้ข้อมูล";
                case SyncIssueClass.Config: return "ตั้งค่า";
                case SyncIssueClass.Wait: return "รอ";
                case SyncIssueClass.Transient: return "ลองใหม่อัตโนมัติ";
                case SyncIssueClass.Policy: return "ต้องใบลดหนี้";
                case SyncIssueClass.Duplicate: return "ตรวจบน NextAcc";
                default: return "ไม่ทราบสาเหตุ";
            }
        }

        public static Exception Data(string rule, string message, string fixKind = null, string fixUrl = null)
        {
            return Enabled ? (Exception)new QueueDataException(rule, message, fixKind, fixUrl) : new Exception(message);
        }

        public static Exception Config(string rule, string mappingKey, string message, string fixUrl = null)
        {
            return Enabled ? (Exception)new QueueConfigException(rule, mappingKey, message, fixUrl) : new Exception(message);
        }

        public static Exception Policy(string rule, string message, string fixUrl = null)
        {
            return Enabled ? (Exception)new QueuePolicyException(rule, message, fixUrl) : new Exception(message);
        }

        public static Exception Wait(string reason, long? dependsOnQueueId = null, int recheckMinutes = 5)
        {
            return Enabled ? (Exception)new QueueWaitException(reason, dependsOnQueueId, recheckMinutes) : new Exception(reason);
        }
    }

    /// <summary>
    /// จำแนก error ของ NextAcc: <c>data.ruleCode</c> ก่อน (NextAcc <c>Middleware/ExceptionMiddleware.cs:191-198</c> แนบรหัสกฎของ
    /// BusinessRuleException ทุกตัว) แล้วค่อยข้อความ. สถานะ: InvalidOperation/Argument → 400, KeyNotFound → 404 (<c>:56-70</c>)
    /// </summary>
    public static class ApiErrorClassifier
    {
        private static readonly Regex RuleCodeRx = new Regex("\"ruleCode\"\\s*:\\s*\"([A-Za-z0-9][A-Za-z0-9\\-_]{1,80})\"", RegexOptions.Compiled);

        public static string ExtractRuleCode(string body)
        {
            if (string.IsNullOrEmpty(body)) return null;
            var m = RuleCodeRx.Match(body);
            return m.Success ? m.Groups[1].Value.ToUpperInvariant() : null;
        }

        public static IssueVerdict Classify(int status, string body)
        {
            string b = body ?? "";
            string rule = ExtractRuleCode(b);
            if (rule != null)
            {
                // ถึงกรมสรรพากร/ยื่นแบบ/นำส่งแล้ว — แก้ด้วยใบลดหนี้เท่านั้น
                //   RD-ETAX-REACHED-VOID: DocumentService.cs:9570 · RD-50TWI-FILED: :9549 · PP36-CHANGE-AFTER-REMIT: Pp36Lifecycle.cs:54 (:9554)
                if (rule.StartsWith("RD-ETAX", StringComparison.Ordinal) || rule == "RD-50TWI-FILED"
                    || rule.StartsWith("PP36-CHANGE", StringComparison.Ordinal) || rule == "PP36-DOC-ALREADY-REMITTED"
                    || rule == "REISSUE-BLOCKED")
                    return new IssueVerdict(SyncIssueClass.Policy, rule, "CREDIT_NOTE");
                // ผูกรอบโอนเงินเกตเวย์แล้ว (DocumentService.cs:9544-9546) — ต้องปลดที่ NextAcc
                if (rule == "SETTLEMENT-ARTIFACT-VOID")
                    return new IssueVerdict(SyncIssueClass.Policy, rule, "NEXTACC");
                // ข้อมูลบรรทัดเราเอง: ประเภทเงินได้หัก ณ ที่จ่าย (DocumentService.cs:475/1175/3121), JE ไม่สมดุล
                if (rule == "WHT-INCOME-TYPE" || rule == "JE-BALANCE")
                    return new IssueVerdict(SyncIssueClass.Data, rule, "EDIT_SOURCE");
                if (rule == "MONEY-SOURCE" || rule == "DEPOSIT-KIND")
                    return new IssueVerdict(SyncIssueClass.Config, rule, "PAIDHOW");
                // รหัสกฎอื่น → ตัดสินจากข้อความต่อ (คงรหัสไว้เป็น rule)
            }

            if (b.Contains("86/4"))
                return new IssueVerdict(SyncIssueClass.Data, rule ?? "BUYER_86_4", "EDIT_CUSTOMER");
            if (b.Contains("ไม่พบผังบัญชี") || b.Contains("ไม่อยู่ในผังของบริษัท")
                || (b.Contains("บัญชี") && b.Contains("ถูกปิดใช้งาน")))
                return new IssueVerdict(SyncIssueClass.Config, rule ?? "NA_ACCOUNT_MISSING", "MAPPING");
            // งวดปิด: JournalEntryBuilder.ClosedPeriodMessage "อยู่ในงวด '…' ที่ปิดแล้ว"
            if ((b.Contains("งวด") && (b.Contains("ปิดแล้ว") || b.Contains("Closed") || b.Contains("Locked")))
                || b.Contains("งวดที่ปิด"))
                return new IssueVerdict(SyncIssueClass.Policy, rule ?? "PERIOD_CLOSED", "CREDIT_NOTE");
            // ยื่น ภ.พ.30 แล้ว (IntegrationService.cs:3505-3516) / รายงานภาษี Filed (DocumentService.cs:9561)
            if ((b.Contains("ภ.พ.30") && (b.Contains("ยื่น") || b.Contains("Filed")))
                || b.Contains("รายงานภาษีที่ Filed"))
                return new IssueVerdict(SyncIssueClass.Policy, rule ?? "VAT_FILED", "CREDIT_NOTE");
            if (b.Contains("ใบลดหนี้/ใบเพิ่มหนี้/เอกสารลูกอ้างถึง") || b.Contains("เอกสารลูก"))
                return new IssueVerdict(SyncIssueClass.Policy, rule ?? "HAS_CHILD_DOC", "CREDIT_NOTE");
            // มัดจำบน NextAcc ถูกใช้ไปแล้ว (DocumentService.cs:6084/6120) — สถานะฝั่ง NextAcc ต้องมีคนดู
            if (b.Contains("ถูกนำไปตัดชำระเอกสารอื่นแล้ว") || b.Contains("ไม่มียอดมัดจำคงเหลือ") || b.Contains("ถูกนำไปหัก"))
                return new IssueVerdict(SyncIssueClass.Duplicate, rule ?? "DEPOSIT_JV_APPLIED", "NEXTACC");
            if (status == 408 || status == 429)
                return new IssueVerdict(SyncIssueClass.Transient, rule ?? ("HTTP_" + status), null);
            // Idempotency-Key กำลังประมวลผล (IdempotencyMiddleware.cs:47-145 → 409 + Retry-After)
            if (status == 409 && (b.IndexOf("idempoten", StringComparison.OrdinalIgnoreCase) >= 0
                                  || b.IndexOf("in progress", StringComparison.OrdinalIgnoreCase) >= 0
                                  || b.Contains("กำลังประมวลผล")))
                return new IssueVerdict(SyncIssueClass.Transient, rule ?? "IDEMPOTENCY_IN_FLIGHT", null);
            if (status >= 500)
                return new IssueVerdict(SyncIssueClass.Transient, rule ?? ("HTTP_" + status), null);
            return new IssueVerdict(SyncIssueClass.Unknown, rule ?? (status > 0 ? "HTTP_" + status : null), null);
        }

        /// <summary>exception ธรรมดา (ไม่มี HTTP status) — แยก "เครือข่าย/หมดเวลา" (ลองใหม่ได้) ออกจาก "ไม่ทราบสาเหตุ"</summary>
        public static IssueVerdict ClassifyMessage(string message)
        {
            string m = message ?? "";
            string[] transient =
            {
                "timed out", "timeout", "Timeout", "failed after", "Unable to connect", "connection was closed",
                "connection attempt failed", "SocketException", "An error occurred while sending the request",
                "(502)", "(503)", "(504)", "Bad Gateway", "Service Unavailable", "Gateway Timeout",
                "TaskCanceledException", "A task was canceled", "จะลองใหม่อัตโนมัติ", "เลื่อนไปรอบถัดไป"
            };
            foreach (string t in transient)
                if (m.IndexOf(t, StringComparison.Ordinal) >= 0)
                    return new IssueVerdict(SyncIssueClass.Transient, "NETWORK_OR_RETRYABLE", null);
            var api = Classify(0, m);
            return api.Class == SyncIssueClass.Unknown ? new IssueVerdict(SyncIssueClass.Unknown, null, null) : api;
        }
    }
}
