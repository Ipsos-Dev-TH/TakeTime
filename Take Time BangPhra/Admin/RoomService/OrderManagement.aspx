<%@ Page Title="Order Management" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="OrderManagement.aspx.cs" Inherits="Take_Time_BangPhra.Admin.RoomService.OrderManagement" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .order-management {
            max-width: 1400px;
            margin: 0 auto;
            padding: 20px;
        }

        .page-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 25px;
            flex-wrap: wrap;
            gap: 15px;
        }

        .page-header h2 {
            margin: 0;
            color: #333;
            font-size: 24px;
            display: flex;
            align-items: center;
            gap: 10px;
        }

        .page-header h2 i { color: #ff9800; }

        .unread-badge {
            background: #f44336;
            color: white;
            padding: 5px 15px;
            border-radius: 20px;
            font-size: 14px;
            font-weight: 600;
            animation: pulse 1s infinite;
        }

        @keyframes pulse {
            0%, 100% { transform: scale(1); }
            50% { transform: scale(1.05); }
        }

        /* Status Badges */
        .status-badge {
            padding: 4px 10px;
            border-radius: 12px;
            font-size: 12px;
            font-weight: 600;
            display: inline-block;
        }
        .status-pending { background: #fff3e0; color: #e65100; }
        .status-confirmed { background: #e3f2fd; color: #1565c0; }
        .status-preparing { background: #f3e5f5; color: #7b1fa2; }
        .status-delivered { background: #e8f5e9; color: #2e7d32; }
        .status-cancelled { background: #ffebee; color: #c62828; }

        /* Order Layout */
        .order-layout {
            display: grid;
            grid-template-columns: 400px 1fr;
            gap: 20px;
            height: calc(100vh - 200px);
            min-height: 500px;
        }

        /* Order List */
        .order-list-container {
            background: white;
            border-radius: 15px;
            box-shadow: 0 3px 15px rgba(0,0,0,0.08);
            overflow: hidden;
            display: flex;
            flex-direction: column;
        }

        .order-list-header {
            padding: 15px 20px;
            background: linear-gradient(135deg, #ff9800, #f57c00);
            color: white;
            font-weight: 600;
            display: flex;
            justify-content: space-between;
            align-items: center;
        }

        .filter-tabs {
            display: flex;
            gap: 5px;
        }

        .filter-tab {
            padding: 5px 10px;
            border: none;
            background: rgba(255,255,255,0.2);
            color: white;
            border-radius: 15px;
            cursor: pointer;
            font-size: 12px;
        }

        .filter-tab.active { background: white; color: #ff9800; }

        .order-list {
            flex: 1;
            overflow-y: auto;
        }

        .order-item {
            padding: 15px 20px;
            border-bottom: 1px solid #eee;
            cursor: pointer;
            transition: all 0.3s ease;
            position: relative;
        }

        .order-item:hover { background: #f8f9fa; }
        .order-item.active { background: #fff3e0; border-left: 4px solid #ff9800; }
        .order-item.new { background: #fff8e1; }
        .order-item.new::before {
            content: 'NEW';
            position: absolute;
            right: 10px;
            top: 10px;
            background: #f44336;
            color: white;
            padding: 2px 8px;
            border-radius: 10px;
            font-size: 10px;
            font-weight: 600;
        }

        .order-item-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 8px;
        }

        .order-number {
            font-weight: 600;
            color: #ff9800;
            font-size: 14px;
        }

        .order-time {
            font-size: 12px;
            color: #999;
        }

        .order-room {
            font-weight: 600;
            color: #333;
            margin-bottom: 5px;
        }

        .order-guest {
            font-size: 13px;
            color: #666;
            margin-bottom: 5px;
        }

        .order-total {
            font-weight: 600;
            color: #2e7d32;
        }

        /* Order Detail */
        .order-detail-container {
            background: white;
            border-radius: 15px;
            box-shadow: 0 3px 15px rgba(0,0,0,0.08);
            display: flex;
            flex-direction: column;
            overflow: hidden;
        }

        .order-detail-header {
            padding: 20px;
            background: #f8f9fa;
            border-bottom: 1px solid #eee;
        }

        .order-info-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 15px;
            margin-bottom: 15px;
        }

        .info-item {
            display: flex;
            flex-direction: column;
        }

        .info-label {
            font-size: 12px;
            color: #999;
            margin-bottom: 3px;
        }

        .info-value {
            font-weight: 600;
            color: #333;
        }

        /* Order Items */
        .order-items-area {
            flex: 1;
            padding: 20px;
            overflow-y: auto;
        }

        .order-items-table {
            width: 100%;
            border-collapse: collapse;
        }

        .order-items-table th,
        .order-items-table td {
            padding: 12px;
            text-align: left;
            border-bottom: 1px solid #eee;
        }

        .order-items-table th {
            background: #f5f5f5;
            font-weight: 600;
            color: #666;
        }

        .item-total {
            font-weight: 600;
            color: #2e7d32;
        }

        /* Action Buttons */
        .action-area {
            padding: 15px 20px;
            background: white;
            border-top: 1px solid #eee;
            display: flex;
            gap: 10px;
            flex-wrap: wrap;
        }

        .btn-action {
            padding: 10px 20px;
            border: none;
            border-radius: 8px;
            font-size: 14px;
            font-weight: 600;
            cursor: pointer;
            transition: all 0.3s ease;
            display: flex;
            align-items: center;
            gap: 8px;
        }

        .btn-claim { background: #4CAF50; color: white; }
        .btn-claim:hover { background: #43A047; }

        .btn-preparing { background: #9c27b0; color: white; }
        .btn-preparing:hover { background: #8e24aa; }

        .btn-delivered { background: #2196F3; color: white; }
        .btn-delivered:hover { background: #1e88e5; }

        .btn-cancel { background: #f44336; color: white; }
        .btn-cancel:hover { background: #e53935; }

        .btn-disabled {
            background: #e0e0e0 !important;
            color: #999 !important;
            cursor: not-allowed !important;
        }

        /* Empty State */
        .empty-state {
            display: flex;
            flex-direction: column;
            align-items: center;
            justify-content: center;
            height: 100%;
            color: #999;
        }

        .empty-state i {
            font-size: 60px;
            margin-bottom: 20px;
            opacity: 0.3;
        }

        /* Alert Notification */
        .alert-overlay {
            display: none;
            position: fixed;
            top: 0;
            left: 0;
            right: 0;
            bottom: 0;
            background: rgba(0,0,0,0.7);
            z-index: 9999;
            align-items: center;
            justify-content: center;
        }

        .alert-overlay.show { display: flex; }

        .alert-box {
            background: white;
            padding: 30px 40px;
            border-radius: 20px;
            text-align: center;
            max-width: 500px;
            max-height: 80vh;
            overflow-y: auto;
        }

        .alert-box i.main-icon {
            font-size: 50px;
            color: #ff9800;
            margin-bottom: 15px;
            animation: alertPulse 1s infinite;
        }

        @keyframes alertPulse {
            0%, 100% { opacity: 1; }
            50% { opacity: 0.5; }
        }

        /* Order Card in Alert */
        .order-card {
            background: #f8f9fa;
            border-radius: 10px;
            padding: 15px;
            margin: 10px 0;
            border-left: 4px solid #ff9800;
            text-align: left;
        }

        .order-card .room-name {
            font-weight: 600;
            color: #ff9800;
            font-size: 16px;
        }

        .order-card .guest-name {
            color: #333;
            margin: 5px 0;
        }

        .order-card .order-items-preview {
            color: #666;
            font-size: 13px;
        }

        .order-card .order-amount {
            font-weight: 600;
            color: #2e7d32;
            margin-top: 8px;
        }

        .btn-claim-order {
            background: linear-gradient(135deg, #4CAF50, #45a049);
            color: white;
            border: none;
            padding: 10px 25px;
            border-radius: 20px;
            cursor: pointer;
            font-weight: 600;
            margin-top: 10px;
        }

        /* Sound Toggle */
        .sound-toggle {
            display: flex;
            align-items: center;
            gap: 10px;
        }

        .sound-toggle label {
            font-size: 14px;
            color: #666;
        }

        /* Mobile Responsive */
        @media (max-width: 992px) {
            .order-layout {
                grid-template-columns: 1fr;
                height: auto;
            }

            .order-list-container { max-height: 300px; }
            .order-detail-container { min-height: 500px; }
        }
    </style>

    <div class="order-management">
        <!-- Page Header -->
        <div class="page-header">
            <h2><i class="fas fa-concierge-bell"></i> Room Service Orders</h2>
            <div style="display: flex; gap: 15px; align-items: center;">
                <div class="sound-toggle">
                    <input type="checkbox" id="chkSound" checked onchange="toggleSound()">
                    <label for="chkSound"><i class="fas fa-volume-up"></i> เสียงแจ้งเตือน</label>
                </div>
                <asp:Panel ID="pnlPendingBadge" runat="server" CssClass="unread-badge" Visible="false">
                    <i class="fas fa-bell"></i> <asp:Label ID="lblPendingCount" runat="server">0</asp:Label> รายการรอดำเนินการ
                </asp:Panel>
            </div>
        </div>

        <!-- Order Layout -->
        <div class="order-layout">
            <!-- Order List -->
            <div class="order-list-container">
                <div class="order-list-header">
                    <span><i class="fas fa-list"></i> รายการสั่งซื้อ</span>
                    <div class="filter-tabs">
                        <button type="button" class="filter-tab active" onclick="filterOrders('all')">ทั้งหมด</button>
                        <button type="button" class="filter-tab" onclick="filterOrders('pending')">รอรับ</button>
                        <button type="button" class="filter-tab" onclick="filterOrders('confirmed')">รับแล้ว</button>
                    </div>
                </div>
                <div class="order-list" id="orderList">
                    <asp:Repeater ID="rptOrderList" runat="server">
                        <ItemTemplate>
                            <div class='order-item <%# Eval("Order_Status").ToString() == "PENDING" ? "new" : "" %>'
                                 data-status='<%# Eval("Order_Status") %>'
                                 onclick="selectOrder(<%# Eval("ID") %>)">
                                <div class="order-item-header">
                                    <span class="order-number">#<%# Eval("Order_Number") %></span>
                                    <span class="order-time"><%# Eval("Order_Date", "{0:HH:mm}") %></span>
                                </div>
                                <div class="order-room">
                                    <i class="fas fa-door-open"></i> <%# Eval("RoomName") %>
                                </div>
                                <div class="order-guest">
                                    <i class="fas fa-user"></i> <%# Eval("GuestName") %>
                                </div>
                                <div style="display: flex; justify-content: space-between; align-items: center;">
                                    <span class='status-badge status-<%# Eval("Order_Status").ToString().ToLower() %>'>
                                        <%# GetStatusText(Eval("Order_Status").ToString()) %>
                                    </span>
                                    <span class="order-total">฿<%# Eval("Total_Amount", "{0:N0}") %></span>
                                </div>
                            </div>
                        </ItemTemplate>
                    </asp:Repeater>

                    <asp:Panel ID="pnlNoOrders" runat="server" Visible="false">
                        <div class="empty-state" style="padding: 40px;">
                            <i class="fas fa-inbox"></i>
                            <p>ยังไม่มีรายการสั่งซื้อ</p>
                        </div>
                    </asp:Panel>
                </div>
            </div>

            <!-- Order Detail -->
            <div class="order-detail-container">
                <asp:Panel ID="pnlOrderDetail" runat="server" Visible="false" style="display: flex; flex-direction: column; height: 100%;">
                    <!-- Header -->
                    <div class="order-detail-header">
                        <div class="order-info-grid">
                            <div class="info-item">
                                <span class="info-label">เลขที่ออเดอร์</span>
                                <span class="info-value">#<asp:Label ID="lblOrderNumber" runat="server"></asp:Label></span>
                            </div>
                            <div class="info-item">
                                <span class="info-label">ห้อง</span>
                                <span class="info-value"><asp:Label ID="lblRoomName" runat="server"></asp:Label></span>
                            </div>
                            <div class="info-item">
                                <span class="info-label">แขก</span>
                                <span class="info-value"><asp:Label ID="lblGuestName" runat="server"></asp:Label></span>
                            </div>
                            <div class="info-item">
                                <span class="info-label">เบอร์โทร</span>
                                <span class="info-value"><asp:Label ID="lblPhone" runat="server"></asp:Label></span>
                            </div>
                            <div class="info-item">
                                <span class="info-label">วันเวลาสั่ง</span>
                                <span class="info-value"><asp:Label ID="lblOrderTime" runat="server"></asp:Label></span>
                            </div>
                            <div class="info-item">
                                <span class="info-label">สถานะ</span>
                                <span class="info-value"><asp:Label ID="lblStatus" runat="server"></asp:Label></span>
                            </div>
                            <div class="info-item">
                                <span class="info-label">การชำระเงิน</span>
                                <span class="info-value"><asp:Label ID="lblPayment" runat="server"></asp:Label></span>
                            </div>
                            <div class="info-item">
                                <span class="info-label">คำแนะนำการจัดส่ง</span>
                                <span class="info-value"><asp:Label ID="lblDeliveryNote" runat="server"></asp:Label></span>
                            </div>
                        </div>
                    </div>

                    <!-- Order Items -->
                    <div class="order-items-area">
                        <h4 style="margin: 0 0 15px 0; color: #333;"><i class="fas fa-shopping-basket"></i> รายการสินค้า</h4>
                        <table class="order-items-table">
                            <thead>
                                <tr>
                                    <th>สินค้า</th>
                                    <th style="text-align: center;">จำนวน</th>
                                    <th style="text-align: right;">ราคา/หน่วย</th>
                                    <th style="text-align: right;">รวม</th>
                                </tr>
                            </thead>
                            <tbody>
                                <asp:Repeater ID="rptOrderItems" runat="server">
                                    <ItemTemplate>
                                        <tr>
                                            <td><%# Eval("Product_Name") %></td>
                                            <td style="text-align: center;"><%# Eval("Quantity") %></td>
                                            <td style="text-align: right;">฿<%# Eval("Unit_Price", "{0:N0}") %></td>
                                            <td style="text-align: right;" class="item-total">฿<%# Eval("Subtotal", "{0:N0}") %></td>
                                        </tr>
                                    </ItemTemplate>
                                </asp:Repeater>
                            </tbody>
                            <tfoot>
                                <tr style="background: #f5f5f5;">
                                    <td colspan="3" style="text-align: right; font-weight: 600;">รวมทั้งสิ้น:</td>
                                    <td style="text-align: right; font-weight: 600; font-size: 18px; color: #2e7d32;">
                                        ฿<asp:Label ID="lblTotalAmount" runat="server"></asp:Label>
                                    </td>
                                </tr>
                            </tfoot>
                        </table>
                    </div>

                    <!-- Action Buttons -->
                    <div class="action-area">
                        <asp:Button ID="btnClaim" runat="server" Text=" รับออเดอร์" CssClass="btn-action btn-claim" OnClick="btnClaim_Click" />
                        <asp:Button ID="btnDelivered" runat="server" Text=" จัดส่งแล้ว" CssClass="btn-action btn-delivered" OnClick="btnDelivered_Click" />
                        <asp:Button ID="btnCancel" runat="server" Text=" ยกเลิก" CssClass="btn-action btn-cancel" OnClick="btnCancel_Click"
                            OnClientClick="return confirm('ยืนยันยกเลิกออเดอร์นี้?');" />
                    </div>
                </asp:Panel>

                <asp:Panel ID="pnlSelectOrder" runat="server" Visible="true">
                    <div class="empty-state">
                        <i class="fas fa-hand-pointer"></i>
                        <h4>เลือกออเดอร์</h4>
                        <p>เลือกออเดอร์จากรายการด้านซ้ายเพื่อดูรายละเอียดและดำเนินการ</p>
                    </div>
                </asp:Panel>
            </div>
        </div>
    </div>

    <!-- Alert Overlay -->
    <div class="alert-overlay" id="alertOverlay">
        <div class="alert-box">
            <i class="fas fa-concierge-bell main-icon"></i>
            <h3>มีออเดอร์ใหม่!</h3>
            <p>กรุณากด "รับออเดอร์" เพื่อดำเนินการจัดส่ง</p>
            <div id="pendingOrdersList"></div>
            <div style="margin-top: 15px;">
                <button type="button" class="btn-action" style="background: #e0e0e0; color: #333;" onclick="dismissAlert()">
                    <i class="fas fa-times"></i> ปิด
                </button>
            </div>
        </div>
    </div>

    <asp:HiddenField ID="hfSelectedOrder" runat="server" />
    <asp:Button ID="btnLoadOrder" runat="server" OnClick="btnLoadOrder_Click" style="display:none;" />

    <!-- Audio for notification -->
    <audio id="notificationSound" preload="auto">
        <source src="data:audio/wav;base64,UklGRnoGAABXQVZFZm10IBAAAAABAAEAQB8AAEAfAAABAAgAZGF0YQoGAACBhYqFbF1sbJaQo5N6YWJ0lJOUlH50ZnKJhZqWlYN0Znd3e4KDmJmXjHlxe3p6f4GGiouJhYB8e35+gIOGiYuKiIaCgYGCgYOFhoaFhYSDgoKCgoKDg4ODg4OCgoKCgoKCgoKCgoKCgoKCgoKCgoKC" type="audio/wav">
    </audio>

    <script>
        var soundEnabled = true;
        var alertInterval = null;
        var checkInterval = null;
        var lastPendingCount = 0;
        var isAlertShowing = false;
        var currentFilter = 'all';

        function selectOrder(orderId) {
            document.getElementById('<%= hfSelectedOrder.ClientID %>').value = orderId;
            document.getElementById('<%= btnLoadOrder.ClientID %>').click();
        }

        function toggleSound() {
            soundEnabled = document.getElementById('chkSound').checked;
        }

        function playSound() {
            if (soundEnabled) {
                try {
                    var audio = document.getElementById('notificationSound');
                    audio.currentTime = 0;
                    audio.play().catch(e => console.log('Audio play error:', e));
                } catch(e) {}
            }
        }

        function filterOrders(status) {
            currentFilter = status;
            var tabs = document.querySelectorAll('.filter-tab');
            tabs.forEach(t => t.classList.remove('active'));
            event.target.classList.add('active');

            var items = document.querySelectorAll('.order-item');
            items.forEach(item => {
                var itemStatus = item.getAttribute('data-status');
                if (status === 'all') {
                    item.style.display = 'block';
                } else if (status === 'pending' && itemStatus === 'PENDING') {
                    item.style.display = 'block';
                } else if (status === 'confirmed' && itemStatus === 'CONFIRMED') {
                    item.style.display = 'block';
                } else {
                    item.style.display = 'none';
                }
            });
        }

        function showAlertWithOrders(orders) {
            if (isAlertShowing) return;
            isAlertShowing = true;

            var html = '';
            orders.forEach(function(order) {
                html += `
                    <div class="order-card">
                        <div class="room-name"><i class="fas fa-door-open"></i> ${order.roomName}</div>
                        <div class="guest-name"><i class="fas fa-user"></i> ${order.guestName}</div>
                        <div class="order-items-preview"><i class="fas fa-shopping-basket"></i> ${order.itemsPreview}</div>
                        <div class="order-amount">฿${order.totalAmount}</div>
                        <button class="btn-claim-order" onclick="claimOrder(${order.orderId})">
                            <i class="fas fa-hand-paper"></i> รับออเดอร์
                        </button>
                    </div>
                `;
            });

            document.getElementById('pendingOrdersList').innerHTML = html;
            document.getElementById('alertOverlay').classList.add('show');

            playSound();
            alertInterval = setInterval(playSound, 3000);
        }

        function dismissAlert() {
            document.getElementById('alertOverlay').classList.remove('show');
            isAlertShowing = false;
            if (alertInterval) {
                clearInterval(alertInterval);
                alertInterval = null;
            }
        }

        function claimOrder(orderId) {
            if (alertInterval) {
                clearInterval(alertInterval);
                alertInterval = null;
            }

            fetch('OrderManagement.aspx/ClaimOrder', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ orderId: orderId })
            })
            .then(response => response.json())
            .then(data => {
                if (data.d && data.d.success) {
                    dismissAlert();
                    selectOrder(orderId);
                } else {
                    alert('ไม่สามารถรับออเดอร์ได้ กรุณาลองใหม่');
                }
            })
            .catch(err => console.log('Claim error:', err));
        }

        function checkNewOrders() {
            fetch('OrderManagement.aspx/GetPendingOrders', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({})
            })
            .then(response => response.json())
            .then(data => {
                if (data.d && data.d.success && data.d.orders && data.d.orders.length > 0) {
                    var pendingCount = data.d.orders.length;

                    // Update badge
                    var badge = document.querySelector('.unread-badge');
                    if (badge) {
                        badge.style.display = 'block';
                        var lblCount = badge.querySelector('span');
                        if (lblCount) lblCount.innerText = pendingCount;
                    }

                    // Show alert if new orders
                    if (pendingCount > lastPendingCount || (!isAlertShowing && pendingCount > 0)) {
                        showAlertWithOrders(data.d.orders);
                    }

                    lastPendingCount = pendingCount;
                } else {
                    var badge = document.querySelector('.unread-badge');
                    if (badge) badge.style.display = 'none';

                    if (isAlertShowing) dismissAlert();
                    lastPendingCount = 0;
                }
            })
            .catch(err => console.log('Check error:', err));
        }

        window.onload = function() {
            // Add icons to buttons
            var btnClaim = document.getElementById('<%= btnClaim.ClientID %>');
            if (btnClaim) btnClaim.innerHTML = '<i class="fas fa-hand-paper"></i> รับออเดอร์';

            var btnDelivered = document.getElementById('<%= btnDelivered.ClientID %>');
            if (btnDelivered) btnDelivered.innerHTML = '<i class="fas fa-check-circle"></i> จัดส่งแล้ว';

            var btnCancel = document.getElementById('<%= btnCancel.ClientID %>');
            if (btnCancel) btnCancel.innerHTML = '<i class="fas fa-times"></i> ยกเลิก';

            checkInterval = setInterval(checkNewOrders, 3000);
            checkNewOrders();
        };

        window.onbeforeunload = function() {
            if (checkInterval) clearInterval(checkInterval);
            if (alertInterval) clearInterval(alertInterval);
        };
    </script>
</asp:Content>
