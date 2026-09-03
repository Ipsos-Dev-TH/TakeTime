<%@ Page Title="จุดรับเงินออนไลน์" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Charge.aspx.cs" Inherits="Take_Time_BangPhra.Payment.Charge" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .qc-wrap { max-width: 880px; margin: 0 auto; padding: 12px 12px 60px; }
        .qc-head { background: linear-gradient(135deg,#1b7a4b,#0f5c37); color:#fff;
                   border-radius:14px; padding:18px 20px; margin-bottom:14px; }
        .qc-head h2 { margin:0 0 4px; font-size:20px; }
        .qc-head p { margin:0; opacity:.92; font-size:13.5px; }
        .qc-card { background:#fff; border-radius:14px; padding:18px; margin-bottom:14px;
                   box-shadow:0 2px 10px rgba(0,0,0,.05); }
        .qc-card h3 { margin:0 0 4px; font-size:16px; color:#1b4332; }
        .qc-card .sub { color:#7b8a83; font-size:13px; margin-bottom:12px; line-height:1.6; }
        .qc-row { display:flex; gap:10px; flex-wrap:wrap; align-items:flex-end; }
        .qc-f { flex:1; min-width:150px; }
        .qc-f label { display:block; font-weight:600; font-size:13px; margin-bottom:5px; }
        .qc-f input { width:100%; padding:10px 12px; border:1.5px solid #dbe3de; border-radius:9px;
                      font-size:16px; }
        .qc-btn { padding:11px 20px; border:0; border-radius:10px; background:#1b7a4b; color:#fff;
                  font-size:14.5px; font-weight:600; cursor:pointer; min-height:44px; }
        .qc-btn.ghost { background:#fff; color:#46584f; border:1.5px solid #dfe6e2; }
        .qc-alert { padding:11px 14px; border-radius:10px; font-size:14px; margin-bottom:12px; line-height:1.6; }
        .qc-alert.err { background:#fdecec; color:#a12626; }
        .qc-alert.ok { background:#e8f6ee; color:#16653e; }
        .qc-alert.info { background:#eef4fb; color:#1d4e79; }

        .qc-result { display:flex; gap:18px; flex-wrap:wrap; align-items:flex-start; }
        .qc-qr { text-align:center; }
        .qc-qr img { max-width:230px; border-radius:10px; border:1px solid #e3e9e5; }
        .qc-qr .cap { font-size:12.5px; color:#7b8a83; margin-top:6px; }
        .qc-link { flex:1; min-width:240px; }
        .qc-link input { width:100%; padding:9px 11px; border:1px solid #dbe3de; border-radius:8px;
                         font-family:monospace; font-size:12.5px; }
        .qc-status { font-size:20px; font-weight:700; padding:14px; border-radius:12px;
                     text-align:center; margin-top:12px; }
        .qc-status.wait { background:#fff6e5; color:#8a5a00; }
        .qc-status.paid { background:#e8f6ee; color:#16653e; }
        .qc-status.bad { background:#fdecec; color:#a12626; }
    </style>

    <div class="qc-wrap">
        <div class="qc-head">
            <h2>💳 จุดรับเงินออนไลน์ (หน้าร้าน)</h2>
            <p>สร้าง QR/ลิงก์ให้ลูกค้าจ่ายตรงนี้ — เงินเข้าแล้วค่อยไปบันทึกการขาย/ออกใบเสร็จ โดยเลือกแหล่งเงิน "<asp:Literal ID="litPaidHowName" runat="server" />"</p>
        </div>

        <asp:Literal ID="litMsg" runat="server" />

        <!-- ── เก็บเงินทั่วไป / หน้าร้าน ── -->
        <div class="qc-card">
            <h3>เก็บเงิน</h3>
            <div class="sub">
                ใช้กับการขายหน้าร้านหรือยอดอื่น ๆ: ใส่ยอด → ลูกค้าสแกน QR พร้อมเพย์ (ตัดยอดอัตโนมัติ)
                หรือเปิดลิงก์กรอกบัตร → หน้าจอนี้ขึ้น ✅ เอง เมื่อเงินเข้า
            </div>
            <div class="qc-row">
                <div class="qc-f" style="max-width:180px">
                    <label>จำนวนเงิน (บาท)</label>
                    <asp:TextBox ID="txtAmount" runat="server" TextMode="Number" step="0.01" />
                </div>
                <div class="qc-f">
                    <label>รายการ (แสดงให้ลูกค้าเห็น)</label>
                    <asp:TextBox ID="txtNote" runat="server" placeholder="เช่น ค่าสินค้าหน้าร้าน" />
                </div>
                <asp:Button ID="btnCharge" runat="server" CssClass="qc-btn" Text="สร้าง QR / ลิงก์"
                    OnClick="btnCharge_Click" UseSubmitBehavior="false"
                    OnClientClick="this.disabled=true;this.value='กำลังสร้าง…';" />
            </div>
            <asp:Panel ID="pnlChargeResult" runat="server" Visible="false" style="margin-top:14px;">
                <div class="qc-result">
                    <div class="qc-qr">
                        <asp:Literal ID="litQr" runat="server" />
                        <div class="cap"><asp:Literal ID="litQrCap" runat="server" /></div>
                    </div>
                    <div class="qc-link">
                        <label style="font-weight:600;font-size:13px;">ลิงก์ให้ลูกค้ากรอกบัตรเอง (ส่งทางแชทได้)</label>
                        <div style="display:flex;gap:6px;align-items:center;">
                            <asp:TextBox ID="txtPayLink" runat="server" ReadOnly="true" />
                            <button type="button" onclick="qcCopy('<%= txtPayLink.ClientID %>',this)"
                                style="flex:none;padding:8px 14px;border:0;border-radius:8px;background:#1b7a4b;color:#fff;font-weight:600;cursor:pointer;white-space:nowrap;">คัดลอก</button>
                        </div>
                        <div id="qcLinkQr" style="margin-top:10px;"></div>
                        <div class="cap" style="font-size:12.5px;color:#7b8a83;">หรือให้ลูกค้าสแกน QR นี้เพื่อเปิดลิงก์</div>
                    </div>
                </div>
                <div id="qcStatus" class="qc-status wait">⏳ รอลูกค้าชำระเงิน…</div>
                <input type="hidden" id="qcRef" value="<%= CurrentTxnRefJs %>" />
            </asp:Panel>
        </div>

        <!-- ── วางวงเงินประกันความเสียหาย ── -->
        <asp:Panel ID="pnlHoldSection" runat="server" CssClass="qc-card">
            <h3>🛡 รับเงินประกันความเสียหาย (ตอนเช็คอิน)</h3>
            <div class="sub">
                เลือกได้สองแบบ — <b>กันวงเงินบัตร</b>: ส่งลิงก์ให้ลูกค้ากรอกเอง เงินไม่เข้าไม่ออก
                (วงเงินอยู่ได้ 7 วัน หมดอายุระบบสร้างลิงก์ใหม่ให้เอง) /
                <b>เงินสด</b>: บันทึกรับเข้าระบบทันที ·
                ทั้งสองแบบไปจบที่หน้าเช็คเอาท์: คืนทั้งหมด หรือหักค่าเสียหายแล้วคืนส่วนที่เหลือ
            </div>
            <div class="qc-row">
                <div class="qc-f" style="max-width:150px">
                    <label>เลขที่การจอง</label>
                    <asp:TextBox ID="txtHoldRes" runat="server" TextMode="Number" />
                </div>
                <div class="qc-f" style="max-width:170px">
                    <label>วงเงินประกัน (บาท)</label>
                    <asp:TextBox ID="txtHoldAmount" runat="server" TextMode="Number" step="0.01"
                        placeholder="ว่าง = ตามห้องพัก" />
                </div>
                <div class="qc-f" style="max-width:210px">
                    <label>วิธีรับประกัน</label>
                    <asp:DropDownList ID="ddlHoldMethod" runat="server"
                        style="width:100%;padding:10px 12px;border:1.5px solid #dbe3de;border-radius:9px;font-size:15px;">
                        <asp:ListItem Value="CARD">💳 กันวงเงินบัตร (ส่งลิงก์)</asp:ListItem>
                        <asp:ListItem Value="CASH">💵 รับเงินสด (บันทึกทันที)</asp:ListItem>
                    </asp:DropDownList>
                </div>
                <asp:Button ID="btnHold" runat="server" CssClass="qc-btn" Text="รับประกัน / สร้างลิงก์"
                    OnClick="btnHold_Click" UseSubmitBehavior="false"
                    OnClientClick="this.disabled=true;this.value='กำลังดำเนินการ…';" />
            </div>
            <div class="sub" style="margin-top:6px;">
                ไม่กรอกยอด = ใช้วงเงินที่ตั้งไว้รายห้องพัก (Accommodation → Security_Deposit_Amount)
                · เงินสด: บันทึกรับเข้าระบบทันที เช็คเอาท์ค่อยคืน/หัก — ไม่ต้องเบิกเงินมารอคืนอีก
            </div>
            <asp:Panel ID="pnlHoldResult" runat="server" Visible="false" style="margin-top:14px;">
                <div class="qc-result">
                    <div class="qc-link">
                        <label style="font-weight:600;font-size:13px;">ลิงก์ให้ลูกค้ากรอกบัตร (กันวงเงิน)</label>
                        <div style="display:flex;gap:6px;align-items:center;">
                            <asp:TextBox ID="txtHoldLink" runat="server" ReadOnly="true" />
                            <button type="button" onclick="qcCopy('<%= txtHoldLink.ClientID %>',this)"
                                style="flex:none;padding:8px 14px;border:0;border-radius:8px;background:#1b7a4b;color:#fff;font-weight:600;cursor:pointer;white-space:nowrap;">คัดลอก</button>
                        </div>
                        <div id="qcHoldQr" style="margin-top:10px;"></div>
                    </div>
                </div>
                <div id="qcHoldStatus" class="qc-status wait">⏳ รอลูกค้ากรอกบัตร…</div>
                <input type="hidden" id="qcHoldRef" value="<%= CurrentHoldRefJs %>" />
            </asp:Panel>
        </asp:Panel>

        <!-- ── รายการล่าสุดของวันนี้ ── -->
        <div class="qc-card">
            <h3>รายการวันนี้</h3>
            <div class="sub">เงินที่รับผ่านจุดนี้วันนี้ — ใช้ตรวจกับการบันทึกขาย/ใบเสร็จ</div>
            <div style="overflow-x:auto;">
                <asp:GridView ID="gvToday" runat="server" AutoGenerateColumns="false" GridLines="None"
                    CssClass="rt-table" EmptyDataText="ยังไม่มีรายการวันนี้"
                    style="width:100%;font-size:13.5px;">
                    <Columns>
                        <asp:TemplateField HeaderText="เวลา">
                            <ItemTemplate><%# Eval("Created_Date", "{0:HH:mm}") %></ItemTemplate>
                        </asp:TemplateField>
                        <asp:BoundField DataField="Txn_Ref" HeaderText="อ้างอิง" />
                        <asp:TemplateField HeaderText="ยอด">
                            <ItemTemplate><%# string.Format("{0:N2}", Eval("Amount")) %></ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="สถานะ">
                            <ItemTemplate><%# StatusThai(Eval("Status")) %></ItemTemplate>
                        </asp:TemplateField>
                        <asp:BoundField DataField="Description" HeaderText="รายการ" />
                    </Columns>
                </asp:GridView>
            </div>
        </div>
    </div>

    <script src="https://cdn.jsdelivr.net/npm/qrcodejs@1.0.0/qrcode.min.js"></script>
    <script>
        (function () {
            function drawQr(elId, text) {
                var el = document.getElementById(elId);
                if (!el || !text || typeof QRCode === 'undefined') return;
                try { new QRCode(el, { text: text, width: 190, height: 190 }); } catch (e) { }
            }
            function poll(refElId, statusElId, isHold) {
                var refEl = document.getElementById(refElId);
                var stEl = document.getElementById(statusElId);
                if (!refEl || !refEl.value || !stEl) return;
                var url = '<%= ResolveUrl("~/API/PaymentStatus.ashx") %>?' +
                          (isHold ? 'hold=' : 'ref=') + encodeURIComponent(refEl.value);
                var timer = setInterval(function () {
                    fetch(url).then(function (r) { return r.json(); }).then(function (d) {
                        if (!d) return;
                        if (d.status === 'PAID' || d.status === 'HELD') {
                            stEl.className = 'qc-status paid';
                            stEl.textContent = '✅ ' + d.thai;
                            clearInterval(timer);
                        } else if (d.status === 'FAILED' || d.status === 'EXPIRED' || d.status === 'CANCELLED') {
                            stEl.className = 'qc-status bad';
                            stEl.textContent = '❌ ' + d.thai;
                            clearInterval(timer);
                        }
                    }).catch(function () { });
                }, 4000);
            }

            var payLink = document.getElementById('<%= txtPayLink.ClientID %>');
            if (payLink && payLink.value) drawQr('qcLinkQr', payLink.value);
            var holdLink = document.getElementById('<%= txtHoldLink.ClientID %>');
            if (holdLink && holdLink.value) drawQr('qcHoldQr', holdLink.value);

            poll('qcRef', 'qcStatus', false);
            poll('qcHoldRef', 'qcHoldStatus', true);
        })();

        // คัดลอกลิงก์ — เดิมต้องลากเมาส์เลือกเองทั้งเส้น
        function qcCopy(id, btn) {
            var el = document.getElementById(id);
            if (!el) return;
            el.select(); el.setSelectionRange(0, 99999);
            try { document.execCommand('copy'); } catch (e) { }
            if (navigator.clipboard) { try { navigator.clipboard.writeText(el.value); } catch (e) { } }
            var old = btn.textContent; btn.textContent = '✓ แล้ว';
            setTimeout(function () { btn.textContent = old; }, 1600);
        }
    </script>
</asp:Content>
