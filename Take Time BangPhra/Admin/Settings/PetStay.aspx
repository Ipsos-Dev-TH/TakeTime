<%@ Page Title="สัตว์เลี้ยงเข้าพัก" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="PetStay.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Settings.PetStaySettings" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <link rel="stylesheet" href="<%= ResolveUrl("~/Content/admin-settings.css") %>?v=<%= Take_Time_BangPhra.Admin.Settings.SettingsUi.AssetVersion %>" />
    <script src="<%= ResolveUrl("~/Scripts/admin-settings.js") %>?v=<%= Take_Time_BangPhra.Admin.Settings.SettingsUi.AssetVersion %>"></script>
    <style>
        .ps-wrap { max-width: 1180px; margin: 0 auto; padding: 12px 12px 30px; }
        .ps-head { background: linear-gradient(135deg,#5d4037,#3e2723); color:#fff;
                   border-radius:14px; padding:20px 22px; margin-bottom:16px; }
        .ps-head h2 { margin:0 0 6px; font-size:21px; }
        .ps-head p { margin:0; opacity:.92; font-size:14px; line-height:1.7; }

        .ps-card { background:#fff; border-radius:14px; padding:18px 20px; margin-bottom:16px;
                   box-shadow:0 2px 10px rgba(0,0,0,.05); }
        .ps-card h3 { margin:0 0 4px; font-size:16.5px; color:#3e2723; }
        .ps-card .sub { color:#7b8a93; font-size:13px; margin-bottom:14px; line-height:1.65; }

        .ps-row { display:flex; gap:16px; padding:11px 0; border-bottom:1px solid #eff2f5; align-items:flex-start; }
        .ps-row:last-child { border-bottom:0; }
        .ps-lbl { flex:0 0 280px; }
        .ps-lbl b { display:block; font-size:14.5px; color:#2c3742; }
        .ps-lbl small { display:block; color:#8b959e; font-size:12.5px; line-height:1.6; margin-top:3px; }
        .ps-in { flex:1; min-width:0; }
        .ps-in select { width:100%; max-width:340px; padding:9px 11px; border:1px solid #dbe1e7; border-radius:9px; font-size:14px; }
        .ps-chk { display:flex; align-items:center; gap:9px; font-size:14px; }
        .ps-chk input { width:18px; height:18px; accent-color:#6d4c41; }

        .ps-btn { padding:11px 20px; border:0; border-radius:10px; background:#5d4037; color:#fff;
                  font-size:14.5px; font-weight:600; cursor:pointer; }
        .ps-btn:hover { background:#4e342e; }
        .ps-btn.ghost { background:#fff; color:#5d4037; border:1.5px solid #d7ccc8; text-decoration:none; display:inline-block; }

        .ps-alert { padding:12px 15px; border-radius:10px; margin-bottom:14px; font-size:14px; line-height:1.7; }
        .ps-alert.ok { background:#e8f6ee; color:#16653e; }
        .ps-alert.err { background:#fdecec; color:#a12626; }
        .ps-alert.warn { background:#fff6e5; color:#8a5a00; }
        .ps-alert.info { background:#eef4fb; color:#1d4e79; }

        .ps-rooms { width:100%; border-collapse:collapse; font-size:14px; }
        .ps-rooms th { background:#f6f2f0; text-align:left; padding:10px; color:#5d4037; font-weight:600; font-size:13px; }
        .ps-rooms td { padding:9px 10px; border-top:1px solid #eff2f5; vertical-align:top; }
        .ps-rooms tr.ps-on td { background:#fffdf8; }
        .ps-rooms input[type=text] { width:110px; padding:8px 10px; border:1px solid #dbe1e7;
                                     border-radius:8px; font-size:14px; text-align:right; }
        .ps-rooms input[type=checkbox] { width:18px; height:18px; accent-color:#6d4c41; }
        .ps-rooms .as-fielderr { max-width:220px; }
        .ps-count { font-size:13px; color:#6d4c41; margin:0 0 10px; font-weight:600; }

        .ps-prev { background:#faf8f7; border:1px dashed #d7ccc8; border-radius:10px; padding:12px 14px;
                   font-size:13.5px; line-height:1.75; color:#4e342e; margin-top:10px; }

        @media (max-width: 768px) {
            .ps-row { flex-direction:column; gap:7px; }
            .ps-lbl { flex:none; }
            .ps-rooms thead { display:none; }
            .ps-rooms tr { display:block; border-top:1px solid #eff2f5; padding:8px 0; }
            .ps-rooms td { display:flex; justify-content:space-between; align-items:center; flex-wrap:wrap;
                           border:0; padding:5px 2px; }
            .ps-rooms td:before { content:attr(data-th); color:#8b959e; font-size:12.5px; }
            .ps-rooms .as-fielderr { flex-basis:100%; max-width:none; text-align:right; }
        }
    </style>

    <div class="ps-wrap" data-as-dirty="1">
        <nav class="as-crumb" aria-label="เส้นทาง">
            <a href="<%= ResolveUrl("~/Admin/Settings/Index") %>"><i class="fas fa-sliders"></i> ศูนย์ตั้งค่า</a>
            <span class="sep">›</span><span>การจอง &amp; นโยบาย</span>
            <span class="sep">›</span><span class="here">สัตว์เลี้ยงเข้าพัก</span>
            <a class="as-back" href="<%= ResolveUrl("~/Admin/Settings/Index") %>">← กลับศูนย์ตั้งค่า</a>
        </nav>

        <div class="ps-head">
            <h2><i class="fas fa-paw"></i> สัตว์เลี้ยงเข้าพัก</h2>
            <p>
                เปิดแล้ว หน้าจองจะมีช่อง <b>🐾 มีสัตว์เลี้ยงเข้าพัก</b> เฉพาะเมื่อลูกค้าเลือกห้องที่รับสัตว์เลี้ยง ·
                ลูกค้าต้องติ๊กยอมรับนโยบายสัตว์เลี้ยงก่อนยืนยันการจอง
            </p>
        </div>

        <asp:Literal ID="litMsg" runat="server" />
        <asp:Literal ID="litInfo" runat="server" />

        <details class="as-help">
            <summary>ทำงานอย่างไร / ค่าบริการไปอยู่ตรงไหน</summary>
            <ul>
                <li>ตั้ง 3 อย่าง: <b>เปิดใช้</b> → <b>ติ๊กห้องที่รับ</b> พร้อมจำนวนสูงสุดและค่าบริการต่อตัว → <b>แก้นโยบาย</b> ให้ตรงกฎของที่พัก</li>
                <li>ค่าบริการถูกบวกเข้ายอดการจองเป็นรายการ <b>"ค่าบริการสัตว์เลี้ยง"</b> แยกจากค่าห้อง
                    (อยู่ในยอดรวม ยอดค้างชำระ ใบเสร็จ และส่งเข้าระบบบัญชีตามรายการค่าใช้จ่ายในห้อง)</li>
                <li>ลูกค้าจองเองต้องติ๊กยอมรับนโยบาย — ระบบบันทึกเวลาและเลขฉบับที่ยอมรับไว้บนใบจอง</li>
                <li>ปิดฟีเจอร์ = หน้าจองไม่แสดงอะไรเกี่ยวกับสัตว์เลี้ยงเลย (ใบจองเดิมที่มีสัตว์เลี้ยงยังแสดงข้อมูลตามเดิม)</li>
            </ul>
        </details>

        <div class="ps-card">
            <h3>① ค่ากลาง</h3>
            <div class="ps-row">
                <div class="ps-lbl"><b>เปิดใช้ฟีเจอร์สัตว์เลี้ยงเข้าพัก</b>
                    <small>ปิด = หน้าจองไม่แสดงอะไรเกี่ยวกับสัตว์เลี้ยงเลย</small></div>
                <div class="ps-in"><label class="ps-chk"><asp:CheckBox ID="chkEnabled" runat="server" /> เปิดใช้งาน</label></div>
            </div>
            <div class="ps-row">
                <div class="ps-lbl"><b>หน่วยคิดค่าบริการ</b>
                    <small>ต่อตัวต่อคืน = จำนวนตัว × ค่าบริการ × จำนวนคืน · ต่อการเข้าพัก = จำนวนตัว × ค่าบริการ (ไม่ขึ้นกับจำนวนคืน)</small></div>
                <div class="ps-in">
                    <asp:DropDownList ID="ddlFeeUnit" runat="server">
                        <asp:ListItem Value="NIGHT" Text="ต่อตัว ต่อคืน (ค่าเริ่มต้น)" />
                        <asp:ListItem Value="STAY" Text="ต่อตัว ต่อการเข้าพัก" />
                    </asp:DropDownList>
                </div>
            </div>
        </div>

        <div class="ps-card">
            <h3>② ห้องที่รับสัตว์เลี้ยง</h3>
            <div class="sub">
                ติ๊ก "รับ" เฉพาะห้องที่อนุญาต · จำนวนสูงสุดต่อห้องต้องอย่างน้อย 1 ตัว · ค่าบริการต่อ 1 ตัว (บาท) ใส่ 0 = ไม่คิดเงิน<br />
                ห้องที่ไม่ติ๊ก ลูกค้าจะเห็นข้อความ "ห้องนี้ไม่รับสัตว์เลี้ยง" และระบุสัตว์เลี้ยงไม่ได้
            </div>
            <div class="ps-count" id="psCount"></div>
            <asp:PlaceHolder ID="phRooms" runat="server" />
        </div>

        <div class="ps-card">
            <h3>③ 🐾 นโยบายการนำสัตว์เลี้ยงเข้าพัก (Pet Policy)</h3>
            <div class="sub">
                แสดงบนหน้าจองเมื่อลูกค้าติ๊กว่ามีสัตว์เลี้ยง (ต้องติ๊กยอมรับก่อนจอง) และบนหน้ายืนยันการจอง ·
                พิมพ์ข้อความธรรมดา ขึ้นบรรทัดได้ · ตัวหนาด้วย <code>**ข้อความ**</code> ·
                บันทึกแล้วข้อความเปลี่ยน = ขึ้นฉบับใหม่ (แยกเลขฉบับจากนโยบายการจองหลัก)
            </div>
            <div class="as-editor">
                <div>
                    <asp:TextBox ID="txtPetPolicy" runat="server" TextMode="MultiLine" ValidateRequestMode="Disabled"
                        data-as-preview="pvPet" data-as-phcount="phPet" data-as-maxlen="20000"
                        data-as-label="นโยบายสัตว์เลี้ยง" />
                    <div class="as-phbar"><span id="phPet" class="as-phcount"></span>
                        <button type="button" class="as-mini-btn" data-as-nextph="<%= txtPetPolicy.ClientID %>">ไปช่อง [แก้ไข] ถัดไป ↓</button></div>
                </div>
                <div><div class="as-pv-label">👁 ลูกค้าจะเห็นแบบนี้ (เมื่อติ๊กว่ามีสัตว์เลี้ยง)</div><div class="as-preview" id="pvPet"></div></div>
            </div>
        </div>

        <div class="ps-card">
            <h3>ฉบับที่บันทึกแล้ว (ลูกค้าเห็นข้อความนี้อยู่ตอนนี้)</h3>
            <asp:Literal ID="litPreview" runat="server" />
        </div>

        <div class="ps-card as-savebar">
            <div class="as-savebar-row">
                <asp:Button ID="btnSave" runat="server" CssClass="ps-btn" Text="💾 บันทึกทั้งหมด" OnClick="btnSave_Click"
                    data-as-validate=".ps-wrap" />
                <span class="as-dirty-flag">● มีการแก้ไขที่ยังไม่ได้บันทึก</span>
                <span class="as-clean-flag">ยังไม่มีการแก้ไข</span>
                <span class="as-clienterr"></span>
                <a class="ps-btn ghost" href="<%= ResolveUrl("~/Admin/Settings/BookingPolicies") %>">นโยบายการจองหลัก</a>
            </div>
        </div>
    </div>

    <script>
        (function () {
            var chk = document.getElementById('<%= chkEnabled.ClientID %>');
            var ta = document.getElementById('<%= txtPetPolicy.ClientID %>');
            var counter = document.getElementById('psCount');

            function roomBoxes() { return document.querySelectorAll('table.ps-rooms input[type=checkbox]'); }

            // นับห้องที่ติ๊ก "รับ" + ไฮไลต์แถว — เห็นผลทันทีไม่ต้องรอบันทึก
            function paintRooms() {
                var boxes = roomBoxes(), n = 0;
                for (var i = 0; i < boxes.length; i++) {
                    var tr = boxes[i].closest ? boxes[i].closest('tr') : null;
                    if (boxes[i].checked) n++;
                    if (tr) { if (boxes[i].checked) tr.classList.add('ps-on'); else tr.classList.remove('ps-on'); }
                }
                if (counter) counter.textContent = boxes.length
                    ? 'รับสัตว์เลี้ยง ' + n + ' จาก ' + boxes.length + ' ห้อง'
                    : '';
            }
            document.addEventListener('change', function (e) {
                if (e.target && e.target.type === 'checkbox' && e.target.closest && e.target.closest('table.ps-rooms')) paintRooms();
            });
            paintRooms();

            if (!window.AsSettings || !chk) return;
            AsSettings.guard('<%= btnSave.ClientID %>', function () {
                // ปิดฟีเจอร์ที่เปิดอยู่ — ลูกค้าจะไม่เห็นตัวเลือกสัตว์เลี้ยงอีก
                if (chk.defaultChecked && !chk.checked)
                    return confirm('ปิดฟีเจอร์สัตว์เลี้ยงเข้าพัก?\nหน้าจองจะไม่แสดงตัวเลือกสัตว์เลี้ยงอีก (ใบจองเดิมไม่เปลี่ยน)');
                if (!chk.checked) return true;

                var boxes = roomBoxes(), n = 0;
                for (var i = 0; i < boxes.length; i++) if (boxes[i].checked) n++;
                if (boxes.length && n === 0 &&
                    !confirm('เปิดใช้แล้วแต่ยังไม่ได้ติ๊กห้องที่รับสัตว์เลี้ยงเลย\nลูกค้าจะเห็นแต่ "ห้องนี้ไม่รับสัตว์เลี้ยง" — บันทึกต่อไหม?'))
                    return false;

                if (ta && ta.value.replace(/\s+/g, '').length === 0) {
                    alert('เปิดใช้แล้วต้องมีข้อความนโยบายสัตว์เลี้ยง (ลูกค้าต้องติ๊กยอมรับ)');
                    ta.focus();
                    return false;
                }
                var ph = ta ? AsSettings.countPlaceholders(ta.value) : 0;
                if (ph > 0)
                    return confirm('นโยบายสัตว์เลี้ยงยังมีข้อความตัวอย่าง/ช่อง [แก้ไข: …] ' + ph + ' จุด\n'
                        + 'ลูกค้าจะเห็นข้อความนี้ตามจริง — บันทึกต่อเลยไหม?');
                return true;
            });
        })();
    </script>
</asp:Content>
