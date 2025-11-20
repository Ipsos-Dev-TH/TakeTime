<%@ Page Title="งบกำไรขาดทุน (P&L Statement)" Language="C#" MasterPageFile="~/Site.Master"
    AutoEventWireup="true" CodeBehind="ProfitLoss.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Report.ProfitLoss" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .pl-container {
            padding: 30px;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
        }

        .pl-header {
            background: white;
            border-radius: 20px;
            padding: 30px;
            margin-bottom: 30px;
            box-shadow: 0 10px 40px rgba(0,0,0,0.1);
        }

        .pl-header h1 {
            margin: 0 0 10px 0;
            color: #2d3748;
            font-size: 32px;
            font-weight: 700;
        }

        .pl-header .subtitle {
            color: #718096;
            font-size: 16px;
        }

        .filter-panel {
            background: white;
            border-radius: 15px;
            padding: 25px;
            margin-bottom: 25px;
            box-shadow: 0 4px 20px rgba(0,0,0,0.08);
            display: flex;
            gap: 15px;
            flex-wrap: wrap;
            align-items: center;
        }

        .filter-panel label {
            font-weight: 600;
            color: #4a5568;
            margin-right: 8px;
        }

        .filter-panel select,
        .filter-panel .btn {
            padding: 10px 20px;
            border: 2px solid #e2e8f0;
            border-radius: 10px;
            font-size: 15px;
            transition: all 0.3s;
        }

        .filter-panel select:focus {
            outline: none;
            border-color: #667eea;
            box-shadow: 0 0 0 3px rgba(102, 126, 234, 0.1);
        }

        .filter-panel .btn-export {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            border: none;
            cursor: pointer;
            font-weight: 600;
            padding: 12px 25px;
        }

        .filter-panel .btn-export:hover {
            transform: translateY(-2px);
            box-shadow: 0 8px 20px rgba(102, 126, 234, 0.3);
        }

        .summary-cards {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
            gap: 20px;
            margin-bottom: 30px;
        }

        .summary-card {
            background: white;
            border-radius: 15px;
            padding: 25px;
            box-shadow: 0 4px 20px rgba(0,0,0,0.08);
            transition: transform 0.3s;
        }

        .summary-card:hover {
            transform: translateY(-5px);
            box-shadow: 0 8px 30px rgba(0,0,0,0.15);
        }

        .summary-card.revenue {
            border-left: 6px solid #48bb78;
        }

        .summary-card.cogs {
            border-left: 6px solid #ed8936;
        }

        .summary-card.gross {
            border-left: 6px solid #4299e1;
        }

        .summary-card.expenses {
            border-left: 6px solid #f56565;
        }

        .summary-card.net {
            border-left: 6px solid #9f7aea;
        }

        .summary-card .card-title {
            font-size: 14px;
            color: #718096;
            margin-bottom: 10px;
            font-weight: 600;
            text-transform: uppercase;
            letter-spacing: 0.5px;
        }

        .summary-card .card-value {
            font-size: 32px;
            font-weight: 700;
            color: #2d3748;
            margin-bottom: 10px;
        }

        .summary-card .card-trend {
            font-size: 14px;
            font-weight: 600;
        }

        .summary-card .card-trend.positive {
            color: #48bb78;
        }

        .summary-card .card-trend.negative {
            color: #f56565;
        }

        .summary-card .card-margin {
            font-size: 13px;
            color: #718096;
            margin-top: 8px;
        }

        .pl-statement {
            background: white;
            border-radius: 15px;
            padding: 30px;
            box-shadow: 0 4px 20px rgba(0,0,0,0.08);
            margin-bottom: 30px;
        }

        .pl-statement h2 {
            margin: 0 0 25px 0;
            color: #2d3748;
            font-size: 22px;
            font-weight: 700;
            padding-bottom: 15px;
            border-bottom: 3px solid #e2e8f0;
        }

        .pl-table {
            width: 100%;
            border-collapse: collapse;
        }

        .pl-table th {
            background: #f7fafc;
            padding: 15px;
            text-align: left;
            font-weight: 700;
            color: #2d3748;
            border-bottom: 2px solid #e2e8f0;
            font-size: 14px;
            text-transform: uppercase;
            letter-spacing: 0.5px;
        }

        .pl-table td {
            padding: 15px;
            border-bottom: 1px solid #f7fafc;
            color: #4a5568;
            font-size: 15px;
        }

        .pl-table tr:hover {
            background: #f7fafc;
        }

        .pl-table .category-row {
            background: #edf2f7;
            font-weight: 700;
            color: #2d3748;
            font-size: 16px;
        }

        .pl-table .subcategory-row td {
            padding-left: 40px;
        }

        .pl-table .total-row {
            background: #667eea;
            color: white;
            font-weight: 700;
            font-size: 17px;
        }

        .pl-table .total-row:hover {
            background: #5a67d8;
        }

        .pl-table .grand-total-row {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            font-weight: 700;
            font-size: 18px;
        }

        .pl-table .amount {
            text-align: right;
            font-family: 'Courier New', monospace;
            font-weight: 600;
        }

        .comparison-section {
            background: white;
            border-radius: 15px;
            padding: 30px;
            box-shadow: 0 4px 20px rgba(0,0,0,0.08);
            margin-bottom: 30px;
        }

        .comparison-section h2 {
            margin: 0 0 25px 0;
            color: #2d3748;
            font-size: 22px;
            font-weight: 700;
        }

        .chart-container {
            position: relative;
            height: 400px;
            margin-bottom: 30px;
        }

        .metrics-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 20px;
            margin-top: 20px;
        }

        .metric-box {
            background: #f7fafc;
            border-radius: 10px;
            padding: 20px;
            text-align: center;
        }

        .metric-box .metric-label {
            font-size: 13px;
            color: #718096;
            margin-bottom: 8px;
            font-weight: 600;
            text-transform: uppercase;
        }

        .metric-box .metric-value {
            font-size: 24px;
            font-weight: 700;
            color: #2d3748;
        }

        @media print {
            .filter-panel,
            .pl-header .subtitle {
                display: none;
            }

            .pl-container {
                background: white;
                padding: 0;
            }

            .summary-card,
            .pl-statement,
            .comparison-section {
                box-shadow: none;
                page-break-inside: avoid;
            }
        }
    </style>

    <div class="pl-container">
        <!-- Header -->
        <div class="pl-header">
            <h1>📊 งบกำไรขาดทุน (Profit & Loss Statement)</h1>
            <div class="subtitle">วิเคราะห์รายได้-ค่าใช้จ่าย และกำไรสุทธิของธุรกิจ</div>
        </div>

        <!-- Filters -->
        <div class="filter-panel">
            <div>
                <label>ช่วงเวลา:</label>
                <asp:DropDownList ID="ddlPeriodType" runat="server" AutoPostBack="true" OnSelectedIndexChanged="ddlPeriodType_SelectedIndexChanged">
                    <asp:ListItem Value="MONTH" Selected="True">รายเดือน</asp:ListItem>
                    <asp:ListItem Value="QUARTER">รายไตรมาส</asp:ListItem>
                    <asp:ListItem Value="YEAR">รายปี</asp:ListItem>
                    <asp:ListItem Value="CUSTOM">กำหนดเอง</asp:ListItem>
                </asp:DropDownList>
            </div>

            <div id="divMonth" runat="server">
                <label>เดือน:</label>
                <asp:DropDownList ID="ddlMonth" runat="server" AutoPostBack="true" />
            </div>

            <div id="divYear" runat="server">
                <label>ปี:</label>
                <asp:DropDownList ID="ddlYear" runat="server" AutoPostBack="true" />
            </div>

            <div id="divQuarter" runat="server" style="display:none;">
                <label>ไตรมาส:</label>
                <asp:DropDownList ID="ddlQuarter" runat="server" AutoPostBack="true">
                    <asp:ListItem Value="Q1">Q1 (ม.ค.-มี.ค.)</asp:ListItem>
                    <asp:ListItem Value="Q2">Q2 (เม.ย.-มิ.ย.)</asp:ListItem>
                    <asp:ListItem Value="Q3">Q3 (ก.ค.-ก.ย.)</asp:ListItem>
                    <asp:ListItem Value="Q4">Q4 (ต.ค.-ธ.ค.)</asp:ListItem>
                </asp:DropDownList>
            </div>

            <div>
                <asp:Button ID="btnExport" runat="server" Text="📥 Export Excel" CssClass="btn-export" OnClick="btnExport_Click" />
            </div>

            <div style="margin-left: auto;">
                <asp:Button ID="btnRefresh" runat="server" Text="🔄 รีเฟรช" CssClass="btn-export" OnClick="btnRefresh_Click" />
            </div>
        </div>

        <!-- Summary Cards -->
        <div class="summary-cards">
            <!-- Total Revenue -->
            <div class="summary-card revenue">
                <div class="card-title">💰 รายได้รวม</div>
                <div class="card-value">฿<asp:Literal ID="litTotalRevenue" runat="server" Text="0" /></div>
                <div class="card-trend"><asp:Literal ID="litRevenueTrend" runat="server" /></div>
            </div>

            <!-- COGS -->
            <div class="summary-card cogs">
                <div class="card-title">📦 ต้นทุนขาย</div>
                <div class="card-value">฿<asp:Literal ID="litTotalCOGS" runat="server" Text="0" /></div>
                <div class="card-margin"><asp:Literal ID="litCOGSPercent" runat="server" /></div>
            </div>

            <!-- Gross Profit -->
            <div class="summary-card gross">
                <div class="card-title">📈 กำไรขั้นต้น</div>
                <div class="card-value">฿<asp:Literal ID="litGrossProfit" runat="server" Text="0" /></div>
                <div class="card-margin">Margin: <asp:Literal ID="litGrossMargin" runat="server" /></div>
            </div>

            <!-- Operating Expenses -->
            <div class="summary-card expenses">
                <div class="card-title">💸 ค่าใช้จ่าย</div>
                <div class="card-value">฿<asp:Literal ID="litTotalExpenses" runat="server" Text="0" /></div>
                <div class="card-margin"><asp:Literal ID="litExpensesPercent" runat="server" /></div>
            </div>

            <!-- Net Profit -->
            <div class="summary-card net">
                <div class="card-title">🎯 กำไรสุทธิ</div>
                <div class="card-value">฿<asp:Literal ID="litNetProfit" runat="server" Text="0" /></div>
                <div class="card-margin">Margin: <asp:Literal ID="litNetMargin" runat="server" /></div>
            </div>
        </div>

        <!-- Detailed P&L Statement -->
        <div class="pl-statement">
            <h2>งบกำไรขาดทุนโดยละเอียด</h2>
            <asp:Literal ID="litPeriodDisplay" runat="server" />

            <table class="pl-table">
                <thead>
                    <tr>
                        <th style="width: 60%;">รายการ</th>
                        <th style="width: 20%;" class="amount">จำนวนเงิน (฿)</th>
                        <th style="width: 20%;" class="amount">% ของรายได้</th>
                    </tr>
                </thead>
                <tbody>
                    <!-- Revenue Section -->
                    <tr class="category-row">
                        <td colspan="3">รายได้ (REVENUE)</td>
                    </tr>
                    <tr class="subcategory-row">
                        <td>รายได้จากห้องพัก</td>
                        <td class="amount"><asp:Literal ID="litAccomRevenue" runat="server" Text="0.00" /></td>
                        <td class="amount"><asp:Literal ID="litAccomRevenuePercent" runat="server" Text="0.00%" /></td>
                    </tr>
                    <tr class="subcategory-row">
                        <td>รายได้จากสินค้า</td>
                        <td class="amount"><asp:Literal ID="litProductRevenue" runat="server" Text="0.00" /></td>
                        <td class="amount"><asp:Literal ID="litProductRevenuePercent" runat="server" Text="0.00%" /></td>
                    </tr>
                    <tr class="subcategory-row">
                        <td>รายได้จากบริการ</td>
                        <td class="amount"><asp:Literal ID="litServiceRevenue" runat="server" Text="0.00" /></td>
                        <td class="amount"><asp:Literal ID="litServiceRevenuePercent" runat="server" Text="0.00%" /></td>
                    </tr>
                    <tr class="subcategory-row">
                        <td>รายได้อื่นๆ</td>
                        <td class="amount"><asp:Literal ID="litOtherRevenue" runat="server" Text="0.00" /></td>
                        <td class="amount"><asp:Literal ID="litOtherRevenuePercent" runat="server" Text="0.00%" /></td>
                    </tr>
                    <tr class="total-row">
                        <td>รายได้รวม</td>
                        <td class="amount"><asp:Literal ID="litTotalRevenueDetail" runat="server" Text="0.00" /></td>
                        <td class="amount">100.00%</td>
                    </tr>

                    <!-- COGS Section -->
                    <tr class="category-row">
                        <td colspan="3">ต้นทุนขาย (COST OF GOODS SOLD)</td>
                    </tr>
                    <tr class="subcategory-row">
                        <td>ต้นทุนสินค้า</td>
                        <td class="amount"><asp:Literal ID="litProductCOGS" runat="server" Text="0.00" /></td>
                        <td class="amount"><asp:Literal ID="litProductCOGSPercent" runat="server" Text="0.00%" /></td>
                    </tr>
                    <tr class="total-row">
                        <td>ต้นทุนขายรวม</td>
                        <td class="amount"><asp:Literal ID="litTotalCOGSDetail" runat="server" Text="0.00" /></td>
                        <td class="amount"><asp:Literal ID="litTotalCOGSPercentDetail" runat="server" Text="0.00%" /></td>
                    </tr>

                    <!-- Gross Profit -->
                    <tr class="grand-total-row">
                        <td>กำไรขั้นต้น (GROSS PROFIT)</td>
                        <td class="amount"><asp:Literal ID="litGrossProfitDetail" runat="server" Text="0.00" /></td>
                        <td class="amount"><asp:Literal ID="litGrossProfitPercent" runat="server" Text="0.00%" /></td>
                    </tr>

                    <!-- Operating Expenses -->
                    <tr class="category-row">
                        <td colspan="3">ค่าใช้จ่ายในการดำเนินงาน (OPERATING EXPENSES)</td>
                    </tr>
                    <tr class="subcategory-row">
                        <td>เงินเดือนพนักงาน</td>
                        <td class="amount"><asp:Literal ID="litSalaryExpense" runat="server" Text="0.00" /></td>
                        <td class="amount"><asp:Literal ID="litSalaryExpensePercent" runat="server" Text="0.00%" /></td>
                    </tr>
                    <tr class="subcategory-row">
                        <td>ค่าสาธารณูปโภค</td>
                        <td class="amount"><asp:Literal ID="litUtilityExpense" runat="server" Text="0.00" /></td>
                        <td class="amount"><asp:Literal ID="litUtilityExpensePercent" runat="server" Text="0.00%" /></td>
                    </tr>
                    <tr class="subcategory-row">
                        <td>ค่าซ่อมบำรุง</td>
                        <td class="amount"><asp:Literal ID="litMaintenanceExpense" runat="server" Text="0.00" /></td>
                        <td class="amount"><asp:Literal ID="litMaintenanceExpensePercent" runat="server" Text="0.00%" /></td>
                    </tr>
                    <tr class="subcategory-row">
                        <td>ค่าใช้จ่ายอื่นๆ</td>
                        <td class="amount"><asp:Literal ID="litOtherExpense" runat="server" Text="0.00" /></td>
                        <td class="amount"><asp:Literal ID="litOtherExpensePercent" runat="server" Text="0.00%" /></td>
                    </tr>
                    <tr class="total-row">
                        <td>ค่าใช้จ่ายรวม</td>
                        <td class="amount"><asp:Literal ID="litTotalExpensesDetail" runat="server" Text="0.00" /></td>
                        <td class="amount"><asp:Literal ID="litTotalExpensesPercent" runat="server" Text="0.00%" /></td>
                    </tr>

                    <!-- Net Profit -->
                    <tr class="grand-total-row">
                        <td>กำไรสุทธิ (NET PROFIT)</td>
                        <td class="amount"><asp:Literal ID="litNetProfitDetail" runat="server" Text="0.00" /></td>
                        <td class="amount"><asp:Literal ID="litNetProfitPercent" runat="server" Text="0.00%" /></td>
                    </tr>
                </tbody>
            </table>
        </div>

        <!-- Comparison Charts -->
        <div class="comparison-section">
            <h2>📊 การเปรียบเทียบ</h2>

            <div class="chart-container">
                <canvas id="plComparisonChart"></canvas>
            </div>

            <div class="metrics-grid">
                <div class="metric-box">
                    <div class="metric-label">Gross Margin</div>
                    <div class="metric-value"><asp:Literal ID="litGrossMarginMetric" runat="server" /></div>
                </div>
                <div class="metric-box">
                    <div class="metric-label">Net Margin</div>
                    <div class="metric-value"><asp:Literal ID="litNetMarginMetric" runat="server" /></div>
                </div>
                <div class="metric-box">
                    <div class="metric-label">COGS Ratio</div>
                    <div class="metric-value"><asp:Literal ID="litCOGSRatio" runat="server" /></div>
                </div>
                <div class="metric-box">
                    <div class="metric-label">OpEx Ratio</div>
                    <div class="metric-value"><asp:Literal ID="litOpExRatio" runat="server" /></div>
                </div>
            </div>
        </div>
    </div>

    <!-- Hidden Fields for Chart Data -->
    <asp:HiddenField ID="hfComparisonLabels" runat="server" />
    <asp:HiddenField ID="hfComparisonRevenue" runat="server" />
    <asp:HiddenField ID="hfComparisonExpenses" runat="server" />
    <asp:HiddenField ID="hfComparisonProfit" runat="server" />

    <!-- Chart.js -->
    <script src="https://cdn.jsdelivr.net/npm/chart.js"></script>
    <script type="text/javascript">
        window.addEventListener('load', function () {
            // Comparison Chart
            var labels = JSON.parse(document.getElementById('<%= hfComparisonLabels.ClientID %>').value || '[]');
            var revenueData = JSON.parse(document.getElementById('<%= hfComparisonRevenue.ClientID %>').value || '[]');
            var expensesData = JSON.parse(document.getElementById('<%= hfComparisonExpenses.ClientID %>').value || '[]');
            var profitData = JSON.parse(document.getElementById('<%= hfComparisonProfit.ClientID %>').value || '[]');

            var ctx = document.getElementById('plComparisonChart').getContext('2d');
            new Chart(ctx, {
                type: 'bar',
                data: {
                    labels: labels,
                    datasets: [{
                        label: 'รายได้',
                        data: revenueData,
                        backgroundColor: 'rgba(72, 187, 120, 0.8)',
                        borderColor: 'rgba(72, 187, 120, 1)',
                        borderWidth: 2
                    }, {
                        label: 'ค่าใช้จ่าย',
                        data: expensesData,
                        backgroundColor: 'rgba(245, 101, 101, 0.8)',
                        borderColor: 'rgba(245, 101, 101, 1)',
                        borderWidth: 2
                    }, {
                        label: 'กำไรสุทธิ',
                        data: profitData,
                        backgroundColor: 'rgba(159, 122, 234, 0.8)',
                        borderColor: 'rgba(159, 122, 234, 1)',
                        borderWidth: 2
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: {
                        legend: {
                            position: 'top',
                            labels: {
                                font: {
                                    size: 14,
                                    weight: 'bold'
                                },
                                padding: 15
                            }
                        },
                        title: {
                            display: true,
                            text: 'เปรียบเทียบรายได้ ค่าใช้จ่าย และกำไร',
                            font: {
                                size: 18,
                                weight: 'bold'
                            },
                            padding: 20
                        }
                    },
                    scales: {
                        y: {
                            beginAtZero: true,
                            ticks: {
                                callback: function (value) {
                                    return '฿' + value.toLocaleString();
                                }
                            }
                        }
                    }
                }
            });
        });
    </script>
</asp:Content>
