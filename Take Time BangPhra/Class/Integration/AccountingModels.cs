using System;
using System.Collections.Generic;

namespace Take_Time_BangPhra.Integration
{
    // ──────────────────────────────────────────────
    // Authentication
    // ──────────────────────────────────────────────

    public class AuthLoginRequest
    {
        public string email { get; set; }
        public string password { get; set; }
    }

    public class AuthLoginResponse
    {
        public string token { get; set; }
        public string refreshToken { get; set; }
        public DateTime? expiresAt { get; set; }
    }

    public class AuthRefreshRequest
    {
        public string refreshToken { get; set; }
    }

    // ──────────────────────────────────────────────
    // Generic API Response Wrapper
    // ──────────────────────────────────────────────

    public class ApiResponse<T>
    {
        public bool success { get; set; }
        public string message { get; set; }
        public T data { get; set; }
        public List<string> errors { get; set; }
    }

    // ──────────────────────────────────────────────
    // Journal Entries
    // ──────────────────────────────────────────────

    public class CreateJournalEntryRequest
    {
        public DateTime entryDate { get; set; }
        public int journalType { get; set; }  // 0=General, 1=Sales, 2=Purchase, 3=CashReceipts, 4=CashPayments
        public string description { get; set; }
        public string reference { get; set; }
        public List<JournalEntryLineRequest> lines { get; set; }
    }

    public class JournalEntryLineRequest
    {
        public Guid accountId { get; set; }
        public decimal debitAmount { get; set; }
        public decimal creditAmount { get; set; }
        public string description { get; set; }
        public int lineOrder { get; set; }
    }

    public class JournalEntryResponse
    {
        public Guid id { get; set; }
        public string entryNumber { get; set; }
        public DateTime entryDate { get; set; }
        public int journalType { get; set; }
        public string description { get; set; }
        public string reference { get; set; }
        public int status { get; set; }  // 0=Draft, 1=Posted, 2=Voided
        public decimal totalDebit { get; set; }
        public decimal totalCredit { get; set; }
        public List<JournalEntryLineResponse> lines { get; set; }
    }

    public class JournalEntryLineResponse
    {
        public Guid id { get; set; }
        public Guid accountId { get; set; }
        public decimal debitAmount { get; set; }
        public decimal creditAmount { get; set; }
        public string description { get; set; }
    }

    // ──────────────────────────────────────────────
    // Chart of Accounts
    // ──────────────────────────────────────────────

    public class AccountResponse
    {
        public Guid id { get; set; }
        public string accountCode { get; set; }
        public string accountName { get; set; }
        public string accountNameEn { get; set; }
        public int accountType { get; set; }  // 1=Asset, 2=Liability, 3=Equity, 4=Revenue, 5=Expense
        public bool isActive { get; set; }
    }

    // ──────────────────────────────────────────────
    // Documents (Invoice, Receipt, CreditNote, etc.)
    // ──────────────────────────────────────────────

    public class CreateDocumentRequest
    {
        public int documentType { get; set; }
        public Guid? contactId { get; set; }
        public DateTime documentDate { get; set; }
        public DateTime? dueDate { get; set; }
        public string reference { get; set; }
        public string notes { get; set; }
        public List<DocumentLineRequest> lines { get; set; }
    }

    public class DocumentLineRequest
    {
        public string description { get; set; }
        public decimal quantity { get; set; }
        public decimal unitPrice { get; set; }
        public decimal? discountPercent { get; set; }
        public decimal? vatPercent { get; set; }
        public Guid? accountId { get; set; }
        public Guid? productId { get; set; }
    }

    public class DocumentResponse
    {
        public Guid id { get; set; }
        public string documentNumber { get; set; }
        public int documentType { get; set; }
        public int status { get; set; }
        public decimal totalAmount { get; set; }
        public decimal vatAmount { get; set; }
        public decimal grandTotal { get; set; }
    }

