using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Web;
using System.Web.Script.Serialization;
using Take_Time_BangPhra.Services;
using Take_Time_BangPhra.Helpers;
using Take_Time_BangPhra.Class;

namespace Take_Time_BangPhra.API
{
    /// <summary>
    /// Tax Invoice API Handler
    /// สำหรับสร้างใบกำกับภาษีจากระบบภายนอก (Channel Manager - STAAH)
    /// </summary>
    public class TaxInvoiceAPI : IHttpHandler
    {
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();
        private readonly code _code = new code();
        private readonly string _connectionString;

        public TaxInvoiceAPI()
        {
            _connectionString = DatabaseHelper.GetConnectionString();
        }

        public void ProcessRequest(HttpContext context)
        {
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization, X-API-Key");

            // Handle preflight requests
            if (context.Request.HttpMethod == "OPTIONS")
            {
                context.Response.StatusCode = 200;
                return;
            }

            try
            {
                // Validate API Key
                string apiKey = context.Request.Headers["X-API-Key"];
                if (!ValidateApiKey(apiKey))
                {
                    WriteErrorResponse(context, 401, "Invalid or missing API Key");
                    return;
                }

                string action = context.Request.QueryString["action"] ?? "";

                switch (context.Request.HttpMethod.ToUpper())
                {
                    case "POST":
                        HandlePostRequest(context, action);
                        break;
                    case "GET":
                        HandleGetRequest(context, action);
                        break;
                    default:
                        WriteErrorResponse(context, 405, "Method not allowed");
                        break;
                }
            }
            catch (Exception ex)
            {
                LogError("TaxInvoiceAPI Error", ex.Message);
                WriteErrorResponse(context, 500, $"Internal server error: {ex.Message}");
            }
        }

        private void HandlePostRequest(HttpContext context, string action)
        {
            string requestBody = new StreamReader(context.Request.InputStream).ReadToEnd();

            switch (action.ToLower())
            {
                case "create":
                    CreateTaxInvoice(context, requestBody);
                    break;
                case "createfromreservation":
                    CreateTaxInvoiceFromReservation(context, requestBody);
                    break;
                default:
                    WriteErrorResponse(context, 400, "Invalid action. Supported: create, createfromreservation");
                    break;
            }
        }

        private void HandleGetRequest(HttpContext context, string action)
        {
            switch (action.ToLower())
            {
                case "status":
                    GetInvoiceStatus(context);
                    break;
                case "download":
                    DownloadInvoice(context);
                    break;
                default:
                    WriteErrorResponse(context, 400, "Invalid action. Supported: status, download");
                    break;
            }
        }

        /// <summary>
        /// สร้างใบกำกับภาษีจากข้อมูลการจองที่มีอยู่
        /// POST /API/TaxInvoiceAPI.ashx?action=createfromreservation
        /// Body: { "reservationId": 12345, "sendETax": true, "documentDate": "2025-01-08" }
        /// </summary>
        private void CreateTaxInvoiceFromReservation(HttpContext context, string requestBody)
        {
            try
            {
                var request = _serializer.Deserialize<CreateFromReservationRequest>(requestBody);

                if (request == null || request.ReservationId <= 0)
                {
                    WriteErrorResponse(context, 400, "Missing or invalid reservationId");
                    return;
                }

                // ดึงข้อมูลการจอง
                var reservationParams = new Dictionary<string, object>
                {
                    { "@reservationId", request.ReservationId }
                };

                DataTable dtReservation = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT r.*, c.FullName, c.Address, c.Address1, c.IDNumber, c.Email, c.MobilePhone,
                             c.Customer_Type_ID, c.Address_ID, c.Branch_Number,
                             a.Province, a.District, a.SubDistrict, a.PostalCode, a.Address_Code
                      FROM Reservation r
                      LEFT JOIN Customer c ON r.Customer_MobilePhone = c.MobilePhone
                      LEFT JOIN Address a ON c.Address_ID = a.ID
                      WHERE r.ID = @reservationId",
                    reservationParams);

                if (dtReservation.Rows.Count == 0)
                {
                    WriteErrorResponse(context, 404, $"Reservation {request.ReservationId} not found");
                    return;
                }

                DataRow reservation = dtReservation.Rows[0];

                // ดึงรายละเอียดห้องพัก/ของเช่า
                DataTable dtItems = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT ri.*,
                             CASE WHEN ri.Product_Type = 1 THEN ac.AccomName ELSE it.ItemName END AS ProductName,
                             CASE WHEN ri.Product_Type = 1 THEN ac.Unit ELSE it.Unit END AS ProductUnit
                      FROM Reservation_Item ri
                      LEFT JOIN Accommodation ac ON ri.Product_ID = ac.ID AND ri.Product_Type = 1
                      LEFT JOIN Item it ON ri.Product_ID = it.ID AND ri.Product_Type = 2
                      WHERE ri.Reservation_ID = @reservationId",
                    reservationParams);

