# QR สั่งอาหาร (Dining) — สัญญา API ของชั้น Foundation สำหรับทีมหน้า (ขั้น 2)

Namespace `Take_Time_BangPhra.Dining` · โฟลเดอร์ `Class/Dining/` · migration `Database/PHASE19_Migration_42_Dining_QR_Ordering.sql`
อ่านคู่กับ `scratchpad/qr_ordering_design.md` + `qr_ordering_spec_v1.md` (คำตัดสินเจ้าของ: ราคารวม VAT, ไม่มี service charge/ทิป,
เงินสดเก็บที่โต๊ะ, โอน = ระบบตรวจสลิปเอง, หลายรอบต่อโต๊ะ + "เช็คบิล", ลงบิลห้องลูกค้าทำเองได้ด้วยเบอร์โทร).

**C# 7.3 เท่านั้น** (โปรเจกต์ .NET Framework 4.7.2) — ห้าม switch expression / `??=` / `using var` / ranges / static local function.

## หน้าที่ทีมขั้น 2 ต้องสร้าง

| ทีม | ไฟล์ | ใช้ service |
|---|---|---|
| A ลูกค้า | `Order/Default.aspx(.cs)` (เมนู+ตะกร้า), `Order/Status.aspx(.cs)` (สถานะ/เช็คบิล/จ่าย), `API/DiningOrder.ashx(.cs)` (JSON), `Content/dining.css`, `Scripts/dining.js`, route `Order/{token}` ใน `App_Start/RouteConfig.cs` → `Order/Default.aspx?t=` | `DiningTableService` (ResolveTable, OpenOrResumeSession, GetSession, RequestBill, TouchSession), `DiningMenuService.GetMenu`, `DiningOrderService` (Quote, PlaceOrder, GetSessionSummary, CallStaff), `DiningPaymentService` (SubmitTransferSlip, ListRoomChargeCandidates, ChargeToRoom), `DiningConfig` |
| B พนักงาน | `Admin/Dining/Orders.aspx(.cs)` (กระดาน + เสียง + poll), `Admin/Dining/Tables.aspx(.cs)` แท็บ "สถานะโต๊ะ", `Admin/NotificationCheck.aspx.cs` `action=dining`, `Site.Master` เมนู/badge, `Global.asax.cs` timer | `DiningOrderService` (ListOrders, GetNewOrdersSince, UpdateStatus, AcknowledgeStaffCall), `DiningPaymentService` (RecordCash, ListPendingSlips, ApprovePendingSlip, RejectPendingSlip), `DiningTableService` (GetBoard, CloseSession, AutoCloseIdleSessions), `DiningPostingService.PostIfDue` |
| C แอดมิน | `Admin/Dining/Tables.aspx(.cs)` แท็บ "โซน & โต๊ะ", `Admin/Dining/Menu.aspx(.cs)`, `Admin/Dining/QrSheet.aspx(.cs)`, การ์ดใน `Admin/Settings/Index`, CLAUDE.md | `DiningTableService` (Zones/Tables CRUD, BulkCreateTables, Rotate*), `DiningMenuService` (admin CRUD), `DiningQr` |

ทุกหน้าพนักงาน/แอดมิน: `if (!Feature.Guard(this, DiningConfig.FeatureName)) return; if (!Perm.Guard(this, Perm.OpsDining)) return;`
หน้าลูกค้า: `if (!DiningConfig.Enabled) → แสดง "ปิดใช้งาน"` (ResolveTable คืน `FEATURE_OFF` ให้อยู่แล้ว).

## ไฟล์ในโฟลเดอร์นี้

