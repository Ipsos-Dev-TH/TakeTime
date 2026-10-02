<%@ Page Title="ช่องทางชำระเงิน" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="PaymentChannels.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Settings.PaymentChannelsSettings" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <link rel="stylesheet" href="<%= ResolveUrl("~/Content/admin-settings.css") %>?v=<%= Take_Time_BangPhra.Admin.Settings.SettingsUi.AssetVersion %>" />
    <script src="<%= ResolveUrl("~/Scripts/admin-settings.js") %>?v=<%= Take_Time_BangPhra.Admin.Settings.SettingsUi.AssetVersion %>"></script>
    <style>
        .pc-wrap { max-width: 1080px; margin: 0 auto; padding: 12px 12px 30px; }
        .pc-head { background: linear-gradient(135deg,#5d4037,#3e2723); color:#fff;
                   border-radius:14px; padding:20px 22px; margin-bottom:16px; }
        .pc-head h2 { margin:0 0 6px; font-size:21px; }
        .pc-head p { margin:0; opacity:.92; font-size:14px; line-height:1.65; }

        .pc-card { background:#fff; border-radius:14px; padding:18px 20px; margin-bottom:16px;
                   box-shadow:0 2px 10px rgba(0,0,0,.05); }
        .pc-card h3 { margin:0 0 4px; font-size:16.5px; color:#3e2723; }
        .pc-card .sub { color:#7b8a83; font-size:13px; margin-bottom:12px; line-height:1.6; }

        .pc-alert { padding:12px 15px; border-radius:10px; margin-bottom:14px; font-size:14px; line-height:1.65; }
        .pc-alert.ok { background:#e8f6ee; color:#16653e; }
        .pc-alert.err { background:#fdecec; color:#a12626; }
        .pc-alert.warn { background:#fff6e5; color:#8a5a00; }
        .pc-alert.info { background:#eef4fb; color:#1d4e79; }
        .pc-alert a { color:inherit; font-weight:600; }

        .pc-ch { border:1px solid #ece6e3; border-radius:12px; padding:14px 16px; margin-bottom:12px; scroll-margin-top:80px; }
        .pc-ch.off { background:#fafafa; }
        .pc-ch.cust { border-left:4px solid #2e7d32; }
        .pc-ch-head { display:flex; gap:10px; align-items:center; flex-wrap:wrap; margin-bottom:10px; }
        .pc-ch-head b { font-size:15px; color:#3e2723; }
        .pc-tag { font-size:11.5px; padding:2px 9px; border-radius:20px; font-weight:600; background:#efebe9; color:#5d4037; }
        .pc-tag.live { background:#e8f6ee; color:#16653e; }
        .pc-tag.wait { background:#fff6e5; color:#8a5a00; }
        .pc-tag.never { background:#fdecec; color:#a12626; }

        .pc-grid { display:grid; grid-template-columns:repeat(auto-fill,minmax(190px,1fr)); gap:10px 14px; }
        .pc-f label { display:block; font-weight:600; font-size:12.5px; color:#5d4037; margin-bottom:3px; }
        .pc-f input[type=text], .pc-f select, .pc-f textarea {
            width:100%; padding:8px 10px; border:1px solid #dbd3cf; border-radius:8px; font-size:14px; font-family:inherit; }
        .pc-f textarea { min-height:64px; }
        .pc-f input:focus, .pc-f select:focus, .pc-f textarea:focus {
            outline:0; border-color:#8d6e63; box-shadow:0 0 0 3px rgba(141,110,99,.15); }
        .pc-f .hint { display:block; font-size:12px; color:#8d8d8d; margin-top:3px; line-height:1.5; }
        .pc-wide { grid-column:1 / -1; }
        .pc-chks { display:flex; gap:16px; flex-wrap:wrap; align-items:center; margin:2px 0 10px; }
        .pc-chks label { display:flex; align-items:center; gap:7px; font-size:14px; font-weight:500; }
        .pc-chks input { width:18px; height:18px; accent-color:#5d4037; }

        details.pc-adv { margin-top:10px; border-top:1px dashed #e0d7d3; padding-top:8px; }
        details.pc-adv > summary { cursor:pointer; font-size:13px; font-weight:600; color:#8d6e63; margin-bottom:8px; outline:none; }

        .pc-btn { padding:11px 20px; border:0; border-radius:10px; background:#5d4037; color:#fff;
                  font-size:14.5px; font-weight:600; cursor:pointer; text-decoration:none; display:inline-block; }
        .pc-btn:hover { background:#4e342e; color:#fff; text-decoration:none; }
        .pc-btn.ghost { background:#fff; color:#5d4037; border:1.5px solid #d7ccc8; }

        .pc-prev { display:grid; grid-template-columns:1fr 1fr; gap:14px; }
        .pc-prev ul { margin:6px 0 0; padding-left:18px; font-size:14px; line-height:1.8; }
        .pc-note { font-size:12.5px; color:#8b978f; }

        @media (max-width: 768px) {
            .pc-prev { grid-template-columns:1fr; }
            .pc-grid { grid-template-columns:1fr; }
        }
    </style>

    <div class="pc-wrap">
        <nav class="as-crumb" aria-label="เส้นทาง">
            <a href="<%= ResolveUrl("~/Admin/Settings/Index") %>"><i class="fas fa-sliders"></i> ศูนย์ตั้งค่า</a>
            <span class="sep">›</span><span>การชำระเงิน</span>
            <span class="sep">›</span><span class="here">ช่องทางชำระเงิน</span>
            <a class="as-back" href="<%= ResolveUrl("~/Admin/Settings/Index") %>">← กลับศูนย์ตั้งค่า</a>
        </nav>

        <div class="pc-head">
            <h2><i class="fas fa-list-check"></i> ช่องทางชำระเงิน</h2>
            <p>
                เลือกว่าลูกค้าและพนักงานเห็นช่องทางจ่ายเงินไหนบ้าง พร้อมข้อความแนะนำ (เลขบัญชี) เงื่อนไข และรูป QR<br />
                ลูกค้า<b>ไม่มีวันเห็น</b> เงินสด · เงินทดรองกรรมการ · OTA — ช่องทาง PaySo จะโผล่ให้ลูกค้าเองเมื่อ PaySo พร้อมใช้งาน
            </p>
        </div>

        <asp:Literal ID="litMsg" runat="server" />

        <div class="pc-card">
            <h3>สถานะเกตเวย์</h3>
            <asp:Literal ID="litStatus" runat="server" />
        </div>

        <div class="pc-card">
            <h3>👁 ลูกค้า/พนักงานเห็นอะไรตอนนี้</h3>
            <div class="sub">คำนวณจากค่าที่<b>บันทึกแล้ว</b> (รวมสถานะเกตเวย์) — แก้ด้านล่างแล้วกดบันทึก รายการนี้จะอัปเดต</div>
            <asp:Literal ID="litPreview" runat="server" />
        </div>

        <div data-as-dirty="1">
            <div class="pc-card">
                <h3>นโยบายยกเลิกหลัก (ครอบทุกช่องทาง)</h3>
                <div class="sub">สรุปสั้น ๆ ที่ครอบทุกช่องทาง — เงื่อนไขเฉพาะช่องทางใส่ในแต่ละช่องด้านล่าง ·
                    นโยบายการยกเลิก<b>ฉบับเต็ม</b>ที่ลูกค้าต้องติ๊กยอมรับ แก้ที่หน้า
                    <a href="<%= ResolveUrl("~/Admin/Settings/BookingPolicies") %>#bp-cancel">นโยบายการจอง</a></div>
                <div class="pc-f">
                    <asp:TextBox ID="txtPolicy" runat="server" TextMode="MultiLine" Rows="5" data-as-maxlen="4000"
                        data-as-label="นโยบายยกเลิกหลัก"
                        placeholder="เช่น ยกเลิกก่อนวันเข้าพัก 7 วัน คืนเงินเต็มจำนวน · 3–6 วัน คืน 50% · น้อยกว่า 3 วัน ไม่คืนเงิน" />
                </div>
            </div>

            <div class="pc-card">
                <h3>รายการช่องทาง</h3>
                <details class="as-help">
                    <summary>แต่ละช่องตั้งอะไรได้บ้าง</summary>
                    <ul>
                        <li><b>ลูกค้าเห็น</b> = แสดงบนหน้าจองของลูกค้า · <b>พนักงานเห็น</b> = แสดงตอนพนักงานรับเงิน ·
                            <b>ต้องแนบสลิป</b> = ลูกค้าต้องอัปโหลดสลิปโอน</li>
                        <li><b>ข้อความแนะนำ</b> = สิ่งที่ลูกค้าเห็นเมื่อเลือกช่องทางนี้ เช่น ชื่อบัญชี เลขบัญชี ธนาคาร</li>
                        <li><b>ตั้งค่าขั้นสูง</b> (รหัส / ชนิด / เกตเวย์ / ลำดับ) — ปกติไม่ต้องแตะ ระบบจัดให้แล้ว</li>
                        <li>ชื่อช่องทางและการผูกบัญชี NextAcc แก้ที่หน้า <b>Accounting Integration → แหล่งเงิน</b> ·
                            เพิ่มแหล่งเงินใหม่ที่นั่นแล้วกลับมาตั้งการแสดงผลที่หน้านี้</li>
                    </ul>
                </details>
                <asp:PlaceHolder ID="phRows" runat="server" />
            </div>
        </div>

        <div class="pc-card as-savebar">
            <div class="as-savebar-row">
                <asp:Button ID="btnSave" runat="server" CssClass="pc-btn" Text="💾 บันทึกทั้งหมด" OnClick="btnSave_Click"
                    data-as-validate=".pc-wrap" />
                <span class="as-dirty-flag">● มีการแก้ไขที่ยังไม่ได้บันทึก</span>
                <span class="as-clean-flag">ยังไม่มีการแก้ไข</span>
                <span class="as-clienterr"></span>
                <a class="pc-btn ghost" href="<%= ResolveUrl("~/Admin/Settings/PaymentGateway") %>">ตั้งค่าเกตเวย์ (PaySo / Omise)</a>
                <a class="pc-btn ghost" href="<%= ResolveUrl("~/Admin/Settings/SecurityDeposit") %>">เงินประกันความเสียหาย</a>
            </div>
        </div>
    </div>

    <script>
        (function () {
            if (!window.AsSettings) return;
            // ไม่มีช่องทางไหนที่ลูกค้าเห็นเลย = ลูกค้าจองแล้วไม่มีทางจ่ายเงิน → ถามก่อนบันทึก
            AsSettings.guard('<%= btnSave.ClientID %>', function () {
                var boxes = document.querySelectorAll("input[type=checkbox][id$='_cust']");
                if (!boxes.length) return true;
                for (var i = 0; i < boxes.length; i++) if (boxes[i].checked && !boxes[i].disabled) return true;
                return confirm('ไม่มีช่องทางไหนติ๊ก "ลูกค้าเห็น" เลย\nลูกค้าที่จองผ่านเว็บจะไม่มีช่องทางชำระเงินให้เลือก — บันทึกต่อไหม?');
            });
        })();
    </script>
</asp:Content>