                // เตรียมข้อมูลสำหรับสร้างใบกำกับภาษี
                DataTable dtReserve = CreateReceiptDetailTable();
                int lineNumber = 1;

                foreach (DataRow item in dtItems.Rows)
                {
                    string productData = BuildProductDescription(item, reservation);
                    decimal pricePerUnit = Convert.ToDecimal(item["Price_Per_Unit"]);
                    int amount = Convert.ToInt32(item["Amount"]);
                    decimal totalPrice = pricePerUnit * amount;

                    dtReserve.Rows.Add(
                        lineNumber++,
                        "",
                        item["Product_Type"],
                        item["Product_ID"],
                        productData,
                        amount,
                        item["ProductUnit"]?.ToString() ?? "ครั้ง",
                        pricePerUnit,
                        totalPrice
                    );
                }

                // กำหนดวันที่เอกสาร
                DateTime documentDate = request.DocumentDate.HasValue
                    ? request.DocumentDate.Value
                    : DateTime.Now;

                // กำหนดประเภทการชำระเงิน
                string paidType = !string.IsNullOrEmpty(request.PaidType)
                    ? request.PaidType
                    : reservation["Paid_Type"]?.ToString() ?? "TRANSFER";

                // สร้างใบกำกับภาษี
                decimal totalAmount = Convert.ToDecimal(reservation["TotalPrice"]);
                bool isDeposit = Convert.ToDecimal(reservation["Deposit"]) < totalAmount;
                bool sendETax = request.SendETax;

                var receiptService = new ReceiptService();
                string customerId = GetOrCreateCustomerId(reservation);

                receiptService.CreateReceipt(
                    request.ReservationId.ToString(),
                    (double)totalAmount,
                    dtReserve,
                    isDeposit,
                    documentDate,
                    sendETax,
                    paidType,
                    "API",
                    customerId
                );

                // ดึงข้อมูลใบกำกับภาษีที่สร้าง
                DataTable dtReceipt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP 1 * FROM Account_Receipt
                      WHERE Reservation_ID = @reservationId
                      ORDER BY Created_Date DESC",
                    reservationParams);

