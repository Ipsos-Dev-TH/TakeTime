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

            DataTable dtReservation = code.DatabaseQuery(conn, "Select * From Reservation inner join Customer on Customer.MobilePhone = Reservation.Customer_MobilePhone Where '" + DateTime.Now.ToString("yyyy-MM-dd") + "' >= CheckinDate AND '" + DateTime.Now.ToString("yyyy-MM-dd") + "' < CheckoutDate");
            DataTable dtReservation_Accom = code.DatabaseQuery(conn, "Select * From Reservation right join Reservation_Accommodation on Reservation.ID = Reservation_Accommodation.Reservation_ID inner join Accommodation on Accommodation.ID = Reservation_Accommodation.Accommodation_ID  Where '" + DateTime.Now.ToString("yyyy-MM-dd") + "' >= CheckinDate AND '" + DateTime.Now.ToString("yyyy-MM-dd") + "' < CheckoutDate order by Accommodation.orderID asc");
            DataTable dtReservation_Items = code.DatabaseQuery(conn, "Select * From Reservation right join Reservation_Items on Reservation.ID = Reservation_Items.Reservation_ID inner join Items on Items.ID = Reservation_Items.Items_ID  Where '" + DateTime.Now.ToString("yyyy-MM-dd") + "' >= CheckinDate AND '" + DateTime.Now.ToString("yyyy-MM-dd") + "' < CheckoutDate order by Items_ID asc");

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

                // Get product charges (excluding cancelled)
                decimal productCharges = 0;
                DataTable dtProductCharges = code.DatabaseQuery(conn,
                    $@"SELECT ISNULL(SUM(TotalAmount), 0) as TotalCharges
                       FROM Reservation_Product_Charges
                       WHERE Reservation_ID = {reservationId}
                       AND Status <> 'CANCELLED'");
                if (dtProductCharges.Rows.Count > 0 && dtProductCharges.Rows[0]["TotalCharges"] != DBNull.Value)
                {
                    productCharges = Convert.ToDecimal(dtProductCharges.Rows[0]["TotalCharges"]);
                }

                // Get total paid
                decimal totalPaid = 0;
                DataTable dtPaid = code.DatabaseQuery(conn,
                    $@"SELECT ISNULL(SUM(PaymentAmount), 0) as TotalPaid
                       FROM Payment_History
                       WHERE Reservation_ID = {reservationId}
                       AND Status = 'COMPLETED'");
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