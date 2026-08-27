using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web;

namespace Take_Time_BangPhra.Payments
{
    /// <summary>
    /// ที่อยู่ (URL) ที่ใช้คุยกับเกตเวย์ — ต้องเป็น URL เต็มที่เข้าถึงได้จากภายนอก
    /// เพราะเกตเวย์ต้องเรียกกลับมาได้เอง
    /// </summary>
    public static class PaymentUrls
    {
        /// <summary>โดเมนของเว็บ — ตั้งค่าทับได้ที่ Payment_Site_BaseUrl (สำคัญเมื่ออยู่หลัง proxy)</summary>
        public static string SiteBase()
        {
            string cfg = PaymentGatewayConfig.Get("Payment_Site_BaseUrl");
            if (!string.IsNullOrWhiteSpace(cfg)) return cfg.TrimEnd('/');

            cfg = AppCfg.Get("SiteBaseUrl");
            if (!string.IsNullOrWhiteSpace(cfg)) return cfg.TrimEnd('/');

            var ctx = HttpContext.Current;
            if (ctx != null && ctx.Request != null)
            {
                var u = ctx.Request.Url;
                string app = ctx.Request.ApplicationPath;
                if (string.IsNullOrEmpty(app) || app == "/") app = "";
                return u.Scheme + "://" + u.Authority + app.TrimEnd('/');
            }
            return "https://taketimebangphra.com";
        }

        public static string ReturnUrl(string txnRef = null)
        {
            string u = SiteBase() + "/Payment/PayResult";
            return string.IsNullOrEmpty(txnRef) ? u : u + "?ref=" + Uri.EscapeDataString(txnRef);
        }

        public static string CancelUrl(string txnRef = null)
        {
            string u = SiteBase() + "/Payment/PayResult?cancelled=1";
            return string.IsNullOrEmpty(txnRef) ? u : u + "&ref=" + Uri.EscapeDataString(txnRef);
        }

        public static string WebhookUrl()
        {
            return SiteBase() + "/API/PaymentWebhook.ashx";
        }

        public static string PayPageUrl(string sourceType, string sourceId, decimal amount)
        {
            return SiteBase() + "/Payment/Pay?src=" + Uri.EscapeDataString(sourceType ?? "")
                 + "&id=" + Uri.EscapeDataString(sourceId ?? "")
                 + "&amt=" + amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// อ่าน/เขียนตาราง Payment_Transaction — ทุกการเปลี่ยนสถานะผ่านที่นี่ที่เดียว
    /// เพื่อให้ "จ่ายซ้ำ / แจ้งซ้ำ" ไม่ทำให้ยอดเพี้ยน
    /// </summary>
    public class PaymentTransactionStore
    {
        private readonly string _conn;

        public PaymentTransactionStore()
        {
            _conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        }

        public PaymentTransactionStore(string connectionString) { _conn = connectionString; }

        /// <summary>ตารางพร้อมใช้งานไหม (ยังไม่รัน migration = ยังไม่พร้อม)</summary>
        public bool TablesReady()
        {
            try
            {
                using (var con = new SqlConnection(_conn))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(
                        "SELECT CASE WHEN OBJECT_ID('dbo.Payment_Transaction','U') IS NULL THEN 0 ELSE 1 END", con))
                        return Convert.ToInt32(cmd.ExecuteScalar()) == 1;
                }
            }
            catch { return false; }
        }

        // ── สร้างเลขอ้างอิง ───────────────────────────────────────────────────

        /// <summary>
        /// เลขอ้างอิงของเรา — ส่งให้เกตเวย์และใช้เป็นกุญแจกันจ่ายซ้ำ
        /// รูปแบบ TT-yyyyMMdd-HHmmss-xxxx (สุ่มท้าย กันชนกันเมื่อสร้างพร้อมกัน)
        /// </summary>
        public static string NewTxnRef()
        {
            string rnd = Guid.NewGuid().ToString("N").Substring(0, 4).ToUpperInvariant();
            return "TT-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + rnd;
        }

        // ── สร้าง / อ่าน ──────────────────────────────────────────────────────

