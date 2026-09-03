<%@ Page Title="ชำระเงิน" Language="C#" AutoEventWireup="true" CodeBehind="Pay.aspx.cs" Inherits="Take_Time_BangPhra.Payment.Pay" %>

<!DOCTYPE html>
<html lang="th">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1, maximum-scale=1" />
    <title>ชำระเงิน — Take Time BangPhra</title>
    <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css" />
    <style>
        * { box-sizing: border-box; }
        body {
            margin: 0; background: #f2f5f3; font-size: 15px; color: #21302a;
            font-family: 'Segoe UI', 'Sarabun', Tahoma, sans-serif;
        }
        .wrap { max-width: 520px; margin: 0 auto; padding: 18px 14px 60px; }
        .card {
            background: #fff; border-radius: 16px; padding: 18px;
            box-shadow: 0 2px 14px rgba(0,0,0,.06); margin-bottom: 14px;
        }
        h1 { font-size: 20px; margin: 4px 0 16px; }
        h2 { font-size: 16px; margin: 0 0 12px; }
        .sum div { display: flex; justify-content: space-between; padding: 7px 0; border-bottom: 1px dashed #e3e9e5; }
        .sum div:last-child { border-bottom: 0; }
        .sum span { color: #6c7d74; }
        .sum b { text-align: right; }
        .total { font-size: 19px; color: #1b7a4b; font-weight: 700; }
        .total span { color: #21302a; font-size: 15px; font-weight: 400; }

        .paylist label {
            display: flex; align-items: center; gap: 12px; padding: 14px;
            border: 2px solid #e3e9e5; border-radius: 12px; margin-bottom: 10px;
            cursor: pointer; background: #fff; transition: .12s;
        }
        .paylist label:hover { border-color: #b9d6c6; }
        .paylist input[type=radio] { width: 18px; height: 18px; accent-color: #1b7a4b; }
        .paylist input[type=radio]:checked + span { color: #1b7a4b; font-weight: 600; }

        .btn {
            display: block; width: 100%; padding: 14px; border: 0; border-radius: 12px;
            background: #1b7a4b; color: #fff; font-size: 16px; font-weight: 600;
            cursor: pointer; text-align: center; text-decoration: none;
        }
        .btn:hover { background: #16653e; }
        .btn.ghost { background: #fff; color: #46584f; border: 2px solid #dfe6e2; }
        .btn + .btn { margin-top: 10px; }

        .qrbox { text-align: center; padding: 10px 0; }
        .qrbox img { max-width: 260px; width: 100%; border-radius: 12px; }
        .bank { white-space: pre-wrap; background: #f6f9f7; border-radius: 10px; padding: 12px; margin-top: 10px; font-size: 14px; }
        .note { color: #6c7d74; font-size: 13.5px; margin-top: 10px; line-height: 1.55; }
        .field { margin: 14px 0; }
        .field label { display: block; font-weight: 600; margin-bottom: 6px; font-size: 14px; }

        .alert { padding: 13px 15px; border-radius: 12px; font-size: 14.5px; line-height: 1.6; margin-bottom: 14px; }
        .alert.err { background: #fdecec; color: #a12626; }
        .alert.ok { background: #e8f6ee; color: #16653e; }
        .alert.info { background: #eef4fb; color: #1d4e79; }

        .refline { font-family: monospace; font-size: 13px; color: #6c7d74; word-break: break-all; margin-top: 12px; }
        .payload { font-family: monospace; font-size: 11.5px; word-break: break-all; background: #f6f9f7;
                   padding: 10px; border-radius: 8px; color: #46584f; margin-top: 10px; }
    </style>
</head>
<body>
<form id="form1" runat="server">
    <div class="wrap">

        <asp:Panel ID="pnlError" runat="server" Visible="false">
            <div class="card">
                <div class="alert err"><asp:Literal ID="litError" runat="server" /></div>
                <a class="btn ghost" href="javascript:history.back()">← ย้อนกลับ</a>
            </div>
        </asp:Panel>

        <asp:Panel ID="pnlMain" runat="server" Visible="false">

            <div class="card">
                <h1><i class="fas fa-lock"></i> ชำระเงิน</h1>
                <div class="sum">
                    <div><span>รายการ</span><b><asp:Literal ID="litItem" runat="server" /></b></div>
                    <asp:PlaceHolder ID="phCustomer" runat="server" Visible="false">
                        <div><span>ผู้ชำระ</span><b><asp:Literal ID="litCustomer" runat="server" /></b></div>
                    </asp:PlaceHolder>
                    <asp:PlaceHolder ID="phSurcharge" runat="server" Visible="false">
                        <div><span>ยอดรายการ</span><b><asp:Literal ID="litBase" runat="server" /></b></div>
                        <div><span>ค่าธรรมเนียมบัตร</span><b><asp:Literal ID="litSurcharge" runat="server" /></b></div>
                    </asp:PlaceHolder>
                    <div class="total"><span>ยอดที่ต้องชำระ</span><b><asp:Literal ID="litAmount" runat="server" /></b></div>
                </div>
            </div>

            <!-- ── เลือกวิธีชำระ ── -->
            <asp:Panel ID="pnlMethods" runat="server" CssClass="card">
                <h2>เลือกวิธีชำระเงิน</h2>
                <asp:RadioButtonList ID="rblMethod" runat="server" CssClass="paylist" RepeatLayout="Flow" />
                <asp:Button ID="btnContinue" runat="server" CssClass="btn" Text="ดำเนินการต่อ" OnClick="btnContinue_Click" />
            </asp:Panel>

            <!-- ── สแกน QR แล้วแนบสลิป (วิธีเดิม) ── -->
            <asp:Panel ID="pnlManual" runat="server" CssClass="card" Visible="false">
                <h2><i class="fas fa-qrcode"></i> สแกน QR / โอนเงิน</h2>
                <div class="qrbox">
                    <asp:Literal ID="litManualQr" runat="server" />
                </div>
                <asp:Literal ID="litBank" runat="server" />
                <div class="note"><asp:Literal ID="litManualNote" runat="server" /></div>

                <div class="field">
                    <label>แนบสลิปการโอนเงิน</label>
                    <asp:FileUpload ID="fuSlip" runat="server" accept="image/*,.pdf" />
                </div>

                <asp:Button ID="btnManualConfirm" runat="server" CssClass="btn"
                    Text="✓ ยืนยันการชำระเงิน" OnClick="btnManualConfirm_Click" />
                <asp:Button ID="btnManualBack" runat="server" CssClass="btn ghost"
                    Text="← เลือกวิธีอื่น" OnClick="btnBack_Click" CausesValidation="false" />
            </asp:Panel>

            <!-- ── ผ่านเกตเวย์ ── -->
            <asp:Panel ID="pnlGateway" runat="server" CssClass="card" Visible="false">
                <h2><i class="fas fa-credit-card"></i> ชำระผ่านระบบออนไลน์</h2>
                <asp:Literal ID="litGwInfo" runat="server" />

                <asp:PlaceHolder ID="phGwLink" runat="server" Visible="false">
                    <asp:HyperLink ID="lnkPay" runat="server" CssClass="btn" Target="_self">
                        ไปหน้าชำระเงิน →
                    </asp:HyperLink>
                    <div class="note">
                        ระบบจะพาไปยังหน้าชำระเงินที่ปลอดภัยของผู้ให้บริการ
                        ข้อมูลบัตรของท่านไม่ผ่านและไม่ถูกเก็บไว้ในระบบของเรา
                    </div>
                </asp:PlaceHolder>

                <asp:PlaceHolder ID="phGwQr" runat="server" Visible="false">
                    <div class="qrbox"><asp:Literal ID="litGwQr" runat="server" /></div>
                </asp:PlaceHolder>

                <div class="refline">เลขอ้างอิง: <asp:Literal ID="litRef" runat="server" /></div>

                <asp:Button ID="btnCheck" runat="server" CssClass="btn ghost"
                    Text="🔄 ตรวจสอบสถานะการชำระเงิน" OnClick="btnCheck_Click" CausesValidation="false" />
                <asp:Button ID="btnGwBack" runat="server" CssClass="btn ghost"
                    Text="← เลือกวิธีอื่น" OnClick="btnBack_Click" CausesValidation="false" />
            </asp:Panel>

            <!-- ── เสร็จสิ้น ── -->
            <asp:Panel ID="pnlDone" runat="server" CssClass="card" Visible="false">
                <div class="alert ok"><asp:Literal ID="litDone" runat="server" /></div>
                <asp:HyperLink ID="lnkBackHome" runat="server" CssClass="btn" NavigateUrl="~/">กลับหน้าแรก</asp:HyperLink>
            </asp:Panel>

        </asp:Panel>
    </div>
</form>
</body>
</html>
