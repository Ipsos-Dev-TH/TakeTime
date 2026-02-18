using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Take_Time_BangPhra.Admin
{
    public partial class ManageProductImagesPage : System.Web.UI.Page
    {
        private readonly string connectionString = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private ProductService productService;
        private ProductDataAccess productDataAccess;
        private code codeInstance = new code();

        private string ProductType
        {
            get { return Request.QueryString["type"] ?? ""; }
        }

        private int ProductId
        {
            get
            {
                int id;
                if (int.TryParse(Request.QueryString["id"], out id))
                    return id;
                return 0;
            }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            // Check admin authentication
            if (Session["UserID"] == null || (Session["permission"] != null ? Session["permission"].ToString() : "") != "True")
            {
                Response.Redirect("~/Admin/Login.aspx");
                return;
            }

            if (string.IsNullOrEmpty(ProductType) || ProductId == 0)
            {
                Response.Redirect("ProductImages.aspx");
                return;
            }

            productService = new ProductService(connectionString);
            productDataAccess = new ProductDataAccess(connectionString);

            if (!IsPostBack)
            {
                LoadProductName();
                LoadImages();
            }
        }

        private void LoadProductName()
        {
            try
            {
                string query = "";
                string nameColumn = "";

                if (ProductType == "PRODUCT")
                {
                    query = "SELECT Product_Name FROM Product WHERE ID = @id";
                    nameColumn = "Product_Name";
                }
                else if (ProductType == "ACCOMMODATION")
                {
                    query = "SELECT AccomName FROM Accommodation WHERE ID = @id";
                    nameColumn = "AccomName";
                }
                else if (ProductType == "ITEM")
                {
                    query = "SELECT ItemName FROM Items WHERE ID = @id";
                    nameColumn = "ItemName";
                }

                if (!string.IsNullOrEmpty(query))
                {
                    var parameters = new Dictionary<string, object>
                    {
                        { "@id", ProductId }
                    };

                    DataTable dt = codeInstance.DatabaseQuerySafe(connectionString, query, parameters);
                    if (dt.Rows.Count > 0)
                    {
                        lblProductName.Text = dt.Rows[0][nameColumn].ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError("เกิดข้อผิดพลาดในการโหลดข้อมูล: " + ex.Message);
            }
        }

        private void LoadImages()
        {
            try
            {
                DataTable dt = productDataAccess.GetProductImages(ProductType, ProductId);

                if (dt.Rows.Count > 0)
                {
                    rptImages.DataSource = dt;
                    rptImages.DataBind();
                    pnlNoImages.Visible = false;
                    lblImageCount.Text = dt.Rows.Count.ToString();
                }
                else
                {
                    rptImages.DataSource = null;
                    rptImages.DataBind();
                    pnlNoImages.Visible = true;
                    lblImageCount.Text = "0";
                }
            }
            catch (Exception ex)
            {
                ShowError("เกิดข้อผิดพลาดในการโหลดรูปภาพ: " + ex.Message);
                pnlNoImages.Visible = true;
            }
        }

        protected void btnUpload_Click(object sender, EventArgs e)
        {
            if (!fuImage.HasFile)
            {
                ShowError("กรุณาเลือกไฟล์รูปภาพ");
                return;
            }

            try
            {
                int adminId = Convert.ToInt32(Session["UserID"]);
                string caption = txtCaption.Text.Trim();
                bool isMainImage = chkIsMainImage.Checked;

                long imageId = productService.UploadProductImage(
                    ProductType,
                    ProductId,
                    fuImage.PostedFile,
                    caption,
                    isMainImage,
                    adminId
                );

                if (imageId > 0)
                {
                    ShowSuccess("อัพโหลดรูปภาพสำเร็จ!");
                    txtCaption.Text = "";
                    chkIsMainImage.Checked = false;
                    LoadImages();
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

        protected void btnAction_Command(object sender, CommandEventArgs e)
        {
            long imageId;
            if (!long.TryParse(e.CommandArgument.ToString(), out imageId))
                return;

            try
            {
                if (e.CommandName == "SetMain")
                {
                    // Unset all main images first
                    var unsetParams = new Dictionary<string, object>
                    {
                        { "@productType", ProductType },
                        { "@productId", ProductId }
                    };
                    codeInstance.DatabaseInsertSafe(connectionString,
                        "UPDATE Product_Images SET IsMainImage = 0 WHERE ProductType = @productType AND Product_ID = @productId",
                        unsetParams);

                    // Set this image as main
                    var setParams = new Dictionary<string, object>
                    {
                        { "@imageId", imageId }
                    };
                    codeInstance.DatabaseInsertSafe(connectionString,
                        "UPDATE Product_Images SET IsMainImage = 1 WHERE ID = @imageId",
                        setParams);

                    ShowSuccess("ตั้งเป็นรูปหลักสำเร็จ!");
                }
                else if (e.CommandName == "DeleteImage")
                {
                    productDataAccess.DeleteProductImage(imageId);
                    ShowSuccess("ลบรูปภาพสำเร็จ!");
                }

                LoadImages();
            }
            catch (Exception ex)
            {
                ShowError("เกิดข้อผิดพลาด: " + ex.Message);
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
