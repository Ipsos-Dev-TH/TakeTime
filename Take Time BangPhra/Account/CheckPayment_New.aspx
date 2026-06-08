<%@ Page MaintainScrollPositionOnPostback="true" Title="ตรวจสอบใบสำคัญจ่าย" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="CheckPayment_New.aspx.cs" Inherits="Take_Time_BangPhra.Account.CheckPayment_New" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <link rel="stylesheet" href="/Content/jquery-ui.css">
    <style>
        .payment-dashboard {
            max-width: 98%;
            margin: 10px auto;
            padding: 5px;
        }

        .dashboard-header {
            background: linear-gradient(135deg, #e74c3c 0%, #c0392b 100%);
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
            background: #e74c3c;
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
            background: #c0392b;
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
            border-bottom: 2px solid #e74c3c;
        }

        .expense-table {
            width: 100%;
            border-collapse: collapse;
            margin-bottom: 15px;
            font-size: 13px;
        }

        .expense-table th {
            background: #34495e;
            color: white;
            padding: 10px;
            text-align: center;
            font-weight: 600;
            border: 1px solid #2c3e50;
        }

        .expense-table td {
            padding: 8px 10px;
            border: 1px solid #ddd;
            text-align: right;
        }

        .expense-table td.category {
            text-align: left;
            font-weight: 600;
            background: #ecf0f1;
        }

        .expense-table tr:hover {
            background: #f8f9fa;
        }

        .expense-table .total-row {
            background: #e74c3c;
            color: white;
            font-weight: 700;
            font-size: 14px;
        }

        .expense-table .total-row td {
            border-color: #c0392b;
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

        .btn-sync-action {
            background: linear-gradient(135deg, #3498db 0%, #2980b9 100%);
            color: white;
            padding: 5px 12px;
            border: none;
            border-radius: 5px;
            font-size: 12px;
            cursor: pointer;
            white-space: nowrap;
        }

        .btn-sync-action:hover {
            background: linear-gradient(135deg, #2980b9 0%, #2471a3 100%);
        }

        .sync-badge {
            display: inline-block;
            padding: 3px 10px;
            border-radius: 12px;
            font-size: 11px;
            font-weight: 600;
            white-space: nowrap;
        }

        .sync-badge.completed { background: #d4edda; color: #155724; }
        .sync-badge.pending { background: #fff3cd; color: #856404; }
        .sync-badge.processing { background: #cce5ff; color: #004085; }
        .sync-badge.failed { background: #f8d7da; color: #721c24; }
        .sync-badge.none { background: #e2e3e5; color: #383d41; }
        a.sync-badge.completed { text-decoration: none; cursor: pointer; }
        a.sync-badge.completed:hover { background: #b7dfb5; text-decoration: underline; }

        .att-wrap { display: flex; flex-wrap: wrap; gap: 4px; max-width: 260px; }
        .att-thumb { width: 48px; height: 48px; object-fit: cover; border: 1px solid #ccc; border-radius: 4px; cursor: pointer; transition: transform .15s; }
        .att-thumb:hover { transform: scale(1.6); z-index: 10; box-shadow: 0 2px 8px rgba(0,0,0,.3); }
        .att-link { display: inline-block; padding: 3px 8px; border-radius: 4px; font-size: 11px; text-decoration: none; background: #ecf0f1; color: #2c3e50; white-space: nowrap; }
        .att-link:hover { background: #d5dbdb; }
        .att-src-label { font-size: 10px; color: #888; display: block; margin-bottom: 2px; }

        /* Mobile responsive */
        @media (max-width: 768px) {
            .payment-dashboard {
                padding: 0;
                max-width: 100%;
            }
            .dashboard-header h2 {
                font-size: 18px;
            }
            .search-row {
                flex-direction: column;
                gap: 8px;
            }
            .search-label {
                min-width: unset;
            }
            .search-input {
                width: 100% !important;
            }
            .btn-search, .btn-export {
                width: 100%;
                margin: 5px 0;
            }
            .expense-table {
                font-size: 11px;
                display: block;
                overflow-x: auto;
                -webkit-overflow-scrolling: touch;
            }
            .summary-section > div[style*="display: flex"] {
                flex-direction: column !important;
                gap: 10px !important;
            }
            .gridview-custom {
                display: block;
                overflow-x: auto;
                -webkit-overflow-scrolling: touch;
                font-size: 12px;
            }
            .gridview-custom th, .gridview-custom td {
                white-space: nowrap;
                padding: 6px 4px;
            }
        }
    </style>

    <div class="payment-dashboard">
        <!-- Header -->
        <div class="dashboard-header">
            <h2>💸 ระบบตรวจสอบใบสำคัญจ่าย</h2>
        </div>

        <!-- Search Section -->
        <div class="search-section">
            <div class="search-row">
                <span class="search-label">ระหว่างวันที่:</span>
                <asp:TextBox ID="txtStartDate" runat="server" CssClass="search-input"
                    placeholder="yyyy-mm-dd" Width="150px"></asp:TextBox>
                <span>ถึง</span>
                <asp:TextBox ID="txtEndDate" runat="server" CssClass="search-input"
                    placeholder="yyyy-mm-dd" Width="150px"></asp:TextBox>
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
                <span class="search-label">ค้นหาผู้รับเงิน:</span>
                <asp:TextBox ID="txtVendorSearch" runat="server" CssClass="search-input"
                    placeholder="พิมพ์ชื่อ Vendor / ผู้รับเงิน" Width="300px"></asp:TextBox>
                <span style="color: #7f8c8d; font-size: 13px;">(ค้นหาบางส่วนของชื่อได้)</span>
            </div>

            <div class="search-row">
                <span class="search-label">ประเภทค่าใช้จ่าย:</span>
                <asp:DropDownList ID="ddlExpenseType" runat="server" CssClass="search-input" Width="250px">
                    <asp:ListItem Value="">-- ทั้งหมด --</asp:ListItem>
                </asp:DropDownList>
                <span class="search-label" style="margin-left: 20px;">ยอดรวมมากกว่า:</span>
                <asp:TextBox ID="txtMinAmount" runat="server" CssClass="search-input"
                    placeholder="0.00" Width="150px" TextMode="Number" step="0.01"></asp:TextBox>
                <span style="color: #7f8c8d; font-size: 13px;">บาท</span>
            </div>

            <div class="search-row">
                <span style="color: #7f8c8d; font-size: 14px;">
                    <i class="fa fa-info-circle"></i>
                    <strong>หมายเหตุ:</strong> ยอดรวมคำนวณเฉพาะเอกสารปกติ (ไม่รวมที่ยกเลิก) / ตารางแสดงทั้งหมดรวมเอกสารที่ยกเลิก
                </span>
            </div>

            <div class="search-row">
                <asp:Button ID="btnSearch" runat="server" Text="🔍 ค้นหา" CssClass="btn-search" OnClick="btnSearch_Click" OnClientClick="showLoading();" />
                <asp:Button ID="btnExport" runat="server" Text="📄 Export CSV" CssClass="btn-export" OnClick="btnExport_Click" />
            </div>
        </div>

        <!-- Summary Section -->
        <div class="summary-section">
            <div class="summary-title">📊 สรุปค่าใช้จ่ายตามวิธีชำระ</div>

            <table class="expense-table">
                <thead>
                    <tr>
                        <th style="width: 35%">วิธีชำระเงิน</th>
                        <th style="width: 20%">ยอดรวม (บาท)</th>
                        <th style="width: 15%">จำนวนรายการ</th>
                        <th style="width: 30%">หมายเหตุ</th>
                    </tr>
                </thead>
                <tbody>
                    <tr>
                        <td class="category">💵 เงินสด</td>
                        <td class="amount-cell"><asp:Label ID="lblCashTotal" runat="server" Text="0.00"></asp:Label></td>
                        <td class="amount-cell"><asp:Label ID="lblCashCount" runat="server" Text="0"></asp:Label></td>
                        <td>จ่ายเป็นเงินสด</td>
                    </tr>
                    <tr>
                        <td class="category">🏦 โอนกสิกร (KBANK)</td>
                        <td class="amount-cell"><asp:Label ID="lblKBANKTotal" runat="server" Text="0.00"></asp:Label></td>
                        <td class="amount-cell"><asp:Label ID="lblKBANKCount" runat="server" Text="0"></asp:Label></td>
                        <td>โอนผ่านธนาคารกสิกรไทย</td>
                    </tr>
                    <tr>
                        <td class="category">🏦 โอนกรุงไทย (KTB)</td>
                        <td class="amount-cell"><asp:Label ID="lblKTBTotal" runat="server" Text="0.00"></asp:Label></td>
                        <td class="amount-cell"><asp:Label ID="lblKTBCount" runat="server" Text="0"></asp:Label></td>
                        <td>โอนผ่านธนาคารกรุงไทย</td>
                    </tr>
                    <tr>
                        <td class="category">👔 เงินกรรมการ</td>
                        <td class="amount-cell"><asp:Label ID="lblDirectorTotal" runat="server" Text="0.00"></asp:Label></td>
                        <td class="amount-cell"><asp:Label ID="lblDirectorCount" runat="server" Text="0"></asp:Label></td>
                        <td>จ่ายโดยกรรมการ</td>
                    </tr>
                    <tr class="total-row">
                        <td>💰 รวมทั้งหมด</td>
                        <td class="amount-cell"><asp:Label ID="lblGrandTotal" runat="server" Text="0.00"></asp:Label></td>
                        <td class="amount-cell"><asp:Label ID="lblTotalCount" runat="server" Text="0"></asp:Label></td>
                        <td>ยอดค่าใช้จ่ายทั้งหมด</td>
                    </tr>
                </tbody>
            </table>

            <!-- Additional Info -->
            <div style="margin-top: 20px; padding: 15px; background: #f8f9fa; border-radius: 5px;">
                <div style="display: flex; gap: 40px;">
                    <div>
                        <strong>จำนวนเอกสารทั้งหมด:</strong>
                        <asp:Label ID="lblDocCount" runat="server" Text="0" style="margin-left: 10px; font-size: 18px; color: #e74c3c; font-weight: 700;"></asp:Label>
                        <span> เอกสาร</span>
                    </div>
                    <div>
                        <strong>ยอดรวมภาษี (VAT):</strong>
                        <asp:Label ID="lblTotalVAT" runat="server" Text="0.00" style="margin-left: 10px; font-size: 18px; color: #e67e22; font-weight: 700;"></asp:Label>
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
            <div class="detail-title">📋 รายละเอียดใบสำคัญจ่าย</div>
            <div style="margin-bottom: 10px;">
                <asp:CheckBox ID="chkEnableDelete" runat="server" Text="เปิดใช้งานปุ่มลบ (Delete)" />
            </div>
            <asp:GridView ID="gvDetails" runat="server" CssClass="gridview-custom"
                AutoGenerateColumns="False" EmptyDataText="ไม่พบข้อมูล"
                DataKeyNames="ID,Status,IsNextAccOnly,NextAccViewUrl"
                OnRowDeleting="gvDetails_RowDeleting"
                OnSelectedIndexChanging="gvDetails_SelectedIndexChanging"
                OnRowCommand="gvDetails_RowCommand"
                OnRowDataBound="gvDetails_RowDataBound">
                <Columns>
                    <asp:CommandField ButtonType="Button" HeaderText="ลบ" DeleteText="🗑️ ลบ" ShowDeleteButton="True" />
                    <asp:CommandField ButtonType="Button" HeaderText="ดู PDF" SelectText="📄 ดู PDF" ShowSelectButton="True" />
                    <asp:ButtonField ButtonType="Button" CommandName="edit" Text="✏️ แก้ไข" HeaderText="แก้ไข" />
                    <asp:BoundField DataField="DisplayDoc" HeaderText="เลขที่เอกสาร" />
                    <asp:BoundField DataField="Created_Date" HeaderText="วันที่สร้าง" DataFormatString="{0:dd/MM/yyyy HH:mm}" />
                    <asp:BoundField DataField="Vendor_Name" HeaderText="ผู้รับเงิน/ผู้ขาย" />
                    <asp:BoundField DataField="Paid_How" HeaderText="วิธีชำระ" />
                    <asp:BoundField DataField="Paid_Type" HeaderText="ประเภทค่าใช้จ่าย" />
                    <asp:BoundField DataField="Total_Amount" HeaderText="ยอดรวม" DataFormatString="{0:N2}" ItemStyle-CssClass="amount-cell" />
                    <asp:BoundField DataField="Vat" HeaderText="VAT" DataFormatString="{0:N2}" ItemStyle-CssClass="amount-cell" />
                    <asp:BoundField DataField="Status" HeaderText="สถานะ" />
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
                    <asp:TemplateField HeaderText="เอกสาร NextAcc">
                        <ItemTemplate>
                            <asp:Literal ID="litNextAccDoc" runat="server"></asp:Literal>
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="ไฟล์แนบ">
                        <ItemTemplate>
                            <asp:Literal ID="litAttachments" runat="server"></asp:Literal>
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
            // ซ่อน overlay เมื่อ page โหลดเสร็จ (postback กลับมาแล้ว)
            hideLoading();
        });

        function showLoading() {
            document.getElementById('loadingOverlay').style.display = 'flex';
        }
        function hideLoading() {
            document.getElementById('loadingOverlay').style.display = 'none';
        }
    </script>
</asp:Content>
