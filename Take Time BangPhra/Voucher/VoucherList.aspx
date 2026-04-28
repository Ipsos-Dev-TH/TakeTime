<%@ Page Title="รายการ Voucher ทั้งหมด" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="VoucherList.aspx.cs" Inherits="Take_Time_BangPhra.Voucher.VoucherList" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/5.15.4/css/all.min.css" />
    <style>
        :root {
            --voucher-primary: #4CAF50;
            --voucher-primary-dark: #388E3C;
            --voucher-secondary: #8D6E63;
            --voucher-success: #27ae60;
            --voucher-warning: #f39c12;
            --voucher-danger: #e74c3c;
            --voucher-info: #3498db;
        }

        .voucher-list-container {
            max-width: 1400px;
            margin: 0 auto;
            padding: 20px;
        }

        /* Header Section */
        .page-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 25px;
            flex-wrap: wrap;
            gap: 15px;
        }

        .page-title {
            display: flex;
            align-items: center;
            gap: 12px;
        }

        .page-title h1 {
            margin: 0;
            font-size: 1.8em;
            color: #333;
        }

        .page-title .subtitle {
            color: #666;
            font-size: 0.9em;
        }

        .header-actions {
            display: flex;
            gap: 10px;
        }

        .btn {
            padding: 10px 20px;
            border: none;
            border-radius: 8px;
            cursor: pointer;
            font-weight: 500;
            display: inline-flex;
            align-items: center;
            gap: 8px;
            transition: all 0.3s;
            text-decoration: none;
        }

        .btn-primary {
            background: linear-gradient(135deg, var(--voucher-primary), var(--voucher-primary-dark));
            color: white;
        }

        .btn-primary:hover {
            transform: translateY(-2px);
            box-shadow: 0 4px 12px rgba(76, 175, 80, 0.4);
        }

        .btn-secondary {
            background: #f5f5f5;
            color: #333;
            border: 1px solid #ddd;
        }

        .btn-export {
            background: linear-gradient(135deg, #2196F3, #1976D2);
            color: white;
        }

        /* Stats Cards */
        .stats-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
            gap: 15px;
            margin-bottom: 25px;
        }

        .stat-card {
            background: white;
            border-radius: 12px;
            padding: 20px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.08);
            text-align: center;
            cursor: pointer;
            transition: all 0.3s;
            border: 2px solid transparent;
            text-decoration: none;
            color: inherit;
        }

        .stat-card:hover {
            transform: translateY(-3px);
            box-shadow: 0 6px 20px rgba(0,0,0,0.12);
        }

        .stat-card.active {
            border-color: var(--voucher-primary);
            background: linear-gradient(135deg, #E8F5E9, #C8E6C9);
        }

        .stat-number {
            font-size: 2em;
            font-weight: 700;
            margin-bottom: 5px;
        }

        .stat-label {
            font-size: 0.85em;
            color: #666;
        }

        .stat-card.all .stat-number { color: var(--voucher-primary); }
        .stat-card.active-voucher .stat-number { color: var(--voucher-info); }
        .stat-card.used .stat-number { color: var(--voucher-secondary); }
        .stat-card.expired .stat-number { color: var(--voucher-warning); }
        .stat-card.cancelled .stat-number { color: var(--voucher-danger); }

        /* Filter Section */
        .filter-section {
            background: white;
            border-radius: 12px;
            padding: 20px;
            margin-bottom: 20px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.08);
        }

        .filter-row {
            display: flex;
            flex-wrap: wrap;
            gap: 15px;
            align-items: flex-end;
        }

        .filter-group {
            display: flex;
            flex-direction: column;
            gap: 5px;
        }

        .filter-group label {
            font-size: 0.85em;
            color: #666;
            font-weight: 500;
        }

        .filter-group input,
        .filter-group select {
            padding: 10px 15px;
            border: 1px solid #ddd;
            border-radius: 8px;
            font-size: 14px;
            min-width: 180px;
        }

        .filter-group input:focus,
        .filter-group select:focus {
            border-color: var(--voucher-primary);
            outline: none;
            box-shadow: 0 0 0 3px rgba(76, 175, 80, 0.1);
        }

        .filter-buttons {
            display: flex;
            gap: 10px;
        }

        .btn-search {
            background: var(--voucher-primary);
            color: white;
            padding: 10px 25px;
        }

        .btn-clear {
            background: #f5f5f5;
            color: #666;
            padding: 10px 20px;
        }

        /* Results Section */
        .results-section {
            background: white;
            border-radius: 12px;
            padding: 20px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.08);
        }

        .results-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 15px;
            flex-wrap: wrap;
            gap: 10px;
        }

        .results-count {
            font-weight: 600;
            color: var(--voucher-primary-dark);
        }

        .results-summary {
            display: flex;
            gap: 20px;
            font-size: 0.9em;
            flex-wrap: wrap;
        }

        .summary-item {
            color: #666;
        }

        .summary-item strong {
            color: var(--voucher-primary-dark);
        }

        /* Table */
        .voucher-table {
            width: 100%;
            border-collapse: collapse;
            min-width: 900px;
        }

        .voucher-table th {
            background: var(--voucher-primary);
            color: white;
            padding: 15px 12px;
            text-align: left;
            font-weight: 500;
            font-size: 0.9em;
            white-space: nowrap;
        }

        .voucher-table td {
            padding: 12px;
            border-bottom: 1px solid #eee;
            font-size: 0.9em;
            vertical-align: middle;
        }

        .voucher-table tr:hover {
            background: #f8f9fa;
        }

        /* Status Badges */
        .status-badge {
            display: inline-block;
            padding: 5px 12px;
            border-radius: 20px;
            font-size: 0.8em;
            font-weight: 600;
            white-space: nowrap;
        }

        .status-active {
            background: #E3F2FD;
            color: #1565C0;
        }

        .status-used {
            background: #F3E5F5;
            color: #7B1FA2;
        }

        .status-expired {
            background: #FFF3E0;
            color: #E65100;
        }

        .status-cancelled {
            background: #FFEBEE;
            color: #C62828;
        }

        /* Voucher Number */
        .voucher-number {
            font-family: 'Courier New', monospace;
            font-weight: 600;
            color: var(--voucher-primary-dark);
        }

        /* Customer Info */
        .customer-name {
            font-weight: 600;
            color: #333;
        }

        .customer-phone {
            color: #666;
            font-size: 0.85em;
            font-family: monospace;
        }

        /* Price Display */
        .price {
            font-weight: 700;
            color: var(--voucher-primary-dark);
        }

        /* Action Buttons */
        .action-buttons {
            display: flex;
            gap: 8px;
        }

        .btn-action {
            width: 36px;
            height: 36px;
            border-radius: 8px;
            display: flex;
            align-items: center;
            justify-content: center;
            border: none;
            cursor: pointer;
            transition: all 0.2s;
            text-decoration: none;
        }

        .btn-view {
            background: #E3F2FD;
            color: #1565C0;
        }

        .btn-view:hover {
            background: #1565C0;
            color: white;
        }

        /* Pagination */
        .pagination-section {
            display: flex;
            justify-content: center;
            align-items: center;
            gap: 15px;
            margin-top: 20px;
            padding-top: 20px;
            border-top: 1px solid #eee;
        }

        .pagination-info {
            color: #666;
        }

        /* Empty State */
        .empty-state {
            text-align: center;
            padding: 60px 20px;
            color: #999;
        }

        .empty-state i {
            font-size: 4em;
            margin-bottom: 20px;
            color: #ddd;
        }

        .empty-state h3 {
            margin: 0 0 10px 0;
            color: #666;
        }

        /* Expired indicator */
        .expired-soon {
            color: var(--voucher-warning);
            font-size: 0.8em;
        }

        .expired-text {
            color: var(--voucher-danger);
            font-size: 0.8em;
        }

        /* Mobile Responsive */
        @media (max-width: 768px) {
            .voucher-list-container {
                padding: 10px;
            }

            .page-header {
                flex-direction: column;
                align-items: flex-start;
            }

            .page-title h1 {
                font-size: 1.4em;
            }

            .stats-grid {
                grid-template-columns: repeat(3, 1fr);
            }

            .stat-card {
                padding: 15px 10px;
            }

            .stat-number {
                font-size: 1.5em;
            }

            .stat-label {
                font-size: 0.75em;
            }

            .filter-row {
                flex-direction: column;
            }

            .filter-group {
                width: 100%;
            }

            .filter-group input,
            .filter-group select {
                width: 100%;
                min-width: unset;
            }

            .filter-buttons {
                width: 100%;
            }

            .filter-buttons .btn {
                flex: 1;
            }

            .header-actions {
                width: 100%;
            }

            .header-actions .btn {
                flex: 1;
                justify-content: center;
            }

            .results-summary {
                flex-direction: column;
                gap: 5px;
            }
        }

        @media (max-width: 480px) {
            .stats-grid {
                grid-template-columns: repeat(2, 1fr);
            }
        }
    </style>

    <div class="voucher-list-container">
        <!-- Header -->
        <div class="page-header">
            <div class="page-title">
                <div>
                    <h1><i class="fas fa-ticket-alt" style="color: #4CAF50;"></i> รายการ Voucher ทั้งหมด</h1>
                    <div class="subtitle">ตรวจสอบและจัดการ Voucher ทั้งหมดในระบบ</div>
                </div>
            </div>
            <div class="header-actions">
                <a href="Default.aspx" class="btn btn-primary">
                    <i class="fas fa-plus"></i> ขาย Voucher ใหม่
                </a>
                <a href="MatchTaxInvoice.aspx" class="btn btn-primary" style="background:#16a085;">
                    <i class="fas fa-clipboard-check"></i> ตรวจใบกำกับภาษี
                </a>
                <asp:Button ID="btnExport" runat="server" Text="Export CSV" CssClass="btn btn-export" OnClick="btnExport_Click" />
            </div>
        </div>

        <!-- Stats Cards -->
        <div class="stats-grid">
            <asp:LinkButton ID="lnkStatAll" runat="server" CssClass="stat-card all" CommandArgument="all" OnClick="StatCard_Click">
                <div class="stat-number"><asp:Label ID="lblStatAll" runat="server" Text="0"></asp:Label></div>
                <div class="stat-label">ทั้งหมด</div>
            </asp:LinkButton>

            <asp:LinkButton ID="lnkStatActive" runat="server" CssClass="stat-card active-voucher" CommandArgument="active" OnClick="StatCard_Click">
                <div class="stat-number"><asp:Label ID="lblStatActive" runat="server" Text="0"></asp:Label></div>
                <div class="stat-label">ใช้งานได้</div>
            </asp:LinkButton>

            <asp:LinkButton ID="lnkStatUsed" runat="server" CssClass="stat-card used" CommandArgument="used" OnClick="StatCard_Click">
                <div class="stat-number"><asp:Label ID="lblStatUsed" runat="server" Text="0"></asp:Label></div>
                <div class="stat-label">ใช้แล้ว</div>
            </asp:LinkButton>

            <asp:LinkButton ID="lnkStatExpired" runat="server" CssClass="stat-card expired" CommandArgument="expired" OnClick="StatCard_Click">
                <div class="stat-number"><asp:Label ID="lblStatExpired" runat="server" Text="0"></asp:Label></div>
                <div class="stat-label">หมดอายุ</div>
            </asp:LinkButton>

            <asp:LinkButton ID="lnkStatCancelled" runat="server" CssClass="stat-card cancelled" CommandArgument="cancelled" OnClick="StatCard_Click">
                <div class="stat-number"><asp:Label ID="lblStatCancelled" runat="server" Text="0"></asp:Label></div>
                <div class="stat-label">ยกเลิก</div>
            </asp:LinkButton>
        </div>

        <!-- Filter Section -->
        <div class="filter-section">
            <div class="filter-row">
                <div class="filter-group">
                    <label><i class="fas fa-search"></i> ค้นหา</label>
                    <asp:TextBox ID="txtSearch" runat="server" placeholder="เลข Voucher, ชื่อ, เบอร์โทร..."></asp:TextBox>
                </div>

                <div class="filter-group">
                    <label><i class="fas fa-filter"></i> สถานะ</label>
                    <asp:DropDownList ID="ddlStatus" runat="server">
                        <asp:ListItem Value="" Text="-- ทั้งหมด --"></asp:ListItem>
                        <asp:ListItem Value="active" Text="ใช้งานได้"></asp:ListItem>
                        <asp:ListItem Value="used" Text="ใช้แล้ว"></asp:ListItem>
                        <asp:ListItem Value="expired" Text="หมดอายุ"></asp:ListItem>
                        <asp:ListItem Value="cancelled" Text="ยกเลิก"></asp:ListItem>
                    </asp:DropDownList>
                </div>

                <div class="filter-group">
                    <label><i class="fas fa-calendar"></i> จากวันที่</label>
                    <asp:TextBox ID="txtDateFrom" runat="server" TextMode="Date"></asp:TextBox>
                </div>

                <div class="filter-group">
                    <label><i class="fas fa-calendar"></i> ถึงวันที่</label>
                    <asp:TextBox ID="txtDateTo" runat="server" TextMode="Date"></asp:TextBox>
                </div>

                <div class="filter-buttons">
                    <asp:Button ID="btnSearch" runat="server" Text="ค้นหา" CssClass="btn btn-search" OnClick="btnSearch_Click" />
                    <asp:Button ID="btnClear" runat="server" Text="ล้าง" CssClass="btn btn-clear" OnClick="btnClear_Click" />
                </div>
            </div>
        </div>

        <!-- Results Section -->
        <div class="results-section">
            <div class="results-header">
                <span class="results-count">
                    <i class="fas fa-ticket-alt"></i> พบ <asp:Label ID="lblResultCount" runat="server" Text="0"></asp:Label> รายการ
                </span>
                <div class="results-summary">
                    <span class="summary-item">ยอดขายรวม: <strong><asp:Label ID="lblTotalSales" runat="server" Text="0"></asp:Label></strong> บาท</span>
                </div>
            </div>

            <div class="mobile-table-wrapper">
                <asp:GridView ID="gvVouchers" runat="server" AutoGenerateColumns="false"
                    CssClass="voucher-table" EmptyDataText=""
                    OnRowDataBound="gvVouchers_RowDataBound"
                    DataKeyNames="Voucher_Number">
                    <Columns>
                        <asp:TemplateField HeaderText="เลข Voucher">
                            <ItemTemplate>
                                <span class="voucher-number"><%# Eval("Voucher_Number") %></span>
                            </ItemTemplate>
                        </asp:TemplateField>

                        <asp:TemplateField HeaderText="วันที่ขาย">
                            <ItemTemplate>
                                <%# Convert.ToDateTime(Eval("Created_Date")).ToString("dd/MM/yyyy") %>
                                <div style="font-size: 0.85em; color: #888;"><%# Convert.ToDateTime(Eval("Created_Date")).ToString("HH:mm") %></div>
                            </ItemTemplate>
                        </asp:TemplateField>

                        <asp:TemplateField HeaderText="ลูกค้า">
                            <ItemTemplate>
                                <div class="customer-name"><%# Eval("CustomerName") %></div>
                                <div class="customer-phone"><%# Eval("CustomerPhone") %></div>
                            </ItemTemplate>
                        </asp:TemplateField>

                        <asp:TemplateField HeaderText="ประเภท">
                            <ItemTemplate>
                                <asp:Literal ID="litRatePlan" runat="server"></asp:Literal>
                            </ItemTemplate>
                        </asp:TemplateField>

                        <asp:TemplateField HeaderText="ราคา">
                            <ItemTemplate>
                                <span class="price"><%# String.Format("{0:N0}", Eval("Sell_Price")) %></span> บาท
                            </ItemTemplate>
                        </asp:TemplateField>

                        <asp:TemplateField HeaderText="วันหมดอายุ">
                            <ItemTemplate>
                                <%# FormatExpiryDate(Eval("Expired_Date")) %>
                            </ItemTemplate>
                        </asp:TemplateField>

                        <asp:TemplateField HeaderText="สถานะ">
                            <ItemTemplate>
                                <asp:Label ID="lblStatus" runat="server" CssClass="status-badge"></asp:Label>
                            </ItemTemplate>
                        </asp:TemplateField>

                        <asp:TemplateField HeaderText="ใช้กับการจอง">
                            <ItemTemplate>
                                <asp:Literal ID="litUsedReservation" runat="server"></asp:Literal>
                            </ItemTemplate>
                        </asp:TemplateField>

                        <asp:TemplateField HeaderText="">
                            <ItemTemplate>
                                <div class="action-buttons">
                                    <a href='<%# "Voucher_Confirmed.aspx?id=" + Server.UrlEncode(Convert.ToDateTime(Eval("Created_Date")).ToString("yyyy-MM-dd HH:mm:ss")) %>'
                                       class="btn-action btn-view" target="_blank" title="ดูรายละเอียด">
                                        <i class="fas fa-eye"></i>
                                    </a>
                                </div>
                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                    <EmptyDataTemplate>
                        <div class="empty-state">
                            <i class="fas fa-ticket-alt"></i>
                            <h3>ไม่พบรายการ Voucher</h3>
                            <p>ลองเปลี่ยนเงื่อนไขการค้นหาหรือล้างตัวกรอง</p>
                        </div>
                    </EmptyDataTemplate>
                </asp:GridView>
            </div>

            <!-- Pagination -->
            <div class="pagination-section">
                <asp:Button ID="btnPrevPage" runat="server" Text="ก่อนหน้า" CssClass="btn btn-clear" OnClick="btnPrevPage_Click" />
                <span class="pagination-info">
                    หน้า <asp:Label ID="lblCurrentPage" runat="server" Text="1"></asp:Label> / <asp:Label ID="lblTotalPages" runat="server" Text="1"></asp:Label>
                </span>
                <asp:Button ID="btnNextPage" runat="server" Text="ถัดไป" CssClass="btn btn-clear" OnClick="btnNextPage_Click" />
            </div>
        </div>
    </div>
</asp:Content>