| ไฟล์ | ชนิด | หน้าที่ |
|---|---|---|
| `DiningModels.cs` | pure | POCO ทุกตัว + `DiningStatus.*` ค่าคงที่ + `DiningError` รหัสผิดพลาด + ผลลัพธ์ (`DiningResult` และลูก) |
| `DiningPricing.cs` | pure static | `ComputeQuote` คิดราคาจากตะกร้า (server เท่านั้น), ตรวจจำนวน/บรรทัด/ขั้นต่ำ/sold-out/โซน/สต๊อก/ตัวเลือก, `ExtractVat`, `OptionsToJson/FromJson`, `R2` |
| `DiningTokens.cs` | pure static | `NewToken` (CSPRNG 32 ไบต์ base64url 43 ตัว), `IsValidToken`, `ConstantTimeEquals`, `NormalizePhone/PhoneMatches/MaskPhone`, `DeviceHash`, `SanitizeIdempotencyKey` |
| `DiningNotifyText.cs` | pure static | ข้อความไทย HTML(Telegram)+Text ของทุกเหตุการณ์ (escape ข้อความลูกค้าแล้ว) |
| `DiningConfig.cs` | static | ค่าตั้ง System_Config/AppCfg + ค่าคงที่ (ชื่อฟีเจอร์, ช่องทาง, โฟลเดอร์สลิป, URL กระดาน) |
| `DiningDb.cs` | internal | ตัวช่วย DB (guard OBJECT_ID, Query/Exec/Scalar/InsertIdentity, Log, Event, converters) |
| `DiningMenuService.cs` | service | เมนูของโซน (cache 60 วิ) + admin CRUD หมวด/รายการ/กลุ่มตัวเลือก/ตัวเลือก |
| `DiningTableService.cs` | service | โซน/โต๊ะ CRUD, token/หมุน QR, ResolveTable, session (เปิด/ต่อ/อ่าน/touch), RequestBill, CloseSession, AutoCloseIdleSessions, GetBoard |
| `DiningOrderService.cs` | service | Quote, PlaceOrder, GetSessionSummary, CallStaff, GetOrder/ListOrders/GetNewOrdersSince, UpdateStatus |
| `DiningPaymentService.cs` | service | Outstanding, RecordCash, SubmitTransferSlip, ApprovePendingSlip/RejectPendingSlip, ListRoomChargeCandidates, ChargeToRoom |
| `DiningPostingService.cs` | job | `PostIfDue()` รายได้ → `ChannelSalesPostingService` + ตัดสต๊อก/COGS ของรายการ Track_Stock |
| `DiningNotifier.cs` | service | ส่ง Telegram (`Notify.Send`) + ในระบบ (`NotifyAllStaff`) + กล่องแชทรวม (`OmniChannelService.ReceiveMessage("DINING", "table_{id}", …)`) |
| `DiningQr.cs` | static | `BuildOrderUrl(token)`, `BuildStatusUrl(sessionToken)`, `RenderSvg/TryRenderSvg/RenderSvgDataUri` (ZXing.BarcodeWriterSvg), `EngineReady` |
| `DiningStaffView.cs` | pure static (ทีม B เพิ่ม) | แปลงโมเดล → `Dictionary<string,object>` สำหรับ JSON หน้าพนักงาน: `Order(o, now)`, `Table(TableBoardRow)`, `Payment(p, session, now)`, `Session(s)`, `Outstanding(o)`, `Poll(p, now)`, `Result(DiningResult)` + ตัวช่วย `PayState(o)` (UNPAID/PENDING_SLIP/PAID/CHARGED/REFUNDED), `PayStateThai`, `TableStatusThai`, `AgeText(min)`, `ItemsPreview(o, max)`, `Money(v)`. ไม่แตะ DB; ค่าทั้งหมดเป็นข้อความล้วน — หน้าต้อง escapeHtml/HtmlEncode ก่อนลง DOM (ใช้โดย `Admin/Dining/Orders.aspx.cs`, `Tables.aspx.cs`, `NotificationCheck?action=dining`) |

ทดสอบ: `tools/dining-tests/run.sh` (mono; คอมไพล์ 4 ไฟล์ pure + `DiningTests.cs` — 136 กรณี)

## กติกาทั่วไป

* ทุก service มี ctor ว่าง (ใช้ `TaketimeConnectionString`) และ ctor รับ connection string. เมธอดอ่านคืน `null`/list ว่างเมื่อยังไม่รัน migration;
  เมธอดเขียนคืน `DiningResult { Ok=false, ErrorCode="NOT_READY" }` (ไม่ throw) · CRUD ของแอดมินโยน `ArgumentException` (ข้อความไทย) เมื่อข้อมูลผิด.
* `DiningResult.Message` เป็นข้อความไทยล้วน (ไม่มี HTML) — หน้าต้อง **HtmlEncode** ก่อนลง HTML เสมอ; JSON ผ่าน `JavaScriptSerializer`.
* `ErrorCode` (คลาส `DiningError`): `FEATURE_OFF NOT_READY BAD_TOKEN TABLE_INACTIVE ZONE_CLOSED SESSION_CLOSED VALIDATION MIN_ORDER SOLD_OUT OUT_OF_STOCK NOT_FOUND
  TRANSITION FORBIDDEN ACCOUNTING_POSTED OUTSTANDING AMOUNT_MISMATCH SLIP_REJECTED SLIP_DUPLICATE RATE_LIMITED LOCKED PHONE_MISMATCH NOTHING_TO_PAY PENDING_SLIP DUPLICATE INTERNAL`