                if (dtReceipt.Rows.Count > 0)
                {
                    var response = new TaxInvoiceResponse
                    {
                        Success = true,
                        Message = "Tax invoice created successfully",
                        InvoiceId = dtReceipt.Rows[0]["ID"].ToString(),
                        InvoiceUID = dtReceipt.Rows[0]["UID"].ToString(),
                        ReservationId = request.ReservationId,
                        TotalAmount = Convert.ToDecimal(dtReceipt.Rows[0]["Total_Amount"]),
                        Vat = Convert.ToDecimal(dtReceipt.Rows[0]["Vat"]),
                        TotalExcludeVat = Convert.ToDecimal(dtReceipt.Rows[0]["Total_Amount_Exclude_Vat"]),
                        ETaxSent = sendETax,
                        CreatedDate = Convert.ToDateTime(dtReceipt.Rows[0]["Created_Date"])
                    };

                    WriteSuccessResponse(context, response);

                    // Log successful creation
                    LogInfo("TaxInvoice Created", $"Invoice {response.InvoiceId} created for Reservation {request.ReservationId}");
                }
                else
                {
                    WriteErrorResponse(context, 500, "Invoice created but could not retrieve details");
                }
            }
            catch (Exception ex)
            {
                LogError("CreateTaxInvoiceFromReservation Error", ex.Message);
                WriteErrorResponse(context, 500, $"Error creating tax invoice: {ex.Message}");
            }
        }

        /// <summary>
        /// สร้างใบกำกับภาษีจากข้อมูลที่ส่งมาโดยตรง
        /// POST /API/TaxInvoiceAPI.ashx?action=create
        /// </summary>
        private void CreateTaxInvoice(HttpContext context, string requestBody)
        {
            try
            {
                var request = _serializer.Deserialize<CreateTaxInvoiceRequest>(requestBody);

                if (request == null)
                {
                    WriteErrorResponse(context, 400, "Invalid request body");
                    return;
                }

                // Validate required fields
                if (string.IsNullOrEmpty(request.CustomerPhone))
                {
                    WriteErrorResponse(context, 400, "Missing customerPhone");
                    return;
                }

                if (request.Items == null || request.Items.Count == 0)
                {
                    WriteErrorResponse(context, 400, "Missing items");
                    return;
                }

                // ตรวจสอบ/สร้างลูกค้า
                string customerId = EnsureCustomerExists(request.Customer);

                // สร้าง Reservation ถ้าจำเป็น (สำหรับ Channel Manager)
                int reservationId = request.ReservationId > 0
                    ? request.ReservationId
                    : CreateReservationForInvoice(request, customerId);

                // เตรียมข้อมูลรายการ
                DataTable dtReserve = CreateReceiptDetailTable();
                int lineNumber = 1;
                decimal totalAmount = 0;

                foreach (var item in request.Items)
                {
                    decimal itemTotal = item.UnitPrice * item.Quantity;
                    totalAmount += itemTotal;

                    dtReserve.Rows.Add(
                        lineNumber++,
                        "",
                        item.ProductType,
                        item.ProductId,
                        item.Description,
                        item.Quantity,
                        item.Unit ?? "ครั้ง",
                        item.UnitPrice,
                        itemTotal
                    );
                }

                // กำหนดวันที่เอกสาร
                DateTime documentDate = request.DocumentDate ?? DateTime.Now;
                string paidType = request.PaidType ?? "TRANSFER";
                bool sendETax = request.SendETax;

                // สร้างใบกำกับภาษี
                var receiptService = new ReceiptService();
                receiptService.CreateReceipt(
                    reservationId.ToString(),
                    (double)totalAmount,
                    dtReserve,
                    request.IsDeposit,
                    documentDate,
                    sendETax,
                    paidType,
                    "API",
                    customerId
                );

                // ดึงข้อมูลใบกำกับภาษีที่สร้าง
                var reservationParams = new Dictionary<string, object>
                {
                    { "@reservationId", reservationId }
                };

                DataTable dtReceipt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP 1 * FROM Account_Receipt
                      WHERE Reservation_ID = @reservationId
                      ORDER BY Created_Date DESC",
                    reservationParams);

                if (dtReceipt.Rows.Count > 0)
                {
                    var response = new TaxInvoiceResponse
                    {
                        Success = true,
                        Message = "Tax invoice created successfully",
                        InvoiceId = dtReceipt.Rows[0]["ID"].ToString(),
                        InvoiceUID = dtReceipt.Rows[0]["UID"].ToString(),
                        ReservationId = reservationId,
                        TotalAmount = Convert.ToDecimal(dtReceipt.Rows[0]["Total_Amount"]),
                        Vat = Convert.ToDecimal(dtReceipt.Rows[0]["Vat"]),
                        TotalExcludeVat = Convert.ToDecimal(dtReceipt.Rows[0]["Total_Amount_Exclude_Vat"]),
                        ETaxSent = sendETax,
                        CreatedDate = Convert.ToDateTime(dtReceipt.Rows[0]["Created_Date"])
                    };

                    WriteSuccessResponse(context, response);
                    LogInfo("TaxInvoice Created", $"Invoice {response.InvoiceId} created via API");
                }
                else
                {
                    WriteErrorResponse(context, 500, "Invoice created but could not retrieve details");
                }
            }
            catch (Exception ex)
            {
                LogError("CreateTaxInvoice Error", ex.Message);
                WriteErrorResponse(context, 500, $"Error creating tax invoice: {ex.Message}");
            }
        }

        /// <summary>
        /// ตรวจสอบสถานะใบกำกับภาษี
        /// GET /API/TaxInvoiceAPI.ashx?action=status&invoiceId=REC-2025-0001
        /// </summary>
        private void GetInvoiceStatus(HttpContext context)
        {
            try
            {
                string invoiceId = context.Request.QueryString["invoiceId"];
                if (string.IsNullOrEmpty(invoiceId))
                {
                    WriteErrorResponse(context, 400, "Missing invoiceId parameter");
                    return;
                }

                var parameters = new Dictionary<string, object>
                {
                    { "@invoiceId", invoiceId }
                };

                DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT ar.*, r.Customer_MobilePhone, c.FullName as CustomerName
                      FROM Account_Receipt ar
                      LEFT JOIN Reservation r ON ar.Reservation_ID = r.ID
                      LEFT JOIN Customer c ON r.Customer_MobilePhone = c.MobilePhone
                      WHERE ar.ID = @invoiceId OR ar.UID = @invoiceId",
                    parameters);

                if (dt.Rows.Count == 0)
                {
                    WriteErrorResponse(context, 404, "Invoice not found");
                    return;
                }

                DataRow row = dt.Rows[0];
                var response = new InvoiceStatusResponse
                {
                    Success = true,
                    InvoiceId = row["ID"].ToString(),
                    InvoiceUID = row["UID"].ToString(),
                    ReservationId = Convert.ToInt32(row["Reservation_ID"]),
                    CustomerName = row["CustomerName"]?.ToString(),
                    CustomerPhone = row["Customer_MobilePhone"]?.ToString(),
                    TotalAmount = Convert.ToDecimal(row["Total_Amount"]),
                    Vat = Convert.ToDecimal(row["Vat"]),
                    Status = row["Status"].ToString(),
                    IsDeposit = row["IsDeposit"].ToString() == "True",
                    ETax = row["Etax"].ToString() == "True",
                    CreatedDate = Convert.ToDateTime(row["Created_Date"]),
                    PaidType = row["Paid_Type"]?.ToString()
                };

                WriteSuccessResponse(context, response);
            }
            catch (Exception ex)
            {
                LogError("GetInvoiceStatus Error", ex.Message);
                WriteErrorResponse(context, 500, $"Error getting invoice status: {ex.Message}");
            }
        }

        /// <summary>
        /// ดาวน์โหลดไฟล์ใบกำกับภาษี PDF
        /// GET /API/TaxInvoiceAPI.ashx?action=download&invoiceId=REC-2025-0001&type=pdf
        /// </summary>
        private void DownloadInvoice(HttpContext context)
        {
            try
            {
                string invoiceId = context.Request.QueryString["invoiceId"];
                string fileType = context.Request.QueryString["type"] ?? "pdf";

                if (string.IsNullOrEmpty(invoiceId))
                {
                    WriteErrorResponse(context, 400, "Missing invoiceId parameter");
                    return;
                }

                var parameters = new Dictionary<string, object>
                {
                    { "@invoiceId", invoiceId }
                };

                DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT * FROM Account_Receipt WHERE ID = @invoiceId OR UID = @invoiceId",
                    parameters);

                if (dt.Rows.Count == 0)
                {
                    WriteErrorResponse(context, 404, "Invoice not found");
                    return;
                }

                DataRow row = dt.Rows[0];
                DateTime createdDate = Convert.ToDateTime(row["Created_Date"]);
                string uid = row["UID"].ToString();
                string receiptId = row["ID"].ToString();

                string basePath = System.Configuration.ConfigurationManager.AppSettings["ReceiptFolderPath"];
                string year = createdDate.Year.ToString();
                string month = createdDate.Month.ToString("00");

                string fileName;
                if (fileType.ToLower() == "etax")
                {
                    fileName = $"{receiptId}_{uid}_etax.pdf";
                }
                else
                {
                    fileName = $"{receiptId}_{uid}.pdf";
                }

                string filePath = Path.Combine(basePath, year, month, fileName);

                if (!File.Exists(filePath))
                {
                    WriteErrorResponse(context, 404, "Invoice file not found");
                    return;
                }

                context.Response.Clear();
                context.Response.ContentType = "application/pdf";
                context.Response.AddHeader("Content-Disposition", $"attachment; filename={fileName}");
                context.Response.TransmitFile(filePath);
                context.Response.End();
            }
            catch (Exception ex)
            {
                LogError("DownloadInvoice Error", ex.Message);
                WriteErrorResponse(context, 500, $"Error downloading invoice: {ex.Message}");
            }
        }

        #region Helper Methods

        private bool ValidateApiKey(string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
                return false;

            // ดึง API Key จาก database หรือ config
            string validApiKey = System.Configuration.ConfigurationManager.AppSettings["TaxInvoiceApiKey"];

            // ถ้ายังไม่ได้ตั้งค่า API Key ให้ใช้ค่า default (ควรเปลี่ยนในระบบ production)
            if (string.IsNullOrEmpty(validApiKey))
            {
                validApiKey = "TakeTime-TaxInvoice-API-2025";
            }

            return apiKey == validApiKey;
        }

        private DataTable CreateReceiptDetailTable()
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("Number", typeof(int));
            dt.Columns.Add("Receipt_ID", typeof(string));
            dt.Columns.Add("ProductType_ID", typeof(int));
            dt.Columns.Add("Product_ID", typeof(int));
            dt.Columns.Add("Product_Data", typeof(string));
            dt.Columns.Add("Product_Amount", typeof(decimal));
            dt.Columns.Add("Product_Unit", typeof(string));
            dt.Columns.Add("Price_PerPeice", typeof(decimal));
            dt.Columns.Add("Price_Amount", typeof(decimal));
            return dt;
        }

        private string BuildProductDescription(DataRow item, DataRow reservation)
        {
            string productName = item["ProductName"]?.ToString() ?? "ที่พัก";
            DateTime checkinDate = Convert.ToDateTime(reservation["CheckinDate"]);
            DateTime checkoutDate = Convert.ToDateTime(reservation["CheckoutDate"]);

            return $"{productName} เช็คอิน {checkinDate:dd MMM yyyy} เช็คเอ้าท์ {checkoutDate:dd MMM yyyy}";
        }

        private string GetOrCreateCustomerId(DataRow reservation)
        {
            string customerPhone = reservation["Customer_MobilePhone"]?.ToString();
            if (string.IsNullOrEmpty(customerPhone))
                return "0";

            var parameters = new Dictionary<string, object>
            {
                { "@phone", customerPhone }
            };

            DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                "SELECT ID FROM Customer WHERE MobilePhone = @phone",
                parameters);

            if (dt.Rows.Count > 0)
            {
                return dt.Rows[0]["ID"].ToString();
            }

            return "0";
        }

        private string EnsureCustomerExists(CustomerInfo customer)
        {
            if (customer == null || string.IsNullOrEmpty(customer.Phone))
                return "0";

            // Resolve the structured Thai address (ตำบล/อำเภอ/จังหวัด/รหัสไปรษณีย์) to an Address_ID,
            // creating the master Address row if it does not exist yet. Without Address_ID the
            // downstream documents (ใบกำกับภาษี/ใบสำคัญจ่าย) fall back to showing only the name,
            // which is the "ลูกค้าขึ้นแค่ชื่อ ไม่มีที่อยู่" issue reported from the accounting side.
            int? addressId = null;
            if (!string.IsNullOrEmpty(customer.Province)
                && !string.IsNullOrEmpty(customer.District)
                && !string.IsNullOrEmpty(customer.SubDistrict)
                && !string.IsNullOrEmpty(customer.PostalCode))
            {
                var addressHelper = new AddressHelper(_connectionString);
                addressId = addressHelper.UpsertAddress(
                    customer.PostalCode, customer.Province, customer.District, customer.SubDistrict);
            }

            var parameters = new Dictionary<string, object>
            {
                { "@phone", customer.Phone }
            };

            DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                "SELECT ID FROM Customer WHERE MobilePhone = @phone",
                parameters);

            if (dt.Rows.Count > 0)
            {
                // อัพเดทข้อมูลลูกค้าถ้ามีข้อมูลใหม่ (ไม่ทับค่าเดิมด้วยค่าว่าง)
                var updateParams = new Dictionary<string, object>
                {
                    { "@phone", customer.Phone },
                    { "@name", customer.Name ?? "" },
                    { "@email", customer.Email ?? "" },
                    { "@idNumber", customer.IDNumber ?? "" },
                    { "@address", customer.Address ?? "" },
                    { "@address1", customer.Address1 ?? "" },
                    { "@branchNumber", customer.BranchNumber ?? "" },
                    { "@addressId", (object)addressId ?? DBNull.Value }
                };

                _code.DatabaseInsertSafe(_connectionString,
                    @"UPDATE Customer SET
                      FullName = CASE WHEN @name = '' THEN FullName ELSE @name END,
                      Email = CASE WHEN @email = '' THEN Email ELSE @email END,
                      IDNumber = CASE WHEN @idNumber = '' THEN IDNumber ELSE @idNumber END,
                      Address = CASE WHEN @address = '' THEN Address ELSE @address END,
                      Address1 = CASE WHEN @address1 = '' THEN Address1 ELSE @address1 END,
                      Branch_Number = CASE WHEN @branchNumber = '' THEN Branch_Number ELSE @branchNumber END,
                      Address_ID = CASE WHEN @addressId IS NULL THEN Address_ID ELSE @addressId END
                      WHERE MobilePhone = @phone",
                    updateParams);

                return dt.Rows[0]["ID"].ToString();
            }

            // สร้างลูกค้าใหม่ พร้อมที่อยู่แบบมีโครงสร้างและเลขสาขา
            var insertParams = new Dictionary<string, object>
            {
                { "@phone", customer.Phone },
                { "@name", customer.Name ?? customer.Phone },
                { "@email", customer.Email ?? "" },
                { "@idNumber", customer.IDNumber ?? "" },
                { "@address", customer.Address ?? "" },
                { "@address1", customer.Address1 ?? "" },
                { "@branchNumber", customer.BranchNumber ?? "" },
                { "@addressId", (object)addressId ?? DBNull.Value },
                { "@customerType", customer.CustomerTypeId > 0 ? customer.CustomerTypeId : 2 }
            };

            _code.DatabaseInsertSafe(_connectionString,
                @"INSERT INTO Customer (MobilePhone, FullName, Email, IDNumber, Address, Address1, Branch_Number, Address_ID, Customer_Type_ID)
                  VALUES (@phone, @name, @email, @idNumber, @address, @address1, @branchNumber, @addressId, @customerType)",
                insertParams);

            // ดึง ID ของลูกค้าที่สร้าง
            DataTable dtNew = _code.DatabaseQuerySafe(_connectionString,
                "SELECT ID FROM Customer WHERE MobilePhone = @phone",
                parameters);

            return dtNew.Rows.Count > 0 ? dtNew.Rows[0]["ID"].ToString() : "0";
        }

        private int CreateReservationForInvoice(CreateTaxInvoiceRequest request, string customerId)
        {
            DateTime checkinDate = request.CheckinDate ?? DateTime.Now;
            DateTime checkoutDate = request.CheckoutDate ?? DateTime.Now.AddDays(1);
            int stayDays = (checkoutDate - checkinDate).Days;
            if (stayDays < 1) stayDays = 1;

            decimal totalPrice = 0;
            foreach (var item in request.Items)
            {
                totalPrice += item.UnitPrice * item.Quantity;
            }

            var parameters = new Dictionary<string, object>
            {
                { "@phone", request.CustomerPhone },
                { "@checkinDate", checkinDate },
                { "@checkoutDate", checkoutDate },
                { "@stayDays", stayDays },
                { "@totalPrice", totalPrice },
                { "@deposit", request.IsDeposit ? totalPrice : 0 },
                { "@status", "API Created" },
                { "@paidType", request.PaidType ?? "TRANSFER" },
                { "@remark", request.Remark ?? "สร้างจาก Channel Manager API" }
            };

            _code.DatabaseInsertSafe(_connectionString,
                @"INSERT INTO Reservation
                  (Customer_MobilePhone, CheckinDate, CheckoutDate, StayDays, TotalPrice, Deposit, Status, Paid_Type, Remark, Created_Date)
                  VALUES (@phone, @checkinDate, @checkoutDate, @stayDays, @totalPrice, @deposit, @status, @paidType, @remark, GETDATE())",
                parameters);

            // ดึง ID ของ Reservation ที่สร้าง
            DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                @"SELECT TOP 1 ID FROM Reservation WHERE Customer_MobilePhone = @phone ORDER BY ID DESC",
                new Dictionary<string, object> { { "@phone", request.CustomerPhone } });

            return dt.Rows.Count > 0 ? Convert.ToInt32(dt.Rows[0]["ID"]) : 0;
        }

        private void WriteSuccessResponse(HttpContext context, object data)
        {
            context.Response.StatusCode = 200;
            context.Response.Write(_serializer.Serialize(data));
        }

        private void WriteErrorResponse(HttpContext context, int statusCode, string message)
        {
            context.Response.StatusCode = statusCode;
            context.Response.Write(_serializer.Serialize(new
            {
                Success = false,
                Error = message,
                StatusCode = statusCode
            }));
        }

        private void LogInfo(string action, string message)
        {
            try
            {
                _code.Logs(_connectionString, action, message, "API");
            }
            catch { }
        }

        private void LogError(string action, string message)
        {
            try
            {
                _code.Logs(_connectionString, $"ERROR: {action}", message, "API");
            }
            catch { }
        }

        #endregion

        public bool IsReusable => false;
    }

    #region Request/Response Models

    public class CreateFromReservationRequest
    {
        public int ReservationId { get; set; }
        public bool SendETax { get; set; }
        public DateTime? DocumentDate { get; set; }
        public string PaidType { get; set; }
    }

    public class CreateTaxInvoiceRequest
    {
        public int ReservationId { get; set; }
        public string CustomerPhone { get; set; }
        public CustomerInfo Customer { get; set; }
        public List<InvoiceItem> Items { get; set; }
        public bool IsDeposit { get; set; }
        public bool SendETax { get; set; }
        public DateTime? DocumentDate { get; set; }
        public DateTime? CheckinDate { get; set; }
        public DateTime? CheckoutDate { get; set; }
        public string PaidType { get; set; }
        public string Remark { get; set; }
    }

    public class CustomerInfo
    {
        public string Phone { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string IDNumber { get; set; }
        public string Address { get; set; }       // ที่อยู่บรรทัดแรก (บ้านเลขที่/ถนน) - free text
        public string Address1 { get; set; }      // ที่อยู่บรรทัดที่สอง (อาคาร/หมู่บ้าน) - free text
        public string SubDistrict { get; set; }   // ตำบล/แขวง
        public string District { get; set; }      // อำเภอ/เขต
        public string Province { get; set; }      // จังหวัด
        public string PostalCode { get; set; }    // รหัสไปรษณีย์
        public int CustomerTypeId { get; set; }
        public string BranchNumber { get; set; }  // เลขที่สาขา (5 หลัก) สำหรับนิติบุคคล
    }

    public class InvoiceItem
    {
        public int ProductType { get; set; } // 1 = Accommodation, 2 = Item
        public int ProductId { get; set; }
        public string Description { get; set; }
        public decimal Quantity { get; set; }
        public string Unit { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public class TaxInvoiceResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string InvoiceId { get; set; }
        public string InvoiceUID { get; set; }
        public int ReservationId { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal Vat { get; set; }
        public decimal TotalExcludeVat { get; set; }
        public bool ETaxSent { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class InvoiceStatusResponse
    {
        public bool Success { get; set; }
        public string InvoiceId { get; set; }
        public string InvoiceUID { get; set; }
        public int ReservationId { get; set; }
        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal Vat { get; set; }
        public string Status { get; set; }
        public bool IsDeposit { get; set; }
        public bool ETax { get; set; }
        public DateTime CreatedDate { get; set; }
        public string PaidType { get; set; }
    }

    #endregion
}
