<%@ Page MaintainScrollPositionOnPostback="true" Title="ตรวจสอบเอกสารและรายได้" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="CheckDocument_New.aspx.cs" Inherits="Take_Time_BangPhra.Account.CheckDocument_New" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <link rel="stylesheet" href="/Content/jquery-ui.css">
    <style>
        .accounting-dashboard {
            max-width: 98%;
            margin: 10px auto;
            padding: 5px;
        }

        .dashboard-header {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 15px 20px;
            border-radius: 8px 8px 0 0;
            margin-bottom: 0;
        }

        .dashboard-header h2 {
            margin: 0;
            font-size: 24px;
            font-weight: 600;
        }

        .search-section {
            background: white;
            padding: 15px 20px;
            border-left: 1px solid #e0e0e0;
            border-right: 1px solid #e0e0e0;
            margin-bottom: 0;
        }

        .search-row {
            display: flex;
            gap: 20px;
            margin-bottom: 15px;
            align-items: center;
        }

        .search-label {
            font-weight: 600;
            color: #2c3e50;
            min-width: 120px;
        }

        .search-input {
            padding: 8px 12px;
            border: 1px solid #ddd;
            border-radius: 4px;
            font-size: 14px;
        }

        .btn-search {
            background: #3498db;
            color: white;
            padding: 10px 30px;
            border: none;
            border-radius: 5px;
            font-size: 16px;
            font-weight: 600;
            cursor: pointer;
            transition: background 0.3s;
        }

        .btn-search:hover {
            background: #2980b9;
        }

        .btn-export {
            background: #27ae60;
            color: white;
            padding: 10px 30px;
            border: none;
            border-radius: 5px;
            font-size: 16px;
            font-weight: 600;
            cursor: pointer;
            margin-left: 10px;
        }

        .summary-section {
            background: white;
            padding: 15px 20px;
            border-left: 1px solid #e0e0e0;
            border-right: 1px solid #e0e0e0;
            border-bottom: 1px solid #e0e0e0;
            border-radius: 0 0 8px 8px;
        }

        .summary-title {
            font-size: 18px;
            font-weight: 600;
            color: #2c3e50;
            margin-bottom: 15px;
            padding-bottom: 8px;
            border-bottom: 2px solid #3498db;
        }

        .revenue-table {
            width: 100%;
            border-collapse: collapse;
            margin-bottom: 15px;
            font-size: 13px;
        }

        .revenue-table th {
            background: #34495e;
            color: white;
            padding: 10px;
            text-align: center;
            font-weight: 600;
            border: 1px solid #2c3e50;
        }

        .revenue-table td {
            padding: 8px 10px;
            border: 1px solid #ddd;
            text-align: right;
        }

        .revenue-table td.category {
            text-align: left;
            font-weight: 600;
            background: #ecf0f1;
        }

        .revenue-table tr:hover {
            background: #f8f9fa;
        }

        .revenue-table .total-row {
            background: #3498db;
            color: white;
            font-weight: 700;
            font-size: 14px;
        }

        .revenue-table .total-row td {
            border-color: #2980b9;
        }

        .validation-box {
            padding: 15px;
            border-radius: 5px;
            margin-top: 20px;
            display: flex;
            align-items: center;
            gap: 10px;
            font-size: 16px;
            font-weight: 600;
        }

        .validation-success {
            background: #d4edda;
            border: 2px solid #28a745;
            color: #155724;
        }

        .validation-error {
            background: #f8d7da;
            border: 2px solid #dc3545;
            color: #721c24;
        }

        .detail-section {
            margin-top: 15px;
            background: white;
            padding: 15px 20px;
            border-radius: 8px;
            border: 1px solid #e0e0e0;
        }

        .detail-title {
            font-size: 18px;
            font-weight: 600;
            color: #2c3e50;
            margin-bottom: 12px;
        }

        .gridview-custom {
            width: 100%;
            border-collapse: collapse;
        }

        .gridview-custom th {
            background: #34495e;
            color: white;
            padding: 8px 10px;
            text-align: left;
            font-weight: 600;
            font-size: 13px;
        }

        .gridview-custom td {
            padding: 6px 10px;
            border-bottom: 1px solid #ddd;
            font-size: 13px;
        }

        .gridview-custom tr:hover {
            background: #f8f9fa;
        }

        .amount-cell {
            text-align: right;
            font-weight: 600;
        }

        .loading-overlay {
            display: none;
            position: fixed;
            top: 0;
            left: 0;
            width: 100%;
            height: 100%;
            background: rgba(0,0,0,0.5);
            z-index: 9999;
            justify-content: center;
            align-items: center;
        }

        .loading-spinner {
            background: white;
            padding: 30px;
            border-radius: 10px;
            text-align: center;
        }

        /* Slip View Button Styles */
        .btn-view-slip {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white !important;
            padding: 6px 14px;
            border-radius: 5px;
            text-decoration: none;
            font-size: 13px;
            font-weight: 600;
            display: inline-block;
            transition: all 0.3s;
            box-shadow: 0 2px 5px rgba(0,0,0,0.15);
        }

        .btn-view-slip:hover {
            background: linear-gradient(135deg, #764ba2 0%, #667eea 100%);
            text-decoration: none;
            transform: translateY(-2px);
            box-shadow: 0 4px 8px rgba(0,0,0,0.2);
        }

        .btn-no-slip {
            background: #bdc3c7;
            color: #7f8c8d !important;
            padding: 6px 14px;
            border-radius: 5px;
            text-decoration: none;
            font-size: 13px;
            font-weight: 600;
            display: inline-block;
            cursor: not-allowed;
            opacity: 0.7;
        }

        /* GridView Button Styles */
        .gridview-custom input[type="button"] {
            background: linear-gradient(135deg, #3498db 0%, #2980b9 100%);
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

        .gridview-custom input[type="button"]:hover {
            background: linear-gradient(135deg, #2980b9 0%, #3498db 100%);
            transform: translateY(-2px);
            box-shadow: 0 4px 8px rgba(0,0,0,0.2);
        }

        /* Delete button - Red theme */
        .gridview-custom td:first-child input[type="button"] {
            background: linear-gradient(135deg, #e74c3c 0%, #c0392b 100%);
        }

        .gridview-custom td:first-child input[type="button"]:hover {
            background: linear-gradient(135deg, #c0392b 0%, #e74c3c 100%);
        }

        /* Edit button - Orange theme */
        .gridview-custom td:nth-child(3) input[type="button"] {
            background: linear-gradient(135deg, #f39c12 0%, #e67e22 100%);
        }

        .gridview-custom td:nth-child(3) input[type="button"]:hover {
            background: linear-gradient(135deg, #e67e22 0%, #f39c12 100%);
        }

        .sync-badge { display: inline-block; padding: 3px 10px; border-radius: 12px; font-size: 11px; font-weight: 600; white-space: nowrap; }
        .sync-badge.completed { background: #d4edda; color: #155724; }
        .sync-badge.pending { background: #fff3cd; color: #856404; }
        .sync-badge.failed { background: #f8d7da; color: #721c24; }
        .sync-badge.none { background: #e2e3e5; color: #383d41; }
        a.sync-badge.completed { text-decoration: none; cursor: pointer; }
        a.sync-badge.completed:hover { background: #b7dfb5; text-decoration: underline; }
        .btn-sync-action { background: linear-gradient(135deg, #3498db 0%, #2980b9 100%); color: white; padding: 5px 12px; border: none; border-radius: 5px; font-size: 11px; cursor: pointer; white-space: nowrap; }
        .btn-sync-action:hover { background: linear-gradient(135deg, #2980b9 0%, #2471a3 100%); }
    </style>

    <div class="accounting-dashboard">
        <!-- Header -->
        <div class="dashboard-header">
            <h2>📊 ระบบตรวจสอบเอกสารและรายได้</h2>
        </div>

        <!-- Search Section -->
        <div class="search-section">
            <div class="search-row">
                <span class="search-label">ระหว่างวันที่:</span>
                <asp:TextBox ID="txtStartDate" runat="server" CssClass="search-input"
                    placeholder="2025-01-01" Width="150px"></asp:TextBox>
                <span>ถึง</span>
                <asp:TextBox ID="txtEndDate" runat="server" CssClass="search-input"
                    placeholder="2025-01-31" Width="150px"></asp:TextBox>
            </div>

            <div class="search-row">
                <span class="search-label">หรือเลือกเดือน:</span>
                <asp:DropDownList ID="ddlMonth" runat="server" CssClass="search-input" Width="200px">
                    <asp:ListItem Value="">-- เลือกเดือน --</asp:ListItem>
                    <asp:ListItem Value="1">มกราคม</asp:ListItem>
                    <asp:ListItem Value="2">กุมภาพันธ์</asp:ListItem>
                    <asp:ListItem Value="3">มีนาคม</asp:ListItem>
                    <asp:ListItem Value="4">เมษายน</asp:ListItem>
                    <asp:ListItem Value="5">พฤษภาคม</asp:ListItem>
                    <asp:ListItem Value="6">มิถุนายน</asp:ListItem>
                    <asp:ListItem Value="7">กรกฎาคม</asp:ListItem>
                    <asp:ListItem Value="8">สิงหาคม</asp:ListItem>
                    <asp:ListItem Value="9">กันยายน</asp:ListItem>
                    <asp:ListItem Value="10">ตุลาคม</asp:ListItem>
                    <asp:ListItem Value="11">พฤศจิกายน</asp:ListItem>
                    <asp:ListItem Value="12">ธันวาคม</asp:ListItem>
                </asp:DropDownList>
                <asp:DropDownList ID="ddlYear" runat="server" CssClass="search-input" Width="100px">
                </asp:DropDownList>
            </div>

            <div class="search-row">
                <span style="color: #7f8c8d; font-size: 14px;">
                    <i class="fa fa-info-circle"></i>
                    <strong>หมายเหตุ:</strong> ยอดรวมคำนวณเฉพาะเอกสารปกติ (ไม่รวมที่ยกเลิก) / ตารางแสดงทั้งหมดรวมเอกสารที่ยกเลิก
                </span>
            </div>

            <div class="search-row">
                <asp:Button ID="btnSearch" runat="server" Text="🔍 ค้นหา" CssClass="btn-search" OnClick="btnSearch_Click" />
                <asp:Button ID="btnExport" runat="server" Text="📄 Export CSV" CssClass="btn-export" OnClick="btnExport_Click" />
                <asp:Button ID="btnAuditPayments" runat="server" Text="🔍 ตรวจยอดชำระ NextAcc" CssClass="btn-export"
                    OnClick="btnAuditPayments_Click" CausesValidation="false"
                    ToolTip="ตรวจทุกใบในช่วงวันที่: รับเงินซ้อน (ชำระเกินยอด) / ค้างชำระ (settle ไม่ครบ)"
                    OnClientClick="this.disabled=true; this.value='⏳ กำลังตรวจ...';" UseSubmitBehavior="false" />
            </div>
        </div>

        <!-- Summary Section -->
        <div class="summary-section">
            <div class="summary-title">📈 สรุปรายได้ตามหมวด</div>

            <table class="revenue-table">
                <thead>
                    <tr>
                        <th style="width: 30%">หมวดรายได้</th>
                        <th style="width: 12%">เงินสด</th>
                        <th style="width: 15%">โอนกสิกร</th>
                        <th style="width: 15%">โอนกรุงไทย</th>
                        <th style="width: 13%">เงินกรรมการ</th>
                        <th style="width: 15%">รวม</th>
                    </tr>
                </thead>
                <tbody>
                    <tr>
                        <td class="category">1️⃣ รายได้จากการจองพัก (เช็คอินในช่วง - ไม่รวมมัดจำ)</td>
                        <td class="amount-cell"><asp:Label ID="lblCat1Cash" runat="server" Text="0.00"></asp:Label></td>
                        <td class="amount-cell"><asp:Label ID="lblCat1KBANK" runat="server" Text="0.00"></asp:Label></td>
                        <td class="amount-cell"><asp:Label ID="lblCat1KTB" runat="server" Text="0.00"></asp:Label></td>
                        <td class="amount-cell"><asp:Label ID="lblCat1Director" runat="server" Text="0.00"></asp:Label></td>
                        <td class="amount-cell"><asp:Label ID="lblCat1Total" runat="server" Text="0.00"></asp:Label></td>
                    </tr>
                    <tr>
                        <td class="category">2️⃣ รายได้จากมัดจำทั้งหมด (ทุกวันเข้าพัก)</td>
                        <td class="amount-cell"><asp:Label ID="lblCat2Cash" runat="server" Text="0.00"></asp:Label></td>
                        <td class="amount-cell"><asp:Label ID="lblCat2KBANK" runat="server" Text="0.00"></asp:Label></td>
                        <td class="amount-cell"><asp:Label ID="lblCat2KTB" runat="server" Text="0.00"></asp:Label></td>
                        <td class="amount-cell"><asp:Label ID="lblCat2Director" runat="server" Text="0.00"></asp:Label></td>
                        <td class="amount-cell"><asp:Label ID="lblCat2Total" runat="server" Text="0.00"></asp:Label></td>
                    </tr>
                    <tr>
                        <td class="category">3️⃣ รายได้จากขายสินค้า</td>
                        <td class="amount-cell"><asp:Label ID="lblCat3Cash" runat="server" Text="0.00"></asp:Label></td>
                        <td class="amount-cell"><asp:Label ID="lblCat3KBANK" runat="server" Text="0.00"></asp:Label></td>
                        <td class="amount-cell"><asp:Label ID="lblCat3KTB" runat="server" Text="0.00"></asp:Label></td>
                        <td class="amount-cell"><asp:Label ID="lblCat3Director" runat="server" Text="0.00"></asp:Label></td>
                        <td class="amount-cell"><asp:Label ID="lblCat3Total" runat="server" Text="0.00"></asp:Label></td>
                    </tr>
                    <tr>
                        <td class="category">4️⃣ รายได้อื่นๆ</td>
                        <td class="amount-cell"><asp:Label ID="lblCat4Cash" runat="server" Text="0.00"></asp:Label></td>
                        <td class="amount-cell"><asp:Label ID="lblCat4KBANK" runat="server" Text="0.00"></asp:Label></td>
                        <td class="amount-cell"><asp:Label ID="lblCat4KTB" runat="server" Text="0.00"></asp:Label></td>
                        <td class="amount-cell"><asp:Label ID="lblCat4Director" runat="server" Text="0.00"></asp:Label></td>
                        <td class="amount-cell"><asp:Label ID="lblCat4Total" runat="server" Text="0.00"></asp:Label></td>
                    </tr>
                    <tr class="total-row">
                        <td>💰 รวมทั้งหมด</td>
                        <td class="amount-cell"><asp:Label ID="lblTotalCash" runat="server" Text="0.00"></asp:Label></td>
                        <td class="amount-cell"><asp:Label ID="lblTotalKBANK" runat="server" Text="0.00"></asp:Label></td>
                        <td class="amount-cell"><asp:Label ID="lblTotalKTB" runat="server" Text="0.00"></asp:Label></td>
                        <td class="amount-cell"><asp:Label ID="lblTotalDirector" runat="server" Text="0.00"></asp:Label></td>
                        <td class="amount-cell"><asp:Label ID="lblGrandTotal" runat="server" Text="0.00"></asp:Label></td>
                    </tr>
                </tbody>
            </table>

            <!-- Validation Box -->
            <asp:Panel ID="pnlValidation" runat="server" CssClass="validation-box" Visible="false">
                <asp:Label ID="lblValidationIcon" runat="server"></asp:Label>
                <asp:Label ID="lblValidationMessage" runat="server"></asp:Label>
            </asp:Panel>

            <!-- Additional Info -->
            <div style="margin-top: 20px; padding: 15px; background: #f8f9fa; border-radius: 5px;">
                <div style="display: flex; gap: 40px;">
                    <div>
                        <strong>จำนวนเอกสารทั้งหมด:</strong>
                        <asp:Label ID="lblDocCount" runat="server" Text="0" style="margin-left: 10px; font-size: 18px; color: #3498db; font-weight: 700;"></asp:Label>
                        <span> เอกสาร</span>
                    </div>
                    <div>
                        <strong>ยอดรวมภาษี (VAT):</strong>
                        <asp:Label ID="lblTotalVAT" runat="server" Text="0.00" style="margin-left: 10px; font-size: 18px; color: #e74c3c; font-weight: 700;"></asp:Label>
                        <span> บาท</span>
                    </div>
                    <div>
                        <strong>ช่วงวันที่:</strong>
                        <asp:Label ID="lblDateRange" runat="server" Text="-" style="margin-left: 10px; color: #7f8c8d;"></asp:Label>
                    </div>
                </div>
            </div>
        </div>

        <!-- Detail Section -->
        <div class="detail-section">
            <div class="detail-title">📋 รายละเอียดเอกสาร</div>
            <div style="margin-bottom: 10px;">
                <asp:CheckBox ID="chkEnableDelete" runat="server" Text="เปิดใช้งานปุ่มลบ (Delete)" />
            </div>
            <asp:GridView ID="gvDetails" runat="server" CssClass="gridview-custom"
                AutoGenerateColumns="False" EmptyDataText="ไม่พบข้อมูล"
                DataKeyNames="ID,Status,IsNextAccOnly,NextAccId,NextAccViewUrl,NextAccDocStatus,Reservation_ID"
                OnRowDeleting="gvDetails_RowDeleting"
                OnSelectedIndexChanging="gvDetails_SelectedIndexChanging"
                OnRowCommand="gvDetails_RowCommand"
                OnRowEditing="gvDetails_RowEditing"
                OnRowDataBound="gvDetails_RowDataBound">
                <Columns>
                    <asp:CommandField ButtonType="Button" HeaderText="ลบ" DeleteText="🗑️ ลบ" ShowDeleteButton="True" />
                    <asp:CommandField ButtonType="Button" HeaderText="ดู PDF" SelectText="📄 ดู PDF" ShowSelectButton="True" />
                    <asp:TemplateField HeaderText="แก้ไข">
                        <ItemTemplate>
                            <asp:Button ID="btnEdit" runat="server"
                                Text="✏️ แก้ไข"
                                CommandName="edit"
                                CommandArgument='<%# Container.DataItemIndex %>'
                                CssClass="btn-edit"
                                CausesValidation="false" />
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="ดูสลิป">
                        <ItemTemplate>
                            <%# GetSlipLinkButton(Eval("SlipFileURL"), Eval("HasSlip")) %>
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:BoundField DataField="DisplayDoc" HeaderText="เลขที่เอกสาร" />
                    <asp:BoundField DataField="Reservation_ID" HeaderText="รหัสจอง" />
                    <asp:BoundField DataField="Created_Date" HeaderText="วันที่สร้าง" DataFormatString="{0:dd/MM/yyyy HH:mm}" />
                    <asp:BoundField DataField="CustomerName" HeaderText="ชื่อลูกค้า" />
                    <asp:BoundField DataField="Customer_MobilePhone" HeaderText="เบอร์โทร" />
                    <asp:BoundField DataField="Paid_Type" HeaderText="วิธีชำระ" />
                    <asp:BoundField DataField="Total_Amount" HeaderText="ยอดรวม" DataFormatString="{0:N2}" ItemStyle-CssClass="amount-cell" />
                    <asp:BoundField DataField="Vat" HeaderText="VAT" DataFormatString="{0:N2}" ItemStyle-CssClass="amount-cell" />
                    <asp:BoundField DataField="IsDeposit" HeaderText="มัดจำ" />
                    <asp:BoundField DataField="UseDeposit" HeaderText="ใช้มัดจำ" />
                    <asp:BoundField DataField="Status" HeaderText="สถานะ" />
                    <asp:BoundField DataField="Remark" HeaderText="หมายเหตุ" />
                    <asp:BoundField DataField="Created_By" HeaderText="ผู้สร้าง" />
                    <asp:TemplateField HeaderText="Sync บัญชี">
                        <ItemTemplate>
                            <asp:Label ID="lblSyncStatus" runat="server"></asp:Label>
                            <asp:Button ID="btnSync" runat="server" Text="📤 Sync" CommandName="sync"
                                CommandArgument='<%# Eval("ID") %>' CssClass="btn-sync-action"
                                OnClientClick="if(!confirm('ยืนยันส่งข้อมูลเข้าระบบบัญชี?'))return false; this.disabled=true; this.value='⏳ กำลัง Sync...';"
                                UseSubmitBehavior="false"
                                Visible="false" />
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="ส่งแก้ไขขึ้น NextAcc">
                        <ItemTemplate>
                            <asp:Button ID="btnFullTaxInvoice" runat="server" Text="🔁 ส่งแก้ไขขึ้น NextAcc"
                                CommandName="fulltaxinvoice"
                                CommandArgument='<%# Container.DataItemIndex %>'
                                CssClass="btn-sync-action" CausesValidation="false"
                                ToolTip="ส่งข้อมูลปัจจุบันของใบนี้ขึ้น NextAcc อีกครั้ง — ใช้หลังแก้ยอด/รายการ/ข้อมูลลูกค้า และใช้กับเคสลูกค้าขอใบกำกับภาษีย้อนหลัง (ถ้ากรอกเลขผู้เสียภาษี+ที่อยู่แล้วจะออกเป็นใบกำกับเต็มรูปให้เอง)"
                                OnClientClick="return confirm('ส่งข้อมูลปัจจุบันของใบนี้ขึ้น NextAcc อีกครั้ง?\n\n• ระบบจะพยายามแก้เอกสารเดิมในที่เดิมก่อน (เลขเอกสารคงเดิม)\n• ถ้า NextAcc ไม่ยอม จะยกเลิกใบเดิมแล้วออกใหม่\n• ถ้ากรอกเลขผู้เสียภาษี 13 หลัก + ที่อยู่ลูกค้าแล้ว จะได้ใบกำกับภาษีเต็มรูป');"
                                UseSubmitBehavior="false" />
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="อัพเดทไฟล์">
                        <ItemTemplate>
                            <asp:Button ID="btnRefreshPdf" runat="server" Text="🔄 ดึงล่าสุด"
                                CommandName="refreshpdf"
                                CommandArgument='<%# Container.DataItemIndex %>'
                                CssClass="btn-sync-action" CausesValidation="false"
                                ToolTip="ดึง PDF ล่าสุดจาก NextAcc (ข้าม cache)"
                                OnClientClick="this.disabled=true; this.value='⏳ กำลังดึง...'; "
                                UseSubmitBehavior="false" />
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="e-Tax">
                        <ItemTemplate>
                            <asp:Button ID="btnSendEtax" runat="server" Text="📧 ส่ง e-Tax"
                                CommandName="sendetax"
                                CommandArgument='<%# Container.DataItemIndex %>'
                                CssClass="btn-sync-action" CausesValidation="false"
                                ToolTip="ส่งใบกำกับภาษีอิเล็กทรอนิกส์ทางอีเมล (เปิดหน้าตรวจก่อนส่ง)"
                                Visible="false" />
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="ดู JE">
                        <ItemTemplate>
                            <button type="button" class="btn-sync-action"
                                title="ดูรายการบัญชี (JE) + เอกสารจาก NextAcc"
                                onclick='viewJE("<%# Eval("ID") %>")'>🧾 ดู JE</button>
                        </ItemTemplate>
                    </asp:TemplateField>
                </Columns>
            </asp:GridView>
        </div>
    </div>

    <!-- JE + เอกสาร Modal -->
    <style type="text/css">
        #jeModal { display:none; position:fixed; inset:0; background:rgba(0,0,0,.55); z-index:9999; overflow:auto; }
        #jeModal .je-box { background:#fff; max-width:900px; margin:32px auto; border-radius:10px; box-shadow:0 10px 40px rgba(0,0,0,.3); overflow:hidden; }
        #jeModal .je-head { background:#2c3e50; color:#fff; padding:14px 20px; display:flex; justify-content:space-between; align-items:center; }
        #jeModal .je-head h3 { margin:0; font-size:17px; }
        #jeModal .je-close { cursor:pointer; font-size:26px; line-height:1; color:#fff; background:none; border:none; }
        #jeModal .je-body { padding:18px 20px; max-height:72vh; overflow:auto; }
        #jeModal .je-doc { background:#f8f9fb; border:1px solid #e3e7ee; border-radius:8px; padding:12px 14px; margin-bottom:16px; }
        #jeModal .je-doc .je-doc-grid { display:grid; grid-template-columns:repeat(4,1fr); gap:8px 14px; font-size:13px; margin-top:8px; }
        #jeModal .je-doc .lbl { color:#888; font-size:11px; display:block; }
        #jeModal .je-je { border:1px solid #e3e7ee; border-radius:8px; margin-bottom:14px; }
        #jeModal .je-je-head { padding:9px 12px; background:#eef2f7; font-size:13px; display:flex; justify-content:space-between; align-items:center; flex-wrap:wrap; gap:6px; }
        #jeModal table.je-lines { width:100%; border-collapse:collapse; font-size:13px; }
        #jeModal table.je-lines th, #jeModal table.je-lines td { padding:6px 10px; border-top:1px solid #eee; text-align:left; }
        #jeModal table.je-lines th { background:#fafbfc; color:#666; font-weight:600; }
        #jeModal table.je-lines td.num, #jeModal table.je-lines th.num { text-align:right; font-variant-numeric:tabular-nums; }
        #jeModal table.je-lines tfoot td { font-weight:700; border-top:2px solid #ccc; background:#fafbfc; }
        #jeModal .badge { display:inline-block; padding:1px 8px; border-radius:10px; font-size:11px; font-weight:600; }
        #jeModal .badge.ok { background:#e7f7ec; color:#1c8b45; }
        #jeModal .badge.warn { background:#fdecea; color:#c0392b; }
        #jeModal .badge.rev { background:#f0e9fb; color:#7b4fc0; }
        #jeModal .badge.gray { background:#eceff3; color:#607080; }
        #jeModal .je-empty { text-align:center; color:#888; padding:26px; }
    </style>
    <div id="jeModal">
        <div class="je-box">
            <div class="je-head">
                <h3 id="jeTitle">รายการบัญชี (JE) + เอกสาร</h3>
                <button type="button" class="je-close" onclick="closeJE()">&times;</button>
            </div>
            <div class="je-body" id="jeBody"></div>
        </div>
    </div>

    <!-- Loading Overlay -->
    <div class="loading-overlay" id="loadingOverlay">
        <div class="loading-spinner">
            <h3>⏳ กำลังประมวลผล...</h3>
            <p>โปรดรอสักครู่</p>
        </div>
    </div>

    <script type="text/javascript">
        // Date picker initialization
        $(function () {
            $("#<%= txtStartDate.ClientID %>").datepicker({
                dateFormat: 'yy-mm-dd',
                changeMonth: true,
                changeYear: true
            });
            $("#<%= txtEndDate.ClientID %>").datepicker({
                dateFormat: 'yy-mm-dd',
                changeMonth: true,
                changeYear: true
            });
        });

        // Show loading on search
        function showLoading() {
            document.getElementById('loadingOverlay').style.display = 'flex';
        }

        // ── ดู JE + เอกสาร (ดึงจาก NextAcc) ──
        function esc(s) {
            if (s === null || s === undefined) return '';
            return String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
        }
        // ค่าที่ฝังใน onclick="fn('...')" — escape JS-string (\ ') ก่อน แล้ว HTML-attr
        function jsArg(s) {
            if (s === null || s === undefined) return '';
            return String(s).replace(/\\/g, '\\\\').replace(/'/g, "\\'")
                .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
        }
        function num(n) {
            var v = (typeof n === 'number') ? n : parseFloat(n || 0);
            if (isNaN(v)) v = 0;
            return v.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
        }
        function closeJE() { document.getElementById('jeModal').style.display = 'none'; }

        function viewJE(doc) {
            var modal = document.getElementById('jeModal');
            var body = document.getElementById('jeBody');
            document.getElementById('jeTitle').textContent = 'รายการบัญชี (JE) + เอกสาร — ' + doc;
            body.innerHTML = '<div class="je-empty">⏳ กำลังดึงข้อมูลจาก NextAcc...</div>';
            modal.style.display = 'block';

            var url = '<%= ResolveUrl("~/Account/CheckDocument_New") %>?action=viewJE&doc=' + encodeURIComponent(doc) + '&_=' + Date.now();
            var ctrl = new AbortController();
            var timer = setTimeout(function () { ctrl.abort(); }, 90000);
            fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' }, signal: ctrl.signal })
                .then(function (r) { clearTimeout(timer); return r.json(); })
                .then(function (d) { body.innerHTML = renderJE(d); })
                .catch(function (err) {
                    clearTimeout(timer);
                    var m = (err && err.name === 'AbortError') ? 'หมดเวลา — NextAcc ไม่ตอบกลับ ลองใหม่อีกครั้ง' : esc(err.message);
                    body.innerHTML = '<div class="je-empty">⚠ ดึงข้อมูลไม่สำเร็จ: ' + m +
                        '</div><div style="text-align:center; margin-top:10px;"><button type="button" class="btn-sync-action" onclick="viewJE(\'' + jsArg(doc) + '\')">🔄 ลองใหม่</button></div>';
                });
        }

        function renderJE(d) {
            if (!d || !d.Success) {
                return '<div class="je-empty">⚠ ' + esc(d ? d.Message : 'ไม่มีข้อมูล') + '</div>';
            }
            var html = '';

            // สรุปหัวเอกสาร
            if (d.HasDocument) {
                var arWarn = (d.BalanceDue && d.BalanceDue > 0.01);
                html += '<div class="je-doc"><strong>' + esc(d.DocumentType) + ' — ' + esc(d.DocumentNumber) + '</strong> ';
                html += '<span class="badge ' + (d.DocumentStatus === 'ยกเลิก' ? 'warn' : 'gray') + '">' + esc(d.DocumentStatus) + '</span>';
                html += '<div class="je-doc-grid">';
                html += '<div><span class="lbl">วันที่</span>' + esc(d.DocumentDate) + '</div>';
                html += '<div><span class="lbl">อ้างอิง</span>' + esc(d.DocumentReference || '-') + '</div>';
                html += '<div><span class="lbl">ยอดก่อน VAT</span>' + num(d.SubTotal) + '</div>';
                html += '<div><span class="lbl">VAT</span>' + num(d.VatAmount) + '</div>';
                html += '<div><span class="lbl">ยอดรวม</span>' + num(d.TotalAmount) + '</div>';
                html += '<div><span class="lbl">ชำระแล้ว</span>' + num(d.PaidAmount) + '</div>';
                html += '<div><span class="lbl">คงค้าง</span>' + (arWarn
                        ? '<span class="badge warn">' + num(d.BalanceDue) + ' (ลูกหนี้ยังเปิด)</span>'
                        : num(d.BalanceDue)) + '</div>';
                html += '<div><span class="lbl">รหัสจอง</span>' + esc(d.ReservationId || '-') + '</div>';
                html += '</div></div>';
            }

            // JE ทุกใบ
            if (!d.Journals || d.Journals.length === 0) {
                html += '<div class="je-empty">ไม่พบรายการบัญชี (JE) ที่ผูกกับใบนี้บน NextAcc' +
                    (d.SearchedRefs && d.SearchedRefs.length ? '<br/><small>ค้นจาก: ' + esc(d.SearchedRefs.join(', ')) + '</small>' : '') +
                    '</div>';
                return html;
            }

            d.Journals.forEach(function (j) {
                html += '<div class="je-je"><div class="je-je-head"><span><strong>' + esc(j.EntryNumber || 'JE') + '</strong> · ' + esc(j.Date) +
                        ' · อ้างอิง ' + esc(j.Reference || '-') + '</span><span>';
                html += '<span class="badge ' + (j.Status === 'ยกเลิก' ? 'warn' : 'gray') + '">' + esc(j.Status) + '</span> ';
                if (j.IsReversal) html += '<span class="badge rev">ตัวกลับรายการ</span> ';
                if (j.IsReversed) html += '<span class="badge rev">ถูกกลับแล้ว</span> ';
                html += '<span class="badge ' + (j.Balanced ? 'ok' : 'warn') + '">' + (j.Balanced ? 'สมดุล ✓' : 'ไม่สมดุล ✗') + '</span>';
                html += '</span></div>';
                if (j.Description) html += '<div style="padding:4px 12px; font-size:12px; color:#777;">' + esc(j.Description) + '</div>';
                html += '<table class="je-lines"><thead><tr><th>บัญชี</th><th>ชื่อบัญชี</th><th class="num">เดบิต</th><th class="num">เครดิต</th></tr></thead><tbody>';
                (j.Lines || []).forEach(function (l) {
                    html += '<tr><td>' + esc(l.AccountCode) + '</td><td>' + esc(l.AccountName) +
                            (l.Description ? ' <small style="color:#999;">' + esc(l.Description) + '</small>' : '') +
                            '</td><td class="num">' + (l.Debit > 0 ? num(l.Debit) : '') +
                            '</td><td class="num">' + (l.Credit > 0 ? num(l.Credit) : '') + '</td></tr>';
                });
                html += '</tbody><tfoot><tr><td colspan="2">รวม</td><td class="num">' + num(j.TotalDebit) +
                        '</td><td class="num">' + num(j.TotalCredit) + '</td></tr></tfoot></table></div>';
            });
            return html;
        }

        // ปิด modal เมื่อคลิกพื้นหลัง / กด Esc
        document.addEventListener('click', function (e) {
            if (e.target && e.target.id === 'jeModal') closeJE();
        });
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') closeJE();
        });
    </script>
</asp:Content>