* `staffId` = `Session["admin_id"]` (int) ของพนักงานที่ล็อกอิน (0 = ไม่รู้) · ชื่อพนักงานอ่านจาก `Session["username"]` ภายใน service เอง.
* Owner/Admin ตรวจผ่าน `SlipVerificationService.CanOverrideCurrentUser()` (service ตรวจเองในจุดที่ต้องการ — หน้าแค่ส่ง `force`/`overrideReason`).
* service **แจ้งเตือนเอง** (DiningNotifier) ในทุกเหตุการณ์ — หน้าไม่ต้องเรียก Notify ซ้ำ.
* services ไม่ throw ออกมาในเส้นทางลูกค้า (คืน `Ok=false` + log `code.Logs` action `Dining`); เส้นทางแอดมิน CRUD อาจ throw `ArgumentException`.

## สถานะ (DiningStatus)

| ที่ | ค่า |
|---|---|
| `Table` | `FREE OCCUPIED BILL_REQUESTED NEEDS_STAFF DISABLED` |
| `Session` | `OPEN BILL_REQUESTED CLOSED` |
| `Order` | `NEW → ACCEPTED → PREPARING → SERVED` (ข้ามขั้นได้) · `CANCELLED` จาก NEW/ACCEPTED/PREPARING — `DiningStatus.Order.CanTransition(from,to)`, `Thai(s)` |
| `Pay` (Dining_Order.Payment_Status) | `UNPAID PAID CHARGED REFUNDED` — สลิปรอตรวจ = ยัง `UNPAID` + `PendingPaymentId` มีค่า |
| `Method` (Dining_Payment.Method) | `CASH TRANSFER ROOM_CHARGE` |
| `Payment` (Dining_Payment.Status) | `CONFIRMED PENDING_SLIP REJECTED VOID` |
| `Kitchen` (item) | `QUEUED COOKING READY SERVED VOID` |
| `ZoneMode` | `AUTO OPEN CLOSED` |
| `Event` (Dining_Event_Log) | `SESSION_OPEN SESSION_CLOSE NEW_ORDER STATUS PAID SLIP_PENDING SLIP_REJECTED ROOM_CHARGE ROOM_CHARGE_FAIL CALL_STAFF BILL_REQUEST QR_ROTATED AUTO_CLOSE` |

## Flow ลูกค้า (ทีม A) — token 2 ตัว

1. **QR_Token ของโต๊ะ** (พิมพ์ติดโต๊ะ, 43 ตัว) อยู่ใน URL `/Order/{token}` (`DiningQr.BuildOrderUrl`).
   `DiningTableService.ResolveTable(token)` → `TableResolveResult { Ok, Table, Zone, ZoneOpen, ErrorCode, Message }`
   (`ZONE_CLOSED` ยังคืน Table/Zone ให้แสดงชื่อโต๊ะ + เวลาทำการ; `BAD_TOKEN` = หมุน QR แล้ว/ปลอม; `TABLE_INACTIVE`; `FEATURE_OFF`).
2. `OpenOrResumeSession(table.Id, DiningTokens.DeviceHash(ua, ip), ip)` → `SessionOpenResult { SessionToken, Session, Resumed }`.
   เก็บ `SessionToken` ใน **cookie `dnsess`** (HttpOnly, SameSite=Lax, path `/`, อายุ ~12 ชม.) และส่งซ้ำใน body ของทุก fetch.
   เครื่องที่สองสแกนโต๊ะเดียวกัน = ได้ session เดิม (บิลเดียวกัน) — `Resumed=true`.
3. เมนู: `DiningMenuService.GetMenu(session.ZoneCode)` → `DiningMenu { Categories (เฉพาะที่มีรายการ + "อื่น ๆ" id 0), Items }`.
   รายการ: `Name NameEn Description Price (รวม VAT) ImageUrl ThumbUrl CategoryId IsSoldOut SoldOutUntil PrepMinutes AllergenTags IsRecommended TrackStock StockQty OptionGroups`
   ใช้ `item.Orderable(zoneCode, DateTime.Now)` ตัดสินปุ่ม "หมด". ตัวเลือก: `OptionGroups[].Options[]` (`PriceDelta`, `MinSelect/MaxSelect/IsRequired`).
