# TakeTime Platform - Microservices Architecture

## Overview

TakeTime is a multi-tenant hospitality management platform built with microservices architecture.
It supports diverse business configurations (VAT/non-VAT, different pricing models, multi-channel)
and can be deployed as a single process (modular monolith) or as distributed microservices.

---

## Architecture Diagram

```
                                    ┌─────────────────────────┐
                                    │      Load Balancer       │
                                    │    (Nginx / Cloud LB)    │
                                    └────────────┬────────────┘
                                                 │
                                    ┌────────────▼────────────┐
                                    │      API Gateway         │
                                    │   (YARP Reverse Proxy)   │
                                    │  - Rate Limiting         │
                                    │  - Authentication        │
                                    │  - Tenant Resolution     │
                                    │  - Request Routing       │
                                    │  Port: 5000              │
                                    └────────────┬────────────┘
                                                 │
                    ┌────────────────────────────┼────────────────────────────┐
                    │                            │                            │
        ┌───────────▼──────────┐   ┌────────────▼───────────┐  ┌────────────▼──────────┐
        │  Reservation Service │   │   Payment Service      │  │  Inventory Service    │
        │  Port: 5001          │   │   Port: 5002           │  │  Port: 5003           │
        │  - Booking           │   │   - Payment Processing │  │  - Products/POS       │
        │  - Check-in/out      │   │   - Receipts           │  │  - Stock Management   │
        │  - Room Management   │   │   - Tax Invoices       │  │  - Sales Transactions │
        │  - Availability      │   │   - VAT Calculation    │  │  - Room Charges       │
        └──────────┬───────────┘   └───────────┬────────────┘  └───────────┬────────────┘
                   │                           │                           │
        ┌──────────▼───────────┐   ┌───────────▼────────────┐  ┌──────────▼────────────┐
        │  HR Service          │   │   CRM Service          │  │  Channel Manager      │
        │  Port: 5004          │   │   Port: 5005           │  │  Port: 5006           │
        │  - Employees         │   │   - Customers          │  │  - OTA Integration    │
        │  - Leave Management  │   │   - Loyalty Program    │  │  - Agoda/Booking.com  │
        │  - Payroll           │   │   - Reviews            │  │  - Rate Sync          │
        │  - Overtime          │   │   - Analytics          │  │  - Inventory Sync     │
        └──────────┬───────────┘   └───────────┬────────────┘  └───────────┬────────────┘
                   │                           │                           │
        ┌──────────▼───────────┐   ┌───────────▼────────────┐  ┌──────────▼────────────┐
        │  Guest Experience    │   │   Accounting Service   │  │  Notification Service │
        │  Port: 5007          │   │   Port: 5008           │  │  Port: 5009           │
        │  - Guest Portal      │   │   - Financial Reports  │  │  - Email (SMTP)       │
        │  - Housekeeping      │   │   - Revenue Tracking   │  │  - LINE Notify        │
        │  - Room Service      │   │   - P&L Analysis       │  │  - Telegram           │
        │  - Maintenance       │   │   - Tax Management     │  │  - SMS                │
        │  - Chat              │   │   - Daily Summaries    │  │  - Push Notifications  │
        └──────────────────────┘   └────────────────────────┘  └────────────────────────┘
                                                                          │
        ┌──────────────────────┐                               ┌──────────▼────────────┐
        │  Affiliate Service   │                               │  Identity Service     │
        │  Port: 5010          │                               │  (Shared Library)     │
        │  - Partners          │                               │  - JWT Authentication │
        │  - Commissions       │                               │  - Role-Based Auth    │
        │  - Payouts           │                               │  - Permission System  │
        └──────────────────────┘                               └────────────────────────┘

        ┌──────────────────────────────────────────────────────────────────────────────┐
        │                         Shared Infrastructure                                │
        │  ┌─────────────┐   ┌──────────────┐   ┌──────────┐   ┌───────────────────┐  │
        │  │  SQL Server  │   │    Redis     │   │ RabbitMQ │   │  Blob Storage     │  │
        │  │  (per-tenant │   │   (Cache)    │   │  (Event  │   │  (Files/Images)   │  │
        │  │   databases) │   │              │   │   Bus)   │   │                   │  │
        │  └─────────────┘   └──────────────┘   └──────────┘   └───────────────────┘  │
        └──────────────────────────────────────────────────────────────────────────────┘
```

