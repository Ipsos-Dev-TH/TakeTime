using System;
using System.Collections.Generic;
using Take_Time_BangPhra.Dining;

/// <summary>mono tests ของคลาสบริสุทธิ์ใน Class/Dining — รันด้วย tools/dining-tests/run.sh</summary>
static class DiningTests
{
    static int _pass, _fail;

    static void Check(bool cond, string name)
    {
        if (cond) { _pass++; Console.WriteLine("  ok   " + name); }
        else { _fail++; Console.WriteLine("  FAIL " + name); }
    }

    static void Eq<T>(T expected, T actual, string name)
    {
        bool ok = EqualityComparer<T>.Default.Equals(expected, actual);
        if (ok) { _pass++; Console.WriteLine("  ok   " + name); }
        else { _fail++; Console.WriteLine("  FAIL " + name + " — expected [" + expected + "] got [" + actual + "]"); }
    }

    // ── fixtures ─────────────────────────────────────────────────────────────

    static DiningOptionGroup Spice()
    {
        var g = new DiningOptionGroup { Id = 1, NameTh = "ระดับเผ็ด", MinSelect = 1, MaxSelect = 1, IsRequired = true };
        g.Options.Add(new DiningOption { Id = 11, GroupId = 1, NameTh = "ไม่เผ็ด", PriceDelta = 0 });
        g.Options.Add(new DiningOption { Id = 12, GroupId = 1, NameTh = "เผ็ดมาก", PriceDelta = 0 });
        return g;
    }

    static DiningOptionGroup Extras()
    {
        var g = new DiningOptionGroup { Id = 2, NameTh = "เพิ่ม", MinSelect = 0, MaxSelect = 2, IsRequired = false };
        g.Options.Add(new DiningOption { Id = 21, GroupId = 2, NameTh = "ไข่ดาว", PriceDelta = 10 });
        g.Options.Add(new DiningOption { Id = 22, GroupId = 2, NameTh = "พิเศษ", PriceDelta = 15.5m });
        g.Options.Add(new DiningOption { Id = 23, GroupId = 2, NameTh = "ยกเลิกแล้ว", PriceDelta = 5, IsActive = false });
        return g;
    }

    static Dictionary<int, DiningMenuItem> Menu()
    {
        var m = new Dictionary<int, DiningMenuItem>();
        var padthai = new DiningMenuItem { ProductId = 100, Name = "ผัดไทย", ProductName = "ผัดไทย", Price = 60m };
        padthai.OptionGroups.Add(Spice()); padthai.OptionGroups.Add(Extras());
        m[100] = padthai;
        m[200] = new DiningMenuItem { ProductId = 200, Name = "น้ำเปล่า", ProductName = "น้ำเปล่า", Price = 15m, TrackStock = true, StockQty = 3m };
        m[300] = new DiningMenuItem { ProductId = 300, Name = "หมดแล้ว", ProductName = "หมดแล้ว", Price = 99m, IsSoldOut = true };
        m[400] = new DiningMenuItem { ProductId = 400, Name = "ริมน้ำเท่านั้น", ProductName = "ริมน้ำเท่านั้น", Price = 120m, AvailableZones = "RIVER, POOL" };
        m[500] = new DiningMenuItem { ProductId = 500, Name = "หมดถึงเที่ยง", ProductName = "x", Price = 50m, IsSoldOut = true, SoldOutUntil = new DateTime(2026, 10, 29, 12, 0, 0) };
        m[600] = new DiningMenuItem { ProductId = 600, Name = "ปิดขาย", ProductName = "x", Price = 50m, IsDining = false };
        return m;
    }

    static Func<int, DiningMenuItem> Lookup(Dictionary<int, DiningMenuItem> m)
    {
        return delegate (int id) { DiningMenuItem it; return m.TryGetValue(id, out it) ? it : null; };
    }

    static readonly DateTime Now = new DateTime(2026, 10, 29, 18, 30, 0);

