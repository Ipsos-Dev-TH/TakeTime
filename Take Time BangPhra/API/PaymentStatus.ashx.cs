using System;
using System.Configuration;
using System.Text;
using System.Web;
using Take_Time_BangPhra.Payments;

namespace Take_Time_BangPhra.API
{
    /// <summary>
    /// จุดถามสถานะรายการชำระเงิน/วงเงินประกันแบบเบา ๆ — หน้าเก็บเงินหน้าร้านใช้ poll
    /// ทุกไม่กี่วินาทีเพื่อขึ้น "✅ เงินเข้าแล้ว" โดยไม่ต้องรีเฟรชทั้งหน้า
    ///
    /// เปิดเผยแค่ "สถานะ" ของเลขอ้างอิงที่ต้องรู้อยู่แล้วเท่านั้น (ไม่มียอด ไม่มีชื่อ)
    /// และเมื่อรายการยังค้าง จะถามเกตเวย์จริงให้เลย เผื่อ webhook มาไม่ถึง
    /// </summary>
    public class PaymentStatus : IHttpHandler
    {
        public void ProcessRequest(HttpContext context)
        {
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.Cache.SetCacheability(HttpCacheability.NoCache);

            string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;

            try
            {
                string txnRef = (context.Request.QueryString["ref"] ?? "").Trim();
                string holdRef = (context.Request.QueryString["hold"] ?? "").Trim();

                if (holdRef.Length > 0)
                {
                    var h = new SecurityHoldService(conn).GetByRef(holdRef);
                    if (h == null) { Write(context, 404, "NOT_FOUND", "ไม่พบรายการ"); return; }
                    Write(context, 200, h.Status, HoldStatus.Thai(h.Status));
                    return;
                }

                if (txnRef.Length == 0) { Write(context, 400, "BAD_REQUEST", "ไม่ระบุเลขอ้างอิง"); return; }

                var svc = new OnlinePaymentService(conn);
                var txn = svc.Store.GetByRef(txnRef);
                if (txn == null) { Write(context, 404, "NOT_FOUND", "ไม่พบรายการ"); return; }

                // ยังค้างอยู่ → ถามเกตเวย์จริงหนึ่งครั้ง (กันกรณี webhook หาย)
                if (txn.Status == Payments.PaymentStatus.Pending
                    || txn.Status == Payments.PaymentStatus.Initiated)
                {
                    try { svc.RefreshStatus(txn); } catch { }
                    txn = svc.Store.GetByRef(txnRef) ?? txn;
                }

                Write(context, 200, txn.Status, Payments.PaymentStatus.Thai(txn.Status));
            }
            catch
            {
                Write(context, 500, "ERROR", "ตรวจสอบไม่สำเร็จ");
            }
        }

        private static void Write(HttpContext ctx, int http, string status, string thai)
        {
            ctx.Response.StatusCode = http;
            ctx.Response.Write("{\"status\":\"" + Js(status) + "\",\"thai\":\"" + Js(thai) + "\"}");
        }

        private static string Js(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder();
            foreach (char c in s)
            {
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c < ' ') sb.Append(' ');
                else sb.Append(c);
            }
            return sb.ToString();
        }

        public bool IsReusable { get { return false; } }
    }
}
