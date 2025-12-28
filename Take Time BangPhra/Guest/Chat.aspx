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
        }

        .status-dot.offline {
            background: #9e9e9e;
        }

        .status-text {
            color: #4CAF50;
        }

        .status-text.offline {
            color: #9e9e9e;
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
        }

        .message-time {
            font-size: 11px;
            opacity: 0.7;
            text-align: right;
        }

        .message.received .message-time {
            color: #888;
        }

        /* Date Separator */
        .date-separator {
            text-align: center;
            margin: 20px 0;
        }

        .date-separator span {
            background: #e0e0e0;
            padding: 5px 15px;
            border-radius: 15px;
            font-size: 12px;
            color: #666;
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

        /* Notice */
        .notice-card {
            background: #fff8e1;
            border-left: 4px solid #FFC107;
            padding: 12px 15px;
            border-radius: 8px;
            margin-bottom: 15px;
            font-size: 13px;
            color: #666;
        }

        .notice-card i {
            color: #FFC107;
            margin-right: 8px;
        }

        /* Bottom Nav Space */
        @media (max-width: 768px) {
            .chat-container {
                padding-bottom: 80px;
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

        <!-- Status Bar -->
        <div class="status-bar">
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

        <!-- Notice -->
        <div class="notice-card">
            <i class="fas fa-info-circle"></i>
            ข้อความของคุณจะถูกส่งไปยังเจ้าหน้าที่ Front Desk ทันที เราจะตอบกลับโดยเร็วที่สุด
        </div>

        <!-- Quick Replies -->
        <div class="quick-replies">
            <button type="button" class="quick-reply-btn" onclick="setQuickMessage('ขอผ้าเช็ดตัวเพิ่มครับ/ค่ะ')">ขอผ้าเช็ดตัว</button>
            <button type="button" class="quick-reply-btn" onclick="setQuickMessage('ขอทำความสะอาดห้องครับ/ค่ะ')">ขอทำความสะอาดห้อง</button>
            <button type="button" class="quick-reply-btn" onclick="setQuickMessage('ขอ Late check-out ได้ไหมครับ/ค่ะ')">Late Check-out</button>
            <button type="button" class="quick-reply-btn" onclick="setQuickMessage('WiFi Password คืออะไรครับ/ค่ะ')">WiFi Password</button>
            <button type="button" class="quick-reply-btn" onclick="setQuickMessage('มีบริการเรียกรถไหมครับ/ค่ะ')">เรียกรถ</button>
        </div>

        <!-- Chat Messages -->
        <div class="chat-messages" id="chatMessages">
            <asp:Repeater ID="rptMessages" runat="server">
                <ItemTemplate>
                    <div class='message <%# Eval("IsFromGuest").ToString() == "True" ? "sent" : "received" %>'>
                        <div class="message-bubble">
                            <div class="message-text"><%# Eval("Message") %></div>
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
        </div>

        <!-- Chat Input -->
        <div class="chat-input-container">
            <asp:TextBox ID="txtMessage" runat="server" CssClass="chat-input"
                TextMode="MultiLine" Rows="1" placeholder="พิมพ์ข้อความ..."
                onkeypress="return handleKeyPress(event)"></asp:TextBox>
            <asp:Button ID="btnSend" runat="server" CssClass="btn-send"
                Text="" OnClick="btnSend_Click" UseSubmitBehavior="false" />
        </div>
    </div>

    <!-- Bottom Navigation -->
    <div class="bottom-nav" style="position: fixed; bottom: 0; left: 0; right: 0; background: white; display: flex; justify-content: space-around; padding: 10px 0; box-shadow: 0 -3px 15px rgba(0,0,0,0.1); z-index: 1000; border-top: 1px solid #eee;">
        <a href="Dashboard.aspx" style="display: flex; flex-direction: column; align-items: center; text-decoration: none; padding: 5px 15px;">
            <i class="fas fa-home" style="font-size: 22px; margin-bottom: 4px; color: #888;"></i>
            <span style="font-size: 10px; color: #888;">หน้าหลัก</span>
        </a>
        <a href="RoomService.aspx" style="display: flex; flex-direction: column; align-items: center; text-decoration: none; padding: 5px 15px;">
            <i class="fas fa-utensils" style="font-size: 22px; margin-bottom: 4px; color: #888;"></i>
            <span style="font-size: 10px; color: #888;">สั่งอาหาร</span>
        </a>
        <a href="Housekeeping.aspx" style="display: flex; flex-direction: column; align-items: center; text-decoration: none; padding: 5px 15px;">
            <i class="fas fa-broom" style="font-size: 22px; margin-bottom: 4px; color: #888;"></i>
            <span style="font-size: 10px; color: #888;">แม่บ้าน</span>
        </a>
        <a href="Balance.aspx" style="display: flex; flex-direction: column; align-items: center; text-decoration: none; padding: 5px 15px;">
            <i class="fas fa-wallet" style="font-size: 22px; margin-bottom: 4px; color: #888;"></i>
            <span style="font-size: 10px; color: #888;">ยอดชำระ</span>
        </a>
        <a href="Review.aspx" style="display: flex; flex-direction: column; align-items: center; text-decoration: none; padding: 5px 15px;">
            <i class="fas fa-star" style="font-size: 22px; margin-bottom: 4px; color: #888;"></i>
            <span style="font-size: 10px; color: #888;">รีวิว</span>
        </a>
    </div>

    <script>
        // Auto-resize textarea
        const textarea = document.querySelector('.chat-input');
        textarea.addEventListener('input', function() {
            this.style.height = 'auto';
            this.style.height = Math.min(this.scrollHeight, 100) + 'px';
        });

        // Scroll to bottom of chat
        function scrollToBottom() {
            const chatMessages = document.getElementById('chatMessages');
            chatMessages.scrollTop = chatMessages.scrollHeight;
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

        // Scroll to bottom on page load
        window.onload = function() {
            scrollToBottom();
            // Set send button icon
            document.getElementById('<%= btnSend.ClientID %>').innerHTML = '<i class="fas fa-paper-plane"></i>';
        };
    </script>
</asp:Content>