    // ── pricing ──────────────────────────────────────────────────────────────

    static void PricingTests()
    {
        Console.WriteLine("DiningPricing");
        var m = Menu();

        // ยอดพื้นฐาน + ตัวเลือก + VAT ถอด 7%
        var cart = new List<DiningCartLine>
        {
            new DiningCartLine { ProductId = 100, Qty = 2, OptionIds = new[] { 12, 21 }, Note = " ไม่ใส่ถั่ว " },
            new DiningCartLine { ProductId = 200, Qty = 3 }
        };
        var q = DiningPricing.ComputeQuote(cart, Lookup(m), "IN", 0m, true, Now, "KITCHEN");
        Check(q.Ok, "quote ok: " + string.Join(" | ", q.Errors.ToArray()));
        Eq(2, q.Lines.Count, "2 lines");
        Eq(140m, q.Lines[0].LineTotal, "ผัดไทย (60+10)x2 = 140");
        Eq(10m, q.Lines[0].OptionsPrice, "options price 10");
        Eq("ไม่ใส่ถั่ว", q.Lines[0].Note, "note trimmed");
        Eq("KITCHEN", q.Lines[0].Station, "station from zone");
        Eq(45m, q.Lines[1].LineTotal, "น้ำ 15x3 = 45");
        Eq(185m, q.Total, "total 185");
        Eq(5, q.ItemCount, "item count 5");
        Eq(12.10m, q.VatAmount, "vat extracted 185 - 172.90");
        Eq(172.90m, q.NetAmount, "net 172.90");
        Check(q.VatIncluded, "prices include VAT");
        Eq(2, q.Lines[0].Options.Count, "2 selected options");
        Eq("เผ็ดมาก", q.Lines[0].Options[0].Name, "sorted option ids → เผ็ดมาก first");

        // ไม่จด VAT
        var q2 = DiningPricing.ComputeQuote(cart, Lookup(m), "IN", 0m, false, Now);
        Eq(0m, q2.VatAmount, "no VAT when business not registered");
        Eq(185m, q2.NetAmount, "net = gross when no VAT");

        // รวมบรรทัดซ้ำ
        var dup = new List<DiningCartLine>
        {
            new DiningCartLine { ProductId = 200, Qty = 1 }, new DiningCartLine { ProductId = 200, Qty = 1 },
            new DiningCartLine { ProductId = 200, Qty = 1, Note = "เย็น" }
        };
        var q3 = DiningPricing.ComputeQuote(dup, Lookup(m), "IN", 0m, false, Now);
        Check(q3.Ok, "merged quote ok");
        Eq(2, q3.Lines.Count, "same product+note merged, different note separate");
        Eq(2m, q3.Lines[0].Qty, "merged qty 2");

        // required option missing
        var q4 = DiningPricing.ComputeQuote(new List<DiningCartLine> { new DiningCartLine { ProductId = 100, Qty = 1 } }, Lookup(m), "IN", 0m, true, Now);
        Check(!q4.Ok && q4.Errors[0].Contains("ระดับเผ็ด"), "required option group enforced: " + (q4.Errors.Count > 0 ? q4.Errors[0] : ""));
        Eq(DiningError.Validation, q4.ErrorCode, "error code VALIDATION");

        // max select exceeded + inactive option + foreign option
        var q5 = DiningPricing.ComputeQuote(new List<DiningCartLine> { new DiningCartLine { ProductId = 100, Qty = 1, OptionIds = new[] { 11, 21, 22, 23 } } }, Lookup(m), "IN", 0m, true, Now);
        Check(!q5.Ok, "inactive option rejected");
        var q6 = DiningPricing.ComputeQuote(new List<DiningCartLine> { new DiningCartLine { ProductId = 200, Qty = 1, OptionIds = new[] { 11 } } }, Lookup(m), "IN", 0m, true, Now);
        Check(!q6.Ok && q6.Errors[0].Contains("#11"), "option from another item rejected");
        var q6b = DiningPricing.ComputeQuote(new List<DiningCartLine> { new DiningCartLine { ProductId = 100, Qty = 1, OptionIds = new[] { 11, 12 } } }, Lookup(m), "IN", 0m, true, Now);
        Check(!q6b.Ok && q6b.Errors[0].Contains("ไม่เกิน 1"), "max select 1 enforced");

        // sold out / zone / stock / inactive / unknown
        Check(!DiningPricing.ComputeQuote(new List<DiningCartLine> { new DiningCartLine { ProductId = 300, Qty = 1 } }, Lookup(m), "IN", 0m, true, Now).Ok, "sold out rejected");
        Eq(DiningError.SoldOut, DiningPricing.ComputeQuote(new List<DiningCartLine> { new DiningCartLine { ProductId = 300, Qty = 1 } }, Lookup(m), "IN", 0m, true, Now).ErrorCode, "SOLD_OUT code");
        Check(DiningPricing.ComputeQuote(new List<DiningCartLine> { new DiningCartLine { ProductId = 500, Qty = 1 } }, Lookup(m), "IN", 0m, true, Now).Ok, "sold-out-until passed → orderable");
        Check(!DiningPricing.ComputeQuote(new List<DiningCartLine> { new DiningCartLine { ProductId = 500, Qty = 1 } }, Lookup(m), "IN", 0m, true, Now.AddHours(-8)).Ok, "sold-out-until not passed → rejected");
        Check(!DiningPricing.ComputeQuote(new List<DiningCartLine> { new DiningCartLine { ProductId = 400, Qty = 1 } }, Lookup(m), "IN", 0m, true, Now).Ok, "zone filter rejects IN");
        Check(DiningPricing.ComputeQuote(new List<DiningCartLine> { new DiningCartLine { ProductId = 400, Qty = 1 } }, Lookup(m), "pool", 0m, true, Now).Ok, "zone filter accepts POOL (case-insensitive)");
        var q7 = DiningPricing.ComputeQuote(new List<DiningCartLine> { new DiningCartLine { ProductId = 200, Qty = 4 } }, Lookup(m), "IN", 0m, true, Now);
        Eq(DiningError.OutOfStock, q7.ErrorCode, "stock 3 < qty 4 → OUT_OF_STOCK");
        Eq(DiningError.NotFound, DiningPricing.ComputeQuote(new List<DiningCartLine> { new DiningCartLine { ProductId = 600, Qty = 1 } }, Lookup(m), "IN", 0m, true, Now).ErrorCode, "Is_Dining=0 → NOT_FOUND");
        Eq(DiningError.NotFound, DiningPricing.ComputeQuote(new List<DiningCartLine> { new DiningCartLine { ProductId = 999, Qty = 1 } }, Lookup(m), "IN", 0m, true, Now).ErrorCode, "unknown product → NOT_FOUND");

        // qty / lines / min order
        Check(!DiningPricing.ComputeQuote(new List<DiningCartLine> { new DiningCartLine { ProductId = 200, Qty = 0 } }, Lookup(m), "IN", 0m, true, Now).Ok, "qty 0 rejected");
        Check(!DiningPricing.ComputeQuote(new List<DiningCartLine> { new DiningCartLine { ProductId = 200, Qty = 100 } }, Lookup(m), "IN", 0m, true, Now).Ok, "qty 100 rejected (max 99)");
        Check(!DiningPricing.ComputeQuote(new List<DiningCartLine> { new DiningCartLine { ProductId = 200, Qty = 1.5m } }, Lookup(m), "IN", 0m, true, Now).Ok, "fractional qty rejected");
        var big = new List<DiningCartLine>();
        for (int i = 0; i < 51; i++) big.Add(new DiningCartLine { ProductId = 100, Qty = 1, OptionIds = new[] { 11 }, Note = "n" + i });
        Check(!DiningPricing.ComputeQuote(big, Lookup(m), "IN", 0m, true, Now).Ok, "51 lines rejected");
        Eq(DiningError.Validation, DiningPricing.ComputeQuote(new List<DiningCartLine>(), Lookup(m), "IN", 0m, true, Now).ErrorCode, "empty cart");
        var q8 = DiningPricing.ComputeQuote(new List<DiningCartLine> { new DiningCartLine { ProductId = 200, Qty = 1 } }, Lookup(m), "IN", 100m, true, Now);
        Eq(DiningError.MinOrder, q8.ErrorCode, "min order 100 > 15 → MIN_ORDER");
        Check(q8.Errors[0].Contains("100.00"), "min order message shows amount");

        // rounding + VAT helper
        decimal net, vat;
        DiningPricing.ExtractVat(107m, true, out net, out vat);
        Eq(100m, net, "107 → net 100"); Eq(7m, vat, "107 → vat 7");
        DiningPricing.ExtractVat(185m, true, out net, out vat);
        Eq(172.90m, net, "185 → net 172.90"); Eq(12.10m, vat, "185 → vat 12.10");
        Eq(0.01m, DiningPricing.R2(0.005m), "R2 half away from zero");
        Eq(1.13m, DiningPricing.R2(1.125m), "R2 1.125 → 1.13");

        // options json round-trip
        var opts = new List<DiningSelectedOption> { new DiningSelectedOption { GroupId = 1, GroupName = "ระดับ \"เผ็ด\"", OptionId = 12, Name = "เผ็ด\nมาก", PriceDelta = 15.5m } };
        string json = DiningPricing.OptionsToJson(opts);
        var back = DiningPricing.OptionsFromJson(json);
        Eq(1, back.Count, "json round-trip count");
        Eq("ระดับ \"เผ็ด\"", back[0].GroupName, "json escapes quotes");
        Eq("เผ็ด\nมาก", back[0].Name, "json escapes newline");
        Eq(15.5m, back[0].PriceDelta, "json price delta");
        Eq(12, back[0].OptionId, "json option id");
        Eq(0, DiningPricing.OptionsFromJson("").Count, "empty json → none");
        Eq(0, DiningPricing.OptionsFromJson("garbage").Count, "garbage json → none");
    }

