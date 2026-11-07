<%@ Page Title="กรอกข้อมูลบัตร" Language="C#" AutoEventWireup="true" CodeBehind="Card.aspx.cs" Inherits="Take_Time_BangPhra.Payment.Card" %>

<!DOCTYPE html>
<html lang="th">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>กรอกข้อมูลบัตร — Take Time BangPhra</title>
    <style>
        * { box-sizing: border-box; }
        body { margin: 0; background: #f2f5f3; color: #21302a; font-size: 15px;
               font-family: 'Segoe UI', 'Sarabun', Tahoma, sans-serif; }
        .wrap { max-width: 460px; margin: 0 auto; padding: 22px 14px 60px; }
        .card { background: #fff; border-radius: 16px; padding: 20px 18px;
                box-shadow: 0 2px 14px rgba(0,0,0,.06); margin-bottom: 14px; }
        h1 { font-size: 19px; margin: 2px 0 4px; }
        .sub { color: #6c7d74; font-size: 13.5px; margin: 0 0 14px; line-height: 1.6; }
        .amount { font-size: 26px; font-weight: 700; color: #1b7a4b; margin: 4px 0 2px; }
        .hold-note { background: #eef4fb; color: #1d4e79; border-radius: 10px;
                     padding: 11px 13px; font-size: 13.5px; line-height: 1.65; margin: 12px 0; }
        .field { margin-bottom: 13px; }
        .field label { display: block; font-weight: 600; font-size: 13.5px; margin-bottom: 5px; }
        .field input, .field select {
            width: 100%; padding: 12px; border: 1.5px solid #dbe3de; border-radius: 10px;
            font-size: 16px; background: #fff;
        }
        .field input:focus { outline: 0; border-color: #1b7a4b; box-shadow: 0 0 0 3px rgba(27,122,75,.12); }
        .row2 { display: flex; gap: 10px; }
        .row2 .field { flex: 1; }
        .btn { display: block; width: 100%; padding: 14px; border: 0; border-radius: 12px;
               background: #1b7a4b; color: #fff; font-size: 16px; font-weight: 600; cursor: pointer; }
        .btn:disabled { opacity: .55; cursor: default; }
        .alert { padding: 12px 14px; border-radius: 10px; font-size: 14px; line-height: 1.6; margin-bottom: 13px; }
        .alert.err { background: #fdecec; color: #a12626; }
        .alert.ok { background: #e8f6ee; color: #16653e; }
        .secure { text-align: center; color: #7b8a83; font-size: 12.5px; margin-top: 14px; line-height: 1.7; }
        .test-band { background: #fff6e5; color: #8a5a00; text-align: center; font-size: 12.5px;
                     padding: 7px; border-radius: 8px; margin-bottom: 12px; }
    </style>
</head>
<body>
<form id="form1" runat="server">
    <asp:HiddenField ID="hfToken" runat="server" />
    <div class="wrap">
        <div class="card">
            <asp:Literal ID="litTestBand" runat="server" />
            <h1><asp:Literal ID="litTitle" runat="server" /></h1>
            <p class="sub"><asp:Literal ID="litDesc" runat="server" /></p>
            <div class="amount"><asp:Literal ID="litAmount" runat="server" /></div>
            <asp:Literal ID="litHoldNote" runat="server" />
            <asp:Literal ID="litMsg" runat="server" />

            <asp:Panel ID="pnlForm" runat="server">
                <div class="field">
                    <label for="ccName">ชื่อบนบัตร</label>
                    <input type="text" id="ccName" autocomplete="cc-name" placeholder="SOMCHAI JAIDEE" />
                </div>
                <div class="field">
                    <label for="ccNumber">หมายเลขบัตร</label>
                    <input type="text" id="ccNumber" inputmode="numeric" autocomplete="cc-number"
                           placeholder="0000 0000 0000 0000" maxlength="23" />
                </div>
                <div class="row2">
                    <div class="field">
                        <label for="ccExp">หมดอายุ (ดด/ปป)</label>
                        <input type="text" id="ccExp" inputmode="numeric" autocomplete="cc-exp"
                               placeholder="12/29" maxlength="5" />
                    </div>
                    <div class="field">
                        <label for="ccCvc">CVV</label>
                        <input type="password" id="ccCvc" inputmode="numeric" autocomplete="cc-csc"
                               placeholder="•••" maxlength="4" />
                    </div>
                </div>
                <button type="button" class="btn" id="btnPay" onclick="ttSubmitCard()">
                    <asp:Literal ID="litButton" runat="server" />
                </button>
                <asp:Button ID="btnServer" runat="server" OnClick="btnServer_Click"
                    style="display:none" UseSubmitBehavior="false" />
            </asp:Panel>

            <asp:Panel ID="pnlDone" runat="server" Visible="false">
                <a class="btn" style="text-decoration:none;text-align:center" href="<%= ResolveUrl("~/") %>">กลับหน้าแรก</a>
            </asp:Panel>

            <div class="secure">
                🔒 ข้อมูลบัตรถูกส่งตรงถึงผู้ให้บริการชำระเงิน (Omise) แบบเข้ารหัส<br />
                ไม่ผ่านและไม่ถูกเก็บไว้ในระบบของที่พัก
            </div>
        </div>
    </div>

    <script src="https://cdn.omise.co/omise.js"></script>
    <script>
        var TT_PKEY = '<%= PublicKeyJs %>';

        function ttFail(msg) {
            var b = document.getElementById('btnPay');
            b.disabled = false; b.textContent = b.getAttribute('data-label');
            alert(msg);
        }

        function ttSubmitCard() {
            var b = document.getElementById('btnPay');
            if (!b.getAttribute('data-label')) b.setAttribute('data-label', b.textContent);

            var name = document.getElementById('ccName').value.trim();
            var num = document.getElementById('ccNumber').value.replace(/[^0-9]/g, '');
            var exp = document.getElementById('ccExp').value.trim();
            var cvc = document.getElementById('ccCvc').value.trim();

            var m = exp.match(/^(\d{1,2})\s*\/\s*(\d{2,4})$/);
            if (!name) { alert('กรุณากรอกชื่อบนบัตร'); return; }
            if (num.length < 12) { alert('หมายเลขบัตรไม่ถูกต้อง'); return; }
            if (!m) { alert('วันหมดอายุให้กรอกเป็น ดด/ปป เช่น 12/29'); return; }
            if (cvc.length < 3) { alert('กรุณากรอก CVV หลังบัตร'); return; }

            var year = parseInt(m[2], 10); if (year < 100) year += 2000;

            if (typeof Omise === 'undefined') {
                alert('โหลดตัวเชื่อมผู้ให้บริการไม่สำเร็จ กรุณาลองใหม่ หรือติดต่อเจ้าหน้าที่');
                return;
            }

            b.disabled = true; b.textContent = 'กำลังดำเนินการ…';

            Omise.setPublicKey(TT_PKEY);
            Omise.createToken('card', {
                name: name,
                number: num,
                expiration_month: parseInt(m[1], 10),
                expiration_year: year,
                security_code: cvc
            }, function (statusCode, response) {
                if (statusCode === 200 && response.id) {
                    // ส่งเฉพาะ token (ใช้ครั้งเดียว) กลับเซิร์ฟเวอร์ — ไม่มีข้อมูลบัตรติดไป
                    document.getElementById('<%= hfToken.ClientID %>').value = response.id;
                    <%= Page.ClientScript.GetPostBackEventReference(btnServer, "") %>;
                } else {
                    ttFail('บัตรไม่ผ่าน: ' + (response && response.message ? response.message : 'กรุณาตรวจข้อมูลบัตร'));
                }
            });
        }
    </script>
</form>
</body>
</html>
