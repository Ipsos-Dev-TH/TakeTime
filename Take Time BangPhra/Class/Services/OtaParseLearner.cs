using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Take_Time_BangPhra.Services
{
    /// <summary>
    /// ตัวจำ "เทมเพลตอีเมลแบบนี้ ควรอ่านด้วยวิธีไหน" + ตัวเรียก AI มาช่วยตอนไม่แน่ใจ
    ///
    /// ═══ วงจรการเรียนรู้ ═══
    ///   1. อีเมลเข้ามา → ทำลายนิ้วมือเทมเพลต (TemplateKey)
    ///   2. OtaFieldReader อ่านค่าด้วยหลายวิธี ให้คะแนนแต่ละวิธี
    ///      โดยบวกโบนัสให้วิธีที่ "เคยถูกกับเทมเพลตนี้" (บทเรียนที่สะสมไว้)
    ///   3. ฟิลด์ไหนคะแนนยังต่ำ → ถาม AI ให้ช่วยอ่าน (ถ้าเปิดใช้)
    ///      คำตอบ AI เป็นแค่ "ผู้สมัครอีกคน" ต้องผ่านตัวตรวจชนิดข้อมูลเหมือนกัน
    ///      ⇒ AI แนะนำผิดก็ไม่หลุดเข้าระบบ
    ///   4. ลงจองสำเร็จ → บันทึกว่าวิธีไหนชนะ (Success++) เทมเพลตเดิมครั้งหน้าเร็วและมั่นใจขึ้น
    ///   5. คะแนนต่ำจริง ๆ → ไม่เดา แจ้งเตือนพร้อมบอกว่าแต่ละวิธีได้อะไรมา
    ///
    /// ⚠ AI ไม่มีสิทธิ์ตัดสินใจแทนกฎความปลอดภัย — ยอด 0 / Booking ID เพี้ยน ยังบล็อกเหมือนเดิม
    /// </summary>
    public class OtaParseLearner
    {
        private readonly string _conn;
        private readonly code _code = new code();
        private static bool? _tableReady;

        public OtaParseLearner(string connectionString) { _conn = connectionString; }

        /// <summary>เกณฑ์คะแนนที่ถือว่า "มั่นใจพอจะใช้เลย" (ต่ำกว่านี้ = ถาม AI / แจ้งคน)</summary>
        public int ConfidenceThreshold
        {
            get { int v; return int.TryParse(Cfg("Email_Rsv_MinConfidence", "60"), out v) ? v : 60; }
        }

        public bool AiAssistEnabled
        {
            get { return Cfg("Email_Rsv_AiAssist", "1") == "1"; }
        }

        // ══ บทเรียน ═════════════════════════════════════════════════════════

        private bool TableReady()
        {
            if (_tableReady.HasValue) return _tableReady.Value;
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    "SELECT COUNT(*) FROM sys.objects WHERE object_id = OBJECT_ID('dbo.OTA_Email_Parse_Learning') AND type = 'U'", null);
                _tableReady = dt != null && dt.Rows.Count > 0 && Convert.ToInt32(dt.Rows[0][0]) > 0;
            }
            catch { _tableReady = false; }
            return _tableReady.Value;
        }

        private Dictionary<string, int> _cache;

        /// <summary>โหลดบทเรียนของเทมเพลตนี้มาไว้ในหน่วยความจำ (เรียกครั้งเดียวต่ออีเมล)</summary>
        public void LoadFor(string templateKey)
        {
            _cache = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (!TableReady() || string.IsNullOrEmpty(templateKey)) return;
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn, @"
                    SELECT Field_Name, Strategy_Key, Success_Count, Fail_Count
                      FROM OTA_Email_Parse_Learning
                     WHERE Template_Key = @t",
                    new Dictionary<string, object> { { "@t", templateKey } });
                if (dt == null) return;
                foreach (DataRow r in dt.Rows)
                {
                    int ok = Convert.ToInt32(r["Success_Count"]);
                    int bad = Convert.ToInt32(r["Fail_Count"]);
                    int total = ok + bad;
                    if (total <= 0) continue;
                    // อัตราสำเร็จ → โบนัสสูงสุด 25 คะแนน และต้องมีตัวอย่างพอสมควรถึงให้เต็ม
                    double rate = (double)ok / total;
                    int bonus = (int)Math.Round(rate * 25 * Math.Min(1.0, total / 5.0));
                    _cache[Key(Convert.ToString(r["Field_Name"]), Convert.ToString(r["Strategy_Key"]))] = bonus;
                }
            }
            catch { }
        }

        /// <summary>โบนัสคะแนนของวิธีนี้กับเทมเพลตนี้ (ส่งเข้า OtaFieldReader.Read)</summary>
        public int Bonus(string field, string strategy)
        {
            int v;
            return _cache != null && _cache.TryGetValue(Key(field, strategy), out v) ? v : 0;
        }

        private static string Key(string f, string s) { return (f ?? "") + "|" + (s ?? ""); }

        /// <summary>จดว่าวิธีไหนถูก/ผิด — เรียกหลังรู้ผลว่าใช้ค่านั้นลงจองได้จริงไหม</summary>
        public void Record(string templateKey, string channel, string field, string strategy,
            bool success, string sampleValue)
        {
            if (!TableReady() || string.IsNullOrEmpty(templateKey) || string.IsNullOrEmpty(strategy)) return;
            try
            {
                _code.DatabaseInsertSafe(_conn, @"
                    MERGE OTA_Email_Parse_Learning AS t
                    USING (SELECT @tk AS Template_Key, @f AS Field_Name, @s AS Strategy_Key) AS src
                       ON t.Template_Key = src.Template_Key
                      AND t.Field_Name = src.Field_Name
                      AND t.Strategy_Key = src.Strategy_Key
                    WHEN MATCHED THEN UPDATE SET
                        Success_Count = t.Success_Count + CASE WHEN @ok = 1 THEN 1 ELSE 0 END,
                        Fail_Count    = t.Fail_Count    + CASE WHEN @ok = 1 THEN 0 ELSE 1 END,
                        Last_Value    = LEFT(@v, 200),
                        Channel       = COALESCE(NULLIF(@ch, ''), t.Channel),
                        Last_Seen     = GETDATE()
                    WHEN NOT MATCHED THEN INSERT
                        (Template_Key, Channel, Field_Name, Strategy_Key, Success_Count, Fail_Count, Last_Value, Last_Seen)
                        VALUES (@tk, NULLIF(@ch, ''), @f, @s,
                                CASE WHEN @ok = 1 THEN 1 ELSE 0 END,
                                CASE WHEN @ok = 1 THEN 0 ELSE 1 END,
                                LEFT(@v, 200), GETDATE());",
                    new Dictionary<string, object>
                    {
                        { "@tk", templateKey }, { "@ch", channel ?? "" }, { "@f", field },
                        { "@s", strategy }, { "@ok", success ? 1 : 0 }, { "@v", sampleValue ?? "" }
                    });
            }
            catch { }
        }

        /// <summary>
        /// จดว่าเจอเทมเพลตนี้ + คะแนนความมั่นใจโดยรวม
        /// ใช้ตอบคำถาม "OTA เปลี่ยนเทมเพลตตั้งแต่เมื่อไหร่ และตั้งแต่นั้นอ่านได้แย่ลงไหม"
        /// </summary>
        public bool NoteTemplate(string templateKey, string channel, string subject, int confidence, bool needsReview)
        {
            if (!TableReady() || string.IsNullOrEmpty(templateKey)) return false;
            bool isNew = false;
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    "SELECT COUNT(*) FROM OTA_Email_Template_Seen WHERE Template_Key = @t",
                    new Dictionary<string, object> { { "@t", templateKey } });
                isNew = dt != null && dt.Rows.Count > 0 && Convert.ToInt32(dt.Rows[0][0]) == 0;

                _code.DatabaseInsertSafe(_conn, @"
                    MERGE OTA_Email_Template_Seen AS t
                    USING (SELECT @tk AS Template_Key) AS src ON t.Template_Key = src.Template_Key
                    WHEN MATCHED THEN UPDATE SET
                        Email_Count  = t.Email_Count + 1,
                        Last_Seen    = GETDATE(),
                        Last_Confidence = @c,
                        Needs_Review = CASE WHEN @nr = 1 THEN 1 ELSE t.Needs_Review END,
                        Channel      = COALESCE(NULLIF(@ch, ''), t.Channel)
                    WHEN NOT MATCHED THEN INSERT
                        (Template_Key, Channel, Sample_Subject, Email_Count, First_Seen, Last_Seen, Last_Confidence, Needs_Review)
                        VALUES (@tk, NULLIF(@ch, ''), LEFT(@subj, 300), 1, GETDATE(), GETDATE(), @c, @nr);",
                    new Dictionary<string, object>
                    {
                        { "@tk", templateKey }, { "@ch", channel ?? "" }, { "@subj", subject ?? "" },
                        { "@c", confidence }, { "@nr", needsReview ? 1 : 0 }
                    });
            }
            catch { }
            return isNew;
        }

        // ══ AI ช่วยอ่าน ═════════════════════════════════════════════════════

        /// <summary>
        /// ให้ AI ช่วยอ่านฟิลด์ที่คะแนนยังต่ำ — คืน dictionary ฟิลด์ → ค่า (ว่าง = ช่วยไม่ได้)
        ///
        /// ⚠ คำตอบของ AI ไม่ได้รับสิทธิ์พิเศษ: ผู้เรียกต้องเอาไปให้คะแนน/ตรวจชนิดข้อมูล
        ///   เหมือนวิธีอื่น แล้วยังต้องผ่านกฎความปลอดภัย (ยอด 0 / Booking ID เพี้ยน) อยู่ดี
        /// ส่งเฉพาะ "ข้อความล้วน" ไม่ส่ง HTML ทั้งก้อน — ประหยัด token และลดข้อมูลรั่ว
        /// </summary>
        public Dictionary<string, string> AskAi(string plainText, IEnumerable<string> fieldsNeeded, out string error)
        {
            error = null;
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var need = (fieldsNeeded ?? Enumerable.Empty<string>()).Distinct().ToList();
            if (need.Count == 0) return result;

            try
            {
                if (!AiAssistEnabled) { error = "ปิดการใช้ AI ช่วยอ่านไว้"; return result; }
                var ai = new DeepSeekService(_conn);
                if (!ai.IsEnabled) { error = "ยังไม่ได้เปิดใช้ AI ในระบบ"; return result; }

                string body = plainText ?? "";
                if (body.Length > 6000) body = body.Substring(0, 6000);   // กันอีเมลยาวผิดปกติ

                var sb = new StringBuilder();
                sb.AppendLine("อ่านอีเมลยืนยันการจองที่พักด้านล่าง แล้วดึงค่าต่อไปนี้ออกมา:");
                foreach (string f in need) sb.AppendLine("- " + FieldHint(f));
                sb.AppendLine();
                sb.AppendLine("ตอบเป็น JSON อย่างเดียว ไม่ต้องอธิบาย ไม่ต้องใส่ ``` :");
                sb.AppendLine("{" + string.Join(", ", need.Select(f => "\"" + f + "\": \"\"")) + "}");
                sb.AppendLine("ถ้าหาค่าไหนไม่เจอ ให้ใส่สตริงว่าง ห้ามเดา ห้ามแต่งค่าขึ้นเอง");
                sb.AppendLine();
                sb.AppendLine("--- เนื้ออีเมล ---");
                sb.AppendLine(body);

                var resp = ai.SendMessage(sb.ToString(), "ota-parse-" + Guid.NewGuid().ToString("N").Substring(0, 8),
                    null,
                    "คุณคือตัวช่วยดึงข้อมูลจากอีเมลจองโรงแรม ตอบเป็น JSON เท่านั้น "
                    + "ห้ามเดาค่าที่ไม่มีในอีเมล ถ้าไม่เจอให้ตอบสตริงว่าง");

                if (resp == null || !resp.Success)
                {
                    error = resp == null ? "เรียก AI ไม่สำเร็จ" : resp.Message;
                    return result;
                }

                string json = ExtractJson(resp.Message);
                if (string.IsNullOrEmpty(json)) { error = "AI ตอบไม่เป็น JSON"; return result; }

                var o = JObject.Parse(json);
                foreach (string f in need)
                {
                    var tok = o[f];
                    string v = tok == null ? "" : Convert.ToString(tok);
                    if (!string.IsNullOrWhiteSpace(v)) result[f] = v.Trim();
                }
            }
            catch (Exception ex) { error = ex.Message; }
            return result;
        }

        private static string FieldHint(string field)
        {
            switch (field)
            {
                case "BookingId": return "BookingId = เลขที่การจองของ OTA (ตัวเลขล้วนหรือมีขีด)";
                case "PaymentType": return "PaymentType = ใครเก็บเงิน เช่น Hotel Collect / Expedia Collect / Prepaid";
                case "ChannelName": return "ChannelName = ชื่อช่องทางที่จองมา เช่น Agoda, Expedia, Booking.com";
                case "GuestName": return "GuestName = ชื่อผู้เข้าพัก";
                case "MobilePhone": return "MobilePhone = เบอร์โทรผู้เข้าพัก";
                case "GrossTotal": return "GrossTotal = ยอดเงินรวมทั้งใบ (ตัวเลขอย่างเดียว ไม่ต้องมีสกุลเงิน)";
                case "CheckIn": return "CheckIn = วันเช็คอิน รูปแบบ yyyy-MM-dd";
                case "CheckOut": return "CheckOut = วันเช็คเอาท์ รูปแบบ yyyy-MM-dd";
                default: return field;
            }
        }

        /// <summary>ดึงก้อน JSON ออกจากคำตอบ (บางครั้ง AI ใส่ ``` หรือคำอธิบายมาด้วย)</summary>
        private static string ExtractJson(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            int a = s.IndexOf('{'), b = s.LastIndexOf('}');
            return a >= 0 && b > a ? s.Substring(a, b - a + 1) : null;
        }

        private string Cfg(string key, string def)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    "SELECT TOP 1 ConfigValue FROM Accounting_Integration_Config WHERE ConfigKey = @k",
                    new Dictionary<string, object> { { "@k", key } });
                if (dt != null && dt.Rows.Count > 0 && dt.Rows[0][0] != DBNull.Value)
                {
                    string v = Convert.ToString(dt.Rows[0][0]);
                    if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
                }
            }
            catch { }
            return def;
        }
    }
}
