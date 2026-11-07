using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using System.Web.Script.Serialization;

namespace Take_Time_BangPhra.Services
{
    /// <summary>
    /// LINE Login (OAuth 2.0) สำหรับผูกบัญชี LINE ของ Admin แต่ละคน → เก็บ userId
    /// ไว้ส่งข้อความแจ้งเตือนเข้าไลน์ส่วนตัวรายคน (ไม่ต้องยิงเข้ากลุ่มรวม)
    ///
    /// ⚠️ LINE ออก userId แยกตาม provider — LINE Login channel ต้องอยู่ provider เดียวกับ
    /// Messaging API channel ที่ใช้ push ไม่งั้น userId ที่ได้จะ push ไม่ถึง
    /// และผู้ใช้ต้องเพิ่ม LINE OA เป็นเพื่อนก่อน ระบบถึงส่งหาได้
    /// </summary>
    public class LineLoginService
    {
        private const string AUTH_URL = "https://access.line.me/oauth2/v2.1/authorize";
        private const string TOKEN_URL = "https://api.line.me/oauth2/v2.1/token";
        private const string PROFILE_URL = "https://api.line.me/v2/profile";
        private const string PUSH_URL = "https://api.line.me/v2/bot/message/push";

        private readonly string _conn;
        private readonly code _code = new code();

        public LineLoginService(string connectionString) { _conn = connectionString; }

        // ── config ────────────────────────────────────────────────────────────────
        public bool IsEnabled => Cfg("LineLogin_Enabled", "0") == "1";
        public string ChannelId => Cfg("LineLogin_ChannelId", "");
        public string ChannelSecret => _code.Derypt(Cfg("LineLogin_ChannelSecret_Encrypted", ""));
        public string CallbackUrl => Cfg("LineLogin_CallbackUrl", "");
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(ChannelId) && !string.IsNullOrWhiteSpace(ChannelSecret)
            && !string.IsNullOrWhiteSpace(CallbackUrl);

        /// <summary>URL ให้ผู้ใช้กดเพื่อเริ่มผูกบัญชี — state กัน CSRF (ผู้เรียกเก็บลง Session)</summary>
        public string BuildAuthorizeUrl(string state)
        {
            return AUTH_URL
                + "?response_type=code"
                + "&client_id=" + HttpUtility.UrlEncode(ChannelId)
                + "&redirect_uri=" + HttpUtility.UrlEncode(CallbackUrl)
                + "&state=" + HttpUtility.UrlEncode(state)
                + "&scope=" + HttpUtility.UrlEncode("profile openid")
                + "&bot_prompt=aggressive";   // ชวนเพิ่ม LINE OA เป็นเพื่อน (จำเป็นต่อการ push)
        }

        public class LinkResult
        {
            public bool Success;
            public string UserId, DisplayName, PictureUrl, Message;
            /// <summary>access token ของรอบนี้ — ใช้ตรวจสถานะเพื่อนกับ LINE OA</summary>
            public string AccessToken;
            /// <summary>true = ผู้ใช้เพิ่ม LINE OA เป็นเพื่อนแล้ว (จำเป็นต่อการส่งข้อความหา)</summary>
            public bool IsFriend;
        }

        /// <summary>บังคับให้ต้องเพิ่ม LINE OA เป็นเพื่อนก่อนใช้งาน (default เปิด)</summary>
        public bool RequireFriend => Cfg("LineLogin_RequireFriend", "1") == "1";

        /// <summary>Basic ID ของ LINE OA เช่น @taketime — ใช้ทำลิงก์/QR เพิ่มเพื่อน</summary>
        public string BotBasicId => Cfg("LineLogin_BotBasicId", "");

        /// <summary>ลิงก์เพิ่มเพื่อน (บนมือถือจะเปิดแอป LINE ให้เลย)</summary>
        public string AddFriendUrl
        {
            get
            {
                string id = (BotBasicId ?? "").Trim();
                if (string.IsNullOrEmpty(id)) return "";
                if (!id.StartsWith("@")) id = "@" + id;
                return "https://line.me/R/ti/p/" + Uri.EscapeDataString(id);
            }
        }

