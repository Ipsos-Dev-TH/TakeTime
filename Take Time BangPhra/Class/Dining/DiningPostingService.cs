using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using Take_Time_BangPhra.Integration;

namespace Take_Time_BangPhra.Dining
{
    /// <summary>
    /// งานเบื้องหลัง (timer ใน Global.asax — ทีมกระดานพนักงานเพิ่มจุดเรียก): รายได้ + ต้นทุน/สต๊อกของ QR สั่งอาหาร
    ///   • Dining_Payment CASH/TRANSFER ที่ CONFIRMED และ Acct_Post_Ref IS NULL → จองด้วย '~' → ChannelSalesPostingService.Record
    ///     (Channel DINING, SourceKey DNP-{paymentId}, SaleDate = Paid_At, Amount = ยอดรวม VAT) → นโยบายเอกสารขายตัดสิน IMMEDIATE/DAILY_ROLLUP/OFF
    ///     (ไม่ seed กฎ DINING = ตกค่ากลาง (*,CASH)=DAILY_ROLLUP, (*,*)=IMMEDIATE) → Acct_Post_Ref = DNP-{id}
    ///   • รายการ Track_Stock ของออเดอร์ที่ครอบ (Stock_Posted=0) → Product_Out (Remark 'ร้านอาหาร', Account_Receipt_ID = DNP-{id}) + EnqueueStockOutCogs
    ///     (stockRef DNCOGS-{orderId}-{pid}) — ลอก RevenuePostingService.cs:334-351 · อาหารปรุง (Track_Stock=0) ไม่ตัด (วัตถุดิบตัดผ่านเบิกใช้ครัว)
    ///   • ROOM_CHARGE → Acct_Post_Ref = RC:{ref} ข้าม (รายได้ไปกับใบเสร็จเช็คเอาท์; สต๊อกตัดโดย RoomChargeService แล้ว)
    /// idempotent: claim '~' + IF NOT EXISTS ใน Product_Out + dedup คิว; claim ค้างเกิน 15 นาที (process ตาย) = ปล่อยให้รอบถัดไปทำต่อ
    /// ไม่มีตาราง Sales_Channel_Posting (ยังไม่รัน PHASE19_32) = ไม่จอง/ไม่ทำอะไร (รอ)
    /// </summary>
    public class DiningPostingService
    {
        private readonly string _conn;
        public DiningPostingService() : this(DiningDb.ConnStr) { }
        public DiningPostingService(string connectionString) { _conn = connectionString; }

