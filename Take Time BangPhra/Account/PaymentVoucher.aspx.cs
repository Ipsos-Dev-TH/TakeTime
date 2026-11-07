using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Data;
using System.Configuration;
using System.IO;
using Microsoft.Reporting.WebForms;
using System.Globalization;
using iTextSharp.text.pdf.parser;
using Take_Time_BangPhra.Class;
using Take_Time_BangPhra;

namespace Take_Time_BangPhra.Account.Report
{
    public partial class PaymentVoucher : System.Web.UI.Page
    {

        _Default code = new _Default();
        string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        DocumentHelper documentHelper;
        AssetService assetService;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Perm.Guard(this, Perm.FinVoucher)) return;   // กลุ่มสิทธิ์ไม่อนุญาตส่วนนี้
            this.MaintainScrollPositionOnPostBack = true;
            documentHelper = new DocumentHelper(conn);
            assetService = new AssetService();
            try
            {
                if (Session["permission"].ToString() == "True" && (Session["User"].ToString() == "Owner" || Session["User"].ToString() == "Admin"))
                {

                }
                else
                {
                    Response.Redirect("/Default");
                }
            }
            catch
            {
                Response.Redirect("/Default");
            }

            // nonce ของ API/PvAssist.ashx (ตรวจใบซ้ำ + คำแนะนำ AI) — ผูกกับ session นี้
            if (!(Session["PvAssistNonce"] is string))
                Session["PvAssistNonce"] = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

