using System;
using System.Collections.Generic;
using System.Data;

namespace Take_Time_BangPhra.Inventory
{
    /// <summary>
    /// ต้นทุนสินค้าแบบถัวเฉลี่ยเคลื่อนที่ (moving average) — เก็บที่ <c>Product.Cost_Price</c>.
    ///
    /// เดิม Cost_Price ไม่เคยถูกอัปเดต (ตั้งครั้งเดียวตอนสร้างสินค้า) ขณะที่รับของลง Dr สินค้าคงเหลือ "ราคาที่จ่ายจริง"
    /// แต่ COGS/ตัดจำหน่าย/คืนสต๊อก ใช้ Cost_Price คงที่ ⇒ ยอดสินค้าคงเหลือใน GL ลอยออกจาก จำนวน × ต้นทุน.
    /// ตอนนี้ทุกการรับซื้อ (Product/In) คำนวณค่าเฉลี่ยใหม่:
    ///     avg' = (onHand × avg + qtyIn × unitCost) / (onHand + qtyIn)        (onHand ≤ 0 หรือ avg ≤ 0 → avg' = unitCost)
    /// แล้วทุกเส้นที่อ่าน Cost_Price อยู่แล้ว (ขาย POS/room charge/รวบรายวัน, ตัดจำหน่าย, ยกเลิก charge)
    /// ได้ต้นทุนเฉลี่ยอัตโนมัติ โดยไม่ต้องแก้โค้ดฝั่งนั้น.
    ///
    /// ไม่ต้องมีบัญชี "ผลต่างราคาซื้อ" (purchase price variance): ส่วนต่างระหว่างราคารับเข้ากับค่าเฉลี่ยเดิมถูก "ดูดซับ"
    /// เข้าค่าเฉลี่ยใหม่ทันที (ต่างจาก standard cost ที่ต้องแยกผลต่าง) — สินค้าคงเหลือ GL = Σ(รับเข้า) − Σ(จ่ายออก ณ ค่าเฉลี่ย)
    /// = จำนวนคงเหลือ × ค่าเฉลี่ย (เหลือแค่เศษปัด). ข้อยกเว้นคือสต๊อกติดลบ (ขายก่อนรับ) ที่ค่าเฉลี่ยรีเซ็ตเป็นราคารับล่าสุด
    /// — ผลต่างกรณีนั้นเล็กและแก้ด้วยการนับสต๊อก.
    ///
    /// คืนสินค้าให้ผู้ขาย: ตัดออก "ณ ค่าเฉลี่ยปัจจุบัน" (เหมือน NextAcc — คืน vendor ใช้ WAC ปัจจุบัน
    /// DocumentService.cs:17183-17186) ⇒ ค่าเฉลี่ยไม่เปลี่ยน; ส่วนต่างระหว่างยอดที่ผู้ขายลดหนี้/คืนเงิน (ราคาซื้อ)
    /// กับมูลค่าที่ตัดออก (ค่าเฉลี่ย) ลงบัญชี PURCHASE_RETURNS (51300) ใน JE คืนสินค้า.
    /// </summary>
    public static class CostService
    {
        /// <summary>สูตรค่าเฉลี่ยเคลื่อนที่ (pure — ใช้ทดสอบได้). ปัด 4 ตำแหน่ง</summary>
        public static decimal ComputeMovingAverage(decimal onHandBefore, decimal avgBefore, decimal qtyIn, decimal unitCost)
        {
            if (qtyIn <= 0) return avgBefore;
            if (unitCost < 0) unitCost = 0;
            if (onHandBefore <= 0 || avgBefore <= 0)
                return Math.Round(unitCost, 4, MidpointRounding.AwayFromZero);
            decimal total = onHandBefore * avgBefore + qtyIn * unitCost;
            return Math.Round(total / (onHandBefore + qtyIn), 4, MidpointRounding.AwayFromZero);
        }

        /// <summary>จำนวนคงเหลือปัจจุบัน = Σ Product_In − Σ Product_Out (ร้านเดียว ไม่มีคลัง — เหมือนหน้า Product/Stock)</summary>
        public static decimal OnHand(string conn, int productId)
        {
            try
            {
                var dt = new code().DatabaseQuerySafe(conn,
                    @"SELECT ISNULL((SELECT SUM(CAST(Amount AS DECIMAL(18,4))) FROM Product_In WHERE Product_ID = @id), 0)
                           - ISNULL((SELECT SUM(CAST(Amount AS DECIMAL(18,4))) FROM Product_Out WHERE Product_ID = @id), 0) AS OnHand",
                    new Dictionary<string, object> { { "@id", productId } });
                if (dt != null && dt.Rows.Count > 0 && dt.Rows[0][0] != DBNull.Value)
                    return Convert.ToDecimal(dt.Rows[0][0]);
            }
            catch { }
            return 0m;
        }

