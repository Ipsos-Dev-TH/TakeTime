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
            min-height: auto;
        }

        .confirmation-header {
            text-align: center;
            padding: 10px 15px;
            background: linear-gradient(135deg, #5d4037 0%, #8d6e63 100%);
            color: white;
        }

        .success-badge {
            background: #4caf50;
            color: white;
            padding: 4px 12px;
            border-radius: 12px;
            font-size: 0.8em;
            font-weight: bold;
            display: inline-block;
            margin-bottom: 5px;
        }

        .main-grid {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 0;
            min-height: auto;
        }

        .left-column {
            padding: 10px;
            background: #fafafa;
        }

        .right-column {
            padding: 10px;
            background: #f5f5f5;
            border-left: 2px solid #d7ccc8;
        }

        .info-card {
            background: white;
            padding: 8px 10px;
            border-radius: 6px;
            margin-bottom: 8px;
            border-left: 3px solid #8d6e63;
            box-shadow: 0 2px 5px rgba(0,0,0,0.05);
        }

        .info-card h3 {
            color: #5d4037;
            margin: 0 0 5px 0;
            font-size: 0.85em;
            border-bottom: 1px solid #d7ccc8;
            padding-bottom: 3px;
        }

        .info-row {
            display: flex;
            justify-content: space-between;
            margin-bottom: 3px;
            padding: 2px 0;
            font-size: 0.75em;
        }

        .info-label {
            font-weight: bold;
            color: #5d4037;
            min-width: 100px;
        }

        .info-value {
            color: #333;
            text-align: right;
            flex: 1;
        }
        
        .payment-card {
            background: linear-gradient(135deg, #fff8e1 0%, #ffecb3 100%);
            padding: 8px;
            border-radius: 6px;
            border: 1px solid #ffd54f;
            margin: 6px 0;
        }

        .slip-card {
            background: white;
            padding: 8px 10px;
            border-radius: 6px;
            text-align: center;
            margin-bottom: 8px;
            box-shadow: 0 2px 5px rgba(0,0,0,0.05);
        }

        .slip-image {
            width: 100%;
            max-height: 100px;
            border-radius: 4px;
            border: 1px solid #d7ccc8;
            object-fit: contain;
            background: #f9f9f9;
        }

        .detail-card {
            background: white;
            padding: 8px 10px;
            border-radius: 6px;
            margin-bottom: 8px;
            box-shadow: 0 1px 3px rgba(0,0,0,0.05);
        }

        .detail-card h3 {
            color: #5d4037;
            margin: 0 0 5px 0;
            font-size: 0.85em;
            border-bottom: 1px solid #e0e0e0;
            padding-bottom: 3px;
        }

        /* รายการที่พัก - แสดงทั้งหมดไม่ scroll */
        .accommodation-box {
            background: #f9f9f9;
            padding: 6px;
            border-radius: 4px;
            font-size: 0.7em;
            line-height: 1.4;
            border-left: 2px solid #8d6e63;
        }

        /* รายการของเช่า/สินค้า - ล็อคขนาดและมี scroll */
        .items-box {
            background: #f9f9f9;
            padding: 6px;
            border-radius: 4px;
            font-size: 0.7em;
            line-height: 1.4;
            max-height: 80px;
            overflow-y: auto;
            border-left: 2px solid #8d6e63;
        }

        /* หมายเหตุ */
        .remark-box {
            background: #fff3e0;
            padding: 6px;
            border-radius: 4px;
            font-size: 0.7em;
            line-height: 1.4;
            max-height: 60px;
            overflow-y: auto;
            border-left: 2px solid #ff9800;
        }
        
        .action-buttons {
            text-align: center;
            padding: 10px;
            background: white;
            border-top: 1px solid #d7ccc8;
        }

        .btn-receipt {
            background: linear-gradient(135deg, #4caf50 0%, #45a049 100%);
            color: white;
            padding: 6px 15px;
            border: none;
            border-radius: 12px;
            font-size: 0.75em;
            font-weight: bold;
            cursor: pointer;
            margin: 0 3px;
        }

        .btn-print {
            background: linear-gradient(135deg, #2196f3 0%, #1976d2 100%);
            color: white;
            padding: 6px 15px;
            border: none;
            border-radius: 12px;
            font-size: 0.75em;
            font-weight: bold;
            cursor: pointer;
            margin: 0 3px;
        }

        .instruction-text {
            text-align: center;
            color: rgba(255,255,255,0.9);
            font-size: 0.65em;
            margin-top: 3px;
        }

        .receipt-section {
            background: linear-gradient(135deg, #e8f5e8 0%, #f1f8e9 100%);
            padding: 6px;
            border-radius: 4px;
            border: 1px solid #c8e6c9;
            margin-top: 6px;
            font-size: 0.7em;
        }

        /* Custom scrollbar */
        .items-box::-webkit-scrollbar,
        .remark-box::-webkit-scrollbar {
            width: 3px;
        }

        .items-box::-webkit-scrollbar-track,
        .remark-box::-webkit-scrollbar-track {
            background: #f1f1f1;
        }

        .items-box::-webkit-scrollbar-thumb,
        .remark-box::-webkit-scrollbar-thumb {
            background: #bcaaa4;
        }
        
        @media print {
            .action-buttons, .instruction-text {
                display: none;
            }
        }
        
        /* Mobile Optimization */
        @media (max-width: 768px) {
            body {
                padding: 3px;
            }

            .confirmation-container {
                border-radius: 8px;
                min-height: auto;
            }

            .confirmation-header {
                padding: 8px 10px;
            }

            .confirmation-header h1 {
                font-size: 1em;
                margin: 2px 0;
            }

            .success-badge {
                font-size: 0.7em;
                padding: 3px 10px;
            }

            .main-grid {
                grid-template-columns: 1fr 1fr;
                min-height: auto;
                gap: 0;
            }

            .right-column {
                border-left: 1px solid #d7ccc8;
                border-top: none;
            }

            .left-column, .right-column {
                padding: 6px;
            }

            .info-card, .slip-card, .detail-card {
                padding: 6px 8px;
                margin-bottom: 6px;
            }

            .info-card h3, .slip-card h3, .detail-card h3 {
                font-size: 0.75em;
                margin-bottom: 4px;
            }

            .info-row {
                flex-direction: column;
                margin-bottom: 2px;
            }

            .info-label {
                min-width: auto;
                margin-bottom: 1px;
                font-size: 0.65em;
            }

            .info-value {
                text-align: left;
                font-size: 0.7em;
            }

            .slip-image {
                max-height: 80px;
            }

            .btn-receipt, .btn-print {
                padding: 5px 12px;
                font-size: 0.7em;
                margin: 2px;
            }

            .accommodation-box {
                font-size: 0.65em;
                line-height: 1.3;
                padding: 5px;
            }

            .items-box {
                max-height: 60px;
                font-size: 0.65em;
                line-height: 1.3;
                padding: 5px;
            }

            .remark-box {
                max-height: 50px;
                font-size: 0.65em;
                line-height: 1.3;
                padding: 5px;
            }

            .instruction-text {
                font-size: 0.6em;
            }

            .action-buttons {
                padding: 8px;
            }
        }
    </style>
    <br /><br />
    <div class="confirmation-container">
        <!-- Header Section -->
        <div class="confirmation-header">
            <div class="success-badge">
                <asp:Label ID="Label10" runat="server" Text=""></asp:Label>
            </div>
            <h1 style="margin: 3px 0; font-size: 1.3em;">การยืนยันการจอง</h1>
            <p style="margin: 0; opacity: 0.9; font-size: 0.8em;">Reservation Confirmation</p>
            <div class="instruction-text">
                <strong>📱 กรุณาบันทึกภาพหน้าจอนี้เพื่อใช้ยืนยันในการลงทะเบียนเข้าพัก</strong>
            </div>
        </div>

        <div class="main-grid">
            <!-- Left Column -->
            <div class="left-column">
                <!-- Basic Information -->
                <div class="info-card">
                    <h3>📋 ข้อมูลการจอง</h3>
                    <div class="info-row">
                        <span class="info-label">รหัสการจอง:</span>
                        <span class="info-value"><asp:Label ID="Label1" runat="server" style="font-weight: bold; color: #d32f2f;"></asp:Label></span>
                    </div>
                    <div class="info-row">
                        <span class="info-label">ชื่อ-นามสกุล:</span>
                        <span class="info-value"><asp:Label ID="Label2" runat="server"></asp:Label></span>
                    </div>
                    <div class="info-row">
                        <span class="info-label">ชื่อเล่น:</span>
                        <span class="info-value"><asp:Label ID="Label3" runat="server"></asp:Label></span>
                    </div>
                    <div class="info-row">
                        <span class="info-label">เบอร์โทรศัพท์:</span>
                        <span class="info-value"><asp:Label ID="Label4" runat="server" style="font-family: monospace;"></asp:Label></span>
                    </div>
                </div>

                <!-- Stay Information -->
                <div class="info-card">
                    <h3>🏨 ข้อมูลการเข้าพัก</h3>
                    <div class="info-row">
                        <span class="info-label">วันที่เช็คอิน:</span>
                        <span class="info-value"><asp:Label ID="Label5" runat="server" style="color: #388e3c;"></asp:Label></span>
                    </div>
                    <div class="info-row">
                        <span class="info-label">วันที่เช็คเอาท์:</span>
                        <span class="info-value"><asp:Label ID="Label6" runat="server" style="color: #388e3c;"></asp:Label></span>
                    </div>
                    <div class="info-row">
                        <span class="info-label">จำนวนคืน:</span>
                        <span class="info-value"><asp:Label ID="Label7" runat="server"></asp:Label></span>
                    </div>
                </div>

                <!-- Payment Summary -->
                <div class="info-card">
                    <h3>💰 สรุปการชำระเงิน</h3>
                    <div class="payment-card">
                        <div class="info-row">
                            <span class="info-label">ราคารวมทั้งหมด:</span>
                            <span class="info-value" style="color: #d32f2f;">
                                ฿<asp:Label ID="Label11" runat="server" style="font-weight: bold;"></asp:Label>
                            </span>
                        </div>
                        <div class="info-row">
                            <span class="info-label">ยอดมัดจำที่ชำระแล้ว:</span>
                            <span class="info-value" style="color: #388e3c;">
                                ฿<asp:Label ID="Label12" runat="server" style="font-weight: bold;"></asp:Label>
                            </span>
                        </div>
                        <div class="info-row">
                            <span class="info-label">ยอดคงเหลือ:</span>
                            <span class="info-value" style="color: #f57c00; font-weight: bold;">
                                ฿<asp:Label ID="Label13" runat="server"></asp:Label>
                            </span>
                        </div>
                    </div>
                </div>

                <!-- Remark -->
                <div class="detail-card">
                    <h3>📝 หมายเหตุ</h3>
                    <div class="remark-box">
                        <asp:Label ID="Label14" runat="server" style="white-space: pre-line;"></asp:Label>
                    </div>
                </div>
            </div>

            <!-- Right Column -->
            <div class="right-column">
                <!-- Accommodation Details -->
                <div class="detail-card">
                    <h3>🛌 รายการที่พัก</h3>
                    <div class="accommodation-box">
                        <asp:Label ID="Label8" runat="server" style="white-space: pre-line;"></asp:Label>
                    </div>
                </div>

                <!-- Rent Items and Product Charges -->
                <div class="detail-card">
                    <h3>🛍️ รายการของเช่า/สินค้า</h3>
                    <div class="items-box">
                        <asp:Label ID="Label9" runat="server" style="white-space: pre-line;"></asp:Label>
                    </div>
                </div>

                <!-- Slip Image & Receipts -->
                <div class="slip-card">
                    <h3>📷 สลิปการโอนเงิน & ใบกำกับภาษี</h3>
                    <asp:Label ID="lblSlipCount" runat="server"
                        style="display: block; color: #4caf50; font-weight: bold; margin-bottom: 6px; font-size: 0.7em;"></asp:Label>

                    <asp:Repeater ID="rptPaymentSlips" runat="server">
                        <ItemTemplate>
                            <div style="margin-bottom: 6px; padding: 5px; background: #f5f5f5; border-radius: 4px; font-size: 0.7em;">
                                <div style="margin-bottom: 2px; font-size: 0.85em;">
                                    <strong>📅 วันที่:</strong> <%# Eval("PaymentDate", "{0:dd/MM/yyyy HH:mm}") %>
                                </div>
                                <div style="margin-bottom: 2px; font-size: 0.85em;">
                                    <strong>💰 จำนวน:</strong> <%# Eval("PaymentAmount", "{0:N2}") %> บาท
                                </div>
                                <div style="margin-bottom: 2px; font-size: 0.85em;">
                                    <strong>📝 ประเภท:</strong> <%# Eval("PaymentType") %>
                                </div>
                                <div>
                                    <a href='<%# ResolveUrl("~/" + Eval("SlipFileURL").ToString()) %>'
                                       target="_blank"
                                       style="color: #1976d2; text-decoration: none; font-weight: bold; font-size: 0.85em;">
                                        🔗 ดูสลิปการโอนเงิน
                                    </a>
                                </div>
                            </div>
                        </ItemTemplate>
                        <FooterTemplate>
                            <div style="color: #999; font-style: italic; margin-top: 6px; font-size: 0.7em;">
                                <%# (((System.Web.UI.WebControls.Repeater)Container.Parent).Items.Count == 0) ? "ไม่พบสลิปการโอนเงิน" : "" %>
                            </div>
                        </FooterTemplate>
                    </asp:Repeater>

                    <asp:Image ID="Image1" runat="server" CssClass="slip-image" Visible="false" />

                    <!-- Receipt Links Section -->
                    <asp:Panel ID="pnlReceiptLinks" runat="server" Visible="false"
                        style="margin-top: 8px; padding: 6px; background: linear-gradient(135deg, #e8f5e8 0%, #f1f8e9 100%); border-radius: 4px; border: 1px solid #c8e6c9;">
                        <div style="font-size: 0.7em; color: #2e7d32; font-weight: bold; margin-bottom: 4px;">
                            🧾 เอกสารการชำระเงิน (ใบเสร็จ/ใบกำกับภาษี)
                        </div>
                        <asp:Repeater ID="rptReceipts" runat="server">
                            <ItemTemplate>
                                <div style="margin: 2px 0;">
                                    <a href='<%# GetReceiptPDFUrl(Eval("ID"), Eval("UID"), Eval("Created_Date")) %>'
                                       target="_blank"
                                       style="color: #4caf50; text-decoration: none; font-weight: bold; font-size: 0.75em;">
                                        📄 <%# GetReceiptDocLabel(Eval("IsDeposit")) %> <%# Eval("ID") %> (<%# Eval("Total_Amount", "{0:N2}") %> บาท)
                                    </a>
                                </div>
                            </ItemTemplate>
                        </asp:Repeater>
                    </asp:Panel>

                    <p style="color: #666; margin: 4px 0 0 0; font-size: 0.65em;">
                        <em>หลักฐานการชำระเงิน</em>
                    </p>
                </div>
            </div>
        </div>

        <!-- Action Buttons -->
        <div class="action-buttons">
            <button type="button" class="btn-print" onclick="captureAndDownload()">🖨️ บันทึกหน้านี้เป็นรูป</button>
        </div>
    </div>

    <!-- Include html2canvas library -->
    <script src="https://cdnjs.cloudflare.com/ajax/libs/html2canvas/1.4.1/html2canvas.min.js"></script>

    <script>
        // Add some interactive effects
        document.addEventListener('DOMContentLoaded', function () {
            // Add fade-in animation to cards
            const cards = document.querySelectorAll('.info-card, .detail-card, .slip-card');
            cards.forEach((card, index) => {
                card.style.opacity = '0';
                card.style.transform = 'translateY(5px)';
                card.style.transition = 'all 0.3s ease';

                setTimeout(() => {
                    card.style.opacity = '1';
                    card.style.transform = 'translateY(0)';
                }, index * 50);
            });
        });

        // Capture and download page as image
        function captureAndDownload() {
            const element = document.querySelector('.confirmation-container');
            const buttons = document.querySelector('.action-buttons');

            // Hide buttons before capture
            if (buttons) buttons.style.display = 'none';

            // Show loading message
            const originalText = event.target.textContent;
            event.target.textContent = '⏳ กำลังสร้างรูป...';
            event.target.disabled = true;

            html2canvas(element, {
                scale: 2, // Higher quality
                useCORS: true,
                logging: false,
                backgroundColor: '#ffffff'
            }).then(canvas => {
                // Convert to image and download
                const link = document.createElement('a');
                const timestamp = new Date().getTime();
                link.download = 'การยืนยันการจอง_' + timestamp + '.png';
                link.href = canvas.toDataURL('image/png');
                link.click();

                // Restore buttons
                if (buttons) buttons.style.display = 'block';
                event.target.textContent = originalText;
                event.target.disabled = false;

                // Show success message
                alert('✅ บันทึกรูปเรียบร้อยแล้ว!');
            }).catch(error => {
                console.error('Error capturing page:', error);

                // Restore buttons
                if (buttons) buttons.style.display = 'block';
                event.target.textContent = originalText;
                event.target.disabled = false;

                alert('❌ เกิดข้อผิดพลาดในการบันทึกรูป กรุณาลองใหม่อีกครั้ง');
            });
        }
    </script>
</asp:Content>