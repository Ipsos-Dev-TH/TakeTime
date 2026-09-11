# NextAcc ↔ TakeTime — Inventory / Stock Two-Way Sync (คำขอ + ดีไซน์)

> ผู้ขอ: TakeTime BangPhra • ผู้รับ: ทีม Wachira-d/Accounting (NextAcc)
> เป้าหมาย: sync สต็อกสินค้า **ไป-กลับ** ระหว่าง TakeTime ↔ NextAcc โดยสะท้อน
> "การอัปเดตล่าสุดของแต่ละฝั่ง" อย่างถูกต้อง ไม่ตีกัน

---

## 1. บริบทฝั่ง TakeTime (สำคัญต่อดีไซน์)

TakeTime เก็บสต็อกเป็น **ledger (movement-based)** ไม่ได้เก็บยอดคงเหลือเป็นตัวเลขเดียว:
- `Product` — สินค้า master (ID, Product_Name, Barcode, Cost_Price, Unit, Category, …)
- `Product_In` — รับเข้า (Amount, InType: PURCHASE/RETURN_FROM_CHARGE/ADJUSTMENT_GAIN, Vendor_ID)
- `Product_Out` — ตัดออก (Amount, OutType: SALE/ROOM_CHARGE/WRITEOFF/ADJUSTMENT_LOSS/RETURN_TO_VENDOR, Reason)
- `Stock_Adjustment_Log` — นับสต็อก/ตัดจำหน่าย (Expected/Actual/Difference, Sync_Status, Nexaacc_Journal_Id)
- ยอดคงเหลือ = `vw_ProductStock` / `fn_GetProductStock` = ΣIn − ΣOut (คำนวณสด)

**ปัจจุบัน sync ทางเดียว (TT → NextAcc) เฉพาะ:**
- บันทึกบัญชี (journal) ของความเคลื่อนไหว: STOCK_IN, STOCK_OUT_COGS(+reverse), STOCK_ADJUSTMENT, STOCK_WRITEOFF
- สินค้า master ผ่าน `/api/integration/products` (upsert by Code = `TT-{id:D5}`) → cache ใน `Accounting_Product_Map`
- **ยังไม่ส่งจำนวนคงเหลือ และไม่มีขากลับ**

## 2. หลักการดีไซน์ที่เสนอ — **Movement-based, ไม่ใช่ qty-overwrite**

เพราะ TakeTime เป็น ledger การ sync ที่ถูกต้องและกันชนกันคือ **ส่ง "ความเคลื่อนไหว (movement)" ไป-กลับ
แบบ append-only + idempotent ด้วย ExternalRef** ไม่ใช่ "เขียนทับยอดคงเหลือล่าสุด"
- movement เป็น additive → ไม่มี race/conflict ของตัวเลข (ต่างจาก "qty ล่าสุดทับ" ที่ทำให้ ledger เพี้ยน)
- แต่ละ movement มี `ExternalRef` ไม่ซ้ำ → ฝั่งรับ dedup ได้ ส่งซ้ำไม่บวกซ้ำ
- "ยอดคงเหลือล่าสุด" ของแต่ละฝั่ง = ผลรวม movement ทั้งหมดที่ทั้งสองฝั่งรับครบ → ตรงกันเองโดยไม่ต้อง overwrite
- **Product master** (ชื่อ/ราคา/หน่วย) ใช้ latest-wins ด้วย `UpdatedAt` ได้ (ฟิลด์ไม่กี่ตัว ไม่ใช่ ledger)

## 3. เงื่อนไขที่ต้องยืนยันก่อน (จาก NextAcc)

การ sync 2 ทางของ "จำนวนสต็อก" จะมีความหมายก็ต่อเมื่อ **NextAcc มีระบบนับจำนวนสต็อก (qty on hand)
ต่อสินค้า** ไม่ใช่แค่ Inventory account ใน GL. โปรดยืนยัน:
- [ ] NextAcc มี **inventory item + qty** ต่อสินค้า (ผูกกับ product master `TT-{id}` / `Nexaacc_Product_Id`)
- [ ] มี endpoint **รับ stock movement** จากภายนอก (ขาเข้า)
- [ ] มีวิธี **ให้ partner ดึงความเปลี่ยนแปลงฝั่ง NextAcc** (pull "changes since cursor" หรือ webhook)

ถ้า NextAcc ลงเฉพาะบัญชี (ไม่นับ qty) → sync 2 ทางของจำนวนไม่จำเป็น (คงทางเดียว journal เดิมพอ)