4. ตะกร้า (client) → `List<DiningCartLine { ProductId, Qty (จำนวนเต็ม ≤99), OptionIds int[], Note ≤300 }>` ≤ 50 บรรทัด.
   `DiningOrderService.Quote(sessionToken, cart)` → `DiningQuote { Ok, Errors[], ErrorCode, Lines[] (UnitPrice/OptionsPrice/LineTotal/Options), Subtotal, Total, VatAmount, NetAmount, ItemCount }`
   — JS แสดงยอดสดได้แต่ server คิดใหม่เสมอ; เรียก Quote ก่อนแสดงหน้ายืนยัน.
5. `PlaceOrder(sessionToken, cart, guestNote, guestName, guestPhone, idempotencyKey, clientIp, intendedMethod, sourceChannel)` →
   `PlaceOrderResult { Ok, Order, Quote, Duplicate }`. `idempotencyKey` = uuid ต่อตะกร้าจาก client (8-64 ตัว [A-Za-z0-9_-]) — ส่งซ้ำ = คืนออเดอร์เดิม.
   `intendedMethod` (`CASH|TRANSFER|ROOM_CHARGE|null`) ใช้แค่ในข้อความแจ้งเตือน. `sourceChannel="STAFF"` เมื่อพนักงานคีย์แทน.
   ทุกรอบ = `Dining_Order` ใหม่ `UNPAID`; ครัวเริ่มทำได้ทันที.
6. หน้าสถานะ `Order/Status.aspx?s={sessionToken}` (หรืออ่านจาก cookie): poll ทุก `DiningConfig.GuestPollSec` วิ →
   `GetSessionSummary(sessionToken)` → `SessionSummary { Session, Orders[] (เรียงรอบ, มี Items), Payments[], Outstanding { Amount, PayableAmount, PendingSlipAmount, PayableOrderIds, PendingSlipOrderIds }, TotalOrdered, TotalPaid, BillRequested, ZoneOpen, ZoneClosedMessage, VersionTicks }`
   ใช้ `VersionTicks` ทำ ETag/304. เรียก `DiningTableService.TouchSession(session.Id)` ทุก poll (กันปิดโต๊ะอัตโนมัติ) — หรือ GetSessionSummary ไม่ touch ให้ ต้องเรียกเอง.
   `ErrorCode=SESSION_CLOSED` = โต๊ะถูกปิด → แสดง "สแกน QR ใหม่" และลบ cookie.
7. ปุ่ม: **สั่งเพิ่ม** (กลับหน้าเมนู session เดิม) · **เรียกพนักงาน** `CallStaff(sessionToken, message≤200)` (1 ครั้ง/60 วิ → `RATE_LIMITED`) ·
   **เช็คบิล** `DiningTableService.RequestBill(sessionToken)` → session/โต๊ะ `BILL_REQUESTED` + แจ้งเตือน; หน้าแสดงยอดค้าง + ตัวเลือกจ่าย.
