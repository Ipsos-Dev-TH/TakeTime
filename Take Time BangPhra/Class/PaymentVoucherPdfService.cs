using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;
using System.IO;
using System.Web;
using System.Web.Hosting;
using Microsoft.Reporting.WebForms;

/// <summary>
/// Service for generating Payment Voucher PDFs
/// Can be used from PayrollService or PaymentVoucher.aspx
/// </summary>
public class PaymentVoucherPdfService
{
    private readonly string connectionString;

    public PaymentVoucherPdfService()
    {
        connectionString = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
    }

    /// <summary>
    /// Get the web application root path
    /// </summary>
    private string GetWebRootPath()
    {
        // Try HttpContext first
        if (HttpContext.Current != null)
        {
            return HttpContext.Current.Server.MapPath("~");
        }
        // Fallback to HostingEnvironment
        if (HostingEnvironment.IsHosted)
        {
            return HostingEnvironment.MapPath("~");
        }
        // Last resort - use AppDomain base directory and remove bin folder if present
        string basePath = AppDomain.CurrentDomain.BaseDirectory;
        if (basePath.EndsWith("\\bin\\") || basePath.EndsWith("/bin/"))
        {
            basePath = Directory.GetParent(basePath.TrimEnd('\\', '/')).Parent.FullName;
        }
        return basePath;
    }

    /// <summary>
    /// Get the output folder path for payment PDFs
    /// </summary>
    private string GetOutputBasePath()
    {
        string configPath = ConfigurationManager.AppSettings["PaymentFolderPath"];
        if (!string.IsNullOrEmpty(configPath))
        {
            return configPath;
        }
        return Path.Combine(GetWebRootPath(), "Documents", "Payment");
    }

    /// <summary>
    /// Generate PDF for a payment voucher
    /// </summary>
    /// <param name="paymentId">Payment voucher ID (e.g., PAY2512170001)</param>
    /// <param name="creatorAdminId">Admin ID of the creator (for signature)</param>
    /// <returns>Tuple of success flag and file path or error message</returns>
    public (bool Success, string FilePath, string Message) GeneratePdf(string paymentId, short creatorAdminId)
    {
        try
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // Get payment record
                DataTable dtPayment = GetPaymentData(conn, paymentId);
                if (dtPayment == null || dtPayment.Rows.Count == 0)
                {
                    return (false, null, $"ไม่พบข้อมูลใบสำคัญจ่าย: {paymentId}");
                }

                string uid = dtPayment.Rows[0]["UID"]?.ToString() ?? "";
                int vendorId = Convert.ToInt32(dtPayment.Rows[0]["Vendor_ID"]);
                DateTime createdDate = Convert.ToDateTime(dtPayment.Rows[0]["Created_Date"]);
                string paidType = dtPayment.Rows[0]["Paid_Type"]?.ToString() ?? "";

                // Get business info
                DataTable dtBusinessInfo = GetBusinessInfo(conn);

                // Get vendor info
                DataTable dtVendor = GetVendorData(conn, vendorId);

                // For payroll payments, override vendor name with employee name
                if (paidType == "เงินเดือน")
                {
                    OverrideVendorNameForPayroll(conn, paymentId, dtVendor);
                }

                // Get payment details
                DataTable dtPaymentDetail = GetPaymentDetails(conn, paymentId);

                // Get signature data
                string receiverIdNumber = dtVendor.Rows.Count > 0 ? dtVendor.Rows[0]["IDNumber"]?.ToString() ?? "" : "";
                SignatureService signatureService = new SignatureService();
                DataTable dtSignature = signatureService.GetPaymentVoucherSignatureData(creatorAdminId, receiverIdNumber);

                // Empty upload table (payroll vouchers don't have attachments)
                DataTable dtUpload = new DataTable();
                dtUpload.Columns.Add("Name");

                // Generate PDF
                string year = createdDate.Year.ToString();
                string month = createdDate.Month.ToString();
                string outputBasePath = GetOutputBasePath();
                string outputDir = Path.Combine(outputBasePath, year, month);
                string outputFile = Path.Combine(outputDir, $"{paymentId}_{uid}.pdf");

                System.Diagnostics.Debug.WriteLine($"PaymentVoucherPdfService: OutputDir={outputDir}, OutputFile={outputFile}");

                // Ensure directory exists
                if (!Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                // Delete existing file if exists
                if (File.Exists(outputFile))
                {
                    File.Delete(outputFile);
                }

                // Render report to PDF
                byte[] pdfBytes = RenderPaymentVoucherReport(
                    dtBusinessInfo, dtVendor, dtPaymentDetail, dtPayment, dtSignature, dtUpload);

                if (pdfBytes == null || pdfBytes.Length == 0)
                {
                    return (false, null, "ไม่สามารถสร้าง PDF ได้");
                }

                // Write PDF file
                File.WriteAllBytes(outputFile, pdfBytes);

                return (true, outputFile, "สร้าง PDF สำเร็จ");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PaymentVoucherPdfService.GeneratePdf Error: {ex.Message}");
            return (false, null, $"เกิดข้อผิดพลาด: {ex.Message}");
        }
    }