    // ──────────────────────────────────────────────
    // Contacts (Customers / Suppliers)
    // ──────────────────────────────────────────────

    public class CreateContactRequest
    {
        public string name { get; set; }
        public string taxId { get; set; }
        public string email { get; set; }
        public string phone { get; set; }
        public string address { get; set; }
        public bool isCustomer { get; set; }
        public bool isSupplier { get; set; }
    }

    public class UpdateContactRequest
    {
        public string name { get; set; }
        public string taxId { get; set; }
        public string email { get; set; }
        public string phone { get; set; }
        public string address { get; set; }
    }

    public class ContactResponse
    {
        public Guid id { get; set; }
        public string name { get; set; }
        public string taxId { get; set; }
        public string email { get; set; }
        public string phone { get; set; }
        public bool isCustomer { get; set; }
        public bool isSupplier { get; set; }
    }

    // ──────────────────────────────────────────────
    // Payments
    // ──────────────────────────────────────────────

    public class CreatePaymentRequest
    {
        public Guid documentId { get; set; }
        public DateTime paymentDate { get; set; }
        public decimal amount { get; set; }
        public string paymentMethod { get; set; }
        public string reference { get; set; }
        public Guid? bankAccountId { get; set; }
    }

    public class PaymentResponse
    {
        public Guid id { get; set; }
        public Guid documentId { get; set; }
        public decimal amount { get; set; }
        public string paymentMethod { get; set; }
        public DateTime paymentDate { get; set; }
    }

    // ──────────────────────────────────────────────
    // Products
    // ──────────────────────────────────────────────

    public class CreateProductRequest
    {
        public string code { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public decimal sellingPrice { get; set; }
        public decimal costPrice { get; set; }
        public string unit { get; set; }
        public Guid? categoryId { get; set; }
        public Guid? salesAccountId { get; set; }
        public Guid? purchaseAccountId { get; set; }
        public Guid? inventoryAccountId { get; set; }
        public bool trackInventory { get; set; }
    }

    public class UpdateProductRequest
    {
        public string name { get; set; }
        public string description { get; set; }
        public decimal sellingPrice { get; set; }
        public decimal costPrice { get; set; }
        public string unit { get; set; }
    }

    public class ProductResponse
    {
        public Guid id { get; set; }
        public string code { get; set; }
        public string name { get; set; }
        public decimal sellingPrice { get; set; }
        public decimal costPrice { get; set; }
        public string unit { get; set; }
        public decimal currentStock { get; set; }
    }

    // ──────────────────────────────────────────────
    // Stock
    // ──────────────────────────────────────────────

    public class StockAdjustmentRequest
    {
        public Guid productId { get; set; }
        public decimal quantity { get; set; }
        public string reason { get; set; }
        public string reference { get; set; }
    }

    public class StockMovementResponse
    {
        public Guid id { get; set; }
        public Guid productId { get; set; }
        public decimal quantity { get; set; }
        public string reason { get; set; }
        public DateTime createdDate { get; set; }
    }

    // ──────────────────────────────────────────────
    // Enums (matching Nexaacc)
    // ──────────────────────────────────────────────

    public static class NexaaccJournalType
    {
        public const int General = 0;
        public const int Sales = 1;
        public const int Purchase = 2;
        public const int CashReceipts = 3;
        public const int CashPayments = 4;
    }

    public static class NexaaccAccountType
    {
        public const int Asset = 1;
        public const int Liability = 2;
        public const int Equity = 3;
        public const int Revenue = 4;
        public const int Expense = 5;
    }

    public static class NexaaccDocumentType
    {
        public const int Quotation = 0;
        public const int Invoice = 1;
        public const int Receipt = 2;
        public const int TaxInvoice = 3;
        public const int DebitNote = 4;
        public const int CreditNote = 5;
    }

    public static class NexaaccDocumentStatus
    {
        public const int Draft = 0;
        public const int Approved = 2;
        public const int Paid = 5;
        public const int Voided = 6;
    }
}