8. **จ่าย** (ต่อ "บิล" ของ session — ปิดชุดออเดอร์ทั้งก้อน ไม่รับจ่ายบางส่วน; `coversOrderIds=null` = ทุกออเดอร์ที่จ่ายได้):
   * เงินสด → แค่บอกลูกค้า "ชำระกับพนักงานที่โต๊ะ" (พนักงานกดรับเงินบนกระดาน) — ไม่มี API ฝั่งลูกค้า.
   * โอน → แสดงบัญชีรับโอน (`PaymentChannelCatalog.ForCustomer("DINING")` / `SlipVerificationService.LoadReceiverAccounts()`; ลอก `Reserve.aspx.SlipFirst.cs:707-751`)
     → อัปโหลดสลิป (ย่อรูปด้วย `Scripts/slip-first.js` `SlipFirst.prepare` ได้) → `DiningPaymentService.SubmitTransferSlip(sessionToken, HttpPostedFile | byte[]+fileName, coversOrderIds, selectedPaidHowId, clientIp)`
     → `SlipOutcome { Ok, Verified, Pending, Rejected, DuplicateSlip, Reason, Message, ContactText, Payment, SlipPath, SlipAmount, CoveredOrderIds }`
     - `Verified` = ออเดอร์ `PAID` แล้ว · `Pending` = รับไว้รอพนักงาน (ออเดอร์ยัง UNPAID + PendingPaymentId; จ่ายซ้ำไม่ได้จนพนักงานตัดสิน) ·
       `Rejected` = ไม่รับ (แสดง Message + ปุ่มติดต่อ `SlipAmountLockUi.ContactButtonsHtml` / `ContactText`) · ยอดสลิปเกิน = ผ่าน (รับเกิน) · ขาด = ไม่ผ่าน.
     - ปิดตรวจสลิป (ไม่มี PHASE19_34) = `Pending` เสมอ (ไม่ PAID อัตโนมัติ).
   * ลงบิลห้อง (เฉพาะ `session.AllowRoomCharge`) → `ListRoomChargeCandidates()` → `List<RoomChargeCandidate { ReservationId, RoomLabel }>` (แสดงเฉพาะเลขห้อง)
     → ลูกค้าเลือก + กรอกเบอร์ → `ChargeToRoom(sessionToken, reservationId, phoneTyped, clientIp)` → `RoomChargeResult { Ok, Payment, RoomLabel, AttemptsLeft, LockedUntil, CoveredOrderIds }`
     - `PHONE_MISMATCH` (แสดง AttemptsLeft) · `LOCKED` (ล็อก 15 นาที หลังผิดครบ `Dining_Room_Charge_Max_Fail`; Telegram แจ้งแล้ว) · `NOTHING_TO_PAY` · `FORBIDDEN` (โซนไม่เปิด).
     - สำเร็จ = ออเดอร์ `CHARGED`, ยอดไปกับใบเสร็จเช็คเอาท์ (RoomChargeService ตัดสต๊อก+COGS ของรายการ Track_Stock แล้ว).
9. handler `API/DiningOrder.ashx` (IRequiresSessionState): แนะนำ actions `menu|quote|place|status|call|bill|slip|rooms|roomcharge`; ต้อง header `X-Dining: 1` + same-origin (ลอก `API/SlipCheck.ashx.cs:45`),
   rate limit ต่อ session/IP (`SlipUploadStore.TryRate` ใช้ซ้ำได้), ไฟล์ ≤ 8 MB + `SlipUploadStore.SniffImage`. `Web.config` `maxRequestLength` เฉพาะ path นี้เหมือน SlipCheck.

## Flow พนักงาน (ทีม B)