        /// <summary>ประมวลผลการชำระที่ยังไม่ลงบัญชี — คืนจำนวนที่ทำสำเร็จ; ไม่ throw</summary>
        public int PostIfDue(int max = 100)
        {
            int done = 0;
            try
            {
                if (!DiningDb.Ready(_conn)) return 0;
                // claim ค้าง (process ตายกลางทาง) → ปล่อย
                DiningDb.Exec(_conn, "UPDATE dbo.Dining_Payment SET Acct_Post_Ref = NULL WHERE Acct_Post_Ref = '~' AND Updated_At < DATEADD(MINUTE, -15, GETDATE())");

                // ลงบิลห้อง: ไม่โพสต์
                DiningDb.Exec(_conn, @"UPDATE dbo.Dining_Payment SET Acct_Post_Ref = LEFT('RC:' + ISNULL(Room_Charge_Ref, 'ROOM'), 60), Updated_At = GETDATE()
                                       WHERE Method = 'ROOM_CHARGE' AND Status = 'CONFIRMED' AND Acct_Post_Ref IS NULL");

                bool ledger = ChannelPosting.TableReady(_conn);
                if (!ledger) { LogOnce("dining-noledger", "ยังไม่รัน PHASE19_32 (Sales_Channel_Posting) — รายได้ร้านอาหารรอโพสต์"); return 0; }
                var cfg = new AccountingConfig(_conn);
                bool acct = cfg.IsConfigured && cfg.Enabled;

                var dt = DiningDb.Query(_conn, @"SELECT TOP (@n) p.ID FROM dbo.Dining_Payment p
                                                  WHERE p.Status = 'CONFIRMED' AND p.Method IN ('CASH','TRANSFER') AND p.Acct_Post_Ref IS NULL AND p.Amount > 0
                                                  ORDER BY p.ID", new Dictionary<string, object> { { "@n", Math.Max(1, max) } });
                foreach (DataRow r in dt.Rows)
                {
                    long id = DiningDb.Long(r["ID"]);
                    try { if (PostOne(id, acct)) done++; }
                    catch (Exception ex)
                    {
                        DiningDb.Log(_conn, "โพสต์การชำระ #" + id + " ล้มเหลว: " + ex.Message, "SYSTEM");
                        try { DiningDb.Exec(_conn, "UPDATE dbo.Dining_Payment SET Acct_Post_Ref = NULL WHERE ID = @id AND Acct_Post_Ref = '~'", new Dictionary<string, object> { { "@id", id } }); } catch { }
                    }
                }
            }
            catch (Exception ex) { DiningDb.Log(_conn, "DiningPostingService.PostIfDue ล้มเหลว: " + ex.Message, "SYSTEM"); }
            return done;
        }

        private bool PostOne(long paymentId, bool accountingOn)
        {
            int claimed = DiningDb.Exec(_conn, "UPDATE dbo.Dining_Payment SET Acct_Post_Ref = '~', Updated_At = GETDATE() WHERE ID = @id AND Acct_Post_Ref IS NULL AND Status = 'CONFIRMED'",
                new Dictionary<string, object> { { "@id", paymentId } });
            if (claimed <= 0) return false;

            var pay = DiningPaymentService.LoadPayment(_conn, paymentId);
            if (pay == null) return false;
            var tables = new DiningTableService(_conn);
            var s = tables.GetSessionById(pay.SessionId);
            var orders = new DiningOrderService(_conn);
            var covered = new List<DiningOrder>();
            foreach (long oid in pay.CoversOrderIds) { var o = orders.GetOrder(oid); if (o != null) covered.Add(o); }

            string sourceKey = "DNP-" + paymentId.ToString(CultureInfo.InvariantCulture);
            var nos = new List<string>();
            foreach (var o in covered) nos.Add(o.OrderNo);
            string desc = ChannelPosting.Trunc("ร้านอาหาร " + (s != null ? s.Label : "") + (nos.Count > 0 ? " " + string.Join(", ", nos.ToArray()) : "") + " (" + DiningStatus.Method.Thai(pay.Method) + ")", 300);

            // 1) รายได้ → สมุดขายรายช่องทาง (นโยบายเอกสารขายตัดสิน IMMEDIATE/DAILY_ROLLUP/OFF; นโยบายปิด = NOPOLICY ไว้ตรวจย้อน)
            var sale = new ChannelSale
            {
                Channel = DiningConfig.SalesChannel,
                SourceKey = sourceKey,
                SaleDate = pay.PaidAt ?? pay.CreatedAt,
                PaidHowId = pay.PaidHowId ?? 0,
                PaidHowName = pay.PaidHowName,
                Amount = pay.Amount,
                Description = desc,
                CustomerName = s != null ? s.GuestName : null,
                CustomerPhone = s != null ? s.GuestPhone : null,
                CreatedBy = "DINING"
            };
            string msg = new ChannelSalesPostingService(_conn).Record(sale);
            if (msg == null)
            {
                // ไม่มีตาราง/ล้มเหลว → ปล่อย claim ให้รอบถัดไป
                DiningDb.Exec(_conn, "UPDATE dbo.Dining_Payment SET Acct_Post_Ref = NULL WHERE ID = @id AND Acct_Post_Ref = '~'", new Dictionary<string, object> { { "@id", paymentId } });
                LogOnce("dining-record-" + paymentId, "บันทึกการขาย " + sourceKey + " ไม่สำเร็จ — จะลองใหม่รอบถัดไป");
                return false;
            }

            // 2) ต้นทุน + สต๊อกในระบบ (รายการ Track_Stock ของออเดอร์ที่ยังไม่ตัด)
            int stockLines = 0;
            DateTime outDate = sale.SaleDate;
            AccountingSyncService sync = accountingOn ? new AccountingSyncService(_conn) : null;
            foreach (var o in covered)
            {
                if (o.StockPosted) continue;
                foreach (var it in o.Items)
                {
                    if (!it.TrackStock || it.Qty <= 0m || it.KitchenStatus == DiningStatus.Kitchen.Void) continue;
                    WriteStockOut(sourceKey, it.ProductId, it.Qty, it.Qty > 0m ? DiningPricing.R2(it.LineTotal / it.Qty) : 0m, outDate);
                    stockLines++;
                    if (sync != null)
                    {
                        try
                        {
                            decimal cost = LookupCost(it.ProductId);
                            sync.EnqueueStockOutCogs(it.ProductId, it.ProductName, it.Qty, cost, outDate, "ร้านอาหาร (QR สั่งอาหาร)",
                                stockRef: "DNCOGS-" + o.Id.ToString(CultureInfo.InvariantCulture) + "-" + it.ProductId.ToString(CultureInfo.InvariantCulture));
                        }
                        catch (Exception ex) { DiningDb.Log(_conn, "COGS " + o.OrderNo + " สินค้า #" + it.ProductId + " ไม่สำเร็จ: " + ex.Message, "SYSTEM"); }
                    }
                }
                DiningDb.Exec(_conn, "UPDATE dbo.Dining_Order SET Stock_Posted = 1 WHERE ID = @id", new Dictionary<string, object> { { "@id", o.Id } });
            }

            DiningDb.Exec(_conn, "UPDATE dbo.Dining_Payment SET Acct_Post_Ref = @r, Updated_At = GETDATE() WHERE ID = @id AND Acct_Post_Ref = '~'",
                new Dictionary<string, object> { { "@id", paymentId }, { "@r", sourceKey } });
            DiningDb.Log(_conn, "โพสต์ " + sourceKey + " " + pay.Amount.ToString("N2", CultureInfo.InvariantCulture) + " (" + pay.PaidHowName + "): " + msg + " · สต๊อก " + stockLines + " รายการ", "SYSTEM");
            return true;
        }

        /// <summary>Product_Out ของร้านอาหาร — Remark 'ร้านอาหาร' + Account_Receipt_ID = DNP-{id} (ไม่เข้าเงื่อนไขรวบ POS: Remark 'ขาย' + '0') · IF NOT EXISTS กันซ้ำ</summary>
        private void WriteStockOut(string refId, int productId, decimal qty, decimal pricePerUnit, DateTime outDate)
        {
            try
            {
                DiningDb.Exec(_conn, @"IF NOT EXISTS (SELECT 1 FROM Product_Out WHERE Account_Receipt_ID = @r AND Product_ID = @pid AND Remark = @rm)
                                       INSERT INTO Product_Out (DateTime_Out, Product_ID, Amount, PricePerUnit, Account_Receipt_ID, Remark)
                                       VALUES (@dt, @pid, @qty, @price, @r, @rm)",
                    new Dictionary<string, object>
                    {
                        { "@r", refId }, { "@pid", productId }, { "@qty", qty }, { "@price", pricePerUnit }, { "@dt", outDate }, { "@rm", DiningConfig.StockOutRemark }
                    });
            }
            catch (Exception ex) { DiningDb.Log(_conn, "ตัดสต๊อก " + refId + " สินค้า #" + productId + " ไม่สำเร็จ: " + ex.Message, "SYSTEM"); }
        }

        private decimal LookupCost(int productId)
        {
            try { return DiningDb.Dec(DiningDb.Scalar(_conn, "SELECT ISNULL(Cost_Price, 0) FROM Product WHERE ID = @id", new Dictionary<string, object> { { "@id", productId } })); }
            catch { return 0m; }
        }

        private static readonly Dictionary<string, DateTime> _once = new Dictionary<string, DateTime>();
        private void LogOnce(string key, string msg)
        {
            lock (_once)
            {
                DateTime t;
                if (_once.TryGetValue(key, out t) && (DateTime.UtcNow - t).TotalMinutes < 30) return;
                _once[key] = DateTime.UtcNow;
            }
            DiningDb.Log(_conn, msg, "SYSTEM");
        }
    }
}
