<%@ Page Title="รายการจองทั้งหมด" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ReservationList.aspx.cs" Inherits="Take_Time_BangPhra.ReservationList" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .reservation-list-container {
            max-width: 1600px;
            margin: 0 auto;
            padding: 20px;
        }

        .page-header {
            background: linear-gradient(135deg, #5D4037 0%, #8D6E63 100%);
            color: white;
            padding: 25px 30px;
            border-radius: 15px;
            margin-bottom: 25px;
            box-shadow: 0 4px 15px rgba(93, 64, 55, 0.3);
        }

        .page-header h1 {
            margin: 0 0 5px 0;
            font-size: 1.8em;
            font-weight: 600;
        }

        .page-header p {
            margin: 0;
            opacity: 0.9;
            font-size: 0.95em;
        }

        /* Stats Cards */
        .stats-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
            gap: 15px;
            margin-bottom: 25px;
        }

        .stat-card {
            background: white;
            border-radius: 12px;
            padding: 20px;
            text-align: center;
            box-shadow: 0 2px 10px rgba(0,0,0,0.08);
            transition: all 0.3s ease;
            cursor: pointer;
            border-left: 4px solid transparent;
        }

        .stat-card:hover {
            transform: translateY(-3px);
            box-shadow: 0 5px 20px rgba(0,0,0,0.12);
        }

        .stat-card.active {
            background: linear-gradient(135deg, #5D4037 0%, #8D6E63 100%);
            color: white;
        }

        .stat-card.active .stat-label {
            color: rgba(255,255,255,0.9);
        }

        .stat-card .stat-icon {
            font-size: 1.8em;
            margin-bottom: 8px;
            opacity: 0.8;
        }

        .stat-card .stat-value {
            font-size: 2em;
            font-weight: 700;
            margin-bottom: 5px;
        }

        .stat-card .stat-label {
            font-size: 0.85em;
            color: #666;
        }

        .stat-all { border-left-color: #5D4037; }
        .stat-deposit { border-left-color: #FF9800; }
        .stat-checkin { border-left-color: #4CAF50; }
        .stat-checkout { border-left-color: #2196F3; }
        .stat-complete { border-left-color: #9C27B0; }
        .stat-cancel { border-left-color: #F44336; }
        .stat-today { border-left-color: #E91E63; }

        /* Filter Section */
        .filter-section {
            background: white;
            border-radius: 12px;
            padding: 20px;
            margin-bottom: 20px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.08);
        }

        .filter-row {
            display: flex;
            flex-wrap: wrap;
            gap: 15px;
            align-items: flex-end;
        }

        .filter-group {
            flex: 1;
            min-width: 180px;
        }

        .filter-group label {
            display: block;
            margin-bottom: 5px;
            font-weight: 500;
            color: #5D4037;
            font-size: 0.9em;
        }

        .filter-group input,
        .filter-group select {
            width: 100%;
            padding: 10px 15px;
            border: 1px solid #ddd;
            border-radius: 8px;
            font-size: 0.95em;
            transition: border-color 0.3s;
        }

        .filter-group input:focus,
        .filter-group select:focus {
            border-color: #5D4037;
            outline: none;
            box-shadow: 0 0 0 3px rgba(93, 64, 55, 0.1);
        }

        .filter-buttons {
            display: flex;
            gap: 10px;
        }

        .btn-search {
            background: linear-gradient(135deg, #5D4037 0%, #8D6E63 100%);
            color: white;
            border: none;
            padding: 10px 25px;
            border-radius: 8px;
            cursor: pointer;
            font-weight: 500;
            transition: all 0.3s;
        }

        .btn-search:hover {
            transform: translateY(-2px);
            box-shadow: 0 4px 15px rgba(93, 64, 55, 0.3);
        }

        .btn-clear {
            background: #f5f5f5;
            color: #666;
            border: 1px solid #ddd;
            padding: 10px 20px;
            border-radius: 8px;
            cursor: pointer;
            transition: all 0.3s;
        }

        .btn-clear:hover {
            background: #eee;
        }

        .btn-export {
            background: linear-gradient(135deg, #4CAF50 0%, #388E3C 100%);
            color: white;
            border: none;
            padding: 10px 20px;
            border-radius: 8px;
            cursor: pointer;
            font-weight: 500;
            transition: all 0.3s;
        }

        .btn-export:hover {
            transform: translateY(-2px);
            box-shadow: 0 4px 15px rgba(76, 175, 80, 0.3);
        }

        /* Results Section */
        .results-section {
            background: white;
            border-radius: 12px;
            overflow: hidden;
            box-shadow: 0 2px 10px rgba(0,0,0,0.08);
        }

        .results-header {
            background: #f8f9fa;
            padding: 15px 20px;
            border-bottom: 1px solid #eee;
            display: flex;
            justify-content: space-between;
            align-items: center;
        }

        .results-count {
            font-weight: 600;
            color: #5D4037;
        }

        .results-summary {
            display: flex;
            gap: 20px;
            font-size: 0.9em;
        }

        .summary-item {
            color: #666;
        }

        .summary-item strong {
            color: #5D4037;
        }

        /* Table Styling */
        .reservation-table {
            width: 100%;
            border-collapse: collapse;
        }

        .reservation-table th {
            background: #5D4037;
            color: white;
            padding: 15px 12px;
            text-align: left;
            font-weight: 500;
            font-size: 0.9em;
            white-space: nowrap;
            position: sticky;
            top: 0;
            z-index: 10;
        }

        .reservation-table td {
            padding: 12px;
            border-bottom: 1px solid #eee;
            font-size: 0.9em;
            vertical-align: middle;
        }

        .reservation-table tr:hover {
            background: #fafafa;
        }

        .reservation-table tr:nth-child(even) {
            background: #f9f9f9;
        }

        .reservation-table tr:nth-child(even):hover {
            background: #f0f0f0;
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

        .status-deposit {
            background: #FFF3E0;
            color: #E65100;
        }

        .status-checkin {
            background: #E8F5E9;
            color: #2E7D32;
        }

        .status-checkout {
            background: #E3F2FD;
            color: #1565C0;
        }

        .status-complete {
            background: #F3E5F5;
            color: #7B1FA2;
        }

        .status-cancel {
            background: #FFEBEE;
            color: #C62828;
        }

        .status-postpone {
            background: #FFF8E1;
            color: #F57F17;
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

        /* Date Display */
        .date-range {
            display: flex;
            flex-direction: column;
            gap: 3px;
        }

        .date-checkin {
            color: #4CAF50;
            font-weight: 500;
        }

        .date-checkout {
            color: #2196F3;
            font-weight: 500;
        }

        .nights-count {
            background: #5D4037;
            color: white;
            padding: 2px 8px;
            border-radius: 10px;
            font-size: 0.8em;
            font-weight: 600;
        }

        /* Price Display */
        .price-total {
            font-weight: 700;
            color: #5D4037;
            font-size: 1em;
        }

        .price-deposit {
            color: #4CAF50;
            font-size: 0.85em;
        }

        .price-remain {
            color: #F44336;
            font-size: 0.85em;
        }

        /* Action Buttons */
        .action-buttons {
            display: flex;
            gap: 5px;
        }

        .btn-action {
            padding: 6px 10px;
            border: none;
            border-radius: 5px;
            cursor: pointer;
            font-size: 0.85em;
            transition: all 0.2s;
            text-decoration: none;
            display: inline-flex;
            align-items: center;
            gap: 4px;
        }

        .btn-view {
            background: #E3F2FD;
            color: #1565C0;
        }

        .btn-view:hover {
            background: #BBDEFB;
        }

        .btn-edit {
            background: #FFF3E0;
            color: #E65100;
        }

        .btn-edit:hover {
            background: #FFE0B2;
        }

        /* Accommodation Tags */
        .accom-tags {
            display: flex;
            flex-wrap: wrap;
            gap: 4px;
        }

        .accom-tag {
            background: #EFEBE9;
            color: #5D4037;
            padding: 3px 8px;
            border-radius: 4px;
            font-size: 0.8em;
            white-space: nowrap;
        }

        /* Pagination */
        .pagination-section {
            padding: 20px;
            background: #f8f9fa;
            display: flex;
            justify-content: center;
            align-items: center;
            gap: 10px;
        }

        .pagination-info {
            color: #666;
            font-size: 0.9em;
        }

        /* Empty State */
        .empty-state {
            text-align: center;
            padding: 60px 20px;
            color: #999;
        }

        .empty-state i {
            font-size: 4em;
            margin-bottom: 15px;
            opacity: 0.5;
        }

        .empty-state h3 {
            margin: 0 0 10px 0;
            color: #666;
        }

        /* Quick Filter Buttons */
        .quick-filters {
            display: flex;
            gap: 10px;
            margin-bottom: 15px;
            flex-wrap: wrap;
        }

        .quick-filter-btn {
            padding: 8px 15px;
            border: 1px solid #ddd;
            background: white;
            border-radius: 20px;
            cursor: pointer;
            font-size: 0.85em;
            transition: all 0.2s;
        }

        .quick-filter-btn:hover,
        .quick-filter-btn.active {
            background: #5D4037;
            color: white;
            border-color: #5D4037;
        }

        /* Today Highlight */
        .today-row {
            background: #FFF8E1 !important;
            border-left: 4px solid #FFC107;
        }

        /* Responsive */
        @media (max-width: 1200px) {
            .reservation-table {
                display: block;
                overflow-x: auto;
            }
        }

        @media (max-width: 768px) {
            .filter-row {
                flex-direction: column;
            }

            .filter-group {
                width: 100%;
            }

            .stats-grid {
                grid-template-columns: repeat(2, 1fr);
            }

            .stat-card .stat-value {
                font-size: 1.5em;
            }

            .results-header {
                flex-direction: column;
                gap: 10px;
            }

            .results-summary {
                flex-wrap: wrap;
                justify-content: center;
            }
        }

        /* Loading Overlay */
        .loading-overlay {
            display: none;
            position: fixed;
            top: 0;
            left: 0;
            right: 0;
            bottom: 0;
            background: rgba(255,255,255,0.9);
            z-index: 9999;
            justify-content: center;
            align-items: center;
        }

        .loading-overlay.show {
            display: flex;
        }

        .loading-spinner {
            text-align: center;
        }

        .loading-spinner i {
            font-size: 3em;
            color: #5D4037;
            animation: spin 1s linear infinite;
        }

        @keyframes spin {
            100% { transform: rotate(360deg); }
        }

        /* Created Date Column */
        .created-date {
            font-size: 0.85em;
            color: #666;
        }

        .created-date .time {
            color: #999;
            font-size: 0.9em;
        }

        /* Reserve By */
        .reserve-by {
            font-size: 0.85em;
            color: #888;
        }

        /* Remark Tooltip */
        .remark-tooltip {
            position: relative;
            cursor: help;
        }

        .remark-tooltip .tooltip-text {
            visibility: hidden;
            background: #333;
            color: white;
            padding: 8px 12px;
            border-radius: 6px;
            position: absolute;
            z-index: 100;
            bottom: 125%;
            left: 50%;
            transform: translateX(-50%);
            white-space: nowrap;
            max-width: 300px;
            font-size: 0.85em;
        }

        .remark-tooltip:hover .tooltip-text {
            visibility: visible;
        }
    </style>

    <div class="reservation-list-container">
        <!-- Page Header -->
        <div class="page-header">
            <h1><i class="fas fa-list-alt"></i> รายการจองทั้งหมด</h1>
            <p>ดูและจัดการรายการจองทั้งหมด เรียงตามวันที่จองล่าสุด</p>
        </div>

        <!-- Stats Cards -->
        <div class="stats-grid">
            <asp:LinkButton ID="btnStatAll" runat="server" CssClass="stat-card stat-all" OnClick="StatCard_Click" CommandArgument="all">
                <div class="stat-icon"><i class="fas fa-calendar-check"></i></div>
                <div class="stat-value"><asp:Label ID="lblStatAll" runat="server" Text="0"></asp:Label></div>
                <div class="stat-label">ทั้งหมด</div>
            </asp:LinkButton>

            <asp:LinkButton ID="btnStatToday" runat="server" CssClass="stat-card stat-today" OnClick="StatCard_Click" CommandArgument="today">
                <div class="stat-icon"><i class="fas fa-calendar-day"></i></div>
                <div class="stat-value"><asp:Label ID="lblStatToday" runat="server" Text="0"></asp:Label></div>
                <div class="stat-label">จองวันนี้</div>
            </asp:LinkButton>

            <asp:LinkButton ID="btnStatDeposit" runat="server" CssClass="stat-card stat-deposit" OnClick="StatCard_Click" CommandArgument="deposit">
                <div class="stat-icon"><i class="fas fa-money-bill-wave"></i></div>
                <div class="stat-value"><asp:Label ID="lblStatDeposit" runat="server" Text="0"></asp:Label></div>
                <div class="stat-label">มัดจำแล้ว</div>
            </asp:LinkButton>

            <asp:LinkButton ID="btnStatCheckin" runat="server" CssClass="stat-card stat-checkin" OnClick="StatCard_Click" CommandArgument="checkin">
                <div class="stat-icon"><i class="fas fa-sign-in-alt"></i></div>
                <div class="stat-value"><asp:Label ID="lblStatCheckin" runat="server" Text="0"></asp:Label></div>
                <div class="stat-label">เช็คอินแล้ว</div>
            </asp:LinkButton>

            <asp:LinkButton ID="btnStatCheckout" runat="server" CssClass="stat-card stat-checkout" OnClick="StatCard_Click" CommandArgument="checkout">
                <div class="stat-icon"><i class="fas fa-sign-out-alt"></i></div>
                <div class="stat-value"><asp:Label ID="lblStatCheckout" runat="server" Text="0"></asp:Label></div>
                <div class="stat-label">เช็คเอาท์แล้ว</div>
            </asp:LinkButton>

            <asp:LinkButton ID="btnStatComplete" runat="server" CssClass="stat-card stat-complete" OnClick="StatCard_Click" CommandArgument="complete">
                <div class="stat-icon"><i class="fas fa-check-circle"></i></div>
                <div class="stat-value"><asp:Label ID="lblStatComplete" runat="server" Text="0"></asp:Label></div>
                <div class="stat-label">เสร็จสิ้น</div>
            </asp:LinkButton>

            <asp:LinkButton ID="btnStatCancel" runat="server" CssClass="stat-card stat-cancel" OnClick="StatCard_Click" CommandArgument="cancel">
                <div class="stat-icon"><i class="fas fa-times-circle"></i></div>
                <div class="stat-value"><asp:Label ID="lblStatCancel" runat="server" Text="0"></asp:Label></div>
                <div class="stat-label">ยกเลิก</div>
            </asp:LinkButton>
        </div>

        <!-- Filter Section -->
        <div class="filter-section">
            <div class="filter-row">
                <div class="filter-group">
                    <label><i class="fas fa-search"></i> ค้นหา</label>
                    <asp:TextBox ID="txtSearch" runat="server" placeholder="ชื่อ, เบอร์โทร, เลขจอง..."></asp:TextBox>
                </div>

                <div class="filter-group">
                    <label><i class="fas fa-filter"></i> สถานะ</label>
                    <asp:DropDownList ID="ddlStatus" runat="server">
                        <asp:ListItem Value="">-- ทั้งหมด --</asp:ListItem>
                        <asp:ListItem Value="มัดจำแล้ว">มัดจำแล้ว</asp:ListItem>
                        <asp:ListItem Value="เช็คอินแล้ว">เช็คอินแล้ว</asp:ListItem>
                        <asp:ListItem Value="เช็คเอาท์แล้ว">เช็คเอาท์แล้ว</asp:ListItem>
                        <asp:ListItem Value="เสร็จสิ้น">เสร็จสิ้น</asp:ListItem>
                        <asp:ListItem Value="ยกเลิก">ยกเลิก (ทั้งหมด)</asp:ListItem>
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

                <div class="filter-group">
                    <label><i class="fas fa-sort"></i> เรียงตาม</label>
                    <asp:DropDownList ID="ddlSortBy" runat="server">
                        <asp:ListItem Value="created_desc">วันที่จอง (ล่าสุด)</asp:ListItem>
                        <asp:ListItem Value="created_asc">วันที่จอง (เก่าสุด)</asp:ListItem>
                        <asp:ListItem Value="checkin_asc">วันเช็คอิน (ใกล้สุด)</asp:ListItem>
                        <asp:ListItem Value="checkin_desc">วันเช็คอิน (ไกลสุด)</asp:ListItem>
                        <asp:ListItem Value="price_desc">ราคา (สูง-ต่ำ)</asp:ListItem>
                        <asp:ListItem Value="price_asc">ราคา (ต่ำ-สูง)</asp:ListItem>
                    </asp:DropDownList>
                </div>

                <div class="filter-buttons">
                    <asp:Button ID="btnSearch" runat="server" Text="ค้นหา" CssClass="btn-search" OnClick="btnSearch_Click" />
                    <asp:Button ID="btnClear" runat="server" Text="ล้าง" CssClass="btn-clear" OnClick="btnClear_Click" />
                    <asp:Button ID="btnExport" runat="server" Text="Excel" CssClass="btn-export" OnClick="btnExport_Click" />
                </div>
            </div>
        </div>

        <!-- Results Section -->
        <div class="results-section">
            <div class="results-header">
                <div class="results-count">
                    <i class="fas fa-list"></i> พบ <asp:Label ID="lblResultCount" runat="server" Text="0"></asp:Label> รายการ
                </div>
                <div class="results-summary">
                    <span class="summary-item">ยอดรวม: <strong><asp:Label ID="lblTotalAmount" runat="server" Text="0"></asp:Label></strong> บาท</span>
                    <span class="summary-item">มัดจำ: <strong><asp:Label ID="lblTotalDeposit" runat="server" Text="0"></asp:Label></strong> บาท</span>
                    <span class="summary-item">ค้างชำระ: <strong><asp:Label ID="lblTotalRemain" runat="server" Text="0"></asp:Label></strong> บาท</span>
                </div>
            </div>

            <div style="overflow-x: auto;">
                <asp:GridView ID="gvReservations" runat="server" AutoGenerateColumns="false"
                    CssClass="reservation-table" EmptyDataText=""
                    OnRowDataBound="gvReservations_RowDataBound"
                    DataKeyNames="ID">
                    <Columns>
                        <asp:TemplateField HeaderText="#">
                            <ItemTemplate>
                                <strong><%# Eval("ID") %></strong>
                            </ItemTemplate>
                        </asp:TemplateField>

                        <asp:TemplateField HeaderText="วันที่จอง">
                            <ItemTemplate>
                                <div class="created-date">
                                    <%# Convert.ToDateTime(Eval("Created_Date")).ToString("dd/MM/yyyy") %>
                                    <div class="time"><%# Convert.ToDateTime(Eval("Created_Date")).ToString("HH:mm") %></div>
                                </div>
                            </ItemTemplate>
                        </asp:TemplateField>

                        <asp:TemplateField HeaderText="ลูกค้า">
                            <ItemTemplate>
                                <div class="customer-name"><%# Eval("Name") %></div>
                                <div class="customer-phone"><%# Eval("Customer_MobilePhone") %></div>
                            </ItemTemplate>
                        </asp:TemplateField>

                        <asp:TemplateField HeaderText="ที่พัก">
                            <ItemTemplate>
                                <div class="accom-tags">
                                    <asp:Literal ID="litAccom" runat="server"></asp:Literal>
                                </div>
                            </ItemTemplate>
                        </asp:TemplateField>

                        <asp:TemplateField HeaderText="วันเข้าพัก">
                            <ItemTemplate>
                                <div class="date-range">
                                    <span class="date-checkin"><i class="fas fa-sign-in-alt"></i> <%# FormatDate(Eval("CheckinDate")) %></span>
                                    <span class="date-checkout"><i class="fas fa-sign-out-alt"></i> <%# FormatDate(Eval("CheckoutDate")) %></span>
                                </div>
                            </ItemTemplate>
                        </asp:TemplateField>

                        <asp:TemplateField HeaderText="คืน">
                            <ItemTemplate>
                                <span class="nights-count"><%# Eval("StayDays") %></span>
                            </ItemTemplate>
                        </asp:TemplateField>

                        <asp:TemplateField HeaderText="ราคา">
                            <ItemTemplate>
                                <div class="price-total"><%# String.Format("{0:N0}", Eval("TotalPrice")) %></div>
                                <div class="price-deposit">มัดจำ: <%# String.Format("{0:N0}", Eval("Deposit")) %></div>
                                <div class="price-remain">ค้าง: <%# String.Format("{0:N0}", Convert.ToDecimal(Eval("TotalPrice")) - Convert.ToDecimal(Eval("Deposit") ?? 0)) %></div>
                            </ItemTemplate>
                        </asp:TemplateField>

                        <asp:TemplateField HeaderText="สถานะ">
                            <ItemTemplate>
                                <asp:Label ID="lblStatus" runat="server" CssClass="status-badge"></asp:Label>
                            </ItemTemplate>
                        </asp:TemplateField>

                        <asp:TemplateField HeaderText="จองโดย">
                            <ItemTemplate>
                                <span class="reserve-by"><%# Eval("Reserve_By") %></span>
                            </ItemTemplate>
                        </asp:TemplateField>

                        <asp:TemplateField HeaderText="หมายเหตุ">
                            <ItemTemplate>
                                <asp:Panel ID="pnlRemark" runat="server" CssClass="remark-tooltip" Visible='<%# !string.IsNullOrEmpty(Eval("Remark")?.ToString()) %>'>
                                    <i class="fas fa-comment-alt" style="color: #888;"></i>
                                    <span class="tooltip-text"><%# Eval("Remark") %></span>
                                </asp:Panel>
                            </ItemTemplate>
                        </asp:TemplateField>

                        <asp:TemplateField HeaderText="">
                            <ItemTemplate>
                                <div class="action-buttons">
                                    <a href='<%# "Reservation_Confirmed.aspx?id=" + Eval("ID") %>' class="btn-action btn-view" target="_blank">
                                        <i class="fas fa-eye"></i>
                                    </a>
                                    <a href='<%# "Reserve.aspx?id=" + Eval("ID") + "&phone=" + Eval("Customer_MobilePhone") + "&command=edit" %>' class="btn-action btn-edit">
                                        <i class="fas fa-edit"></i>
                                    </a>
                                </div>
                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                    <EmptyDataTemplate>
                        <div class="empty-state">
                            <i class="fas fa-search"></i>
                            <h3>ไม่พบรายการจอง</h3>
                            <p>ลองเปลี่ยนเงื่อนไขการค้นหาหรือล้างตัวกรอง</p>
                        </div>
                    </EmptyDataTemplate>
                </asp:GridView>
            </div>

            <!-- Pagination -->
            <div class="pagination-section">
                <asp:Button ID="btnPrevPage" runat="server" Text="ก่อนหน้า" CssClass="btn-clear" OnClick="btnPrevPage_Click" />
                <span class="pagination-info">
                    หน้า <asp:Label ID="lblCurrentPage" runat="server" Text="1"></asp:Label> / <asp:Label ID="lblTotalPages" runat="server" Text="1"></asp:Label>
                </span>
                <asp:Button ID="btnNextPage" runat="server" Text="ถัดไป" CssClass="btn-clear" OnClick="btnNextPage_Click" />
            </div>
        </div>
    </div>

    <!-- Loading Overlay -->
    <div class="loading-overlay" id="loadingOverlay">
        <div class="loading-spinner">
            <i class="fas fa-circle-notch fa-spin"></i>
            <p>กำลังโหลด...</p>
        </div>
    </div>

    <script type="text/javascript">
        // Show loading on postback
        function showLoading() {
            document.getElementById('loadingOverlay').classList.add('show');
        }

        // Hide loading
        function hideLoading() {
            document.getElementById('loadingOverlay').classList.remove('show');
        }

        // Auto-hide loading after page load
        window.onload = function () {
            hideLoading();
        };
    </script>
</asp:Content>
