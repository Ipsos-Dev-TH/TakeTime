using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Web.UI;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Admin.Report
{
    public partial class AIReportSummary : Page
    {
        private string ConnStr => ConfigurationManager.ConnectionStrings["TaketimeConnectionString"]?.ConnectionString ?? "";

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Feature.Guard(this, "AI", "~/Default")) return;   // ฟีเจอร์ถูกปิด (ตั้งค่าระบบ → หมวดฟีเจอร์)
            // Check permission
            if (Session["permission"]?.ToString() != "True" ||
                (Session["User"]?.ToString() != "Owner" && Session["User"]?.ToString() != "Admin"))
            {
                Response.Redirect("~/Admin/Login");
                return;
            }

            // Handle POST API requests
            if (Request.HttpMethod == "POST" && Request.ContentType?.Contains("application/json") == true)
            {
                HandlePost();
                return;
            }
        }

        private void HandlePost()
        {
            string body;
            using (var reader = new StreamReader(Request.InputStream))
                body = reader.ReadToEnd();

            string action = Request.QueryString["action"] ?? "";
            var serializer = new JavaScriptSerializer();
            var data = serializer.Deserialize<Dictionary<string, object>>(body);
            Dictionary<string, object> result;

            switch (action)
            {
                case "getQuickInsights":
                    result = HandleGetQuickInsights();
                    break;
                case "generateDaily":
                    result = HandleGenerateDaily(data);
                    break;
                case "generateWeekly":
                    result = HandleGenerateWeekly(data);
                    break;
                case "generateMonthly":
                    result = HandleGenerateMonthly(data);
                    break;
                case "getLatest":
                    result = HandleGetLatest(data);
                    break;
                case "getHistory":
                    result = HandleGetHistory(data);
                    break;
                default:
                    result = new Dictionary<string, object>
                    {
                        { "success", false },
                        { "message", "Unknown action: " + action }
                    };
                    break;
            }

            WriteJson(result);
        }

        private Dictionary<string, object> HandleGetQuickInsights()
        {
            try
            {
                var svc = new AIReportService(ConnStr);
                var insights = svc.GetQuickInsights();

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "data", insights }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object>
                {
                    { "success", false },
                    { "message", "เกิดข้อผิดพลาด: " + ex.Message }
                };
            }
        }

        private Dictionary<string, object> HandleGenerateDaily(Dictionary<string, object> data)
        {
            try
            {
                if (data == null || !data.ContainsKey("date") || !DateTime.TryParse(data["date"]?.ToString(), out DateTime date))
                {
                    return new Dictionary<string, object>
                    {
                        { "success", false },
                        { "message", "กรุณาระบุวันที่ที่ถูกต้อง" }
                    };
                }

                var svc = new AIReportService(ConnStr);
                string summary = svc.GenerateDailySummary(date);

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "data", summary }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object>
                {
                    { "success", false },
                    { "message", "เกิดข้อผิดพลาด: " + ex.Message }
                };
            }
        }

        private Dictionary<string, object> HandleGenerateWeekly(Dictionary<string, object> data)
        {
            try
            {
                if (data == null || !data.ContainsKey("date") || !DateTime.TryParse(data["date"]?.ToString(), out DateTime date))
                {
                    return new Dictionary<string, object>
                    {
                        { "success", false },
                        { "message", "กรุณาระบุวันที่เริ่มต้นสัปดาห์ที่ถูกต้อง" }
                    };
                }

                var svc = new AIReportService(ConnStr);
                string summary = svc.GenerateWeeklySummary(date);

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "data", summary }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object>
                {
                    { "success", false },
                    { "message", "เกิดข้อผิดพลาด: " + ex.Message }
                };
            }
        }

        private Dictionary<string, object> HandleGenerateMonthly(Dictionary<string, object> data)
        {
            try
            {
                if (data == null || !data.ContainsKey("date") || !DateTime.TryParse(data["date"]?.ToString(), out DateTime date))
                {
                    return new Dictionary<string, object>
                    {
                        { "success", false },
                        { "message", "กรุณาระบุเดือนที่ถูกต้อง" }
                    };
                }

                var svc = new AIReportService(ConnStr);
                string summary = svc.GenerateMonthlySummary(date);

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "data", summary }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object>
                {
                    { "success", false },
                    { "message", "เกิดข้อผิดพลาด: " + ex.Message }
                };
            }
        }

        private Dictionary<string, object> HandleGetLatest(Dictionary<string, object> data)
        {
            try
            {
                string reportType = data != null && data.ContainsKey("reportType")
                    ? data["reportType"]?.ToString() ?? "DAILY"
                    : "DAILY";

                var svc = new AIReportService(ConnStr);
                string summary = svc.GetLatestSummary(reportType);

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "data", summary }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object>
                {
                    { "success", false },
                    { "message", "เกิดข้อผิดพลาด: " + ex.Message }
                };
            }
        }

        private Dictionary<string, object> HandleGetHistory(Dictionary<string, object> data)
        {
            try
            {
                string reportType = data != null && data.ContainsKey("reportType")
                    ? data["reportType"]?.ToString() ?? "DAILY"
                    : "DAILY";

                int limit = 20;
                if (data != null && data.ContainsKey("limit"))
                {
                    int.TryParse(data["limit"]?.ToString(), out limit);
                    if (limit <= 0) limit = 20;
                }

                var svc = new AIReportService(ConnStr);
                DataTable dt = svc.GetSummaryHistory(reportType, limit);

                // Convert DataTable to list of dictionaries for JSON serialization
                var list = new List<Dictionary<string, object>>();
                foreach (DataRow row in dt.Rows)
                {
                    var item = new Dictionary<string, object>();
                    foreach (DataColumn col in dt.Columns)
                    {
                        if (row[col] != DBNull.Value)
                        {
                            if (col.DataType == typeof(DateTime))
                                item[col.ColumnName] = ((DateTime)row[col]).ToString("yyyy-MM-ddTHH:mm:ss");
                            else
                                item[col.ColumnName] = row[col];
                        }
                        else
                        {
                            item[col.ColumnName] = null;
                        }
                    }
                    list.Add(item);
                }

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "data", list }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object>
                {
                    { "success", false },
                    { "message", "เกิดข้อผิดพลาด: " + ex.Message }
                };
            }
        }

        private void WriteJson(object result)
        {
            Response.ContentType = "application/json";
            Response.Write(new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(result));
            Response.End();
        }
    }
}
