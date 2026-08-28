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

        /* ── แถบขั้นตอนตั้งค่า ── */
        .pg-steps { display:flex; gap:8px; flex-wrap:wrap; }
        .pg-step { display:flex; align-items:center; gap:8px; padding:8px 14px; border-radius:24px;
                   font-size:13.5px; background:#f2f6f4; color:#5a6b62; }
        .pg-step.done { background:#e8f6ee; color:#16653e; font-weight:600; }
        .pg-step .n { width:22px; height:22px; border-radius:50%; display:inline-flex; align-items:center;
                      justify-content:center; background:#fff; font-weight:700; font-size:12px;
                      border:1.5px solid currentColor; flex:none; }
        .pg-steps-sum { margin-top:10px; font-size:13px; color:#7b8a83; }
        .pg-steps-sum.ok { color:#16653e; font-weight:600; }

        /* ── การ์ดที่ซ่อน/พับได้ ── */
        .pg-2col { display:grid; grid-template-columns:1fr 1fr; gap:0 26px; }
        .pg-methods .pg-chk { margin-top:2px; }
        .pg-mnote { display:block; color:#8b978f; font-size:12.3px; margin:1px 0 10px 27px; }
        .pg-toggle { cursor:pointer; -webkit-user-select:none; user-select:none; }
        .pg-caret { font-size:12px; color:#8b978f; display:inline-block; transition:transform .15s; }
        .pg-collapsed .pg-body, .pg-collapsed .sub { display:none; }
        .pg-collapsed .pg-caret { transform:rotate(-90deg); }
        .pg-collapsed h3 { margin-bottom:0; }
        .pg-dim { opacity:.45; }

        @media (max-width: 760px) {
            .pg-row { flex-direction:column; gap:7px; }
            .pg-label { flex:none; }
            .pg-2col { grid-template-columns:1fr; }
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

        <!-- ── ค่าตั้งค่าทั้งหมด (วาดจากฐานข้อมูล จัดกลุ่มเป็นขั้นตอน) ── -->
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

        <!-- ── ที่อยู่ที่ต้องนำไปตั้งค่าฝั่งเกตเวย์ ── -->
        <div class="pg-card">
            <h3>ที่อยู่ที่ต้องนำไปใส่ในระบบของผู้ให้บริการ</h3>
            <div class="sub">
                คัดลอกไปวางในระบบของเกตเวย์ที่เลือกใช้ —
                <span data-pg-provider="OMISE">Omise: Dashboard → Webhooks (ใส่ Webhook URL ช่อง Endpoint)</span><span
                    data-pg-provider="PAYSO">Payso: หน้า Merchant → ตั้งค่า Callback</span> ·
                ถ้าใส่ผิด เงินจะเข้าแต่ระบบเราจะไม่รู้
            </div>
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

        <asp:Panel ID="pnlTest" runat="server" CssClass="pg-card" Visible="false">
            <h3>ผลการทดสอบ</h3>
            <div class="sub">ใช้ "คำตอบที่ได้" ด้านล่างปรับ <b>ตำแหน่งฟิลด์ในคำตอบ</b> ให้ตรงกับที่เกตเวย์ส่งมาจริง</div>
            <div class="pg-pre"><asp:Literal ID="litTest" runat="server" /></div>
        </asp:Panel>

        <!-- ── คืนเงิน (เปิดจากปุ่มในตาราง) ── -->
        <asp:Panel ID="pnlRefund" runat="server" CssClass="pg-card" Visible="false"
            style="border-left:4px solid #a12626;">
            <h3>↩ คืนเงินลูกค้า</h3>
            <div class="sub">
                ใช้เมื่อ: จ่ายซ้ำ/ซ้อน · ยกเลิกการจอง-ออเดอร์ที่จ่ายผ่านเกตเวย์ · เก็บผิดยอด (คืนบางส่วน)<br />
                ⚠ การคืนเงิน<b>ไม่</b>ย้อนใบเสร็จ/ยอดการจองให้ — ต้องไปปรับเอกสารที่เกี่ยวข้องเองด้วย
            </div>
            <div class="pg-row">
                <div class="pg-label"><b>รายการ</b></div>
                <div class="pg-input"><asp:Literal ID="litRefundInfo" runat="server" /></div>
            </div>
            <div class="pg-row">
                <div class="pg-label"><b>ยอดที่จะคืน (บาท)</b>
                    <small>น้อยกว่ายอดเต็ม = คืนบางส่วน</small></div>
                <div class="pg-input">
                    <asp:TextBox ID="txtRefundAmount" runat="server" TextMode="Number" step="0.01"
                        style="max-width:180px;" />
                </div>
            </div>
            <div class="pg-row">
                <div class="pg-label"><b>เหตุผล</b></div>
                <div class="pg-input"><asp:TextBox ID="txtRefundReason" runat="server"
                    placeholder="เช่น ลูกค้าจ่ายซ้ำ / ยกเลิกการจอง #123" /></div>
            </div>
            <div class="pg-actions">
                <asp:Button ID="btnDoRefund" runat="server" Text="↩ ยืนยันคืนเงิน"
                    OnClick="btnDoRefund_Click" UseSubmitBehavior="false"
                    OnClientClick="if(!confirm('ยืนยันคืนเงินตามยอดที่กรอก? เงินจะถูกส่งกลับช่องทางเดิมของลูกค้า'))return false;this.disabled=true;"
                    style="padding:11px 20px;border:0;border-radius:10px;background:#a12626;color:#fff;font-weight:600;cursor:pointer;" />
                <asp:Button ID="btnCancelRefund" runat="server" CssClass="pg-btn ghost" Text="ยกเลิก"
                    OnClick="btnCancelRefund_Click" CausesValidation="false" />
            </div>
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
                            <asp:LinkButton ID="lbRefund" runat="server" CommandName="StartRefund"
                                CommandArgument='<%# Eval("ID") %>' Text="↩ คืนเงิน"
                                Visible='<%# ShowRefund(Eval("Status"), Eval("Provider")) %>'
                                CausesValidation="false" style="color:#a12626;" />
                            <%-- ลิงก์ที่เคยส่งให้ลูกค้า — เดิมหาไม่เจออีกเลยหลังปิดหน้าจอ --%>
                            <asp:Literal ID="litLink" runat="server"
                                Text='<%# LinkCell(Eval("Payment_Url"), Eval("Source_Type"), Eval("Source_ID"), Eval("Customer_Phone"), Eval("Status")) %>' />
                        </ItemTemplate>
                    </asp:TemplateField>
                </Columns>
            </asp:GridView>
        </div>
    </div>

    <script>
        // คัดลอกลิงก์จากตารางรายการ
        function pgCopy(url, el) {
            if (navigator.clipboard) { try { navigator.clipboard.writeText(url); } catch (e) { } }
            else {
                var t = document.createElement('textarea');
                t.value = url; document.body.appendChild(t); t.select();
                try { document.execCommand('copy'); } catch (e) { }
                document.body.removeChild(t);
            }
            var old = el.textContent; el.textContent = '✓ คัดลอกแล้ว';
            setTimeout(function () { el.textContent = old; }, 1600);
        }

        // ── แสดงเฉพาะสิ่งที่เกี่ยวข้อง — เลือกเกตเวย์ไหนเห็นแค่ของเจ้านั้น ──
        // ทุกอย่างยังอยู่ในฟอร์มครบ (แค่ซ่อนด้วย CSS) การบันทึกจึงทำงานเหมือนเดิมทุกประการ
        (function () {
            function $one(sel) { return document.querySelector(sel); }
            function $all(sel) { return document.querySelectorAll(sel); }

            var provider = $one("select[id$='cfg_Payment_Provider']");
            var master = $one("input[id$='cfg_Payment_Enabled']");

            function apply() {
                var prov = provider ? (provider.value || 'OMISE').toUpperCase() : 'OMISE';

                // การ์ด/ข้อความของเกตเวย์: โชว์เฉพาะเจ้าที่เลือก
                var tagged = $all('[data-pg-provider]');
                for (var i = 0; i < tagged.length; i++) {
                    var el = tagged[i];
                    el.style.display = el.getAttribute('data-pg-provider') === prov ? '' : 'none';
                }

                // สวิตช์ใหญ่ปิด: หรี่การ์ดถัด ๆ ไปให้เห็นว่า "ยังไม่มีผล" (แก้ค่าได้ตามปกติ)
                var off = master && !master.checked;
                var cards = $all(".pg-card[id^='pgcat']");
                for (var c = 0; c < cards.length; c++) {
                    if (cards[c].id === 'pgcat1') continue;
                    if (off) cards[c].classList.add('pg-dim');
                    else cards[c].classList.remove('pg-dim');
                }

                // การ์ดที่มีติ๊กหลัก (เช่น วงเงินประกัน): ปิดอยู่ให้เหลือแค่แถวสวิตช์
                var mastered = $all('.pg-card[data-pg-master]');
                for (var m = 0; m < mastered.length; m++) {
                    var card = mastered[m];
                    var key = card.getAttribute('data-pg-master');
                    var box = card.querySelector("input[id$='" + key + "']");
                    var rows = card.querySelectorAll('.pg-row');
                    for (var r = 0; r < rows.length; r++) {
                        var isMasterRow = !!rows[r].querySelector("input[id$='" + key + "']");
                        rows[r].style.display = (isMasterRow || !box || box.checked) ? '' : 'none';
                    }
                }
            }

            if (provider) provider.addEventListener('change', apply);
            if (master) master.addEventListener('change', apply);
            var masterBoxes = $all('.pg-card[data-pg-master] input[type=checkbox]');
            for (var b = 0; b < masterBoxes.length; b++) masterBoxes[b].addEventListener('change', apply);

            // การ์ดขั้นสูง: หัวข้อกดพับ/กางได้
            var toggles = $all('.pg-card[data-pg-adv] h3.pg-toggle');
            for (var t = 0; t < toggles.length; t++) {
                toggles[t].addEventListener('click', function () {
                    this.parentNode.classList.toggle('pg-collapsed');
                });
            }

            apply();
        })();
    </script>
</asp:Content>
