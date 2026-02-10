# Migration Guide: Legacy WebForms → Microservices

## Overview

This guide maps every module from the legacy ASP.NET Web Forms application
to its corresponding microservice in the new architecture.

---

## File-to-Service Mapping

### Reservation Service (Port 5001)

| Legacy File | New Location | Notes |
|---|---|---|
| `Reserve.aspx.cs` (435KB) | `Reservation.Application/Commands/` | Split into CreateReservation, CheckIn, CheckOut commands |
| `ReserveTable.aspx.cs` | `Reservation.Application/Commands/` | Table reservation support |
| `ReservationList.aspx.cs` | `Reservation.Application/Queries/SearchReservationsQuery` | List with pagination/filter |
| `DisplayReserve.aspx.cs` | `Reservation.Application/Queries/GetReservationByIdQuery` | Single reservation view |
| `DisplayToday.aspx.cs` | `Reservation.Application/Queries/GetTodayCheckInsQuery` | Today's activity |
| `CountReserved.aspx.cs` | `Reservation.Application/Queries/GetDashboardSummaryQuery` | Dashboard stats |
| `PostponeList.aspx.cs` | `Reservation.Application/Commands/PostponeCheckInCommand` | Postponement handling |
| `Class/ReservationService.cs` | `Reservation.Application/Services/` | Business logic extracted |
| `Admin/HolidayPrice.aspx.cs` | `Reservation.Application/Services/PricingService` | Dynamic pricing rules |
| `Admin/Pricing/DynamicPricing.aspx.cs` | `Reservation.Application/Services/PricingService` | Pricing engine |

### Payment Service (Port 5002)

| Legacy File | New Location | Notes |
|---|---|---|
| `Class/PaymentService.cs` | `Payment.Application/Commands/` | Payment processing |
| `Account/CheckPayment_New.aspx.cs` | `Payment.Application/Commands/VerifyPaymentCommand` | Payment verification |
| `Account/Receipt.aspx.cs` | `Payment.Application/Commands/GenerateReceiptCommand` | Receipt generation |
| `Account/PaymentVoucher.aspx.cs` | `Payment.Application/Commands/` | Payment vouchers |
| `Account/SlipVerification.aspx.cs` | `Payment.Application/Commands/VerifySlipCommand` | Slip OCR |
| `Class/ReceiptService.cs` | `Payment.Application/Services/` | Receipt logic |
| `API/TaxInvoiceAPI.ashx.cs` | `Payment.API/Controllers/TaxInvoicesController` | Tax invoice API |
| `Checkout.aspx.cs` | `Payment.Application/Commands/ProcessCheckoutCommand` | Checkout flow |

### Inventory Service (Port 5003)

| Legacy File | New Location | Notes |
|---|---|---|
| `Product/Default.aspx.cs` (100KB) | `Inventory.Application/Commands/CreateSalesTransactionCommand` | POS operations |
| `Product/In.aspx.cs` | `Inventory.Application/Commands/AdjustStockCommand` | Stock management |
| `Product/Stock.aspx.cs` | `Inventory.Application/Queries/GetProductsQuery` | Stock viewing |
| `Product/SellReport.aspx.cs` | `Inventory.Application/Queries/GetSalesReportQuery` | Sales reports |

### Human Resources Service (Port 5004)

| Legacy File | New Location | Notes |
|---|---|---|
| `Class/EmployeeService.cs` (92KB) | `HumanResources.Application/` | Employee management |
| `Admin/HR/EmployeeManagement.aspx.cs` | `HumanResources.Application/Commands/` | Employee CRUD |
| `Admin/HR/EmployeeProfile.aspx.cs` | `HumanResources.Application/Queries/` | Profile viewing |
| `Admin/HR/OTEntry.aspx.cs` | `HumanResources.Application/Commands/` | OT entry |
| `Admin/HR/OTManagement.aspx.cs` | `HumanResources.Application/Queries/` | OT management |
| `Class/LeaveService.cs` (92KB) | `HumanResources.Application/Commands/` | Leave management |
| `Admin/Leave/*.aspx.cs` | `HumanResources.Application/` | Leave workflows |
| `Class/PayrollService.cs` (58KB) | `HumanResources.Application/Commands/CalculatePayrollCommand` | Payroll |
| `Admin/Payroll/*.aspx.cs` | `HumanResources.Application/` | Payroll UI |

