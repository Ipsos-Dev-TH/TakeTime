using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Web.Script.Serialization;
using System.Web.UI;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Admin.Chat
{
    public partial class ChannelSettings : Page
    {
        private string ConnStr => ConfigurationManager.ConnectionStrings["TaketimeConnectionString"]?.ConnectionString ?? "";

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Feature.Guard(this, "Chat", "~/Default")) return;   // ฟีเจอร์ถูกปิด (ตั้งค่าระบบ → หมวดฟีเจอร์)
            if (Session["permission"]?.ToString() != "True" ||
                (Session["User"]?.ToString() != "Owner" && Session["User"]?.ToString() != "Admin"))
            {
                Response.Redirect("~/Admin/Login");
                return;
            }

            if (Request.HttpMethod == "POST" && Request.ContentType?.Contains("application/json") == true)
            {
                HandlePost();
                return;
            }

            if (!IsPostBack)
                LoadChannels();
        }

        private void LoadChannels()
        {
            try
            {
                var svc = new OmniChannelService(ConnStr);
                DataTable dt = svc.GetChannels();
                var serializer = new JavaScriptSerializer();
                var list = new List<object>();

                foreach (DataRow row in dt.Rows)
                {
                    Dictionary<string, object> config = new Dictionary<string, object>();
                    if (row["Config"] != DBNull.Value && !string.IsNullOrEmpty(row["Config"].ToString()))
                    {
                        try { config = serializer.Deserialize<Dictionary<string, object>>(row["Config"].ToString()); }
                        catch { }
                    }

                    list.Add(new
                    {
                        id = Convert.ToInt32(row["ID"]),
                        code = row["ChannelCode"].ToString(),
                        name = row["ChannelName"].ToString(),
                        type = row["ChannelType"].ToString(),
                        icon = row["IconClass"].ToString(),
                        color = row["BrandColor"].ToString(),
                        enabled = Convert.ToBoolean(row["IsEnabled"]),
                        config = config
                    });
                }

                hfChannels.Value = serializer.Serialize(list);
            }
            catch
            {
                hfChannels.Value = "[]";
            }
        }

        private void HandlePost()
        {
            string body;
            using (var reader = new StreamReader(Request.InputStream))
                body = reader.ReadToEnd();

            string action = Request.QueryString["action"] ?? "";
            var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(body ?? "{}");
            Dictionary<string, object> result;

            switch (action)
            {
                case "toggle":
                    result = ToggleChannel(data);
                    break;
                case "saveConfig":
                    result = SaveConfig(data);
                    break;
                default:
                    result = new Dictionary<string, object> { { "success", false }, { "message", "Unknown" } };
                    break;
            }

            Response.Clear();
            Response.ContentType = "application/json";
            Response.Write(new JavaScriptSerializer().Serialize(result));
            Response.End();
        }

        private Dictionary<string, object> ToggleChannel(Dictionary<string, object> data)
        {
            try
            {
                int id = Convert.ToInt32(data["id"]);
                bool enabled = Convert.ToBoolean(data["enabled"]);
                var svc = new OmniChannelService(ConnStr);
                svc.UpdateChannel(id, enabled, null);
                return new Dictionary<string, object> { { "success", true } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        private Dictionary<string, object> SaveConfig(Dictionary<string, object> data)
        {
            try
            {
                string code = data["code"]?.ToString();
                var config = data.ContainsKey("config") ? data["config"] as Dictionary<string, object> : new Dictionary<string, object>();
                var svc = new OmniChannelService(ConnStr);
                svc.SetChannelConfig(code, config);
                return new Dictionary<string, object> { { "success", true } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }
    }
}
