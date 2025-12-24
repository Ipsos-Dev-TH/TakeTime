<%@ Page Title="Customer Analytics - วิเคราะห์ลูกค้า" Language="C#"
    MasterPageFile="~/Site.Master" AutoEventWireup="true"
    CodeBehind="CustomerAnalytics.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Report.CustomerAnalytics" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <!-- ApexCharts CDN -->
    <script src="https://cdn.jsdelivr.net/npm/apexcharts"></script>

    <style>
        /* ============================================
           CUSTOMER ANALYTICS STYLES
           ============================================ */
        .analytics-container {
            padding: 20px;
            background: #f8f9fa;
            min-height: 100vh;
        }

        /* Page Header */
        .analytics-header {
            background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%);
            padding: 30px;
            border-radius: 15px;
            color: white;
            margin-bottom: 30px;
            box-shadow: 0 10px 30px rgba(0,0,0,0.2);
        }

        .analytics-header h1 {
            margin: 0;
            font-size: 2.5rem;
            font-weight: 700;
        }

        .analytics-header p {
            margin: 10px 0 0 0;
            opacity: 0.9;
            font-size: 1.1rem;
        }

        /* Filter Panel */
        .filter-panel {
            background: white;
            border-radius: 12px;
            padding: 25px;
            box-shadow: 0 4px 15px rgba(0,0,0,0.08);
            margin-bottom: 30px;
        }

        .filter-panel h3 {
            margin: 0 0 20px 0;
            color: #1f2937;
            font-size: 1.2rem;
        }

        .filter-row {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 15px;
            margin-bottom: 15px;
        }

        .filter-group {
            display: flex;
            flex-direction: column;
        }

        .filter-label {
            font-weight: 600;
            margin-bottom: 5px;
            color: #4b5563;
            font-size: 0.9rem;
        }

        .filter-input {
            padding: 10px;
            border: 1px solid #d1d5db;
            border-radius: 8px;
            font-size: 0.95rem;
            transition: border-color 0.2s;
        }

        .filter-input:focus {
            outline: none;
            border-color: #3b82f6;
            box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.1);
        }

        /* Summary Cards */
        .summary-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
            gap: 20px;
            margin-bottom: 30px;
        }

        .summary-card {
            background: white;
            border-radius: 12px;
            padding: 20px;
            box-shadow: 0 4px 15px rgba(0,0,0,0.08);
            border-left: 4px solid;
            transition: transform 0.3s ease;
        }

        .summary-card:hover {
            transform: translateY(-5px);
        }

        .summary-card.total { border-left-color: #3b82f6; }
        .summary-card.new { border-left-color: #10b981; }
        .summary-card.returning { border-left-color: #f59e0b; }
        .summary-card.vip { border-left-color: #8b5cf6; }

        .summary-card-label {
            font-size: 0.85rem;
            color: #6b7280;
            font-weight: 600;
            text-transform: uppercase;
            margin-bottom: 10px;
        }

        .summary-card-value {
            font-size: 2rem;
            font-weight: 700;
            color: #1f2937;
        }

        .summary-card-icon {
            font-size: 2rem;
            opacity: 0.3;
            float: right;
        }

        /* Charts Section */
        .charts-section {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(400px, 1fr));
            gap: 20px;
            margin-bottom: 30px;
        }

        .chart-card {
            background: white;
            border-radius: 12px;
            padding: 25px;
            box-shadow: 0 4px 15px rgba(0,0,0,0.08);
        }

        .chart-card-header {
            margin-bottom: 20px;
            padding-bottom: 15px;
            border-bottom: 2px solid #f3f4f6;
        }

        .chart-card-title {
            font-size: 1.2rem;
            font-weight: 600;
            color: #1f2937;
            margin: 0;
        }

        /* Tables */
        .data-table {
            background: white;
            border-radius: 12px;
            padding: 25px;
            box-shadow: 0 4px 15px rgba(0,0,0,0.08);
            margin-bottom: 30px;
            overflow-x: auto;
        }

        .data-table h3 {
            margin: 0 0 20px 0;
            color: #1f2937;
            font-size: 1.3rem;
            display: flex;
            justify-content: space-between;
            align-items: center;
        }

        .table-responsive {
            overflow-x: auto;
        }

        .customer-table {
            width: 100%;
            border-collapse: collapse;
        }

        .customer-table thead {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
        }

        .customer-table th {
            padding: 15px;
            text-align: left;
            font-weight: 600;
            font-size: 0.9rem;
            white-space: nowrap;
        }

        .customer-table td {
            padding: 12px 15px;
            border-bottom: 1px solid #e5e7eb;
            vertical-align: middle;
        }

        .customer-table tbody tr {
            transition: background 0.2s ease;
        }

        .customer-table tbody tr:hover {
            background: #f9fafb;
        }

        /* Badges */
        .badge {
            display: inline-block;
            padding: 4px 12px;
            border-radius: 20px;
            font-size: 0.8rem;
            font-weight: 600;
        }

        .badge-new {
            background: linear-gradient(135deg, #10b981 0%, #059669 100%);
            color: white;
        }

        .badge-returning {
            background: linear-gradient(135deg, #f59e0b 0%, #d97706 100%);
            color: white;
        }

        .badge-vip {
            background: linear-gradient(135deg, #8b5cf6 0%, #7c3aed 100%);
            color: white;
        }

        /* Action Buttons */
        .btn-custom {
            padding: 10px 20px;
            border-radius: 8px;
            font-weight: 600;
            border: none;
            cursor: pointer;
            transition: all 0.3s ease;
            display: inline-flex;
            align-items: center;
            gap: 8px;
        }

        .btn-primary-custom {
            background: linear-gradient(135deg, #3b82f6 0%, #2563eb 100%);
            color: white;
        }

        .btn-primary-custom:hover {
            transform: translateY(-2px);
            box-shadow: 0 4px 12px rgba(59, 130, 246, 0.4);
        }

        .btn-success-custom {
            background: linear-gradient(135deg, #10b981 0%, #059669 100%);
            color: white;
        }

        .btn-success-custom:hover {
            transform: translateY(-2px);
            box-shadow: 0 4px 12px rgba(16, 185, 129, 0.4);
        }

        /* Empty State */
        .empty-state {
            text-align: center;
            padding: 60px 20px;
            color: #6b7280;
        }

        .empty-state-icon {
            font-size: 64px;
            margin-bottom: 20px;
            opacity: 0.5;
        }

        .empty-state-text {
            font-size: 1.1rem;
            margin: 0;
        }

        /* View Detail Button */
        .btn-view-detail {
            background: linear-gradient(135deg, #3b82f6 0%, #2563eb 100%);
            color: white;
            padding: 6px 12px;
            border-radius: 6px;
            text-decoration: none;
            font-size: 0.85rem;
            display: inline-block;
            transition: all 0.3s ease;
        }

        .btn-view-detail:hover {
            transform: translateY(-2px);
            box-shadow: 0 4px 12px rgba(59, 130, 246, 0.4);
            color: white;
        }

        /* Status Badges */
        .status-badge {
            display: inline-block;
            padding: 4px 10px;
            border-radius: 15px;
            font-size: 0.75rem;
            font-weight: 600;
        }

        .status-confirmed {
            background: #d1fae5;
            color: #065f46;
        }

        .status-checkedin {
            background: #dbeafe;
            color: #1e40af;
        }

        .status-checkedout {
            background: #e5e7eb;
            color: #374151;
        }

        .status-cancelled {
            background: #fee2e2;
            color: #991b1b;
        }

        /* Responsive */
        @media (max-width: 768px) {
            .analytics-header h1 {
                font-size: 1.8rem;
            }

            .charts-section {
                grid-template-columns: 1fr;
            }

            .summary-grid {
                grid-template-columns: 1fr;
            }

            .filter-row {
                grid-template-columns: 1fr;
            }
        }
    </style>

    <div class="analytics-container">
        <!-- Header -->
        <div class="analytics-header">
            <h1>👥 Customer Analytics - วิเคราะห์ลูกค้า</h1>
            <p>รายงานข้อมูลลูกค้า วิเคราะห์พฤติกรรม และติดตามลูกค้าประจำ</p>
        </div>

        <!-- Filter Panel -->
        <div class="filter-panel">
            <h3>🔍 ตัวกรองข้อมูล</h3>
            <div class="filter-row">
                <div class="filter-group">
                    <label class="filter-label">ปี</label>
                    <asp:DropDownList ID="ddlYear" runat="server" CssClass="filter-input" AutoPostBack="true"
                        OnSelectedIndexChanged="ddlYear_SelectedIndexChanged">
                    </asp:DropDownList>
                </div>
                <div class="filter-group">
                    <label class="filter-label">ประเภทลูกค้า</label>
                    <asp:DropDownList ID="ddlCustomerType" runat="server" CssClass="filter-input" AutoPostBack="true"
                        OnSelectedIndexChanged="ddlCustomerType_SelectedIndexChanged">
                        <asp:ListItem Value="ALL" Selected="True">ทั้งหมด</asp:ListItem>
                        <asp:ListItem Value="NEW">ลูกค้าใหม่</asp:ListItem>
                        <asp:ListItem Value="RETURNING">ลูกค้าประจำ (2-5 ครั้ง)</asp:ListItem>
                        <asp:ListItem Value="VIP">VIP (>5 ครั้ง)</asp:ListItem>
                    </asp:DropDownList>
                </div>
                <div class="filter-group">
                    <label class="filter-label">จำนวนแสดง</label>
                    <asp:DropDownList ID="ddlLimit" runat="server" CssClass="filter-input" AutoPostBack="true"
                        OnSelectedIndexChanged="ddlLimit_SelectedIndexChanged">
                        <asp:ListItem Value="10">10 รายการ</asp:ListItem>
                        <asp:ListItem Value="25" Selected="True">25 รายการ</asp:ListItem>
                        <asp:ListItem Value="50">50 รายการ</asp:ListItem>
                        <asp:ListItem Value="100">100 รายการ</asp:ListItem>
                        <asp:ListItem Value="0">ทั้งหมด</asp:ListItem>
                    </asp:DropDownList>
                </div>
                <div class="filter-group" style="display: flex; align-items: flex-end;">
                    <asp:Button ID="btnExport" runat="server" Text="📥 Export Excel"
                        CssClass="btn-success-custom" OnClick="btnExport_Click" />
                </div>
            </div>
        </div>

        <!-- Summary Cards -->
        <div class="summary-grid">
            <div class="summary-card total">
                <div class="summary-card-icon">👥</div>
                <div class="summary-card-label">ลูกค้าทั้งหมด</div>
                <div class="summary-card-value">
                    <asp:Literal ID="litTotalCustomers" runat="server" Text="0" />
                </div>
            </div>
            <div class="summary-card new">
                <div class="summary-card-icon">🆕</div>
                <div class="summary-card-label">ลูกค้าใหม่ (1 ครั้ง)</div>
                <div class="summary-card-value">
                    <asp:Literal ID="litNewCustomers" runat="server" Text="0" />
                </div>
            </div>
            <div class="summary-card returning">
                <div class="summary-card-icon">🔄</div>
                <div class="summary-card-label">ลูกค้าประจำ (2-5 ครั้ง)</div>
                <div class="summary-card-value">
                    <asp:Literal ID="litReturningCustomers" runat="server" Text="0" />
                </div>
            </div>
            <div class="summary-card vip">
                <div class="summary-card-icon">⭐</div>
                <div class="summary-card-label">VIP (>5 ครั้ง)</div>
                <div class="summary-card-value">
                    <asp:Literal ID="litVIPCustomers" runat="server" Text="0" />
                </div>
            </div>
        </div>

        <!-- Charts -->
        <div class="charts-section">
            <!-- Customer Segmentation Pie Chart -->
            <div class="chart-card">
                <div class="chart-card-header">
                    <h3 class="chart-card-title">📊 การแบ่งกลุ่มลูกค้า</h3>
                </div>
                <div id="customerSegmentationChart"></div>
            </div>

            <!-- Top Customers Bar Chart -->
            <div class="chart-card">
                <div class="chart-card-header">
                    <h3 class="chart-card-title">🏆 Top 10 ลูกค้ายอดนิยม</h3>
                </div>
                <div id="topCustomersChart"></div>
            </div>
        </div>

        <!-- Repeat Customers Table (ตามที่ user ขอ!) -->
        <div class="data-table">
            <h3>
                <span>🔄 ลูกค้าที่กลับมาพักซ้ำในปี <asp:Literal ID="litSelectedYear" runat="server" /></span>
                <span style="font-size: 0.9rem; color: #6b7280;">
                    (พบ <asp:Literal ID="litRepeatCount" runat="server" Text="0" /> ราย)
                </span>
            </h3>
            <div class="table-responsive">
                <asp:GridView ID="gvRepeatCustomers" runat="server" AutoGenerateColumns="False"
                    CssClass="customer-table" EmptyDataText="ไม่พบข้อมูลลูกค้าที่กลับมาพักซ้ำ">
                    <Columns>
                        <asp:TemplateField HeaderText="#">
                            <ItemTemplate>
                                <%# Container.DataItemIndex + 1 %>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:BoundField DataField="CustomerName" HeaderText="ชื่อลูกค้า" />
                        <asp:BoundField DataField="PhoneNumber" HeaderText="เบอร์โทร" />
                        <asp:BoundField DataField="TotalBookings" HeaderText="จำนวนครั้ง" />
                        <asp:TemplateField HeaderText="ประเภท">
                            <ItemTemplate>
                                <%# GetCustomerTypeBadge(Convert.ToInt32(Eval("TotalBookings"))) %>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:BoundField DataField="TotalSpent" HeaderText="ยอดใช้จ่ายรวม" DataFormatString="฿{0:N2}" />
                        <asp:BoundField DataField="AvgSpending" HeaderText="ค่าเฉลี่ย/ครั้ง" DataFormatString="฿{0:N2}" />
                        <asp:BoundField DataField="FirstVisit" HeaderText="มาครั้งแรก" DataFormatString="{0:dd/MM/yyyy}" />
                        <asp:BoundField DataField="LastVisit" HeaderText="มาครั้งล่าสุด" DataFormatString="{0:dd/MM/yyyy}" />
                        <asp:BoundField DataField="PreferredAccommodation" HeaderText="ที่พักที่ชอบ" />
                    </Columns>
                </asp:GridView>
            </div>
        </div>

        <!-- All Customers Table with View Reservations -->
        <div class="data-table">
            <h3>
                <span>📋 รายชื่อลูกค้าทั้งหมด</span>
                <span style="font-size: 0.9rem; color: #6b7280;">
                    (แสดง <asp:Literal ID="litDisplayCount" runat="server" /> จาก <asp:Literal ID="litTotalCount" runat="server" /> ราย)
                </span>
            </h3>
            <div class="table-responsive">
                <asp:GridView ID="gvAllCustomers" runat="server" AutoGenerateColumns="False"
                    CssClass="customer-table" AllowPaging="True" PageSize="25"
                    OnPageIndexChanging="gvAllCustomers_PageIndexChanging"
                    OnRowCommand="gvAllCustomers_RowCommand">
                    <Columns>
                        <asp:TemplateField HeaderText="#">
                            <ItemTemplate>
                                <%# Container.DataItemIndex + 1 %>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:BoundField DataField="CustomerName" HeaderText="ชื่อลูกค้า" />
                        <asp:BoundField DataField="PhoneNumber" HeaderText="เบอร์โทร" />
                        <asp:BoundField DataField="Email" HeaderText="อีเมล" />
                        <asp:BoundField DataField="TotalBookings" HeaderText="จำนวนครั้ง" />
                        <asp:TemplateField HeaderText="ประเภท">
                            <ItemTemplate>
                                <%# GetCustomerTypeBadge(Convert.ToInt32(Eval("TotalBookings"))) %>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:BoundField DataField="TotalSpent" HeaderText="ยอดใช้จ่ายรวม" DataFormatString="฿{0:N2}" />
                        <asp:BoundField DataField="LastVisit" HeaderText="มาครั้งล่าสุด" DataFormatString="{0:dd/MM/yyyy}" />
                        <asp:TemplateField HeaderText="ดูรายละเอียด">
                            <ItemTemplate>
                                <asp:LinkButton ID="lnkViewReservations" runat="server"
                                    CommandName="ViewReservations" CommandArgument='<%# Eval("PhoneNumber") %>'
                                    CssClass="btn-view-detail" ToolTip="ดูประวัติการจอง">
                                    🔍 ดูประวัติ
                                </asp:LinkButton>
                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                    <PagerStyle CssClass="pagination" HorizontalAlign="Center" />
                </asp:GridView>
            </div>
        </div>

        <!-- Customer Reservations Detail Panel -->
        <asp:Panel ID="pnlReservationDetail" runat="server" Visible="false" CssClass="data-table">
            <h3>
                <span>📝 ประวัติการจองของ: <asp:Literal ID="litSelectedCustomer" runat="server" /></span>
                <asp:Button ID="btnCloseDetail" runat="server" Text="❌ ปิด" CssClass="btn-custom"
                    OnClick="btnCloseDetail_Click" style="float: right; background: #ef4444; color: white;" />
            </h3>
            <div class="table-responsive">
                <asp:GridView ID="gvReservationDetail" runat="server" AutoGenerateColumns="False"
                    CssClass="customer-table" EmptyDataText="ไม่พบประวัติการจอง">
                    <Columns>
                        <asp:BoundField DataField="ID" HeaderText="รหัส" />
                        <asp:TemplateField HeaderText="วันที่เข้าพัก">
                            <ItemTemplate>
                                <%# Convert.ToDateTime(Eval("CheckIn_Date")).ToString("dd/MM/yyyy") %> -
                                <%# Convert.ToDateTime(Eval("CheckOut_Date")).ToString("dd/MM/yyyy") %>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:BoundField DataField="AccommodationList" HeaderText="ที่พัก" />
                        <asp:BoundField DataField="TotalPrice" HeaderText="ยอดรวม" DataFormatString="฿{0:N2}" />
                        <asp:TemplateField HeaderText="สถานะ">
                            <ItemTemplate>
                                <span class='<%# GetStatusBadgeClass(Eval("Status").ToString()) %>'>
                                    <%# Eval("Status") %>
                                </span>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:BoundField DataField="Created_Date" HeaderText="วันที่จอง" DataFormatString="{0:dd/MM/yyyy HH:mm}" />
                        <asp:BoundField DataField="Remark" HeaderText="หมายเหตุ" />
                    </Columns>
                </asp:GridView>
            </div>
        </asp:Panel>

        <!-- Monthly Spending Trend Chart -->
        <div class="charts-section">
            <div class="chart-card" style="grid-column: span 2;">
                <div class="chart-card-header">
                    <h3 class="chart-card-title">📈 แนวโน้มการใช้จ่ายรายเดือน</h3>
                </div>
                <div id="monthlyTrendChart"></div>
            </div>
        </div>

        <!-- Additional Analytics Cards -->
        <div class="summary-grid">
            <div class="summary-card" style="border-left-color: #06b6d4;">
                <div class="summary-card-icon">💰</div>
                <div class="summary-card-label">ยอดใช้จ่ายเฉลี่ย/ลูกค้า</div>
                <div class="summary-card-value" style="font-size: 1.5rem;">
                    ฿<asp:Literal ID="litAvgSpendingPerCustomer" runat="server" Text="0" />
                </div>
            </div>
            <div class="summary-card" style="border-left-color: #ec4899;">
                <div class="summary-card-icon">🏨</div>
                <div class="summary-card-label">จำนวนการจองทั้งหมด</div>
                <div class="summary-card-value">
                    <asp:Literal ID="litTotalReservations" runat="server" Text="0" />
                </div>
            </div>
            <div class="summary-card" style="border-left-color: #14b8a6;">
                <div class="summary-card-icon">🔄</div>
                <div class="summary-card-label">อัตราการกลับมาใช้บริการ</div>
                <div class="summary-card-value" style="font-size: 1.5rem;">
                    <asp:Literal ID="litReturnRate" runat="server" Text="0" />%
                </div>
            </div>
            <div class="summary-card" style="border-left-color: #f97316;">
                <div class="summary-card-icon">⏰</div>
                <div class="summary-card-label">ระยะเวลาเฉลี่ยก่อนกลับมา</div>
                <div class="summary-card-value" style="font-size: 1.5rem;">
                    <asp:Literal ID="litAvgDaysBetweenVisits" runat="server" Text="0" /> วัน
                </div>
            </div>
        </div>
    </div>

    <!-- Hidden fields for chart data -->
    <asp:HiddenField ID="hfSegmentationData" runat="server" />
    <asp:HiddenField ID="hfTopCustomersData" runat="server" />
    <asp:HiddenField ID="hfTopCustomersLabels" runat="server" />
    <asp:HiddenField ID="hfMonthlyTrendLabels" runat="server" />
    <asp:HiddenField ID="hfMonthlyTrendData" runat="server" />
    <asp:HiddenField ID="hfMonthlyTrendCustomers" runat="server" />

    <!-- Charts Script -->
    <script type="text/javascript">
        // Customer Segmentation Chart (Pie Chart)
        var segmentationData = <%= hfSegmentationData.Value != "" ? hfSegmentationData.Value : "[0,0,0]" %>;
        var segmentationOptions = {
            series: segmentationData,
            chart: {
                type: 'pie',
                height: 350
            },
            labels: ['ลูกค้าใหม่', 'ลูกค้าประจำ', 'VIP'],
            colors: ['#10b981', '#f59e0b', '#8b5cf6'],
            legend: {
                position: 'bottom'
            },
            dataLabels: {
                enabled: true,
                formatter: function(val, opts) {
                    return opts.w.config.series[opts.seriesIndex];
                }
            },
            tooltip: {
                y: {
                    formatter: function(val) {
                        return val + ' ราย';
                    }
                }
            }
        };
        var segmentationChart = new ApexCharts(document.querySelector("#customerSegmentationChart"), segmentationOptions);
        segmentationChart.render();

        // Top Customers Chart (Bar Chart)
        var topCustomersData = <%= hfTopCustomersData.Value != "" ? hfTopCustomersData.Value : "[0,0,0,0,0,0,0,0,0,0]" %>;
        var topCustomersLabels = <%= hfTopCustomersLabels.Value != "" ? hfTopCustomersLabels.Value : "['','','','','','','','','','']" %>;

        var topCustomersOptions = {
            series: [{
                name: 'ยอดใช้จ่าย',
                data: topCustomersData
            }],
            chart: {
                type: 'bar',
                height: 350,
                toolbar: { show: false }
            },
            colors: ['#f093fb'],
            plotOptions: {
                bar: {
                    borderRadius: 8,
                    horizontal: true,
                    distributed: false
                }
            },
            dataLabels: {
                enabled: true,
                formatter: function(val) {
                    return '฿' + val.toLocaleString();
                }
            },
            xaxis: {
                categories: topCustomersLabels,
                labels: {
                    formatter: function(val) {
                        return '฿' + val.toLocaleString();
                    }
                }
            }
        };
        var topCustomersChart = new ApexCharts(document.querySelector("#topCustomersChart"), topCustomersOptions);
        topCustomersChart.render();

        // Monthly Trend Chart (Line + Bar)
        var monthlyTrendLabels = <%= hfMonthlyTrendLabels.Value != "" ? hfMonthlyTrendLabels.Value : "['ม.ค.','ก.พ.','มี.ค.','เม.ย.','พ.ค.','มิ.ย.','ก.ค.','ส.ค.','ก.ย.','ต.ค.','พ.ย.','ธ.ค.']" %>;
        var monthlyTrendData = <%= hfMonthlyTrendData.Value != "" ? hfMonthlyTrendData.Value : "[0,0,0,0,0,0,0,0,0,0,0,0]" %>;
        var monthlyTrendCustomers = <%= hfMonthlyTrendCustomers.Value != "" ? hfMonthlyTrendCustomers.Value : "[0,0,0,0,0,0,0,0,0,0,0,0]" %>;

        var monthlyTrendOptions = {
            series: [{
                name: 'ยอดใช้จ่าย (฿)',
                type: 'column',
                data: monthlyTrendData
            }, {
                name: 'จำนวนลูกค้า',
                type: 'line',
                data: monthlyTrendCustomers
            }],
            chart: {
                height: 350,
                type: 'line',
                toolbar: { show: false }
            },
            stroke: {
                width: [0, 4]
            },
            colors: ['#667eea', '#f093fb'],
            plotOptions: {
                bar: {
                    columnWidth: '50%',
                    borderRadius: 5
                }
            },
            dataLabels: {
                enabled: false
            },
            xaxis: {
                categories: monthlyTrendLabels
            },
            yaxis: [{
                title: {
                    text: 'ยอดใช้จ่าย (฿)'
                },
                labels: {
                    formatter: function(val) {
                        return '฿' + val.toLocaleString();
                    }
                }
            }, {
                opposite: true,
                title: {
                    text: 'จำนวนลูกค้า'
                }
            }],
            legend: {
                position: 'top'
            }
        };
        var monthlyTrendChart = new ApexCharts(document.querySelector("#monthlyTrendChart"), monthlyTrendOptions);
        monthlyTrendChart.render();
    </script>

</asp:Content>
