<%@ Page Title="" Language="C#" EnableEventValidation="true" EnableViewState="true" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="PaymentAffiliate.aspx.cs" Inherits="Take_Time_BangPhra.Admin.PaymentAffiliate" %>
<%@ Register assembly="Microsoft.ReportViewer.WebForms" namespace="Microsoft.Reporting.WebForms" tagprefix="rsweb" %>
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
  th, td {
    padding: 5px;
  }

  /* Modern Form Styling */
  .form-title {
    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
    color: white;
    padding: 15px 25px;
    border-radius: 10px;
    margin-bottom: 20px;
    text-align: center;
    font-size: 18px;
    font-weight: 600;
    box-shadow: 0 4px 15px rgba(102, 126, 234, 0.3);
  }

  input[type="text"], input[type="number"], input[type="date"], select, textarea {
    padding: 8px 12px;
    border: 1px solid #ddd;
    border-radius: 6px;
    transition: all 0.3s;
    font-size: 14px;
  }

  input[type="text"]:focus, input[type="number"]:focus, select:focus {
    border-color: #667eea;
    box-shadow: 0 0 0 3px rgba(102, 126, 234, 0.15);
    outline: none;
  }

  /* Mobile responsive */
  @media (max-width: 768px) {
    .form-title {
      font-size: 16px;
      padding: 12px 15px;
    }
    table {
      font-size: 14px;
    }
    tr {
      display: block;
      margin-bottom: 2px;
      border-bottom: 1px solid #f0f0f0;
      padding: 4px 0;
    }
    td {
      display: block;
      width: 100% !important;
      text-align: left !important;
      padding: 4px 10px !important;
      box-sizing: border-box;
    }
    td.modal-sm {
      background: none;
      font-weight: 700;
      color: #444;
      padding: 8px 10px 2px 10px !important;
      font-size: 13px;
    }
    input[type="text"], input[type="number"], input[type="date"], select, textarea {
      width: 100% !important;
      max-width: 100% !important;
      box-sizing: border-box;
      margin: 3px 0;
      padding: 10px 12px;
      font-size: 15px;
    }
    input[type="file"] {
      width: 100%;
      margin: 5px 0;
    }
    input[type="submit"], button {
      width: 100%;
      margin: 5px 0;
      padding: 12px;
      font-size: 15px;
    }
    .mydatagrid {
      display: block;
      overflow-x: auto;
      -webkit-overflow-scrolling: touch;
    }
  }
         </style>

    
    <div class="form-title">
        สร้างใบสำคัญจ่าย
    </div>
    <p>
        <table style="width:100%;">
             <tr style="background-color:whitesmoke;">
                 <td class="modal-sm" style="width: 20%; text-align: right">วันที่ใบสำคัญจ่าย:</td>
                <td>
                    &nbsp;<asp:TextBox ID="TextBox8" runat="server" Width="30%" TextMode="Date"></asp:TextBox>
                 </td>
            </tr>
                        <tr>
     <td class="modal-sm" style="width: 20%; text-align: right">วิธีจ่ายเงิน:</td>
    <td>&nbsp;<asp:DropDownList ID="DropDownList2" runat="server" Width="60%" DataSourceID="SqlDataSource2" DataTextField="Paid_How" DataValueField="ID" AppendDataBoundItems="true">
    <asp:ListItem>---โปรดเลือก---</asp:ListItem>    
    </asp:DropDownList>
        <asp:SqlDataSource ID="SqlDataSource2" runat="server" ConnectionString="<%$ ConnectionStrings:TaketimeConnectionString %>" SelectCommand="SELECT * FROM [Account_Paid_How] WHERE ([Status] = @Status)">
            <SelectParameters>
                <asp:Parameter DefaultValue="True" Name="Status" Type="Boolean" />
            </SelectParameters>
        </asp:SqlDataSource>
     </td>

