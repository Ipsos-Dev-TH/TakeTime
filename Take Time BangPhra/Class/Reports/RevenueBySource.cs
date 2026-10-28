using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Take_Time_BangPhra.TaxReports
{
    /// <summary>
    /// แยกยอดรายได้ตาม "แหล่งเงิน" สำหรับตารางสรุปของหน้า CheckDocument_New (CLAUDE.md ข้อ 23).
    ///
    /// เดิมแต่ละคอลัมน์ (เงินสด/โอนกสิกร/โอนกรุงไทย/เงินกรรมการ) กรองด้วยคำค้นแยกกัน ⇒ แหล่งเงินที่ไม่มีคำเหล่านี้
    /// (QR, เกตเวย์, ธนาคารอื่น) ไม่ถูกนับเลย และยอด "รวม" ต่ำกว่าเงินที่รับจริง. ตอนนี้ทุกวิธีชำระเข้ากล่องเดียวเสมอ
    /// (ตามลำดับคำค้นเดิม แล้วตาม Channel_Type) — ไม่เข้ากล่องไหน = "อื่น ๆ" พร้อมรายชื่อแหล่งเงิน ⇒ ผลรวมคอลัมน์ = ยอดรวมทั้งหมด.
    /// โค้ดบริสุทธิ์ (อ่าน DataTable ที่ query แล้ว) — เทสต์ด้วย mono.
    /// </summary>
    public sealed class RevenueBySource
    {
        public const string Cash = "CASH";
        public const string Kbank = "KBANK";
        public const string Ktb = "KTB";
        public const string Director = "DIRECTOR";
        public const string Other = "OTHER";
        public static readonly string[] Buckets = { Cash, Kbank, Ktb, Director, Other };

        private readonly Dictionary<string, decimal> _bucket = Buckets.ToDictionary(b => b, b => 0m);
        private readonly Dictionary<string, decimal> _otherBySource = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        private readonly IDictionary<string, string> _channelTypes;

        /// <param name="channelTypes">ชื่อแหล่งเงิน (Account_Paid_How.Paid_How) → Channel_Type (CASH/DIRECTOR_LOAN/…); null ได้</param>
        public RevenueBySource(IDictionary<string, string> channelTypes = null)
        {
            _channelTypes = channelTypes != null
                ? new Dictionary<string, string>(channelTypes, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public decimal this[string bucket] { get { decimal v; return _bucket.TryGetValue(bucket ?? "", out v) ? v : 0m; } }
        public decimal Total { get { return _bucket.Values.Sum(); } }

        /// <summary>แหล่งเงินที่ตกกล่อง "อื่น ๆ" → ยอด (เรียงมากไปน้อย)</summary>
        public IList<KeyValuePair<string, decimal>> OtherBySource
        {
            get { return _otherBySource.Where(kv => kv.Value != 0m).OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).ToList(); }
        }

        /// <summary>กล่องของวิธีชำระหนึ่งตัว — คำค้นเดิมก่อน (คงตัวเลขของคอลัมน์เดิม) แล้วค่อย Channel_Type</summary>
        public string BucketOf(string method)
        {
            string m = (method ?? "").Trim();
            if (m.Length == 0) return Other;
            if (m.Contains("เงินสด")) return Cash;
            if (m.Contains("กสิกร")) return Kbank;
            if (m.Contains("กรุงไทย")) return Ktb;
            if (m.Contains("กรรมการ")) return Director;
            string ct;
            if (_channelTypes.TryGetValue(m, out ct))
            {
                switch ((ct ?? "").Trim().ToUpperInvariant())
                {
                    case "CASH": return Cash;
                    case "DIRECTOR_LOAN": return Director;
                }
            }
            return Other;
        }

        private void Add(string method, decimal amount)
        {
            string b = BucketOf(method);
            _bucket[b] += amount;
            if (b == Other)
            {
                string key = string.IsNullOrWhiteSpace(method) ? "(ไม่ระบุวิธีชำระ)" : method.Trim();
                decimal v; _otherBySource.TryGetValue(key, out v);
                _otherBySource[key] = v + amount;
            }
        }

        /// <summary>
        /// เพิ่มแถวจาก query ของหน้า: Payment_History (มี PaymentHistoryID/PaymentMethod/PaymentAmount — นับครั้งเดียวต่อแถว
        /// ชำระ, ไม่มี ID = ข้ามเหมือนเดิม) หรือ Account_Receipt (ID/Paid_Type/Total_Amount — นับครั้งเดียวต่อใบ;
        /// Paid_Type หลายวิธีคั่น "," แบ่งยอดเท่า ๆ กันตามเดิม)
        /// </summary>
        public void AddRows(DataTable dt)
        {
            if (dt == null) return;
            if (dt.Columns.Contains("PaymentHistoryID"))
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (DataRow row in dt.Rows)
                {
                    string id = row["PaymentHistoryID"] == DBNull.Value ? "" : row["PaymentHistoryID"]?.ToString() ?? "";
                    if (id.Length == 0 || !seen.Add(id)) continue;
                    decimal amount = row["PaymentAmount"] != DBNull.Value ? Convert.ToDecimal(row["PaymentAmount"]) : 0m;
                    Add(row["PaymentMethod"] == DBNull.Value ? "" : row["PaymentMethod"]?.ToString(), amount);
                }
                return;
            }

            var seenReceipts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow row in dt.Rows)
            {
                string id = row["ID"] == DBNull.Value ? "" : row["ID"]?.ToString() ?? "";
                if (id.Length > 0 && !seenReceipts.Add(id)) continue;     // query หมวด 3-4 join บรรทัด → ใบเดียวหลายแถว
                decimal amount = row["Total_Amount"] != DBNull.Value ? Convert.ToDecimal(row["Total_Amount"]) : 0m;
                string paid = row["Paid_Type"] == DBNull.Value ? "" : row["Paid_Type"]?.ToString() ?? "";
                var parts = paid.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
                if (parts.Count == 0) { Add("", amount); continue; }
                if (parts.Count == 1) { Add(parts[0], amount); continue; }
                // หลายวิธี: แบ่งเท่า ๆ กัน (กติกาเดิม) — เศษสตางค์ไปวิธีสุดท้าย ⇒ ผลรวมเท่ายอดใบพอดี
                decimal each = Math.Round(amount / parts.Count, 2, MidpointRounding.AwayFromZero);
                decimal used = 0m;
                for (int i = 0; i < parts.Count; i++)
                {
                    decimal a = i == parts.Count - 1 ? amount - used : each;
                    used += a;
                    Add(parts[i], a);
                }
            }
        }
    }
}
