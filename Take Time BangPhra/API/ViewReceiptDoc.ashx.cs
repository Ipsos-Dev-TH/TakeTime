using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Text.RegularExpressions;
using System.Web;
using Take_Time_BangPhra.Helpers;

namespace Take_Time_BangPhra.API
{
    /// <summary>
    /// เปิดดูเอกสารใบเสร็จ/ใบกำกับของใบเสร็จเลขที่ระบุ — ยึด "เอกสารทางการจาก NextAcc" ก่อนเสมอ
    /// (DOCUMENT mode: มัดจำ = ใบเสร็จรับเงิน / รับชำระ = ใบกำกับภาษี ที่ออกบน NextAcc)
    /// แล้วค่อย fallback ไฟล์ PDF ที่ระบบ render เอง — ใช้กับลิงก์หน้า Guest/ยืนยันการจอง
    /// GET /API/ViewReceiptDoc.ashx?doc=REC260702001
    /// </summary>
    public class ViewReceiptDoc : IHttpHandler
    {
        private static readonly Regex DocPattern = new Regex(@"^REC[0-9A-Za-z\-]{4,20}$", RegexOptions.Compiled);

        public void ProcessRequest(HttpContext context)
        {
            string doc = context.Request.QueryString["doc"] ?? "";
            if (!DocPattern.IsMatch(doc))
            {
                context.Response.StatusCode = 400;
                context.Response.Write("invalid doc");
                return;
            }

            string conn = DatabaseHelper.GetConnectionString();
            var code = new code();

            // 1) เอกสารทางการจาก NextAcc (smart cache — ครั้งแรกดึง ครั้งต่อไปเปิดทันที)
            try
            {
                var cfg = new Take_Time_BangPhra.Integration.AccountingConfig(conn);
                if (cfg.IsConfigured && cfg.Enabled && cfg.IsReceiptDocumentMode)
                {
                    // สถานะใบ (ใบยกเลิกให้ได้ลายน้ำ)
                    bool cancelled = false;
                    try
                    {
                        var st = code.DatabaseQuerySafe(conn,
                            "SELECT TOP 1 Status FROM Account_Receipt WHERE ID = @id",
                            new Dictionary<string, object> { { "@id", doc } });
                        cancelled = st != null && st.Rows.Count > 0
                            && string.Equals(st.Rows[0][0]?.ToString(), "Cancel", StringComparison.OrdinalIgnoreCase);
                    }
                    catch { }

                    context.Server.ScriptTimeout = 300;
                    var pdf = System.Threading.Tasks.Task.Run(() =>
                        new Take_Time_BangPhra.Integration.AccountingSyncService(conn)
                            .DownloadReceiptPdfFromNextAccAsync(doc, cancelled)
                    ).GetAwaiter().GetResult();
                    if (pdf != null && pdf.Found && !string.IsNullOrEmpty(pdf.PdfRelativeUrl))
                    {
                        context.Response.Redirect(pdf.PdfRelativeUrl, false);
                        context.ApplicationInstance.CompleteRequest();
                        return;
                    }
                }
            }
            catch { /* NextAcc ไม่พร้อม → ใช้ไฟล์ local */ }

            // 2) fallback: PDF ที่ระบบ render เอง (path ตามปี/เดือนของ Created_Date)
            try
            {
                var dt = code.DatabaseQuerySafe(conn,
                    "SELECT TOP 1 UID, Created_Date FROM Account_Receipt WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", doc } });
                if (dt != null && dt.Rows.Count > 0)
                {
                    string uid = dt.Rows[0]["UID"]?.ToString() ?? "";
                    DateTime created = Convert.ToDateTime(dt.Rows[0]["Created_Date"]);
                    string basePath = AppCfg.Get("ReceiptFolderPath");
                    foreach (string month in new[] { created.Month.ToString("00"), created.Month.ToString() })
                    {
                        foreach (string name in new[] { $"{doc}_{uid}.pdf", $"{doc}.pdf" })
                        {
                            string full = Path.Combine(basePath, created.Year.ToString(), month, name);
                            if (File.Exists(full))
                            {
                                string rel = $"/Documents/Receipt/{created.Year}/{month}/{name}";
                                context.Response.Redirect(rel, false);
                                context.ApplicationInstance.CompleteRequest();
                                return;
                            }
                        }
                    }
                }
            }
            catch { }

            context.Response.StatusCode = 404;
            context.Response.Write("document not found");
        }

        public bool IsReusable => false;
    }
}