* กระดาน: `DiningOrderService.ListOrders(new DiningOrderFilter { ZoneId, TableId, SessionId, Status ("ACTIVE" = ยังไม่เสิร์ฟ), UnpaidOnly, PendingSlipOnly, Since, Days=2, Max=200 })` → `List<DiningOrder>` (มี `Items`, `TableCode/TableName/ZoneName`, `IsOutstanding/IsPayable/IsSettled`).
* poll ทุก `DiningConfig.StaffPollSec` วิ: `GetNewOrdersSince(sinceTicks)` → `DiningBoardPoll { NewOrders[], LatestTicks, NewCount, PendingSlipCount, CallStaffCount, BillRequestedCount }` (ส่ง `LatestTicks` กลับรอบถัดไป; 0 = ทั้งหมดที่ยัง NEW). ใช้ตัวนี้ทั้งใน WebMethod ของกระดานและ `NotificationCheck?action=dining`.
* สถานะ: `UpdateStatus(orderId, "ACCEPTED|PREPARING|SERVED|CANCELLED", staffId, reason)` → `DiningResult` (`TRANSITION` ผิดลำดับ · ยกเลิกออเดอร์ที่ PAID = Owner/Admin + เหตุผล ≥5 ตัว; ลงบัญชีแล้ว = `ACCOUNTING_POSTED` → ชี้ไป `Account/CreditNotes` · `CHARGED` = บล็อก ให้ยกเลิกที่หน้าการจอง).
* เรียกพนักงาน: โต๊ะ `NEEDS_STAFF` → ปุ่มรับทราบ `AcknowledgeStaffCall(tableId)`.
* รับเงินสด: `DiningPaymentService.RecordCash(sessionId, orderIds|null, paidHowId (0 = CASH แถวแรก; dropdown จาก `PaymentChannelCatalog.ForStaff("DINING")` หรือ `Account_Paid_How` ชนิด CASH), staffId)` → `PaymentResult`.
* สลิปรอตรวจ: `ListPendingSlips()` → `List<DiningPayment>` (SlipPath เปิดรูป, SlipNote เหตุผลที่ระบบไม่ผ่าน) → `ApprovePendingSlip(paymentId, staffId, overrideReason)` (ตรวจซ้ำ `VerifyFileFixedTotal`; ไม่ผ่าน = Owner/Admin + เหตุผล ≥5 ตัว หรือกดซ้ำใน 10 นาที; สลิปซ้ำ = หยุดเสมอ) / `RejectPendingSlip(paymentId, staffId, reason)`.
* แผนผังโต๊ะ: `DiningTableService.GetBoard(zoneId|null)` → `List<TableBoardRow { Table, SessionId, SessionStatus, SessionOpenedAt, BillRequested, OpenOrders, TotalOrders, Outstanding, PendingSlipAmount, LatestOrderAt, LatestOrderAgeMinutes }>`.
* ปิดโต๊ะ: `CloseSession(sessionId, staffId, reason, force)` — ค้างชำระ = `OUTSTANDING` เว้นแต่ `force` + Owner/Admin + เหตุผล ≥5 ตัว (ออเดอร์ที่ยังไม่เสิร์ฟถูกยกเลิก, session CLOSED, โต๊ะ FREE, conversation แชท RESOLVED).
* พนักงานเปิดโต๊ะ/สั่งแทนลูกค้า: `OpenOrResumeSession(tableId, "staff", ip)` แล้ว `PlaceOrder(..., sourceChannel: "STAFF")`.
* **Global.asax timer** (ถัดจาก `PostRoomServiceRevenueIfDue` :141-144): `new DiningPostingService().PostIfDue();` และ `new DiningTableService().AutoCloseIdleSessions();` (ทั้งคู่ไม่ throw, no-op เมื่อยังไม่รัน migration).
* Site.Master: เมนู "ร้านอาหาร / QR สั่งอาหาร" ภายใต้ `Perm.CanView(Perm.OpsDining) && Feature.On("Dining")`; badge จาก `GetNewOrdersSince(0).NewCount + PendingSlipCount + CallStaffCount + BillRequestedCount`.
* **สิ่งที่ทีม B ทำแล้ว (ต.ค. 2026):** `Admin/Dining/Orders.aspx(.cs)` (CodeBehind, namespace `Take_Time_BangPhra.Admin.Dining`) = กระดาน + **WebMethod ทั้งหมดของฝั่งพนักงาน**
  (`LoadBoard`, `Poll`, `SetStatus`, `RecordCash`, `ApproveSlip`, `RejectSlip`, `CloseTable`, `AckCall`, `LoadTables`, `SessionDetail`, `OpenForGuest` — ทุกตัว `EnableSession`,
  ตรวจ `DiningConfig.Enabled + Perm.CanAccess(OpsDining)` เอง, คืน `{ok, message, code, …}` จาก `DiningStaffView`) · `Admin/Dining/Tables.aspx(.cs)` แผนผังโต๊ะ (เรียก WebMethod ของ Orders.aspx) ·
  `Scripts/dining-staff.js` (`DnStaff.init(boot)` โหมด board/tables, escapeHtml ทุกค่า, เสียง WebAudio แยกชนิด ออเดอร์/เรียก/ขอบิล, modal/แผงข้าง/kitchen ticket 80 มม.) · `Content/dining-staff.css` ·
  `Site.Master` (`phNavDining` + badge `diningBadge` + modal `globalDiningAlert` + poll `checkDiningOrders()` ทุก 5 วิ เมื่อ `ShowDiningPoll`) · `Admin/NotificationCheck.aspx.cs` `action=dining`
  (guard `Perm.OpsDining` เฉพาะ action นี้; JSON `{success,newCount,pendingSlipCount,callStaffCount,billRequestedCount,total,latest{orderId,orderNo,table,zone,itemsPreview,totalAmount},callTables,billTables}`) ·
  `Global.asax.cs` เรียก `PostIfDue` + `AutoCloseIdleSessions` ใต้ `DiningConfig.Enabled`. ลิงก์ `Orders?o={orderId}` ไฮไลต์การ์ด (แนบมาแม้ไม่เข้าตัวกรอง), `?attn=1` = เฉพาะโต๊ะขอบิล/เรียกพนักงาน, `?table=&zone=&status=&unpaid=1&slip=1`.
  "เปิดโต๊ะแทนลูกค้า" = `OpenOrResumeSession(tableId, "staff", ip)` แล้วเปิด `DiningQr.BuildOrderUrl(table.QrToken)` ให้พนักงานคีย์บนหน้าลูกค้า (ทีม A — `sourceChannel=STAFF` เป็นเรื่องของ handler ฝั่งนั้น).

