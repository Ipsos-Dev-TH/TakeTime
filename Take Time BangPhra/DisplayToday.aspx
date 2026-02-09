<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="DisplayToday.aspx.cs" Inherits="Take_Time_BangPhra.DisplayToday" %>

<!DOCTYPE html>

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title></title>
    <link rel="stylesheet" href="/Content/jquery-ui.css">
  <link rel="stylesheet" href="/Content/style.css">
    <link rel="stylesheet" type="text/css" href="/Content/GridView2.css">
    <style type="text/css">
    .wrap { white-space: normal; width: 100px; }
</style>
    <style>
            th, td {
  padding: 5px;
}
                .auto-style1 {
            width: 25%;
            height: 35px;
            color: #000000;
            font-size: large;
        }
                </style>

     <style>
 .header-center{
        text-align:center;
    }
  .header-right{
        text-align:right;
    }
         .auto-style1 {
             color: #FF0000;
         }
         </style>
    <style type="text/css">
        .auto-style1 {
            font-size: large;
        }
         .div-1 {
        background-color: #EBEBEB;
    }
    /* Mobile responsive table styles */
    .mobile-table-wrapper {
        width: 100%;
        overflow-x: auto;
        -webkit-overflow-scrolling: touch;
        -ms-overflow-style: -ms-autohiding-scrollbar;
        touch-action: pan-x pan-y;
    }
    @media (max-width: 768px) {
        .mobile-table-wrapper {
            overflow-x: scroll !important;
            -webkit-overflow-scrolling: touch !important;
            touch-action: pan-x pan-y !important;
        }
        .mobile-table-wrapper table {
            min-width: 700px;
        }
    }
    </style>
</head>
<body>
    <form id="form1" runat="server">
        <div class="div-1">
            <strong>
            <asp:Label ID="Label1" runat="server" CssClass="auto-style1" Text="Label"></asp:Label>
            &nbsp;&nbsp;&nbsp;&nbsp;
            </strong>
            <br />
            <div class="mobile-table-wrapper">
            <asp:GridView AlternatingRowStyle-BackColor="WhiteSmoke" ID="GridView1" runat="server" CssClass="mydatagrid div-1" PagerStyle-CssClass="pager" HeaderStyle-CssClass="header" RowStyle-CssClass="rows" AutoGenerateColumns="False" >
                <Columns>
                    <asp:BoundField DataField="Name" HeaderText="ชื่อผู้จอง" />
                    <asp:BoundField DataField="AccomName" HeaderText="รายชื่อห้องพัก" />
                    <asp:BoundField DataField="StayDays" HeaderText="จำนวนคืน" />
                    <asp:BoundField DataField="Items" HeaderText="รายการของเช่า" />
                    <asp:BoundField DataField="TotalPrice" HeaderText="ราคาทั้งหมด" />
                    <asp:BoundField DataField="Deposit" HeaderText="ยอดเงินรับมา" />
                    <asp:BoundField DataField="Remain" HeaderText="ส่วนที่เหลือ" />
                    <asp:BoundField DataField="remark" HeaderText="หมายเหตุ" />
                    <asp:BoundField DataField="Reserve_By" HeaderText="จองโดย" />
                    <asp:BoundField DataField="Status" HeaderText="สถานะ" />
                </Columns>
                <HeaderStyle CssClass="header" />
                <PagerStyle CssClass="pager" />
                <RowStyle CssClass="rows" />
            </asp:GridView>
            </div>
        </div>
    </form>
</body>
</html>
