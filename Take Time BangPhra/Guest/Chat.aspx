<%@ Page Title="Chat with Front Desk" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Chat.aspx.cs" Inherits="Take_Time_BangPhra.Guest.Chat" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .chat-container {
            max-width: 800px;
            margin: 0 auto;
            padding: 15px;
            padding-bottom: 100px;
        }

        .page-header {
            background: linear-gradient(135deg, #30cfd0 0%, #330867 100%);
            color: white;
            padding: 20px;
            border-radius: 15px;
            margin-bottom: 20px;
            display: flex;
            justify-content: space-between;
            align-items: center;
        }

        .page-header h2 {
            margin: 0;
            font-size: 20px;
            font-weight: 700;
            display: flex;
            align-items: center;
            gap: 10px;
        }

        .btn-back {
            background: rgba(255,255,255,0.2);
            color: white;
            border: none;
            padding: 8px 16px;
            border-radius: 20px;
            text-decoration: none;
            font-weight: 500;
            font-size: 14px;
            transition: all 0.3s ease;
        }

        .btn-back:hover {
            background: rgba(255,255,255,0.3);
            color: white;
            text-decoration: none;
        }

        /* Status Bar */
        .status-bar {
            background: white;
            padding: 15px 20px;
            border-radius: 12px;
            margin-bottom: 15px;
            display: flex;
            align-items: center;
            gap: 12px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.08);
        }

        .status-avatar {
            width: 50px;
            height: 50px;
            background: linear-gradient(135deg, #667eea, #764ba2);
            border-radius: 50%;
            display: flex;
            align-items: center;
            justify-content: center;
            color: white;
            font-size: 20px;
        }

        .status-info h4 {
            margin: 0 0 3px 0;
            font-size: 16px;
            color: #333;
        }

        .status-info .status {
            display: flex;
            align-items: center;
            gap: 5px;
            font-size: 13px;
        }

        .status-dot {
            width: 8px;
            height: 8px;
            border-radius: 50%;
            background: #4CAF50;
            animation: pulse 2s infinite;
        }

        @keyframes pulse {
            0%, 100% { opacity: 1; }
            50% { opacity: 0.5; }
        }

        .status-text {
            color: #4CAF50;
        }

        /* Chat Messages */
        .chat-messages {
            background: #f5f5f5;
            border-radius: 15px;
            padding: 20px;
            min-height: 400px;
            max-height: 500px;
            overflow-y: auto;
            margin-bottom: 15px;
        }

        .message {
            display: flex;
            margin-bottom: 15px;
            animation: fadeIn 0.3s ease;
        }

        @keyframes fadeIn {
            from { opacity: 0; transform: translateY(10px); }
            to { opacity: 1; transform: translateY(0); }
        }

        .message.sent {
            justify-content: flex-end;
        }

        .message-bubble {
            max-width: 75%;
            padding: 12px 16px;
            border-radius: 18px;
            position: relative;
        }

        .message.received .message-bubble {
            background: white;
            color: #333;
            border-bottom-left-radius: 5px;
            box-shadow: 0 1px 3px rgba(0,0,0,0.1);
        }

        .message.sent .message-bubble {
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

        .message-time {
            font-size: 11px;
            opacity: 0.7;
            text-align: right;
        }

        .message.received .message-time {
            color: #888;
        }

        /* Quick Replies */
        .quick-replies {
            display: flex;
            flex-wrap: wrap;
            gap: 8px;
            margin-bottom: 15px;
        }

        .quick-reply-btn {
            background: white;
            border: 1px solid #e0e0e0;
            padding: 8px 15px;
            border-radius: 20px;
            font-size: 13px;
            color: #555;
            cursor: pointer;
            transition: all 0.3s ease;
        }

        .quick-reply-btn:hover {
            background: #667eea;
            border-color: #667eea;
            color: white;
        }

        /* Empty State */
        .empty-chat {
            text-align: center;
            padding: 60px 20px;
            color: #888;
        }

        .empty-chat i {
            font-size: 60px;
            margin-bottom: 20px;
            opacity: 0.3;
        }

        .empty-chat h4 {
            margin: 0 0 10px 0;
            color: #555;
            font-size: 18px;
        }

        .empty-chat p {
            margin: 0;
            font-size: 14px;
        }

        /* Chat Input */
        .chat-input-container {
            background: white;
            border-radius: 15px;
            padding: 15px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.08);
            display: flex;
            gap: 10px;
            align-items: flex-end;
            position: fixed;
            bottom: 70px;
            left: 15px;
            right: 15px;
            max-width: 770px;
            margin: 0 auto;
        }

        .chat-input {
            flex: 1;
            border: 1px solid #e0e0e0;
            border-radius: 20px;
            padding: 12px 18px;
            font-size: 14px;
            resize: none;
            max-height: 100px;
            font-family: inherit;
            transition: border-color 0.3s;
        }

        .chat-input:focus {
            outline: none;
            border-color: #667eea;
        }

        .btn-send {
            width: 48px;
            height: 48px;
            border: none;
            border-radius: 50%;
            background: linear-gradient(135deg, #667eea, #764ba2);
            color: white;
            font-size: 18px;
            cursor: pointer;
            transition: all 0.3s ease;
            display: flex;
            align-items: center;
            justify-content: center;
        }

        .btn-send:hover {
            transform: scale(1.05);
            box-shadow: 0 3px 10px rgba(102, 126, 234, 0.4);
        }

        /* Notice */
        .notice-card {
            background: #e8f5e9;
            border-left: 4px solid #4CAF50;
            padding: 12px 15px;
            border-radius: 8px;
            margin-bottom: 15px;
            font-size: 13px;
            color: #2e7d32;
        }

        .notice-card i {
            margin-right: 8px;
        }

        /* Bottom Nav */
        .bottom-nav {
            position: fixed;
            bottom: 0;
            left: 0;
            right: 0;
            background: white;
            display: flex;
            justify-content: space-around;
            padding: 10px 0;
            box-shadow: 0 -3px 15px rgba(0,0,0,0.1);
            z-index: 1000;
            border-top: 1px solid #eee;
        }

        .bottom-nav a {
            display: flex;
            flex-direction: column;
            align-items: center;
            text-decoration: none;
            padding: 5px 15px;
        }

        .bottom-nav a i {
            font-size: 22px;
            margin-bottom: 4px;
            color: #888;
        }

        .bottom-nav a.active i {
            color: #667eea;
        }

        .bottom-nav a span {
            font-size: 10px;
            color: #888;
        }

        .bottom-nav a.active span {
            color: #667eea;
        }

        /* Typing indicator */
        .typing-indicator {
            display: none;
            padding: 10px;
            color: #888;
            font-size: 13px;
            font-style: italic;
        }

        .typing-indicator.show {
            display: block;
        }

        /* Chat Mode Tabs */
        .chat-mode-tabs { display: flex; gap: 0; margin-bottom: 15px; background: white; border-radius: 12px; overflow: hidden; box-shadow: 0 2px 10px rgba(0,0,0,0.08); }
        .chat-tab { flex: 1; padding: 12px; border: none; background: white; font-size: 14px; font-weight: 500; font-family: inherit; cursor: pointer; color: #888; transition: all 0.3s; display: flex; align-items: center; justify-content: center; gap: 8px; }
        .chat-tab.active { background: linear-gradient(135deg, #667eea, #764ba2); color: white; }
        .chat-tab:not(.active):hover { background: #f5f5f5; }

        /* AI Chat Area */
        .ai-chat-messages { background: #f5f5f5; border-radius: 15px; padding: 20px; min-height: 400px; max-height: 500px; overflow-y: auto; margin-bottom: 15px; display: none; }
        .ai-msg { display: flex; margin-bottom: 12px; animation: fadeIn 0.3s ease; }
        .ai-msg.user { justify-content: flex-end; }
        .ai-msg .ai-bubble { max-width: 80%; padding: 12px 16px; border-radius: 18px; font-size: 14px; line-height: 1.6; white-space: pre-wrap; word-wrap: break-word; }
        .ai-msg.user .ai-bubble { background: linear-gradient(135deg, #7C4DFF, #B388FF); color: white; border-bottom-right-radius: 5px; }
        .ai-msg.assistant .ai-bubble { background: white; color: #333; border-bottom-left-radius: 5px; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }
        .ai-msg .ai-time { font-size: 10px; opacity: 0.6; margin-top: 4px; text-align: right; }
        .ai-typing { display: none; padding: 10px; color: #7C4DFF; font-size: 13px; font-style: italic; }
        .ai-typing.show { display: block; }
        .ai-empty { text-align: center; padding: 60px 20px; color: #888; }
        .ai-empty i { font-size: 50px; margin-bottom: 15px; opacity: 0.3; color: #7C4DFF; display: block; }

        .ai-input-container { background: white; border-radius: 15px; padding: 15px; box-shadow: 0 2px 10px rgba(0,0,0,0.08); display: none; gap: 10px; align-items: flex-end; position: fixed; bottom: 70px; left: 15px; right: 15px; max-width: 770px; margin: 0 auto; }
        .ai-input-container input { flex: 1; border: 1px solid #e0e0e0; border-radius: 20px; padding: 12px 18px; font-size: 14px; font-family: inherit; }
        .ai-input-container input:focus { outline: none; border-color: #7C4DFF; }
        .ai-send-btn { width: 48px; height: 48px; border: none; border-radius: 50%; background: linear-gradient(135deg, #7C4DFF, #B388FF); color: white; font-size: 18px; cursor: pointer; display: flex; align-items: center; justify-content: center; }

        @media (min-width: 768px) {
            .chat-input-container {
                position: relative;
                bottom: auto;
                left: auto;
                right: auto;
            }
            .chat-container {
                padding-bottom: 30px;
            }
            .bottom-nav {
                display: none;
            }
        }
    </style>

    <div class="chat-container">
        <!-- Header -->
        <div class="page-header">
            <h2><i class="fas fa-comments"></i> Chat</h2>
            <a href="Dashboard.aspx" class="btn-back">
                <i class="fas fa-arrow-left"></i> กลับ
            </a>
        </div>

        <!-- Chat Mode Tabs -->
        <asp:Panel ID="pnlAiTab" runat="server" Visible="false">
            <div class="chat-mode-tabs">
                <button type="button" class="chat-tab active" id="tabFrontDesk" onclick="switchChatMode('frontdesk')">
                    <i class="fas fa-headset"></i> Front Desk
                </button>
                <button type="button" class="chat-tab" id="tabAI" onclick="switchChatMode('ai')">
                    <i class="fas fa-robot"></i> AI ผู้ช่วย
                </button>
            </div>
        </asp:Panel>

        <!-- Status Bar -->
        <div class="status-bar" id="statusFrontDesk">
            <div class="status-avatar">
                <i class="fas fa-headset"></i>
            </div>
            <div class="status-info">
                <h4>Front Desk</h4>
                <div class="status">
                    <span class="status-dot"></span>
                    <span class="status-text">พร้อมให้บริการ 24 ชม.</span>
                </div>
            </div>
        </div>
        <div class="status-bar" id="statusAI" style="display:none;">
            <div class="status-avatar" style="background: linear-gradient(135deg, #7C4DFF, #B388FF);">
                <i class="fas fa-robot"></i>
            </div>
            <div class="status-info">
                <h4>AI ผู้ช่วยอัจฉริยะ</h4>
                <div class="status">
                    <span class="status-dot"></span>
                    <span class="status-text">ตอบทันที 24 ชม.</span>
                </div>
            </div>
        </div>

        <!-- Notice -->
        <div class="notice-card" id="noticeFrontDesk">
            <i class="fas fa-bolt"></i>
            ข้อความจะถูกส่งถึงเจ้าหน้าที่ทันที เราจะตอบกลับโดยเร็วที่สุด
        </div>
        <div class="notice-card" id="noticeAI" style="display:none; background:#ede7f6; border-color:#7C4DFF; color:#4527A0;">
            <i class="fas fa-robot"></i>
            สอบถามข้อมูลที่พัก สิ่งอำนวยความสะดวก กิจกรรม และบริการต่างๆ ได้ทันที
        </div>

        <!-- Quick Replies (Front Desk) -->
        <div class="quick-replies" id="quickRepliesFD">
            <button type="button" class="quick-reply-btn" onclick="setQuickMessage('ขอผ้าเช็ดตัวเพิ่มครับ/ค่ะ')">ขอผ้าเช็ดตัว</button>
            <button type="button" class="quick-reply-btn" onclick="setQuickMessage('ขอทำความสะอาดห้องครับ/ค่ะ')">ทำความสะอาดห้อง</button>
            <button type="button" class="quick-reply-btn" onclick="setQuickMessage('ขอ Late check-out ได้ไหมครับ/ค่ะ')">Late Check-out</button>
            <button type="button" class="quick-reply-btn" onclick="setQuickMessage('WiFi Password คืออะไรครับ/ค่ะ')">WiFi Password</button>
            <button type="button" class="quick-reply-btn" onclick="setQuickMessage('มีบริการเรียกรถไหมครับ/ค่ะ')">เรียกรถ</button>
        </div>
        <!-- Quick Replies (AI) -->
        <div class="quick-replies" id="quickRepliesAI" style="display:none;">
            <button type="button" class="quick-reply-btn" onclick="setAiQuickMessage('มีกิจกรรมอะไรทำได้บ้าง?')">กิจกรรม</button>
            <button type="button" class="quick-reply-btn" onclick="setAiQuickMessage('ร้านอาหารใกล้ๆ แนะนำหน่อย')">ร้านอาหาร</button>
            <button type="button" class="quick-reply-btn" onclick="setAiQuickMessage('สิ่งอำนวยความสะดวกมีอะไรบ้าง?')">สิ่งอำนวยความสะดวก</button>
            <button type="button" class="quick-reply-btn" onclick="setAiQuickMessage('เช็คเอาท์กี่โมง?')">เวลาเช็คเอาท์</button>
        </div>

        <!-- Chat Messages -->
        <div class="chat-messages" id="chatMessages">
            <asp:Repeater ID="rptMessages" runat="server">
                <ItemTemplate>
                    <div class='message <%# Eval("IsFromGuest").ToString() == "True" ? "sent" : "received" %>'>
                        <div class="message-bubble">
                            <div class="message-text"><%# Server.HtmlEncode(Eval("Message").ToString()) %></div>
                            <div class="message-time"><%# Eval("CreatedAt", "{0:HH:mm}") %></div>
                        </div>
                    </div>
                </ItemTemplate>
            </asp:Repeater>

            <asp:Panel ID="pnlEmptyChat" runat="server" Visible="true">
                <div class="empty-chat">
                    <i class="fas fa-comments"></i>
                    <h4>เริ่มสนทนากับเรา</h4>
                    <p>มีคำถามหรือต้องการความช่วยเหลือ?<br>ส่งข้อความหาเราได้เลย!</p>
                </div>
            </asp:Panel>

            <div class="typing-indicator" id="typingIndicator">
                <i class="fas fa-ellipsis-h"></i> เจ้าหน้าที่กำลังพิมพ์...
            </div>
        </div>

        <!-- AI Chat Messages -->
        <div class="ai-chat-messages" id="aiChatMessages">
            <div class="ai-empty" id="aiEmpty">
                <i class="fas fa-robot"></i>
                <h4>AI ผู้ช่วยอัจฉริยะ</h4>
                <p>สอบถามข้อมูลที่พัก กิจกรรม<br/>และบริการต่างๆ ได้ทันที</p>
            </div>
            <div class="ai-typing" id="aiTyping">
                <i class="fas fa-spinner fa-spin"></i> AI กำลังตอบ...
            </div>
        </div>

        <!-- Chat Input (Front Desk) -->
        <div class="chat-input-container" id="inputFrontDesk">
            <asp:TextBox ID="txtMessage" runat="server" CssClass="chat-input"
                TextMode="MultiLine" Rows="1" placeholder="พิมพ์ข้อความ..."
                onkeypress="return handleKeyPress(event)"></asp:TextBox>
            <asp:Button ID="btnSend" runat="server" CssClass="btn-send"
                Text="" OnClick="btnSend_Click" UseSubmitBehavior="false" />
        </div>

        <!-- Chat Input (AI) -->
        <div class="ai-input-container" id="inputAI">
            <input type="text" id="txtAiMessage" placeholder="ถาม AI ผู้ช่วย..." onkeypress="if(event.key==='Enter')sendAiMessage()" />
            <button type="button" class="ai-send-btn" onclick="sendAiMessage()"><i class="fas fa-paper-plane"></i></button>
        </div>
    </div>

    <!-- Bottom Navigation -->
    <div class="bottom-nav">
        <a href="Dashboard.aspx">
            <i class="fas fa-home"></i>
            <span>หน้าหลัก</span>
        </a>
        <a href="RoomService.aspx">
            <i class="fas fa-utensils"></i>
            <span>สั่งอาหาร</span>
        </a>
        <a href="Housekeeping.aspx">
            <i class="fas fa-broom"></i>
            <span>แม่บ้าน</span>
        </a>
        <a href="Balance.aspx">
            <i class="fas fa-wallet"></i>
            <span>ยอดชำระ</span>
        </a>
        <a href="Review.aspx">
            <i class="fas fa-star"></i>
            <span>รีวิว</span>
        </a>
    </div>

    <asp:HiddenField ID="hfReservationId" runat="server" />

    <script>
        // Auto-resize textarea
        const textarea = document.querySelector('.chat-input');
        if (textarea) {
            textarea.addEventListener('input', function() {
                this.style.height = 'auto';
                this.style.height = Math.min(this.scrollHeight, 100) + 'px';
            });
        }

        // Scroll to bottom of chat
        function scrollToBottom() {
            const chatMessages = document.getElementById('chatMessages');
            if (chatMessages) {
                chatMessages.scrollTop = chatMessages.scrollHeight;
            }
        }

        // Handle Enter key
        function handleKeyPress(e) {
            if (e.keyCode === 13 && !e.shiftKey) {
                e.preventDefault();
                document.getElementById('<%= btnSend.ClientID %>').click();
                return false;
            }
            return true;
        }

        // Set quick message
        function setQuickMessage(message) {
            document.getElementById('<%= txtMessage.ClientID %>').value = message;
            document.getElementById('<%= txtMessage.ClientID %>').focus();
        }

        // Auto-refresh chat every 5 seconds
        var refreshInterval;
        function startAutoRefresh() {
            refreshInterval = setInterval(function() {
                checkNewMessages();
            }, 5000);
        }

        function checkNewMessages() {
            // Use AJAX to check for new messages
            var reservationId = document.getElementById('<%= hfReservationId.ClientID %>').value;
            if (reservationId) {
                fetch('<%= ResolveUrl("~/Guest/Chat.aspx/GetNewMessages") %>', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify({ reservationId: parseInt(reservationId) })
                })
                    .then(response => response.json())
                    .then(data => {
                        if (data.d && data.d.hasNewMessages) {
                            location.reload();
                        }
                    })
                    .catch(err => console.log('Check messages error:', err));
            }
        }

        // Page load
        window.onload = function() {
            scrollToBottom();
            // Set send button icon
            var btnSend = document.getElementById('<%= btnSend.ClientID %>');
            if (btnSend) {
                btnSend.innerHTML = '<i class="fas fa-paper-plane"></i>';
            }
            startAutoRefresh();
        };

        // AI Chat
        var currentMode = 'frontdesk';
        var aiSessionKey = 'guest_' + new Date().getTime();

        function switchChatMode(mode) {
            currentMode = mode;
            if (mode === 'ai') {
                $('#tabFrontDesk').removeClass('active');
                $('#tabAI').addClass('active');
                $('#statusFrontDesk, #noticeFrontDesk, #quickRepliesFD').hide();
                $('#statusAI, #noticeAI, #quickRepliesAI').show();
                $('#chatMessages, #inputFrontDesk').hide();
                $('#aiChatMessages, #inputAI').show().css('display', 'flex');
                $('#aiChatMessages').css('display', 'block');
                clearInterval(refreshInterval);
            } else {
                $('#tabAI').removeClass('active');
                $('#tabFrontDesk').addClass('active');
                $('#statusAI, #noticeAI, #quickRepliesAI').hide();
                $('#statusFrontDesk, #noticeFrontDesk, #quickRepliesFD').show();
                $('#aiChatMessages, #inputAI').hide();
                $('#chatMessages, #inputFrontDesk').show();
                startAutoRefresh();
            }
        }

        function setAiQuickMessage(msg) {
            $('#txtAiMessage').val(msg);
            sendAiMessage();
        }

        function sendAiMessage() {
            var msg = $('#txtAiMessage').val().trim();
            if (!msg) return;
            $('#txtAiMessage').val('');

            $('#aiEmpty').hide();
            appendAiMsg('user', msg);
            $('#aiTyping').addClass('show');
            scrollAiBottom();

            $.ajax({
                url: '<%= ResolveUrl("~/Guest/Chat.aspx/AskAI") %>',
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify({ message: msg, sessionKey: aiSessionKey }),
                success: function (r) {
                    $('#aiTyping').removeClass('show');
                    if (r.d && r.d.success) {
                        appendAiMsg('assistant', r.d.message);
                    } else {
                        appendAiMsg('assistant', (r.d && r.d.message) || 'ขออภัย ไม่สามารถตอบได้ในขณะนี้');
                    }
                },
                error: function () {
                    $('#aiTyping').removeClass('show');
                    appendAiMsg('assistant', 'ขออภัย เกิดข้อผิดพลาด กรุณาลองใหม่');
                }
            });
        }

        function appendAiMsg(role, text) {
            var now = new Date();
            var time = ('0' + now.getHours()).slice(-2) + ':' + ('0' + now.getMinutes()).slice(-2);
            var escaped = $('<div>').text(text).html();
            var html = '<div class="ai-msg ' + role + '"><div><div class="ai-bubble">' + escaped + '</div><div class="ai-time">' + time + '</div></div></div>';
            $('#aiTyping').before(html);
            scrollAiBottom();
        }

        function scrollAiBottom() {
            var el = document.getElementById('aiChatMessages');
            if (el) el.scrollTop = el.scrollHeight;
        }

        // Play notification sound for new messages
        function playNotificationSound() {
            try {
                var audio = new Audio('data:audio/wav;base64,UklGRnoGAABXQVZFZm10IBAAAAABAAEAQB8AAEAfAAABAAgAZGF0YQoGAACBhYqFbF1sbJaQo5N6YWJ0lJOUlH50ZnKJhZqWlYN0Znd3e4KDmJmXjHlxe3p6f4GGiouJhYB8e35+gIOGiYuKiIaCgYGCgYOFhoaFhYSDgoKCgoKDg4ODg4OCgoKCgoKCgoKCgoKCgoKCgoKCgoKC');
                audio.play();
            } catch(e) {}
        }
    </script>
</asp:Content>