---

## Bounded Contexts (Microservices)

### 1. Reservation Service
**Responsibility:** Managing the complete booking lifecycle

| Capability | Description |
|---|---|
| Booking | Create, modify, cancel reservations |
| Room Management | Accommodation CRUD, availability tracking |
| Check-in/Check-out | Guest arrival and departure processing |
| Pricing | Dynamic pricing, seasonal rates, tenant-specific rules |
| Rental Items | Equipment and add-on rentals |

**Key Domain Events:**
- `ReservationCreated` → triggers notification, CRM update
- `ReservationConfirmed` → triggers payment request
- `GuestCheckedIn` → triggers housekeeping, guest portal activation
- `GuestCheckedOut` → triggers final billing, review request

### 2. Payment Service
**Responsibility:** All financial transactions and document generation

| Capability | Description |
|---|---|
| Payment Processing | Accept and verify payments (bank transfer, cash, QR, etc.) |
| Receipt Generation | Receipts with/without VAT based on tenant config |
| Tax Invoices | Full tax invoice generation for VAT-registered businesses |
| Refunds | Full and partial refund processing |
| Slip Verification | Payment slip upload and verification |

**Multi-Tenant VAT Support:**
```
Tenant A (VAT Registered):
  SubTotal: 1,000.00 THB
  VAT 7%:      70.00 THB
  Total:    1,070.00 THB

Tenant B (Non-VAT):
  Total:    1,000.00 THB
  (No VAT breakdown)
```

### 3. Inventory Service
**Responsibility:** Product management and point-of-sale operations

| Capability | Description |
|---|---|
| Product Catalog | Product CRUD with barcode, pricing, categories |
| Stock Management | Stock-in, stock-out, adjustments, low-stock alerts |
| POS | Point-of-sale transactions with room charge option |
| Sales Reports | Transaction history and sales analytics |

### 4. Human Resources Service
**Responsibility:** Complete HR management

| Capability | Description |
|---|---|
| Employee Management | Profiles, documents, contracts |
| Leave Management | Requests, approvals, replacement tracking |
| Overtime | OT entry, calculation, approval |
| Payroll | Salary calculation, tax deduction, social security |

### 5. CRM Service
**Responsibility:** Customer relationship management

| Capability | Description |
|---|---|
| Customer Profiles | Complete guest profiles with history |
| Loyalty Program | Multi-tier loyalty with points (configurable per tenant) |
| Reviews | Guest review collection and management |
| Analytics | Customer segmentation and behavior analysis |

### 6. Channel Manager Service
**Responsibility:** OTA integration and distribution

| Capability | Description |
|---|---|
| OTA Connections | Agoda, Booking.com, Expedia integration |
| Rate Management | Rate parity and distribution |
| Inventory Sync | Room availability synchronization |
| Reservation Import | Import bookings from OTA channels |

### 7. Guest Experience Service
**Responsibility:** In-stay guest services

| Capability | Description |
|---|---|
| Guest Portal | Self-service guest dashboard |
| Housekeeping | Task management and room status |
| Room Service | Order management with room charging |
| Maintenance | Maintenance request tracking |
| Chat | Guest-staff communication |

### 8. Accounting Service
**Responsibility:** Financial reporting and analysis

| Capability | Description |
|---|---|
| Revenue Tracking | Daily/monthly revenue summaries |
| P&L Reports | Profit and loss analysis |
| Tax Management | Tax reporting and compliance |
| Financial Analytics | KPIs: RevPAR, ADR, occupancy rate |

### 9. Notification Service
**Responsibility:** Multi-channel communication

