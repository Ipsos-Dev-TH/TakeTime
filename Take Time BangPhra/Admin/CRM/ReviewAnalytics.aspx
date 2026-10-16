<%@ Page Title="วิเคราะห์รีวิวทุกแพลตฟอร์ม" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ReviewAnalytics.aspx.cs" Inherits="Take_Time_BangPhra.Admin.CRM.ReviewAnalytics" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
<script src="https://cdn.jsdelivr.net/npm/apexcharts"></script>
<style>
    .ra { --br-900:#3E2723; --br-700:#5D4037; --br-500:#8D6E63; --br-300:#BCAAA4; --br-100:#EFEBE9; --br-50:#FAF7F5;
          --ink:#2b2420; --ink-2:#6d625c; --ink-3:#9a8f88; --line:#e7e0dc; --good:#2E7D32; --bad:#C62828; --warn:#B26A00;
          max-width:1500px; margin:16px auto; padding:0 16px 40px; color:var(--ink); }
    .ra *, .ra *::before, .ra *::after { box-sizing:border-box; }
    .ra-head { background:linear-gradient(135deg,var(--br-700),var(--br-500)); color:#fff; border-radius:14px; padding:22px 24px; margin-bottom:16px; }
    .ra-head h1 { margin:0 0 4px; font-size:24px; font-weight:700; }
    .ra-head p { margin:0; opacity:.9; font-size:14px; }
    .ra-links { display:flex; flex-wrap:wrap; gap:8px; margin-top:12px; }
    .ra-links a { color:#fff; background:rgba(255,255,255,.16); border:1px solid rgba(255,255,255,.3); padding:6px 12px; border-radius:999px; font-size:13px; text-decoration:none; }
    .ra-links a:hover { background:rgba(255,255,255,.28); }
    .ra-card { background:#fff; border:1px solid var(--line); border-radius:12px; padding:16px; margin-bottom:16px; }
    .ra-card h2 { margin:0 0 12px; font-size:16px; font-weight:600; color:var(--br-700); display:flex; align-items:center; gap:8px; flex-wrap:wrap; }
    .ra-card h2 small { font-weight:400; color:var(--ink-3); font-size:12px; }
    .ra-note { background:#FFF8E1; border:1px solid #FFE082; color:#6d4c00; border-radius:10px; padding:10px 14px; font-size:13px; margin-bottom:16px; }

    /* filters */
    .ra-frow { display:flex; flex-wrap:wrap; gap:10px 16px; align-items:flex-end; margin-bottom:10px; }
    .ra-f { display:flex; flex-direction:column; gap:4px; min-width:0; }
    .ra-f > label, .ra-flabel { font-size:12px; color:var(--ink-2); font-weight:500; }
    .ra input[type=date], .ra input[type=text], .ra select, .ra textarea { font:inherit; font-size:14px; padding:7px 10px; border:1px solid var(--line); border-radius:8px; background:#fff; color:var(--ink); max-width:100%; }
    .ra input:focus, .ra select:focus, .ra textarea:focus { outline:2px solid var(--br-300); border-color:var(--br-500); }
    .ra-chips { display:flex; flex-wrap:wrap; gap:6px; }
    .ra-chip { border:1px solid var(--line); background:#fff; color:var(--ink); border-radius:999px; padding:5px 12px; font-size:13px; cursor:pointer; display:inline-flex; align-items:center; gap:6px; line-height:1.3; }
    .ra-chip .dot { width:9px; height:9px; border-radius:50%; display:inline-block; }
    .ra-chip.on { background:var(--br-700); color:#fff; border-color:var(--br-700); }
    .ra-chip .cnt { font-size:11px; opacity:.75; }
    .ra-checks { display:flex; flex-wrap:wrap; gap:6px 16px; font-size:13px; }
    .ra-checks label { display:inline-flex; gap:6px; align-items:center; cursor:pointer; }
    .ra-btn { font:inherit; font-size:14px; border:1px solid var(--br-700); background:var(--br-700); color:#fff; border-radius:8px; padding:8px 14px; cursor:pointer; display:inline-flex; align-items:center; gap:6px; }
    .ra-btn:hover { background:var(--br-900); }
    .ra-btn.ghost { background:#fff; color:var(--br-700); }
    .ra-btn.ghost:hover { background:var(--br-50); }
    .ra-btn.sm { padding:4px 10px; font-size:12px; }
    .ra-btn[disabled] { opacity:.5; cursor:not-allowed; }
    .ra-actions { display:flex; flex-wrap:wrap; gap:8px; }

    /* KPI */
    .ra-kpis { display:grid; grid-template-columns:repeat(auto-fit,minmax(160px,1fr)); gap:12px; margin-bottom:16px; }
    .ra-kpi { background:#fff; border:1px solid var(--line); border-radius:12px; padding:14px 16px; }
    .ra-kpi .lbl { font-size:12px; color:var(--ink-2); }
    .ra-kpi .val { font-size:28px; font-weight:700; color:var(--ink); line-height:1.2; margin-top:4px; font-variant-numeric:tabular-nums; }
    .ra-kpi .sub { font-size:12px; color:var(--ink-3); margin-top:2px; }

    /* table */
    .ra-tablewrap { overflow-x:auto; -webkit-overflow-scrolling:touch; }
    .ra table { width:100%; border-collapse:collapse; font-size:13px; }
    .ra th { text-align:left; font-weight:600; color:var(--ink-2); background:var(--br-50); padding:8px 10px; border-bottom:1px solid var(--line); white-space:nowrap; }
    .ra td { padding:8px 10px; border-bottom:1px solid var(--line); vertical-align:top; }
    .ra td.num, .ra th.num { text-align:right; font-variant-numeric:tabular-nums; white-space:nowrap; }
    .ra-badge { display:inline-flex; align-items:center; gap:6px; padding:2px 10px; border-radius:999px; font-size:12px; font-weight:600; background:var(--br-100); color:var(--ink); white-space:nowrap; }
    .ra-badge .dot { width:8px; height:8px; border-radius:50%; }
    .ra-tag { display:inline-block; padding:1px 8px; border-radius:6px; font-size:11px; font-weight:600; margin-left:4px; }
    .ra-tag.prov { background:#E3F2FD; color:#0D47A1; }
    .ra-tag.excl { background:#FFEBEE; color:#B71C1C; }
    .ra-tag.flag { background:#FFF3E0; color:#8a4b00; }
    .ra-ok { color:var(--good); } .ra-bad { color:var(--bad); } .ra-muted { color:var(--ink-3); }

    /* charts */
    .ra-grid2 { display:grid; grid-template-columns:1fr 1fr; gap:16px; }
    @media (max-width:980px) { .ra-grid2 { grid-template-columns:1fr; } }
    .ra-chart { min-height:300px; }
    .ra-fallbar { display:flex; align-items:center; gap:8px; font-size:13px; margin:4px 0; }
    .ra-fallbar .bar { height:12px; background:var(--br-500); border-radius:0 4px 4px 0; }

    /* topics */
    .ra-topic { display:grid; grid-template-columns:minmax(90px,160px) 1fr auto; gap:8px; align-items:center; font-size:13px; margin:6px 0; }
    .ra-topic .track { height:10px; background:var(--br-100); border-radius:5px; overflow:hidden; display:flex; }
    .ra-topic .pos { background:#6D9F71; } .ra-topic .neg { background:#C0645A; } .ra-topic .oth { background:var(--br-300); }

    /* review list */
    .ra-listbar { display:flex; flex-wrap:wrap; gap:10px; align-items:center; justify-content:space-between; margin-bottom:10px; }
    .ra-rev { border:1px solid var(--line); border-radius:12px; padding:12px 14px; margin-bottom:10px; background:#fff; }
    .ra-rev.is-excl { background:#fdf6f6; border-style:dashed; }
    .ra-rev-top { display:flex; flex-wrap:wrap; justify-content:space-between; gap:8px; align-items:flex-start; }
    .ra-rev-meta { font-size:12px; color:var(--ink-2); display:flex; flex-wrap:wrap; gap:4px 12px; margin-top:4px; }
    .ra-score { text-align:right; white-space:nowrap; }
    .ra-score .big { font-size:20px; font-weight:700; font-variant-numeric:tabular-nums; }
    .ra-score .small { font-size:12px; color:var(--ink-3); }
    .ra-text { margin:8px 0 0; font-size:14px; line-height:1.6; white-space:pre-wrap; word-break:break-word; }
    .ra-text.clamp { display:-webkit-box; -webkit-line-clamp:3; -webkit-box-orient:vertical; overflow:hidden; }
    .ra-more { font-size:12px; color:var(--br-700); cursor:pointer; background:none; border:none; padding:0; text-decoration:underline; }
    .ra-resp { margin-top:8px; background:var(--br-50); border-left:3px solid var(--br-500); padding:8px 10px; border-radius:6px; font-size:13px; white-space:pre-wrap; word-break:break-word; }
    .ra-rev-actions { display:flex; flex-wrap:wrap; gap:6px; margin-top:10px; }
    .ra-respform { margin-top:8px; display:none; }
    .ra-respform.open { display:block; }
    .ra-respform textarea { width:100%; min-height:80px; }
    .ra-pager { display:flex; flex-wrap:wrap; gap:6px; align-items:center; justify-content:center; margin-top:12px; font-size:13px; }
    .ra-ai { white-space:pre-wrap; line-height:1.7; font-size:14px; background:var(--br-50); border-radius:10px; padding:14px; display:none; word-break:break-word; }
    .ra-ai.show { display:block; }
    .ra-toast { position:fixed; right:16px; bottom:16px; z-index:9999; background:var(--ink); color:#fff; padding:10px 16px; border-radius:10px; font-size:14px; opacity:0; transform:translateY(10px); transition:all .25s; pointer-events:none; max-width:calc(100vw - 32px); }
    .ra-toast.show { opacity:1; transform:none; }
    .ra-loading { color:var(--ink-3); font-size:13px; text-align:center; padding:20px; }
    @media (max-width:600px) {
        .ra-head h1 { font-size:20px; }
        .ra-kpi .val { font-size:22px; }
        .ra-frow > .ra-f { flex:1 1 140px; }
        .ra-topic { grid-template-columns:90px 1fr auto; }
    }
</style>

<div class="ra" id="ra">
    <div class="ra-head">
        <h1><i class="fas fa-chart-pie"></i> วิเคราะห์รีวิวทุกแพลตฟอร์ม</h1>
        <p>รวมรีวิวภายใน + Google / OTA / โซเชียล — คะแนนที่ไม่ทราบไม่นำมาเฉลี่ย และรายการที่ "ตัดออก" ไม่นับในสถิติ</p>
        <div class="ra-links">
            <a href="<%= ResolveUrl("~/Admin/CRM/ReviewSources") %>"><i class="fas fa-plug"></i> ตั้งค่าแหล่งรีวิว / นำเข้า</a>
            <a href="<%= ResolveUrl("~/Admin/CRM/ReviewManagement") %>"><i class="fas fa-star"></i> จัดการรีวิวภายใน</a>
            <a href="<%= ResolveUrl("~/Admin/CRM/AIReviewDashboard") %>"><i class="fas fa-robot"></i> เครื่องมือ AI รีวิว</a>
        </div>
    </div>

    <div class="ra-note" id="raMigrateNote" style="display:none">
        ยังไม่ได้รัน migration <b>PHASE19_26</b> — หน้านี้ใช้คอลัมน์เดิมไปก่อน (ไม่มีคะแนนต้นฉบับ/ลิงก์/สถานะชั่วคราว และยังตัดรายการออกจากสถิติไม่ได้)
    </div>

    <!-- ===== Filters ===== -->
    <div class="ra-card" id="raFilters">
        <h2><i class="fas fa-filter"></i> ตัวกรอง</h2>
        <div class="ra-frow">
            <div class="ra-f">
                <span class="ra-flabel">ช่วงเวลา</span>
                <div class="ra-chips" id="raPresets">
                    <button type="button" class="ra-chip" data-preset="30">30 วัน</button>
                    <button type="button" class="ra-chip" data-preset="90">90 วัน</button>
                    <button type="button" class="ra-chip on" data-preset="365">365 วัน</button>
                    <button type="button" class="ra-chip" data-preset="0">ทั้งหมด</button>
                    <button type="button" class="ra-chip" data-preset="custom">กำหนดเอง</button>
                </div>
            </div>
            <div class="ra-f"><label for="raFrom">ตั้งแต่</label><input type="date" id="raFrom" /></div>
            <div class="ra-f"><label for="raTo">ถึง</label><input type="date" id="raTo" /></div>
        </div>
        <div class="ra-frow">
            <div class="ra-f" style="flex:1 1 100%">
                <span class="ra-flabel">แพลตฟอร์ม <span class="ra-muted">(ไม่เลือก = ทั้งหมด)</span></span>
                <div class="ra-chips" id="raPlatforms"><span class="ra-muted" style="font-size:13px">กำลังโหลด...</span></div>
            </div>
        </div>
        <div class="ra-frow">
            <div class="ra-f">
                <span class="ra-flabel">คะแนน (ปัดเป็นดาว จาก /5)</span>
                <div class="ra-chips" id="raBands">
                    <button type="button" class="ra-chip" data-band="5">5★</button>
                    <button type="button" class="ra-chip" data-band="4">4★</button>
                    <button type="button" class="ra-chip" data-band="3">3★</button>
                    <button type="button" class="ra-chip" data-band="2">2★</button>
                    <button type="button" class="ra-chip" data-band="1">1★</button>
                    <button type="button" class="ra-chip" data-band="0">ไม่มีคะแนน</button>
                </div>
            </div>
            <div class="ra-f"><label for="raLang">ภาษา</label><select id="raLang"><option value="">ทุกภาษา</option></select></div>
            <div class="ra-f"><label for="raResp">การตอบกลับ</label>
                <select id="raResp"><option value="">ทั้งหมด</option><option value="yes">ตอบแล้ว</option><option value="no">ยังไม่ตอบ</option></select></div>
            <div class="ra-f"><label for="raIngest">วิธีนำเข้า</label><select id="raIngest"><option value="">ทุกวิธี</option></select></div>
            <div class="ra-f"><label for="raSent">ความรู้สึก (AI)</label>
                <select id="raSent"><option value="">ทั้งหมด</option><option value="POSITIVE">เชิงบวก</option><option value="NEGATIVE">เชิงลบ</option><option value="NEUTRAL">กลาง</option><option value="MIXED">ผสม</option><option value="NONE">ยังไม่วิเคราะห์</option></select></div>
        </div>
        <div class="ra-frow">
            <div class="ra-f" style="flex:1 1 260px"><label for="raSearch">ค้นหาข้อความ</label><input type="text" id="raSearch" placeholder="ข้อความรีวิว / ชื่อผู้รีวิว / คำตอบ" maxlength="200" /></div>
            <div class="ra-checks">
                <label><input type="checkbox" id="raInternal" checked /> รวมรีวิวภายใน</label>
                <label><input type="checkbox" id="raProv" checked /> รวมรายการชั่วคราว</label>
                <label><input type="checkbox" id="raShowExcl" /> แสดงรายการที่ตัดออก <span class="ra-muted">(เฉพาะรายการ ไม่นับสถิติ)</span></label>
            </div>
        </div>
        <div class="ra-actions">
            <button type="button" class="ra-btn" id="raApply"><i class="fas fa-search"></i> ใช้ตัวกรอง</button>
            <button type="button" class="ra-btn ghost" id="raReset"><i class="fas fa-undo"></i> ล้าง</button>
            <button type="button" class="ra-btn ghost" id="raExport"><i class="fas fa-file-csv"></i> Export CSV</button>
            <button type="button" class="ra-btn ghost" id="raAiBtn" <%= AiAvailable ? "" : "disabled title=\"ระบบ AI ปิดอยู่\"" %>><i class="fas fa-magic"></i> สรุปด้วย AI</button>
        </div>
    </div>

    <!-- ===== KPI ===== -->
    <div class="ra-kpis" id="raKpis"></div>

    <!-- ===== AI summary ===== -->
    <div class="ra-card" id="raAiCard" style="display:none">
        <h2><i class="fas fa-magic"></i> สรุปด้วย AI <small id="raAiMeta"></small>
            <button type="button" class="ra-btn ghost sm" id="raAiRefresh" style="margin-left:auto">สร้างใหม่</button></h2>
        <div class="ra-ai" id="raAiText"></div>
    </div>

    <!-- ===== Per platform ===== -->
    <div class="ra-card">
        <h2><i class="fas fa-layer-group"></i> สรุปรายแพลตฟอร์ม <small>คะแนนที่คำนวณจากรีวิวที่เก็บได้ เทียบกับคะแนนที่แพลตฟอร์มประกาศ</small></h2>
        <div class="ra-tablewrap"><table>
            <thead><tr>
                <th>แพลตฟอร์ม</th><th class="num">รีวิว</th><th class="num">เฉลี่ย /5</th><th class="num">เฉลี่ย /10</th>
                <th class="num">แพลตฟอร์มประกาศ</th><th class="num">คำนวณ (สเกลเดียวกัน)</th>
                <th class="num">บวก %</th><th class="num">ลบ %</th><th class="num">ตอบกลับ %</th><th class="num">เวลาตอบ</th>
                <th>รีวิวล่าสุด</th><th>สถานะดึงข้อมูล</th>
            </tr></thead>
            <tbody id="raPlatTable"><tr><td colspan="12" class="ra-loading">กำลังโหลด...</td></tr></tbody>
        </table></div>
    </div>

    <!-- ===== Charts ===== -->
    <div class="ra-grid2">
        <div class="ra-card"><h2><i class="fas fa-chart-line"></i> คะแนนเฉลี่ยรายเดือน (/5)</h2><div id="raTrendAvg" class="ra-chart"></div></div>
        <div class="ra-card"><h2><i class="fas fa-chart-bar"></i> จำนวนรีวิวรายเดือน</h2><div id="raTrendCnt" class="ra-chart"></div></div>
    </div>
    <div class="ra-grid2">
        <div class="ra-card"><h2><i class="fas fa-star-half-alt"></i> การกระจายคะแนน</h2><div id="raDist" class="ra-chart"></div></div>
        <div class="ra-card">
            <h2><i class="fas fa-tags"></i> หัวข้อที่ถูกพูดถึง <small>จากผลวิเคราะห์ AI ของแต่ละรีวิว</small></h2>
            <div id="raSentiment" style="font-size:13px;margin-bottom:10px"></div>
            <div id="raTopics"></div>
        </div>
    </div>

    <!-- ===== Review list ===== -->
    <div class="ra-card">
        <div class="ra-listbar">
            <h2 style="margin:0"><i class="fas fa-comments"></i> รายการรีวิว <small id="raListCount"></small></h2>
            <div class="ra-f" style="flex-direction:row;align-items:center;gap:8px">
                <label for="raSort" class="ra-flabel">เรียง</label>
                <select id="raSort">
                    <option value="date_desc">ใหม่ → เก่า</option>
                    <option value="date_asc">เก่า → ใหม่</option>
                    <option value="rating_asc">คะแนนต่ำ → สูง</option>
                    <option value="rating_desc">คะแนนสูง → ต่ำ</option>
                    <option value="platform">แพลตฟอร์ม</option>
                    <option value="unanswered">ยังไม่ตอบขึ้นก่อน</option>
                </select>
            </div>
        </div>
        <div id="raList"><div class="ra-loading">กำลังโหลด...</div></div>
        <div class="ra-pager" id="raPager"></div>
    </div>
</div>
<div class="ra-toast" id="raToast" role="status" aria-live="polite"></div>

<script>
(function () {
    'use strict';
    var TOKEN = '<%= CsrfToken %>';
    var state = { preset: '365', platforms: [], bands: [], page: 1, pageSize: 20, platformMeta: {}, charts: {}, lastFilterKey: null };
    var $ = function (id) { return document.getElementById(id); };

    // ---------- helpers ----------
    function esc(s) {
        if (s === null || s === undefined) return '';
        return String(s).replace(/[&<>"']/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]; });
    }
    function num(v, d) { return (v === null || v === undefined || isNaN(v)) ? '–' : Number(v).toFixed(d === undefined ? 1 : d); }
    function pct(v) { return (v === null || v === undefined) ? '–' : Number(v).toFixed(1) + '%'; }
    function fmtDate(iso) {
        if (!iso) return '–';
        var d = new Date(iso);
        if (isNaN(d.getTime())) return esc(iso);
        return d.toLocaleDateString('th-TH', { year: 'numeric', month: 'short', day: 'numeric' });
    }
    function fmtHours(h) {
        if (h === null || h === undefined) return '–';
        if (h < 1) return Math.round(h * 60) + ' นาที';
        if (h < 48) return num(h, 1) + ' ชม.';
        return num(h / 24, 1) + ' วัน';
    }
    function isoDay(d) { var m = d.getMonth() + 1, dd = d.getDate(); return d.getFullYear() + '-' + (m < 10 ? '0' : '') + m + '-' + (dd < 10 ? '0' : '') + dd; }
    function toast(msg) {
        var t = $('raToast'); t.textContent = msg; t.classList.add('show');
        clearTimeout(toast._t); toast._t = setTimeout(function () { t.classList.remove('show'); }, 3500);
    }
    function api(action, body) {
        return fetch('?action=' + encodeURIComponent(action), {
            method: 'POST', credentials: 'same-origin',
            headers: { 'Content-Type': 'application/json; charset=utf-8', 'X-RA-Token': TOKEN },
            body: JSON.stringify(body || {})
        }).then(function (r) {
            return r.json().catch(function () { return { success: false, message: 'HTTP ' + r.status }; });
        });
    }
    function colorOf(code) { var m = state.platformMeta[code]; return (m && m.color) || '#8D6E63'; }
    function nameOf(code) { var m = state.platformMeta[code]; return (m && m.name) || code; }
    function badge(code) {
        return '<span class="ra-badge"><span class="dot" style="background:' + esc(colorOf(code)) + '"></span>' + esc(nameOf(code)) + '</span>';
    }

    // ---------- filter state ----------
    function setPreset(p) {
        state.preset = p;
        var chips = $('raPresets').querySelectorAll('.ra-chip');
        for (var i = 0; i < chips.length; i++) chips[i].classList.toggle('on', chips[i].getAttribute('data-preset') === p);
        if (p !== 'custom') {
            if (p === '0') { $('raFrom').value = ''; $('raTo').value = ''; }
            else { var to = new Date(), from = new Date(); from.setDate(from.getDate() - parseInt(p, 10) + 1); $('raFrom').value = isoDay(from); $('raTo').value = isoDay(to); }
        }
    }
    function filter() {
        return {
            platforms: state.platforms.slice(), bands: state.bands.slice(),
            from: $('raFrom').value, to: $('raTo').value,
            language: $('raLang').value, hasResponse: $('raResp').value, ingest: $('raIngest').value, sentiment: $('raSent').value,
            includeInternal: $('raInternal').checked, includeProvisional: $('raProv').checked, showExcluded: $('raShowExcl').checked,
            search: $('raSearch').value.trim(), sort: $('raSort').value, page: state.page, pageSize: state.pageSize
        };
    }
    function toggleIn(arr, v) { var i = arr.indexOf(v); if (i >= 0) arr.splice(i, 1); else arr.push(v); }

    // ---------- options ----------
    function loadOptions() {
        return api('options').then(function (res) {
            if (!res.success) { toast(res.message || 'โหลดตัวเลือกไม่สำเร็จ'); return; }
            var d = res.data;
            $('raMigrateNote').style.display = d.migrated ? 'none' : 'block';
            var html = '';
            (d.platforms || []).forEach(function (p) {
                state.platformMeta[p.code] = p;
                html += '<button type="button" class="ra-chip" data-platform="' + esc(p.code) + '"><span class="dot" style="background:' + esc(p.color) + '"></span>' +
                        esc(p.name) + ' <span class="cnt">' + p.count + '</span></button>';
            });
            $('raPlatforms').innerHTML = html || '<span class="ra-muted" style="font-size:13px">ยังไม่มีรีวิว</span>';
            fillSelect('raLang', d.languages, 'ทุกภาษา');
            fillSelect('raIngest', d.ingestMethods, 'ทุกวิธี');
        });
    }
    function fillSelect(id, items, allLabel) {
        var el = $(id), cur = el.value, html = '<option value="">' + esc(allLabel) + '</option>';
        (items || []).forEach(function (x) { html += '<option value="' + esc(x.code) + '">' + esc(x.code) + ' (' + x.count + ')</option>'; });
        el.innerHTML = html; el.value = cur;
    }

    // ---------- analytics ----------
    function loadAnalytics() {
        $('raKpis').innerHTML = '<div class="ra-loading">กำลังคำนวณ...</div>';
        return api('analytics', { filter: filter() }).then(function (res) {
            if (!res.success) { $('raKpis').innerHTML = '<div class="ra-note">' + esc(res.message) + '</div>'; return; }
            var a = res.data;
            renderKpis(a.kpi);
            renderPlatforms(a.platforms);
            renderTrend(a.months, a.trend, a.platforms);
            renderDist(a.distribution);
            renderTopics(a.topics, a.sentiment);
        }).catch(function (e) { $('raKpis').innerHTML = '<div class="ra-note">' + esc(e.message) + '</div>'; });
    }
    function kpi(lbl, val, sub) { return '<div class="ra-kpi"><div class="lbl">' + lbl + '</div><div class="val">' + val + '</div><div class="sub">' + (sub || '') + '</div></div>'; }
    function renderKpis(k) {
        $('raKpis').innerHTML =
            kpi('คะแนนเฉลี่ย', num(k.avg5, 2) + '<span style="font-size:14px;color:#9a8f88"> /5</span>', num(k.avg10, 2) + ' /10 · จาก ' + k.rated + ' รีวิวที่มีคะแนน') +
            kpi('จำนวนรีวิว', (k.total || 0).toLocaleString(), k.excluded ? ('ตัดออก ' + k.excluded + ' รายการ (ไม่นับ)') : (k.lastDate ? 'ล่าสุด ' + fmtDate(k.lastDate) : '')) +
            kpi('เชิงบวก', pct(k.pctPositive), 'คะแนน ≥ 4 · ' + (k.positive || 0) + ' รีวิว') +
            kpi('เชิงลบ', pct(k.pctNegative), 'คะแนน ≤ 2 · ' + (k.negative || 0) + ' รีวิว') +
            kpi('อัตราการตอบกลับ', pct(k.responseRate), (k.responded || 0) + ' จาก ' + (k.total || 0)) +
            kpi('เวลาตอบเฉลี่ย', fmtHours(k.avgResponseHours), 'นับจากวันที่รีวิวถึงวันที่ตอบ');
    }
    function renderPlatforms(rows) {
        if (!rows || !rows.length) { $('raPlatTable').innerHTML = '<tr><td colspan="12" class="ra-loading">ไม่มีข้อมูลตามตัวกรอง</td></tr>'; return; }
        var html = '';
        rows.forEach(function (p) {
            state.platformMeta[p.code] = state.platformMeta[p.code] || { code: p.code, name: p.name, color: p.color };
            var pub = p.publishedRating !== null ? num(p.publishedRating, 1) + '/' + num(p.publishedScale, 0) + (p.publishedCount ? ' <span class="ra-muted">(' + Number(p.publishedCount).toLocaleString() + ')</span>' : '') : '<span class="ra-muted">–</span>';
            var comp = p.computedNative !== null ? num(p.computedNative, 2) + '/' + num(p.publishedScale, 0) : '<span class="ra-muted">–</span>';
            if (p.computedNative !== null && p.publishedRating !== null) {
                var diff = p.computedNative - p.publishedRating;
                comp += ' <span class="' + (Math.abs(diff) < 0.05 ? 'ra-muted' : (diff > 0 ? 'ra-ok' : 'ra-bad')) + '">(' + (diff > 0 ? '+' : '') + diff.toFixed(2) + ')</span>';
            }
            var st = p.fetchOk === false ? '<span class="ra-bad"><i class="fas fa-exclamation-triangle"></i> ' + esc(p.fetchStatus) + '</span>'
                   : p.fetchOk === true ? '<span class="ra-ok"><i class="fas fa-check-circle"></i> ' + esc(p.fetchStatus) + '</span>'
                   : '<span class="ra-muted">' + esc(p.fetchStatus) + '</span>';
            html += '<tr><td>' + badge(p.code) + '</td><td class="num">' + p.total + '</td><td class="num">' + num(p.avg5, 2) + '</td><td class="num">' + num(p.avg10, 2) +
                    '</td><td class="num">' + pub + '</td><td class="num">' + comp + '</td><td class="num">' + pct(p.pctPositive) + '</td><td class="num">' + pct(p.pctNegative) +
                    '</td><td class="num">' + pct(p.responseRate) + '</td><td class="num">' + fmtHours(p.avgResponseHours) + '</td><td>' + fmtDate(p.lastReview) + '</td><td style="min-width:180px">' + st + '</td></tr>';
        });
        $('raPlatTable').innerHTML = html;
    }

    function destroyChart(key) { if (state.charts[key]) { try { state.charts[key].destroy(); } catch (e) { } state.charts[key] = null; } }
    function chartsAvailable() { return typeof window.ApexCharts !== 'undefined'; }
    var baseChart = { fontFamily: 'inherit', toolbar: { show: false }, animations: { enabled: false } };

    function renderTrend(months, trend, platforms) {
        destroyChart('avg'); destroyChart('cnt');
        if (!months || !months.length) { $('raTrendAvg').innerHTML = $('raTrendCnt').innerHTML = '<div class="ra-loading">ไม่มีข้อมูล</div>'; return; }
        var codes = (platforms || []).map(function (p) { return p.code; });   // fixed order = by volume, color follows entity
        var idx = {}; months.forEach(function (m, i) { idx[m] = i; });
        var avgSeries = [], cntSeries = [];
        codes.forEach(function (c) {
            var a = months.map(function () { return null; }), n = months.map(function () { return 0; });
            trend.forEach(function (t) { if (t.platform === c) { a[idx[t.ym]] = t.avg5; n[idx[t.ym]] = t.count; } });
            avgSeries.push({ name: nameOf(c), data: a, color: colorOf(c) });
            cntSeries.push({ name: nameOf(c), data: n, color: colorOf(c) });
        });
        var labels = months.map(function (m) { var p = m.split('-'); return new Date(+p[0], +p[1] - 1, 1).toLocaleDateString('th-TH', { month: 'short', year: '2-digit' }); });
        if (!chartsAvailable()) { fallbackBars('raTrendCnt', labels, cntSeries); $('raTrendAvg').innerHTML = '<div class="ra-loading">โหลดไลบรารีกราฟไม่ได้</div>'; return; }
        $('raTrendAvg').innerHTML = ''; $('raTrendCnt').innerHTML = '';
        state.charts.avg = new ApexCharts($('raTrendAvg'), {
            chart: Object.assign({ type: 'line', height: 300 }, baseChart),
            series: avgSeries, colors: avgSeries.map(function (s) { return s.color; }),
            stroke: { width: 2, curve: 'straight' }, markers: { size: 4, strokeWidth: 2, strokeColors: '#fff' },
            xaxis: { categories: labels }, yaxis: { min: 1, max: 5, tickAmount: 4, labels: { formatter: function (v) { return v === null ? '' : v.toFixed(1); } } },
            grid: { borderColor: '#eee' }, legend: { position: 'top', horizontalAlign: 'left' },
            tooltip: { shared: true, y: { formatter: function (v) { return v === null || v === undefined ? '–' : v.toFixed(2) + ' /5'; } } }
        });
        state.charts.avg.render();
        state.charts.cnt = new ApexCharts($('raTrendCnt'), {
            chart: Object.assign({ type: 'bar', height: 300, stacked: true }, baseChart),
            series: cntSeries, colors: cntSeries.map(function (s) { return s.color; }),
            plotOptions: { bar: { columnWidth: '60%', borderRadius: 3 } }, stroke: { width: 2, colors: ['#fff'] }, dataLabels: { enabled: false },
            xaxis: { categories: labels }, yaxis: { labels: { formatter: function (v) { return Math.round(v); } } },
            grid: { borderColor: '#eee' }, legend: { position: 'top', horizontalAlign: 'left' },
            tooltip: { shared: true, intersect: false, y: { formatter: function (v) { return v + ' รีวิว'; } } }
        });
        state.charts.cnt.render();
    }
    function renderDist(dist) {
        destroyChart('dist');
        var bands = [5, 4, 3, 2, 1, 0], totals = [0, 0, 0, 0, 0, 0];
        (dist || []).forEach(function (d) { var i = bands.indexOf(d.band); if (i >= 0) totals[i] += d.count; });
        var labels = ['5★', '4★', '3★', '2★', '1★', 'ไม่มีคะแนน'];
        if (!totals.some(function (x) { return x > 0; })) { $('raDist').innerHTML = '<div class="ra-loading">ไม่มีข้อมูล</div>'; return; }
        if (!chartsAvailable()) { fallbackBars('raDist', labels, [{ name: 'รีวิว', data: totals, color: '#8D6E63' }]); return; }
        $('raDist').innerHTML = '';
        state.charts.dist = new ApexCharts($('raDist'), {
            chart: Object.assign({ type: 'bar', height: 300 }, baseChart),
            series: [{ name: 'จำนวนรีวิว', data: totals }], colors: ['#8D6E63'],
            plotOptions: { bar: { horizontal: true, barHeight: '60%', borderRadius: 3 } },
            dataLabels: { enabled: true, style: { colors: ['#3E2723'] }, offsetX: 24, formatter: function (v) { return v; } },
            xaxis: { categories: labels }, grid: { borderColor: '#eee' },
            tooltip: { y: { formatter: function (v) { var s = totals.reduce(function (a, b) { return a + b; }, 0); return v + ' รีวิว (' + (s ? (v * 100 / s).toFixed(1) : 0) + '%)'; } } }
        });
        state.charts.dist.render();
    }
    function fallbackBars(id, labels, series) {
        var max = 0, html = '';
        labels.forEach(function (l, i) { var s = 0; series.forEach(function (x) { s += x.data[i] || 0; }); max = Math.max(max, s); });
        labels.forEach(function (l, i) {
            var s = 0; series.forEach(function (x) { s += x.data[i] || 0; });
            html += '<div class="ra-fallbar"><span style="width:70px">' + esc(l) + '</span><span class="bar" style="width:' + (max ? Math.max(2, s * 70 / max) : 0) + '%"></span><span>' + s + '</span></div>';
        });
        $(id).innerHTML = html;
    }
    function renderTopics(topics, sentiment) {
        var s = sentiment || {}, lbl = { POSITIVE: 'เชิงบวก', NEGATIVE: 'เชิงลบ', NEUTRAL: 'กลาง', MIXED: 'ผสม', NONE: 'ยังไม่วิเคราะห์' }, parts = [];
        Object.keys(lbl).forEach(function (k) { if (s[k]) parts.push(lbl[k] + ' <b>' + s[k] + '</b>'); });
        $('raSentiment').innerHTML = parts.length ? 'ความรู้สึก (AI): ' + parts.join(' · ') : '';
        if (!topics || !topics.length) { $('raTopics').innerHTML = '<div class="ra-loading">ยังไม่มีผลวิเคราะห์หัวข้อ — กด "วิเคราะห์ที่รอ" ในหน้าเครื่องมือ AI รีวิว</div>'; return; }
        var max = topics[0].count, html = '';
        topics.slice(0, 15).forEach(function (t) {
            var w = t.count * 100 / max, pos = t.pctPositive * w / 100, neg = t.pctNegative * w / 100, oth = Math.max(0, w - pos - neg);
            html += '<div class="ra-topic" title="บวก ' + t.pctPositive + '% · ลบ ' + t.pctNegative + '%' + (t.avgScore !== null ? ' · คะแนนหัวข้อ ' + t.avgScore : '') + '">' +
                    '<span>' + esc(t.label) + '</span><span class="track"><span class="pos" style="width:' + pos + '%"></span><span class="oth" style="width:' + oth + '%"></span><span class="neg" style="width:' + neg + '%"></span></span>' +
                    '<span class="num">' + t.count + ' <span class="ra-' + (t.pctNegative >= 30 ? 'bad' : 'muted') + '">(ลบ ' + Math.round(t.pctNegative) + '%)</span></span></div>';
        });
        html += '<div class="ra-muted" style="font-size:12px;margin-top:8px"><span style="color:#6D9F71">■</span> บวก <span style="color:#BCAAA4">■</span> อื่น ๆ <span style="color:#C0645A">■</span> ลบ — ความยาวแท่ง = จำนวนครั้งที่ถูกพูดถึง</div>';
        $('raTopics').innerHTML = html;
    }

    // ---------- list ----------
    function loadList() {
        $('raList').innerHTML = '<div class="ra-loading">กำลังโหลด...</div>';
        return api('reviews', { filter: filter() }).then(function (res) {
            if (!res.success) { $('raList').innerHTML = '<div class="ra-note">' + esc(res.message) + '</div>'; return; }
            var d = res.data;
            if (d.rows.length === 0 && d.page > 1 && d.total > 0) { state.page = d.pages; return loadList(); }
            $('raListCount').textContent = d.total.toLocaleString() + ' รายการ';
            $('raList').innerHTML = d.rows.length ? d.rows.map(renderReview).join('') : '<div class="ra-loading">ไม่พบรีวิวตามตัวกรอง</div>';
            renderPager(d);
        }).catch(function (e) { $('raList').innerHTML = '<div class="ra-note">' + esc(e.message) + '</div>'; });
    }
    function scoreHtml(r) {
        if (r.r5 === null) return '<div class="ra-score"><div class="big ra-muted">–</div><div class="small">ไม่มีคะแนน</div></div>';
        var orig = (r.raw !== null && r.scale) ? num(r.raw, r.scale > 5 ? 1 : 0) + '/' + r.scale : null;
        return '<div class="ra-score"><div class="big">' + num(r.r5, 1) + '<span class="small"> /5</span></div><div class="small">' +
               (orig && r.scale !== 5 ? 'ต้นฉบับ ' + orig + ' · ' : '') + num(r.r10, 1) + '/10</div></div>';
    }
    function renderReview(r) {
        var key = r.kind + r.id, text = r.text || '', long = text.length > 280;
        var meta = [];
        meta.push('<i class="far fa-calendar"></i> ' + fmtDate(r.date));
        if (r.reviewer) meta.push('<i class="far fa-user"></i> ' + esc(r.reviewer));
        if (r.lang) meta.push(esc(r.lang));
        if (r.ingest) meta.push('นำเข้า: ' + esc(r.ingest));
        if (r.roomType) meta.push('ห้อง: ' + esc(r.roomType));
        if (r.traveller) meta.push(esc(r.traveller));
        if (r.stayDate) meta.push('เข้าพัก ' + fmtDate(r.stayDate));
        if (r.sentiment) meta.push('AI: ' + esc(r.sentiment));
        var h = '<div class="ra-rev' + (r.excluded ? ' is-excl' : '') + '" id="rev-' + key + '">' +
            '<div class="ra-rev-top"><div>' + badge(r.platform) +
            (r.provisional ? '<span class="ra-tag prov" title="ข้อมูลจากอีเมล/นำเข้ามือ รอข้อมูลจริง">ชั่วคราว</span>' : '') +
            (r.excluded ? '<span class="ra-tag excl">ตัดออก</span>' : '') +
            (r.flagged ? '<span class="ra-tag flag">ติดธง</span>' : '') +
            (r.title ? '<div style="font-weight:600;margin-top:6px">' + esc(r.title) + '</div>' : '') +
            '<div class="ra-rev-meta">' + meta.join('<span>·</span>') + '</div></div>' + scoreHtml(r) + '</div>';
        if (text) h += '<p class="ra-text' + (long ? ' clamp' : '') + '" id="txt-' + key + '">' + esc(text) + '</p>' +
                       (long ? '<button type="button" class="ra-more" data-act="more" data-key="' + key + '">แสดงทั้งหมด</button>' : '');
        else h += '<p class="ra-text ra-muted">(ไม่มีข้อความ)</p>';
        if (r.response) h += '<div class="ra-resp"><b><i class="fas fa-reply"></i> คำตอบเจ้าของ</b>' + (r.responseDate ? ' <span class="ra-muted">' + fmtDate(r.responseDate) + '</span>' : '') + '\n' + esc(r.response) + '</div>';
        h += '<div class="ra-rev-actions">';
        if (r.url) h += '<a class="ra-btn ghost sm" href="' + esc(r.url) + '" target="_blank" rel="noopener noreferrer"><i class="fas fa-external-link-alt"></i> เปิดที่แพลตฟอร์ม</a>';
        h += '<button type="button" class="ra-btn ghost sm" data-act="resp" data-key="' + key + '"><i class="fas fa-reply"></i> ' + (r.response ? 'แก้คำตอบ' : 'บันทึกว่าตอบแล้ว') + '</button>';
        if (r.kind === 'O') h += '<button type="button" class="ra-btn ghost sm" data-act="excl" data-kind="O" data-id="' + r.id + '" data-v="' + (r.excluded ? 'false' : 'true') + '">' +
                                (r.excluded ? '<i class="fas fa-undo"></i> นำกลับเข้าสถิติ' : '<i class="fas fa-ban"></i> ตัดออกจากสถิติ') + '</button>';
        h += '</div><div class="ra-respform" id="form-' + key + '"><textarea id="ta-' + key + '" placeholder="วางข้อความที่ตอบบนแพลตฟอร์ม (บันทึกในระบบเท่านั้น ไม่ได้โพสต์ขึ้นแพลตฟอร์ม)">' + esc(r.response || '') + '</textarea>' +
             '<div class="ra-actions" style="margin-top:6px"><input type="date" id="dt-' + key + '" value="' + (r.responseDate ? r.responseDate.substring(0, 10) : isoDay(new Date())) + '" title="วันที่ตอบ" />' +
             '<button type="button" class="ra-btn sm" data-act="saveResp" data-kind="' + r.kind + '" data-id="' + r.id + '" data-key="' + key + '">บันทึก</button>' +
             '<button type="button" class="ra-btn ghost sm" data-act="resp" data-key="' + key + '">ยกเลิก</button></div></div></div>';
        return h;
    }
    function renderPager(d) {
        if (d.pages <= 1) { $('raPager').innerHTML = ''; return; }
        var h = '<button type="button" class="ra-btn ghost sm" data-page="' + (d.page - 1) + '"' + (d.page <= 1 ? ' disabled' : '') + '>‹ ก่อนหน้า</button>';
        h += '<span>หน้า ' + d.page + ' / ' + d.pages + '</span>';
        h += '<button type="button" class="ra-btn ghost sm" data-page="' + (d.page + 1) + '"' + (d.page >= d.pages ? ' disabled' : '') + '>ถัดไป ›</button>';
        $('raPager').innerHTML = h;
    }

    // ---------- AI ----------
    function aiSummary(force) {
        $('raAiCard').style.display = 'block';
        var box = $('raAiText'); box.classList.add('show'); box.textContent = 'กำลังสรุป... (อาจใช้เวลาสักครู่)';
        $('raAiMeta').textContent = '';
        api('aiSummary', { filter: filter(), force: !!force }).then(function (res) {
            box.textContent = res.success ? res.summary : (res.message || 'สรุปไม่สำเร็จ');
            if (res.success) $('raAiMeta').textContent = (res.cached ? 'จากแคช · ' : 'สร้างใหม่ · ') + fmtDate(res.createdAt) + (res.tokens ? ' · ' + res.tokens + ' tokens' : '') + (res.note ? ' · ' + res.note : '');
        }).catch(function (e) { box.textContent = 'เกิดข้อผิดพลาด: ' + e.message; });
    }

    // ---------- events ----------
    function refreshAll() { state.page = 1; loadAnalytics(); loadList(); }
    $('raPresets').addEventListener('click', function (e) { var b = e.target.closest('[data-preset]'); if (!b) return; setPreset(b.getAttribute('data-preset')); if (state.preset !== 'custom') refreshAll(); });
    $('raFrom').addEventListener('change', function () { setPreset('custom'); });
    $('raTo').addEventListener('change', function () { setPreset('custom'); });
    $('raPlatforms').addEventListener('click', function (e) { var b = e.target.closest('[data-platform]'); if (!b) return; toggleIn(state.platforms, b.getAttribute('data-platform')); b.classList.toggle('on'); });
    $('raBands').addEventListener('click', function (e) { var b = e.target.closest('[data-band]'); if (!b) return; toggleIn(state.bands, parseInt(b.getAttribute('data-band'), 10)); b.classList.toggle('on'); });
    $('raApply').addEventListener('click', refreshAll);
    // Enter ในช่องกรอกต้องไม่ submit ฟอร์มของ Site.Master (postback)
    $('ra').addEventListener('keydown', function (e) {
        if (e.key !== 'Enter' || !e.target || e.target.tagName !== 'INPUT') return;
        e.preventDefault();
        if (e.target.id === 'raSearch') refreshAll();
    });
    $('raSort').addEventListener('change', function () { state.page = 1; loadList(); });
    $('raShowExcl').addEventListener('change', function () { state.page = 1; loadList(); });
    $('raReset').addEventListener('click', function () {
        state.platforms = []; state.bands = [];
        var on = document.querySelectorAll('#raPlatforms .ra-chip.on, #raBands .ra-chip.on');
        for (var i = 0; i < on.length; i++) on[i].classList.remove('on');
        ['raLang', 'raResp', 'raIngest', 'raSent'].forEach(function (id) { $(id).value = ''; });
        $('raSearch').value = ''; $('raInternal').checked = true; $('raProv').checked = true; $('raShowExcl').checked = false; $('raSort').value = 'date_desc';
        setPreset('365'); refreshAll();
    });
    $('raExport').addEventListener('click', function () {
        var f = filter(); delete f.page; delete f.pageSize;
        window.location.href = '?action=exportCsv&filter=' + encodeURIComponent(JSON.stringify(f));
    });
    $('raAiBtn').addEventListener('click', function () { aiSummary(false); });
    $('raAiRefresh').addEventListener('click', function () { aiSummary(true); });
    $('raPager').addEventListener('click', function (e) { var b = e.target.closest('[data-page]'); if (!b || b.disabled) return; state.page = parseInt(b.getAttribute('data-page'), 10); loadList(); $('raList').scrollIntoView({ behavior: 'smooth', block: 'start' }); });
    $('raList').addEventListener('click', function (e) {
        var b = e.target.closest('[data-act]'); if (!b) return;
        var act = b.getAttribute('data-act'), key = b.getAttribute('data-key');
        if (act === 'more') { var t = $('txt-' + key); var c = t.classList.toggle('clamp'); b.textContent = c ? 'แสดงทั้งหมด' : 'ย่อ'; }
        else if (act === 'resp') { $('form-' + key).classList.toggle('open'); }
        else if (act === 'saveResp') {
            var text = $('ta-' + key).value.trim();
            if (!text) { toast('กรุณาใส่ข้อความตอบกลับ'); return; }
            b.disabled = true;
            api('saveResponse', { kind: b.getAttribute('data-kind'), id: b.getAttribute('data-id'), text: text, date: $('dt-' + key).value }).then(function (res) {
                b.disabled = false; toast(res.message || (res.success ? 'บันทึกแล้ว' : 'ไม่สำเร็จ'));
                if (res.success) { loadList(); loadAnalytics(); }
            });
        }
        else if (act === 'excl') {
            var v = b.getAttribute('data-v') === 'true';
            if (v && !confirm('ตัดรีวิวนี้ออกจากสถิติ? (ยังเก็บไว้ ดูได้เมื่อติ๊ก "แสดงรายการที่ตัดออก")')) return;
            b.disabled = true;
            api('setExcluded', { kind: 'O', id: b.getAttribute('data-id'), excluded: v }).then(function (res) {
                b.disabled = false; toast(res.message || (res.success ? 'สำเร็จ' : 'ไม่สำเร็จ'));
                if (res.success) { loadList(); loadAnalytics(); }
            });
        }
    });

    // ---------- init ----------
    setPreset('365');
    loadOptions().then(refreshAll, refreshAll);
})();
</script>
</asp:Content>