        public PaymentTransaction Create(PaymentChargeRequest req, string provider, decimal surcharge)
        {
            if (string.IsNullOrEmpty(req.TxnRef)) req.TxnRef = NewTxnRef();

            using (var con = new SqlConnection(_conn))
            {
                con.Open();
                using (var cmd = new SqlCommand(@"
                    INSERT INTO Payment_Transaction
                        (Txn_Ref, Provider, Method, Source_Type, Source_ID, Amount, Surcharge_Amount,
                         Currency, [Description], Customer_Name, Customer_Phone, Customer_Email,
                         [Status], Expires_At, Created_Date, Created_By)
                    VALUES
                        (@ref, @prov, @method, @stype, @sid, @amt, @sur,
                         @cur, @desc, @cname, @cphone, @cmail,
                         @status, @exp, GETDATE(), @by);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);", con))
                {
                    cmd.Parameters.AddWithValue("@ref", req.TxnRef);
                    cmd.Parameters.AddWithValue("@prov", provider ?? "");
                    cmd.Parameters.AddWithValue("@method", req.Method ?? "");
                    cmd.Parameters.AddWithValue("@stype", req.SourceType ?? PaymentSource.Other);
                    cmd.Parameters.AddWithValue("@sid", (object)req.SourceId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@amt", req.Amount);
                    cmd.Parameters.AddWithValue("@sur", surcharge);
                    cmd.Parameters.AddWithValue("@cur", string.IsNullOrEmpty(req.Currency) ? "THB" : req.Currency);
                    cmd.Parameters.AddWithValue("@desc", (object)Trim(req.Description, 255) ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@cname", (object)Trim(req.CustomerName, 200) ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@cphone", (object)Trim(req.CustomerPhone, 50) ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@cmail", (object)Trim(req.CustomerEmail, 200) ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@status", PaymentStatus.Initiated);
                    cmd.Parameters.AddWithValue("@exp",
                        (object)DateTime.Now.AddMinutes(PaymentGatewayConfig.ExpiryMinutes));
                    cmd.Parameters.AddWithValue("@by", (object)req.CreatedByAdminId ?? DBNull.Value);

                    object id = cmd.ExecuteScalar();
                    return GetById(Convert.ToInt32(id));
                }
            }
        }

        public PaymentTransaction GetByRef(string txnRef)
        {
            if (string.IsNullOrEmpty(txnRef)) return null;
            return QueryOne("SELECT TOP 1 * FROM Payment_Transaction WHERE Txn_Ref = @p", "@p", txnRef);
        }

        public PaymentTransaction GetById(int id)
        {
            return QueryOne("SELECT TOP 1 * FROM Payment_Transaction WHERE ID = @p", "@p", id);
        }

        public PaymentTransaction GetByProviderTxnId(string provider, string providerTxnId)
        {
            if (string.IsNullOrEmpty(providerTxnId)) return null;
            using (var con = new SqlConnection(_conn))
            {
                con.Open();
                using (var cmd = new SqlCommand(
                    "SELECT TOP 1 * FROM Payment_Transaction WHERE Provider = @prov AND Provider_Txn_ID = @t ORDER BY ID DESC", con))
                {
                    cmd.Parameters.AddWithValue("@prov", provider ?? "");
                    cmd.Parameters.AddWithValue("@t", providerTxnId);
                    using (var rd = cmd.ExecuteReader())
                        return rd.Read() ? Map(rd) : null;
                }
            }
        }

        /// <summary>รายการที่ยัง "รอชำระ" อยู่ของต้นทางนี้ (กันสร้างซ้ำซ้อนโดยไม่จำเป็น)</summary>
        public PaymentTransaction GetOpenForSource(string sourceType, string sourceId, string method)
        {
            using (var con = new SqlConnection(_conn))
            {
                con.Open();
                using (var cmd = new SqlCommand(@"
                    SELECT TOP 1 * FROM Payment_Transaction
                     WHERE Source_Type = @st AND ISNULL(Source_ID,'') = ISNULL(@sid,'')
                       AND Method = @m AND [Status] = 'PENDING'
                       AND (Expires_At IS NULL OR Expires_At > GETDATE())
                     ORDER BY ID DESC", con))
                {
                    cmd.Parameters.AddWithValue("@st", sourceType ?? "");
                    cmd.Parameters.AddWithValue("@sid", (object)sourceId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@m", method ?? "");
                    using (var rd = cmd.ExecuteReader())
                        return rd.Read() ? Map(rd) : null;
                }
            }
        }

        /// <summary>มีรายการที่จ่ายสำเร็จแล้วของต้นทางนี้หรือยัง (กันเก็บเงินซ้ำ)</summary>
        public PaymentTransaction GetPaidForSource(string sourceType, string sourceId)
        {
            using (var con = new SqlConnection(_conn))
            {
                con.Open();
                using (var cmd = new SqlCommand(@"
                    SELECT TOP 1 * FROM Payment_Transaction
                     WHERE Source_Type = @st AND ISNULL(Source_ID,'') = ISNULL(@sid,'')
                       AND [Status] = 'PAID'
                     ORDER BY ID DESC", con))
                {
                    cmd.Parameters.AddWithValue("@st", sourceType ?? "");
                    cmd.Parameters.AddWithValue("@sid", (object)sourceId ?? DBNull.Value);
                    using (var rd = cmd.ExecuteReader())
                        return rd.Read() ? Map(rd) : null;
                }
            }
        }

        // ── เปลี่ยนสถานะ ──────────────────────────────────────────────────────

        public void SaveChargeResult(int id, PaymentChargeResult r)
        {
            Exec(@"UPDATE Payment_Transaction
                      SET [Status] = @s, Provider_Txn_ID = COALESCE(NULLIF(@ptid,''), Provider_Txn_ID),
                          Payment_Url = @url, Qr_Payload = @qr,
                          Fail_Reason = CASE WHEN @s = 'FAILED' THEN @msg ELSE Fail_Reason END,
                          Raw_Request = @req, Raw_Response = @res, Updated_Date = GETDATE()
                    WHERE ID = @id",
                new Dictionary<string, object>
                {
                    { "@id", id },
                    { "@s", r.Status ?? PaymentStatus.Failed },
                    { "@ptid", r.ProviderTxnId ?? "" },
                    { "@url", (object)Trim(r.PaymentUrl, 1000) ?? DBNull.Value },
                    { "@qr", (object)r.QrPayload ?? DBNull.Value },
                    { "@msg", (object)Trim(r.Message, 500) ?? DBNull.Value },
                    { "@req", (object)r.RawRequest ?? DBNull.Value },
                    { "@res", (object)r.RawResponse ?? DBNull.Value }
                });
        }

        /// <summary>
        /// ทำเครื่องหมายว่าจ่ายแล้ว — ทำได้ครั้งเดียวเท่านั้น
        /// คืน true เฉพาะครั้งที่เปลี่ยนสถานะสำเร็จจริง ⇒ ผู้เรียกจึงลงบันทึกปลายทางได้ปลอดภัย
        /// (เกตเวย์ส่ง webhook ซ้ำเป็นเรื่องปกติ + ผู้ใช้กดรีเฟรชหน้าผลลัพธ์ซ้ำได้)
        /// </summary>
        public bool MarkPaid(int id, string providerTxnId, decimal? fee, string cardBrand, string cardLast4)
        {
            using (var con = new SqlConnection(_conn))
            {
                con.Open();
                using (var cmd = new SqlCommand(@"
                    UPDATE Payment_Transaction
                       SET [Status] = 'PAID', Paid_At = GETDATE(), Updated_Date = GETDATE(),
                           Provider_Txn_ID = COALESCE(NULLIF(@ptid,''), Provider_Txn_ID),
                           Fee_Amount = COALESCE(@fee, Fee_Amount),
                           Card_Brand = COALESCE(NULLIF(@brand,''), Card_Brand),
                           Card_Last4 = COALESCE(NULLIF(@last4,''), Card_Last4)
                     WHERE ID = @id AND [Status] <> 'PAID'", con))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.Parameters.AddWithValue("@ptid", providerTxnId ?? "");
                    cmd.Parameters.AddWithValue("@fee", (object)fee ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@brand", cardBrand ?? "");
                    cmd.Parameters.AddWithValue("@last4", cardLast4 ?? "");
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        /// <summary>เปลี่ยนเป็นสถานะปลายทางอื่น (ล้มเหลว/หมดอายุ/ยกเลิก) — ไม่ทับรายการที่จ่ายแล้ว</summary>
        public bool MarkTerminal(int id, string status, string reason)
        {
            using (var con = new SqlConnection(_conn))
            {
                con.Open();
                using (var cmd = new SqlCommand(@"
                    UPDATE Payment_Transaction
                       SET [Status] = @s, Fail_Reason = @r, Updated_Date = GETDATE()
                     WHERE ID = @id AND [Status] NOT IN ('PAID','REFUNDED')", con))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.Parameters.AddWithValue("@s", status);
                    cmd.Parameters.AddWithValue("@r", (object)Trim(reason, 500) ?? DBNull.Value);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        /// <summary>
        /// จองสิทธิ์ "ลงบันทึกเข้าระบบเดิม" — คืน true เฉพาะผู้เรียกรายแรก
        /// กันการออกใบเสร็จ/ตัดยอดซ้ำเมื่อ webhook กับหน้าผลลัพธ์มาพร้อมกัน
        /// </summary>
        public bool TryClaimApply(int id)
        {
            using (var con = new SqlConnection(_conn))
            {
                con.Open();
                using (var cmd = new SqlCommand(@"
                    UPDATE Payment_Transaction
                       SET Applied_At = GETDATE()
                     WHERE ID = @id AND [Status] = 'PAID' AND Applied_At IS NULL", con))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        /// <summary>คืนสิทธิ์เมื่อบันทึกปลายทางไม่สำเร็จ เพื่อให้รอบถัดไปลองใหม่ได้</summary>
        public void ReleaseApply(int id, string note)
        {
            Exec("UPDATE Payment_Transaction SET Applied_At = NULL, Applied_Note = @n, Updated_Date = GETDATE() WHERE ID = @id",
                new Dictionary<string, object> { { "@id", id }, { "@n", (object)Trim(note, 500) ?? DBNull.Value } });
        }

        public void SetApplied(int id, string note, string receiptId)
        {
            Exec(@"UPDATE Payment_Transaction
                      SET Applied_Note = @n, Receipt_ID = @r, Updated_Date = GETDATE()
                    WHERE ID = @id",
                new Dictionary<string, object>
                {
                    { "@id", id },
                    { "@n", (object)Trim(note, 500) ?? DBNull.Value },
                    { "@r", (object)Trim(receiptId, 50) ?? DBNull.Value }
                });
        }

        /// <summary>ปิดรายการที่เลยเวลาไปแล้ว — เรียกจากงานเบื้องหลัง</summary>
        public int ExpireStale()
        {
            using (var con = new SqlConnection(_conn))
            {
                con.Open();
                using (var cmd = new SqlCommand(@"
                    UPDATE Payment_Transaction
                       SET [Status] = 'EXPIRED', Fail_Reason = N'เลยเวลาชำระเงินที่กำหนด', Updated_Date = GETDATE()
                     WHERE [Status] IN ('INITIATED','PENDING')
                       AND Expires_At IS NOT NULL AND Expires_At < GETDATE()", con))
                    return cmd.ExecuteNonQuery();
            }
        }

        /// <summary>รายการที่ยังค้าง ใช้ตามสถานะซ้ำเมื่อ webhook หาย</summary>
        public List<PaymentTransaction> GetPendingForPoll(int max)
        {
            var list = new List<PaymentTransaction>();
            using (var con = new SqlConnection(_conn))
            {
                con.Open();
                using (var cmd = new SqlCommand(@"
                    SELECT TOP (@n) * FROM Payment_Transaction
                     WHERE [Status] = 'PENDING' AND Provider_Txn_ID IS NOT NULL
                       AND Provider <> 'MANUAL_QR'
                       AND Created_Date > DATEADD(DAY, -3, GETDATE())
                     ORDER BY ISNULL(Updated_Date, Created_Date) ASC", con))
                {
                    cmd.Parameters.AddWithValue("@n", max);
                    using (var rd = cmd.ExecuteReader())
                        while (rd.Read()) list.Add(Map(rd));
                }
            }
            return list;
        }

        // ── เหตุการณ์จากเกตเวย์ ───────────────────────────────────────────────

        /// <summary>
        /// บันทึกเหตุการณ์ webhook — คืน false ถ้าเป็นเหตุการณ์เดิมที่เคยรับแล้ว
        /// (ดักด้วย unique index บน Provider+Event_ID)
        /// </summary>
        public bool LogEvent(PaymentWebhookEvent ev, string provider, int? txnId, string rawHeaders,
                             string rawBody, string remoteIp, out int eventId)
        {
            eventId = 0;
            try
            {
                using (var con = new SqlConnection(_conn))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(@"
                        INSERT INTO Payment_Transaction_Event
                            (Txn_ID, Txn_Ref, Provider, Event_ID, Event_Type, Signature_Valid,
                             Remote_IP, Raw_Headers, Raw_Body, Created_Date)
                        VALUES (@tid, @tref, @prov, @eid, @etype, @sig, @ip, @h, @b, GETDATE());
                        SELECT CAST(SCOPE_IDENTITY() AS INT);", con))
                    {
                        cmd.Parameters.AddWithValue("@tid", (object)txnId ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@tref", (object)Trim(ev == null ? null : ev.TxnRef, 50) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@prov", provider ?? "");
                        cmd.Parameters.AddWithValue("@eid",
                            (object)Trim(ev == null ? null : ev.EventId, 120) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@etype",
                            (object)Trim(ev == null ? null : ev.EventType, 60) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@sig", ev == null ? (object)DBNull.Value : ev.SignatureValid);
                        cmd.Parameters.AddWithValue("@ip", (object)Trim(remoteIp, 60) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@h", (object)rawHeaders ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@b", (object)rawBody ?? DBNull.Value);
                        eventId = Convert.ToInt32(cmd.ExecuteScalar());
                        return true;
                    }
                }
            }
            catch (SqlException ex)
            {
                // 2601/2627 = ซ้ำกับ unique index ⇒ เคยรับเหตุการณ์นี้ไปแล้ว
                if (ex.Number == 2601 || ex.Number == 2627) return false;
                throw;
            }
        }

        public void MarkEventHandled(int eventId, string note)
        {
            if (eventId <= 0) return;
            Exec("UPDATE Payment_Transaction_Event SET Handled = 1, Handle_Note = @n WHERE ID = @id",
                new Dictionary<string, object> { { "@id", eventId }, { "@n", (object)Trim(note, 500) ?? DBNull.Value } });
        }

        // ── รายงาน ────────────────────────────────────────────────────────────

        public DataTable Search(DateTime from, DateTime to, string status, string method, string keyword, int top)
        {
            var dt = new DataTable();
            using (var con = new SqlConnection(_conn))
            using (var da = new SqlDataAdapter(@"
                SELECT TOP (@top) ID, Txn_Ref, Provider, Method, Source_Type, Source_ID,
                       Amount, Surcharge_Amount, Fee_Amount, [Status], Provider_Txn_ID,
                       Card_Brand, Card_Last4, Customer_Name, Customer_Phone,
                       Paid_At, Applied_At, Receipt_ID, Fail_Reason, Created_Date
                  FROM Payment_Transaction
                 WHERE Created_Date >= @from AND Created_Date < DATEADD(DAY, 1, @to)
                   AND (@status = '' OR [Status] = @status)
                   AND (@method = '' OR Method = @method)
                   AND (@kw = '' OR Txn_Ref LIKE '%' + @kw + '%'
                        OR ISNULL(Provider_Txn_ID,'') LIKE '%' + @kw + '%'
                        OR ISNULL(Source_ID,'') LIKE '%' + @kw + '%'
                        OR ISNULL(Customer_Name,'') LIKE '%' + @kw + '%'
                        OR ISNULL(Customer_Phone,'') LIKE '%' + @kw + '%')
                 ORDER BY ID DESC", con))
            {
                da.SelectCommand.Parameters.AddWithValue("@top", top <= 0 ? 200 : top);
                da.SelectCommand.Parameters.AddWithValue("@from", from.Date);
                da.SelectCommand.Parameters.AddWithValue("@to", to.Date);
                da.SelectCommand.Parameters.AddWithValue("@status", status ?? "");
                da.SelectCommand.Parameters.AddWithValue("@method", method ?? "");
                da.SelectCommand.Parameters.AddWithValue("@kw", keyword ?? "");
                da.Fill(dt);
            }
            return dt;
        }

        // ── helpers ───────────────────────────────────────────────────────────

        private PaymentTransaction QueryOne(string sql, string pName, object pValue)
        {
            using (var con = new SqlConnection(_conn))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue(pName, pValue);
                    using (var rd = cmd.ExecuteReader())
                        return rd.Read() ? Map(rd) : null;
                }
            }
        }

        private void Exec(string sql, Dictionary<string, object> ps)
        {
            using (var con = new SqlConnection(_conn))
            {
                con.Open();
                using (var cmd = new SqlCommand(sql, con))
                {
                    foreach (var kv in ps) cmd.Parameters.AddWithValue(kv.Key, kv.Value ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private static PaymentTransaction Map(IDataRecord rd)
        {
            var t = new PaymentTransaction();
            t.ID = GetInt(rd, "ID") ?? 0;
            t.TxnRef = GetStr(rd, "Txn_Ref");
            t.Provider = GetStr(rd, "Provider");
            t.Method = GetStr(rd, "Method");
            t.SourceType = GetStr(rd, "Source_Type");
            t.SourceId = GetStr(rd, "Source_ID");
            t.Amount = GetDec(rd, "Amount") ?? 0m;
            t.SurchargeAmount = GetDec(rd, "Surcharge_Amount") ?? 0m;
            t.FeeAmount = GetDec(rd, "Fee_Amount");
            t.Currency = GetStr(rd, "Currency");
            t.Description = GetStr(rd, "Description");
            t.CustomerName = GetStr(rd, "Customer_Name");
            t.CustomerPhone = GetStr(rd, "Customer_Phone");
            t.CustomerEmail = GetStr(rd, "Customer_Email");
            t.Status = GetStr(rd, "Status");
            t.ProviderTxnId = GetStr(rd, "Provider_Txn_ID");
            t.CardBrand = GetStr(rd, "Card_Brand");
            t.CardLast4 = GetStr(rd, "Card_Last4");
            t.PaymentUrl = GetStr(rd, "Payment_Url");
            t.QrPayload = GetStr(rd, "Qr_Payload");
            t.ExpiresAt = GetDate(rd, "Expires_At");
            t.PaidAt = GetDate(rd, "Paid_At");
            t.FailReason = GetStr(rd, "Fail_Reason");
            t.AppliedAt = GetDate(rd, "Applied_At");
            t.AppliedNote = GetStr(rd, "Applied_Note");
            t.ReceiptId = GetStr(rd, "Receipt_ID");
            t.CreatedDate = GetDate(rd, "Created_Date") ?? DateTime.Now;
            t.CreatedBy = GetInt(rd, "Created_By");
            return t;
        }

        private static bool Has(IDataRecord rd, string name)
        {
            for (int i = 0; i < rd.FieldCount; i++)
                if (string.Equals(rd.GetName(i), name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string GetStr(IDataRecord rd, string n)
        {
            if (!Has(rd, n)) return null;
            object o = rd[n];
            return o == DBNull.Value ? null : o.ToString();
        }

        private static int? GetInt(IDataRecord rd, string n)
        {
            if (!Has(rd, n)) return null;
            object o = rd[n];
            return o == DBNull.Value ? (int?)null : Convert.ToInt32(o);
        }

        private static decimal? GetDec(IDataRecord rd, string n)
        {
            if (!Has(rd, n)) return null;
            object o = rd[n];
            return o == DBNull.Value ? (decimal?)null : Convert.ToDecimal(o);
        }

        private static DateTime? GetDate(IDataRecord rd, string n)
        {
            if (!Has(rd, n)) return null;
            object o = rd[n];
            return o == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(o);
        }

        private static string Trim(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return null;
            return s.Length <= max ? s : s.Substring(0, max);
        }
    }
}