| Capability | Description |
|---|---|
| Email | SMTP-based email with templates |
| LINE | LINE Notify and Messaging API |
| Telegram | Telegram bot notifications |
| SMS | SMS messaging |
| Templates | Configurable notification templates per tenant |

### 10. Affiliate Service
**Responsibility:** Affiliate partner management

| Capability | Description |
|---|---|
| Partner Management | Registration, verification, profiles |
| Commission Tracking | Commission calculation per booking |
| Payouts | Commission payment processing |
| Analytics | Affiliate performance reports |

---

## Multi-Tenancy Architecture

### Tenant Isolation Strategy

```
┌──────────────────────────────────────────────────────────┐
│                  Tenant Management DB                     │
│  ┌──────────────────────────────────────────────────┐    │
│  │ Tenants Table                                     │    │
│  │ - Id, Code, Name, ConnectionString               │    │
│  │ - BusinessSettings (JSON)                         │    │
│  │ - IsActive, SubscriptionPlan                      │    │
│  └──────────────────────────────────────────────────┘    │
└──────────────────────────────────────────────────────────┘
         │                    │                    │
         ▼                    ▼                    ▼
┌────────────────┐  ┌────────────────┐  ┌────────────────┐
│  Tenant A DB   │  │  Tenant B DB   │  │  Tenant C DB   │
│  (TakeTime     │  │  (Resort XYZ)  │  │  (Hotel ABC)   │
│   BangPhra)    │  │                │  │                │
│  VAT: 7%       │  │  VAT: None     │  │  VAT: 10%      │
│  THB Currency  │  │  THB Currency  │  │  USD Currency  │
└────────────────┘  └────────────────┘  └────────────────┘
```

### Tenant Resolution Flow

```
HTTP Request
    │
    ▼
┌─────────────────────────────┐
│ 1. Check X-Tenant-Id Header │ ← API clients
├─────────────────────────────┤
│ 2. Check Subdomain          │ ← Web browsers (bangphra.taketime.com)
├─────────────────────────────┤
│ 3. Check JWT Claims         │ ← Authenticated users
├─────────────────────────────┤
│ 4. Check Query String       │ ← Fallback (?tenantId=xxx)
└─────────────────────────────┘
    │
    ▼
┌─────────────────────────────┐
│ Load Tenant Configuration   │
│ (cached in Redis/Memory)    │
└─────────────────────────────┘
    │
    ▼
┌─────────────────────────────┐
│ Set DbContext Connection    │
│ String for this request     │
└─────────────────────────────┘
```

### Configurable Business Settings Per Tenant

```json
{
  "tax": {
    "enableVAT": true,
    "vatRate": 7.0,
    "taxId": "0105556123456",
    "vatRegistrationNumber": "VAT-001"
  },
  "pricing": {
    "defaultCurrency": "THB",
    "pricingModel": "PerNight",
    "enableDynamicPricing": true,
    "enableSeasonalPricing": true
  },
  "payment": {
    "acceptedMethods": ["BankTransfer", "Cash", "QRCode", "PromptPay"],
    "enablePartialPayment": true,
    "depositPercentage": 50,
    "bankAccounts": [
      {
        "bankName": "Bangkok Bank",
        "accountNumber": "xxx-x-xxxxx-x",
        "accountName": "Take Time BangPhra"
      }
    ]
  },
  "reservation": {
    "defaultCheckInTime": "14:00",
    "defaultCheckOutTime": "12:00",
    "maxAdvanceBookingDays": 365,
    "cancellationPolicyHours": 48,
    "enableAutoConfirmation": false
  },
  "loyalty": {
    "enabled": true,
    "pointsPerCurrencyUnit": 1,
    "tiers": [
      { "name": "Member", "minPoints": 0, "discount": 0 },
      { "name": "Silver", "minPoints": 1000, "discount": 5 },
      { "name": "Gold", "minPoints": 5000, "discount": 10 },
      { "name": "Platinum", "minPoints": 15000, "discount": 15 },
      { "name": "VIP", "minPoints": 50000, "discount": 20 }
    ]
  },
  "features": {
    "enableAffiliateProgram": true,
    "enableChannelManager": true,
    "enablePOS": true,
    "enableRoomService": true,
    "enableHousekeeping": true
  }
}
```

