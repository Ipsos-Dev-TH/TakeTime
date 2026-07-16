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
                DataKeyNames="ID,Status,IsNextAccOnly,NextAccId,NextAccViewUrl"
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
                    <asp:BoundField DataField="ID" HeaderText="เลขที่เอกสาร" />
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
                </Columns>
            </asp:GridView>
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
    </script>
</asp:Content>
