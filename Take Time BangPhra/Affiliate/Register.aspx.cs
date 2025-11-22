using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Data;
using System.Configuration;
using System.IO;
using Take_Time_BangPhra.Class;

namespace Take_Time_BangPhra.Affiliate
{
    public partial class Register : System.Web.UI.Page
    {
        code code = new code();
        string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;

        // ✨ Helper Classes for refactored system
        private AddressHelper _addressHelper;
        private CustomerHelper _customerHelper;
        private DocumentHelper _documentHelper;

        protected void Page_Load(object sender, EventArgs e)
        {
            // ✨ Initialize Helper Classes
            _addressHelper = new AddressHelper(conn);
            _customerHelper = new CustomerHelper(conn);
            _documentHelper = new DocumentHelper(conn);

            try
            {

                try 
                {
                    if(RadioButtonList1.SelectedIndex >= 0)
                    {

                    }
                    else
                    {
                        RadioButtonList1.SelectedIndex = 0;
                        RadioButtonList1.DataBind();
                    }
                }
                catch
                {
                    RadioButtonList1.SelectedIndex = 0;
                    RadioButtonList1.DataBind();
                }
                if (this.IsPostBack)
                {
                    TextBox8.Attributes["value"] = TextBox8.Text;
                    TextBox9.Attributes["value"] = TextBox9.Text;
                }
                if (!IsPostBack)
                {
                    //TextBox13.Text = "ข้อกำหนดและเงื่อนไขสำหรับการเข้าร่วม Affiliate Program ของ Take Time Nature Resort\r\n\r\nกรุณาอ่านข้อกำหนดและเงื่อนไขนี้อย่างละเอียดก่อนสมัครเข้าร่วม Affiliate Program หากท่านกด \"ยอมรับ\" หมายความว่าท่านตกลงและยอมรับในเงื่อนไขทั้งหมดดังต่อไปนี้\r\n1. การเข้าร่วมโปรแกรม Affiliate\r\n1.1 ผู้สมัครจะต้องให้ข้อมูลที่ถูกต้องและครบถ้วนในการสมัครสมาชิก\r\n1.2 ผู้สมัครจะได้รับรหัสส่วนลด (Affiliate Code) ที่สามารถนำไปแชร์หรือประชาสัมพันธ์ให้กับลูกค้าเพื่อใช้ในการจองที่พัก\r\n1.3 การสมัครเข้าร่วม Affiliate Program ถือเป็นการยอมรับเงื่อนไขทั้งหมดในเอกสารนี้\r\n2. การใช้งาน Affiliate Code\r\n2.1 ลูกค้าที่ใช้ Affiliate Code จะได้รับส่วนลดพิเศษสำหรับการจองที่พักตามอัตราที่กำหนดโดย Take Time Nature Resort\r\n2.2 ผู้สมัคร Affiliate มีหน้าที่ในการโปรโมท Affiliate Code ของตนเองโดยไม่ใช้วิธีการที่ผิดกฎหมาย ผิดจรรยาบรรณ หรือทำให้เกิดความเสียหายต่อชื่อเสียงของ Take Time Nature Resort\r\n2.3 Take Time Nature Resort ขอสงวนสิทธิ์ในการยกเลิก Affiliate Code หากตรวจพบการกระทำที่ขัดต่อเงื่อนไข\r\n3. การคำนวณค่าคอมมิชชั่น\r\n3.1 ค่าคอมมิชชั่นจะคำนวณจากยอดชำระค่าที่พักของลูกค้าหลังหักส่วนลด โดยคิดในอัตรา 5% ของยอดที่จ่ายจริง\r\n3.2 ค่าคอมมิชชั่นจะถูกคำนวณรวมเป็นรายเดือน โดย Take Time Nature Resort จะโอนเงินให้ผู้สมัคร Affiliate ในทุกต้นเดือน (ภายในวันที่ 5 ของเดือน)\r\n3.3 ค่าคอมมิชชั่นจะจ่ายผ่านช่องทางที่กำหนด โดยผู้สมัครต้องแจ้งข้อมูลบัญชีธนาคารหรือช่องทางการรับเงินที่ถูกต้อง\r\n4. เงื่อนไขการชำระเงิน\r\n4.1 ยอดค่าคอมมิชชั่นจะจ่ายให้เฉพาะเมื่อมียอดสะสมขั้นต่ำที่กำหนด (เช่น 500 บาท) หากยอดสะสมยังไม่ถึง จะถูกยกไปในเดือนถัดไป\r\n4.2 Take Time Nature Resort ไม่รับผิดชอบหากผู้สมัครให้ข้อมูลบัญชีธนาคารที่ผิดพลาด\r\n5. ข้อจำกัดความรับผิดชอบ\r\n5.1 Take Time Nature Resort ไม่รับผิดชอบในความเสียหายที่เกิดจากการโปรโมท Affiliate Code ของผู้สมัคร\r\n5.2 Take Time Nature Resort ขอสงวนสิทธิ์ในการเปลี่ยนแปลงข้อกำหนดและเงื่อนไขโดยไม่ต้องแจ้งให้ทราบล่วงหน้า\r\n6. การยกเลิกและการสิ้นสุดโปรแกรม\r\n6.1 ผู้สมัครสามารถยกเลิกการเข้าร่วมโปรแกรม Affiliate ได้ทุกเมื่อโดยการแจ้ง Take Time Nature Resort ล่วงหน้า\r\n6.2 Take Time Nature Resort ขอสงวนสิทธิ์ในการยกเลิกการเข้าร่วมของผู้สมัครในกรณีที่มีการฝ่าฝืนเงื่อนไขหรือกระทำการใด ๆ ที่ทำให้บริษัทได้รับความเสียหาย\r\n7. การติดต่อ\r\nหากมีข้อสงสัยเพิ่มเติมเกี่ยวกับ Affiliate Program กรุณาติดต่อ\r\nโทรศัพท์: 0861489669\r\n";
                    TextBox13.Text = "1. การเข้าร่วมแคมเปญ Time for Friends, Time for Reward\r\n1.1 ผู้สมัครจะต้องให้ข้อมูลที่ถูกต้องและครบถ้วนในการสมัครสมาชิก\r\n1.2 ผู้สมัครจะได้รับรหัสส่วนลดที่สามารถนำไปแชร์หรือประชาสัมพันธ์ให้กับลูกค้าเพื่อใช้ในการจองที่พัก\r\n1.3 การสมัครเข้าร่วมแคมเปญถือเป็นการยอมรับเงื่อนไขทั้งหมดนี้\r\n\r\n2. การใช้งานรหัสส่วนลด\r\n2.1 ลูกค้าจะได้รับส่วนลดพิเศษสำหรับการจองที่พักตามอัตราที่กำหนดโดย Take Time Nature Resort เมื่อระบุรหัสส่วนลดนี้ในการจองเข้าพัก\r\n2.2 ผู้สมัครสามารถโปรโมทรหัสส่วนลดของตนเองโดยไม่ใช้วิธีการที่ผิดกฎหมาย ผิดจรรยาบรรณ หรือทำให้เกิดความเสียหายต่อชื่อเสียงของ Take Time Nature Resort\r\n\r\n3. การคำนวณค่าคอมมิชชั่นและเงื่อนไขการชำระเงิน\r\n3.1 ค่าคอมมิชชั่นจะคำนวณจากยอดชำระค่าที่พักของลูกค้าหลังหักส่วนลด โดยคิดในอัตรา 5% ของยอดที่จ่ายจริง\r\n3.2 ค่าคอมมิชชั่นจะถูกคำนวณรวมเป็นรายเดือน โดย Take Time Nature Resort จะโอนเงินให้ผู้สมัครภายในวันที่ 5 ของเดือนถัดไป เมื่อมียอดสะสมขั้นต่ำที่ 500 บาทขึ้นไป หากไม่ถึงจะถูกยกไปรวมในเดือนถัดไป\r\n3.3 Take Time Nature Resort ไม่รับผิดชอบหากผู้สมัครให้ข้อมูลบัญชีธนาคารที่ผิดพลาด\r\n\r\n4. ข้อจำกัดความรับผิดชอบ\r\n4.1 Take Time Nature Resort ไม่รับผิดชอบในความเสียหายที่เกิดจากการโปรโมทรหัสส่วนลดของผู้สมัคร\r\n4.2 Take Time Nature Resort ขอสงวนสิทธิ์ในการเปลี่ยนแปลงข้อกำหนดและเงื่อนไขโดยไม่ต้องแจ้งให้ทราบล่วงหน้า\r\n\r\n5. การยกเลิกและการสิ้นสุดการเข้าร่วมแคมเปญ\r\n5.1 ผู้สมัครสามารถยกเลิกการเข้าร่วมแคมเปญได้ทุกเมื่อโดยการแจ้ง Take Time Nature Resort ล่วงหน้า\r\n5.2 Take Time Nature Resort ขอสงวนสิทธิ์ในการยกเลิกการเข้าร่วมของผู้สมัคร ในกรณีที่มีการฝ่าฝืนเงื่อนไขหรือกระทำการใด ๆ ที่ทำให้บริษัทฯได้รับความเสียหาย\r\n\r\nหากมีข้อสงสัยเพิ่มเติม กรุณาติดต่อ 086-148-9669\r\n";
                    //DataTable dtCustomerType = code.DatabaseQuery(conn, "Select [Customer_Type],ID From Customer_Type");
                    //for (int i = 0; i < dtCustomerType.Rows.Count; i++)
                    //{
                    //    DropDownList1.Items.Add(new ListItem(dtCustomerType.Rows[i][0].ToString(), dtCustomerType.Rows[i][1].ToString()));
                    //}
                    //DropDownList1.DataBind();


                    getAddress("SELECT DISTINCT [Province] FROM [Address] order by Province ASC", "SELECT DISTINCT [District] FROM [Address] order by District ASC", "SELECT DISTINCT [SubDistrict] FROM [Address] order by SubDistrict ASC");

                }
            }
            catch { Response.Redirect("/Default");  }
        }

