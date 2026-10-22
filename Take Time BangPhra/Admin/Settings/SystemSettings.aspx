<%@ Page Title="ตั้งค่าระบบ" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="SystemSettings.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Settings.SystemSettings" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .ss-wrap { max-width: 1100px; margin: 0 auto; padding: 18px 12px 50px; }
        .ss-head { background: linear-gradient(135deg, #37474f, #546e7a); color: #fff;
                   border-radius: 12px; padding: 22px 26px; margin-bottom: 20px; }
        .ss-head h2 { margin: 0 0 6px; font-weight: 700; }
        .ss-head p { margin: 0; opacity: .92; font-size: 14px; }
        .ss-card { background: #fff; border-radius: 10px; box-shadow: 0 2px 10px rgba(0,0,0,.08);
                   padding: 20px; margin-bottom: 18px; }
        .ss-card h3 { margin: 0 0 4px; font-size: 1.1em; color: #37474f; font-weight: 700; }
        .ss-card .sub { font-size: 13px; color: #8a9aa3; margin-bottom: 16px; }
        .row { display: grid; grid-template-columns: 260px 1fr auto; gap: 12px;
               align-items: start; padding: 12px 0; border-bottom: 1px solid #f0f3f5; }
        .row:last-child { border-bottom: none; }
        .row .lbl { font-weight: 600; font-size: 14px; padding-top: 9px; }
        .row .lbl small { display: block; font-weight: 400; color: #8a9aa3; font-size: 12px; margin-top: 2px; }
        .row input[type=text], .row input[type=password], .row input[type=number], .row select {
            width: 100%; padding: 9px 12px; border: 1.5px solid #dbe2e7; border-radius: 8px; font-size: 14px;
        }
        .row input:focus, .row select:focus { outline: none; border-color: #546e7a; }
        .src { font-size: 11.5px; padding: 5px 10px; border-radius: 12px; white-space: nowrap; margin-top: 9px; }
        .src-db { background: #e8f5e9; color: #1e7e42; }
        .src-web { background: #fff3e0; color: #97591b; }
        .src-none { background: #f1f3f4; color: #90a4ae; }
        .hint { background: #eef4f8; border-left: 4px solid #546e7a; padding: 12px 15px;
                border-radius: 8px; font-size: 13px; color: #37474f; margin-bottom: 18px; }
        .test-bar { margin-top: 14px; padding-top: 14px; border-top: 1px dashed #e0e6ea; }
        .res { margin-top: 10px; font-size: 13.5px; }
        .res.ok { color: #1e7e42; } .res.err { color: #c0392b; }

        .ss-search { position: relative; margin-bottom: 16px; }
        .ss-search input { width: 100%; padding: 12px 16px 12px 44px; font-size: 15px;
                           border: 1.5px solid #dbe2e7; border-radius: 10px; background: #fff; }
        .ss-search input:focus { outline: none; border-color: #546e7a; box-shadow: 0 0 0 3px rgba(84,110,122,.12); }
        .ss-search i { position: absolute; left: 16px; top: 50%; transform: translateY(-50%); color: #90a4ae; }
        .ss-card > h3 { cursor: pointer; }
        .ss-card.ss-folded > *:not(h3) { display: none; }
        .ss-card > h3 .fold-caret { float: right; font-size: 12px; color: #b0b6bd; transition: transform .15s; }
        .ss-card.ss-folded > h3 .fold-caret { transform: rotate(-90deg); }
    </style>

    <div class="ss-wrap">
        <div class="ss-head">
            <h2><i class="fas fa-sliders"></i> ศูนย์รวมการตั้งค่าระบบ</h2>
            <p>ตั้งค่า Token / API Key / อีเมล / ที่เก็บไฟล์ ได้จากที่เดียว — มีผลทันที ไม่ต้องแก้ Web.config</p>
        </div>

        <div class="hint">
            <b>ค่าที่เว้นว่าง = ใช้ค่าจาก Web.config เดิม</b> (ป้าย <span class="src src-web">Web.config</span>)
            · กรอกแล้วบันทึก = เก็บใน DB และใช้ค่านั้นแทน (ป้าย <span class="src src-db">ตั้งใน DB</span>)<br />
            ค่าลับ (Token / รหัสผ่าน / API Key) ถูก<b>เข้ารหัส</b>ก่อนเก็บ และไม่แสดงค่าเดิมออกมา —
            เว้นว่างไว้ถ้าไม่ต้องการเปลี่ยน, ใส่ <code>-</code> เพื่อล้างค่าใน DB (กลับไปใช้ Web.config)
        </div>

        <asp:Panel ID="pnlMsg" runat="server" Visible="false" CssClass="ss-card" style="padding:14px 18px;">
            <asp:Literal ID="litMsg" runat="server" />
        </asp:Panel>

        <div class="ss-search">
            <i class="fas fa-magnifying-glass"></i>
            <input type="text" id="ssSearch" placeholder="ค้นหาการตั้งค่า… เช่น Telegram, อีเมล, Token, โฟลเดอร์"
                autocomplete="off" onkeydown="if(event.key==='Enter'){event.preventDefault();return false;}" />
        </div>

        <asp:Literal ID="litGroups" runat="server" />

        <div class="ss-card">
            <h3><i class="fas fa-vial"></i> ทดสอบการเชื่อมต่อ</h3>
            <div class="sub">ทดสอบด้วยค่าที่ใช้งานจริงตอนนี้ (บันทึกก่อนทดสอบ)</div>
            <asp:Button ID="btnTestTelegram" runat="server" Text="📨 ส่ง Telegram ทดสอบ"
                CssClass="btn btn-default" OnClick="btnTestTelegram_Click" UseSubmitBehavior="false"
                OnClientClick="this.disabled=true; this.value='⏳ กำลังส่ง...';" />
            <asp:Button ID="btnTestLine" runat="server" Text="💬 ตรวจ Token LINE OA"
                CssClass="btn btn-default" OnClick="btnTestLine_Click" UseSubmitBehavior="false"
                OnClientClick="this.disabled=true; this.value='⏳ กำลังตรวจ...';" />
            <asp:Button ID="btnTestEmail" runat="server" Text="✉️ ส่งอีเมลทดสอบ"
                CssClass="btn btn-default" OnClick="btnTestEmail_Click" UseSubmitBehavior="false"
                OnClientClick="this.disabled=true; this.value='⏳ กำลังส่ง...';" />
            <asp:TextBox ID="txtTestEmailTo" runat="server" placeholder="อีเมลปลายทางสำหรับทดสอบ"
                style="padding:8px 12px; border:1.5px solid #dbe2e7; border-radius:8px; min-width:240px; margin-left:6px;" />
            <div class="res" id="divRes" runat="server"><asp:Literal ID="litRes" runat="server" /></div>
        </div>

        <div style="text-align:right;">
            <asp:Button ID="btnSave" runat="server" Text="💾 บันทึกการตั้งค่าทั้งหมด"
                CssClass="btn btn-success btn-lg" OnClick="btnSave_Click" />
        </div>
    </div>

    <script>
        // ค้นหา + หัวข้อกดพับได้ — ส่วนเสริมล้วน ๆ ไม่แตะคอนโทรลฝั่งเซิร์ฟเวอร์
        (function () {
            var box = document.getElementById('ssSearch');
            var cards = document.querySelectorAll('.ss-card');

            for (var i = 0; i < cards.length; i++) {
                var h = cards[i].querySelector('h3');
                if (!h || !cards[i].querySelector('.row')) continue;   // การ์ดข้อความ/ปุ่ม ไม่ต้องพับ
                (function (card, h3) {
                    var caret = document.createElement('span');
                    caret.className = 'fold-caret';
                    caret.textContent = '▾';
                    h3.appendChild(caret);
                    h3.addEventListener('click', function () { card.classList.toggle('ss-folded'); });
                })(cards[i], h);
            }

            if (!box) return;
            box.addEventListener('input', function () {
                var q = this.value.trim().toLowerCase();
                for (var c = 0; c < cards.length; c++) {
                    var rows = cards[c].querySelectorAll('.row');
                    if (!rows.length) continue;
                    var hitCount = 0;
                    for (var r = 0; r < rows.length; r++) {
                        var text = (rows[r].textContent || '').toLowerCase();
                        var input = rows[r].querySelector('input, select');
                        if (input && input.name) text += ' ' + input.name.toLowerCase();
                        var hit = !q || text.indexOf(q) >= 0;
                        rows[r].style.display = hit ? '' : 'none';
                        if (hit) hitCount++;
                    }
                    cards[c].style.display = hitCount ? '' : 'none';
                    if (q && hitCount) cards[c].classList.remove('ss-folded');
                }
            });
        })();
    </script>
</asp:Content>
