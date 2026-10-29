<%@ Page Title="" Async="true" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Reservation_Confirmed.aspx.cs" Inherits="Take_Time_BangPhra.Reservation_Confirmed" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <%-- สไตล์ทั้งหน้าอยู่ใน Content/reservation-confirmed.css (mobile-first · โหมดจับภาพ .rc-capturing · print) --%>
    <link rel="stylesheet" href="/Content/reservation-confirmed.css?v=1" />
    <br />
    <div class="rc-wrap">
    <%-- #rcCapture = กรอบที่ปุ่ม "โหลดรูปยืนยันการจอง" จับภาพ (html2canvas) — ปุ่มอยู่นอกกรอบ --%>
    <div class="confirmation-container" id="rcCapture" runat="server" ClientIDMode="Static">
        <!-- Header Section -->
        <div class="confirmation-header">
            <asp:Label ID="Label10" runat="server" Text="" CssClass="success-badge"></asp:Label>
            <h1>การยืนยันการจอง</h1>
            <p class="rc-h-en" lang="en">Reservation Confirmation</p>
            <div class="rc-code">รหัสการจอง (Booking No.) <b>#<asp:Label ID="Label1" runat="server"></asp:Label></b></div>
            <div class="instruction-text">
                <strong>📱 กรุณาบันทึกหน้านี้ไว้ และแสดงรหัสการจองตอนเช็คอิน</strong><br />
                Please save this page and show your booking number at check-in.
            </div>
        </div>

        <%-- 1) สถานะการชำระเงิน + ขั้นตอนถัดไป (โอน: ได้รับสลิป/รอตรวจ · ออนไลน์: ชำระแล้ว/รอชำระ) --%>
        <asp:Literal ID="litNextSteps" runat="server" />

        <div class="rc-grid">
            <%-- 2) รายละเอียดการจอง: ผู้จอง → วันเข้าพัก → ห้อง → ของเช่า/ค่าบริการ → สัตว์เลี้ยง → หมายเหตุ --%>
            <div class="rc-col rc-col-main">
                <div class="info-card">
                    <h3>👤 ข้อมูลผู้จอง <span class="rc-h-sub">Guest</span></h3>
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
                        <span class="info-value"><asp:Label ID="Label4" runat="server" CssClass="rc-mono"></asp:Label></span>
                    </div>
                </div>

                <div class="info-card">
                    <h3>🏨 ข้อมูลการเข้าพัก <span class="rc-h-sub">Stay</span></h3>
                    <div class="info-row">
                        <span class="info-label">เช็คอิน:</span>
                        <span class="info-value"><asp:Label ID="Label5" runat="server" CssClass="rc-date"></asp:Label></span>
                    </div>
                    <div class="info-row">
                        <span class="info-label">เช็คเอาท์:</span>
                        <span class="info-value"><asp:Label ID="Label6" runat="server" CssClass="rc-date"></asp:Label></span>
                    </div>
                    <div class="info-row">
                        <span class="info-label">จำนวนคืน:</span>
                        <span class="info-value"><asp:Label ID="Label7" runat="server"></asp:Label></span>
                    </div>
                </div>

                <div class="detail-card">
                    <h3>🛌 ห้องพัก <span class="rc-h-sub">Room(s)</span></h3>
                    <div class="accommodation-box">
                        <asp:Label ID="Label8" runat="server" style="white-space: pre-line;"></asp:Label>
                    </div>
                </div>

                <div class="detail-card">
                    <h3>🛍️ ของเช่า / ค่าบริการเพิ่มเติม <span class="rc-h-sub">Extras</span></h3>
                    <div class="items-box">
                        <asp:Label ID="Label9" runat="server" style="white-space: pre-line;"></asp:Label>
                    </div>
                </div>

                <!-- 🐾 สัตว์เลี้ยงเข้าพัก (แสดงเฉพาะใบจองที่มีสัตว์เลี้ยง — ตั้งค่าที่ Admin/Settings/PetStay) -->
                <asp:Panel ID="pnlPetInfo" runat="server" Visible="false" CssClass="detail-card">
                    <h3>🐾 สัตว์เลี้ยงเข้าพัก <span class="rc-h-sub">Pets</span></h3>
                    <asp:Literal ID="litPetInfo" runat="server" />
                </asp:Panel>

                <div class="detail-card">
                    <h3>📝 หมายเหตุ / คำขอพิเศษ <span class="rc-h-sub">Remarks</span></h3>
                    <div class="remark-box">
                        <asp:Label ID="Label14" runat="server" style="white-space: pre-line;"></asp:Label>
                    </div>
                </div>
            </div>

            <%-- 3) การชำระเงิน → 4) นโยบายการยกเลิกการจอง --%>
            <div class="rc-col rc-col-side">
                <div class="info-card">
                    <h3>💰 สรุปการชำระเงิน <span class="rc-h-sub">Payment</span></h3>
                    <div class="payment-card">
                        <div class="info-row">
                            <span class="info-label">ราคารวมทั้งหมด:</span>
                            <span class="info-value rc-amt-total">฿<asp:Label ID="Label11" runat="server"></asp:Label></span>
                        </div>
                        <div class="info-row">
                            <span class="info-label">ชำระแล้ว:</span>
                            <span class="info-value rc-amt-paid">฿<asp:Label ID="Label12" runat="server"></asp:Label></span>
                        </div>
                        <div class="info-row rc-due">
                            <span class="info-label">ยอดคงเหลือ:</span>
                            <span class="info-value rc-amt-due">฿<asp:Label ID="Label13" runat="server"></asp:Label></span>
                        </div>
                    </div>
                </div>

                <div class="slip-card">
                    <h3>📷 หลักฐานการชำระเงิน &amp; เอกสาร <span class="rc-h-sub">Slips &amp; documents</span></h3>
                    <asp:Label ID="lblSlipCount" runat="server" CssClass="rc-slip-count"></asp:Label>

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
                            <div class="rc-slip-none">
                                <%# (((System.Web.UI.WebControls.Repeater)Container.Parent).Items.Count == 0) ? "ไม่พบสลิปการโอนเงิน" : "" %>
                            </div>
                        </FooterTemplate>
                    </asp:Repeater>

                    <asp:Image ID="Image1" runat="server" CssClass="slip-image" Visible="false" />

                    <!-- Receipt Links Section -->
                    <asp:Panel ID="pnlReceiptLinks" runat="server" Visible="false" CssClass="rc-docs">
                        <div class="rc-docs-h">🧾 เอกสารการชำระเงิน (ใบเสร็จ/ใบกำกับภาษี)</div>
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

                <%-- นโยบายการยกเลิกการจอง — ข้อความเดียวกับหน้าจอง/หน้า /Policy/Cancellation (ศูนย์ตั้งค่า → นโยบายการจอง)
                     กฎสำคัญแสดงเสมอ ที่เหลือพับ "อ่านเพิ่มเติม" · ติดในรูปยืนยันการจองด้วย · ไม่มีข้อความ = ซ่อนทั้งส่วน --%>
                <asp:Panel ID="pnlCancelPolicy" runat="server" Visible="false" CssClass="rc-policy">
                    <h3>📅 นโยบายการยกเลิกการจอง <span class="rc-h-sub">Cancellation policy</span></h3>
                    <asp:Literal ID="litCancelPolicy" runat="server" />
                </asp:Panel>
            </div>
        </div>

        <!-- นโยบายอื่น ๆ (คืนเงิน / ข้อกำหนด / ความเป็นส่วนตัว) + ฉบับที่ลูกค้ายอมรับ — พับไว้ ไม่ติดในรูป -->
        <asp:Panel ID="pnlPolicies" runat="server" Visible="false" CssClass="detail-card rc-section rc-other-pol rc-no-capture">
            <details class="rc-collapse">
                <summary>📜 เงื่อนไขและนโยบายอื่น ๆ <span>(แตะเพื่อดู · Other booking policies)</span></summary>
                <asp:Literal ID="litPolicies" runat="server" />
            </details>
        </asp:Panel>

        <%-- 5) เช็คอิน / ติดต่อที่พัก --%>
        <div class="rc-card rc-contact">
            <h3>🕑 เช็คอิน &amp; ติดต่อที่พัก <span class="rc-h-sub">Check-in &amp; contact</span></h3>
            <div class="rc-contact-grid">
                <div><b>เช็คอิน:</b> <span>14:00 น. เป็นต้นไป (Check-in from 14:00)</span></div>
                <div><b>แสดงตอนเช็คอิน:</b> <span>รหัสการจอง + บัตรประชาชน/หนังสือเดินทาง</span></div>
                <div><b>โทร:</b> <a href="tel:0634161496">063-416-1496</a></div>
                <div><b>LINE:</b> <a href="https://lin.ee/U8N29MP" target="_blank" rel="noopener">@taketime</a></div>
                <div><b>ที่อยู่:</b> <span>Take Time Nature Resort 202/24 หมู่ 5 ต.บางพระ อ.ศรีราชา จ.ชลบุรี 20110</span></div>
            </div>
            <div class="rc-contact-btns rc-no-capture">
                <a class="rc-btn-call" href="tel:0634161496">📞 โทรหาที่พัก</a>
                <a class="rc-btn-line" href="https://lin.ee/U8N29MP" target="_blank" rel="noopener">💬 LINE @taketime</a>
                <a class="rc-btn-map" href="/Contact">📍 แผนที่ / ติดต่ออื่น ๆ</a>
            </div>
        </div>
    </div>

    <%-- ปุ่มหลัก — อยู่นอกกรอบที่จับภาพ · มือถือติดขอบล่าง --%>
    <div class="action-buttons rc-actions rc-no-capture">
        <button type="button" class="rc-btn rc-btn-primary" onclick="captureAndDownload(this)">📥 โหลดรูปยืนยันการจอง (Save as image)</button>
        <asp:HyperLink ID="lnkCalendar" runat="server" Visible="false" CssClass="rc-btn rc-btn-ghost" NavigateUrl="#">📆 เพิ่มลงปฏิทิน</asp:HyperLink>
        <button type="button" class="rc-btn rc-btn-ghost" onclick="window.print()">🖨️ พิมพ์ (Print)</button>
        <p class="rc-actions-hint">รูปยืนยันมีรหัสการจอง ยอดชำระ และนโยบายการยกเลิก — เก็บไว้แสดงตอนเช็คอิน</p>
    </div>
    </div>

    <!-- Include html2canvas library -->
    <script src="https://cdnjs.cloudflare.com/ajax/libs/html2canvas/1.4.1/html2canvas.min.js"></script>

    <script>
        (function () {
            var CAPTURE_ID = 'rcCapture';

            function captureRoot() {
                return document.getElementById(CAPTURE_ID) || document.querySelector('.confirmation-container');
            }

            function inNoCapture(el) {
                while (el && el !== document) {
                    if (el.classList && el.classList.contains('rc-no-capture')) return true;
                    el = el.parentNode;
                }
                return false;
            }

            // เปิดทุก <details> ในกรอบ (ยกเว้นส่วนที่ไม่ติดรูป) แล้วคืนฟังก์ชันสำหรับคืนสถานะเดิม
            function openDetails(root, skipNoCapture) {
                var list = root ? root.querySelectorAll('details') : [];
                var was = [];
                for (var i = 0; i < list.length; i++) {
                    was.push(list[i].open);
                    if (!skipNoCapture || !inNoCapture(list[i])) list[i].open = true;
                }
                return function () { for (var j = 0; j < list.length; j++) list[j].open = was[j]; };
            }

            function download(href, name, revoke) {
                var link = document.createElement('a');
                link.download = name;
                link.href = href;
                link.rel = 'noopener';
                document.body.appendChild(link);
                link.click();
                document.body.removeChild(link);
                if (revoke && window.URL && URL.revokeObjectURL) setTimeout(function () { URL.revokeObjectURL(href); }, 15000);
            }

            // บันทึกหน้านี้เป็นรูป — จับเฉพาะกรอบ #rcCapture (ไม่รวมปุ่ม/นโยบายอื่นที่พับ) และเปิด "อ่านเพิ่มเติม"
            // ของนโยบายการยกเลิกให้ติดในรูปครบ แล้วคืนสถานะเดิมทุกอย่างหลังบันทึก
            window.captureAndDownload = function (btn) {
                var element = captureRoot();
                if (!element) return;
                if (typeof html2canvas === 'undefined') {
                    alert('ไม่สามารถสร้างรูปได้ในขณะนี้ — กรุณาใช้การจับภาพหน้าจอแทน');
                    return;
                }
                var code = element.getAttribute('data-booking') || '';
                var restoreDetails = openDetails(element, true);
                var scrollY = window.pageYOffset || 0;
                document.body.className += ' rc-capturing';
                var originalHtml = btn ? btn.innerHTML : '';
                if (btn) { btn.textContent = '⏳ กำลังสร้างรูป...'; btn.disabled = true; }
                window.scrollTo(0, 0);

                function restore() {
                    document.body.className = document.body.className.replace(/\s*rc-capturing/g, '');
                    restoreDetails();
                    if (btn) { btn.innerHTML = originalHtml; btn.disabled = false; }
                    window.scrollTo(0, scrollY);
                }

                html2canvas(element, {
                    scale: 2, // คมชัดบนจอมือถือ
                    useCORS: true,
                    logging: false,
                    backgroundColor: '#ffffff'
                }).then(function (canvas) {
                    // ชื่อไฟล์ ASCII — Chromium ทิ้งชื่อไฟล์ภาษาไทยใน download attribute (กลายเป็น "download")
                    var name = 'TakeTime_Booking_' + (code ? code : new Date().getTime()) + '.png';
                    if (canvas.toBlob && window.URL && URL.createObjectURL) {
                        canvas.toBlob(function (blob) {
                            if (blob) download(URL.createObjectURL(blob), name, true);
                            else download(canvas.toDataURL('image/png'), name, false);
                            restore();
                        }, 'image/png');
                    } else {
                        download(canvas.toDataURL('image/png'), name, false);
                        restore();
                    }
                }).catch(function (error) {
                    if (window.console && console.error) console.error('Error capturing page:', error);
                    restore();
                    alert('❌ เกิดข้อผิดพลาดในการบันทึกรูป กรุณาลองใหม่อีกครั้ง หรือจับภาพหน้าจอแทน');
                });
            };

            // เพิ่มลงปฏิทิน (.ics ทั้งวัน: เช็คอิน → เช็คเอาท์) — ข้อมูลวันที่จาก data-* ที่ฝั่งเซิร์ฟเวอร์ตั้งให้
            window.rcAddToCalendar = function (a) {
                try {
                    var s = a.getAttribute('data-start'), e = a.getAttribute('data-end');
                    var code = a.getAttribute('data-code') || '', title = a.getAttribute('data-title') || 'เข้าพัก Take Time Nature Resort';
                    if (!s || !e || !window.Blob || !window.URL || !URL.createObjectURL) return true;
                    function esc(t) { return String(t).replace(/\\/g, '\\\\').replace(/;/g, '\\;').replace(/,/g, '\\,').replace(/\r?\n/g, '\\n'); }
                    var stamp = new Date().toISOString().replace(/[-:]/g, '').replace(/\.\d+Z$/, 'Z');
                    var lines = [
                        'BEGIN:VCALENDAR', 'VERSION:2.0', 'PRODID:-//Take Time Nature Resort//Booking//TH', 'CALSCALE:GREGORIAN',
                        'BEGIN:VEVENT',
                        'UID:booking-' + code + '@taketimebangphra.com',
                        'DTSTAMP:' + stamp,
                        'DTSTART;VALUE=DATE:' + s,
                        'DTEND;VALUE=DATE:' + e,
                        'SUMMARY:' + esc(title),
                        'DESCRIPTION:' + esc('รหัสการจอง #' + code + '\nเช็คอิน 14:00 น. เป็นต้นไป — แสดงรหัสการจองตอนเช็คอิน'),
                        'LOCATION:' + esc('Take Time Nature Resort, 202/24 หมู่ 5 ต.บางพระ อ.ศรีราชา จ.ชลบุรี 20110'),
                        'END:VEVENT', 'END:VCALENDAR'];
                    var blob = new Blob([lines.join('\r\n')], { type: 'text/calendar;charset=utf-8' });
                    download(URL.createObjectURL(blob), 'taketime-booking-' + (code || 'stay') + '.ics', true);
                } catch (err) {
                    if (window.console && console.error) console.error('calendar', err);
                }
                return false;
            };

            // พิมพ์: เปิดส่วนที่พับทั้งหมดให้ติดกระดาษ แล้วคืนสถานะเดิม
            var restorePrint = null;
            window.addEventListener('beforeprint', function () { restorePrint = openDetails(captureRoot(), false); });
            window.addEventListener('afterprint', function () { if (restorePrint) { restorePrint(); restorePrint = null; } });
        })();
    </script>
</asp:Content>