        protected void TextBox1_TextChanged(object sender, EventArgs e)
        {
            if(TextBox1.Text.Length == 13)
            {
                // SECURE: Check vendor with parameterized query
                var vendorParams = new Dictionary<string, object>
                {
                    { "@IDNumber", TextBox1.Text }
                };
                DataTable dt = code.DatabaseQuerySafe(conn,
                    "SELECT * FROM Vendor LEFT JOIN Customer_Type ON Customer_Type.ID = Vendor_Type_ID " +
                    "LEFT JOIN Address ON Address.ID = Address_ID WHERE IDNumber = @IDNumber",
                    vendorParams);
                if(dt.Rows.Count > 0)
                {
                    TextBox2.Text = dt.Rows[0]["Name"].ToString();
                    TextBox3.Text = dt.Rows[0]["Branch_Number"].ToString();
                    TextBox7.Text = dt.Rows[0]["Phone_Number"].ToString();
                    TextBox4.Text = dt.Rows[0]["Address"].ToString();
                    TextBox5.Text = dt.Rows[0]["Address1"].ToString();

                    try //Address
                    {
                        TextBox6.Text = dt.Rows[0]["PostalCode"].ToString();
                        DropDownList2.ClearSelection();
                        DropDownList2.Items.FindByText(dt.Rows[0]["Province"].ToString()).Selected = true;
                        DropDownList2.SelectedIndex = DropDownList2.Items.IndexOf(DropDownList2.Items.FindByText(dt.Rows[0]["Province"].ToString()));
                        DropDownList3.ClearSelection();
                        DropDownList3.Items.FindByText(dt.Rows[0]["District"].ToString()).Selected = true;
                        DropDownList3.SelectedIndex = DropDownList3.Items.IndexOf(DropDownList3.Items.FindByText(dt.Rows[0]["District"].ToString()));
                        DropDownList4.ClearSelection();
                        DropDownList4.Items.FindByText(dt.Rows[0]["SubDistrict"].ToString()).Selected = true;
                        DropDownList4.SelectedIndex = DropDownList4.Items.IndexOf(DropDownList4.Items.FindByText(dt.Rows[0]["SubDistrict"].ToString()));

                        RadioButtonList1.ClearSelection();
                        RadioButtonList1.Items.FindByValue(dt.Rows[0]["Customer_Type_ID"].ToString()).Selected = true;
                        RadioButtonList1.SelectedIndex = RadioButtonList1.Items.IndexOf(RadioButtonList1.Items.FindByValue(dt.Rows[0]["Customer_Type_ID"].ToString()));

                        //DropDownList1.ClearSelection();
                        //DropDownList1.Items.FindByValue(dt.Rows[0]["Customer_Type_ID"].ToString()).Selected = true;
                        //DropDownList1.SelectedIndex = DropDownList1.Items.IndexOf(DropDownList1.Items.FindByValue(dt.Rows[0]["Customer_Type_ID"].ToString()));

                    }
                    catch { }

                }
            }
            else
            {
                TextBox1.Text = "";
                ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('คุณระบุเลขประจำตัวผู้เสียภาษีไม่ถูกต้อง');", true);
            }
        }

