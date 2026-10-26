<%@ Page Title="ชำระเงินเพิ่ม" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="MakePayment.aspx.cs" Inherits="Take_Time_BangPhra.Payment.MakePayment" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .payment-container {
            max-width: 1200px;
            margin: 20px auto;
            padding: 20px;
        }

        .payment-card {
            background: white;
            border-radius: 8px;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
            padding: 25px;
            margin-bottom: 20px;
        }

        .card-header {
            font-size: 20px;
            font-weight: bold;
            color: #2c3e50;
            margin-bottom: 20px;
            padding-bottom: 10px;
            border-bottom: 2px solid #3498db;
        }

        .info-row {
            display: flex;
            justify-content: space-between;
            padding: 10px 0;
            border-bottom: 1px solid #ecf0f1;
        }

        .info-label {
            font-weight: bold;
            color: #7f8c8d;
        }

        .info-value {
            color: #2c3e50;
        }

        .amount-highlight {
            font-size: 24px;
            font-weight: bold;
            color: #e74c3c;
        }

        .amount-paid {
            color: #27ae60;
        }

        .form-group {
            margin-bottom: 20px;
        }

        .form-label {
            display: block;
            font-weight: bold;
            margin-bottom: 8px;
            color: #2c3e50;
        }

        .form-control {
            width: 100%;
            padding: 10px;
            border: 1px solid #ddd;
            border-radius: 4px;
            font-size: 14px;
        }

        .form-control:focus {
            border-color: #3498db;
            outline: none;
            box-shadow: 0 0 0 3px rgba(52, 152, 219, 0.1);
        }

        .btn-primary {
            background-color: #3498db;
            color: white;
            padding: 12px 30px;
            border: none;
            border-radius: 4px;
            font-size: 16px;
            font-weight: bold;
            cursor: pointer;
            transition: background-color 0.3s;
        }

        .btn-primary:hover {
            background-color: #2980b9;
        }

        .btn-secondary {
            background-color: #95a5a6;
            color: white;
            padding: 12px 30px;
            border: none;
            border-radius: 4px;
            font-size: 16px;
            cursor: pointer;
            text-decoration: none;
            display: inline-block;
            margin-left: 10px;
        }

        .btn-secondary:hover {
            background-color: #7f8c8d;
        }

        .alert {
            padding: 15px;
            border-radius: 4px;
            margin-bottom: 20px;
        }

        .alert-success {
            background-color: #d4edda;
            border: 1px solid #c3e6cb;
            color: #155724;
        }

        .alert-danger {
            background-color: #f8d7da;
            border: 1px solid #f5c6cb;
            color: #721c24;
        }

        .alert-warning {
            background-color: #fff3cd;
            border: 1px solid #ffeaa7;
            color: #856404;
        }

        .payment-history-table {
            width: 100%;
            border-collapse: collapse;
            margin-top: 10px;
        }

        .payment-history-table th {
            background-color: #34495e;
            color: white;
            padding: 12px;
            text-align: left;
            font-weight: bold;
        }

        .payment-history-table td {
            padding: 10px;
            border-bottom: 1px solid #ecf0f1;
        }

        .payment-history-table tr:hover {
            background-color: #f8f9fa;
        }

        .badge {
            padding: 4px 8px;
            border-radius: 3px;
            font-size: 12px;
            font-weight: bold;
        }

        .badge-success {
            background-color: #27ae60;
            color: white;
        }

        .badge-warning {
            background-color: #f39c12;
            color: white;
        }

        .badge-info {
            background-color: #3498db;
            color: white;
        }
    </style>

    <div class="payment-container">
        <h2 style="color: #2c3e50; margin-bottom: 30px;">
            <i class="fa fa-credit-card"></i> ชำระเงินเพิ่ม
        </h2>

        <!-- Alert Messages -->
        <asp:Panel ID="pnlSuccess" runat="server" CssClass="alert alert-success" Visible="false">
            <asp:Label ID="lblSuccess" runat="server"></asp:Label>
        </asp:Panel>

        <asp:Panel ID="pnlError" runat="server" CssClass="alert alert-danger" Visible="false">
            <asp:Label ID="lblError" runat="server"></asp:Label>
        </asp:Panel>

        <!-- Reservation Information -->
        <div class="payment-card">
            <div class="card-header">
                <i class="fa fa-info-circle"></i> ข้อมูลการจอง
            </div>

            <div class="info-row">
                <span class="info-label">รหัสการจอง:</span>
                <span class="info-value"><asp:Label ID="lblReservationID" runat="server"></asp:Label></span>
            </div>

            <div class="info-row">
                <span class="info-label">ชื่อลูกค้า:</span>
                <span class="info-value"><asp:Label ID="lblCustomerName" runat="server"></asp:Label></span>
            </div>

            <div class="info-row">
                <span class="info-label">เบอร์โทร:</span>
                <span class="info-value"><asp:Label ID="lblCustomerPhone" runat="server"></asp:Label></span>
            </div>

            <div class="info-row">
                <span class="info-label">วันเช็คอิน:</span>
                <span class="info-value"><asp:Label ID="lblCheckinDate" runat="server"></asp:Label></span>
            </div>

            <div class="info-row">
                <span class="info-label">วันเช็คเอาท์:</span>
                <span class="info-value"><asp:Label ID="lblCheckoutDate" runat="server"></asp:Label></span>
            </div>

            <div class="info-row">
                <span class="info-label">ยอดรวมทั้งหมด:</span>
                <span class="info-value amount-paid">
                    <asp:Label ID="lblTotalPrice" runat="server"></asp:Label> บาท
                </span>
            </div>

            <div class="info-row">
                <span class="info-label">ชำระแล้ว:</span>
                <span class="info-value amount-paid">
                    <asp:Label ID="lblTotalPaid" runat="server"></asp:Label> บาท
                </span>
            </div>

            <div class="info-row" style="border-bottom: none; margin-top: 10px;">
                <span class="info-label" style="font-size: 18px;">ยอดคงเหลือ:</span>
                <span class="info-value amount-highlight">
                    <asp:Label ID="lblRemainingBalance" runat="server"></asp:Label> บาท
                </span>
            </div>
        </div>

        <!-- Payment Form -->
        <div class="payment-card">
            <div class="card-header">
                <i class="fa fa-money"></i> ข้อมูลการชำระเงิน
            </div>

            <%-- 🧾 สลิปก่อนยอด (CLAUDE.md ข้อ 18): ช่องทางโอน/QR → เลือกวิธี → แนบสลิป (อ่านทันที) → เห็นยอด · ปิดฟีเจอร์ = ลำดับเดิม --%>
            <div id="mpForm" class="sf-root">
            <div id="mpSfGuide" class="form-group mp-sf-guide" style="display:none;"></div>

            <div class="form-group" id="mpAmountGroup">
                <label class="form-label">จำนวนเงินที่ต้องการชำระ (บาท) <span style="color: red;">*</span></label>
                <asp:TextBox ID="txtPaymentAmount" runat="server" CssClass="form-control" TextMode="Number"
                    min="1" step="0.01" placeholder="กรอกจำนวนเงิน"></asp:TextBox>
                <%-- 🔒 ยอดล็อกตามสลิป (PHASE19_36): เลือกไฟล์สลิปแล้วระบบอ่านยอดให้ — อ่านได้ชัด = ล็อก, อ่านไม่ได้ = กรอกเอง --%>
                <span id="mpSlipLockMsg" class="slip-lock-msg" style="display:none;"></span>
                <asp:RequiredFieldValidator ID="rfvAmount" runat="server" ControlToValidate="txtPaymentAmount"
                    ErrorMessage="กรุณากรอกจำนวนเงิน" ForeColor="Red" Display="Dynamic"></asp:RequiredFieldValidator>
                <asp:RangeValidator ID="rvAmount" runat="server" ControlToValidate="txtPaymentAmount"
                    MinimumValue="0.01" MaximumValue="1000000" Type="Double"
                    ErrorMessage="จำนวนเงินไม่ถูกต้อง" ForeColor="Red" Display="Dynamic"></asp:RangeValidator>
                <div id="mpSfOutcome" class="sf-outcome" aria-live="polite"></div>
            </div>

            <div class="form-group" id="mpMethodGroup">
                <label class="form-label">วิธีการชำระเงิน <span style="color: red;">*</span></label>
                <asp:DropDownList ID="ddlPaymentMethod" runat="server" CssClass="form-control" DataTextField="Paid_How" DataValueField="ID">
                    <asp:ListItem Value="">-- เลือกวิธีการชำระ --</asp:ListItem>
                </asp:DropDownList>
                <asp:RequiredFieldValidator ID="rfvPaymentMethod" runat="server" ControlToValidate="ddlPaymentMethod"
                    InitialValue="" ErrorMessage="กรุณาเลือกวิธีการชำระเงิน" ForeColor="Red" Display="Dynamic"></asp:RequiredFieldValidator>
            </div>

            <div class="form-group" id="mpSlipGroup">
                <label class="form-label">อัพโหลดสลิปการโอนเงิน (JPG, PNG, PDF เท่านั้น, ไม่เกิน 5MB)</label>
                <asp:FileUpload ID="fuPaymentSlip" runat="server" CssClass="form-control" />
                <small class="mp-slip-note" style="color: #7f8c8d;">หมายเหตุ: หากชำระผ่านโอนเงิน กรุณาอัพโหลดสลิปเพื่อยืนยันการชำระเงิน</small>
                <%-- ไม่มีสลิป (ช่องทางโอน/QR): เฉพาะ Owner / Admin ที่มีสิทธิ์การเงิน — ต้องมีเหตุผล · บันทึกตรวจสอบ + ติดธง + แจ้งเจ้าของ
                     (เซิร์ฟเวอร์ตรวจสิทธิ์/เหตุผลซ้ำใน btnSubmit_Click · คนอื่นไม่เห็นลิงก์และบันทึกช่องทางโอนโดยไม่มีสลิปไม่ได้) --%>
                <asp:Panel ID="pnlNoSlip" runat="server" Visible="false">
                    <a href="#" id="mpSfManual" class="mp-sf-manual" style="display:none;">ไม่มีสลิป? บันทึกยอดเองพร้อมเหตุผล (Owner/Admin)</a>
                    <div id="mpNoSlipBox" class="sf-noslip" style="display:none;">
                        <label for="<%= txtNoSlipReason.ClientID %>">เหตุผลที่ไม่มีสลิป (บังคับ อย่างน้อย 10 ตัวอักษร)</label>
                        <asp:TextBox ID="txtNoSlipReason" runat="server" TextMode="MultiLine" Rows="2" MaxLength="500"
                            placeholder="เช่น ลูกค้าโอนผ่านตู้ ATM ไม่มีสลิป — ตรวจยอดเข้าบัญชีในแอปธนาคารแล้ว" />
                        <small>ระบบบันทึกชื่อผู้บันทึกและเหตุผล ติดธง "ไม่มีสลิป — รอตรวจ" ในรายการรับเงิน และแจ้งเตือนเจ้าของ</small>
                    </div>
                </asp:Panel>
            </div>

            <div class="form-group" id="mpNotesGroup">
                <label class="form-label">หมายเหตุ</label>
                <asp:TextBox ID="txtNotes" runat="server" CssClass="form-control" TextMode="MultiLine"
                    Rows="3" placeholder="ระบุหมายเหตุเพิ่มเติม (ถ้ามี)"></asp:TextBox>
            </div>

            <div class="form-group" id="mpSubmitGroup">
                <asp:Button ID="btnSubmit" runat="server" Text="ชำระเงิน" CssClass="btn-primary" OnClick="btnSubmit_Click"
                    OnClientClick="if (window.mpSfBlocks) { if (mpSfBlocks()) return false; }" />
                <asp:HyperLink ID="lnkCancel" runat="server" NavigateUrl="~/ReserveTable.aspx" CssClass="btn-secondary">
                    ยกเลิก
                </asp:HyperLink>
            </div>
            </div>
        </div>

        <%-- 🧾 สลิปก่อนยอด — คลาส .sf-* (ไม่ใช้ = ไม่มีผล) --%>
        <link id="slipFirstCss" rel="stylesheet" href="<%= ResolveUrl("~/Content/slip-first.css?v=2") %>" />
        <style>
            #mpForm { --sf-accent: #3498db; --sf-accent-dark: #2c3e50; --sf-soft: #eaf3fb; --sf-line: #d6e4f0; }
            #mpForm.mp-sf { display: flex; flex-direction: column; }
            #mpForm.mp-sf #mpMethodGroup { order: 1; }
            #mpForm.mp-sf #mpSfGuide { order: 2; }
            #mpForm.mp-sf #mpSlipGroup { order: 3; }
            #mpForm.mp-sf #mpAmountGroup { order: 4; }
            #mpForm.mp-sf #mpNotesGroup { order: 5; }
            #mpForm.mp-sf #mpSubmitGroup { order: 6; }
            #mpForm[data-sf="pick"] #mpAmountGroup, #mpForm[data-sf="pick"] #mpSlipGroup,
            #mpForm[data-sf="wait"] #mpAmountGroup, #mpForm[data-sf="reading"] #mpAmountGroup { display: none; }
            #mpForm[data-sf] .mp-slip-note { display: none; }
            #mpForm[data-sf="wait"] .mp-sf-manual { display: inline-block !important; margin-top: 8px; min-height: 36px; line-height: 36px; }
            #mpForm[data-sf="locked"] #mpAmountGroup .form-control { font-size: 20px; font-weight: 700; }
            #mpForm:not([data-sf]) #mpSfGuide { display: none !important; }
        </style>
        <script src="<%= ResolveUrl("~/Scripts/slip-first.js?v=2") %>"></script>
        <script>
            (function () {
                var cfg = <%= SlipFirstJs %>;
                function start() {
                    var form = document.getElementById('mpForm');
                    if (!cfg || !cfg.on || !form || !window.SlipFirst) return;   // ปิดฟีเจอร์ = ลำดับ/หน้าตาเดิม
                    var ddl = document.getElementById('<%= ddlPaymentMethod.ClientID %>');
                    var amt = document.getElementById('<%= txtPaymentAmount.ClientID %>');
                    var file = document.getElementById('<%= fuPaymentSlip.ClientID %>');
                    var guide = document.getElementById('mpSfGuide');
                    var out = document.getElementById('mpSfOutcome');
                    var manual = document.getElementById('mpSfManual');
                    if (!ddl || !amt || !file) return;
                    form.className += ' mp-sf';
                    var read = '';          // '', 'reading', 'locked', 'edit'
                    var manualOn = false;
                    var transfer = cfg.transfer || [];
                    var money = SlipFirst.money;

                    guide.innerHTML = '<ol class="sf-steps" id="mpSfSteps" aria-label="ขั้นตอนการชำระเงิน"><li>เลือกวิธีชำระ</li><li>แนบสลิป</li><li>ตรวจยอด</li><li>บันทึก</li></ol>'
                        + (cfg.due > 0
                            ? '<div class="sf-guide"><div class="sf-line"><div class="sf-line-l">ยอดคงเหลือที่ต้องรับ<small>ให้ลูกค้าโอนยอดนี้ หรือบางส่วน แล้วแนบสลิป — ระบบอ่านยอดให้เอง</small></div>'
                              + '<div class="sf-line-v">฿' + money(cfg.due) + '</div>'
                              + '<button type="button" class="sf-copy" data-sf-copy="' + (Math.round(cfg.due) === cfg.due ? String(cfg.due) : cfg.due.toFixed(2)) + '" aria-label="คัดลอกยอดคงเหลือ">คัดลอก</button></div></div>'
                            : '');

                    // ย่อรูปก่อนตรวจ/บันทึก (SlipFirst.prepare — QR/ยอดอ่านได้เท่ารูปเดิม) แล้วใส่กลับลงช่องไฟล์ → ตรวจ (sliplock:go) และบันทึกส่งรูปเล็ก
                    var prepping = false;
                    if (window.Promise) file.setAttribute('data-sf-compress', '1');
                    var dz = SlipFirst.dropzone({
                        input: file, accept: 'image+pdf', maxMB: 5, rawMaxMB: 30,
                        texts: { title: 'แนบสลิปที่ลูกค้าโอน', sub: 'ถ่ายรูป / เลือกรูป / ลากไฟล์มาวาง — ระบบอ่านยอดให้ทันที' },
                        onPick: function (f) {
                            manualOn = false;
                            if (file.getAttribute('data-sf-compress') !== '1') return;
                            prepping = true;
                            read = 'reading'; apply();
                            if (dz) dz.setState('reading', '', null);
                            SlipFirst.prepare(f).then(function (p) {
                                prepping = false;
                                if (p.compressed) SlipFirst.setInputFile(file, p.file);
                                var ev;
                                try { ev = new CustomEvent('sliplock:go'); } catch (e0) { ev = document.createEvent('CustomEvent'); ev.initCustomEvent('sliplock:go', false, false, null); }
                                file.dispatchEvent(ev);
                            });
                        }
                    });

                    function steps(onIdx) {
                        var li = document.querySelectorAll('#mpSfSteps li');
                        for (var i = 0; i < li.length; i++) li[i].className = i < onIdx ? 'is-done' : i === onIdx ? 'is-on' : '';
                    }
                    function isTransfer() { return transfer.indexOf(ddl.value) >= 0; }
                    function apply() {
                        var st;
                        if (!ddl.value) st = 'pick';
                        else if (!isTransfer()) st = '';
                        else if (manualOn) st = 'edit';
                        else st = read || 'wait';
                        if (st) form.setAttribute('data-sf', st); else form.removeAttribute('data-sf');
                        guide.style.display = st && st !== 'pick' ? '' : 'none';
                        steps(st === 'pick' ? 0 : st === 'wait' ? 1 : st === 'reading' || st === 'edit' ? 2 : st === 'locked' ? 3 : 0);
                        upd();
                    }
                    function upd() {
                        if (!out) return;
                        if (!form.getAttribute('data-sf')) { out.className = 'sf-outcome'; out.innerHTML = ''; return; }
                        var v = parseFloat(String(amt.value || '').replace(/,/g, ''));
                        var o = SlipFirst.outcome({ amount: isNaN(v) ? 0 : v, due: cfg.due, mode: 'pay' });
                        out.className = 'sf-outcome' + (o.cls ? ' ' + o.cls : '');
                        out.innerHTML = o.html;
                    }
                    file.addEventListener('sliplock:start', function () { read = 'reading'; if (dz) dz.setState('reading', '', null); apply(); });
                    file.addEventListener('sliplock:result', function (e) {
                        var j = e.detail || {};
                        read = j.locked ? 'locked' : 'edit';
                        if (dz) dz.setState('done', j.locked ? '✅ อ่านยอดจากสลิปแล้ว' : '✏️ อ่านยอดไม่ชัด — กรอกยอดตามสลิป', j.locked ? 'ok' : 'warn');
                        apply();
                        if (!j.locked) { try { amt.focus(); } catch (x) { } }
                    });
                    file.addEventListener('sliplock:error', function () {
                        read = file.files && file.files.length ? 'edit' : '';
                        if (dz) dz.setState(read ? 'done' : 'empty', read ? '✏️ ตรวจสลิปไม่ได้ตอนนี้ — กรอกยอดตามสลิป' : '', read ? 'warn' : null);
                        apply();
                    });
                    // ไม่มีสลิป: ลิงก์มีเฉพาะ Owner/Admin สิทธิ์การเงิน (pnlNoSlip) → เปิดช่องยอด + ช่องเหตุผล (บังคับ)
                    var noSlipBox = document.getElementById('mpNoSlipBox');
                    var noSlipReason = document.getElementById('<%= txtNoSlipReason.ClientID %>');
                    if (manual) manual.onclick = function () {
                        manualOn = true;
                        try { file.value = ''; } catch (x0) { }
                        if (dz) dz.setState('empty', '', null);
                        apply();
                        if (noSlipBox) noSlipBox.style.display = '';
                        try { (noSlipReason || amt).focus(); } catch (x) { }
                        return false;
                    };
                    file.addEventListener('change', function () { if (noSlipBox && file.files && file.files.length) noSlipBox.style.display = 'none'; });
                    ddl.addEventListener('change', apply);
                    amt.addEventListener('input', upd);
                    window.mpSfBlocks = function () {
                        var st = form.getAttribute('data-sf');
                        if (st === 'reading' || prepping) { if (dz) dz.setError('รอระบบตรวจสลิปสักครู่'); return true; }
                        if (st === 'wait') { if (dz) dz.setError('แนบสลิปก่อน — ระบบจะอ่านยอดให้' + (manual ? ' (หรือแตะ "ไม่มีสลิป?" ด้านล่าง)' : '')); return true; }
                        if (manualOn && noSlipReason && String(noSlipReason.value || '').replace(/^\s+|\s+$/g, '').length < 10) {
                            if (noSlipBox) noSlipBox.style.display = '';
                            try { noSlipReason.focus(); } catch (x2) { }
                            return true;
                        }
                        return false;
                    };
                    apply();
                }
                if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start); else start();
            })();
        </script>

        <!-- ให้ลูกค้าจ่ายเอง (แสดงเฉพาะเมื่อเปิดฟีเจอร์รับชำระเงินออนไลน์) -->
        <asp:Panel ID="pnlOnlineLink" runat="server" CssClass="payment-card" Visible="false">
            <div class="card-header">
                <h3>ให้ลูกค้าชำระเงินเอง</h3>
            </div>
            <div style="padding:15px;">
                <p style="color:#7f8c8d;margin:0 0 10px;">
                    ส่งลิงก์นี้ให้ลูกค้าทาง LINE / SMS — ลูกค้าเลือกได้ว่าจะสแกน QR แนบสลิป หรือจ่ายด้วยบัตรเครดิต
                    เมื่อจ่ายสำเร็จระบบจะบันทึกเข้าการจองนี้ให้อัตโนมัติ
                </p>
                <asp:TextBox ID="txtPayLink" runat="server" ReadOnly="true"
                    style="width:100%;padding:9px 11px;border:1px solid #dbe3de;border-radius:8px;font-family:monospace;font-size:12.5px;" />
                <div style="margin-top:10px; display:flex; gap:10px; align-items:center; flex-wrap:wrap;">
                    <%-- เดิมมีแต่ช่องอ่านอย่างเดียว ต้องลากเมาส์เลือกเองทั้งเส้น --%>
                    <button type="button" onclick="mpCopyLink(this)"
                        style="padding:9px 16px;border:0;border-radius:8px;background:#27ae60;color:#fff;font-weight:600;cursor:pointer;">
                        📋 คัดลอกลิงก์
                    </button>
                    <asp:HyperLink ID="lnkOpenPay" runat="server" Target="_blank" CssClass="btn-secondary">
                        เปิดหน้าชำระเงิน ↗
                    </asp:HyperLink>
                    <span style="font-size:12.5px;color:#7f8c8d;">
                        ลิงก์นี้<b>ไม่หมดอายุ</b> — เปิดวันไหนระบบคิดยอดคงเหลือ ณ ตอนนั้นให้เอง
                    </span>
                </div>
                <div id="mpQr" style="margin-top:12px;"></div>
            </div>
        </asp:Panel>

        <script src="https://cdn.jsdelivr.net/npm/qrcodejs@1.0.0/qrcode.min.js"></script>
        <script>
            function mpCopyLink(btn) {
                var el = document.getElementById('<%= txtPayLink.ClientID %>');
                if (!el) return;
                el.select(); el.setSelectionRange(0, 99999);
                try { document.execCommand('copy'); } catch (e) { }
                if (navigator.clipboard) { try { navigator.clipboard.writeText(el.value); } catch (e) { } }
                var old = btn.textContent; btn.textContent = '✓ คัดลอกแล้ว';
                setTimeout(function () { btn.textContent = old; }, 1600);
            }
            (function () {
                var el = document.getElementById('<%= txtPayLink.ClientID %>');
                var box = document.getElementById('mpQr');
                if (!el || !box || !el.value || typeof QRCode === 'undefined') return;
                new QRCode(box, { text: el.value, width: 160, height: 160, correctLevel: QRCode.CorrectLevel.M });
                box.insertAdjacentHTML('beforeend',
                    '<div style="font-size:12.5px;color:#7f8c8d;margin-top:6px;">หรือให้ลูกค้าสแกน QR นี้</div>');
            })();
        </script>

        <!-- Payment History -->
        <div class="payment-card">
            <div class="card-header">
                <i class="fa fa-history"></i> ประวัติการชำระเงิน
            </div>

            <asp:GridView ID="gvPaymentHistory" runat="server" CssClass="payment-history-table"
                AutoGenerateColumns="False" EmptyDataText="ยังไม่มีประวัติการชำระเงิน">
                <Columns>
                    <asp:BoundField DataField="PaymentDate" HeaderText="วันที่ชำระ" DataFormatString="{0:dd/MM/yyyy HH:mm}" />
                    <asp:BoundField DataField="PaymentAmount" HeaderText="จำนวนเงิน" DataFormatString="{0:N2}" />
                    <asp:TemplateField HeaderText="ประเภท">
                        <ItemTemplate>
                            <span class="badge badge-info"><%# Eval("PaymentType") %></span>
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:BoundField DataField="PaymentMethod" HeaderText="วิธีชำระ" />
                    <asp:BoundField DataField="Receipt_ID" HeaderText="เลขที่ใบเสร็จ" />
                    <asp:TemplateField HeaderText="สลิป">
                        <ItemTemplate>
                            <%# !string.IsNullOrEmpty(Eval("SlipFileURL")?.ToString()) ?
                                "<a href='" + ResolveUrl("~/") + Eval("SlipFileURL") + "' target='_blank' style='color: #3498db; text-decoration: none;'><i class='fa fa-file-image-o'></i> ดูสลิป</a>" :
                                "<span style='color: #95a5a6;'>-</span>" %>
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="สถานะ">
                        <ItemTemplate>
                            <span class="badge <%# Eval("Status").ToString() == "COMPLETED" ? "badge-success" : "badge-warning" %>">
                                <%# Eval("Status").ToString() == "COMPLETED" ? "สำเร็จ" : "รอดำเนินการ" %>
                            </span>
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:BoundField DataField="RemainingBalance" HeaderText="ยอดคงเหลือ" DataFormatString="{0:N2}" />
                </Columns>
            </asp:GridView>
        </div>
    </div>
</asp:Content>
