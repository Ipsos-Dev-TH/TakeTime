using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Web.UI;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Admin.Chat
{
    public partial class OmniChannelInbox : Page
    {
        private string ConnStr => ConfigurationManager.ConnectionStrings["TaketimeConnectionString"]?.ConnectionString ?? "";

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Perm.Guard(this, Perm.OpsChat)) return;   // กลุ่มสิทธิ์ไม่อนุญาตส่วนนี้
            if (!Feature.Guard(this, "Chat", "~/Default")) return;   // ฟีเจอร์ถูกปิด (ตั้งค่าระบบ → หมวดฟีเจอร์)
            if (Session["permission"]?.ToString() != "True")
            {
                Response.Redirect("~/Admin/Login");
                return;
            }

            if (Request.HttpMethod == "POST" && Request.ContentType?.Contains("application/json") == true)
            {
                HandlePost();
                return;
            }

            string action = Request.QueryString["action"];
            if (!string.IsNullOrEmpty(action))
            {
                HandleAction(action);
                return;
            }

            if (!IsPostBack)
                LoadInitialData();
        }

        private void LoadInitialData()
        {
            try
            {
                var svc = new OmniChannelService(ConnStr);
                var serializer = new JavaScriptSerializer();

                DataTable dtChannels = svc.GetEnabledChannels();
                var channelList = new List<object>();
                foreach (DataRow row in dtChannels.Rows)
                {
                    channelList.Add(new
                    {
                        code = row["ChannelCode"].ToString(),
                        name = row["ChannelName"].ToString(),
                        icon = row["IconClass"].ToString(),
                        color = row["BrandColor"].ToString()
                    });
                }
                hfChannelsData.Value = serializer.Serialize(channelList);

                DataTable dtCanned = svc.GetCannedResponses();
                var cannedList = new List<object>();
                foreach (DataRow row in dtCanned.Rows)
                {
                    cannedList.Add(new
                    {
                        id = Convert.ToInt32(row["ID"]),
                        shortcut = row["Shortcut"].ToString(),
                        title = row["Title"].ToString(),
                        content = row["Content"].ToString()
                    });
                }
                hfCannedData.Value = serializer.Serialize(cannedList);
            }
            catch
            {
                hfChannelsData.Value = "[]";
                hfCannedData.Value = "[]";
            }
        }

        private void HandleAction(string action)
        {
            Dictionary<string, object> result;
            switch (action)
            {
                case "stats":
                    result = GetStats();
                    break;
                default:
                    result = new Dictionary<string, object> { { "success", false }, { "message", "Unknown action" } };
                    break;
            }
            WriteJson(result);
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
                case "conversations":
                    result = GetConversations(data);
                    break;
                case "detail":
                    result = GetConversationDetail(data);
                    break;
                case "send":
                    result = SendMessage(data);
                    break;
                case "updateStatus":
                    result = UpdateStatus(data);
                    break;
                case "assign":
                    result = Assign(data);
                    break;
                case "cannedUsed":
                    result = CannedUsed(data);
                    break;
                case "aiSuggest":
                    result = AiSuggest(data);
                    break;
                case "confirmBooking":
                    result = ConfirmBooking(data);
                    break;
                case "pendingBookings":
                    result = GetPendingBookings();
                    break;
                default:
                    result = new Dictionary<string, object> { { "success", false }, { "message", "Unknown action" } };
                    break;
            }

            WriteJson(result);
        }

        private Dictionary<string, object> GetStats()
        {
            try
            {
                var svc = new OmniChannelService(ConnStr);
                return svc.GetConversationStats().ToDictionary(k => k.Key, k => (object)k.Value);
            }
            catch
            {
                return new Dictionary<string, object> { { "open", 0 }, { "pending", 0 }, { "unread", 0 } };
            }
        }

        private Dictionary<string, object> GetConversations(Dictionary<string, object> data)
        {
            try
            {
                var svc = new OmniChannelService(ConnStr);
                string channel = data.ContainsKey("channel") ? data["channel"]?.ToString() : "ALL";
                string status = data.ContainsKey("status") ? data["status"]?.ToString() : "ALL";
                string search = data.ContainsKey("search") ? data["search"]?.ToString() : null;
                string reply = data.ContainsKey("reply") ? data["reply"]?.ToString() : null;
                string sort = data.ContainsKey("sort") ? data["sort"]?.ToString() : null;
                bool unreadOnly = data.ContainsKey("unreadOnly") && data["unreadOnly"] != null
                                  && data["unreadOnly"].ToString().ToLowerInvariant() == "true";
                int limit = 50, offset = 0;
                if (data.ContainsKey("limit")) int.TryParse(data["limit"]?.ToString(), out limit);
                if (data.ContainsKey("offset")) int.TryParse(data["offset"]?.ToString(), out offset);

                DataTable dt = svc.GetConversations(channel, status, search, limit, reply, sort, offset, unreadOnly);
                int total = svc.CountConversations(channel, status, search, reply, unreadOnly);
                var convList = new List<object>();

                foreach (DataRow row in dt.Rows)
                {
                    DateTime? lastMsg = row["LastMessageDate"] != DBNull.Value ? (DateTime?)Convert.ToDateTime(row["LastMessageDate"]) : null;
                    string timeLabel = "";
                    if (lastMsg.HasValue)
                    {
                        var diff = DateTime.Now - lastMsg.Value;
                        if (diff.TotalMinutes < 1) timeLabel = "เมื่อสักครู่";
                        else if (diff.TotalMinutes < 60) timeLabel = (int)diff.TotalMinutes + " นาที";
                        else if (diff.TotalHours < 24) timeLabel = lastMsg.Value.ToString("HH:mm");
                        else timeLabel = lastMsg.Value.ToString("dd/MM");
                    }

                    convList.Add(new
                    {
                        id = Convert.ToInt64(row["ID"]),
                        name = row["DisplayName"]?.ToString() ?? "Unknown",
                        channelName = row["ChannelName"]?.ToString(),
                        iconClass = row["IconClass"]?.ToString(),
                        brandColor = row["BrandColor"]?.ToString(),
                        preview = row["LastMessagePreview"]?.ToString(),
                        lastTime = timeLabel,
                        unread = Convert.ToInt32(row["UnreadCount"]),
                        status = row["Status"]?.ToString(),
                        assigned = row["AssignedTo"]?.ToString(),
                        phone = row["MobilePhone"]?.ToString(),
                        // ยังไม่ตอบ = ข้อความล่าสุดมาจากลูกค้า — ใช้ติดป้ายในรายการ
                        needsReply = row.Table.Columns.Contains("NeedsReply")
                                     && Convert.ToInt32(row["NeedsReply"]) == 1,
                        waitingLabel = WaitLabel(row)
                    });
                }

                return new Dictionary<string, object>
                {
                    { "conversations", convList },
                    { "total", total },
                    { "offset", offset },
                    { "hasMore", offset + convList.Count < total }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "conversations", new List<object>() }, { "error", ex.Message } };
            }
        }

        /// <summary>"รอตอบมานานเท่าไร" — แสดงเฉพาะรายการที่ยังไม่ได้ตอบ</summary>
        private static string WaitLabel(DataRow row)
        {
            if (!row.Table.Columns.Contains("NeedsReply") || Convert.ToInt32(row["NeedsReply"]) != 1) return "";
            if (!row.Table.Columns.Contains("WaitingMinutes") || row["WaitingMinutes"] == DBNull.Value) return "";
            int m = Convert.ToInt32(row["WaitingMinutes"]);
            if (m < 0) return "";
            if (m < 60) return "รอ " + m + " นาที";
            if (m < 1440) return "รอ " + (m / 60) + " ชม.";
            return "รอ " + (m / 1440) + " วัน";
        }

        private Dictionary<string, object> GetConversationDetail(Dictionary<string, object> data)
        {
            try
            {
                long convId = Convert.ToInt64(data["conversationId"]);
                var svc = new OmniChannelService(ConnStr);

                svc.MarkConversationRead(convId);

                DataTable dtConv = svc.GetConversationDetail(convId);
                if (dtConv.Rows.Count == 0)
                    return new Dictionary<string, object> { { "success", false } };

                DataRow conv = dtConv.Rows[0];
                DataTable dtMsgs = svc.GetMessages(convId);

                var messages = new List<object>();
                foreach (DataRow msg in dtMsgs.Rows)
                {
                    DateTime created = Convert.ToDateTime(msg["Created_Date"]);
                    messages.Add(new
                    {
                        direction = msg["Direction"]?.ToString(),
                        sender = msg["SenderName"]?.ToString(),
                        content = msg["Content"]?.ToString(),
                        type = msg["MessageType"]?.ToString(),
                        mediaUrl = msg["MediaUrl"]?.ToString(),
                        time = created.ToString("HH:mm"),
                        dateLabel = created.Date == DateTime.Today ? "วันนี้" :
                                    created.Date == DateTime.Today.AddDays(-1) ? "เมื่อวาน" :
                                    created.ToString("dd/MM/yyyy")
                    });
                }

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "conversation", new {
                        id = Convert.ToInt64(conv["ID"]),
                        name = conv["DisplayName"]?.ToString(),
                        channelName = conv["ChannelName"]?.ToString(),
                        iconClass = conv["IconClass"]?.ToString(),
                        brandColor = conv["BrandColor"]?.ToString(),
                        status = conv["Status"]?.ToString(),
                        phone = conv["MobilePhone"]?.ToString(),
                        email = conv["Email"]?.ToString()
                    }},
                    { "messages", messages }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        private Dictionary<string, object> SendMessage(Dictionary<string, object> data)
        {
            try
            {
                long convId = Convert.ToInt64(data["conversationId"]);
                string content = data["content"]?.ToString() ?? "";
                string senderName = Session["username"]?.ToString() ?? "Staff";

                var svc = new OmniChannelService(ConnStr);
                var result = svc.SendMessage(convId, content, senderName);

                if (result.Success)
                {
                    try
                    {
                        var aiSvc = new AIKnowledgeService(ConnStr);
                        if (aiSvc.IsFeatureEnabled("LEARN_FROM_STAFF"))
                        {
                            DataTable dtMsgs = svc.GetMessages(convId, 5);
                            string lastCustomerMsg = null;
                            for (int i = dtMsgs.Rows.Count - 1; i >= 0; i--)
                            {
                                if (dtMsgs.Rows[i]["Direction"]?.ToString() == "IN")
                                {
                                    lastCustomerMsg = dtMsgs.Rows[i]["Content"]?.ToString();
                                    break;
                                }
                            }
                            if (!string.IsNullOrEmpty(lastCustomerMsg))
                                aiSvc.LearnFromStaffReply(lastCustomerMsg, content, null, senderName);
                        }
                    }
                    catch { }
                }

                return new Dictionary<string, object>
                {
                    { "success", result.Success },
                    { "message", result.Error ?? "ส่งสำเร็จ" },
                    // บันทึกได้ ≠ ลูกค้าได้รับ — ส่งสถานะจริงไปให้หน้าเว็บเตือน
                    { "delivery", result.DeliveryStatus ?? "" },
                    { "warning", result.Warning ?? "" }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        private Dictionary<string, object> UpdateStatus(Dictionary<string, object> data)
        {
            try
            {
                long convId = Convert.ToInt64(data["conversationId"]);
                string status = data["status"]?.ToString();
                var svc = new OmniChannelService(ConnStr);
                svc.UpdateConversationStatus(convId, status);
                return new Dictionary<string, object> { { "success", true } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        private Dictionary<string, object> Assign(Dictionary<string, object> data)
        {
            try
            {
                long convId = Convert.ToInt64(data["conversationId"]);
                string assignedTo = data["assignedTo"]?.ToString();
                var svc = new OmniChannelService(ConnStr);
                svc.UpdateConversationStatus(convId, null, assignedTo);
                return new Dictionary<string, object> { { "success", true } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        private Dictionary<string, object> CannedUsed(Dictionary<string, object> data)
        {
            try
            {
                int id = Convert.ToInt32(data["id"]);
                var svc = new OmniChannelService(ConnStr);
                svc.IncrementCannedUsage(id);
                return new Dictionary<string, object> { { "success", true } };
            }
            catch { return new Dictionary<string, object> { { "success", true } }; }
        }

        private Dictionary<string, object> AiSuggest(Dictionary<string, object> data)
        {
            try
            {
                long convId = Convert.ToInt64(data["conversationId"]);
                var aiSvc = new DeepSeekService(ConnStr);
                if (!aiSvc.IsAdminSuggestEnabled)
                    return new Dictionary<string, object> { { "success", false }, { "message", "AI แนะนำยังไม่เปิดใช้งาน" } };

                var omniSvc = new OmniChannelService(ConnStr);
                DataTable dtMsgs = omniSvc.GetMessages(convId, 10);

                var history = new List<ChatMessage>();
                foreach (DataRow row in dtMsgs.Rows)
                {
                    history.Add(new ChatMessage
                    {
                        Role = row["Direction"]?.ToString() == "IN" ? "user" : "assistant",
                        Content = row["Content"]?.ToString()
                    });
                }

                string prompt = "จากบทสนทนาด้านบน ช่วยแนะนำข้อความตอบกลับที่เหมาะสม สั้นกระชับ สุภาพ เป็นภาษาไทย ตอบเฉพาะข้อความตอบกลับเท่านั้น";
                var result = aiSvc.SendMessage(prompt, "omni_suggest_" + convId + "_" + DateTime.Now.Ticks, history);

                return new Dictionary<string, object>
                {
                    { "success", result.Success },
                    { "suggestion", result.Message }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        private Dictionary<string, object> ConfirmBooking(Dictionary<string, object> data)
        {
            try
            {
                int resId = Convert.ToInt32(data["reservationId"]);
                string staffName = Session["username"]?.ToString() ?? "Staff";

                var aiSvc = new AIKnowledgeService(ConnStr);
                var result = aiSvc.ConfirmBookingByStaff(resId, staffName);

                if (Convert.ToBoolean(result["success"]))
                {
                    long convId = Convert.ToInt64(result["conversationId"]);
                    if (convId > 0)
                    {
                        var omniSvc = new OmniChannelService(ConnStr);
                        omniSvc.SendBookingStatusUpdate(convId, resId, "ยืนยันแล้ว", staffName);
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        private Dictionary<string, object> GetPendingBookings()
        {
            try
            {
                var aiSvc = new AIKnowledgeService(ConnStr);
                DataTable dt = aiSvc.GetPendingAIBookings();
                var bookings = new List<object>();

                foreach (DataRow row in dt.Rows)
                {
                    bookings.Add(new
                    {
                        id = Convert.ToInt32(row["ID"]),
                        reservationId = Convert.ToInt32(row["ReservationID"]),
                        conversationId = Convert.ToInt64(row["ConversationID"]),
                        customerName = row["CustomerName"]?.ToString(),
                        customerPhone = row["Customer_MobilePhone"]?.ToString(),
                        roomName = row["RoomNames"]?.ToString(),
                        checkIn = Convert.ToDateTime(row["CheckinDate"]).ToString("dd/MM/yyyy"),
                        checkOut = Convert.ToDateTime(row["CheckoutDate"]).ToString("dd/MM/yyyy"),
                        totalPrice = Convert.ToDecimal(row["TotalPrice"]),
                        status = row["Status"]?.ToString(),
                        createdDate = Convert.ToDateTime(row["Created_Date"]).ToString("dd/MM/yyyy HH:mm")
                    });
                }

                return new Dictionary<string, object> { { "bookings", bookings } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "bookings", new List<object>() }, { "error", ex.Message } };
            }
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
