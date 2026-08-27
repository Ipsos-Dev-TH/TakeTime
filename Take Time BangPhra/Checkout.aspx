<%@ Page Title="เช็คเอาท์" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Checkout.aspx.cs" Inherits="Take_Time_BangPhra.Checkout" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .checkout-container {
            max-width: 1000px;
            margin: 20px auto;
            padding: 20px;
        }

        .checkout-card {
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

        .info-section {
            display: grid;
            grid-template-columns: repeat(2, 1fr);
            gap: 15px;
            margin-bottom: 20px;
        }

        .info-item {
            display: flex;
            flex-direction: column;
            gap: 5px;
        }

        .info-label {
            font-weight: bold;
            color: #7f8c8d;
            font-size: 14px;
        }

        .info-value {
            color: #2c3e50;
            font-size: 16px;
        }

        .payment-status {
            background: linear-gradient(135deg, #56ab2f 0%, #a8e063 100%);
            color: white;
            padding: 15px;
            border-radius: 8px;
            margin-bottom: 20px;
            text-align: center;
        }

        .payment-status.incomplete {
            background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%);
        }

        .payment-amount {
            font-size: 24px;
            font-weight: bold;
        }

        .checklist-section {
            margin-top: 20px;
        }

        .checklist-item {
            padding: 15px;
            border: 1px solid #ddd;
            border-radius: 4px;
            margin-bottom: 10px;
            display: flex;
            align-items: center;
            gap: 15px;
            transition: background-color 0.3s;
        }

        .checklist-item:hover {
            background-color: #f8f9fa;
        }

        .checklist-item input[type="checkbox"] {
            width: 20px;
            height: 20px;
            cursor: pointer;
        }

        .checklist-label {
            flex: 1;
            font-size: 16px;
            color: #2c3e50;
        }

        .checklist-description {
            font-size: 13px;
            color: #7f8c8d;
            margin-top: 5px;
        }

        .rating-section {
            margin-top: 20px;
        }

        .rating-stars {
            display: flex;
            gap: 10px;
            justify-content: center;
            margin: 20px 0;
        }

        .star {
            font-size: 40px;
            color: #ddd;
            cursor: pointer;
            transition: color 0.3s;
        }

        .star.active {
            color: #f39c12;
        }

        .star:hover {
            color: #f39c12;
        }

        .notes-section {
            margin-top: 20px;
        }

        .form-control {
            width: 100%;
            padding: 10px;
            border: 1px solid #ddd;
            border-radius: 4px;
            font-size: 14px;
            min-height: 40px; /* ป้องกัน dropdown ถูกตัด */
        }

        .form-control:focus {
            border-color: #3498db;
            outline: none;
            box-shadow: 0 0 0 3px rgba(52, 152, 219, 0.1);
        }

        /* ✅ Fix dropdown height issue - ensure full visibility */
        select.form-control {
            height: auto !important;
            min-height: 40px;
            overflow: visible;
        }

        /* ✅ Ensure dropdown options are fully visible */
        .info-item {
            overflow: visible;
            position: relative;
            z-index: 1;
        }

        .btn-primary {
            background-color: #27ae60;
            color: white;
            padding: 15px 40px;
            border: none;
            border-radius: 4px;
            font-size: 18px;
            font-weight: bold;
            cursor: pointer;
            transition: background-color 0.3s;
            width: 100%;
            margin-top: 20px;
        }

        .btn-primary:hover {
            background-color: #229954;
        }

        .btn-primary:disabled {
            background-color: #95a5a6;
            cursor: not-allowed;
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
            margin-top: 10px;
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

        .summary-box {
            background: #ecf0f1;
            padding: 20px;
            border-radius: 8px;
            margin-top: 20px;
        }

        .summary-row {
            display: flex;
            justify-content: space-between;
            padding: 10px 0;
            border-bottom: 1px solid #bdc3c7;
        }

        .summary-row:last-child {
            border-bottom: none;
            font-size: 18px;
            font-weight: bold;
            color: #27ae60;
        }

        .icon-success {
            color: #27ae60;
            font-size: 20px;
        }

        .icon-warning {
            color: #f39c12;
            font-size: 20px;
        }
    </style>

    <div class="checkout-container">
        <h2 style="color: #2c3e50; margin-bottom: 30px;">
            <i class="fa fa-sign-out-alt"></i> เช็คเอาท์
        </h2>

        <!-- Alert Messages -->
        <asp:Panel ID="pnlSuccess" runat="server" CssClass="alert alert-success" Visible="false">
            <asp:Label ID="lblSuccess" runat="server"></asp:Label>
        </asp:Panel>

        <asp:Panel ID="pnlError" runat="server" CssClass="alert alert-danger" Visible="false">
            <asp:Label ID="lblError" runat="server"></asp:Label>
        </asp:Panel>

        <asp:Panel ID="pnlWarning" runat="server" CssClass="alert alert-warning" Visible="false">
            <asp:Label ID="lblWarning" runat="server"></asp:Label>
        </asp:Panel>

        <!-- Reservation Information -->
        <div class="checkout-card">
            <div class="card-header">
                <i class="fa fa-info-circle"></i> ข้อมูลการจอง
            </div>

            <div class="info-section">
                <div class="info-item">
                    <span class="info-label">รหัสการจอง</span>
                    <span class="info-value"><asp:Label ID="lblReservationID" runat="server"></asp:Label></span>
                </div>

                <div class="info-item">
                    <span class="info-label">ชื่อลูกค้า</span>
                    <span class="info-value"><asp:Label ID="lblCustomerName" runat="server"></asp:Label></span>
                </div>

                <div class="info-item">
                    <span class="info-label">เบอร์โทร</span>
                    <span class="info-value"><asp:Label ID="lblCustomerPhone" runat="server"></asp:Label></span>
                </div>

                <div class="info-item">
                    <span class="info-label">ห้องพัก</span>
                    <span class="info-value"><asp:Label ID="lblAccommodation" runat="server"></asp:Label></span>
                </div>

                <div class="info-item">
                    <span class="info-label">วันเช็คอิน</span>
                    <span class="info-value"><asp:Label ID="lblCheckinDate" runat="server"></asp:Label></span>
                </div>

                <div class="info-item">
                    <span class="info-label">วันเช็คเอาท์</span>
                    <span class="info-value"><asp:Label ID="lblCheckoutDate" runat="server"></asp:Label></span>
                </div>
            </div>
        </div>

        <!-- Payment Status -->
        <asp:Panel ID="pnlPaymentComplete" runat="server" CssClass="payment-status" Visible="false">
            <i class="fa fa-check-circle" style="font-size: 30px;"></i>
            <div style="margin-top: 10px;">ชำระเงินครบถ้วนแล้ว</div>
            <div class="payment-amount">
                <asp:Label ID="lblTotalPaid" runat="server"></asp:Label> บาท
            </div>
        </asp:Panel>

        <asp:Panel ID="pnlPaymentIncomplete" runat="server" CssClass="payment-status incomplete" Visible="false">
            <i class="fa fa-exclamation-triangle" style="font-size: 30px;"></i>
            <div style="margin-top: 10px;">ยังชำระเงินไม่ครบ</div>
            <div class="payment-amount">
                คงเหลือ: <asp:Label ID="lblRemainingBalance" runat="server"></asp:Label> บาท
            </div>
            <div style="margin-top: 10px; font-size: 14px;">
                💡 กรุณาไปชำระเงินที่หน้า <strong>Reserve (โหมดแก้ไข)</strong> ให้ครบก่อนเช็คเอาท์
            </div>
            <div style="margin-top: 10px;">
                <asp:HyperLink ID="lnkGoToReserve" runat="server" CssClass="btn-secondary" style="background-color: #3498db; color: white; padding: 10px 20px; border-radius: 4px; text-decoration: none; display: inline-block;">
                    <i class="fa fa-edit"></i> ไปชำระเงินที่หน้า Reserve
                </asp:HyperLink>
            </div>
        </asp:Panel>

        <!-- วงเงินประกันความเสียหาย (แสดงเฉพาะเมื่อการจองนี้มีวงเงินกันไว้) -->
        <asp:Panel ID="pnlSecurityHold" runat="server" CssClass="checkout-card" Visible="false"
            style="border-left:4px solid #1d6fb8;">
            <h3 style="margin:0 0 6px;">🛡 วงเงินประกันความเสียหาย</h3>
            <div style="color:#5a6b62;font-size:13.5px;margin-bottom:10px;">
                <asp:Literal ID="litHoldInfo" runat="server" />
            </div>
            <asp:Literal ID="litHoldMsg" runat="server" />
            <asp:Panel ID="pnlHoldActions" runat="server">
                <div style="display:flex;gap:10px;flex-wrap:wrap;align-items:flex-end;">
                    <div>
                        <label style="display:block;font-weight:600;font-size:13px;margin-bottom:4px;">
                            ค่าเสียหายที่จะตัด (บาท)</label>
                        <asp:TextBox ID="txtCaptureAmount" runat="server" TextMode="Number" step="0.01"
                            style="padding:9px 11px;border:1.5px solid #dbe3de;border-radius:8px;width:150px;font-size:16px;" />
                    </div>
                    <div style="flex:1;min-width:180px;">
                        <label style="display:block;font-weight:600;font-size:13px;margin-bottom:4px;">
                            เหตุผล/รายละเอียดความเสียหาย</label>
                        <asp:TextBox ID="txtCaptureReason" runat="server" placeholder="เช่น ผ้าเช็ดตัวหาย 2 ผืน"
                            style="width:100%;padding:9px 11px;border:1.5px solid #dbe3de;border-radius:8px;font-size:14px;" />
                    </div>
                    <asp:Button ID="btnCaptureHold" runat="server" Text="💥 ตัดค่าเสียหาย"
                        OnClick="btnCaptureHold_Click" UseSubmitBehavior="false"
                        OnClientClick="if(!confirm('ตัดค่าเสียหายจากวงเงินประกันตามยอดที่กรอก? ส่วนที่เหลือจะคืนลูกค้าทันที'))return false;this.disabled=true;"
                        style="padding:11px 16px;border:0;border-radius:9px;background:#c0392b;color:#fff;font-weight:600;cursor:pointer;min-height:44px;" />
                    <asp:Button ID="btnReleaseHold" runat="server" Text="✅ คืนวงเงินทั้งหมด (ไม่มีความเสียหาย)"
                        OnClick="btnReleaseHold_Click" UseSubmitBehavior="false"
                        OnClientClick="if(!confirm('คืนวงเงินประกันทั้งหมดให้ลูกค้า?'))return false;this.disabled=true;"
                        style="padding:11px 16px;border:0;border-radius:9px;background:#1b7a4b;color:#fff;font-weight:600;cursor:pointer;min-height:44px;" />
                </div>
            </asp:Panel>
        </asp:Panel>

        <!-- Checkout Checklist -->
        <div class="checkout-card">
            <div class="card-header">
                <i class="fa fa-tasks"></i> รายการตรวจสอบก่อนเช็คเอาท์
            </div>

            <div class="checklist-section">
                <div class="checklist-item">
                    <asp:CheckBox ID="chkRoomCondition" runat="server" />
                    <div>
                        <div class="checklist-label">ตรวจสอบสภาพห้องพัก</div>
                        <div class="checklist-description">
                            ตรวจสอบความเสียหายของห้อง อุปกรณ์ และเฟอร์นิเจอร์
                        </div>
                        <div style="margin-top:6px;">
                            <label style="font-size:0.85em;color:#c0392b;">ค่าเสียหาย (บาท) — กรอกเมื่อพบความเสียหาย (ไม่ติ๊กช่องนี้):</label>
                            <asp:TextBox ID="txtDamageAmount" runat="server" TextMode="Number" step="0.01" min="0"
                                Style="max-width:160px;display:inline-block;" placeholder="0.00" />
                        </div>
                    </div>
                </div>

                <div class="checklist-item">
                    <asp:CheckBox ID="chkMissingItems" runat="server" />
                    <div>
                        <div class="checklist-label">ตรวจนับอุปกรณ์ครบถ้วน</div>
                        <div class="checklist-description">
                            ผ้าเช็ดตัว ผ้าปูที่นอน หมอน ไม้แขวนเสื้อ รีโมท ฯลฯ
                        </div>
                        <div style="margin-top:6px;">
                            <label style="font-size:0.85em;color:#c0392b;">ค่าอุปกรณ์สูญหาย (บาท) — กรอกเมื่อของหาย (ไม่ติ๊กช่องนี้):</label>
                            <asp:TextBox ID="txtMissingAmount" runat="server" TextMode="Number" step="0.01" min="0"
                                Style="max-width:160px;display:inline-block;" placeholder="0.00" />
                        </div>
                    </div>
                </div>

                <div class="checklist-item">
                    <asp:CheckBox ID="chkKeyReturn" runat="server" />
                    <div>
                        <div class="checklist-label">รับคืนกุญแจห้อง</div>
                        <div class="checklist-description">
                            ตรวจสอบกุญแจห้องพักและกุญแจอื่นๆ ที่ยืมไป
                        </div>
                    </div>
                </div>

                <div class="checklist-item">
                    <asp:CheckBox ID="chkCleaning" runat="server" />
                    <div>
                        <div class="checklist-label">ความสะอาดเบื้องต้น</div>
                        <div class="checklist-description">
                            ตรวจสอบความสะอาดภายในห้อง ขยะ และภาชนะอาหาร
                        </div>
                    </div>
                </div>

                <div class="checklist-item">
                    <asp:CheckBox ID="chkElectrical" runat="server" />
                    <div>
                        <div class="checklist-label">ปิดเครื่องใช้ไฟฟ้า</div>
                        <div class="checklist-description">
                            ตรวจสอบการปิดไฟ แอร์ พัดลม และเครื่องใช้ไฟฟ้าอื่นๆ
                        </div>
                    </div>
                </div>

                <div class="checklist-item">
                    <asp:CheckBox ID="chkPersonalItems" runat="server" />
                    <div>
                        <div class="checklist-label">ของส่วนตัวลูกค้า</div>
                        <div class="checklist-description">
                            แจ้งให้ลูกค้าตรวจสอบของส่วนตัวก่อนออกจากห้อง
                        </div>
                    </div>
                </div>
            </div>
        </div>

        <!-- Guest Satisfaction Rating -->
        <div class="checkout-card">
            <div class="card-header">
                <i class="fa fa-star"></i> ความพึงพอใจของลูกค้า
            </div>

            <div class="rating-section">
                <p style="text-align: center; color: #7f8c8d; margin-bottom: 10px;">
                    กรุณาให้คะแนนความพึงพอใจในการเข้าพัก
                </p>

                <div class="rating-stars" id="ratingStars">
                    <span class="star" data-rating="1">&#9733;</span>
                    <span class="star" data-rating="2">&#9733;</span>
                    <span class="star" data-rating="3">&#9733;</span>
                    <span class="star" data-rating="4">&#9733;</span>
                    <span class="star" data-rating="5">&#9733;</span>
                </div>

                <asp:HiddenField ID="hfRating" runat="server" Value="0" />

                <div style="text-align: center; color: #2c3e50; font-size: 18px; margin-top: 10px;">
                    <span id="ratingText">ยังไม่ได้ให้คะแนน</span>
                </div>
            </div>
        </div>

        <!-- Additional Notes -->
        <div class="checkout-card">
            <div class="card-header">
                <i class="fa fa-comment"></i> หมายเหตุและความคิดเห็น
            </div>

            <div class="notes-section">
                <asp:TextBox ID="txtNotes" runat="server" CssClass="form-control" TextMode="MultiLine"
                    Rows="4" placeholder="ระบุหมายเหตุเพิ่มเติม (ถ้ามี) เช่น ความเสียหาย ของหาย หรือคำแนะนำจากลูกค้า"></asp:TextBox>
            </div>
        </div>

        <!-- Summary Box -->
        <div class="summary-box">
            <div class="summary-row">
                <span>ยอดรวมทั้งหมด:</span>
                <span><asp:Label ID="lblTotalPrice" runat="server"></asp:Label> บาท</span>
            </div>
            <div class="summary-row">
                <span>ชำระแล้ว:</span>
                <span style="color: #27ae60;"><asp:Label ID="lblPaidAmount" runat="server"></asp:Label> บาท</span>
            </div>
            <div class="summary-row">
                <span>สถานะการชำระเงิน:</span>
                <span>
                    <asp:Label ID="lblPaymentStatus" runat="server"></asp:Label>
                </span>
            </div>
        </div>

        <!-- Action Buttons -->
        <asp:Button ID="btnCheckout" runat="server" Text="ยืนยันเช็คเอาท์" CssClass="btn-primary"
            OnClick="btnCheckout_Click" OnClientClick="return confirmCheckout();" />

        <div style="text-align: center;">
            <asp:HyperLink ID="lnkCancel" runat="server" NavigateUrl="~/ReserveTable.aspx" CssClass="btn-secondary">
                ยกเลิก
            </asp:HyperLink>
        </div>
    </div>

    <script type="text/javascript">
        // Rating system
        const stars = document.querySelectorAll('.star');
        const ratingField = document.getElementById('<%= hfRating.ClientID %>');
        const ratingText = document.getElementById('ratingText');

        const ratingLabels = {
            0: 'ยังไม่ได้ให้คะแนน',
            1: 'แย่มาก',
            2: 'ไม่ดี',
            3: 'ปานกลาง',
            4: 'ดี',
            5: 'ดีเยี่ยม'
        };

        stars.forEach(star => {
            star.addEventListener('click', function () {
                const rating = this.getAttribute('data-rating');
                ratingField.value = rating;
                updateStars(rating);
            });

            star.addEventListener('mouseenter', function () {
                const rating = this.getAttribute('data-rating');
                highlightStars(rating);
            });
        });

        document.getElementById('ratingStars').addEventListener('mouseleave', function () {
            const currentRating = ratingField.value;
            updateStars(currentRating);
        });

        function highlightStars(rating) {
            stars.forEach((star, index) => {
                if (index < rating) {
                    star.classList.add('active');
                } else {
                    star.classList.remove('active');
                }
            });
            ratingText.innerText = ratingLabels[rating];
        }

        function updateStars(rating) {
            highlightStars(rating);
        }

        function confirmCheckout() {
            if (ratingField.value == '0') {
                alert('กรุณาให้คะแนนความพึงพอใจก่อนเช็คเอาท์');
                return false;
            }

            return confirm('ยืนยันการเช็คเอาท์หรือไม่? การดำเนินการนี้จะไม่สามารถยกเลิกได้');
        }
    </script>
</asp:Content>