## แอดมิน (ทีม C)

* โซน: `ListZones(includeInactive)`, `GetZone(id)`, `GetZoneByCode`, `SaveZone(DiningZone, updatedBy)` (Code A-Z0-9_- ≤20 ไม่ซ้ำ; ต้องเปิดวิธีจ่าย ≥1), `SetZoneActive`.
  `DiningZone { Code NameTh SortOrder IsActive OpenTime/CloseTime (TimeSpan?) ManualMode(AUTO|OPEN|CLOSED) ClosedMessage AllowCash AllowTransfer AllowRoomCharge KitchenStation }` — `DiningTableService.IsZoneOpen(zone, out msg)` ใช้แสดงสถานะเปิด/ปิด.
* โต๊ะ: `ListTables(zoneId|null, includeInactive)`, `GetTable`, `SaveTable(DiningTable)` (ใหม่ = ออก QR_Token ให้), `BulkCreateTables(zoneId, "A", 1, 20, seats)`, `RotateTableToken(tableId)` / `RotateZoneTokens(zoneId)` (confirm ก่อน — QR เดิมใช้ไม่ได้ทันที), `SetTableActive`, `DeleteTable` (มีประวัติ = ปิดใช้แทน).
* QR sheet: `DiningQr.BuildOrderUrl(table.QrToken)` + `DiningQr.TryRenderSvg(url, 512, out svg)` (SVG inline; `false` = ไม่มี zxing.dll → fallback `qrcodejs` CDN เหมือน `OrderManagement.aspx:779`) หรือ `RenderSvgDataUri`. โลโก้/ชื่อร้านจาก `Business_Info`.
* เมนู: `ListProductsForAdmin(onlyInMenu)` (ทุก Product ใช้งาน + ธงในเมนู), `SaveMenuItem(DiningMenuItem, updatedBy)` (upsert: IsDining, CategoryId, Name(ชื่อแสดง — เท่ากับ ProductName = ไม่เก็บ), NameEn, Description, IsSoldOut/SoldOutUntil, PrepMinutes, AllergenTags, SortOrder, AvailableZones CSV รหัสโซน, TrackStock, IsRecommended),
  `SetSoldOut(productId, bool, until)`, `RemoveMenuItem`, `SetItemOptionGroups(productId, groupIds)`, หมวด `ListCategories/SaveCategory/DeleteCategory`, ตัวเลือก `ListOptionGroups(includeInactive)/SaveOptionGroup/DeleteOptionGroup/SaveOption/DeleteOption`.
  รูปสินค้า = ลิงก์ไป `Admin/ProductImages.aspx` เดิม (อ่านจาก `Product_Images` ProductType='PRODUCT' Status=1 รูปหลักก่อน). บันทึกแล้ว service `Invalidate()` cache ให้เอง.
* ตั้งค่า: `System_Config` คีย์ `Feature_Dining`, `Dining_Session_Idle_Minutes` (30), `Dining_Min_Order` (0), `Dining_Guest_Poll_Sec` (5), `Dining_Staff_Poll_Sec` (3), `Dining_Room_Charge_Max_Fail` (5) — อ่านผ่าน `DiningConfig.*`. ลิงก์ไป `Admin/Settings/SalesDocPolicy` (ช่องทาง DINING), `SlipCheckSettings`, Notification settings (4 เหตุการณ์), Accounting Integration (mapping `DINING_REVENUE`).

## แจ้งเตือน (ยิงจาก service — ไม่ต้องเรียกเองในหน้า)

