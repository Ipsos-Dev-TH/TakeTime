<%@ Page Title="ช่องทางชำระเงิน" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="PaymentChannels.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Settings.PaymentChannelsSettings" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .pc-wrap { max-width: 1080px; margin: 0 auto; padding: 12px 12px 60px; }
        .pc-head { background: linear-gradient(135deg,#1b7a4b,#0f5c37); color:#fff;
                   border-radius:14px; padding:20px 22px; margin-bottom:16px; }
        .pc-head h2 { margin:0 0 6px; font-size:21px; }
        .pc-head p { margin:0; opacity:.92; font-size:14px; line-height:1.65; }

        .pc-card { background:#fff; border-radius:14px; padding:18px 20px; margin-bottom:16px;
                   box-shadow:0 2px 10px rgba(0,0,0,.05); }
        .pc-card h3 { margin:0 0 4px; font-size:16.5px; color:#1b4332; }
        .pc-card .sub { color:#7b8a83; font-size:13px; margin-bottom:12px; line-height:1.6; }

        .pc-alert { padding:12px 15px; border-radius:10px; margin-bottom:14px; font-size:14px; line-height:1.65; }
        .pc-alert.ok { background:#e8f6ee; color:#16653e; }
        .pc-alert.err { background:#fdecec; color:#a12626; }
        .pc-alert.warn { background:#fff6e5; color:#8a5a00; }
        .pc-alert.info { background:#eef4fb; color:#1d4e79; }

        .pc-ch { border:1px solid #e6ece8; border-radius:12px; padding:14px 16px; margin-bottom:12px; }
        .pc-ch.off { background:#fafbfa; }
        .pc-ch-head { display:flex; gap:10px; align-items:center; flex-wrap:wrap; margin-bottom:10px; }
        .pc-ch-head b { font-size:15px; color:#2c3e37; }
        .pc-tag { font-size:11.5px; padding:2px 9px; border-radius:20px; font-weight:600; background:#eef2f5; color:#4a5b66; }
        .pc-tag.live { background:#e8f6ee; color:#16653e; }
        .pc-tag.wait { background:#fff6e5; color:#8a5a00; }
        .pc-tag.never { background:#fdecec; color:#a12626; }

        .pc-grid { display:grid; grid-template-columns:repeat(auto-fill,minmax(190px,1fr)); gap:10px 14px; }
        .pc-f label { display:block; font-weight:600; font-size:12.5px; color:#46584f; margin-bottom:3px; }
        .pc-f input[type=text], .pc-f select, .pc-f textarea {
            width:100%; padding:8px 10px; border:1px solid #dbe3de; border-radius:8px; font-size:14px; font-family:inherit; }
        .pc-f textarea { min-height:64px; }
        .pc-f input:focus, .pc-f select:focus, .pc-f textarea:focus {
            outline:0; border-color:#1b7a4b; box-shadow:0 0 0 3px rgba(27,122,75,.12); }
        .pc-wide { grid-column:1 / -1; }
        .pc-chks { display:flex; gap:16px; flex-wrap:wrap; align-items:center; margin:8px 0 4px; }
        .pc-chks label { display:flex; align-items:center; gap:7px; font-size:14px; font-weight:500; }
        .pc-chks input { width:18px; height:18px; accent-color:#1b7a4b; }

        .pc-btn { padding:11px 20px; border:0; border-radius:10px; background:#1b7a4b; color:#fff;
                  font-size:14.5px; font-weight:600; cursor:pointer; text-decoration:none; display:inline-block; }
        .pc-btn:hover { background:#16653e; color:#fff; text-decoration:none; }
        .pc-btn.ghost { background:#fff; color:#46584f; border:1.5px solid #dbe3de; }
        .pc-actions { display:flex; gap:10px; flex-wrap:wrap; }

        .pc-prev { display:grid; grid-template-columns:1fr 1fr; gap:14px; }
        .pc-prev ul { margin:6px 0 0; padding-left:18px; font-size:14px; line-height:1.8; }
        .pc-note { font-size:12.5px; color:#8b978f; }

        @media (max-width: 760px) {
            .pc-prev { grid-template-columns:1fr; }
        }
    </style>

    <div class="pc-wrap">
        <div class="pc-head">
            <h2><i class="fas fa-list-check"></i> ช่องทางชำระเงิน</h2>
            <p>
                กำหนดว่าลูกค้า/พนักงานเห็นช่องทางไหน พร้อมข้อความแนะนำ เงื่อนไข และ QR รายช่องทาง<br />
                ลูกค้า<b>ไม่มีวันเห็น</b> เงินสด · เงินทดรองกรรมการ · OTA — ช่องทาง PaySo โผล่เองเมื่อ PaySo พร้อมใช้งาน
            </p>
        </div>

        <asp:Literal ID="litMsg" runat="server" />

        <div class="pc-card">
            <h3>สถานะเกตเวย์</h3>
            <asp:Literal ID="litStatus" runat="server" />
        </div>

        <div class="pc-card">
            <h3>ตัวอย่างที่แสดงผลจริงตอนนี้</h3>
            <div class="sub">คำนวณจากค่าที่บันทึกแล้ว (รวมสถานะเกตเวย์) — ใช้ตรวจก่อนเปิดให้ลูกค้า</div>
            <asp:Literal ID="litPreview" runat="server" />
        </div>

        <div class="pc-card">
            <h3>นโยบายยกเลิกหลัก (ครอบทุกช่องทาง)</h3>
            <div class="sub">แสดงคู่กับทุกช่องทางในหน้าจองของลูกค้า — เงื่อนไขเฉพาะช่องทางใส่ในแต่ละช่องด้านล่าง</div>
            <div class="pc-f">
                <asp:TextBox ID="txtPolicy" runat="server" TextMode="MultiLine" Rows="5"
                    placeholder="เช่น ยกเลิกก่อนวันเข้าพัก 7 วัน คืนเงินเต็มจำนวน · 3–6 วัน คืน 50% · น้อยกว่า 3 วัน ไม่คืนเงิน" />
            </div>
        </div>

        <div class="pc-card">
            <h3>รายการช่องทาง</h3>
            <div class="sub">
                หนึ่งช่องทาง = หนึ่งแถวแหล่งเงิน (Account_Paid_How) — การผูกบัญชี NextAcc ทำที่หน้า
                <b>Accounting Integration → แหล่งเงิน</b> · เพิ่มแหล่งเงินใหม่แล้วกลับมาจัดชนิดที่หน้านี้
            </div>
            <asp:PlaceHolder ID="phRows" runat="server" />
        </div>

        <div class="pc-card">
            <div class="pc-actions">
                <asp:Button ID="btnSave" runat="server" CssClass="pc-btn" Text="💾 บันทึกทั้งหมด" OnClick="btnSave_Click" />
                <a class="pc-btn ghost" href="<%= ResolveUrl("~/Admin/Settings/PaymentGateway") %>">ตั้งค่าเกตเวย์ (PaySo / Omise)</a>
                <a class="pc-btn ghost" href="<%= ResolveUrl("~/Admin/Settings/SecurityDeposit") %>">เงินประกันความเสียหาย</a>
            </div>
        </div>
    </div>
</asp:Content>
