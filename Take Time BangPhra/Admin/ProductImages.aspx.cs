using System;
using System.Configuration;
using System.Data;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Take_Time_BangPhra.Admin
{
    public partial class ProductImagesPage : System.Web.UI.Page
    {
        private readonly string connectionString = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private ProductService productService;
        private ProductDataAccess productDataAccess;
        private code codeInstance = new code();

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Perm.Guard(this, Perm.WebContent)) return;   // กลุ่มสิทธิ์ไม่อนุญาตส่วนนี้
            // Check admin authentication
            if (Session["UserID"] == null || (Session["permission"] != null ? Session["permission"].ToString() : "") != "True")
            {
                Response.Redirect("~/Admin/Login.aspx");
                return;
            }

            productService = new ProductService(connectionString);
            productDataAccess = new ProductDataAccess(connectionString);

            if (!IsPostBack)
            {
                EnsureProductImagesTable();
                LoadProducts();
                LoadAccommodations();
                LoadItems();
            }
        }

        private void LoadProducts()
        {
            try
            {
                // Query to get products from Product table (Room Service products)
                // Product table uses Product_Name and Sell_Price columns;
                // stock is calculated from Product_In - Product_Out
                string query = @"
                    SELECT
                        p.ID AS ProductID,
                        p.Product_Name AS ProductName,
                        p.Sell_Price AS Price,
                        ISNULL((SELECT SUM(Amount) FROM Product_In WHERE Product_ID = p.ID), 0) -
                        ISNULL((SELECT SUM(Amount) FROM Product_Out WHERE Product_ID = p.ID), 0) AS Quantity,
                        (SELECT COUNT(*) FROM Product_Images pi2 WHERE pi2.Product_ID = p.ID AND pi2.ProductType = 'PRODUCT' AND pi2.Status = 1) AS TotalImages
                    FROM Product p
                    WHERE p.Status = 'True' OR p.Status = '1'
                    ORDER BY p.Product_Name";

                DataTable dt = codeInstance.DatabaseQuerySafe(connectionString, query, null);
                rptProducts.DataSource = dt;
                rptProducts.DataBind();
            }
            catch (Exception ex)
            {
                ShowError("เกิดข้อผิดพลาดในการโหลดข้อมูลสินค้า: " + ex.Message);
            }
        }

        private void LoadAccommodations()
        {
            try
            {
                // Query to get accommodations with image count
                // Accommodation table uses ID (not AccommodationID) and Status = 1 for active
                string query = @"
                    SELECT
                        a.ID AS AccommodationID,
                        a.AccomName,
                        a.Price,
                        (SELECT COUNT(*) FROM Product_Images pi2 WHERE pi2.Product_ID = a.ID AND pi2.ProductType = 'ACCOMMODATION' AND pi2.Status = 1) AS TotalImages
                    FROM Accommodation a
                    WHERE a.Status = 1
                    ORDER BY a.OrderID, a.AccomName";

                DataTable dt = codeInstance.DatabaseQuerySafe(connectionString, query, null);
                rptAccommodations.DataSource = dt;
                rptAccommodations.DataBind();
            }
            catch (Exception ex)
            {
                ShowError("เกิดข้อผิดพลาดในการโหลดข้อมูลที่พัก: " + ex.Message);
            }
        }

        private void LoadItems()
        {
            try
            {
                // Query to get items with image count
                // Items table uses ID (not ItemID)
                string query = @"
                    SELECT
                        i.ID AS ItemID,
                        i.ItemName,
                        i.Price,
                        (SELECT COUNT(*) FROM Product_Images pi2 WHERE pi2.Product_ID = i.ID AND pi2.ProductType = 'ITEM' AND pi2.Status = 1) AS TotalImages
                    FROM Items i
                    WHERE i.Status = 1
                    ORDER BY i.ItemName";

                DataTable dt = codeInstance.DatabaseQuerySafe(connectionString, query, null);
                rptItems.DataSource = dt;
                rptItems.DataBind();
            }
            catch (Exception ex)
            {
                ShowError("เกิดข้อผิดพลาดในการโหลดข้อมูลอุปกรณ์: " + ex.Message);
            }
        }

        protected void ddlProductType_SelectedIndexChanged(object sender, EventArgs e)
        {
            ddlProduct.Items.Clear();
            ddlProduct.Items.Add(new ListItem("-- เลือกสินค้า --", ""));

            string productType = ddlProductType.SelectedValue;

            if (string.IsNullOrEmpty(productType))
                return;

            try
            {
                DataTable dt;

                if (productType == "PRODUCT")
                {
                    // Load products for Room Service
                    string query = @"
                        SELECT ID, Product_Name
                        FROM Product
                        WHERE Status = 'True' OR Status = '1'
                        ORDER BY Product_Name";

                    dt = codeInstance.DatabaseQuerySafe(connectionString, query, null);

                    foreach (DataRow row in dt.Rows)
                    {
                        ddlProduct.Items.Add(new ListItem(
                            row["Product_Name"].ToString(),
                            row["ID"].ToString()
                        ));
                    }
                }
                else if (productType == "ACCOMMODATION")
                {
                    string query = @"
                        SELECT ID, AccomName
                        FROM Accommodation
                        WHERE Status = 1
                        ORDER BY OrderID, AccomName";

                    dt = codeInstance.DatabaseQuerySafe(connectionString, query, null);

                    foreach (DataRow row in dt.Rows)
                    {
                        ddlProduct.Items.Add(new ListItem(
                            row["AccomName"].ToString(),
                            row["ID"].ToString()
                        ));
                    }
                }
                else if (productType == "ITEM")
                {
                    string query = @"
                        SELECT ID, ItemName
                        FROM Items
                        WHERE Status = 1
                        ORDER BY ItemName";

                    dt = codeInstance.DatabaseQuerySafe(connectionString, query, null);

                    foreach (DataRow row in dt.Rows)
                    {
                        ddlProduct.Items.Add(new ListItem(
                            row["ItemName"].ToString(),
                            row["ID"].ToString()
                        ));
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError("เกิดข้อผิดพลาดในการโหลดรายการสินค้า: " + ex.Message);
            }
        }

        protected void btnUpload_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid)
                return;

            // Validate inputs
            if (string.IsNullOrEmpty(ddlProductType.SelectedValue))
            {
                ShowError("กรุณาเลือกประเภทสินค้า");
                return;
            }

            if (string.IsNullOrEmpty(ddlProduct.SelectedValue))
            {
                ShowError("กรุณาเลือกสินค้า");
                return;
            }

            if (!fuImage.HasFile)
            {
                ShowError("กรุณาเลือกไฟล์รูปภาพ");
                return;
            }

            try
            {
                string productType = ddlProductType.SelectedValue;
                int productId = int.Parse(ddlProduct.SelectedValue);
                string caption = txtCaption.Text.Trim();
                bool isMainImage = chkIsMainImage.Checked;
                var imageFile = fuImage.PostedFile;

                // Validate file type
                string fileExtension = System.IO.Path.GetExtension(imageFile.FileName).ToLower();
                if (fileExtension != ".jpg" && fileExtension != ".jpeg" && fileExtension != ".png")
                {
                    ShowError("กรุณาอัพโหลดไฟล์ JPG หรือ PNG เท่านั้น");
                    return;
                }

                // Validate file size (max 10MB)
                if (imageFile.ContentLength > 10 * 1024 * 1024)
                {
                    ShowError("ขนาดไฟล์ต้องไม่เกิน 10MB");
                    return;
                }

                // Get admin ID
                int adminId = Convert.ToInt32(Session["UserID"]);

                // Upload image via ProductService
                long imageId = productService.UploadProductImage(
                    productType,
                    productId,
                    imageFile,
                    caption,
                    isMainImage,
                    adminId
                );

                if (imageId > 0)
                {
                    ShowSuccess("อัพโหลดรูปภาพสำเร็จ!<br/>" +
                               "ID รูปภาพ: " + imageId + "<br/>" +
                               "ขนาดไฟล์: " + (imageFile.ContentLength / 1024.0).ToString("N2") + " KB");

                    // Reload product lists
                    LoadProducts();
                    LoadAccommodations();
                    LoadItems();

                    // Clear form
                    ddlProductType.SelectedIndex = 0;
                    ddlProduct.Items.Clear();
                    ddlProduct.Items.Add(new ListItem("-- เลือกสินค้า --", ""));
                    txtCaption.Text = "";
                    chkIsMainImage.Checked = false;
                }
                else
                {
                    ShowError("การอัพโหลดล้มเหลว");
                }
            }
            catch (Exception ex)
            {
                ShowError("เกิดข้อผิดพลาด: " + ex.Message);
            }
        }

        private void EnsureProductImagesTable()
        {
            try
            {
                string sql = @"
                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Product_Images')
                    BEGIN
                        CREATE TABLE Product_Images (
                            ID BIGINT IDENTITY(1,1) PRIMARY KEY,
                            ProductType NVARCHAR(50) NOT NULL,
                            Product_ID INT NOT NULL,
                            ImageURL NVARCHAR(500) NOT NULL,
                            ThumbnailURL NVARCHAR(500) NULL,
                            ImageOrder INT NOT NULL DEFAULT 0,
                            IsMainImage BIT NOT NULL DEFAULT 0,
                            Caption NVARCHAR(500) NULL,
                            UploadedBy_AdminID INT NULL,
                            Status INT NOT NULL DEFAULT 1,
                            CreatedDate DATETIME NOT NULL DEFAULT GETDATE()
                        );

                        CREATE INDEX IX_Product_Images_ProductType_ProductID
                            ON Product_Images (ProductType, Product_ID);
                    END

                    IF EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_Product_Images_ProductType')
                    BEGIN
                        ALTER TABLE Product_Images DROP CONSTRAINT CK_Product_Images_ProductType;
                    END";

                codeInstance.DatabaseInsertSafe(connectionString, sql, null);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("EnsureProductImagesTable Error: " + ex.Message);
            }
        }

        private void ShowSuccess(string message)
        {
            pnlSuccess.Visible = true;
            pnlError.Visible = false;
            lblSuccess.Text = message;
        }

        private void ShowError(string message)
        {
            pnlError.Visible = true;
            pnlSuccess.Visible = false;
            lblError.Text = message;
        }
    }
}