---

## Clean Architecture (Per Service)

Each microservice follows Clean Architecture with 4 layers:

```
┌────────────────────────────────────────────────────┐
│                    API Layer                         │
│  Controllers, Middleware, Program.cs                 │
│  Depends on: Application, Infrastructure            │
├────────────────────────────────────────────────────┤
│                Application Layer                     │
│  Commands, Queries, DTOs, Validators, Services      │
│  Depends on: Domain                                  │
├────────────────────────────────────────────────────┤
│              Infrastructure Layer                    │
│  DbContext, Repositories, External Services         │
│  Depends on: Domain, Application                     │
├────────────────────────────────────────────────────┤
│                 Domain Layer                         │
│  Entities, Value Objects, Interfaces, Events        │
│  Depends on: TakeTime.Core (shared)                  │
│  NO external dependencies                            │
└────────────────────────────────────────────────────┘
```

### Dependency Rule
- Inner layers NEVER depend on outer layers
- Domain layer has ZERO framework dependencies
- All dependencies point inward

---

## CQRS Pattern

Commands and Queries are separated using MediatR:

```csharp
// Command (Write)
public record CreateReservationCommand(
    string CustomerName,
    DateTime CheckInDate,
    DateTime CheckOutDate,
    List<Guid> AccommodationIds
) : IRequest<ReservationDto>;

// Query (Read)
public record SearchReservationsQuery(
    string? CustomerName,
    DateTime? FromDate,
    DateTime? ToDate,
    ReservationStatus? Status,
    int Page = 1,
    int PageSize = 20
) : IRequest<PagedResult<ReservationSummaryDto>>;
```

### MediatR Pipeline Behaviors

```
Request → ValidationBehavior → TenantBehavior → LoggingBehavior → Handler
```

1. **ValidationBehavior** - FluentValidation rules before handler
2. **TenantBehavior** - Ensures tenant context is set
3. **LoggingBehavior** - Structured logging with correlation IDs

---

## Inter-Service Communication

### Synchronous (HTTP)
- Service-to-service REST calls via HttpClient
- Used for real-time data needs (e.g., check availability)

### Asynchronous (Event Bus)
- Domain events published via RabbitMQ/MassTransit
- Used for eventual consistency between services

```
Reservation Service                    Payment Service
    │                                       │
    │  ReservationConfirmed Event           │
    ├──────────────────────────────────────►│
    │                                       │
    │                                       │  PaymentVerified Event
    │◄──────────────────────────────────────┤
    │                                       │
    │  Update reservation status            │
    │                                       │

Reservation Service                    Notification Service
    │                                       │
    │  GuestCheckedIn Event                 │
    ├──────────────────────────────────────►│
    │                                       │  Send welcome email
    │                                       │  Send LINE notification
    │                                       │

Reservation Service                    CRM Service
    │                                       │
    │  GuestCheckedOut Event                │
    ├──────────────────────────────────────►│
    │                                       │  Update loyalty points
    │                                       │  Update visit history
    │                                       │  Request review
```

---

## Deployment Strategies

### Strategy 1: Modular Monolith (Recommended for Start)

Deploy all services in a single process. Uses in-memory event bus.
Easiest to develop, test, and deploy. Can be split later.

```
┌──────────────────────────────────────┐
│           Single Process              │
│  ┌──────────┐  ┌──────────────────┐  │
│  │ API      │  │ All Services     │  │
│  │ Gateway  │  │ (in-process)     │  │
│  └──────────┘  └──────────────────┘  │
│  ┌──────────────────────────────────┐│
│  │ Shared Database (multi-tenant)   ││
│  └──────────────────────────────────┘│
└──────────────────────────────────────┘
```

### Strategy 2: Hybrid (Medium Scale)

Split high-traffic services into separate processes.