    // ── tokens ───────────────────────────────────────────────────────────────

    static void TokenTests()
    {
        Console.WriteLine("DiningTokens");
        string t1 = DiningTokens.NewToken(), t2 = DiningTokens.NewToken();
        Eq(43, t1.Length, "token length 43");
        Check(t1 != t2, "tokens differ");
        Check(DiningTokens.IsValidToken(t1), "generated token valid");
        Check(!DiningTokens.IsValidToken(t1 + "x"), "44 chars invalid");
        Check(!DiningTokens.IsValidToken(t1.Substring(1) + "+"), "'+' invalid");
        Check(!DiningTokens.IsValidToken(null) && !DiningTokens.IsValidToken(""), "null/empty invalid");
        Check(DiningTokens.ConstantTimeEquals(t1, t1), "constant-time equal");
        Check(!DiningTokens.ConstantTimeEquals(t1, t2), "constant-time not equal");
        Check(!DiningTokens.ConstantTimeEquals(t1, null), "null not equal");
        Check(!DiningTokens.ConstantTimeEquals("abc", "abcd"), "different length not equal");

        Eq("0812345678", DiningTokens.NormalizePhone("081-234-5678"), "dashes stripped");
        Eq("0812345678", DiningTokens.NormalizePhone("+66 81 234 5678"), "+66 → 0");
        Eq("0812345678", DiningTokens.NormalizePhone("66812345678"), "66 → 0");
        Eq("0812345678", DiningTokens.NormalizePhone("0066812345678"), "0066 → 0");
        Eq("0812345678", DiningTokens.NormalizePhone("๐๘๑๒๓๔๕๖๗๘"), "thai digits");
        Eq("", DiningTokens.NormalizePhone("  "), "blank → empty");
        Eq("", DiningTokens.NormalizePhone("abc"), "letters → empty");
        Eq("0212345678", DiningTokens.NormalizePhone("(02) 123-4567 8"), "landline");
        Check(DiningTokens.PhoneMatches("+66812345678", "081-234-5678"), "match after normalize");
        Check(!DiningTokens.PhoneMatches("0812345679", "0812345678"), "mismatch");
        Check(!DiningTokens.PhoneMatches("12345", "12345"), "too short never matches");
        Eq("08*****678", DiningTokens.MaskPhone("0812345678"), "mask phone");
        Eq("****", DiningTokens.MaskPhone("1234"), "mask short");
        Eq(64, DiningTokens.Sha256Hex("x").Length, "sha256 hex 64");
        Eq(DiningTokens.Sha256Hex("a|b"), DiningTokens.DeviceHash("a", "b"), "device hash deterministic");
        Eq("abcdefgh-123_X", DiningTokens.SanitizeIdempotencyKey(" abcdefgh-123_X "), "idempotency key trimmed");
        Eq(null, DiningTokens.SanitizeIdempotencyKey("short"), "idempotency < 8 → null");
        Eq(null, DiningTokens.SanitizeIdempotencyKey("abcdefgh;drop"), "idempotency bad char → null");
        Eq(null, DiningTokens.SanitizeIdempotencyKey(new string('a', 65)), "idempotency > 64 → null");
    }

