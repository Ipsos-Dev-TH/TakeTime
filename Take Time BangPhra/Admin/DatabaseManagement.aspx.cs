using System;
using System.Collections.Generic;
using System.Configuration;
using System.Web.UI;

namespace Take_Time_BangPhra.Admin
{
    public partial class DatabaseManagement : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            try
            {
                if (Session["permission"] == null ||
                    Session["permission"].ToString() != "True" ||
                    Session["User"] == null ||
                    Session["User"].ToString() != "Owner")
                {
                    Response.Redirect("/Default");
                    return;
                }
            }
            catch
            {
                Response.Redirect("/Default");
                return;
            }

            if (!IsPostBack)
            {
                RenderCategories();
            }
        }

        private void RenderCategories()
        {
            var categories = GetTableCategories();
            var sb = new System.Text.StringBuilder();

            foreach (var category in categories)
            {
                sb.Append("<div class=\"db-category\">");

                // Header
                sb.AppendFormat(
                    "<div class=\"db-category-header\" onclick=\"toggleCategory(this)\">" +
                    "<div class=\"category-icon\"><i class=\"fas {0}\"></i></div>" +
                    "<h3>{1}</h3>" +
                    "<span class=\"category-count\">{2} ตาราง</span>" +
                    "<i class=\"fas fa-chevron-down toggle-icon\"></i>" +
                    "</div>",
                    category.Icon, category.Name, category.Tables.Count);

                // Grid
                sb.Append("<div class=\"db-table-grid\">");
                foreach (var table in category.Tables)
                {
                    string editUrl = ResolveUrl("~/Admin/Edit_Data.aspx?data=" + table.TableName);
                    sb.AppendFormat(
                        "<a href=\"{0}\" class=\"db-table-card\" data-table=\"{1}\" data-desc=\"{2}\">" +
                        "<div class=\"card-icon {3}\"><i class=\"fas {4}\"></i></div>" +
                        "<div class=\"card-info\">" +
                        "<div class=\"table-name\">{1}</div>" +
                        "<div class=\"table-desc\">{2}</div>" +
                        "</div>" +
                        "<i class=\"fas fa-chevron-right card-arrow\"></i>" +
                        "</a>",
                        editUrl, table.TableName, table.Description, table.ColorClass, table.Icon);
                }
                sb.Append("</div>"); // grid
                sb.Append("</div>"); // category
            }

            pnlCategories.Controls.Add(new LiteralControl(sb.ToString()));
        }

        private List<TableCategory> GetTableCategories()
        {
            return new List<TableCategory>
            {
                new TableCategory
                {
                    Name = "ที่พักและห้องพัก",
                    Icon = "fa-bed",
                    Tables = new List<TableInfo>
                    {
                        new TableInfo("Accommodation", "ประเภทที่พัก/ห้องพัก", "icon-blue", "fa-bed"),
                        new TableInfo("Accommodation_HolidayPrice", "ราคาวันหยุดพิเศษ", "icon-blue", "fa-calendar-day"),
                        new TableInfo("Accommodation_RatePlan", "แผนราคาที่พัก", "icon-blue", "fa-tags"),
                        new TableInfo("Accommodation_Holiday", "วันหยุดพิเศษ", "icon-blue", "fa-umbrella-beach"),
                        new TableInfo("Accommodation_DayType", "ประเภทวัน (ธรรมดา/หยุด)", "icon-blue", "fa-calendar-check"),
                        new TableInfo("Accommodation_Type", "หมวดหมู่ที่พัก", "icon-blue", "fa-layer-group"),
                    }
                },
                new TableCategory
                {
                    Name = "ลูกค้าและที่อยู่",
                    Icon = "fa-users",
                    Tables = new List<TableInfo>
                    {
                        new TableInfo("Customer", "ข้อมูลลูกค้า", "icon-green", "fa-user"),
                        new TableInfo("Address", "ที่อยู่ (รหัสไปรษณีย์/จังหวัด/อำเภอ/ตำบล)", "icon-green", "fa-map-marker-alt"),
                        new TableInfo("Customer_Type", "ประเภทลูกค้า", "icon-green", "fa-user-tag"),
                    }
                },
                new TableCategory
                {
                    Name = "สินค้าและคลัง",
                    Icon = "fa-box",
                    Tables = new List<TableInfo>
                    {
                        new TableInfo("Product", "รายการสินค้า", "icon-orange", "fa-box"),
                        new TableInfo("Product_Category", "หมวดหมู่สินค้า", "icon-orange", "fa-th-large"),
                        new TableInfo("Items", "ของเช่า/สิ่งของ", "icon-orange", "fa-cubes"),
                        new TableInfo("Product_Images", "รูปภาพสินค้า", "icon-orange", "fa-images"),
                    }
                },
                new TableCategory
                {
                    Name = "บัญชีและการเงิน",
                    Icon = "fa-file-invoice-dollar",
                    Tables = new List<TableInfo>
                    {
                        new TableInfo("Account_Paid_How", "วิธีการชำระเงิน", "icon-purple", "fa-credit-card"),
                        new TableInfo("Account_Paid_Type", "ประเภทการชำระ", "icon-purple", "fa-money-bill"),
                        new TableInfo("Account_Vat_Type", "ประเภทภาษีมูลค่าเพิ่ม", "icon-purple", "fa-percent"),
                        new TableInfo("Account_ProductType", "ประเภทสินค้าในบัญชี", "icon-purple", "fa-tags"),
                        new TableInfo("Account_PaymentMethod", "วิธีรับชำระ", "icon-purple", "fa-wallet"),
                        new TableInfo("Business_Info", "ข้อมูลบริษัท", "icon-purple", "fa-building"),
                    }
                },
                new TableCategory
                {
                    Name = "การจอง",
                    Icon = "fa-calendar-alt",
                    Tables = new List<TableInfo>
                    {
                        new TableInfo("Reservation", "ข้อมูลการจอง", "icon-teal", "fa-calendar-check"),
                        new TableInfo("Reservation_Accommodation", "การจองกับห้องพัก", "icon-teal", "fa-link"),
                        new TableInfo("Reservation_Items", "สิ่งของในการจอง", "icon-teal", "fa-list"),
                        new TableInfo("Reservation_Product_Charges", "ค่าสินค้าในห้อง", "icon-teal", "fa-shopping-cart"),
                        new TableInfo("Reservation_Reschedule_History", "ประวัติการเลื่อนจอง", "icon-teal", "fa-history"),
                    }
                },
                new TableCategory
                {
                    Name = "Voucher",
                    Icon = "fa-ticket-alt",
                    Tables = new List<TableInfo>
                    {
                        new TableInfo("Voucher", "บัตรกำนัล/Voucher", "icon-pink", "fa-ticket-alt"),
                        new TableInfo("Voucher_RatePlan_Group", "กลุ่มแผนราคา Voucher", "icon-pink", "fa-layer-group"),
                    }
                },
                new TableCategory
                {
                    Name = "Affiliate",
                    Icon = "fa-handshake",
                    Tables = new List<TableInfo>
                    {
                        new TableInfo("Affiliate_Member", "สมาชิก Affiliate", "icon-amber", "fa-user-plus"),
                        new TableInfo("Affiliate_Reservation", "การจองจาก Affiliate", "icon-amber", "fa-calendar"),
                        new TableInfo("Affiliate_Discount", "ส่วนลด Affiliate", "icon-amber", "fa-percentage"),
                        new TableInfo("Affiliate_Discount_RatePlan", "แผนส่วนลด Affiliate", "icon-amber", "fa-tags"),
                    }
                },
                new TableCategory
                {
                    Name = "Supplier/Vendor",
                    Icon = "fa-truck",
                    Tables = new List<TableInfo>
                    {
                        new TableInfo("Vendor", "ผู้ขาย/Vendor", "icon-red", "fa-store"),
                        new TableInfo("Supplier", "Supplier/ผู้จัดจำหน่าย", "icon-red", "fa-truck"),
                        new TableInfo("Supplier_Type", "ประเภท Supplier", "icon-red", "fa-th-list"),
                        new TableInfo("Supplier_Contact", "ข้อมูลติดต่อ Supplier", "icon-red", "fa-address-card"),
                    }
                },
                new TableCategory
                {
                    Name = "ระบบผู้ดูแลและ Mapping",
                    Icon = "fa-cogs",
                    Tables = new List<TableInfo>
                    {
                        new TableInfo("Admin", "รหัสผู้ดูแลระบบ", "icon-brown", "fa-user-shield"),
                        new TableInfo("MapDataWithSTAAH", "Mapping ข้อมูลกับ STAAH", "icon-brown", "fa-exchange-alt"),
                    }
                },
                new TableCategory
                {
                    Name = "CRM และ Loyalty",
                    Icon = "fa-heart",
                    Tables = new List<TableInfo>
                    {
                        new TableInfo("Guest_Preferences", "ความชอบของแขก", "icon-indigo", "fa-heart"),
                        new TableInfo("Guest_Notes", "บันทึกข้อมูลแขก", "icon-indigo", "fa-sticky-note"),
                        new TableInfo("Loyalty_Tiers", "ระดับสมาชิก Loyalty", "icon-indigo", "fa-crown"),
                        new TableInfo("Customer_Loyalty", "Loyalty ลูกค้า", "icon-indigo", "fa-star"),
                        new TableInfo("Loyalty_Rewards", "รางวัล Loyalty", "icon-indigo", "fa-gift"),
                        new TableInfo("Customer_Special_Dates", "วันพิเศษลูกค้า", "icon-indigo", "fa-birthday-cake"),
                    }
                },
                new TableCategory
                {
                    Name = "HR และพนักงาน",
                    Icon = "fa-user-tie",
                    Tables = new List<TableInfo>
                    {
                        new TableInfo("Employees", "ข้อมูลพนักงาน", "icon-blue", "fa-id-badge"),
                        new TableInfo("Employee_Profile", "โปรไฟล์พนักงาน", "icon-blue", "fa-user-circle"),
                        new TableInfo("Employee_Documents", "เอกสารพนักงาน", "icon-blue", "fa-file-alt"),
                        new TableInfo("Employee_Salary", "เงินเดือนพนักงาน", "icon-blue", "fa-money-check"),
                        new TableInfo("Leave_Types", "ประเภทการลา", "icon-blue", "fa-calendar-minus"),
                    }
                },
                new TableCategory
                {
                    Name = "สินทรัพย์",
                    Icon = "fa-boxes",
                    Tables = new List<TableInfo>
                    {
                        new TableInfo("Asset_Category", "หมวดหมู่สินทรัพย์", "icon-teal", "fa-folder"),
                        new TableInfo("Assets", "รายการสินทรัพย์", "icon-teal", "fa-boxes"),
                    }
                },
            };
        }

        // Inner classes for table metadata
        private class TableCategory
        {
            public string Name { get; set; }
            public string Icon { get; set; }
            public List<TableInfo> Tables { get; set; }
        }

        private class TableInfo
        {
            public string TableName { get; set; }
            public string Description { get; set; }
            public string ColorClass { get; set; }
            public string Icon { get; set; }

            public TableInfo(string tableName, string description, string colorClass, string icon)
            {
                TableName = tableName;
                Description = description;
                ColorClass = colorClass;
                Icon = icon;
            }
        }
    }
}