```
┌───────────────┐  ┌──────────────────┐  ┌─────────────────┐
│ API Gateway   │  │ Reservation +    │  │ Notification    │
│               │  │ Payment +        │  │ Service         │
│               │  │ CRM (combined)   │  │ (separate)      │
└───────────────┘  └──────────────────┘  └─────────────────┘
```

### Strategy 3: Full Microservices (Large Scale)

Each service in its own container with own database.

```
Docker Compose / Kubernetes
├── api-gateway (1-3 replicas)
├── reservation-api (2-5 replicas)
├── payment-api (2-3 replicas)
├── inventory-api (1-2 replicas)
├── hr-api (1 replica)
├── crm-api (1-2 replicas)
├── channel-manager-api (1 replica)
├── guest-experience-api (1-2 replicas)
├── accounting-api (1 replica)
├── notification-api (2-3 replicas)
├── affiliate-api (1 replica)
├── sqlserver
├── redis
└── rabbitmq
```

---

## Project Structure

```
TakeTime/
├── TakeTime.Microservices.sln          # New microservices solution
├── Take Time BangPhra.sln              # Legacy WebForms solution (to migrate from)
│
├── src/
│   ├── Core/
│   │   └── TakeTime.Core/             # Shared domain base classes, interfaces
│   │       ├── Domain/
│   │       │   ├── Base/              # Entity, AggregateRoot, ValueObject
│   │       │   ├── Interfaces/        # IRepository, IUnitOfWork, IDomainEvent
│   │       │   ├── ValueObjects/      # Money, DateRange, PhoneNumber, Address, Email
│   │       │   └── Events/            # DomainEvent base class
│   │       ├── Application/
│   │       │   ├── Interfaces/        # ICurrentTenantService, ICurrentUserService
│   │       │   └── Behaviors/         # Validation, Logging, Tenant behaviors
│   │       ├── Exceptions/            # DomainException, NotFoundException, etc.
│   │       ├── Constants/             # Roles, Permissions
│   │       └── Extensions/            # DI extensions
│   │
│   ├── MultiTenancy/
│   │   └── TakeTime.MultiTenancy/     # Multi-tenant infrastructure
│   │       ├── Core/                  # Tenant entity, business settings
│   │       ├── Resolution/            # Header, Subdomain, Claims strategies
│   │       ├── Configuration/         # EF DbContext, tenant store
│   │       ├── Middleware/            # Tenant resolution middleware
│   │       └── Services/             # Tenant management services
│   │
│   ├── Identity/
│   │   └── TakeTime.Identity/         # Authentication & authorization
│   │       ├── Domain/               # User, Role entities
│   │       ├── Services/             # Auth, JWT, Password services
│   │       └── Middleware/           # JWT middleware
│   │
│   ├── Infrastructure/
│   │   └── TakeTime.Infrastructure/   # Shared infrastructure
│   │       ├── Database/             # BaseDbContext, BaseRepository, UnitOfWork
│   │       ├── Messaging/            # Event bus (InMemory, MassTransit)
│   │       ├── Caching/              # Memory cache, Redis cache
│   │       ├── Logging/              # Serilog configuration
│   │       └── ExternalServices/     # Email, LINE, Telegram
│   │
│   ├── Services/
│   │   ├── Reservation/
│   │   │   ├── TakeTime.Reservation.Domain/
│   │   │   ├── TakeTime.Reservation.Application/
│   │   │   ├── TakeTime.Reservation.Infrastructure/
│   │   │   └── TakeTime.Reservation.API/
│   │   │
│   │   ├── Payment/
│   │   │   ├── TakeTime.Payment.Domain/
│   │   │   ├── TakeTime.Payment.Application/
│   │   │   ├── TakeTime.Payment.Infrastructure/
│   │   │   └── TakeTime.Payment.API/
│   │   │
│   │   ├── Inventory/          # Same 4-layer structure
│   │   ├── HumanResources/     # Same 4-layer structure
│   │   ├── CRM/                # Same 4-layer structure
│   │   ├── ChannelManager/     # Same 4-layer structure
│   │   ├── GuestExperience/    # Same 4-layer structure
│   │   ├── Accounting/         # Same 4-layer structure
│   │   ├── Notification/       # Same 4-layer structure
│   │   └── Affiliate/          # Same 4-layer structure
│   │
│   ├── Gateway/
│   │   └── TakeTime.ApiGateway/       # YARP-based API Gateway
│   │
│   └── Migrator/
│       └── TakeTime.Migrator/         # Database migrations & seeding
│
├── docker-compose.yml                  # Full stack deployment
├── docker-compose.dev.yml              # Development overrides
└── ARCHITECTURE.md                     # This document
```

