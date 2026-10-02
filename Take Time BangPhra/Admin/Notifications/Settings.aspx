<%@ Page Title="การแจ้งเตือน" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Settings.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Notifications.Settings" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .nt-wrap { max-width: 1080px; margin: 0 auto; padding: 12px 12px 60px; }

        .nt-head { background: linear-gradient(135deg,#5D4037,#3E2723); color:#fff;
                   border-radius:14px; padding:20px 22px; margin-bottom:16px; }
        .nt-head h2 { margin:0 0 6px; font-size:21px; }
        .nt-head p { margin:0; opacity:.92; font-size:14px; line-height:1.65; }

        .nt-card { background:#fff; border-radius:14px; padding:18px 20px; margin-bottom:16px;
                   box-shadow:0 2px 10px rgba(0,0,0,.05); }
        .nt-card h3 { margin:0 0 4px; font-size:16.5px; color:#3E2723; }
        .nt-card .sub { color:#8d7f76; font-size:13px; margin-bottom:14px; line-height:1.6; }

        .nt-row { display:flex; gap:16px; padding:11px 0; border-bottom:1px solid #f3efec; align-items:flex-start; }
        .nt-row:last-child { border-bottom:0; }
        .nt-lbl { flex:0 0 260px; }
        .nt-lbl b { display:block; font-size:14.5px; color:#2c2320; }
        .nt-lbl small { display:block; color:#988c85; font-size:12.5px; line-height:1.6; margin-top:3px; }
        .nt-in { flex:1; min-width:0; }
        .nt-in input[type=text] { width:100%; padding:9px 11px; border:1px solid #e0d8d2; border-radius:9px; font-size:14px; }
        .nt-in input[type=text]:focus { outline:0; border-color:#5D4037; box-shadow:0 0 0 3px rgba(93,64,55,.12); }
        .nt-chk { display:flex; align-items:center; gap:9px; font-size:14px; }
        .nt-chk input { width:19px; height:19px; accent-color:#2e7d32; }

        /* ── ตารางเหตุการณ์ ── */
        .nt-grp { margin-top:18px; }
        .nt-grp > .cat { font-size:13px; font-weight:700; color:#8d6e63; text-transform:none;
                         padding:8px 0 6px; border-bottom:2px solid #efe7e2; margin-bottom:4px; }

        .ev { display:grid; grid-template-columns: 1fr 150px 150px; gap:12px;
              align-items:start; padding:12px 0; border-bottom:1px solid #f5f1ee; }
        .ev:last-child { border-bottom:0; }
        .ev-name b { font-size:14.5px; color:#2c2320; }
        .ev-name .urgent { display:inline-block; background:#fdecec; color:#a12626; border-radius:5px;
                           font-size:11px; font-weight:700; padding:1px 6px; margin-left:6px; }
        .ev-name small { display:block; color:#988c85; font-size:12.5px; line-height:1.55; margin-top:2px; }
        .ev-ch { display:flex; flex-direction:column; gap:6px; }
        .ev-ch .top { display:flex; align-items:center; gap:7px; font-size:13.5px; }
        .ev-ch input[type=checkbox] { width:19px; height:19px; accent-color:#2e7d32; }
        .ev-ch input[type=text] { width:100%; padding:5px 8px; border:1px solid #e6ded8; border-radius:7px;
                                  font-size:12px; font-family:Consolas,monospace; }
        .ev-hdr { display:grid; grid-template-columns: 1fr 150px 150px; gap:12px;
                  font-size:12.5px; font-weight:700; color:#8d6e63; padding-bottom:6px; }

        .nt-btn { padding:11px 20px; border:0; border-radius:10px; background:#5D4037; color:#fff;
                  font-size:14.5px; font-weight:600; cursor:pointer; }
        .nt-btn:hover { background:#4E342E; }
        .nt-btn.ghost { background:#fff; color:#5d4c45; border:1.5px solid #e0d8d2; }
        .nt-actions { display:flex; gap:10px; flex-wrap:wrap; }

        .nt-alert { padding:12px 15px; border-radius:10px; margin-bottom:14px; font-size:14px; line-height:1.65; }
        .nt-alert.ok { background:#e8f6ee; color:#16653e; }
        .nt-alert.err { background:#fdecec; color:#a12626; }
        .nt-alert.warn { background:#fff6e5; color:#8a5a00; }

        .nt-pre { background:#241d1a; color:#e2d9d4; border-radius:10px; padding:13px;
                  font-family:Consolas,monospace; font-size:12.3px; white-space:pre-wrap;
                  max-height:280px; overflow:auto; }

        .nt-status { display:inline-block; padding:3px 10px; border-radius:11px; font-size:12px; font-weight:600; }
        .nt-status.on { background:#e8f5e9; color:#2e7d32; }
        .nt-status.off { background:#fff3e0; color:#e65100; }

        .nt-log { width:100%; border-collapse:collapse; font-size:12.8px; }
        .nt-log th { text-align:left; padding:7px 8px; background:#f7f3f1; color:#5d4c45; }
        .nt-log td { padding:7px 8px; border-top:1px solid #f3efec; vertical-align:top; }

        @media (max-width: 760px) {
            .nt-row { flex-direction:column; gap:7px; }
            .nt-lbl { flex:none; }
            .ev, .ev-hdr { grid-template-columns: 1fr; }
            .ev-hdr { display:none; }
            .ev-ch { border-top:1px dashed #efe7e2; padding-top:8px; }
        }
    </style>

    <div class="nt-wrap">
        <div class="nt-head">
            <h2><i class="fas fa-bell"></i> การแจ้งเตือน</h2>
            <p>
                เลือกได้ทีละเรื่องว่าจะให้ส่งอะไรเข้า <b>Telegram</b> / <b>LINE</b> บ้าง<br />
                ปิดเรื่องไหน เรื่องนั้นก็เงียบทันที ส่วนที่เหลือยังทำงานเหมือนเดิม
            </p>
        </div>

        <asp:Literal ID="litMsg" runat="server" />

        <!-- ── ช่องทาง ── -->
        <div class="nt-card">
            <h3>ช่องทางและปลายทาง</h3>
            <div class="sub">สวิตช์ใหญ่ของแต่ละช่องทาง — ปิดที่นี่แล้วไม่ส่งเลย ไม่ว่าเหตุการณ์ข้างล่างจะเปิดอยู่</div>

            <div class="nt-row">
                <div class="nt-lbl"><b>Telegram</b>
                    <small>สถานะ Token: <asp:Literal ID="litTgToken" runat="server" /></small></div>
                <div class="nt-in">
                    <label class="nt-chk"><asp:CheckBox ID="chkTelegram" runat="server" /> เปิดใช้งาน Telegram</label>
                </div>
            </div>
            <div class="nt-row">
                <div class="nt-lbl"><b>ปลายทาง Telegram (กลาง)</b>
                    <small>chat id ของกลุ่ม/คนที่จะรับ — ใส่หลายรายการคั่นด้วยจุลภาค</small></div>
                <div class="nt-in"><asp:TextBox ID="txtTgTarget" runat="server" /></div>
            </div>

            <div class="nt-row">
                <div class="nt-lbl"><b>LINE</b>
                    <small>สถานะ Token: <asp:Literal ID="litLineToken" runat="server" /></small></div>
                <div class="nt-in">
                    <label class="nt-chk"><asp:CheckBox ID="chkLine" runat="server" /> เปิดใช้งาน LINE</label>
                </div>
            </div>
            <div class="nt-row">
                <div class="nt-lbl"><b>ปลายทาง LINE (กลาง)</b>
                    <small>userId หรือ groupId — ใส่หลายรายการคั่นด้วยจุลภาค</small></div>
                <div class="nt-in"><asp:TextBox ID="txtLineTarget" runat="server" /></div>
            </div>

            <div class="nt-actions" style="margin-top:12px;">
                <asp:Button ID="btnTestTg" runat="server" CssClass="nt-btn ghost" Text="📨 ทดสอบส่ง Telegram"
                    OnClick="btnTestTg_Click" CausesValidation="false" />
                <asp:Button ID="btnTestLine" runat="server" CssClass="nt-btn ghost" Text="💬 ทดสอบส่ง LINE"
                    OnClick="btnTestLine_Click" CausesValidation="false" />
            </div>
        </div>

        <asp:Panel ID="pnlTest" runat="server" CssClass="nt-card" Visible="false">
            <h3>ผลการทดสอบ</h3>
            <div class="nt-pre"><asp:Literal ID="litTest" runat="server" /></div>
        </asp:Panel>

        <!-- ── ช่วงเวลาเงียบ ── -->
        <div class="nt-card">
            <h3>ช่วงเวลาเงียบ</h3>
            <div class="sub">ในช่วงนี้ระบบจะไม่ส่งแจ้งเตือน — เว้นแต่เรื่องด่วน (ถ้าเปิดข้อยกเว้นไว้). ปล่อยว่างทั้งคู่ = ไม่ใช้</div>
            <div class="nt-row">
                <div class="nt-lbl"><b>ตั้งแต่เวลา</b><small>รูปแบบ HH:mm เช่น 22:00</small></div>
                <div class="nt-in"><asp:TextBox ID="txtQuietFrom" runat="server" placeholder="22:00" /></div>
            </div>
            <div class="nt-row">
                <div class="nt-lbl"><b>ถึงเวลา</b><small>ข้ามเที่ยงคืนได้ เช่น 07:00</small></div>
                <div class="nt-in"><asp:TextBox ID="txtQuietTo" runat="server" placeholder="07:00" /></div>
            </div>
            <div class="nt-row">
                <div class="nt-lbl"><b>ยกเว้นเรื่องด่วน</b>
                    <small>เรื่องที่ติดป้าย "ด่วน" ด้านล่างจะยังส่งได้ในช่วงเงียบ</small></div>
                <div class="nt-in">
                    <label class="nt-chk"><asp:CheckBox ID="chkQuietUrgent" runat="server" /> ส่งเรื่องด่วนได้เสมอ</label>
                </div>
            </div>
        </div>

        <!-- ── เหตุการณ์ ── -->
        <div class="nt-card">
            <h3>ส่งอะไรบ้าง</h3>
            <div class="sub">
                ติ๊ก = ส่ง / ไม่ติ๊ก = ไม่ส่ง · ช่องข้างใต้คือปลายทางเฉพาะเรื่องนั้น
                (ปล่อยว่าง = ใช้ปลายทางกลางด้านบน) — เช่น ให้เรื่องบัญชีเข้ากลุ่มบัญชีโดยเฉพาะ
            </div>
            <div class="ev-hdr">
                <div>เหตุการณ์</div><div>Telegram</div><div>LINE</div>
            </div>
            <asp:PlaceHolder ID="phEvents" runat="server" />
        </div>

        <div class="nt-card">
            <div class="nt-actions">
                <asp:Button ID="btnSave" runat="server" CssClass="nt-btn" Text="💾 บันทึกการตั้งค่า" OnClick="btnSave_Click" />
                <asp:Button ID="btnAllTgOn" runat="server" CssClass="nt-btn ghost" Text="เปิด Telegram ทุกเรื่อง"
                    OnClick="btnAllTgOn_Click" CausesValidation="false" />
                <asp:Button ID="btnAllTgOff" runat="server" CssClass="nt-btn ghost" Text="ปิด Telegram ทุกเรื่อง"
                    OnClick="btnAllTgOff_Click" CausesValidation="false" />
            </div>
        </div>

        <!-- ── บันทึกล่าสุด ── -->
        <div class="nt-card">
            <h3>ที่ส่งไปล่าสุด</h3>
            <div class="sub">เก็บทุกครั้งที่ข้าม/ส่งไม่สำเร็จ ใช้ตรวจว่าทำไมไม่ได้รับ</div>
            <asp:GridView ID="gvLog" runat="server" AutoGenerateColumns="false" CssClass="nt-log"
                GridLines="None" EmptyDataText="ยังไม่มีบันทึก — แปลว่าส่งได้ปกติทุกครั้ง">
                <Columns>
                    <asp:TemplateField HeaderText="เวลา">
                        <ItemTemplate><%# Eval("LogDateTime", "{0:dd/MM/yy HH:mm}") %></ItemTemplate>
                    </asp:TemplateField>
                    <asp:BoundField DataField="LogDetail" HeaderText="รายละเอียด" />
                </Columns>
            </asp:GridView>
        </div>
    </div>
</asp:Content>
