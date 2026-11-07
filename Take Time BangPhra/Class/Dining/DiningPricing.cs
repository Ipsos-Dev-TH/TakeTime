using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Take_Time_BangPhra.Dining
{
    /// <summary>
    /// คิดราคาตะกร้า QR สั่งอาหาร — <b>บริสุทธิ์</b> (ไม่มี DB/HttpContext) ทดสอบด้วย mono ได้
    /// กติกา (คำตัดสินเจ้าของ 29 ต.ค. 2026): ราคาเมนูรวม VAT แล้ว · ไม่มี service charge · ไม่มีทิป ·
    /// ราคาคิดฝั่ง server จาก Product.Sell_Price + Option.Price_Delta เสมอ (ไม่เชื่อราคาจาก client)
    /// ตรวจ: จำนวน > 0 และ ≤ 99 ต่อบรรทัด · ≤ 50 บรรทัด · ยอดขั้นต่ำ · รายการต้องสั่งได้ (ใช้งาน/ไม่ sold-out/อยู่ในโซน/มีสต๊อก)
    /// · ตัวเลือกต้องเป็นของกลุ่มที่ผูกกับรายการ และครบตาม Min/Max/Required
    /// </summary>
    public static class DiningPricing
    {
        public const int MaxLines = 50;
        public const decimal MaxQtyPerLine = 99m;
        public const decimal VatRate = 7m;

        /// <summary>ปัด 2 ตำแหน่ง (ครึ่งปัดขึ้น — แบบเดียวกับเอกสารขาย)</summary>
        public static decimal R2(decimal v)
        {
            return Math.Round(v, 2, MidpointRounding.AwayFromZero);
        }

        /// <summary>ถอด VAT จากยอดรวม (gross) — net = round(gross×100/107), vat = gross − net; ไม่จด VAT → vat 0</summary>
        public static void ExtractVat(decimal gross, bool useVat, out decimal net, out decimal vat)
        {
            gross = R2(gross);
            if (!useVat || gross == 0m) { net = gross; vat = 0m; return; }
            net = R2(gross * 100m / (100m + VatRate));
            vat = R2(gross - net);
        }

        /// <summary>ยอดเงินแสดงผล "1,250.00" (InvariantCulture)</summary>
        public static string Money(decimal v)
        {
            return v.ToString("N2", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// คิดใบเสนอราคาจากตะกร้า
        /// </summary>
        /// <param name="cart">บรรทัดจาก client (ProductId/Qty/OptionIds/Note)</param>
        /// <param name="lookup">หา DiningMenuItem (พร้อม OptionGroups) จาก ProductId — คืน null = ไม่มีในเมนู</param>
        /// <param name="zoneCode">โซนของโต๊ะ (ตรวจ Available_Zones)</param>
        /// <param name="minOrder">ยอดขั้นต่ำต่อรอบ (0 = ไม่มี)</param>
        /// <param name="useVat">กิจการจด VAT (ถอด 7% เพื่อแสดง/ลงบัญชี — ยอดที่ลูกค้าจ่ายไม่เปลี่ยน)</param>
        /// <param name="now">เวลาปัจจุบัน (sold-out until)</param>
        /// <param name="station">สถานีครัวของโซน (ใส่ลง line)</param>
        public static DiningQuote ComputeQuote(IList<DiningCartLine> cart, Func<int, DiningMenuItem> lookup, string zoneCode,
            decimal minOrder, bool useVat, DateTime now, string station = null)
        {
            var q = new DiningQuote();
            if (cart == null || cart.Count == 0)
            {
                q.Errors.Add("ตะกร้าว่าง — กรุณาเลือกรายการก่อน");
                q.ErrorCode = DiningError.Validation;
                return q;
            }
            if (cart.Count > MaxLines)
            {
                q.Errors.Add("สั่งได้สูงสุด " + MaxLines + " รายการต่อรอบ");
                q.ErrorCode = DiningError.Validation;
                return q;
            }

            // รวมบรรทัดที่เหมือนกัน (สินค้า + ตัวเลือกชุดเดียวกัน + หมายเหตุเดียวกัน)
            var merged = new List<DiningCartLine>();
            var keyOf = new Dictionary<string, DiningCartLine>(StringComparer.Ordinal);
            foreach (var raw in cart)
            {
                if (raw == null) continue;
                string k = LineKey(raw);
                DiningCartLine exist;
                if (keyOf.TryGetValue(k, out exist)) exist.Qty += raw.Qty;
                else
                {
                    var copy = new DiningCartLine { ProductId = raw.ProductId, Qty = raw.Qty, OptionIds = NormalizeIds(raw.OptionIds), Note = (raw.Note ?? "").Trim() };
                    keyOf[k] = copy;
                    merged.Add(copy);
                }
            }

            foreach (var line in merged)
            {
                if (line.Qty <= 0m) { q.Errors.Add("จำนวนต้องมากกว่า 0 (รายการ #" + line.ProductId + ")"); continue; }
                if (line.Qty > MaxQtyPerLine) { q.Errors.Add("สั่งได้สูงสุด " + MaxQtyPerLine.ToString("0") + " ชิ้นต่อรายการ (รายการ #" + line.ProductId + ")"); continue; }
                if (line.Qty != Math.Floor(line.Qty)) { q.Errors.Add("จำนวนต้องเป็นจำนวนเต็ม (รายการ #" + line.ProductId + ")"); continue; }
                if (line.Note != null && line.Note.Length > 300) line.Note = line.Note.Substring(0, 300);

                DiningMenuItem item = lookup == null ? null : lookup(line.ProductId);
                if (item == null) { q.Errors.Add("ไม่พบรายการ #" + line.ProductId + " ในเมนู"); q.ErrorCode = q.ErrorCode ?? DiningError.NotFound; continue; }
                if (!item.IsDining || !item.ProductActive) { q.Errors.Add("\"" + item.Name + "\" ไม่เปิดขายในเมนู QR"); q.ErrorCode = q.ErrorCode ?? DiningError.NotFound; continue; }
                if (item.SoldOutNow(now)) { q.Errors.Add("\"" + item.Name + "\" หมดชั่วคราว"); q.ErrorCode = q.ErrorCode ?? DiningError.SoldOut; continue; }
                if (!item.InZone(zoneCode)) { q.Errors.Add("\"" + item.Name + "\" ไม่มีขายในโซนนี้"); q.ErrorCode = q.ErrorCode ?? DiningError.NotFound; continue; }
                if (item.TrackStock && item.StockQty.HasValue && item.StockQty.Value < line.Qty)
                {
                    q.Errors.Add("\"" + item.Name + "\" เหลือ " + item.StockQty.Value.ToString("0.##") + " — สั่งได้ไม่เกินนั้น");
                    q.ErrorCode = q.ErrorCode ?? DiningError.OutOfStock;
                    continue;
                }

                string optErr;
                var selected = ResolveOptions(item, line.OptionIds, out optErr);
                if (optErr != null) { q.Errors.Add("\"" + item.Name + "\": " + optErr); q.ErrorCode = q.ErrorCode ?? DiningError.Validation; continue; }

                decimal optPrice = 0m;
                foreach (var o in selected) optPrice += o.PriceDelta;
                optPrice = R2(optPrice);
                decimal unit = R2(item.Price);
                if (unit + optPrice < 0m) { q.Errors.Add("\"" + item.Name + "\" ราคาติดลบ"); q.ErrorCode = q.ErrorCode ?? DiningError.Validation; continue; }

                var ql = new DiningQuoteLine
                {
                    ProductId = item.ProductId,
                    Name = item.Name,
                    Qty = line.Qty,
                    UnitPrice = unit,
                    OptionsPrice = optPrice,
                    Options = selected,
                    Note = line.Note,
                    TrackStock = item.TrackStock,
                    Station = station,
                    LineTotal = R2((unit + optPrice) * line.Qty)
                };
                q.Lines.Add(ql);
                q.Subtotal += ql.LineTotal;
                q.ItemCount += (int)line.Qty;
            }

            if (!q.Ok) { q.ErrorCode = q.ErrorCode ?? DiningError.Validation; return q; }

            q.Subtotal = R2(q.Subtotal);
            q.Discount = 0m;
            q.Total = R2(q.Subtotal - q.Discount);
            decimal net, vat;
            ExtractVat(q.Total, useVat, out net, out vat);
            q.NetAmount = net;
            q.VatAmount = vat;
            q.VatIncluded = true;

            if (minOrder > 0m && q.Total < minOrder)
            {
                q.Errors.Add("ยอดสั่งขั้นต่ำ " + Money(minOrder) + " บาท (ตอนนี้ " + Money(q.Total) + " บาท)");
                q.ErrorCode = DiningError.MinOrder;
            }
            return q;
        }

        /// <summary>
        /// ตรวจตัวเลือกที่เลือกกับกลุ่มที่ผูกกับรายการ — คืนรายการตัวเลือก (snapshot) หรือ error (ไทย)
        /// กฎ: ตัวเลือกต้องอยู่ในกลุ่มที่ผูกกับรายการและใช้งานอยู่ · ต่อกลุ่ม ≥ Min_Select (หรือ ≥1 เมื่อ Is_Required) และ ≤ Max_Select (0 = ไม่จำกัด)
        /// </summary>
        public static List<DiningSelectedOption> ResolveOptions(DiningMenuItem item, int[] optionIds, out string error)
        {
            error = null;
            var result = new List<DiningSelectedOption>();
            var ids = NormalizeIds(optionIds);
            var groups = item.OptionGroups ?? new List<DiningOptionGroup>();

            var countPerGroup = new Dictionary<int, int>();
            foreach (int id in ids)
            {
                DiningOptionGroup g = null; DiningOption found = null;
                foreach (var grp in groups)
                {
                    if (!grp.IsActive) continue;
                    foreach (var o in grp.Options)
                        if (o.Id == id && o.IsActive) { g = grp; found = o; break; }
                    if (found != null) break;
                }
                if (found == null) { error = "ตัวเลือก #" + id + " ไม่มีสำหรับรายการนี้"; return result; }
                int c; countPerGroup.TryGetValue(g.Id, out c); countPerGroup[g.Id] = c + 1;
                result.Add(new DiningSelectedOption { GroupId = g.Id, GroupName = g.NameTh, OptionId = found.Id, Name = found.NameTh, PriceDelta = R2(found.PriceDelta) });
            }
            foreach (var grp in groups)
            {
                if (!grp.IsActive) continue;
                int c; countPerGroup.TryGetValue(grp.Id, out c);
                int min = Math.Max(grp.MinSelect, grp.IsRequired ? 1 : 0);
                if (c < min) { error = "กรุณาเลือก \"" + grp.NameTh + "\"" + (min > 1 ? " อย่างน้อย " + min + " รายการ" : ""); return result; }
                if (grp.MaxSelect > 0 && c > grp.MaxSelect) { error = "\"" + grp.NameTh + "\" เลือกได้ไม่เกิน " + grp.MaxSelect + " รายการ"; return result; }
            }
            return result;
        }

        /// <summary>JSON ของตัวเลือกที่เลือก (เก็บลง Options_Json) — เขียนเองเพื่อไม่พึ่ง System.Web ในไฟล์บริสุทธิ์</summary>
        public static string OptionsToJson(IList<DiningSelectedOption> options)
        {
            if (options == null || options.Count == 0) return "[]";
            var sb = new StringBuilder("[");
            for (int i = 0; i < options.Count; i++)
            {
                var o = options[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"g\":").Append(o.GroupId.ToString(CultureInfo.InvariantCulture))
                  .Append(",\"gn\":\"").Append(JsonEscape(o.GroupName)).Append('"')
                  .Append(",\"id\":").Append(o.OptionId.ToString(CultureInfo.InvariantCulture))
                  .Append(",\"n\":\"").Append(JsonEscape(o.Name)).Append('"')
                  .Append(",\"p\":").Append(o.PriceDelta.ToString("0.##", CultureInfo.InvariantCulture))
                  .Append('}');
            }
            return sb.Append(']').ToString();
        }

        /// <summary>อ่าน Options_Json กลับ (parser เล็ก ๆ เฉพาะรูปแบบที่ OptionsToJson เขียน; อ่านไม่ได้ = ว่าง)</summary>
        public static List<DiningSelectedOption> OptionsFromJson(string json)
        {
            var list = new List<DiningSelectedOption>();
            if (string.IsNullOrWhiteSpace(json)) return list;
            try
            {
                int i = 0;
                while ((i = json.IndexOf('{', i)) >= 0)
                {
                    int end = json.IndexOf('}', i);
                    if (end < 0) break;
                    string obj = json.Substring(i + 1, end - i - 1);
                    var o = new DiningSelectedOption();
                    o.GroupId = ReadInt(obj, "g");
                    o.GroupName = ReadStr(obj, "gn");
                    o.OptionId = ReadInt(obj, "id");
                    o.Name = ReadStr(obj, "n");
                    decimal p;
                    decimal.TryParse(ReadRaw(obj, "p"), NumberStyles.Any, CultureInfo.InvariantCulture, out p);
                    o.PriceDelta = p;
                    list.Add(o);
                    i = end + 1;
                }
            }
            catch { }
            return list;
        }

        // ── helpers ─────────────────────────────────────────────────────────

        private static string LineKey(DiningCartLine l)
        {
            var ids = NormalizeIds(l.OptionIds);
            var sb = new StringBuilder();
            sb.Append(l.ProductId).Append('|');
            foreach (int id in ids) sb.Append(id).Append(',');
            sb.Append('|').Append((l.Note ?? "").Trim());
            return sb.ToString();
        }

        private static int[] NormalizeIds(int[] ids)
        {
            if (ids == null || ids.Length == 0) return new int[0];
            var set = new SortedSet<int>();
            foreach (int id in ids) if (id > 0) set.Add(id);
            var arr = new int[set.Count];
            set.CopyTo(arr);
            return arr;
        }

        public static string JsonEscape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length + 8);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        private static string ReadRaw(string obj, string key)
        {
            string k = "\"" + key + "\":";
            int p = obj.IndexOf(k, StringComparison.Ordinal);
            if (p < 0) return null;
            p += k.Length;
            int e = p;
            while (e < obj.Length && obj[e] != ',' ) e++;
            return obj.Substring(p, e - p).Trim();
        }

        private static int ReadInt(string obj, string key)
        {
            int v; int.TryParse(ReadRaw(obj, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out v); return v;
        }

        private static string ReadStr(string obj, string key)
        {
            string k = "\"" + key + "\":\"";
            int p = obj.IndexOf(k, StringComparison.Ordinal);
            if (p < 0) return "";
            p += k.Length;
            var sb = new StringBuilder();
            for (int i = p; i < obj.Length; i++)
            {
                char c = obj[i];
                if (c == '\\' && i + 1 < obj.Length)
                {
                    char n = obj[++i];
                    switch (n)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 < obj.Length) { sb.Append((char)Convert.ToInt32(obj.Substring(i + 1, 4), 16)); i += 4; }
                            break;
                        default: sb.Append(n); break;
                    }
                    continue;
                }
                if (c == '"') break;
                sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