---

## Migration Path (Legacy → Microservices)

### Phase 1: Foundation
- [x] Create microservices solution structure
- [x] Implement Core libraries (Entity, ValueObject, Repository)
- [x] Implement Multi-tenancy system
- [x] Implement Identity/Auth system
- [x] Create shared Infrastructure (DB, Cache, Messaging)

### Phase 2: Core Services
- [ ] Migrate Reservation logic from Reserve.aspx.cs → Reservation Service
- [ ] Migrate Payment logic from PaymentService.cs → Payment Service
- [ ] Migrate Product/POS from Product/Default.aspx.cs → Inventory Service
- [ ] Set up API Gateway routing

### Phase 3: Supporting Services
- [ ] Migrate HR from EmployeeService.cs → HR Service
- [ ] Migrate CRM from CustomerService.cs → CRM Service
- [ ] Migrate Channel Manager → Channel Manager Service
- [ ] Migrate Guest features → Guest Experience Service

### Phase 4: Analytics & Integration
- [ ] Migrate Accounting/Reports → Accounting Service
- [ ] Migrate Notifications → Notification Service
- [ ] Migrate Affiliate → Affiliate Service
- [ ] Full API Gateway configuration

### Phase 5: Multi-Tenant Onboarding
- [ ] Tenant provisioning workflow
- [ ] Tenant admin panel
- [ ] Self-service tenant registration
- [ ] Billing/subscription management

---

## Technology Stack

| Layer | Technology |
|---|---|
| **Runtime** | .NET 8 (LTS) |
| **API** | ASP.NET Core Web API |
| **API Gateway** | YARP (Yet Another Reverse Proxy) |
| **ORM** | Entity Framework Core 8 |
| **CQRS** | MediatR |
| **Validation** | FluentValidation |
| **Mapping** | AutoMapper |
| **Database** | SQL Server 2019+ / PostgreSQL 15+ |
| **Cache** | Redis / In-Memory |
| **Message Bus** | RabbitMQ + MassTransit |
| **Authentication** | JWT Bearer Tokens |
| **Logging** | Serilog (structured logging) |
| **Containerization** | Docker + Docker Compose |
| **Orchestration** | Kubernetes (future) |
| **CI/CD** | GitHub Actions |

---

## Key Design Decisions

### 1. Database-per-Tenant Strategy
Each tenant gets its own database for maximum isolation. Shared tenant management database stores tenant metadata and configuration.

**Rationale:** Strongest data isolation, independent scaling, easy tenant onboarding/offboarding, compliance-friendly.

### 2. CQRS without Event Sourcing
Using CQRS pattern (separate commands/queries) but with traditional state-based persistence. Event sourcing can be added later if needed.

**Rationale:** Simpler to implement and debug. Most hospitality operations don't require event replay capabilities.

### 3. Modular Monolith First
The architecture supports both monolith and microservice deployment. Start as modular monolith, split when scale demands it.

**Rationale:** Avoid distributed system complexity until necessary. Service boundaries are clearly defined for easy extraction.

### 4. Multi-Strategy Tenant Resolution
Support multiple tenant resolution strategies (header, subdomain, JWT claims, query string) to accommodate different client types.

**Rationale:** API clients use headers, web browsers use subdomains, authenticated users carry tenant in JWT.

### 5. Configurable Business Rules
All business-specific rules (VAT, pricing, payment methods, etc.) are stored in tenant configuration, not hard-coded.

**Rationale:** Different businesses have different requirements. New tenants can be onboarded by configuring settings, not writing code.