        // ✨ MIGRATED: CheckAddressID() has been replaced with AddressHelper.GetAddressIdString()
        // See AddressHelper class in Take_Time_BangPhra.Class namespace

        public void getAddress(string commp, string commd, string commsd)
        {
            string Command = Request.QueryString["Command"];
            string ID = Request.QueryString["ID"];
            DataTable dtProvince = code.DatabaseQuery(conn, commp);
            DataTable dtDistrict = code.DatabaseQuery(conn, commd);
            DataTable dtSubDistrict = code.DatabaseQuery(conn, commsd);
            try
            {
                if (dtProvince.Rows.Count <= 0)
                {
                    ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('ไม่พบหมายเลขไปรษณีย์ที่คุณระบุ');", true);
                    TextBox6.Enabled = true;
                }
                else
                {

                        List<string> ddl = new List<string>();

                        for (int i = 0; i < dtProvince.Rows.Count; i++)
                        {
                            ddl.Add(dtProvince.Rows[i][0].ToString());
                        }
                        DropDownList2.DataSource = ddl;
                        DropDownList2.DataBind();
                        //DropDownList5.SelectedIndex = 0;

                        ddl.Clear();

                        for (int i = 0; i < dtDistrict.Rows.Count; i++)
                        {
                            ddl.Add(dtDistrict.Rows[i][0].ToString());
                        }
                        DropDownList3.DataSource = ddl;
                        DropDownList3.DataBind();
                        //DropDownList6.SelectedIndex = 0;

                        ddl.Clear();

                        for (int i = 0; i < dtSubDistrict.Rows.Count; i++)
                        {
                            ddl.Add(dtSubDistrict.Rows[i][0].ToString());
                        }
                        DropDownList4.DataSource = ddl;
                        DropDownList4.DataBind();
                        //DropDownList7.SelectedIndex = 0;
                }
            }
            catch { }
        }

