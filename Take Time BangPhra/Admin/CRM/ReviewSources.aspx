<%@ Page Title="แหล่งรีวิว" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ReviewSources.aspx.cs" Inherits="Take_Time_BangPhra.Admin.CRM.ReviewSources" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
<style>
    .rs-wrap { max-width: 1400px; margin: 20px auto; padding: 0 16px 40px; font-family: 'Prompt', 'Sarabun', sans-serif; color: #3e2f28; }
    .rs-head { background: linear-gradient(135deg, #5D4037 0%, #8D6E63 100%); color: #fff; padding: 22px 26px; border-radius: 12px; margin-bottom: 20px; }
    .rs-head h1 { margin: 0 0 6px; font-size: 26px; font-weight: 700; }
    .rs-head p { margin: 0; opacity: .9; font-size: 14px; }
    .rs-card { background: #fff; border-radius: 12px; box-shadow: 0 2px 8px rgba(0,0,0,.08); padding: 20px 22px; margin-bottom: 20px; }
    .rs-card h3 { margin: 0 0 14px; font-size: 18px; font-weight: 600; color: #5D4037; border-bottom: 2px solid #f0ece8; padding-bottom: 8px; }
    .rs-alert { padding: 12px 14px; border-radius: 8px; margin-bottom: 14px; font-size: 14px; }
    .rs-alert.warn { background: #fff8e1; border: 1px solid #ffe082; }
    .rs-alert.err { background: #ffebee; border: 1px solid #ef9a9a; }
    .rs-alert.ok { background: #e8f5e9; border: 1px solid #a5d6a7; }
    .rs-table { width: 100%; border-collapse: collapse; font-size: 13.5px; }
    .rs-table th { text-align: left; background: #faf7f5; padding: 9px 8px; font-weight: 600; border-bottom: 2px solid #eee; white-space: nowrap; }
    .rs-table td { padding: 9px 8px; border-bottom: 1px solid #f2f2f2; vertical-align: top; }
    .rs-table tr:hover td { background: #fffaf6; }
    .rs-scroll { overflow-x: auto; }
    .rs-badge { display: inline-block; padding: 2px 8px; border-radius: 10px; font-size: 12px; font-weight: 600; background: #eee; color: #555; white-space: nowrap; }
    .rs-badge.on { background: #e8f5e9; color: #2e7d32; }
    .rs-badge.off { background: #f5f5f5; color: #999; }
    .rs-badge.OK { background: #e8f5e9; color: #2e7d32; }
    .rs-badge.PARTIAL { background: #fff8e1; color: #f57f17; }
    .rs-badge.FAILED { background: #ffebee; color: #c62828; }
    .rs-badge.SKIPPED, .rs-badge.RUNNING { background: #eceff1; color: #546e7a; }
    .rs-err { color: #c62828; font-size: 12px; max-width: 320px; word-break: break-word; }
    .rs-muted { color: #999; font-size: 12px; }
    .rs-btn { border: none; border-radius: 6px; padding: 6px 12px; font-size: 13px; cursor: pointer; background: #5D4037; color: #fff; margin: 2px; font-family: inherit; }
    .rs-btn.sec { background: #efebe9; color: #5D4037; }
    .rs-btn.warn { background: #ef6c00; }
    .rs-btn:disabled { opacity: .5; cursor: wait; }
    .rs-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(260px, 1fr)); gap: 12px 18px; }
    .rs-field label { display: block; font-size: 13px; font-weight: 600; margin-bottom: 4px; }
    .rs-field input[type=text], .rs-field input[type=password], .rs-field input[type=number], .rs-field select, .rs-field textarea {
        width: 100%; box-sizing: border-box; padding: 7px 9px; border: 1px solid #d7ccc8; border-radius: 6px; font-size: 13.5px; font-family: inherit; }
    .rs-field .hint { font-size: 11.5px; color: #8d6e63; margin-top: 3px; }
    .rs-help { background: #faf7f5; border-left: 4px solid #8D6E63; padding: 10px 14px; border-radius: 6px; font-size: 13px; line-height: 1.6; margin: 10px 0; }
    .rs-help.legal { border-left-color: #ef6c00; background: #fff8e1; }
    .rs-modal-bg { position: fixed; inset: 0; background: rgba(0,0,0,.45); display: none; z-index: 2000; overflow-y: auto; }
    .rs-modal { background: #fff; max-width: 860px; margin: 40px auto; border-radius: 12px; padding: 22px 24px; }
    .rs-modal h3 { margin-top: 0; }
    .rs-tabs { display: flex; gap: 6px; flex-wrap: wrap; margin-bottom: 12px; }
    .rs-tabs button { border: 1px solid #d7ccc8; background: #fff; border-radius: 18px; padding: 5px 14px; cursor: pointer; font-family: inherit; }
    .rs-tabs button.active { background: #5D4037; color: #fff; border-color: #5D4037; }
    .rs-check li { margin-bottom: 6px; }
    .rs-toast { position: fixed; right: 18px; bottom: 18px; max-width: 420px; z-index: 3000; display: none; }
    @media (max-width: 700px) { .rs-head h1 { font-size: 21px; } .rs-modal { margin: 10px; padding: 16px; } }
</style>

<div class="rs-wrap">
    <div class="rs-head">
        <h1><i class="fas fa-plug"></i> แหล่งรีวิว — ดึงอัตโนมัติ / นำเข้า</h1>
        <p>ตั้งวิธีรับรีวิวจากแต่ละแพลตฟอร์ม (Google, Booking.com, Agoda, Expedia, Trip.com, Tripadvisor …) ดูสถานะการดึง และนำเข้าไฟล์จาก extranet</p>
    </div>

    <div id="rsSchemaWarn" class="rs-alert err" style="display:none">
        ⚠ ยังไม่ได้รัน migration <b>Database/PHASE19_Migration_26_Review_Aggregation.sql</b> — หน้านี้ดูได้อย่างเดียวจนกว่าจะรัน (หลังรัน recycle App Pool)
    </div>

    <!-- ค่าตั้งกลาง -->
    <div class="rs-card">
        <h3><i class="fas fa-sliders-h"></i> ค่าตั้งกลาง</h3>
        <div class="rs-grid" id="rsSettings"></div>
        <div style="margin-top:12px">
            <button type="button" class="rs-btn" id="btnSaveSettings" onclick="rsSaveSettings()">บันทึกค่าตั้ง</button>
            <button type="button" class="rs-btn sec" onclick="rsAnalyzeNow(this)">ให้ AI วิเคราะห์รีวิวที่ค้างตอนนี้</button>
            <span class="rs-muted" id="rsAiUsage"></span>
        </div>
        <div class="rs-muted" id="rsSettingsNote" style="margin-top:6px"></div>
    </div>

    <!-- ตารางแหล่ง -->
    <div class="rs-card">
        <h3><i class="fas fa-list"></i> แหล่งรีวิวทั้งหมด</h3>
        <div class="rs-scroll">
            <table class="rs-table">
                <thead><tr>
                    <th>แพลตฟอร์ม</th><th>วิธีรับรีวิว</th><th>ดึงอัตโนมัติ</th><th>รอบ</th>
                    <th>สำเร็จล่าสุด / รอบถัดไป</th><th>ปัญหาล่าสุด</th><th>คะแนนบนแพลตฟอร์ม</th><th>เก็บแล้ว</th><th></th>
                </tr></thead>
                <tbody id="rsBody"><tr><td colspan="9" class="rs-muted">กำลังโหลด…</td></tr></tbody>
            </table>
        </div>
    </div>

    <!-- ผลทดสอบดึง AGODA_LIGHT (ไม่บันทึก) -->
    <div class="rs-card" id="rsTestCard" style="display:none">
        <h3><i class="fas fa-vial"></i> ผลทดสอบดึง Agoda (ไม่บันทึก) <span class="rs-muted" id="rsTestWhen"></span></h3>
        <div id="rsTestBody"></div>
    </div>

    <!-- log -->
    <div class="rs-card">
        <h3><i class="fas fa-history"></i> บันทึกการดึง <span class="rs-muted" id="rsLogFilter"></span></h3>
        <div style="margin-bottom:8px"><button type="button" class="rs-btn sec" onclick="rsLoadLog('')">ดูทุกแหล่ง</button></div>
        <div class="rs-scroll">
            <table class="rs-table">
                <thead><tr><th>เริ่ม</th><th>แหล่ง</th><th>สถานะ</th><th>ดึง</th><th>ใหม่</th><th>อัปเดต</th><th>ข้อความ</th></tr></thead>
                <tbody id="rsLog"><tr><td colspan="7" class="rs-muted">—</td></tr></tbody>
            </table>
        </div>
    </div>

    <!-- นำเข้า -->
    <div class="rs-card">
        <h3><i class="fas fa-file-import"></i> นำเข้า / ป้อนรีวิวเอง</h3>
        <div class="rs-tabs">
            <button type="button" class="active" id="tabCsv" onclick="rsTab('csv')">นำเข้าไฟล์ CSV</button>
            <button type="button" id="tabManual" onclick="rsTab('manual')">ป้อนรีวิวทีละรายการ</button>
        </div>

        <div id="paneCsv">
            <div class="rs-help">
                <b>Booking.com:</b> Extranet → Guest reviews → ดาวน์โหลด/Export (ถ้าได้ .xlsx ให้เปิดแล้ว Save As → <i>CSV UTF-8</i>) — เลือกโปรไฟล์ "Booking.com" (สเกล 10)<br />
                <b>Agoda:</b> YCS → Performance → Guest reviews (ถ้ามีปุ่ม export) — โปรไฟล์ "Agoda" (สเกล 10)<br />
                <b>อื่น ๆ / คัดลอกเอง:</b> ใช้ <a href="?action=csvTemplate">แม่แบบ CSV ของระบบ</a> (มีคอลัมน์ platform, rating, rating_scale …)<br />
                ระบบจับหัวคอลัมน์แบบยืดหยุ่นและกันซ้ำให้เอง (นำเข้าไฟล์เดิมซ้ำได้) — เลขการจองไม่ถูกเก็บ (ใช้ทำรหัสกันซ้ำแบบแฮชเท่านั้น)
            </div>
            <div class="rs-grid">
                <div class="rs-field"><label>ผูกกับแหล่ง</label><select id="csvSource"></select></div>
                <div class="rs-field"><label>โปรไฟล์หัวคอลัมน์</label>
                    <select id="csvProfile"><option value="BOOKING">Booking.com extranet</option><option value="AGODA">Agoda YCS</option><option value="GENERIC" selected>แม่แบบของระบบ / ทั่วไป</option></select></div>
                <div class="rs-field"><label>คะแนนเต็ม (ถ้าไฟล์ไม่ระบุ)</label>
                    <select id="csvScale"><option value="">ตามแพลตฟอร์ม</option><option value="5">5</option><option value="10">10</option><option value="100">100</option></select></div>
                <div class="rs-field"><label>ไฟล์ (.csv / .tsv ≤ 5 MB)</label><input type="file" id="csvFile" accept=".csv,.tsv,.txt" /></div>
            </div>
            <div style="margin-top:12px"><button type="button" class="rs-btn" onclick="rsImportCsv(this)">นำเข้า</button></div>
            <div id="csvResult" style="margin-top:10px"></div>
        </div>

        <div id="paneManual" style="display:none">
            <div class="rs-help">ใช้กับแพลตฟอร์มที่ไม่มีทางดึงอัตโนมัติ เช่น Facebook (Meta เลิกให้ดึงรีวิวเพจตั้งแต่ Graph API v22), Airbnb, Wongnai, Traveloka — คัดลอกจากหน้า extranet มาวาง</div>
            <div class="rs-grid">
                <div class="rs-field"><label>แหล่ง *</label><select id="mSource"></select></div>
                <div class="rs-field"><label>ชื่อผู้รีวิว (ตามที่แสดง)</label><input type="text" id="mName" maxlength="100" /></div>
                <div class="rs-field"><label>คะแนน</label><input type="text" id="mRating" placeholder="เช่น 8.6 หรือ 4" /></div>
                <div class="rs-field"><label>คะแนนเต็ม</label><select id="mScale"><option value="">ตามแพลตฟอร์ม</option><option value="5">5</option><option value="10">10</option></select></div>
                <div class="rs-field"><label>วันที่รีวิว</label><input type="text" id="mDate" placeholder="yyyy-MM-dd" /></div>
                <div class="rs-field"><label>วันที่เข้าพัก</label><input type="text" id="mStay" placeholder="yyyy-MM-dd" /></div>
                <div class="rs-field"><label>ประเภทห้อง</label><input type="text" id="mRoom" maxlength="200" /></div>
                <div class="rs-field"><label>ประเภทผู้เข้าพัก</label><input type="text" id="mTrav" maxlength="100" placeholder="Couple / Family / Solo …" /></div>
                <div class="rs-field"><label>ลิงก์รีวิว</label><input type="text" id="mUrl" maxlength="1000" /></div>
                <div class="rs-field"><label>รหัสรีวิวของแพลตฟอร์ม (ถ้ามี)</label><input type="text" id="mExt" maxlength="200" /></div>
                <div class="rs-field"><label>แพลตฟอร์มที่เขียนจริง (ถ้าต่างจากแหล่ง)</label><input type="text" id="mOrigin" maxlength="50" placeholder="เช่น Booking.com ที่แสดงบน Agoda" /></div>
                <div class="rs-field"><label>ภาษา</label><input type="text" id="mLang" maxlength="10" placeholder="th / en" /></div>
            </div>
            <div class="rs-grid" style="margin-top:10px">
                <div class="rs-field"><label>หัวข้อ</label><input type="text" id="mTitle" maxlength="300" /></div>
            </div>
            <div class="rs-field" style="margin-top:10px"><label>ข้อความรีวิว</label><textarea id="mText" rows="4"></textarea></div>
            <div class="rs-field" style="margin-top:10px"><label>คำตอบของโรงแรมบนแพลตฟอร์ม (ถ้ามี)</label><textarea id="mResp" rows="2"></textarea></div>
            <div style="margin-top:12px"><button type="button" class="rs-btn" onclick="rsAddManual(this)">บันทึกรีวิว</button></div>
            <div id="manualResult" style="margin-top:10px"></div>
        </div>
    </div>

    <!-- คู่มือเจ้าของ -->
    <div class="rs-card">
        <h3><i class="fas fa-clipboard-check"></i> สิ่งที่เจ้าของต้องทำ (ครั้งเดียว)</h3>
        <ol class="rs-check">
            <li><b>Google Business Profile API</b> (ทางเดียวที่ได้รีวิว Google ทั้งหมด + คำตอบ อย่างถูกข้อกำหนด):
                ตรวจว่าโปรไฟล์ <i>ยืนยันแล้ว เปิดมาเกิน 60 วัน และมีเว็บไซต์</i> → สร้าง Google Cloud project (จดเลข project number) →
                ส่งแบบฟอร์ม <i>GBP API access request</i> จากบัญชี Owner/Manager ของโปรไฟล์ (รอ ~14 วัน โควตาจะเปลี่ยนจาก 0 เป็น 300/นาที) →
                เปิด My Business Account Management API, Business Information API, Google My Business API (v4) →
                สร้าง OAuth client (Web) ตั้ง consent screen เป็น <b>In production</b> (ไม่งั้น refresh token หมดอายุ 7 วัน) →
                ขอ refresh token ด้วย scope <code>https://www.googleapis.com/auth/business.manage</code> (เช่นผ่าน OAuth Playground ด้วย client ของเราเอง) →
                ใส่ accountId, locationId, clientId, clientSecret, refreshToken ที่แหล่ง GOOGLE แล้วเปลี่ยนวิธีเป็น GBP_API</li>
            <li><b>ถาม STAAH</b> (channel manager ที่ใช้อยู่): (ก) STAAH มีสิทธิ์ Booking.com Guest Review API / Expedia Reviews API สำหรับที่พักเราไหม
                (ข) ReviewMinder รองรับ Agoda/Trip.com/Traveloka ไหม และ export CSV / API / webhook ได้ไหม (ค) ราคา —
                ถ้าได้ จะเพิ่มตัวดึงจาก STAAH ภายหลัง</li>
            <li><b>เปิดอีเมลแจ้ง "มีรีวิวใหม่"</b> ใน extranet ทุกเจ้า (Booking.com, Agoda YCS, Expedia Partner Central, Trip.com eBooking, Traveloka TERA, Tripadvisor)
                ให้ส่งเข้ากล่องเมลเดียวกับที่ระบบอ่านอีเมลจอง (หรือ alias ที่ forward เข้ามา) → ระบบย้ายเมลรีวิวเข้า label <code>Review-Processed</code> เอง ไม่แตะเมลจอง STAAH
                <br /><span class="rs-muted">ส่งตัวอย่างอีเมลรีวิวจริงของแต่ละเจ้า 2–3 ฉบับให้ผู้พัฒนา เพื่อปรับตัวแกะให้แม่นขึ้น (อีเมลมักมีแค่คะแนน+ข้อความบางส่วน — แถวจากอีเมลจึงเป็น "ชั่วคราว" จนกว่าจะนำเข้า CSV)</span></li>
            <li><b>Export ย้อนหลังครั้งแรก</b> จาก Booking.com (Guest reviews → Download) และเจ้าอื่นที่มี แล้วนำเข้าด้านบน; ทำซ้ำเดือนละครั้งได้ (กันซ้ำอัตโนมัติ)</li>
            <li><b>Tripadvisor (ถ้าต้องการ):</b> สมัคร Content API key (ต้องมีบัตร + งบรายวัน) และอ่านเงื่อนไขการเก็บเนื้อหารีวิวก่อนเปิดเก็บข้อความ</li>
            <li><b>PDPA:</b> เพิ่มย่อหน้าในประกาศความเป็นส่วนตัวว่าเก็บรีวิวสาธารณะจากแพลตฟอร์มเพื่อวิเคราะห์คุณภาพบริการ (ประโยชน์โดยชอบด้วยกฎหมาย ม.24(5)) พร้อมระยะเก็บและช่องทางคัดค้าน — ระบบเก็บเฉพาะชื่อที่แสดง ไม่เก็บรูป/ลิงก์โปรไฟล์</li>
            <li><b>หมุน Google API key เดิม</b> ที่เคยฝังในโค้ด/Web.config (หลุดอยู่ในประวัติ git) แล้วใส่คีย์ใหม่ที่ ศูนย์ตั้งค่า (GooglePlacesApiKey) หรือที่แหล่ง GOOGLE</li>
        </ol>
        <div class="rs-help legal"><b>ข้อกฎหมาย/ข้อกำหนด:</b> Booking.com, Google Maps, Tripadvisor, Airbnb ห้ามดึงหน้าเว็บอัตโนมัติ (scraping) —
            วิธี URL_FETCH จึงปิดสำหรับตัวตั้งเวลาเป็นค่าเริ่มต้น; AGODA_LIGHT (อ่านหน้าที่พัก Agoda วันละครั้ง ไม่ crawl) เป็นการเลือกของเจ้าของ — เปิดได้เฉพาะ Owner/Admin และปิดตัวเองเมื่อโดนกันบอท; Google Places API ห้ามเก็บเนื้อหารีวิวถาวร (ได้แค่ 5 รายการ);
            รีวิวจาก Booking.com Review API (ผ่าน channel manager) ใช้ภายในเท่านั้น ห้ามนำไปแสดงบนเว็บโรงแรม</div>
    </div>
</div>

<!-- โมดัลแก้ไขแหล่ง -->
<div class="rs-modal-bg" id="rsModalBg" onclick="if(event.target===this)rsClose()">
    <div class="rs-modal">
        <h3 id="mdTitle">แก้ไขแหล่ง</h3>
        <div class="rs-grid">
            <div class="rs-field"><label>วิธีรับรีวิว</label><select id="mdMethod" onchange="rsRenderFields()"></select></div>
            <div class="rs-field"><label>รอบดึง (นาที)</label><input type="number" id="mdInterval" min="15" max="10080" />
                <div class="hint">ขั้นต่ำ 15 นาที (AGODA_LIGHT ขั้นต่ำ 720) — แนะนำ: อีเมล 60, GBP 180, Tripadvisor/Places/Agoda 1440</div></div>
            <div class="rs-field"><label>&nbsp;</label><label style="font-weight:400"><input type="checkbox" id="mdAuto" /> เปิดดึงอัตโนมัติ</label></div>
            <div class="rs-field" id="mdUrlWrap"><label>URL หน้ารีวิว / หน้าที่พัก</label><input type="text" id="mdUrl" maxlength="1000" /></div>
        </div>
        <div id="mdHelp" class="rs-help"></div>
        <div class="rs-grid" id="mdFields"></div>
        <div id="mdSecretNote" class="rs-muted" style="margin-top:8px"></div>
        <div style="margin-top:16px; text-align:right">
            <button type="button" class="rs-btn sec" onclick="rsClose()">ยกเลิก</button>
            <button type="button" class="rs-btn" onclick="rsSaveSource(this)">บันทึก</button>
        </div>
    </div>
</div>

<div class="rs-toast" id="rsToast"></div>

<script>
(function () {
    var CSRF = '<%= CsrfToken %>';
    var state = { sources: [], settings: {}, isAdmin: false, ready: false, editing: null };

    var METHOD_LABEL = {
        'GBP_API': 'Google Business Profile API', 'PLACES_API': 'Google Places API (5 รีวิว)',
        'TRIPADVISOR_API': 'Tripadvisor Content API', 'EMAIL': 'อีเมลแจ้งรีวิวใหม่',
        'CSV': 'นำเข้า CSV (มือ)', 'MANUAL': 'ป้อนเอง', 'URL_FETCH': 'ดึงหน้าเว็บ + AI (⚠ ToS)',
        'AGODA_LIGHT': 'Agoda: อ่านหน้าที่พักวันละครั้ง (light)', 'AI_SEARCH': 'ค้นหาด้วย AI (มือ)'
    };
    var AUTO_METHODS = ['GBP_API', 'PLACES_API', 'TRIPADVISOR_API', 'EMAIL', 'URL_FETCH', 'AGODA_LIGHT'];
    var AGODA_MIN_INTERVAL = 720;

    // ช่องตั้งค่าต่อวิธี — secret:true = เขียนได้อย่างเดียว (ค่าจริงไม่ถูกส่งมาที่เบราว์เซอร์)
    var FIELDS = {
        'GBP_API': [
            { k: 'accountId', l: 'accountId', h: 'เช่น 1234567890 หรือ accounts/1234567890' },
            { k: 'locationId', l: 'locationId', h: 'เช่น 987654321 หรือ locations/987654321' },
            { k: 'clientId', l: 'OAuth clientId' },
            { k: 'clientSecret', l: 'OAuth clientSecret', secret: true },
            { k: 'refreshToken', l: 'refreshToken', secret: true, h: 'scope business.manage — consent screen ต้องเป็น In production' },
            { k: 'fullResyncDays', l: 'รอบดึงเต็ม (วัน)', h: 'ค่าเริ่ม 7 — จับรีวิวที่แก้ไข/คำตอบใหม่' },
            { k: 'maxPages', l: 'จำนวนหน้าสูงสุดต่อรอบ', h: 'หน้าละ 50 รีวิว (ค่าเริ่ม 20)' }
        ],
        'PLACES_API': [
            { k: 'placeId', l: 'placeId', h: 'เว้นว่าง = ใช้ GooglePlaceId ของระบบ' },
            { k: 'apiKey', l: 'API key', secret: true, h: 'เว้นว่าง = ใช้ GooglePlacesApiKey ของระบบ (คีย์เดียวกับหน้าแรก)' },
            { k: 'ratingOnly', l: 'เก็บเฉพาะคะแนนรวม (true/false)', h: 'true = ทำตามข้อกำหนด Google (ไม่เก็บเนื้อหารีวิว)' }
        ],
        'TRIPADVISOR_API': [
            { k: 'locationId', l: 'locationId', h: 'ตัวเลขใน URL หน้าที่พัก เช่น d1234567 → 1234567' },
            { k: 'apiKey', l: 'API key', secret: true },
            { k: 'language', l: 'ภาษา', h: 'ค่าเริ่ม en' },
            { k: 'referer', l: 'Referer (ถ้าคีย์จำกัดโดเมน)' },
            { k: 'ratingOnly', l: 'เก็บเฉพาะคะแนนรวม (true/false)' }
        ],
        'EMAIL': [
            { k: 'senders', l: 'ผู้ส่ง (คั่นด้วย ,)', h: 'ส่วนหนึ่งของที่อยู่ผู้ส่ง เช่น booking.com' },
            { k: 'excludeSenders', l: 'ผู้ส่งที่ต้องข้าม', h: 'เช่น guest.booking.com (ข้อความลูกค้า ไม่ใช่รีวิว)' },
            { k: 'subjectContains', l: 'คำในหัวเรื่อง', h: 'เช่น review,รีวิว' },
            { k: 'excludeSubjects', l: 'หัวเรื่องที่ต้องข้าม' },
            { k: 'extraFolders', l: 'โฟลเดอร์อ่านอย่างเดียว', h: 'ค่าเริ่ม Chat-Processed (เมลที่ตัวอ่านแชทย้ายไปก่อน)' },
            { k: 'processedLabel', l: 'label เมื่ออ่านแล้ว', h: 'ค่าเริ่ม Review-Processed' },
            { k: 'lookbackDays', l: 'ย้อนหลัง (วัน)', h: 'ค่าเริ่ม 14' },
            { k: 'maxPerRun', l: 'สูงสุดต่อรอบ', h: 'ค่าเริ่ม 50' },
            { k: 'moveProcessed', l: 'ย้ายเมลเข้า label (true/false)', h: 'ค่าเริ่ม true' },
            { k: 'scoreRegex', l: 'regex คะแนน (ขั้นสูง)', h: 'กลุ่ม 1 = คะแนน, กลุ่ม 2 = คะแนนเต็ม' }
        ],
        'URL_FETCH': [],
        'AGODA_LIGHT': [
            { k: 'propertyId', l: 'รหัสที่พัก (propertyId)', h: 'เว้นว่าง = หาเองจากหน้าเว็บ (ดูผลที่ปุ่ม ทดสอบดึง)' },
            { k: 'apiEnabled', l: 'เรียก endpoint รีวิวเมื่อหน้าไม่มีรีวิวฝังมา (true/false)', h: 'ค่าเริ่ม true — false = อ่านเฉพาะหน้าที่พัก (1 คำขอ/รอบ)' },
            { k: 'apiUrl', l: 'endpoint รีวิว (ขั้นสูง)', h: '⚠ ค่าเริ่มยังไม่ยืนยัน: https://www.agoda.com/api/cronos/property/review/ReviewComments — ใช้ {hotelId} ได้' },
            { k: 'apiMethod', l: 'HTTP method ของ endpoint', h: 'POST (ค่าเริ่ม) หรือ GET' },
            { k: 'apiBody', l: 'body ของ POST (JSON, ใช้ {hotelId} {languageId} {locale})', h: 'เว้นว่าง = ค่าเริ่ม (หน้าแรก 20 รายการ ล่าสุดก่อน + languageId — ยังไม่ยืนยัน)' },
            { k: 'languageId', l: 'languageId ของ Agoda', h: '⚠ ยังไม่ยืนยัน: ค่าเริ่ม 22 (ไทย) — "off" = ไม่ใส่ languageId ใน body (กลับเป็น body เดิมที่ทดสอบผ่าน)' },
            { k: 'locale', l: 'locale', h: 'ค่าเริ่ม th-th (ใช้กับ Accept-Language และ {locale})' },
            { k: 'sendLocaleHeaders', l: 'ส่ง header ag-language-locale / ag-language-id (true/false)', h: 'ค่าเริ่ม false — ลองเปิดถ้ารีวิวยังมาเป็นคำแปลอังกฤษ' },
            { k: 'summaryUrl', l: 'endpoint สรุปคะแนน (ไม่บังคับ)', h: 'เว้นว่าง = ปิด; ตั้งแล้วเรียกเฉพาะเมื่อหาคะแนนรวมจาก JSON-LD/endpoint รีวิว/หน้าเว็บไม่ได้ (งบคำขอ +1)' },
            { k: 'summaryMethod', l: 'method ของ endpoint สรุป', h: 'GET (ค่าเริ่ม) หรือ POST' },
            { k: 'summaryBody', l: 'body ของ endpoint สรุป (POST)', h: 'ค่าเริ่ม {"hotelId":{hotelId}}' },
            { k: 'apiHeaders', l: 'header เพิ่มเติม', h: 'รูปแบบ Name: value | Name2: value' },
            { k: 'forwardCookies', l: 'ส่งคุกกี้จากหน้าที่พักต่อให้ endpoint (true/false)', h: 'ค่าเริ่ม true' },
            { k: 'providerMap', l: 'providerId → แพลตฟอร์ม', h: 'ค่าเริ่ม 332:Agoda (อื่น ๆ ยังไม่ยืนยัน เช่น 3038:Booking.com)' },
            { k: 'maxItems', l: 'จำนวนรีวิวสูงสุดต่อรอบ', h: 'ค่าเริ่ม 20 (หน้าแรกเท่านั้น)' }
        ],
        'AI_SEARCH': [{ k: 'searchKeyword', l: 'คำค้น', h: 'ใช้กับปุ่มค้นหาด้วย AI ในหน้าวิเคราะห์รีวิว' }],
        'CSV': [], 'MANUAL': []
    };

    var HELP = {
        'GBP_API': '<b>แนะนำสำหรับ Google</b> — ได้รีวิวทั้งหมด + คำตอบของโรงแรม ฟรี ถูกข้อกำหนด (ต้องได้รับอนุมัติ GBP API ก่อน ~14 วัน ดูขั้นตอนท้ายหน้า). ดึงเฉพาะที่อัปเดตตั้งแต่รอบก่อน และดึงเต็มเป็นระยะ',
        'PLACES_API': '<b>ชั่วคราว</b> ระหว่างรอ GBP — ได้สูงสุด <b>5 รีวิว</b>ต่อครั้ง ย้อนหลังไม่ได้ และข้อกำหนด Google Maps Platform ข้อ 3.2.3 <b>ห้ามเก็บเนื้อหารีวิวถาวร</b> (ตั้ง ratingOnly = true เพื่อเก็บเฉพาะคะแนนรวม/จำนวนรีวิว). เกิน 1,000 ครั้ง/เดือนมีค่าใช้จ่าย',
        'TRIPADVISOR_API': 'ได้ 5 รีวิวล่าสุดต่อครั้ง + คะแนนรวม — ดึงวันละครั้งก็พอสำหรับที่พักขนาดเรา. ข้อกำหนด Content API เน้นการแสดงผล: ตรวจเงื่อนไขการเก็บก่อน (หรือ ratingOnly = true)',
        'EMAIL': 'อ่านอีเมล "มีรีวิวใหม่" จากกล่องเมลเดียวกับการอ่านอีเมลจอง (ตั้งที่หน้า อ่านอีเมลจอง) — ไม่ขัดข้อกำหนดใด ๆ. อีเมลมักมีแค่คะแนน/ข้อความบางส่วน ⇒ แถวเป็น <b>ชั่วคราว</b> และจะถูกเติมข้อมูลเมื่อนำเข้า CSV ของรีวิวเดียวกัน. ไม่มีอีเมลแจ้งเตือน = เปิดที่ extranet ก่อน',
        'CSV': 'ไม่มีการดึงอัตโนมัติ — export จาก extranet แล้วนำเข้าที่ส่วน "นำเข้า" ด้านล่าง (เดือนละครั้ง / ย้อนหลังครั้งแรก)',
        'MANUAL': 'ไม่มีการดึงอัตโนมัติ — ป้อนทีละรีวิวที่ส่วน "นำเข้า" (เช่น Facebook ที่ Meta เลิกให้ API แล้ว, Airbnb, Wongnai)',
        'URL_FETCH': '<b style="color:#c62828">⚠ ขัดข้อกำหนดการใช้งานของ Booking.com / Agoda / Tripadvisor / Google</b> และพังบ่อย (หน้า render ด้วย JavaScript / กันบอท). ตัวตั้งเวลาจะไม่ทำงานจนกว่า Owner เปิด "อนุญาตดึงจากหน้าเว็บ OTA" ในค่าตั้งกลาง — ใช้ token AI ทุกครั้ง',
        'AGODA_LIGHT': '<b>Agoda แบบเบา</b> — ไม่ crawl: ต่อรอบอ่านหน้าที่พัก 1 ครั้ง (คะแนนรวมจาก JSON-LD + รีวิวที่หน้าแสดง) และถ้าหน้าไม่มีรีวิวจะเรียก endpoint รีวิวหน้าแรกอีก 1 ครั้ง (รวมไม่เกิน 2 คำขอ). '
            + 'รอบขั้นต่ำ 720 นาที (แนะนำ 1440), เพิ่มเฉพาะรีวิวใหม่, เก็บแค่ชื่อที่แสดง. โดนกันบอท 3 ครั้งติดระบบปิดดึงอัตโนมัติเองและแจ้งเตือน. '
            + 'เปิดดึงอัตโนมัติได้เฉพาะ Owner/Admin — กด <b>ทดสอบดึง (ไม่บันทึก)</b> ก่อนเปิด. '
            + '<br/><b style="color:#c62828">⚠ ข้อกำหนดการใช้งานของ Agoda ไม่อนุญาตการดึงข้อมูลอัตโนมัติ</b> — เจ้าของเลือกใช้เองโดยรับความเสี่ยง (ทางที่ถูกข้อกำหนดคือ อีเมลแจ้งรีวิว / CSV จาก YCS)',
        'AI_SEARCH': 'ค้นหาเว็บ/สังคมออนไลน์ด้วย AI จากหน้าวิเคราะห์รีวิวเดิม (กดเอง ไม่ตั้งเวลา) — ข้อมูลไม่แน่นอน ใช้เป็นสัญญาณเท่านั้น',
        '': 'ยังไม่ได้เลือกวิธี'
    };

    var SETTINGS = [
        { k: 'Review_Aggregation_Enabled', l: 'เปิดดึงรีวิวอัตโนมัติ (สวิตช์ใหญ่)', t: 'bool' },
        { k: 'Review_AI_AutoAnalyze', l: 'ให้ AI วิเคราะห์รีวิวใหม่อัตโนมัติ', t: 'bool' },
        { k: 'Review_AI_Daily_Token_Budget', l: 'งบ token AI ต่อวัน (รีวิว)', t: 'num' },
        { k: 'Review_Negative_Threshold', l: 'แจ้งเตือนรีวิวคะแนน ≤ (ดาว /5)', t: 'num' },
        { k: 'Review_Alert_MaxAgeDays', l: 'แจ้งเตือนเฉพาะรีวิวอายุ ≤ (วัน)', t: 'num' },
        { k: 'Review_FetchFail_Alert_After', l: 'แจ้งเตือนเมื่อดึงล้มเหลวติดกัน (ครั้ง)', t: 'num' },
        { k: 'Review_UrlFetch_Allowed', l: '⚠ อนุญาตดึงจากหน้าเว็บ OTA อัตโนมัติ (ขัด ToS)', t: 'bool' }
    ];

    function esc(s) { return (s === null || s === undefined) ? '' : String(s).replace(/[&<>"']/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]; }); }
    function $(id) { return document.getElementById(id); }

    function toast(msg, kind) {
        var t = $('rsToast');
        t.innerHTML = '<div class="rs-alert ' + (kind || 'ok') + '">' + esc(msg) + '</div>';
        t.style.display = 'block';
        clearTimeout(t._h); t._h = setTimeout(function () { t.style.display = 'none'; }, 7000);
    }

    function api(action, data) {
        return fetch('?action=' + encodeURIComponent(action), {
            method: 'POST', credentials: 'same-origin',
            headers: { 'Content-Type': 'application/json', 'X-CSRF-Token': CSRF },
            body: JSON.stringify(data || {})
        }).then(function (r) { return r.json(); });
    }

    function load() {
        api('getSources').then(function (res) {
            if (!res.success) { toast(res.message, 'err'); return; }
            state.sources = res.sources; state.settings = res.settings; state.isAdmin = res.isSecretAdmin; state.ready = res.schemaReady;
            state.emailDefaults = res.emailDefaults || {};
            $('rsSchemaWarn').style.display = res.schemaReady ? 'none' : 'block';
            $('rsAiUsage').textContent = 'token AI รีวิววันนี้: ' + (res.aiUsedToday || 0) + ' / ' + (res.settings.Review_AI_Daily_Token_Budget || '-');
            renderSettings(); renderTable(); fillSourceSelects();
        }).catch(function (e) { toast('โหลดข้อมูลไม่สำเร็จ: ' + e, 'err'); });
    }

    function renderSettings() {
        var h = '';
        SETTINGS.forEach(function (s) {
            var v = state.settings[s.k] || '';
            var dis = state.isAdmin ? '' : ' disabled';
            if (s.t === 'bool')
                h += '<div class="rs-field"><label style="font-weight:400"><input type="checkbox" data-set="' + s.k + '"' + (v === '1' || v === 'true' ? ' checked' : '') + dis + ' /> ' + esc(s.l) + '</label></div>';
            else
                h += '<div class="rs-field"><label>' + esc(s.l) + '</label><input type="number" min="0" data-set="' + s.k + '" value="' + esc(v) + '"' + dis + ' /></div>';
        });
        $('rsSettings').innerHTML = h;
        $('btnSaveSettings').disabled = !state.isAdmin;
        $('rsSettingsNote').textContent = state.isAdmin ? '' : 'แก้ค่าตั้งกลางได้เฉพาะ Owner / Admin';
    }

    window.rsSaveSettings = function () {
        var d = {};
        document.querySelectorAll('[data-set]').forEach(function (el) { d[el.getAttribute('data-set')] = el.type === 'checkbox' ? (el.checked ? '1' : '0') : el.value; });
        api('saveSettings', d).then(function (r) { toast(r.message + (r.warning ? ' — ' + r.warning : ''), r.success ? (r.warning ? 'warn' : 'ok') : 'err'); if (r.success) load(); });
    };

    window.rsAnalyzeNow = function (btn) {
        btn.disabled = true;
        api('analyzeNow').then(function (r) { btn.disabled = false; toast(r.message, r.success ? 'ok' : 'err'); load(); })
            .catch(function () { btn.disabled = false; });
    };

    function fmtRating(s) {
        if (s.platformRating === null || s.platformRating === undefined) return '<span class="rs-muted">—</span>';
        return '<b>' + Number(s.platformRating).toFixed(1) + '</b>/' + (s.platformScale ? Number(s.platformScale).toFixed(0) : '?')
            + (s.platformCount ? '<div class="rs-muted">' + s.platformCount + ' รีวิว</div>' : '');
    }

    function renderTable() {
        var h = '';
        state.sources.forEach(function (s, i) {
            var auto = AUTO_METHODS.indexOf(s.method) >= 0;
            h += '<tr>'
                + '<td><i class="' + esc(s.icon || 'fas fa-star') + '" style="color:' + esc(s.color || '#5D4037') + '"></i> <b>' + esc(s.name) + '</b><div class="rs-muted">' + esc(s.code) + '</div></td>'
                + '<td>' + esc(METHOD_LABEL[s.method] || (s.method || '— ยังไม่ตั้ง —')) + '</td>'
                + '<td>' + (auto ? (s.autoEnabled ? '<span class="rs-badge on">เปิด</span>' : '<span class="rs-badge off">ปิด</span>') : '<span class="rs-muted">มือ</span>') + '</td>'
                + '<td>' + (auto ? s.intervalMin + ' นาที' : '—') + '</td>'
                + '<td>' + (esc(s.lastSuccessAt) || '<span class="rs-muted">ยังไม่เคย</span>') + (s.autoEnabled && s.nextFetchAt ? '<div class="rs-muted">ถัดไป ' + esc(s.nextFetchAt) + '</div>' : '') + '</td>'
                + '<td>' + (s.lastError ? '<div class="rs-err">' + (s.failures > 0 ? '❌ ×' + s.failures + ' ' : '') + esc(s.lastError) + '</div>' : '<span class="rs-muted">—</span>') + '</td>'
                + '<td>' + fmtRating(s) + '</td>'
                + '<td>' + (s.totalReviews || 0) + '</td>'
                + '<td style="white-space:nowrap">'
                + '<button type="button" class="rs-btn sec" onclick="rsEdit(' + i + ')">ตั้งค่า</button>'
                + (auto ? '<button type="button" class="rs-btn" onclick="rsFetch(' + i + ', this)">ดึงตอนนี้</button>' : '')
                + (s.method === 'AGODA_LIGHT' ? '<button type="button" class="rs-btn sec" onclick="rsTestAgoda(' + i + ', this)">ทดสอบดึง (ไม่บันทึก)</button>' : '')
                + '<button type="button" class="rs-btn sec" onclick="rsLoadLog(\'' + esc(s.code) + '\')">log</button>'
                + '</td></tr>';
        });
        $('rsBody').innerHTML = h || '<tr><td colspan="9" class="rs-muted">ไม่มีแหล่งรีวิว</td></tr>';
    }

    function fillSourceSelects() {
        var opts = state.sources.map(function (s) { return '<option value="' + esc(s.code) + '">' + esc(s.name) + '</option>'; }).join('');
        $('csvSource').innerHTML = opts; $('mSource').innerHTML = opts;
        var b = state.sources.filter(function (s) { return s.code === 'BOOKING'; });
        if (b.length) $('csvSource').value = 'BOOKING';
    }

    window.rsFetch = function (i, btn) {
        var s = state.sources[i];
        btn.disabled = true; btn.textContent = 'กำลังดึง…';
        api('fetchNow', { code: s.code }).then(function (r) {
            btn.disabled = false; btn.textContent = 'ดึงตอนนี้';
            var kind = r.status === 'OK' ? 'ok' : (r.status === 'FAILED' ? 'err' : 'warn');
            toast(s.name + ': ' + (r.message || r.status), kind);
            load(); rsLoadLog(s.code);
        }).catch(function (e) { btn.disabled = false; btn.textContent = 'ดึงตอนนี้'; toast('ดึงไม่สำเร็จ: ' + e, 'err'); });
    };

    // ── ทดสอบดึง AGODA_LIGHT (dry-run) — ทุกค่าที่มาจาก Agoda ผ่าน esc() ──
    window.rsTestAgoda = function (i, btn) {
        var s = state.sources[i];
        btn.disabled = true; btn.textContent = 'กำลังทดสอบ…';
        api('testAgoda', { code: s.code }).then(function (r) {
            btn.disabled = false; btn.textContent = 'ทดสอบดึง (ไม่บันทึก)';
            if (!r.success) { toast(r.message, 'warn'); return; }
            renderAgodaDiag(s, r.diag);
        }).catch(function (e) { btn.disabled = false; btn.textContent = 'ทดสอบดึง (ไม่บันทึก)'; toast('ทดสอบไม่สำเร็จ: ' + e, 'err'); });
    };

    function renderAgodaDiag(s, d) {
        var h = '';
        var kind = d.blocked ? 'err' : (d.error ? 'err' : (d.reviewsParsed > 0 ? 'ok' : 'warn'));
        h += '<div class="rs-alert ' + kind + '">'
            + (d.blocked ? '⛔ <b>โดนกันบอท</b> — ' : (d.error ? '❌ ' : (d.reviewsParsed > 0 ? '✅ ' : '⚠ ')))
            + esc(d.error || ('แกะได้ ' + d.reviewsParsed + ' รีวิว · เส้นทาง: ' + d.parsePath)) + '</div>';
        h += '<table class="rs-table"><thead><tr><th>คำขอ</th><th>method</th><th>URL</th><th>HTTP</th><th>ขนาด</th><th>เวลา</th><th>ตัวบ่งชี้กันบอท</th></tr></thead><tbody>';
        (d.requests || []).forEach(function (q) {
            h += '<tr><td>' + esc(q.label) + (q.hops > 1 ? ' <span class="rs-muted">(' + q.hops + ' hops)</span>' : '') + '</td><td>' + esc(q.method) + '</td>'
                + '<td style="word-break:break-all;max-width:360px">' + esc(q.url) + '</td>'
                + '<td>' + (q.error ? '<span class="rs-err">' + esc(q.error) + '</span>' : esc(q.status)) + '</td>'
                + '<td>' + esc(Math.round((q.bytes || 0) / 1024)) + ' KB</td><td>' + esc(q.ms) + ' ms</td>'
                + '<td>' + (q.block ? '<span class="rs-badge FAILED">' + esc(q.block) + '</span>' : '<span class="rs-muted">ไม่พบ</span>') + '</td></tr>';
        });
        h += '</tbody></table>';
        h += '<ul style="margin:12px 0;line-height:1.8">'
            + '<li>JSON-LD: ' + (d.jsonLdFound ? 'พบ ' + esc(d.jsonLdBlocks) + ' บล็อก' : 'ไม่พบ') + '</li>'
            + '<li>คะแนนรวม: ' + (d.rating !== null && d.rating !== undefined
                ? '<b>' + esc(d.rating) + '/' + esc(d.ratingBest || 10) + '</b>' + (d.ratingCount ? ' (' + esc(d.ratingCount) + ' รีวิว)' : '')
                  + ' <span class="rs-muted">จาก <code>' + esc(d.ratingSource) + '</code></span>'
                : '<span class="rs-err">ไม่พบอย่างมั่นใจ (ไม่ตั้งค่าเดา)</span>') + '</li>'
            + ((d.ratingCandidates || []).length ? '<li>ค่าที่พบ (คีย์=ค่า, p=ความน่าเชื่อถือ): <div class="rs-muted" style="word-break:break-all">'
                + d.ratingCandidates.map(esc).join(' · ') + '</div></li>' : '')
            + (d.apiTopKeys ? '<li>คีย์ชั้นบนของคำตอบ endpoint: <div class="rs-muted" style="word-break:break-all"><code>' + esc(d.apiTopKeys) + '</code></div></li>' : '')
            + (d.commentFields ? '<li>ฟิลด์ของ comment แรก: <div class="rs-muted" style="word-break:break-all"><code>' + esc(d.commentFields) + '</code></div>'
                + (d.commentShortValues ? '<div class="rs-muted" style="word-break:break-all">ค่าสั้น: <code>' + esc(d.commentShortValues) + '</code></div>' : '') + '</li>' : '')
            + '<li>รหัสที่พัก: ' + (d.propertyId ? '<b>' + esc(d.propertyId) + '</b> <span class="rs-muted">(' + esc(d.propertyIdPattern) + ')</span>' : '<span class="rs-err">ไม่พบ</span>')
            + (d.propertyIdCandidates ? ' <span class="rs-muted">อื่น ๆ: ' + esc(d.propertyIdCandidates) + '</span>' : '') + '</li>'
            + '<li>เส้นทาง HTML (DOM รายการรีวิว): ' + (d.htmlDomReviews > 0 ? '<b>พบ ' + esc(d.htmlDomReviews) + ' รีวิว</b>'
                : (d.htmlDomBlocks > 0 ? '<span class="rs-err">เห็น ' + esc(d.htmlDomBlocks) + ' บล็อก แต่แกะไม่ได้</span>' : 'ไม่พบใน HTML ที่เซิร์ฟเวอร์ส่งมา')) + '</li>'
            + '<li>เส้นทางที่ใช้ได้: <code>' + esc(d.parsePath) + '</code> · แกะได้ <b>' + esc(d.reviewsParsed) + '</b> รีวิว</li>'
            + ((d.unknownProviders || []).length ? '<li>providerId ไม่รู้จัก: ' + esc(d.unknownProviders.join(', ')) + ' (ตั้ง providerMap)</li>' : '')
            + '</ul>';
        if ((d.previews || []).length) {
            h += '<div class="rs-muted" style="margin-bottom:4px">ตัวอย่าง 3 รายการแรก</div>';
            d.previews.forEach(function (p) {
                h += '<div class="rs-help">⭐ <b>' + esc(p.rating === null || p.rating === undefined ? '?' : p.rating) + '/' + esc(p.scale || 10) + '</b> · '
                    + esc(p.origin) + ' · ' + esc(p.name || '(ไม่มีชื่อ)') + ' · ' + esc(p.date || 'ไม่มีวันที่')
                    + (p.extId ? ' <span class="rs-muted">' + esc(p.extId) + '</span>' : '')
                    + ' · ภาษา ' + esc(p.language || '?') + (p.machineTranslated ? ' <span class="rs-badge PARTIAL">แปลโดย Agoda</span>' : '')
                    + (p.title ? '<div><b>' + esc(p.title) + '</b></div>' : '') + '<div style="white-space:pre-line">' + esc(p.text) + '</div>'
                    + (p.translatedText || p.translatedTitle ? '<div class="rs-muted" style="margin-top:4px;white-space:pre-line">คำแปลที่ Agoda แสดง (ไม่บันทึก): '
                        + (p.translatedTitle ? '<b>' + esc(p.translatedTitle) + '</b> ' : '') + esc(p.translatedText || '') + '</div>' : '')
                    + '</div>';
            });
        }
        if ((d.warnings || []).length) h += '<div class="rs-alert warn">' + d.warnings.map(esc).join('<br/>') + '</div>';
        h += '<div class="rs-muted">บรรทัดสรุป (แบบเดียวกับที่บันทึกใน log ตอนดึงจริง): <code>' + esc(d.compact) + '</code></div>';
        $('rsTestBody').innerHTML = h;
        $('rsTestWhen').textContent = '(' + s.name + ' · ' + new Date().toLocaleString('th-TH') + ')';
        $('rsTestCard').style.display = 'block';
        $('rsTestCard').scrollIntoView({ behavior: 'smooth' });
    }

    window.rsLoadLog = function (code) {
        $('rsLogFilter').textContent = code ? '(' + code + ')' : '(ทุกแหล่ง)';
        api('getLog', { code: code, limit: 50 }).then(function (r) {
            var h = '';
            (r.rows || []).forEach(function (x) {
                h += '<tr><td style="white-space:nowrap">' + esc(x.startedAt) + '</td><td>' + esc(x.name || x.code) + '</td>'
                    + '<td><span class="rs-badge ' + esc(x.status) + '">' + esc(x.status) + '</span></td>'
                    + '<td>' + (x.fetched === null ? '' : x.fetched) + '</td><td>' + (x.inserted === null ? '' : x.inserted) + '</td><td>' + (x.updated === null ? '' : x.updated) + '</td>'
                    + '<td style="max-width:520px;word-break:break-word">' + esc(x.message) + '</td></tr>';
            });
            $('rsLog').innerHTML = h || '<tr><td colspan="7" class="rs-muted">ยังไม่มีบันทึก</td></tr>';
        });
    };

    // ── แก้ไขแหล่ง ──
    window.rsEdit = function (i) {
        var s = state.sources[i];
        state.editing = s;
        $('mdTitle').textContent = 'ตั้งค่าแหล่ง: ' + s.name + ' (' + s.code + ')';
        var mopts = '<option value="">— ยังไม่ตั้ง —</option>';
        Object.keys(METHOD_LABEL).forEach(function (m) { mopts += '<option value="' + m + '">' + esc(METHOD_LABEL[m]) + '</option>'; });
        $('mdMethod').innerHTML = mopts;
        $('mdMethod').value = s.method || '';
        $('mdInterval').value = s.intervalMin || 1440;
        $('mdAuto').checked = !!s.autoEnabled;
        $('mdUrl').value = s.sourceUrl || '';
        rsRenderFields();
        $('rsModalBg').style.display = 'block';
    };

    window.rsClose = function () { $('rsModalBg').style.display = 'none'; state.editing = null; };

    window.rsRenderFields = function () {
        var s = state.editing; if (!s) return;
        var m = $('mdMethod').value;
        $('mdHelp').innerHTML = HELP[m] || '';
        $('mdHelp').className = 'rs-help' + (m === 'URL_FETCH' || m === 'PLACES_API' || m === 'AGODA_LIGHT' ? ' legal' : '');
        $('mdUrlWrap').style.display = (m === 'URL_FETCH' || m === 'AGODA_LIGHT') ? '' : 'none';
        $('mdInterval').min = m === 'AGODA_LIGHT' ? AGODA_MIN_INTERVAL : 15;
        if (m === 'AGODA_LIGHT' && Number($('mdInterval').value) < AGODA_MIN_INTERVAL) $('mdInterval').value = 1440;
        if (m === 'AGODA_LIGHT' && s.code === 'AGODA' && !$('mdUrl').value) $('mdUrl').value = 'https://www.agoda.com/th-th/take-time-mirror-room/hotel/chonburi-th.html';
        var cfg = s.config || {};
        var h = '';
        (FIELDS[m] || []).forEach(function (f) {
            var v = cfg[f.k];
            if (f.secret) {
                var set = v && String(v).length > 0;
                h += '<div class="rs-field"><label>' + esc(f.l) + ' 🔒</label>'
                    + '<input type="password" autocomplete="new-password" data-cfg="' + f.k + '" data-secret="1" placeholder="' + (set ? 'ตั้งไว้แล้ว (' + esc(v) + ') — เว้นว่าง = คงเดิม' : 'ยังไม่ได้ตั้ง') + '"' + (state.isAdmin ? '' : ' disabled') + ' />'
                    + (set && state.isAdmin ? '<label style="font-weight:400;font-size:12px"><input type="checkbox" data-clear="' + f.k + '" /> ล้างค่านี้</label>' : '')
                    + (f.h ? '<div class="hint">' + esc(f.h) + '</div>' : '') + '</div>';
            } else {
                var def = '';
                if (m === 'EMAIL' && state.emailDefaults && state.emailDefaults[s.code]) {
                    var d = state.emailDefaults[s.code];
                    def = f.k === 'senders' ? d[0] : f.k === 'excludeSenders' ? d[1] : f.k === 'subjectContains' ? d[2] : '';
                }
                h += '<div class="rs-field"><label>' + esc(f.l) + '</label><input type="text" data-cfg="' + f.k + '" value="' + esc(v === undefined || v === null ? '' : v) + '"'
                    + (def ? ' placeholder="ค่าเริ่ม: ' + esc(def) + '"' : '') + ' />'
                    + (f.h ? '<div class="hint">' + esc(f.h) + '</div>' : '') + '</div>';
            }
        });
        $('mdFields').innerHTML = h;
        $('mdSecretNote').textContent = state.isAdmin
            ? 'ช่อง 🔒 เขียนได้อย่างเดียว — ค่าจริงถูกเข้ารหัสในฐานข้อมูลและไม่ถูกส่งกลับมาที่หน้าเว็บ'
            : 'ช่อง 🔒 (API key / token) แก้ได้เฉพาะ Owner / Admin';
    };

    window.rsSaveSource = function (btn) {
        var s = state.editing; if (!s) return;
        // คงคีย์เดิมที่ไม่ใช่ความลับ (เช่น searchKeyword ของหน้าเดิม) แล้วทับด้วยค่าที่แก้
        var cfg = {};
        Object.keys(s.config || {}).forEach(function (k) {
            var v = s.config[k];
            if (typeof v === 'string' && v.indexOf('••••') === 0) return;   // ความลับ — ไม่ส่งค่า mask กลับ
            cfg[k] = v;
        });
        document.querySelectorAll('#mdFields [data-cfg]').forEach(function (el) {
            var k = el.getAttribute('data-cfg');
            if (el.getAttribute('data-secret')) { if (el.value) cfg[k] = el.value; }
            else if (el.value.trim() === '') delete cfg[k];
            else cfg[k] = el.value.trim();
        });
        document.querySelectorAll('#mdFields [data-clear]').forEach(function (el) { if (el.checked) cfg[el.getAttribute('data-clear')] = '__CLEAR__'; });
        btn.disabled = true;
        api('saveSource', {
            code: s.code, method: $('mdMethod').value, intervalMin: $('mdInterval').value,
            autoEnabled: $('mdAuto').checked, sourceUrl: $('mdUrl').value, config: cfg
        }).then(function (r) {
            btn.disabled = false;
            if (r.success) { rsClose(); load(); }
            toast(r.message + (r.warning ? ' — ' + r.warning : ''), r.success ? (r.warning ? 'warn' : 'ok') : 'err');
        }).catch(function (e) { btn.disabled = false; toast('บันทึกไม่สำเร็จ: ' + e, 'err'); });
    };

    // ── นำเข้า ──
    window.rsTab = function (t) {
        $('paneCsv').style.display = t === 'csv' ? '' : 'none';
        $('paneManual').style.display = t === 'manual' ? '' : 'none';
        $('tabCsv').className = t === 'csv' ? 'active' : '';
        $('tabManual').className = t === 'manual' ? 'active' : '';
    };

    window.rsImportCsv = function (btn) {
        var f = $('csvFile').files[0];
        if (!f) { toast('ยังไม่ได้เลือกไฟล์', 'warn'); return; }
        var fd = new FormData();
        fd.append('csrf', CSRF); fd.append('code', $('csvSource').value);
        fd.append('profile', $('csvProfile').value); fd.append('scale', $('csvScale').value); fd.append('file', f);
        btn.disabled = true;
        fetch('?action=importCsv', { method: 'POST', credentials: 'same-origin', body: fd })
            .then(function (r) { return r.json(); })
            .then(function (r) {
                btn.disabled = false;
                $('csvResult').innerHTML = '<div class="rs-alert ' + (r.success ? 'ok' : 'err') + '">' + esc(r.message)
                    + (r.aiNote ? '<div class="rs-muted">AI: ' + esc(r.aiNote) + '</div>' : '') + '</div>';
                if (r.success) load();
            }).catch(function (e) { btn.disabled = false; toast('นำเข้าไม่สำเร็จ: ' + e, 'err'); });
    };

    window.rsAddManual = function (btn) {
        var d = {
            code: $('mSource').value, reviewerName: $('mName').value, rating: $('mRating').value, scale: $('mScale').value,
            reviewDate: $('mDate').value, stayDate: $('mStay').value, roomType: $('mRoom').value, travellerType: $('mTrav').value,
            url: $('mUrl').value, externalId: $('mExt').value, origin: $('mOrigin').value, language: $('mLang').value,
            title: $('mTitle').value, text: $('mText').value, ownerResponse: $('mResp').value
        };
        btn.disabled = true;
        api('addManual', d).then(function (r) {
            btn.disabled = false;
            $('manualResult').innerHTML = '<div class="rs-alert ' + (r.success ? 'ok' : 'err') + '">' + esc(r.message) + '</div>';
            if (r.success) { ['mName', 'mRating', 'mTitle', 'mText', 'mResp', 'mUrl', 'mExt', 'mStay', 'mRoom', 'mTrav'].forEach(function (id) { $(id).value = ''; }); load(); }
        }).catch(function (e) { btn.disabled = false; toast('บันทึกไม่สำเร็จ: ' + e, 'err'); });
    };

    document.addEventListener('DOMContentLoaded', function () { load(); rsLoadLog(''); });
})();
</script>
</asp:Content>
