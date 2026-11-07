using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Threading.Tasks;
using System.Web.UI.WebControls;
using Take_Time_BangPhra.Integration;

namespace Take_Time_BangPhra.Account
{
    /// <summary>
    /// CLAUDE.md ข้อ 25 ④ — บล็อก "e-Tax ใบลดหนี้ / ส่งอีเมล" ของหน้า ใบลดหนี้ (แยกไฟล์จากรายการหลัก ที่ทีมอื่นดูแล)
    ///   ส่ง e-Tax: NextAcc POST etax/generate (EtaxController.cs:61-68; CN → TypeCode 81 EtaxDocumentTypeMap.cs:32; CN ต้องอ้างใบต้นทางฝั่งขาย
    ///   EtaxInvoiceService.GenerateAsync) · ส่งกรมสรรพากร: etax/{id}/submit (:100-106) หรือ sign-and-submit (:210-220) · อีเมล: document/{id}/send-email
    ///   (DocumentController.cs:104-121). อัตโนมัติ: CreditNote_Etax_Auto=1 + ใบต้นทาง e-Tax ถึงกรมสรรพากรแล้ว (hook หลังคิว CREATE_CREDIT_NOTE)
    /// </summary>
    public partial class CreditNotes
    {
        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);
            try
            {
                if (Mode == "list" && pnlEnabled.Visible && pnlList.Visible && CreditNoteService.TableReady(conn)) BindCnEtax();
            }
            catch (Exception ex)
            {
                pnlCnEtax.Visible = true;
                litCnEtaxNote.Text = "<div class='cn-alert warn'>" + H("โหลดสถานะ e-Tax ใบลดหนี้ไม่ได้: " + ex.Message) + "</div>";
            }
        }

        private void BindCnEtax()
        {
            var svc = new AccountingSyncService(conn);
            bool cols = svc.CreditNoteEtaxColumnsReady;
            DateTime from, to;
            if (!DateTime.TryParse(txtFrom.Text, CultureInfo.InvariantCulture, DateTimeStyles.None, out from)) from = DateTime.Today.AddMonths(-1);
            if (!DateTime.TryParse(txtTo.Text, CultureInfo.InvariantCulture, DateTimeStyles.None, out to)) to = DateTime.Today;
            from = AccountingSyncService.NormalizeEra(from);
            to = AccountingSyncService.NormalizeEra(to);
            var dt = new code().DatabaseQuerySafe(conn,
                @"SELECT TOP 100 ID, CN_No, CN_Date, Source_Receipt_ID, Status, Nexaacc_Path, Nexaacc_Doc_No,
                         CAST(Nexaacc_Doc_Id AS NVARCHAR(40)) AS Nexaacc_Doc_Id, " +
                (cols ? "Etax_Status, Etax_Ref, Etax_Error" : "CAST(NULL AS NVARCHAR(20)) AS Etax_Status, CAST(NULL AS NVARCHAR(80)) AS Etax_Ref, CAST(NULL AS NVARCHAR(1000)) AS Etax_Error") + @"
                    FROM Credit_Note
                   WHERE Status = 'SYNCED' AND Nexaacc_Doc_Id IS NOT NULL AND ISNULL(Nexaacc_Path, '') NOT IN ('JOURNAL', 'LOCAL')
                     AND CN_Date >= @f AND CN_Date <= @t
                   ORDER BY CN_Date DESC, ID DESC",
                new Dictionary<string, object> { { "@f", from.Date }, { "@t", to.Date } });
            if (dt == null || dt.Rows.Count == 0) { pnlCnEtax.Visible = false; return; }
            dt.Columns.Add("SrcEtax", typeof(string));
            foreach (DataRow r in dt.Rows)
            {
                string src = Convert.ToString(r["Source_Receipt_ID"]);
                string et = string.IsNullOrEmpty(src) ? null : CreditNoteGuards.EtaxReachedRd(conn, src);
                r["SrcEtax"] = et == null ? "ใบต้นทางไม่ได้ส่ง e-Tax" : "ใบต้นทาง e-Tax: " + et;
            }
            pnlCnEtax.Visible = true;
            string auto = new AccountingConfig(conn).GetConfigValue(AccountingSyncService.CfgCreditNoteEtaxAuto, "0");
            litCnEtaxNote.Text = "<div class='cn-note' style='margin-bottom:8px'>ใบลดหนี้ที่ลดยอดใบกำกับซึ่งส่ง e-Tax ไปแล้ว ควรส่ง e-Tax ใบลดหนี้ (รหัส 81) คู่กัน · "
                + (auto == "1" ? "ส่งอัตโนมัติเปิดอยู่ (เมื่อใบต้นทางส่ง e-Tax ถึงกรมสรรพากรแล้ว)" : "ส่งอัตโนมัติปิดอยู่ (<code>CreditNote_Etax_Auto</code> = 0) — กดส่งเองที่ปุ่ม")
                + (cols ? "" : " · ยังไม่รัน PHASE19_41 — สถานะแสดงหลังกด ↻")
                + "</div>";
            rptCnEtax.DataSource = dt;
            rptCnEtax.DataBind();
        }

        protected bool CnEtaxCanGenerate(object dataItem)
        {
            var rv = dataItem as DataRowView;
            if (rv == null) return false;
            return CreditNoteEtaxPolicy.CanGenerate(Convert.ToString(rv["Status"]), Convert.ToString(rv["Nexaacc_Path"]),
                !string.IsNullOrEmpty(Convert.ToString(rv["Nexaacc_Doc_Id"])), Convert.ToString(rv["Etax_Status"])) == null;
        }

        protected string CnEtaxStatusHtml(object status, object refNo, object error)
        {
            string s = status == null || status == DBNull.Value ? "" : status.ToString();
            string label = CreditNoteEtaxPolicy.StatusLabel(s);
            string css = CreditNoteEtaxPolicy.IsRetryable(s) ? "ERR" : s.Length == 0 ? "DRAFT" : "SYNCED";
            string html = "<span class='cn-chip " + css + "'>" + H(label) + "</span>";
            string rf = refNo == null || refNo == DBNull.Value ? "" : refNo.ToString();
            if (rf.Length > 0) html += "<br/><span class='cn-note'>" + H(rf) + "</span>";
            string err = error == null || error == DBNull.Value ? "" : error.ToString();
            if (err.Length > 0) html += "<br/><span class='cn-note' title='" + H(err) + "'>⚠ " + H(err.Length > 80 ? err.Substring(0, 80) + "…" : err) + "</span>";
            return html;
        }

        protected void rptCnEtax_ItemCommand(object source, RepeaterCommandEventArgs e)
        {
            int id;
            if (!int.TryParse(Convert.ToString(e.CommandArgument), out id) || id <= 0) return;
            if (!CanApprove && e.CommandName != "EtaxRefresh") { Flash("err", "ไม่มีสิทธิ์ (Owner / Admin ที่มีสิทธิ์ตั้งค่าบัญชี)"); Response.Redirect(Request.RawUrl, false); return; }
            var svc = new AccountingSyncService(conn);
            NxActionResult r;
            try
            {
                switch (e.CommandName)
                {
                    case "EtaxGen":
                        r = Task.Run(() => svc.GenerateCreditNoteEtaxAsync(id, false)).GetAwaiter().GetResult();
                        break;
                    case "EtaxSubmit":
                        r = Task.Run(() => svc.SubmitCreditNoteEtaxAsync(id)).GetAwaiter().GetResult();
                        break;
                    case "EtaxRefresh":
                        r = Task.Run(() => svc.RefreshCreditNoteEtaxAsync(id)).GetAwaiter().GetResult();
                        break;
                    case "DocEmail":
                    {
                        var row = new code().DatabaseQuerySafe(conn,
                            "SELECT TOP 1 CN_No, Source_Receipt_ID, CAST(Nexaacc_Doc_Id AS NVARCHAR(40)) AS D FROM Credit_Note WHERE ID = @id AND Status = 'SYNCED'",
                            new Dictionary<string, object> { { "@id", id } });
                        Guid docId;
                        if (row == null || row.Rows.Count == 0 || !Guid.TryParse(Convert.ToString(row.Rows[0]["D"]), out docId))
                        { r = NxActionResult.Fail("BLOCKED", "ใบลดหนี้นี้ยังไม่มีเอกสารบน NextAcc"); break; }
                        string to = hfCnEtaxEmail.Value;
                        string src = Convert.ToString(row.Rows[0]["Source_Receipt_ID"]);
                        r = Task.Run(() => svc.SendNextAccDocumentEmailAsync(docId, to, null, null, src, id)).GetAwaiter().GetResult();
                        if (r.Message != null) r.Message = Convert.ToString(row.Rows[0]["CN_No"]) + ": " + r.Message;
                        break;
                    }
                    default:
                        return;
                }
            }
            catch (Exception ex) { r = NxActionResult.Fail("FAILED", ex.Message); }
            Flash(r.Ok ? "ok" : r.Status == "BLOCKED" ? "warn" : "err", r.Message ?? r.Status);
            Response.Redirect(Request.RawUrl, false);
            Context.ApplicationInstance.CompleteRequest();
        }
    }
}