            if (!IsPostBack)
            {
                DataTable dtUpload = new DataTable();
                try
                {
                    dtUpload.Columns.Add("Name");
                }
                catch
                {

                }
                Session["dtUpload"] = dtUpload;
                Session["dtExistingFiles"] = new List<string>(); // Initialize empty list for new vouchers

                DataTable dtPaidHow = code.DatabaseQuery(SqlDataSource2.ConnectionString, SqlDataSource2.SelectCommand);
                for (int i = 0; i < dtPaidHow.Rows.Count; i++)
                {
                    DropDownList2.Items.Add(new ListItem( dtPaidHow.Rows[i]["Paid_How"].ToString(), dtPaidHow.Rows[i]["ID"].ToString()));
                }
                DropDownList2.DataBind();

                DataTable dtPaidType = code.DatabaseQuery(SqlDataSource3.ConnectionString, SqlDataSource3.SelectCommand);
                for (int i = 0; i < dtPaidType.Rows.Count; i++)
                {
                    DropDownList3.Items.Add(new ListItem( dtPaidType.Rows[i]["Paid_Type"].ToString() , dtPaidType.Rows[i]["ID"].ToString()));
                    ddlLineCategory.Items.Add(new ListItem( dtPaidType.Rows[i]["Paid_Type"].ToString() , dtPaidType.Rows[i]["ID"].ToString()));
                }
                DropDownList3.DataBind();
                ddlLineCategory.DataBind();

                DataTable dtVatType = code.DatabaseQuery(SqlDataSource4.ConnectionString, SqlDataSource4.SelectCommand);
                for (int i = 0; i < dtVatType.Rows.Count; i++)
                {
                    DropDownList4.Items.Add(new ListItem( dtVatType.Rows[i]["Vat_Type"].ToString() , dtVatType.Rows[i]["ID"].ToString()));
                }
                DropDownList4.DataBind();

                DataTable dtVendor_Group = code.DatabaseQuery(SqlDataSource5.ConnectionString, SqlDataSource5.SelectCommand);
                for (int i = 0; i < dtVendor_Group.Rows.Count; i++)
                {
                    DropDownList5.Items.Add(new ListItem( dtVendor_Group.Rows[i]["Vendor_Group"].ToString() , dtVendor_Group.Rows[i]["Vendor_Group"].ToString()));
                }
                DropDownList5.DataBind();

                DataTable dtVendor = code.DatabaseQuery(SqlDataSource1.ConnectionString, SqlDataSource1.SelectCommand);
                for (int i = 0; i < dtVendor.Rows.Count; i++)
                {
                    DropDownList1.Items.Add(new ListItem( dtVendor.Rows[i]["Name"].ToString() , dtVendor.Rows[i]["ID"].ToString()));
                }
                DropDownList1.DataBind();

                // Load Asset Categories
                LoadAssetCategories();

                // โหลดผังบัญชีจริงจาก NextAcc (เหมือนหน้า OCR) — แหล่งจ่ายเงิน + ผังค่าใช้จ่าย
                LoadNexaaccPaidHowOptions();
                LoadNexaaccChargeOptions();

                // ตัวเลือกที่ผู้ใช้เห็น: "จ่ายจาก" (แหล่งเงิน + บัญชี NextAcc) และ "หมวดค่าใช้จ่าย" (หมวดเดิม + ผัง NextAcc)
                // → แปลงกลับเป็นค่าของ control เดิมก่อนโค้ดเพิ่มรายการ/บันทึกเดิมทำงาน (ApplyPayFromToLegacy / ApplyLineAccountToLegacy)
                LoadPayFromOptions(dtPaidHow);
                LoadLineAccountOptions(dtPaidType);
                StoreVatPercentMap(dtVatType);

                // ชำระค่าสินค้าที่รับเข้าสต๊อกแล้ว (GR/IR) — รายการรับของที่ยังไม่ผูกใบสำคัญจ่าย
                LoadGoodsReceiptOptions();

                // ใบรับรองแทนใบเสร็จรับเงิน — ค่าตั้งต้นผู้รับรอง (ผู้ใช้ที่ login) / พยาน (กรรมการ)
                InitCertInLieuDefaults();

                string command = Request.QueryString["command"];
                string uid = Request.QueryString["uid"];
                if (command == "edit")
                {
                    // SECURE: Get payment details with parameterized query
                    var paymentParams = new Dictionary<string, object>
                    {
                        { "@UID", uid ?? "" }
                    };
                    DataTable dtPayment = code.DatabaseQuerySafe(conn,
                        "SELECT * FROM Account_Payment WHERE UID = @UID",
                        paymentParams);
                    string id = dtPayment.Rows[0]["ID"].ToString();

                    // ✅ เก็บ id และ uid ไว้ใน ViewState เพื่อใช้ใน GetFileUrl
                    ViewState["PaymentID"] = id;
                    ViewState["PaymentUID"] = uid;

                    // SECURE: Get payment details with parameterized query
                    var detailParams = new Dictionary<string, object>
                    {
                        { "@PaymentID", id }
                    };
                    DataTable dtPaymentDetail;
                    try
                    {
                        dtPaymentDetail = code.DatabaseQuerySafe(conn,
                            "SELECT Number, Detail, Amount, ISNULL(Paid_Type_ID, 0) AS PaidTypeId, ISNULL(Paid_Type_Name, N'') AS PaidTypeName, ISNULL(CAST(Nexaacc_AccountId AS NVARCHAR(50)), N'') AS NexaaccAccountId FROM Account_Payment_Detail WHERE Payment_ID = @PaymentID",
                            detailParams);
                    }
                    catch
                    {
                        dtPaymentDetail = code.DatabaseQuerySafe(conn,
                            "SELECT Number, Detail, Amount FROM Account_Payment_Detail WHERE Payment_ID = @PaymentID",
                            detailParams);
                    }

                    // SECURE: Get vendor details with parameterized query
                    var vendorParams = new Dictionary<string, object>
                    {
                        { "@VendorID", dtPayment.Rows[0]["Vendor_ID"].ToString() }
                    };
                    DataTable dtVendorSelected = code.DatabaseQuerySafe(conn,
                        "SELECT * FROM Vendor WHERE ID = @VendorID",
                        vendorParams);

                    GridView1.DataSource = dtPaymentDetail;
                    GridView1.DataBind();

                    DateTime docdate = Convert.ToDateTime(dtPayment.Rows[0]["Created_Date"].ToString());
                    TextBox8.Text = docdate.ToString("yyyy-MM-dd");


                    TextBox3.Text = dtPayment.Rows[0]["Total_Amount_Exclude_Vat"].ToString();
                    TextBox4.Text = dtPayment.Rows[0]["Vat"].ToString();
                    TextBox6.Text = dtPayment.Rows[0]["Total_Amount"].ToString();

                    // Load WHT data if columns exist
                    if (dtPayment.Columns.Contains("WHT_Rate") && dtPayment.Rows[0]["WHT_Rate"] != DBNull.Value)
                    {
                        decimal savedWhtRate = Convert.ToDecimal(dtPayment.Rows[0]["WHT_Rate"]);
                        var whtItem = ddlWHTRate.Items.FindByValue(savedWhtRate.ToString("G"));
                        if (whtItem != null) ddlWHTRate.SelectedValue = whtItem.Value;
                    }
                    if (dtPayment.Columns.Contains("WHT_Amount") && dtPayment.Rows[0]["WHT_Amount"] != DBNull.Value)
                    {
                        txtWHTAmount.Text = Convert.ToDecimal(dtPayment.Rows[0]["WHT_Amount"]).ToString("N2");
                    }

                    // Load IsCredit flag
                    if (dtPayment.Columns.Contains("IsCredit") && dtPayment.Rows[0]["IsCredit"] != DBNull.Value)
                    {
                        chkIsCredit.Checked = Convert.ToBoolean(dtPayment.Rows[0]["IsCredit"]);
                    }

                    // ใบรับรองแทนใบเสร็จรับเงิน (ถ้าใบนี้เคยบันทึกไว้) — ต้องโหลดก่อน LoadNextAccReference (ใช้เลือกลิงก์)
                    LoadCertInLieu(dtPayment.Rows[0]);

                    // Load NextAcc document reference
                    LoadNextAccReference(id);

                    DropDownList2.SelectedIndex = DropDownList2.Items.IndexOf(DropDownList2.Items.FindByText(dtPayment.Rows[0]["Paid_How"].ToString()));
                    DropDownList2.DataBind();
                    SelectPayFromFromLegacy();
                    if (dtPayment.Columns.Contains("Supplier_Invoice_No") && dtPayment.Rows[0]["Supplier_Invoice_No"] != DBNull.Value)
                        txtSupplierInvoiceNo.Text = dtPayment.Rows[0]["Supplier_Invoice_No"].ToString();
                    DropDownList3.SelectedIndex = DropDownList3.Items.IndexOf(DropDownList3.Items.FindByText(dtPayment.Rows[0]["Paid_Type"].ToString()));
                    DropDownList3.DataBind();
                    DropDownList4.SelectedIndex = DropDownList4.Items.IndexOf(DropDownList4.Items.FindByValue(dtPayment.Rows[0]["Vat_Type_ID"].ToString()));
                    DropDownList4.DataBind();

                    DropDownList5.SelectedIndex = DropDownList5.Items.IndexOf(DropDownList5.Items.FindByText(dtVendorSelected.Rows[0]["Vendor_Group"].ToString()));
                    DropDownList5.DataBind();
                    DropDownList1.SelectedIndex = DropDownList1.Items.IndexOf(DropDownList1.Items.FindByValue(dtVendorSelected.Rows[0]["ID"].ToString()));
                    DropDownList1.DataBind();

                    string path = AppCfg.Get("PaymentFolderPath").ToString();
                    string paymentPath = path + "\\" + docdate.Year + "\\" + docdate.Month;
                    // Fallback: check padded month directory for files created with zero-padded month
                    if (!Directory.Exists(paymentPath))
                    {
                        string altPath = path + "\\" + docdate.Year + "\\" + docdate.Month.ToString().PadLeft(2, '0');
                        if (Directory.Exists(altPath))
                            paymentPath = altPath;
                    }

                    // Track existing files with full names (with prefix) for edit mode
                    List<string> existingFilesFullName = new List<string>();

                    if (Directory.Exists(paymentPath))
                    {
                        string[] dirs = Directory.GetFiles(paymentPath, id + "_" + uid + "*");
                        foreach (string file in dirs)
                        {
                            string fileName = System.IO.Path.GetFileName(file);
                            // Skip the main PDF file (id_uid.pdf)
                            if(fileName.Split('.')[0].Replace(id, "").Replace("_" + uid, "").Length > 0)
                            {
                                // Store display name (without prefix) for UI
                                string displayName = fileName.Split('.')[0].Replace(id+"_", "").Replace( uid+"_", "")+"."+ fileName.Split('.')[1];
                                dtUpload.Rows.Add(displayName);
                                // Store full file name for tracking existing files
                                existingFilesFullName.Add(fileName);
                            }

                        }
                    }
                    GridView2.DataSource = dtUpload;
                    GridView2.DataBind();

                    // Store existing files list to avoid re-renaming them
                    Session["dtExistingFiles"] = existingFilesFullName;
                    Session["dtUpload"] = dtUpload;

                    // Ensure per-line category columns exist (backward compat for pre-migration data)
                    if (!dtPaymentDetail.Columns.Contains("PaidTypeId"))
                        dtPaymentDetail.Columns.Add("PaidTypeId");
                    if (!dtPaymentDetail.Columns.Contains("PaidTypeName"))
                        dtPaymentDetail.Columns.Add("PaidTypeName");
                    if (!dtPaymentDetail.Columns.Contains("NexaaccAccountId"))
                        dtPaymentDetail.Columns.Add("NexaaccAccountId");
                    if (!dtPaymentDetail.Columns.Contains("AccountCode"))
                        dtPaymentDetail.Columns.Add("AccountCode");

                    // For old data without per-line categories, fill from header category
                    for (int i = 0; i < dtPaymentDetail.Rows.Count; i++)
                    {
                        if (string.IsNullOrEmpty(dtPaymentDetail.Rows[i]["PaidTypeName"]?.ToString()))
                        {
                            dtPaymentDetail.Rows[i]["PaidTypeName"] = DropDownList3.SelectedItem?.Text ?? "";
                            int ptId;
                            dtPaymentDetail.Rows[i]["PaidTypeId"] = int.TryParse(DropDownList3.SelectedValue, out ptId) ? (object)ptId : DBNull.Value;
                        }
                    }

                    Session["dtDetail"] = dtPaymentDetail;

                    // การรับของที่ผูกกับใบนี้ (แก้ไข = ลบแล้วบันทึกใหม่ → ต้องผูกซ้ำตอนบันทึก)
                    RestoreLinkedGoodsReceipts(id);

                    // Check if there's an asset linked to this payment voucher
                    LoadExistingAssetData(id);

                }
                DataTable dtDetail = new DataTable();
                try
                {
                    dtDetail.Columns.Add("Number");
                    dtDetail.Columns.Add("Detail");
                    dtDetail.Columns.Add("Amount");
                    dtDetail.Columns.Add("PaidTypeId");
                    dtDetail.Columns.Add("PaidTypeName");
                    dtDetail.Columns.Add("NexaaccAccountId");
                    dtDetail.Columns.Add("AccountCode");
                }
                catch
                {

                }
                if (command == "edit")
                {
                    // Set voucher number from existing ID (if not being edited)
                    if (!chkEditVoucherNo.Checked)
                    {
                        var getIdParams = new Dictionary<string, object>
                        {
                            { "@UID", Request.QueryString["uid"] ?? "" }
                        };
                        string existingId = code.DatabaseQuerySafe(conn,
                            "SELECT ID FROM [Account_Payment] WHERE UID = @UID",
                            getIdParams).Rows[0][0].ToString();
                        txtVoucherNo.Text = existingId;
                    }
                }
                else
                {
                    Session["dtDetail"] = dtDetail;
                    TextBox8.Text = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

                    // Generate voucher number for new voucher
                    DateTime docDate = DateTime.Now;
                    string docNum = documentHelper.CreateDocumentNumber("Account_Payment", "PAY", docDate);
                    txtVoucherNo.Text = docNum;

                    // มาจากหน้า นำเข้าสินค้า (จ่ายแล้ว/ซื้อเชื่อ) หรือปุ่ม "ใบสำคัญจ่าย" ของการรับของ → กรอกให้
                    string grn = Request.QueryString["grn"];
                    if (!string.IsNullOrEmpty(grn))
                        PrefillFromGoodsReceipt(grn, Request.QueryString["ph"], Request.QueryString["credit"] == "1");
                }
                try
                {
                    var vendorNames = new List<string>();
                    DataTable dt = code.DatabaseQuery(conn, "SELECT Distinct([Name]) as Material_Name FROM [Taketime].[dbo].[Vendor] Where [Status] = 'True'");
                    for (int i = 0; i < dt.Rows.Count; i++)
                    {
                        vendorNames.Add(dt.Rows[i][0].ToString());
                    }
                    var jsSerializer = new System.Web.Script.Serialization.JavaScriptSerializer();
                    string jsonArray = jsSerializer.Serialize(vendorNames);
                    Literal1.Text = "<script> var Material_Name = " + jsonArray + ";\r\nautocomplete(document.getElementById(\"MainContent_TextBox9\"), Material_Name);</script>";
                }
                catch { }

                // แจ้งบันทึกสำเร็จ (server-side redirect จาก Button3 → ?saved=1) + สถานะ sync ไป NextAcc
                if (Request.QueryString["saved"] == "1")
                {
                    string s = Request.QueryString["s"] ?? "";
                    string syncMsg, kind = "ok";
                    if (s == "queued") syncMsg = "ส่งเข้าคิวไป NextAcc แล้ว — ดูเลขเอกสาร/สถานะได้ที่หน้า ตรวจสอบใบสำคัญจ่าย";
                    else if (s == "off") { syncMsg = "ยังไม่ได้เปิด/ตั้งค่า NextAcc — ใบนี้บันทึกในระบบเท่านั้น"; kind = "info"; }
                    else { syncMsg = "แต่ส่งเข้าคิว NextAcc ไม่สำเร็จ — ตรวจการตั้งค่าหรือหน้าคิว แล้วกดส่งใหม่"; kind = "warn"; }
                    string no = Request.QueryString["no"] ?? "";
                    litSavedMsg.Text = "<div class='pv-msg pv-msg-" + kind + "' role='status'><b>บันทึกใบสำคัญจ่าย"
                        + (no.Length > 0 ? " " + HttpUtility.HtmlEncode(no) : "") + " แล้ว</b>" + HttpUtility.HtmlEncode(syncMsg)
                        + " · <a href='/Account/CheckPayment_New'>ไปหน้าตรวจสอบใบสำคัญจ่าย</a></div>";
                }

            }

        }

        protected void Button2_Click(object sender, EventArgs e)
        {
            SetVatLabel();

            // "หมวดค่าใช้จ่าย" ตัวเดียว → ค่าของ control เดิม (หมวดเดิม หรือ ผังบัญชี NextAcc โดยตรง อย่างใดอย่างหนึ่ง)
            ApplyLineAccountToLegacy();
            bool pickedNexaaccCharge = !string.IsNullOrEmpty(ddlLineChargeNexaacc.SelectedValue);
            if (string.IsNullOrEmpty(ddlLineCategory.SelectedValue) && !pickedNexaaccCharge)
            {
                ShowMsg(litLineMsg, "err", "เลือก \"หมวดค่าใช้จ่าย\" ของรายการนี้ก่อน", null);
                return;
            }

            decimal lineAmountCheck;
            if (TextBox1.Text.Trim().Length < 2)
            {
                ShowMsg(litLineMsg, "err", "กรอกรายละเอียดค่าใช้จ่าย (อย่างน้อย 2 ตัวอักษร)", null);
                return;
            }
            if (TextBox2.Text.Length == 0 || !decimal.TryParse(TextBox2.Text, out lineAmountCheck) || lineAmountCheck == 0m)
            {
                ShowMsg(litLineMsg, "err", "กรอกจำนวนเงินเป็นตัวเลข เช่น 1500 หรือ 1500.50", null);
                return;
            }

            if (TextBox1.Text.Length > 1 && TextBox2.Text.Length > 0)
            {
                DataTable dtDetail = (DataTable)Session["dtDetail"];
                if (!dtDetail.Columns.Contains("PaidTypeId"))
                {
                    dtDetail.Columns.Add("PaidTypeId");
                    dtDetail.Columns.Add("PaidTypeName");
                    dtDetail.Columns.Add("NexaaccAccountId");
                }
                if (!dtDetail.Columns.Contains("AccountCode"))
                    dtDetail.Columns.Add("AccountCode");

                // เลือกผังบัญชี NextAcc โดยตรง → code/GUID/ชื่อบัญชีนั้นตรง ๆ (เลี่ยง mapping) · หมวดเดิม → GUID ตาม mapping ของหมวด
                // (ตรรกะเดิมของปุ่มนี้ ย้ายไป PaymentVoucherForm.BuildLineRow — ค่าที่เก็บเท่าเดิม)
                var lineRow = Integration.PaymentVoucherForm.BuildLineRow(
                    ddlLineCategory.SelectedValue, ddlLineCategory.SelectedItem?.Text,
                    ddlLineChargeNexaacc.SelectedValue, ddlLineChargeNexaacc.SelectedItem?.Text,
                    LookupNexaaccByCode,
                    name => new Integration.AccountingSyncService(conn).LookupPaidTypeAccountId(name));

                dtDetail.Rows.Add(dtDetail.Rows.Count + 1, TextBox1.Text,
                    NumberHelper.TwoDecimalPoints(Convert.ToDecimal(TextBox2.Text)),
                    lineRow.PaidTypeId, lineRow.PaidTypeName, lineRow.NexaaccAccountId, lineRow.AccountCode);
                Session["dtDetail"] = (DataTable)dtDetail;
                GridView1.DataSource = dtDetail;
                GridView1.DataBind();
                calAmount(dtDetail, DropDownList4.SelectedValue);
                TextBox1.Text = "";
                TextBox2.Text = "";
                AutoSelectHeaderPaidType();
                if (DropDownList4.SelectedIndex <= 0)
                    ShowMsg(litLineMsg, "info", "เพิ่มรายการแล้ว — เลือกประเภทภาษี (ขั้นที่ 4) เพื่อคำนวณ VAT", null);
            }
        }

        public void calAmount(DataTable dtDetail,string vatType)
        {
            decimal totalAmount = 0m;
            for(int i=0;i<dtDetail.Rows.Count;i++)
            {
                totalAmount += Convert.ToDecimal(dtDetail.Rows[i]["Amount"].ToString());
            }

            // SECURE: Get VAT percent with parameterized query
            var vatParams = new Dictionary<string, object>
            {
                { "@VatTypeID", DropDownList4.SelectedValue }
            };
            // ยังไม่เลือกประเภทภาษี (ฟอร์มใหม่: ภาษีอยู่ขั้นที่ 4 หลังรายการ) = 0% ชั่วคราว — เลือกแล้วคำนวณใหม่ (DropDownList4_SelectedIndexChanged)
            DataTable dtVatPct = DropDownList4.SelectedIndex > 0
                ? code.DatabaseQuerySafe(conn, "SELECT Vat_Percent FROM Account_Vat_Type WHERE Status = 'True' AND ID = @VatTypeID", vatParams)
                : null;
            int vatPercent = dtVatPct != null && dtVatPct.Rows.Count > 0 ? Convert.ToInt32(dtVatPct.Rows[0][0].ToString()) : 0;
            decimal vat = (totalAmount * vatPercent) / 100m;
            decimal AmountIncludeVat = 0m;
            if (vatType == "2")
            {
                // VAT included: line items sum is VAT-inclusive total
                decimal preVat = totalAmount / (1m + vatPercent / 100m);
                vat = totalAmount - preVat;
                AmountIncludeVat = totalAmount;
                totalAmount = preVat; // override: TextBox3 shows pre-VAT
            }
            else
            {
                AmountIncludeVat = totalAmount + vat;
            }

            TextBox3.Text = (NumberHelper.TwoDecimalPoints(totalAmount)).ToString();
            TextBox4.Text = (NumberHelper.TwoDecimalPoints(vat)).ToString();
            TextBox6.Text = (NumberHelper.TwoDecimalPoints(AmountIncludeVat)).ToString();
            RecalcWHTAmount();
        }

        protected void GridView1_RowDeleting(object sender, GridViewDeleteEventArgs e)
        {
            DataTable dtDetail = (DataTable)Session["dtDetail"];
            dtDetail.Rows[e.RowIndex].Delete();
            dtDetail.AcceptChanges();
            for(int i = 0;i<dtDetail.Rows.Count;i++)
            {
                dtDetail.Rows[i][0] = i + 1;
            }
            Session["dtDetail"] = (DataTable)dtDetail;
            GridView1.DataSource = dtDetail;
            GridView1.DataBind();
            calAmount(dtDetail,DropDownList4.SelectedValue);
        }

        protected void DropDownList4_SelectedIndexChanged(object sender, EventArgs e)
        {
            SetVatLabel();
            // เปลี่ยนประเภทภาษีหลังเพิ่มรายการ → คำนวณยอดใหม่ (ยกเว้นโหมดแก้ยอดเอง — ไม่ทับยอดที่ผู้ใช้กรอก)
            DataTable dtDetail = Session["dtDetail"] as DataTable;
            if (!CheckBox1.Checked && dtDetail != null && dtDetail.Rows.Count > 0)
                calAmount(dtDetail, DropDownList4.SelectedValue);
        }

        /// <summary>ป้ายแถว VAT ในสรุปยอด = ชื่อประเภทภาษีที่เลือก (เดิม Label1 = DropDownList4.SelectedItem.Text)</summary>
        private void SetVatLabel()
        {
            Label1.Text = DropDownList4.SelectedIndex > 0 ? DropDownList4.SelectedItem.Text : "ภาษีมูลค่าเพิ่ม";
        }

        protected void DropDownList3_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (DropDownList3.SelectedIndex > 0)
            {
                var item = ddlLineCategory.Items.FindByValue(DropDownList3.SelectedValue);
                if (item != null)
                    ddlLineCategory.SelectedValue = DropDownList3.SelectedValue;
                var li = ddlLineAccount.Items.FindByValue(Integration.PaymentVoucherForm.PaidTypePrefix + DropDownList3.SelectedValue);
                if (li != null && string.IsNullOrEmpty(ddlLineAccount.SelectedValue))
                    ddlLineAccount.SelectedValue = li.Value;
            }
        }

        protected void ddlWHTRate_SelectedIndexChanged(object sender, EventArgs e)
        {
            RecalcWHTAmount();
        }

        private void RecalcWHTAmount()
        {
            decimal whtRate = 0;
            decimal.TryParse(ddlWHTRate.SelectedValue, out whtRate);
            decimal amountExVat = 0;
            decimal.TryParse(TextBox3.Text, out amountExVat);
            decimal whtAmount = Math.Round(amountExVat * whtRate / 100, 2);
            txtWHTAmount.Text = whtAmount.ToString("N2");
        }

        // ──────────────────────────────────────────────
        // ผังบัญชีจาก NextAcc โดยตรง (เหมือนหน้า OCR) — เลี่ยงปัญหา mapping ผิด
        // ──────────────────────────────────────────────

        /// <summary>ดึงผังบัญชีจาก cache NextAcc (Accounting_Nexaacc_Accounts) ตาม code filter.
        /// codeFilter เป็นค่าคงที่ในโค้ด (ไม่ใช่ user input). คืน null ถ้ายังไม่ได้ Sync ผังบัญชี</summary>
        private DataTable LoadNexaaccAccounts(string codeFilter)
        {
            try
            {
                return code.DatabaseQuerySafe(conn,
                    "SELECT Nexaacc_AccountId, Account_Code, Account_Name FROM Accounting_Nexaacc_Accounts " +
                    "WHERE ISNULL(Is_Active, 1) = 1 AND (" + codeFilter + ") ORDER BY Account_Code", null);
            }
            catch { return null; }
        }

        /// <summary>แหล่งจ่ายเงิน — ผังบัญชีจริงจาก NextAcc (11x เงินสด/ธนาคาร + 21x เจ้าหนี้กรรมการ), value = Nexaacc_AccountId</summary>
        private void LoadNexaaccPaidHowOptions()
        {
            ddlPaidHowNexaacc.Items.Clear();
            ddlPaidHowNexaacc.Items.Add(new ListItem("— ใช้ตามวิธีจ่ายเงิน (mapping) —", ""));
            var na = LoadNexaaccAccounts("Account_Code LIKE '11%' OR Account_Code LIKE '21%'");
            if (na != null)
                foreach (DataRow r in na.Rows)
                    ddlPaidHowNexaacc.Items.Add(new ListItem(
                        ($"{r["Account_Code"]} {r["Account_Name"]}").Trim(),
                        r["Nexaacc_AccountId"]?.ToString() ?? ""));
        }

        /// <summary>ผังบัญชีค่าใช้จ่าย — ผังบัญชีจริงจาก NextAcc (5x ค่าใช้จ่าย + 12x สินทรัพย์ถาวร), value = Account_Code</summary>
        private void LoadNexaaccChargeOptions()
        {
            ddlLineChargeNexaacc.Items.Clear();
            ddlLineChargeNexaacc.Items.Add(new ListItem("— ใช้หมวดค่าใช้จ่าย (mapping) —", ""));
            // 5x ค่าใช้จ่าย + 12x สินทรัพย์ถาวร + เจ้าหนี้/เงินทดรองกรรมการ (2x ชื่อมี "กรรมการ")
            // → รองรับเคส "คืนเงินทดรองกรรมการ" (Dr เจ้าหนี้กรรมการ / Cr เงินสด-ธนาคาร)
            var na = LoadNexaaccAccounts(
                "Account_Code LIKE '5%' OR Account_Code LIKE '12%' OR (Account_Code LIKE '2%' AND Account_Name LIKE N'%กรรมการ%')");
            if (na != null)
                foreach (DataRow r in na.Rows)
                {
                    string c = r["Account_Code"]?.ToString() ?? "";
                    string n = r["Account_Name"]?.ToString() ?? "";
                    ddlLineChargeNexaacc.Items.Add(new ListItem($"{c} {n}".Trim(), c));
                }
        }

        /// <summary>หา Nexaacc_AccountId (GUID) + ชื่อบัญชี จาก Account_Code ใน cache. คืน (id, name)</summary>
        private Tuple<string, string> LookupNexaaccByCode(string accountCode)
        {
            try
            {
                var dt = code.DatabaseQuerySafe(conn,
                    "SELECT TOP 1 Nexaacc_AccountId, Account_Name FROM Accounting_Nexaacc_Accounts WHERE Account_Code = @c",
                    new Dictionary<string, object> { { "@c", accountCode ?? "" } });
                if (dt != null && dt.Rows.Count > 0)
                    return Tuple.Create(dt.Rows[0]["Nexaacc_AccountId"]?.ToString(), dt.Rows[0]["Account_Name"]?.ToString());
            }
            catch { }
            return Tuple.Create<string, string>(null, null);
        }

        // ──────────────────────────────────────────────
        // ฟอร์มใหม่ (CLAUDE.md ข้อ 3b): "จ่ายจาก" + "หมวดค่าใช้จ่าย" ตัวเดียว → ค่าของ control เดิม · ข้อความในหน้าแทน alert
        // ──────────────────────────────────────────────

        private bool _scrollToMsg;

        private static string Col(DataRow r, string col)
            => r.Table.Columns.Contains(col) && r[col] != DBNull.Value ? (r[col]?.ToString() ?? "").Trim() : "";

        /// <summary>"จ่ายจาก" = แหล่งเงินทุกแถว (พร้อมบัญชี NextAcc ที่ผูก) + บัญชี 11x/21x ในผัง NextAcc ที่ยังไม่มีแหล่งเงินผูก</summary>
        private void LoadPayFromOptions(DataTable dtPaidHow)
        {
            ddlPayFrom.Items.Clear();
            ddlPayFrom.Items.Add(new ListItem("— เลือกแหล่งเงินที่จ่าย —", ""));
            var na = LoadNexaaccAccounts("Account_Code LIKE '11%' OR Account_Code LIKE '21%'");
            var byId = new Dictionary<string, DataRow>(StringComparer.OrdinalIgnoreCase);
            var byCode = new Dictionary<string, DataRow>(StringComparer.OrdinalIgnoreCase);
            if (na != null)
                foreach (DataRow a in na.Rows)
                {
                    string gid = Col(a, "Nexaacc_AccountId"), c = Col(a, "Account_Code");
                    if (gid.Length > 0 && !byId.ContainsKey(gid)) byId[gid] = a;
                    if (c.Length > 0 && !byCode.ContainsKey(c)) byCode[c] = a;
                }
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (dtPaidHow != null)
                foreach (DataRow r in dtPaidHow.Rows)
                {
                    string gid = Col(r, "Nexaacc_AccountId"), accCode = Col(r, "Nexaacc_AccountCode"), accName = null;
                    DataRow a = null;
                    if ((gid.Length > 0 && byId.TryGetValue(gid, out a)) || (accCode.Length > 0 && byCode.TryGetValue(accCode, out a)))
                    {
                        accName = Col(a, "Account_Name");
                        if (accCode.Length == 0) accCode = Col(a, "Account_Code");
                        used.Add(Col(a, "Nexaacc_AccountId"));
                    }
                    ddlPayFrom.Items.Add(new ListItem(
                        Integration.PaymentVoucherForm.PayFromLabel(r["Paid_How"]?.ToString(), accCode, accName),
                        Integration.PaymentVoucherForm.PaidHowPrefix + r["ID"]));
                }
            if (na != null)
                foreach (DataRow a in na.Rows)
                {
                    string gid = Col(a, "Nexaacc_AccountId");
                    if (gid.Length == 0 || !used.Add(gid)) continue;
                    ddlPayFrom.Items.Add(new ListItem(
                        Integration.PaymentVoucherForm.AccountOnlyLabel(Col(a, "Account_Code"), Col(a, "Account_Name")),
                        Integration.PaymentVoucherForm.AccountPrefix + gid));
                }
        }

        /// <summary>"หมวดค่าใช้จ่าย" = หมวดเดิม (พร้อมบัญชีที่ผูก) + บัญชีค่าใช้จ่าย/สินทรัพย์/เจ้าหนี้กรรมการในผัง NextAcc ที่ไม่มีหมวดผูก
        /// (ชุดเดียวกับ ddlLineChargeNexaacc)</summary>
        private void LoadLineAccountOptions(DataTable dtPaidType)
        {
            ddlLineAccount.Items.Clear();
            ddlLineAccount.Items.Add(new ListItem("— เลือกหมวดค่าใช้จ่าย —", ""));
            var codeName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (ListItem li in ddlLineChargeNexaacc.Items)
                if (!string.IsNullOrEmpty(li.Value) && !codeName.ContainsKey(li.Value))
                    codeName[li.Value] = li.Text.StartsWith(li.Value + " ") ? li.Text.Substring(li.Value.Length + 1) : li.Text;
            var mappedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (dtPaidType != null)
                foreach (DataRow r in dtPaidType.Rows)
                {
                    string accCode = Col(r, "Nexaacc_AccountCode"), accName;
                    if (accCode.Length > 0) mappedCodes.Add(accCode);
                    codeName.TryGetValue(accCode, out accName);
                    if (accName == null && accCode.Length > 0)
                        accName = LookupNexaaccByCode(accCode).Item2;
                    ddlLineAccount.Items.Add(new ListItem(
                        Integration.PaymentVoucherForm.LineAccountLabel(r["Paid_Type"]?.ToString(), accCode, accName),
                        Integration.PaymentVoucherForm.PaidTypePrefix + r["ID"]));
                }
            foreach (ListItem li in ddlLineChargeNexaacc.Items)
            {
                if (string.IsNullOrEmpty(li.Value) || mappedCodes.Contains(li.Value)) continue;
                ddlLineAccount.Items.Add(new ListItem(li.Text, Integration.PaymentVoucherForm.AccountPrefix + li.Value));
            }
        }

        /// <summary>Account_Vat_Type.ID → อัตรา % (ให้สคริปต์ส่งอัตราปัจจุบันไปถาม AI)</summary>
        private void StoreVatPercentMap(DataTable dtVatType)
        {
            var map = new Dictionary<string, int>();
            if (dtVatType != null)
                foreach (DataRow r in dtVatType.Rows)
                {
                    int pct;
                    if (int.TryParse(Col(r, "Vat_Percent"), out pct)) map[r["ID"].ToString()] = pct;
                }
            ViewState["VatPctJson"] = new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(map);
        }

        /// <summary>แหล่งเงินเดิมที่เลือกใน DropDownList2 (โหมดแก้ไข/เปิดจากหน้ารับของ) → "จ่ายจาก"</summary>
        private void SelectPayFromFromLegacy()
        {
            if (DropDownList2.SelectedIndex <= 0) return;
            var li = ddlPayFrom.Items.FindByValue(Integration.PaymentVoucherForm.PayFromValueForPaidHow(DropDownList2.SelectedValue));
            if (li != null) ddlPayFrom.SelectedValue = li.Value;
        }

        /// <summary>"จ่ายจาก" → DropDownList2 (ข้อความ Paid_How ที่บันทึก/ส่งคิว) + ddlPaidHowNexaacc (บัญชีบังคับ; "" = mapping)</summary>
        private void ApplyPayFromToLegacy()
        {
            var c = Integration.PaymentVoucherForm.ResolvePayFrom(ddlPayFrom.SelectedValue, DropDownList2.SelectedValue);
            if (c.PaidHowId.Length > 0 && DropDownList2.Items.FindByValue(c.PaidHowId) != null)
                DropDownList2.SelectedValue = c.PaidHowId;
            else
                DropDownList2.SelectedIndex = 0;

            if (c.AccountOverride.Length > 0)
            {
                if (ddlPaidHowNexaacc.Items.FindByValue(c.AccountOverride) == null)
                    ddlPaidHowNexaacc.Items.Add(new ListItem(c.AccountOverride, c.AccountOverride));
                ddlPaidHowNexaacc.SelectedValue = c.AccountOverride;
            }
            else if (ddlPaidHowNexaacc.Items.Count > 0)
                ddlPaidHowNexaacc.SelectedIndex = 0;   // "— ใช้ตามวิธีจ่ายเงิน (mapping) —" (value "")
        }

        /// <summary>"หมวดค่าใช้จ่าย" → ddlLineCategory (หมวดเดิม) หรือ ddlLineChargeNexaacc (ผัง NextAcc) อย่างใดอย่างหนึ่ง</summary>
        private void ApplyLineAccountToLegacy()
        {
            var c = Integration.PaymentVoucherForm.ResolveLineAccount(ddlLineAccount.SelectedValue);
            if (c.PaidTypeId.Length > 0 && ddlLineCategory.Items.FindByValue(c.PaidTypeId) != null)
                ddlLineCategory.SelectedValue = c.PaidTypeId;
            else if (ddlLineCategory.Items.Count > 0)
                ddlLineCategory.SelectedIndex = 0;

            if (c.ChargeCode.Length > 0 && ddlLineChargeNexaacc.Items.FindByValue(c.ChargeCode) != null)
                ddlLineChargeNexaacc.SelectedValue = c.ChargeCode;
            else if (ddlLineChargeNexaacc.Items.Count > 0)
                ddlLineChargeNexaacc.SelectedIndex = 0;
        }

        /// <summary>หมวดหลักของใบ (DropDownList3 → Account_Payment.Paid_Type) ยังไม่เลือก = หมวดของรายการแรก</summary>
        private void AutoSelectHeaderPaidType()
        {
            if (DropDownList3.SelectedIndex > 0) return;
            string idv = Integration.PaymentVoucherForm.DeriveHeaderPaidTypeId(Session["dtDetail"] as DataTable, PaidTypeIdByAccountCode);
            if (idv != null && DropDownList3.Items.FindByValue(idv) != null)
                DropDownList3.SelectedValue = idv;
        }

        private string PaidTypeIdByAccountCode(string accountCode)
        {
            try
            {
                var dt = code.DatabaseQuerySafe(conn,
                    "SELECT TOP 2 ID FROM Account_Paid_Type WHERE Status = 'True' AND Nexaacc_AccountCode = @c",
                    new Dictionary<string, object> { { "@c", accountCode ?? "" } });
                return dt != null && dt.Rows.Count == 1 ? dt.Rows[0]["ID"].ToString() : null;
            }
            catch { return null; }
        }

        /// <summary>ช่องบังคับ (เดิมตรวจรวมเป็น "กรุณาระบุข้อมูลให้ครบถ้วน" หลังลบใบเดิม) — คืนข้อความ หรือ null</summary>
        private string RequiredFieldsError()
        {
            int vendorId;
            if (!int.TryParse(DropDownList1.SelectedValue, out vendorId))
                return "เลือกผู้ขาย / ผู้รับเงิน (ขั้นที่ 1)";
            DateTime d;
            if (!DateTime.TryParse(TextBox8.Text, out d))
                return "เลือกวันที่ใบสำคัญจ่าย (ขั้นที่ 1)";
            DataTable dtDetail = Session["dtDetail"] as DataTable;
            if (dtDetail == null || dtDetail.Rows.Count == 0)
                return "เพิ่มรายการค่าใช้จ่ายอย่างน้อย 1 รายการ (ขั้นที่ 2)";
            if (DropDownList3.SelectedIndex <= 0)
                return "เลือก \"หมวดหลักของใบ\" ใต้ตารางรายการ (ขั้นที่ 2) — ระบบเลือกให้ไม่ได้เพราะรายการใช้บัญชี NextAcc ที่ยังไม่มีหมวด";
            if (DropDownList2.SelectedIndex <= 0)
                return (ddlPayFrom.SelectedValue ?? "").StartsWith(Integration.PaymentVoucherForm.AccountPrefix)
                    ? "เลือก \"บันทึกในระบบเป็นวิธีจ่าย\" ใต้ช่องจ่ายจาก (ขั้นที่ 3)"
                    : "เลือก \"จ่ายจาก\" (ขั้นที่ 3)";
            if (DropDownList4.SelectedIndex <= 0)
                return "เลือกประเภทภาษีมูลค่าเพิ่ม (ขั้นที่ 4)";
            if (TextBox6.Text.Length == 0)
                return "ยังไม่มียอดจ่ายสุทธิ — เพิ่มรายการ หรือติ๊ก \"แก้ไขยอดเงินด้วยตนเอง\" แล้วกรอกยอด";
            return null;
        }

        /// <summary>ตรวจยอดรายการ/สมการบัญชี/หมวดต่อบรรทัด (ตรรกะเดิมของปุ่มบันทึก — ย้ายมาตรวจก่อนลบใบเดิม). false = แสดงข้อความแล้ว</summary>
        private bool ValidateLinesAndTotals()
        {
            DataTable dtDetail = (DataTable)Session["dtDetail"];

            // Validate: line amounts sum matches total (before VAT)
            decimal lineSum = 0m;
            bool allLinesHaveCategory = true;
            for (int i = 0; i < dtDetail.Rows.Count; i++)
            {
                lineSum += Convert.ToDecimal(dtDetail.Rows[i]["Amount"]);
                if (dtDetail.Columns.Contains("PaidTypeName"))
                {
                    string catName = dtDetail.Rows[i]["PaidTypeName"]?.ToString();
                    if (string.IsNullOrEmpty(catName))
                        allLinesHaveCategory = false;
                }
            }

            decimal displayedTotal;
            decimal.TryParse(TextBox3.Text, out displayedTotal);
            if (Math.Abs(lineSum - displayedTotal) > 0.01m && !CheckBox1.Checked)
            {
                ShowFormError($"ยอดรวมรายการ ({lineSum:N2}) ไม่ตรงกับยอดก่อนภาษี ({displayedTotal:N2}) — ลบแล้วเพิ่มรายการใหม่ หรือติ๊ก \"แก้ไขยอดเงินด้วยตนเอง\"");
                return false;
            }

            // Centralized accounting validation: Subtotal + VAT - WHT = Net Payable
            decimal vSubtotal = decimal.TryParse(TextBox3.Text, out var s) ? s : 0;
            decimal vVat = decimal.TryParse(TextBox4.Text, out var v) ? v : 0;
            decimal vWht = decimal.TryParse(txtWHTAmount.Text, out var w) ? w : 0;
            decimal vNet = decimal.TryParse(TextBox6.Text, out var n) ? n : 0;
            decimal vWhtRate = decimal.TryParse(ddlWHTRate.SelectedValue, out var wr) ? wr : 0;

            var validationResults = new List<AccountingArithmeticValidator.ValidationResult>();

            // WHT auto-calc check (wht == subtotal×rate): ข้ามได้ในโหมด "แก้ไขยอดเงินด้วยตนเอง"
            if (vWhtRate > 0 && !CheckBox1.Checked)
            {
                var whtCheck = AccountingArithmeticValidator.ValidateWhtCalculation(vSubtotal, vWhtRate, vWht);
                if (!whtCheck.IsValid) validationResults.Add(whtCheck);
            }

            // สมการยอด (ยอดก่อน VAT + VAT − WHT = สุทธิ) — บังคับ "เสมอ" แม้โหมดแก้ยอดเอง
            var totalCheck = AccountingArithmeticValidator.ValidateInvoiceTotal(vSubtotal, vVat, vWht, vNet);
            bool totalInvalid = !totalCheck.IsValid;
            if (totalInvalid) validationResults.Add(totalCheck);

            if (validationResults.Count > 0)
            {
                string user = Session["User"]?.ToString() ?? "ANON";
                foreach (var r in validationResults)
                    AccountingArithmeticValidator.LogValidationFailure("VOUCHER", txtVoucherNo.Text, r, user);

                bool block = totalInvalid
                    || (AccountingArithmeticValidator.HasBlockingError(validationResults) && !CheckBox1.Checked);
                if (block)
                {
                    ShowFormError("ยอดไม่ถูกต้องทางบัญชี — ยอดก่อน VAT + VAT − หัก ณ ที่จ่าย ต้องเท่ากับยอดจ่ายสุทธิ\n"
                        + AccountingArithmeticValidator.FormatErrors(validationResults));
                    return false;
                }
            }

            if (!allLinesHaveCategory)
            {
                ShowFormError("มีรายการที่ยังไม่มีหมวดค่าใช้จ่าย — ลบรายการนั้นแล้วเพิ่มใหม่พร้อมเลือกหมวด");
                return false;
            }
            return true;
        }

        /// <summary>ใบซ้ำ (ผู้ขายเดียวกัน + เลขที่ใบเดียวกัน) — เจอ = แสดงรายการพร้อมลิงก์แล้วหยุด; กดบันทึกซ้ำด้วยชุดเดิม = ยืนยัน
        /// (จ่ายงวดถัดไปของใบเดิมได้). true = หยุดบันทึก</summary>
        private bool CheckDuplicateBeforeSave(string excludeId)
        {
            string inv = (txtSupplierInvoiceNo.Text ?? "").Trim();
            if (inv.Length == 0) return false;
            List<Integration.PaymentVoucherForm.DuplicateHit> hits;
            try
            {
                decimal net; decimal.TryParse(TextBox6.Text, out net);
                DateTime d;
                if (!DateTime.TryParseExact(TextBox8.Text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) d = DateTime.Today;
                hits = new Integration.PaymentVoucherAssist(conn).FindDuplicates(DropDownList1.SelectedValue, inv, net, d, excludeId, includeRemote: true);
            }
            catch { return false; }
            if (hits == null || hits.Count == 0) { ViewState.Remove("DupAck"); return false; }
            string sig = Integration.PaymentVoucherForm.DuplicateSignature(hits);
            if ((ViewState["DupAck"] as string) == sig) return false;
            ViewState["DupAck"] = sig;

            var sb = new System.Text.StringBuilder("<ul>");
            foreach (var h in hits)
            {
                string label = HttpUtility.HtmlEncode(string.IsNullOrEmpty(h.Number) ? "(ไม่มีเลข)" : h.Number);
                sb.Append("<li>").Append(HttpUtility.HtmlEncode(Integration.PaymentVoucherForm.SourceLabel(h.Source))).Append(' ');
                if (!string.IsNullOrEmpty(h.Url) && ((h.Url.StartsWith("/") && !h.Url.StartsWith("//")) || h.Url.StartsWith("https://") || h.Url.StartsWith("http://")))
                    sb.Append("<a href=\"").Append(HttpUtility.HtmlAttributeEncode(h.Url)).Append("\" target=\"_blank\" rel=\"noopener\">").Append(label).Append("</a>");
                else sb.Append("<b>").Append(label).Append("</b>");
                if (!string.IsNullOrEmpty(h.Date)) sb.Append(" · ").Append(HttpUtility.HtmlEncode(h.Date));
                if (h.Amount != 0m) sb.Append(" · ").Append(h.Amount.ToString("N2")).Append(" บาท");
                if (!string.IsNullOrEmpty(h.Reason)) sb.Append(" — ").Append(HttpUtility.HtmlEncode(h.Reason));
                sb.Append("</li>");
            }
            sb.Append("</ul><div>ถ้าเป็นการจ่ายงวดถัดไปของใบเดิม (ไม่ใช่ใบซ้ำ) กด \"บันทึกใบสำคัญจ่าย\" อีกครั้งเพื่อยืนยัน</div>");
            ShowMsg(litFormMsg, "warn", "อาจเป็นใบซ้ำ — เลขที่ใบ \"" + inv + "\" ของผู้ขายรายนี้เคยบันทึกแล้ว", sb.ToString());
            RequestScrollToMsg();
            return true;
        }

        /// <summary>เก็บเลขที่ใบของผู้ขาย (PHASE19_40) — ไม่มีคอลัมน์ = ข้าม (การ sync ไม่ใช้ค่านี้)</summary>
        private void SaveSupplierInvoiceNo(string docNum)
        {
            string inv = (txtSupplierInvoiceNo.Text ?? "").Trim();
            if (inv.Length == 0 || !Integration.PaymentVoucherAssist.HasSupplierInvoiceColumn(conn)) return;
            if (inv.Length > 100) inv = inv.Substring(0, 100);
            try
            {
                code.DatabaseInsertSafe(conn,
                    "UPDATE [dbo].[Account_Payment] SET Supplier_Invoice_No = @i, Supplier_Invoice_No_Norm = @n WHERE ID = @ID",
                    new Dictionary<string, object>
                    {
                        { "@i", inv },
                        { "@n", Integration.OcrReview.OcrReviewMath.NormalizeDocNo(inv) },
                        { "@ID", docNum }
                    });
            }
            catch (Exception ex)
            {
                try { new code().Logs(conn, "Accounting Sync", $"SaveSupplierInvoiceNo: doc={docNum} {ex.Message}", "SYSTEM"); } catch { }
            }
        }

        /// <summary>ข้อความในหน้า (แทน alert) — text ถูก encode; extraHtml = HTML ที่ผู้เรียก encode แล้ว</summary>
        private static void ShowMsg(Literal lit, string kind, string text, string extraHtml)
        {
            string role = kind == "err" || kind == "warn" ? "alert" : "status";
            lit.Text = "<div class=\"pv-msg pv-msg-" + kind + "\" role=\"" + role + "\">"
                + HttpUtility.HtmlEncode(text ?? "").Replace("\r", "").Replace("\n", "<br/>")
                + (extraHtml ?? "") + "</div>";
        }

        private void ShowFormError(string text)
        {
            ShowMsg(litFormMsg, "err", text, null);
            RequestScrollToMsg();
        }

        private void RequestScrollToMsg() { _scrollToMsg = true; }

        /// <summary>ค่าตั้งของ Scripts/pv-voucher.js (nonce, id ของ control, อัตรา VAT, เปิด AI หรือไม่)</summary>
        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);
            try
            {
                bool ai = false;
                try { ai = new Integration.PaymentVoucherAssist(conn).AiEnabled; } catch { }
                DataTable dtDetail = Session["dtDetail"] as DataTable;
                var js = new System.Web.Script.Serialization.JavaScriptSerializer();
                var cfg = new Dictionary<string, object>
                {
                    { "nonce", Session["PvAssistNonce"] as string ?? "" },
                    { "api", ResolveUrl("~/API/PvAssist.ashx") },
                    { "ai", ai },
                    { "exclude", ViewState["PaymentID"] as string ?? "" },
                    { "lineCount", dtDetail?.Rows.Count ?? 0 },
                    { "vatInclusiveId", "2" },
                    { "scrollToMsg", _scrollToMsg },
                    { "ids", new Dictionary<string, string>
                        {
                            { "vendor", DropDownList1.ClientID }, { "inv", txtSupplierInvoiceNo.ClientID }, { "date", TextBox8.ClientID },
                            { "desc", TextBox1.ClientID }, { "amt", TextBox2.ClientID }, { "line", ddlLineAccount.ClientID },
                            { "headCat", DropDownList3.ClientID }, { "payFrom", ddlPayFrom.ClientID }, { "paidHowAs", DropDownList2.ClientID },
                            { "credit", chkIsCredit.ClientID }, { "vat", DropDownList4.ClientID }, { "wht", ddlWHTRate.ClientID },
                            { "whtAmt", txtWHTAmount.ClientID }, { "sub", TextBox3.ClientID }, { "vatAmt", TextBox4.ClientID },
                            { "net", TextBox6.ClientID }, { "cil", chkCertInLieu.ClientID }, { "save", Button3.ClientID }
                        } }
                };
                string json = js.Serialize(cfg);
                string vatJson = ViewState["VatPctJson"] as string ?? "{}";
                json = json.Substring(0, json.Length - 1) + ",\"vatPct\":" + vatJson + "}";
                litPvCfg.Text = "<script>window.PV_CFG = " + json.Replace("</", "<\\/") + ";</script>";
            }
            catch { litPvCfg.Text = ""; }
        }

        /// <summary>ตัวค้นหาบัญชีของ payload (AccountingSyncService + บัญชีล้างรับของของหน้านี้)</summary>
        private sealed class SyncLookups : Integration.PaymentVoucherForm.IVoucherLookups
        {
            private readonly Integration.AccountingSyncService _s;
            private readonly Func<Tuple<string, string, string>> _clearing;

            public SyncLookups(Integration.AccountingSyncService s, Func<Tuple<string, string, string>> clearing)
            {
                _s = s;
                _clearing = clearing;
            }

            public string LookupPaidHowAccountId(string t) => _s.LookupPaidHowAccountId(t);
            public string LookupPaidHowAccountCode(string t) => _s.LookupPaidHowAccountCode(t);
            public string LookupPaidTypeAccountId(string t) => _s.LookupPaidTypeAccountId(t);
            public string LookupPaidTypeAccountCode(string t) => _s.LookupPaidTypeAccountCode(t);
            public bool IsPaidHowCashOrBank(string t) => _s.IsPaidHowCashOrBank(t);
            public Tuple<string, string> GoodsReceiptClearingAccount()
            {
                var c = _clearing();
                return Tuple.Create(c.Item1, c.Item2);
            }
        }

        // ──────────────────────────────────────────────
        // ชำระค่าสินค้าที่รับเข้าสต๊อกแล้ว (GR/IR, PHASE19_31)
        //   รับของ (Product/In) = Dr สินค้าคงเหลือ / Cr GRNI 21240 → ใบสำคัญจ่ายของสินค้านั้นต้อง Dr GRNI (ล้าง) ไม่ใช่ค่าใช้จ่าย
        //   ไม่งั้นต้นทุนซ้ำ (COGS ตอนขาย + ค่าใช้จ่ายตอนจ่าย) และ GRNI ค้างไม่มีวันล้าง
        // ──────────────────────────────────────────────

        private static readonly System.Text.RegularExpressions.Regex GrnBatchRegex =
            new System.Text.RegularExpressions.Regex(@"GR-\d{8}-\d{6}-\d{3}");

        private void LoadGoodsReceiptOptions()
        {
            ddlGoodsReceipt.Items.Clear();
            ddlGoodsReceipt.Items.Add(new ListItem("— ไม่ใช่ค่าสินค้าที่รับเข้าสต๊อก —", ""));
            var dt = Take_Time_BangPhra.Purchasing.PurchaseService.ListBatches(conn, true, 200);
            if (dt == null)
            {
                ddlGoodsReceipt.Enabled = false;
                btnAddGrniLine.Enabled = false;
                return;
            }
            foreach (DataRow r in dt.Rows)
                ddlGoodsReceipt.Items.Add(new ListItem(GoodsReceiptOptionText(r), r["Batch"].ToString()));
        }

        private static string GoodsReceiptOptionText(DataRow r)
        {
            DateTime at = r["Received_At"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(r["Received_At"]);
            decimal net = r["Net_Total"] == DBNull.Value ? 0m : Convert.ToDecimal(r["Net_Total"]);
            decimal vat = r["Vat_Amount"] == DBNull.Value ? 0m : Convert.ToDecimal(r["Vat_Amount"]);
            string st = r["Payment_Status"]?.ToString() ?? "";
            string stTh = st == "PAID" ? "จ่ายแล้ว" : st == "CREDIT" ? "ซื้อเชื่อ" : "รอใบแจ้งหนี้";
            return $"{r["Batch"]} · {at:dd/MM/yy} · {r["Supplier_Name"]} · ใบ {r["Supplier_Doc_No"]} · สุทธิ {net:N2}" +
                   (vat > 0 ? $" + VAT {vat:N2}" : "") + $" · {stTh}";
        }

        /// <summary>โหมดแก้ไข: ให้ batch ที่ผูกกับใบนี้อยู่ปรากฏในรายการ (ถูกกรองออกเพราะผูกแล้ว)</summary>
        private void RestoreLinkedGoodsReceipts(string voucherId)
        {
            foreach (var b in Take_Time_BangPhra.Purchasing.PurchaseService.BatchesOfVoucher(conn, voucherId))
                if (ddlGoodsReceipt.Items.FindByValue(b) == null)
                    ddlGoodsReceipt.Items.Add(new ListItem(b + " (ผูกกับใบนี้อยู่)", b));
        }

        /// <summary>(GUID, code, label) ของบัญชีล้างรับของตามโหมดรับของ (GRNI / INVENTORY / ACCOUNTS_PAYABLE)</summary>
        private Tuple<string, string, string> ResolveGoodsReceiptClearingAccount()
        {
            string key = "GRNI";
            try { key = Take_Time_BangPhra.Purchasing.PurchaseService.ClearingAccountKey(new Integration.AccountingConfig(conn)); } catch { }
            var acc = Take_Time_BangPhra.Purchasing.PurchaseService.LookupMappedAccount(conn, key);
            return Tuple.Create(acc.Item1, acc.Item2, Take_Time_BangPhra.Purchasing.PurchaseService.ClearingAccountLabel(key));
        }

        /// <summary>batch ที่ผูกกับใบนี้ = batch ที่อยู่ในรายละเอียดของบรรทัดล้างรับของ (รอดทั้งการลบบรรทัดและการแก้ไขใบ)</summary>
        private List<string> GetLinkedGoodsReceipts()
        {
            var res = new List<string>();
            DataTable dt = Session["dtDetail"] as DataTable;
            if (dt == null || !dt.Columns.Contains("PaidTypeName")) return res;
            foreach (DataRow r in dt.Rows)
            {
                if (r.RowState == DataRowState.Deleted) continue;
                if (!(r["PaidTypeName"]?.ToString() ?? "").StartsWith(Take_Time_BangPhra.Purchasing.PurchaseService.GrniLinePrefix)) continue;
                foreach (System.Text.RegularExpressions.Match m in GrnBatchRegex.Matches(r["Detail"]?.ToString() ?? ""))
                    if (!res.Contains(m.Value)) res.Add(m.Value);
            }
            return res;
        }

        protected void btnAddGrniLine_Click(object sender, EventArgs e)
        {
            string batch = ddlGoodsReceipt.SelectedValue;
            if (string.IsNullOrEmpty(batch))
            {
                ShowMsg(litGrniInfo, "err", "เลือกการรับของที่จะชำระก่อน", null);
                return;
            }
            string err = AddGoodsReceiptLine(batch);
            if (err != null)
                ShowMsg(litGrniInfo, "err", err, null);
            else
                AutoSelectHeaderPaidType();
        }

        /// <summary>เพิ่มบรรทัด "ชำระค่าสินค้าที่รับเข้าแล้ว" ของ batch (ยอด = สุทธิของจำนวนที่ยังไม่คืน). คืนข้อความผิดพลาด / null</summary>
        private string AddGoodsReceiptLine(string batch)
        {
            var info = Take_Time_BangPhra.Purchasing.PurchaseService.GetBatch(conn, batch);
            if (info == null) return "ไม่พบการรับของ " + batch;
            if (GetLinkedGoodsReceipts().Contains(batch)) return "การรับของนี้อยู่ในรายการแล้ว";

            decimal amount = 0m;
            foreach (var l in info.Lines)
                amount += Math.Round(l.ReturnableQty * l.UnitCost, 2, MidpointRounding.AwayFromZero);
            if (amount <= 0) return "การรับของนี้คืนผู้ขายครบแล้ว — ไม่มียอดให้ชำระ";

            var clr = ResolveGoodsReceiptClearingAccount();
            bool syncOn = false;
            try { var cfg = new Integration.AccountingConfig(conn); syncOn = cfg.IsConfigured && cfg.Enabled && !cfg.IsVoucherLocal; } catch { }
            // ส่ง NextAcc ต้องมี GUID จริงของบัญชี (JOURNAL mode อ่านแค่ AccountId — ไม่มี = ตกไปบัญชีค่าใช้จ่ายตามหมวด = ผิด)
            if (syncOn && string.IsNullOrEmpty(clr.Item1))
                return string.IsNullOrEmpty(clr.Item2)
                    ? "ยังไม่ได้ตั้ง mapping บัญชีล้างรับของ (GRNI/INVENTORY) ในหน้า Accounting Integration"
                    : "ไม่พบรหัสบัญชี " + clr.Item2 + " ในผังบัญชี NextAcc — กด \"ดึง Chart of Accounts\" ในหน้า Accounting Integration ก่อน";

            DataTable dtDetail = (DataTable)Session["dtDetail"];
            if (!dtDetail.Columns.Contains("PaidTypeId"))
            {
                dtDetail.Columns.Add("PaidTypeId");
                dtDetail.Columns.Add("PaidTypeName");
                dtDetail.Columns.Add("NexaaccAccountId");
            }
            if (!dtDetail.Columns.Contains("AccountCode"))
                dtDetail.Columns.Add("AccountCode");

            string detail = $"ค่าสินค้ารับเข้า {batch} {info.SupplierName}" + (string.IsNullOrEmpty(info.SupplierDocNo) ? "" : $" ใบ {info.SupplierDocNo}");
            string catName = $"{Take_Time_BangPhra.Purchasing.PurchaseService.GrniLinePrefix} {clr.Item2} {clr.Item3}".Trim();
            dtDetail.Rows.Add(dtDetail.Rows.Count + 1, detail, NumberHelper.TwoDecimalPoints(amount),
                "", catName, clr.Item1 ?? "", clr.Item2 ?? "");
            Session["dtDetail"] = dtDetail;
            GridView1.DataSource = dtDetail;
            GridView1.DataBind();
            if (DropDownList4.SelectedIndex > 0)
                calAmount(dtDetail, DropDownList4.SelectedValue);

            // ภาษีซื้อ: มี VAT + ผู้ขายมีเลขผู้เสียภาษี → เคลม (ใบกำกับเต็มรูป); รับของที่ไม่มี VAT → ไม่มีอะไรให้เคลม
            if (info.VatAmount > 0 && info.VatLikelyClaimable) ddlVatClaim.SelectedValue = "1";

            litGrniInfo.Text = "<div style='margin-top:4px;font-size:12px;color:#1b5e20;'>✔ เพิ่ม " + HttpUtility.HtmlEncode(batch)
                + " สุทธิ " + amount.ToString("N2") + (info.VatAmount > 0 ? " (VAT ตอนรับของ " + info.VatAmount.ToString("N2") + ")" : "")
                + " → Dr " + HttpUtility.HtmlEncode(clr.Item3) + "</div>";
            return null;
        }

        /// <summary>เปิดจากหน้า นำเข้าสินค้า: เลือกผู้ขาย/แหล่งเงิน/เครดิต/ประเภทภาษี + เพิ่มบรรทัดล้างรับของให้</summary>
        private void PrefillFromGoodsReceipt(string batch, string paidHowId, bool isCredit)
        {
            var info = Take_Time_BangPhra.Purchasing.PurchaseService.GetBatch(conn, batch);
            if (info == null) return;
            try
            {
                if (ddlGoodsReceipt.Items.FindByValue(batch) != null) ddlGoodsReceipt.SelectedValue = batch;

                // ผู้ขาย: เลขผู้เสียภาษีก่อน แล้วชื่อ
                DataTable dtV = null;
                if (Take_Time_BangPhra.Purchasing.PurchaseService.IsThaiTaxId(info.SupplierTaxId))
                    dtV = code.DatabaseQuerySafe(conn, "SELECT TOP 1 ID FROM Vendor WHERE Status = 'True' AND REPLACE(REPLACE(ISNULL(IDNumber,''),'-',''),' ','') = @t",
                        new Dictionary<string, object> { { "@t", System.Text.RegularExpressions.Regex.Replace(info.SupplierTaxId, @"\D", "") } });
                if ((dtV == null || dtV.Rows.Count == 0) && !string.IsNullOrEmpty(info.SupplierName))
                    dtV = code.DatabaseQuerySafe(conn, "SELECT TOP 1 ID FROM Vendor WHERE Status = 'True' AND Name = @n",
                        new Dictionary<string, object> { { "@n", info.SupplierName } });
                if (dtV != null && dtV.Rows.Count > 0)
                {
                    var it = DropDownList1.Items.FindByValue(dtV.Rows[0]["ID"].ToString());
                    if (it != null) DropDownList1.SelectedValue = it.Value;
                }

                if (!string.IsNullOrEmpty(paidHowId) && DropDownList2.Items.FindByValue(paidHowId) != null)
                {
                    DropDownList2.SelectedValue = paidHowId;
                    SelectPayFromFromLegacy();
                }
                chkIsCredit.Checked = isCredit;
                if (info.SupplierDocDate.HasValue)
                    TextBox8.Text = info.SupplierDocDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

                // ประเภทภาษี: มี VAT → 7% แบบไม่รวม (รายการเป็นยอดสุทธิ), ไม่มี → 0%
                var dtVat = code.DatabaseQuerySafe(conn,
                    "SELECT ID, Vat_Percent FROM Account_Vat_Type WHERE Status = 'True' ORDER BY ID", null);
                if (dtVat != null)
                {
                    int wantPct = info.VatAmount > 0 ? 7 : 0;
                    foreach (DataRow r in dtVat.Rows)
                    {
                        int pct; int.TryParse(r["Vat_Percent"]?.ToString(), out pct);
                        string vid = r["ID"].ToString();
                        if (pct == wantPct && vid != "2" && DropDownList4.Items.FindByValue(vid) != null)
                        {
                            DropDownList4.SelectedValue = vid;
                            Label1.Text = DropDownList4.SelectedItem.Text;
                            break;
                        }
                    }
                }

                // หมวดหัวใบ: หมวดที่ชื่อสื่อถึงซื้อสินค้า (ถ้ามี)
                foreach (ListItem li in DropDownList3.Items)
                    if (li.Text.Contains("สินค้า") || li.Text.Contains("ซื้อ")) { DropDownList3.SelectedValue = li.Value; break; }
            }
            catch { }

            string err = AddGoodsReceiptLine(batch);
            if (err != null)
                litGrniInfo.Text = "<div style='margin-top:4px;font-size:12px;color:#b71c1c;'>" + HttpUtility.HtmlEncode(err) + "</div>";
        }

        /// <summary>
        /// ตรวจก่อนบันทึก (สองจังหวะ — กดซ้ำ = ยืนยัน):
        ///  • ไม่มีบรรทัดล้างรับของ แต่มีการรับของที่ยังไม่ผูกของผู้ขายเดียวกัน/สินค้าตรงกับรายละเอียด → เตือนต้นทุนซ้ำ
        ///  • มีบรรทัดล้างรับของ + เลือก "ไม่เคลม VAT" → VAT ถูกรวมเข้าบรรทัด GRNI = GRNI ค้างเศษ
        /// คืนข้อความเตือน หรือ null (ผ่าน)
        /// </summary>
        private string CheckGoodsReceiptLinkWarnings()
        {
            try
            {
                if (!Take_Time_BangPhra.Purchasing.PurchaseService.HasPurchaseColumns(conn)) return null;
                DataTable dtDetail = Session["dtDetail"] as DataTable;
                if (dtDetail == null || dtDetail.Rows.Count == 0) return null;
                var linked = GetLinkedGoodsReceipts();
                string msg = null, sig = null;

                if (linked.Count == 0)
                {
                    string paidType = DropDownList3.SelectedItem?.Text ?? "";
                    if (paidType.Contains("เงินเดือน")) return null;
                    string vendorName = DropDownList1.SelectedItem?.Text ?? "";
                    string vendorTax = null;
                    try
                    {
                        var dtT = code.DatabaseQuerySafe(conn, "SELECT TOP 1 IDNumber FROM Vendor WHERE ID = @id",
                            new Dictionary<string, object> { { "@id", DropDownList1.SelectedValue ?? "" } });
                        if (dtT != null && dtT.Rows.Count > 0) vendorTax = dtT.Rows[0][0]?.ToString();
                    }
                    catch { }
                    var texts = new List<string>();
                    foreach (DataRow r in dtDetail.Rows) texts.Add(r["Detail"]?.ToString() ?? "");
                    var matches = Take_Time_BangPhra.Purchasing.PurchaseService.FindUnlinkedMatches(conn, vendorName, vendorTax, texts);
                    if (matches.Count > 0)
                    {
                        sig = "U:" + string.Join(",", matches);
                        msg = "⚠️ ใบนี้ดูเหมือนค่าสินค้าที่บันทึกรับเข้าสต๊อกแล้ว แต่ยังไม่ได้ผูกการรับของ:\n" + string.Join("\n", matches)
                            + "\n\nถ้าใช่: เลือกในช่อง \"ค่าสินค้าที่รับเข้าแล้ว\" แล้วกด \"ชำระค่าสินค้าที่รับเข้าแล้ว\" แทนบรรทัดค่าใช้จ่าย (ไม่งั้นต้นทุนซ้ำ + GRNI ค้าง)"
                            + "\nถ้าไม่ใช่: กดบันทึกอีกครั้งเพื่อยืนยัน";
                    }
                }
                else if (ddlVatClaim.SelectedValue == "0" && decimal.TryParse(TextBox4.Text, out var vatNow) && vatNow > 0)
                {
                    sig = "V:" + string.Join(",", linked) + ":" + vatNow.ToString("0.00");
                    msg = "⚠️ ใบที่ผูกการรับของ แต่เลือก \"ไม่เคลมภาษีซื้อ\": VAT จะถูกรวมเข้าบรรทัดล้างรับของ → GRNI ค้างเศษเท่ายอด VAT"
                        + "\nถ้าผู้ขายออกใบกำกับเต็มรูปไม่ได้ ควรบันทึกรับของใหม่ด้วยราคารวม VAT (ไม่มี VAT) แทน"
                        + "\nกดบันทึกอีกครั้งเพื่อยืนยันบันทึกตามนี้";
                }

                if (msg == null) return null;
                if ((ViewState["GrniWarnAck"] as string) == sig) return null;   // กดซ้ำ = ยืนยัน
                ViewState["GrniWarnAck"] = sig;
                return msg;
            }
            catch { return null; }
        }

        protected void Button3_Click(object sender, EventArgs e)
        {
            string command = Request.QueryString["command"];
            string uid = Request.QueryString["uid"];
            string id = "";
            try
            {
                // SECURE: Get payment ID with parameterized query
                var getIdParams = new Dictionary<string, object>
                {
                    { "@UID", uid ?? "" }
                };
                id = code.DatabaseQuerySafe(conn,
                    "SELECT ID FROM [Account_Payment] WHERE UID = @UID",
                    getIdParams).Rows[0][0].ToString();
            }
            catch { }

            // Store original UID for edit mode - to preserve file attachment names
            string originalUid = uid;

            // "จ่ายจาก" → DropDownList2 + ddlPaidHowNexaacc · หมวดหลักของใบ (ยังไม่เลือก) = หมวดของรายการแรก
            ApplyPayFromToLegacy();
            AutoSelectHeaderPaidType();

            // ตรวจทุกอย่าง "ก่อน" ขั้น void/ลบใบเดิมในโหมดแก้ไข — เดิมช่องบังคับ/ยอด/หมวดถูกตรวจหลังลบ = กรอกไม่ครบแล้วใบเดิมหาย
            // ใบรับรองแทนใบเสร็จรับเงิน
            bool isCertInLieu = chkCertInLieu.Checked;
            if (isCertInLieu)
            {
                string cilErr = ValidateCertInLieu();
                if (cilErr != null)
                {
                    ShowFormError(cilErr);
                    return;
                }
            }

            string requiredErr = RequiredFieldsError();
            if (requiredErr != null)
            {
                ShowFormError(requiredErr);
                return;
            }

            if (!ValidateLinesAndTotals())
                return;

            if (chkRecordAsset.Checked && ViewState["ExistingAssetID"] == null && string.IsNullOrWhiteSpace(txtAssetName.Text))
            {
                ShowFormError("กรอกชื่อสินทรัพย์ (ติ๊ก \"บันทึกเป็นสินทรัพย์ถาวร\" ไว้)");
                return;
            }

            // ค่าสินค้าที่รับเข้าสต๊อกแล้วแต่ไม่ได้ผูก = ต้นทุนซ้ำ (Dr สินค้าคงเหลือตอนรับ + Dr ค่าใช้จ่ายตอนจ่าย) → เตือนก่อน
            // (กดบันทึกซ้ำ = ยืนยันว่าไม่ใช่ค่าสินค้าที่รับเข้าแล้ว)
            string grniWarn = CheckGoodsReceiptLinkWarnings();
            if (grniWarn != null)
            {
                ShowMsg(litFormMsg, "warn", grniWarn, null);
                RequestScrollToMsg();
                return;
            }

            // ใบซ้ำ: ผู้ขายเดียวกัน + เลขที่ใบกำกับ/ใบเสร็จเดียวกัน (ระบบนี้ + หน้าสแกน + NextAcc) — กดบันทึกซ้ำ = ยืนยัน
            if (CheckDuplicateBeforeSave(command == "edit" ? id : null))
                return;

            if (command == "edit")
            {
                try
                {
                    var sync = new Integration.AccountingSyncService(conn);
                    sync.EnqueueVoidPaymentVoucher(id);
                }
                catch (Exception accEx)
                {
                    new code().Logs(conn, "Accounting Sync", $"Void voucher error (PaymentVoucher edit): id={id} {accEx.Message}", "SYSTEM");
                }

                var deleteParams = new Dictionary<string, object>
                {
                    { "@UID", uid ?? "" },
                    { "@PaymentID", id }
                };
                code.DatabaseInsertSafe(conn,
                    @"BEGIN TRAN;
                      DELETE FROM [dbo].[Account_Payment_Detail] WHERE Payment_ID = @PaymentID;
                      DELETE FROM [dbo].[Account_Payment] WHERE UID = @UID;
                      COMMIT TRAN;",
                    deleteParams);
            }
            else
            {
            }

            {
                DataTable dtDetail = (DataTable)Session["dtDetail"];

                DateTime createDate = Convert.ToDateTime(TextBox8.Text);
                DateTime docDate = Convert.ToDateTime(TextBox8.Text);
                string docNum;

                // Priority 1: If user has edited the voucher number, use their value
                if (chkEditVoucherNo.Checked && !string.IsNullOrWhiteSpace(txtVoucherNo.Text))
                {
                    docNum = txtVoucherNo.Text.Trim();
                }
                // Priority 2: If edit mode and not editing number, use existing ID
                else if (command == "edit")
                {
                    docNum = id;
                }
                // Priority 3: Generate new number
                else
                {
                    docNum = documentHelper.CreateDocumentNumber("Account_Payment", "PAY", docDate);
                }

                // Extract Year/Month for directory structure
                string Year = docDate.Year.ToString();
                string Month = docDate.Month.ToString("00");

                // SECURE: Insert payment record with parameterized query
                // In edit mode, preserve the original UID to keep file attachments working
                decimal whtRate = 0;
                decimal.TryParse(ddlWHTRate.SelectedValue, out whtRate);
                decimal whtAmount = 0;
                decimal.TryParse(txtWHTAmount.Text, out whtAmount);

                // ใบรับรองแทนใบเสร็จ = จ่ายเงินแล้วเสมอ (ไม่มีเครดิต)
                bool isCredit = chkIsCredit.Checked && !isCertInLieu;

                var paymentInsertParams = new Dictionary<string, object>
                {
                    { "@ID", docNum },
                    { "@VendorID", DropDownList1.SelectedValue },
                    { "@CreatedDate", createDate.ToString("yyyy-MM-dd") },
                    { "@TotalAmount", TextBox6.Text },
                    { "@VatTypeID", DropDownList4.SelectedValue },
                    { "@Vat", TextBox4.Text },
                    { "@TotalAmountExcludeVat", TextBox3.Text },
                    { "@PaidHow", DropDownList2.SelectedItem.Text },
                    { "@PaidType", DropDownList3.SelectedItem.Text },
                    { "@CreatedByID", Session["UserID"].ToString() },
                    { "@WHTRate", whtRate },
                    { "@WHTAmount", whtAmount },
                    { "@IsCredit", isCredit }
                };

                // If editing, preserve the original UID; otherwise let database generate new one
                if (command == "edit" && !string.IsNullOrEmpty(originalUid))
                {
                    paymentInsertParams.Add("@UID", originalUid);
                    code.DatabaseInsertSafe(conn,
                        "INSERT INTO [dbo].[Account_Payment] ([ID],[UID],[Vendor_ID],[Created_Date],[Total_Amount],[Vat_Type_ID],[Vat],[Total_Amount_Exclude_Vat],[Paid_How],[Paid_Type],[Status],[Created_By_ID],[WHT_Rate],[WHT_Amount],[IsCredit]) " +
                        "VALUES (@ID,@UID,@VendorID,@CreatedDate,@TotalAmount,@VatTypeID,@Vat,@TotalAmountExcludeVat,@PaidHow,@PaidType,N'Normal',@CreatedByID,@WHTRate,@WHTAmount,@IsCredit)",
                        paymentInsertParams);
                }
                else
                {
                    code.DatabaseInsertSafe(conn,
                        "INSERT INTO [dbo].[Account_Payment] ([ID],[Vendor_ID],[Created_Date],[Total_Amount],[Vat_Type_ID],[Vat],[Total_Amount_Exclude_Vat],[Paid_How],[Paid_Type],[Status],[Created_By_ID],[WHT_Rate],[WHT_Amount],[IsCredit]) " +
                        "VALUES (@ID,@VendorID,@CreatedDate,@TotalAmount,@VatTypeID,@Vat,@TotalAmountExcludeVat,@PaidHow,@PaidType,N'Normal',@CreatedByID,@WHTRate,@WHTAmount,@IsCredit)",
                        paymentInsertParams);
                }

                if (isCertInLieu)
                    SaveCertInLieuColumns(docNum);

                // เลขที่ใบกำกับ/ใบเสร็จของผู้ขาย (PHASE19_40 — เก็บในระบบเพื่อตรวจใบซ้ำ ไม่ได้ส่งเข้าคิว)
                SaveSupplierInvoiceNo(docNum);

                // SECURE: Insert payment details with parameterized queries (including per-line category)
                for(int i = 0;i<dtDetail.Rows.Count;i++)
                {
                    string linePaidTypeId = dtDetail.Columns.Contains("PaidTypeId") ? dtDetail.Rows[i]["PaidTypeId"]?.ToString() : "";
                    string linePaidTypeName = dtDetail.Columns.Contains("PaidTypeName") ? dtDetail.Rows[i]["PaidTypeName"]?.ToString() : "";
                    string lineNexaaccId = dtDetail.Columns.Contains("NexaaccAccountId") ? dtDetail.Rows[i]["NexaaccAccountId"]?.ToString() : "";

                    var detailInsertParams = new Dictionary<string, object>
                    {
                        { "@PaymentID", docNum },
                        { "@Number", dtDetail.Rows[i]["Number"].ToString() },
                        { "@Detail", dtDetail.Rows[i]["Detail"].ToString() },
                        { "@Amount", dtDetail.Rows[i]["Amount"].ToString() }
                    };

                    try
                    {
                        detailInsertParams.Add("@PaidTypeId", string.IsNullOrEmpty(linePaidTypeId) || linePaidTypeId == "0" ? (object)DBNull.Value : Convert.ToInt32(linePaidTypeId));
                        detailInsertParams.Add("@PaidTypeName", string.IsNullOrEmpty(linePaidTypeName) ? (object)DBNull.Value : linePaidTypeName);
                        detailInsertParams.Add("@NexaaccAccountId", string.IsNullOrEmpty(lineNexaaccId) ? (object)DBNull.Value : lineNexaaccId);
                        code.DatabaseInsertSafe(conn,
                            "INSERT INTO [dbo].[Account_Payment_Detail]([Payment_ID],[Number],[Detail],[Amount],[Paid_Type_ID],[Paid_Type_Name],[Nexaacc_AccountId]) " +
                            "VALUES (@PaymentID,@Number,@Detail,@Amount,@PaidTypeId,@PaidTypeName,@NexaaccAccountId)",
                            detailInsertParams);
                    }
                    catch
                    {
                        var fallbackParams = new Dictionary<string, object>
                        {
                            { "@PaymentID", docNum },
                            { "@Number", dtDetail.Rows[i]["Number"].ToString() },
                            { "@Detail", dtDetail.Rows[i]["Detail"].ToString() },
                            { "@Amount", dtDetail.Rows[i]["Amount"].ToString() }
                        };
                        code.DatabaseInsertSafe(conn,
                            "INSERT INTO [dbo].[Account_Payment_Detail]([Payment_ID],[Number],[Detail],[Amount]) " +
                            "VALUES (@PaymentID,@Number,@Detail,@Amount)",
                            fallbackParams);
                    }
                }

                // ผูกการรับของ (Product_In.Linked_Voucher_Id / Payment_Status, Account_Payment.Linked_Goods_Receipt)
                try
                {
                    if (command == "edit" && !string.IsNullOrEmpty(id))
                        Take_Time_BangPhra.Purchasing.PurchaseService.UnlinkVoucher(conn, id);
                    var linkedBatches = GetLinkedGoodsReceipts();
                    if (linkedBatches.Count > 0)
                        Take_Time_BangPhra.Purchasing.PurchaseService.LinkBatchesToVoucher(conn, linkedBatches, docNum, isCredit);
                }
                catch (Exception linkEx)
                {
                    try { new code().Logs(conn, "Purchase", $"Link goods receipt failed: doc={docNum} {linkEx.Message}", "SYSTEM"); } catch { }
                }

                string path = AppCfg.Get("PaymentFolderPath").ToString();
                try
                {
                    System.IO.Directory.CreateDirectory(path + "\\" + Year);
                    System.IO.Directory.CreateDirectory(path + "\\" + Year + "\\" + Month);
                }
                catch (Exception ex)
                {

                }
                string PayNumber = docNum;
                DataTable dtbusinessinfo = code.DatabaseQuery(conn, "Select * from Business_Info left join Customer_Type on Business_Type_ID = Customer_Type.ID left join Address on Address.ID = Address_ID");
                DataTable dtBusinessinfoReport = new DataTable();
                dtBusinessinfoReport = dtbusinessinfo.Copy();
                try
                {
                    // Check if Address_ID exists - if not, use Address field as-is (complete address already stored)
                    if (dtbusinessinfo.Rows[0]["Address_ID"] == DBNull.Value || string.IsNullOrEmpty(dtbusinessinfo.Rows[0]["Address_ID"].ToString()))
                    {
                        // No Address_ID - use complete address from Address field without adding prefixes
                        dtBusinessinfoReport.Rows[0]["Address"] = dtbusinessinfo.Rows[0]["Address"].ToString();
                    }
                    else
                    {
                        // Has Address_ID - build address from structured fields with appropriate prefixes
                        if (dtbusinessinfo.Rows[0]["Province"].ToString().Contains("กรุงเทพ"))
                        {
                            dtBusinessinfoReport.Rows[0]["Address"] = dtbusinessinfo.Rows[0]["Address"].ToString() + " " + dtbusinessinfo.Rows[0]["Address1"].ToString() + " แขวง " + dtbusinessinfo.Rows[0]["SubDistrict"].ToString() + " เขต " + dtbusinessinfo.Rows[0]["District"].ToString() + " " + dtbusinessinfo.Rows[0]["Province"].ToString() + " " + dtbusinessinfo.Rows[0]["PostalCode"].ToString();
                        }
                        else
                        {
                            dtBusinessinfoReport.Rows[0]["Address"] = dtbusinessinfo.Rows[0]["Address"].ToString() + " " + dtbusinessinfo.Rows[0]["Address1"].ToString() + " ต." + dtbusinessinfo.Rows[0]["SubDistrict"].ToString() + " อ." + dtbusinessinfo.Rows[0]["District"].ToString() + " จ." + dtbusinessinfo.Rows[0]["Province"].ToString() + " " + dtbusinessinfo.Rows[0]["PostalCode"].ToString();
                        }
                    }
                }
                catch
                {
                    dtBusinessinfoReport.Rows[0]["Address"] = dtbusinessinfo.Rows[0]["Address"].ToString();
                }

                var vendorParams = new Dictionary<string, object> { { "@VendorID", DropDownList1.SelectedValue } };
                DataTable dtVendor = code.DatabaseQuerySafe(conn, "Select * from Vendor left join Customer_Type on Customer_Type.ID = Vendor_Type_ID left join Address on Address.ID = Address_ID Where Vendor.ID = @VendorID", vendorParams);

                DataTable dtVendorReport = new DataTable();
                dtVendorReport = dtVendor.Copy();


                try
                {
                    // Check if Address_ID exists - if not, use Address field as-is (complete address already stored)
                    if (dtVendorReport.Rows[0]["Address_ID"] == DBNull.Value || string.IsNullOrEmpty(dtVendorReport.Rows[0]["Address_ID"].ToString()))
                    {
                        // No Address_ID - use complete address from Address field without adding prefixes
                        dtVendorReport.Rows[0]["Address"] = dtVendor.Rows[0]["Address"].ToString();
                    }
                    else
                    {
                        // Has Address_ID - build address from structured fields with appropriate prefixes
                        if (dtVendorReport.Rows[0]["Province"].ToString().Contains("กรุงเทพ"))
                        {
                            dtVendorReport.Rows[0]["Address"] = dtVendorReport.Rows[0]["Address"].ToString() + " " + dtVendorReport.Rows[0]["Address1"].ToString() + " แขวง " + dtVendorReport.Rows[0]["SubDistrict"].ToString() + " เขต " + dtVendorReport.Rows[0]["District"].ToString() + " " + dtVendorReport.Rows[0]["Province"].ToString() + " " + dtVendorReport.Rows[0]["PostalCode"].ToString();
                        }
                        else
                        {
                            dtVendorReport.Rows[0]["Address"] = dtVendorReport.Rows[0]["Address"].ToString() + " " + dtVendorReport.Rows[0]["Address1"].ToString() + " ต." + dtVendorReport.Rows[0]["SubDistrict"].ToString() + " อ." + dtVendorReport.Rows[0]["District"].ToString() + " จ." + dtVendorReport.Rows[0]["Province"].ToString() + " " + dtVendorReport.Rows[0]["PostalCode"].ToString();
                        }
                    }
                }
                catch
                {
                    dtVendorReport.Rows[0]["Address"] = dtVendor.Rows[0]["Address"].ToString();
                }
                try
                {
                    if (dtVendorReport.Rows[0]["Branch_Number"].ToString().Length == 5)
                    {
                        dtVendorReport.Rows[0]["Name"] += " สาขาที่ " + dtVendorReport.Rows[0]["Branch_Number"].ToString();
                    }
                }
                catch { }

                var payDetailParams = new Dictionary<string, object> { { "@PaymentID", PayNumber } };
                DataTable dtPaymentDetail = code.DatabaseQuerySafe(conn, "SELECT * FROM [Account_Payment_Detail] Where Payment_ID = @PaymentID order by Number ASC", payDetailParams);

                var payParams = new Dictionary<string, object> { { "@PaymentID", PayNumber } };
                DataTable dtPayment = code.DatabaseQuerySafe(conn, "SELECT * FROM [Account_Payment] inner join Account_Vat_Type on Account_Vat_Type.ID = Vat_Type_ID Where Account_Payment.ID = @PaymentID", payParams);

                // For payroll payments (เงินเดือน), get employee name from Payroll_Records via VoucherNumber
                // This shows employee name instead of "เงินเดือนพนักงาน"
                try
                {
                    if (dtPayment.Rows.Count > 0 && dtPayment.Rows[0]["Paid_Type"]?.ToString() == "เงินเดือน")
                    {
                        // Get employee name from Payroll_Records where VoucherNumber = Account_Payment.ID
                        var empNameParams = new Dictionary<string, object>
                        {
                            { "@VoucherNumber", PayNumber }
                        };
                        DataTable dtEmployeeName = code.DatabaseQuerySafe(conn,
                            "SELECT EmployeeName FROM Payroll_Records WHERE VoucherNumber = @VoucherNumber",
                            empNameParams);

                        if (dtEmployeeName != null && dtEmployeeName.Rows.Count > 0 &&
                            dtEmployeeName.Rows[0]["EmployeeName"] != DBNull.Value &&
                            !string.IsNullOrEmpty(dtEmployeeName.Rows[0]["EmployeeName"].ToString()))
                        {
                            // Override vendor name with employee name for payroll vouchers
                            dtVendorReport.Rows[0]["Name"] = dtEmployeeName.Rows[0]["EmployeeName"].ToString();
                            dtVendorReport.Rows[0]["Address"] = ""; // Clear address for employee payments
                            System.Diagnostics.Debug.WriteLine($"Payroll voucher: Using employee name '{dtVendorReport.Rows[0]["Name"]}' instead of vendor");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error getting employee name for payroll payment: {ex.Message}");
                }
                uid = dtPayment.Rows[0]["UID"].ToString();
                //GridView1.DataSource = dt;
                //GridView1.DataBind();

                // Build signature DataTable directly like master (bypass SignatureService)
                DataTable dtSignature = new DataTable();
                dtSignature.Columns.Add("CreatedName");
                dtSignature.Columns.Add("Created");
                dtSignature.Columns.Add("ApprovedName");
                dtSignature.Columns.Add("Approved");
                dtSignature.Columns.Add("CheckedName");
                dtSignature.Columns.Add("Checked");
                dtSignature.Columns.Add("ReceivedName");
                dtSignature.Columns.Add("Received");

                string Signaturepath = AppCfg.Get("StaffSignatureFolderPath")?.ToString() ?? "";

                // Get creator name
                var creatorParams = new Dictionary<string, object> { { "@UserID", Session["UserID"]?.ToString() } };
                DataTable dtCreator = code.DatabaseQuerySafe(conn, "Select * from Admin Where ID = @UserID", creatorParams);
                string CreatorFullName = dtCreator.Rows.Count > 0 ?
                    dtCreator.Rows[0]["FirstName"].ToString() + " " + dtCreator.Rows[0]["LastName"].ToString() : "";

                // Get approver name (CEO)
                DataTable dtApprover = code.DatabaseQuery(conn, "Select * from Admin Where IsCEO = 'True'");
                string ApproverFullName = dtApprover.Rows.Count > 0 ?
                    dtApprover.Rows[0]["FirstName"].ToString() + " " + dtApprover.Rows[0]["LastName"].ToString() : "";

                // Get receiver name from employee by IDNumber
                string ReceivedFullName = "";
                string ReceivedPath = "";
                if (dtVendor.Rows.Count > 0 && dtVendor.Rows[0]["IDNumber"] != DBNull.Value)
                {
                    string idNumber = dtVendor.Rows[0]["IDNumber"].ToString();
                    if (!string.IsNullOrEmpty(idNumber))
                    {
                        var empParams = new Dictionary<string, object> { { "@IDNumber", idNumber } };
                        DataTable dtEmployee = code.DatabaseQuerySafe(conn, "Select * From Admin Where IDNumber = @IDNumber", empParams);
                        if (dtEmployee.Rows.Count > 0)
                        {
                            ReceivedFullName = dtEmployee.Rows[0]["FirstName"].ToString() + " " + dtEmployee.Rows[0]["LastName"].ToString();
                            ReceivedPath = Signaturepath + "\\" + ReceivedFullName.ToLower() + ".png";
                        }
                    }
                }

                // Build signature row exactly like master
                dtSignature.Rows.Add(
                    CreatorFullName,
                    "File:\\" + Signaturepath + "\\" + CreatorFullName.ToLower() + ".png",
                    ApproverFullName,
                    "File:\\" + Signaturepath + "\\" + ApproverFullName.ToLower() + ".png",
                    "",
                    "",
                    ReceivedFullName,
                    string.IsNullOrEmpty(ReceivedPath) ? "" : "File:\\" + ReceivedPath
                );

                DataTable dtUpload = (DataTable)Session["dtUpload"];
                try
                {
                    Account.Report.DataSet2 dataSet1 = new Account.Report.DataSet2();
                    dataSet1.Tables.Add(dtbusinessinfo);
                    ReportViewer2.LocalReport.DisplayName = "Payment Voucher";
                    ReportViewer2.LocalReport.DataSources.Add(new ReportDataSource("DataSet1", dtBusinessinfoReport));
                    ReportViewer2.LocalReport.DataSources.Add(new ReportDataSource("DataSet2", dtVendorReport));
                    ReportViewer2.LocalReport.DataSources.Add(new ReportDataSource("DataSet3", dtPaymentDetail));
                    ReportViewer2.LocalReport.DataSources.Add(new ReportDataSource("DataSet4", dtPayment));
                    ReportViewer2.LocalReport.DataSources.Add(new ReportDataSource("DataSet5", dtSignature));
                    ReportViewer2.LocalReport.DataSources.Add(new ReportDataSource("DataSet6", dtUpload));
                    
                    try
                    {

                   //     var deviceInfo = @"<DeviceInfo>
                   // <EmbedFonts>None</EmbedFonts>
                   //</DeviceInfo>";


                        Warning[] warnings;
                        string[] streamids;
                        string mimeType;
                        string encoding;
                        string filenameExtension;

                        byte[] bytes = ReportViewer2.LocalReport.Render(
                            "PDF", null, out mimeType, out encoding, out filenameExtension,
                            out streamids, out warnings);

                        using (FileStream fs = new FileStream(path + "\\" + Year + "\\" + Month + "\\" + docNum + "_"+uid+".pdf", FileMode.Append))
                        {
                            fs.Write(bytes, 0, bytes.Length);
                        }
                    }
                    catch (Exception ex)
                    {
                    }

                    //ReportViewer2.LocalReport.Refresh();


                }

                catch { }
                try
                {
                    string filepath = path + "\\" + Year + "\\" + Month + "\\";

                    // Get list of existing files (files that were already on disk before editing)
                    List<string> existingFiles = Session["dtExistingFiles"] as List<string> ?? new List<string>();

                    for (int i = 0; i < dtUpload.Rows.Count; i++)
                    {
                        string displayName = dtUpload.Rows[i][0].ToString();
                        string targetFileName = docNum + "_" + uid + "_" + displayName;

                        // Check if this is an existing file (already has prefix on disk)
                        string existingFileFullName = existingFiles.Find(f => f.EndsWith("_" + displayName));

                        if (!string.IsNullOrEmpty(existingFileFullName))
                        {
                            // This is an existing file - check if it needs to be renamed (if ID changed)
                            if (existingFileFullName != targetFileName && File.Exists(filepath + existingFileFullName))
                            {
                                // ID changed, need to rename from old prefix to new prefix
                                File.Move(filepath + existingFileFullName, filepath + targetFileName);
                            }
                            // else: file already has correct name, no action needed
                        }
                        else
                        {
                            // This is a new file - rename from simple name to prefixed name
                            if (File.Exists(filepath + displayName))
                            {
                                File.Move(filepath + displayName, filepath + targetFileName);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("File rename error: " + ex.Message);
                }

                // Create asset if checkbox is checked
                // (ห่อ try/catch + TryParse: ใบสำคัญจ่ายถูกบันทึกไปแล้ว — อย่าให้ asset ล้มเหลว
                //  ทำทั้งหน้าพังเป็น error แล้วทิ้ง voucher ค้างไม่ sync)
                if (chkRecordAsset.Checked)
                {
                    try
                    {
                        decimal purchasePrice;
                        int vendorId;
                        if (decimal.TryParse(TextBox6.Text, out purchasePrice)
                            && int.TryParse(DropDownList1.SelectedValue, out vendorId))
                        {
                            CreateAssetFromPaymentVoucher(docNum, purchasePrice, docDate, vendorId);
                        }
                    }
                    catch (Exception assetEx)
                    {
                        try { new code().Logs(conn, "Asset", $"CreateAssetFromPaymentVoucher error: docNum={docNum} {assetEx.Message}", "SYSTEM"); } catch { }
                    }
                }

                // Auto-sync voucher to accounting (with per-line expense categories)
                string syncStatus = "off";   // off=ปิด/ไม่ตั้งค่า, queued=ส่งคิวแล้ว, err=ล้มเหลว
                try
                {
                    var config = new Integration.AccountingConfig(conn);
                    if (config.IsConfigured && config.Enabled)
                    {
                        // Resolve vendor TaxId + stable ExternalId so the supplier contact
                        // links correctly in NextAcc (deduped by TaxId, then Name).
                        string vendorExternalId = null, vendorTaxId = null;
                        try
                        {
                            string vendorIdForTax = DropDownList1.SelectedValue;
                            if (!string.IsNullOrEmpty(vendorIdForTax) && vendorIdForTax != "0")
                            {
                                vendorExternalId = "VENDOR-" + vendorIdForTax;
                                var dtVendorTax = code.DatabaseQuerySafe(conn,
                                    "SELECT TOP 1 IDNumber FROM Vendor WHERE ID = @id",
                                    new Dictionary<string, object> { { "@id", vendorIdForTax } });
                                if (dtVendorTax?.Rows.Count > 0)
                                    vendorTaxId = dtVendorTax.Rows[0]["IDNumber"]?.ToString();
                            }
                        }
                        catch { }

                        var sync = new Integration.AccountingSyncService(conn);
                        // payload คิว (บัญชีแหล่งเงิน/บัญชีต่อบรรทัด/GRNI/VAT ไม่เคลม/WHT/เครดิต/ใบรับรอง) — ตรรกะเดิมย้ายไป
                        // PaymentVoucherForm.BuildVoucherPayload (mono test เทียบผลกับโค้ดเดิม) · ห้ามเปลี่ยนผล
                        var p = Integration.PaymentVoucherForm.BuildVoucherPayload(new Integration.PaymentVoucherForm.VoucherInput
                        {
                            NetTotalText = TextBox6.Text,
                            PaymentMethod = DropDownList2.SelectedItem?.Text,
                            ExpenseCategory = DropDownList3.SelectedItem?.Text,
                            VendorName = DropDownList1.SelectedItem?.Text,
                            VendorExternalId = vendorExternalId,
                            VendorTaxId = vendorTaxId,
                            PaymentAccountOverride = ddlPaidHowNexaacc.SelectedValue,
                            Detail = dtDetail,
                            VatText = TextBox4.Text,
                            VatClaimValue = ddlVatClaim.SelectedValue,
                            IsCertInLieu = isCertInLieu,
                            WhtRateText = ddlWHTRate.SelectedValue,
                            WhtAmountText = txtWHTAmount.Text,
                            IsCredit = isCredit,
                            DocDate = docDate,
                            DocNumber = docNum,
                            CertificateInLieu = isCertInLieu ? BuildCertInLieuInfo() : null,
                            GrniLinePrefix = Take_Time_BangPhra.Purchasing.PurchaseService.GrniLinePrefix
                        }, new SyncLookups(sync, ResolveGoodsReceiptClearingAccount));
                        decimal voucherAmount = p.Amount;
                        string expenseCategory = p.ExpenseCategory;
                        string expAccId = p.ExpenseAccountId;

                        long syncQid = sync.EnqueuePaymentVoucher(0, p.ExpenseCategory, p.Amount, p.PaymentMethod, p.VoucherDate, p.Description, p.PayeeName,
                            hasInputVat: p.HasInputVat, whtRate: p.WhtRate, whtAmount: p.WhtAmount,
                            documentNumber: p.DocumentNumber, paymentAccountId: p.PaymentAccountId, expenseAccountId: p.ExpenseAccountId,
                            expenseLines: p.ExpenseLines,
                            isCredit: p.IsCredit, autoRecordPayment: p.AutoRecordPayment,
                            supplierExternalId: p.SupplierExternalId, supplierTaxId: p.SupplierTaxId,
                            vatAmount: p.VatAmount,
                            certificateInLieu: p.CertificateInLieu);
                        syncStatus = syncQid > 0 ? "queued" : "err";

                        // Asset reclassification: DR Fixed Asset / CR Expense
                        if (chkRecordAsset.Checked && voucherAmount > 0)
                        {
                            string assetName = txtAssetName.Text.Trim();
                            if (!string.IsNullOrEmpty(assetName))
                            {
                                sync.EnqueueAssetReclassification(voucherAmount, assetName, docDate, docNum,
                                    expenseAccountId: expAccId, expenseCategory: expenseCategory);
                            }
                        }
                    }
                }
                catch (Exception accEx)
                {
                    syncStatus = "err";
                    new code().Logs(conn, "Accounting Sync", $"Voucher auto-sync error (PaymentVoucher): docNum={docNum} {accEx.Message}", "SYSTEM");
                }

                // บันทึกสำเร็จ → redirect ฝั่ง server (เชื่อถือได้กว่า alert+window.location ฝั่ง client
                // ซึ่งเงียบหายถ้า JS error) แล้วแสดง alert ผ่าน ?saved=1 ใน Page_Load (พร้อมสถานะ sync)
                Response.Redirect("/Account/PaymentVoucher?saved=1&s=" + syncStatus + "&no=" + HttpUtility.UrlEncode(docNum), false);
                Context.ApplicationInstance.CompleteRequest();
                return;
            }
        }

        protected void Button4_Click(object sender, EventArgs e)
        {
            DateTime createDate = Convert.ToDateTime(TextBox8.Text);
            if (TextBox7.Text.Length > 0)
            {
                if (FileUpload1.HasFile)
                {
                    DataTable dtUpload = (DataTable)Session["dtUpload"];
                    string path = AppDomain.CurrentDomain.BaseDirectory + "\\Documents\\Payment";
                    try
                    {
                        System.IO.Directory.CreateDirectory(path + "\\" + createDate.Year.ToString());
                        System.IO.Directory.CreateDirectory(path + "\\" + createDate.Year.ToString() + "\\" + createDate.Month.ToString("00"));
                    }
                    catch (Exception ex)
                    {

                    }
                    path = path + "\\" + createDate.Year.ToString() + "\\" + createDate.Month.ToString("00");
                    string FileName = System.IO.Path.GetFileName(FileUpload1.PostedFile.FileName);
                    string FileExtension = FileName.Substring(FileName.LastIndexOf('.') + 1).ToLower();
                    string FileSaveWithPath = "";
                    string filename = TextBox7.Text + "."+FileExtension;
                    FileSaveWithPath = Server.MapPath("\\Documents\\Payment\\" + createDate.Year.ToString() + "\\" + createDate.Month.ToString("00") +"\\"+ filename);
                    FileUpload1.SaveAs(FileSaveWithPath);
                    dtUpload.Rows.Add(filename);
                    Session["dtUpload"] = dtUpload;
                    GridView2.DataSource = dtUpload;
                    GridView2.DataBind();
                    FileUpload1.Dispose();
                }
                else
                {
                    ShowMsg(litUploadMsg, "err", "เลือกไฟล์ที่จะอัปโหลดก่อน", null);
                }
            }
            else
            {
                ShowMsg(litUploadMsg, "err", "ตั้งชื่อไฟล์ก่อนอัปโหลด (เช่น ใบกำกับภาษี)", null);
            }
        }

        protected void GridView2_RowDeleted(object sender, GridViewDeletedEventArgs e)
        {
        }

        protected void GridView2_RowDeleting(object sender, GridViewDeleteEventArgs e)
        {
            DateTime createDate = Convert.ToDateTime(TextBox8.Text);
            string path = AppDomain.CurrentDomain.BaseDirectory + "\\Documents\\Payment";
            path = path + "\\" + createDate.Year.ToString() + "\\" + createDate.Month.ToString("00");
            DataTable dtUpload = (DataTable)Session["dtUpload"];
            File.Delete(path+"\\"+dtUpload.Rows[e.RowIndex][0].ToString());
            dtUpload.Rows[e.RowIndex].Delete();
            dtUpload.AcceptChanges();
            Session["dtUpload"] = dtUpload;
            GridView2.DataSource = dtUpload;
            GridView2.DataBind();
        }

        protected void DropDownList5_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                string query = "SELECT * FROM [Vendor] WHERE ([Status] = 'True') AND Vendor_Group LIKE @VendorGroup ORDER BY Vendor_Group, Name ASC";
                var parameters = new Dictionary<string, object> { { "@VendorGroup", DropDownList5.SelectedValue } };
                DropDownList1.Items.Clear();
                DropDownList1.Dispose();

                DataTable dtVendor = code.DatabaseQuerySafe(conn, query, parameters);
                for (int i = 0; i < dtVendor.Rows.Count; i++)
                {
                    DropDownList1.Items.Add(new ListItem(dtVendor.Rows[i]["Name"].ToString(), dtVendor.Rows[i]["ID"].ToString()));
                }
                DropDownList1.DataBind();
            }
            catch{ }
        }

        protected void CheckBox1_CheckedChanged(object sender, EventArgs e)
        {
            if(CheckBox1.Checked == true)
            {
                TextBox3.Enabled = true;
                TextBox4.Enabled = true;
                TextBox6.Enabled = true;
            }
            else
            {
                TextBox3.Enabled = false;
                TextBox4.Enabled = false;
                TextBox6.Enabled = false;
            }
        }

        protected void Button5_Click(object sender, EventArgs e)
        {
            Response.Redirect("/Admin/Vendor");
        }

        protected void TextBox9_TextChanged(object sender, EventArgs e)
        {
            try
            {
                var searchParams = new Dictionary<string, object> { { "@VendorName", TextBox9.Text } };
                DataTable dt = code.DatabaseQuerySafe(conn, "SELECT * FROM [Taketime].[dbo].[Vendor] Where [Name] like @VendorName", searchParams);
                DropDownList5.SelectedIndex = DropDownList5.Items.IndexOf(DropDownList5.Items.FindByText(dt.Rows[0]["Vendor_Group"].ToString()));
                DropDownList5_SelectedIndexChanged(null, null);
                DropDownList1.SelectedIndex = DropDownList1.Items.IndexOf(DropDownList1.Items.FindByValue(dt.Rows[0]["ID"].ToString()));
            }
            catch
            {
                TextBox9.Text = "";
            }
        }

        /// <summary>
        /// 🔗 Get file URL for viewing attachment
        /// ค้นหาไฟล์จริงในโฟลเดอร์และส่ง URL ที่ถูกต้อง
        /// </summary>
        protected string GetFileUrl(object fileName)
        {
            try
            {
                if (fileName == null || string.IsNullOrEmpty(fileName.ToString()))
                    return "#";

                DateTime createDate = Convert.ToDateTime(TextBox8.Text);
                string year = createDate.Year.ToString();
                string month = createDate.Month.ToString("00");
                string searchPattern = fileName.ToString(); // filename ที่ถูก strip แล้ว (เช่น "ใบเสร็จ.pdf")

                // Get payment folder path
                string basePath = AppCfg.Get("PaymentFolderPath")?.ToString();
                if (string.IsNullOrEmpty(basePath))
                    return "#";

                string folderPath = System.IO.Path.Combine(basePath, year, month);

                // Fallback: check padded month directory for files created with zero-padded month
                if (!Directory.Exists(folderPath))
                {
                    string paddedMonth = month.PadLeft(2, '0');
                    string altFolderPath = System.IO.Path.Combine(basePath, year, paddedMonth);
                    if (Directory.Exists(altFolderPath))
                    {
                        folderPath = altFolderPath;
                        month = paddedMonth;
                    }
                }

                // ตรวจสอบว่าโฟลเดอร์มีอยู่จริง
                if (!Directory.Exists(folderPath))
                    return "#";

                // ✅ ดึง id และ uid จาก ViewState เพื่อค้นหาไฟล์ที่ถูกต้อง
                string paymentId = ViewState["PaymentID"]?.ToString() ?? "";
                string paymentUid = ViewState["PaymentUID"]?.ToString() ?? "";

                // ✅ ค้นหาไฟล์ด้วย pattern ที่เฉพาะเจาะจง: id_uid_filename
                // เพื่อป้องกันการดึงไฟล์ของ Payment อื่นที่มีชื่อไฟล์เดียวกัน
                string specificPattern = "";
                if (!string.IsNullOrEmpty(paymentId) && !string.IsNullOrEmpty(paymentUid))
                {
                    specificPattern = $"{paymentId}_{paymentUid}_*{searchPattern}";
                }
                else
                {
                    // Fallback: ถ้าไม่มี id/uid ให้ใช้ pattern เดิม (สำหรับ create mode)
                    specificPattern = "*" + searchPattern;
                }

                string[] matchingFiles = Directory.GetFiles(folderPath, specificPattern);

                if (matchingFiles.Length > 0)
                {
                    // ใช้ไฟล์แรกที่พบ
                    string actualFileName = System.IO.Path.GetFileName(matchingFiles[0]);
                    string virtualPath = $"~/Documents/Payment/{year}/{month}/{actualFileName}";

                    System.Diagnostics.Debug.WriteLine($"📂 GetFileUrl: Pattern='{specificPattern}', Found='{actualFileName}'");

                    return ResolveUrl(virtualPath);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ GetFileUrl: File not found for pattern '{specificPattern}' in {folderPath}");
                    return "#";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ GetFileUrl Error: {ex.Message}");
                return "#";
            }
        }

        /// <summary>
        /// 🎨 GridView2 RowDataBound event handler for custom styling
        /// </summary>
        protected void GridView2_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            if (e.Row.RowType == DataControlRowType.DataRow)
            {
                // Style the delete button
                Button deleteButton = e.Row.Cells[1].Controls[0] as Button;
                if (deleteButton != null)
                {
                    deleteButton.CssClass = "btn-delete-file";
                    deleteButton.OnClientClick = "return confirm('คุณต้องการลบไฟล์นี้หรือไม่?');";
                }
            }
        }

        private void LoadNextAccReference(string paymentId)
        {
            try
            {
                // First check Account_Payment columns directly
                var refParams = new Dictionary<string, object> { { "@ID", paymentId } };
                DataTable dtRef = null;
                try
                {
                    dtRef = code.DatabaseQuerySafe(conn,
                        "SELECT Nexaacc_Document_Number, Nexaacc_Response_Id, Nexaacc_Payment_Id FROM Account_Payment WHERE ID = @ID",
                        refParams);
                }
                catch { }

                string nexaaccDocNumber = dtRef?.Rows.Count > 0 ? dtRef.Rows[0]["Nexaacc_Document_Number"]?.ToString() : null;
                string nexaaccResponseId = dtRef?.Rows.Count > 0 ? dtRef.Rows[0]["Nexaacc_Response_Id"]?.ToString() : null;
                string nexaaccPaymentId = dtRef?.Rows.Count > 0 ? dtRef.Rows[0]["Nexaacc_Payment_Id"]?.ToString() : null;

                // If not stored in Account_Payment yet, look up from sync queue
                if (string.IsNullOrEmpty(nexaaccDocNumber))
                {
                    try
                    {
                        var sync = new Integration.AccountingSyncService(conn);
                        DataTable dtSync = sync.GetSyncDocumentInfo(paymentId);
                        if (dtSync?.Rows.Count > 0)
                        {
                            nexaaccDocNumber = dtSync.Rows[0]["Nexaacc_Document_Number"]?.ToString();
                            nexaaccResponseId = dtSync.Rows[0]["Nexaacc_Response_Id"]?.ToString();
                            string syncStatus = dtSync.Rows[0]["Status"]?.ToString();
                            string docType = dtSync.Rows[0]["Nexaacc_Document_Type"]?.ToString();

                            // Backfill to Account_Payment for future lookups
                            if (!string.IsNullOrEmpty(nexaaccDocNumber) || !string.IsNullOrEmpty(nexaaccResponseId))
                            {
                                try
                                {
                                    code.DatabaseInsertSafe(conn,
                                        "UPDATE Account_Payment SET Nexaacc_Document_Number = @DocNum, Nexaacc_Response_Id = @RespId WHERE ID = @ID",
                                        new Dictionary<string, object>
                                        {
                                            { "@DocNum", (object)nexaaccDocNumber ?? DBNull.Value },
                                            { "@RespId", (object)nexaaccResponseId ?? DBNull.Value },
                                            { "@ID", paymentId }
                                        });
                                }
                                catch { }
                            }

                            if (!string.IsNullOrEmpty(nexaaccDocNumber))
                            {
                                lblNextAccDocNumber.Text = nexaaccDocNumber;
                                lblNextAccSyncStatus.Text = $"(สถานะ: {syncStatus}, ประเภท: {docType ?? "N/A"})";
                            }
                            else if (!string.IsNullOrEmpty(nexaaccResponseId) && !nexaaccResponseId.StartsWith("SKIPPED"))
                            {
                                lblNextAccDocNumber.Text = nexaaccResponseId;
                                lblNextAccSyncStatus.Text = $"(สถานะ: {syncStatus})";
                            }

                            // Build NextAcc link
                            if (!string.IsNullOrEmpty(nexaaccResponseId) && !nexaaccResponseId.StartsWith("SKIPPED"))
                            {
                                string url = sync.BuildNexaaccDocumentUrl(nexaaccResponseId, docType);
                                if (!string.IsNullOrEmpty(url))
                                {
                                    lnkNextAccDoc.NavigateUrl = url;
                                    lnkNextAccDoc.Visible = true;
                                }
                            }

                            pnlNextAccRef.Visible = true;
                        }
                        else
                        {
                            // Check for pending sync with single query
                            try
                            {
                                string pattern = "%" + paymentId + "%";
                                var dtPendingCheck = code.DatabaseQuerySafe(conn,
                                    "SELECT TOP 1 ID FROM Accounting_Sync_Queue WHERE Status = 'PENDING' AND Entity_Type = 'VOUCHER' AND Payload LIKE @pattern",
                                    new Dictionary<string, object> { { "@pattern", pattern } });
                                if (dtPendingCheck?.Rows.Count > 0)
                                {
                                    lblNextAccDocNumber.Text = "รอส่ง NextAcc...";
                                    lblNextAccSyncStatus.Text = "(อยู่ในคิว)";
                                    pnlNextAccRef.Visible = true;
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                }

                // Display from Account_Payment columns
                if (!string.IsNullOrEmpty(nexaaccDocNumber))
                {
                    lblNextAccDocNumber.Text = nexaaccDocNumber;
                    pnlNextAccRef.Visible = true;

                    if (!string.IsNullOrEmpty(nexaaccPaymentId))
                        lblNextAccPaymentStatus.Text = "✅ บันทึกชำระเงินแล้ว";

                    if (!string.IsNullOrEmpty(nexaaccResponseId) && !nexaaccResponseId.StartsWith("SKIPPED"))
                    {
                        try
                        {
                            var sync = new Integration.AccountingSyncService(conn);
                            string url = sync.BuildNexaaccDocumentUrl(nexaaccResponseId,
                                chkCertInLieu.Checked ? "CERTIFICATE_IN_LIEU" : "EXPENSE");
                            if (!string.IsNullOrEmpty(url))
                            {
                                lnkNextAccDoc.NavigateUrl = url;
                                lnkNextAccDoc.Visible = true;
                            }
                        }
                        catch { }
                    }
                }

                // ปุ่มดู PDF ทางการจาก NextAcc (ใบสำคัญจ่าย / ใบรับรองแทนใบเสร็จ) — ใช้ตัวดึง+cache เดียวกับหน้า CheckPayment
                bool hasNextAccDoc = !string.IsNullOrEmpty(nexaaccResponseId) && !nexaaccResponseId.StartsWith("SKIPPED");
                lnkNextAccPdf.Visible = pnlNextAccRef.Visible && hasNextAccDoc;
                if (pnlNextAccRef.Visible && chkCertInLieu.Checked)
                    lblNextAccSyncStatus.Text = (lblNextAccSyncStatus.Text + " ใบรับรองแทนใบเสร็จรับเงิน").Trim();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadNextAccReference Error: {ex.Message}");
            }
        }

        // ──────────────────────────────────────────────
        // ใบรับรองแทนใบเสร็จรับเงิน (ผู้รับเงินออกใบเสร็จไม่ได้ — แท็กซี่/แผงลอย/ลูกจ้างรายวัน)
        // เลือกแล้ว: ใบนี้ sync เป็นเอกสาร NextAcc type 15 "แทน" ใบสำคัญจ่าย/ค่าใช้จ่าย (ไม่ลง GL ซ้ำ),
        // จ่ายเงินสดเสมอ (ไม่มีเครดิต), VAT รวมเข้าค่าใช้จ่าย (ไม่มีใบกำกับภาษี)
        // ──────────────────────────────────────────────

        protected void chkCertInLieu_CheckedChanged(object sender, EventArgs e)
        {
            ApplyCertInLieuMode();
        }

        private void ApplyCertInLieuMode()
        {
            bool on = chkCertInLieu.Checked;
            pnlCertInLieu.Visible = on;
            if (on)
            {
                chkIsCredit.Checked = false;
                if (ddlVatClaim.Items.FindByValue("0") != null)
                    ddlVatClaim.SelectedValue = "0";
                if (string.IsNullOrWhiteSpace(txtCilCertifierName.Text) || string.IsNullOrWhiteSpace(txtCilCertifierPosition.Text))
                    InitCertInLieuDefaults();
            }
            chkIsCredit.Enabled = !on;
            ddlVatClaim.Enabled = !on;
        }

        /// <summary>ผู้รับรอง = ผู้ใช้ที่ login (+ตำแหน่งจาก Employee_Salary / ค่าตั้ง Nexaacc_Cil_Certifier_Position),
        /// พยาน/ผู้อนุมัติ = กรรมการ (Admin.IsCEO) — เติมเฉพาะช่องที่ยังว่าง</summary>
        private void InitCertInLieuDefaults()
        {
            try
            {
                string adminId = Session["UserID"]?.ToString();
                if (string.IsNullOrWhiteSpace(txtCilCertifierName.Text) && !string.IsNullOrEmpty(adminId))
                {
                    DataTable dtMe = code.DatabaseQuerySafe(conn,
                        "SELECT TOP 1 FirstName, LastName FROM Admin WHERE ID = @id",
                        new Dictionary<string, object> { { "@id", adminId } });
                    if (dtMe != null && dtMe.Rows.Count > 0)
                        txtCilCertifierName.Text = (dtMe.Rows[0]["FirstName"] + " " + dtMe.Rows[0]["LastName"]).Trim();
                }
                if (string.IsNullOrWhiteSpace(txtCilCertifierPosition.Text))
                    txtCilCertifierPosition.Text = new Integration.AccountingSyncService(conn).GetCertificateInLieuDefaultPosition(adminId);
                if (string.IsNullOrWhiteSpace(txtCilWitnessName.Text))
                {
                    DataTable dtCeo = code.DatabaseQuery(conn, "SELECT TOP 1 FirstName, LastName FROM Admin WHERE IsCEO = 'True'");
                    if (dtCeo != null && dtCeo.Rows.Count > 0)
                        txtCilWitnessName.Text = (dtCeo.Rows[0]["FirstName"] + " " + dtCeo.Rows[0]["LastName"]).Trim();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"InitCertInLieuDefaults Error: {ex.Message}");
            }
        }

        /// <summary>โหลดข้อมูลใบรับรองที่บันทึกไว้ (โหมดแก้ไข) — ไม่มีคอลัมน์ (ยังไม่รัน PHASE19_19) = ข้าม</summary>
        private void LoadCertInLieu(DataRow row)
        {
            try
            {
                if (row == null || !row.Table.Columns.Contains("Is_Certificate_In_Lieu")
                    || row["Is_Certificate_In_Lieu"] == DBNull.Value
                    || !Convert.ToBoolean(row["Is_Certificate_In_Lieu"]))
                    return;

                chkCertInLieu.Checked = true;
                string C(string col) => row.Table.Columns.Contains(col) && row[col] != DBNull.Value ? row[col].ToString() : "";
                txtCilReason.Text = C("Cil_Reason");
                txtCilPayeeName.Text = C("Cil_Payee_Name");
                txtCilPayeeAddress.Text = C("Cil_Payee_Address");
                if (C("Cil_Certifier_Name").Length > 0) txtCilCertifierName.Text = C("Cil_Certifier_Name");
                if (C("Cil_Certifier_Position").Length > 0) txtCilCertifierPosition.Text = C("Cil_Certifier_Position");
                if (C("Cil_Witness_Name").Length > 0) txtCilWitnessName.Text = C("Cil_Witness_Name");
                txtCilWitnessPosition.Text = C("Cil_Witness_Position");
                ApplyCertInLieuMode();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadCertInLieu Error: {ex.Message}");
            }
        }

        /// <summary>คืนข้อความ error (ไม่มี ' ) หรือ null ถ้าผ่าน</summary>
        private string ValidateCertInLieu()
        {
            if (string.IsNullOrWhiteSpace(txtCilReason.Text))
                return "กรุณาระบุเหตุผลที่ไม่ได้รับใบเสร็จ (ใบรับรองแทนใบเสร็จรับเงิน)";
            if (string.IsNullOrWhiteSpace(txtCilCertifierName.Text))
                return "กรุณาระบุชื่อผู้รับรอง (ใบรับรองแทนใบเสร็จรับเงิน)";
            if ((DropDownList3.SelectedItem?.Text ?? "").Contains("เงินเดือน"))
                return "ใบจ่ายเงินเดือนออกเป็นใบรับรองแทนใบเสร็จไม่ได้ — กรุณาเอาเครื่องหมายออก";
            try
            {
                var cfg = new Integration.AccountingConfig(conn);
                if (cfg.IsConfigured && cfg.Enabled && cfg.IsVoucherDocumentMode && !cfg.CanUseCompanyEndpoints)
                    return "ออกใบรับรองแทนใบเสร็จใน NextAcc ต้องเปิด company endpoint (Nexaacc_Company_Endpoints=1 และตั้ง CompanyId) — ติดต่อผู้ดูแลระบบ";
            }
            catch { }
            return null;
        }

        private Integration.CertificateInLieuInfo BuildCertInLieuInfo()
        {
            return new Integration.CertificateInLieuInfo
            {
                Reason = txtCilReason.Text.Trim(),
                PayeeName = txtCilPayeeName.Text.Trim(),
                PayeeAddress = txtCilPayeeAddress.Text.Trim(),
                CertifierName = txtCilCertifierName.Text.Trim(),
                CertifierPosition = txtCilCertifierPosition.Text.Trim(),
                WitnessName = txtCilWitnessName.Text.Trim(),
                WitnessPosition = txtCilWitnessPosition.Text.Trim()
            };
        }

        /// <summary>เก็บข้อมูลใบรับรองลง Account_Payment (migration PHASE19_19). ยังไม่รัน migration → log แล้วไปต่อ
        /// (การ sync ใช้ข้อมูลจาก payload คิว ไม่ได้อ่านคอลัมน์เหล่านี้)</summary>
        private void SaveCertInLieuColumns(string docNum)
        {
            try
            {
                var info = BuildCertInLieuInfo();
                code.DatabaseInsertSafe(conn,
                    @"UPDATE [dbo].[Account_Payment]
                      SET Is_Certificate_In_Lieu = 1,
                          Cil_Reason = @Reason, Cil_Payee_Name = @PayeeName, Cil_Payee_Address = @PayeeAddress,
                          Cil_Certifier_Name = @CertName, Cil_Certifier_Position = @CertPos,
                          Cil_Witness_Name = @WitName, Cil_Witness_Position = @WitPos
                      WHERE ID = @ID",
                    new Dictionary<string, object>
                    {
                        { "@Reason", info.Reason },
                        { "@PayeeName", string.IsNullOrEmpty(info.PayeeName) ? (object)DBNull.Value : info.PayeeName },
                        { "@PayeeAddress", string.IsNullOrEmpty(info.PayeeAddress) ? (object)DBNull.Value : info.PayeeAddress },
                        { "@CertName", info.CertifierName },
                        { "@CertPos", string.IsNullOrEmpty(info.CertifierPosition) ? (object)DBNull.Value : info.CertifierPosition },
                        { "@WitName", string.IsNullOrEmpty(info.WitnessName) ? (object)DBNull.Value : info.WitnessName },
                        { "@WitPos", string.IsNullOrEmpty(info.WitnessPosition) ? (object)DBNull.Value : info.WitnessPosition },
                        { "@ID", docNum }
                    });
            }
            catch (Exception ex)
            {
                try { new code().Logs(conn, "Accounting Sync", $"SaveCertInLieuColumns: doc={docNum} {ex.Message} (รัน PHASE19_Migration_19 แล้วหรือยัง?)", "SYSTEM"); } catch { }
            }
        }

        protected void lnkNextAccPdf_Click(object sender, EventArgs e)
        {
            string docNum = ViewState["PaymentID"]?.ToString();
            if (string.IsNullOrEmpty(docNum)) return;
            string msg;
            try
            {
                Server.ScriptTimeout = 300;
                var res = System.Threading.Tasks.Task.Run(() =>
                    new Integration.AccountingSyncService(conn).DownloadVoucherDocumentFromNextAccAsync(docNum, false, false)
                ).GetAwaiter().GetResult();
                if (res != null && res.Found && !string.IsNullOrEmpty(res.PdfRelativeUrl))
                {
                    Response.Redirect(res.PdfRelativeUrl, false);
                    Context.ApplicationInstance.CompleteRequest();
                    return;
                }
                msg = res?.Message ?? "ยังไม่พบเอกสารบน NextAcc";
            }
            catch (Exception ex)
            {
                msg = "ดึง PDF จาก NextAcc ไม่สำเร็จ: " + ex.Message;
            }
            ShowMsg(litFormMsg, "warn", msg ?? "ยังไม่พบเอกสารบน NextAcc", null);
            RequestScrollToMsg();
        }

        #region Asset Management Integration

        /// <summary>
        /// Load asset categories into dropdown
        /// </summary>
        private void LoadAssetCategories()
        {
            try
            {
                DataTable dt = assetService.GetAssetCategories();
                if (dt != null && dt.Rows.Count > 0)
                {
                    ddlAssetCategory.Items.Clear();
                    foreach (DataRow row in dt.Rows)
                    {
                        string text = row["CategoryCode"].ToString() + " - " + row["CategoryName"].ToString();
                        ddlAssetCategory.Items.Add(new System.Web.UI.WebControls.ListItem(text, row["ID"].ToString()));
                    }

                    // Set default useful life from first category
                    if (ddlAssetCategory.Items.Count > 0)
                    {
                        int defaultLife = Convert.ToInt32(dt.Rows[0]["DefaultUsefulLifeYears"]);
                        txtAssetUsefulLife.Text = defaultLife.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadAssetCategories Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle checkbox change for recording asset
        /// </summary>
        protected void chkRecordAsset_CheckedChanged(object sender, EventArgs e)
        {
            pnlAssetDetails.Visible = chkRecordAsset.Checked;
        }

        /// <summary>
        /// Handle category change to update default useful life
        /// </summary>
        protected void ddlAssetCategory_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(ddlAssetCategory.SelectedValue))
                {
                    DataTable dt = assetService.GetCategoryById(Convert.ToInt32(ddlAssetCategory.SelectedValue));
                    if (dt != null && dt.Rows.Count > 0)
                    {
                        txtAssetUsefulLife.Text = dt.Rows[0]["DefaultUsefulLifeYears"].ToString();
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Load existing asset data if payment voucher was linked to an asset
        /// </summary>
        private void LoadExistingAssetData(string paymentVoucherId)
        {
            try
            {
                DataTable dtAsset = assetService.GetAssetsByPaymentVoucherId(paymentVoucherId);
                if (dtAsset != null && dtAsset.Rows.Count > 0)
                {
                    // Asset exists - check the checkbox and show the panel
                    chkRecordAsset.Checked = true;
                    pnlAssetDetails.Visible = true;

                    DataRow asset = dtAsset.Rows[0];

                    // Populate asset fields
                    txtAssetName.Text = asset["AssetName"]?.ToString() ?? "";
                    txtAssetBrand.Text = asset["Brand"]?.ToString() ?? "";
                    txtAssetModel.Text = asset["Model"]?.ToString() ?? "";
                    txtAssetSerial.Text = asset["SerialNumber"]?.ToString() ?? "";
                    txtAssetLocation.Text = asset["Location"]?.ToString() ?? "";

                    // Set category dropdown
                    if (asset["CategoryID"] != DBNull.Value)
                    {
                        string categoryId = asset["CategoryID"].ToString();
                        var categoryItem = ddlAssetCategory.Items.FindByValue(categoryId);
                        if (categoryItem != null)
                        {
                            ddlAssetCategory.SelectedValue = categoryId;
                        }
                    }

                    // Set useful life and residual value
                    if (asset["UsefulLifeYears"] != DBNull.Value)
                    {
                        txtAssetUsefulLife.Text = asset["UsefulLifeYears"].ToString();
                    }
                    if (asset["ResidualValue"] != DBNull.Value)
                    {
                        txtAssetResidual.Text = Convert.ToDecimal(asset["ResidualValue"]).ToString("0");
                    }

                    // Store the existing asset ID in ViewState (to prevent duplicate creation)
                    ViewState["ExistingAssetID"] = asset["ID"].ToString();

                    // Disable checkbox to prevent unchecking (asset already created)
                    chkRecordAsset.Enabled = false;
                    chkRecordAsset.ToolTip = "สินทรัพย์ถูกบันทึกแล้ว ไม่สามารถยกเลิกได้";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadExistingAssetData Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Create asset(s) from payment voucher
        /// Supports creating multiple assets if quantity > 1
        /// </summary>
        private void CreateAssetFromPaymentVoucher(string paymentVoucherId, decimal totalPurchasePrice, DateTime purchaseDate, int vendorId)
        {
            try
            {
                if (!chkRecordAsset.Checked)
                    return;

                // Check if asset already exists (from edit mode)
                if (ViewState["ExistingAssetID"] != null)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ Asset already exists (ID: {ViewState["ExistingAssetID"]}), skipping creation");
                    return;
                }

                if (string.IsNullOrWhiteSpace(txtAssetName.Text))
                {
                    // ตรวจก่อนบันทึกแล้ว (Button3_Click) — ถึงตรงนี้ = ข้าม
                    return;
                }

                short userId = Convert.ToInt16(Session["UserID"]);
                int categoryId = Convert.ToInt32(ddlAssetCategory.SelectedValue);
                int usefulLife = string.IsNullOrEmpty(txtAssetUsefulLife.Text) ? 5 : Convert.ToInt32(txtAssetUsefulLife.Text);
                decimal totalResidualValue = string.IsNullOrEmpty(txtAssetResidual.Text) ? 0 : decimal.Parse(txtAssetResidual.Text);

                // Get quantity (default to 1)
                int quantity = 1;
                if (!string.IsNullOrEmpty(txtAssetQuantity.Text))
                {
                    int.TryParse(txtAssetQuantity.Text, out quantity);
                    if (quantity < 1) quantity = 1;
                }

                // Calculate price per unit
                decimal pricePerUnit = Math.Round(totalPurchasePrice / quantity, 2);
                decimal residualPerUnit = Math.Round(totalResidualValue / quantity, 2);

                string baseAssetName = txtAssetName.Text.Trim();
                int successCount = 0;

                for (int i = 1; i <= quantity; i++)
                {
                    // Add sequence number if multiple items
                    string assetName = quantity > 1
                        ? $"{baseAssetName} ({i}/{quantity})"
                        : baseAssetName;

                    // For serial number, only use for first item or leave blank for others
                    string serialNumber = (i == 1) ? txtAssetSerial.Text.Trim() : "";

                    var result = assetService.CreateAsset(
                        assetName: assetName,
                        description: null,
                        categoryId: categoryId,
                        serialNumber: serialNumber,
                        brand: txtAssetBrand.Text.Trim(),
                        model: txtAssetModel.Text.Trim(),
                        purchaseDate: purchaseDate,
                        purchasePrice: pricePerUnit,
                        vendorId: vendorId,
                        paymentVoucherId: paymentVoucherId,
                        invoiceNumber: null,
                        warrantyExpireDate: null,
                        usefulLifeYears: usefulLife,
                        residualValue: residualPerUnit,
                        location: txtAssetLocation.Text.Trim(),
                        department: null,
                        responsiblePersonId: null,
                        notes: quantity > 1
                            ? $"สร้างจากใบสำคัญจ่าย: {paymentVoucherId} (ชิ้นที่ {i}/{quantity})"
                            : $"สร้างจากใบสำคัญจ่าย: {paymentVoucherId}",
                        createdBy: userId
                    );

                    if (result.Success)
                    {
                        successCount++;
                        System.Diagnostics.Debug.WriteLine($"✅ Asset {i}/{quantity} created: {result.Message}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ Asset {i}/{quantity} creation failed: {result.Message}");
                    }
                }

                if (successCount > 0 && quantity > 1)
                {
                    System.Diagnostics.Debug.WriteLine($"✅ Created {successCount}/{quantity} assets successfully");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreateAssetFromPaymentVoucher Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle Edit checkbox change for voucher number
        /// Toggle ReadOnly state of txtVoucherNo
        /// </summary>
        protected void chkEditVoucherNo_CheckedChanged(object sender, EventArgs e)
        {
            if (chkEditVoucherNo.Checked)
            {
                // Enable editing
                txtVoucherNo.ReadOnly = false;
                txtVoucherNo.BackColor = System.Drawing.Color.White;
            }
            else
            {
                // Disable editing
                txtVoucherNo.ReadOnly = true;
                txtVoucherNo.BackColor = System.Drawing.Color.LightGray;
            }
        }

        /// <summary>
        /// Handle date change - regenerate voucher number based on new date
        /// </summary>
        protected void TextBox8_TextChanged(object sender, EventArgs e)
        {
            try
            {
                string command = Request.QueryString["command"];

                // Generate new voucher number for the selected date
                DateTime newDate = Convert.ToDateTime(TextBox8.Text);
                string newDocNum = documentHelper.CreateDocumentNumber("Account_Payment", "PAY", newDate);

                txtVoucherNo.Text = newDocNum;

                // If user had Edit checkbox checked, keep it editable
                if (chkEditVoucherNo.Checked)
                {
                    txtVoucherNo.ReadOnly = false;
                    txtVoucherNo.BackColor = System.Drawing.Color.White;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TextBox8_TextChanged Error: {ex.Message}");
            }
        }

        #endregion
    }
}