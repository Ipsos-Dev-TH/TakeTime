<%@ Page MaintainScrollPositionOnPostback="true" Title="" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="PaymentVoucher.aspx.cs" Inherits="Take_Time_BangPhra.Account.Report.PaymentVoucher" %>
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
  .autocomplete {
  position: relative;
  display: inline-block;
}

     .autocomplete-items {
  position: absolute;
  border: 1px solid #d4d4d4;
  border-bottom: none;
  border-top: none;
  z-index: 99;
  /*position the autocomplete items to be the same width as the container:*/
  top: 100%;
  left: 0;
  right: 0;
  background-color: lemonchiffon;
}
.autocomplete-items div {
  padding: 10px;
  cursor: pointer;
  border-bottom: 1px solid #d4d4d4;
}

.autocomplete-active {
  /*when navigating through the items using the arrow keys:*/
  background-color: DodgerBlue !important;
  color: #ffffff;
}

/* 📎 File Attachment Styles */
.file-grid {
    border-collapse: collapse;
    width: 100%;
    margin-top: 10px;
}

.file-grid th {
    background: linear-gradient(135deg, #5d4037 0%, #8d6e63 100%);
    color: white;
    padding: 10px;
    text-align: center;
    font-weight: 600;
    font-size: 14px;
}

.file-grid td {
    padding: 8px;
    border: 1px solid #e0e0e0;
    background: white;
    vertical-align: middle;
}

.file-grid tr:hover td {
    background: #f8f9fa;
}

.file-link {
    color: #1976d2;
    text-decoration: none;
    font-weight: 600;
    padding: 5px 10px;
    border-radius: 4px;
    display: inline-block;
    transition: all 0.3s;
}

.file-link:hover {
    background: #e3f2fd;
    transform: translateX(3px);
}

.btn-delete-file {
    background: linear-gradient(135deg, #e74c3c 0%, #c0392b 100%);
    color: white;
    padding: 6px 12px;
    border: none;
    border-radius: 5px;
    font-size: 13px;
    font-weight: 600;
    cursor: pointer;
    transition: all 0.3s;
    box-shadow: 0 2px 5px rgba(0,0,0,0.15);
}

.btn-delete-file:hover {
    background: linear-gradient(135deg, #c0392b 0%, #e74c3c 100%);
    transform: translateY(-2px);
    box-shadow: 0 4px 8px rgba(0,0,0,0.2);
}

/* 🎨 Button Styles */
.btn-upload {
    background: linear-gradient(135deg, #3498db 0%, #2980b9 100%);
    color: white;
    padding: 8px 20px;
    border: none;
    border-radius: 5px;
    font-size: 14px;
    font-weight: 600;
    cursor: pointer;
    transition: all 0.3s;
    box-shadow: 0 2px 5px rgba(0,0,0,0.15);
}

.btn-upload:hover {
    background: linear-gradient(135deg, #2980b9 0%, #3498db 100%);
    transform: translateY(-2px);
    box-shadow: 0 4px 8px rgba(0,0,0,0.2);
}

.btn-save {
    background: linear-gradient(135deg, #27ae60 0%, #229954 100%);
    color: white;
    padding: 10px 30px;
    border: none;
    border-radius: 8px;
    font-size: 16px;
    font-weight: 700;
    cursor: pointer;
    transition: all 0.3s;
    box-shadow: 0 3px 8px rgba(0,0,0,0.2);
}

.btn-save:hover {
    background: linear-gradient(135deg, #229954 0%, #27ae60 100%);
    transform: translateY(-3px);
    box-shadow: 0 6px 12px rgba(0,0,0,0.3);
}

.btn-add {
    background: linear-gradient(135deg, #f39c12 0%, #e67e22 100%);
    color: white;
    padding: 8px 20px;
    border: none;
    border-radius: 5px;
    font-size: 14px;
    font-weight: 600;
    cursor: pointer;
    transition: all 0.3s;
    box-shadow: 0 2px 5px rgba(0,0,0,0.15);
}

.btn-add:hover {
    background: linear-gradient(135deg, #e67e22 0%, #f39c12 100%);
    transform: translateY(-2px);
    box-shadow: 0 4px 8px rgba(0,0,0,0.2);
}

.btn-secondary {
    background: linear-gradient(135deg, #95a5a6 0%, #7f8c8d 100%);
    color: white;
    padding: 8px 20px;
    border: none;
    border-radius: 5px;
    font-size: 14px;
    font-weight: 600;
    cursor: pointer;
    transition: all 0.3s;
    box-shadow: 0 2px 5px rgba(0,0,0,0.15);
}

.btn-secondary:hover {
    background: linear-gradient(135deg, #7f8c8d 0%, #95a5a6 100%);
    transform: translateY(-2px);
    box-shadow: 0 4px 8px rgba(0,0,0,0.2);
}

/* File upload section styling */
.upload-section {
    padding: 10px;
    background: #f9f9f9;
    border-radius: 5px;
    border: 1px solid #e0e0e0;
}
         </style>
            <style>
            th, td {
              padding: 5px;
            }
            .auto-style1 {
                width: 20%;
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
              vertical-align: middle;
            }

            /* Edit checkbox highlight */
            .edit-checkbox-row {
              background: linear-gradient(135deg, #fff3cd 0%, #ffeeba 100%) !important;
              border-left: 4px solid #f39c12;
            }

            .edit-checkbox-row td {
              padding: 12px !important;
            }

            /* Table row styling */
            tr:hover {
              background-color: rgba(102, 126, 234, 0.05) !important;
            }

            /* Mobile responsive */
            @media (max-width: 768px) {
              .form-title {
                font-size: 16px;
                padding: 12px 15px;
                margin: 0 -5px 15px -5px;
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
              td.modal-sm, td.auto-style1 {
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
              .autocomplete {
                width: 100% !important;
              }
              .upload-section {
                padding: 10px 5px;
              }
              .upload-section input[type="file"] {
                width: 100%;
                margin: 5px 0;
              }
              .file-grid td {
                display: table-cell;
                width: auto !important;
              }
              input[type="submit"], button, .btn,
              .btn-save, .btn-add, .btn-upload, .btn-secondary {
                width: 100%;
                margin: 5px 0;
                padding: 12px;
                font-size: 15px;
                box-sizing: border-box;
              }
              input[type="checkbox"] {
                width: 20px;
                height: 20px;
              }
              /* Asset section inner table */
              div[style*="border: 2px solid"] table td {
                padding: 4px 5px !important;
              }
            }
            </style>
      <script type="text/javascript">
      function checkEnter(event) {
          if (event.key === 'Enter') {
              __doPostBack('<%= TextBox9.UniqueID %>', '');
              event.preventDefault();
          }
      }
      </script>
    
    <div class="form-title">
        <i class="fas fa-file-invoice-dollar"></i> สร้าง/แก้ไข ใบสำคัญจ่าย
    </div>
        <table style="width:100%;">
            <tr>
                 <td class="modal-sm" style="width: 20%; text-align: right">เลขที่ใบสำคัญจ่าย:</td>
                <td>
                    &nbsp;<asp:TextBox ID="txtVoucherNo" runat="server" Width="30%" ReadOnly="True" BackColor="LightGray"></asp:TextBox>
                    <asp:CheckBox ID="chkEditVoucherNo" Text="Edit" runat="server" AutoPostBack="True" OnCheckedChanged="chkEditVoucherNo_CheckedChanged" />
                 </td>
            </tr>
             <tr style="background-color:whitesmoke;">
                 <td class="modal-sm" style="width: 20%; text-align: right">วันที่ใบสำคัญจ่าย:</td>
                <td>
                    &nbsp;<asp:TextBox ID="TextBox8" runat="server" Width="30%" TextMode="Date" AutoPostBack="True" OnTextChanged="TextBox8_TextChanged"></asp:TextBox>
                 </td>
            </tr>
            <tr style="background-color:whitesmoke;">
                <td class="auto-style1" style="text-align: right" rowspan="2">จ่ายเงินให้: </td>
                <td>
                     <div class="autocomplete" style="width:100%">
                    <asp:TextBox ID="TextBox9" runat="server" Width="60%" autocomplete="off" AutoPostBack="True" OnTextChanged="TextBox9_TextChanged"></asp:TextBox>
                    <asp:Literal ID="Literal1" runat="server"></asp:Literal>
                         </div>
                </td>
              
            </tr>
            <tr style="background-color:whitesmoke;">
                <td>
                    <asp:DropDownList ID="DropDownList5" runat="server" Width="60%" AppendDataBoundItems="true" AutoPostBack="True" OnSelectedIndexChanged="DropDownList5_SelectedIndexChanged">
                        <asp:ListItem Value="%">---โปรดเลือก---</asp:ListItem>
                    </asp:DropDownList>
                    <asp:SqlDataSource ID="SqlDataSource5" runat="server" ConnectionString="<%$ ConnectionStrings:TaketimeConnectionString %>" SelectCommand="Select distinct(Vendor_Group) from Vendor"></asp:SqlDataSource>
                    <br />
                    <asp:DropDownList ID="DropDownList1" runat="server" Width="60%" AppendDataBoundItems="true">
                        <asp:ListItem>---โปรดเลือก---</asp:ListItem>
                    </asp:DropDownList>
                    <asp:SqlDataSource ID="SqlDataSource1" runat="server" ConnectionString="<%$ ConnectionStrings:TaketimeConnectionString %>" SelectCommand="SELECT * FROM [Vendor] WHERE ([Status] = 'True') order by Vendor_Group,Name ASC">
                        <SelectParameters>
                            <asp:Parameter DefaultValue="True" Name="Status" Type="Boolean" />
                        </SelectParameters>
                    </asp:SqlDataSource>
                    <asp:Button ID="Button5" runat="server" Text="🔧 สร้าง/แก้ไข Vendor" CssClass="btn-secondary" OnClick="Button5_Click" />
                </td>
              
            </tr>
            <tr>
                 <td class="modal-sm" style="width: 20%; text-align: right">วิธีจ่ายเงิน:</td>
                <td>&nbsp;<asp:DropDownList ID="DropDownList2" runat="server" Width="60%" AppendDataBoundItems="true">
                <asp:ListItem>---โปรดเลือก---</asp:ListItem>    
                </asp:DropDownList>
                    <asp:SqlDataSource ID="SqlDataSource2" runat="server" ConnectionString="<%$ ConnectionStrings:TaketimeConnectionString %>" SelectCommand="SELECT * FROM [Account_Paid_How] WHERE ([Status] = 'True')">
                        <SelectParameters>
                            <asp:Parameter DefaultValue="True" Name="Status" Type="Boolean" />
                        </SelectParameters>
                    </asp:SqlDataSource>
                 </td>
            
            </tr>

            <tr style="background-color:whitesmoke;">
                 <td class="modal-sm" style="width: 20%; text-align: right">ประเภทการจ่ายเงิน:</td>
                <td>&nbsp;<asp:DropDownList ID="DropDownList3" runat="server" Width="60%" AppendDataBoundItems="true" AutoPostBack="True" OnSelectedIndexChanged="DropDownList3_SelectedIndexChanged">
                <asp:ListItem>---โปรดเลือก---</asp:ListItem>
                </asp:DropDownList>
                    <asp:SqlDataSource ID="SqlDataSource3" runat="server" ConnectionString="<%$ ConnectionStrings:TaketimeConnectionString %>" SelectCommand="SELECT * FROM [Account_Paid_Type] WHERE ([Status] = 'True')">
                        <SelectParameters>
                            <asp:Parameter DefaultValue="True" Name="Status" Type="Boolean" />
                        </SelectParameters>
                    </asp:SqlDataSource>
                 </td>
            
            </tr>

            <tr>
                 <td class="modal-sm" style="width: 20%; text-align: right">ประเภทภาษี: </td>
                <td>
                    &nbsp;<asp:DropDownList ID="DropDownList4" runat="server" Width="60%"  OnSelectedIndexChanged="DropDownList4_SelectedIndexChanged" AppendDataBoundItems="true">
                    <asp:ListItem>---โปรดเลือก---</asp:ListItem>
                    </asp:DropDownList>
                    <asp:SqlDataSource ID="SqlDataSource4" runat="server" ConnectionString="<%$ ConnectionStrings:TaketimeConnectionString %>" SelectCommand="SELECT * FROM [Account_Vat_Type] WHERE ([Status] = 'True')">
                        <SelectParameters>
                            <asp:Parameter DefaultValue="True" Name="Status" Type="Boolean" />
                        </SelectParameters>
                    </asp:SqlDataSource>
                 </td>
            
            </tr>

           

            <tr style="background-color:whitesmoke;">
                 <td class="modal-sm" style="width: 20%; text-align: right">หมวดค่าใช้จ่าย (รายการ): </td>
                <td>
                    &nbsp;<asp:DropDownList ID="ddlLineCategory" runat="server" Width="60%" AppendDataBoundItems="true">
                    <asp:ListItem Value="">---เลือกหมวดค่าใช้จ่าย---</asp:ListItem>
                    </asp:DropDownList>
                 </td>
            </tr>

            <tr>
                 <td class="modal-sm" style="width: 20%; text-align: right">รายละเอียดค่าใช้จ่าย: </td>
                <td>
                    &nbsp;<asp:TextBox ID="TextBox1" runat="server" Width="60%"></asp:TextBox>
                 </td>

            </tr>

            <tr style="background-color:whitesmoke;">
                 <td class="modal-sm" style="width: 20%; text-align: right">จำนวนเงิน(ไม่รวมภาษี): </td>
                <td>
                    &nbsp;<asp:TextBox ID="TextBox2" runat="server" Width="30%"></asp:TextBox>
                 &nbsp;บาท</td>

            </tr>

            <tr>
                 <td class="modal-sm" style="width: 20%; text-align: right">&nbsp;</td>
                <td>
                    &nbsp;<asp:Button ID="Button2" runat="server" Text="➕ เพิ่ม" CssClass="btn-add" OnClick="Button2_Click" />
                 </td>

            </tr>

            <tr>
                <td class="modal-sm" style="width: 20%; text-align: right">&nbsp;</td>
                 <td style="text-align: right">
                     <asp:GridView ID="GridView1" runat="server" AutoGenerateColumns="False" OnRowDeleting="GridView1_RowDeleting">
                         <Columns>
                             <asp:BoundField DataField="Number" HeaderText="ลำดับ" />
                             <asp:BoundField DataField="PaidTypeName" HeaderText="หมวดค่าใช้จ่าย" />
                             <asp:BoundField DataField="Detail" HeaderText="รายละเอียด" />
                             <asp:BoundField DataField="Amount" HeaderText="จำนวนเงิน" DataFormatString="{0:N2}" />
                             <asp:CommandField ShowDeleteButton="True" ButtonType="Button" />
                         </Columns>
                     </asp:GridView>
                     </td>
            
            </tr>

            <tr>
                 <td class="modal-sm" style="width: 20%; text-align: right">&nbsp;</td>
                <td>
                    <asp:CheckBox ID="CheckBox1" runat="server" Text="แก้ไขยอดเงินด้วยตนเอง" AutoPostBack="True" OnCheckedChanged="CheckBox1_CheckedChanged" />
                 </td>
            </tr>
            <tr style="background-color:whitesmoke;">
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
                    &nbsp;บาท
                 </td>
            </tr>

            <tr style="background-color:whitesmoke;">
                 <td class="modal-sm" style="width: 20%; text-align: right">จำนวนสุทธิ: </td>
                <td>
                    &nbsp;<asp:TextBox ID="TextBox6" runat="server" Width="30%" Enabled="False"></asp:TextBox>
                    &nbsp;บาท</td>
            </tr>

            <!-- Asset Recording Section -->
            <tr>
                <td colspan="2" style="padding: 15px 0;">
                    <div style="border: 2px solid #667eea; border-radius: 8px; padding: 15px; background: linear-gradient(135deg, #f8f9ff 0%, #eef2ff 100%);">
                        <div style="margin-bottom: 10px;">
                            <asp:CheckBox ID="chkRecordAsset" runat="server" Text=" บันทึกเป็นสินทรัพย์ถาวร"
                                Font-Bold="true" Font-Size="14px" AutoPostBack="true"
                                OnCheckedChanged="chkRecordAsset_CheckedChanged" />
                            <span style="color: #666; font-size: 12px; margin-left: 10px;">
                                (สำหรับการซื้อทรัพย์สิน เช่น คอมพิวเตอร์ เฟอร์นิเจอร์ ยานพาหนะ ฯลฯ)
                            </span>
                        </div>
                        <asp:Panel ID="pnlAssetDetails" runat="server" Visible="false">
                            <table style="width: 100%;">
                                <tr>
                                    <td style="width: 20%; text-align: right; padding: 5px;">หมวดหมู่สินทรัพย์:</td>
                                    <td style="padding: 5px;">
                                        <asp:DropDownList ID="ddlAssetCategory" runat="server" Width="50%"
                                            AutoPostBack="true" OnSelectedIndexChanged="ddlAssetCategory_SelectedIndexChanged">
                                        </asp:DropDownList>
                                    </td>
                                </tr>
                                <tr>
                                    <td style="text-align: right; padding: 5px;">ชื่อสินทรัพย์:</td>
                                    <td style="padding: 5px;">
                                        <asp:TextBox ID="txtAssetName" runat="server" Width="60%"
                                            placeholder="เช่น คอมพิวเตอร์ตั้งโต๊ะ, แอร์ห้องประชุม"></asp:TextBox>
                                    </td>
                                </tr>
                                <tr>
                                    <td style="text-align: right; padding: 5px;">จำนวน:</td>
                                    <td style="padding: 5px;">
                                        <asp:TextBox ID="txtAssetQuantity" runat="server" Width="15%" TextMode="Number" Text="1" style="text-align: center;"></asp:TextBox>
                                        &nbsp;ชิ้น
                                        <span style="color: #666; font-size: 12px; margin-left: 10px;">
                                            (ถ้ามากกว่า 1 ชิ้น ระบบจะสร้างสินทรัพย์แยกแต่ละชิ้น และคำนวณราคาต่อชิ้นอัตโนมัติ)
                                        </span>
                                    </td>
                                </tr>
                                <tr>
                                    <td style="text-align: right; padding: 5px;">ยี่ห้อ/รุ่น:</td>
                                    <td style="padding: 5px;">
                                        <asp:TextBox ID="txtAssetBrand" runat="server" Width="30%" placeholder="ยี่ห้อ"></asp:TextBox>
                                        <asp:TextBox ID="txtAssetModel" runat="server" Width="28%" placeholder="รุ่น"></asp:TextBox>
                                    </td>
                                </tr>
                                <tr>
                                    <td style="text-align: right; padding: 5px;">หมายเลขซีเรียล:</td>
                                    <td style="padding: 5px;">
                                        <asp:TextBox ID="txtAssetSerial" runat="server" Width="40%"></asp:TextBox>
                                    </td>
                                </tr>
                                <tr>
                                    <td style="text-align: right; padding: 5px;">อายุการใช้งาน:</td>
                                    <td style="padding: 5px;">
                                        <asp:TextBox ID="txtAssetUsefulLife" runat="server" Width="15%" TextMode="Number" Text="5"></asp:TextBox>
                                        &nbsp;ปี
                                        <span style="margin-left: 20px;">มูลค่าซาก:</span>
                                        <asp:TextBox ID="txtAssetResidual" runat="server" Width="20%" TextMode="Number" Text="0"></asp:TextBox>
                                        &nbsp;บาท
                                    </td>
                                </tr>
                                <tr>
                                    <td style="text-align: right; padding: 5px;">สถานที่ตั้ง:</td>
                                    <td style="padding: 5px;">
                                        <asp:TextBox ID="txtAssetLocation" runat="server" Width="40%" placeholder="เช่น สำนักงาน, ห้องประชุม"></asp:TextBox>
                                    </td>
                                </tr>
                            </table>
                        </asp:Panel>
                    </div>
                </td>
            </tr>

            <tr>
                 <td class="modal-sm" style="width: 20%; text-align: right; vertical-align: top;">อัพโหลดไฟล์ที่เกี่ยวข้อง: </td>
                <td class="upload-section">
                    <div style="margin-bottom: 8px;">
                        <strong>📎 เลือกไฟล์:</strong>
                        <asp:FileUpload ID="FileUpload1" runat="server" />
                    </div>
                    <div style="margin-bottom: 8px;">
                        <strong>📝 คำอธิบายไฟล์:</strong>
                        <asp:TextBox ID="TextBox7" runat="server" Text="" Width="60%" placeholder="ระบุคำอธิบายไฟล์ (เช่น ใบเสร็จ, สัญญา, ฯลฯ)"></asp:TextBox>
                    </div>
                    <asp:Button ID="Button4" runat="server" Text="📤 Upload" CssClass="btn-upload" OnClick="Button4_Click" />
                </td>
            </tr>

            <tr style="background-color:whitesmoke;">
                 <td class="modal-sm" style="width: 20%; text-align: right; vertical-align: top;">รายการไฟล์แนบ:</td>
                <td>
                    <asp:GridView ID="GridView2" runat="server" AutoGenerateColumns="False"
                        OnRowDeleted="GridView2_RowDeleted" OnRowDeleting="GridView2_RowDeleting"
                        CssClass="file-grid" OnRowDataBound="GridView2_RowDataBound">
                        <Columns>
                            <asp:TemplateField HeaderText="ชื่อไฟล์">
                                <ItemTemplate>
                                    <asp:HyperLink ID="lnkViewFile" runat="server"
                                        NavigateUrl='<%# GetFileUrl(Eval("Name")) %>'
                                        Text='<%# "📄 " + Eval("Name") %>'
                                        Target="_blank"
                                        CssClass="file-link">
                                    </asp:HyperLink>
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:CommandField ShowDeleteButton="True" DeleteText="🗑️ ลบ"
                                ButtonType="Button" HeaderText="ลบไฟล์">
                                <ControlStyle CssClass="btn-delete-file" />
                            </asp:CommandField>
                        </Columns>
                        <EmptyDataTemplate>
                            <div style="padding: 15px; text-align: center; color: #999; font-style: italic;">
                                ไม่มีไฟล์แนบ
                            </div>
                        </EmptyDataTemplate>
                    </asp:GridView>
                 </td>
            </tr>

            <tr>
                 <td class="modal-sm" style="width: 20%; text-align: right">&nbsp;</td>
                <td style="padding: 15px 0;">
                    <asp:Button ID="Button3" runat="server" Text="💾 บันทึก" CssClass="btn-save" OnClick="Button3_Click" />
                 </td>
            </tr>

        </table>
    </p>
    <rsweb:reportviewer Visible="false" ID="ReportViewer2" runat="server" BackColor="" ClientIDMode="AutoID" HighlightBackgroundColor="" InternalBorderColor="204, 204, 204" InternalBorderStyle="Solid" InternalBorderWidth="1px" LinkActiveColor="" LinkActiveHoverColor="" LinkDisabledColor="" PrimaryButtonBackgroundColor="" PrimaryButtonForegroundColor="" PrimaryButtonHoverBackgroundColor="" PrimaryButtonHoverForegroundColor="" SecondaryButtonBackgroundColor="" SecondaryButtonForegroundColor="" SecondaryButtonHoverBackgroundColor="" SecondaryButtonHoverForegroundColor="" SplitterBackColor="" ToolbarDividerColor="" ToolbarForegroundColor="" ToolbarForegroundDisabledColor="" ToolbarHoverBackgroundColor="" ToolbarHoverForegroundColor="" ToolBarItemBorderColor="" ToolBarItemBorderStyle="Solid" ToolBarItemBorderWidth="1px" ToolBarItemHoverBackColor="" ToolBarItemPressedBorderColor="51, 102, 153" ToolBarItemPressedBorderStyle="Solid" ToolBarItemPressedBorderWidth="1px" ToolBarItemPressedHoverBackColor="153, 187, 226" Height="500px" CssClass="auto-style5" style="margin-top: 172px">
        <LocalReport EnableExternalImages="true" ReportPath="Account\Report\Payment.rdlc">
            
        </LocalReport>
</rsweb:reportviewer>
</asp:Content>