## 4. Endpoint ที่ TakeTime ขอจาก NextAcc

### 4.1 ขาเข้า (TakeTime → NextAcc): รับ stock movement
`POST /api/integration/inventory/movements`
Headers: `X-Integration-Key` (int_)
```jsonc
{
  "movements": [
    {
      "externalRef": "TTIN-12345",            // idempotency key (Product_In.ID / Product_Out.ID / adj id)
      "productCode": "TT-00042",              // = product master code ที่ sync ไว้
      "movementType": "In",                    // In | Out | AdjustGain | AdjustLoss
      "quantity": 10.00,
      "unitCost": 25.00,                       // ทุน/หน่วย (สำหรับ valuation; optional ฝั่ง Out)
      "movementDate": "2026-06-27",
      "reason": "PURCHASE",                    // PURCHASE/SALE/ROOM_CHARGE/WRITEOFF/COUNT_VARIANCE/...
      "reference": "PO/บิล/เลขเอกสารที่เกี่ยวข้อง",
      "note": "..."
    }
  ]
}
```
- **Recalculate ยอดคงเหลือฝั่ง NextAcc จาก movement ที่ได้รับ** (idempotent by `externalRef` — ซ้ำ = ข้าม)
- ตอบกลับ: ต่อ movement → `{ externalRef, accepted, nexaaccMovementId, message }`
- **(ออปชัน) ถ้า NextAcc auto-post GL จาก movement อยู่แล้ว** → TakeTime จะเลิกส่ง journal เดิมซ้ำ
  (ปัจจุบัน TakeTime โพสต์ journal เอง); ระบุให้ชัดว่า movement endpoint โพสต์ GL ด้วยหรือไม่ กันลงซ้ำ

### 4.2 ขากลับ (NextAcc → TakeTime): ดึงความเปลี่ยนแปลง
**ทางเลือก A (pull — แนะนำ, ง่ายต่อ partner):**
`GET /api/integration/inventory/movements?since={cursor}&page={n}&pageSize=200`
- คืน movement ที่เกิด/แก้ฝั่ง NextAcc หลัง `cursor` (timestamp หรือ sequence id แบบ monotonic)
- แต่ละรายการ: `{ nexaaccMovementId, productCode, movementType, quantity, unitCost, movementDate, reason, source, createdAt, cursor }`
- `source` แยกได้ว่า movement นั้น "มาจาก TakeTime" (กันดึงกลับของตัวเอง) หรือ "เกิดที่ NextAcc"
- TakeTime เก็บ `cursor` ล่าสุดไว้ดึงครั้งถัดไป (delta sync)

**ทางเลือก B (webhook):** NextAcc ยิง `POST {partnerWebhook}/inventory-movement` เมื่อสต็อกเปลี่ยน
(TakeTime ฝั่ง headless/cron อาจรับ webhook ไม่ได้ → A เชื่อถือได้กว่า)

### 4.3 Product master (มีอยู่แล้ว — ขอเพิ่ม UpdatedAt + ขากลับ)
- ขาออกมีแล้ว: `POST /api/integration/products` (upsert by Code)
- ขอเพิ่ม: field `updatedAt` ใน product + `GET /api/integration/products?since={cursor}` เพื่อ TakeTime
  ดึงการแก้ชื่อ/ราคาจากฝั่ง NextAcc กลับมา (latest-wins ด้วย updatedAt)

## 5. สิ่งที่ TakeTime จะทำฝั่งตัวเอง (เมื่อ endpoint พร้อม)

1. **ขาออก (push movement):** enqueue `STOCK_MOVEMENT_PUSH` ต่อแถว `Product_In`/`Product_Out`/`Stock_Adjustment_Log`
   (`ExternalRef` = ชนิด+ID) → ส่งเข้า §4.1; reuse `Accounting_Sync_Queue` (entity `STOCK`) + marker กันซ้ำ
2. **ขาเข้า (pull):** job ดึง §4.2 ตาม cursor → กรอง `source != TakeTime` (กัน echo) → insert เป็น
   `Product_In`/`Product_Out` (InType/OutType = ADJUSTMENT_*) พร้อม marker `Nexaacc_Movement_Id`
   (กันลงซ้ำ idempotent) → ยอด `vw_ProductStock` อัปเดตเอง