    // ── status transitions ───────────────────────────────────────────────────

    static void StatusTests()
    {
        Console.WriteLine("DiningStatus.Order");
        Check(DiningStatus.Order.CanTransition("NEW", "ACCEPTED"), "NEW→ACCEPTED");
        Check(DiningStatus.Order.CanTransition("NEW", "SERVED"), "NEW→SERVED (skip)");
        Check(DiningStatus.Order.CanTransition("ACCEPTED", "PREPARING"), "ACCEPTED→PREPARING");
        Check(DiningStatus.Order.CanTransition("PREPARING", "SERVED"), "PREPARING→SERVED");
        Check(DiningStatus.Order.CanTransition("PREPARING", "CANCELLED"), "PREPARING→CANCELLED");
        Check(!DiningStatus.Order.CanTransition("SERVED", "CANCELLED"), "SERVED→CANCELLED blocked");
        Check(!DiningStatus.Order.CanTransition("SERVED", "PREPARING"), "no going back");
        Check(!DiningStatus.Order.CanTransition("CANCELLED", "NEW"), "cancelled is final");
        Check(!DiningStatus.Order.CanTransition("new", "new"), "same status no-op");
        Check(DiningStatus.Order.CanTransition("new", "accepted"), "case-insensitive");
        Eq("เสิร์ฟแล้ว", DiningStatus.Order.Thai("SERVED"), "thai label");
        Eq("ลงบิลห้องพัก", DiningStatus.Pay.Thai("CHARGED"), "pay thai label");

        var it = new DiningMenuItem { IsSoldOut = true, SoldOutUntil = Now.AddMinutes(1) };
        Check(it.SoldOutNow(Now), "sold out until future");
        Check(!it.SoldOutNow(Now.AddMinutes(2)), "sold out expired");
        var o = new DiningOrder { OrderStatus = "NEW", PaymentStatus = "UNPAID" };
        Check(o.IsOutstanding && o.IsPayable, "unpaid new order outstanding+payable");
        o.PendingPaymentId = 5;
        Check(o.IsOutstanding && !o.IsPayable, "pending slip → outstanding but not payable");
        o.OrderStatus = "CANCELLED";
        Check(!o.IsOutstanding, "cancelled not outstanding");
    }