</tr>
            <tr style="background-color:whitesmoke;">
                <td class="modal-sm" style="width: 20%;  text-align: right">จ่ายเงินให้: </td>
                <td>
                    <asp:DropDownList ID="DropDownList5" runat="server" Width="60%" DataSourceID="SqlDataSource5" DataTextField="Coupon_Code" DataValueField="ID" AppendDataBoundItems="True" AutoPostBack="True" OnSelectedIndexChanged="DropDownList5_SelectedIndexChanged">
                        <asp:ListItem Value="%">---โปรดเลือก---</asp:ListItem>
                    </asp:DropDownList>
                    <asp:SqlDataSource ID="SqlDataSource5" runat="server" ConnectionString="<%$ ConnectionStrings:TaketimeConnectionString %>" SelectCommand="SELECT * FROM [Affiliate_Member] WHERE ([Status] = @Status)">
                        <SelectParameters>
                            <asp:Parameter DefaultValue="True" Name="Status" Type="Boolean" />
                        </SelectParameters>
                    </asp:SqlDataSource>
                    <asp:Button ID="Button5" runat="server" Text="Search" OnClick="Button5_Click1" />
                    <br />
                </td>
              
            </tr>
           
           

            <tr>
                 <td class="modal-sm" style="width: 20%; text-align: right">&nbsp;</td>
                <td>
                    <asp:GridView ID="GridView3" runat="server" AutoGenerateColumns="False">
                        <Columns>
                             <asp:TemplateField HeaderText="เลือก" HeaderStyle-Width="5%">
  <ItemTemplate>
      <asp:CheckBox ID="chkSelect" runat="server" Width="100%" CommandName="Check" AutoPostBack="true"/>

 </ItemTemplate>
     <HeaderStyle Width="5%"></HeaderStyle>
 </asp:TemplateField>
                             <asp:BoundField DataField="ID" HeaderText="ID" />
                             <asp:BoundField DataField="StayDate" HeaderText="StayDate" />
                             <asp:BoundField DataField="AccomName" HeaderText="AccomName" />
                             <asp:BoundField DataField="PricePerDay" HeaderText="PricePerDay" />
                             <asp:BoundField DataField="Commission" HeaderText="Commission" />
                        </Columns>
                    </asp:GridView>
                 </td>
            
            </tr>

           

            <tr style="background-color:whitesmoke;">
                 <td class="modal-sm" style="width: 20%; text-align: right">จำนวนรวมก่อนภาษีมูลค่าเพิ่ม: </td>
                <td>
                    &nbsp;<asp:TextBox ID="TextBox3" runat="server" Width="30%" Enabled="False"></asp:TextBox>
                 &nbsp;บาท</td>
            </tr>
            <tr>
                 <td class="modal-sm" style="width: 20%; text-align: right">
                     ภาษีหัก ณ ที่จ่าย 0%: </td>
                <td>
                    &nbsp;<asp:TextBox ID="TextBox4" runat="server" Width="30%" Enabled="False"></asp:TextBox>
                    &nbsp;บาท
                    <asp:CheckBox ID="CheckBox1" runat="server" Text="Edit" OnCheckedChanged="CheckBox1_CheckedChanged" AutoPostBack="True" />
                 </td>
            </tr>

            <tr style="background-color:whitesmoke;">
                 <td class="modal-sm" style="width: 20%; text-align: right">จำนวนสุทธิ: </td>
                <td>
                    &nbsp;<asp:TextBox ID="TextBox6" runat="server" Width="30%" Enabled="False"></asp:TextBox>
                    &nbsp;บาท</td>
            </tr>

            <tr>
                 <td class="modal-sm" style="width: 20%; text-align: right">อัพโหลดไฟล์ที่เกี่ยวข้อง: </td>
                <td>
                    <asp:FileUpload ID="FileUpload1" runat="server" Width="30%" />
                    <asp:TextBox ID="TextBox7" runat="server" Text="" Width="30%" placeholder="คำอธิบายไฟล์"></asp:TextBox>
                    &nbsp;<asp:Button ID="Button4" runat="server" Text="Upload" OnClick="Button4_Click" />
&nbsp; </td>
            </tr>

            <tr style="background-color:whitesmoke;">
                 <td class="modal-sm" style="width: 20%; text-align: right">&nbsp;</td>
                <td>
                    <asp:GridView ID="GridView2" runat="server" AutoGenerateColumns="False" OnRowDeleted="GridView2_RowDeleted" OnRowDeleting="GridView2_RowDeleting">
                        <Columns>
                            <asp:BoundField DataField="Name" HeaderText="ชื่อไฟล์" />
                            <asp:CommandField ShowDeleteButton="True" />
                        </Columns>
                    </asp:GridView>
                 </td>
            </tr>

            <tr>
                 <td class="modal-sm" style="width: 20%; text-align: right">&nbsp;</td>
                <td>
                    &nbsp;<asp:Button ID="Button3" runat="server" Text="บันทึก" Width="100px" Height="33px" OnClick="Button3_Click" />
                 </td>
            </tr>

        </table>
    </p>
    <rsweb:reportviewer Visible="false" ID="ReportViewer2" runat="server" BackColor="" ClientIDMode="AutoID" HighlightBackgroundColor="" InternalBorderColor="204, 204, 204" InternalBorderStyle="Solid" InternalBorderWidth="1px" LinkActiveColor="" LinkActiveHoverColor="" LinkDisabledColor="" PrimaryButtonBackgroundColor="" PrimaryButtonForegroundColor="" PrimaryButtonHoverBackgroundColor="" PrimaryButtonHoverForegroundColor="" SecondaryButtonBackgroundColor="" SecondaryButtonForegroundColor="" SecondaryButtonHoverBackgroundColor="" SecondaryButtonHoverForegroundColor="" SplitterBackColor="" ToolbarDividerColor="" ToolbarForegroundColor="" ToolbarForegroundDisabledColor="" ToolbarHoverBackgroundColor="" ToolbarHoverForegroundColor="" ToolBarItemBorderColor="" ToolBarItemBorderStyle="Solid" ToolBarItemBorderWidth="1px" ToolBarItemHoverBackColor="" ToolBarItemPressedBorderColor="51, 102, 153" ToolBarItemPressedBorderStyle="Solid" ToolBarItemPressedBorderWidth="1px" ToolBarItemPressedHoverBackColor="153, 187, 226" Height="500px" CssClass="auto-style5" style="margin-top: 172px">
        <LocalReport EnableExternalImages="true" ReportPath="Account\Report\Payment.rdlc">
            
        </LocalReport>
</rsweb:reportviewer>
</asp:Content>
