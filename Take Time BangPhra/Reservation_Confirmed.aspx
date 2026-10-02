<%@ Page Title="" Async="true" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Reservation_Confirmed.aspx.cs" Inherits="Take_Time_BangPhra.Reservation_Confirmed" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style type="text/css">
        body {
            background: linear-gradient(135deg, #6d4c41 0%, #8d6e63 100%);
            min-height: 100vh;
            padding: 10px;
            margin: 0;
        }

        .confirmation-container {
            font-family: 'Prompt', Arial, sans-serif;
            max-width: 1000px;
            margin: 0 auto;
            background: white;
            border-radius: 15px;
            box-shadow: 0 10px 30px rgba(0,0,0,0.2);
            overflow: hidden;
            color: #3E2723;
            font-size: 15px;
            line-height: 1.55;
        }

        .confirmation-header {
            text-align: center;
            padding: 16px 15px 14px;
            background: linear-gradient(135deg, #5d4037 0%, #8d6e63 100%);
            color: white;
        }

        .confirmation-header h1 { margin: 6px 0 0; font-size: 1.45em; }
        .confirmation-header p { margin: 0; opacity: 0.9; font-size: 0.9em; }

        .success-badge, .error-badge {
            color: white;
            padding: 5px 14px;
            border-radius: 14px;
            font-size: 0.85em;
            font-weight: bold;
            display: inline-block;
        }
        .success-badge { background: #4caf50; }
        .error-badge { background: #e53935; }

        .rc-code {
            display: inline-block; margin-top: 10px; background: rgba(255,255,255,0.16); border-radius: 10px;
            padding: 6px 14px; font-size: 1em;
        }
        .rc-code b { font-size: 1.35em; letter-spacing: 0.04em; }

        .instruction-text {
            text-align: center;
            color: rgba(255,255,255,0.92);
            font-size: 0.85em;
            margin-top: 8px;
        }

        /* สถานะการชำระเงิน + ขั้นตอนถัดไป */
        .rc-status { margin: 14px 14px 4px; border-radius: 12px; padding: 12px 16px; border: 1px solid; }
        .rc-status h2 { margin: 0 0 4px; font-size: 1.08em; }
        .rc-status p { margin: 0 0 4px; }
        .rc-ok { background: #E8F5E9; border-color: #A5D6A7; color: #1B5E20; }
        .rc-wait { background: #FFF8E1; border-color: #FFE082; color: #6D4C41; }
        .rc-info { background: #E3F2FD; border-color: #90CAF9; color: #0D47A1; }
        .rc-bad { background: #FFEBEE; border-color: #EF9A9A; color: #B71C1C; }
        .rc-off { background: #F5F5F5; border-color: #E0E0E0; color: #616161; }
        .rc-pay-btn {
            display: inline-flex; align-items: center; justify-content: center; min-height: 44px; margin-top: 8px;
            padding: 8px 20px; border-radius: 22px; background: #2E7D32; color: #fff !important; font-weight: bold; text-decoration: none;
        }
        .rc-next { margin: 8px 14px 0; background: #FAFAFA; border: 1px solid #EEE; border-radius: 12px; padding: 10px 16px; }
        .rc-next b { color: #5D4037; }
        .rc-next ol { margin: 6px 0 0; padding-left: 20px; }
        .rc-next li { margin: 2px 0; }

        .main-grid {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 0;
        }

        .left-column { padding: 12px; background: #fafafa; min-width: 0; }
        .right-column { padding: 12px; background: #f5f5f5; border-left: 2px solid #d7ccc8; min-width: 0; }

        .info-card, .detail-card, .slip-card {
            background: white;
            padding: 10px 12px;
            border-radius: 8px;
            margin-bottom: 10px;
            box-shadow: 0 2px 5px rgba(0,0,0,0.05);
        }
        .info-card { border-left: 3px solid #8d6e63; }
        .slip-card { text-align: left; }

        .info-card h3, .detail-card h3, .slip-card h3 {
            color: #5d4037;
            margin: 0 0 6px 0;
            font-size: 1em;
            border-bottom: 1px solid #d7ccc8;
            padding-bottom: 4px;
        }

        .info-row {
            display: flex;
            justify-content: space-between;
            gap: 10px;
            padding: 3px 0;
            font-size: 0.95em;
        }
        .info-label { font-weight: bold; color: #5d4037; flex: 0 0 auto; }
        .info-value { color: #333; text-align: right; flex: 1; min-width: 0; word-break: break-word; }

        .payment-card {
            background: linear-gradient(135deg, #fff8e1 0%, #ffecb3 100%);
            padding: 8px 10px;
            border-radius: 8px;
            border: 1px solid #ffd54f;
        }
        .rc-due { font-size: 1.1em; }

        .slip-image {
            width: 100%;
            max-height: 220px;
            border-radius: 6px;
            border: 1px solid #d7ccc8;
            object-fit: contain;
            background: #f9f9f9;
        }

        .accommodation-box, .items-box, .remark-box {
            background: #f9f9f9;
            padding: 8px 10px;
            border-radius: 6px;
            font-size: 0.93em;
            line-height: 1.6;
            border-left: 2px solid #8d6e63;
        }
        .items-box { max-height: 220px; overflow-y: auto; }
        .remark-box { background: #fff3e0; border-left-color: #ff9800; max-height: 160px; overflow-y: auto; }

        .rc-slip-item { margin-bottom: 8px; padding: 8px 10px; background: #f5f5f5; border-radius: 6px; font-size: 0.9em; }
        .rc-slip-item a { color: #1976d2; text-decoration: none; font-weight: bold; display: inline-block; padding: 6px 0; }
        .rc-docs { margin-top: 8px; padding: 8px 10px; background: linear-gradient(135deg, #e8f5e8 0%, #f1f8e9 100%); border-radius: 6px; border: 1px solid #c8e6c9; }
        .rc-docs a { color: #2e7d32; text-decoration: none; font-weight: bold; display: inline-block; padding: 6px 0; }

        /* นโยบาย/สัตว์เลี้ยง (พับได้) */
        .rc-section { margin: 0 12px 10px; }
        .rc-collapse > summary { cursor: pointer; font-weight: bold; color: #5d4037; padding: 8px 0; min-height: 28px; list-style-position: inside; }
        .rc-collapse > summary span { font-weight: normal; color: #999; font-size: 0.9em; }

        .action-buttons {
            text-align: center;
            padding: 12px;
            background: white;
            border-top: 1px solid #d7ccc8;
        }

        .btn-print {
            background: linear-gradient(135deg, #8d6e63 0%, #5d4037 100%);
            color: white;
            min-height: 48px;
            padding: 10px 24px;
            border: none;
            border-radius: 24px;
            font-size: 1em;
            font-weight: bold;
            cursor: pointer;
            margin: 4px;
            font-family: inherit;
        }

        @media print {
            .action-buttons, .instruction-text, .rc-pay-btn { display: none; }
            body { background: #fff; }
        }

        /* มือถือ: คอลัมน์เดียว ตัวอักษรอ่านง่าย */
        @media (max-width: 700px) {
            body { padding: 4px; }
            .confirmation-container { border-radius: 10px; font-size: 15px; }
            .main-grid { grid-template-columns: 1fr; }
            .right-column { border-left: 0; border-top: 2px solid #d7ccc8; }
            .left-column, .right-column { padding: 10px; }
            .rc-status, .rc-next { margin-left: 10px; margin-right: 10px; }
            .rc-section { margin: 0 10px 10px; }
            .btn-print { width: 100%; }
        }
    </style>
    <br />
    <div class="confirmation-container">
        <!-- Header Section -->
        <div class="confirmation-header">
            <asp:Label ID="Label10" runat="server" Text="" CssClass="success-badge"></asp:Label>
            <h1>การยืนยันการจอง</h1>
            <p>Reservation Confirmation</p>
            <div class="rc-code">รหัสการจอง (Booking No.) <b>#<asp:Label ID="Label1" runat="server"></asp:Label></b></div>
            <div class="instruction-text">
                <strong>📱 กรุณาบันทึกหน้านี้ไว้ และแสดงรหัสการจองตอนเช็คอิน</strong><br />
                Please save this page and show your booking number at check-in.
            </div>
        </div>

        <%-- สถานะการชำระเงิน + ขั้นตอนถัดไป (โอน: ได้รับสลิป/รอตรวจ · ออนไลน์: ชำระแล้ว/รอชำระ) --%>
        <asp:Literal ID="litNextSteps" runat="server" />

        <div class="main-grid">
            <!-- Left Column -->
            <div class="left-column">
                <!-- Basic Information -->
                <div class="info-card">
                    <h3>👤 ข้อมูลผู้จอง <span style="font-weight:normal; color:#999; font-size:0.85em;">Guest</span></h3>
                    <div class="info-row">
                        <span class="info-label">ชื่อ-นามสกุล:</span>
                        <span class="info-value"><asp:Label ID="Label2" runat="server"></asp:Label></span>
                    </div>
                    <div class="info-row">
                        <span class="info-label">Facebook / Line:</span>
                        <span class="info-value"><asp:Label ID="Label3" runat="server"></asp:Label></span>
                    </div>
                    <div class="info-row">
                        <span class="info-label">เบอร์โทรศัพท์:</span>
                        <span class="info-value"><asp:Label ID="Label4" runat="server" style="font-family: monospace;"></asp:Label></span>
                    </div>
                </div>

                <!-- Stay Information -->
                <div class="info-card">
                    <h3>🏨 ข้อมูลการเข้าพัก <span style="font-weight:normal; color:#999; font-size:0.85em;">Stay</span></h3>
                    <div class="info-row">
                        <span class="info-label">เช็คอิน:</span>
                        <span class="info-value"><asp:Label ID="Label5" runat="server" style="color: #388e3c; font-weight: bold;"></asp:Label></span>
                    </div>
                    <div class="info-row">
                        <span class="info-label">เช็คเอาท์:</span>
                        <span class="info-value"><asp:Label ID="Label6" runat="server" style="color: #388e3c; font-weight: bold;"></asp:Label></span>
                    </div>
                    <div class="info-row">
                        <span class="info-label">จำนวนคืน:</span>
                        <span class="info-value"><asp:Label ID="Label7" runat="server"></asp:Label></span>
                    </div>
                </div>

                <!-- Payment Summary -->
                <div class="info-card">
                    <h3>💰 สรุปการชำระเงิน <span style="font-weight:normal; color:#999; font-size:0.85em;">Payment</span></h3>
                    <div class="payment-card">
                        <div class="info-row">
                            <span class="info-label">ราคารวมทั้งหมด:</span>
                            <span class="info-value" style="color: #d32f2f; font-weight: bold;">
                                ฿<asp:Label ID="Label11" runat="server"></asp:Label>
                            </span>
                        </div>
                        <div class="info-row">
                            <span class="info-label">ชำระแล้ว:</span>
                            <span class="info-value" style="color: #388e3c; font-weight: bold;">
                                ฿<asp:Label ID="Label12" runat="server"></asp:Label>
                            </span>
                        </div>
                        <div class="info-row rc-due">
                            <span class="info-label">ยอดคงเหลือ:</span>
                            <span class="info-value" style="color: #e65100; font-weight: bold;">
                                ฿<asp:Label ID="Label13" runat="server"></asp:Label>
                            </span>
                        </div>
                    </div>
                </div>

                <!-- Remark -->
                <div class="detail-card">
                    <h3>📝 หมายเหตุ / คำขอพิเศษ</h3>
                    <div class="remark-box">
                        <asp:Label ID="Label14" runat="server" style="white-space: pre-line;"></asp:Label>
                    </div>
                </div>
            </div>

            <!-- Right Column -->
            <div class="right-column">
                <!-- Accommodation Details -->
                <div class="detail-card">
                    <h3>🛌 ห้องพัก <span style="font-weight:normal; color:#999; font-size:0.85em;">Room(s)</span></h3>
                    <div class="accommodation-box">
                        <asp:Label ID="Label8" runat="server" style="white-space: pre-line;"></asp:Label>
                    </div>
                </div>

                <!-- Rent Items and Product Charges -->
                <div class="detail-card">
                    <h3>🛍️ ของเช่า / ค่าบริการเพิ่มเติม</h3>
                    <div class="items-box">
                        <asp:Label ID="Label9" runat="server" style="white-space: pre-line;"></asp:Label>
                    </div>
                </div>

                <!-- Slip Image & Receipts -->
                <div class="slip-card">
                    <h3>📷 หลักฐานการชำระเงิน &amp; เอกสาร</h3>
                    <asp:Label ID="lblSlipCount" runat="server"
                        style="display: block; color: #2e7d32; font-weight: bold; margin-bottom: 6px; font-size: 0.9em;"></asp:Label>

                    <asp:Repeater ID="rptPaymentSlips" runat="server">
                        <ItemTemplate>
                            <div class="rc-slip-item">
                                <div><strong>📅</strong> <%# Eval("PaymentDate", "{0:dd/MM/yyyy HH:mm}") %>
                                    · <strong>฿<%# Eval("PaymentAmount", "{0:N2}") %></strong>
                                    · <%# Eval("PaymentType") %></div>
                                <div>
                                    <a href='<%# ResolveUrl("~/" + Eval("SlipFileURL").ToString()) %>' target="_blank" rel="noopener">
                                        🔗 ดูสลิปการโอนเงิน
                                    </a>
                                </div>
                            </div>
                        </ItemTemplate>
                        <FooterTemplate>
                            <div style="color: #999; font-style: italic; margin-top: 6px; font-size: 0.9em;">
                                <%# (((System.Web.UI.WebControls.Repeater)Container.Parent).Items.Count == 0) ? "ไม่พบสลิปการโอนเงิน" : "" %>
                            </div>
                        </FooterTemplate>
                    </asp:Repeater>

                    <asp:Image ID="Image1" runat="server" CssClass="slip-image" Visible="false" />

                    <!-- Receipt Links Section -->
                    <asp:Panel ID="pnlReceiptLinks" runat="server" Visible="false" CssClass="rc-docs">
                        <div style="font-size: 0.9em; color: #2e7d32; font-weight: bold; margin-bottom: 2px;">
                            🧾 เอกสารการชำระเงิน (ใบเสร็จ/ใบกำกับภาษี)
                        </div>
                        <asp:Repeater ID="rptReceipts" runat="server">
                            <ItemTemplate>
                                <div>
                                    <a href='<%# GetReceiptPDFUrl(Eval("ID"), Eval("UID"), Eval("Created_Date")) %>' target="_blank" rel="noopener">
                                        📄 <%# GetReceiptDocLabel(Eval("IsDeposit")) %> <%# Eval("ID") %> (<%# Eval("Total_Amount", "{0:N2}") %> บาท)
                                    </a>
                                </div>
                            </ItemTemplate>
                        </asp:Repeater>
                    </asp:Panel>
                </div>
            </div>
        </div>

        <!-- 🐾 สัตว์เลี้ยงเข้าพัก (แสดงเฉพาะใบจองที่มีสัตว์เลี้ยง — ตั้งค่าที่ Admin/Settings/PetStay) -->
        <asp:Panel ID="pnlPetInfo" runat="server" Visible="false" CssClass="detail-card rc-section">
            <h3>🐾 สัตว์เลี้ยงเข้าพัก <span style="font-weight:normal; color:#999; font-size:0.85em;">Pets</span></h3>
            <asp:Literal ID="litPetInfo" runat="server" />
        </asp:Panel>

        <!-- Booking Policies (ตั้งค่าที่ Admin/Settings/BookingPolicies) — พับไว้ แตะเพื่อดู -->
        <asp:Panel ID="pnlPolicies" runat="server" Visible="false" CssClass="detail-card rc-section">
            <details class="rc-collapse">
                <summary>📜 เงื่อนไขและนโยบายการจอง <span>(แตะเพื่อดู · Booking policies)</span></summary>
                <asp:Literal ID="litPolicies" runat="server" />
            </details>
        </asp:Panel>

        <!-- Action Buttons -->
        <div class="action-buttons">
            <button type="button" class="btn-print" onclick="captureAndDownload(this)">📥 บันทึกหน้านี้เป็นรูป (Save as image)</button>
            <button type="button" class="btn-print" onclick="window.print()">🖨️ พิมพ์ (Print)</button>
        </div>
    </div>

    <!-- Include html2canvas library -->
    <script src="https://cdnjs.cloudflare.com/ajax/libs/html2canvas/1.4.1/html2canvas.min.js"></script>

    <script>
        // Capture and download page as image
        function captureAndDownload(btn) {
            var element = document.querySelector('.confirmation-container');
            var buttons = document.querySelector('.action-buttons');
            if (!element) return;
            if (typeof html2canvas === 'undefined') {
                alert('ไม่สามารถสร้างรูปได้ในขณะนี้ — กรุณาใช้การจับภาพหน้าจอแทน');
                return;
            }

            // เปิดนโยบายที่พับไว้ให้ติดในรูป แล้วคืนสถานะเดิมหลังบันทึก
            var details = element.querySelectorAll('details');
            var wasOpen = [];
            for (var i = 0; i < details.length; i++) { wasOpen.push(details[i].open); details[i].open = true; }

            if (buttons) buttons.style.display = 'none';
            var originalText = btn ? btn.textContent : '';
            if (btn) { btn.textContent = '⏳ กำลังสร้างรูป...'; btn.disabled = true; }

            function restore() {
                for (var j = 0; j < details.length; j++) details[j].open = wasOpen[j];
                if (buttons) buttons.style.display = 'block';
                if (btn) { btn.textContent = originalText; btn.disabled = false; }
            }

            html2canvas(element, {
                scale: 2, // Higher quality
                useCORS: true,
                logging: false,
                backgroundColor: '#ffffff'
            }).then(function (canvas) {
                var link = document.createElement('a');
                link.download = 'การยืนยันการจอง_' + new Date().getTime() + '.png';
                link.href = canvas.toDataURL('image/png');
                link.click();
                restore();
            }).catch(function (error) {
                console.error('Error capturing page:', error);
                restore();
                alert('❌ เกิดข้อผิดพลาดในการบันทึกรูป กรุณาลองใหม่อีกครั้ง หรือจับภาพหน้าจอแทน');
            });
        }
    </script>
</asp:Content>