        protected void Button6_Click(object sender, EventArgs e)
        {
            TextBox6.Enabled = true;
            TextBox6.Text = string.Empty;
            DropDownList2.Items.Clear();
            DropDownList3.Items.Clear();
            DropDownList4.Items.Clear();
        }

        protected void Button5_Click(object sender, EventArgs e)
        {
            TextBox6.Enabled = false;

            // SECURE: Get address with parameterized query
            var addressParams = new Dictionary<string, object>
            {
                { "@PostalCode", TextBox6.Text ?? "" }
            };

            DataTable dtProvince = code.DatabaseQuerySafe(conn,
                "SELECT DISTINCT [Province] FROM [Address] WHERE PostalCode = @PostalCode ORDER BY Province ASC",
                addressParams);
            DataTable dtDistrict = code.DatabaseQuerySafe(conn,
                "SELECT DISTINCT [District] FROM [Address] WHERE PostalCode = @PostalCode ORDER BY District ASC",
                addressParams);
            DataTable dtSubDistrict = code.DatabaseQuerySafe(conn,
                "SELECT DISTINCT [SubDistrict] FROM [Address] WHERE PostalCode = @PostalCode ORDER BY SubDistrict ASC",
                addressParams);

            populateAddressDropdowns(dtProvince, dtDistrict, dtSubDistrict);
        }

