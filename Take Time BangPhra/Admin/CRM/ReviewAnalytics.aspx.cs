// ===========================================================================
// ReviewAnalytics.aspx.cs
// วิเคราะห์รีวิวทุกแพลตฟอร์ม (ภายใน + ออนไลน์): ตัวกรอง, KPI, ตารางรายแพลตฟอร์ม,
// แนวโน้ม, การกระจายคะแนน, หัวข้อ, รายการรีวิว (แบ่งหน้าฝั่งเซิร์ฟเวอร์),
// ตัดออก/นำกลับ, บันทึกคำตอบ, สรุปด้วย AI (cache), Export CSV.
// Query ทั้งหมดอยู่ใน Class/Reviews/ReviewQuery.cs (กันคอลัมน์ที่ยังไม่ migrate)
// ===========================================================================

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web;
using System.Web.Script.Serialization;
using System.Web.UI;
using Take_Time_BangPhra.Reviews;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Admin.CRM
{
    public partial class ReviewAnalytics : Page
    {
        private const string CsrfSessionKey = "ReviewAnalytics_Csrf";
        private string ConnStr => ConfigurationManager.ConnectionStrings["TaketimeConnectionString"]?.ConnectionString ?? "";

        /// <summary>Anti-forgery token echoed by the page's fetch() calls (header X-RA-Token).</summary>
        protected string CsrfToken = "";
        /// <summary>AI button enabled (Feature AI + AI_Enabled). Server re-checks on every call.</summary>
        protected bool AiAvailable;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Perm.Guard(this, Perm.CrmReview)) return;   // กลุ่มสิทธิ์ไม่อนุญาตส่วนนี้
            if (!Feature.Guard(this, "Reviews", "~/Default")) return;   // ฟีเจอร์ถูกปิด (ตั้งค่าระบบ → หมวดฟีเจอร์)
            if (Session["permission"]?.ToString() != "True")
            {
                Response.Redirect("~/Admin/Login", false);
                Context.ApplicationInstance.CompleteRequest();
                return;
            }

            CsrfToken = Session[CsrfSessionKey] as string;
            if (string.IsNullOrEmpty(CsrfToken))
            {
                CsrfToken = Guid.NewGuid().ToString("N");
                Session[CsrfSessionKey] = CsrfToken;
            }

            string action = Request.QueryString["action"] ?? "";
            if (Request.HttpMethod == "GET" && action == "exportCsv")
            {
                ExportCsv();
                return;
            }
            if (Request.HttpMethod == "POST" && Request.ContentType != null && Request.ContentType.Contains("application/json"))
            {
                HandlePost(action);
                return;
            }

            try { AiAvailable = Feature.On("AI") && new DeepSeekService(ConnStr).IsEnabled; }
            catch { AiAvailable = false; }
        }

        // ------------------------------------------------------------------
        // JSON endpoints
        // ------------------------------------------------------------------
        private void HandlePost(string action)
        {
            Dictionary<string, object> result;
            if (!string.Equals(Request.Headers["X-RA-Token"], CsrfToken, StringComparison.Ordinal))
            {
                Response.StatusCode = 403;
                WriteJson(new Dictionary<string, object> { { "success", false }, { "message", "เซสชันหมดอายุ กรุณารีเฟรชหน้า" } });
                return;
            }

            Dictionary<string, object> data;
            try
            {
                string body;
                using (var reader = new StreamReader(Request.InputStream, Encoding.UTF8))
                    body = reader.ReadToEnd();
                data = string.IsNullOrWhiteSpace(body)
                    ? new Dictionary<string, object>()
                    : new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 }.Deserialize<Dictionary<string, object>>(body);
            }
            catch
            {
                WriteJson(new Dictionary<string, object> { { "success", false }, { "message", "ข้อมูลที่ส่งมาไม่ถูกต้อง" } });
                return;
            }

            try
            {
                switch (action)
                {
                    case "options":
                        result = Ok("data", ReviewQuery.GetFilterOptions(ConnStr));
                        break;
                    case "analytics":
                        result = Ok("data", ReviewQuery.GetAnalytics(ConnStr, FilterOf(data)));
                        break;
                    case "reviews":
                        result = Ok("data", ReviewQuery.GetReviewPage(ConnStr, FilterOf(data)));
                        break;
                    case "setExcluded":
                        {
                            string msg;
                            bool ok = ReviewQuery.SetExcluded(ConnStr, Str(data, "kind"), Long(data, "id"), Str(data, "excluded") == "True" || Str(data, "excluded") == "true", out msg);
                            result = new Dictionary<string, object> { { "success", ok }, { "message", msg } };
                            break;
                        }
                    case "saveResponse":
                        {
                            string msg;
                            DateTime d;
                            DateTime? when = DateTime.TryParseExact(Str(data, "date"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d) ? d : (DateTime?)null;
                            short adminId;
                            short.TryParse(Session["UserID"]?.ToString(), out adminId);
                            bool ok = ReviewQuery.SaveResponse(ConnStr, Str(data, "kind"), Long(data, "id"), Str(data, "text"), when, CurrentUserName(), adminId, out msg);
                            result = new Dictionary<string, object> { { "success", ok }, { "message", msg } };
                            break;
                        }
                    case "aiSummary":
                        if (!Feature.On("AI"))
                            result = new Dictionary<string, object> { { "success", false }, { "message", "ฟีเจอร์ AI ถูกปิด (ตั้งค่าระบบ → หมวดฟีเจอร์)" } };
                        else
                            result = ReviewQuery.GenerateAiSummary(ConnStr, FilterOf(data), CurrentUserName(), Str(data, "force") == "True" || Str(data, "force") == "true");
                        break;
                    default:
                        result = new Dictionary<string, object> { { "success", false }, { "message", "ไม่รู้จักคำสั่ง: " + action } };
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("ReviewAnalytics." + action + ": " + ex);
                result = new Dictionary<string, object> { { "success", false }, { "message", "เกิดข้อผิดพลาด: " + ex.Message } };
            }
            WriteJson(result);
        }

        private static ReviewFilter FilterOf(Dictionary<string, object> data)
        {
            object f;
            var inner = data != null && data.TryGetValue("filter", out f) ? f as Dictionary<string, object> : null;
            return ReviewFilter.FromDictionary(inner ?? data);
        }

        // ------------------------------------------------------------------
        // CSV export (current filter; UTF-8 BOM so Excel reads Thai)
        // ------------------------------------------------------------------
        private void ExportCsv()
        {
            DataTable dt;
            try
            {
                var filter = ReviewFilter.FromJson(Request.Unvalidated.QueryString["filter"]);   // JSON may contain "<" in search text
                dt = ReviewQuery.GetExportRows(ConnStr, filter, 20000);
            }
            catch (Exception ex)
            {
                Response.Clear();
                Response.StatusCode = 500;
                Response.ContentType = "text/plain; charset=utf-8";
                Response.Write("Export ไม่สำเร็จ: " + ex.Message);
                Response.End();
                return;
            }

            var sb = new StringBuilder();
            string[] head = { "ชนิด", "ID", "แพลตฟอร์ม", "แหล่งที่มา", "วันที่รีวิว", "ผู้รีวิว", "คะแนน/5", "คะแนน/10", "คะแนนต้นฉบับ", "สเกล",
                "หัวข้อ", "ข้อความ", "คำตอบเจ้าของ", "วันที่ตอบ", "ภาษา", "วิธีนำเข้า", "ลิงก์", "ชั่วคราว", "ตัดออก", "Sentiment", "ประเภทห้อง", "ประเภทผู้เข้าพัก", "วันที่เข้าพัก" };
            sb.AppendLine(string.Join(",", Array.ConvertAll(head, Csv)));
            foreach (DataRow r in dt.Rows)
            {
                var cells = new List<string>
                {
                    Convert.ToString(r["Kind"]) == "I" ? "ภายใน" : "ออนไลน์",
                    Convert.ToString(r["ID"]),
                    Convert.ToString(r["Platform"]),
                    Convert.ToString(r["SourceCode"]),
                    DateCell(r["ReviewDate"]),
                    Convert.ToString(r["ReviewerName"]),
                    NumCell(r["R5"], "0.00"),
                    NumCell(r["R10"], "0.0"),
                    NumCell(r["RatingRaw"], "0.##"),
                    NumCell(r["RatingScale"], "0"),
                    Convert.ToString(r["Title"]),
                    Convert.ToString(r["ReviewText"]),
                    Convert.ToString(r["Resp"]),
                    DateCell(r["RespDate"]),
                    Convert.ToString(r["Lang"]),
                    Convert.ToString(r["Ingest"]),
                    Convert.ToString(r["Url"]),
                    Convert.ToInt32(r["IsProv"]) == 1 ? "Y" : "",
                    Convert.ToInt32(r["IsExcl"]) == 1 ? "Y" : "",
                    Convert.ToString(r["Sentiment"]),
                    Convert.ToString(r["RoomType"]),
                    Convert.ToString(r["Traveller"]),
                    DateCell(r["StayDate"])
                };
                sb.AppendLine(string.Join(",", cells.ConvertAll(Csv)));
            }

            Response.Clear();
            Response.ContentType = "text/csv";
            Response.Charset = "utf-8";
            Response.ContentEncoding = new UTF8Encoding(false);   // BOM written explicitly below
            Response.AddHeader("Content-Disposition", "attachment; filename=\"reviews_" + DateTime.Now.ToString("yyyyMMdd_HHmm", CultureInfo.InvariantCulture) + ".csv\"");
            Response.BinaryWrite(new UTF8Encoding(true).GetPreamble());
            Response.Write(sb.ToString());
            Response.End();
        }

        private static string Csv(string v)
        {
            v = v ?? "";
            // กัน CSV/formula injection ใน Excel
            if (v.Length > 0 && "=+-@\t\r".IndexOf(v[0]) >= 0) v = "'" + v;
            if (v.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0) v = "\"" + v.Replace("\"", "\"\"") + "\"";
            return v;
        }

        private static string DateCell(object o)
        {
            return o == null || o == DBNull.Value ? "" : Convert.ToDateTime(o).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

        private static string NumCell(object o, string fmt)
        {
            return o == null || o == DBNull.Value ? "" : Convert.ToDouble(o).ToString(fmt, CultureInfo.InvariantCulture);
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------
        private string CurrentUserName()
        {
            return Session["UserName"]?.ToString() ?? Session["User"]?.ToString() ?? "Admin";
        }

        private static Dictionary<string, object> Ok(string key, object value)
        {
            return new Dictionary<string, object> { { "success", true }, { key, value } };
        }

        private static string Str(Dictionary<string, object> d, string k)
        {
            object v;
            return d != null && d.TryGetValue(k, out v) && v != null ? Convert.ToString(v, CultureInfo.InvariantCulture) : "";
        }

        private static long Long(Dictionary<string, object> d, string k)
        {
            long v;
            return long.TryParse(Str(d, k), out v) ? v : 0;
        }

        private void WriteJson(object data)
        {
            Response.Clear();
            Response.ContentType = "application/json";
            Response.Write(new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(data));
            Response.End();
        }
    }
}