        /// <summary>QR เพิ่มเพื่อน (LINE สร้างให้จาก basic id)</summary>
        public string AddFriendQrUrl
        {
            get
            {
                string id = (BotBasicId ?? "").Trim().TrimStart('@');
                return string.IsNullOrEmpty(id) ? "" : $"https://qr-official.line.me/gs/M_{id}_GW.png";
            }
        }

        /// <summary>
        /// ตรวจว่าผู้ใช้เพิ่ม LINE OA (ที่ผูกกับ Login channel) เป็นเพื่อนแล้วหรือยัง
        /// LINE ไม่มีวิธี "บังคับ" เพิ่มเพื่อน — ทำได้แค่ชวน (bot_prompt) แล้วตรวจผลด้วย API นี้
        /// แล้วให้ระบบเราไม่ปล่อยผ่านจนกว่าจะเพิ่มจริง
        /// </summary>
        public bool CheckIsFriend(string accessToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(accessToken)) return false;
                string json = GetWithBearer("https://api.line.me/friendship/v1/status", accessToken);
                var r = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
                return r != null && r.ContainsKey("friendFlag")
                       && r["friendFlag"]?.ToString().Equals("true", StringComparison.OrdinalIgnoreCase) == true;
            }
            catch
            {
                // เรียกไม่ได้ (Login channel ยังไม่ผูกกับ bot / เน็ตมีปัญหา) → ถือว่าเป็นเพื่อน
                // เพื่อไม่ให้ผู้ใช้ติดค้างเข้าระบบไม่ได้เพราะเรื่องที่ตรวจไม่ได้
                return true;
            }
        }

        /// <summary>หา Admin จาก LINE userId — ใช้ "เข้าสู่ระบบด้วย LINE" ตอนกดลิงก์จากแชท</summary>
        public DataRow FindAdminByLineUserId(string lineUserId)
        {
            if (string.IsNullOrWhiteSpace(lineUserId)) return null;
            var dt = _code.DatabaseQuerySafe(_conn,
                @"SELECT TOP 1 ID, Username, Role, Line_UserId, Line_DisplayName
                    FROM [dbo].[Admin]
                   WHERE Line_UserId = @uid AND Status = 1",
                new Dictionary<string, object> { { "@uid", lineUserId } });
            return dt?.Rows.Count > 0 ? dt.Rows[0] : null;
        }

        /// <summary>แลก code → userId อย่างเดียว (ไม่ผูกบัญชี) สำหรับ flow เข้าสู่ระบบด้วย LINE</summary>
        public LinkResult ResolveProfile(string code)
        {
            var res = new LinkResult();
            try
            {
                if (!IsConfigured) { res.Message = "ยังไม่ได้ตั้งค่า LINE Login"; return res; }
                if (string.IsNullOrWhiteSpace(code)) { res.Message = "ไม่ได้รับรหัสยืนยันจาก LINE"; return res; }

                string body = "grant_type=authorization_code"
                            + "&code=" + HttpUtility.UrlEncode(code)
                            + "&redirect_uri=" + HttpUtility.UrlEncode(CallbackUrl)
                            + "&client_id=" + HttpUtility.UrlEncode(ChannelId)
                            + "&client_secret=" + HttpUtility.UrlEncode(ChannelSecret);

                var token = new JavaScriptSerializer()
                    .Deserialize<Dictionary<string, object>>(PostForm(TOKEN_URL, body));
                if (token == null || !token.ContainsKey("access_token"))
                { res.Message = "แลก token ไม่สำเร็จ"; return res; }

                var profile = new JavaScriptSerializer()
                    .Deserialize<Dictionary<string, object>>(GetWithBearer(PROFILE_URL, token["access_token"].ToString()));
                if (profile == null || !profile.ContainsKey("userId"))
                { res.Message = "อ่านโปรไฟล์ LINE ไม่สำเร็จ"; return res; }

                res.UserId = profile["userId"].ToString();
                res.DisplayName = profile.ContainsKey("displayName") ? profile["displayName"]?.ToString() : "";
                res.PictureUrl = profile.ContainsKey("pictureUrl") ? profile["pictureUrl"]?.ToString() : "";
                res.AccessToken = token["access_token"].ToString();
                res.IsFriend = CheckIsFriend(res.AccessToken);
                res.Success = true;
                return res;
            }
            catch (Exception ex) { res.Message = ex.Message; return res; }
        }

        /// <summary>แลก code → access token → โปรไฟล์ แล้วผูกกับ Admin ที่ล็อกอินอยู่</summary>
        public LinkResult HandleCallback(string code, int adminId)
        {
            var res = new LinkResult();
            try
            {
                if (!IsConfigured) { res.Message = "ยังไม่ได้ตั้งค่า LINE Login (Channel ID/Secret/Callback)"; return res; }
                if (string.IsNullOrWhiteSpace(code)) { res.Message = "ไม่ได้รับรหัสยืนยันจาก LINE"; return res; }
                if (adminId <= 0) { res.Message = "ไม่พบผู้ใช้ที่ล็อกอินอยู่"; return res; }

                // 1) code → access_token
                string body = "grant_type=authorization_code"
                            + "&code=" + HttpUtility.UrlEncode(code)
                            + "&redirect_uri=" + HttpUtility.UrlEncode(CallbackUrl)
                            + "&client_id=" + HttpUtility.UrlEncode(ChannelId)
                            + "&client_secret=" + HttpUtility.UrlEncode(ChannelSecret);

                string tokenJson = PostForm(TOKEN_URL, body);
                var token = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(tokenJson);
                if (token == null || !token.ContainsKey("access_token"))
                { res.Message = "แลก token ไม่สำเร็จ: " + Trim(tokenJson, 200); return res; }

                string accessToken = token["access_token"].ToString();

                // 2) access_token → โปรไฟล์ (userId)
                string profileJson = GetWithBearer(PROFILE_URL, accessToken);
                var profile = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(profileJson);
                if (profile == null || !profile.ContainsKey("userId"))
                { res.Message = "อ่านโปรไฟล์ LINE ไม่สำเร็จ: " + Trim(profileJson, 200); return res; }

                res.UserId = profile["userId"].ToString();
                res.DisplayName = profile.ContainsKey("displayName") ? profile["displayName"]?.ToString() : "";
                res.PictureUrl = profile.ContainsKey("pictureUrl") ? profile["pictureUrl"]?.ToString() : "";
                res.AccessToken = accessToken;
                res.IsFriend = CheckIsFriend(accessToken);

                // 3) กัน userId ซ้ำกับ Admin คนอื่น (1 บัญชี LINE = 1 คน)
                var dup = _code.DatabaseQuerySafe(_conn,
                    "SELECT TOP 1 ID, Username FROM [dbo].[Admin] WHERE Line_UserId = @uid AND ID <> @id",
                    new Dictionary<string, object> { { "@uid", res.UserId }, { "@id", adminId } });
                if (dup?.Rows.Count > 0)
                {
                    res.Message = $"บัญชี LINE นี้ถูกผูกกับผู้ใช้ \"{dup.Rows[0]["Username"]}\" อยู่แล้ว " +
                                  "— ให้ผู้ใช้นั้นยกเลิกการผูกก่อน";
                    return res;
                }

                // 4) บันทึก
                _code.DatabaseInsertSafe(_conn,
                    @"UPDATE [dbo].[Admin]
                         SET Line_UserId = @uid, Line_DisplayName = @name,
                             Line_PictureUrl = @pic, Line_LinkedDate = GETDATE()
                       WHERE ID = @id",
                    new Dictionary<string, object>
                    {
                        { "@uid", res.UserId },
                        { "@name", (object)res.DisplayName ?? DBNull.Value },
                        { "@pic", (object)res.PictureUrl ?? DBNull.Value },
                        { "@id", adminId }
                    });

                _code.Logs(_conn, "LineLogin", $"admin #{adminId} ผูกบัญชี LINE: {res.DisplayName} ({Mask(res.UserId)})", "SYSTEM");
                res.Success = true;
                res.Message = $"ผูกบัญชี LINE สำเร็จ: {res.DisplayName}";
                return res;
            }
            catch (Exception ex)
            {
                _code.Logs(_conn, "LineLogin", "callback error: " + ex.Message, "SYSTEM");
                res.Message = "ผูกบัญชีไม่สำเร็จ: " + ex.Message;
                return res;
            }
        }

        // ── ผูกบัญชีด้วยการ "เลือกชื่อตัวเอง" (ไม่ต้องล็อกอินระบบก่อน) ─────────────

        /// <summary>รายชื่อบัญชีที่ยังไม่ได้ผูก LINE — ให้ผู้ใช้เลือกว่าตัวเองคือใคร</summary>
        public DataTable GetUnlinkedAdmins()
        {
            return _code.DatabaseQuerySafe(_conn,
                @"SELECT ID, Username, Role,
                         ISNULL(NULLIF(LTRIM(RTRIM(ISNULL(FirstName,'') + ' ' + ISNULL(LastName,''))), ''), Username) AS FullName
                    FROM [dbo].[Admin]
                   WHERE Status = 1 AND (Line_UserId IS NULL OR Line_UserId = '')
                   ORDER BY FullName", null);
        }

        /// <summary>ผูกทันทีโดยยืนยันด้วยรหัสผ่านของบัญชีนั้น (เส้นทางที่เร็วที่สุด)</summary>
        public (bool Ok, string Message) LinkWithPassword(int adminId, string password, LinkResult profile)
        {
            try
            {
                if (profile == null || string.IsNullOrWhiteSpace(profile.UserId))
                    return (false, "ข้อมูล LINE ไม่ครบ กรุณาเริ่มใหม่");
                if (string.IsNullOrWhiteSpace(password)) return (false, "กรุณากรอกรหัสผ่าน");

                var dt = _code.DatabaseQuerySafe(_conn,
                    "SELECT TOP 1 ID, Password, Username FROM [dbo].[Admin] WHERE ID = @id AND Status = 1",
                    new Dictionary<string, object> { { "@id", adminId } });
                if (dt == null || dt.Rows.Count == 0) return (false, "ไม่พบบัญชีนี้");

                if (!SecurityHelper.VerifyPassword(password, dt.Rows[0]["Password"]?.ToString() ?? ""))
                {
                    _code.Logs(_conn, "LineLogin",
                        $"ผูกบัญชีล้มเหลว: รหัสผ่านไม่ถูกต้อง (admin #{adminId}, line {Mask(profile.UserId)})", "SYSTEM");
                    return (false, "รหัสผ่านไม่ถูกต้อง");
                }

                var dup = _code.DatabaseQuerySafe(_conn,
                    "SELECT TOP 1 Username FROM [dbo].[Admin] WHERE Line_UserId = @uid AND ID <> @id",
                    new Dictionary<string, object> { { "@uid", profile.UserId }, { "@id", adminId } });
                if (dup?.Rows.Count > 0)
                    return (false, $"บัญชี LINE นี้ถูกผูกกับ \"{dup.Rows[0][0]}\" อยู่แล้ว");

                ApplyLink(adminId, profile);
                _code.Logs(_conn, "LineLogin",
                    $"ผูกบัญชีสำเร็จ (ยืนยันด้วยรหัสผ่าน): {dt.Rows[0]["Username"]} ← {profile.DisplayName}", "SYSTEM");
                return (true, "ผูกบัญชีสำเร็จ");
            }
            catch (Exception ex) { return (false, "ผูกบัญชีไม่สำเร็จ: " + ex.Message); }
        }

        /// <summary>ส่งคำขอให้ผู้ดูแลอนุมัติ (สำหรับคนที่จำรหัสผ่านไม่ได้)</summary>
        public (bool Ok, string Message) RequestLinkApproval(int adminId, LinkResult profile)
        {
            try
            {
                if (profile == null || string.IsNullOrWhiteSpace(profile.UserId))
                    return (false, "ข้อมูล LINE ไม่ครบ กรุณาเริ่มใหม่");

                var dup = _code.DatabaseQuerySafe(_conn,
                    "SELECT TOP 1 Username FROM [dbo].[Admin] WHERE Line_UserId = @uid",
                    new Dictionary<string, object> { { "@uid", profile.UserId } });
                if (dup?.Rows.Count > 0)
                    return (false, $"บัญชี LINE นี้ถูกผูกกับ \"{dup.Rows[0][0]}\" อยู่แล้ว");

                // มีคำขอค้างอยู่แล้ว → ไม่สร้างซ้ำ
                var pending = _code.DatabaseQuerySafe(_conn,
                    "SELECT TOP 1 ID FROM Admin_Line_Link_Requests WHERE Line_UserId = @uid AND Status = 'PENDING'",
                    new Dictionary<string, object> { { "@uid", profile.UserId } });
                if (pending?.Rows.Count > 0)
                    return (true, "ส่งคำขอไปแล้ว กำลังรอผู้ดูแลอนุมัติ");

                _code.DatabaseInsertSafe(_conn,
                    @"INSERT INTO Admin_Line_Link_Requests
                        (Admin_ID, Line_UserId, Line_DisplayName, Line_PictureUrl, Status, RequestedDate)
                      VALUES (@id, @uid, @name, @pic, 'PENDING', GETDATE())",
                    new Dictionary<string, object>
                    {
                        { "@id", adminId }, { "@uid", profile.UserId },
                        { "@name", (object)profile.DisplayName ?? DBNull.Value },
                        { "@pic", (object)profile.PictureUrl ?? DBNull.Value }
                    });

                NotifyOwnersOfRequest(adminId, profile);
                return (true, "ส่งคำขอให้ผู้ดูแลอนุมัติแล้ว");
            }
            catch (Exception ex) { return (false, "ส่งคำขอไม่สำเร็จ: " + ex.Message); }
        }

        private void NotifyOwnersOfRequest(int adminId, LinkResult profile)
        {
            try
            {
                var who = _code.DatabaseQuerySafe(_conn,
                    "SELECT TOP 1 Username FROM [dbo].[Admin] WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", adminId } });
                string username = who?.Rows.Count > 0 ? who.Rows[0][0].ToString() : "#" + adminId;

                string msg = "🔗 มีคำขอผูกบัญชี LINE\n\n"
                           + $"บัญชีระบบ: {username}\n"
                           + $"บัญชี LINE: {profile.DisplayName}\n\n"
                           + "กรุณาตรวจสอบว่าเป็นคนคนเดียวกันจริง แล้วกดอนุมัติที่\n"
                           + "เมนู จัดการโรงแรม → บัญชี LINE ของฉัน";
                Broadcast(msg, "Owner");
            }
            catch { }
        }

        public DataTable GetPendingLinkRequests()
        {
            return _code.DatabaseQuerySafe(_conn,
                @"SELECT r.ID, r.Admin_ID, r.Line_UserId, r.Line_DisplayName, r.Line_PictureUrl,
                         r.RequestedDate, A.Username, A.Role
                    FROM Admin_Line_Link_Requests r
                    INNER JOIN [dbo].[Admin] A ON A.ID = r.Admin_ID
                   WHERE r.Status = 'PENDING'
                   ORDER BY r.RequestedDate DESC", null);
        }

        public (bool Ok, string Message) DecideLinkRequest(long requestId, bool approve, int decidedByAdminId, string reason = null)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    @"SELECT TOP 1 Admin_ID, Line_UserId, Line_DisplayName, Line_PictureUrl, Status
                        FROM Admin_Line_Link_Requests WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", requestId } });
                if (dt == null || dt.Rows.Count == 0) return (false, "ไม่พบคำขอนี้");
                if (dt.Rows[0]["Status"]?.ToString() != "PENDING") return (false, "คำขอนี้ถูกดำเนินการไปแล้ว");

                int adminId = Convert.ToInt32(dt.Rows[0]["Admin_ID"]);
                string uid = dt.Rows[0]["Line_UserId"].ToString();

                if (approve)
                {
                    var dup = _code.DatabaseQuerySafe(_conn,
                        "SELECT TOP 1 Username FROM [dbo].[Admin] WHERE Line_UserId = @uid AND ID <> @id",
                        new Dictionary<string, object> { { "@uid", uid }, { "@id", adminId } });
                    if (dup?.Rows.Count > 0) return (false, $"บัญชี LINE นี้ถูกผูกกับ \"{dup.Rows[0][0]}\" ไปแล้ว");

                    ApplyLink(adminId, new LinkResult
                    {
                        UserId = uid,
                        DisplayName = dt.Rows[0]["Line_DisplayName"]?.ToString(),
                        PictureUrl = dt.Rows[0]["Line_PictureUrl"]?.ToString()
                    });
                    PushText(uid, "✅ ผู้ดูแลอนุมัติการผูกบัญชีแล้ว\nคุณจะได้รับแจ้งเตือนจากระบบทาง LINE นี้");
                }
                else
                {
                    PushText(uid, "❌ คำขอผูกบัญชีไม่ได้รับอนุมัติ"
                                  + (string.IsNullOrWhiteSpace(reason) ? "" : "\nเหตุผล: " + reason));
                }

                _code.DatabaseInsertSafe(_conn,
                    @"UPDATE Admin_Line_Link_Requests
                         SET Status = @st, DecidedBy_AdminID = @by, DecidedDate = GETDATE(), RejectReason = @rs
                       WHERE ID = @id",
                    new Dictionary<string, object>
                    {
                        { "@st", approve ? "APPROVED" : "REJECTED" },
                        { "@by", decidedByAdminId }, { "@id", requestId },
                        { "@rs", (object)reason ?? DBNull.Value }
                    });

                _code.Logs(_conn, "LineLogin",
                    $"คำขอผูกบัญชี #{requestId} → {(approve ? "อนุมัติ" : "ปฏิเสธ")} โดย admin #{decidedByAdminId}", "SYSTEM");
                return (true, approve ? "อนุมัติและผูกบัญชีแล้ว" : "ปฏิเสธคำขอแล้ว");
            }
            catch (Exception ex) { return (false, "ดำเนินการไม่สำเร็จ: " + ex.Message); }
        }

        private void ApplyLink(int adminId, LinkResult p)
        {
            _code.DatabaseInsertSafe(_conn,
                @"UPDATE [dbo].[Admin]
                     SET Line_UserId = @uid, Line_DisplayName = @name,
                         Line_PictureUrl = @pic, Line_LinkedDate = GETDATE()
                   WHERE ID = @id",
                new Dictionary<string, object>
                {
                    { "@uid", p.UserId },
                    { "@name", (object)p.DisplayName ?? DBNull.Value },
                    { "@pic", (object)p.PictureUrl ?? DBNull.Value },
                    { "@id", adminId }
                });
        }

        public void Unlink(int adminId)
        {
            _code.DatabaseInsertSafe(_conn,
                @"UPDATE [dbo].[Admin]
                     SET Line_UserId = NULL, Line_DisplayName = NULL,
                         Line_PictureUrl = NULL, Line_LinkedDate = NULL
                   WHERE ID = @id",
                new Dictionary<string, object> { { "@id", adminId } });
            _code.Logs(_conn, "LineLogin", $"admin #{adminId} ยกเลิกการผูกบัญชี LINE", "SYSTEM");
        }

        public DataRow GetAdminLineInfo(int adminId)
        {
            var dt = _code.DatabaseQuerySafe(_conn,
                @"SELECT ID, Username, Role, Line_UserId, Line_DisplayName, Line_PictureUrl,
                         Line_LinkedDate, Line_NotifyEnabled
                    FROM [dbo].[Admin] WHERE ID = @id",
                new Dictionary<string, object> { { "@id", adminId } });
            return dt?.Rows.Count > 0 ? dt.Rows[0] : null;
        }

        public DataTable GetLinkedAdmins()
        {
            return _code.DatabaseQuerySafe(_conn,
                @"SELECT ID, Username, Role, Line_UserId, Line_DisplayName,
                         Line_LinkedDate, Line_NotifyEnabled
                    FROM [dbo].[Admin]
                   WHERE Status = 1
                   ORDER BY CASE WHEN Line_UserId IS NULL THEN 1 ELSE 0 END, Username", null);
        }

        public void SetNotifyEnabled(int adminId, bool enabled)
        {
            _code.DatabaseInsertSafe(_conn,
                "UPDATE [dbo].[Admin] SET Line_NotifyEnabled = @e WHERE ID = @id",
                new Dictionary<string, object> { { "@e", enabled ? 1 : 0 }, { "@id", adminId } });
        }

        // ── ส่งข้อความส่วนตัว ──────────────────────────────────────────────────────
        /// <summary>ส่งข้อความหา Admin คนเดียว (ตาม userId ที่ผูกไว้)</summary>
        public (bool Ok, string Message) SendToAdmin(int adminId, string text)
        {
            var info = GetAdminLineInfo(adminId);
            if (info == null) return (false, "ไม่พบผู้ใช้");
            if (info["Line_UserId"] == DBNull.Value || string.IsNullOrWhiteSpace(info["Line_UserId"].ToString()))
                return (false, "ผู้ใช้นี้ยังไม่ได้ผูกบัญชี LINE");
            if (info["Line_NotifyEnabled"] != DBNull.Value && !ToBool(info["Line_NotifyEnabled"]))
                return (false, "ผู้ใช้นี้ปิดรับแจ้งเตือนทาง LINE");

            return PushText(info["Line_UserId"].ToString(), text);
        }

        /// <summary>
        /// ส่งข้อความหา Admin ทุกคนที่ผูกบัญชีแล้วและเปิดรับแจ้งเตือน
        /// roles: จำกัดเฉพาะบทบาท (เช่น "Owner","Admin") — ว่าง = ทุกคน
        /// </summary>
        public (int Sent, int Failed, string Detail) Broadcast(string text, params string[] roles)
        {
            var dt = _code.DatabaseQuerySafe(_conn,
                @"SELECT ID, Username, Role, Line_UserId FROM [dbo].[Admin]
                   WHERE Status = 1 AND Line_UserId IS NOT NULL AND Line_NotifyEnabled = 1", null);
            if (dt == null || dt.Rows.Count == 0) return (0, 0, "ยังไม่มีผู้ใช้ที่ผูกบัญชี LINE");

            int ok = 0, fail = 0;
            var lines = new List<string>();
            foreach (DataRow r in dt.Rows)
            {
                if (roles != null && roles.Length > 0)
                {
                    string role = r["Role"]?.ToString() ?? "";
                    if (Array.IndexOf(roles, role) < 0) continue;
                }
                var (sent, msg) = PushText(r["Line_UserId"].ToString(), text);
                if (sent) { ok++; }
                else { fail++; lines.Add($"{r["Username"]}: {msg}"); }
            }
            return (ok, fail, string.Join("; ", lines));
        }

        /// <summary>push ข้อความผ่าน Messaging API (ใช้ token ของ LINE OA เดิมในระบบ)</summary>
        public (bool Ok, string Message) PushText(string userId, string text)
        {
            try
            {
                string token = GetMessagingToken();
                if (string.IsNullOrEmpty(token)) return (false, "ไม่พบ LINE channel access token (Messaging API)");

                string json = new JavaScriptSerializer().Serialize(new
                {
                    to = userId,
                    messages = new[] { new { type = "text", text = Trim(text, 4900) } }
                });

                var req = (HttpWebRequest)WebRequest.Create(PUSH_URL);
                req.Method = "POST";
                req.ContentType = "application/json";
                req.Timeout = 20000;
                req.Headers.Add("Authorization", "Bearer " + token);
                byte[] data = Encoding.UTF8.GetBytes(json);
                req.ContentLength = data.Length;
                using (var st = req.GetRequestStream()) st.Write(data, 0, data.Length);
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var rd = new StreamReader(resp.GetResponseStream())) rd.ReadToEnd();
                return (true, "ส่งสำเร็จ");
            }
            catch (WebException wex)
            {
                string detail = "";
                try
                {
                    if (wex.Response != null)
                        using (var rd = new StreamReader(wex.Response.GetResponseStream())) detail = rd.ReadToEnd();
                }
                catch { }
                // 400 + "The user hasn't added the LINE Official Account as a friend" = ยังไม่ได้เป็นเพื่อนกับ OA
                string hint = detail.IndexOf("friend", StringComparison.OrdinalIgnoreCase) >= 0
                    ? " (ผู้ใช้ยังไม่ได้เพิ่ม LINE OA เป็นเพื่อน)"
                    : "";
                return (false, Trim(wex.Message + " " + detail, 300) + hint);
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        /// <summary>token ของ Messaging API — ใช้ตัวเดียวกับที่ระบบใช้ส่ง LINE อยู่แล้ว</summary>
        private string GetMessagingToken()
        {
            // 1) token เฉพาะงานรายงานรายวัน (ถ้าตั้งไว้)
            string ov = _code.Derypt(Cfg("Line_DailyReport_TokenOverride_Encrypted", ""));
            if (!string.IsNullOrWhiteSpace(ov)) return ov;
            // 2) LINE OA เดิม (OmniChannel_Channels)
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    "SELECT Config FROM OmniChannel_Channels WHERE ChannelCode = 'LINE'", null);
                if (dt?.Rows.Count > 0 && dt.Rows[0]["Config"] != DBNull.Value)
                {
                    var cfg = new JavaScriptSerializer()
                        .Deserialize<Dictionary<string, object>>(dt.Rows[0]["Config"].ToString());
                    if (cfg != null && cfg.ContainsKey("channelAccessToken"))
                    {
                        string t = cfg["channelAccessToken"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(t)) return t;
                    }
                }
            }
            catch { }
            // 3) Web.config
            return AppCfg.Get("linechannelaccesstokentaketime") ?? "";
        }

        // ── helpers ───────────────────────────────────────────────────────────────
        private static string PostForm(string url, string body)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "POST";
            req.ContentType = "application/x-www-form-urlencoded";
            req.Timeout = 20000;
            byte[] data = Encoding.UTF8.GetBytes(body);
            req.ContentLength = data.Length;
            using (var st = req.GetRequestStream()) st.Write(data, 0, data.Length);
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var rd = new StreamReader(resp.GetResponseStream())) return rd.ReadToEnd();
        }

        private static string GetWithBearer(string url, string accessToken)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "GET";
            req.Timeout = 20000;
            req.Headers.Add("Authorization", "Bearer " + accessToken);
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var rd = new StreamReader(resp.GetResponseStream())) return rd.ReadToEnd();
        }

        private string Cfg(string key, string def)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    "SELECT TOP 1 ConfigValue FROM Accounting_Integration_Config WHERE ConfigKey = @k",
                    new Dictionary<string, object> { { "@k", key } });
                if (dt?.Rows.Count > 0 && dt.Rows[0][0] != DBNull.Value)
                {
                    string v = dt.Rows[0][0].ToString();
                    return string.IsNullOrEmpty(v) ? def : v;
                }
            }
            catch { }
            return def;
        }

        private static string Trim(string s, int max) =>
            string.IsNullOrEmpty(s) ? "" : (s.Length > max ? s.Substring(0, max) : s);

        public static string Mask(string id) =>
            string.IsNullOrEmpty(id) || id.Length <= 8 ? id : id.Substring(0, 6) + "…" + id.Substring(id.Length - 4);

        private static bool ToBool(object v)
        {
            if (v == null || v == DBNull.Value) return false;
            if (v is bool b) return b;
            string s = v.ToString();
            return s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
