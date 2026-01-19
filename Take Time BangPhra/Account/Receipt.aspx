<%@ Page MaintainScrollPositionOnPostback="true" Title="" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Receipt.aspx.cs" Inherits="Take_Time_BangPhra.Account.Report.Receipt" %>
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

  /* Input styling */
  input[type="text"], input[type="number"], input[type="date"], select, textarea {
    padding: 8px 12px;
    border: 1px solid #ddd;
    border-radius: 6px;
    transition: all 0.3s;
    font-size: 14px;
  }

  input[type="text"]:focus, input[type="number"]:focus, input[type="date"]:focus, select:focus, textarea:focus {
    border-color: #667eea;
    box-shadow: 0 0 0 3px rgba(102, 126, 234, 0.15);
    outline: none;
  }

  input:disabled, input[readonly] {
    background-color: #f5f5f5;
    cursor: not-allowed;
  }

  /* Checkbox styling */
  input[type="checkbox"] {
    width: 18px;
    height: 18px;
    margin-right: 8px;
    cursor: pointer;
  }

  /* Edit checkbox highlight */
  .edit-checkbox-row {
    background: linear-gradient(135deg, #fff3cd 0%, #ffeeba 100%) !important;
    border-left: 4px solid #f39c12;
  }

  .edit-checkbox-row td {
    padding: 12px !important;
  }

  /* Button styling */
  input[type="submit"], button, .btn {
    padding: 10px 20px;
    border: none;
    border-radius: 6px;
    cursor: pointer;
    font-weight: 600;
    transition: all 0.3s;
  }

  .btn-primary {
    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
    color: white;
  }

  .btn-primary:hover {
    transform: translateY(-2px);
    box-shadow: 0 4px 12px rgba(102, 126, 234, 0.4);
  }

  .btn-success {
    background: linear-gradient(135deg, #27ae60 0%, #229954 100%);
    color: white;
  }

  .btn-success:hover {
    transform: translateY(-2px);
    box-shadow: 0 4px 12px rgba(39, 174, 96, 0.4);
  }

  /* Table row styling */
  tr:hover {
    background-color: rgba(102, 126, 234, 0.05) !important;
  }

  /* Amount section styling */
  .amount-section {
    background: linear-gradient(135deg, #f8f9ff 0%, #eef2ff 100%);
    border: 2px solid #667eea;
    border-radius: 10px;
    margin: 15px 0;
    padding: 15px;
  }

  .amount-label {
    font-weight: 600;
    color: #333;
  }

  .amount-value {
    font-size: 16px;
    font-weight: bold;
    color: #667eea;
  }

  /* Mobile responsive */
  @media (max-width: 768px) {
    table {
      font-size: 13px;
    }
    input[type="text"], input[type="number"], select {
      width: 100% !important;
      max-width: 100%;
    }
    td {
      display: block;
      width: 100%;
      text-align: left !important;
      padding: 8px 5px !important;
    }
    td.modal-sm {
      background: #f5f5f5;
      font-weight: 600;
    }
  }
         </style>

    
    <div class="form-title">
        <i class="fas fa-file-invoice"></i> สร้าง/แก้ไข ใบกำกับภาษี
    </div>
        <table style="width:100%;">
            <tr>
                 <td class="modal-sm" style="width: 20%; text-align: right">เลขที่:</td>
                <td>
                    &nbsp;<asp:TextBox ID="TextBox5" runat="server" Width="30%" ReadOnly="True" BackColor="LightGray" ></asp:TextBox>
                    <asp:CheckBox ID="CheckBox2" Text="Edit" runat="server" AutoPostBack="True" OnCheckedChanged="CheckBox2_CheckedChanged" />
                 </td>
            </tr>
             <tr style="background-color:whitesmoke;">
                 <td class="modal-sm" style="width: 20%; text-align: right">วันที่ใบกำกับภาษี:</td>
                <td>
                    &nbsp;<asp:TextBox ID="TextBox8" runat="server" Width="30%" TextMode="Date" AutoPostBack="True" OnTextChanged="TextBox8_TextChanged"></asp:TextBox>
                 </td>
            </tr>
            <tr>
                <td class="modal-sm" style="width: 20%;  text-align: right">หมายเลขการจอง:</td>
                <td>
                    <asp:TextBox ID="TextBox9" runat="server" Width="60%" AutoPostBack="True" OnTextChanged="TextBox9_TextChanged"></asp:TextBox>
                 </td>
              
            </tr>
            <tr style="background-color:whitesmoke;">
                <td class="modal-sm" style="width: 20%;  text-align: right">ชื่อลูกค้า:</td>
                <td>
                    <asp:DropDownList ID="DropDownList8" runat="server" Width="20%" AutoPostBack="True" OnSelectedIndexChanged="DropDownList8_SelectedIndexChanged">
                    </asp:DropDownList>
                    <asp:TextBox ID="TextBox10" runat="server" Width="40%"></asp:TextBox>
                    <asp:TextBox ID="TextBox7" runat="server" Width="20%" PlaceHolder="รหัสสาขา" text="00000"></asp:TextBox>
                            
                 </td>
              
            </tr>
            <tr>
                <td class="modal-sm" style="width: 20%;  text-align: right">ที่อยู่:</td>
                <td>
                    <asp:TextBox ID="TextBox11" runat="server" Width="20%"></asp:TextBox>
                    <asp:TextBox ID="TextBox18" runat="server" Width="40%"></asp:TextBox>
                 </td>
              
            </tr>
            <tr style="background-color:whitesmoke;">
                <td class="modal-sm" style="width: 20%;  text-align: right">รหัสไปรษณีย์</td>
                <td>
                    <asp:TextBox ID="TextBox16" runat="server" Width="30%" AutoPostBack="True" OnTextChanged="TextBox16_TextChanged" TextMode="Number"></asp:TextBox>
                 &nbsp;&nbsp;
                    <asp:Button ID="Button5" runat="server" Text="ค้นหา" Width="20%" OnClick="Button5_Click" />
                 &nbsp;&nbsp;
                    <asp:Button ID="Button6" runat="server" Text="ยกเลิก" Width="20%" OnClick="Button6_Click" />
                 </td>
              
            </tr>
            <tr>
                <td class="modal-sm" style="width: 20%;  text-align: right">จังหวัด/อำเภอ/ตำบล</td>
                <td>
                    <asp:DropDownList ID="DropDownList5" runat="server" Width="25%" AutoPostBack="True" OnSelectedIndexChanged="DropDownList5_SelectedIndexChanged">
                    </asp:DropDownList>
                &nbsp;&nbsp;
                    <asp:DropDownList ID="DropDownList6" runat="server" Width="25%" AutoPostBack="True" OnSelectedIndexChanged="DropDownList6_SelectedIndexChanged">
                    </asp:DropDownList>
                &nbsp;&nbsp;
                    <asp:DropDownList ID="DropDownList7" runat="server" Width="25%">
                    </asp:DropDownList>
                </td>
              
            </tr>
            <tr style="background-color:whitesmoke;">
                <td class="modal-sm" style="width: 20%;  text-align: right">เลขประจำตัวผู้เสียภาษี:</td>
                <td>
                    <asp:TextBox ID="TextBox12" runat="server" Width="60%"></asp:TextBox>
                 </td>
              
            </tr>
            <tr>
                <td class="modal-sm" style="width: 20%;  text-align: right">เบอร์โทร:</td>
                <td>
                    <asp:TextBox ID="TextBox13" runat="server" Width="60%"></asp:TextBox>
                 </td>
              
            </tr>
            <tr>
                <td class="modal-sm" style="width: 20%;  text-align: right">อีเมล์</td>
                <td>
                    <asp:TextBox ID="TextBox17" runat="server" Width="60%"></asp:TextBox>
                 </td>
              
            </tr>
            <tr style="background-color:whitesmoke;">
                 <td class="modal-sm" style="width: 20%; text-align: right">วิธีชำระเงิน:</td>
                <td>&nbsp;<asp:DropDownList ID="DropDownList2" runat="server" Width="60%" AppendDataBoundItems="true">
                <asp:ListItem>---โปรดเลือก---</asp:ListItem>    
                </asp:DropDownList>
                    <asp:SqlDataSource ID="SqlDataSource2" runat="server" ConnectionString="<%$ ConnectionStrings:TaketimeConnectionString %>" SelectCommand="SELECT * FROM [Account_Paid_How] WHERE ([Status] = 'True')">
                        <SelectParameters>
                            <asp:Parameter DefaultValue="True" Name="Status" Type="Boolean" />
                        </SelectParameters>
                    </asp:SqlDataSource>
                 </td>
            <tr>
                 <td class="modal-sm" style="width: 20%; text-align: right">ประเภทภาษี: </td>
                <td>
                    &nbsp;<asp:DropDownList ID="DropDownList4" runat="server" Width="60%" OnSelectedIndexChanged="DropDownList4_SelectedIndexChanged" AppendDataBoundItems="true">
                    <asp:ListItem>---โปรดเลือก---</asp:ListItem>
                    </asp:DropDownList>
                    <asp:SqlDataSource ID="SqlDataSource4" runat="server" ConnectionString="<%$ ConnectionStrings:TaketimeConnectionString %>" SelectCommand="SELECT * FROM [Account_Vat_Type] WHERE ([Status] = 'True')">
                        <SelectParameters>
                            <asp:Parameter DefaultValue="True" Name="Status" Type="Boolean" />
                        </SelectParameters>
                    </asp:SqlDataSource>
                 </td>
            
            </tr>
            </tr>

            <tr>
                 <td class="modal-sm" style="width: 20%; text-align: right">&nbsp;</td>
                <td>
                    <asp:CheckBox ID="CheckBox1" runat="server" Text="IsDeposit" />
                 </td>
            
            </tr>

            <tr>
                 <td class="modal-sm" style="width: 20%; text-align: right">&nbsp;</td>
                <td>
                    <asp:CheckBox ID="CheckBox3" Text="ประสงค์ไม่ระบุชื่อในใบกำกับภาษี" runat="server" AutoPostBack="True" OnCheckedChanged="CheckBox3_CheckedChanged" />
                 </td>
            
            </tr>

            <tr>
                 <td class="modal-sm" style="width: 20%; text-align: right">&nbsp;</td>
                <td>
                    <asp:CheckBox ID="CheckBox5" Text="ต้องการรับ e tax invoice" runat="server" AutoPostBack="True" OnCheckedChanged="CheckBox5_CheckedChanged" />
                 &nbsp;&nbsp;
                    </td>
            
            </tr>

            <tr style="background-color:whitesmoke;">
                 <td class="modal-sm" style="width: 20%; text-align: right">&nbsp;</td>
                <td>&nbsp;</td>
            
            </tr>

            <tr style="background-color:whitesmoke;">
                 <td class="modal-sm" style="width: 20%; text-align: right">ประเภท:</td>
                <td>&nbsp;<asp:DropDownList ID="DropDownList3" runat="server" Width="60%" DataSourceID="SqlDataSource3" DataTextField="ProductType_Name" DataValueField="ID" AppendDataBoundItems="true" >
                <asp:ListItem>---โปรดเลือก---</asp:ListItem>    
                </asp:DropDownList>
                    <asp:SqlDataSource ID="SqlDataSource3" runat="server" ConnectionString="<%$ ConnectionStrings:TaketimeConnectionString %>" SelectCommand="SELECT * FROM [Account_ProductType]">
                    </asp:SqlDataSource>
                 </td>
            
            </tr>

            

           

            <tr style="background-color:whitesmoke;">
                 <td class="modal-sm" style="width: 20%; text-align: right">รายละเอียด: </td>
                <td>
                    &nbsp;<asp:TextBox ID="TextBox1" runat="server" Width="60%"></asp:TextBox>
                 </td>
            
            </tr>

            <tr style="background-color:whitesmoke;">
                 <td class="modal-sm" style="width: 20%; text-align: right">จำนวน:</td>
                <td>
                    &nbsp;<asp:TextBox ID="TextBox14" runat="server" Width="30%" TextMode="Number"></asp:TextBox>
                 </td>
            
            </tr>

            <tr style="background-color:whitesmoke;">
                 <td class="modal-sm" style="width: 20%; text-align: right">หน่วย:</td>
                <td>
                    &nbsp;<asp:TextBox ID="TextBox15" runat="server" Width="30%"></asp:TextBox>
                 </td>
            
            </tr>

            <tr style="background-color:whitesmoke;">
                 <td class="modal-sm" style="width: 20%; text-align: right">จำนวนเงินต่อหน่วย(รวมภาษีแล้ว): </td>
                <td>
                    &nbsp;<asp:TextBox ID="TextBox2" runat="server" Width="30%"></asp:TextBox>
                 &nbsp;บาท</td>
            
            </tr>

            <tr style="background-color:whitesmoke;">
                 <td class="modal-sm" style="width: 20%; text-align: right">&nbsp;</td>
                <td>
                    &nbsp;<asp:Button ID="Button2" runat="server" Text="เพิ่ม" Width="75px" OnClick="Button2_Click" />
                 </td>
            
            </tr>

            <tr style="background-color:whitesmoke;">
                <td class="modal-sm" style="width: 20%; text-align: right">&nbsp;</td>
                 <td style="text-align: right">
                     <asp:GridView ID="GridView1" runat="server" AutoGenerateColumns="False" OnRowDeleting="GridView1_RowDeleting">
                         <Columns>
                             <asp:BoundField DataField="Number" HeaderText="ลำดับ" />
                             <asp:BoundField DataField="ProductType_ID" HeaderText="ประเภท" />
                             <asp:BoundField DataField="Product_Data" HeaderText="รายละเอียด" />
                             <asp:BoundField DataField="Product_Amount" HeaderText="จำนวน" />
                             <asp:BoundField DataField="Product_Unit" HeaderText="หน่วย" />
                             <asp:BoundField DataField="Price_PerPeice" HeaderText="ราคาต่อหน่วย" />
                             <asp:BoundField DataField="Price_Amount" HeaderText="จำนวนเงินรวม" />
                             <asp:CommandField ShowDeleteButton="True" ButtonType="Button" />
                         </Columns>
                     </asp:GridView>
                     </td>
            
            </tr>

            <tr>
                 <td class="modal-sm" style="width: 20%; text-align: right">&nbsp;</td>
                <td>
                    <asp:CheckBox ID="ChkEditAmount" runat="server" Text="แก้ไขยอดเงินด้วยตนเอง" AutoPostBack="True" OnCheckedChanged="ChkEditAmount_CheckedChanged" />
                </td>
            </tr>
            <tr>
                 <td class="modal-sm" style="width: 20%; text-align: right">จำนวนรวมก่อนภาษีมุลค่าเพิ่ม: </td>
                <td>
                    &nbsp;<asp:TextBox ID="TextBox3" runat="server" Width="30%" Enabled="False"></asp:TextBox>
                 &nbsp;บาท</td>
            </tr>
            <tr>
                 <td class="modal-sm" style="width: 20%; text-align: right">
                     <asp:Label ID="Label1" runat="server" Text=""></asp:Label>: </td>
                <td>
                    &nbsp;<asp:TextBox ID="TextBox4" runat="server" Width="30%" Enabled="False"></asp:TextBox>
                    &nbsp;บาท</td>
            </tr>

            <tr>
                 <td class="modal-sm" style="width: 20%; text-align: right">จำนวนสุทธิ: </td>
                <td>
                    &nbsp;<asp:TextBox ID="TextBox6" runat="server" Width="30%" Enabled="False"></asp:TextBox>
                    &nbsp;บาท</td>
            </tr>

            <tr>
                 <td class="modal-sm" style="width: 20%; text-align: right">&nbsp;</td>
                <td>
                    &nbsp;<asp:CheckBox ID="CheckBox4" runat="server" Text="ยกเลิกใบกำกับภาษี" AutoPostBack="True" OnCheckedChanged="CheckBox4_CheckedChanged" />
&nbsp;<asp:Button ID="Button4" runat="server" Text="ยกเลิก" Width="100px" Height="33px" Enabled="False" OnClick="Button4_Click1" />
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
        <LocalReport EnableExternalImages="true" ReportPath="Account\Report\Receipt.rdlc">
            
        </LocalReport>
</rsweb:reportviewer>

    <asp:Panel ID="Panel1" runat="server" Visible="False"><center><iframe id="myFrame" runat="server" width="640" height="480" frameborder="0"></iframe></center></asp:Panel>
</asp:Content>
