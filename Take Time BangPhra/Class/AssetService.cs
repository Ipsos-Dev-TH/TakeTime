using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace Take_Time_BangPhra.Class
{
    /// <summary>
    /// Service class for Asset Management operations
    /// ระบบจัดการสินทรัพย์ถาวร
    /// </summary>
    public class AssetService
    {
        private readonly string conn;
        private readonly _Default code;

        public AssetService()
        {
            conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
            code = new _Default();
        }

        #region Asset Categories

        /// <summary>
        /// Get all active asset categories
        /// </summary>
        public DataTable GetAssetCategories()
        {
            string query = @"SELECT * FROM Asset_Category WHERE Status = 1 ORDER BY CategoryCode";
            return code.DatabaseQuery(conn, query);
        }

        /// <summary>
        /// Get category by ID
        /// </summary>
        public DataTable GetCategoryById(int categoryId)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@ID", categoryId }
            };
            return code.DatabaseQuerySafe(conn, "SELECT * FROM Asset_Category WHERE ID = @ID", parameters);
        }

        #endregion

        #region Assets CRUD

        /// <summary>
        /// Get all assets with summary view
        /// </summary>
        public DataTable GetAllAssets(string status = null, int? categoryId = null)
        {
            string query = @"SELECT * FROM vw_AssetSummary WHERE 1=1";
            var parameters = new Dictionary<string, object>();

            if (!string.IsNullOrEmpty(status))
            {
                query += " AND Status = @Status";
                parameters.Add("@Status", status);
            }

            if (categoryId.HasValue)
            {
                query += " AND CategoryID = @CategoryID";
                parameters.Add("@CategoryID", categoryId.Value);
            }

            query += " ORDER BY AssetCode DESC";

            if (parameters.Count > 0)
                return code.DatabaseQuerySafe(conn, query, parameters);
            else
                return code.DatabaseQuery(conn, query);
        }

        /// <summary>
        /// Get asset by ID (from summary view)
        /// </summary>
        public DataTable GetAssetById(int assetId)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@ID", assetId }
            };
            return code.DatabaseQuerySafe(conn, "SELECT * FROM vw_AssetSummary WHERE ID = @ID", parameters);
        }

        /// <summary>
        /// Get full asset details by ID (including all columns from Asset table)
        /// </summary>
        public DataTable GetAssetFullDetails(int assetId)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@ID", assetId }
            };
            string query = @"
                SELECT a.*,
                       c.CategoryName, c.CategoryCode,
                       v.Name as VendorName
                FROM Asset a
                LEFT JOIN Asset_Category c ON a.CategoryID = c.ID
                LEFT JOIN Vendor v ON a.VendorID = v.ID
                WHERE a.ID = @ID";
            return code.DatabaseQuerySafe(conn, query, parameters);
        }

        /// <summary>
        /// Get asset by Payment Voucher ID
        /// </summary>
        public DataTable GetAssetsByPaymentVoucherId(string paymentVoucherId)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@PaymentVoucherID", paymentVoucherId }
            };
            return code.DatabaseQuerySafe(conn,
                "SELECT * FROM vw_AssetSummary WHERE PaymentVoucherID = @PaymentVoucherID",
                parameters);
        }

        /// <summary>
        /// Generate new asset code
        /// </summary>
        public string GenerateAssetCode(string categoryCode)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@CategoryCode", categoryCode }
            };
            DataTable dt = code.DatabaseQuerySafe(conn,
                "SELECT dbo.fn_GenerateAssetCode(@CategoryCode) AS NewCode",
                parameters);

            if (dt != null && dt.Rows.Count > 0)
                return dt.Rows[0]["NewCode"].ToString();

            // Fallback if function doesn't exist
            string year = DateTime.Now.Year.ToString().Substring(2);
            return $"{categoryCode}-{year}-0001";
        }

        /// <summary>
        /// Create new asset
        /// </summary>
        public (bool Success, string Message, int AssetId) CreateAsset(
            string assetName,
            string description,
            int categoryId,
            string serialNumber,
            string brand,
            string model,
            DateTime purchaseDate,
            decimal purchasePrice,
            int? vendorId,
            string paymentVoucherId,
            string invoiceNumber,
            DateTime? warrantyExpireDate,
            int usefulLifeYears,
            decimal residualValue,
            string location,
            string department,
            short? responsiblePersonId,
            string notes,
            short createdBy)
        {
            try
            {
                // Get category code
                DataTable dtCategory = GetCategoryById(categoryId);
                if (dtCategory == null || dtCategory.Rows.Count == 0)
                    return (false, "ไม่พบหมวดหมู่สินทรัพย์", 0);

                string categoryCode = dtCategory.Rows[0]["CategoryCode"].ToString();
                string assetCode = GenerateAssetCode(categoryCode);

                // Calculate depreciation values
                int usefulLifeMonths = usefulLifeYears * 12;
                decimal depreciableAmount = purchasePrice - residualValue;
                decimal monthlyDepreciation = usefulLifeMonths > 0 ? depreciableAmount / usefulLifeMonths : 0;
                DateTime depreciationStartDate = new DateTime(purchaseDate.Year, purchaseDate.Month, 1).AddMonths(1);

                var parameters = new Dictionary<string, object>
                {
                    { "@AssetCode", assetCode },
                    { "@AssetName", assetName },
                    { "@Description", description ?? (object)DBNull.Value },
                    { "@CategoryID", categoryId },
                    { "@SerialNumber", serialNumber ?? (object)DBNull.Value },
                    { "@Brand", brand ?? (object)DBNull.Value },
                    { "@Model", model ?? (object)DBNull.Value },
                    { "@PurchaseDate", purchaseDate },
                    { "@PurchasePrice", purchasePrice },
                    { "@VendorID", vendorId.HasValue ? (object)vendorId.Value : DBNull.Value },
                    { "@PaymentVoucherID", paymentVoucherId ?? (object)DBNull.Value },
                    { "@InvoiceNumber", invoiceNumber ?? (object)DBNull.Value },
                    { "@WarrantyExpireDate", warrantyExpireDate.HasValue ? (object)warrantyExpireDate.Value : DBNull.Value },
                    { "@UsefulLifeYears", usefulLifeYears },
                    { "@UsefulLifeMonths", usefulLifeMonths },
                    { "@ResidualValue", residualValue },
                    { "@DepreciationStartDate", depreciationStartDate },
                    { "@MonthlyDepreciation", monthlyDepreciation },
                    { "@BookValue", purchasePrice },
                    { "@Location", location ?? (object)DBNull.Value },
                    { "@Department", department ?? (object)DBNull.Value },
                    { "@ResponsiblePersonID", responsiblePersonId.HasValue ? (object)responsiblePersonId.Value : DBNull.Value },
                    { "@Notes", notes ?? (object)DBNull.Value },
                    { "@CreatedBy", createdBy }
                };

                string query = @"
                    INSERT INTO Assets (
                        AssetCode, AssetName, Description, CategoryID, SerialNumber, Brand, Model,
                        PurchaseDate, PurchasePrice, VendorID, PaymentVoucherID, InvoiceNumber,
                        WarrantyExpireDate, UsefulLifeYears, UsefulLifeMonths, ResidualValue,
                        DepreciationStartDate, MonthlyDepreciation, AccumulatedDepreciation, BookValue,
                        Location, Department, ResponsiblePersonID, Notes, CreatedBy, Status
                    ) VALUES (
                        @AssetCode, @AssetName, @Description, @CategoryID, @SerialNumber, @Brand, @Model,
                        @PurchaseDate, @PurchasePrice, @VendorID, @PaymentVoucherID, @InvoiceNumber,
                        @WarrantyExpireDate, @UsefulLifeYears, @UsefulLifeMonths, @ResidualValue,
                        @DepreciationStartDate, @MonthlyDepreciation, 0, @BookValue,
                        @Location, @Department, @ResponsiblePersonID, @Notes, @CreatedBy, 'ACTIVE'
                    );
                    SELECT SCOPE_IDENTITY();";

                DataTable dt = code.DatabaseQuerySafe(conn, query, parameters);
                int assetId = Convert.ToInt32(dt.Rows[0][0]);

                // Log history
                LogAssetHistory(assetId, "CREATE", $"สร้างสินทรัพย์ใหม่: {assetName}", null, location, null, department, null, createdBy);

                return (true, assetCode, assetId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreateAsset Error: {ex.Message}");
                return (false, $"เกิดข้อผิดพลาด: {ex.Message}", 0);
            }
        }

        /// <summary>
        /// Update existing asset
        /// </summary>
        public (bool Success, string Message) UpdateAsset(
            int assetId,
            string assetName,
            string description,
            string serialNumber,
            string brand,
            string model,
            DateTime? warrantyExpireDate,
            string location,
            string department,
            short? responsiblePersonId,
            string notes,
            short modifiedBy)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@ID", assetId },
                    { "@AssetName", assetName },
                    { "@Description", description ?? (object)DBNull.Value },
                    { "@SerialNumber", serialNumber ?? (object)DBNull.Value },
                    { "@Brand", brand ?? (object)DBNull.Value },
                    { "@Model", model ?? (object)DBNull.Value },
                    { "@WarrantyExpireDate", warrantyExpireDate.HasValue ? (object)warrantyExpireDate.Value : DBNull.Value },
                    { "@Location", location ?? (object)DBNull.Value },
                    { "@Department", department ?? (object)DBNull.Value },
                    { "@ResponsiblePersonID", responsiblePersonId.HasValue ? (object)responsiblePersonId.Value : DBNull.Value },
                    { "@Notes", notes ?? (object)DBNull.Value },
                    { "@ModifiedBy", modifiedBy },
                    { "@ModifiedDate", DateTime.Now }
                };

                string query = @"
                    UPDATE Assets SET
                        AssetName = @AssetName,
                        Description = @Description,
                        SerialNumber = @SerialNumber,
                        Brand = @Brand,
                        Model = @Model,
                        WarrantyExpireDate = @WarrantyExpireDate,
                        Location = @Location,
                        Department = @Department,
                        ResponsiblePersonID = @ResponsiblePersonID,
                        Notes = @Notes,
                        ModifiedBy = @ModifiedBy,
                        ModifiedDate = @ModifiedDate
                    WHERE ID = @ID";

                code.DatabaseInsertSafe(conn, query, parameters);
                return (true, "บันทึกข้อมูลสำเร็จ");
            }
            catch (Exception ex)
            {
                return (false, $"เกิดข้อผิดพลาด: {ex.Message}");
            }
        }

        /// <summary>
        /// Dispose/Sell/Write-off asset
        /// </summary>
        public (bool Success, string Message) DisposeAsset(
            int assetId,
            string status, // DISPOSED, SOLD, WRITTEN_OFF
            DateTime disposalDate,
            decimal? disposalPrice,
            string reason,
            short modifiedBy)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@ID", assetId },
                    { "@Status", status },
                    { "@DisposalDate", disposalDate },
                    { "@DisposalPrice", disposalPrice.HasValue ? (object)disposalPrice.Value : DBNull.Value },
                    { "@DisposalReason", reason ?? (object)DBNull.Value },
                    { "@ModifiedBy", modifiedBy },
                    { "@ModifiedDate", DateTime.Now }
                };

                string query = @"
                    UPDATE Assets SET
                        Status = @Status,
                        DisposalDate = @DisposalDate,
                        DisposalPrice = @DisposalPrice,
                        DisposalReason = @DisposalReason,
                        ModifiedBy = @ModifiedBy,
                        ModifiedDate = @ModifiedDate
                    WHERE ID = @ID";

                code.DatabaseInsertSafe(conn, query, parameters);

                // Log history
                LogAssetHistory(assetId, "STATUS_CHANGE", $"เปลี่ยนสถานะเป็น {status}: {reason}", null, null, null, null, disposalPrice, modifiedBy);

                return (true, "บันทึกการจำหน่ายสำเร็จ");
            }
            catch (Exception ex)
            {
                return (false, $"เกิดข้อผิดพลาด: {ex.Message}");
            }
        }

        #endregion

        #region Depreciation

        /// <summary>
        /// Get depreciation history for an asset
        /// </summary>
        public DataTable GetDepreciationHistory(int assetId)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@AssetID", assetId }
            };
            return code.DatabaseQuerySafe(conn,
                @"SELECT * FROM Asset_Depreciation
                  WHERE AssetID = @AssetID
                  ORDER BY Year DESC, Month DESC",
                parameters);
        }

        /// <summary>
        /// Calculate monthly depreciation for all active assets
        /// </summary>
        public (bool Success, string Message, int AffectedAssets) CalculateMonthlyDepreciation(int year, int month)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@Year", year },
                    { "@Month", month }
                };

                DataTable dt = code.DatabaseQuerySafe(conn,
                    "EXEC sp_CalculateMonthlyDepreciation @Year, @Month",
                    parameters);

                int affected = dt != null && dt.Rows.Count > 0 ? Convert.ToInt32(dt.Rows[0]["AffectedAssets"]) : 0;
                return (true, $"คำนวณค่าเสื่อมราคาสำเร็จ {affected} รายการ", affected);
            }
            catch (Exception ex)
            {
                return (false, $"เกิดข้อผิดพลาด: {ex.Message}", 0);
            }
        }

        /// <summary>
        /// Get depreciation summary report
        /// </summary>
        public DataTable GetDepreciationReport(int year, int? month = null)
        {
            string query = @"
                SELECT
                    c.CategoryName,
                    COUNT(a.ID) AS AssetCount,
                    SUM(a.PurchasePrice) AS TotalPurchasePrice,
                    SUM(a.AccumulatedDepreciation) AS TotalAccumulatedDepreciation,
                    SUM(a.BookValue) AS TotalBookValue,
                    SUM(a.MonthlyDepreciation) AS MonthlyDepreciation
                FROM Assets a
                INNER JOIN Asset_Category c ON a.CategoryID = c.ID
                WHERE a.Status = 'ACTIVE'
                GROUP BY c.CategoryName
                ORDER BY c.CategoryName";

            return code.DatabaseQuery(conn, query);
        }

        #endregion

        #region Asset History

        /// <summary>
        /// Log asset history
        /// </summary>
        public void LogAssetHistory(int assetId, string actionType, string description,
            string fromLocation, string toLocation, string fromDepartment, string toDepartment,
            decimal? cost, short createdBy)
        {
            try
            {
                var parameters = new Dictionary<string, object>
                {
                    { "@AssetID", assetId },
                    { "@ActionType", actionType },
                    { "@ActionDate", DateTime.Now.Date },
                    { "@Description", description ?? (object)DBNull.Value },
                    { "@FromLocation", fromLocation ?? (object)DBNull.Value },
                    { "@ToLocation", toLocation ?? (object)DBNull.Value },
                    { "@FromDepartment", fromDepartment ?? (object)DBNull.Value },
                    { "@ToDepartment", toDepartment ?? (object)DBNull.Value },
                    { "@Cost", cost.HasValue ? (object)cost.Value : DBNull.Value },
                    { "@CreatedBy", createdBy }
                };

                code.DatabaseInsertSafe(conn,
                    @"INSERT INTO Asset_History (AssetID, ActionType, ActionDate, Description,
                      FromLocation, ToLocation, FromDepartment, ToDepartment, Cost, CreatedBy)
                      VALUES (@AssetID, @ActionType, @ActionDate, @Description,
                      @FromLocation, @ToLocation, @FromDepartment, @ToDepartment, @Cost, @CreatedBy)",
                    parameters);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LogAssetHistory Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Get asset history
        /// </summary>
        public DataTable GetAssetHistory(int assetId)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@AssetID", assetId }
            };
            return code.DatabaseQuerySafe(conn,
                @"SELECT ah.*, adm.UserName AS CreatedByName
                  FROM Asset_History ah
                  LEFT JOIN Admin adm ON ah.CreatedBy = adm.ID
                  WHERE AssetID = @AssetID
                  ORDER BY ActionDate DESC, ID DESC",
                parameters);
        }

        #endregion

        #region Reports

        /// <summary>
        /// Get asset summary by category
        /// </summary>
        public DataTable GetAssetSummaryByCategory()
        {
            string query = @"
                SELECT
                    c.CategoryCode,
                    c.CategoryName,
                    COUNT(a.ID) AS AssetCount,
                    SUM(CASE WHEN a.Status = 'ACTIVE' THEN 1 ELSE 0 END) AS ActiveCount,
                    SUM(a.PurchasePrice) AS TotalPurchasePrice,
                    SUM(a.AccumulatedDepreciation) AS TotalDepreciation,
                    SUM(a.BookValue) AS TotalBookValue
                FROM Asset_Category c
                LEFT JOIN Assets a ON c.ID = a.CategoryID
                WHERE c.Status = 1
                GROUP BY c.ID, c.CategoryCode, c.CategoryName
                ORDER BY c.CategoryCode";

            return code.DatabaseQuery(conn, query);
        }

        /// <summary>
        /// Get assets for accounting report
        /// </summary>
        public DataTable GetAssetsForAccounting(int year)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@Year", year }
            };

            string query = @"
                SELECT
                    a.AssetCode,
                    a.AssetName,
                    c.CategoryName,
                    c.AccountCode,
                    a.PurchaseDate,
                    a.PurchasePrice,
                    a.UsefulLifeYears,
                    a.ResidualValue,
                    a.MonthlyDepreciation,
                    ISNULL((SELECT SUM(DepreciationAmount) FROM Asset_Depreciation
                            WHERE AssetID = a.ID AND Year < @Year), 0) AS OpeningAccumulatedDepreciation,
                    ISNULL((SELECT SUM(DepreciationAmount) FROM Asset_Depreciation
                            WHERE AssetID = a.ID AND Year = @Year), 0) AS YearDepreciation,
                    a.AccumulatedDepreciation AS ClosingAccumulatedDepreciation,
                    a.BookValue,
                    a.Status
                FROM Assets a
                INNER JOIN Asset_Category c ON a.CategoryID = c.ID
                WHERE YEAR(a.PurchaseDate) <= @Year
                ORDER BY c.AccountCode, a.AssetCode";

            return code.DatabaseQuerySafe(conn, query, parameters);
        }

        /// <summary>
        /// Get assets for export to Excel (full details for accounting office and revenue department)
        /// รายละเอียดสินทรัพย์สำหรับส่งสำนักงานบัญชีและสรรพากร
        /// </summary>
        public DataTable GetAssetsForExport(int? year = null, string status = null, int? categoryId = null)
        {
            var parameters = new Dictionary<string, object>();
            string whereClause = "WHERE 1=1";

            if (year.HasValue)
            {
                whereClause += " AND YEAR(a.PurchaseDate) <= @Year";
                parameters.Add("@Year", year.Value);
            }

            if (!string.IsNullOrEmpty(status))
            {
                whereClause += " AND a.Status = @Status";
                parameters.Add("@Status", status);
            }

            if (categoryId.HasValue)
            {
                whereClause += " AND a.CategoryID = @CategoryID";
                parameters.Add("@CategoryID", categoryId.Value);
            }

            string query = $@"
                SELECT
                    ROW_NUMBER() OVER (ORDER BY c.CategoryCode, a.AssetCode) AS ลำดับ,
                    a.AssetCode AS รหัสสินทรัพย์,
                    a.AssetName AS ชื่อสินทรัพย์,
                    a.Brand AS ยี่ห้อ,
                    a.Model AS รุ่น,
                    a.SerialNumber AS หมายเลขซีเรียล,
                    c.CategoryCode AS รหัสหมวดหมู่,
                    c.CategoryName AS หมวดหมู่,
                    c.AccountCode AS รหัสบัญชี,
                    CONVERT(VARCHAR(10), a.PurchaseDate, 103) AS วันที่ซื้อ,
                    a.PurchasePrice AS ราคาซื้อ,
                    a.UsefulLifeYears AS อายุใช้งาน_ปี,
                    a.ResidualValue AS มูลค่าซาก,
                    a.MonthlyDepreciation AS ค่าเสื่อมรายเดือน,
                    a.AccumulatedDepreciation AS ค่าเสื่อมสะสม,
                    a.BookValue AS มูลค่าตามบัญชี,
                    a.Location AS สถานที่ตั้ง,
                    a.Department AS แผนก,
                    v.Name AS ผู้ขาย,
                    a.InvoiceNumber AS เลขที่ใบกำกับ,
                    a.PaymentVoucherID AS เลขที่ใบสำคัญจ่าย,
                    CONVERT(VARCHAR(10), a.WarrantyExpireDate, 103) AS วันหมดประกัน,
                    CASE a.Status
                        WHEN 'ACTIVE' THEN N'ใช้งานอยู่'
                        WHEN 'DISPOSED' THEN N'จำหน่ายแล้ว'
                        WHEN 'SOLD' THEN N'ขายแล้ว'
                        WHEN 'WRITTEN_OFF' THEN N'ตัดจำหน่าย'
                        ELSE a.Status
                    END AS สถานะ,
                    CONVERT(VARCHAR(10), a.DisposalDate, 103) AS วันที่จำหน่าย,
                    a.DisposalPrice AS ราคาจำหน่าย,
                    a.DisposalReason AS เหตุผลการจำหน่าย,
                    a.Notes AS หมายเหตุ
                FROM Asset a
                INNER JOIN Asset_Category c ON a.CategoryID = c.ID
                LEFT JOIN Vendor v ON a.VendorID = v.ID
                {whereClause}
                ORDER BY c.CategoryCode, a.AssetCode";

            if (parameters.Count > 0)
                return code.DatabaseQuerySafe(conn, query, parameters);
            else
                return code.DatabaseQuery(conn, query);
        }

        #endregion
    }
}