3. **Schema เพิ่ม (migration):**
   - `Inventory_Sync_Cursor` (Source, Last_Cursor, Updated_Date) — เก็บตำแหน่ง pull ล่าสุด
   - `Product_In.Nexaacc_Movement_Id` / `Product_Out.Nexaacc_Movement_Id` (UNIQUEIDENTIFIER, unique) — dedup ขาเข้า
   - `Product_In.Nexaacc_Push_Marker` / `Product_Out.Nexaacc_Push_Marker` — dedup ขาออก
   - `Product.UpdatedAt` (สำหรับ product master latest-wins)
4. **กัน echo loop:** movement ที่มาจาก pull (source=NextAcc) จะ**ไม่** push กลับ (เช็ค marker `Nexaacc_Movement_Id`)
5. **GL:** ถ้า §4.1 โพสต์ GL ให้แล้ว → TakeTime ปิดการ enqueue journal เดิม (กันลงซ้ำ); ถ้าไม่ → คงเดิม

## 6. สรุป conflict / latest-update
- **จำนวนสต็อก:** ใช้ movement (additive) → ไม่มี conflict; "ล่าสุดของแต่ละฝั่ง" = ทุก movement ถูก sync
  ครบทั้งสองทาง → ยอดตรงกันเอง (ไม่ต้อง overwrite)
- **Product master (ชื่อ/ราคา):** latest-wins ด้วย `updatedAt`/`UpdatedAt`
- ทุกอย่าง idempotent ด้วย `externalRef` (ขาออก) + `nexaaccMovementId` (ขาเข้า)

## 7. ขั้นต่ำที่รับได้ (ถ้าทำเต็มไม่ได้)
- อย่างน้อยขอ **§4.1 (รับ movement) + §4.2A (pull changes-since)** ก็พอเริ่ม 2 ทางได้
- ถ้า NextAcc ไม่นับ qty ต่อสินค้า → แจ้งกลับ; TakeTime จะคง one-way journal เดิม (ไม่เสียเวลาทั้งคู่)

---

## 8. ✅ VERIFIED จาก Wachira-d/Accounting (มิ.ย. 2026, ผ่าน GitHub code search)

**NextAcc มีระบบ inventory เต็มรูปแบบ + นับ qty จริง** (ไม่ใช่แค่ Inventory account ใน GL):
- Entity **`StockMovement`** (ledger ต่อสินค้า) + costing service (`InventoryCostingService` —
  `Product.CostingMethod ∈ {SpecificIdentification, FIFO, WeightedAverage}`; ห้าม LIFO), lot/batch (FEFO).
- `ProductController` (company endpoint `/api/companies/{companyId}/product/*`):
  - `GET .../product/{productId}/stock-movements` → `ApiResponse<List<StockMovementResponse>>`
    (`IProductService.GetStockMovementsAsync(companyId, productId)`) — **query ต่อสินค้า** (ไม่ใช่ global feed)
  - `POST .../product/stock/adjust` (`StockAdjustmentRequest` → `StockMovementResponse`,
    `AdjustStockAsync`) — ปรับสต็อก/สร้าง movement ฝั่ง NextAcc
  - `GET .../product/stock/low`, `/product/inventory/valuation|balance|aging`, `/product/stock-counts*`
- **StockMovement ถูกสร้างอัตโนมัติตอน document approve** (DOCUMENT_FLOW 5.6) + จาก OCR
  (`StockMovementsCreated`) + StockTransfer. Auth: company endpoint (int_ ผ่าน X-Api-Key fallback ได้).
- **ไม่มี integration endpoint (`/api/integration/*`) สำหรับ inventory** — เป็น company endpoint ล้วน.

### ผลต่อดีไซน์ (สำคัญมาก)
1. **ทิศ TT → NextAcc ส่วนใหญ่ "เกิดขึ้นแล้ว" ผ่านเอกสาร** — เมื่อ TakeTime push invoice/receipt/expense
   (DOCUMENT mode) และ line ผูกกับ product ที่ track stock ฝั่ง NextAcc → NextAcc สร้าง StockMovement
   ให้เอง. **⟹ ห้าม push movement ตรง ๆ ซ้ำ (จะ double-count).** ต้องยืนยันก่อนว่า product ของ TakeTime
   บน NextAcc เปิด stock tracking และ line ItemCode map ถึงสินค้าจริงหรือไม่.
