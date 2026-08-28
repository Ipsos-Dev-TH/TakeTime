<%@ Page Title="ทดสอบเกตเวย์ (Sandbox)" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="GatewayTest.aspx.cs" Inherits="Take_Time_BangPhra.Payment.GatewayTest" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .gt-wrap { max-width: 980px; margin: 0 auto; padding: 12px 12px 60px; }
        .gt-head { background: linear-gradient(135deg,#4527a0,#283593); color:#fff;
                   border-radius:14px; padding:18px 20px; margin-bottom:14px; }
        .gt-head h2 { margin:0 0 4px; font-size:20px; }
        .gt-head p { margin:0; opacity:.92; font-size:13.5px; line-height:1.6; }
        .gt-card { background:#fff; border-radius:14px; padding:18px; margin-bottom:14px;
                   box-shadow:0 2px 10px rgba(0,0,0,.05); }
        .gt-card h3 { margin:0 0 4px; font-size:16px; color:#283593; }
        .gt-card .sub { color:#7b8a83; font-size:13px; margin-bottom:12px; line-height:1.65; }
        .gt-btn { padding:10px 18px; border:0; border-radius:10px; background:#3949ab; color:#fff;
                  font-size:14px; font-weight:600; cursor:pointer; min-height:42px; margin:3px 4px 3px 0; }
        .gt-btn.ghost { background:#fff; color:#3949ab; border:1.5px solid #c5cae9; }
        .gt-btn.danger { background:#a12626; }
        .gt-alert { padding:12px 15px; border-radius:10px; margin-bottom:13px; font-size:14px; line-height:1.65; }
        .gt-alert.ok { background:#e8f6ee; color:#16653e; }
        .gt-alert.err { background:#fdecec; color:#a12626; }
        .gt-alert.warn { background:#fff6e5; color:#8a5a00; }
        .gt-mode { display:inline-block; padding:4px 12px; border-radius:20px; font-weight:700; font-size:13px; }
        .gt-mode.test { background:#e8f6ee; color:#16653e; }
        .gt-mode.live { background:#fdecec; color:#a12626; }
        .gt-pre { background:#1a1f36; color:#d6dcf5; border-radius:10px; padding:13px;
                  font-family:Consolas,monospace; font-size:12px; white-space:pre-wrap;
                  max-height:380px; overflow:auto; }
        .gt-grid { width:100%; border-collapse:collapse; font-size:13.2px; }
        .gt-grid th { background:#eef0fa; text-align:left; padding:8px 10px; color:#3a4160; }
        .gt-grid td { padding:8px 10px; border-top:1px solid #f0f1f7; vertical-align:top; }
        .gt-grid a { margin-right:8px; }
        .gt-qr img { max-width:200px; border-radius:10px; border:1px solid #e0e3f0; }
        .gt-link input { width:100%; padding:8px 10px; border:1px solid #dbe0ee; border-radius:8px;
                         font-family:monospace; font-size:12px; }
        .gt-cards-hint { background:#eef4fb; color:#1d4e79; border-radius:10px; padding:11px 13px;
                         font-size:13px; line-height:1.7; font-family:Consolas,monospace; }
    </style>

    <div class="gt-wrap">
        <div class="gt-head">
            <h2>🧪 ทดสอบเกตเวย์ชำระเงิน (Sandbox)</h2>
            <p>ยิงรายการจริงไปที่เกตเวย์ด้วยคีย์ทดสอบ — ดูคำตอบดิบ ทดลองจ่าย/กันวงเงิน/ตัด/คืน/คืนเงิน ครบทุกปุ่มก่อนใช้คีย์จริง</p>
        </div>

        <asp:Literal ID="litMsg" runat="server" />

        <div class="gt-card">
            <h3>สถานะ</h3>
            <div style="display:flex;gap:14px;flex-wrap:wrap;align-items:center;">
                <div>เกตเวย์: <b><asp:Literal ID="litProvider" runat="server" /></b></div>
                <asp:Literal ID="litMode" runat="server" />
            </div>
            <asp:Panel ID="pnlLiveGuard" runat="server" Visible="false" style="margin-top:12px;">
                <div class="gt-alert err">
                    ⚠ <b>กำลังใช้คีย์จริง (LIVE)</b> — ทุกรายการในหน้านี้จะตัดเงินจริง!
                    ติ๊กยืนยันก่อนถึงจะกดปุ่มได้
                </div>
                <label style="display:flex;align-items:center;gap:8px;font-size:14px;">
                    <asp:CheckBox ID="chkLiveOk" runat="server" AutoPostBack="true" OnCheckedChanged="chkLiveOk_Changed" />
                    เข้าใจแล้วว่าเงินจะถูกตัดจริง — เปิดปุ่มทดสอบ
                </label>
            </asp:Panel>
            <div class="gt-cards-hint" style="margin-top:12px;">
                บัตรทดสอบ Omise (โหมด test):<br />
                4242 4242 4242 4242 · หมดอายุอนาคตใด ๆ · CVV 123  → สำเร็จ<br />
                4111 1111 1111 1140 → ถูกปฏิเสธ (insufficient_fund) — ไว้ทดสอบเคสบัตรไม่ผ่าน
            </div>
        </div>

        <div class="gt-card">
            <h3>สร้างรายการทดสอบ</h3>
            <div class="sub">ยอดเล็ก ๆ ก็พอ — ทุกรายการติดป้าย [ทดสอบ] และแสดงในตารางข้างล่างพร้อมปุ่มจัดการ</div>
            <div style="display:flex;gap:10px;flex-wrap:wrap;align-items:flex-end;">
                <div>
                    <label style="display:block;font-weight:600;font-size:13px;margin-bottom:4px;">ยอด (บาท)</label>
                    <asp:TextBox ID="txtAmount" runat="server" TextMode="Number" step="0.01" Text="20"
                        style="width:120px;padding:9px 11px;border:1.5px solid #dbe0ee;border-radius:9px;font-size:15px;" />
                </div>
                <asp:Button ID="btnTestQr" runat="server" CssClass="gt-btn" Text="📱 QR พร้อมเพย์"
                    OnClick="btnTestQr_Click" UseSubmitBehavior="false" />
                <asp:Button ID="btnTestCard" runat="server" CssClass="gt-btn" Text="💳 ลิงก์กรอกบัตร"
                    OnClick="btnTestCard_Click" UseSubmitBehavior="false" />
                <asp:Button ID="btnTestHold" runat="server" CssClass="gt-btn ghost" Text="🛡 กันวงเงิน (ลิงก์บัตร)"
                    OnClick="btnTestHold_Click" UseSubmitBehavior="false" />
                <asp:Button ID="btnConn" runat="server" CssClass="gt-btn ghost" Text="🔌 ทดสอบเชื่อมต่อ"
                    OnClick="btnConn_Click" UseSubmitBehavior="false" />
            </div>

            <asp:Panel ID="pnlResult" runat="server" Visible="false" style="margin-top:14px;display:flex;gap:18px;flex-wrap:wrap;">
                <div class="gt-qr"><asp:Literal ID="litQr" runat="server" /></div>
                <div class="gt-link" style="flex:1;min-width:250px;">
                    <label style="font-weight:600;font-size:13px;">ลิงก์ (เปิดในมือถือ/ส่งให้ตัวเองทดสอบ)</label>
                    <asp:TextBox ID="txtLink" runat="server" ReadOnly="true" />
                </div>
            </asp:Panel>
        </div>

        <asp:Panel ID="pnlRaw" runat="server" CssClass="gt-card" Visible="false">
            <h3>คำตอบดิบจากเกตเวย์</h3>
            <div class="gt-pre"><asp:Literal ID="litRaw" runat="server" /></div>
        </asp:Panel>

        <div class="gt-card">
            <h3>รายการทดสอบวันนี้</h3>
            <div class="sub">กดปุ่มในแถวเพื่อไล่ครบวงจร: ตรวจสถานะ → คืนเงิน / (วงเงิน: ตัดครึ่ง → ดูส่วนที่เหลือคืน / คืนทั้งหมด)</div>
            <div style="overflow-x:auto;">
                <asp:GridView ID="gvTest" runat="server" AutoGenerateColumns="false" GridLines="None"
                    CssClass="gt-grid" EmptyDataText="ยังไม่มีรายการทดสอบวันนี้" DataKeyNames="ID"
                    OnRowCommand="gvTest_RowCommand">
                    <Columns>
                        <asp:TemplateField HeaderText="เวลา">
                            <ItemTemplate><%# Eval("T", "{0:HH:mm}") %></ItemTemplate>
                        </asp:TemplateField>
                        <asp:BoundField DataField="Kind" HeaderText="ชนิด" />
                        <asp:BoundField DataField="Ref" HeaderText="อ้างอิง" />
                        <asp:TemplateField HeaderText="ยอด">
                            <ItemTemplate><%# string.Format("{0:N2}", Eval("Amount")) %></ItemTemplate>
                        </asp:TemplateField>
                        <asp:BoundField DataField="StatusThai" HeaderText="สถานะ" />
                        <asp:TemplateField HeaderText="จัดการ">
                            <ItemTemplate>
                                <asp:LinkButton runat="server" CommandName="Check" CommandArgument='<%# Eval("Key") %>'
                                    Text="ตรวจสถานะ" CausesValidation="false" />
                                <asp:LinkButton runat="server" CommandName="Raw" CommandArgument='<%# Eval("Key") %>'
                                    Text="ดูคำตอบดิบ" CausesValidation="false" />
                                <asp:LinkButton runat="server" CommandName="Refund" CommandArgument='<%# Eval("Key") %>'
                                    Text="↩ คืนเงิน" Visible='<%# (bool)Eval("CanRefund") %>' CausesValidation="false"
                                    OnClientClick="return confirm('คืนเงินรายการทดสอบนี้ทั้งยอด?');" style="color:#a12626;" />
                                <asp:LinkButton runat="server" CommandName="CapHalf" CommandArgument='<%# Eval("Key") %>'
                                    Text="💥 ตัดครึ่ง" Visible='<%# (bool)Eval("CanHoldOps") %>' CausesValidation="false"
                                    OnClientClick="return confirm('ตัดค่าเสียหายครึ่งหนึ่งของวงเงินทดสอบ?');" />
                                <asp:LinkButton runat="server" CommandName="Release" CommandArgument='<%# Eval("Key") %>'
                                    Text="✅ คืนวงเงิน" Visible='<%# (bool)Eval("CanHoldOps") %>' CausesValidation="false"
                                    OnClientClick="return confirm('คืนวงเงินทดสอบทั้งหมด?');" />
                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                </asp:GridView>
            </div>
        </div>
    </div>
</asp:Content>
