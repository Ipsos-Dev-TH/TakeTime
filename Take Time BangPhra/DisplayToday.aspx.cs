using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;

namespace Take_Time_BangPhra
{
    public partial class DisplayToday : System.Web.UI.Page
    {
        _Default code = new _Default();
        string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        protected void Page_Load(object sender, EventArgs e)
        {
            Label1.Text = DateTime.Now.ToString("dd MMMM yyyy");

            // SECURE: Current date lookup with parameterized query
            var dateParams = new Dictionary<string, object>
            {
                { "@CurrentDate", DateTime.Now.ToString("yyyy-MM-dd") }
            };

            DataTable dtReservation = code.DatabaseQuerySafe(conn,
                "SELECT * FROM Reservation INNER JOIN Customer ON Customer.MobilePhone = Reservation.Customer_MobilePhone " +
                "WHERE @CurrentDate >= CheckinDate AND @CurrentDate < CheckoutDate",
                dateParams);

            DataTable dtReservation_Accom = code.DatabaseQuerySafe(conn,
                "SELECT * FROM Reservation RIGHT JOIN Reservation_Accommodation ON Reservation.ID = Reservation_Accommodation.Reservation_ID " +
                "INNER JOIN Accommodation ON Accommodation.ID = Reservation_Accommodation.Accommodation_ID " +
                "WHERE @CurrentDate >= CheckinDate AND @CurrentDate < CheckoutDate ORDER BY Accommodation.orderID ASC",
                dateParams);

            DataTable dtReservation_Items = code.DatabaseQuerySafe(conn,
                "SELECT * FROM Reservation RIGHT JOIN Reservation_Items ON Reservation.ID = Reservation_Items.Reservation_ID " +
                "INNER JOIN Items ON Items.ID = Reservation_Items.Items_ID " +
                "WHERE @CurrentDate >= CheckinDate AND @CurrentDate < CheckoutDate ORDER BY Items_ID ASC",
                dateParams);

            // ยอดเงินของทุกการจองที่แสดง — query เดียว (เดิม query ต่อแถว 2 ครั้ง)
            Dictionary<int, ReservationBalance> balances = ReservationBalance.LoadMany(conn,
                "@CurrentDate >= r.CheckinDate AND @CurrentDate < r.CheckoutDate", dateParams);

            try
            {
                dtReservation.Columns.Add("AccomName");
                dtReservation.Columns.Add("Items");
                dtReservation.Columns.Add("Remain");
                dtReservation.Columns.Add("Order");
                dtReservation.Columns.Add("Received");
                dtReservation.Columns.Add("GrandTotal");
            }
            catch
            {

            }

            for (int i = 0; i < dtReservation.Rows.Count; i++)
            {
                // ชื่อ - ชื่อเล่น - เบอร์  (เว้นท่อนที่ว่าง ไม่ให้เหลือ " - - " ลอย ๆ)
                // ⚠ ใบจาก OTA อาจไม่มีเบอร์จริง (ผูกด้วยรหัสอ้างอิง OTA_xxx) — อย่าโชว์เป็นเบอร์โทร
                string dispPhone = dtReservation.Rows[i]["Customer_MobilePhone"].ToString();
                if (Take_Time_BangPhra.Services.GuestPhone.IsReference(dispPhone))
                    dispPhone = "ไม่มีเบอร์ (จองผ่าน OTA)";
                var nameParts = new System.Collections.Generic.List<string>();
                foreach (string part in new[] { dtReservation.Rows[i]["Name"].ToString(),
                                                dtReservation.Rows[i]["NickName"].ToString(), dispPhone })
                    if (!string.IsNullOrWhiteSpace(part)) nameParts.Add(part.Trim());
                dtReservation.Rows[i]["Name"] = string.Join(" - ", nameParts);
                string AccomName = "";
                int order = 99;
                int orderID = 99;
                for (int j = 0; j < dtReservation_Accom.Rows.Count; j++)
                {

                    if (order > Convert.ToInt32(dtReservation_Accom.Rows[j]["Accommodation_ID"].ToString()))
                    {
                        order = Convert.ToInt32(dtReservation_Accom.Rows[j]["Accommodation_ID"].ToString());
                    }
                    if (dtReservation.Rows[i]["ID"].ToString() == dtReservation_Accom.Rows[j]["Reservation_ID"].ToString())
                    {
                        AccomName += dtReservation_Accom.Rows[j]["AccomName"].ToString() + " ";
                        if (dtReservation_Accom.Rows[j]["LimitWithPeople"].ToString() == "True")
                        {
                            AccomName += ": (" + dtReservation_Accom.Rows[j]["Amount"].ToString() + "คน) ";
                        }
                        if (Convert.ToInt32(dtReservation_Accom.Rows[j]["OrderID"].ToString()) < orderID)
                        {
                            orderID = Convert.ToInt32(dtReservation_Accom.Rows[j]["OrderID"].ToString());
                        }
                    }

                }
                dtReservation.Rows[i]["Order"] = orderID;
                dtReservation.Rows[i]["AccomName"] = AccomName;
                string Items = "";
                for (int j = 0; j < dtReservation_Items.Rows.Count; j++)
                {
                    if (dtReservation.Rows[i]["ID"].ToString() == dtReservation_Items.Rows[j]["Reservation_ID"].ToString())
                    {
                        Items += "[" + dtReservation_Items.Rows[j]["ItemName"].ToString() + " : (" + dtReservation_Items.Rows[j]["Amount"].ToString() + "ชิ้น)] ";
                    }
                }
                dtReservation.Rows[i]["Items"] = Items;

                // ยอดเงิน — สูตรกลาง ReservationBalance (รับแล้ว/คงเหลือมาจากก้อนเดียวกัน ไม่ขัดกันเอง)
                // เดิม "ยอดเงินรับมา" = Deposit ดิบ แต่ "ส่วนที่เหลือ" คิดจาก Payment_History ⇒ สองช่องไม่ตรงกัน
                int reservationId = Convert.ToInt32(dtReservation.Rows[i]["ID"]);
                ReservationBalance bal;
                if (!balances.TryGetValue(reservationId, out bal))
                {
                    decimal baseTotal = dtReservation.Rows[i]["TotalPrice"] != DBNull.Value ? Convert.ToDecimal(dtReservation.Rows[i]["TotalPrice"]) : 0m;
                    decimal dep = dtReservation.Rows[i]["Deposit"] != DBNull.Value ? Convert.ToDecimal(dtReservation.Rows[i]["Deposit"]) : 0m;
                    bal = ReservationBalance.Compute(reservationId, ReservationBalance.ModeNone, baseTotal, 0m, 0m, 0m, 0, dep);
                }

                dtReservation.Rows[i]["GrandTotal"] = bal.Total.ToString("N0");
                dtReservation.Rows[i]["Received"] = bal.Received.ToString("N0") + (bal.IsChannelCollect ? " (OTA เก็บแล้ว)" : "");
                dtReservation.Rows[i]["Remain"] = bal.Due.ToString("N0");
            }

            DataTable dtAccommodation = code.DatabaseQuery(conn, "Select * From Accommodation Where Status = 1 order by OrderID asc");
            DataView view = dtReservation.DefaultView;
            view.Sort = "Order ASC";
            DataTable sortedReservation = view.ToTable();
            Session["dtShow"] = sortedReservation;

            GridView1.DataSource = sortedReservation;
            GridView1.DataBind();
        }
    }
}