| เหตุการณ์ | `Notify.Ev` | ยิงที่ |
|---|---|---|
| ออเดอร์ใหม่ | `OrderDining` (HIGH) | `PlaceOrder` |
| รับ/กำลังทำ/เสิร์ฟ/ยกเลิก | `OrderDiningStatus` | `UpdateStatus` |
| สลิปผ่าน / รอตรวจ / ไม่ผ่าน / ยืนยันโดยพนักงาน / ปฏิเสธ / รับเงินสด | `OrderDiningStatus` | `SubmitTransferSlip`, `ApprovePendingSlip`, `RejectPendingSlip`, `RecordCash` |
| เรียกพนักงาน | `DiningCallStaff` (urgent) | `CallStaff` |
| เช็คบิล / ลงบิลห้อง / ล็อกลงบิลห้อง / ปิดโต๊ะ (รวมอัตโนมัติ) | `DiningBill` (urgent) | `RequestBill`, `ChargeToRoom`, `CloseSession`, `AutoCloseIdleSessions` |

ทุกเหตุการณ์ยังไป `NotificationService.NotifyAllStaff(title, text, "DINING_ORDER", HIGH|NORMAL)` และกล่องแชทรวมช่องทาง `DINING` (1 conversation ต่อโต๊ะ `table_{tableId}`; ปิดโต๊ะ = RESOLVED; การสั่งใหม่เปิด conversation เดิมอัตโนมัติ). ลิงก์ในข้อความ = `PaymentUrls.SiteBase() + "/Admin/Dining/Orders?o={orderId}"` — ทีม B ต้องรองรับ query `o=` (เลื่อน/ไฮไลต์ออเดอร์).

## บัญชี / สต๊อก (อัตโนมัติ)

* `DiningPostingService.PostIfDue()` (timer): `Dining_Payment` CASH/TRANSFER `CONFIRMED` → `ChannelSalesPostingService.Record(ChannelSale { Channel="DINING", SourceKey="DNP-{paymentId}", SaleDate=Paid_At, PaidHowId, Amount })` → นโยบายเอกสารขาย (ไม่ seed กฎ DINING = `(*,CASH)=DAILY_ROLLUP` ใบสรุป `DNDAY-{yyyyMMdd}-{paidHowId}`, `(*,*)=IMMEDIATE` ใบรายการ `DNP-{id}`) → mapping `DINING_REVENUE → PRODUCT_REVENUE`.
  รายการ `Track_Stock=1` ของออเดอร์ที่ครอบ → `Product_Out` (Remark `ร้านอาหาร`, Account_Receipt_ID = `DNP-{id}`) + `EnqueueStockOutCogs(stockRef "DNCOGS-{orderId}-{pid}")`; อาหารปรุงไม่ตัด. `Dining_Payment.Acct_Post_Ref` = `DNP-{id}` / `RC:{ref}` (ลงบิลห้อง — ไม่โพสต์).
* รายงานภาษี (`Report/DailySalesTaxSummary`) เห็นช่องทาง `DINING` จากสมุด `Sales_Channel_Posting` อัตโนมัติ (`TaxReportKit.ChDining`).
* ยกเลิก/คืนเงินหลังลงบัญชี = ใบลดหนี้ (`Account/CreditNotes`) — `UpdateStatus` บล็อกให้แล้ว.

## ข้อควรรู้ที่ไม่อยู่ในโค้ด

* ไม่มีหน้าใดสร้างในขั้นนี้ (ไม่มี .aspx/.ashx/.js/.css); ไม่มีการแก้ `Global.asax`, `Site.Master`, `RouteConfig`, `NotificationCheck` — ทีม B/A ทำ.
* ยังไม่ทำ: จ่ายผ่านเกตเวย์ (`PaymentSource.Dining` จองไว้แล้ว), ย้ายโต๊ะ/แยกบิล, kitchen ticket, ใบเสร็จอย่างย่อให้ลูกค้า (ม.86/6 — ดู `Sales_Doc_Local_Slip_Required`), ตอบแชทกลับถึงมือถือลูกค้า, รายงานยอดขายร้านอาหาร.
* ตรวจสลิปต้องมี PHASE19_34/36 + เปิด `SlipVerify_Enabled`; ไม่งั้นสลิปทุกใบ = รอพนักงาน.
* ลงบิลห้องใช้ `fn_GetReservationRoomNames` (PHASE5) เป็นป้ายห้อง; รายการที่เช็คอินแล้ว/ถือห้องวันนี้ (`RescheduleService.SqlHoldsRoom`) + วันเข้าพักวันนี้/เมื่อวาน.
* ต้อง build + test จริงบน Windows/IIS (Linux ทำได้แค่ compile-check + mono test ของคลาสบริสุทธิ์).