    // ── notify text ──────────────────────────────────────────────────────────

    static void NotifyTests()
    {
        Console.WriteLine("DiningNotifyText");
        var s = new DiningTableSession { Id = 7, TableId = 3, TableCode = "A3", ZoneName = "ริมน้ำ" };
        var o = new DiningOrder { Id = 42, OrderNo = "DN261029-0007", RoundNo = 2, TotalAmount = 185m, PaymentStatus = "UNPAID", CreatedAt = Now, GuestName = "<b>คุณเอ</b>", GuestNote = "ไม่ใส่ผัก & เร็ว <script>" };
        o.Items.Add(new DiningOrderItem { ProductName = "ผัดไทย <x>", Qty = 2, LineTotal = 140m, ItemNote = "เผ็ด<ม>าก", Options = new List<DiningSelectedOption> { new DiningSelectedOption { Name = "ไข่ดาว", PriceDelta = 10 } } });
        o.Items.Add(new DiningOrderItem { ProductName = "น้ำเปล่า", Qty = 3, LineTotal = 45m });
        var m = DiningNotifyText.NewOrder(o, s, DiningNotifyText.PaymentStateText("CASH", false, false), "https://x.test/Admin/Dining/Orders?o=42");
        Eq(DiningNotifyText.EvOrder, m.Event, "event ORDER_DINING");
        Eq("HIGH", m.Priority, "new order HIGH");
        Check(m.Html.Contains("<b>ออเดอร์ใหม่ โต๊ะ A3 (ริมน้ำ) รอบ 2</b>"), "header");
        Check(m.Html.Contains("&lt;b&gt;คุณเอ&lt;/b&gt;"), "guest name escaped");
        Check(m.Html.Contains("ไม่ใส่ผัก &amp; เร็ว &lt;script&gt;"), "note escaped");
        Check(m.Html.Contains("ผัดไทย &lt;x&gt; x2 — 140.00 บาท (ไข่ดาว (+10)) ✎ เผ็ด&lt;ม&gt;าก"), "item line with options + note");
        Check(m.Html.Contains("💰 รวม <b>185.00</b> บาท · 💵 เงินสด — ชำระที่โต๊ะ"), "total + payment state");
        Check(m.Html.Contains("https://x.test/Admin/Dining/Orders?o=42"), "link");
        Check(!m.Text.Contains("<b>ออเดอร์ใหม่") && m.Text.Contains("ออเดอร์ใหม่ โต๊ะ A3") && m.Text.Contains("<b>คุณเอ</b>"), "plain text: markup stripped, guest text restored verbatim");
        Check(m.Text.Contains("ไม่ใส่ผัก & เร็ว <script>"), "plain text restores & and <");
        Check(m.Title.Contains("DN261029-0007"), "title has order no");

        var st = DiningNotifyText.StatusChanged(o, s, "NEW", "CANCELLED", "พนักงาน<1>", "ลูกค้า<เปลี่ยนใจ>", null);
        Eq(DiningNotifyText.EvOrderStatus, st.Event, "status event");
        Eq("HIGH", st.Priority, "cancel is HIGH");
        Check(st.Html.Contains("❌ <b>ยกเลิก</b>") && st.Html.Contains("พนักงาน&lt;1&gt;") && st.Html.Contains("ลูกค้า&lt;เปลี่ยนใจ&gt;"), "status html escaped");
        Eq("NORMAL", DiningNotifyText.StatusChanged(o, s, "NEW", "ACCEPTED", null, null, null).Priority, "accept NORMAL");

        var p = new DiningPayment { Amount = 185m, PaidHowName = "KBANK <x>", SlipBankAmount = 190m, SlipTransRef = "REF<1>" };
        var sv = DiningNotifyText.SlipVerified(p, s, new List<string> { "DN261029-0007", "DN261029-0008" }, null);
        Check(sv.Html.Contains("185.00 บาท (สลิป 190.00) → KBANK &lt;x&gt;"), "slip verified amounts + paid how escaped");
        Check(sv.Html.Contains("🧾 ออเดอร์: DN261029-0007, DN261029-0008"), "covered order nos");
        Check(sv.Html.Contains("REF&lt;1&gt;"), "trans ref escaped");
        Check(DiningNotifyText.SlipPending(p, s, null, "อ่านยอดไม่ได้", null).Html.Contains("⏳ <b>สลิปรอพนักงานตรวจ</b>"), "slip pending header");
        Check(DiningNotifyText.SlipRejected(s, 185m, "สลิปซ้ำ <dup>").Html.Contains("สาเหตุ: สลิปซ้ำ &lt;dup&gt;"), "slip rejected reason escaped");
        Check(DiningNotifyText.CashReceived(p, s, null, "แคชเชียร์").Html.Contains("💵 <b>รับเงินสดแล้ว</b> โต๊ะ A3 (ริมน้ำ)"), "cash header");
        var rc = DiningNotifyText.RoomCharged(new DiningPayment { Amount = 185m, ReservationId = 149 }, s, "B2 <vip>", null);
        Eq(DiningNotifyText.EvBill, rc.Event, "room charge → DINING_BILL");
        Check(rc.Html.Contains("ห้อง B2 &lt;vip&gt; (การจอง #149)"), "room label escaped + reservation");
        var lk = DiningNotifyText.RoomChargeLocked(s, 5, new DateTime(2026, 10, 29, 18, 45, 0));
        Check(lk.Html.Contains("ผิดครบ 5 ครั้ง") && lk.Html.Contains("18:45") && lk.Priority == "HIGH", "lockout message");
        var cs = DiningNotifyText.CallStaff(s, "ขอน้ำแข็ง <เพิ่ม>");
        Eq(DiningNotifyText.EvCallStaff, cs.Event, "call staff event");
        Check(cs.Html.Contains("💬 ขอน้ำแข็ง &lt;เพิ่ม&gt;"), "call message escaped");
        var bill = DiningNotifyText.BillRequested(s, 185m, 2);
        Check(bill.Html.Contains("💰 ยอดค้าง <b>185.00</b> บาท (2 ออเดอร์)"), "bill outstanding");
        Check(DiningNotifyText.BillRequested(s, 0m, 2).Html.Contains("ชำระครบแล้ว"), "bill zero outstanding");
        var cl = DiningNotifyText.TableClosed(s, "ผู้จัดการ", "ลูกค้าหนี", 50m, false);
        Check(cl.Html.Contains("⚠️ ปิดทั้งที่ยังค้างชำระ 50.00 บาท") && cl.Priority == "HIGH", "close with outstanding");
        var ac = DiningNotifyText.TableClosed(s, null, null, 0m, true);
        Check(ac.Html.Contains("ปิดโต๊ะอัตโนมัติ") && ac.Priority == "NORMAL", "auto close");
        Eq("✅ โอนแล้ว ตรวจสลิปผ่าน", DiningNotifyText.PaymentStateText("TRANSFER", true, false), "payment state verified");
        Eq("⏳ โอนแล้ว สลิปรอตรวจ", DiningNotifyText.PaymentStateText("transfer", false, true), "payment state pending (case-insensitive)");
        Eq("ยังไม่ชำระ (จ่ายตอนเช็คบิล)", DiningNotifyText.PaymentStateText(null, false, false), "payment state none");
        Eq("a &amp; b &lt;c&gt;", DiningNotifyText.E("a & b <c>"), "E escapes");
        Eq("", DiningNotifyText.E(null), "E null → empty");
        Eq("x & <y>", DiningNotifyText.StripHtml("<b>x</b> &amp; &lt;y&gt;"), "StripHtml");
        var nos = DiningNotifyText.NewOrder(o, null, null, null);
        Check(nos.Html.Contains("โต๊ะ ?"), "null session tolerated");
    }

    static int Main()
    {
        PricingTests();
        TokenTests();
        StatusTests();
        NotifyTests();
        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "ALL PASSED (" + _pass + ")" : "FAILED " + _fail + " / passed " + _pass);
        return _fail == 0 ? 0 : 1;
    }
}