    private DataTable GetPaymentData(SqlConnection conn, string paymentId)
    {
        using (SqlCommand cmd = new SqlCommand(@"
            SELECT ap.*, avt.Vat_Type, avt.Vat_Percent
            FROM Account_Payment ap
            INNER JOIN Account_Vat_Type avt ON avt.ID = ap.Vat_Type_ID
            WHERE ap.ID = @PaymentID", conn))
        {
            cmd.Parameters.AddWithValue("@PaymentID", paymentId);
            using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
            {
                DataTable dt = new DataTable();
                adapter.Fill(dt);
                return dt;
            }
        }
    }

    private DataTable GetBusinessInfo(SqlConnection conn)
    {
        using (SqlCommand cmd = new SqlCommand(@"
            SELECT * FROM Business_Info
            LEFT JOIN Customer_Type ON Business_Type_ID = Customer_Type.ID
            LEFT JOIN Address ON Address.ID = Address_ID", conn))
        {
            using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
            {
                DataTable dt = new DataTable();
                adapter.Fill(dt);

                // Format address
                if (dt.Rows.Count > 0)
                {
                    try
                    {
                        DataRow row = dt.Rows[0];
                        if (row["Address_ID"] == DBNull.Value || string.IsNullOrEmpty(row["Address_ID"].ToString()))
                        {
                            // Use Address as-is
                        }
                        else
                        {
                            string province = row["Province"]?.ToString() ?? "";
                            if (province.Contains("กรุงเทพ"))
                            {
                                row["Address"] = $"{row["Address"]} {row["Address1"]} แขวง {row["SubDistrict"]} เขต {row["District"]} {row["Province"]} {row["PostalCode"]}";
                            }
                            else
                            {
                                row["Address"] = $"{row["Address"]} {row["Address1"]} ต.{row["SubDistrict"]} อ.{row["District"]} จ.{row["Province"]} {row["PostalCode"]}";
                            }
                        }
                    }
                    catch { }
                }

                return dt;
            }
        }
    }

    private DataTable GetVendorData(SqlConnection conn, int vendorId)
    {
        using (SqlCommand cmd = new SqlCommand(@"
            SELECT * FROM Vendor
            LEFT JOIN Customer_Type ON Customer_Type.ID = Vendor_Type_ID
            LEFT JOIN Address ON Address.ID = Address_ID
            WHERE Vendor.ID = @VendorID", conn))
        {
            cmd.Parameters.AddWithValue("@VendorID", vendorId);
            using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
            {
                DataTable dt = new DataTable();
                adapter.Fill(dt);

                // Format address and branch
                if (dt.Rows.Count > 0)
                {
                    try
                    {
                        DataRow row = dt.Rows[0];
                        if (row["Address_ID"] == DBNull.Value || string.IsNullOrEmpty(row["Address_ID"].ToString()))
                        {
                            // Use Address as-is
                        }
                        else
                        {
                            string province = row["Province"]?.ToString() ?? "";
                            if (province.Contains("กรุงเทพ"))
                            {
                                row["Address"] = $"{row["Address"]} {row["Address1"]} แขวง {row["SubDistrict"]} เขต {row["District"]} {row["Province"]} {row["PostalCode"]}";
                            }
                            else
                            {
                                row["Address"] = $"{row["Address"]} {row["Address1"]} ต.{row["SubDistrict"]} อ.{row["District"]} จ.{row["Province"]} {row["PostalCode"]}";
                            }
                        }

                        // Add branch number to name
                        string branchNumber = row["Branch_Number"]?.ToString() ?? "";
                        if (branchNumber.Length == 5)
                        {
                            row["Name"] = row["Name"] + " สาขาที่ " + branchNumber;
                        }
                    }
                    catch { }
                }

                return dt;
            }
        }
    }

    private void OverrideVendorNameForPayroll(SqlConnection conn, string paymentId, DataTable dtVendor)
    {
        if (dtVendor == null || dtVendor.Rows.Count == 0) return;

        try
        {
            // First check if vendor name is already the employee name (new vouchers)
            // by checking if it's NOT "เงินเดือนพนักงาน"
            string currentVendorName = dtVendor.Rows[0]["Name"]?.ToString() ?? "";
            if (currentVendorName != "เงินเดือนพนักงาน")
            {
                // Vendor name is already the employee name
                return;
            }

            // Get employee name from Payroll_Records (for old vouchers using generic vendor)
            using (SqlCommand cmd = new SqlCommand(
                "SELECT EmployeeName FROM Payroll_Records WHERE VoucherNumber = @VoucherNumber", conn))
            {
                cmd.Parameters.AddWithValue("@VoucherNumber", paymentId);
                object result = cmd.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                {
                    dtVendor.Rows[0]["Name"] = result.ToString();
                    dtVendor.Rows[0]["Address"] = ""; // Clear address for employee
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OverrideVendorNameForPayroll Error: {ex.Message}");
        }
    }

    private DataTable GetPaymentDetails(SqlConnection conn, string paymentId)
    {
        using (SqlCommand cmd = new SqlCommand(@"
            SELECT * FROM Account_Payment_Detail
            WHERE Payment_ID = @PaymentID
            ORDER BY Number ASC", conn))
        {
            cmd.Parameters.AddWithValue("@PaymentID", paymentId);
            using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
            {
                DataTable dt = new DataTable();
                adapter.Fill(dt);
                return dt;
            }
        }
    }

    private byte[] RenderPaymentVoucherReport(
        DataTable dtBusinessInfo,
        DataTable dtVendor,
        DataTable dtPaymentDetail,
        DataTable dtPayment,
        DataTable dtSignature,
        DataTable dtUpload)
    {
        try
        {
            // Create LocalReport
            LocalReport localReport = new LocalReport();

            // Set report path - use GetWebRootPath() for correct path resolution
            string webRoot = GetWebRootPath();
            string rdlcPath = Path.Combine(webRoot, "Account", "Report", "Payment.rdlc");

            System.Diagnostics.Debug.WriteLine($"PaymentVoucherPdfService: WebRoot={webRoot}, RdlcPath={rdlcPath}");

            if (!File.Exists(rdlcPath))
            {
                System.Diagnostics.Debug.WriteLine($"Report file not found at: {rdlcPath}");
                return null;
            }

            localReport.ReportPath = rdlcPath;
            localReport.EnableExternalImages = true;

            // Add data sources
            localReport.DataSources.Clear();
            localReport.DataSources.Add(new ReportDataSource("DataSet1", dtBusinessInfo));
            localReport.DataSources.Add(new ReportDataSource("DataSet2", dtVendor));
            localReport.DataSources.Add(new ReportDataSource("DataSet3", dtPaymentDetail));
            localReport.DataSources.Add(new ReportDataSource("DataSet4", dtPayment));
            localReport.DataSources.Add(new ReportDataSource("DataSet5", dtSignature));
            localReport.DataSources.Add(new ReportDataSource("DataSet6", dtUpload));

            // Render to PDF
            Warning[] warnings;
            string[] streamIds;
            string mimeType;
            string encoding;
            string fileNameExtension;

            byte[] pdfBytes = localReport.Render(
                "PDF",
                null,
                out mimeType,
                out encoding,
                out fileNameExtension,
                out streamIds,
                out warnings);

            return pdfBytes;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"RenderPaymentVoucherReport Error: {ex.Message}");
            return null;
        }
    }
}