### CRM Service (Port 5005)

| Legacy File | New Location | Notes |
|---|---|---|
| `Class/CustomerService.cs` | `CRM.Application/Commands/UpsertCustomerCommand` | Customer CRUD |
| `Class/CustomerHelper.cs` | `CRM.Application/Services/` | Customer utilities |
| `Admin/CRM/GuestProfile.aspx.cs` | `CRM.Application/Queries/GetCustomerProfileQuery` | Profile |
| `Admin/CRM/LoyaltyDashboard.aspx.cs` | `CRM.Application/Queries/` | Loyalty program |
| `Admin/CRM/ReviewManagement.aspx.cs` | `CRM.Application/Commands/` | Reviews |
| `Account/TierBenefitsManagement.aspx.cs` | `CRM.Application/Commands/` | Tier config |
| `Admin/CustomerManagement.aspx.cs` | `CRM.Application/Queries/` | Customer list |

### Channel Manager Service (Port 5006)

| Legacy File | New Location | Notes |
|---|---|---|
| `Admin/ChannelManager/Dashboard.aspx.cs` | `ChannelManager.Application/` | OTA management |

### Guest Experience Service (Port 5007)

| Legacy File | New Location | Notes |
|---|---|---|
| `Guest/Portal.aspx.cs` | `GuestExperience.API/Controllers/PortalController` | Guest portal |
| `Guest/Dashboard.aspx.cs` | `GuestExperience.Application/Queries/` | Guest dashboard |
| `Guest/RoomService.aspx.cs` | `GuestExperience.Application/Commands/` | Room service orders |
| `Guest/Housekeeping.aspx.cs` | `GuestExperience.Application/Commands/` | Housekeeping requests |
| `Guest/Chat.aspx.cs` | `GuestExperience.Application/Commands/` | Guest chat |
| `Guest/Review.aspx.cs` | Cross-service → CRM | Review submission |
| `Guest/Payment/*.aspx.cs` | Cross-service → Payment | Guest payments |
| `Admin/Housekeeping/*.aspx.cs` | `GuestExperience.Application/` | Housekeeping admin |
| `Admin/RoomService/*.aspx.cs` | `GuestExperience.Application/` | Room service admin |
| `Admin/Maintenance/*.aspx.cs` | `GuestExperience.Application/` | Maintenance admin |
| `Admin/Chat/*.aspx.cs` | `GuestExperience.Application/` | Chat admin |

### Accounting Service (Port 5008)

| Legacy File | New Location | Notes |
|---|---|---|
| `Admin/Report/Report.aspx.cs` | `Accounting.Application/Queries/` | Reports |
| `Admin/Report/ProfitLoss.aspx.cs` | `Accounting.Application/Queries/GetProfitLossQuery` | P&L |
| `Admin/Report/CustomerAnalytics.aspx.cs` | Cross-service → CRM | Analytics |
| `Admin/WebAnalytics.aspx.cs` | `Accounting.Application/Queries/` | Web analytics |

### Notification Service (Port 5009)

| Legacy File | New Location | Notes |
|---|---|---|
| `Class/EmailService.cs` | `Notification.Infrastructure/Providers/EmailProvider` | Email |
| `Class/TelegramService.cs` | `Notification.Infrastructure/Providers/TelegramProvider` | Telegram |
| `Admin/Notifications/Settings.aspx.cs` | `Notification.Application/Commands/` | Settings |

### Affiliate Service (Port 5010)

