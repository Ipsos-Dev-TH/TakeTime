<%@ Page Title="เงินประกันความเสียหาย" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="SecurityDeposit.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Settings.SecurityDepositSettings" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .sd-wrap { max-width: 1000px; margin: 0 auto; padding: 12px 12px 60px; }
        .sd-head { background: linear-gradient(135deg,#1d4e79,#123553); color:#fff;
                   border-radius:14px; padding:20px 22px; margin-bottom:16px; }
        .sd-head h2 { margin:0 0 6px; font-size:21px; }
        .sd-head p { margin:0; opacity:.92; font-size:14px; line-height:1.7; }

        .sd-card { background:#fff; border-radius:14px; padding:18px 20px; margin-bottom:16px;
                   box-shadow:0 2px 10px rgba(0,0,0,.05); }
        .sd-card h3 { margin:0 0 4px; font-size:16.5px; color:#123553; }
        .sd-card .sub { color:#7b8a93; font-size:13px; margin-bottom:14px; line-height:1.65; }

        .sd-row { display:flex; gap:16px; padding:11px 0; border-bottom:1px solid #eff2f5; align-items:flex-start; }
        .sd-row:last-child { border-bottom:0; }
        .sd-lbl { flex:0 0 280px; }
        .sd-lbl b { display:block; font-size:14.5px; color:#2c3742; }
        .sd-lbl small { display:block; color:#8b959e; font-size:12.5px; line-height:1.6; margin-top:3px; }
        .sd-in { flex:1; min-width:0; }
        .sd-in input[type=text] { width:100%; max-width:220px; padding:9px 11px; border:1px solid #dbe1e7;
                                  border-radius:9px; font-size:14px; }
        .sd-in input:focus { outline:0; border-color:#1d4e79; box-shadow:0 0 0 3px rgba(29,78,121,.12); }
        .sd-chk { display:flex; align-items:center; gap:9px; font-size:14px; }
        .sd-chk input { width:18px; height:18px; accent-color:#1d4e79; }

        .sd-btn { padding:11px 20px; border:0; border-radius:10px; background:#1d4e79; color:#fff;
                  font-size:14.5px; font-weight:600; cursor:pointer; }
        .sd-btn:hover { background:#163c5e; }
        .sd-btn.ghost { background:#fff; color:#46545f; border:1.5px solid #dbe1e7; }
        .sd-actions { display:flex; gap:10px; flex-wrap:wrap; align-items:center; }

        .sd-alert { padding:12px 15px; border-radius:10px; margin-bottom:14px; font-size:14px; line-height:1.7; }
        .sd-alert.ok { background:#e8f6ee; color:#16653e; }
        .sd-alert.err { background:#fdecec; color:#a12626; }
        .sd-alert.warn { background:#fff6e5; color:#8a5a00; }
        .sd-alert.info { background:#eef4fb; color:#1d4e79; }

        /* ── ตารางห้องพัก ── */
        .sd-rooms { width:100%; border-collapse:collapse; font-size:14px; }
        .sd-rooms th { background:#f4f7f9; text-align:left; padding:10px; color:#46545f; font-weight:600;
                       font-size:13px; }
        .sd-rooms td { padding:9px 10px; border-top:1px solid #eff2f5; vertical-align:middle; }
        .sd-rooms tr:hover td { background:#fafcfd; }
        .sd-rooms input[type=text] { width:130px; padding:8px 10px; border:1px solid #dbe1e7;
                                     border-radius:8px; font-size:14px; text-align:right; }
        .sd-rooms input:focus { outline:0; border-color:#1d4e79; box-shadow:0 0 0 3px rgba(29,78,121,.12); }
        .sd-use { font-size:12.5px; color:#8b959e; }
        .sd-use.own { color:#16653e; font-weight:600; }

        .sd-bulk { background:#f7fafc; border-radius:10px; padding:12px 14px; margin-bottom:14px;
                   display:flex; gap:10px; align-items:center; flex-wrap:wrap; font-size:13.5px; }
        .sd-bulk input[type=text] { width:120px; padding:7px 10px; border:1px solid #dbe1e7;
                                    border-radius:8px; font-size:14px; text-align:right; }

        .sd-steps { display:flex; gap:8px; flex-wrap:wrap; margin-bottom:4px; }
        .sd-step { display:flex; align-items:center; gap:8px; padding:8px 14px; border-radius:24px;
                   font-size:13.5px; background:#f2f5f7; color:#5a6770; }
        .sd-step.done { background:#e8f6ee; color:#16653e; font-weight:600; }
        .sd-step .n { width:22px; height:22px; border-radius:50%; display:inline-flex; align-items:center;
                      justify-content:center; background:#fff; font-weight:700; font-size:12px;
                      border:1.5px solid currentColor; flex:none; }

        @media (max-width: 760px) {
            .sd-row { flex-direction:column; gap:7px; }
            .sd-lbl { flex:none; }
            .sd-rooms thead { display:none; }
            .sd-rooms tr { display:block; border-top:1px solid #eff2f5; padding:8px 0; }
            .sd-rooms td { display:flex; justify-content:space-between; align-items:center;
                           border:0; padding:5px 2px; }
            .sd-rooms td:before { content:attr(data-th); color:#8b959e; font-size:12.5px; }
        }
    </style>

    <div class="sd-wrap">
        <div class="sd-head">
            <h2><i class="fas fa-shield-halved"></i> เงินประกันความเสียหาย</h2>
            <p>
                แทนการให้ลูกค้าโอนเงินประกันเข้าบัญชีแล้วคืนเป็นเงินสดทีหลัง —
                ระบบ<b>กันวงเงินไว้บนบัตร</b> เงินไม่เข้าไม่ออกจริง<br />
                เช็คเอาท์ไม่มีความเสียหาย = คืนวงเงินอัตโนมัติ · มีความเสียหาย = ตัดเฉพาะที่เสียหายจริง
            </p>
        </div>

        <asp:Literal ID="litMsg" runat="server" />

        <!-- ── สถานะความพร้อม ── -->
        <div class="sd-card">
            <h3>ความพร้อมของระบบ</h3>
            <div class="sub">ต้องครบทุกข้อ ลูกค้าถึงจะวางวงเงินผ่านบัตรได้จริง (รับเป็นเงินสดใช้ได้เสมอ)</div>
            <asp:Literal ID="litReady" runat="server" />
        </div>

        <!-- ── ค่ากลาง ── -->
        <div class="sd-card">
            <h3>ค่ากลางของที่พัก</h3>
            <div class="sub">ใช้กับห้องที่ไม่ได้ตั้งวงเงินเฉพาะไว้ — ค่าเดียวกับที่อยู่ในหน้ารับชำระเงินออนไลน์ แก้ที่นี่ก็ได้</div>

            <div class="sd-row">
                <div class="sd-lbl"><b>เปิดใช้เงินประกันความเสียหาย</b>
                    <small>ปิดอยู่ = ระบบทำงานเหมือนเดิมทุกอย่าง ไม่มีอะไรโผล่ให้ลูกค้าหรือพนักงานเห็น</small></div>
                <div class="sd-in">
                    <label class="sd-chk"><asp:CheckBox ID="chkEnabled" runat="server" /> เปิดใช้งาน</label>
                </div>
            </div>

            <div class="sd-row">
                <div class="sd-lbl"><b>วงเงินแนะนำ (บาท)</b>
                    <small>ตัวเลขตั้งต้นที่หน้าเช็คอินเติมให้ พนักงานแก้เป็นรายครั้งได้เสมอ</small></div>
                <div class="sd-in"><asp:TextBox ID="txtDefault" runat="server" /></div>
            </div>

            <div class="sd-row">
                <div class="sd-lbl"><b>เตือนก่อนวงเงินหมดอายุ (ชั่วโมง)</b>
                    <small>วงเงินบนบัตรอยู่ได้ 7 วันแล้วคืนลูกค้าเอง — ระบบเตือนล่วงหน้าตามค่านี้ให้ตัดสินใจก่อนหมดเวลา</small></div>
                <div class="sd-in"><asp:TextBox ID="txtWarnHours" runat="server" /></div>
            </div>
        </div>

        <!-- ── รายห้อง ── -->
        <div class="sd-card">
            <h3>วงเงินรายห้องพัก</h3>
            <div class="sub">
                เว้นว่าง = ใช้ค่ากลางด้านบน · ใส่ <b>0</b> = ห้องนี้ไม่เก็บเงินประกัน<br />
                ใบจองที่มีหลายห้อง ระบบใช้<b>ค่าสูงสุด</b>ของห้องในใบนั้น
            </div>

            <div class="sd-bulk">
                <span>เติมให้ทุกห้องพร้อมกัน:</span>
                <asp:TextBox ID="txtBulk" runat="server" placeholder="เช่น 2000" />
                <asp:Button ID="btnBulk" runat="server" CssClass="sd-btn ghost" Text="เติมทุกห้อง"
                    OnClick="btnBulk_Click" CausesValidation="false"
                    OnClientClick="return confirm('เขียนทับวงเงินของทุกห้องด้วยค่านี้?');" />
                <asp:Button ID="btnClear" runat="server" CssClass="sd-btn ghost" Text="ล้างทุกห้อง (กลับไปใช้ค่ากลาง)"
                    OnClick="btnClear_Click" CausesValidation="false"
                    OnClientClick="return confirm('ล้างวงเงินเฉพาะห้องทั้งหมด ให้กลับไปใช้ค่ากลาง?');" />
            </div>

            <asp:PlaceHolder ID="phRooms" runat="server" />
        </div>

        <div class="sd-card">
            <div class="sd-actions">
                <asp:Button ID="btnSave" runat="server" CssClass="sd-btn" Text="💾 บันทึกทั้งหมด" OnClick="btnSave_Click" />
                <a class="sd-btn ghost" style="text-decoration:none;display:inline-block"
                   href="<%= ResolveUrl("~/Payment/Charge") %>">ไปหน้าจุดรับเงิน (รับประกันตอนเช็คอิน)</a>
                <a class="sd-btn ghost" style="text-decoration:none;display:inline-block"
                   href="<%= ResolveUrl("~/Admin/Settings/PaymentGateway") %>">ตั้งค่าเกตเวย์</a>
            </div>
        </div>

        <!-- ── วงเงินที่ยังค้างอยู่ ── -->
        <div class="sd-card">
            <h3>วงเงินที่ยังกันอยู่ตอนนี้</h3>
            <div class="sub">รายการที่ยังไม่ได้ตัดหรือคืน — จัดการต่อได้ที่หน้าจุดรับเงิน/เช็คเอาท์</div>
            <asp:Literal ID="litHolds" runat="server" />
        </div>
    </div>
</asp:Content>
