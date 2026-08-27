<%@ Page Title="รับชำระเงินออนไลน์" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="PaymentGateway.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Settings.PaymentGatewaySettings" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .pg-wrap { max-width: 1080px; margin: 0 auto; padding: 12px 12px 60px; }
        .pg-head { background: linear-gradient(135deg,#1b7a4b,#0f5c37); color:#fff;
                   border-radius:14px; padding:20px 22px; margin-bottom:16px; }
        .pg-head h2 { margin:0 0 6px; font-size:21px; }
        .pg-head p { margin:0; opacity:.92; font-size:14px; line-height:1.65; }

        .pg-card { background:#fff; border-radius:14px; padding:18px 20px; margin-bottom:16px;
                   box-shadow:0 2px 10px rgba(0,0,0,.05); }
        .pg-card h3 { margin:0 0 4px; font-size:16.5px; color:#1b4332; }
        .pg-card .sub { color:#7b8a83; font-size:13px; margin-bottom:14px; }

        .pg-row { display:flex; gap:16px; padding:11px 0; border-bottom:1px solid #f0f3f1; align-items:flex-start; }
        .pg-row:last-child { border-bottom:0; }
        .pg-label { flex:0 0 270px; }
        .pg-label b { display:block; font-size:14.5px; color:#2c3e37; }
        .pg-label small { display:block; color:#8b978f; font-size:12.5px; line-height:1.6; margin-top:3px; }
        .pg-input { flex:1; min-width:0; }
        .pg-input input[type=text], .pg-input input[type=password], .pg-input select, .pg-input textarea {
            width:100%; padding:9px 11px; border:1px solid #dbe3de; border-radius:9px; font-size:14px;
            font-family:inherit;
        }
        .pg-input textarea { min-height:96px; font-family:Consolas,monospace; font-size:12.5px; }
        .pg-input input:focus, .pg-input select:focus, .pg-input textarea:focus {
            outline:0; border-color:#1b7a4b; box-shadow:0 0 0 3px rgba(27,122,75,.12);
        }
        .pg-chk { display:flex; align-items:center; gap:9px; font-size:14px; }
        .pg-chk input { width:18px; height:18px; accent-color:#1b7a4b; }

        .pg-btn { padding:11px 20px; border:0; border-radius:10px; background:#1b7a4b; color:#fff;
                  font-size:14.5px; font-weight:600; cursor:pointer; }
        .pg-btn:hover { background:#16653e; }
        .pg-btn.ghost { background:#fff; color:#46584f; border:1.5px solid #dbe3de; }
        .pg-actions { display:flex; gap:10px; flex-wrap:wrap; margin-top:6px; }

        .pg-alert { padding:12px 15px; border-radius:10px; margin-bottom:14px; font-size:14px; line-height:1.65; }
        .pg-alert.ok { background:#e8f6ee; color:#16653e; }
        .pg-alert.err { background:#fdecec; color:#a12626; }
        .pg-alert.warn { background:#fff6e5; color:#8a5a00; }

        .pg-url { display:flex; gap:8px; align-items:center; background:#f6f9f7; border-radius:9px;
                  padding:10px 12px; font-family:Consolas,monospace; font-size:12.8px; word-break:break-all; }

        .pg-pre { background:#1e2a24; color:#d7e6dd; border-radius:10px; padding:14px;
                  font-family:Consolas,monospace; font-size:12.3px; white-space:pre-wrap;
                  max-height:420px; overflow:auto; }

        .pg-grid { width:100%; border-collapse:collapse; font-size:13.4px; }
        .pg-grid th { background:#f2f6f4; text-align:left; padding:9px 10px; color:#46584f; font-weight:600; }
        .pg-grid td { padding:9px 10px; border-top:1px solid #f0f3f1; }
        .pill { display:inline-block; padding:2px 9px; border-radius:20px; font-size:12px; font-weight:600; }
        .pill.PAID { background:#e8f6ee; color:#16653e; }
        .pill.PENDING { background:#fff6e5; color:#8a5a00; }
        .pill.FAILED, .pill.EXPIRED, .pill.CANCELLED { background:#fdecec; color:#a12626; }
        .pill.INITIATED { background:#eef2f5; color:#4a5b66; }
        .pill.REFUNDED { background:#eef4fb; color:#1d4e79; }

        @media (max-width: 760px) {
            .pg-row { flex-direction:column; gap:7px; }
            .pg-label { flex:none; }
        }
    </style>

    <div class="pg-wrap">
        <div class="pg-head">
            <h2><i class="fas fa-credit-card"></i> รับชำระเงินออนไลน์</h2>
            <p>
                ให้ลูกค้าเลือกได้ว่าจะ <b>สแกน QR โอนแล้วแนบสลิป (แบบเดิม)</b> หรือ
                <b>จ่ายด้วยบัตรเครดิตผ่านเกตเวย์</b><br />
                ปิดสวิตช์เมื่อไหร่ ระบบก็กลับไปทำงานเหมือนเดิมทุกอย่างทันที
            </p>
        </div>

        <asp:Literal ID="litMsg" runat="server" />

        <!-- ── ที่อยู่ที่ต้องนำไปตั้งค่าฝั่งเกตเวย์ ── -->
        <div class="pg-card">
            <h3>ที่อยู่ที่ต้องนำไปใส่ในระบบของผู้ให้บริการ</h3>
            <div class="sub">คัดลอกไปวางในหน้าตั้งค่าของ Payso — ถ้าใส่ผิด เงินจะเข้าแต่ระบบเราจะไม่รู้</div>
            <div class="pg-row">
                <div class="pg-label"><b>Webhook / Callback URL</b>
                    <small>ที่อยู่ที่เกตเวย์ใช้แจ้งผลการจ่ายกลับมา</small></div>
                <div class="pg-input"><div class="pg-url"><asp:Literal ID="litWebhookUrl" runat="server" /></div></div>
            </div>
            <div class="pg-row">
                <div class="pg-label"><b>Return URL</b>
                    <small>ที่อยู่ที่ลูกค้าจะถูกพากลับมาหลังจ่ายเสร็จ</small></div>
                <div class="pg-input"><div class="pg-url"><asp:Literal ID="litReturnUrl" runat="server" /></div></div>
            </div>
            <div class="pg-row">
                <div class="pg-label"><b>หน้าชำระเงินของลูกค้า</b>
                    <small>ใช้สร้างลิงก์ส่งให้ลูกค้าจ่ายเอง</small></div>
                <div class="pg-input"><div class="pg-url"><asp:Literal ID="litPayUrl" runat="server" /></div></div>
            </div>
        </div>

        <!-- ── ค่าตั้งค่าทั้งหมด (วาดจากฐานข้อมูล) ── -->
        <asp:PlaceHolder ID="phSettings" runat="server" />

        <div class="pg-card">
            <div class="pg-actions">
                <asp:Button ID="btnSave" runat="server" CssClass="pg-btn" Text="💾 บันทึกการตั้งค่า" OnClick="btnSave_Click" />
                <asp:Button ID="btnTest" runat="server" CssClass="pg-btn ghost" Text="🔌 ทดสอบการเชื่อมต่อ"
                    OnClick="btnTest_Click" CausesValidation="false" />
                <asp:Button ID="btnReload" runat="server" CssClass="pg-btn ghost" Text="↻ โหลดค่าใหม่"
                    OnClick="btnReload_Click" CausesValidation="false" />
            </div>
        </div>

        <asp:Panel ID="pnlTest" runat="server" CssClass="pg-card" Visible="false">
            <h3>ผลการทดสอบ</h3>
            <div class="sub">ใช้ "คำตอบที่ได้" ด้านล่างปรับ <b>ตำแหน่งฟิลด์ในคำตอบ</b> ให้ตรงกับที่เกตเวย์ส่งมาจริง</div>
            <div class="pg-pre"><asp:Literal ID="litTest" runat="server" /></div>
        </asp:Panel>

        <!-- ── รายการชำระเงินล่าสุด ── -->
        <div class="pg-card">
            <h3>รายการชำระเงินล่าสุด</h3>
            <div class="sub">ทุกคำขอ-คำตอบถูกเก็บไว้ ตรวจย้อนหลังได้เสมอ</div>
            <asp:GridView ID="gvTxn" runat="server" AutoGenerateColumns="false" CssClass="pg-grid"
                GridLines="None" EmptyDataText="ยังไม่มีรายการ" DataKeyNames="ID"
                OnRowCommand="gvTxn_RowCommand">
                <Columns>
                    <asp:TemplateField HeaderText="เวลา">
                        <ItemTemplate><%# Eval("Created_Date", "{0:dd/MM/yy HH:mm}") %></ItemTemplate>
                    </asp:TemplateField>
                    <asp:BoundField DataField="Txn_Ref" HeaderText="อ้างอิง" />
                    <asp:TemplateField HeaderText="วิธี">
                        <ItemTemplate><%# MethodText(Eval("Method")) %></ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="รายการ">
                        <ItemTemplate><%# SourceText(Eval("Source_Type"), Eval("Source_ID")) %></ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="ยอด">
                        <ItemTemplate><%# AmountText(Eval("Amount"), Eval("Surcharge_Amount")) %></ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="สถานะ">
                        <ItemTemplate><%# StatusPill(Eval("Status")) %></ItemTemplate>
                    </asp:TemplateField>
                    <asp:BoundField DataField="Customer_Name" HeaderText="ลูกค้า" />
                    <asp:TemplateField HeaderText="">
                        <ItemTemplate>
                            <asp:LinkButton ID="lbCheck" runat="server" CommandName="CheckStatus"
                                CommandArgument='<%# Eval("ID") %>' Text="ตรวจสถานะ"
                                Visible='<%# ShowCheck(Eval("Status")) %>' CausesValidation="false" />
                        </ItemTemplate>
                    </asp:TemplateField>
                </Columns>
            </asp:GridView>
        </div>
    </div>
</asp:Content>
