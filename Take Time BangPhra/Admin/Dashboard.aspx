<%@ Page Title="Executive Dashboard - Take Time BangPhra" Language="C#"
    MasterPageFile="~/Site.Master" AutoEventWireup="true"
    CodeBehind="Dashboard.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Dashboard" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <!-- ApexCharts CDN -->
    <script src="https://cdn.jsdelivr.net/npm/apexcharts"></script>

    <style>
        /* ============================================
           EXECUTIVE DASHBOARD STYLES
           ============================================ */
        .dashboard-container {
            padding: 20px;
            background: #f8f9fa;
            min-height: 100vh;
        }

        /* Page Header */
        .dashboard-header {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            padding: 30px;
            border-radius: 15px;
            color: white;
            margin-bottom: 30px;
            box-shadow: 0 10px 30px rgba(0,0,0,0.2);
        }

        .dashboard-header h1 {
            margin: 0;
            font-size: 2.5rem;
            font-weight: 700;
        }

        .dashboard-header p {
            margin: 10px 0 0 0;
            opacity: 0.9;
            font-size: 1.1rem;
        }

        /* Quick Stats Cards */
        .stats-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
            gap: 20px;
            margin-bottom: 30px;
        }

        .stat-card {
            background: white;
            border-radius: 12px;
            padding: 25px;
            box-shadow: 0 4px 15px rgba(0,0,0,0.08);
            transition: transform 0.3s ease, box-shadow 0.3s ease;
            border-left: 4px solid;
        }

        .stat-card:hover {
            transform: translateY(-5px);
            box-shadow: 0 8px 25px rgba(0,0,0,0.15);
        }

        .stat-card.revenue { border-left-color: #10b981; }
        .stat-card.bookings { border-left-color: #3b82f6; }
        .stat-card.occupancy { border-left-color: #f59e0b; }
        .stat-card.profit { border-left-color: #8b5cf6; }

        .stat-card-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 15px;
        }

        .stat-card-title {
            font-size: 0.9rem;
            color: #6b7280;
            text-transform: uppercase;
            font-weight: 600;
            letter-spacing: 0.5px;
        }

        .stat-card-icon {
            width: 50px;
            height: 50px;
            border-radius: 10px;
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 24px;
        }

        .stat-card.revenue .stat-card-icon {
            background: linear-gradient(135deg, #10b981 0%, #059669 100%);
            color: white;
        }

        .stat-card.bookings .stat-card-icon {
            background: linear-gradient(135deg, #3b82f6 0%, #2563eb 100%);
            color: white;
        }

        .stat-card.occupancy .stat-card-icon {
            background: linear-gradient(135deg, #f59e0b 0%, #d97706 100%);
            color: white;
        }

        .stat-card.profit .stat-card-icon {
            background: linear-gradient(135deg, #8b5cf6 0%, #7c3aed 100%);
            color: white;
        }

        .stat-card-value {
            font-size: 2.2rem;
            font-weight: 700;
            color: #1f2937;
            margin-bottom: 5px;
        }

        .stat-card-trend {
            font-size: 0.85rem;
            display: flex;
            align-items: center;
            gap: 5px;
        }

        .trend-up {
            color: #10b981;
        }

        .trend-down {
            color: #ef4444;
        }

        .trend-neutral {
            color: #6b7280;
        }

        /* Charts Section */
        .charts-grid {
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

        .chart-card-subtitle {
            font-size: 0.85rem;
            color: #6b7280;
            margin: 5px 0 0 0;
        }

        /* Alerts Section */
        .alerts-section {
            background: white;
            border-radius: 12px;
            padding: 25px;
            box-shadow: 0 4px 15px rgba(0,0,0,0.08);
            margin-bottom: 30px;
        }

        .alert-item {
            padding: 15px;
            border-left: 4px solid;
            margin-bottom: 10px;
            border-radius: 4px;
            display: flex;
            align-items: center;
            gap: 15px;
            transition: background 0.2s ease;
        }

        .alert-item:hover {
            background: #f9fafb;
        }

        .alert-item.warning {
            border-left-color: #f59e0b;
            background: #fffbeb;
        }

        .alert-item.danger {
            border-left-color: #ef4444;
            background: #fef2f2;
        }

        .alert-item.info {
            border-left-color: #3b82f6;
            background: #eff6ff;
        }

        .alert-icon {
            font-size: 24px;
            width: 40px;
            height: 40px;
            display: flex;
            align-items: center;
            justify-content: center;
            border-radius: 50%;
        }

        .alert-item.warning .alert-icon {
            background: #f59e0b;
            color: white;
        }

        .alert-item.danger .alert-icon {
            background: #ef4444;
            color: white;
        }

        .alert-item.info .alert-icon {
            background: #3b82f6;
            color: white;
        }

        .alert-content {
            flex: 1;
        }

        .alert-title {
            font-weight: 600;
            margin-bottom: 3px;
        }

        .alert-description {
            font-size: 0.9rem;
            color: #6b7280;
        }

        .alert-action {
            text-decoration: none;
            color: #3b82f6;
            font-weight: 600;
            font-size: 0.9rem;
        }

        .alert-action:hover {
            text-decoration: underline;
        }

        /* Responsive */
        @media (max-width: 768px) {
            .dashboard-header h1 {
                font-size: 1.8rem;
            }

            .stats-grid {
                grid-template-columns: 1fr;
            }

            .charts-grid {
                grid-template-columns: 1fr;
            }

            .stat-card-value {
                font-size: 1.8rem;
            }
        }

        /* Loading Animation */
        .loading-spinner {
            display: inline-block;
            width: 20px;
            height: 20px;
            border: 3px solid rgba(255,255,255,.3);
            border-radius: 50%;
            border-top-color: #fff;
            animation: spin 1s ease-in-out infinite;
        }

        @keyframes spin {
            to { transform: rotate(360deg); }
        }
    </style>

    <div class="dashboard-container">
        <!-- Header -->
        <div class="dashboard-header">
            <h1>📊 Executive Dashboard</h1>
            <p>สวัสดี <strong><asp:Literal ID="litUsername" runat="server" /></strong> | อัพเดตล่าสุด: <asp:Literal ID="litLastUpdate" runat="server" /></p>
        </div>

        <!-- Today's Quick Stats -->
        <div class="stats-grid">
            <!-- Revenue Today Card -->
            <div class="stat-card revenue">
                <div class="stat-card-header">
                    <div class="stat-card-title">💰 รายได้วันนี้</div>
                    <div class="stat-card-icon">฿</div>
                </div>
                <div class="stat-card-value">
                    ฿<asp:Literal ID="litRevenueToday" runat="server" Text="0" />
                </div>
                <div class="stat-card-trend">
                    <asp:Literal ID="litRevenueTrend" runat="server" />
                </div>
            </div>

            <!-- Bookings Today Card -->
            <div class="stat-card bookings">
                <div class="stat-card-header">
                    <div class="stat-card-title">📅 การจองวันนี้</div>
                    <div class="stat-card-icon">🏨</div>
                </div>
                <div class="stat-card-value">
                    <asp:Literal ID="litBookingsToday" runat="server" Text="0" />
                </div>
                <div class="stat-card-trend">
                    <span><asp:Literal ID="litCheckIns" runat="server" Text="0" /> เช็คอิน</span> |
                    <span><asp:Literal ID="litCheckOuts" runat="server" Text="0" /> เช็คเอาท์</span>
                </div>
            </div>

            <!-- Occupancy Rate Card -->
            <div class="stat-card occupancy">
                <div class="stat-card-header">
                    <div class="stat-card-title">📈 อัตราการจอง</div>
                    <div class="stat-card-icon">%</div>
                </div>
                <div class="stat-card-value">
                    <asp:Literal ID="litOccupancyRate" runat="server" Text="0" />%
                </div>
                <div class="stat-card-trend">
                    <asp:Literal ID="litOccupancyDetails" runat="server" />
                </div>
            </div>

            <!-- Net Profit MTD Card -->
            <div class="stat-card profit">
                <div class="stat-card-header">
                    <div class="stat-card-title">💹 กำไรสุทธิ (เดือนนี้)</div>
                    <div class="stat-card-icon">💰</div>
                </div>
                <div class="stat-card-value">
                    ฿<asp:Literal ID="litNetProfit" runat="server" Text="0" />
                </div>
                <div class="stat-card-trend">
                    <asp:Literal ID="litProfitMargin" runat="server" />
                </div>
            </div>
        </div>

        <!-- Charts Section -->
        <div class="charts-grid">
            <!-- Revenue Trend Chart -->
            <div class="chart-card">
                <div class="chart-card-header">
                    <h3 class="chart-card-title">📈 Revenue Trend (30 วัน)</h3>
                    <p class="chart-card-subtitle">รายได้ย้อนหลัง 30 วัน</p>
                </div>
                <div id="revenueTrendChart"></div>
            </div>

            <!-- Payment Method Distribution -->
            <div class="chart-card">
                <div class="chart-card-header">
                    <h3 class="chart-card-title">💳 ช่องทางการชำระเงิน</h3>
                    <p class="chart-card-subtitle">สัดส่วนการชำระเงิน (เดือนนี้)</p>
                </div>
                <div id="paymentMethodChart"></div>
            </div>

            <!-- Top Accommodations -->
            <div class="chart-card">
                <div class="chart-card-header">
                    <h3 class="chart-card-title">🏆 ที่พักยอดนิยม Top 5</h3>
                    <p class="chart-card-subtitle">จำนวนการจอง (เดือนนี้)</p>
                </div>
                <div id="topAccommodationsChart"></div>
            </div>
        </div>

        <!-- Action Required Alerts -->
        <div class="alerts-section">
            <h3 style="margin-bottom: 20px; color: #1f2937;">⚠️ สิ่งที่ต้องดำเนินการ</h3>

            <asp:Repeater ID="rptAlerts" runat="server">
                <ItemTemplate>
                    <div class="alert-item <%# Eval("Type") %>">
                        <div class="alert-icon">
                            <%# Eval("Icon") %>
                        </div>
                        <div class="alert-content">
                            <div class="alert-title"><%# Eval("Title") %></div>
                            <div class="alert-description"><%# Eval("Description") %></div>
                        </div>
                        <a href='<%# Eval("ActionUrl") %>' class="alert-action">ดูรายละเอียด →</a>
                    </div>
                </ItemTemplate>
            </asp:Repeater>

            <asp:Literal ID="litNoAlerts" runat="server" Visible="false">
                <div style="text-align: center; padding: 30px; color: #6b7280;">
                    <i class="fas fa-check-circle" style="font-size: 48px; color: #10b981; margin-bottom: 10px;"></i>
                    <p style="margin: 0; font-size: 1.1rem;">ไม่มีรายการที่ต้องดำเนินการ ทุกอย่างเรียบร้อย! 🎉</p>
                </div>
            </asp:Literal>
        </div>

        <!-- Quick Links -->
        <div class="charts-grid">
            <div class="chart-card" style="text-align: center; padding: 40px;">
                <h4 style="margin-bottom: 20px;">🚀 Quick Actions</h4>
                <div style="display: flex; gap: 10px; justify-content: center; flex-wrap: wrap;">
                    <a href="/Admin/Report" class="btn btn-primary">📊 ดูรายงานทั้งหมด</a>
                    <a href="/ReserveTable" class="btn btn-success">👥 ผู้เข้าพักวันนี้</a>
                    <a href="/Account/CheckDocument_New" class="btn btn-info">💰 รายงานรายได้</a>
                </div>
            </div>
        </div>
    </div>

    <!-- Hidden fields for chart data -->
    <asp:HiddenField ID="hfRevenueTrendData" runat="server" />
    <asp:HiddenField ID="hfPaymentMethodData" runat="server" />
    <asp:HiddenField ID="hfTopAccommodationsData" runat="server" />

    <!-- ApexCharts Script -->
    <script type="text/javascript">
        // Revenue Trend Chart (Line Chart)
        var revenueTrendOptions = {
            series: [{
                name: 'รายได้',
                data: <%= hfRevenueTrendData.Value != "" ? hfRevenueTrendData.Value : "[0]" %>
            }],
            chart: {
                height: 300,
                type: 'area',
                toolbar: { show: false },
                zoom: { enabled: false }
            },
            colors: ['#10b981'],
            dataLabels: { enabled: false },
            stroke: {
                curve: 'smooth',
                width: 3
            },
            fill: {
                type: 'gradient',
                gradient: {
                    shadeIntensity: 1,
                    opacityFrom: 0.7,
                    opacityTo: 0.3
                }
            },
            xaxis: {
                type: 'datetime'
            },
            yaxis: {
                labels: {
                    formatter: function(val) {
                        return '฿' + val.toLocaleString();
                    }
                }
            },
            tooltip: {
                y: {
                    formatter: function(val) {
                        return '฿' + val.toLocaleString();
                    }
                }
            }
        };
        var revenueTrendChart = new ApexCharts(document.querySelector("#revenueTrendChart"), revenueTrendOptions);
        revenueTrendChart.render();

        // Payment Method Chart (Donut Chart)
        var paymentMethodOptions = {
            series: <%= hfPaymentMethodData.Value != "" ? hfPaymentMethodData.Value : "[0,0,0,0]" %>,
            chart: {
                type: 'donut',
                height: 300
            },
            labels: ['เงินสด', 'โอนกสิกร', 'โอนกรุงไทย', 'เงินกรรมการ'],
            colors: ['#10b981', '#3b82f6', '#f59e0b', '#8b5cf6'],
            legend: {
                position: 'bottom'
            },
            plotOptions: {
                pie: {
                    donut: {
                        labels: {
                            show: true,
                            total: {
                                show: true,
                                label: 'ยอดรวม',
                                formatter: function(w) {
                                    return '฿' + w.globals.seriesTotals.reduce((a, b) => a + b, 0).toLocaleString();
                                }
                            }
                        }
                    }
                }
            },
            tooltip: {
                y: {
                    formatter: function(val) {
                        return '฿' + val.toLocaleString();
                    }
                }
            }
        };
        var paymentMethodChart = new ApexCharts(document.querySelector("#paymentMethodChart"), paymentMethodOptions);
        paymentMethodChart.render();

        // Top Accommodations Chart (Bar Chart)
        var topAccommodationsOptions = {
            series: [{
                name: 'จำนวนการจอง',
                data: <%= hfTopAccommodationsData.Value != "" ? hfTopAccommodationsData.Value : "[0,0,0,0,0]" %>
            }],
            chart: {
                type: 'bar',
                height: 300,
                toolbar: { show: false }
            },
            colors: ['#667eea'],
            plotOptions: {
                bar: {
                    borderRadius: 8,
                    horizontal: true,
                    distributed: false
                }
            },
            dataLabels: {
                enabled: true
            },
            xaxis: {
                categories: ['กระโจม', 'ห้องกระจก', 'วิลล่า', 'เต็นท์', 'เคบิ้น']
            },
            yaxis: {
                labels: {
                    style: {
                        fontSize: '13px'
                    }
                }
            }
        };
        var topAccommodationsChart = new ApexCharts(document.querySelector("#topAccommodationsChart"), topAccommodationsOptions);
        topAccommodationsChart.render();
    </script>

</asp:Content>
