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

            try
            {
                dtReservation.Columns.Add("AccomName");
                dtReservation.Columns.Add("Items");
                dtReservation.Columns.Add("Remain");
                dtReservation.Columns.Add("Order");
            }
            catch
            {

            }

            for (int i = 0; i < dtReservation.Rows.Count; i++)
            {
                dtReservation.Rows[i]["Name"] = dtReservation.Rows[i]["Name"].ToString() + " - " + dtReservation.Rows[i]["NickName"].ToString() + " - " + dtReservation.Rows[i]["Customer_MobilePhone"].ToString();
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

                // Calculate remaining balance using direct query
                int reservationId = Convert.ToInt32(dtReservation.Rows[i]["ID"]);

                // Get base total price
                decimal baseTotalPrice = Convert.ToDecimal(dtReservation.Rows[i]["TotalPrice"]);

                // SECURE: Get product charges (excluding cancelled) with parameterized query
                decimal productCharges = 0;
                var chargesParams = new Dictionary<string, object>
                {
                    { "@ReservationID", reservationId }
                };
                DataTable dtProductCharges = code.DatabaseQuerySafe(conn,
                    @"SELECT ISNULL(SUM(TotalAmount), 0) as TotalCharges
                      FROM Reservation_Product_Charges
                      WHERE Reservation_ID = @ReservationID
                      AND Status <> 'CANCELLED'",
                    chargesParams);
                if (dtProductCharges.Rows.Count > 0 && dtProductCharges.Rows[0]["TotalCharges"] != DBNull.Value)
                {
                    productCharges = Convert.ToDecimal(dtProductCharges.Rows[0]["TotalCharges"]);
                }

                // SECURE: Get total paid with parameterized query
                decimal totalPaid = 0;
                var paidParams = new Dictionary<string, object>
                {
                    { "@ReservationID", reservationId }
                };
                DataTable dtPaid = code.DatabaseQuerySafe(conn,
                    @"SELECT ISNULL(SUM(PaymentAmount), 0) as TotalPaid
                      FROM Payment_History
                      WHERE Reservation_ID = @ReservationID
                      AND Status = 'COMPLETED'",
                    paidParams);
                if (dtPaid.Rows.Count > 0 && dtPaid.Rows[0]["TotalPaid"] != DBNull.Value)
                {
                    totalPaid = Convert.ToDecimal(dtPaid.Rows[0]["TotalPaid"]);
                }

                // Calculate remaining balance
                decimal totalPrice = baseTotalPrice + productCharges;
                decimal remainingBalance = totalPrice - totalPaid;

                dtReservation.Rows[i]["Remain"] = remainingBalance.ToString("N0");
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