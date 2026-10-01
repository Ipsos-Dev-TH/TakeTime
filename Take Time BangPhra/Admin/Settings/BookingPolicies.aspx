<%@ Page Title="นโยบายการจอง" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="BookingPolicies.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Settings.BookingPolicies" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <link rel="stylesheet" href="<%= ResolveUrl("~/Content/admin-settings.css") %>?v=<%= Take_Time_BangPhra.Admin.Settings.SettingsUi.AssetVersion %>" />
    <script src="<%= ResolveUrl("~/Scripts/admin-settings.js") %>?v=<%= Take_Time_BangPhra.Admin.Settings.SettingsUi.AssetVersion %>"></script>
    <style>
        .bp-wrap { max-width: 1180px; margin: 0 auto; padding: 12px 12px 30px; }
        .bp-head { background: linear-gradient(135deg,#5d4037,#3e2723); color:#fff;
                   border-radius:14px; padding:20px 22px; margin-bottom:16px; }
        .bp-head h2 { margin:0 0 6px; font-size:21px; }
        .bp-head p { margin:0; opacity:.92; font-size:14px; line-height:1.7; }

        .bp-card { background:#fff; border-radius:14px; padding:18px 20px; margin-bottom:16px;
                   box-shadow:0 2px 10px rgba(0,0,0,.05); }
        .bp-card h3 { margin:0 0 4px; font-size:16.5px; color:#3e2723; }
        .bp-card .sub { color:#7b8a93; font-size:13px; margin-bottom:12px; line-height:1.65; }

        .bp-btn { padding:11px 20px; border:0; border-radius:10px; background:#5d4037; color:#fff;
                  font-size:14.5px; font-weight:600; cursor:pointer; }
        .bp-btn:hover { background:#4e342e; }
        .bp-btn.ghost { background:#fff; color:#5d4037; border:1.5px solid #d7ccc8; text-decoration:none;
                        display:inline-block; }

        .bp-alert { padding:12px 15px; border-radius:10px; margin-bottom:14px; font-size:14px; line-height:1.7; }
        .bp-alert.ok { background:#e8f6ee; color:#16653e; }
        .bp-alert.err { background:#fdecec; color:#a12626; }
        .bp-alert.info { background:#eef4fb; color:#1d4e79; }
        .bp-alert.warn { background:#fff6e5; color:#8a5a00; }

        .bp-toc { display:flex; flex-wrap:wrap; gap:8px; margin-top:10px; }
        .bp-toc a { font-size:13px; padding:5px 12px; border-radius:20px; background:#efebe9; color:#5d4037;
                    text-decoration:none; font-weight:600; }
        .bp-toc a:hover { background:#d7ccc8; }

        .bp-prev { background:#faf8f7; border:1px dashed #d7ccc8; border-radius:10px; padding:12px 14px;
                   font-size:13.5px; line-height:1.75; color:#4e342e; margin-top:10px; }
        .bp-prev summary { cursor:pointer; font-weight:600; color:#6d4c41; }
    </style>

    <div class="bp-wrap" data-as-dirty="1">
        <nav class="as-crumb" aria-label="เส้นทาง">
            <a href="<%= ResolveUrl("~/Admin/Settings/Index") %>"><i class="fas fa-sliders"></i> ศูนย์ตั้งค่า</a>
            <span class="sep">›</span><span>การจอง &amp; นโยบาย</span>
            <span class="sep">›</span><span class="here">นโยบายการจอง</span>
            <a class="as-back" href="<%= ResolveUrl("~/Admin/Settings/Index") %>">← กลับศูนย์ตั้งค่า</a>
        </nav>

        <div class="bp-head">
            <h2><i class="fas fa-file-contract"></i> นโยบายการจอง</h2>
            <p>
                ข้อความที่ลูกค้าเห็นบน<b>หน้าจองห้องพัก</b> (ต้องติ๊กยอมรับก่อนกดยืนยัน) และบน<b>หน้ายืนยันการจอง</b> ·
                นโยบายการยกเลิกแสดงคู่กับทุกช่องทางการชำระเงิน
            </p>
        </div>

        <asp:Literal ID="litMsg" runat="server" />

        <div class="bp-card">
            <h3>สถานะ</h3>
            <asp:Literal ID="litInfo" runat="server" />
            <details class="as-help">
                <summary>วิธีใช้หน้านี้ (อ่านครั้งแรก)</summary>
                <ul>
                    <li>พิมพ์เป็นข้อความธรรมดา กด Enter ขึ้นบรรทัดได้ตามปกติ · อยากให้ตัวหนา พิมพ์ <code>**ข้อความ**</code></li>
                    <li>กล่องขวามือคือ <b>ตัวอย่างที่ลูกค้าจะเห็น</b> — เปลี่ยนทันทีที่พิมพ์</li>
                    <li>ข้อความ <mark class="as-ph">[แก้ไข: …]</mark> คือช่องที่ต้องใส่ข้อมูลจริงของที่พัก — กด "ไปช่องถัดไป" เพื่อกระโดดไปทีละจุด</li>
                    <li>กดบันทึกแล้วถ้าข้อความเปลี่ยน ระบบขึ้น <b>ฉบับใหม่</b> ให้อัตโนมัติ — ใบจองเก็บเลขฉบับที่ลูกค้ายอมรับไว้ (ฉบับเก่าไม่หาย)</li>
                    <li>ระบบไม่รับแท็ก HTML (แสดงเป็นตัวอักษรตามที่พิมพ์ เพื่อความปลอดภัย)</li>
                </ul>
            </details>
            <div class="bp-toc">
                <a href="#bp-terms">📜 เงื่อนไข</a><a href="#bp-privacy">🔒 ความเป็นส่วนตัว</a>
                <a href="#bp-refund">💸 คืนเงิน</a><a href="#bp-cancel">📅 ยกเลิก</a>
                <a href="<%= ResolveUrl("~/Admin/Settings/PetStay") %>">🐾 สัตว์เลี้ยง →</a>
            </div>
        </div>

        <div class="bp-card" id="bp-terms">
            <h3>📜 ข้อกำหนดและเงื่อนไข (Terms &amp; Conditions)</h3>
            <div class="sub">เงื่อนไขการจอง การเข้าพัก เวลาเช็คอิน/เอาท์ กฎระเบียบ</div>
            <div class="as-editor">
                <div>
                    <asp:TextBox ID="txtTerms" runat="server" TextMode="MultiLine" ValidateRequestMode="Disabled"
                        data-as-preview="pvTerms" data-as-phcount="phTerms" data-as-req="1" data-as-maxlen="20000"
                        data-as-label="ข้อกำหนดและเงื่อนไข" />
                    <div class="as-phbar"><span id="phTerms" class="as-phcount"></span>
                        <button type="button" class="as-mini-btn" data-as-nextph="<%= txtTerms.ClientID %>">ไปช่อง [แก้ไข] ถัดไป ↓</button></div>
                </div>
                <div><div class="as-pv-label">👁 ลูกค้าจะเห็นแบบนี้</div><div class="as-preview" id="pvTerms"></div></div>
            </div>
        </div>

        <div class="bp-card" id="bp-privacy">
            <h3>🔒 นโยบายความเป็นส่วนตัว (Privacy Policy)</h3>
            <div class="sub">ข้อมูลส่วนบุคคลที่เก็บ วัตถุประสงค์ ระยะเวลา และช่องทางติดต่อ (PDPA)</div>
            <div class="as-editor">
                <div>
                    <asp:TextBox ID="txtPrivacy" runat="server" TextMode="MultiLine" ValidateRequestMode="Disabled"
                        data-as-preview="pvPrivacy" data-as-phcount="phPrivacy" data-as-req="1" data-as-maxlen="20000"
                        data-as-label="นโยบายความเป็นส่วนตัว" />
                    <div class="as-phbar"><span id="phPrivacy" class="as-phcount"></span>
                        <button type="button" class="as-mini-btn" data-as-nextph="<%= txtPrivacy.ClientID %>">ไปช่อง [แก้ไข] ถัดไป ↓</button></div>
                </div>
                <div><div class="as-pv-label">👁 ลูกค้าจะเห็นแบบนี้</div><div class="as-preview" id="pvPrivacy"></div></div>
            </div>
        </div>

        <div class="bp-card" id="bp-refund">
            <h3>💸 นโยบายการคืนเงิน (Refund Policy)</h3>
            <div class="sub">ช่องทางและระยะเวลาคืนเงิน แยกตามวิธีชำระ (โอน / บัตร / ออนไลน์)</div>
            <div class="as-editor">
                <div>
                    <asp:TextBox ID="txtRefund" runat="server" TextMode="MultiLine" ValidateRequestMode="Disabled"
                        data-as-preview="pvRefund" data-as-phcount="phRefund" data-as-req="1" data-as-maxlen="20000"
                        data-as-label="นโยบายการคืนเงิน" />
                    <div class="as-phbar"><span id="phRefund" class="as-phcount"></span>
                        <button type="button" class="as-mini-btn" data-as-nextph="<%= txtRefund.ClientID %>">ไปช่อง [แก้ไข] ถัดไป ↓</button></div>
                </div>
                <div><div class="as-pv-label">👁 ลูกค้าจะเห็นแบบนี้</div><div class="as-preview" id="pvRefund"></div></div>
            </div>
        </div>

        <div class="bp-card" id="bp-cancel">
            <h3>📅 นโยบายการยกเลิกการจอง (Cancellation Policy — นโยบายหลัก)</h3>
            <div class="sub">แสดงคู่กับ<b>ทุกช่องทางการชำระเงิน</b>บนหน้าจอง และบนหน้ายืนยันการจอง</div>
            <div class="as-editor">
                <div>
                    <asp:TextBox ID="txtCancel" runat="server" TextMode="MultiLine" ValidateRequestMode="Disabled"
                        data-as-preview="pvCancel" data-as-phcount="phCancel" data-as-req="1" data-as-maxlen="20000"
                        data-as-label="นโยบายการยกเลิกการจอง" />
                    <div class="as-phbar"><span id="phCancel" class="as-phcount"></span>
                        <button type="button" class="as-mini-btn" data-as-nextph="<%= txtCancel.ClientID %>">ไปช่อง [แก้ไข] ถัดไป ↓</button></div>
                </div>
                <div><div class="as-pv-label">👁 ลูกค้าจะเห็นแบบนี้</div><div class="as-preview" id="pvCancel"></div></div>
            </div>
        </div>

        <div class="bp-card">
            <h3>🐾 นโยบายการนำสัตว์เลี้ยงเข้าพัก (Pet Policy)</h3>
            <div class="sub" style="margin-bottom:0;">
                มีเลขฉบับของตัวเอง — แก้ข้อความ พร้อมตั้งห้องที่รับสัตว์เลี้ยง/จำนวนสูงสุด/ค่าบริการ ได้ที่หน้า
                <a href="<%= ResolveUrl("~/Admin/Settings/PetStay") %>">สัตว์เลี้ยงเข้าพัก</a>
            </div>
        </div>

        <div class="bp-card">
            <h3>ฉบับที่บันทึกแล้ว (ลูกค้าเห็นข้อความนี้อยู่ตอนนี้)</h3>
            <div class="sub" style="margin-bottom:4px;">ใช้เทียบกับที่กำลังแก้ — ถ้ายังไม่กดบันทึก ลูกค้ายังเห็นฉบับนี้</div>
            <asp:Literal ID="litPreview" runat="server" />
        </div>

        <div class="bp-card as-savebar">
            <div class="as-savebar-row">
                <asp:Button ID="btnSave" runat="server" CssClass="bp-btn" Text="💾 บันทึกนโยบาย" OnClick="btnSave_Click"
                    data-as-validate=".bp-wrap" />
                <span class="as-dirty-flag">● มีการแก้ไขที่ยังไม่ได้บันทึก</span>
                <span class="as-clean-flag">ยังไม่มีการแก้ไข</span>
                <span class="as-clienterr"></span>
                <a class="bp-btn ghost" href="<%= ResolveUrl("~/Admin/Settings/Index") %>">กลับศูนย์ตั้งค่า</a>
            </div>
        </div>
    </div>

    <script>
        (function () {
            // บันทึกทั้งที่ยังมี "[แก้ไข: …]" ค้าง — ถามก่อน (ลูกค้าจะเห็นข้อความตัวอย่างตามจริง)
            if (!window.AsSettings) return;
            var ids = ['<%= txtTerms.ClientID %>', '<%= txtPrivacy.ClientID %>', '<%= txtRefund.ClientID %>', '<%= txtCancel.ClientID %>'];
            AsSettings.guard('<%= btnSave.ClientID %>', function () {
                var n = 0;
                for (var i = 0; i < ids.length; i++) {
                    var ta = document.getElementById(ids[i]);
                    if (ta) n += AsSettings.countPlaceholders(ta.value);
                }
                if (n === 0) return true;
                return confirm('ยังมีข้อความตัวอย่าง/ช่อง [แก้ไข: …] ค้างอยู่ ' + n + ' จุด\n'
                    + 'ลูกค้าจะเห็นข้อความนี้ตามจริง — บันทึกต่อเลยไหม?');
            });
        })();
    </script>
</asp:Content>
