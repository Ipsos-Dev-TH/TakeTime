<%@ Page Title="AI Review Dashboard" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="AIReviewDashboard.aspx.cs" Inherits="Take_Time_BangPhra.Admin.CRM.AIReviewDashboard" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
<style>
    /* ===== Base ===== */
    @import url('https://fonts.googleapis.com/css2?family=Prompt:wght@300;400;500;600;700&display=swap');

    .ai-review-dash { max-width:1600px; margin:20px auto; padding:20px; font-family:'Prompt',sans-serif; }

    /* ===== Header ===== */
    .page-header { background:linear-gradient(135deg,#5D4037 0%,#7C4DFF 100%); color:#fff; padding:30px; border-radius:12px; margin-bottom:30px; box-shadow:0 4px 12px rgba(0,0,0,.15); }
    .page-header h1 { margin:0 0 8px 0; font-size:30px; font-weight:700; }
    .page-header p { margin:0; opacity:.85; font-size:14px; }

    /* ===== Stats row ===== */
    .stats-row { display:grid; grid-template-columns:repeat(auto-fit,minmax(180px,1fr)); gap:16px; margin-bottom:28px; }
    .stat-card { background:#fff; padding:20px; border-radius:12px; box-shadow:0 2px 8px rgba(0,0,0,.08); text-align:center; border-top:4px solid #5D4037; transition:transform .2s; }
    .stat-card:hover { transform:translateY(-3px); box-shadow:0 4px 14px rgba(0,0,0,.12); }
    .stat-card h4 { margin:0 0 8px; font-size:13px; color:#888; text-transform:uppercase; letter-spacing:.5px; }
    .stat-card .val { font-size:32px; font-weight:700; color:#5D4037; }
    .stat-card .val.accent { color:#7C4DFF; }
    .stat-card .sub { font-size:12px; color:#999; margin-top:4px; }
    .star-display { color:#FF9800; font-size:20px; }

    /* ===== Cards ===== */
    .card { background:#fff; border-radius:12px; box-shadow:0 2px 8px rgba(0,0,0,.08); padding:24px; margin-bottom:24px; }
    .card h3 { margin:0 0 16px; font-size:18px; font-weight:600; color:#5D4037; border-bottom:2px solid #f0ece8; padding-bottom:10px; }

    /* ===== Grid layouts ===== */
    .grid-2 { display:grid; grid-template-columns:1fr 1fr; gap:24px; }
    .grid-3 { display:grid; grid-template-columns:1fr 1fr 1fr; gap:24px; }
    @media(max-width:1024px){ .grid-2,.grid-3 { grid-template-columns:1fr; } }

    /* ===== Sentiment bar ===== */
    .sentiment-bar-wrap { margin-bottom:12px; }
    .sentiment-bar-wrap .label-row { display:flex; justify-content:space-between; font-size:13px; margin-bottom:4px; }
    .sentiment-bar-wrap .label-row .lbl { font-weight:500; }
    .bar-bg { height:22px; background:#f0f0f0; border-radius:11px; overflow:hidden; }
    .bar-fill { height:100%; border-radius:11px; transition:width .6s ease; }
    .bar-positive { background:#4CAF50; }
    .bar-negative { background:#F44336; }
    .bar-neutral  { background:#9E9E9E; }
    .bar-mixed    { background:#FF9800; }

    /* ===== Stacked horizontal chart ===== */
    .stacked-bar { display:flex; height:32px; border-radius:16px; overflow:hidden; margin:12px 0; }
    .stacked-bar .seg { display:flex; align-items:center; justify-content:center; color:#fff; font-size:11px; font-weight:600; min-width:24px; transition:width .6s ease; }
    .legend-row { display:flex; gap:16px; flex-wrap:wrap; margin-top:8px; }
    .legend-item { display:flex; align-items:center; gap:6px; font-size:12px; }
    .legend-dot { width:12px; height:12px; border-radius:50%; }

    /* ===== Source breakdown ===== */
    .source-item { display:flex; align-items:center; justify-content:space-between; padding:10px 0; border-bottom:1px solid #f5f5f5; }
    .source-item:last-child { border-bottom:none; }
    .source-badge { padding:4px 12px; border-radius:16px; font-size:12px; font-weight:600; color:#fff; }
    .source-badge.INTERNAL { background:#5D4037; }
    .source-badge.GOOGLE   { background:#4285F4; }
    .source-badge.FACEBOOK { background:#1877F2; }
    .source-badge.TRIPADVISOR { background:#34E0A1; color:#333; }
    .source-badge.AGODA   { background:#5E2590; }
    .source-badge.BOOKING { background:#003580; }
    .source-info { display:flex; align-items:center; gap:12px; }
    .source-stats { text-align:right; }
    .source-count { font-size:18px; font-weight:700; color:#5D4037; }
    .source-avg { font-size:12px; color:#888; }

    /* ===== Rating distribution ===== */
    .rating-row { display:flex; align-items:center; gap:10px; margin-bottom:8px; }
    .rating-label { width:50px; text-align:right; font-size:14px; font-weight:500; color:#555; white-space:nowrap; }
    .rating-bar-bg { flex:1; height:18px; background:#f0f0f0; border-radius:9px; overflow:hidden; }
    .rating-bar-fill { height:100%; background:linear-gradient(90deg,#FF9800,#FF5722); border-radius:9px; transition:width .6s ease; }
    .rating-count { width:40px; text-align:left; font-size:13px; color:#888; }

    /* ===== Topic pills ===== */
    .topic-cloud { display:flex; flex-wrap:wrap; gap:8px; }
    .topic-pill { padding:6px 14px; border-radius:20px; background:#f3e5f5; color:#7C4DFF; font-size:13px; font-weight:500; display:flex; align-items:center; gap:6px; }
    .topic-pill .cnt { background:#7C4DFF; color:#fff; border-radius:10px; padding:2px 8px; font-size:11px; font-weight:700; }

    /* ===== Monthly trend ===== */
    .trend-table { width:100%; border-collapse:collapse; font-size:13px; }
    .trend-table th { background:#f5f0eb; color:#5D4037; font-weight:600; padding:10px 12px; text-align:left; }
    .trend-table td { padding:8px 12px; border-bottom:1px solid #f0f0f0; }
    .trend-table tr:hover td { background:#faf7f4; }
    .mini-bar { display:inline-block; height:14px; background:linear-gradient(90deg,#7C4DFF,#B388FF); border-radius:7px; min-width:4px; transition:width .4s; }

    /* ===== Reviews list ===== */
    .filter-bar { display:flex; flex-wrap:wrap; gap:12px; align-items:center; margin-bottom:16px; }
    .filter-bar select, .filter-bar input { padding:8px 12px; border:1px solid #ddd; border-radius:8px; font-family:'Prompt',sans-serif; font-size:13px; }
    .filter-bar button { padding:8px 18px; border:none; border-radius:8px; background:#7C4DFF; color:#fff; font-family:'Prompt',sans-serif; font-weight:500; cursor:pointer; transition:background .2s; }
    .filter-bar button:hover { background:#651FFF; }

    .review-list { max-height:600px; overflow-y:auto; }
    .review-item { padding:16px; border:1px solid #f0ece8; border-radius:10px; margin-bottom:12px; cursor:pointer; transition:all .2s; }
    .review-item:hover { border-color:#7C4DFF; box-shadow:0 2px 8px rgba(124,77,255,.1); }
    .review-top { display:flex; justify-content:space-between; align-items:center; margin-bottom:8px; }
    .review-meta { display:flex; align-items:center; gap:10px; }
    .review-name { font-weight:600; color:#333; }
    .review-date { font-size:12px; color:#999; }
    .review-stars { color:#FF9800; font-size:14px; }
    .review-text { font-size:13px; color:#555; line-height:1.6; display:-webkit-box; -webkit-line-clamp:3; -webkit-box-orient:vertical; overflow:hidden; }

    /* Sentiment badges */
    .badge { padding:3px 10px; border-radius:12px; font-size:11px; font-weight:600; display:inline-block; }
    .badge-POSITIVE { background:#E8F5E9; color:#2E7D32; }
    .badge-NEGATIVE { background:#FFEBEE; color:#C62828; }
    .badge-NEUTRAL  { background:#F5F5F5; color:#616161; }
    .badge-MIXED    { background:#FFF3E0; color:#E65100; }
    .badge-pending  { background:#FFF8E1; color:#F57F17; }

    /* ===== Modal ===== */
    .modal-overlay { display:none; position:fixed; top:0; left:0; width:100%; height:100%; background:rgba(0,0,0,.5); z-index:9999; justify-content:center; align-items:center; }
    .modal-overlay.active { display:flex; }
    .modal-box { background:#fff; border-radius:14px; padding:30px; width:90%; max-width:700px; max-height:85vh; overflow-y:auto; box-shadow:0 8px 32px rgba(0,0,0,.2); position:relative; }
    .modal-box h3 { margin:0 0 16px; font-size:20px; color:#5D4037; }
    .modal-close { position:absolute; top:14px; right:18px; font-size:24px; cursor:pointer; color:#999; background:none; border:none; }
    .modal-close:hover { color:#333; }

    .modal-section { margin-bottom:16px; }
    .modal-section label { font-size:13px; font-weight:600; color:#666; display:block; margin-bottom:4px; }
    .modal-section .value { font-size:14px; color:#333; line-height:1.6; }

    .ai-summary { background:#f3e5f5; border-left:4px solid #7C4DFF; padding:14px 16px; border-radius:0 8px 8px 0; margin:12px 0; font-size:13px; line-height:1.7; color:#333; }
    .ai-response { background:#E8F5E9; border-left:4px solid #4CAF50; padding:14px 16px; border-radius:0 8px 8px 0; margin:12px 0; font-size:13px; line-height:1.7; color:#333; }

    /* ===== Buttons ===== */
    .btn { padding:8px 18px; border:none; border-radius:8px; cursor:pointer; font-family:'Prompt',sans-serif; font-size:13px; font-weight:500; transition:all .2s; display:inline-flex; align-items:center; gap:6px; }
    .btn-primary { background:#7C4DFF; color:#fff; }
    .btn-primary:hover { background:#651FFF; }
    .btn-brown  { background:#5D4037; color:#fff; }
    .btn-brown:hover { background:#4E342E; }
    .btn-green  { background:#4CAF50; color:#fff; }
    .btn-green:hover { background:#388E3C; }
    .btn-red    { background:#F44336; color:#fff; }
    .btn-red:hover { background:#D32F2F; }
    .btn-orange { background:#FF9800; color:#fff; }
    .btn-orange:hover { background:#F57C00; }
    .btn-outline { background:#fff; color:#5D4037; border:1px solid #ccc; }
    .btn-outline:hover { border-color:#5D4037; }
    .btn:disabled { opacity:.5; cursor:not-allowed; }

    .btn-group { display:flex; gap:10px; flex-wrap:wrap; margin-top:16px; }

    /* ===== Tools section ===== */
    .tools-grid { display:grid; grid-template-columns:repeat(auto-fit,minmax(200px,1fr)); gap:12px; }
    .tool-card { background:#faf7f4; border:1px solid #e8e0d8; border-radius:10px; padding:18px; text-align:center; }
    .tool-card h4 { margin:0 0 8px; font-size:14px; color:#5D4037; }
    .tool-card p { font-size:12px; color:#888; margin:0 0 12px; }

    /* ===== Source settings ===== */
    .source-setting { display:flex; align-items:flex-start; gap:16px; padding:16px; border:1px solid #f0ece8; border-radius:10px; margin-bottom:12px; }
    .source-setting .info { flex:1; }
    .source-setting h4 { margin:0 0 6px; font-size:15px; color:#5D4037; }
    .toggle-switch { position:relative; width:48px; height:26px; flex-shrink:0; }
    .toggle-switch input { opacity:0; width:0; height:0; }
    .toggle-slider { position:absolute; cursor:pointer; top:0; left:0; right:0; bottom:0; background:#ccc; border-radius:13px; transition:.3s; }
    .toggle-slider:before { content:''; position:absolute; height:20px; width:20px; left:3px; bottom:3px; background:#fff; border-radius:50%; transition:.3s; }
    .toggle-switch input:checked + .toggle-slider { background:#4CAF50; }
    .toggle-switch input:checked + .toggle-slider:before { transform:translateX(22px); }
    .config-area { width:100%; min-height:80px; margin-top:8px; font-family:monospace; font-size:12px; padding:8px; border:1px solid #ddd; border-radius:6px; resize:vertical; }

    /* ===== Toast ===== */
    .toast { position:fixed; top:24px; right:24px; padding:14px 24px; border-radius:10px; color:#fff; font-family:'Prompt',sans-serif; font-size:14px; font-weight:500; z-index:99999; opacity:0; transform:translateY(-20px); transition:all .3s; box-shadow:0 4px 12px rgba(0,0,0,.2); }
    .toast.show { opacity:1; transform:translateY(0); }
    .toast-success { background:#4CAF50; }
    .toast-error   { background:#F44336; }
    .toast-info    { background:#7C4DFF; }

    /* ===== Loading ===== */
    .loading-overlay { display:none; position:fixed; top:0; left:0; width:100%; height:100%; background:rgba(255,255,255,.7); z-index:99998; justify-content:center; align-items:center; }
    .loading-overlay.active { display:flex; }
    .spinner { width:48px; height:48px; border:4px solid #f0ece8; border-top-color:#7C4DFF; border-radius:50%; animation:spin .8s linear infinite; }
    @keyframes spin { to { transform:rotate(360deg); } }

    /* ===== Scrollbar ===== */
    .review-list::-webkit-scrollbar { width:6px; }
    .review-list::-webkit-scrollbar-track { background:#f5f5f5; border-radius:3px; }
    .review-list::-webkit-scrollbar-thumb { background:#ccc; border-radius:3px; }
    .review-list::-webkit-scrollbar-thumb:hover { background:#999; }

    /* ===== Summary period select ===== */
    .period-select { display:flex; gap:8px; align-items:center; }
    .period-select select { padding:8px 12px; border:1px solid #ddd; border-radius:8px; font-family:'Prompt',sans-serif; font-size:13px; }
</style>

<div class="ai-review-dash">
    <!-- Header -->
    <div class="page-header">
        <h1>&#x1F4CA; AI Review Dashboard</h1>
        <p>แดชบอร์ดวิเคราะห์รีวิวอัจฉริยะ - วิเคราะห์ความรู้สึก, แนวโน้ม, และจัดการการตอบกลับรีวิว</p>
    </div>

    <!-- Stats Row -->
    <div class="stats-row" id="statsRow">
        <div class="stat-card">
            <h4>รีวิวทั้งหมด</h4>
            <div class="val" id="statTotal">-</div>
        </div>
        <div class="stat-card">
            <h4>คะแนนเฉลี่ย</h4>
            <div class="val accent" id="statAvgRating">-</div>
            <div class="star-display" id="statStars"></div>
        </div>
        <div class="stat-card">
            <h4>เชิงบวก</h4>
            <div class="val" id="statPositive" style="color:#4CAF50">-</div>
            <div class="sub">เปอร์เซ็นต์</div>
        </div>
        <div class="stat-card">
            <h4>เชิงลบ</h4>
            <div class="val" id="statNegative" style="color:#F44336">-</div>
            <div class="sub">รีวิว</div>
        </div>
        <div class="stat-card">
            <h4>ถูกตั้งค่าสถานะ</h4>
            <div class="val" id="statFlagged" style="color:#FF9800">-</div>
        </div>
        <div class="stat-card">
            <h4>รอตอบกลับ</h4>
            <div class="val" id="statPending" style="color:#7C4DFF">-</div>
        </div>
    </div>

    <!-- Row: Sentiment + Source + Rating -->
    <div class="grid-3">
        <!-- Sentiment Overview -->
        <div class="card">
            <h3>&#x1F4CA; ภาพรวมความรู้สึก</h3>
            <div id="sentimentChart"></div>
        </div>

        <!-- Source Breakdown -->
        <div class="card">
            <h3>&#x1F310; แหล่งที่มารีวิว</h3>
            <div id="sourceBreakdown"></div>
        </div>

        <!-- Rating Distribution -->
        <div class="card">
            <h3>&#x2B50; การกระจายคะแนน</h3>
            <div id="ratingDist"></div>
        </div>
    </div>

    <!-- Row: Topics + Monthly Trend -->
    <div class="grid-2">
        <!-- Topic Analysis -->
        <div class="card">
            <h3>&#x1F3F7;&#xFE0F; หัวข้อที่พูดถึง</h3>
            <div class="topic-cloud" id="topicCloud"></div>
        </div>

        <!-- Monthly Trend -->
        <div class="card">
            <h3>&#x1F4C8; แนวโน้มรายเดือน</h3>
            <div id="monthlyTrend" style="max-height:360px;overflow-y:auto;"></div>
        </div>
    </div>

    <!-- Recent Reviews -->
    <div class="card">
        <h3>&#x1F4DD; รีวิวล่าสุด</h3>
        <div class="filter-bar">
            <select id="filterSource"><option value="">ทุกแหล่ง</option><option value="INTERNAL">ภายใน</option><option value="GOOGLE">Google</option><option value="FACEBOOK">Facebook</option></select>
            <select id="filterSentiment"><option value="">ทุกความรู้สึก</option><option value="POSITIVE">เชิงบวก</option><option value="NEGATIVE">เชิงลบ</option><option value="NEUTRAL">กลาง</option><option value="MIXED">ผสม</option></select>
            <select id="filterMinRating"><option value="">คะแนนต่ำสุด</option><option value="1">1</option><option value="2">2</option><option value="3">3</option><option value="4">4</option><option value="5">5</option></select>
            <select id="filterMaxRating"><option value="">คะแนนสูงสุด</option><option value="1">1</option><option value="2">2</option><option value="3">3</option><option value="4">4</option><option value="5">5</option></select>
            <input type="text" id="filterSearch" placeholder="ค้นหารีวิว..." style="flex:1;min-width:150px;" />
            <button onclick="loadReviews()">&#x1F50D; ค้นหา</button>
        </div>
        <div class="review-list" id="reviewList">
            <p style="text-align:center;color:#999;">กำลังโหลด...</p>
        </div>
    </div>

    <!-- Tools Section -->
    <div class="card">
        <h3><i class="fas fa-tools" style="color:#7C4DFF"></i> เครื่องมือดึงรีวิว</h3>
        <div class="tools-grid">
            <div class="tool-card">
                <h4><i class="fab fa-google" style="color:#4285F4"></i> Google Reviews</h4>
                <p>ดึงรีวิวจาก Google Places API (ต้องตั้งค่า placeId + apiKey)</p>
                <button class="btn btn-primary" onclick="fetchGoogle()">ดึงรีวิว Google</button>
            </div>
            <div class="tool-card">
                <h4><i class="fab fa-facebook" style="color:#1877F2"></i> Facebook Reviews</h4>
                <p>ดึงรีวิวจาก Facebook Page (รองรับ pagination ดึงทั้งหมด)</p>
                <button class="btn btn-primary" onclick="fetchFacebook()">ดึงรีวิว Facebook</button>
            </div>
            <div class="tool-card">
                <h4><i class="fas fa-globe" style="color:#5542F6"></i> ดึงรีวิว OTA</h4>
                <p>ดึงจาก Agoda, Booking, TripAdvisor ฯลฯ (AI อ่านหน้าเว็บ)</p>
                <div style="display:flex;gap:6px;flex-wrap:wrap;margin-top:8px;">
                    <select id="otaSource" style="padding:6px 10px;border:1px solid #ddd;border-radius:6px;font-family:'Prompt',sans-serif;font-size:13px;">
                        <optgroup label="OTA แพลตฟอร์ม">
                            <option value="AGODA">Agoda</option>
                            <option value="BOOKING">Booking.com</option>
                            <option value="TRIPADVISOR">TripAdvisor</option>
                            <option value="EXPEDIA">Expedia</option>
                            <option value="TRAVELOKA">Traveloka</option>
                        </optgroup>
                        <optgroup label="Social Media">
                            <option value="PANTIP">Pantip</option>
                            <option value="TIKTOK">TikTok</option>
                            <option value="LEMON8">Lemon8</option>
                            <option value="WONGNAI">Wongnai</option>
                            <option value="TWITTER">X (Twitter)</option>
                            <option value="INSTAGRAM">Instagram</option>
                            <option value="YOUTUBE">YouTube</option>
                        </optgroup>
                    </select>
                    <button class="btn btn-primary" onclick="fetchOTA()">ดึงรีวิว</button>
                </div>
            </div>
            <div class="tool-card">
                <h4><i class="fas fa-sync-alt" style="color:#4CAF50"></i> ดึงทุกแหล่ง</h4>
                <p>ดึงรีวิวจากทุกแหล่งที่เปิดใช้งานพร้อมกัน</p>
                <button class="btn btn-green" onclick="fetchAll()">ดึงรีวิวทั้งหมด</button>
            </div>
            <div class="tool-card">
                <h4><i class="fas fa-robot" style="color:#7C4DFF"></i> AI ค้นหาทุกแหล่ง</h4>
                <p>AI ค้นหารีวิว/การกล่าวถึงจากเว็บทุกแพลตฟอร์มอัตโนมัติ ไม่ต้องตั้งค่า API</p>
                <button class="btn btn-green" id="btnAiDiscover" onclick="aiDiscoverAll()">🤖 เริ่ม AI ค้นหา</button>
                <div id="aiDiscoverProgress" style="display:none;margin-top:10px;max-height:220px;overflow-y:auto;font-size:12px;line-height:1.8;background:#f8f9fa;border-radius:8px;padding:8px 12px;"></div>
            </div>
            <div class="tool-card">
                <h4><i class="fas fa-file-import" style="color:#FF9800"></i> นำเข้ารีวิว (JSON)</h4>
                <p>วาง JSON array ที่มี reviewerName, rating, reviewText, reviewDate</p>
                <button class="btn btn-brown" onclick="openImportModal()">นำเข้า JSON</button>
            </div>
            <div class="tool-card">
                <h4><i class="fas fa-paste" style="color:#9C27B0"></i> นำเข้าจากข้อความ (AI)</h4>
                <p>Copy ข้อความรีวิวจากเว็บมาวาง AI จะแปลงให้อัตโนมัติ</p>
                <button class="btn btn-brown" onclick="openImportTextModal()">วาง + นำเข้า</button>
            </div>
            <div class="tool-card">
                <h4><i class="fas fa-brain" style="color:#7C4DFF"></i> วิเคราะห์ Sentiment</h4>
                <p>วิเคราะห์ sentiment รีวิวที่ยังไม่ได้วิเคราะห์ (สูงสุด 20 รายการ)</p>
                <button class="btn btn-green" onclick="analyzePending()">วิเคราะห์ทั้งหมด</button>
            </div>
            <div class="tool-card">
                <h4><i class="fas fa-chart-pie" style="color:#E91E63"></i> สร้างสรุปรีวิว</h4>
                <p>AI สรุปภาพรวมรีวิวตามช่วงเวลา</p>
                <div class="period-select">
                    <select id="summaryPeriod">
                        <option value="WEEK">สัปดาห์</option>
                        <option value="MONTH">เดือน</option>
                        <option value="QUARTER">ไตรมาส</option>
                    </select>
                    <button class="btn btn-primary" onclick="generateSummary()">สร้างสรุป</button>
                </div>
            </div>
        </div>
    </div>

    <!-- Import Text Modal -->
    <div id="importTextModal" style="display:none;position:fixed;top:0;left:0;width:100%;height:100%;background:rgba(0,0,0,0.5);z-index:9999;display:none;justify-content:center;align-items:center;">
        <div style="background:white;border-radius:12px;padding:30px;max-width:700px;width:95%;max-height:80vh;overflow-y:auto;">
            <h3 style="margin:0 0 15px;color:#5D4037;"><i class="fas fa-paste"></i> นำเข้ารีวิวจากข้อความ (AI Parse)</h3>
            <p style="color:#666;font-size:13px;margin-bottom:15px;">
                Copy ข้อความรีวิวจากเว็บไซต์ OTA มาวางด้านล่าง<br>
                AI จะอ่านและแปลงเป็นข้อมูลรีวิวพร้อมชื่อ, คะแนน, วันที่ ให้อัตโนมัติ<br>
                <strong>ระบบจะไม่นำเข้าซ้ำ</strong> ถ้ามีรีวิวจากคนเดิม วันเดียวกัน คะแนนเดียวกันอยู่แล้ว
            </p>
            <div style="margin-bottom:12px;">
                <label style="font-weight:500;font-size:14px;">แหล่งที่มา:</label>
                <select id="importTextSource" style="padding:8px 12px;border:1px solid #ddd;border-radius:6px;font-family:'Prompt',sans-serif;width:100%;margin-top:4px;">
                    <optgroup label="OTA">
                        <option value="AGODA">Agoda</option>
                        <option value="BOOKING">Booking.com</option>
                        <option value="TRIPADVISOR">TripAdvisor</option>
                        <option value="EXPEDIA">Expedia</option>
                        <option value="TRAVELOKA">Traveloka</option>
                    </optgroup>
                    <optgroup label="Reviews">
                        <option value="GOOGLE">Google</option>
                        <option value="FACEBOOK">Facebook</option>
                        <option value="WONGNAI">Wongnai</option>
                    </optgroup>
                    <optgroup label="Social">
                        <option value="PANTIP">Pantip</option>
                        <option value="TIKTOK">TikTok</option>
                        <option value="LEMON8">Lemon8</option>
                        <option value="TWITTER">X (Twitter)</option>
                        <option value="INSTAGRAM">Instagram</option>
                        <option value="YOUTUBE">YouTube</option>
                    </optgroup>
                </select>
            </div>
            <textarea id="importTextContent" rows="12" style="width:100%;padding:12px;border:1px solid #ddd;border-radius:8px;font-family:'Prompt',sans-serif;font-size:13px;resize:vertical;" placeholder="วางข้อความรีวิวที่ copy มาจากเว็บไซต์ที่นี่...&#10;&#10;ตัวอย่าง:&#10;John D. - 9.2/10 - 15 มกราคม 2024&#10;ห้องสะอาดมาก บริการดี วิวสวย แนะนำเลย&#10;&#10;สมชาย - 8.0/10 - 3 กุมภาพันธ์ 2024&#10;โดยรวมดี แต่ wifi ช้า"></textarea>
            <div style="display:flex;gap:10px;justify-content:flex-end;margin-top:15px;">
                <button class="btn" onclick="closeImportTextModal()" style="background:#eee;color:#333;">ยกเลิก</button>
                <button class="btn btn-primary" onclick="importFromText()" id="btnImportText">
                    <i class="fas fa-robot"></i> AI วิเคราะห์ + นำเข้า
                </button>
            </div>
        </div>
    </div>

    <!-- Source Settings -->
    <div class="card">
        <h3>&#x2699;&#xFE0F; ตั้งค่าแหล่งรีวิว</h3>
        <div id="sourceSettings">
            <p style="text-align:center;color:#999;">กำลังโหลด...</p>
        </div>
    </div>
</div>

<!-- Review Detail Modal -->
<div class="modal-overlay" id="reviewModal">
    <div class="modal-box">
        <button class="modal-close" onclick="closeModal('reviewModal')">&times;</button>
        <h3 id="modalReviewTitle">รายละเอียดรีวิว</h3>

        <div class="modal-section">
            <label>ผู้รีวิว</label>
            <div class="value" id="modalReviewer">-</div>
        </div>
        <div class="modal-section">
            <label>แหล่งที่มา / วันที่</label>
            <div class="value"><span id="modalSource"></span> &mdash; <span id="modalDate"></span></div>
        </div>
        <div class="modal-section">
            <label>คะแนน</label>
            <div class="value" id="modalRating"></div>
        </div>
        <div class="modal-section">
            <label>ความรู้สึก</label>
            <div class="value" id="modalSentiment"></div>
        </div>
        <div class="modal-section">
            <label>ข้อความรีวิว</label>
            <div class="value" id="modalText" style="white-space:pre-wrap;"></div>
        </div>
        <div class="modal-section" id="modalSummarySection" style="display:none;">
            <label>AI สรุป</label>
            <div class="ai-summary" id="modalSummary"></div>
        </div>
        <div class="modal-section" id="modalResponseSection" style="display:none;">
            <label>AI แนะนำการตอบกลับ</label>
            <div class="ai-response" id="modalAIResponse"></div>
        </div>

        <div class="btn-group">
            <button class="btn btn-green" id="btnApplyAI" onclick="applyAIResponse()">&#x2705; ใช้คำตอบ AI</button>
            <button class="btn btn-primary" onclick="openCustomResponseArea()">&#x270F;&#xFE0F; เขียนคำตอบเอง</button>
            <button class="btn btn-orange" onclick="openFlagArea()">&#x1F6A9; ตั้งค่าสถานะ</button>
            <button class="btn btn-outline" id="btnAnalyzeOne" onclick="analyzeOneReview()">&#x1F9E0; วิเคราะห์ Sentiment</button>
        </div>

        <!-- Custom response area (hidden by default) -->
        <div id="customResponseArea" style="display:none;margin-top:16px;">
            <label style="font-size:13px;font-weight:600;color:#666;">เขียนคำตอบ:</label>
            <textarea id="customResponseText" rows="4" style="width:100%;padding:10px;border:1px solid #ddd;border-radius:8px;font-family:'Prompt',sans-serif;font-size:13px;margin:8px 0;resize:vertical;"></textarea>
            <button class="btn btn-green" onclick="saveCustomResponse()">&#x1F4BE; บันทึกคำตอบ</button>
        </div>

        <!-- Flag area (hidden by default) -->
        <div id="flagArea" style="display:none;margin-top:16px;">
            <label style="font-size:13px;font-weight:600;color:#666;">เหตุผลในการตั้งค่าสถานะ:</label>
            <textarea id="flagReason" rows="3" style="width:100%;padding:10px;border:1px solid #ddd;border-radius:8px;font-family:'Prompt',sans-serif;font-size:13px;margin:8px 0;resize:vertical;"></textarea>
            <button class="btn btn-orange" onclick="submitFlag()">&#x1F6A9; ยืนยันตั้งค่าสถานะ</button>
        </div>
    </div>
</div>

<!-- Import Modal -->
<div class="modal-overlay" id="importModal">
    <div class="modal-box">
        <button class="modal-close" onclick="closeModal('importModal')">&times;</button>
        <h3>&#x1F4E5; นำเข้ารีวิว</h3>
        <div class="modal-section">
            <label>รหัสแหล่งที่มา</label>
            <select id="importSourceCode" style="width:100%;padding:8px 12px;border:1px solid #ddd;border-radius:8px;font-family:'Prompt',sans-serif;">
                <option value="INTERNAL">INTERNAL</option>
                <option value="GOOGLE">GOOGLE</option>
                <option value="FACEBOOK">FACEBOOK</option>
                <option value="TRIPADVISOR">TRIPADVISOR</option>
                <option value="AGODA">AGODA</option>
                <option value="BOOKING">BOOKING</option>
            </select>
        </div>
        <div class="modal-section">
            <label>เนื้อหา JSON</label>
            <textarea id="importContent" rows="10" style="width:100%;padding:10px;border:1px solid #ddd;border-radius:8px;font-family:monospace;font-size:12px;resize:vertical;" placeholder='[{"reviewerName":"...","rating":5,"reviewText":"...","reviewDate":"2025-01-01"}]'></textarea>
        </div>
        <div class="btn-group">
            <button class="btn btn-green" onclick="submitImport()">&#x1F4E5; นำเข้า</button>
            <button class="btn btn-outline" onclick="closeModal('importModal')">ยกเลิก</button>
        </div>
    </div>
</div>

<!-- Summary Modal -->
<div class="modal-overlay" id="summaryModal">
    <div class="modal-box">
        <button class="modal-close" onclick="closeModal('summaryModal')">&times;</button>
        <h3>&#x1F4CB; สรุปรีวิวโดย AI</h3>
        <div class="ai-summary" id="summaryContent" style="white-space:pre-wrap;">กำลังสร้าง...</div>
        <div class="btn-group">
            <button class="btn btn-outline" onclick="closeModal('summaryModal')">ปิด</button>
        </div>
    </div>
</div>

<!-- Loading Overlay -->
<div class="loading-overlay" id="loadingOverlay">
    <div class="spinner"></div>
</div>

<!-- Toast -->
<div class="toast" id="toast"></div>

<script>
    // ===== State =====
    var dashboardData = null;
    var currentReview = null;

    // ===== Helpers =====
    function apiCall(action, data) {
        return fetch('?action=' + action, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(data || {})
        }).then(function (r) { return r.json(); });
    }

    function showLoading() { document.getElementById('loadingOverlay').classList.add('active'); }
    function hideLoading() { document.getElementById('loadingOverlay').classList.remove('active'); }

    function showToast(msg, type) {
        var t = document.getElementById('toast');
        t.textContent = msg;
        t.className = 'toast toast-' + (type || 'info') + ' show';
        setTimeout(function () { t.classList.remove('show'); }, 3500);
    }

    function openModal(id) { document.getElementById(id).classList.add('active'); }
    function closeModal(id) { document.getElementById(id).classList.remove('active'); }

    function renderStars(rating) {
        var full = Math.floor(rating);
        var half = rating - full >= 0.5 ? 1 : 0;
        var empty = 5 - full - half;
        var s = '';
        for (var i = 0; i < full; i++) s += '★';
        if (half) s += '☆';
        for (var i = 0; i < empty; i++) s += '☆';
        return s;
    }

    function sentimentBadge(sentiment) {
        if (!sentiment) return '<span class="badge badge-pending">รอวิเคราะห์</span>';
        var cls = 'badge-' + sentiment;
        var labels = { 'POSITIVE': 'เชิงบวก', 'NEGATIVE': 'เชิงลบ', 'NEUTRAL': 'กลาง', 'MIXED': 'ผสม' };
        return '<span class="badge ' + cls + '">' + (labels[sentiment] || sentiment) + '</span>';
    }

    function sourceBadge(src) {
        return '<span class="source-badge ' + (src || '') + '">' + (src || '-') + '</span>';
    }

    function formatDate(d) {
        if (!d) return '-';
        var dt = new Date(d);
        if (isNaN(dt.getTime())) return d;
        return dt.toLocaleDateString('th-TH', { year: 'numeric', month: 'short', day: 'numeric' });
    }

    function truncate(text, len) {
        if (!text) return '';
        return text.length > len ? text.substring(0, len) + '...' : text;
    }

    // ===== Dashboard =====
    function loadDashboard() {
        showLoading();
        apiCall('getDashboard').then(function (res) {
            hideLoading();
            if (res.success) {
                dashboardData = res.data;
                renderStats(dashboardData);
                renderSentimentChart(dashboardData.sentimentBreakdown);
                renderSourceBreakdown(dashboardData.sourceBreakdown);
                renderRatingDist(dashboardData.ratingDistribution);
                renderTopics(dashboardData.topicBreakdown);
                renderMonthlyTrend(dashboardData.monthlyTrend);
                renderRecentReviews(dashboardData.recentReviews);
            } else {
                showToast(res.message || 'ไม่สามารถโหลดข้อมูลได้', 'error');
            }
        }).catch(function (err) {
            hideLoading();
            showToast('เกิดข้อผิดพลาด: ' + err.message, 'error');
        });
    }

    function renderStats(d) {
        document.getElementById('statTotal').textContent = (d.totalReviews || 0).toLocaleString();
        var avg = parseFloat(d.avgRating || 0);
        document.getElementById('statAvgRating').textContent = avg.toFixed(1);
        document.getElementById('statStars').textContent = renderStars(avg);

        var sb = d.sentimentBreakdown || {};
        var total = (sb.positive || 0) + (sb.negative || 0) + (sb.neutral || 0) + (sb.mixed || 0);
        var posPct = total > 0 ? ((sb.positive || 0) / total * 100).toFixed(1) : '0.0';
        document.getElementById('statPositive').textContent = posPct + '%';
        document.getElementById('statNegative').textContent = (sb.negative || 0).toLocaleString();
        document.getElementById('statFlagged').textContent = (d.flaggedCount || 0).toLocaleString();
        document.getElementById('statPending').textContent = (d.pendingResponseCount || 0).toLocaleString();
    }

    // ===== Sentiment Chart =====
    function renderSentimentChart(sb) {
        if (!sb) { document.getElementById('sentimentChart').innerHTML = '<p style="color:#999">ไม่มีข้อมูล</p>'; return; }
        var total = (sb.positive || 0) + (sb.negative || 0) + (sb.neutral || 0) + (sb.mixed || 0);
        if (total === 0) { document.getElementById('sentimentChart').innerHTML = '<p style="color:#999">ไม่มีข้อมูล</p>'; return; }

        var items = [
            { key: 'positive', label: 'เชิงบวก', color: '#4CAF50', count: sb.positive || 0 },
            { key: 'negative', label: 'เชิงลบ', color: '#F44336', count: sb.negative || 0 },
            { key: 'neutral', label: 'กลาง', color: '#9E9E9E', count: sb.neutral || 0 },
            { key: 'mixed', label: 'ผสม', color: '#FF9800', count: sb.mixed || 0 }
        ];

        // Stacked bar
        var html = '<div class="stacked-bar">';
        items.forEach(function (it) {
            var pct = (it.count / total * 100);
            if (pct > 0) {
                html += '<div class="seg" style="width:' + pct + '%;background:' + it.color + ';">' + (pct >= 8 ? Math.round(pct) + '%' : '') + '</div>';
            }
        });
        html += '</div>';

        // Legend
        html += '<div class="legend-row">';
        items.forEach(function (it) {
            html += '<div class="legend-item"><div class="legend-dot" style="background:' + it.color + '"></div>' + it.label + ' (' + it.count + ')</div>';
        });
        html += '</div>';

        // Individual bars
        html += '<div style="margin-top:16px;">';
        items.forEach(function (it) {
            var pct = total > 0 ? (it.count / total * 100) : 0;
            html += '<div class="sentiment-bar-wrap">';
            html += '<div class="label-row"><span class="lbl">' + it.label + '</span><span>' + it.count + ' (' + pct.toFixed(1) + '%)</span></div>';
            html += '<div class="bar-bg"><div class="bar-fill bar-' + it.key + '" style="width:' + pct + '%;"></div></div>';
            html += '</div>';
        });
        html += '</div>';

        document.getElementById('sentimentChart').innerHTML = html;
    }

    // ===== Source Breakdown =====
    function renderSourceBreakdown(sources) {
        var el = document.getElementById('sourceBreakdown');
        if (!sources || sources.length === 0) { el.innerHTML = '<p style="color:#999">ไม่มีข้อมูล</p>'; return; }
        var html = '';
        sources.forEach(function (s) {
            html += '<div class="source-item">';
            html += '<div class="source-info">' + sourceBadge(s.source) + '</div>';
            html += '<div class="source-stats">';
            html += '<div class="source-count">' + (s.count || 0) + ' <span style="font-size:12px;color:#888;">รีวิว</span></div>';
            html += '<div class="source-avg"><span style="color:#FF9800;">' + renderStars(s.avgRating || 0) + '</span> ' + (s.avgRating || 0).toFixed(1) + '</div>';
            html += '</div></div>';
        });
        el.innerHTML = html;
    }

    // ===== Rating Distribution =====
    function renderRatingDist(rd) {
        var el = document.getElementById('ratingDist');
        if (!rd) { el.innerHTML = '<p style="color:#999">ไม่มีข้อมูล</p>'; return; }
        var maxCount = 0;
        for (var i = 5; i >= 1; i--) {
            var c = rd[i] || rd[String(i)] || 0;
            if (c > maxCount) maxCount = c;
        }
        var html = '';
        for (var i = 5; i >= 1; i--) {
            var c = rd[i] || rd[String(i)] || 0;
            var pct = maxCount > 0 ? (c / maxCount * 100) : 0;
            html += '<div class="rating-row">';
            html += '<div class="rating-label">' + i + ' <span style="color:#FF9800;">★</span></div>';
            html += '<div class="rating-bar-bg"><div class="rating-bar-fill" style="width:' + pct + '%;"></div></div>';
            html += '<div class="rating-count">' + c + '</div>';
            html += '</div>';
        }
        el.innerHTML = html;
    }

    // ===== Topics =====
    function renderTopics(topics) {
        var el = document.getElementById('topicCloud');
        if (!topics || topics.length === 0) { el.innerHTML = '<p style="color:#999">ไม่มีข้อมูล</p>'; return; }
        var html = '';
        topics.forEach(function (t) {
            html += '<div class="topic-pill">' + (t.topic || '-') + ' <span class="cnt">' + (t.count || 0) + '</span></div>';
        });
        el.innerHTML = html;
    }

    // ===== Monthly Trend =====
    function renderMonthlyTrend(trend) {
        var el = document.getElementById('monthlyTrend');
        if (!trend || trend.length === 0) { el.innerHTML = '<p style="color:#999">ไม่มีข้อมูล</p>'; return; }
        var maxCount = 0;
        trend.forEach(function (m) { if ((m.count || 0) > maxCount) maxCount = m.count; });

        var html = '<table class="trend-table"><thead><tr><th>เดือน</th><th>จำนวน</th><th></th><th>คะแนนเฉลี่ย</th></tr></thead><tbody>';
        trend.forEach(function (m) {
            var pct = maxCount > 0 ? ((m.count || 0) / maxCount * 100) : 0;
            html += '<tr>';
            html += '<td>' + (m.month || '-') + '</td>';
            html += '<td style="font-weight:600;">' + (m.count || 0) + '</td>';
            html += '<td style="width:40%;"><div class="mini-bar" style="width:' + pct + '%;"></div></td>';
            html += '<td><span style="color:#FF9800;">' + renderStars(m.avgRating || 0) + '</span> ' + (m.avgRating || 0).toFixed(1) + '</td>';
            html += '</tr>';
        });
        html += '</tbody></table>';
        el.innerHTML = html;
    }

    // ===== Recent Reviews =====
    function renderRecentReviews(reviews) {
        var el = document.getElementById('reviewList');
        if (!reviews || reviews.length === 0) { el.innerHTML = '<p style="text-align:center;color:#999;">ไม่มีรีวิว</p>'; return; }
        var html = '';
        reviews.forEach(function (r) {
            html += '<div class="review-item" onclick=\'openReviewDetail(' + JSON.stringify(r).replace(/'/g, "\\'") + ')\'>';
            html += '<div class="review-top">';
            html += '<div class="review-meta">';
            html += sourceBadge(r.source);
            html += '<span class="review-name">' + (r.reviewerName || 'ไม่ระบุชื่อ') + '</span>';
            html += '<span class="review-stars">' + renderStars(r.rating || 0) + '</span>';
            html += '</div>';
            html += '<div>' + sentimentBadge(r.sentiment) + ' <span class="review-date">' + formatDate(r.reviewDate) + '</span></div>';
            html += '</div>';
            html += '<div class="review-text">' + truncate(r.reviewText || '', 200) + '</div>';
            html += '</div>';
        });
        el.innerHTML = html;
    }

    // ===== Load filtered reviews =====
    function loadReviews() {
        showLoading();
        apiCall('getReviews', {
            source: document.getElementById('filterSource').value,
            sentiment: document.getElementById('filterSentiment').value,
            minRating: document.getElementById('filterMinRating').value || null,
            maxRating: document.getElementById('filterMaxRating').value || null,
            search: document.getElementById('filterSearch').value,
            limit: 50
        }).then(function (res) {
            hideLoading();
            if (res.success) {
                renderRecentReviews(res.data);
            } else {
                showToast(res.message || 'ไม่สามารถโหลดรีวิวได้', 'error');
            }
        }).catch(function (err) {
            hideLoading();
            showToast('เกิดข้อผิดพลาด: ' + err.message, 'error');
        });
    }

    // ===== Review Detail =====
    function openReviewDetail(review) {
        currentReview = review;
        document.getElementById('modalReviewer').textContent = review.reviewerName || 'ไม่ระบุชื่อ';
        document.getElementById('modalSource').innerHTML = sourceBadge(review.source);
        document.getElementById('modalDate').textContent = formatDate(review.reviewDate);
        document.getElementById('modalRating').innerHTML = '<span style="color:#FF9800;font-size:18px;">' + renderStars(review.rating || 0) + '</span> (' + (review.rating || 0) + '/5)';
        document.getElementById('modalSentiment').innerHTML = sentimentBadge(review.sentiment) + (review.sentimentScore ? ' <span style="font-size:12px;color:#888;">คะแนน: ' + review.sentimentScore + '</span>' : '');
        document.getElementById('modalText').textContent = review.reviewText || '-';

        if (review.summary) {
            document.getElementById('modalSummarySection').style.display = 'block';
            document.getElementById('modalSummary').textContent = review.summary;
        } else {
            document.getElementById('modalSummarySection').style.display = 'none';
        }

        if (review.suggestedResponse) {
            document.getElementById('modalResponseSection').style.display = 'block';
            document.getElementById('modalAIResponse').textContent = review.suggestedResponse;
            document.getElementById('btnApplyAI').style.display = '';
        } else {
            document.getElementById('modalResponseSection').style.display = 'none';
            document.getElementById('btnApplyAI').style.display = 'none';
        }

        document.getElementById('customResponseArea').style.display = 'none';
        document.getElementById('flagArea').style.display = 'none';
        document.getElementById('customResponseText').value = '';
        document.getElementById('flagReason').value = '';

        openModal('reviewModal');
    }

    function getSourceType() {
        if (!currentReview) return 'ONLINE';
        return currentReview.source === 'INTERNAL' ? 'INTERNAL' : 'ONLINE';
    }

    function applyAIResponse() {
        if (!currentReview) return;
        showLoading();
        apiCall('applyResponse', { reviewId: currentReview.id, sourceType: getSourceType() }).then(function (res) {
            hideLoading();
            if (res.success) {
                showToast('ใช้คำตอบ AI สำเร็จ', 'success');
                closeModal('reviewModal');
                loadDashboard();
            } else {
                showToast(res.message || 'เกิดข้อผิดพลาด', 'error');
            }
        }).catch(function (err) { hideLoading(); showToast('เกิดข้อผิดพลาด: ' + err.message, 'error'); });
    }

    function openCustomResponseArea() {
        document.getElementById('customResponseArea').style.display = 'block';
        document.getElementById('flagArea').style.display = 'none';
        document.getElementById('customResponseText').focus();
    }

    function saveCustomResponse() {
        if (!currentReview) return;
        var response = document.getElementById('customResponseText').value.trim();
        if (!response) { showToast('กรุณาเขียนคำตอบ', 'error'); return; }
        showLoading();
        apiCall('saveResponse', { reviewId: currentReview.id, sourceType: getSourceType(), response: response }).then(function (res) {
            hideLoading();
            if (res.success) {
                showToast('บันทึกคำตอบสำเร็จ', 'success');
                closeModal('reviewModal');
                loadDashboard();
            } else {
                showToast(res.message || 'เกิดข้อผิดพลาด', 'error');
            }
        }).catch(function (err) { hideLoading(); showToast('เกิดข้อผิดพลาด: ' + err.message, 'error'); });
    }

    function openFlagArea() {
        document.getElementById('flagArea').style.display = 'block';
        document.getElementById('customResponseArea').style.display = 'none';
        document.getElementById('flagReason').focus();
    }

    function submitFlag() {
        if (!currentReview) return;
        var reason = document.getElementById('flagReason').value.trim();
        if (!reason) { showToast('กรุณาระบุเหตุผล', 'error'); return; }
        showLoading();
        apiCall('flagReview', { reviewId: currentReview.id, sourceType: getSourceType(), reason: reason }).then(function (res) {
            hideLoading();
            if (res.success) {
                showToast('ตั้งค่าสถานะสำเร็จ', 'success');
                closeModal('reviewModal');
                loadDashboard();
            } else {
                showToast(res.message || 'เกิดข้อผิดพลาด', 'error');
            }
        }).catch(function (err) { hideLoading(); showToast('เกิดข้อผิดพลาด: ' + err.message, 'error'); });
    }

    function analyzeOneReview() {
        if (!currentReview) return;
        showLoading();
        apiCall('analyzeOne', { reviewId: currentReview.id, sourceType: getSourceType() }).then(function (res) {
            hideLoading();
            if (res.success) {
                showToast('วิเคราะห์ Sentiment สำเร็จ', 'success');
                closeModal('reviewModal');
                loadDashboard();
            } else {
                showToast(res.message || 'เกิดข้อผิดพลาด', 'error');
            }
        }).catch(function (err) { hideLoading(); showToast('เกิดข้อผิดพลาด: ' + err.message, 'error'); });
    }

    // ===== Tools =====
    function fetchGoogle() {
        showLoading();
        apiCall('fetchGoogle').then(function (res) {
            hideLoading();
            showToast(res.message || 'เสร็จสิ้น', res.success ? 'success' : 'error');
            if (res.success) loadDashboard();
        }).catch(function (err) { hideLoading(); showToast('เกิดข้อผิดพลาด: ' + err.message, 'error'); });
    }

    function fetchFacebook() {
        showLoading();
        apiCall('fetchFacebook').then(function (res) {
            hideLoading();
            showToast(res.message || 'เสร็จสิ้น', res.success ? 'success' : 'error');
            if (res.success) loadDashboard();
        }).catch(function (err) { hideLoading(); showToast('เกิดข้อผิดพลาด: ' + err.message, 'error'); });
    }

    function fetchOTA() {
        var sourceCode = document.getElementById('otaSource').value;
        showLoading();
        apiCall('fetchOTA', { sourceCode: sourceCode }).then(function (res) {
            hideLoading();
            showToast(res.message || 'เสร็จสิ้น', res.success ? 'success' : 'error');
            if (res.success) loadDashboard();
        }).catch(function (err) { hideLoading(); showToast('เกิดข้อผิดพลาด: ' + err.message, 'error'); });
    }

    function fetchAll() {
        showLoading();
        apiCall('fetchAll').then(function (res) {
            hideLoading();
            if (res.success && res.data) {
                var msgs = [];
                for (var i = 0; i < res.data.length; i++) {
                    msgs.push(res.data[i].message || res.data[i].source);
                }
                showToast(msgs.join('\n'), 'success');
                loadDashboard();
            } else {
                showToast(res.message || 'เกิดข้อผิดพลาด', 'error');
            }
        }).catch(function (err) { hideLoading(); showToast('เกิดข้อผิดพลาด: ' + err.message, 'error'); });
    }

    // ===== AI Discovery: search the web for reviews on every platform =====
    var AI_DISCOVER_SOURCES = [
        { code: 'GOOGLE',      name: 'Google' },
        { code: 'FACEBOOK',    name: 'Facebook' },
        { code: 'AGODA',       name: 'Agoda' },
        { code: 'BOOKING',     name: 'Booking.com' },
        { code: 'TRIPADVISOR', name: 'TripAdvisor' },
        { code: 'EXPEDIA',     name: 'Expedia' },
        { code: 'TRAVELOKA',   name: 'Traveloka' },
        { code: 'PANTIP',      name: 'Pantip' },
        { code: 'TIKTOK',      name: 'TikTok' },
        { code: 'LEMON8',      name: 'Lemon8' },
        { code: 'WONGNAI',     name: 'Wongnai' },
        { code: 'TWITTER',     name: 'X (Twitter)' },
        { code: 'INSTAGRAM',   name: 'Instagram' },
        { code: 'YOUTUBE',     name: 'YouTube' }
    ];
    var aiDiscoverRunning = false;

    function aiDiscoverAll() {
        if (aiDiscoverRunning) return;
        aiDiscoverRunning = true;

        var btn = document.getElementById('btnAiDiscover');
        var panel = document.getElementById('aiDiscoverProgress');
        btn.disabled = true;
        btn.textContent = '⏳ กำลังค้นหา...';
        panel.style.display = 'block';
        panel.innerHTML = '';

        var totalNew = 0;
        var idx = 0;

        function lineHtml(name, status, cls) {
            return '<div id="aiDisc_' + idx + '"><b>' + name + '</b>: <span style="color:' + cls + '">' + status + '</span></div>';
        }

        function next() {
            if (idx >= AI_DISCOVER_SOURCES.length) {
                btn.disabled = false;
                btn.textContent = '🤖 เริ่ม AI ค้นหา';
                aiDiscoverRunning = false;
                panel.innerHTML += '<div style="margin-top:6px;font-weight:bold;color:#2e7d32;">เสร็จสิ้น — พบรีวิวใหม่รวม ' + totalNew + ' รายการ</div>';
                if (totalNew > 0) loadDashboard();
                return;
            }

            var src = AI_DISCOVER_SOURCES[idx];
            panel.innerHTML += lineHtml(src.name, '⏳ กำลังค้นหา...', '#888');
            panel.scrollTop = panel.scrollHeight;
            var lineEl = document.getElementById('aiDisc_' + idx);

            apiCall('aiDiscover', { sourceCode: src.code }).then(function (res) {
                if (res.success) {
                    var n = res.newCount || 0;
                    totalNew += n;
                    lineEl.innerHTML = '<b>' + src.name + '</b>: <span style="color:' + (n > 0 ? '#2e7d32' : '#888') + '">'
                        + (n > 0 ? '✅ พบใหม่ ' + n + ' รายการ' : '— ไม่พบรีวิวใหม่') + '</span>';
                } else {
                    lineEl.innerHTML = '<b>' + src.name + '</b>: <span style="color:#c0392b">❌ ' + (res.message || 'ไม่สำเร็จ') + '</span>';
                }
                idx++;
                next();
            }).catch(function (err) {
                lineEl.innerHTML = '<b>' + src.name + '</b>: <span style="color:#c0392b">❌ ' + err.message + '</span>';
                idx++;
                next();
            });
        }

        next();
    }

    function openImportModal() {
        document.getElementById('importContent').value = '';
        openModal('importModal');
    }

    function submitImport() {
        var sourceCode = document.getElementById('importSourceCode').value;
        var content = document.getElementById('importContent').value.trim();
        if (!content) { showToast('กรุณาใส่เนื้อหา JSON', 'error'); return; }
        showLoading();
        apiCall('importReviews', { sourceCode: sourceCode, content: content }).then(function (res) {
            hideLoading();
            showToast(res.message || 'เสร็จสิ้น', res.success ? 'success' : 'error');
            if (res.success) { closeModal('importModal'); loadDashboard(); }
        }).catch(function (err) { hideLoading(); showToast('เกิดข้อผิดพลาด: ' + err.message, 'error'); });
    }

    function openImportTextModal() {
        document.getElementById('importTextContent').value = '';
        document.getElementById('importTextModal').style.display = 'flex';
    }

    function closeImportTextModal() {
        document.getElementById('importTextModal').style.display = 'none';
    }

    function importFromText() {
        var sourceCode = document.getElementById('importTextSource').value;
        var text = document.getElementById('importTextContent').value.trim();
        if (!text) { showToast('กรุณาวางข้อความรีวิว', 'error'); return; }
        var btn = document.getElementById('btnImportText');
        btn.disabled = true;
        btn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> AI กำลังวิเคราะห์...';
        showLoading();
        apiCall('importText', { sourceCode: sourceCode, text: text }).then(function (res) {
            hideLoading();
            btn.disabled = false;
            btn.innerHTML = '<i class="fas fa-robot"></i> AI วิเคราะห์ + นำเข้า';
            showToast(res.message || 'เสร็จสิ้น', res.success ? 'success' : 'error');
            if (res.success) { closeImportTextModal(); loadDashboard(); }
        }).catch(function (err) {
            hideLoading();
            btn.disabled = false;
            btn.innerHTML = '<i class="fas fa-robot"></i> AI วิเคราะห์ + นำเข้า';
            showToast('เกิดข้อผิดพลาด: ' + err.message, 'error');
        });
    }

    function analyzePending() {
        showLoading();
        apiCall('analyzePending').then(function (res) {
            hideLoading();
            if (res.success) {
                showToast('วิเคราะห์สำเร็จ: ' + (res.count || 0) + ' รีวิว', 'success');
                loadDashboard();
            } else {
                showToast(res.message || 'เกิดข้อผิดพลาด', 'error');
            }
        }).catch(function (err) { hideLoading(); showToast('เกิดข้อผิดพลาด: ' + err.message, 'error'); });
    }

    function generateSummary() {
        var period = document.getElementById('summaryPeriod').value;
        document.getElementById('summaryContent').textContent = 'กำลังสร้างสรุป...';
        openModal('summaryModal');
        showLoading();
        apiCall('generateSummary', { period: period }).then(function (res) {
            hideLoading();
            if (res.success) {
                document.getElementById('summaryContent').textContent = res.summary || 'ไม่มีข้อมูลสรุป';
            } else {
                document.getElementById('summaryContent').textContent = 'เกิดข้อผิดพลาด: ' + (res.message || '');
            }
        }).catch(function (err) {
            hideLoading();
            document.getElementById('summaryContent').textContent = 'เกิดข้อผิดพลาด: ' + err.message;
        });
    }

    // ===== Source Settings =====
    function loadSources() {
        apiCall('getSources').then(function (res) {
            if (res.success) {
                renderSourceSettings(res.data);
            } else {
                document.getElementById('sourceSettings').innerHTML =
                    '<p style="color:#c0392b;text-align:center;">' + (res.message || 'โหลดแหล่งที่มาไม่สำเร็จ') + '</p>';
            }
        }).catch(function (err) {
            document.getElementById('sourceSettings').innerHTML =
                '<p style="color:#c0392b;text-align:center;">โหลดแหล่งที่มาไม่สำเร็จ: ' + err.message + '</p>';
        });
    }

    function renderSourceSettings(sources) {
        var el = document.getElementById('sourceSettings');
        if (!sources || sources.length === 0) { el.innerHTML = '<p style="color:#999">ไม่พบแหล่งที่มา</p>'; return; }
        var html = '';
        sources.forEach(function (s, idx) {
            var isEnabled = s.IsEnabled === true || s.IsEnabled === 'True' || s.IsEnabled === 1;
            html += '<div class="source-setting">';
            html += '<div class="toggle-switch"><input type="checkbox" id="srcToggle_' + idx + '" ' + (isEnabled ? 'checked' : '') + ' onchange="updateSource(\'' + s.SourceCode + '\', this.checked, ' + idx + ')"><label class="toggle-slider" for="srcToggle_' + idx + '"></label></div>';
            html += '<div class="info">';
            html += '<h4>' + sourceBadge(s.SourceCode) + ' ' + (s.SourceName || s.SourceCode) + '</h4>';
            html += '<p style="font-size:12px;color:#888;margin:4px 0;">รหัส: ' + s.SourceCode + (s.LastFetchDate ? ' | ดึงล่าสุด: ' + formatDate(s.LastFetchDate) : '') + (s.TotalReviews ? ' | รีวิว: ' + s.TotalReviews : '') + '</p>';
            html += '<textarea class="config-area" id="srcConfig_' + idx + '" placeholder=\'{"apiKey":"...","placeId":"..."}\'>' + (s.ApiConfig || '') + '</textarea>';
            html += '<button class="btn btn-brown" style="margin-top:8px;" onclick="saveSourceConfig(\'' + s.SourceCode + '\', ' + idx + ')">&#x1F4BE; บันทึกการตั้งค่า</button>';
            html += '</div></div>';
        });
        el.innerHTML = html;
    }

    function updateSource(sourceCode, enabled, idx) {
        var apiConfig = document.getElementById('srcConfig_' + idx).value;
        apiCall('updateSource', { sourceCode: sourceCode, enabled: enabled, apiConfig: apiConfig }).then(function (res) {
            if (res.success) {
                showToast('อัปเดตแหล่งที่มาสำเร็จ', 'success');
            } else {
                showToast(res.message || 'เกิดข้อผิดพลาด', 'error');
            }
        }).catch(function (err) { showToast('เกิดข้อผิดพลาด: ' + err.message, 'error'); });
    }

    function saveSourceConfig(sourceCode, idx) {
        var toggle = document.getElementById('srcToggle_' + idx);
        var enabled = toggle ? toggle.checked : false;
        var apiConfig = document.getElementById('srcConfig_' + idx).value;
        showLoading();
        apiCall('updateSource', { sourceCode: sourceCode, enabled: enabled, apiConfig: apiConfig }).then(function (res) {
            hideLoading();
            if (res.success) {
                showToast('บันทึกการตั้งค่าสำเร็จ', 'success');
            } else {
                showToast(res.message || 'เกิดข้อผิดพลาด', 'error');
            }
        }).catch(function (err) { hideLoading(); showToast('เกิดข้อผิดพลาด: ' + err.message, 'error'); });
    }

    // ===== Init =====
    document.addEventListener('DOMContentLoaded', function () {
        loadDashboard();
        loadSources();
    });
</script>
</asp:Content>
