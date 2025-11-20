<%@ Page MaintainScrollPositionOnPostback="true" Title="" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="CheckDocument.aspx.cs" Inherits="Take_Time_BangPhra.Account.CheckDocument" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
     <link rel="stylesheet" href="/Content/jquery-ui.css">
  <link rel="stylesheet" href="/Content/style.css">
    <link rel="stylesheet" type="text/css" href="/Content/GridView.css">
     <style>


 .header-center{
        text-align:center;
    }
  .header-right{
        text-align:right;
    }
         </style>
            <style>
            th, td {
  padding: 5px;
}
                </style>
    <p>
        <br /><br /><br />
        <table style="width:100%;">
            <tr>
                <td style="width:20%" class="text-right">ประเภทเอกสาร</td>
                <td>
                    <asp:DropDownList ID="DropDownList1" runat="server" Width="60%">
                        <asp:ListItem Selected="True" Value="Account_Payment">ใบสำคัญจ่าย</asp:ListItem>
                        <asp:ListItem Value="Account_Receipt">ใบเสร็จรับเงิน</asp:ListItem>
                        <asp:ListItem Value="P&L">งบกำไรขาดทุน</asp:ListItem>
                        <asp:ListItem Value="Detail_Account_Payment">รายละเอียดใบสำคัญจ่าย</asp:ListItem>
                        <asp:ListItem Value="Detail_Account_Receipt">รายละเอียดใบเสร็จรับเงิน</asp:ListItem>
                    </asp:DropDownList>
                </td>
                
            </tr>
            <tr>
                <td class="text-right">ระหว่างวันที่</td>
                <td>
                    <asp:TextBox ID="TextBox1" runat="server" placeholder="2022-01-31" Width="20%"></asp:TextBox>
                &nbsp;ถึง
                    <asp:TextBox ID="TextBox2" runat="server" placeholder="2022-01-31" Width="20%"></asp:TextBox>
                    <br />
                    <asp:DropDownList ID="DropDownList3" runat="server" Width="30%">
                        <asp:ListItem>--เลือกเดือนที่ต้องการตรวจสอบ--</asp:ListItem>
                        <asp:ListItem>1</asp:ListItem>
                        <asp:ListItem>2</asp:ListItem>
                        <asp:ListItem>3</asp:ListItem>
                        <asp:ListItem>4</asp:ListItem>
                        <asp:ListItem>5</asp:ListItem>
                        <asp:ListItem>6</asp:ListItem>
                        <asp:ListItem>7</asp:ListItem>
                        <asp:ListItem>8</asp:ListItem>
                        <asp:ListItem>9</asp:ListItem>
                        <asp:ListItem>10</asp:ListItem>
                        <asp:ListItem>11</asp:ListItem>
                        <asp:ListItem>12</asp:ListItem>
                    </asp:DropDownList>
                    <asp:DropDownList ID="DropDownList4" runat="server" Width="20%">
                    </asp:DropDownList>
                </td>
               
            </tr>
            <tr>
                <td class="text-right">สถานะ</td>
                <td>
                    <asp:DropDownList ID="DropDownList2" runat="server" Width="60%">
                        <asp:ListItem Selected="True" Value="%">ทั้งหมด</asp:ListItem>
                        <asp:ListItem Value="Normal">ปกติ</asp:ListItem>
                        <asp:ListItem Value="Cancel">ยกเลิก</asp:ListItem>
                    </asp:DropDownList>
                </td>
               
            </tr>
            <tr>
                <td>&nbsp;</td>
                <td>
                    <asp:Button ID="Button2" runat="server" Text="ค้นหา" OnClick="Button2_Click" />
                &nbsp;&nbsp;
                    <asp:Button ID="Button3" runat="server" OnClick="Button3_Click" Text="Export to SCV" />
                </td>
               
            </tr>
            <tr>
                <td>&nbsp;</td>
                <td>
                    <asp:CheckBox ID="CheckBox1" runat="server" Text="Delete?" />
                    </td>
               
            </tr>
            <tr>
                <td>ยอดรวมเงินสด</td>
                <td>
                    <asp:Label ID="Label10" runat="server" Text=""></asp:Label>
                &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;
                    <asp:Label ID="Label14" runat="server" Text=""></asp:Label>
                </td>
               
            </tr>
            <tr>
                <td>ยอดรวมเงินโอนบัญชี</td>
                <td>
                    <asp:Label ID="Label11" runat="server" Text=""></asp:Label>
                &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;
                    <asp:Label ID="Label15" runat="server" Text=""></asp:Label>
                </td>
               
            </tr>
            <tr>
                <td>ยอดเงินกรรมการ</td>
                <td>
                    <asp:Label ID="Label13" runat="server" Text=""></asp:Label>
                &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;
                    <asp:Label ID="Label16" runat="server" Text=""></asp:Label>
                </td>
               
            </tr>
            <tr>
                <td>ยอดรวมภาษีทั้งสดและโอน</td>
                <td>
                    <asp:Label ID="Label12" runat="server" Text=""></asp:Label>
                &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;
                    <asp:Label ID="Label17" runat="server" Text=""></asp:Label>
                </td>
               
            </tr>
            <tr>
                <td>จำนวนเอกสาร</td>
                <td>
                    <asp:Label ID="Label18" runat="server" Text=""></asp:Label>
                </td>
               
            </tr>
            <tr>
                <td colspan="2">
                    <asp:GridView ID="GridView1" runat="server" OnRowDeleting="GridView1_RowDeleting1" OnSelectedIndexChanging="GridView1_SelectedIndexChanging" OnRowCommand="GridView1_RowCommand">
                        <Columns>
                            <asp:CommandField ButtonType="Button" HeaderText="Delete" ShowDeleteButton="True" />
                            <asp:CommandField ButtonType="Button" HeaderText="View" SelectText="View" ShowSelectButton="True" />
                            <asp:ButtonField ButtonType="Button" CommandName="edit" Text="Edit" />
                        </Columns>
                    </asp:GridView>
                </td>
               
            </tr>
        </table>
    </p>
</asp:Content>