# Room Charge System Design Document
**Project**: Take Time BangPhra - POS Integration with Reservation System
**Date**: 2025-11-06
**Author**: Claude AI Assistant

---

## 📋 Table of Contents
1. [Overview](#overview)
2. [Current System Analysis](#current-system-analysis)
3. [Requirements](#requirements)
4. [Database Schema Design](#database-schema-design)
5. [Business Logic Layer](#business-logic-layer)
6. [UI Modifications](#ui-modifications)
7. [Workflows](#workflows)
8. [Implementation Plan](#implementation-plan)

---

## 1. Overview

### Purpose
Integrate the POS (Product sales) system with the Reservation system to allow:
- Charging products/food/beverages to guest room accounts
- Deferred payment (charge now, pay at checkout)
- Immediate payment option
- Combined receipt generation (accommodation + products + rentals)

### Key Features
1. **Room Charge Mode**: Deduct stock, add to room bill, no immediate payment
2. **Pay Now Mode**: Deduct stock, collect payment immediately
3. **Product Management in Reservations**: View, delete, and manage charged products
4. **Combined Receipt**: Generate single tax invoice for all charges when fully paid
5. **Rental Equipment Integration**: Ensure rental items appear in receipt details

---

## 2. Current System Analysis

### 2.1 Product Page (Product/Default.aspx)
**Location**: `/Take Time BangPhra/Product/Default.aspx`

**Current Functionality**:
- Barcode/name scanning for product selection
- Shopping cart (GridView1) stored in Session["dtOrder"]
- Payment method selection (DropDownList1 → Account_Paid_How)
- Optional tax invoice generation (Panel1 with customer details)
- Saves to Account_Receipt and Account_Receipt_Detail tables
- Stock deduction happens on Button2_Click

**Key Code Sections**:
```csharp
// Product/Default.aspx.cs
- Line 69: Load products from Product table
- Line 98: renderProduct() - adds items to cart
- Line 419: Receipt generation with Account_Receipt_Detail
```

**Database Tables Used**:
- `Product` (ID, Barcode, Product_Name, Sell_Price, Category_ID, Status, Amount)
- `Account_Receipt` (ID, Date, Total, Customer info)
- `Account_Receipt_Detail` (Receipt_ID, Product_Name, Amount, Price, Total)

### 2.2 Reserve Page (Reserve.aspx)
**Location**: `/Take Time BangPhra/Reserve.aspx`

**Current Functionality**:
- Check-in date selection (TextBox12)
- Accommodation selection (GridView1)
- Rental item selection (GridView2 in Panel2)
- Deposit payment with slip upload
- Payment history display (gvPaymentHistory) - lines 611-672
- Guest information collection
- Tax invoice option (CheckBox3, Panel1)

**Key Tables**:
- `Reservation` (ID, CheckInDate, TotalPrice, Deposit, Status, Customer info)
- `Accommodation` (ID, AccomName, Price, People)
- `Items` (ID, ItemName, Price, Amount) - rental equipment
- `Payment_History` (Reservation_ID, PaymentAmount, PaymentType, PaymentMethod, Status)
- `Account_Receipt` / `Account_Receipt_Detail` - tax invoices

### 2.3 Existing Services
**AccountingService.cs** (`/Take Time BangPhra/Class/AccountingService.cs`):
- CreateReceipt() - generates tax invoices

**ReceiptService.cs** (`/Take Time BangPhra/Class/ReceiptService.cs`):
- CreateDepositReceipt() - creates deposit receipts
- CreateRegularReceipt() - creates payment receipts

**PaymentDataAccess.cs** (`/Take Time BangPhra/Class/PaymentDataAccess.cs`):
- RecordPayment() - records payments in Payment_History
- GetRemainingBalance() - calculates remaining balance

---

## 3. Requirements

### 3.1 Product Page Requirements
**REQ-PROD-01**: Add guest selection dropdown
- Show only guests with active reservations (Status = 'เข้าพักแล้ว' or similar)
- Filter by check-in date = today OR current check-in guests

**REQ-PROD-02**: Add "Charge Mode" selection
- Radio buttons or dropdown:
  - Option 1: "ชาร์จเข้าห้อง" (Charge to Room)
  - Option 2: "ชำระเงินทันที" (Pay Now)

**REQ-PROD-03**: Charge to Room behavior
- Deduct stock from Product.Amount
- Create record in Reservation_Product_Charges (new table)
- Update Reservation.TotalPrice += product total
- Do NOT create Account_Receipt
- Do NOT collect payment

**REQ-PROD-04**: Pay Now behavior
- Existing behavior (create receipt, collect payment)
- Also create link record in Reservation_Product_Charges if guest is selected
- Allow non-guest purchases (regular POS sales)

**REQ-PROD-05**: Tax Invoice Integration
- When "Charge to Room" + "ออกใบกำกับภาษี" is checked:
  - Pull customer info from Reservation
  - Generate receipt immediately OR defer until checkout

### 3.2 Reserve Page Requirements
**REQ-RES-01**: Add "Product Charges" section (GridView3)
- Show after Guest Information section
- Visible only when editing existing reservation (not on first-time booking)
- Columns: Product Name, Quantity, Price, Total, Date Charged, Delete Button

**REQ-RES-02**: Delete Product Charge
- Remove from Reservation_Product_Charges
- Return stock to Product.Amount
- Reduce Reservation.TotalPrice
- Update Payment_History remaining balance

**REQ-RES-03**: Pre-configured Products for New Reservations
- Show product selection for new bookings
- "สินค้าที่สามารถจองล่วงหน้า" (Products available for advance booking)
- Filter: Product.CanPreBook = 1 (new column needed)
- Examples: BBQ packages, breakfast sets, etc.

**REQ-RES-04**: Rental Equipment Receipt Integration
- Check if Items from GridView2 are in Account_Receipt_Detail
- If missing, add line item: "เช่าอุปกรณ์" with total rental cost
- Ensure rental items appear in combined receipt

**REQ-RES-05**: Combined Receipt Generation
- Trigger: When remaining balance = 0 (fully paid)
- Location: Reserve.aspx OR MakePayment page
- Contents:
  - Accommodation: "ค่าที่พัก {nights} คืน"
  - Rental: "เช่าอุปกรณ์" (itemized or grouped)
  - Products: "อาหารและเครื่องดื่ม" (itemized or grouped)
  - Individual line items in Account_Receipt_Detail

---

## 4. Database Schema Design

### 4.1 New Table: Reservation_Product_Charges

```sql
CREATE TABLE [dbo].[Reservation_Product_Charges] (
    [ID] bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,

    -- Core References
    [Reservation_ID] int NOT NULL,
    [Product_ID] int NOT NULL,

    -- Product Info (snapshot at time of charge)
    [Product_Name] nvarchar(255) NOT NULL,
    [Product_Barcode] nvarchar(50) NULL,
    [Category_ID] int NULL,

    -- Quantity & Pricing
    [Quantity] decimal(10,2) NOT NULL DEFAULT 1,
    [UnitPrice] decimal(10,2) NOT NULL,
    [TotalAmount] decimal(10,2) NOT NULL,

    -- Status & Tracking
    [Status] nvarchar(20) NOT NULL DEFAULT 'PENDING',
        -- PENDING: Charged to room, not paid
        -- PAID: Payment collected
        -- CANCELLED: Deleted/refunded, stock returned

    [ChargeType] nvarchar(20) NOT NULL DEFAULT 'ROOM_CHARGE',
        -- ROOM_CHARGE: Deferred payment
        -- IMMEDIATE: Paid immediately at POS
        -- PRE_BOOKING: Pre-booked with reservation

    [IsPaid] bit NOT NULL DEFAULT 0,

    -- Payment Tracking
    [Receipt_ID] nvarchar(50) NULL, -- Link to Account_Receipt when paid
    [PaymentDate] datetime NULL,

    -- Stock Management
    [StockDeducted] bit NOT NULL DEFAULT 1,
    [StockReturned] bit NOT NULL DEFAULT 0,
    [OriginalStock] int NULL, -- For audit trail

    -- Audit Fields
    [ChargedDate] datetime NOT NULL DEFAULT GETDATE(),
    [ChargedBy_AdminID] smallint NULL,
    [CancelledDate] datetime NULL,
    [CancelledBy_AdminID] smallint NULL,
    [CancelReason] nvarchar(500) NULL,
    [Notes] nvarchar(500) NULL,

    -- Foreign Keys
    CONSTRAINT [FK_ReservationProductCharges_Reservation]
        FOREIGN KEY ([Reservation_ID]) REFERENCES [dbo].[Reservation]([ID]),

    CONSTRAINT [FK_ReservationProductCharges_Product]
        FOREIGN KEY ([Product_ID]) REFERENCES [dbo].[Product]([ID]),

    CONSTRAINT [FK_ReservationProductCharges_Admin_Charged]
        FOREIGN KEY ([ChargedBy_AdminID]) REFERENCES [dbo].[Admin]([ID]),

    CONSTRAINT [FK_ReservationProductCharges_Admin_Cancelled]
        FOREIGN KEY ([CancelledBy_AdminID]) REFERENCES [dbo].[Admin]([ID]),

    -- Constraints
    CONSTRAINT [CK_ReservationProductCharges_Status]
        CHECK ([Status] IN ('PENDING', 'PAID', 'CANCELLED')),

    CONSTRAINT [CK_ReservationProductCharges_ChargeType]
        CHECK ([ChargeType] IN ('ROOM_CHARGE', 'IMMEDIATE', 'PRE_BOOKING')),

    CONSTRAINT [CK_ReservationProductCharges_Quantity]
        CHECK ([Quantity] > 0),

    CONSTRAINT [CK_ReservationProductCharges_Amount]
        CHECK ([TotalAmount] >= 0)
);

-- Indexes
CREATE NONCLUSTERED INDEX [IX_ReservationProductCharges_Reservation]
ON [dbo].[Reservation_Product_Charges] ([Reservation_ID], [Status])
INCLUDE ([TotalAmount]);

CREATE NONCLUSTERED INDEX [IX_ReservationProductCharges_Product]
ON [dbo].[Reservation_Product_Charges] ([Product_ID], [Status]);

CREATE NONCLUSTERED INDEX [IX_ReservationProductCharges_Receipt]
ON [dbo].[Reservation_Product_Charges] ([Receipt_ID])
WHERE [Receipt_ID] IS NOT NULL;

CREATE NONCLUSTERED INDEX [IX_ReservationProductCharges_ChargedDate]
ON [dbo].[Reservation_Product_Charges] ([ChargedDate] DESC);
```

### 4.2 Table Modifications

**Product Table - Add Column**:
```sql
-- Allow products to be pre-booked during reservation
ALTER TABLE [dbo].[Product]
ADD [CanPreBook] bit NOT NULL DEFAULT 0;

-- Add index
CREATE NONCLUSTERED INDEX [IX_Product_PreBook]
ON [dbo].[Product] ([CanPreBook])
WHERE [Status] = 'True' AND [CanPreBook] = 1;
```

**Account_Receipt_Detail - Add Column** (if not exists):
```sql
-- Link receipt details to room charges
ALTER TABLE [dbo].[Account_Receipt_Detail]
ADD [ReservationProductCharge_ID] bigint NULL;

ALTER TABLE [dbo].[Account_Receipt_Detail]
ADD CONSTRAINT [FK_ReceiptDetail_ReservationProductCharge]
    FOREIGN KEY ([ReservationProductCharge_ID])
    REFERENCES [dbo].[Reservation_Product_Charges]([ID]);
```

### 4.3 Views

**View: Active Guest Reservations (for POS dropdown)**
```sql
CREATE VIEW [dbo].[vw_ActiveGuestReservations]
AS
SELECT
    r.ID AS ReservationID,
    r.CheckInDate,
    r.CheckOutDate,
    r.Name AS GuestName,
    r.MobilePhone,
    r.TotalPrice,
    r.Deposit,
    r.Status,
    (SELECT ISNULL(SUM(PaymentAmount), 0)
     FROM Payment_History
     WHERE Reservation_ID = r.ID AND Status = 'COMPLETED') AS TotalPaid,
    (r.TotalPrice - ISNULL((SELECT SUM(PaymentAmount)
                            FROM Payment_History
                            WHERE Reservation_ID = r.ID AND Status = 'COMPLETED'), 0)) AS RemainingBalance,
    (SELECT ISNULL(SUM(TotalAmount), 0)
     FROM Reservation_Product_Charges
     WHERE Reservation_ID = r.ID AND Status = 'PENDING') AS PendingCharges
FROM [dbo].[Reservation] r
WHERE r.Status IN (N'เข้าพักแล้ว', N'จองเรียบร้อย')
  AND r.CheckInDate <= GETDATE()
  AND r.CheckOutDate >= GETDATE();
GO
```

**View: Reservation Product Charges Summary**
```sql
CREATE VIEW [dbo].[vw_ReservationProductCharges]
AS
SELECT
    rpc.ID,
    rpc.Reservation_ID,
    rpc.Product_Name,
    rpc.Quantity,
    rpc.UnitPrice,
    rpc.TotalAmount,
    rpc.Status,
    rpc.ChargeType,
    rpc.IsPaid,
    rpc.ChargedDate,
    rpc.Receipt_ID,
    rpc.PaymentDate,
    p.Category_Name,
    a.Username AS ChargedBy,
    r.Name AS GuestName,
    r.MobilePhone
FROM [dbo].[Reservation_Product_Charges] rpc
INNER JOIN [dbo].[Reservation] r ON rpc.Reservation_ID = r.ID
LEFT JOIN [dbo].[Product] p ON rpc.Product_ID = p.ID
LEFT JOIN [dbo].[Product_Category] pc ON p.Category_ID = pc.ID
LEFT JOIN [dbo].[Admin] a ON rpc.ChargedBy_AdminID = a.ID;
GO
```

---

## 5. Business Logic Layer

### 5.1 RoomChargeDataAccess.cs

**Location**: `/Take Time BangPhra/Class/RoomChargeDataAccess.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Data;

namespace Take_Time_BangPhra
{
    /// <summary>
    /// 🔒 SECURE Data Access Layer for Room Charge operations
    /// All methods use parameterized queries to prevent SQL Injection
    /// </summary>
    public class RoomChargeDataAccess
    {
        private readonly code _code;
        private readonly string _connectionString;

        public RoomChargeDataAccess(string connectionString)
        {
            _code = new code();
            _connectionString = connectionString;
        }

        #region Charge Creation

        /// <summary>
        /// Create a room charge record
        /// </summary>
        public long CreateRoomCharge(
            int reservationId,
            int productId,
            string productName,
            string productBarcode,
            int? categoryId,
            decimal quantity,
            decimal unitPrice,
            decimal totalAmount,
            string chargeType,
            int? chargedByAdminId,
            string notes = null)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@reservationId", reservationId },
                { "@productId", productId },
                { "@productName", productName },
                { "@barcode", productBarcode },
                { "@categoryId", categoryId },
                { "@quantity", quantity },
                { "@unitPrice", unitPrice },
                { "@totalAmount", totalAmount },
                { "@chargeType", chargeType },
                { "@chargedBy", chargedByAdminId },
                { "@notes", notes }
            };

            return _code.DatabaseInsertReturnSafe(_connectionString,
                @"INSERT INTO Reservation_Product_Charges (
                    Reservation_ID, Product_ID, Product_Name, Product_Barcode,
                    Category_ID, Quantity, UnitPrice, TotalAmount, ChargeType,
                    ChargedBy_AdminID, Notes, Status, IsPaid, StockDeducted
                  )
                  VALUES (
                    @reservationId, @productId, @productName, @barcode,
                    @categoryId, @quantity, @unitPrice, @totalAmount, @chargeType,
                    @chargedBy, @notes, 'PENDING', 0, 1
                  );
                  SELECT SCOPE_IDENTITY();",
                parameters);
        }

        #endregion

        #region Charge Queries

        /// <summary>
        /// Get all charges for a reservation
        /// </summary>
        public DataTable GetReservationCharges(int reservationId, string status = null)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@reservationId", reservationId }
            };

            string query = @"
                SELECT * FROM vw_ReservationProductCharges
                WHERE Reservation_ID = @reservationId";

            if (!string.IsNullOrEmpty(status))
            {
                query += " AND Status = @status";
                parameters.Add("@status", status);
            }

            query += " ORDER BY ChargedDate DESC";

            return _code.DatabaseQuerySafe(_connectionString, query, parameters);
        }

        /// <summary>
        /// Get total pending charges for a reservation
        /// </summary>
        public decimal GetTotalPendingCharges(int reservationId)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@reservationId", reservationId }
            };

            var result = _code.DatabaseQuerySafe(_connectionString,
                @"SELECT ISNULL(SUM(TotalAmount), 0) as TotalPending
                  FROM Reservation_Product_Charges
                  WHERE Reservation_ID = @reservationId
                  AND Status = 'PENDING'",
                parameters);

            if (result.Rows.Count > 0)
            {
                return Convert.ToDecimal(result.Rows[0]["TotalPending"]);
            }

            return 0;
        }

        /// <summary>
        /// Get active guest reservations (for POS dropdown)
        /// </summary>
        public DataTable GetActiveGuestReservations()
        {
            return _code.DatabaseQuerySafe(_connectionString,
                "SELECT * FROM vw_ActiveGuestReservations ORDER BY CheckInDate DESC",
                null);
        }

        #endregion

        #region Charge Updates

        /// <summary>
        /// Mark charge as paid (when receipt is generated)
        /// </summary>
        public void MarkChargeAsPaid(long chargeId, string receiptId)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@chargeId", chargeId },
                { "@receiptId", receiptId },
                { "@paymentDate", DateTime.Now }
            };

            _code.DatabaseInsertSafe(_connectionString,
                @"UPDATE Reservation_Product_Charges
                  SET Status = 'PAID',
                      IsPaid = 1,
                      Receipt_ID = @receiptId,
                      PaymentDate = @paymentDate
                  WHERE ID = @chargeId",
                parameters);
        }

        /// <summary>
        /// Cancel charge and return stock
        /// </summary>
        public void CancelCharge(
            long chargeId,
            int? cancelledByAdminId,
            string cancelReason)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@chargeId", chargeId },
                { "@cancelledBy", cancelledByAdminId },
                { "@cancelReason", cancelReason },
                { "@cancelDate", DateTime.Now }
            };

            _code.DatabaseInsertSafe(_connectionString,
                @"UPDATE Reservation_Product_Charges
                  SET Status = 'CANCELLED',
                      StockReturned = 1,
                      CancelledDate = @cancelDate,
                      CancelledBy_AdminID = @cancelledBy,
                      CancelReason = @cancelReason
                  WHERE ID = @chargeId",
                parameters);
        }

        #endregion

        #region Stock Management

        /// <summary>
        /// Deduct product stock
        /// </summary>
        public void DeductProductStock(int productId, decimal quantity)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@productId", productId },
                { "@quantity", quantity }
            };

            _code.DatabaseInsertSafe(_connectionString,
                @"UPDATE Product
                  SET Amount = Amount - @quantity
                  WHERE ID = @productId",
                parameters);
        }

        /// <summary>
        /// Return product stock
        /// </summary>
        public void ReturnProductStock(int productId, decimal quantity)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@productId", productId },
                { "@quantity", quantity }
            };

            _code.DatabaseInsertSafe(_connectionString,
                @"UPDATE Product
                  SET Amount = Amount + @quantity
                  WHERE ID = @productId",
                parameters);
        }

        #endregion
    }
}
```

### 5.2 RoomChargeService.cs

**Location**: `/Take Time BangPhra/Class/RoomChargeService.cs`

```csharp
using System;
using System.Data;

namespace Take_Time_BangPhra
{
    /// <summary>
    /// Room Charge Service - Business logic for room charge operations
    /// </summary>
    public class RoomChargeService
    {
        private readonly string _connectionString;
        private readonly RoomChargeDataAccess _chargeDA;
        private readonly PaymentDataAccess _paymentDA;
        private readonly code _code;

        public RoomChargeService(string connectionString)
        {
            _connectionString = connectionString;
            _chargeDA = new RoomChargeDataAccess(connectionString);
            _paymentDA = new PaymentDataAccess(connectionString);
            _code = new code();
        }

        /// <summary>
        /// Charge products to guest room
        /// </summary>
        public long ChargeToRoom(
            int reservationId,
            DataTable cartItems, // dtOrder from session
            int? adminId,
            string notes = null)
        {
            long lastChargeId = 0;

            try
            {
                foreach (DataRow item in cartItems.Rows)
                {
                    int productId = Convert.ToInt32(item["ID"]);
                    string productName = item["Product_Name"].ToString();
                    string barcode = item["Barcode"]?.ToString();
                    int? categoryId = item["Category_ID"] != DBNull.Value
                        ? Convert.ToInt32(item["Category_ID"])
                        : (int?)null;
                    decimal quantity = Convert.ToDecimal(item["Amount"]);
                    decimal unitPrice = Convert.ToDecimal(item["Sell_Price"]);
                    decimal total = Convert.ToDecimal(item["Price_Total"]);

                    // Deduct stock
                    _chargeDA.DeductProductStock(productId, quantity);

                    // Create charge record
                    lastChargeId = _chargeDA.CreateRoomCharge(
                        reservationId,
                        productId,
                        productName,
                        barcode,
                        categoryId,
                        quantity,
                        unitPrice,
                        total,
                        "ROOM_CHARGE",
                        adminId,
                        notes
                    );

                    // Update reservation total
                    UpdateReservationTotal(reservationId, total, isAddition: true);
                }

                // Log success
                _code.Logs(_connectionString,
                    "RoomChargeService.ChargeToRoom",
                    $"Charged {cartItems.Rows.Count} items to Reservation {reservationId}",
                    adminId?.ToString() ?? "SYSTEM");

                return lastChargeId;
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString,
                    "RoomChargeService.ChargeToRoom Error",
                    $"ReservationID: {reservationId}, Error: {ex.Message}",
                    adminId?.ToString() ?? "SYSTEM");
                throw;
            }
        }

        /// <summary>
        /// Cancel room charge and return stock
        /// </summary>
        public void CancelRoomCharge(
            long chargeId,
            int? adminId,
            string reason)
        {
            try
            {
                // Get charge details
                var chargeData = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT * FROM Reservation_Product_Charges WHERE ID = @chargeId",
                    new System.Collections.Generic.Dictionary<string, object>
                    {
                        { "@chargeId", chargeId }
                    });

                if (chargeData.Rows.Count == 0)
                    throw new Exception("Charge not found");

                var charge = chargeData.Rows[0];

                if (charge["Status"].ToString() != "PENDING")
                    throw new Exception("Can only cancel PENDING charges");

                int reservationId = Convert.ToInt32(charge["Reservation_ID"]);
                int productId = Convert.ToInt32(charge["Product_ID"]);
                decimal quantity = Convert.ToDecimal(charge["Quantity"]);
                decimal totalAmount = Convert.ToDecimal(charge["TotalAmount"]);

                // Return stock
                _chargeDA.ReturnProductStock(productId, quantity);

                // Mark as cancelled
                _chargeDA.CancelCharge(chargeId, adminId, reason);

                // Update reservation total
                UpdateReservationTotal(reservationId, totalAmount, isAddition: false);

                // Log
                _code.Logs(_connectionString,
                    "RoomChargeService.CancelRoomCharge",
                    $"Cancelled Charge {chargeId}, Returned {quantity} units of Product {productId}",
                    adminId?.ToString() ?? "SYSTEM");
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString,
                    "RoomChargeService.CancelRoomCharge Error",
                    $"ChargeID: {chargeId}, Error: {ex.Message}",
                    adminId?.ToString() ?? "SYSTEM");
                throw;
            }
        }

        /// <summary>
        /// Update reservation total price
        /// </summary>
        private void UpdateReservationTotal(int reservationId, decimal amount, bool isAddition)
        {
            string operation = isAddition ? "+" : "-";

            _code.DatabaseInsertSafe(_connectionString,
                $@"UPDATE Reservation
                   SET TotalPrice = TotalPrice {operation} @amount
                   WHERE ID = @reservationId",
                new System.Collections.Generic.Dictionary<string, object>
                {
                    { "@reservationId", reservationId },
                    { "@amount", amount }
                });
        }

        /// <summary>
        /// Get pending charges for receipt generation
        /// </summary>
        public DataTable GetPendingChargesForReceipt(int reservationId)
        {
            return _chargeDA.GetReservationCharges(reservationId, "PENDING");
        }

        /// <summary>
        /// Mark all charges as paid (when combined receipt is generated)
        /// </summary>
        public void MarkAllChargesAsPaid(int reservationId, string receiptId)
        {
            var charges = GetPendingChargesForReceipt(reservationId);

            foreach (DataRow charge in charges.Rows)
            {
                long chargeId = Convert.ToInt64(charge["ID"]);
                _chargeDA.MarkChargeAsPaid(chargeId, receiptId);
            }
        }
    }
}
```

---

## 6. UI Modifications

### 6.1 Product Page (Product/Default.aspx)

**Add Guest Selection Section** (after line 213, before product search):

```html
<!-- Guest Room Charge Section -->
<tr>
    <td class="auto-style1">เลือกห้องพัก (Room Charge):</td>
    <td class="auto-style2">
        <asp:DropDownList ID="ddlGuestReservation" runat="server" Width="60%"
            CssClass="rounded-textbox" AutoPostBack="True"
            OnSelectedIndexChanged="ddlGuestReservation_SelectedIndexChanged"
            AppendDataBoundItems="true">
            <asp:ListItem Value="0">--- ไม่ชาร์จเข้าห้อง (ชำระทันที) ---</asp:ListItem>
        </asp:DropDownList>
        <asp:Label ID="lblGuestInfo" runat="server" CssClass="text-info"
            style="margin-left: 10px;"></asp:Label>
    </td>
</tr>

<tr id="trChargeMode" runat="server" visible="false">
    <td class="auto-style1">โหมดการชำระเงิน:</td>
    <td class="auto-style2">
        <asp:RadioButtonList ID="rblChargeMode" runat="server" RepeatDirection="Horizontal">
            <asp:ListItem Value="ROOM_CHARGE" Selected="True">ชาร์จเข้าห้อง (ชำระทีหลัง)</asp:ListItem>
            <asp:ListItem Value="PAY_NOW">ชำระเงินทันที</asp:ListItem>
        </asp:RadioButtonList>
        <div class="text-muted" style="margin-top: 5px; font-size: 0.9em;">
            💡 <strong>ชาร์จเข้าห้อง:</strong> ตัดสต๊อก แต่ไม่เก็บเงิน (รวมในบิลเช็คเอาท์)<br />
            💡 <strong>ชำระเงินทันที:</strong> ตัดสต๊อกและเก็บเงินเลย + ออกใบเสร็จ
        </div>
    </td>
</tr>
```

**Code-behind modifications (Product/Default.aspx.cs)**:

```csharp
// Add at class level
private RoomChargeService _roomChargeService;

protected void Page_Load(object sender, EventArgs e)
{
    // ... existing code ...

    _roomChargeService = new RoomChargeService(conn);

    if (!IsPostBack)
    {
        // ... existing code ...
        LoadActiveGuests();
    }
}

/// <summary>
/// Load active guest reservations into dropdown
/// </summary>
private void LoadActiveGuests()
{
    try
    {
        var roomChargeDA = new RoomChargeDataAccess(conn);
        var guests = roomChargeDA.GetActiveGuestReservations();

        ddlGuestReservation.DataSource = guests;
        ddlGuestReservation.DataTextField = "DisplayText"; // Format: "Room 101 - Guest Name (0812345678)"
        ddlGuestReservation.DataValueField = "ReservationID";
        ddlGuestReservation.DataBind();

        // Add default item at top
        ddlGuestReservation.Items.Insert(0, new ListItem("--- ไม่ชาร์จเข้าห้อง (ชำระทันที) ---", "0"));
    }
    catch (Exception ex)
    {
        code.Logs(conn, "Product.LoadActiveGuests Error", ex.Message, Session["User"]?.ToString());
    }
}

/// <summary>
/// Guest selection changed - show/hide charge mode
/// </summary>
protected void ddlGuestReservation_SelectedIndexChanged(object sender, EventArgs e)
{
    if (ddlGuestReservation.SelectedValue != "0")
    {
        trChargeMode.Visible = true;

        // Load guest info
        int reservationId = Convert.ToInt32(ddlGuestReservation.SelectedValue);
        LoadGuestInfo(reservationId);

        // Default to Room Charge mode
        rblChargeMode.SelectedValue = "ROOM_CHARGE";

        // Disable payment method selection for room charge
        DropDownList1.Enabled = false;
    }
    else
    {
        trChargeMode.Visible = false;
        DropDownList1.Enabled = true;
        lblGuestInfo.Text = "";
    }
}

/// <summary>
/// Load and display guest information
/// </summary>
private void LoadGuestInfo(int reservationId)
{
    try
    {
        var dt = code.DatabaseQuerySafe(conn,
            "SELECT * FROM vw_ActiveGuestReservations WHERE ReservationID = @id",
            new Dictionary<string, object> { { "@id", reservationId } });

        if (dt.Rows.Count > 0)
        {
            var row = dt.Rows[0];
            decimal remaining = Convert.ToDecimal(row["RemainingBalance"]);
            decimal pendingCharges = Convert.ToDecimal(row["PendingCharges"]);

            lblGuestInfo.Text = $"ยอดค้างชำระ: {remaining:N2} บาท | สินค้าที่ยังไม่ชำระ: {pendingCharges:N2} บาท";
        }
    }
    catch (Exception ex)
    {
        code.Logs(conn, "Product.LoadGuestInfo Error", ex.Message, Session["User"]?.ToString());
    }
}

/// <summary>
/// Modify Button2_Click to handle room charge
/// </summary>
protected void Button2_Click(object sender, EventArgs e)
{
    try
    {
        DataTable dtOrder = (DataTable)Session["dtOrder"];

        if (dtOrder == null || dtOrder.Rows.Count == 0)
        {
            ClientScript.RegisterStartupScript(this.GetType(), "alert",
                "alert('ไม่มีสินค้าในตะกร้า');", true);
            return;
        }

        // Check if room charge mode
        if (ddlGuestReservation.SelectedValue != "0" && rblChargeMode.SelectedValue == "ROOM_CHARGE")
        {
            // ROOM CHARGE MODE
            ProcessRoomCharge(dtOrder);
        }
        else
        {
            // REGULAR POS MODE (existing logic)
            ProcessRegularSale(dtOrder);
        }
    }
    catch (Exception ex)
    {
        ClientScript.RegisterStartupScript(this.GetType(), "alert",
            $"alert('เกิดข้อผิดพลาด: {ex.Message}');", true);
        code.Logs(conn, "Product.Button2_Click Error", ex.Message, Session["User"]?.ToString());
    }
}

/// <summary>
/// Process room charge (new method)
/// </summary>
private void ProcessRoomCharge(DataTable dtOrder)
{
    int reservationId = Convert.ToInt32(ddlGuestReservation.SelectedValue);
    int? adminId = Session["AdminID"] != null ? Convert.ToInt32(Session["AdminID"]) : (int?)null;

    // Charge to room
    long chargeId = _roomChargeService.ChargeToRoom(reservationId, dtOrder, adminId);

    // Clear cart
    dtOrder.Clear();
    Session["dtOrder"] = dtOrder;
    GridView1.DataSource = dtOrder;
    GridView1.DataBind();
    TextBox2.Text = "0";

    // Success message
    ClientScript.RegisterStartupScript(this.GetType(), "success",
        "alert('บันทึกรายการชาร์จเข้าห้องเรียบร้อยแล้ว');", true);

    // Reload guest info
    LoadGuestInfo(reservationId);
}

/// <summary>
/// Process regular sale (existing logic, refactored)
/// </summary>
private void ProcessRegularSale(DataTable dtOrder)
{
    // ... existing Button2_Click code ...
    // Generate receipt, collect payment, etc.
}
```

---

### 6.2 Reserve Page (Reserve.aspx)

**Add Product Charges GridView** (after Payment Information section, around line 682):

```html
<!-- Product Charges Section (for existing reservations) -->
<div class="form-panel" id="divProductCharges" runat="server" visible="false">
    <h3 class="section-header">📦 รายการสินค้าที่ชาร์จเข้าห้อง</h3>

    <div style="margin-bottom: 10px;">
        <asp:Label ID="lblProductChargesSummary" runat="server" CssClass="price-display"></asp:Label>
    </div>

    <asp:GridView ID="gvProductCharges" runat="server" CssClass="mydatagrid"
        AutoGenerateColumns="False" EmptyDataText="ไม่มีรายการสินค้าที่ชาร์จเข้าห้อง"
        OnRowCommand="gvProductCharges_RowCommand">
        <Columns>
            <asp:BoundField DataField="ChargedDate" HeaderText="วันที่"
                DataFormatString="{0:dd/MM/yyyy HH:mm}" />
            <asp:BoundField DataField="Product_Name" HeaderText="รายการ" />
            <asp:BoundField DataField="Quantity" HeaderText="จำนวน"
                DataFormatString="{0:N2}" ItemStyle-HorizontalAlign="Center" />
            <asp:BoundField DataField="UnitPrice" HeaderText="ราคา/หน่วย"
                DataFormatString="{0:N2}" ItemStyle-HorizontalAlign="Right" />
            <asp:BoundField DataField="TotalAmount" HeaderText="รวม"
                DataFormatString="{0:N2}" ItemStyle-HorizontalAlign="Right" />
            <asp:TemplateField HeaderText="สถานะ">
                <ItemTemplate>
                    <span style="padding: 4px 8px; border-radius: 4px;
                        background-color: <%# Eval("Status").ToString() == "PENDING" ? "#FF9800" : "#4CAF50" %>;
                        color: white; font-size: 12px;">
                        <%# Eval("Status").ToString() == "PENDING" ? "รอชำระ" : "ชำระแล้ว" %>
                    </span>
                </ItemTemplate>
            </asp:TemplateField>
            <asp:TemplateField HeaderText="ลบ">
                <ItemTemplate>
                    <asp:Button ID="btnDeleteCharge" runat="server"
                        Text="ลบ" CssClass="reservation-button"
                        CommandName="DeleteCharge"
                        CommandArgument='<%# Eval("ID") %>'
                        Visible='<%# Eval("Status").ToString() == "PENDING" %>'
                        OnClientClick="return confirm('ต้องการลบรายการนี้? สต๊อกสินค้าจะถูกคืน');" />
                </ItemTemplate>
            </asp:TemplateField>
        </Columns>
    </asp:GridView>
</div>
```

**Code-behind modifications (Reserve.aspx.cs)**:

```csharp
// Add at class level
private RoomChargeService _roomChargeService;
private RoomChargeDataAccess _roomChargeDA;

protected void Page_Load(object sender, EventArgs e)
{
    // ... existing code ...

    _roomChargeService = new RoomChargeService(conn);
    _roomChargeDA = new RoomChargeDataAccess(conn);

    if (!IsPostBack)
    {
        // ... existing code ...
    }
    else
    {
        // Check if editing reservation
        string command = Request.QueryString["Command"];
        string id = Request.QueryString["ID"];

        if ((command == "Edit" || command == "CheckIn" || command == "CheckOut") && !string.IsNullOrEmpty(id))
        {
            LoadProductCharges(Convert.ToInt32(id));
        }
    }
}

/// <summary>
/// Load product charges for reservation
/// </summary>
private void LoadProductCharges(int reservationId)
{
    try
    {
        var charges = _roomChargeDA.GetReservationCharges(reservationId);

        if (charges.Rows.Count > 0)
        {
            divProductCharges.Visible = true;
            gvProductCharges.DataSource = charges;
            gvProductCharges.DataBind();

            // Calculate summary
            decimal totalPending = _roomChargeDA.GetTotalPendingCharges(reservationId);
            lblProductChargesSummary.Text = $"รวมสินค้าที่ยังไม่ชำระ: {totalPending:N2} บาท";
        }
        else
        {
            divProductCharges.Visible = false;
        }
    }
    catch (Exception ex)
    {
        code.Logs(conn, "Reserve.LoadProductCharges Error", ex.Message, Session["User"]?.ToString());
    }
}

/// <summary>
/// Handle delete charge button
/// </summary>
protected void gvProductCharges_RowCommand(object sender, GridViewCommandEventArgs e)
{
    if (e.CommandName == "DeleteCharge")
    {
        try
        {
            long chargeId = Convert.ToInt64(e.CommandArgument);
            int? adminId = Session["AdminID"] != null ? Convert.ToInt32(Session["AdminID"]) : (int?)null;

            // Cancel charge and return stock
            _roomChargeService.CancelRoomCharge(chargeId, adminId, "ลบโดยผู้ใช้จากหน้า Reserve");

            // Reload
            string id = Request.QueryString["ID"];
            if (!string.IsNullOrEmpty(id))
            {
                LoadProductCharges(Convert.ToInt32(id));

                // Reload reservation info to show updated total
                LoadReservationData(Convert.ToInt32(id));
            }

            ClientScript.RegisterStartupScript(this.GetType(), "success",
                "alert('ลบรายการเรียบร้อย สต๊อกสินค้าถูกคืนแล้ว');", true);
        }
        catch (Exception ex)
        {
            ClientScript.RegisterStartupScript(this.GetType(), "error",
                $"alert('เกิดข้อผิดพลาด: {ex.Message}');", true);
            code.Logs(conn, "Reserve.gvProductCharges_RowCommand Error", ex.Message, Session["User"]?.ToString());
        }
    }
}
```

---

## 7. Workflows

### 7.1 Workflow: Charge to Room

```
[POS Terminal] → [Select Guest] → [Add Products to Cart] → [Select "Charge to Room"] → [Save]
    ↓
1. Deduct stock from Product.Amount
2. Create record in Reservation_Product_Charges (Status=PENDING)
3. Update Reservation.TotalPrice += product total
4. Clear cart
5. Show success message
```

### 7.2 Workflow: Pay Now with Guest Selected

```
[POS Terminal] → [Select Guest] → [Add Products] → [Select "Pay Now"] → [Generate Receipt] → [Collect Payment]
    ↓
1. Deduct stock from Product.Amount
2. Create Account_Receipt + Account_Receipt_Detail
3. Create record in Reservation_Product_Charges (Status=PAID, IsPaid=1, Receipt_ID filled)
4. Create Payment_History record (if charging to reservation)
5. Clear cart
```

### 7.3 Workflow: Delete Product Charge

```
[Reserve Page] → [Edit Reservation] → [Product Charges Section] → [Click Delete]
    ↓
1. Confirm deletion dialog
2. Return stock: Product.Amount += Quantity
3. Update Reservation_Product_Charges (Status=CANCELLED, StockReturned=1)
4. Update Reservation.TotalPrice -= TotalAmount
5. Recalculate remaining balance in Payment_History
6. Reload grid
```

### 7.4 Workflow: Combined Receipt Generation

```
[Reserve/MakePayment Page] → [Final Payment] → [RemainingBalance = 0] → [Generate Combined Receipt]
    ↓
1. Check if remaining balance = 0
2. Gather all charges:
   - Accommodation: "ค่าที่พัก {nights} คืน" (from Reservation)
   - Rental Items: "เช่าอุปกรณ์" (from GridView2 selections)
   - Product Charges: "อาหารและเครื่องดื่ม" (from Reservation_Product_Charges WHERE Status=PENDING)
3. Create Account_Receipt
4. Create Account_Receipt_Detail entries for each category
5. Mark all Reservation_Product_Charges as PAID (Status=PAID, Receipt_ID filled)
6. Create Payment_History record
7. Generate PDF/print receipt
```

---

## 8. Implementation Plan

### Phase 1: Database Setup
**Duration**: 1 hour

1. Create migration SQL script
2. Add Reservation_Product_Charges table
3. Add Product.CanPreBook column
4. Create views (vw_ActiveGuestReservations, vw_ReservationProductCharges)
5. Create indexes
6. Test migration on dev database

### Phase 2: Business Logic Layer
**Duration**: 2 hours

1. Create RoomChargeDataAccess.cs
2. Create RoomChargeService.cs
3. Write unit tests (optional)
4. Integration testing with existing services

### Phase 3: Product Page Modifications
**Duration**: 3 hours

1. Add guest selection UI (dropdown + radio buttons)
2. Implement LoadActiveGuests()
3. Modify Button2_Click to route to ProcessRoomCharge() or ProcessRegularSale()
4. Implement ProcessRoomCharge()
5. Test room charge workflow
6. Test pay now workflow with guest selected

### Phase 4: Reserve Page Modifications
**Duration**: 3 hours

1. Add Product Charges GridView
2. Implement LoadProductCharges()
3. Implement delete charge functionality
4. Add rental equipment check (ensure Items in receipt)
5. Test delete and stock return workflow

### Phase 5: Combined Receipt Generation
**Duration**: 4 hours

1. Detect when remaining balance = 0
2. Gather all charge types (accommodation, rentals, products)
3. Create combined Account_Receipt_Detail entries
4. Mark charges as paid
5. Generate PDF receipt
6. Test various scenarios (with/without products, with/without rentals)

### Phase 6: Testing & Refinement
**Duration**: 2 hours

1. End-to-end testing of all workflows
2. Stock management verification
3. Payment tracking verification
4. Receipt generation testing
5. Edge case testing (cancel after charge, duplicate charges, etc.)
6. UI/UX refinements

### Phase 7: Deployment
**Duration**: 1 hour

1. Create deployment checklist
2. Backup production database
3. Run migration on production
4. Deploy code changes
5. Smoke testing
6. Monitor logs

---

## 9. Testing Checklist

### Stock Management Tests
- [ ] Charge to room deducts stock correctly
- [ ] Delete charge returns stock correctly
- [ ] Pay now deducts stock correctly
- [ ] Stock cannot go negative

### Financial Tests
- [ ] Reservation.TotalPrice updates correctly on charge
- [ ] Reservation.TotalPrice updates correctly on delete
- [ ] Payment_History RemainingBalance is accurate
- [ ] Combined receipt total matches reservation total

### Receipt Generation Tests
- [ ] Charge to room does not generate immediate receipt
- [ ] Pay now generates receipt immediately
- [ ] Combined receipt includes all charge types
- [ ] Rental equipment appears in combined receipt
- [ ] Receipt details match charges

### User Experience Tests
- [ ] Guest dropdown shows only active reservations
- [ ] Guest info displays correctly (balance, pending charges)
- [ ] Product charges grid shows/hides appropriately
- [ ] Delete confirmation works
- [ ] Success/error messages are clear
- [ ] Page reloads preserve state

### Edge Cases
- [ ] Guest checks out with pending charges (should prevent or force payment)
- [ ] Duplicate product charge
- [ ] Delete charge after partial payment
- [ ] Cancel reservation with pending charges
- [ ] Receipt generation with mixed paid/unpaid charges

---

## 10. Future Enhancements

1. **Mobile App Integration**: Allow guests to view their room charges in real-time
2. **Pre-authorization**: Hold credit card for pending charges
3. **Charge Approval Workflow**: Require manager approval for large charges
4. **Product Categories**: Group products by category in receipts (beverages, food, snacks)
5. **Daily Charge Summary**: Email guests daily summary of charges
6. **Loyalty Points**: Award points for room charges
7. **Package Deals**: Pre-book product packages with reservation (BBQ set, breakfast, etc.)
8. **Reporting**: Daily room charge reports, top products, etc.

---

## Appendix

### A. Database Table Reference

**Existing Tables Used**:
- `Reservation` - guest reservations
- `Product` - product inventory
- `Account_Receipt` - tax invoices
- `Account_Receipt_Detail` - invoice line items
- `Payment_History` - payment tracking
- `Items` - rental equipment
- `Admin` - staff accounts

**New Tables Created**:
- `Reservation_Product_Charges` - room charge records

### B. Code File Reference

**New Files**:
- `/Take Time BangPhra/Class/RoomChargeDataAccess.cs`
- `/Take Time BangPhra/Class/RoomChargeService.cs`
- `/Database/PHASE5_Migration_01_Room_Charge_System.sql`

**Modified Files**:
- `/Take Time BangPhra/Product/Default.aspx`
- `/Take Time BangPhra/Product/Default.aspx.cs`
- `/Take Time BangPhra/Reserve.aspx`
- `/Take Time BangPhra/Reserve.aspx.cs`

### C. Contact & Support

For questions or issues during implementation, refer to:
- Previous session summaries
- Code comments
- This design document

---

**Document Version**: 1.0
**Last Updated**: 2025-11-06
**Status**: Design Complete - Ready for Implementation