        /// <summary>ต้นทุนเฉลี่ยปัจจุบัน (Product.Cost_Price; ไม่มี = 0)</summary>
        public static decimal CurrentAverage(string conn, int productId)
        {
            try
            {
                var dt = new code().DatabaseQuerySafe(conn,
                    "SELECT TOP 1 ISNULL(Cost_Price, 0) FROM Product WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", productId } });
                if (dt != null && dt.Rows.Count > 0 && dt.Rows[0][0] != DBNull.Value)
                    return Convert.ToDecimal(dt.Rows[0][0]);
            }
            catch { }
            return 0m;
        }

        /// <summary>
        /// อัปเดตค่าเฉลี่ยหลังรับซื้อ. เรียก "ก่อน" insert แถว Product_In ของการรับนี้ (qtyAlreadyInserted=false)
        /// หรือหลัง insert แล้ว (true — หักจำนวนนี้ออกจาก onHand ก่อนคำนวณ).
        /// คำนวณใน batch เดียว (อ่าน onHand + Cost_Price ณ เวลาเขียน) — สองการรับสินค้าตัวเดียวกันพร้อมกันอาจคลาดเศษเล็กน้อย
        /// (ยอมรับได้: ร้านเดียว คีย์รับของทีละใบ).
        /// คืนค่าเฉลี่ยใหม่ (ล้มเหลว = -1, ไม่กระทบการรับของ).
        /// </summary>
        public static decimal ApplyReceipt(string conn, int productId, decimal qtyIn, decimal unitCost, bool qtyAlreadyInserted)
        {
            if (productId <= 0 || qtyIn <= 0 || unitCost <= 0) return -1m;
            try
            {
                var c = new code();
                c.DatabaseInsertSafe(conn,
                    @"DECLARE @onHand DECIMAL(18,4) =
                          ISNULL((SELECT SUM(CAST(Amount AS DECIMAL(18,4))) FROM Product_In WHERE Product_ID = @id), 0)
                        - ISNULL((SELECT SUM(CAST(Amount AS DECIMAL(18,4))) FROM Product_Out WHERE Product_ID = @id), 0)
                        - CASE WHEN @already = 1 THEN @q ELSE 0 END;
                      UPDATE Product
                         SET Cost_Price = CASE
                               WHEN @onHand <= 0 OR ISNULL(Cost_Price, 0) <= 0 THEN @c
                               ELSE ROUND((@onHand * CAST(Cost_Price AS DECIMAL(18,4)) + @q * @c) / (@onHand + @q), 4)
                             END
                       WHERE ID = @id;",
                    new Dictionary<string, object>
                    {
                        { "@id", productId }, { "@q", qtyIn }, { "@c", unitCost }, { "@already", qtyAlreadyInserted ? 1 : 0 }
                    });
                return CurrentAverage(conn, productId);
            }
            catch (Exception ex)
            {
                try { new code().Logs(conn, "CostService", $"ApplyReceipt product={productId} qty={qtyIn} cost={unitCost}: {ex.Message}", "SYSTEM"); } catch { }
                return -1m;
            }
        }

        /// <summary>
        /// คืนสินค้าให้ผู้ขาย: ตัดออก ณ ค่าเฉลี่ยปัจจุบัน → ค่าเฉลี่ยไม่เปลี่ยน (ดูหมายเหตุของคลาส).
        /// คืน "ต้นทุนต่อหน่วยที่ใช้ตัดสินค้าคงเหลือ" — ค่าเฉลี่ย ถ้ายังไม่มีค่าเฉลี่ย (= 0) ใช้ราคาที่ซื้อมา (fallbackUnitCost)
        /// เพื่อให้ JE Cr สินค้าคงเหลือไม่เป็นศูนย์. ถ้าไม่มีค่าเฉลี่ยเลย ตั้ง Cost_Price = fallback ไว้ด้วย.
        /// </summary>
        public static decimal ApplyReturn(string conn, int productId, decimal qtyOut, decimal fallbackUnitCost)
        {
            decimal avg = CurrentAverage(conn, productId);
            if (avg > 0) return avg;
            if (fallbackUnitCost > 0 && productId > 0)
            {
                try
                {
                    new code().DatabaseInsertSafe(conn,
                        "UPDATE Product SET Cost_Price = @c WHERE ID = @id AND ISNULL(Cost_Price, 0) <= 0",
                        new Dictionary<string, object> { { "@id", productId }, { "@c", fallbackUnitCost } });
                }
                catch { }
            }
            return fallbackUnitCost;
        }
    }
}
