using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web.UI;
using Take_Time_BangPhra.Integration;

namespace Take_Time_BangPhra.Account
{
    /// <summary>
    /// ศูนย์บัญชี NextAcc (CLAUDE.md ข้อ 24) — หน้าแรกของงานบัญชี: การเชื่อมต่อ, คิวตามชนิดปัญหา, งานที่ต้องมีคนทำ,
    /// ก่อนยื่น ภ.พ.30, ตรวจเทียบ, งานจากส่วนอื่น (สลิป/ใบลดหนี้/รอใบลดหนี้ผู้ขาย/OCR), ปุ่มลัด.
    /// สิทธิ์: Owner / Admin ที่มี SYS_ACCOUNTING หรือ FIN_REPORT (ReceiptPreflight.CanOverrideCurrentUser — กติกาเดียวกับการยืนยันข้ามตัวตรวจ).
    /// ปุ่มลัดเรียก action เดิมของ Admin/Settings/AccountingIntegration (ต้องมี SYS_ACCOUNTING) — ไม่มีตรรกะซ้ำที่นี่
    /// </summary>
    public partial class NextAccHub : Page
    {
        private readonly string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        protected string IntegrationPath = "/Admin/Settings/AccountingIntegration";

        protected static string H(object o) => NextAccUi.H(o == null || o == DBNull.Value ? "" : Convert.ToString(o, Inv));

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Session["permission"]?.ToString() != "True") { Response.Redirect("~/Admin/Login", false); Context.ApplicationInstance.CompleteRequest(); return; }
            if (!ReceiptPreflight.CanOverrideCurrentUser())
            {
                Response.Redirect("~/ReserveTable?denied=" + Perm.FinReport, false);
                Context.ApplicationInstance.CompleteRequest();
                return;
            }
            Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);
            IntegrationPath = ResolveUrl("~" + NextAccUi.QueuePagePath);

            bool isOwner = string.Equals(Session["User"]?.ToString(), "Owner", StringComparison.OrdinalIgnoreCase);
            pnlActions.Visible = isOwner || Perm.CanAccess(Perm.SysAccounting);

            NextAccHubData d;
            try { d = NextAccHubData.Load(conn, DateTime.Today); }
            catch (Exception ex)
            {
                litWarn.Text = "<div class='nh-warn'>อ่านข้อมูลไม่สำเร็จ: " + H(ex.Message) + "</div>";
                return;
            }
            litUpdated.Text = H(DateTime.Now.ToString("HH:mm", Inv));
            litConn.Text = RenderConnection(d);
            litQueue.Text = RenderQueue(d);
            litWork.Text = RenderWork(d);
            litFiling.Text = RenderFiling(d);
            litFindings.Text = RenderFindings(d);
            litCross.Text = RenderCross(d);
            litLinks.Text = RenderLinks();
            if (d.Errors.Count > 0)
                litWarn.Text = "<div class='nh-warn'>บางส่วนอ่านไม่ได้ (ตารางยังไม่ได้อัปเดตหรือฐานข้อมูลช้า): " + H(string.Join(" · ", d.Errors.Take(4))) + "</div>";
        }

        // ── helpers ─────────────────────────────────────────────────────────────────

        private string U(string appPath) => ResolveUrl("~" + appPath);
        private string QueueLink(string cls = null, string status = null, string q = null) => U(NextAccUi.QueueUrl("", cls: cls, status: status, q: q));

        private static string Ago(DateTime? t)
        {
            if (!t.HasValue) return "ไม่เคย";
            var span = DateTime.Now - t.Value;
            string when = t.Value.ToString("dd/MM/yyyy HH:mm", Inv);
            if (span.TotalMinutes < 1) return "เมื่อสักครู่ (" + when + ")";
            if (span.TotalMinutes < 60) return (int)span.TotalMinutes + " นาทีที่แล้ว";
            if (span.TotalHours < 24) return (int)span.TotalHours + " ชม. ที่แล้ว";
            return when;
        }

        private static string M(decimal v) => v.ToString("#,##0.00", Inv);

        private string Tile(string css, int n, string title, string hint, string href)
        {
            var sb = new StringBuilder();
            bool link = !string.IsNullOrEmpty(href);
            sb.Append(link ? "<a class='nh-tile " : "<div class='nh-tile ").Append(H(css)).Append(n == 0 ? " zero" : "").Append('\'');
            if (link) sb.Append(" href='").Append(H(href)).Append("' target='_self'");
            sb.Append("><div class='n'>").Append(n.ToString("#,##0", Inv)).Append("</div><div class='t'>").Append(H(title)).Append("</div>");
            if (!string.IsNullOrEmpty(hint)) sb.Append("<div class='h'>").Append(H(hint)).Append("</div>");
            sb.Append(link ? "</a>" : "</div>");
            return sb.ToString();
        }

        private static string PreflightTh(string mode)
        {
            switch ((mode ?? "").ToUpperInvariant())
            {
                case "WARN": return "เตือนอย่างเดียว";
                case "BLOCK": return "หยุดใบที่ผิดก่อนบันทึก";
                default: return "ปิด";
            }
        }

        // ── 1) การเชื่อมต่อ ──────────────────────────────────────────────────────────

        private string RenderConnection(NextAccHubData d)
        {
            string dot, title, sub;
            if (!d.Configured) { dot = "off"; title = "ยังไม่ได้ตั้งค่าการเชื่อม NextAcc"; sub = "ใส่ที่อยู่ระบบ คีย์ และรหัสบริษัท ที่หน้าตั้งค่า NextAcc"; }
            else if (!d.Enabled) { dot = "off"; title = "ปิดการส่งเข้า NextAcc อยู่"; sub = "รายการใหม่ยังเข้าคิวได้ แต่จะไม่ถูกส่งจนกว่าจะเปิด"; }
            else if (d.ServerDown)
            {
                dot = "err"; title = "NextAcc ไม่พร้อมใช้งาน — ระบบพักการส่ง";
                sub = "ส่งต่อเองหลัง " + (d.ServerDownUntil.HasValue ? d.ServerDownUntil.Value.ToString("HH:mm", Inv) : "-") + " · ไม่ต้องกดส่งซ้ำ";
            }
            else if (d.StalePending > 0 || d.FailedTotal > 0) { dot = "warn"; title = "เชื่อมต่อได้ — มีรายการที่ต้องดูแล"; sub = "ดูตัวเลขด้านล่าง"; }
            else { dot = "ok"; title = "ปกติ"; sub = "ส่งเข้า NextAcc ได้ตามปกติ"; }

            var sb = new StringBuilder();
            sb.Append("<h3 id='nhConnT'>🔌 การเชื่อมต่อ</h3>");
            sb.Append("<div class='nh-conn'><span class='nh-dot ").Append(dot).Append("' aria-hidden='true'></span><div style='min-width:0'>")
              .Append("<div class='nh-conn-title'>").Append(H(title)).Append("</div><div class='nh-sub'>").Append(H(sub)).Append("</div>");
            if (d.ServerDown && !string.IsNullOrEmpty(d.ServerDownError))
                sb.Append("<div class='nh-err'>").Append(H(d.ServerDownError.Length > 200 ? d.ServerDownError.Substring(0, 200) + "…" : d.ServerDownError)).Append("</div>");
            sb.Append("</div></div><div class='nh-facts'>");
            sb.Append("<div class='nh-fact'><b>ส่งสำเร็จล่าสุด</b>").Append(H(Ago(d.LastSuccess))).Append("</div>");
            sb.Append("<div class='nh-fact'><b>คีย์ที่ใช้</b>").Append(H(NextAccUi.KeyTypeTh(d.HasKey, d.IsIntegrationKey, d.HasCompanyKey))).Append("</div>");
            sb.Append("<div class='nh-fact'><b>ผังบัญชีจาก NextAcc</b>")
              .Append(d.ChartAccounts > 0 ? H(d.ChartAccounts.ToString("#,##0", Inv) + " บัญชี · ดึงล่าสุด " + Ago(d.ChartSyncedAt)) : "ยังไม่ได้ดึง").Append("</div>");
            sb.Append("<div class='nh-fact'><b>ตัวตรวจก่อนส่ง</b>").Append(H(PreflightTh(d.PreflightMode))).Append("</div>");
            sb.Append("<div class='nh-fact'><b>วันนี้</b>").Append(H("เข้าคิว " + d.TodayTotal + " · สำเร็จ " + d.TodayCompleted + " · ล้ม " + d.TodayFailed)).Append("</div>");
            sb.Append("</div>");
            return sb.ToString();
        }

        // ── 2) คิวตามชนิดปัญหา ─────────────────────────────────────────────────────────

        private string RenderQueue(NextAccHubData d)
        {
            var sb = new StringBuilder("<h3 id='nhQueueT'>📥 คิวส่งบัญชี <small>กดตัวเลขเพื่อเปิดรายการในคิว</small></h3>");
            if (!d.QueueAvailable) return sb.Append("<div class='nh-sub'>ยังไม่มีตารางคิว (ยังไม่ได้ตั้งค่าการเชื่อม)</div>").ToString();
            int otherFailed = d.FailedOf("") + d.FailedOf("TRANSIENT") + d.FailedOf("UNKNOWN") + d.FailedOf("WAIT");
            sb.Append("<div class='nh-tiles'>");
            sb.Append(Tile("data", d.FailedOf("DATA"), NextAccUi.StateTh(NextAccUi.NaState.NeedsData), "แก้ใบแล้วระบบส่งใหม่เอง", QueueLink(cls: "DATA")));
            sb.Append(Tile("config", d.FailedOf("CONFIG"), NextAccUi.StateTh(NextAccUi.NaState.NeedsConfig), "ผูกบัญชี/แหล่งเงินให้ครบ", QueueLink(cls: "CONFIG")));
            sb.Append(Tile("cn", d.FailedOf("POLICY"), NextAccUi.StateTh(NextAccUi.NaState.NeedsCreditNote), "แก้ตรงไม่ได้แล้ว", QueueLink(cls: "POLICY")));
            sb.Append(Tile("dup", d.FailedOf("DUPLICATE"), NextAccUi.StateTh(NextAccUi.NaState.MaybeDuplicate), "ตรวจบน NextAcc ก่อนส่งซ้ำ", QueueLink(cls: "DUPLICATE")));
            sb.Append(Tile("failed", otherFailed, NextAccUi.StateTh(NextAccUi.NaState.Failed), "ชั่วคราว/ไม่ทราบสาเหตุ", QueueLink(status: "FAILED")));
            sb.Append(Tile("wait", d.Waiting, NextAccUi.StateTh(NextAccUi.NaState.Waiting), "ระบบส่งต่อเองเมื่อพร้อม", QueueLink(cls: "WAIT")));
            sb.Append(Tile("wait", d.AwaitingSlip, "รอตรวจสลิป", "อนุมัติสลิปแล้วส่งต่อเอง", U("/Account/SlipVerification")));
            sb.Append(Tile(d.StalePending > 0 ? "failed" : "wait", d.Pending + d.Processing, NextAccUi.StateTh(NextAccUi.NaState.Pending),
                d.StalePending > 0 ? "ค้างเกิน 6 ชม. " + d.StalePending + " รายการ" : "ระบบส่งเองในรอบถัดไป", QueueLink(status: "PENDING")));
            sb.Append("</div>");
            if (!d.QueueClassReady)
                sb.Append("<div class='nh-sub' style='margin-top:6px;'>ยังไม่ได้อัปเดตฐานข้อมูลส่วนแยกชนิดปัญหา — รายการที่ล้มเหลวทั้งหมดนับใน \"ส่งไม่สำเร็จ\"</div>");
            return sb.ToString();
        }

        // ── 3) งานที่ต้องมีคนทำ ────────────────────────────────────────────────────────

        private string RenderWork(NextAccHubData d)
        {
            var sb = new StringBuilder("<h3 id='nhWorkT'>🧑‍💼 งานที่ต้องมีคนทำ <small>ส่งใหม่ไม่ช่วย — ต้องแก้ข้อมูล ตั้งค่า ออกใบลดหนี้ หรือตรวจบน NextAcc</small></h3>");
            if (d.Work.Count == 0) return sb.Append("<div class='nh-empty'>✅ ไม่มีงานค้างที่ต้องมีคนทำ</div>").ToString();
            sb.Append("<ul class='nh-list'>");
            foreach (var w in d.Work.Take(15))
            {
                var input = new NextAccUi.NaBadgeInput
                {
                    Queue = new NextAccUi.NaQueueRow { Id = w.QueueId, Status = "FAILED", ErrorClass = w.ErrorClass, ErrorMessage = w.Error, FixUrl = w.FixUrl }
                };
                var lc = NextAccStatusData.GetLinkContext(conn);
                var badge = NextAccUi.ResolveBadge(input);
                var links = NextAccUi.BuildBadgeLinks(badge, input, lc.WebBase, lc.CompanyId,
                    string.IsNullOrEmpty(w.Key) ? w.QueueId.ToString(Inv) : w.Key, null, U);
                sb.Append("<li><div class='nh-row1'><span class='nh-key'>").Append(H(string.IsNullOrEmpty(w.Key) ? "#" + w.QueueId : w.Key)).Append("</span>")
                  .Append(NextAccUi.RenderBadge(badge, null, compact: true))
                  .Append("<span class='nh-sub'>").Append(H(w.ActionTh)).Append(w.Created.HasValue ? " · " + H(w.Created.Value.ToString("dd/MM HH:mm", Inv)) : "").Append("</span></div>");
                string hint = NextAccUi.ClassHintTh(w.ErrorClass);
                string err = w.Error ?? "";
                if (err.Length > 220) err = err.Substring(0, 220) + "…";
                sb.Append("<div class='nh-err'>").Append(H(hint.Length > 0 ? hint + " — " + err : err)).Append("</div>")
                  .Append("<div>").Append(NextAccUi.RenderBadgeLinks(links)).Append("</div></li>");
            }
            sb.Append("</ul>");
            if (d.Work.Count > 15) sb.Append("<div class='nh-sub'>แสดง 15 รายการล่าสุด — <a href='").Append(H(QueueLink(status: "FAILED"))).Append("'>ดูทั้งหมดในคิว</a></div>");
            return sb.ToString();
        }

        // ── 4) ก่อนยื่น ภ.พ.30 ────────────────────────────────────────────────────────

        private string RenderFiling(NextAccHubData d)
        {
            var sb = new StringBuilder("<h3 id='nhVatT'>🧾 ก่อนยื่น ภ.พ.30</h3>");
            bool hot = d.DaysToFiling <= 5 && (d.PreFilingGap > 0 || d.VatMonthUnsentQueue > 0);
            sb.Append("<div class='nh-big").Append(hot ? " hot" : "").Append("'>ภาษีเดือน ").Append(H(d.VatMonth.ToString("MM/yyyy", Inv)))
              .Append(" · ยื่นภายใน ").Append(H(d.FilingDate.ToString("dd/MM/yyyy", Inv)))
              .Append(d.DaysToFiling == 0 ? " (วันนี้)" : " (อีก " + d.DaysToFiling + " วัน)").Append("</div>");
            sb.Append("<div class='nh-sub'>ตั้งวันยื่นไว้วันที่ ").Append(d.FilingDay).Append(" ของเดือน — ใบของเดือนภาษีต้องขึ้น NextAcc ครบก่อนยื่น</div>");
            sb.Append("<div class='nh-chips'>")
              .Append("<span class='nh-chip").Append(d.PreFilingGap > 0 ? " hot" : "").Append("'>ใบเดือนก่อนยังไม่ขึ้น ").Append(d.PreFilingGap)
              .Append(d.PreFilingGapAmount > 0 ? " ใบ · " + H(M(d.PreFilingGapAmount)) + " บาท" : " ใบ").Append("</span>")
              .Append("<a class='nh-chip").Append(d.VatMonthUnsentQueue > 0 ? " hot" : "").Append("' href='").Append(H(QueueLink(q: null, status: "FAILED")))
              .Append("'>คิวใบเสร็จของเดือนภาษีที่ยังไม่สำเร็จ ").Append(d.VatMonthUnsentQueue).Append("</a></div>");
            if (d.PreFilingItems.Count > 0)
            {
                sb.Append("<ul class='nh-list'>");
                foreach (var f in d.PreFilingItems)
                {
                    string key = string.IsNullOrEmpty(f.Receipt) ? (f.ReservationId > 0 ? f.ReservationId.ToString(Inv) : "") : f.Receipt;
                    sb.Append("<li><div class='nh-row1'><span class='nh-key'>").Append(H(string.IsNullOrEmpty(f.Receipt) ? "-" : f.Receipt)).Append("</span>")
                      .Append(f.Amount.HasValue ? "<span>" + H(M(f.Amount.Value)) + "</span>" : "")
                      .Append(f.ReservationId > 0 ? "<span class='nh-sub'>การจอง #" + f.ReservationId + "</span>" : "");
                    if (key.Length > 0) sb.Append("<a class='na-lk' href='").Append(H(QueueLink(q: key))).Append("'>ดูในคิว</a>");
                    sb.Append("</div></li>");
                }
                sb.Append("</ul>");
            }
            else if (d.PreFilingGap == 0)
                sb.Append("<div class='nh-empty'>✅ ตรวจรายคืนล่าสุดไม่พบใบเดือนก่อนที่ค้าง</div>");
            sb.Append("<div class='nh-sub'>รายการนี้มาจากการตรวจรายคืน — กด \"ตรวจความครบถ้วนตอนนี้\" ด้านล่างเพื่ออัปเดตทันที</div>");
            return sb.ToString();
        }

        // ── 5) ตรวจเทียบ ──────────────────────────────────────────────────────────────

        private string RenderFindings(NextAccHubData d)
        {
            var sb = new StringBuilder("<h3 id='nhRecT'>🔍 ตรวจเทียบ <small>ระบบ ↔ NextAcc และตรวจรายคืน</small></h3>");
            if (!d.FindingsAvailable) return sb.Append("<div class='nh-sub'>ยังไม่ได้อัปเดตฐานข้อมูลส่วนตัวตรวจเทียบ</div>").ToString();
            if (d.FindingsTotal == 0) return sb.Append("<div class='nh-empty'>✅ ไม่มีผลตรวจที่ยังเปิดอยู่</div>").ToString();
            sb.Append("<div class='nh-count'>").Append(d.FindingsTotal).Append("</div><div class='nh-sub'>เรื่องที่ยังไม่ปิด</div><div class='nh-chips'>");
            foreach (var kv in d.FindingsByType.OrderByDescending(x => x.Value))
                sb.Append("<span class='nh-chip'>").Append(H(NextAccUi.FindingTypeTh(kv.Key))).Append(" ").Append(kv.Value).Append("</span>");
            sb.Append("</div><ul class='nh-list'>");
            foreach (var f in d.Findings)
            {
                string key = !string.IsNullOrEmpty(f.Receipt) ? f.Receipt : (f.ReservationId > 0 ? f.ReservationId.ToString(Inv) : "");
                string det = f.Detail ?? "";
                if (det.Length > 180) det = det.Substring(0, 180) + "…";
                sb.Append("<li><div class='nh-row1'><b>").Append(H(f.TypeTh)).Append("</b>")
                  .Append(f.ReservationId > 0 ? "<span class='nh-sub'>การจอง #" + f.ReservationId + "</span>" : "")
                  .Append(!string.IsNullOrEmpty(f.Receipt) ? "<span class='nh-key'>" + H(f.Receipt) + "</span>" : "")
                  .Append(!string.IsNullOrEmpty(f.DocNumber) ? "<span class='nh-sub'>NextAcc " + H(f.DocNumber) + "</span>" : "")
                  .Append(f.Amount.HasValue ? "<span>" + H(M(f.Amount.Value)) + "</span>" : "");
                if (key.Length > 0) sb.Append("<a class='na-lk' href='").Append(H(QueueLink(q: key))).Append("'>ดูในคิว</a>");
                sb.Append("</div>");
                if (det.Length > 0) sb.Append("<div class='nh-err'>").Append(H(det)).Append("</div>");
                sb.Append("</li>");
            }
            sb.Append("</ul><div class='nh-sub'>แก้รายการระดับการจอง: ตารางจอง → ปุ่ม \"💼 บัญชี NextAcc\" ของการจองนั้น</div>");
            return sb.ToString();
        }

        // ── 6) งานจากส่วนอื่น ─────────────────────────────────────────────────────────

        private string RenderCross(NextAccHubData d)
        {
            var sb = new StringBuilder("<h3 id='nhCrossT'>🗂️ งานจากส่วนอื่นที่กระทบบัญชี</h3><div class='nh-tiles'>");
            int n = 0;
            if (d.SlipPending.HasValue)
            {
                n++;
                sb.Append(Tile(d.SlipFlagged.GetValueOrDefault() > 0 ? "failed" : "wait", d.SlipPending.Value, "สลิปโอนรอตรวจ",
                    d.SlipFlagged.GetValueOrDefault() > 0 ? "ยอดไม่ตรง/ซ้ำ/ผู้รับผิด " + d.SlipFlagged.Value + " ใบ" : "ใบเสร็จออกเมื่ออนุมัติ", U("/Account/SlipVerification")));
            }
            if (d.CnDraft.HasValue)
            {
                n += 3;
                sb.Append(Tile("data", d.CnDraft.Value, "ใบลดหนี้รออนุมัติ", "คืนเงิน/ส่ง NextAcc เมื่ออนุมัติ", U("/Account/CreditNotes")));
                sb.Append(Tile("failed", d.CnSyncError.GetValueOrDefault(), "ใบลดหนี้ส่งไม่สำเร็จ", "ดูป้ายในรายการใบลดหนี้", U("/Account/CreditNotes")));
                sb.Append(Tile("wait", d.CnRefundPending.GetValueOrDefault(), "คืนเงินรอบันทึก", "ใบลดหนี้ที่ยังไม่คืนเงิน", U("/Account/CreditNotes")));
            }
            if (d.VendorCnPending.HasValue)
            {
                n++;
                sb.Append(Tile(d.VendorCnOldestDays.GetValueOrDefault() > 7 ? "failed" : "wait", d.VendorCnPending.Value, "รอใบลดหนี้ผู้ขาย",
                    d.VendorCnPending.Value > 0 && d.VendorCnOldestDays.HasValue ? "เก่าสุด " + d.VendorCnOldestDays.Value + " วัน" : "คืนสินค้าที่รอใบจากผู้ขาย", U("/Product/PurchaseReturn")));
            }
            if (d.OcrOpen.HasValue)
            {
                n++;
                sb.Append(Tile("wait", d.OcrOpen.Value, "เอกสารสแกนค้าง", "ร่างที่ยังไม่ส่งเกิน 12 ชม.", U("/Voucher/OcrUpload")));
            }
            sb.Append("</div>");
            if (n == 0) return "<h3 id='nhCrossT'>🗂️ งานจากส่วนอื่นที่กระทบบัญชี</h3><div class='nh-sub'>ยังไม่ได้เปิดใช้ส่วนที่เกี่ยวข้อง</div>";
            return sb.ToString();
        }

        private string RenderLinks()
        {
            var links = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("คิวทั้งหมด", QueueLink()),
                new KeyValuePair<string, string>("ตั้งค่า NextAcc", U(NextAccUi.QueuePagePath)),
                new KeyValuePair<string, string>("ตรวจสอบเอกสารขาย", U("/Account/CheckDocument_New")),
                new KeyValuePair<string, string>("ตรวจสอบใบสำคัญจ่าย", U("/Account/CheckPayment_New")),
                new KeyValuePair<string, string>("สรุปยอดขาย/ภาษีรายวัน", U("/Report/DailySalesTaxSummary")),
                new KeyValuePair<string, string>("กระทบยอดกับ NextAcc", U("/Report/NextAccReconciliation")),
                new KeyValuePair<string, string>("รายงานสินค้าคงเหลือ", U("/Report/StockMovementReport"))
            };
            return "<div class='nh-links'>" + string.Join("", links.Select(l => "<a href='" + H(l.Value) + "'>" + H(l.Key) + "</a>")) + "</div>";
        }
    }
}
