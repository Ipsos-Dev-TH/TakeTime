using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Take_Time_BangPhra.Integration
{
    // ══════════════════════════════════════════════════════════════════════════════════════════
    //  ป้าย "สถานะ NextAcc" แบบเดียวทุกหน้า (CLAUDE.md ข้อ 24)
    //
    //  ResolveBadge(input)  — ตัดสินสถานะจากแถวคิวล่าสุด (Status + Error_Class) + เอกสารที่รู้แล้ว (marker/Nexaacc_Doc_Id)
    //  BuildBadgeLinks(...) — ลิงก์ เปิดใน NextAcc / PDF / แก้ที่นี่ / ดูในคิว (ตรวจรูปแบบ URL ก่อนใช้ทุกครั้ง)
    //  RenderBadge(b, l)    — HTML (encode ทุกข้อความ) ใช้ CSS กลาง Content/nextacc-ui.css (.na-st)
    //  AggregateBadges(...) — หลายรายการรวมเป็นป้ายเดียว (เช่น การรับของ 1 ใบมีหลายสินค้า)
    //  โค้ดบริสุทธิ์ (ไม่แตะ DB/HttpContext) — เทสต์ด้วย mono ได้. ดึงข้อมูลแบบ batch อยู่ที่ NextAccStatusData.
    // ══════════════════════════════════════════════════════════════════════════════════════════
    public static partial class NextAccUi
    {
        public enum NaState
        {
            Sent,             // ส่งแล้ว (มีเอกสาร/JE บน NextAcc)
            Pending,          // รอส่ง
            Sending,          // กำลังส่ง
            Waiting,          // รอเอกสารอื่น / รอตรวจสลิป
            NeedsData,        // ต้องแก้ข้อมูล (DATA)
            NeedsConfig,      // ต้องตั้งค่า (CONFIG)
            NeedsCreditNote,  // ต้องใบลดหนี้ (POLICY)
            MaybeDuplicate,   // อาจซ้ำ (DUPLICATE) — ตรวจบน NextAcc ก่อน
            Failed,           // ส่งไม่สำเร็จ (ชั่วคราว/ไม่ทราบสาเหตุ — ลองใหม่ได้)
            Voided,           // ยกเลิกแล้ว (void บน NextAcc / กลับรายการ)
            NotSent,          // ยังไม่ส่ง
            NotApplicable     // ไม่ส่งบัญชี (นโยบาย/LOCAL/ยังไม่อนุมัติ)
        }

        /// <summary>แถวคิว (Accounting_Sync_Queue) ที่ป้ายต้องใช้ — ค่าที่ไม่มี = null</summary>
        public sealed class NaQueueRow
        {
            public long Id;
            public string Status;
            public string ActionType;
            public string ErrorClass;
            public string ErrorMessage;
            public string FixUrl;
            public string ResponseId;       // Nexaacc_Response_Id
            public string DocNumber;        // Nexaacc_Document_Number
            public string DocType;          // Nexaacc_Document_Type (INVOICE/RECEIPT/JOURNAL/…)
            public DateTime? Created;
        }

        public sealed class NaBadgeInput
        {
            /// <summary>แถวคิวล่าสุดของรายการ (เฉพาะงานสร้าง/ส่ง — ไม่ใช่ VOID) · null = ไม่เคยเข้าคิว</summary>
            public NaQueueRow Queue;
            /// <summary>เอกสารที่รู้แล้วจากฝั่งรายการ (marker/Nexaacc_Doc_Id/Journal_Ref) — มีค่า = ส่งแล้วแม้ไม่พบแถวคิว</summary>
            public string DocId;
            public string DocNumber;
            public string DocType;
            /// <summary>รู้ว่าลงบัญชีแล้ว (เช่น JE อย่างเดียว ไม่มีเลขเอกสาร) — นับเป็นส่งแล้วเมื่อไม่มีแถวคิวตัดสิน</summary>
            public bool KnownPosted;
            /// <summary>รายการ/เอกสารถูกยกเลิก (void) หรือกลับรายการแล้ว</summary>
            public bool Voided;
            public string VoidedLabel;
            /// <summary>รายการนี้ไม่ส่งบัญชี (นโยบาย/โหมด LOCAL/ยังไม่อนุมัติ) — ตัดสินก่อนคิว</summary>
            public bool NotApplicable;
            public string NotApplicableLabel;
            public string NotApplicableReason;
            /// <summary>ป้ายของสถานะส่งแล้ว (ค่าเริ่มต้น "ส่งแล้ว · เลขเอกสาร") — เช่น เอกสารที่สร้างบน NextAcc เอง</summary>
            public string SentLabel;
            /// <summary>ป้ายเมื่อไม่มีคิวและไม่มีเอกสาร (ค่าเริ่มต้น "ยังไม่ส่ง")</summary>
            public string NotSentLabel;
            /// <summary>ไม่มีแถวคิวและไม่มีเอกสาร = ไม่ต้องลงบัญชี (เช่น คืนสินค้าที่ไม่มีคู่บัญชี)</summary>
            public bool NoQueueMeansNotApplicable;
            /// <summary>NextAcc ล่ม/พักส่ง (AccountingApiClient.IsServerDown) — ป้ายรอส่งบอกเหตุ</summary>
            public bool ServerDown;
            public DateTime? ServerDownUntil;
            /// <summary>รายละเอียดเพิ่มใน tooltip (เช่น สถานะจากตัวตรวจเทียบ)</summary>
            public string Note;
        }

        public sealed class NaBadgeLinks
        {
            public string OpenUrl;   // เปิดเอกสารบนเว็บ NextAcc (absolute http/https)
            public string PdfUrl;    // PDF (path ในแอป หรือ absolute)
            public string FixUrl;    // แก้ที่นี่ (path ในแอป)
            public string QueueUrl;  // ดูในคิว (path ในแอป)
        }

        public sealed class NaBadge
        {
            public NaState State;
            public string Label;
            public string Title;
            public string Icon;
            public bool ShowRetry;
            public string RetryText = "🔄 ส่งใหม่";
            public long QueueId;
            public string ErrorClass;
            public string DocNumber;
            /// <summary>รหัสสถานะสำหรับ CSS/JS: sent|pending|sending|waiting|data|config|cn|dup|failed|voided|notsent|na</summary>
            public string Code { get { return StateCode(State); } }
        }

        public static string StateCode(NaState s)
        {
            switch (s)
            {
                case NaState.Sent: return "sent";
                case NaState.Pending: return "pending";
                case NaState.Sending: return "sending";
                case NaState.Waiting: return "waiting";
                case NaState.NeedsData: return "data";
                case NaState.NeedsConfig: return "config";
                case NaState.NeedsCreditNote: return "cn";
                case NaState.MaybeDuplicate: return "dup";
                case NaState.Failed: return "failed";
                case NaState.Voided: return "voided";
                case NaState.NotSent: return "notsent";
                default: return "na";
            }
        }

        /// <summary>ป้ายไทยมาตรฐานของแต่ละสถานะ (ใช้เป็นหัวการ์ด/คำอธิบาย)</summary>
        public static string StateTh(NaState s)
        {
            switch (s)
            {
                case NaState.Sent: return "ส่งแล้ว";
                case NaState.Pending: return "รอส่ง";
                case NaState.Sending: return "กำลังส่ง";
                case NaState.Waiting: return "รอเอกสารอื่น";
                case NaState.NeedsData: return "ต้องแก้ข้อมูล";
                case NaState.NeedsConfig: return "ต้องตั้งค่า";
                case NaState.NeedsCreditNote: return "ต้องใบลดหนี้";
                case NaState.MaybeDuplicate: return "อาจซ้ำ";
                case NaState.Failed: return "ส่งไม่สำเร็จ";
                case NaState.Voided: return "ยกเลิกแล้ว";
                case NaState.NotSent: return "ยังไม่ส่ง";
                default: return "ไม่ส่งบัญชี";
            }
        }

        private static string StateIcon(NaState s)
        {
            switch (s)
            {
                case NaState.Sent: return "✓";
                case NaState.Pending: return "🕐";
                case NaState.Sending: return "⏳";
                case NaState.Waiting: return "🟦";
                case NaState.NeedsData: return "🟧";
                case NaState.NeedsConfig: return "🟪";
                case NaState.NeedsCreditNote: return "🟥";
                case NaState.MaybeDuplicate: return "⬛";
                case NaState.Failed: return "⚠";
                case NaState.Voided: return "✖";
                case NaState.NotSent: return "○";
                default: return "–";
            }
        }

        /// <summary>ตัดสินสถานะ — ลำดับ: ยกเลิกแล้ว → ไม่ส่งบัญชี → แถวคิวล่าสุด → เอกสารที่รู้ → ยังไม่ส่ง</summary>
        public static NaBadge ResolveBadge(NaBadgeInput input)
        {
            var i = input ?? new NaBadgeInput();
            var q = i.Queue;
            string knownNo = FirstNonEmpty(i.DocNumber, q != null && Up(q.Status) == "COMPLETED" ? q.DocNumber : null);
            bool hasDoc = i.KnownPosted || !string.IsNullOrWhiteSpace(i.DocId) || !string.IsNullOrWhiteSpace(i.DocNumber);
            var b = new NaBadge { QueueId = q != null ? q.Id : 0, ErrorClass = q != null ? Up(q.ErrorClass) : "", DocNumber = knownNo };

            if (i.Voided)
            {
                b.State = NaState.Voided;
                b.Label = !string.IsNullOrWhiteSpace(i.VoidedLabel) ? i.VoidedLabel.Trim()
                        : "ยกเลิกแล้ว" + (string.IsNullOrWhiteSpace(knownNo) ? "" : " · " + knownNo.Trim());
                b.Title = JoinTitle("เอกสารถูกยกเลิก (void) / กลับรายการบน NextAcc แล้ว", i.Note);
                return Finish(b);
            }
            if (i.NotApplicable)
            {
                b.State = NaState.NotApplicable;
                b.Label = string.IsNullOrWhiteSpace(i.NotApplicableLabel) ? "ไม่ส่งบัญชี" : i.NotApplicableLabel.Trim();
                b.Title = JoinTitle(i.NotApplicableReason, i.Note);
                return Finish(b);
            }

            if (q != null && !string.IsNullOrWhiteSpace(q.Status))
            {
                string st = Up(q.Status);
                string cls = Up(q.ErrorClass);
                string err = Trunc(q.ErrorMessage, 300);
                switch (st)
                {
                    case "COMPLETED":
                        b.State = NaState.Sent;
                        b.Label = !string.IsNullOrWhiteSpace(i.SentLabel) ? i.SentLabel.Trim()
                                : "ส่งแล้ว" + (string.IsNullOrWhiteSpace(knownNo) ? "" : " · " + knownNo.Trim());
                        b.Title = JoinTitle("ส่งเข้า NextAcc สำเร็จ", i.Note);
                        return Finish(b);
                    case "PROCESSING":
                        b.State = NaState.Sending;
                        b.Label = "กำลังส่ง";
                        b.Title = JoinTitle("ระบบกำลังส่งรายการนี้เข้า NextAcc", i.Note);
                        return Finish(b);
                    case "AWAITING_SLIP":
                        b.State = NaState.Waiting;
                        b.Label = "รอตรวจสลิป";
                        b.Title = JoinTitle("ระบบพักไว้จนกว่าสลิปจะผ่าน/อนุมัติที่หน้าตรวจสลิปโอน", i.Note);
                        return Finish(b);
                    case "PENDING":
                        if (cls == "WAIT")
                        {
                            b.State = NaState.Waiting;
                            b.Label = "รอเอกสารอื่น";
                            b.Title = JoinTitle(ClassHintTh("WAIT") + (err.Length > 0 ? " · " + err : ""), i.Note);
                            return Finish(b);
                        }
                        b.State = NaState.Pending;
                        b.Label = "รอส่ง";
                        b.Title = JoinTitle(i.ServerDown
                            ? "NextAcc ไม่พร้อมใช้งานตอนนี้ — ระบบพักการส่ง"
                              + (i.ServerDownUntil.HasValue ? " ถึง " + i.ServerDownUntil.Value.ToString("HH:mm", CultureInfo.InvariantCulture) : "")
                              + " แล้วส่งต่อเอง"
                            : "อยู่ในคิว — ระบบส่งเองในรอบถัดไป", i.Note);
                        return Finish(b);
                    case "FAILED":
                        b.Title = JoinTitle((cls.Length > 0 ? ClassHintTh(cls) + " · " : "") + err, i.Note);
                        switch (cls)
                        {
                            case "DATA":
                                b.State = NaState.NeedsData; b.Label = "ต้องแก้ข้อมูล";
                                b.ShowRetry = true; b.RetryText = "🔄 ส่งใหม่ (หลังแก้ใบ)";
                                break;
                            case "CONFIG": b.State = NaState.NeedsConfig; b.Label = "ต้องตั้งค่า"; break;
                            case "POLICY": b.State = NaState.NeedsCreditNote; b.Label = "ต้องใบลดหนี้"; break;
                            case "DUPLICATE": b.State = NaState.MaybeDuplicate; b.Label = "อาจซ้ำ — ตรวจบน NextAcc"; break;
                            case "WAIT":
                                b.State = NaState.Failed; b.Label = "รอนานเกินกำหนด"; b.ShowRetry = true;
                                break;
                            default:
                                b.State = NaState.Failed; b.Label = "ส่งไม่สำเร็จ"; b.ShowRetry = true;
                                break;
                        }
                        return Finish(b);
                    case "SUPERSEDED":
                    case "CANCELLED":
                    case "SKIPPED":
                        break;   // แถวเก่าที่ไม่มีผล — ตัดสินจากเอกสารที่รู้ (ด้านล่าง)
                    default:
                        b.State = NaState.Pending;
                        b.Label = "รอส่ง";
                        b.Title = JoinTitle(StatusTh(st), i.Note);
                        return Finish(b);
                }
            }

            if (hasDoc)
            {
                b.State = NaState.Sent;
                b.Label = !string.IsNullOrWhiteSpace(i.SentLabel) ? i.SentLabel.Trim()
                        : "ส่งแล้ว" + (string.IsNullOrWhiteSpace(i.DocNumber) ? "" : " · " + i.DocNumber.Trim());
                b.Title = JoinTitle("มีเอกสารบน NextAcc แล้ว", i.Note);
                return Finish(b);
            }
            if (i.NoQueueMeansNotApplicable)
            {
                b.State = NaState.NotApplicable;
                b.Label = string.IsNullOrWhiteSpace(i.NotApplicableLabel) ? "ไม่มีรายการบัญชี" : i.NotApplicableLabel.Trim();
                b.Title = JoinTitle(i.NotApplicableReason, i.Note);
                return Finish(b);
            }
            b.State = NaState.NotSent;
            b.Label = string.IsNullOrWhiteSpace(i.NotSentLabel) ? "ยังไม่ส่ง" : i.NotSentLabel.Trim();
            b.Title = JoinTitle(q != null && !string.IsNullOrWhiteSpace(q.Status) ? "รายการคิวเดิม: " + StatusTh(Up(q.Status)) : "ยังไม่เคยส่งเข้า NextAcc", i.Note);
            // แถวคิวเดิมถูกแทน/ยกเลิก (ไม่มีเอกสาร) — ไม่เสนอส่งใหม่จากป้าย (เหมือนป้ายเดิม) ให้ดูในคิวก่อน
            b.ShowRetry = q == null || string.IsNullOrWhiteSpace(q.Status);
            b.RetryText = "📤 ส่งเข้าบัญชี";
            return Finish(b);
        }

        private static NaBadge Finish(NaBadge b)
        {
            b.Icon = StateIcon(b.State);
            if (b.Title == null) b.Title = "";
            return b;
        }

        /// <summary>
        /// ลิงก์ของป้าย. webBase/companyId = ที่อยู่เว็บ NextAcc (ว่าง = ไม่มีลิงก์เปิดเอกสาร) · queueQuery = คำค้นหน้าคิว (เลขใบ/เลขคิว)
        /// · pdfUrl = ตัวดู PDF ที่มีอยู่แล้วของหน้า · resolveAppPath = ResolveUrl("~"+path) ของหน้า (null = ใช้ path เดิม)
        /// </summary>
        public static NaBadgeLinks BuildBadgeLinks(NaBadge b, NaBadgeInput input, string webBase, Guid companyId,
                                                   string queueQuery, string pdfUrl, Func<string, string> resolveAppPath)
        {
            var l = new NaBadgeLinks();
            if (b == null) return l;
            var i = input ?? new NaBadgeInput();
            Func<string, string> res = resolveAppPath ?? (p => p);

            // เปิดใน NextAcc: เอกสารที่รู้ (marker) ก่อน แล้วค่อยคำตอบของคิว
            string docId = FirstNonEmpty(i.DocId, i.Queue != null && Up(i.Queue.Status) == "COMPLETED" ? i.Queue.ResponseId : null);
            string docType = FirstNonEmpty(i.DocType, i.Queue != null ? i.Queue.DocType : null);
            if (b.State == NaState.Sent || b.State == NaState.Voided || (!string.IsNullOrWhiteSpace(i.DocId) && b.State != NaState.NotApplicable))
                l.OpenUrl = DocWebUrl(webBase, companyId, docId, docType);

            if (!string.IsNullOrWhiteSpace(pdfUrl) && (b.State == NaState.Sent || b.State == NaState.Voided || !string.IsNullOrWhiteSpace(i.DocId)))
                l.PdfUrl = IsSafeAppPath(pdfUrl) ? res(pdfUrl) : (IsHttpUrl(pdfUrl) ? pdfUrl : null);

            bool needsFix = b.State == NaState.NeedsData || b.State == NaState.NeedsConfig || b.State == NaState.NeedsCreditNote
                            || b.State == NaState.Failed;
            if (needsFix)
            {
                string fix = i.Queue != null ? (i.Queue.FixUrl ?? "").Trim() : "";
                if (!IsSafeAppPath(fix))
                {
                    if (fix.StartsWith("?focus=", StringComparison.Ordinal) && IsSafeAppPath(QueuePagePath + fix)) fix = QueuePagePath + fix;
                    else if (b.State == NaState.NeedsConfig) fix = QueuePagePath + "?focus=mapping";
                    else if (b.State == NaState.NeedsCreditNote) fix = "/Account/CreditNotes";
                    else fix = "";
                }
                if (fix.Length > 0 && IsSafeAppPath(fix)) l.FixUrl = res(fix);
            }

            if (i.Queue != null && i.Queue.Id > 0 && b.State != NaState.Sent && b.State != NaState.NotApplicable)
            {
                string q = string.IsNullOrWhiteSpace(queueQuery) ? i.Queue.Id.ToString(CultureInfo.InvariantCulture) : queueQuery.Trim();
                string cls = b.State == NaState.MaybeDuplicate ? "DUPLICATE" : null;
                string url = QueueUrl("", cls: cls, q: q);
                if (IsSafeAppPath(url.Split('#')[0])) l.QueueUrl = res(url);
            }
            return l;
        }

        /// <summary>HTML ของป้าย + ลิงก์ (encode ทุกข้อความ/URL) — compact = ไม่แสดงลิงก์ (ใช้ในตารางแคบ)</summary>
        public static string RenderBadge(NaBadge b, NaBadgeLinks links, bool compact = false)
        {
            if (b == null) return "";
            var sb = new StringBuilder();
            sb.Append("<span class=\"na-st na-st--").Append(b.Code).Append("\" data-na-state=\"").Append(b.Code).Append('"');
            if (!string.IsNullOrEmpty(b.Title)) sb.Append(" title=\"").Append(H(b.Title)).Append('"');
            sb.Append("><span class=\"na-st__i\" aria-hidden=\"true\">").Append(H(b.Icon)).Append("</span>")
              .Append("<span class=\"na-st__t\">").Append(H(b.Label)).Append("</span></span>");
            if (compact || links == null) return sb.ToString();
            return sb.Append(RenderBadgeLinks(links)).ToString();
        }

        /// <summary>เฉพาะลิงก์ของป้าย (เปิดใน NextAcc / PDF / แก้ที่นี่ / ดูในคิว) — ว่าง = ""</summary>
        public static string RenderBadgeLinks(NaBadgeLinks links)
        {
            if (links == null) return "";
            var sb = new StringBuilder();
            var parts = new List<string>();
            if (IsHttpUrl(links.OpenUrl))
                parts.Add("<a class=\"na-lk na-lk--open\" href=\"" + H(links.OpenUrl) + "\" target=\"_blank\" rel=\"noopener\">เปิดใน NextAcc</a>");
            if (LinkOk(links.PdfUrl))
                parts.Add("<a class=\"na-lk na-lk--pdf\" href=\"" + H(links.PdfUrl) + "\" target=\"_blank\" rel=\"noopener\">PDF</a>");
            if (LinkOk(links.FixUrl))
                parts.Add("<a class=\"na-lk na-lk--fix\" href=\"" + H(links.FixUrl) + "\" target=\"_blank\">🛠 แก้ที่นี่</a>");
            if (LinkOk(links.QueueUrl))
                parts.Add("<a class=\"na-lk na-lk--queue\" href=\"" + H(links.QueueUrl) + "\" target=\"_blank\">ดูในคิว</a>");
            if (parts.Count > 0) sb.Append("<span class=\"na-lks\">").Append(string.Join("", parts)).Append("</span>");
            return sb.ToString();
        }

        /// <summary>ป้ายแบบโครงสร้างสำหรับ JSON → Scripts/nextacc-ui.js (NaBadge.render) — ไม่ส่ง HTML ไปฝั่ง client.
        /// ลิงก์ที่รูปแบบไม่ปลอดภัยถูกตัดทิ้งที่นี่ (JS ตรวจซ้ำอีกชั้น)</summary>
        public static Dictionary<string, object> BadgeToDict(NaBadge b, NaBadgeLinks links)
        {
            if (b == null) return null;
            var l = links ?? new NaBadgeLinks();
            return new Dictionary<string, object>
            {
                { "code", b.Code }, { "label", b.Label ?? "" }, { "title", b.Title ?? "" }, { "icon", b.Icon ?? "" },
                { "showRetry", b.ShowRetry }, { "queueId", b.QueueId },
                { "links", new Dictionary<string, object>
                    {
                        { "open", IsHttpUrl(l.OpenUrl) ? l.OpenUrl : "" },
                        { "pdf", LinkOk(l.PdfUrl) ? l.PdfUrl : "" },
                        { "fix", LinkOk(l.FixUrl) && !IsHttpUrl(l.FixUrl) ? l.FixUrl : "" },
                        { "queue", LinkOk(l.QueueUrl) && !IsHttpUrl(l.QueueUrl) ? l.QueueUrl : "" }
                    } }
            };
        }

        /// <summary>ป้ายรวมของหลายรายการ (สถานะที่ต้องมีคนทำชนะ) — ว่าง = null</summary>
        public static NaBadge AggregateBadges(IList<NaBadge> badges)
        {
            if (badges == null || badges.Count == 0) return null;
            if (badges.Count == 1) return badges[0];
            NaState[] priority =
            {
                NaState.MaybeDuplicate, NaState.NeedsCreditNote, NaState.NeedsConfig, NaState.NeedsData, NaState.Failed,
                NaState.Sending, NaState.Waiting, NaState.Pending, NaState.NotSent, NaState.Voided, NaState.Sent, NaState.NotApplicable
            };
            int sent = badges.Count(x => x != null && x.State == NaState.Sent);
            int total = badges.Count(x => x != null);
            foreach (var st in priority)
            {
                var hit = badges.Where(x => x != null && x.State == st).ToList();
                if (hit.Count == 0) continue;
                var first = hit[0];
                var agg = new NaBadge
                {
                    State = st, Icon = first.Icon, QueueId = first.QueueId, ErrorClass = first.ErrorClass, DocNumber = first.DocNumber,
                    ShowRetry = first.ShowRetry, RetryText = first.RetryText
                };
                agg.Label = st == NaState.Sent ? "ส่งแล้ว " + sent + "/" + total + " รายการ"
                          : StateTh(st) + " " + hit.Count + "/" + total + " รายการ";
                agg.Title = (st == NaState.Sent ? "" : "ส่งแล้ว " + sent + "/" + total + " รายการ · ") + (first.Title ?? "");
                return agg;
            }
            return badges[0];
        }

        /// <summary>URL เอกสารบนเว็บ NextAcc (รูปแบบเดียวกับ AccountingSyncService.BuildNexaaccDocumentUrl) —
        /// ไม่มีที่อยู่/บริษัท/รหัสเอกสาร หรือที่อยู่ไม่ใช่ http(s) = null</summary>
        public static string DocWebUrl(string webBase, Guid companyId, string docId, string docType)
        {
            string b = (webBase ?? "").Trim().TrimEnd('/');
            string id = (docId ?? "").Trim();
            Guid g;
            if (b.Length == 0 || companyId == Guid.Empty || !Guid.TryParse(id, out g) || g == Guid.Empty || !IsHttpUrl(b)) return null;
            string path;
            switch (Up(docType))
            {
                case "INVOICE": path = "invoices"; break;
                case "EXPENSE": path = "expenses"; break;
                case "JOURNAL": path = "journals"; break;
                case "CREDIT_NOTE": path = "credit-notes"; break;
                case "DEBIT_NOTE": path = "debit-notes"; break;
                default: path = "documents"; break;
            }
            return b + "/" + companyId.ToString() + "/" + path + "/" + g.ToString();
        }

        /// <summary>ลิงก์ http(s) ที่ไม่มีอักขระทำลาย attribute</summary>
        public static bool IsHttpUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            Uri u;
            return Uri.TryCreate(url.Trim(), UriKind.Absolute, out u)
                   && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps)
                   && url.IndexOfAny(new[] { '<', '>', '"', '\'', ' ' }) < 0;
        }

        private static bool LinkOk(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            if (IsHttpUrl(url)) return true;
            // path ในแอปหลัง ResolveUrl ("/app/…") — ห้าม //host, javascript:
            return url.StartsWith("/", StringComparison.Ordinal) && !url.StartsWith("//", StringComparison.Ordinal)
                   && url.IndexOfAny(new[] { '<', '>', '"', '\'', ' ', '\\' }) < 0;
        }

        // ── วันยื่น ภ.พ.30 ────────────────────────────────────────────────────────────

        /// <summary>วันยื่น ภ.พ.30 ถัดไปของภาษีเดือนก่อน (filingDay 1-28): ยังไม่เลยวันยื่นเดือนนี้ = เดือนนี้ (ภาษีเดือนก่อน),
        /// เลยแล้ว = เดือนหน้า (ภาษีเดือนนี้). คืนจำนวนวันที่เหลือ (0 = วันนี้)</summary>
        public static int VatFilingCountdown(DateTime today, int filingDay, out DateTime filingDate, out DateTime vatMonth)
        {
            int d = Math.Max(1, Math.Min(28, filingDay));
            var t = today.Date;
            var thisMonth = new DateTime(t.Year, t.Month, 1);
            filingDate = thisMonth.AddDays(d - 1);
            vatMonth = thisMonth.AddMonths(-1);
            if (t > filingDate)
            {
                filingDate = thisMonth.AddMonths(1).AddDays(d - 1);
                vatMonth = thisMonth;
            }
            return (filingDate - t).Days;
        }

        // ── ผลตรวจเทียบ / ตรวจรายคืน (Accounting_Reconcile_Finding.Finding_Type) ─────────────

        /// <summary>คำไทยของชนิดผลตรวจ (ข้อ 19 ตัวตรวจเทียบ + ข้อ 21 ตรวจรายคืน LOC:) — ไม่รู้จัก = รหัสเดิม</summary>
        public static string FindingTypeTh(string type)
        {
            switch ((type ?? "").Trim().ToUpperInvariant().TrimEnd('?'))
            {
                case "DUPLICATE": return "เอกสารซ้ำบน NextAcc";
                case "VOID_STUCK": return "สั่งยกเลิกแล้วแต่ยังไม่ถูกยกเลิก";
                case "AMOUNT_MISMATCH": return "ยอดไม่ตรงกับใบเสร็จ";
                case "MISSING_ON_NEXTACC": return "ไม่มีเอกสารใช้งานบน NextAcc";
                case "DEPOSIT_MISMATCH": return "มัดจำบน NextAcc ไม่ตรง";
                case "ORPHAN_SUPERSEDED": return "เอกสารรุ่นเก่ายังไม่ถูกยกเลิก";
                case "ORPHAN_DELETED_LOCAL": return "ใบในระบบถูกลบแต่เอกสารยังอยู่";
                case "ORPHAN_OTHER_BOOKING": return "เอกสารอาจเป็นของการจองอื่น";
                case "UNKNOWN_NEXTACC_ONLY": return "เอกสารไม่ทราบที่มา";
                case "PREFLIGHT_BLOCK": return "ใบผิดกฎตัวตรวจก่อนส่ง";
                case "NOT_ENQUEUED": return "ใบไม่เคยเข้าคิว";
                case "ZERO_TOTAL": return "ใบยอดเป็นศูนย์";
                case "DEPOSIT_LEDGER": return "มัดจำการจอง ≠ ใบมัดจำ";
                case "BALANCE_VS_RECEIPTS": return "เช็คเอาท์แล้วยอดไม่ปิด";
                case "QUEUE_STUCK": return "คิวค้างนาน";
                case "PRE_FILING_GAP": return "ยังไม่ขึ้น NextAcc ก่อนยื่น ภ.พ.30";
                case "": return "-";
                default: return type;
            }
        }

        // ── ชนิดคีย์ (คำไทย) ──────────────────────────────────────────────────────────

        /// <summary>คำอธิบายชนิดคีย์แบบไม่มีรหัส int_/acc_</summary>
        public static string KeyTypeTh(bool hasKey, bool isIntegrationKey, bool hasCompanyKey)
        {
            if (!hasKey) return "ยังไม่ได้ใส่คีย์เชื่อมระบบ";
            if (!isIntegrationKey) return "ใช้คีย์บริษัทอย่างเดียว — ส่งเอกสารอัตโนมัติไม่ได้ (ต้องใช้คีย์เชื่อมระบบ)";
            return hasCompanyKey ? "คีย์เชื่อมระบบ + คีย์บริษัท (แนะนำ)" : "คีย์เชื่อมระบบ (ใช้ได้ทุกงาน)";
        }

        // ── helpers ─────────────────────────────────────────────────────────────────

        private static string Up(string s) { return (s ?? "").Trim().ToUpperInvariant(); }

        private static string FirstNonEmpty(params string[] xs)
        {
            foreach (var x in xs) if (!string.IsNullOrWhiteSpace(x)) return x.Trim();
            return null;
        }

        private static string JoinTitle(string a, string b)
        {
            a = (a ?? "").Trim(); b = (b ?? "").Trim();
            if (a.Length == 0) return b;
            if (b.Length == 0) return a;
            return a + " · " + b;
        }

        private static readonly Regex JsonValueRx = new Regex("\"([A-Za-z0-9_]+)\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.Compiled);

        /// <summary>ค่า string ของ key ใน payload JSON แบบ flat ("key":"value") — ไม่พบ = null</summary>
        public static string PayloadValue(string json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key)) return null;
            foreach (Match m in JsonValueRx.Matches(json))
                if (m.Groups[1].Value == key) return Regex.Unescape(m.Groups[2].Value);
            return null;
        }
    }
}