        protected void Button2_Click(object sender, EventArgs e)
        {
            if (TextBox1.Text.Length == 13)
            {
                if (TextBox10.Text.Contains(" ") || TextBox10.Text.Contains("-"))
                {
                    if (TextBox8.Text == TextBox9.Text)
                    {
                        if (DropDownList6.SelectedValue != "0" && TextBox12.Text.Length > 8)
                        {
                            if (Button7.Enabled == false && Button8.Enabled == false)
                            {
                                
                                string firstname = "TI";
                                string lastname = "ME";
                                if(TextBox10.Text.Contains(" "))
                                {
                                    string[] input = TextBox10.Text.Split(' ');
                                    firstname = input[0];
                                    lastname = input[1];
                                }
                                else if(TextBox10.Text.Contains("-"))
                                {
                                    string[] input = TextBox10.Text.Split('-');
                                    firstname = input[0];
                                    lastname = input[1];
                                }
                                int ID = 0;
                                // SECURE: Check vendor with parameterized query
                                var checkVendorParams = new Dictionary<string, object>
                                {
                                    { "@IDNumber", TextBox1.Text }
                                };
                                DataTable dt = code.DatabaseQuerySafe(conn,
                                    "SELECT * FROM Vendor LEFT JOIN Customer_Type ON Customer_Type.ID = Vendor_Type_ID " +
                                    "LEFT JOIN Address ON Address.ID = Address_ID WHERE IDNumber = @IDNumber",
                                    checkVendorParams);
                                if (dt.Rows.Count > 0)
                                {
                                    // SECURE: Update vendor with parameterized query
                                    var updateVendorParams = new Dictionary<string, object>
                                    {
                                        { "@IDNumber", TextBox1.Text },
                                        { "@VendorTypeID", RadioButtonList1.SelectedValue },
                                        { "@Name", TextBox2.Text },
                                        { "@Address", TextBox4.Text },
                                        { "@Address1", TextBox5.Text },
                                        { "@AddressID", _addressHelper.GetAddressIdString(TextBox6.Text, DropDownList2.SelectedValue, DropDownList3.SelectedValue, DropDownList4.SelectedValue) },
                                        { "@PhoneNumber", TextBox7.Text },
                                        { "@BranchNumber", TextBox3.Text },
                                        { "@WhereIDNumber", TextBox1.Text }
                                    };
                                    code.DatabaseInsertSafe(conn,
                                        "UPDATE [dbo].[Vendor] SET [IDNumber] = @IDNumber,[Vendor_Type_ID] = @VendorTypeID,[Name] = @Name," +
                                        "[Address] = @Address,[Address1] = @Address1,[Address_ID] = @AddressID,[Phone_Number] = @PhoneNumber," +
                                        "[Vendor_Group] = N'15-Affiliate',[Branch_Number] = @BranchNumber WHERE IDNumber = @WhereIDNumber",
                                        updateVendorParams);
                                }
                                else
                                {
                                    code.Logs(conn, "Affiliate-Register-Success", TextBox1.Text, TextBox1.Text);

                                    // SECURE: Insert vendor with parameterized query
                                    var insertVendorParams = new Dictionary<string, object>
                                    {
                                        { "@IDNumber", TextBox1.Text },
                                        { "@VendorTypeID", RadioButtonList1.SelectedValue },
                                        { "@Name", TextBox2.Text },
                                        { "@BranchNumber", TextBox3.Text },
                                        { "@PhoneNumber", TextBox7.Text },
                                        { "@Address", TextBox4.Text },
                                        { "@Address1", TextBox5.Text },
                                        { "@AddressID", _addressHelper.GetAddressIdString(TextBox6.Text, DropDownList2.SelectedValue, DropDownList3.SelectedValue, DropDownList4.SelectedValue) }
                                    };
                                    ID = code.DatabaseInsertReturnSafe(conn,
                                        "INSERT INTO [dbo].[Vendor](IDNumber,Vendor_Type_ID,Name,Branch_Number,Phone_Number,Address,Address1,Address_ID,Vendor_Group) " +
                                        "VALUES (@IDNumber,@VendorTypeID,@Name,@BranchNumber,@PhoneNumber,@Address,@Address1,@AddressID,N'15-Affiliate'); SELECT SCOPE_IDENTITY();",
                                        insertVendorParams);
                                }

                                // SECURE: Insert affiliate member with parameterized query
                                var insertMemberParams = new Dictionary<string, object>
                                {
                                    { "@IDNumber", TextBox1.Text },
                                    { "@Password", TextBox8.Text },
                                    { "@RegisterDate", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") },
                                    { "@VendorID", ID },
                                    { "@FirstName", firstname },
                                    { "@LastName", lastname },
                                    { "@CouponCode", GenCouponCode(firstname, lastname) },
                                    { "@BankCode", DropDownList6.SelectedValue },
                                    { "@BankNumber", TextBox12.Text.Replace("-", "").Replace(" ", "") }
                                };
                                code.DatabaseInsertSafe(conn,
                                    "INSERT INTO [dbo].[Affiliate_Member] ([ID_Number],[Password],[Register_Date],[Vendor_ID],[FirstName],[LastName],[Coupon_Code],[Bank_Code],[Bank_Number]) " +
                                    "VALUES (@IDNumber,@Password,@RegisterDate,@VendorID,@FirstName,@LastName,@CouponCode,@BankCode,@BankNumber)",
                                    insertMemberParams);
                                Response.Redirect("../Affiliate/Login?id=" + TextBox1.Text);
                            }
                            else
                            {
                                ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('กรุณาอัพโหลด ID Card และ Book Bank);", true);
                            }
                        }
                        else
                        {
                            ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('กรุณาเลือก Bank หรือ ใส่ เลขบัญชีให้ถูกต้อง');", true);
                        }
                    }
                    else
                    {
                        ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('ระบุรหัสผ่านทั้ง 2 ช่อง ไม่ตรงกัน');", true);
                    }
                }
                else
                {
                    ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('กรุณาใส่ชื่อและนามสกุลภาษาอังกฤษ');", true);
                }
            }
            else
            {
                ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('เลขผู้เสียภาษีไม่ครบ 13 หลัก');", true);
            }
        }

        public string GenCouponCode(string firstname,string lastname)
        {
            string couponcode = firstname.Substring(0,3)+lastname.Substring(0,3);

            // SECURE: Check coupon code with parameterized query
            var couponParams = new Dictionary<string, object>
            {
                { "@CouponCode", couponcode + "%" }
            };
            DataTable dt = code.DatabaseQuerySafe(conn,
                "SELECT * FROM [Taketime].[dbo].[Affiliate_Member] WHERE Coupon_Code LIKE @CouponCode",
                couponParams);
            if(dt.Rows.Count > 0)
            {
                if(dt.Rows.Count<10)
                {
                    couponcode = couponcode + "0" + dt.Rows.Count.ToString();
                }
                else
                {
                    couponcode = couponcode + dt.Rows.Count.ToString();
                }
            }
            else
            {
                couponcode = couponcode + "00";
            }
            return couponcode.ToUpper();
        }

        protected void TextBox3_TextChanged(object sender, EventArgs e)
        {
            if (TextBox3.Text.Length == 5)
            {
                
            }
            else { ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('เลขสาขาไม่ครบ 5 หลัก');", true); }
        }

        protected void TextBox7_TextChanged(object sender, EventArgs e)
        {
            TextBox7.Text = TextBox7.Text.Replace("-","").Replace(" ", "");
        }

        protected void DropDownList2_SelectedIndexChanged(object sender, EventArgs e)
        {
            DataTable dtProvince, dtDistrict, dtSubDistrict;

            if (TextBox6.Enabled == false)
            {
                // SECURE: Filter by PostalCode and Province with parameterized query
                var addressParams = new Dictionary<string, object>
                {
                    { "@PostalCode", TextBox6.Text ?? "" },
                    { "@Province", DropDownList2.SelectedValue ?? "" }
                };

                dtProvince = code.DatabaseQuerySafe(conn,
                    "SELECT DISTINCT [Province] FROM [Address] WHERE PostalCode = @PostalCode AND Province = @Province ORDER BY Province ASC",
                    addressParams);
                dtDistrict = code.DatabaseQuerySafe(conn,
                    "SELECT DISTINCT [District] FROM [Address] WHERE PostalCode = @PostalCode AND Province = @Province ORDER BY District ASC",
                    addressParams);
                dtSubDistrict = code.DatabaseQuerySafe(conn,
                    "SELECT DISTINCT [SubDistrict] FROM [Address] WHERE PostalCode = @PostalCode AND Province = @Province ORDER BY SubDistrict ASC",
                    addressParams);
            }
            else
            {
                // SECURE: Filter by Province only with parameterized query
                var addressParams = new Dictionary<string, object>
                {
                    { "@Province", DropDownList2.SelectedValue ?? "" }
                };

                dtProvince = code.DatabaseQuerySafe(conn,
                    "SELECT DISTINCT [Province] FROM [Address] WHERE Province = @Province ORDER BY Province ASC",
                    addressParams);
                dtDistrict = code.DatabaseQuerySafe(conn,
                    "SELECT DISTINCT [District] FROM [Address] WHERE Province = @Province ORDER BY District ASC",
                    addressParams);
                dtSubDistrict = code.DatabaseQuerySafe(conn,
                    "SELECT DISTINCT [SubDistrict] FROM [Address] WHERE Province = @Province ORDER BY SubDistrict ASC",
                    addressParams);
            }

            populateAddressDropdowns(dtProvince, dtDistrict, dtSubDistrict);
        }

        protected void DropDownList3_SelectedIndexChanged(object sender, EventArgs e)
        {
            DataTable dtProvince, dtDistrict, dtSubDistrict;

            if (TextBox6.Enabled == false)
            {
                // SECURE: Filter by PostalCode and District with parameterized query
                var addressParams = new Dictionary<string, object>
                {
                    { "@PostalCode", TextBox6.Text ?? "" },
                    { "@District", DropDownList3.SelectedValue ?? "" }
                };

                dtProvince = code.DatabaseQuerySafe(conn,
                    "SELECT DISTINCT [Province] FROM [Address] WHERE PostalCode = @PostalCode AND District = @District ORDER BY Province ASC",
                    addressParams);
                dtDistrict = code.DatabaseQuerySafe(conn,
                    "SELECT DISTINCT [District] FROM [Address] WHERE PostalCode = @PostalCode AND District = @District ORDER BY District ASC",
                    addressParams);
                dtSubDistrict = code.DatabaseQuerySafe(conn,
                    "SELECT DISTINCT [SubDistrict] FROM [Address] WHERE PostalCode = @PostalCode AND District = @District ORDER BY SubDistrict ASC",
                    addressParams);
            }
            else
            {
                // SECURE: Filter by District only with parameterized query
                var addressParams = new Dictionary<string, object>
                {
                    { "@District", DropDownList3.SelectedValue ?? "" }
                };

                dtProvince = code.DatabaseQuerySafe(conn,
                    "SELECT DISTINCT [Province] FROM [Address] WHERE District = @District ORDER BY Province ASC",
                    addressParams);
                dtDistrict = code.DatabaseQuerySafe(conn,
                    "SELECT DISTINCT [District] FROM [Address] WHERE District = @District ORDER BY District ASC",
                    addressParams);
                dtSubDistrict = code.DatabaseQuerySafe(conn,
                    "SELECT DISTINCT [SubDistrict] FROM [Address] WHERE District = @District ORDER BY SubDistrict ASC",
                    addressParams);
            }

            populateAddressDropdowns(dtProvince, dtDistrict, dtSubDistrict);
        }

        // ✨ SECURE: Helper method to populate address dropdowns
        private void populateAddressDropdowns(DataTable dtProvince, DataTable dtDistrict, DataTable dtSubDistrict)
        {
            try
            {
                if (dtProvince.Rows.Count <= 0)
                {
                    ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('ไม่พบหมายเลขไปรษณีย์ที่คุณระบุ');", true);
                    TextBox6.Enabled = true;
                }
                else
                {
                    List<string> ddl = new List<string>();

                    for (int i = 0; i < dtProvince.Rows.Count; i++)
                    {
                        ddl.Add(dtProvince.Rows[i][0].ToString());
                    }
                    DropDownList2.DataSource = ddl;
                    DropDownList2.DataBind();

                    ddl.Clear();

                    for (int i = 0; i < dtDistrict.Rows.Count; i++)
                    {
                        ddl.Add(dtDistrict.Rows[i][0].ToString());
                    }
                    DropDownList3.DataSource = ddl;
                    DropDownList3.DataBind();

                    ddl.Clear();

                    for (int i = 0; i < dtSubDistrict.Rows.Count; i++)
                    {
                        ddl.Add(dtSubDistrict.Rows[i][0].ToString());
                    }
                    DropDownList4.DataSource = ddl;
                    DropDownList4.DataBind();
                }
            }
            catch { }
        }

        protected void Button7_Click(object sender, EventArgs e)
        {
            if (TextBox1.Text.Length > 0)
            {
                if (FileUpload1.HasFile)
                {
                    if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\Affiliate\\IDCard\\" + TextBox1.Text + ".jpg"))
                    {
                        File.Delete(AppDomain.CurrentDomain.BaseDirectory + "\\Affiliate\\IDCard\\" + TextBox1.Text + ".jpg");
                    }

                    string FileSaveWithPath = "";
                    string filename = TextBox1.Text + ".jpg";
                    FileSaveWithPath = Server.MapPath("\\Affiliate\\IDCard\\" + filename);
                    FileUpload1.SaveAs(FileSaveWithPath);
                    if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\Affiliate\\IDCard\\" + TextBox1.Text + ".jpg"))
                    {
                        FileUpload1.Enabled = false;
                        Button7.Enabled = false;
                        Button7.Text = "Uploaded";
                    }

                }
                else
                {
                    ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('กรุณาเลือกไฟล์ที่ต้องการจะอัพโหลดก่อน');", true);
                }
            }
            else
            {
                ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('กรุณาเระบุเลขประจำตัวผู้เสียภาษีก่อน');", true);
            }
        }

        protected void Button8_Click(object sender, EventArgs e)
        {
            if (TextBox1.Text.Length > 0)
            {
                if (FileUpload2.HasFile)
                {
                    if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\Affiliate\\BookBank\\" + TextBox1.Text + ".jpg"))
                    {
                        File.Delete(AppDomain.CurrentDomain.BaseDirectory + "\\Affiliate\\BookBank\\" + TextBox1.Text + ".jpg");
                    }

                    string FileSaveWithPath = "";
                    string filename = TextBox1.Text + ".jpg";
                    FileSaveWithPath = Server.MapPath("\\Affiliate\\BookBank\\" + filename);
                    FileUpload2.SaveAs(FileSaveWithPath);
                    if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\Affiliate\\BookBank\\" + TextBox1.Text + ".jpg"))
                    {
                        FileUpload2.Enabled = false;
                        Button8.Enabled = false;
                        Button8.Text = "Uploaded";
                    }

                }
                else
                {
                    ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('กรุณาเลือกไฟล์ที่ต้องการจะอัพโหลดก่อน');", true);
                }
            }
            else
            {
                ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('กรุณาเระบุเลขประจำตัวผู้เสียภาษีก่อน');", true);
            }
        }

        protected void DropDownList1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if(RadioButtonList1.SelectedValue == "2")
            {
                TextBox3.Visible = false;
            }
            else
            {
                TextBox3.Visible = true;
            }
        }

        protected void CheckBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (CheckBox1.Checked == true)
            {
                Button2.Enabled = true;
            }
            else
            {
                Button2.Enabled = false;
            }
        }

        protected void RadioButtonList1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (RadioButtonList1.SelectedValue == "2")
            {
                TextBox3.Visible = false;
            }
            else
            {
                TextBox3.Visible = true;
            }
        }

        protected void TextBox8_TextChanged(object sender, EventArgs e)
        {
            Session["Password1"] = TextBox8.Text;
        }

        protected void TextBox9_TextChanged(object sender, EventArgs e)
        {
            Session["Password2"] = TextBox9.Text;
        }
    }
}