2. **ทิศที่ "ขาด" จริงคือ NextAcc → TT** (สต็อกที่ปรับ/นับ/โอนฝั่ง NextAcc เอง). ดึงผ่าน
   `GET .../product/{productId}/stock-movements` — แต่ **เป็นต่อสินค้า ไม่มี "changes since" รวม** →
   pull ต้อง loop ทีละ product + dedup ด้วย `StockMovement.Id` + กรอง movement ที่ "มาจากเอกสารของ TakeTime"
   (กัน echo/double เข้า ledger ฝั่งเรา).
3. **คำขอที่จะช่วยให้ inbound ทำได้มีประสิทธิภาพ** (ฝาก NextAcc): เพิ่ม **global delta query**
   `GET /api/integration/inventory/movements?since={cursor}` ที่คืน movement ทุกสินค้าหลัง cursor พร้อม
   field `source` (Document/Adjustment/Transfer/Ocr) + `sourceExternalRef` → TakeTime กรอง echo ได้แม่นยำ
   และไม่ต้อง loop ต่อสินค้า. ถ้าไม่มี global query → TakeTime จะ poll ต่อสินค้าตาม `GetStockMovementsAsync`.

### สิ่งที่ TakeTime จะทำ (อิงของจริงที่ verify แล้ว)
- **เลิกแนวคิด "push movement"** ทิศออก (เพราะเอกสารทำให้แล้ว) — คงการ sync เอกสาร/journal เดิม
- โฟกัส **inbound pull**: client `GetStockMovementsAsync(productId)` / `AdjustStockAsync` (ถ้าจำเป็นต้อง
  เขียนกลับ) + job ดึง movement ที่ NextAcc-originated → ลงเป็น `Product_In`/`Product_Out`
  (ADJUSTMENT_*) ใน TakeTime, dedup ด้วย `Nexaacc_Movement_Id`
- ต้องได้ field list เต็มของ `StockMovementResponse` / `StockAdjustmentRequest` (MovementType enum,
  source/reference, qty, unitCost, date) ก่อนเขียน DTO ฝั่ง TakeTime ให้ตรง

## 9. ✅ IMPLEMENTED ฝั่ง TakeTime (qty 2 ทาง — PHASE18_02, feature-flag default off)

Routes ที่ใช้ (verified): `POST {company}/product/stock/adjust` (qty-only, **ไม่โพสต์ GL** — ProductService
ไม่มี Journal) + `GET {company}/product/{productId}/stock/movements`.
DTO: `StockAdjustmentRequest(ProductId, Quantity, MovementType IN/OUT/ADJUST/TRANSFER_*, UnitCost?, ...)`,
`StockMovementResponse(Id, ProductId, ProductName, MovementDate, MovementType, Quantity, UnitCost)`.

- **ขาออก (`Nexaacc_StockQtySync`):** ทุก `EnqueueStock*` (In/OutCogs/Reverse/Adjustment/WriteOff) เพิ่ม
  paired `STOCK_QTY_PUSH` → `ProcessStockQtyPush` resolve `Accounting_Product_Map` (TakeTime→Nexaacc GUID;
  ถ้ายังไม่ map → EnqueueProductSync แล้ว retry) → `AdjustStockAsync`. ไม่เบิล GL (journal เดิมยังโพสต์มูลค่า,
  adjust คุม qty). บันทึก movement id ที่สร้างใน `Nexaacc_Stock_Movement_Seen` (กัน echo).
- **ขากลับ (`Nexaacc_StockQtyPull`):** `PullNextAccStockMovementsIfDue` (เรียกจาก timer Global.asax) วน
  product ที่ map (round-robin ตาม `Accounting_Product_Map.Stock_Last_Pulled`, cap 25/รอบ) →
  `GetProductStockMovementsAsync` → movement ที่ไม่อยู่ใน Seen (= ปรับฝั่ง NextAcc เอง) → ลง
  `Product_In` (IN) / `Product_Out` (OUT, Remark='NEXTACC_SYNC') → mark Seen. echo-safe เพราะ push ทุกตัว
  อยู่ใน Seen + inbound insert ตรง ๆ ไม่ trigger ขาออก.
- Migration PHASE18_02: `Nexaacc_Stock_Movement_Seen`, `Accounting_Product_Map.Stock_Last_Pulled`, 2 flags.
  Admin toggles เพิ่มในหน้า Accounting Integration. **ข้อจำกัด:** pull เป็น per-product polling (ยังไม่มี
  global delta — ถ้า NextAcc เพิ่ม `?since=` ตาม §4.3 จะเร็วขึ้นมาก); inbound Product_Out omit
  Account_Paid_How_ID (ถ้า NOT NULL ต้องปรับ). Needs Windows build + live test.
