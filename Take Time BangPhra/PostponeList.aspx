<%@ Page Title="" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="PostponeList.aspx.cs" Inherits="Take_Time_BangPhra.PostponeList" validateRequest="false" enableEventValidation="false"  %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <link rel="stylesheet" href="/Content/jquery-ui.css">
  <link rel="stylesheet" href="/Content/style.css">
    <link rel="stylesheet" type="text/css" href="/Content/GridView2.css">
    <style type="text/css">
    .wrap { white-space: normal; width: 100px; }
</style>
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
                .auto-style1 {
                    width: 25%;
                    height: 35px;
                }
                .auto-style2 {
                    width: 75%;
                    height: 35px;
                }
                .auto-style3 {
                    width: 25%;
                    height: 32px;
                }
                .auto-style4 {
                    width: 75%;
                    height: 32px;
                }
            </style>
    <p class="text-left">
        &nbsp;<p class="text-left">
        <strong><span style="font-size: large">รายการผู้เลื่อนเข้าพัก</span></strong><br />
        <center>
        <asp:GridView ID="GridView1" runat="server" OnRowCommand="GridView1_RowCommand" AutoGenerateColumns="False"  CssClass="mydatagrid" PagerStyle-CssClass="pager" HeaderStyle-CssClass="header" RowStyle-CssClass="rows">
            <Columns>
                <asp:ButtonField ButtonType="Button" CommandName="Edit" Text="ลงจอง" />
                <asp:BoundField DataField="Name" HeaderText="ชื่อผู้จอง/ชื่อเล่น" HeaderStyle-CssClass="header-center"  >
<HeaderStyle CssClass="header-center"></HeaderStyle>
                </asp:BoundField>
                <asp:BoundField DataField="NickName" HeaderText="ชื่อ Facebook" HeaderStyle-CssClass="header-center" >
<HeaderStyle CssClass="header-center"></HeaderStyle>
                </asp:BoundField>
                <asp:BoundField DataField="Customer_MobilePhone" HeaderText="เบอร์โทรศัพท์" HeaderStyle-CssClass="header-center" ItemStyle-CssClass="header-center" >
<HeaderStyle CssClass="header-center"></HeaderStyle>

<ItemStyle CssClass="header-center"></ItemStyle>
                </asp:BoundField>
                <asp:BoundField DataField="Deposit" HeaderText="ยอดเงินรับมา" HeaderStyle-CssClass="header-center" ItemStyle-CssClass="header-center">
<HeaderStyle CssClass="header-center"></HeaderStyle>

<ItemStyle CssClass="header-center"></ItemStyle>
                </asp:BoundField>
                <asp:BoundField DataField="Remark" HeaderText="หมายเหตุ" HeaderStyle-CssClass="header-center">
<HeaderStyle CssClass="header-center"></HeaderStyle>
                </asp:BoundField>
                <asp:BoundField DataField="ID" HeaderText="หมายเลขการจอง" />
                <asp:TemplateField  HeaderStyle-Width="3%">
                             <ItemTemplate>
                                <asp:Button ID="Button1" runat="server" Text="ลบ" 
    CommandArgument='<%# Eval("ID") %>' 
    CommandName="Delete" 
    OnClientClick="return confirm('ยืนยันการลบรายการผู้เลื่อนเข้าพักหรือไม่');"
    OnClick="DeleteButton_Click"/>
                            </ItemTemplate>
<HeaderStyle Width="3%"></HeaderStyle>
                            </asp:TemplateField>
            </Columns>
            
<HeaderStyle CssClass="header"></HeaderStyle>

<PagerStyle CssClass="pager"></PagerStyle>

<RowStyle CssClass="rows"></RowStyle>
            
        </asp:GridView></center>
    </p>
</asp:Content>
