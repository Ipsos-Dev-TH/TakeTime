using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace Take_Time_BangPhra.Services
{
    public class OmniChannelService
    {
        private readonly string _connStr;
        private readonly code _code;

        public OmniChannelService(string connectionString)
        {
            _connStr = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _code = new code();
        }

        #region Channels

        public DataTable GetChannels()
        {
            return _code.DatabaseQuerySafe(_connStr,
                "SELECT * FROM OmniChannel_Channels ORDER BY ChannelType, ChannelName", null);
        }

        public DataTable GetEnabledChannels()
        {
            return _code.DatabaseQuerySafe(_connStr,
                "SELECT * FROM OmniChannel_Channels WHERE IsEnabled = 1 ORDER BY ChannelType, ChannelName", null);
        }

        public void UpdateChannel(int id, bool enabled, string config)
        {
            _code.DatabaseInsertSafe(_connStr,
                "UPDATE OmniChannel_Channels SET IsEnabled = @Enabled, Config = @Config, Updated_Date = GETDATE() WHERE ID = @Id",
                new Dictionary<string, object>
                {
                    { "@Id", id },
                    { "@Enabled", enabled },
                    { "@Config", config != null ? (object)config : DBNull.Value }
                });
        }

        public Dictionary<string, object> GetChannelConfig(string channelCode)
        {
            DataTable dt = _code.DatabaseQuerySafe(_connStr,
                "SELECT Config FROM OmniChannel_Channels WHERE ChannelCode = @Code",
                new Dictionary<string, object> { { "@Code", channelCode } });
            if (dt.Rows.Count > 0 && dt.Rows[0]["Config"] != DBNull.Value)
            {
                string json = dt.Rows[0]["Config"].ToString();
                if (!string.IsNullOrEmpty(json))
                    return new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
            }
            return new Dictionary<string, object>();
        }

        public void SetChannelConfig(string channelCode, Dictionary<string, object> config)
        {
            string json = new JavaScriptSerializer().Serialize(config);
            _code.DatabaseInsertSafe(_connStr,
                "UPDATE OmniChannel_Channels SET Config = @Config, Updated_Date = GETDATE() WHERE ChannelCode = @Code",
                new Dictionary<string, object> { { "@Code", channelCode }, { "@Config", json } });
        }

        public string GetWebhookSecret(string channelCode)
        {
            DataTable dt = _code.DatabaseQuerySafe(_connStr,
                "SELECT WebhookSecret FROM OmniChannel_Channels WHERE ChannelCode = @Code",
                new Dictionary<string, object> { { "@Code", channelCode } });
            return dt.Rows.Count > 0 ? dt.Rows[0]["WebhookSecret"]?.ToString() : null;
        }

        #endregion

        #region Conversations

        public DataTable GetConversations(string channelFilter = null, string statusFilter = null, string search = null, int limit = 50)
        {
            string sql = @"SELECT TOP (@Limit)
                    c.ID, c.ChannelCode, c.Status, c.Priority, c.AssignedTo, c.Tags,
                    c.LastMessageDate, c.LastMessagePreview, c.UnreadCount,
                    ct.DisplayName, ct.AvatarUrl, ct.MobilePhone, ct.Customer_MobilePhone,
                    ch.ChannelName, ch.IconClass, ch.BrandColor
                FROM OmniChannel_Conversations c
                JOIN OmniChannel_Contacts ct ON c.ContactID = ct.ID
                JOIN OmniChannel_Channels ch ON c.ChannelCode = ch.ChannelCode
                WHERE 1=1";

            var parms = new Dictionary<string, object> { { "@Limit", limit } };

            if (!string.IsNullOrEmpty(channelFilter) && channelFilter != "ALL")
            {
                sql += " AND c.ChannelCode = @Channel";
                parms["@Channel"] = channelFilter;
            }

            if (!string.IsNullOrEmpty(statusFilter) && statusFilter != "ALL")
            {
                sql += " AND c.Status = @Status";
                parms["@Status"] = statusFilter;
            }
            else
            {
                sql += " AND c.Status IN ('OPEN','PENDING')";
            }

            if (!string.IsNullOrEmpty(search))
            {
                sql += " AND (ct.DisplayName LIKE @Search OR ct.MobilePhone LIKE @Search OR c.LastMessagePreview LIKE @Search"
                 + "      OR ct.Email LIKE @Search OR c.Subject LIKE @Search OR c.Tags LIKE @Search)";
                parms["@Search"] = "%" + search + "%";
            }

            sql += " ORDER BY c.UnreadCount DESC, c.LastMessageDate DESC";

            return _code.DatabaseQuerySafe(_connStr, sql, parms);
        }

        public DataTable GetConversationDetail(long conversationId)
        {
            return _code.DatabaseQuerySafe(_connStr,
                @"SELECT c.*, ct.DisplayName, ct.AvatarUrl, ct.MobilePhone, ct.Email, ct.Customer_MobilePhone, ct.Reservation_ID,
                         ch.ChannelName, ch.IconClass, ch.BrandColor
                  FROM OmniChannel_Conversations c
                  JOIN OmniChannel_Contacts ct ON c.ContactID = ct.ID
                  JOIN OmniChannel_Channels ch ON c.ChannelCode = ch.ChannelCode
                  WHERE c.ID = @Id",
                new Dictionary<string, object> { { "@Id", conversationId } });
        }

        public void UpdateConversationStatus(long conversationId, string status, string assignedTo = null)
        {
            _code.DatabaseInsertSafe(_connStr,
                @"UPDATE OmniChannel_Conversations
                  SET Status = @Status,
                      AssignedTo = CASE WHEN @AssignedTo IS NOT NULL THEN @AssignedTo ELSE AssignedTo END,
                      Updated_Date = GETDATE()
                  WHERE ID = @Id",
                new Dictionary<string, object>
                {
                    { "@Id", conversationId },
                    { "@Status", status },
                    { "@AssignedTo", assignedTo != null ? (object)assignedTo : DBNull.Value }
                });
        }

        public void MarkConversationRead(long conversationId)
        {
            _code.DatabaseInsertSafe(_connStr,
                @"UPDATE OmniChannel_Messages SET IsRead = 1, ReadDate = GETDATE()
                  WHERE ConversationID = @Id AND Direction = 'IN' AND IsRead = 0",
                new Dictionary<string, object> { { "@Id", conversationId } });

            _code.DatabaseInsertSafe(_connStr,
                "UPDATE OmniChannel_Conversations SET UnreadCount = 0, Updated_Date = GETDATE() WHERE ID = @Id",
                new Dictionary<string, object> { { "@Id", conversationId } });
        }

        public Dictionary<string, int> GetConversationStats()
        {
            var stats = new Dictionary<string, int>();
            DataTable dt = _code.DatabaseQuerySafe(_connStr,
                @"SELECT
                    SUM(CASE WHEN Status = 'OPEN' THEN 1 ELSE 0 END) AS OpenCount,
                    SUM(CASE WHEN Status = 'PENDING' THEN 1 ELSE 0 END) AS PendingCount,
                    SUM(CASE WHEN Status = 'RESOLVED' THEN 1 ELSE 0 END) AS ResolvedCount,
                    SUM(UnreadCount) AS TotalUnread
                  FROM OmniChannel_Conversations", null);

            if (dt.Rows.Count > 0)
            {
                stats["open"] = Convert.ToInt32(dt.Rows[0]["OpenCount"] ?? 0);
                stats["pending"] = Convert.ToInt32(dt.Rows[0]["PendingCount"] ?? 0);
                stats["resolved"] = Convert.ToInt32(dt.Rows[0]["ResolvedCount"] ?? 0);
                stats["unread"] = Convert.ToInt32(dt.Rows[0]["TotalUnread"] ?? 0);
            }
            return stats;
        }

        public Dictionary<string, int> GetChannelMessageCounts()
        {
            var counts = new Dictionary<string, int>();
            DataTable dt = _code.DatabaseQuerySafe(_connStr,
                @"SELECT c.ChannelCode, COUNT(m.ID) AS MsgCount
                  FROM OmniChannel_Conversations c
                  JOIN OmniChannel_Messages m ON m.ConversationID = c.ID
                  WHERE m.Created_Date >= DATEADD(DAY, -30, GETDATE())
                  GROUP BY c.ChannelCode", null);
            foreach (DataRow row in dt.Rows)
                counts[row["ChannelCode"].ToString()] = Convert.ToInt32(row["MsgCount"]);
            return counts;
        }

        #endregion

        #region Messages

        public DataTable GetMessages(long conversationId, int limit = 100)
        {
            return _code.DatabaseQuerySafe(_connStr,
                @"SELECT TOP (@Limit) * FROM OmniChannel_Messages
                  WHERE ConversationID = @ConvId ORDER BY Created_Date ASC",
                new Dictionary<string, object> { { "@ConvId", conversationId }, { "@Limit", limit } });
        }

        public IncomingMessageResult ReceiveMessage(
            string channelCode, string platformUserId, string platformUserName,
            string content, string messageType = "TEXT",
            string mediaUrl = null, string mediaType = null,
            string platformMessageId = null, string metadata = null,
            string displayName = null, string avatarUrl = null)
        {
            try
            {
                DataTable dt = _code.DatabaseQuerySafe(_connStr,
                    @"EXEC sp_OmniChannel_AddMessage
                        @ChannelCode = @Ch, @PlatformUserId = @PUid, @PlatformUserName = @PName,
                        @Direction = 'IN', @SenderName = @Sender, @MessageType = @MType,
                        @Content = @Content, @MediaUrl = @MUrl, @MediaType = @MdType,
                        @PlatformMessageId = @PMid, @Metadata = @Meta,
                        @DisplayName = @DName, @AvatarUrl = @Avatar",
                    new Dictionary<string, object>
                    {
                        { "@Ch", channelCode },
                        { "@PUid", platformUserId },
                        { "@PName", platformUserName },
                        { "@Sender", displayName ?? platformUserName },
                        { "@MType", messageType },
                        { "@Content", content },
                        { "@MUrl", mediaUrl != null ? (object)mediaUrl : DBNull.Value },
                        { "@MdType", mediaType != null ? (object)mediaType : DBNull.Value },
                        { "@PMid", platformMessageId != null ? (object)platformMessageId : DBNull.Value },
                        { "@Meta", metadata != null ? (object)metadata : DBNull.Value },
                        { "@DName", displayName != null ? (object)displayName : DBNull.Value },
                        { "@Avatar", avatarUrl != null ? (object)avatarUrl : DBNull.Value }
                    });

                if (dt.Rows.Count > 0)
                {
                    return new IncomingMessageResult
                    {
                        Success = true,
                        MessageID = Convert.ToInt64(dt.Rows[0]["MessageID"]),
                        ConversationID = Convert.ToInt64(dt.Rows[0]["ConversationID"]),
                        ContactID = Convert.ToInt64(dt.Rows[0]["ContactID"])
                    };
                }
                return new IncomingMessageResult { Success = false };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("OmniChannel.ReceiveMessage: " + ex.Message);
                return new IncomingMessageResult { Success = false, Error = ex.Message };
            }
        }

        public SendMessageResult SendMessage(long conversationId, string content, string senderName,
            string messageType = "TEXT", string mediaUrl = null,
            bool isAI = false, double aiConfidence = 0, string aiSource = null)
        {
            try
            {
                DataTable dtConv = GetConversationDetail(conversationId);
                if (dtConv.Rows.Count == 0)
                    return new SendMessageResult { Success = false, Error = "ไม่พบการสนทนา" };

                string channelCode = dtConv.Rows[0]["ChannelCode"].ToString();

                _code.DatabaseInsertSafe(_connStr,
                    @"INSERT INTO OmniChannel_Messages (ConversationID, Direction, SenderName, MessageType, Content, MediaUrl, IsRead, DeliveryStatus, IsAIGenerated, AIConfidence, AISource, Created_Date)
                      VALUES (@ConvId, 'OUT', @Sender, @MType, @Content, @MUrl, 1, 'SENT', @IsAI, @AICnf, @AISrc, GETDATE())",
                    new Dictionary<string, object>
                    {
                        { "@ConvId", conversationId },
                        { "@Sender", senderName },
                        { "@MType", messageType },
                        { "@Content", content },
                        { "@MUrl", mediaUrl != null ? (object)mediaUrl : DBNull.Value },
                        { "@IsAI", isAI },
                        { "@AICnf", isAI ? (object)aiConfidence : DBNull.Value },
                        { "@AISrc", aiSource != null ? (object)aiSource : DBNull.Value }
                    });

                _code.DatabaseInsertSafe(_connStr,
                    "UPDATE OmniChannel_Conversations SET LastMessageDate = GETDATE(), LastMessagePreview = @Preview, Updated_Date = GETDATE() WHERE ID = @Id",
                    new Dictionary<string, object>
                    {
                        { "@Id", conversationId },
                        { "@Preview", content.Length > 200 ? content.Substring(0, 200) : content }
                    });

                DeliverOutbound(channelCode, conversationId, content);

                // ── รายงานผลส่งออกจริง ────────────────────────────────────────────
                // DeliverOutbound เป็น fire-and-forget + กลืน exception ⇒ เดิมคืน Success=true
                // เสมอ ต่อให้ SMTP ไม่ได้ตั้งค่า/ส่งไม่ออก ⇒ พนักงานเห็น "ส่งสำเร็จ" แล้วเข้าใจว่า
                // ลูกค้าได้รับแล้ว ทั้งที่ข้อความค้างอยู่ในระบบเฉย ๆ (อาการ "ตอบไม่ได้" แบบเงียบ)
                // อ่านสถานะที่ตัวส่งเขียนไว้กลับมาบอกผู้ใช้ตรง ๆ
                string delivery = ReadLastOutboundStatus(conversationId);
                return new SendMessageResult
                {
                    Success = true,
                    DeliveryStatus = delivery,
                    Warning = delivery == "FAILED"
                        ? "บันทึกข้อความแล้ว แต่ส่งออกช่องทาง " + channelCode + " ไม่สำเร็จ — "
                          + "ลูกค้ายังไม่ได้รับ (ดูสาเหตุที่ log แล้วตรวจการตั้งค่าช่องทาง)"
                        : null
                };
            }
            catch (Exception ex)
            {
                return new SendMessageResult { Success = false, Error = ex.Message };
            }
        }

        #endregion

        #region Outbound Delivery

        private void DeliverOutbound(string channelCode, long conversationId, string content)
        {
            try
            {
                switch (channelCode)
                {
                    case "LINE":
                        DeliverToLine(conversationId, content);
                        break;
                    case "FACEBOOK":
                        DeliverToFacebook(conversationId, content);
                        break;
                    case "WHATSAPP":
                        DeliverToWhatsApp(conversationId, content);
                        break;
                    case "TELEGRAM":
                        DeliverToTelegram(conversationId, content);
                        break;
                    case "EMAIL":
                        // สะพานแชท↔อีเมลลูกค้า OTA (Agoda/Booking relay) — ตอบเป็นอีเมลเบื้องหลัง
                        new EmailChatService(_connStr).DeliverToEmail(conversationId, content);
                        break;
                    case "TIKTOK":
                        DeliverToTikTok(conversationId, content);
                        break;
                    // WEBCHAT ไม่มี outbound push — ลูกค้าดึงคำตอบผ่าน endpoint เอง (polling/หลังส่ง)
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("OmniChannel.DeliverOutbound [{0}]: {1}", channelCode, ex.Message);
                // มาร์คให้ตรงกับความจริง — ไม่งั้นข้อความค้างสถานะ SENT ทั้งที่ไม่เคยออกจากระบบ
                MarkLastOutboundStatus(conversationId, "FAILED");
                _code.Logs(_connStr, "OmniChannel",
                    $"ส่งออกช่องทาง {channelCode} ไม่สำเร็จ (conv {conversationId}): {ex.Message}", "SYSTEM");
            }
        }

        /// <summary>สถานะส่งออกของข้อความขาออกล่าสุด — ใช้บอกผู้ใช้ว่าถึงลูกค้าจริงไหม</summary>
        private string ReadLastOutboundStatus(long conversationId)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_connStr,
                    @"SELECT TOP 1 DeliveryStatus FROM OmniChannel_Messages
                      WHERE ConversationID = @Id AND Direction = 'OUT' ORDER BY ID DESC",
                    new Dictionary<string, object> { { "@Id", conversationId } });
                return dt?.Rows.Count > 0 ? dt.Rows[0][0]?.ToString() : null;
            }
            catch { return null; }
        }

        private void MarkLastOutboundStatus(long conversationId, string status)
        {
            try
            {
                _code.DatabaseInsertSafe(_connStr,
                    @"UPDATE OmniChannel_Messages SET DeliveryStatus = @S
                      WHERE ID = (SELECT MAX(ID) FROM OmniChannel_Messages
                                  WHERE ConversationID = @Id AND Direction = 'OUT')",
                    new Dictionary<string, object> { { "@S", status }, { "@Id", conversationId } });
            }
            catch { }
        }

        private void DeliverToLine(long conversationId, string content)
        {
            var config = GetChannelConfig("LINE");
            string token = config.ContainsKey("channelAccessToken") ? config["channelAccessToken"]?.ToString() : "";
            if (string.IsNullOrEmpty(token)) return;

            string userId = GetPlatformUserId(conversationId);
            if (string.IsNullOrEmpty(userId)) return;

            var body = new
            {
                to = userId,
                messages = new[] { new { type = "text", text = content } }
            };

            PostJson("https://api.line.me/v2/bot/message/push", new JavaScriptSerializer().Serialize(body),
                new Dictionary<string, string> { { "Authorization", "Bearer " + token } });
        }

        private void DeliverToFacebook(long conversationId, string content)
        {
            var config = GetChannelConfig("FACEBOOK");
            string pageToken = config.ContainsKey("pageAccessToken") ? config["pageAccessToken"]?.ToString() : "";
            if (string.IsNullOrEmpty(pageToken)) return;

            string recipientId = GetPlatformUserId(conversationId);
            if (string.IsNullOrEmpty(recipientId)) return;

            var body = new
            {
                recipient = new { id = recipientId },
                message = new { text = content }
            };

            PostJson("https://graph.facebook.com/v18.0/me/messages?access_token=" + pageToken,
                new JavaScriptSerializer().Serialize(body), null);
        }

        private void DeliverToWhatsApp(long conversationId, string content)
        {
            var config = GetChannelConfig("WHATSAPP");
            string token = config.ContainsKey("accessToken") ? config["accessToken"]?.ToString() : "";
            string phoneNumberId = config.ContainsKey("phoneNumberId") ? config["phoneNumberId"]?.ToString() : "";
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(phoneNumberId)) return;

            string recipientPhone = GetPlatformUserId(conversationId);
            if (string.IsNullOrEmpty(recipientPhone)) return;

            var body = new
            {
                messaging_product = "whatsapp",
                to = recipientPhone,
                type = "text",
                text = new { body = content }
            };

            PostJson("https://graph.facebook.com/v18.0/" + phoneNumberId + "/messages",
                new JavaScriptSerializer().Serialize(body),
                new Dictionary<string, string> { { "Authorization", "Bearer " + token } });
        }

        private void DeliverToTelegram(long conversationId, string content)
        {
            var config = GetChannelConfig("TELEGRAM");
            string botToken = config.ContainsKey("botToken") ? config["botToken"]?.ToString() : "";
            if (string.IsNullOrEmpty(botToken)) return;

            string chatId = GetPlatformUserId(conversationId);
            if (string.IsNullOrEmpty(chatId)) return;

            var body = new { chat_id = chatId, text = content };
            PostJson("https://api.telegram.org/bot" + botToken + "/sendMessage",
                new JavaScriptSerializer().Serialize(body), null);
        }

        /// <summary>
        /// ส่งข้อความออกทาง TikTok Business Messaging.
        /// TikTok DM API ยังจำกัดสิทธิ์ (ต้องได้รับอนุมัติ scope + business account) และรูปแบบ
        /// endpoint ต่างกันตามรุ่นที่ได้รับอนุมัติ → ให้ตั้ง sendUrl + accessToken เองในหน้าตั้งค่า
        /// ช่องทาง (ถ้าไม่ตั้ง = รับข้อความเข้ากล่องแชทได้ แต่ตอบต้องพนักงานตอบผ่านแอป TikTok)
        /// </summary>
        private void DeliverToTikTok(long conversationId, string content)
        {
            var config = GetChannelConfig("TIKTOK");
            string token = config.ContainsKey("accessToken") ? config["accessToken"]?.ToString() : "";
            string sendUrl = config.ContainsKey("sendUrl") ? config["sendUrl"]?.ToString() : "";
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(sendUrl)) return;

            string recipientId = GetPlatformUserId(conversationId);
            if (string.IsNullOrEmpty(recipientId)) return;

            var body = new { recipient_id = recipientId, message = new { text = content } };
            PostJson(sendUrl, new JavaScriptSerializer().Serialize(body),
                new Dictionary<string, string> { { "Authorization", "Bearer " + token } });
        }

        private string GetPlatformUserId(long conversationId)
        {
            DataTable dt = _code.DatabaseQuerySafe(_connStr,
                @"SELECT ci.PlatformUserId
                  FROM OmniChannel_Conversations c
                  JOIN OmniChannel_Contact_Identifiers ci ON ci.ContactID = c.ContactID AND ci.ChannelCode = c.ChannelCode
                  WHERE c.ID = @Id",
                new Dictionary<string, object> { { "@Id", conversationId } });
            return dt.Rows.Count > 0 ? dt.Rows[0]["PlatformUserId"].ToString() : null;
        }

        private void PostJson(string url, string json, Dictionary<string, string> headers)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.Timeout = 15000;
            if (headers != null)
                foreach (var h in headers) request.Headers.Add(h.Key, h.Value);

            byte[] data = Encoding.UTF8.GetBytes(json);
            request.ContentLength = data.Length;
            using (var stream = request.GetRequestStream())
                stream.Write(data, 0, data.Length);

            using (var response = (HttpWebResponse)request.GetResponse())
            using (var reader = new StreamReader(response.GetResponseStream()))
                reader.ReadToEnd();
        }

        #endregion

        #region Canned Responses

        public DataTable GetCannedResponses(string category = null)
        {
            if (!string.IsNullOrEmpty(category))
                return _code.DatabaseQuerySafe(_connStr,
                    "SELECT * FROM OmniChannel_CannedResponses WHERE IsActive = 1 AND Category = @Cat ORDER BY UsageCount DESC",
                    new Dictionary<string, object> { { "@Cat", category } });

            return _code.DatabaseQuerySafe(_connStr,
                "SELECT * FROM OmniChannel_CannedResponses WHERE IsActive = 1 ORDER BY UsageCount DESC", null);
        }

        public void IncrementCannedUsage(int id)
        {
            _code.DatabaseInsertSafe(_connStr,
                "UPDATE OmniChannel_CannedResponses SET UsageCount = UsageCount + 1 WHERE ID = @Id",
                new Dictionary<string, object> { { "@Id", id } });
        }

        #endregion

        #region Booking Confirmation Delivery

        public void SendBookingConfirmation(long conversationId, int reservationId)
        {
            try
            {
                DataTable dtConv = GetConversationDetail(conversationId);
                if (dtConv.Rows.Count == 0) return;

                string channelCode = dtConv.Rows[0]["ChannelCode"].ToString();
                var aiSvc = new AIKnowledgeService(_connStr);

                if (channelCode == "LINE")
                {
                    string flexJson = aiSvc.GenerateLineFlexConfirmation(reservationId);
                    if (!string.IsNullOrEmpty(flexJson))
                    {
                        DeliverLineFlexMessage(conversationId, flexJson, "Booking Confirmation #" + reservationId);

                        string textConfirm = aiSvc.GenerateBookingConfirmationText(reservationId);
                        _code.DatabaseInsertSafe(_connStr,
                            @"INSERT INTO OmniChannel_Messages (ConversationID, Direction, SenderName, MessageType, Content, IsRead, DeliveryStatus, IsAIGenerated, AISource, Created_Date)
                              VALUES (@ConvId, 'OUT', N'AI Assistant', 'TEXT', @Content, 1, 'SENT', 1, 'BOOKING_CONFIRM', GETDATE())",
                            new Dictionary<string, object>
                            {
                                { "@ConvId", conversationId },
                                { "@Content", textConfirm }
                            });

                        _code.DatabaseInsertSafe(_connStr,
                            "UPDATE OmniChannel_Conversations SET LastMessageDate = GETDATE(), LastMessagePreview = N'[ยืนยันการจอง #" + reservationId + "]', Updated_Date = GETDATE() WHERE ID = @Id",
                            new Dictionary<string, object> { { "@Id", conversationId } });
                        return;
                    }
                }

                string confirmation = aiSvc.GenerateBookingConfirmationText(reservationId);
                if (!string.IsNullOrEmpty(confirmation))
                    SendMessage(conversationId, confirmation, "AI Assistant", isAI: true, aiSource: "BOOKING_CONFIRM");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("SendBookingConfirmation error: {0}", ex.Message);
            }
        }

        public void SendBookingStatusUpdate(long conversationId, int reservationId, string newStatus, string staffName)
        {
            try
            {
                var aiSvc = new AIKnowledgeService(_connStr);
                DataTable dtConv = GetConversationDetail(conversationId);
                if (dtConv.Rows.Count == 0) return;

                string channelCode = dtConv.Rows[0]["ChannelCode"].ToString();
                string statusEmoji = newStatus == "ยืนยันแล้ว" ? "✅" : "📋";
                string headerMsg = statusEmoji + " อัพเดทสถานะการจอง #" + reservationId + "\nสถานะ: " + newStatus;
                if (!string.IsNullOrEmpty(staffName))
                    headerMsg += "\nโดย: " + staffName;

                if (channelCode == "LINE")
                {
                    string flexJson = aiSvc.GenerateLineFlexConfirmation(reservationId);
                    if (!string.IsNullOrEmpty(flexJson))
                    {
                        DeliverLineFlexMessage(conversationId, flexJson, "Booking #" + reservationId + " - " + newStatus);

                        _code.DatabaseInsertSafe(_connStr,
                            @"INSERT INTO OmniChannel_Messages (ConversationID, Direction, SenderName, MessageType, Content, IsRead, DeliveryStatus, IsAIGenerated, AISource, Created_Date)
                              VALUES (@ConvId, 'OUT', N'AI Assistant', 'TEXT', @Content, 1, 'SENT', 1, 'BOOKING_STATUS', GETDATE())",
                            new Dictionary<string, object>
                            {
                                { "@ConvId", conversationId },
                                { "@Content", headerMsg + "\n\n" + aiSvc.GenerateBookingConfirmationText(reservationId) }
                            });

                        _code.DatabaseInsertSafe(_connStr,
                            "UPDATE OmniChannel_Conversations SET LastMessageDate = GETDATE(), LastMessagePreview = @Preview, Updated_Date = GETDATE() WHERE ID = @Id",
                            new Dictionary<string, object> { { "@Id", conversationId }, { "@Preview", "[อัพเดทสถานะจอง #" + reservationId + "]" } });
                        return;
                    }
                }

                string confirmation = aiSvc.GenerateBookingConfirmationText(reservationId);
                string fullMessage = headerMsg + "\n\n" + confirmation;
                SendMessage(conversationId, fullMessage, "AI Assistant", isAI: true, aiSource: "BOOKING_STATUS");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("SendBookingStatusUpdate error: {0}", ex.Message);
            }
        }

        private void DeliverLineFlexMessage(long conversationId, string flexJson, string altText)
        {
            var config = GetChannelConfig("LINE");
            string token = config.ContainsKey("channelAccessToken") ? config["channelAccessToken"]?.ToString() : "";
            if (string.IsNullOrEmpty(token)) return;

            string userId = GetPlatformUserId(conversationId);
            if (string.IsNullOrEmpty(userId)) return;

            string body = "{\"to\":\"" + userId + "\",\"messages\":[{\"type\":\"flex\",\"altText\":\"" +
                altText.Replace("\"", "\\\"") + "\",\"contents\":" + flexJson + "}]}";

            PostJson("https://api.line.me/v2/bot/message/push", body,
                new Dictionary<string, string> { { "Authorization", "Bearer " + token } });
        }

        #endregion

        #region Contact Management

        public void LinkContactToCustomer(long contactId, string customerPhone)
        {
            _code.DatabaseInsertSafe(_connStr,
                "UPDATE OmniChannel_Contacts SET Customer_MobilePhone = @Phone, Updated_Date = GETDATE() WHERE ID = @Id",
                new Dictionary<string, object> { { "@Id", contactId }, { "@Phone", customerPhone } });
        }

        #endregion
    }

    public class IncomingMessageResult
    {
        public bool Success { get; set; }
        public long MessageID { get; set; }
        public long ConversationID { get; set; }
        public long ContactID { get; set; }
        public string Error { get; set; }
    }

    public class SendMessageResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }

        /// <summary>สถานะส่งออกจริง (SENT / FAILED) — บันทึกได้เสมอ ไม่ได้แปลว่าส่งถึงลูกค้า</summary>
        public string DeliveryStatus { get; set; }

        /// <summary>ข้อความเตือนเมื่อบันทึกได้แต่ส่งออกไม่สำเร็จ (null = ปกติ)</summary>
        public string Warning { get; set; }
    }
}
