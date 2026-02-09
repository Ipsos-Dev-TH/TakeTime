<%@ Page MaintainScrollPositionOnPostback="true" Title="" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="Take_Time_BangPhra.Voucher.Default" %>
<%@ Register assembly="Microsoft.ReportViewer.WebForms" namespace="Microsoft.Reporting.WebForms" tagprefix="rsweb" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
     <link rel="stylesheet" href="/Content/jquery-ui.css">
  <link rel="stylesheet" href="/Content/style.css">
    <link rel="stylesheet" type="text/css" href="/Content/GridView.css">
     <style>
        .header-center { text-align: center; }
        .header-right { text-align: right; }
        th, td { padding: 8px 12px; }

        /* Modern Form Styles */
        .voucher-container {
            max-width: 900px;
            margin: 0 auto;
            padding: 20px;
        }

        .voucher-title {
            text-align: center;
            font-size: 24px;
            font-weight: bold;
            color: #333;
            margin-bottom: 25px;
            padding-bottom: 15px;
            border-bottom: 3px solid #4CAF50;
        }

        .form-section {
            background: #fff;
            border-radius: 10px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
            padding: 25px;
            margin-bottom: 20px;
        }

        .section-title {
            font-size: 16px;
            font-weight: bold;
            color: #4CAF50;
            margin-bottom: 15px;
            padding-bottom: 8px;
            border-bottom: 2px solid #e0e0e0;
        }

        .form-row {
            display: flex;
            align-items: center;
            padding: 12px 0;
            border-bottom: 1px solid #f0f0f0;
        }

        .form-row:last-child {
            border-bottom: none;
        }

        .form-row.alt {
            background-color: #f9f9f9;
            margin: 0 -25px;
            padding: 12px 25px;
        }

        .form-label {
            width: 200px;
            text-align: right;
            padding-right: 15px;
            font-weight: 500;
            color: #555;
            flex-shrink: 0;
        }

        .form-input {
            flex: 1;
        }

        .required-mark {
            color: #e53935;
            font-weight: bold;
            margin-left: 2px;
        }

        .form-input input[type="text"],
        .form-input input[type="number"],
        .form-input input[type="date"],
        .form-input select {
            padding: 10px 12px;
            border: 1px solid #ddd;
            border-radius: 6px;
            font-size: 14px;
            transition: border-color 0.3s, box-shadow 0.3s;
        }

        .form-input input:focus,
        .form-input select:focus {
            border-color: #4CAF50;
            box-shadow: 0 0 0 3px rgba(76,175,80,0.1);
            outline: none;
        }

        .btn-primary {
            background: linear-gradient(135deg, #4CAF50, #45a049);
            color: white;
            border: none;
            padding: 12px 30px;
            border-radius: 6px;
            font-size: 16px;
            font-weight: bold;
            cursor: pointer;
            transition: all 0.3s;
        }

        .btn-primary:hover {
            background: linear-gradient(135deg, #45a049, #3d8b40);
            box-shadow: 0 4px 12px rgba(76,175,80,0.3);
        }

        .btn-secondary {
            background: #f5f5f5;
            color: #333;
            border: 1px solid #ddd;
            padding: 8px 20px;
            border-radius: 6px;
            cursor: pointer;
            transition: all 0.3s;
        }

        .btn-secondary:hover {
            background: #e0e0e0;
        }

        .btn-add {
            background: #2196F3;
            color: white;
            border: none;
            padding: 8px 20px;
            border-radius: 6px;
            cursor: pointer;
        }

        .btn-add:hover {
            background: #1976D2;
        }

        .upload-section {
            background: #fff3e0;
            border: 2px dashed #ff9800;
            border-radius: 10px;
            padding: 20px;
            text-align: center;
            margin: 15px 0;
        }

        .upload-section .upload-hint {
            color: #e65100;
            font-weight: bold;
            margin-bottom: 10px;
        }

        .price-summary {
            background: linear-gradient(135deg, #e8f5e9, #c8e6c9);
            border-radius: 10px;
            padding: 20px;
            margin-top: 20px;
        }

        .price-row {
            display: flex;
            justify-content: space-between;
            padding: 8px 0;
        }

        .price-total {
            font-size: 18px;
            font-weight: bold;
            color: #2e7d32;
            border-top: 2px solid #4CAF50;
            padding-top: 12px;
            margin-top: 8px;
        }

        .checkbox-group {
            padding: 10px 0;
        }

        .checkbox-group label {
            display: flex;
            align-items: center;
            cursor: pointer;
            padding: 8px 0;
        }

        .help-text {
            font-size: 12px;
            color: #888;
            margin-top: 5px;
        }

        /* Legacy styles for compatibility */
        .auto-style1 { width: 20%; height: 32px; }
        .auto-style2 { height: 32px; }
        .auto-style3 { color: #FF0000; }
        .auto-style4 { width: 20%; height: 30px; }
        .auto-style5 { height: 30px; }
        .auto-style6 { width: 20%; height: 35px; }
        .auto-style7 { height: 35px; }
        .auto-style8 { width: 20%; height: 37px; }
        .auto-style9 { height: 37px; }

        /* GridView styling */
        .styled-grid {
            width: 100%;
            border-collapse: collapse;
            margin: 15px 0;
        }

        .styled-grid th {
            background: #4CAF50;
            color: white;
            padding: 12px;
            text-align: left;
        }

        .styled-grid td {
            padding: 10px 12px;
            border-bottom: 1px solid #e0e0e0;
        }

        .styled-grid tr:hover {
            background: #f5f5f5;
        }

        /* ==================== MOBILE RESPONSIVE ==================== */
        @media screen and (max-width: 768px) {
            .voucher-container {
                padding: 10px;
                max-width: 100%;
            }

            .voucher-title {
                font-size: 20px;
                margin-bottom: 15px;
                padding-bottom: 10px;
            }

            .form-section {
                padding: 15px;
                margin-bottom: 15px;
                border-radius: 8px;
            }

            .section-title {
                font-size: 14px;
                margin-bottom: 12px;
            }

            /* Stack form rows vertically on mobile */
            .form-row {
                flex-direction: column;
                align-items: flex-start;
                padding: 10px 0;
            }

            .form-row.alt {
                margin: 0 -15px;
                padding: 10px 15px;
            }

            .form-label {
                width: 100%;
                text-align: left;
                padding-right: 0;
                padding-bottom: 5px;
                font-size: 13px;
            }

            .form-input {
                width: 100%;
            }

            /* Make inputs full width on mobile */
            .form-input input[type="text"],
            .form-input input[type="number"],
            .form-input input[type="date"],
            .form-input select {
                width: 100% !important;
                max-width: 100%;
                box-sizing: border-box;
                font-size: 16px; /* Prevent zoom on iOS */
            }

            .form-input .aspNetDisabled {
                width: 100% !important;
            }

            /* Dropdowns stacked */
            .form-input select {
                margin-bottom: 8px;
            }

            /* Buttons responsive */
            .btn-secondary,
            .btn-add {
                width: 100%;
                margin-top: 8px;
                padding: 12px 15px;
            }

            .btn-primary {
                width: 100%;
                padding: 15px 20px;
                font-size: 18px;
            }

            /* Price summary responsive */
            .price-summary {
                padding: 15px;
                margin-top: 15px;
            }

            .price-summary .form-row {
                padding: 8px 0;
            }

            .price-total {
                font-size: 16px;
            }

            /* Upload section responsive */
            .upload-section {
                padding: 15px;
            }

            .upload-section .upload-hint {
                font-size: 13px;
            }

            .upload-section input[type="file"] {
                width: 100% !important;
                margin-bottom: 10px;
            }

            .upload-section .btn-add {
                width: 100%;
                margin-left: 0 !important;
                margin-top: 10px;
            }

            .upload-section img {
                width: 100% !important;
                max-width: 100%;
                height: auto !important;
                max-height: none !important;
            }

            /* GridView responsive */
            .styled-grid {
                font-size: 12px;
                display: block;
                overflow-x: auto;
                white-space: nowrap;
            }

            .styled-grid th,
            .styled-grid td {
                padding: 8px 6px;
            }

            /* Help text */
            .help-text {
                font-size: 11px;
            }

            /* Checkbox group */
            .checkbox-group label {
                font-size: 13px;
            }
        }

        /* Extra small devices */
        @media screen and (max-width: 480px) {
            .voucher-title {
                font-size: 18px;
            }

            .form-section {
                padding: 12px;
            }

            .section-title {
                font-size: 13px;
            }

            .form-label {
                font-size: 12px;
            }

            .form-input input,
            .form-input select {
                font-size: 16px;
                padding: 10px;
            }

            .btn-primary {
                font-size: 16px;
                padding: 14px 15px;
            }

            .styled-grid {
                font-size: 11px;
            }

            .styled-grid th,
            .styled-grid td {
                padding: 6px 4px;
            }
        }
    </style>

    
    <div class="voucher-container">
        <div class="voucher-title">ขาย Voucher</div>

        <!-- ส่วนที่ 1: ข้อมูลลูกค้า -->
        <div class="form-section">
            <div class="section-title">ข้อมูลลูกค้า</div>

            <div class="form-row alt">
                <div class="form-label">วันที่ขาย Voucher<span class="required-mark">*</span></div>
                <div class="form-input">
                    <asp:TextBox ID="TextBox8" runat="server" Width="200px" TextMode="Date"></asp:TextBox>
                </div>
            </div>

            <div class="form-row">
                <div class="form-label">เบอร์โทร<span class="required-mark">*</span></div>
                <div class="form-input">
                    <asp:TextBox ID="TextBox13" runat="server" Width="300px" OnTextChanged="TextBox13_TextChanged" AutoPostBack="True" placeholder="กรอกเบอร์โทรเพื่อค้นหาข้อมูลลูกค้า"></asp:TextBox>
                </div>
            </div>

            <div class="form-row alt">
                <div class="form-label">ชื่อลูกค้า<span class="required-mark">*</span></div>
                <div class="form-input">
                    <asp:DropDownList ID="DropDownList8" runat="server" Width="150px" AutoPostBack="True" OnSelectedIndexChanged="DropDownList8_SelectedIndexChanged"></asp:DropDownList>
                    <asp:TextBox ID="TextBox10" runat="server" Width="250px" placeholder="ชื่อ-นามสกุล"></asp:TextBox>
                    <asp:TextBox ID="TextBox7" runat="server" Width="120px" PlaceHolder="รหัสสาขา" text="00000"></asp:TextBox>
                </div>
            </div>

            <div class="form-row">
                <div class="form-label">ที่อยู่</div>
                <div class="form-input">
                    <asp:TextBox ID="TextBox11" runat="server" Width="100px" placeholder="เลขที่"></asp:TextBox>
                    <asp:TextBox ID="TextBox18" runat="server" Width="350px" placeholder="รายละเอียดที่อยู่"></asp:TextBox>
                </div>
            </div>

            <div class="form-row alt">
                <div class="form-label">รหัสไปรษณีย์</div>
                <div class="form-input">
                    <asp:TextBox ID="TextBox16" runat="server" Width="150px" AutoPostBack="True" OnTextChanged="TextBox16_TextChanged" TextMode="Number" placeholder="รหัสไปรษณีย์"></asp:TextBox>
                    <asp:Button ID="Button5" runat="server" Text="ค้นหา" CssClass="btn-secondary" OnClick="Button5_Click" />
                    <asp:Button ID="Button6" runat="server" Text="ยกเลิก" CssClass="btn-secondary" OnClick="Button6_Click" />
                </div>
            </div>

            <div class="form-row">
                <div class="form-label">จังหวัด/อำเภอ/ตำบล</div>
                <div class="form-input">
                    <asp:DropDownList ID="DropDownList5" runat="server" Width="160px" AutoPostBack="True" OnSelectedIndexChanged="DropDownList5_SelectedIndexChanged"></asp:DropDownList>
                    <asp:DropDownList ID="DropDownList6" runat="server" Width="160px" AutoPostBack="True" OnSelectedIndexChanged="DropDownList6_SelectedIndexChanged"></asp:DropDownList>
                    <asp:DropDownList ID="DropDownList7" runat="server" Width="160px"></asp:DropDownList>
                </div>
            </div>

            <div class="form-row alt">
                <div class="form-label">เลขประจำตัวผู้เสียภาษี</div>
                <div class="form-input">
                    <asp:TextBox ID="TextBox12" runat="server" Width="300px" AutoPostBack="True" OnTextChanged="TextBox12_TextChanged" placeholder="13 หลัก"></asp:TextBox>
                </div>
            </div>

            <div class="form-row">
                <div class="form-label">อีเมล์</div>
                <div class="form-input">
                    <asp:TextBox ID="TextBox17" runat="server" Width="300px" placeholder="email@example.com"></asp:TextBox>
                </div>
            </div>

            <div class="form-row alt">
                <div class="form-label"></div>
                <div class="form-input checkbox-group">
                    <asp:CheckBox ID="CheckBox4" runat="server" Text=" ไม่ออกใบกำกับภาษีในระบบ" OnCheckedChanged="CheckBox4_CheckedChanged" AutoPostBack="True" CssClass="mycheckbox"/>
                </div>
            </div>

            <div class="form-row">
                <div class="form-label"></div>
                <div class="form-input checkbox-group">
                    <asp:CheckBox ID="CheckBox3" Text=" ประสงค์ไม่ระบุชื่อในใบกำกับภาษี" runat="server" AutoPostBack="True" OnCheckedChanged="CheckBox3_CheckedChanged" Checked="true" />
                </div>
            </div>

            <div class="form-row alt">
                <div class="form-label"></div>
                <div class="form-input checkbox-group">
                    <asp:CheckBox ID="CheckBox5" Text=" ต้องการรับ e-Tax Invoice" runat="server" AutoPostBack="True" OnCheckedChanged="CheckBox5_CheckedChanged" />
                </div>
            </div>
        </div>

        <!-- ส่วนที่ 2: เลือกประเภท Voucher -->
        <div class="form-section">
            <div class="section-title">เลือกประเภท Voucher<span class="required-mark">*</span></div>
            <p class="help-text">กรุณาเลือกประเภทเรทที่พักและกดปุ่ม "เพิ่ม" เพื่อเพิ่มในรายการ</p>

            <div class="form-row">
                <div class="form-label">ประเภทเรทที่พัก</div>
                <div class="form-input">
                    <asp:DropDownList ID="DropDownList3" runat="server" Width="300px" DataSourceID="SqlDataSource3" DataTextField="Group_Name" DataValueField="GroupID" AppendDataBoundItems="true">
                        <asp:ListItem>---โปรดเลือก---</asp:ListItem>
                    </asp:DropDownList>
                    <asp:SqlDataSource ID="SqlDataSource3" runat="server" ConnectionString="<%$ ConnectionStrings:TaketimeConnectionString %>" SelectCommand="SELECT Distinct([Group_Name]),GroupID FROM [Taketime].[dbo].[Accommodation_RatePlan_Group] Where [Status] = 'True'"></asp:SqlDataSource>
                </div>
            </div>

            <div class="form-row alt">
                <div class="form-label">ราคาที่ต้องจ่ายเพิ่มเมื่อใช้ Voucher</div>
                <div class="form-input">
                    <asp:TextBox ID="TextBox2" runat="server" Width="150px" TextMode="Number">0</asp:TextBox> บาท
                    <asp:Button ID="Button2" runat="server" Text="+ เพิ่ม" CssClass="btn-add" OnClick="Button2_Click" />
                </div>
            </div>

            <div class="form-row">
                <div class="form-label"></div>
                <div class="form-input">
                    <div class="mobile-table-wrapper">
                    <asp:GridView ID="GridView1" runat="server" AutoGenerateColumns="False" OnRowDeleting="GridView1_RowDeleting" CssClass="styled-grid gridview-table">
                        <Columns>
                            <asp:BoundField DataField="Number" HeaderText="ลำดับ" />
                            <asp:BoundField DataField="RatePlan_Group" HeaderText="ประเภทเรทที่พัก" />
                            <asp:BoundField DataField="PriceTo" HeaderText="ราคาที่ต้องจ่ายเพิ่ม" />
                            <asp:CommandField ShowDeleteButton="True" ButtonType="Button" DeleteText="ลบ" />
                        </Columns>
                    </asp:GridView>
                    </div>
                </div>
            </div>
        </div>

        <!-- ส่วนที่ 3: การชำระเงิน -->
        <div class="form-section">
            <div class="section-title">การชำระเงิน</div>

            <div class="form-row alt">
                <div class="form-label">วิธีชำระเงิน<span class="required-mark">*</span></div>
                <div class="form-input">
                    <asp:DropDownList ID="DropDownList2" runat="server" Width="300px" AppendDataBoundItems="true">
                        <asp:ListItem>---โปรดเลือก---</asp:ListItem>
                    </asp:DropDownList>
                    <asp:SqlDataSource ID="SqlDataSource2" runat="server" ConnectionString="<%$ ConnectionStrings:TaketimeConnectionString %>" SelectCommand="SELECT * FROM [Account_Paid_How] WHERE ([Status] = 'True')">
                        <SelectParameters>
                            <asp:Parameter DefaultValue="True" Name="Status" Type="Boolean" />
                        </SelectParameters>
                    </asp:SqlDataSource>
                </div>
            </div>

            <div class="form-row">
                <div class="form-label">ประเภทภาษี<span class="required-mark">*</span></div>
                <div class="form-input">
                    <asp:DropDownList ID="DropDownList4" runat="server" Width="300px" OnSelectedIndexChanged="DropDownList4_SelectedIndexChanged" AppendDataBoundItems="true" AutoPostBack="True">
                        <asp:ListItem>---โปรดเลือก---</asp:ListItem>
                    </asp:DropDownList>
                    <asp:SqlDataSource ID="SqlDataSource4" runat="server" ConnectionString="<%$ ConnectionStrings:TaketimeConnectionString %>" SelectCommand="SELECT * FROM [Account_Vat_Type] WHERE ([Status] = 'True')">
                        <SelectParameters>
                            <asp:Parameter DefaultValue="True" Name="Status" Type="Boolean" />
                        </SelectParameters>
                    </asp:SqlDataSource>
                </div>
            </div>

            <div class="form-row alt">
                <div class="form-label">หมายเหตุ</div>
                <div class="form-input">
                    <asp:TextBox ID="TextBox20" runat="server" Width="400px" Enabled="true" placeholder="หมายเหตุเพิ่มเติม (ถ้ามี)"></asp:TextBox>
                </div>
            </div>

            <!-- Price Summary Section -->
            <div class="price-summary">
                <div class="form-row">
                    <div class="form-label">ราคาขายต่อ 1 ใบ<span class="required-mark">*</span></div>
                    <div class="form-input">
                        <asp:TextBox ID="TextBox6" runat="server" Width="150px" Enabled="true" OnTextChanged="TextBox6_TextChanged" AutoPostBack="True" placeholder="0.00"></asp:TextBox> บาท
                    </div>
                </div>

                <div class="form-row">
                    <div class="form-label">จำนวนใบที่ขาย<span class="required-mark">*</span></div>
                    <div class="form-input">
                        <asp:TextBox ID="TextBox19" runat="server" Width="80px" Enabled="true" AutoPostBack="True" TextMode="Number" Text="1" OnTextChanged="TextBox19_TextChanged"></asp:TextBox> ใบ
                    </div>
                </div>

                <div class="form-row">
                    <div class="form-label">จำนวนรวมก่อนภาษีมูลค่าเพิ่ม</div>
                    <div class="form-input">
                        <asp:TextBox ID="TextBox3" runat="server" Width="150px" Enabled="false" OnTextChanged="TextBox3_TextChanged" style="background:#fff;"></asp:TextBox> บาท
                    </div>
                </div>

                <div class="form-row">
                    <div class="form-label"><asp:Label ID="Label1" runat="server" Text="ภาษี"></asp:Label></div>
                    <div class="form-input">
                        <asp:TextBox ID="TextBox4" runat="server" Width="150px" Enabled="False" style="background:#fff;"></asp:TextBox> บาท
                    </div>
                </div>

                <div class="form-row price-total">
                    <div class="form-label">ราคาขาย Voucher สุทธิ</div>
                    <div class="form-input">
                        <asp:TextBox ID="TextBox1" runat="server" Width="150px" Enabled="False" style="background:#fff; font-weight:bold; font-size:16px;"></asp:TextBox> บาท
                    </div>
                </div>

                <div class="form-row">
                    <div class="form-label">Voucher หมดอายุ</div>
                    <div class="form-input">
                        <asp:TextBox ID="TextBox21" runat="server" Width="200px" TextMode="Date"></asp:TextBox>
                    </div>
                </div>
            </div>
        </div>

        <!-- ส่วนที่ 4: อัพโหลดสลิป -->
        <div class="form-section">
            <div class="section-title">อัพโหลดหลักฐานการชำระเงิน<span class="required-mark">*</span></div>
            <div class="upload-section">
                <div class="upload-hint">กรุณาอัพโหลดสลิปการโอนเงินก่อนกดบันทึก</div>
                <asp:FileUpload ID="FileUpload1" runat="server" Width="300px" CssClass="rounded-textbox"/>
                <asp:Button ID="Button7" runat="server" Text="อัพโหลดรูป" CssClass="btn-add" OnClick="Button7_Click" style="margin-left:10px;" />
                <br /><br />
                <asp:Image ID="Image1" runat="server" Width="400px" style="max-height:300px; object-fit:contain;" />
            </div>
        </div>

        <!-- ปุ่มบันทึก -->
        <div style="text-align:center; margin: 30px 0;">
            <asp:Button ID="Button3" runat="server" Text="บันทึก Voucher" CssClass="btn-primary" OnClick="Button3_Click" OnClientClick="return mydialog(this)" />
        </div>

        <div id="mypopdiv" style="display:none">
            <br /><h2>Voucher Number = <asp:Label ID="LabelVoucher" runat="server" Text=""></asp:Label></h2>
        </div>

        <script>
            var myok = false
            function mydialog(btn) {
                if (myok)
                    return true

                var mydiv = $("#mypopdiv")
                mydiv.dialog({
                    modal: true, appendTo: "form",
                    title: "ยืนยันการบันทึก Voucher", closeText: "",
                    dialogClass: "dialogWithDropShadow",
                    position: { my: 'center', at: 'center', of: window },
                    buttons: {
                        "ยืนยัน": function () {
                            mydiv.dialog('close')
                            myok = true
                            btn.click()
                        },
                        "ยกเลิก": function () {
                            mydiv.dialog('close')
                        }
                    }
                });

                return false
            }
        </script>
    </div>

    <rsweb:reportviewer Visible="false" ID="ReportViewer2" runat="server" BackColor="" ClientIDMode="AutoID" HighlightBackgroundColor="" InternalBorderColor="204, 204, 204" InternalBorderStyle="Solid" InternalBorderWidth="1px" LinkActiveColor="" LinkActiveHoverColor="" LinkDisabledColor="" PrimaryButtonBackgroundColor="" PrimaryButtonForegroundColor="" PrimaryButtonHoverBackgroundColor="" PrimaryButtonHoverForegroundColor="" SecondaryButtonBackgroundColor="" SecondaryButtonForegroundColor="" SecondaryButtonHoverBackgroundColor="" SecondaryButtonHoverForegroundColor="" SplitterBackColor="" ToolbarDividerColor="" ToolbarForegroundColor="" ToolbarForegroundDisabledColor="" ToolbarHoverBackgroundColor="" ToolbarHoverForegroundColor="" ToolBarItemBorderColor="" ToolBarItemBorderStyle="Solid" ToolBarItemBorderWidth="1px" ToolBarItemHoverBackColor="" ToolBarItemPressedBorderColor="51, 102, 153" ToolBarItemPressedBorderStyle="Solid" ToolBarItemPressedBorderWidth="1px" ToolBarItemPressedHoverBackColor="153, 187, 226" Height="500px" CssClass="auto-style5" style="margin-top: 10px">
        <LocalReport EnableExternalImages="true" ReportPath="Account\Report\Receipt.rdlc">
            
        </LocalReport>
</rsweb:reportviewer>

    <asp:Panel ID="Panel1" runat="server" Visible="False"><center><iframe id="myFrame" runat="server" width="640" height="480" frameborder="0"></iframe></center></asp:Panel>
</asp:Content>