| Legacy File | New Location | Notes |
|---|---|---|
| `Class/AffiliateService.cs` | `Affiliate.Application/` | Affiliate logic |
| `Affiliate/Default.aspx.cs` | `Affiliate.Application/Queries/` | Dashboard |
| `Affiliate/Register.aspx.cs` (40KB) | `Affiliate.Application/Commands/RegisterAffiliateCommand` | Registration |
| `Affiliate/Login.aspx.cs` | Cross-service → Identity | Authentication |
| `Admin/AffiliateManagement.aspx.cs` | `Affiliate.Application/Queries/` | Admin management |
| `Admin/PaymentAffiliate.aspx.cs` | `Affiliate.Application/Commands/` | Payouts |

### Shared/Core Libraries

| Legacy File | New Location | Notes |
|---|---|---|
| `Code.cs` (42KB) | `TakeTime.Infrastructure/Database/BaseDbContext` | DB access layer |
| `Class/ValidationHelper.cs` | `TakeTime.Core/Domain/ValueObjects/` | Value object validation |
| `Class/DocumentHelper.cs` | `TakeTime.Core/Extensions/` | Document utilities |
| `Class/AddressHelper.cs` | `TakeTime.Core/Domain/ValueObjects/Address` | Thai address |
| `Class/DatabaseHelper.cs` | `TakeTime.Infrastructure/Database/` | DB abstraction |
| `Class/LoggingService.cs` | `TakeTime.Infrastructure/Logging/` | Serilog replaces |
| `Class/SignatureService.cs` | `TakeTime.Infrastructure/ExternalServices/` | Signature |
| `Class/WebAnalyticsService.cs` | `TakeTime.Infrastructure/ExternalServices/` | Analytics |

---

## Step-by-Step Migration Process

### Step 1: Set Up New Infrastructure
```bash
# Clone and set up
git checkout claude/restructure-system-architecture-szeM0

# Start infrastructure
docker-compose -f docker-compose.dev.yml up -d sqlserver redis rabbitmq

# Run migrations
dotnet run --project src/Migrator/TakeTime.Migrator
```

### Step 2: Migrate Service by Service
For each service, follow this pattern:

1. **Extract domain logic** from .aspx.cs code-behind into Domain entities
2. **Create commands/queries** in Application layer
3. **Implement repositories** in Infrastructure layer
4. **Create API endpoints** in API layer
5. **Test the API** independently
6. **Wire up in API Gateway**

### Step 3: Run Both Systems in Parallel
During migration, both old (WebForms) and new (APIs) can run simultaneously:
- Old system continues serving web UI
- New APIs handle new integrations and mobile
- Gradually move UI to new frontend (React/Vue)

### Step 4: Onboard New Tenants
Once the new system is stable:
1. Create tenant record with business settings
2. Provision tenant database
3. Configure tenant-specific settings (VAT, pricing, etc.)
4. Set up tenant subdomain/URL
5. Create admin user for the tenant

---

## Key Configuration Differences Between Tenants

### Example: TakeTime BangPhra (VAT Registered)
```json
{
  "code": "taketime-bangphra",
  "businessSettings": {
    "tax": { "enableVAT": true, "vatRate": 7.0 },
    "pricing": { "defaultCurrency": "THB", "pricingModel": "PerNight" },
    "payment": { "depositPercentage": 50 },
    "features": { "enablePOS": true, "enableAffiliateProgram": true }
  }
}
```

### Example: Resort No-VAT (Non-Registered)
```json
{
  "code": "resort-xyz",
  "businessSettings": {
    "tax": { "enableVAT": false, "vatRate": 0 },
    "pricing": { "defaultCurrency": "THB", "pricingModel": "PerNight" },
    "payment": { "depositPercentage": 100 },
    "features": { "enablePOS": false, "enableAffiliateProgram": false }
  }
}
```

### Example: International Hotel (USD, 10% VAT)
```json
{
  "code": "hotel-abc",
  "businessSettings": {
    "tax": { "enableVAT": true, "vatRate": 10.0 },
    "pricing": { "defaultCurrency": "USD", "pricingModel": "PerNight" },
    "payment": { "acceptedMethods": ["CreditCard", "BankTransfer"] },
    "features": { "enableChannelManager": true }
  }
}
```
