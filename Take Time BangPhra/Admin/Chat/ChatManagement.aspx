<%@ Page Title="Chat Management" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ChatManagement.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Chat.ChatManagement" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .chat-management {
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

        .page-header h2 i {
            color: #667eea;
        }

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

        /* Chat Layout */
        .chat-layout {
            display: grid;
            grid-template-columns: 350px 1fr;
            gap: 20px;
            height: calc(100vh - 200px);
            min-height: 500px;
        }

        /* Chat List */
        .chat-list-container {
            background: white;
            border-radius: 15px;
            box-shadow: 0 3px 15px rgba(0,0,0,0.08);
            overflow: hidden;
            display: flex;
            flex-direction: column;
        }

        .chat-list-header {
            padding: 15px 20px;
            background: linear-gradient(135deg, #667eea, #764ba2);
            color: white;
            font-weight: 600;
        }

        .chat-list {
            flex: 1;
            overflow-y: auto;
        }

        .chat-item {
            padding: 15px 20px;
            border-bottom: 1px solid #eee;
            cursor: pointer;
            transition: all 0.3s ease;
            display: flex;
            gap: 12px;
            align-items: flex-start;
        }

        .chat-item:hover {
            background: #f8f9fa;
        }

        .chat-item.active {
            background: #e8f4fd;
            border-left: 3px solid #667eea;
        }

        .chat-item.unread {
            background: #fff8e1;
        }

        .chat-item.unread::before {
            content: '';
            position: absolute;
            left: 0;
            top: 0;
            bottom: 0;
            width: 4px;
            background: #f44336;
        }

        .chat-avatar {
            width: 45px;
            height: 45px;
            border-radius: 50%;
            background: linear-gradient(135deg, #667eea, #764ba2);
            display: flex;
            align-items: center;
            justify-content: center;
            color: white;
            font-weight: 600;
            font-size: 16px;
            flex-shrink: 0;
        }

        .chat-item-info {
            flex: 1;
            min-width: 0;
        }

        .chat-item-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 5px;
        }

        .chat-item-name {
            font-weight: 600;
            color: #333;
            font-size: 14px;
        }

        .chat-item-room {
            font-size: 12px;
            color: #667eea;
            font-weight: 500;
        }

        .chat-item-time {
            font-size: 11px;
            color: #999;
        }

        .chat-item-preview {
            font-size: 13px;
            color: #666;
            white-space: nowrap;
            overflow: hidden;
            text-overflow: ellipsis;
        }

        .chat-item.unread .chat-item-preview {
            font-weight: 600;
            color: #333;
        }

        .unread-count {
            background: #f44336;
            color: white;
            font-size: 11px;
            padding: 2px 8px;
            border-radius: 10px;
            font-weight: 600;
        }

        /* Chat Detail */
        .chat-detail-container {
            background: white;
            border-radius: 15px;
            box-shadow: 0 3px 15px rgba(0,0,0,0.08);
            display: flex;
            flex-direction: column;
            overflow: hidden;
        }

        .chat-detail-header {
            padding: 15px 20px;
            background: #f8f9fa;
            border-bottom: 1px solid #eee;
            display: flex;
            justify-content: space-between;
            align-items: center;
        }

        .guest-info h4 {
            margin: 0 0 5px 0;
            font-size: 16px;
            color: #333;
        }

        .guest-info p {
            margin: 0;
            font-size: 13px;
            color: #666;
        }

        .guest-info p i {
            margin-right: 5px;
            color: #667eea;
        }

        .chat-actions {
            display: flex;
            gap: 10px;
        }

        .btn-action {
            padding: 8px 15px;
            border: none;
            border-radius: 8px;
            font-size: 13px;
            cursor: pointer;
            transition: all 0.3s ease;
            display: flex;
            align-items: center;
            gap: 5px;
        }

        .btn-action.primary {
            background: #667eea;
            color: white;
        }

        .btn-action.primary:hover {
            background: #5a6fd6;
        }

        .btn-action.secondary {
            background: #e0e0e0;
            color: #333;
        }

        /* Chat Messages */
        .chat-messages-area {
            flex: 1;
            padding: 20px;
            overflow-y: auto;
            background: #f5f5f5;
        }

        .message {
            display: flex;
            margin-bottom: 15px;
        }

        .message.from-guest {
            justify-content: flex-start;
        }

        .message.from-staff {
            justify-content: flex-end;
        }

        .message-bubble {
            max-width: 70%;
            padding: 12px 16px;
            border-radius: 15px;
        }

        .message.from-guest .message-bubble {
            background: white;
            color: #333;
            border-bottom-left-radius: 5px;
            box-shadow: 0 1px 3px rgba(0,0,0,0.1);
        }

        .message.from-staff .message-bubble {
            background: linear-gradient(135deg, #667eea, #764ba2);
            color: white;
            border-bottom-right-radius: 5px;
        }

        .message-text {
            font-size: 14px;
            line-height: 1.5;
            margin-bottom: 5px;
            white-space: pre-wrap;
        }

        .message-meta {
            font-size: 11px;
            opacity: 0.7;
            display: flex;
            justify-content: space-between;
            gap: 10px;
        }

        /* Reply Area */
        .reply-area {
            padding: 15px 20px;
            background: white;
            border-top: 1px solid #eee;
            display: flex;
            gap: 10px;
            align-items: flex-end;
        }

        .reply-input {
            flex: 1;
            border: 1px solid #ddd;
            border-radius: 20px;
            padding: 12px 18px;
            font-size: 14px;
            resize: none;
            max-height: 100px;
            font-family: inherit;
        }

        .reply-input:focus {
            outline: none;
            border-color: #667eea;
        }

        .btn-send-reply {
            width: 48px;
            height: 48px;
            border: none;
            border-radius: 50%;
            background: linear-gradient(135deg, #667eea, #764ba2);
            color: white;
            font-size: 18px;
            cursor: pointer;
            transition: all 0.3s ease;
        }

        .btn-send-reply:hover {
            transform: scale(1.05);
        }

        .btn-ai-suggest {
            width: 40px; height: 40px; border: none; border-radius: 50%;
            background: linear-gradient(135deg, #7C4DFF, #B388FF); color: white;
            font-size: 16px; cursor: pointer; transition: all 0.3s; flex-shrink: 0;
        }
        .btn-ai-suggest:hover { transform: scale(1.1); box-shadow: 0 2px 8px rgba(124,77,255,0.4); }
        .btn-ai-suggest.loading { opacity: 0.7; pointer-events: none; }

        /* Quick Replies for Staff */
        .quick-replies-staff {
            padding: 10px 20px;
            background: #f8f9fa;
            border-top: 1px solid #eee;
            display: flex;
            gap: 8px;
            flex-wrap: wrap;
        }

        .quick-reply-staff {
            background: white;
            border: 1px solid #ddd;
            padding: 6px 12px;
            border-radius: 15px;
            font-size: 12px;
            cursor: pointer;
            transition: all 0.3s ease;
        }

        .quick-reply-staff:hover {
            background: #667eea;
            color: white;
            border-color: #667eea;
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

        .empty-state h4 {
            margin: 0 0 10px 0;
            color: #666;
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

        .alert-overlay.show {
            display: flex;
        }

        .alert-box {
            background: white;
            padding: 30px 40px;
            border-radius: 20px;
            text-align: center;
            animation: alertBounce 0.5s ease infinite alternate;
            max-width: 400px;
        }

        @keyframes alertBounce {
            from { transform: scale(1); }
            to { transform: scale(1.02); }
        }

        .alert-box i {
            font-size: 50px;
            color: #f44336;
            margin-bottom: 15px;
            animation: alertPulse 1s infinite;
        }

        @keyframes alertPulse {
            0%, 100% { opacity: 1; }
            50% { opacity: 0.5; }
        }

        .alert-box h3 {
            margin: 0 0 10px 0;
            color: #333;
        }

        .alert-box p {
            margin: 0 0 20px 0;
            color: #666;
        }

        .alert-box .room-info {
            background: #f5f5f5;
            padding: 10px 15px;
            border-radius: 10px;
            margin-bottom: 20px;
        }

        .alert-box .btn-respond {
            background: linear-gradient(135deg, #667eea, #764ba2);
            color: white;
            border: none;
            padding: 12px 30px;
            border-radius: 25px;
            font-size: 14px;
            font-weight: 600;
            cursor: pointer;
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
            .chat-layout {
                grid-template-columns: 1fr;
                height: auto;
            }

            .chat-list-container {
                max-height: 300px;
            }

            .chat-detail-container {
                min-height: 500px;
            }
        }
    </style>

    <div class="chat-management">
        <!-- Page Header -->
        <div class="page-header">
            <h2><i class="fas fa-comments"></i> Chat Management</h2>
            <div style="display: flex; gap: 15px; align-items: center;">
                <div class="sound-toggle">
                    <input type="checkbox" id="chkSound" checked onchange="toggleSound()">
                    <label for="chkSound"><i class="fas fa-volume-up"></i> เสียงแจ้งเตือน</label>
                </div>
                <asp:Panel ID="pnlUnreadBadge" runat="server" CssClass="unread-badge" Visible="false">
                    <i class="fas fa-bell"></i> <asp:Label ID="lblUnreadCount" runat="server">0</asp:Label> ข้อความใหม่
                </asp:Panel>
            </div>
        </div>

        <!-- Chat Layout -->
        <div class="chat-layout">
            <!-- Chat List -->
            <div class="chat-list-container">
                <div class="chat-list-header">
                    <i class="fas fa-inbox"></i> การสนทนา
                </div>
                <div class="chat-list" id="chatList">
                    <asp:Repeater ID="rptChatList" runat="server" OnItemCommand="rptChatList_ItemCommand">
                        <ItemTemplate>
                            <div class='chat-item <%# Convert.ToInt32(Eval("UnreadCount")) > 0 ? "unread" : "" %> <%# Eval("ReservationId").ToString() == hfSelectedChat.Value ? "active" : "" %>'
                                 onclick="selectChat(<%# Eval("ReservationId") %>)">
                                <div class="chat-avatar">
                                    <%# Eval("GuestName").ToString().Substring(0, 1).ToUpper() %>
                                </div>
                                <div class="chat-item-info">
                                    <div class="chat-item-header">
                                        <div>
                                            <span class="chat-item-name"><%# Eval("GuestName") %></span>
                                            <span class="chat-item-room"><%# Eval("RoomName") %></span>
                                        </div>
                                        <span class="chat-item-time"><%# Eval("LastMessageTime", "{0:HH:mm}") %></span>
                                    </div>
                                    <div class="chat-item-preview">
                                        <%# Eval("LastMessage") %>
                                    </div>
                                    <%# Convert.ToInt32(Eval("UnreadCount")) > 0 ? "<span class='unread-count'>" + Eval("UnreadCount") + "</span>" : "" %>
                                </div>
                            </div>
                        </ItemTemplate>
                    </asp:Repeater>

                    <asp:Panel ID="pnlNoChats" runat="server" Visible="false">
                        <div class="empty-state" style="padding: 40px;">
                            <i class="fas fa-inbox"></i>
                            <p>ยังไม่มีการสนทนา</p>
                        </div>
                    </asp:Panel>
                </div>
            </div>

            <!-- Chat Detail -->
            <div class="chat-detail-container">
                <asp:Panel ID="pnlChatDetail" runat="server" Visible="false" style="display: flex; flex-direction: column; height: 100%;">
                    <!-- Header -->
                    <div class="chat-detail-header">
                        <div class="guest-info">
                            <h4><asp:Label ID="lblChatGuestName" runat="server"></asp:Label></h4>
                            <p>
                                <i class="fas fa-door-open"></i> <asp:Label ID="lblChatRoomName" runat="server"></asp:Label>
                                &nbsp;&nbsp;
                                <i class="fas fa-phone"></i> <asp:Label ID="lblChatPhone" runat="server"></asp:Label>
                            </p>
                        </div>
                        <div class="chat-actions">
                            <asp:Button ID="btnMarkResolved" runat="server" Text="Mark Resolved" CssClass="btn-action secondary" OnClick="btnMarkResolved_Click" />
                        </div>
                    </div>

                    <!-- Messages -->
                    <div class="chat-messages-area" id="chatMessagesArea">
                        <asp:Repeater ID="rptChatMessages" runat="server">
                            <ItemTemplate>
                                <div class='message <%# Eval("IsFromGuest").ToString() == "True" ? "from-guest" : "from-staff" %>'>
                                    <div class="message-bubble">
                                        <div class="message-text"><%# Server.HtmlEncode(Eval("Message").ToString()) %></div>
                                        <div class="message-meta">
                                            <span><%# Eval("IsFromGuest").ToString() == "True" ? "แขก" : Eval("StaffName") %></span>
                                            <span><%# Eval("CreatedAt", "{0:dd/MM HH:mm}") %></span>
                                        </div>
                                    </div>
                                </div>
                            </ItemTemplate>
                        </asp:Repeater>
                    </div>

                    <!-- Quick Replies -->
                    <div class="quick-replies-staff">
                        <button type="button" class="quick-reply-staff" onclick="setStaffReply('รับทราบครับ/ค่ะ กำลังดำเนินการ')">รับทราบ กำลังดำเนินการ</button>
                        <button type="button" class="quick-reply-staff" onclick="setStaffReply('ดำเนินการเรียบร้อยแล้วครับ/ค่ะ')">ดำเนินการเรียบร้อย</button>
                        <button type="button" class="quick-reply-staff" onclick="setStaffReply('WiFi Password: TakeTime2024')">WiFi Password</button>
                        <button type="button" class="quick-reply-staff" onclick="setStaffReply('Check-out มาตรฐาน 12:00 น. หากต้องการ Late check-out กรุณาแจ้งล่วงหน้า')">Check-out Info</button>
                    </div>

                    <!-- Reply Area -->
                    <div class="reply-area">
                        <asp:TextBox ID="txtReply" runat="server" CssClass="reply-input" TextMode="MultiLine"
                            Rows="1" placeholder="พิมพ์ข้อความตอบกลับ..."></asp:TextBox>
                        <button type="button" class="btn-ai-suggest" id="btnAiSuggest" onclick="getAISuggestion()" title="AI แนะนำคำตอบ" style="display:none;">
                            <i class="fas fa-robot"></i>
                        </button>
                        <asp:Button ID="btnSendReply" runat="server" CssClass="btn-send-reply" Text=""
                            OnClick="btnSendReply_Click" OnClientClick="return validateReply();" />
                    </div>
                </asp:Panel>

                <asp:Panel ID="pnlSelectChat" runat="server" Visible="true">
                    <div class="empty-state">
                        <i class="fas fa-comments"></i>
                        <h4>เลือกการสนทนา</h4>
                        <p>เลือกการสนทนาจากรายการด้านซ้ายเพื่อดูและตอบกลับ</p>
                    </div>
                </asp:Panel>
            </div>
        </div>
    </div>

    <!-- Alert Overlay - Popup for new messages -->
    <div class="alert-overlay" id="alertOverlay">
        <div class="alert-box" style="max-width: 500px; max-height: 80vh; overflow-y: auto;">
            <i class="fas fa-bell"></i>
            <h3>🔔 ข้อความใหม่จากแขก!</h3>
            <p>กรุณากด "รับเรื่อง" เพื่อดำเนินการตอบกลับ</p>

            <!-- List of pending chats -->
            <div id="pendingChatsList" style="text-align: left; margin: 15px 0;"></div>

            <div style="margin-top: 15px;">
                <button type="button" class="btn-action secondary" onclick="dismissAlert()" style="padding: 10px 20px;">
                    <i class="fas fa-times"></i> ปิด
                </button>
            </div>
        </div>
    </div>

    <asp:HiddenField ID="hfSelectedChat" runat="server" />
    <asp:Button ID="btnLoadChat" runat="server" OnClick="btnLoadChat_Click" style="display:none;" />

    <!-- Audio for notification -->
    <audio id="notificationSound" preload="auto">
        <source src="data:audio/wav;base64,UklGRnoGAABXQVZFZm10IBAAAAABAAEAQB8AAEAfAAABAAgAZGF0YQoGAACBhYqFbF1sbJaQo5N6YWJ0lJOUlH50ZnKJhZqWlYN0Znd3e4KDmJmXjHlxe3p6f4GGiouJhYB8e35+gIOGiYuKiIaCgYGCgYOFhoaFhYSDgoKCgoKDg4ODg4OCgoKCgoKCgoKCgoKCgoKCgoKCgoKC" type="audio/wav">
    </audio>

    <script>
        var soundEnabled = true;
        var alertInterval = null;
        var checkInterval = null;
        var lastUnreadCount = 0;
        var isAlertShowing = false;

        // Select chat
        function selectChat(reservationId) {
            document.getElementById('<%= hfSelectedChat.ClientID %>').value = reservationId;
            document.getElementById('<%= btnLoadChat.ClientID %>').click();
        }

        // Set staff reply
        function setStaffReply(message) {
            document.getElementById('<%= txtReply.ClientID %>').value = message;
            document.getElementById('<%= txtReply.ClientID %>').focus();
        }

        // Validate reply
        function validateReply() {
            var reply = document.getElementById('<%= txtReply.ClientID %>').value.trim();
            if (!reply) {
                alert('กรุณาพิมพ์ข้อความก่อนส่ง');
                return false;
            }
            return true;
        }

        // Toggle sound
        function toggleSound() {
            soundEnabled = document.getElementById('chkSound').checked;
        }

        // Play notification sound
        function playSound() {
            if (soundEnabled) {
                try {
                    var audio = document.getElementById('notificationSound');
                    audio.currentTime = 0;
                    audio.play().catch(e => console.log('Audio play error:', e));
                } catch(e) {}
            }
        }

        // Show alert overlay with pending chats list
        function showAlertWithChats(chats) {
            if (isAlertShowing) return;
            isAlertShowing = true;

            var listHtml = '';
            chats.forEach(function(chat) {
                listHtml += `
                    <div style="background: #f8f9fa; border-radius: 10px; padding: 12px; margin-bottom: 10px; border-left: 4px solid #f44336;">
                        <div style="display: flex; justify-content: space-between; align-items: flex-start;">
                            <div style="flex: 1;">
                                <strong style="color: #667eea;">🚪 ${chat.roomName}</strong><br>
                                <span style="color: #333;">👤 ${chat.guestName}</span><br>
                                <small style="color: #666;">💬 ${chat.message}</small><br>
                                <small style="color: #999;">⏰ ${chat.time}</small>
                            </div>
                            <button type="button" onclick="claimChat(${chat.reservationId})"
                                style="background: linear-gradient(135deg, #4CAF50, #45a049); color: white; border: none;
                                       padding: 10px 20px; border-radius: 20px; cursor: pointer; font-weight: 600;
                                       white-space: nowrap; margin-left: 10px;">
                                <i class="fas fa-hand-paper"></i> รับเรื่อง
                            </button>
                        </div>
                    </div>
                `;
            });

            document.getElementById('pendingChatsList').innerHTML = listHtml;
            document.getElementById('alertOverlay').classList.add('show');

            // Play sound repeatedly
            playSound();
            alertInterval = setInterval(playSound, 3000);
        }

        // Dismiss alert without claiming
        function dismissAlert() {
            document.getElementById('alertOverlay').classList.remove('show');
            isAlertShowing = false;
            if (alertInterval) {
                clearInterval(alertInterval);
                alertInterval = null;
            }
        }

        // Claim a chat conversation
        function claimChat(reservationId) {
            // Stop sound
            if (alertInterval) {
                clearInterval(alertInterval);
                alertInterval = null;
            }

            // Show loading
            var btn = event.target;
            var originalText = btn.innerHTML;
            btn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> กำลังรับเรื่อง...';
            btn.disabled = true;

            fetch('ChatManagement.aspx/ClaimChat', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ reservationId: reservationId })
            })
            .then(response => response.json())
            .then(data => {
                if (data.d && data.d.success) {
                    // Close popup
                    dismissAlert();
                    // Select the chat to show messages
                    selectChat(reservationId);
                } else {
                    btn.innerHTML = originalText;
                    btn.disabled = false;
                    alert('ไม่สามารถรับเรื่องได้ กรุณาลองใหม่');
                }
            })
            .catch(err => {
                btn.innerHTML = originalText;
                btn.disabled = false;
                console.log('Claim error:', err);
            });
        }

        // Check for new messages
        function checkNewMessages() {
            fetch('ChatManagement.aspx/GetAllUnreadChats', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({})
            })
            .then(response => response.json())
            .then(data => {
                if (data.d && data.d.success && data.d.chats && data.d.chats.length > 0) {
                    var unreadCount = data.d.chats.length;

                    // Update badge
                    var badge = document.querySelector('.unread-badge');
                    if (badge) {
                        badge.style.display = 'block';
                        var lblCount = badge.querySelector('span');
                        if (lblCount) lblCount.innerText = unreadCount;
                    }

                    // Show alert popup if there are new messages and popup not already showing
                    if (unreadCount > lastUnreadCount || (!isAlertShowing && unreadCount > 0)) {
                        showAlertWithChats(data.d.chats);
                    } else if (isAlertShowing && unreadCount > 0) {
                        // Update the list if popup is already showing
                        var listHtml = '';
                        data.d.chats.forEach(function(chat) {
                            listHtml += `
                                <div style="background: #f8f9fa; border-radius: 10px; padding: 12px; margin-bottom: 10px; border-left: 4px solid #f44336;">
                                    <div style="display: flex; justify-content: space-between; align-items: flex-start;">
                                        <div style="flex: 1;">
                                            <strong style="color: #667eea;">🚪 ${chat.roomName}</strong><br>
                                            <span style="color: #333;">👤 ${chat.guestName}</span><br>
                                            <small style="color: #666;">💬 ${chat.message}</small><br>
                                            <small style="color: #999;">⏰ ${chat.time}</small>
                                        </div>
                                        <button type="button" onclick="claimChat(${chat.reservationId})"
                                            style="background: linear-gradient(135deg, #4CAF50, #45a049); color: white; border: none;
                                                   padding: 10px 20px; border-radius: 20px; cursor: pointer; font-weight: 600;
                                                   white-space: nowrap; margin-left: 10px;">
                                            <i class="fas fa-hand-paper"></i> รับเรื่อง
                                        </button>
                                    </div>
                                </div>
                            `;
                        });
                        document.getElementById('pendingChatsList').innerHTML = listHtml;
                    }

                    lastUnreadCount = unreadCount;
                } else {
                    // No unread messages - hide badge and popup
                    var badge = document.querySelector('.unread-badge');
                    if (badge) badge.style.display = 'none';

                    if (isAlertShowing) {
                        dismissAlert();
                    }
                    lastUnreadCount = 0;
                }
            })
            .catch(err => console.log('Check error:', err));
        }

        // Scroll to bottom of messages
        function scrollToBottom() {
            var messagesArea = document.getElementById('chatMessagesArea');
            if (messagesArea) {
                messagesArea.scrollTop = messagesArea.scrollHeight;
            }
        }

        // Auto-resize textarea
        var replyInput = document.querySelector('.reply-input');
        if (replyInput) {
            replyInput.addEventListener('input', function() {
                this.style.height = 'auto';
                this.style.height = Math.min(this.scrollHeight, 100) + 'px';
            });
        }

        // Page load
        window.onload = function() {
            // Set send button icon
            var btnSend = document.getElementById('<%= btnSendReply.ClientID %>');
            if (btnSend) {
                btnSend.innerHTML = '<i class="fas fa-paper-plane"></i>';
            }

            scrollToBottom();

            // Start checking for new messages every 3 seconds (faster for real-time feel)
            checkInterval = setInterval(checkNewMessages, 3000);

            // Initial check
            checkNewMessages();
        };

        // AI Suggest
        function checkAiEnabled() {
            $.ajax({
                url: '<%= ResolveUrl("~/Admin/Chat/ChatManagement.aspx/GetAISuggestion") %>',
                type: 'POST', contentType: 'application/json',
                data: JSON.stringify({ reservationId: 0 }),
                success: function() { $('#btnAiSuggest').show(); },
                error: function() {}
            });
        }
        checkAiEnabled();

        function getAISuggestion() {
            var chatId = document.getElementById('<%= hfSelectedChat.ClientID %>').value;
            if (!chatId) return;
            var btn = $('#btnAiSuggest');
            btn.addClass('loading').html('<i class="fas fa-spinner fa-spin"></i>');
            $.ajax({
                url: '<%= ResolveUrl("~/Admin/Chat/ChatManagement.aspx/GetAISuggestion") %>',
                type: 'POST', contentType: 'application/json',
                data: JSON.stringify({ reservationId: parseInt(chatId) }),
                success: function(r) {
                    btn.removeClass('loading').html('<i class="fas fa-robot"></i>');
                    if (r.d && r.d.success && r.d.suggestion) {
                        document.getElementById('<%= txtReply.ClientID %>').value = r.d.suggestion;
                        document.getElementById('<%= txtReply.ClientID %>').focus();
                    } else {
                        alert(r.d ? r.d.message : 'ไม่สามารถสร้างคำแนะนำได้');
                    }
                },
                error: function() {
                    btn.removeClass('loading').html('<i class="fas fa-robot"></i>');
                }
            });
        }

        // Clean up on page unload
        window.onbeforeunload = function() {
            if (checkInterval) clearInterval(checkInterval);
            if (alertInterval) clearInterval(alertInterval);
        };
    </script>
</asp:Content>
