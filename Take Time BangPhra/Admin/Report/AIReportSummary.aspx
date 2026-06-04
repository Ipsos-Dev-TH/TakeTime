<%@ Page Title="AI Report Summary - สรุปรายงาน AI" Language="C#"
    MasterPageFile="~/Site.Master" AutoEventWireup="true"
    CodeBehind="AIReportSummary.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Report.AIReportSummary" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <style>
        /* ============================================
           AI REPORT SUMMARY STYLES
           ============================================ */
        .air-container {
            padding: 20px;
            background: #f5f0eb;
            min-height: 100vh;
            font-family: 'Prompt', sans-serif;
        }

        /* Page Header */
        .air-header {
            background: linear-gradient(135deg, #5D4037 0%, #7C4DFF 100%);
            padding: 30px;
            border-radius: 15px;
            color: white;
            margin-bottom: 30px;
            box-shadow: 0 10px 30px rgba(93, 64, 55, 0.3);
        }

        .air-header h1 {
            margin: 0;
            font-size: 2rem;
            font-weight: 700;
            display: flex;
            align-items: center;
            gap: 12px;
        }

        .air-header h1 i {
            font-size: 1.8rem;
        }

        .air-header p {
            margin: 8px 0 0 0;
            opacity: 0.9;
            font-size: 1rem;
        }

        /* Section Title */
        .section-title {
            font-size: 1.2rem;
            font-weight: 700;
            color: #5D4037;
            margin: 0 0 20px 0;
            display: flex;
            align-items: center;
            gap: 10px;
        }

        .section-title i {
            color: #7C4DFF;
            font-size: 1.3rem;
        }

        /* KPI Cards Grid */
        .kpi-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(170px, 1fr));
            gap: 16px;
            margin-bottom: 30px;
        }

        .kpi-card {
            background: white;
            border-radius: 12px;
            padding: 20px;
            box-shadow: 0 2px 10px rgba(0, 0, 0, 0.08);
            text-align: center;
            transition: transform 0.2s, box-shadow 0.2s;
        }

        .kpi-card:hover {
            transform: translateY(-3px);
            box-shadow: 0 6px 20px rgba(0, 0, 0, 0.12);
        }

        .kpi-icon {
            width: 48px;
            height: 48px;
            border-radius: 12px;
            display: flex;
            align-items: center;
            justify-content: center;
            margin: 0 auto 12px auto;
            font-size: 1.3rem;
        }

        .kpi-icon.blue { background: #E3F2FD; color: #1565C0; }
        .kpi-icon.green { background: #E8F5E9; color: #2E7D32; }
        .kpi-icon.orange { background: #FFF3E0; color: #E65100; }
        .kpi-icon.red { background: #FFEBEE; color: #C62828; }
        .kpi-icon.purple { background: #EDE7F6; color: #7C4DFF; }
        .kpi-icon.teal { background: #E0F2F1; color: #00695C; }

        .kpi-value {
            font-size: 1.8rem;
            font-weight: 700;
            color: #333;
            margin-bottom: 4px;
        }

        .kpi-label {
            font-size: 0.85rem;
            color: #888;
            font-weight: 500;
        }

        /* Month Running Cards */
        .month-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
            gap: 16px;
            margin-bottom: 30px;
        }

        .month-card {
            background: white;
            border-radius: 12px;
            padding: 24px;
            box-shadow: 0 2px 10px rgba(0, 0, 0, 0.08);
            display: flex;
            align-items: center;
            gap: 16px;
        }

        .month-card .month-icon {
            width: 56px;
            height: 56px;
            border-radius: 14px;
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 1.5rem;
            flex-shrink: 0;
        }

        .month-card .month-icon.revenue { background: linear-gradient(135deg, #E8F5E9, #C8E6C9); color: #2E7D32; }
        .month-card .month-icon.bookings { background: linear-gradient(135deg, #E3F2FD, #BBDEFB); color: #1565C0; }
        .month-card .month-icon.occupancy { background: linear-gradient(135deg, #EDE7F6, #D1C4E9); color: #7C4DFF; }

        .month-info .month-value {
            font-size: 1.6rem;
            font-weight: 700;
            color: #333;
        }

        .month-info .month-label {
            font-size: 0.85rem;
            color: #888;
            margin-top: 2px;
        }

        /* Alerts Panel */
        .alerts-container {
            margin-bottom: 30px;
        }

        .alert-box {
            background: white;
            border-radius: 12px;
            padding: 16px 20px;
            margin-bottom: 10px;
            box-shadow: 0 2px 10px rgba(0, 0, 0, 0.08);
            display: flex;
            align-items: center;
            gap: 12px;
            border-left: 4px solid #FFC107;
        }

        .alert-box.warning { border-left-color: #FFC107; }
        .alert-box.danger { border-left-color: #F44336; }
        .alert-box.info { border-left-color: #2196F3; }
        .alert-box.success { border-left-color: #4CAF50; }

        .alert-box i {
            font-size: 1.2rem;
            flex-shrink: 0;
        }

        .alert-box.warning i { color: #F57F17; }
        .alert-box.danger i { color: #C62828; }
        .alert-box.info i { color: #1565C0; }
        .alert-box.success i { color: #2E7D32; }

        .alert-box .alert-text {
            font-size: 0.9rem;
            color: #333;
            font-weight: 500;
        }

        .no-alerts {
            background: white;
            border-radius: 12px;
            padding: 20px;
            box-shadow: 0 2px 10px rgba(0, 0, 0, 0.08);
            text-align: center;
            color: #4CAF50;
            font-weight: 500;
        }

        /* Quick Insights */
        .insights-card {
            background: white;
            border-radius: 12px;
            padding: 24px;
            box-shadow: 0 2px 10px rgba(0, 0, 0, 0.08);
            margin-bottom: 30px;
        }

        .insight-list {
            list-style: none;
            padding: 0;
            margin: 0;
        }

        .insight-list li {
            padding: 10px 0;
            border-bottom: 1px solid #f5f5f5;
            display: flex;
            align-items: center;
            gap: 10px;
            font-size: 0.95rem;
            color: #444;
        }

        .insight-list li:last-child {
            border-bottom: none;
        }

        .insight-list li i {
            color: #7C4DFF;
            font-size: 0.9rem;
            flex-shrink: 0;
        }

        /* AI Summary Generator */
        .generator-card {
            background: white;
            border-radius: 12px;
            padding: 28px;
            box-shadow: 0 2px 10px rgba(0, 0, 0, 0.08);
            margin-bottom: 30px;
        }

        .tab-buttons {
            display: flex;
            gap: 8px;
            margin-bottom: 20px;
            flex-wrap: wrap;
        }

        .tab-btn {
            padding: 10px 24px;
            border: 2px solid #5D4037;
            border-radius: 10px;
            background: white;
            color: #5D4037;
            font-weight: 600;
            font-size: 0.9rem;
            cursor: pointer;
            transition: all 0.3s;
            font-family: 'Prompt', sans-serif;
        }

        .tab-btn:hover {
            background: #EFEBE9;
        }

        .tab-btn.active {
            background: #5D4037;
            color: white;
        }

        .generator-form {
            display: flex;
            align-items: flex-end;
            gap: 12px;
            margin-bottom: 20px;
            flex-wrap: wrap;
        }

        .form-group {
            display: flex;
            flex-direction: column;
            gap: 6px;
        }

        .form-group label {
            font-size: 0.85rem;
            font-weight: 600;
            color: #5D4037;
        }

        .form-group input[type="date"],
        .form-group input[type="month"] {
            padding: 10px 14px;
            border: 2px solid #ddd;
            border-radius: 8px;
            font-size: 0.9rem;
            font-family: 'Prompt', sans-serif;
            min-width: 200px;
        }

        .form-group input:focus {
            outline: none;
            border-color: #7C4DFF;
        }

        .btn-generate {
            padding: 10px 28px;
            background: linear-gradient(135deg, #7C4DFF, #651FFF);
            color: white;
            border: none;
            border-radius: 10px;
            font-size: 0.9rem;
            font-weight: 600;
            cursor: pointer;
            font-family: 'Prompt', sans-serif;
            display: inline-flex;
            align-items: center;
            gap: 8px;
            transition: all 0.3s;
        }

        .btn-generate:hover {
            transform: translateY(-2px);
            box-shadow: 0 6px 20px rgba(124, 77, 255, 0.3);
        }

        .btn-generate:disabled {
            opacity: 0.6;
            cursor: not-allowed;
            transform: none;
            box-shadow: none;
        }

        /* Loading Spinner */
        .spinner {
            display: none;
            text-align: center;
            padding: 40px;
        }

        .spinner.show {
            display: block;
        }

        .spinner-icon {
            width: 40px;
            height: 40px;
            border: 4px solid #EDE7F6;
            border-top-color: #7C4DFF;
            border-radius: 50%;
            animation: spin 0.8s linear infinite;
            margin: 0 auto 12px auto;
        }

        @keyframes spin {
            to { transform: rotate(360deg); }
        }

        .spinner-text {
            font-size: 0.9rem;
            color: #7C4DFF;
            font-weight: 500;
        }

        /* Summary Result Display */
        .summary-result {
            display: none;
            background: #FAFAFA;
            border: 1px solid #E0E0E0;
            border-radius: 12px;
            padding: 28px;
            line-height: 1.8;
            font-size: 0.95rem;
            color: #333;
        }

        .summary-result.show {
            display: block;
        }

        .summary-result h2 {
            font-size: 1.3rem;
            color: #5D4037;
            margin: 24px 0 12px 0;
            border-bottom: 2px solid #EDE7F6;
            padding-bottom: 6px;
        }

        .summary-result h2:first-child {
            margin-top: 0;
        }

        .summary-result h3 {
            font-size: 1.1rem;
            color: #5D4037;
            margin: 18px 0 10px 0;
        }

        .summary-result strong {
            color: #5D4037;
        }

        .summary-result ul {
            padding-left: 24px;
            margin: 8px 0;
        }

        .summary-result ul li {
            margin-bottom: 6px;
        }

        .summary-result p {
            margin: 8px 0;
        }

        /* Summary History */
        .history-card {
            background: white;
            border-radius: 12px;
            padding: 28px;
            box-shadow: 0 2px 10px rgba(0, 0, 0, 0.08);
            margin-bottom: 30px;
        }

        .history-filters {
            display: flex;
            gap: 10px;
            margin-bottom: 20px;
            flex-wrap: wrap;
        }

        .history-filter-btn {
            padding: 8px 20px;
            border: 2px solid #ddd;
            border-radius: 8px;
            background: white;
            color: #666;
            font-weight: 500;
            font-size: 0.85rem;
            cursor: pointer;
            transition: all 0.3s;
            font-family: 'Prompt', sans-serif;
        }

        .history-filter-btn:hover {
            border-color: #7C4DFF;
            color: #7C4DFF;
        }

        .history-filter-btn.active {
            background: #7C4DFF;
            border-color: #7C4DFF;
            color: white;
        }

        .history-list {
            display: flex;
            flex-direction: column;
            gap: 12px;
        }

        .history-item {
            background: #FAFAFA;
            border: 1px solid #E0E0E0;
            border-radius: 10px;
            overflow: hidden;
            transition: box-shadow 0.2s;
        }

        .history-item:hover {
            box-shadow: 0 4px 15px rgba(0, 0, 0, 0.08);
        }

        .history-item-header {
            padding: 16px 20px;
            cursor: pointer;
            display: flex;
            justify-content: space-between;
            align-items: center;
        }

        .history-item-header:hover {
            background: #f5f0eb;
        }

        .history-meta {
            display: flex;
            align-items: center;
            gap: 12px;
            flex-wrap: wrap;
        }

        .history-type-badge {
            padding: 4px 12px;
            border-radius: 6px;
            font-size: 0.75rem;
            font-weight: 600;
            text-transform: uppercase;
        }

        .badge-daily { background: #E3F2FD; color: #1565C0; }
        .badge-weekly { background: #EDE7F6; color: #7C4DFF; }
        .badge-monthly { background: #FFF3E0; color: #E65100; }

        .history-period {
            font-size: 0.9rem;
            font-weight: 600;
            color: #333;
        }

        .history-date {
            font-size: 0.8rem;
            color: #999;
        }

        .history-preview {
            font-size: 0.85rem;
            color: #666;
            max-width: 400px;
            overflow: hidden;
            text-overflow: ellipsis;
            white-space: nowrap;
        }

        .history-expand-icon {
            color: #999;
            transition: transform 0.3s;
            flex-shrink: 0;
        }

        .history-item.expanded .history-expand-icon {
            transform: rotate(180deg);
        }

        .history-item-body {
            display: none;
            padding: 0 20px 20px 20px;
            border-top: 1px solid #E0E0E0;
        }

        .history-item.expanded .history-item-body {
            display: block;
        }

        .history-summary-content {
            background: white;
            border-radius: 8px;
            padding: 20px;
            margin-top: 12px;
            line-height: 1.8;
            font-size: 0.9rem;
            color: #333;
        }

        .history-summary-content h2 {
            font-size: 1.2rem;
            color: #5D4037;
            margin: 18px 0 8px 0;
            border-bottom: 2px solid #EDE7F6;
            padding-bottom: 4px;
        }

        .history-summary-content h2:first-child {
            margin-top: 0;
        }

        .history-summary-content h3 {
            font-size: 1rem;
            color: #5D4037;
            margin: 14px 0 8px 0;
        }

        .history-summary-content strong {
            color: #5D4037;
        }

        .history-summary-content ul {
            padding-left: 24px;
            margin: 6px 0;
        }

        .history-summary-content ul li {
            margin-bottom: 4px;
        }

        .history-summary-content p {
            margin: 6px 0;
        }

        .history-tokens {
            font-size: 0.8rem;
            color: #999;
            margin-top: 10px;
            text-align: right;
        }

        .empty-history {
            text-align: center;
            padding: 40px;
            color: #999;
            font-size: 0.95rem;
        }

        .empty-history i {
            font-size: 2.5rem;
            display: block;
            margin-bottom: 12px;
            color: #ddd;
        }

        /* Error display */
        .error-box {
            background: #FFEBEE;
            border: 1px solid #FFCDD2;
            border-radius: 10px;
            padding: 16px 20px;
            color: #C62828;
            font-size: 0.9rem;
            display: none;
        }

        .error-box.show {
            display: block;
        }

        /* Loading skeleton */
        .skeleton {
            background: linear-gradient(90deg, #f0f0f0 25%, #e0e0e0 50%, #f0f0f0 75%);
            background-size: 200% 100%;
            animation: shimmer 1.5s infinite;
            border-radius: 8px;
        }

        @keyframes shimmer {
            0% { background-position: 200% 0; }
            100% { background-position: -200% 0; }
        }

        .skeleton-kpi {
            height: 120px;
        }

        /* Two column layout */
        .two-col {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 20px;
            margin-bottom: 30px;
        }

        @media (max-width: 768px) {
            .air-container { padding: 12px; }
            .air-header { padding: 20px; }
            .air-header h1 { font-size: 1.4rem; }
            .kpi-grid { grid-template-columns: repeat(auto-fit, minmax(140px, 1fr)); gap: 10px; }
            .kpi-value { font-size: 1.4rem; }
            .month-grid { grid-template-columns: 1fr; }
            .generator-form { flex-direction: column; align-items: stretch; }
            .two-col { grid-template-columns: 1fr; }
            .history-preview { display: none; }
        }
    </style>

    <div class="air-container">

        <!-- Page Header -->
        <div class="air-header">
            <h1><i class="fas fa-robot"></i> AI Report Summary</h1>
            <p>สรุปรายงานอัจฉริยะ - วิเคราะห์ข้อมูลโรงแรมด้วย AI</p>
        </div>

        <!-- Today's KPIs Dashboard -->
        <h3 class="section-title"><i class="fas fa-chart-line"></i> KPI วันนี้</h3>
        <div class="kpi-grid" id="kpiGrid">
            <div class="kpi-card skeleton skeleton-kpi"></div>
            <div class="kpi-card skeleton skeleton-kpi"></div>
            <div class="kpi-card skeleton skeleton-kpi"></div>
            <div class="kpi-card skeleton skeleton-kpi"></div>
            <div class="kpi-card skeleton skeleton-kpi"></div>
            <div class="kpi-card skeleton skeleton-kpi"></div>
        </div>

        <!-- Month Running Totals -->
        <h3 class="section-title"><i class="fas fa-calendar-alt"></i> สรุปเดือนนี้ (สะสม)</h3>
        <div class="month-grid" id="monthGrid">
            <div class="month-card skeleton" style="height:80px;"></div>
            <div class="month-card skeleton" style="height:80px;"></div>
            <div class="month-card skeleton" style="height:80px;"></div>
        </div>

        <!-- Alerts & Quick Insights -->
        <div class="two-col">
            <!-- Alerts Panel -->
            <div>
                <h3 class="section-title"><i class="fas fa-exclamation-triangle"></i> การแจ้งเตือน</h3>
                <div class="alerts-container" id="alertsContainer">
                    <div class="no-alerts"><i class="fas fa-check-circle"></i> กำลังโหลด...</div>
                </div>
            </div>

            <!-- Quick Insights -->
            <div>
                <h3 class="section-title"><i class="fas fa-lightbulb"></i> Quick Insights</h3>
                <div class="insights-card" id="insightsCard">
                    <ul class="insight-list" id="insightList">
                        <li><i class="fas fa-spinner fa-spin"></i> กำลังโหลดข้อมูล...</li>
                    </ul>
                </div>
            </div>
        </div>

        <!-- AI Summary Generator -->
        <h3 class="section-title"><i class="fas fa-magic"></i> สร้างสรุป AI</h3>
        <div class="generator-card">

            <!-- Tabs -->
            <div class="tab-buttons">
                <button class="tab-btn active" onclick="switchTab('daily')" id="tabDaily">
                    <i class="fas fa-sun"></i> สรุปรายวัน
                </button>
                <button class="tab-btn" onclick="switchTab('weekly')" id="tabWeekly">
                    <i class="fas fa-calendar-week"></i> สรุปรายสัปดาห์
                </button>
                <button class="tab-btn" onclick="switchTab('monthly')" id="tabMonthly">
                    <i class="fas fa-calendar"></i> สรุปรายเดือน
                </button>
            </div>

            <!-- Daily form -->
            <div class="generator-form" id="formDaily">
                <div class="form-group">
                    <label>เลือกวันที่</label>
                    <input type="date" id="dailyDate" />
                </div>
                <button class="btn-generate" onclick="generateSummary('daily')" id="btnGenerateDaily">
                    <i class="fas fa-wand-magic-sparkles"></i> สร้างสรุป AI
                </button>
            </div>

            <!-- Weekly form -->
            <div class="generator-form" id="formWeekly" style="display:none;">
                <div class="form-group">
                    <label>เลือกวันเริ่มต้นสัปดาห์</label>
                    <input type="date" id="weeklyDate" />
                </div>
                <button class="btn-generate" onclick="generateSummary('weekly')" id="btnGenerateWeekly">
                    <i class="fas fa-wand-magic-sparkles"></i> สร้างสรุป AI
                </button>
            </div>

            <!-- Monthly form -->
            <div class="generator-form" id="formMonthly" style="display:none;">
                <div class="form-group">
                    <label>เลือกเดือน</label>
                    <input type="month" id="monthlyDate" />
                </div>
                <button class="btn-generate" onclick="generateSummary('monthly')" id="btnGenerateMonthly">
                    <i class="fas fa-wand-magic-sparkles"></i> สร้างสรุป AI
                </button>
            </div>

            <!-- Loading -->
            <div class="spinner" id="generateSpinner">
                <div class="spinner-icon"></div>
                <div class="spinner-text">กำลังสร้างสรุป AI... กรุณารอสักครู่</div>
            </div>

            <!-- Error -->
            <div class="error-box" id="generateError"></div>

            <!-- Result -->
            <div class="summary-result" id="summaryResult"></div>
        </div>

        <!-- Summary History -->
        <h3 class="section-title"><i class="fas fa-history"></i> ประวัติสรุปรายงาน</h3>
        <div class="history-card">

            <!-- History Filters -->
            <div class="history-filters">
                <button class="history-filter-btn active" onclick="filterHistory('DAILY')" id="histDaily">
                    รายวัน
                </button>
                <button class="history-filter-btn" onclick="filterHistory('WEEKLY')" id="histWeekly">
                    รายสัปดาห์
                </button>
                <button class="history-filter-btn" onclick="filterHistory('MONTHLY')" id="histMonthly">
                    รายเดือน
                </button>
            </div>

            <!-- History List -->
            <div class="history-list" id="historyList">
                <div class="empty-history">
                    <i class="fas fa-spinner fa-spin"></i>
                    กำลังโหลดประวัติ...
                </div>
            </div>
        </div>

    </div>

    <script>
        // ============================================
        // AI REPORT SUMMARY - JavaScript
        // ============================================

        var currentTab = 'daily';
        var currentHistoryFilter = 'DAILY';

        // Initialize on page load
        document.addEventListener('DOMContentLoaded', function () {
            initDates();
            loadQuickInsights();
            loadHistory('DAILY');
        });

        // Set default date values
        function initDates() {
            var today = new Date();
            var todayStr = formatDateISO(today);
            document.getElementById('dailyDate').value = todayStr;

            // Set weekly date to last Monday
            var monday = new Date(today);
            monday.setDate(today.getDate() - today.getDay() + (today.getDay() === 0 ? -6 : 1));
            document.getElementById('weeklyDate').value = formatDateISO(monday);

            // Set monthly date to current month
            document.getElementById('monthlyDate').value = today.getFullYear() + '-' + String(today.getMonth() + 1).padStart(2, '0');
        }

        function formatDateISO(d) {
            return d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0');
        }

        // ============================================
        // API Calls
        // ============================================

        function apiCall(action, data) {
            return fetch('?action=' + action, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(data || {})
            }).then(function (r) { return r.json(); });
        }

        // ============================================
        // Quick Insights & KPIs
        // ============================================

        function loadQuickInsights() {
            apiCall('getQuickInsights', {})
                .then(function (data) {
                    if (data.success) {
                        renderKPIs(data.data.todayKPIs || {});
                        renderMonthTotals(data.data.monthRunningTotals || {});
                        renderAlerts(data.data.alerts || []);
                        renderInsights(data.data.quickInsightLines || []);
                    } else {
                        showKPIError(data.message || 'ไม่สามารถโหลดข้อมูลได้');
                    }
                })
                .catch(function (err) {
                    showKPIError('เกิดข้อผิดพลาดในการเชื่อมต่อ');
                });
        }

        function renderKPIs(kpis) {
            var cards = [
                { key: 'checkinsToday', label: 'เช็คอินวันนี้', icon: 'fas fa-sign-in-alt', color: 'blue', format: 'number' },
                { key: 'checkoutsToday', label: 'เช็คเอาท์วันนี้', icon: 'fas fa-sign-out-alt', color: 'green', format: 'number' },
                { key: 'newBookings', label: 'การจองใหม่', icon: 'fas fa-plus-circle', color: 'purple', format: 'number' },
                { key: 'cancellations', label: 'ยกเลิก', icon: 'fas fa-times-circle', color: 'red', format: 'number' },
                { key: 'occupancyRate', label: 'อัตราการเข้าพัก', icon: 'fas fa-bed', color: 'teal', format: 'percent' },
                { key: 'revenue', label: 'รายได้วันนี้', icon: 'fas fa-coins', color: 'orange', format: 'money' }
            ];

            var html = '';
            for (var i = 0; i < cards.length; i++) {
                var c = cards[i];
                var val = kpis[c.key] !== undefined ? kpis[c.key] : 0;
                var displayVal = '';

                if (c.format === 'money') {
                    displayVal = formatMoney(val);
                } else if (c.format === 'percent') {
                    displayVal = formatPercent(val);
                } else {
                    displayVal = formatNumber(val);
                }

                html += '<div class="kpi-card">' +
                    '<div class="kpi-icon ' + c.color + '"><i class="' + c.icon + '"></i></div>' +
                    '<div class="kpi-value">' + displayVal + '</div>' +
                    '<div class="kpi-label">' + c.label + '</div>' +
                    '</div>';
            }

            document.getElementById('kpiGrid').innerHTML = html;
        }

        function renderMonthTotals(data) {
            var totalRevenue = data.totalRevenue || 0;
            var totalBookings = data.totalReservations || 0;
            var avgOccupancy = data.avgOccupancyRate || 0;

            var html =
                '<div class="month-card">' +
                    '<div class="month-icon revenue"><i class="fas fa-coins"></i></div>' +
                    '<div class="month-info">' +
                        '<div class="month-value">' + formatMoney(totalRevenue) + '</div>' +
                        '<div class="month-label">รายได้รวมเดือนนี้</div>' +
                    '</div>' +
                '</div>' +
                '<div class="month-card">' +
                    '<div class="month-icon bookings"><i class="fas fa-bookmark"></i></div>' +
                    '<div class="month-info">' +
                        '<div class="month-value">' + formatNumber(totalBookings) + '</div>' +
                        '<div class="month-label">จำนวนการจอง</div>' +
                    '</div>' +
                '</div>' +
                '<div class="month-card">' +
                    '<div class="month-icon occupancy"><i class="fas fa-chart-pie"></i></div>' +
                    '<div class="month-info">' +
                        '<div class="month-value">' + formatPercent(avgOccupancy) + '</div>' +
                        '<div class="month-label">Occupancy เฉลี่ย</div>' +
                    '</div>' +
                '</div>';

            document.getElementById('monthGrid').innerHTML = html;
        }

        function renderAlerts(alerts) {
            var container = document.getElementById('alertsContainer');

            if (!alerts || alerts.length === 0) {
                container.innerHTML = '<div class="no-alerts"><i class="fas fa-check-circle"></i> ไม่มีการแจ้งเตือนในขณะนี้</div>';
                return;
            }

            var html = '';
            for (var i = 0; i < alerts.length; i++) {
                var alertText = alerts[i];
                var severity = classifyAlertSeverity(alertText);
                var icon = severity === 'danger' ? 'fas fa-exclamation-circle' :
                           severity === 'info' ? 'fas fa-info-circle' :
                           'fas fa-exclamation-triangle';

                html += '<div class="alert-box ' + severity + '">' +
                    '<i class="' + icon + '"></i>' +
                    '<span class="alert-text">' + escapeHtml(alertText) + '</span>' +
                    '</div>';
            }

            container.innerHTML = html;
        }

        function classifyAlertSeverity(text) {
            if (!text) return 'warning';
            var lower = text.toLowerCase();
            if (lower.indexOf('ลดลง') !== -1 || lower.indexOf('สูงมาก') !== -1 || lower.indexOf('สูง (') !== -1)
                return 'danger';
            if (lower.indexOf('ต่ำกว่า') !== -1 || lower.indexOf('ค้าง') !== -1 || lower.indexOf('ยกเลิก') !== -1)
                return 'warning';
            return 'info';
        }

        function renderInsights(lines) {
            var list = document.getElementById('insightList');

            if (!lines || lines.length === 0) {
                list.innerHTML = '<li><i class="fas fa-minus-circle"></i> ไม่มีข้อมูล insight ในขณะนี้</li>';
                return;
            }

            var html = '';
            for (var i = 0; i < lines.length; i++) {
                html += '<li><i class="fas fa-chevron-right"></i> ' + escapeHtml(lines[i]) + '</li>';
            }

            list.innerHTML = html;
        }

        function showKPIError(msg) {
            document.getElementById('kpiGrid').innerHTML =
                '<div style="grid-column:1/-1;text-align:center;color:#C62828;padding:20px;">' +
                '<i class="fas fa-exclamation-circle"></i> ' + escapeHtml(msg) + '</div>';
        }

        // ============================================
        // AI Summary Generator
        // ============================================

        function switchTab(tab) {
            currentTab = tab;

            // Update tab buttons
            document.getElementById('tabDaily').classList.toggle('active', tab === 'daily');
            document.getElementById('tabWeekly').classList.toggle('active', tab === 'weekly');
            document.getElementById('tabMonthly').classList.toggle('active', tab === 'monthly');

            // Show/hide forms
            document.getElementById('formDaily').style.display = tab === 'daily' ? 'flex' : 'none';
            document.getElementById('formWeekly').style.display = tab === 'weekly' ? 'flex' : 'none';
            document.getElementById('formMonthly').style.display = tab === 'monthly' ? 'flex' : 'none';

            // Clear results
            document.getElementById('summaryResult').classList.remove('show');
            document.getElementById('generateError').classList.remove('show');
            document.getElementById('generateSpinner').classList.remove('show');
        }

        function generateSummary(type) {
            var dateVal = '';
            var action = '';
            var btnId = '';

            if (type === 'daily') {
                dateVal = document.getElementById('dailyDate').value;
                action = 'generateDaily';
                btnId = 'btnGenerateDaily';
            } else if (type === 'weekly') {
                dateVal = document.getElementById('weeklyDate').value;
                action = 'generateWeekly';
                btnId = 'btnGenerateWeekly';
            } else if (type === 'monthly') {
                var monthVal = document.getElementById('monthlyDate').value;
                // Convert YYYY-MM to YYYY-MM-01
                dateVal = monthVal + '-01';
                action = 'generateMonthly';
                btnId = 'btnGenerateMonthly';
            }

            if (!dateVal) {
                alert('กรุณาเลือกวันที่');
                return;
            }

            // Show loading
            var btn = document.getElementById(btnId);
            btn.disabled = true;
            document.getElementById('generateSpinner').classList.add('show');
            document.getElementById('summaryResult').classList.remove('show');
            document.getElementById('generateError').classList.remove('show');

            apiCall(action, { date: dateVal })
                .then(function (data) {
                    document.getElementById('generateSpinner').classList.remove('show');
                    btn.disabled = false;

                    if (data.success) {
                        var resultDiv = document.getElementById('summaryResult');
                        resultDiv.innerHTML = markdownToHtml(data.data || '');
                        resultDiv.classList.add('show');

                        // Refresh history
                        var histType = type === 'daily' ? 'DAILY' : type === 'weekly' ? 'WEEKLY' : 'MONTHLY';
                        loadHistory(histType);
                    } else {
                        var errDiv = document.getElementById('generateError');
                        errDiv.textContent = data.message || 'เกิดข้อผิดพลาดในการสร้างสรุป';
                        errDiv.classList.add('show');
                    }
                })
                .catch(function (err) {
                    document.getElementById('generateSpinner').classList.remove('show');
                    btn.disabled = false;
                    var errDiv = document.getElementById('generateError');
                    errDiv.textContent = 'เกิดข้อผิดพลาดในการเชื่อมต่อ';
                    errDiv.classList.add('show');
                });
        }

        // ============================================
        // Summary History
        // ============================================

        function filterHistory(type) {
            currentHistoryFilter = type;

            // Update buttons
            document.getElementById('histDaily').classList.toggle('active', type === 'DAILY');
            document.getElementById('histWeekly').classList.toggle('active', type === 'WEEKLY');
            document.getElementById('histMonthly').classList.toggle('active', type === 'MONTHLY');

            loadHistory(type);
        }

        function loadHistory(reportType) {
            var listEl = document.getElementById('historyList');
            listEl.innerHTML = '<div class="empty-history"><i class="fas fa-spinner fa-spin"></i> กำลังโหลดประวัติ...</div>';

            apiCall('getHistory', { reportType: reportType, limit: 20 })
                .then(function (data) {
                    if (data.success && data.data && data.data.length > 0) {
                        renderHistory(data.data);
                    } else {
                        listEl.innerHTML = '<div class="empty-history"><i class="fas fa-folder-open"></i> ยังไม่มีประวัติสรุปรายงานประเภทนี้</div>';
                    }
                })
                .catch(function () {
                    listEl.innerHTML = '<div class="empty-history"><i class="fas fa-exclamation-circle"></i> ไม่สามารถโหลดประวัติได้</div>';
                });
        }

        function renderHistory(items) {
            var html = '';

            for (var i = 0; i < items.length; i++) {
                var item = items[i];
                var typeLabel = item.ReportType || item.PeriodType || 'DAILY';
                var badgeClass = typeLabel === 'DAILY' ? 'badge-daily' :
                                 typeLabel === 'WEEKLY' ? 'badge-weekly' : 'badge-monthly';
                var typeText = typeLabel === 'DAILY' ? 'รายวัน' :
                               typeLabel === 'WEEKLY' ? 'รายสัปดาห์' : 'รายเดือน';

                var periodStart = item.PeriodStart ? formatThaiDate(item.PeriodStart) : '-';
                var periodEnd = item.PeriodEnd ? formatThaiDate(item.PeriodEnd) : '-';
                var periodDisplay = periodStart === periodEnd ? periodStart : periodStart + ' - ' + periodEnd;
                var createdDate = item.Created_Date ? formatThaiDateTime(item.Created_Date) : '-';
                var summary = item.AISummary || item.Summary || '';
                var preview = stripHtmlAndTrim(summary, 80);
                var tokens = item.TokensUsed || 0;

                html += '<div class="history-item" id="hist_' + i + '">' +
                    '<div class="history-item-header" onclick="toggleHistoryItem(' + i + ')">' +
                        '<div class="history-meta">' +
                            '<span class="history-type-badge ' + badgeClass + '">' + typeText + '</span>' +
                            '<span class="history-period">' + escapeHtml(periodDisplay) + '</span>' +
                            '<span class="history-date">สร้างเมื่อ: ' + escapeHtml(createdDate) + '</span>' +
                        '</div>' +
                        '<div style="display:flex;align-items:center;gap:12px;">' +
                            '<span class="history-preview">' + escapeHtml(preview) + '</span>' +
                            '<i class="fas fa-chevron-down history-expand-icon"></i>' +
                        '</div>' +
                    '</div>' +
                    '<div class="history-item-body">' +
                        '<div class="history-summary-content">' + markdownToHtml(summary) + '</div>' +
                        (tokens > 0 ? '<div class="history-tokens">Tokens used: ' + formatNumber(tokens) + '</div>' : '') +
                    '</div>' +
                '</div>';
            }

            document.getElementById('historyList').innerHTML = html;
        }

        function toggleHistoryItem(index) {
            var item = document.getElementById('hist_' + index);
            if (item) {
                item.classList.toggle('expanded');
            }
        }

        // ============================================
        // Formatting Helpers
        // ============================================

        function formatMoney(val) {
            var num = parseFloat(val) || 0;
            return '฿' + num.toLocaleString('th-TH', { minimumFractionDigits: 0, maximumFractionDigits: 0 });
        }

        function formatPercent(val) {
            var num = parseFloat(val) || 0;
            return num.toFixed(1) + '%';
        }

        function formatNumber(val) {
            var num = parseInt(val) || 0;
            return num.toLocaleString('th-TH');
        }

        function formatThaiDate(dateStr) {
            if (!dateStr) return '-';
            try {
                var d = new Date(dateStr);
                if (isNaN(d.getTime())) return dateStr;
                return String(d.getDate()).padStart(2, '0') + '/' +
                       String(d.getMonth() + 1).padStart(2, '0') + '/' +
                       (d.getFullYear() + 543);
            } catch (e) {
                return dateStr;
            }
        }

        function formatThaiDateTime(dateStr) {
            if (!dateStr) return '-';
            try {
                var d = new Date(dateStr);
                if (isNaN(d.getTime())) return dateStr;
                return String(d.getDate()).padStart(2, '0') + '/' +
                       String(d.getMonth() + 1).padStart(2, '0') + '/' +
                       (d.getFullYear() + 543) + ' ' +
                       String(d.getHours()).padStart(2, '0') + ':' +
                       String(d.getMinutes()).padStart(2, '0');
            } catch (e) {
                return dateStr;
            }
        }

        function escapeHtml(text) {
            if (!text) return '';
            var div = document.createElement('div');
            div.textContent = text;
            return div.innerHTML;
        }

        function stripHtmlAndTrim(text, maxLen) {
            if (!text) return '';
            // Remove markdown-style formatting
            var clean = text.replace(/[#*_\-]/g, ' ').replace(/\s+/g, ' ').trim();
            if (clean.length > maxLen) {
                clean = clean.substring(0, maxLen) + '...';
            }
            return clean;
        }

        // Simple markdown-to-HTML converter
        function markdownToHtml(text) {
            if (!text) return '';

            // Escape HTML first
            var html = text
                .replace(/&/g, '&amp;')
                .replace(/</g, '&lt;')
                .replace(/>/g, '&gt;');

            // Headers: ## and ###
            html = html.replace(/^###\s+(.+)$/gm, '<h3>$1</h3>');
            html = html.replace(/^##\s+(.+)$/gm, '<h2>$1</h2>');

            // Bold: **text**
            html = html.replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>');

            // Italic: *text*
            html = html.replace(/\*(.+?)\*/g, '<em>$1</em>');

            // Bullet points: - item or * item
            // Group consecutive bullet lines into <ul>
            html = html.replace(/((?:^[\s]*[-*]\s+.+\n?)+)/gm, function (match) {
                var items = match.trim().split('\n');
                var ul = '<ul>';
                for (var i = 0; i < items.length; i++) {
                    var item = items[i].replace(/^[\s]*[-*]\s+/, '').trim();
                    if (item) {
                        ul += '<li>' + item + '</li>';
                    }
                }
                ul += '</ul>';
                return ul;
            });

            // Numbered list: 1. item
            html = html.replace(/((?:^\d+\.\s+.+\n?)+)/gm, function (match) {
                var items = match.trim().split('\n');
                var ol = '<ol>';
                for (var i = 0; i < items.length; i++) {
                    var item = items[i].replace(/^\d+\.\s+/, '').trim();
                    if (item) {
                        ol += '<li>' + item + '</li>';
                    }
                }
                ol += '</ol>';
                return ol;
            });

            // Line breaks for remaining lines (but not inside tags)
            html = html.replace(/\n\n/g, '</p><p>');
            html = html.replace(/\n/g, '<br/>');

            // Wrap in paragraph if not already wrapped
            if (html && !html.startsWith('<')) {
                html = '<p>' + html + '</p>';
            }

            return html;
        }
    </script>

</asp:Content>
