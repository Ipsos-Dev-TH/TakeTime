<%@ Page Title="ศูนย์บัญชี NextAcc" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="NextAccHub.aspx.cs" Inherits="Take_Time_BangPhra.Account.NextAccHub" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <%-- ศูนย์บัญชี NextAcc (CLAUDE.md ข้อ 24): สถานะการเชื่อมต่อ · คิวตามชนิดปัญหา · งานที่ต้องมีคนทำ · ก่อนยื่น ภ.พ.30 ·
         ตรวจเทียบ · งานจากส่วนอื่น · ปุ่มลัด (เรียก action เดิมของหน้า Accounting Integration) — อ่าน DB อย่างเดียว ไม่เรียก API ตอนเปิดหน้า --%>
    <style>
        .nh { max-width: 1180px; margin: 12px auto 48px; padding: 0 16px; color: #1f2933; }
        .nh-head { display: flex; flex-wrap: wrap; align-items: flex-end; justify-content: space-between; gap: 10px; margin-bottom: 14px; }
        .nh-head h2 { margin: 0; font-size: 22px; font-weight: 700; }
        .nh-head p { margin: 4px 0 0; font-size: 13px; color: #5f6b76; }
        .nh-tools { display: flex; align-items: center; gap: 10px; font-size: 13px; color: #5f6b76; flex-wrap: wrap; }
        .nh-tools label { margin: 0; font-weight: 400; display: inline-flex; align-items: center; gap: 6px; cursor: pointer; }
        .nh-grid { display: grid; grid-template-columns: 1fr; gap: 14px; }
        @media (min-width: 900px) { .nh-grid { grid-template-columns: 1fr 1fr; } .nh-span { grid-column: 1 / -1; } }
        .nh-card { background: #fff; border: 1px solid #e4e7eb; border-radius: 12px; padding: 14px 16px; min-width: 0; }
        .nh-card h3 { margin: 0 0 10px; font-size: 16px; font-weight: 700; display: flex; align-items: center; gap: 8px; flex-wrap: wrap; }
        .nh-card h3 small { font-size: 12px; font-weight: 400; color: #6b7785; }
        .nh-sub { font-size: 12px; color: #6b7785; line-height: 1.6; }
        .nh-conn { display: flex; gap: 12px; align-items: flex-start; }
        .nh-dot { flex: 0 0 auto; width: 14px; height: 14px; border-radius: 50%; margin-top: 4px; }
        .nh-dot.ok { background: #2e9d4f; box-shadow: 0 0 0 4px #e6f4ea; }
        .nh-dot.warn { background: #e8a33d; box-shadow: 0 0 0 4px #fff3dd; }
        .nh-dot.err { background: #d93025; box-shadow: 0 0 0 4px #fde7e5; }
        .nh-dot.off { background: #9aa5b1; box-shadow: 0 0 0 4px #eef0f2; }
        .nh-conn-title { font-size: 17px; font-weight: 700; }
        .nh-facts { display: grid; grid-template-columns: repeat(auto-fit, minmax(140px, 1fr)); gap: 8px 16px; margin-top: 10px; }
        .nh-fact { font-size: 13px; line-height: 1.5; }
        .nh-fact b { display: block; font-size: 11px; font-weight: 600; color: #6b7785; text-transform: none; }
        .nh-tiles { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 8px; }
        @media (min-width: 600px) { .nh-tiles { grid-template-columns: repeat(4, minmax(0, 1fr)); } }
        .nh-tile { display: block; border-radius: 10px; padding: 10px 12px; text-decoration: none !important; color: inherit; border: 1px solid #e4e7eb; background: #f8f9fa; min-height: 72px; }
        .nh-tile:hover, .nh-tile:focus { border-color: #9aa5b1; }
        .nh-tile .n { font-size: 24px; font-weight: 700; line-height: 1.1; }
        .nh-tile .t { font-size: 12px; font-weight: 600; margin-top: 2px; }
        .nh-tile .h { font-size: 11px; color: #6b7785; margin-top: 2px; line-height: 1.4; }
        .nh-tile.zero { opacity: .55; }
        .nh-tile.data { background: var(--na-data-bg, #fff0e0); border-color: #f5c08a; color: var(--na-data-fg, #a04a00); }
        .nh-tile.config { background: var(--na-config-bg, #f1e9fb); border-color: #d3bdf0; color: var(--na-config-fg, #6a3d9a); }
        .nh-tile.cn { background: var(--na-cn-bg, #fde7e5); border-color: #f3b0aa; color: var(--na-cn-fg, #b3261e); }
        .nh-tile.dup { background: #eceff1; border-color: #9aa5b1; color: #263238; }
        .nh-tile.failed { background: #fff4f3; border-color: #f3b0aa; color: #b3261e; }
        .nh-tile.wait { background: var(--na-wait-bg, #e3f0fa); border-color: #b6d4ee; color: var(--na-wait-fg, #1d5f8a); }
        .nh-tile.ok { background: var(--na-sent-bg, #e6f4ea); border-color: #b7dfc2; color: var(--na-sent-fg, #1e7e34); }
        .nh-tile .h, .nh-tile.zero .t { color: inherit; opacity: .8; }
        .nh-list { list-style: none; margin: 0; padding: 0; }
        .nh-list li { padding: 10px 0; border-top: 1px solid #eef0f2; display: flex; flex-direction: column; gap: 4px; min-width: 0; }
        .nh-list li:first-child { border-top: 0; padding-top: 2px; }
        .nh-row1 { display: flex; flex-wrap: wrap; align-items: center; gap: 6px 10px; font-size: 13px; }
        .nh-key { font-weight: 700; word-break: break-all; }
        .nh-list .na-lks { margin-left: 0; }
        .nh-err { font-size: 12px; color: #52606d; line-height: 1.5; overflow-wrap: anywhere; }
        .nh-empty { font-size: 13px; color: #2e7d32; padding: 6px 0; }
        .nh-count { font-size: 30px; font-weight: 700; line-height: 1; }
        .nh-chips { display: flex; flex-wrap: wrap; gap: 6px; margin: 6px 0 8px; }
        .nh-chip { font-size: 12px; padding: 3px 9px; border-radius: 999px; background: #eef0f2; color: #3e4c59; }
        .nh-chip.hot { background: #fde7e5; color: #b3261e; }
        .nh-actions { display: flex; flex-wrap: wrap; gap: 8px; }
        .nh-btn { border: 1px solid #c5ccd3; background: #fff; color: #1f2933; border-radius: 8px; padding: 9px 14px; font-size: 13px; font-weight: 600; cursor: pointer; min-height: 40px; }
        .nh-btn:hover { background: #f5f7fa; }
        .nh-btn.primary { background: #1a5fb4; border-color: #1a5fb4; color: #fff; }
        .nh-btn.primary:hover { background: #164f96; }
        .nh-btn[disabled] { opacity: .55; cursor: wait; }
        .nh-result { margin-top: 10px; font-size: 13px; line-height: 1.6; white-space: pre-wrap; overflow-wrap: anywhere; }
        .nh-result:empty { display: none; }
        .nh-result .box { border-radius: 8px; padding: 10px 12px; background: #f5f7fa; border: 1px solid #e4e7eb; }
        .nh-result .box.err { background: #fff4f3; border-color: #f3b0aa; color: #8a1c14; }
        .nh-hc { list-style: none; padding: 0; margin: 6px 0 0; white-space: normal; }
        .nh-hc li { padding: 6px 0; border-top: 1px solid #e4e7eb; }
        .nh-links { display: flex; flex-wrap: wrap; gap: 6px 16px; font-size: 13px; }
        .nh-links a { color: #1a5fb4; }
        .nh-warn { background: #fff8e1; border: 1px solid #ffe082; color: #6d4c00; border-radius: 8px; padding: 8px 12px; font-size: 13px; margin-bottom: 12px; }
        .nh-big { font-size: 15px; font-weight: 700; }
        .nh-big.hot { color: #b3261e; }
        @media (max-width: 600px) {
            .nh { padding: 0 12px; }
            .nh-head h2 { font-size: 19px; }
            .nh-card { padding: 12px; border-radius: 10px; }
            .nh-btn { flex: 1 1 calc(50% - 8px); }
        }
    </style>

    <div class="nh" id="nhRoot" data-integration="<%= H(IntegrationPath) %>">
        <div class="nh-head">
            <div>
                <h2>📒 ศูนย์บัญชี NextAcc</h2>
                <p>งานบัญชีที่ต้องดูแลวันนี้ในหน้าเดียว — ตัวเลขทุกช่องกดเข้าไปดูรายการได้</p>
            </div>
            <div class="nh-tools">
                <span>อัปเดต <asp:Literal ID="litUpdated" runat="server" /></span>
                <label><input type="checkbox" id="nhAuto" /> รีเฟรชเองทุก 60 วินาที</label>
                <button type="button" class="nh-btn" onclick="location.reload()">🔄 รีเฟรช</button>
            </div>
        </div>

        <asp:Literal ID="litWarn" runat="server" />

        <div class="nh-grid">
            <section class="nh-card nh-span" aria-labelledby="nhConnT"><asp:Literal ID="litConn" runat="server" /></section>
            <section class="nh-card nh-span" aria-labelledby="nhQueueT"><asp:Literal ID="litQueue" runat="server" /></section>
            <section class="nh-card nh-span" aria-labelledby="nhWorkT"><asp:Literal ID="litWork" runat="server" /></section>
            <section class="nh-card" aria-labelledby="nhVatT"><asp:Literal ID="litFiling" runat="server" /></section>
            <section class="nh-card" aria-labelledby="nhRecT"><asp:Literal ID="litFindings" runat="server" /></section>
            <section class="nh-card nh-span" aria-labelledby="nhCrossT"><asp:Literal ID="litCross" runat="server" /></section>

            <asp:Panel ID="pnlActions" runat="server" CssClass="nh-card nh-span">
                <h3 id="nhActT">⚡ ปุ่มลัด <small>ใช้ตัวเดียวกับหน้าตั้งค่า NextAcc — ไม่มีการส่งซ้ำแบบเสี่ยง</small></h3>
                <div class="nh-actions">
                    <button type="button" class="nh-btn primary" data-act="processQueue">▶ ประมวลผลคิวตอนนี้</button>
                    <button type="button" class="nh-btn" data-act="healthCheck">🩺 ตรวจสุขภาพการเชื่อมต่อ</button>
                    <button type="button" class="nh-btn" data-act="preflightScan" data-q="days=30">🔎 ตรวจใบเสร็จย้อนหลัง 30 วัน</button>
                    <button type="button" class="nh-btn" data-act="integrityScan">🧮 ตรวจความครบถ้วนตอนนี้</button>
                </div>
                <div class="nh-sub" style="margin-top:6px;">ประมวลผลคิว = ส่งรายการที่รออยู่เดี๋ยวนี้ (ปกติระบบส่งเองทุกไม่กี่วินาที) ·
                    ตรวจใบเสร็จย้อนหลัง = อ่านข้อมูลในระบบอย่างเดียว ไม่ส่งอะไรไป NextAcc</div>
                <div id="nhResult" class="nh-result" aria-live="polite"></div>
            </asp:Panel>

            <section class="nh-card nh-span">
                <h3>🔗 ไปต่อ</h3>
                <asp:Literal ID="litLinks" runat="server" />
            </section>
        </div>
    </div>

    <script>
        (function () {
            var root = document.getElementById('nhRoot');
            if (!root) return;
            var integ = root.getAttribute('data-integration') || '';
            var busy = false;

            function esc(s) {
                return (window.NaBadge ? NaBadge.esc(s) : String(s == null ? '' : s).replace(/[&<>"']/g, function (c) {
                    return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
                }));
            }
            function safe(u) {
                if (typeof u !== 'string' || !u) return '';
                if (u.charAt(0) === '?') u = integ + u;            // "?focus=…" = ส่วนในหน้าตั้งค่า NextAcc
                return window.NaBadge ? NaBadge.safeUrl(u, false) : (/^\/[A-Za-z]/.test(u) && !/[<>"'\s]/.test(u) ? u : '');
            }
            function show(html) { document.getElementById('nhResult').innerHTML = html; }

            function renderHealth(d) {
                var items = d.issues || [];
                if (!items.length) return '<div class="box">✅ ไม่พบปัญหา (' + esc(d.checkedAt || '') + ')</div>';
                var icon = { error: '🔴', warn: '🟠', info: '🔵', ok: '🟢' };
                var h = '<div class="box"><b>ผลตรวจสุขภาพ ' + esc(d.checkedAt || '') + '</b><ul class="nh-hc">';
                items.forEach(function (it) {
                    var fix = safe(it.fixUrl);
                    h += '<li>' + (icon[it.level] || '•') + ' <b>' + esc(it.title) + '</b>'
                        + (it.detail ? '<div class="nh-err">' + esc(it.detail) + '</div>' : '')
                        + (fix ? '<a href="' + esc(fix) + '" target="_blank">🛠 ' + esc(it.fixLabel || 'แก้ที่นี่') + '</a>' : '') + '</li>';
                });
                return h + '</ul></div>';
            }

            function run(btn) {
                if (busy) return;
                var act = btn.getAttribute('data-act');
                var extra = btn.getAttribute('data-q');
                busy = true;
                var label = btn.textContent;
                Array.prototype.forEach.call(root.querySelectorAll('[data-act]'), function (b) { b.disabled = true; });
                btn.textContent = '⏳ กำลังทำ…';
                show('');
                var ctl = window.AbortController ? new AbortController() : null;
                var timer = ctl ? setTimeout(function () { ctl.abort(); }, 180000) : null;
                fetch(integ + '?action=' + encodeURIComponent(act) + (extra ? '&' + extra : '') + '&_=' + Date.now(),
                      { credentials: 'same-origin', signal: ctl ? ctl.signal : undefined })
                    .then(function (r) { return r.text(); })
                    .then(function (t) {
                        var d;
                        try { d = JSON.parse(t); } catch (e) { d = null; }
                        if (!d) { show('<div class="box err">ทำไม่สำเร็จ — ไม่มีสิทธิ์ใช้ปุ่มนี้ หรือหมดเวลาเข้าสู่ระบบ (เปิดหน้าตั้งค่า NextAcc เพื่อทำแทน)</div>'); return; }
                        if (act === 'healthCheck' && d.success) { show(renderHealth(d)); return; }
                        show('<div class="box' + (d.success ? '' : ' err') + '">' + esc(d.message || (d.success ? 'เสร็จแล้ว' : 'ทำไม่สำเร็จ')) + '</div>');
                    })
                    .catch(function (e) { show('<div class="box err">' + esc(e && e.name === 'AbortError' ? 'ใช้เวลานานเกินไป — ลองใหม่ภายหลัง' : 'เชื่อมต่อไม่ได้: ' + (e && e.message)) + '</div>'); })
                    .then(function () {
                        if (timer) clearTimeout(timer);
                        busy = false;
                        btn.textContent = label;
                        Array.prototype.forEach.call(root.querySelectorAll('[data-act]'), function (b) { b.disabled = false; });
                    });
            }
            Array.prototype.forEach.call(root.querySelectorAll('[data-act]'), function (b) {
                b.addEventListener('click', function () { run(b); });
            });

            // รีเฟรชเอง (ค่าจำต่อเครื่อง) — ข้ามเมื่อกำลังทำงาน/แท็บซ่อน
            var auto = document.getElementById('nhAuto');
            var on = false;
            try { on = localStorage.getItem('naHubAuto') === '1'; } catch (e) { }
            if (auto) {
                auto.checked = on;
                auto.addEventListener('change', function () { try { localStorage.setItem('naHubAuto', auto.checked ? '1' : '0'); } catch (e) { } });
            }
            setInterval(function () {
                if (auto && auto.checked && !busy && !document.hidden && !document.getElementById('nhResult').innerHTML) location.reload();
            }, 60000);
        })();
    </script>
</asp:Content>
