using Microsoft.Extensions.Logging;
using TakeTime.Payment.Domain.Entities;

namespace TakeTime.Payment.Infrastructure.ExternalServices;

/// <summary>
/// Interface for PDF document generation.
/// </summary>
public interface IPdfGenerationService
{
    Task<byte[]> GenerateReceiptPdfAsync(Receipt receipt, CancellationToken cancellationToken = default);
    Task<byte[]> GenerateTaxInvoicePdfAsync(TaxInvoice taxInvoice, CancellationToken cancellationToken = default);
}

/// <summary>
/// PDF generation service for receipts and tax invoices.
/// Uses a simple HTML-to-PDF approach; in production, QuestPDF or similar
/// library would be used for pixel-perfect layout control.
/// </summary>
public class PdfGenerationService : IPdfGenerationService
{
    private readonly ILogger<PdfGenerationService> _logger;

    public PdfGenerationService(ILogger<PdfGenerationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Generates a PDF receipt document.
    /// </summary>
    public async Task<byte[]> GenerateReceiptPdfAsync(
        Receipt receipt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        _logger.LogInformation("Generating PDF for receipt {ReceiptNumber}", receipt.ReceiptNumber);

        try
        {
            var html = BuildReceiptHtml(receipt);
            var pdfBytes = await ConvertHtmlToPdfAsync(html, cancellationToken);

            _logger.LogInformation(
                "PDF generated successfully for receipt {ReceiptNumber}, size: {Size} bytes",
                receipt.ReceiptNumber, pdfBytes.Length);

            return pdfBytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate PDF for receipt {ReceiptNumber}", receipt.ReceiptNumber);
            throw;
        }
    }

    /// <summary>
    /// Generates a PDF tax invoice document.
    /// Complies with Thai Revenue Department requirements for tax invoices.
    /// </summary>
    public async Task<byte[]> GenerateTaxInvoicePdfAsync(
        TaxInvoice taxInvoice, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(taxInvoice);

        _logger.LogInformation("Generating PDF for tax invoice {InvoiceNumber}", taxInvoice.InvoiceNumber);

        try
        {
            var html = BuildTaxInvoiceHtml(taxInvoice);
            var pdfBytes = await ConvertHtmlToPdfAsync(html, cancellationToken);

            _logger.LogInformation(
                "PDF generated successfully for tax invoice {InvoiceNumber}, size: {Size} bytes",
                taxInvoice.InvoiceNumber, pdfBytes.Length);

            return pdfBytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate PDF for tax invoice {InvoiceNumber}", taxInvoice.InvoiceNumber);
            throw;
        }
    }

    private static string BuildReceiptHtml(Receipt receipt)
    {
        var itemRows = string.Join("\n", receipt.Items.Select(item => $"""
            <tr>
                <td style="padding: 6px; border: 1px solid #ddd;">{item.Description}</td>
                <td style="padding: 6px; border: 1px solid #ddd; text-align: center;">{item.Quantity}</td>
                <td style="padding: 6px; border: 1px solid #ddd; text-align: right;">{item.UnitPrice.Amount:N2}</td>
                <td style="padding: 6px; border: 1px solid #ddd; text-align: right;">{item.Amount.Amount:N2}</td>
            </tr>
            """));

        return $"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="utf-8">
                <style>
                    body {{ font-family: 'Sarabun', Arial, sans-serif; font-size: 12px; color: #333; margin: 40px; }}
                    h1 {{ font-size: 24px; margin-bottom: 5px; }}
                    h2 {{ font-size: 18px; color: #555; margin-top: 0; }}
                    table {{ border-collapse: collapse; width: 100%; margin-top: 20px; }}
                    th {{ background-color: #f5f5f5; padding: 8px; border: 1px solid #ddd; text-align: left; }}
                    .total-row {{ font-weight: bold; background-color: #f9f9f9; }}
                    .header {{ display: flex; justify-content: space-between; border-bottom: 2px solid #333; padding-bottom: 10px; }}
                    .receipt-info {{ text-align: right; }}
                </style>
            </head>
            <body>
                <div class="header">
                    <div>
                        <h1>Receipt / ใบเสร็จรับเงิน</h1>
                        <h2>{receipt.ReceiptType}</h2>
                    </div>
                    <div class="receipt-info">
                        <p><strong>Receipt No:</strong> {receipt.ReceiptNumber}</p>
                        <p><strong>Date:</strong> {receipt.IssuedAt:dd/MM/yyyy}</p>
                        <p><strong>Issued by:</strong> {receipt.IssuedBy}</p>
                    </div>
                </div>

                <div style="margin-top: 20px;">
                    <p><strong>Customer:</strong> {receipt.CustomerId}</p>
                    <p><strong>Reservation:</strong> {receipt.ReservationId}</p>
                </div>

                <table>
                    <thead>
                        <tr>
                            <th>Description</th>
                            <th style="text-align: center; width: 80px;">Qty</th>
                            <th style="text-align: right; width: 120px;">Unit Price</th>
                            <th style="text-align: right; width: 120px;">Amount</th>
                        </tr>
                    </thead>
                    <tbody>
                        {itemRows}
                    </tbody>
                    <tfoot>
                        <tr>
                            <td colspan="3" style="padding: 6px; border: 1px solid #ddd; text-align: right;"><strong>Subtotal</strong></td>
                            <td style="padding: 6px; border: 1px solid #ddd; text-align: right;">{receipt.SubTotal.Amount:N2}</td>
                        </tr>
                        <tr>
                            <td colspan="3" style="padding: 6px; border: 1px solid #ddd; text-align: right;"><strong>VAT ({receipt.VATRate}%)</strong></td>
                            <td style="padding: 6px; border: 1px solid #ddd; text-align: right;">{receipt.VATAmount.Amount:N2}</td>
                        </tr>
                        <tr class="total-row">
                            <td colspan="3" style="padding: 8px; border: 1px solid #ddd; text-align: right; font-size: 14px;"><strong>Total / รวมทั้งสิ้น</strong></td>
                            <td style="padding: 8px; border: 1px solid #ddd; text-align: right; font-size: 14px;"><strong>{receipt.TotalAmount.Amount:N2} {receipt.TotalAmount.Currency}</strong></td>
                        </tr>
                    </tfoot>
                </table>

                {(receipt.IsVoided ? "<div style='color: red; font-size: 18px; text-align: center; margin-top: 30px; border: 2px solid red; padding: 10px;'>VOIDED</div>" : "")}

                <div style="margin-top: 40px; font-size: 10px; color: #999; text-align: center;">
                    <p>This is a computer-generated document. No signature required.</p>
                </div>
            </body>
            </html>
            """;
    }

    private static string BuildTaxInvoiceHtml(TaxInvoice invoice)
    {
        var itemRows = string.Join("\n", invoice.Items.Select((item, index) => $"""
            <tr>
                <td style="padding: 6px; border: 1px solid #ddd; text-align: center;">{index + 1}</td>
                <td style="padding: 6px; border: 1px solid #ddd;">{item.Description}</td>
                <td style="padding: 6px; border: 1px solid #ddd; text-align: center;">{item.Quantity}</td>
                <td style="padding: 6px; border: 1px solid #ddd; text-align: right;">{item.UnitPrice.Amount:N2}</td>
                <td style="padding: 6px; border: 1px solid #ddd; text-align: right;">{item.Amount.Amount:N2}</td>
            </tr>
            """));

        return $"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="utf-8">
                <style>
                    body {{ font-family: 'Sarabun', Arial, sans-serif; font-size: 11px; color: #333; margin: 30px; }}
                    h1 {{ font-size: 20px; text-align: center; }}
                    table {{ border-collapse: collapse; width: 100%; }}
                    th {{ background-color: #f5f5f5; padding: 6px; border: 1px solid #ddd; }}
                    .company-info {{ margin-bottom: 15px; }}
                    .two-column {{ display: flex; justify-content: space-between; }}
                </style>
            </head>
            <body>
                <h1>TAX INVOICE / ใบกำกับภาษี</h1>
                <p style="text-align: center; font-size: 10px;">Invoice No: {invoice.InvoiceNumber} | Date: {invoice.IssuedDate:dd/MM/yyyy}</p>

                <div class="two-column" style="margin-top: 20px;">
                    <div class="company-info" style="width: 48%;">
                        <p><strong>Seller / ผู้ขาย:</strong></p>
                        <p>{invoice.SellerInfo.CompanyName}</p>
                        <p>Tax ID: {invoice.SellerInfo.TaxId} Branch: {invoice.SellerInfo.BranchNumber}</p>
                        <p>{invoice.SellerInfo.Address}</p>
                        <p>Tel: {invoice.SellerInfo.Phone} Email: {invoice.SellerInfo.Email}</p>
                    </div>
                    <div class="company-info" style="width: 48%;">
                        <p><strong>Buyer / ผู้ซื้อ:</strong></p>
                        <p>{invoice.BuyerInfo.CompanyName}</p>
                        <p>Tax ID: {invoice.BuyerInfo.TaxId} Branch: {invoice.BuyerInfo.BranchNumber}</p>
                        <p>{invoice.BuyerInfo.Address}</p>
                    </div>
                </div>

                <table style="margin-top: 15px;">
                    <thead>
                        <tr>
                            <th style="width: 40px;">#</th>
                            <th>Description / รายการ</th>
                            <th style="width: 60px; text-align: center;">Qty</th>
                            <th style="width: 100px; text-align: right;">Unit Price</th>
                            <th style="width: 100px; text-align: right;">Amount</th>
                        </tr>
                    </thead>
                    <tbody>{itemRows}</tbody>
                    <tfoot>
                        <tr>
                            <td colspan="4" style="padding: 6px; border: 1px solid #ddd; text-align: right;"><strong>Subtotal / ราคาสินค้า</strong></td>
                            <td style="padding: 6px; border: 1px solid #ddd; text-align: right;">{invoice.SubTotal.Amount:N2}</td>
                        </tr>
                        <tr>
                            <td colspan="4" style="padding: 6px; border: 1px solid #ddd; text-align: right;"><strong>VAT / ภาษีมูลค่าเพิ่ม ({invoice.VATRate}%)</strong></td>
                            <td style="padding: 6px; border: 1px solid #ddd; text-align: right;">{invoice.VATAmount.Amount:N2}</td>
                        </tr>
                        <tr style="font-weight: bold; background: #f5f5f5;">
                            <td colspan="4" style="padding: 8px; border: 1px solid #ddd; text-align: right; font-size: 13px;"><strong>Total / รวมทั้งสิ้น</strong></td>
                            <td style="padding: 8px; border: 1px solid #ddd; text-align: right; font-size: 13px;"><strong>{invoice.TotalAmount.Amount:N2} {invoice.TotalAmount.Currency}</strong></td>
                        </tr>
                    </tfoot>
                </table>

                {(invoice.Status == "Voided" ? "<div style='color: red; font-size: 16px; text-align: center; margin-top: 20px; border: 2px solid red; padding: 8px;'>VOIDED / ยกเลิก</div>" : "")}

                <div style="margin-top: 30px; font-size: 9px; color: #999; text-align: center;">
                    <p>This is a computer-generated tax invoice. No signature required.</p>
                    <p>เอกสารนี้ออกโดยระบบคอมพิวเตอร์ ไม่ต้องลงนาม</p>
                </div>
            </body>
            </html>
            """;
    }

    /// <summary>
    /// Converts HTML to PDF bytes. In production, use QuestPDF, wkhtmltopdf, or Puppeteer.
    /// This placeholder returns the HTML as UTF-8 bytes for development purposes.
    /// </summary>
    private static Task<byte[]> ConvertHtmlToPdfAsync(string html, CancellationToken cancellationToken)
    {
        // Production implementation would use QuestPDF:
        // var document = Document.Create(container => { ... });
        // return Task.FromResult(document.GeneratePdf());

        // Development fallback: return HTML as bytes
        // Replace this with actual PDF generation library in production
        var bytes = System.Text.Encoding.UTF8.GetBytes(html);
        return Task.FromResult(bytes);
    }
}
