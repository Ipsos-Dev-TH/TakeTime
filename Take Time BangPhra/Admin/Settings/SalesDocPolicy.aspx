<%@ Page Title="นโยบายเอกสารขาย" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="SalesDocPolicy.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Settings.SalesDocPolicy" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <link rel="stylesheet" href="<%= ResolveUrl("~/Content/admin-settings.css") %>?v=<%= Take_Time_BangPhra.Admin.Settings.SettingsUi.AssetVersion %>" />
    <script src="<%= ResolveUrl("~/Scripts/admin-settings.js") %>?v=<%= Take_Time_BangPhra.Admin.Settings.SettingsUi.AssetVersion %>"></script>
    <style>
        .sd-wrap { max-width: 1240px; margin: 0 auto; padding: 12px 12px 30px; }
        .sd-head { background: linear-gradient(135deg,#00695c,#004d40); color:#fff; border-radius:14px; padding:20px 22px; margin-bottom:16px; }
        .sd-head h2 { margin:0 0 6px; font-size:21px; }
        .sd-head p { margin:0; opacity:.93; font-size:14px; line-height:1.7; }
        .sd-card { background:#fff; border-radius:14px; padding:18px 20px; margin-bottom:16px; box-shadow:0 2px 10px rgba(0,0,0,.05); }
        .sd-card h3 { margin:0 0 4px; font-size:16.5px; color:#004d40; }
        .sd-card .sub { color:#6b7c85; font-size:13px; margin-bottom:12px; line-height:1.65; }
        .sd-alert { padding:12px 15px; border-radius:10px; margin-bottom:14px; font-size:14px; line-height:1.7; }
        .sd-alert.ok { background:#e8f6ee; color:#16653e; } .sd-alert.err { background:#fdecec; color:#a12626; }
        .sd-legal { background:#fff6e5; border-left:5px solid #f0a020; color:#5f3d00; border-radius:10px; padding:14px 16px;
                    font-size:13.5px; line-height:1.8; margin-bottom:16px; }
        .sd-legal b { color:#7a4a00; }
        .sd-legal ul { margin:6px 0 0; padding-left:20px; }
        .sd-status { display:flex; flex-wrap:wrap; gap:6px; }
        .sd-switch { display:flex; gap:10px; align-items:flex-start; padding:10px 0; border-bottom:1px solid #f0f3f5; font-size:14px; }
        .sd-switch:last-child { border-bottom:0; }
        .sd-switch .sd-sub { margin-top:2px; }
        .sd-scroll { overflow-x:auto; border:1px solid #eef1f3; border-radius:10px; }
        table.sd-t { width:100%; border-collapse:collapse; font-size:12.5px; min-width:980px; }
        table.sd-t.sd-ov { min-width:640px; font-size:13.5px; }
        table.sd-t th { background:#f5f7f9; padding:8px; text-align:left; font-weight:650; color:#37474f; border-bottom:2px solid #e3e9ed; vertical-align:top; }
        table.sd-t td { padding:6px 8px; border-bottom:1px solid #f0f3f5; vertical-align:top; }
        table.sd-t th.sd-rowh { background:#fafbfc; min-width:150px; }
        table.sd-t .sd-corner { font-size:11.5px; color:#78909c; }
        .sd-cell.sd-set { background:#f1f8f6; }
        .sd-cell.sd-dis { background:#fbf3f3; }
        .sd-sel { width:100%; min-width:104px; padding:4px 6px; border:1.5px solid #dbe2e7; border-radius:7px; font-size:12.5px; background:#fff; }
        .sd-en { display:block; margin-top:4px; font-size:11.5px; color:#607d8b; font-weight:400; white-space:nowrap; }
        .sd-sub { font-size:11.5px; color:#90a4ae; font-weight:400; line-height:1.5; }
        .sd-inactive td { opacity:.6; }
        .sd-empty { color:#a12626; padding:12px; }
        ul.sd-prev { margin:0; padding-left:20px; line-height:1.75; font-size:13.5px; }
        ul.sd-prev li { margin-bottom:6px; }
        .sd-btn { padding:11px 22px; border:0; border-radius:10px; background:#00796b; color:#fff; font-size:14.5px; font-weight:600; cursor:pointer; }
        .sd-btn:hover { background:#00695c; }
        .sd-btn[disabled] { background:#b0bec5; cursor:not-allowed; }
        @media (max-width: 640px) {
            .sd-wrap { padding: 8px 4px 24px; }
            .sd-head { padding: 16px; }
            .sd-card { padding: 14px; }
        }
    </style>

    <div class="sd-wrap" data-as-dirty="1">
        <nav class="as-crumb" aria-label="เส้นทาง">
            <a href="<%= ResolveUrl("~/Admin/Settings/Index") %>"><i class="fas fa-sliders"></i> ศูนย์ตั้งค่า</a>
            <span class="sep">›</span><span>บัญชี &amp; ภาษี</span>
            <span class="sep">›</span><span class="here">นโยบายเอกสารขาย</span>
            <a class="as-back" href="<%= ResolveUrl("~/Admin/Settings/Index") %>">← กลับศูนย์ตั้งค่า</a>
        </nav>

        <div class="sd-head">
            <h2><i class="fas fa-receipt"></i> นโยบายเอกสารขาย</h2>
            <p>
                เลือกว่าการขายแต่ละช่องทาง × วิธีรับเงิน จะ <b>ออกเอกสารบน NextAcc ทันทีรายใบ</b> /
                <b>รวมเป็นเอกสารสรุปยอดขายรายวัน</b> (ต่อวัน × แหล่งเงิน) / <b>ไม่ส่งเข้าบัญชี</b> ·
                ค่าเริ่มต้น: เงินสด = สรุปรายวัน · โอน / QR / บัตร = ทันที · ใบเสร็จการจอง = ทันทีเสมอ
            </p>
        </div>

        <asp:Literal ID="litMsg" runat="server" />

        <div class="sd-legal">
            <b>⚠ ข้อกฎหมายที่ต้องรู้ก่อนเปิด "สรุปรายวัน" (ประมวลรัษฎากร ม.86, ม.86/6)</b>
            <ul>
                <li>ผู้ประกอบการจด VAT ต้องออก <b>ใบกำกับภาษีให้ลูกค้าทุกครั้งที่ขาย</b> ไม่ว่าจ่ายเงินสด โอน หรือ QR
                    — <b>เอกสารสรุปรายวันบน NextAcc ไม่ใช่ใบกำกับภาษีของลูกค้าแต่ละราย</b> ใช้เป็นเอกสารประกอบการลงบัญชี
                    และลงรายงานภาษีขายเป็นยอดรวมรายวันเท่านั้น</li>
                <li>ดังนั้นการขายที่ "สรุปรายวัน" หน้าร้านต้องยังให้ <b>ใบกำกับภาษีอย่างย่อรายใบ (มีเลขเรียง)</b> กับลูกค้า —
                    เปิดสวิตช์ "พิมพ์ใบกำกับภาษีอย่างย่อในเครื่อง" ด้านล่าง ระบบ POS จะพิมพ์ให้ทุกบิล และเอกสารสรุปรายวันจะอ้างช่วงเลขที่แรก–สุดท้าย</li>
                <li>ใบกำกับภาษีอย่างย่อที่พิมพ์จาก <b>เครื่อง/โปรแกรมบันทึกการเก็บเงิน</b> อาจต้องได้รับอนุมัติ <b>ภ.พ.06</b> ก่อน —
                    ยังเป็นจุดที่ควร <b>สอบถามสรรพากรพื้นที่ (ชลบุรี)</b> ทางเลือกที่ปลอดภัย: ขอ ภ.พ.06 / ใช้ "ทันที" (เอกสารออกจาก NextAcc รายใบ) / ออกใบเต็มรูป</li>
                <li>หัวเอกสาร "ใบเสร็จรับเงิน/ใบกำกับภาษีอย่างย่อ" บน NextAcc ต้องตั้งบริษัทเป็น <b>ผู้ค้าปลีก (ได้รับสิทธิ์ออกใบอย่างย่อ)</b> ในข้อมูลบริษัทของ NextAcc
                    ไม่งั้น NextAcc พิมพ์ "ใบเสร็จรับเงิน" (ภาษีขายยังเข้า ภ.พ.30 ครบ) · ใบอย่างย่อออก e-Tax ไม่ได้ · ลูกค้านิติบุคคลที่ขอใบกำกับต้องได้ใบเต็มรูป (ติ๊ก "ขอใบกำกับเต็มรูป" ที่ POS)</li>
            </ul>
        </div>

        <div class="sd-card">
            <h3>สถานะ</h3>
            <asp:Literal ID="litStatus" runat="server" />
            <details class="as-help" style="margin-top:12px;">
                <summary>ลำดับการตัดสิน (อ่านครั้งแรก)</summary>
                <ul>
                    <li><b>1.</b> ตั้งเฉพาะแหล่งเงิน (ตารางล่าง) — ถ้าตั้งไว้ ใช้ค่านั้นกับ <b>ทุกช่องทางขาย</b> รวมใบเสร็จการจอง</li>
                    <li><b>2.</b> ช่อง (ช่องทางขาย × ชนิดการชำระ) → <b>3.</b> (ช่องทางขาย × ทุกชนิด) → <b>4.</b> (ทุกช่องทาง × ชนิด) → <b>5.</b> (ทุกช่องทาง × ทุกชนิด)</li>
                    <li>ไม่มีกฎที่ตรงเลย = ออกเอกสารทันที · เอาติ๊ก "ใช้กฎนี้" ออก = ข้ามกฎนั้นไปกฎถัดไป (เก็บค่าไว้)</li>
                    <li>ชนิดการชำระของแหล่งเงินมาจากหน้า <a href="<%= ResolveUrl("~/Admin/Settings/PaymentChannels") %>">ช่องทางชำระเงิน</a> (ยังไม่จัด = เดาจากชื่อ)</li>
                    <li>POS: ติ๊ก "ขอใบกำกับเต็มรูป/ระบุชื่อ" = ออกเอกสารรายใบเสมอไม่ว่านโยบายจะเป็นอะไร · ชาร์จเข้าห้อง = ไปกับใบเสร็จเช็คเอาท์ (ไม่ขึ้นกับหน้านี้)</li>
                    <li>สรุปรายวันรวบหลังวันนั้นจบ (รอบอัตโนมัติทุก 30 วินาที) · ขายย้อนวันหลังวันนั้นสรุปแล้ว → เอกสารเพิ่มเติม <code>PS{ปปดดวว}-{แหล่งเงิน}-S1</code> (เช่น PS261022-2-S1)</li>
                </ul>
            </details>
        </div>

        <div class="sd-card">
            <h3>สวิตช์หลัก</h3>
            <div class="sd-switch">
                <asp:CheckBox ID="chkEnabled" runat="server" data-sd-mode="1" />
                <div><b>เปิดใช้นโยบายเอกสารขาย</b>
                    <div class="sd-sub">ปิด = พฤติกรรมเดิม (POS ติ๊ก "ออกใบกำกับภาษีในระบบ" = รายใบ · ไม่ติ๊ก = รวบรายวันเมื่อเปิด Nexaacc_PosDailyRollup ในหน้า NextAcc)</div>
                </div>
            </div>
            <div class="sd-switch">
                <asp:CheckBox ID="chkLocalSlip" runat="server" data-sd-mode="1" />
                <div><b>พิมพ์ใบกำกับภาษีอย่างย่อในเครื่อง (เลขเรียง) เมื่อไม่ได้ออกเอกสารรายใบบน NextAcc</b>
                    <div class="sd-sub">การขายที่นโยบายเป็น "สรุปรายวัน" หรือ "ไม่ส่งเข้าบัญชี" — POS แสดงใบให้พิมพ์มอบลูกค้าหลังกดบันทึก (เลขที่ ABB{ปีเดือน}-00001 เรียงต่อเดือน)</div>
                </div>
            </div>
            <div class="sd-switch">
                <asp:CheckBox ID="chkLocalEmail" runat="server" />
                <div><b>ยังส่งอีเมล PDF "[INV]" ของ TakeTime แม้ออกเอกสารบน NextAcc แล้ว</b>
                    <div class="sd-sub">ค่าแนะนำ = ปิด — ลูกค้าจะได้เอกสารภาษีสองเลข (ของ TakeTime และของ NextAcc) ถ้าเปิด</div>
                </div>
            </div>
        </div>

        <div class="sd-card">
            <h3>ตารางนโยบาย: ช่องทางขาย × ชนิดการชำระเงิน</h3>
            <div class="sub">เว้น "— ตามกฎกลาง —" = ใช้กฎที่กว้างกว่าตามลำดับ · ช่องที่ตั้งค่าไว้แล้วเป็นสีเขียว · ช่องที่ปิดไว้เป็นสีแดงอ่อน
                (ช่องทางอื่นนอกจาก POS ใช้นโยบายนี้เมื่อโมดูลนั้นรองรับ)</div>
            <asp:Literal ID="litMatrix" runat="server" />
        </div>

        <div class="sd-card">
            <h3>ตั้งเฉพาะรายแหล่งเงิน</h3>
            <div class="sub">เช่น บัญชีโอนบางบัญชีที่อยากให้รวมรายวันเหมือนเงินสด หรือแหล่งเงินที่ไม่ต้องการส่งเข้าบัญชี — ค่านี้ชนะตารางด้านบนทุกช่องทาง</div>
            <asp:Literal ID="litOverrides" runat="server" />
        </div>

        <div class="sd-card">
            <h3>ตัวอย่าง: ขายหน้าร้าน (POS) ไม่ได้ติ๊กขอใบกำกับเต็มรูป</h3>
            <div class="sub">คำนวณใหม่ทันทีเมื่อแก้ค่าในหน้านี้ (ยังไม่บันทึก) — แหล่งเงินที่ปิดใช้งานไม่แสดง</div>
            <asp:Literal ID="litPreview" runat="server" />
        </div>

        <div class="sd-card as-savebar">
            <div class="as-savebar-row">
                <asp:Button ID="btnSave" runat="server" Text="💾 บันทึกนโยบาย" CssClass="sd-btn" OnClick="btnSave_Click" />
                <span class="as-dirty-flag">มีค่าที่ยังไม่ได้บันทึก</span>
            </div>
        </div>
    </div>

    <asp:Literal ID="litJsData" runat="server" />
    <script>
    (function () {
        'use strict';
        var MODES = { IMMEDIATE: 1, DAILY_ROLLUP: 1, OFF: 1 };

        function val(name) {
            var el = document.querySelector("[name='" + name + "']");
            return el ? el.value : '';
        }
        function checked(name) {
            var el = document.querySelector("[name='" + name + "']");
            return !!(el && el.checked);
        }
        function ctlChecked(idPart) {
            var el = document.querySelector("input[type=checkbox][id$='" + idPart + "']");
            return !!(el && el.checked);
        }
        function esc(s) {
            return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) {
                return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
            });
        }

        // ลำดับเดียวกับ SalesDocumentPolicy.Resolve (C#)
        function resolve(channel, paidHow) {
            var ov = val('sdo__' + paidHow.id);
            if (MODES[ov]) return { mode: ov, reason: 'ตั้งเฉพาะแหล่งเงินนี้' };
            var order = [[channel, paidHow.type], [channel, 'ANY'], ['ANY', paidHow.type], ['ANY', 'ANY']];
            for (var i = 0; i < order.length; i++) {
                var key = order[i][0] + '__' + order[i][1];
                var m = val('sdm__' + key);
                if (MODES[m] && checked('sde__' + key)) return { mode: m, reason: 'กฎ ' + key.replace('__', ' × ').replace(/ANY/g, 'ทั้งหมด') };
            }
            return { mode: 'IMMEDIATE', reason: 'ไม่มีกฎที่ตรง — ค่าเริ่มต้น' };
        }

        function describe(mode, ph, slip) {
            if (mode === 'DAILY_ROLLUP')
                return 'ไม่ออกเอกสารรายใบ — รวมเข้าเอกสารสรุปยอดขายรายวัน POSDAY-{วัน}-' + ph.id + ' (ใบกำกับภาษีขายสดยอดรวม เข้าบัญชีของ "' + ph.name + '")'
                    + (slip ? ' · POS พิมพ์ใบกำกับภาษีอย่างย่อเลขเรียงให้ลูกค้าทุกบิล' : ' · ⚠ ไม่มีใบให้ลูกค้า (ปิดการพิมพ์ใบในเครื่อง)');
            if (mode === 'OFF')
                return 'ไม่ส่งเข้าระบบบัญชีเลย' + (slip ? ' · POS พิมพ์ใบกำกับภาษีอย่างย่อให้ลูกค้า' : ' · ⚠ ไม่มีใบให้ลูกค้า และไม่มีรายได้ใน NextAcc');
            return 'ออกใบเสร็จรับเงิน/ใบกำกับภาษีอย่างย่อ (ขายสด ผู้ซื้อไม่ประสงค์รับใบกำกับ) บน NextAcc ทันทีรายใบ · ลูกค้าขอใบเต็มรูป → พนักงานติ๊ก "ขอใบกำกับเต็มรูป/ระบุชื่อ"';
        }

        function render() {
            var list = document.getElementById('sdPreviewList');
            if (!list || !window.SD_PAIDHOWS) return;
            var on = ctlChecked('chkEnabled');
            var slip = ctlChecked('chkLocalSlip');
            var html = '';
            for (var i = 0; i < window.SD_PAIDHOWS.length; i++) {
                var ph = window.SD_PAIDHOWS[i];
                var r = resolve('POS', ph);
                html += '<li>ถ้าขายหน้าร้านด้วย <b>' + esc(ph.name) + '</b> (' + esc(ph.typeLabel) + ') → '
                    + esc(on ? describe(r.mode, ph, slip)
                             : 'นโยบายปิด: พฤติกรรมเดิม (ติ๊กออกใบกำกับ = รายใบ · ไม่ติ๊ก = รวบรายวันเมื่อเปิด Nexaacc_PosDailyRollup)')
                    + '<div class="sd-sub">' + esc(r.reason) + '</div></li>';
            }
            list.innerHTML = html;
        }

        document.addEventListener('change', function (e) {
            var t = e.target;
            if (t && t.getAttribute && (t.getAttribute('data-sd-mode') === '1' || (t.parentNode && t.parentNode.getAttribute && t.parentNode.getAttribute('data-sd-mode') === '1')))
                render();
        });
        if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', render); else render();
    })();
    </script>
</asp:Content>
