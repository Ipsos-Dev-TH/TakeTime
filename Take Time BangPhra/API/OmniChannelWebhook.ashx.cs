using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web;
using System.Web.Script.Serialization;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.API
{
    public class OmniChannelWebhook : IHttpHandler
    {
        private static string ConnStr => ConfigurationManager.ConnectionStrings["TaketimeConnectionString"]?.ConnectionString ?? "";

        public void ProcessRequest(HttpContext context)
        {
            context.Response.ContentType = "application/json";
            string channel = context.Request.QueryString["channel"]?.ToUpper() ?? "";

            try
            {
                if (context.Request.HttpMethod == "GET")
                {
                    HandleVerification(context, channel);
                    return;
                }

                string body;
                using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
                    body = reader.ReadToEnd();

                if (string.IsNullOrEmpty(body))
                {
                    context.Response.Write("{\"status\":\"empty\"}");
                    return;
                }

                switch (channel)
                {
                    case "LINE":
                        HandleLine(context, body);
                        break;
                    case "FACEBOOK":
                        HandleFacebook(context, body);
                        break;
                    case "WHATSAPP":
                        HandleWhatsApp(context, body);
                        break;
                    case "TELEGRAM":
                        HandleTelegram(context, body);
                        break;
                    case "WECHAT":
                        HandleWeChat(context, body);
                        break;
                    case "TIKTOK":
                        // TikTok Messaging ส่ง event เป็น JSON POST → ใช้ตัวรับทั่วไป
                        // (senderId / senderName / message / messageId) + auto-reply
                        HandleGeneric(context, body, "TIKTOK");
                        break;
                    default:
                        HandleGeneric(context, body, channel);
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("OmniChannelWebhook [{0}]: {1}", channel, ex.Message);
                context.Response.StatusCode = 200;
                context.Response.Write("{\"status\":\"error\"}");
            }
        }

        private void HandleVerification(HttpContext context, string channel)
        {
            if (channel == "FACEBOOK")
            {
                string mode = context.Request.QueryString["hub.mode"];
                string token = context.Request.QueryString["hub.verify_token"];
                string challenge = context.Request.QueryString["hub.challenge"];

                var svc = new OmniChannelService(ConnStr);
                var config = svc.GetChannelConfig("FACEBOOK");
                string verifyToken = config.ContainsKey("verifyToken") ? config["verifyToken"]?.ToString() : "";

                if (mode == "subscribe" && token == verifyToken)
                {
                    context.Response.ContentType = "text/plain";
                    context.Response.Write(challenge);
                    return;
                }
                context.Response.StatusCode = 403;
                context.Response.Write("Verification failed");
                return;
            }

            context.Response.Write("{\"status\":\"ok\"}");
        }

        #region LINE

        private void HandleLine(HttpContext context, string body)
        {
            var svc = new OmniChannelService(ConnStr);
            var config = svc.GetChannelConfig("LINE");
            string channelSecret = config.ContainsKey("channelSecret") ? config["channelSecret"]?.ToString() : "";

            if (!string.IsNullOrEmpty(channelSecret))
            {
                string signature = context.Request.Headers["X-Line-Signature"];
                if (!VerifyLineSignature(body, channelSecret, signature))
                {
                    context.Response.StatusCode = 400;
                    context.Response.Write("{\"error\":\"invalid signature\"}");
                    return;
                }
            }

            var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(body);
            var events = data.ContainsKey("events") ? data["events"] as ArrayList : null;
            if (events == null) { context.Response.Write("{\"status\":\"ok\"}"); return; }

            foreach (Dictionary<string, object> evt in events)
            {
                string type = evt.ContainsKey("type") ? evt["type"]?.ToString() : "";
                if (type != "message") continue;

                var source = evt.ContainsKey("source") ? evt["source"] as Dictionary<string, object> : null;
                var message = evt.ContainsKey("message") ? evt["message"] as Dictionary<string, object> : null;
                if (source == null || message == null) continue;

                string userId = source.ContainsKey("userId") ? source["userId"]?.ToString() : "";
                string msgType = message.ContainsKey("type") ? message["type"]?.ToString() : "text";
                string text = message.ContainsKey("text") ? message["text"]?.ToString() : "";
                string msgId = message.ContainsKey("id") ? message["id"]?.ToString() : "";

                if (string.IsNullOrEmpty(userId)) continue;

                string displayName = GetLineDisplayName(userId, config);

                if (msgType == "text")
                {
                    var inRes = svc.ReceiveMessage("LINE", userId, displayName, text,
                        platformMessageId: msgId, displayName: displayName);
                    TryAutoReply(svc, inRes, "LINE", text, null);
                }
                else if (msgType == "image" || msgType == "video" || msgType == "audio" || msgType == "file")
                {
                    svc.ReceiveMessage("LINE", userId, displayName,
                        "[" + msgType + "]", msgType.ToUpper(), platformMessageId: msgId, displayName: displayName);
                }
                else if (msgType == "sticker")
                {
                    string stickerId = message.ContainsKey("stickerId") ? message["stickerId"]?.ToString() : "";
                    svc.ReceiveMessage("LINE", userId, displayName,
                        "[Sticker: " + stickerId + "]", "STICKER", platformMessageId: msgId, displayName: displayName);
                }
            }

            context.Response.Write("{\"status\":\"ok\"}");
        }

        private string GetLineDisplayName(string userId, Dictionary<string, object> config)
        {
            try
            {
                string token = config.ContainsKey("channelAccessToken") ? config["channelAccessToken"]?.ToString() : "";
                if (string.IsNullOrEmpty(token)) return userId;

                var req = (System.Net.HttpWebRequest)System.Net.WebRequest.Create("https://api.line.me/v2/bot/profile/" + userId);
                req.Headers.Add("Authorization", "Bearer " + token);
                req.Timeout = 5000;

                using (var resp = req.GetResponse())
                using (var reader = new StreamReader(resp.GetResponseStream()))
                {
                    var profile = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(reader.ReadToEnd());
                    return profile.ContainsKey("displayName") ? profile["displayName"]?.ToString() : userId;
                }
            }
            catch { return userId; }
        }

        private bool VerifyLineSignature(string body, string channelSecret, string signature)
        {
            if (string.IsNullOrEmpty(signature)) return false;
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(channelSecret)))
            {
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
                string computed = Convert.ToBase64String(hash);
                return computed == signature;
            }
        }

        #endregion

        #region Facebook Messenger

        private void HandleFacebook(HttpContext context, string body)
        {
            var svc = new OmniChannelService(ConnStr);
            var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(body);

            var entries = data.ContainsKey("entry") ? data["entry"] as ArrayList : null;
            if (entries == null) { context.Response.Write("{\"status\":\"ok\"}"); return; }

            foreach (Dictionary<string, object> entry in entries)
            {
                var messaging = entry.ContainsKey("messaging") ? entry["messaging"] as ArrayList : null;
                if (messaging == null) continue;

                foreach (Dictionary<string, object> msg in messaging)
                {
                    var senderObj = msg.ContainsKey("sender") ? msg["sender"] as Dictionary<string, object> : null;
                    var messageObj = msg.ContainsKey("message") ? msg["message"] as Dictionary<string, object> : null;
                    if (senderObj == null || messageObj == null) continue;

                    string senderId = senderObj.ContainsKey("id") ? senderObj["id"]?.ToString() : "";
                    string text = messageObj.ContainsKey("text") ? messageObj["text"]?.ToString() : "";
                    string msgId = messageObj.ContainsKey("mid") ? messageObj["mid"]?.ToString() : "";

                    if (string.IsNullOrEmpty(senderId) || string.IsNullOrEmpty(text)) continue;

                    var fbRes = svc.ReceiveMessage("FACEBOOK", senderId, null, text, platformMessageId: msgId);
                    TryAutoReply(svc, fbRes, "FACEBOOK", text, null);

                    var attachments = messageObj.ContainsKey("attachments") ? messageObj["attachments"] as ArrayList : null;
                    if (attachments != null)
                    {
                        foreach (Dictionary<string, object> att in attachments)
                        {
                            string attType = att.ContainsKey("type") ? att["type"]?.ToString() : "";
                            var payload = att.ContainsKey("payload") ? att["payload"] as Dictionary<string, object> : null;
                            string url = payload != null && payload.ContainsKey("url") ? payload["url"]?.ToString() : "";
                            if (!string.IsNullOrEmpty(url))
                                svc.ReceiveMessage("FACEBOOK", senderId, null, "[" + attType + "]",
                                    attType.ToUpper(), mediaUrl: url, platformMessageId: msgId);
                        }
                    }
                }
            }

            context.Response.Write("{\"status\":\"ok\"}");
        }

        #endregion

        #region WhatsApp (Cloud API)

        private void HandleWhatsApp(HttpContext context, string body)
        {
            var svc = new OmniChannelService(ConnStr);
            var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(body);

            var entries = data.ContainsKey("entry") ? data["entry"] as ArrayList : null;
            if (entries == null) { context.Response.Write("{\"status\":\"ok\"}"); return; }

            foreach (Dictionary<string, object> entry in entries)
            {
                var changes = entry.ContainsKey("changes") ? entry["changes"] as ArrayList : null;
                if (changes == null) continue;

                foreach (Dictionary<string, object> change in changes)
                {
                    var value = change.ContainsKey("value") ? change["value"] as Dictionary<string, object> : null;
                    if (value == null) continue;

                    var messages = value.ContainsKey("messages") ? value["messages"] as ArrayList : null;
                    var contacts = value.ContainsKey("contacts") ? value["contacts"] as ArrayList : null;
                    if (messages == null) continue;

                    foreach (Dictionary<string, object> msg in messages)
                    {
                        string from = msg.ContainsKey("from") ? msg["from"]?.ToString() : "";
                        string msgType = msg.ContainsKey("type") ? msg["type"]?.ToString() : "text";
                        string msgId = msg.ContainsKey("id") ? msg["id"]?.ToString() : "";

                        string displayName = null;
                        if (contacts != null && contacts.Count > 0)
                        {
                            var contact = contacts[0] as Dictionary<string, object>;
                            var profile = contact?.ContainsKey("profile") == true ? contact["profile"] as Dictionary<string, object> : null;
                            displayName = profile?.ContainsKey("name") == true ? profile["name"]?.ToString() : null;
                        }

                        if (msgType == "text")
                        {
                            var textObj = msg.ContainsKey("text") ? msg["text"] as Dictionary<string, object> : null;
                            string text = textObj?.ContainsKey("body") == true ? textObj["body"]?.ToString() : "";
                            var waRes = svc.ReceiveMessage("WHATSAPP", from, displayName, text,
                                platformMessageId: msgId, displayName: displayName);
                            TryAutoReply(svc, waRes, "WHATSAPP", text, from);
                        }
                        else
                        {
                            svc.ReceiveMessage("WHATSAPP", from, displayName,
                                "[" + msgType + "]", msgType.ToUpper(),
                                platformMessageId: msgId, displayName: displayName);
                        }
                    }
                }
            }

            context.Response.Write("{\"status\":\"ok\"}");
        }

        #endregion

        #region Telegram

        private void HandleTelegram(HttpContext context, string body)
        {
            var svc = new OmniChannelService(ConnStr);
            var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(body);

            var message = data.ContainsKey("message") ? data["message"] as Dictionary<string, object> : null;
            if (message == null) { context.Response.Write("{\"status\":\"ok\"}"); return; }

            var from = message.ContainsKey("from") ? message["from"] as Dictionary<string, object> : null;
            string chatId = "";
            if (message.ContainsKey("chat"))
            {
                var chat = message["chat"] as Dictionary<string, object>;
                chatId = chat?.ContainsKey("id") == true ? chat["id"]?.ToString() : "";
            }
            string text = message.ContainsKey("text") ? message["text"]?.ToString() : "";
            string firstName = from?.ContainsKey("first_name") == true ? from["first_name"]?.ToString() : "";
            string lastName = from?.ContainsKey("last_name") == true ? from["last_name"]?.ToString() : "";
            string displayName = (firstName + " " + lastName).Trim();

            if (!string.IsNullOrEmpty(chatId) && !string.IsNullOrEmpty(text))
            {
                var tgRes = svc.ReceiveMessage("TELEGRAM", chatId, displayName, text, displayName: displayName);
                TryAutoReply(svc, tgRes, "TELEGRAM", text, null);
            }

            context.Response.Write("{\"status\":\"ok\"}");
        }

        #endregion

        #region WeChat

        private void HandleWeChat(HttpContext context, string body)
        {
            var svc = new OmniChannelService(ConnStr);
            svc.ReceiveMessage("WECHAT", "unknown", null, body, "RAW");
            context.Response.Write("{\"status\":\"ok\"}");
        }

        #endregion

        #region Generic (OTA and others)

        private void HandleGeneric(HttpContext context, string body, string channel)
        {
            if (string.IsNullOrEmpty(channel))
            {
                context.Response.StatusCode = 400;
                context.Response.Write("{\"error\":\"missing channel parameter\"}");
                return;
            }

            var svc = new OmniChannelService(ConnStr);

            try
            {
                var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(body);
                string senderId = data.ContainsKey("senderId") ? data["senderId"]?.ToString() : "unknown";
                string senderName = data.ContainsKey("senderName") ? data["senderName"]?.ToString() : null;
                string content = data.ContainsKey("message") ? data["message"]?.ToString() : body;
                string msgId = data.ContainsKey("messageId") ? data["messageId"]?.ToString() : null;

                var genRes = svc.ReceiveMessage(channel, senderId, senderName, content,
                    platformMessageId: msgId, displayName: senderName, metadata: body);
                TryAutoReply(svc, genRes, channel, content, null);
            }
            catch
            {
                svc.ReceiveMessage(channel, "unknown", null, body, "RAW", metadata: body);
            }

            context.Response.Write("{\"status\":\"ok\"}");
        }

        #endregion

        #region AI Auto-Reply

        private void TryAutoReply(OmniChannelService omniSvc, IncomingMessageResult inResult, string channelCode, string text, string customerPhone)
        {
            if (!inResult.Success || string.IsNullOrEmpty(text) || text.StartsWith("[")) return;

            try
            {
                var aiSvc = new AIKnowledgeService(ConnStr);
                if (!aiSvc.IsFeatureEnabled("AUTO_REPLY")) return;

                var reply = aiSvc.ProcessAutoReply(text, channelCode, customerPhone, inResult.ConversationID);
                if (!reply.Success || string.IsNullOrEmpty(reply.Reply)) return;

                omniSvc.SendMessage(inResult.ConversationID, reply.Reply, "AI Assistant", isAI: true,
                    aiConfidence: reply.Confidence, aiSource: reply.Source);

                if (reply.BookingData != null && reply.BookingData.ContainsKey("reservationId"))
                {
                    int resId = Convert.ToInt32(reply.BookingData["reservationId"]);
                    omniSvc.SendBookingConfirmation(inResult.ConversationID, resId);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("AI AutoReply error [{0}]: {1}", channelCode, ex.Message);
            }
        }

        #endregion

        public bool IsReusable => false;
    }
}
