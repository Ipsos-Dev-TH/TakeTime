<%@ Page Title="Omni-Channel Inbox" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="OmniChannelInbox.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Chat.OmniChannelInbox" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .omni-page { max-width: 1500px; margin: 0 auto; padding: 15px; }

        /* Header */
        .omni-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px; flex-wrap: wrap; gap: 12px; }
        .omni-header h2 { margin: 0; font-size: 22px; color: #333; display: flex; align-items: center; gap: 10px; }
        .omni-header h2 i { color: #5D4037; }
        .header-badges { display: flex; gap: 8px; flex-wrap: wrap; }
        .stat-pill { padding: 6px 14px; border-radius: 20px; font-size: 12px; font-weight: 600; }
        .stat-pill.unread { background: #FFEBEE; color: #C62828; }
        .stat-pill.open { background: #E3F2FD; color: #1565C0; }
        .stat-pill.pending { background: #FFF3E0; color: #E65100; }

        /* Layout */
        .omni-layout { display: grid; grid-template-columns: 360px 1fr; gap: 0; height: calc(100vh - 180px); min-height: 500px; background: white; border-radius: 12px; overflow: hidden; box-shadow: 0 2px 12px rgba(0,0,0,0.08); }

        /* Sidebar */
        .conv-sidebar { border-right: 1px solid #e8e8e8; display: flex; flex-direction: column; }
        .conv-filters { padding: 12px; border-bottom: 1px solid #e8e8e8; background: #fafafa; }
        .filter-row { display: flex; gap: 6px; margin-bottom: 8px; }
        .filter-row:last-child { margin-bottom: 0; }
        .filter-row input, .filter-row select { flex: 1; padding: 7px 10px; border: 1px solid #ddd; border-radius: 6px; font-size: 12px; font-family: 'Prompt',sans-serif; }
        .filter-row input:focus, .filter-row select:focus { outline: none; border-color: #5D4037; }

        /* Channel Filter Pills */
        .channel-pills { display: flex; gap: 4px; flex-wrap: wrap; padding: 8px 12px; border-bottom: 1px solid #e8e8e8; }
        .ch-pill { padding: 4px 10px; border-radius: 12px; font-size: 11px; border: 1px solid #ddd; background: white; cursor: pointer; font-family: 'Prompt',sans-serif; display: flex; align-items: center; gap: 4px; transition: all 0.2s; }
        .ch-pill:hover { background: #f5f5f5; }
        .ch-pill.active { background: #5D4037; color: white; border-color: #5D4037; }
        .ch-pill .pill-count { font-size: 10px; background: rgba(0,0,0,0.1); padding: 1px 5px; border-radius: 8px; }
        .ch-pill.active .pill-count { background: rgba(255,255,255,0.3); }

        /* Conversation List */
        .conv-list { flex: 1; overflow-y: auto; }
        .conv-item { padding: 14px 16px; border-bottom: 1px solid #f0f0f0; cursor: pointer; transition: background 0.2s; display: flex; gap: 12px; align-items: flex-start; position: relative; }
        .conv-item:hover { background: #f9f9f9; }
        .conv-item.active { background: #EFEBE9; }
        .conv-item.has-unread { background: #FFF8E1; }
        .conv-item.has-unread::before { content: ''; position: absolute; left: 0; top: 0; bottom: 0; width: 3px; }

        .conv-avatar { width: 42px; height: 42px; border-radius: 50%; display: flex; align-items: center; justify-content: center; color: white; font-size: 16px; flex-shrink: 0; }
        .conv-body { flex: 1; min-width: 0; }
        .conv-top { display: flex; justify-content: space-between; align-items: center; margin-bottom: 3px; }
        .conv-name { font-weight: 600; font-size: 13px; color: #333; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; max-width: 180px; }
        .conv-time { font-size: 11px; color: #999; flex-shrink: 0; }
        .conv-preview { font-size: 12px; color: #888; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
        .conv-item.has-unread .conv-preview { color: #333; font-weight: 500; }
        .conv-meta { display: flex; justify-content: space-between; align-items: center; margin-top: 4px; }
        .conv-channel-badge { font-size: 10px; padding: 2px 6px; border-radius: 4px; color: white; font-weight: 500; }
        .conv-unread-badge { background: #F44336; color: white; font-size: 10px; padding: 2px 7px; border-radius: 10px; font-weight: 700; }
        .conv-priority-badge { font-size: 10px; padding: 2px 6px; border-radius: 4px; }
        .priority-HIGH { background: #FFEBEE; color: #C62828; }
        .priority-URGENT { background: #F44336; color: white; }
        .conv-assigned { font-size: 10px; color: #7C4DFF; }

        /* Chat Detail */
        .chat-panel { display: flex; flex-direction: column; }
        .chat-header { padding: 14px 20px; border-bottom: 1px solid #e8e8e8; background: #fafafa; display: flex; justify-content: space-between; align-items: center; }
        .chat-header-left { display: flex; align-items: center; gap: 12px; }
        .chat-header-left .ch-icon { width: 36px; height: 36px; border-radius: 50%; display: flex; align-items: center; justify-content: center; color: white; font-size: 16px; }
        .chat-header-info h4 { margin: 0; font-size: 15px; color: #333; }
        .chat-header-info p { margin: 2px 0 0; font-size: 12px; color: #888; }
        .chat-actions { display: flex; gap: 6px; }
        .btn-act { padding: 6px 12px; border: 1px solid #ddd; background: white; border-radius: 6px; font-size: 12px; cursor: pointer; font-family: 'Prompt',sans-serif; display: flex; align-items: center; gap: 4px; transition: all 0.2s; }
        .btn-act:hover { background: #f5f5f5; }
        .btn-act.primary { background: #5D4037; color: white; border-color: #5D4037; }
        .btn-act.primary:hover { background: #4E342E; }
        .btn-act.success { background: #4CAF50; color: white; border-color: #4CAF50; }
        .btn-act.danger { background: #F44336; color: white; border-color: #F44336; }

        /* Messages Area */
        .messages-area { flex: 1; overflow-y: auto; padding: 20px; background: #f5f5f5; }
        .msg-date-sep { text-align: center; margin: 15px 0; font-size: 11px; color: #999; }
        .msg-date-sep span { background: #e8e8e8; padding: 3px 12px; border-radius: 10px; }
        .msg { display: flex; margin-bottom: 10px; }
        .msg.incoming { justify-content: flex-start; }
        .msg.outgoing { justify-content: flex-end; }
        .msg .bubble { max-width: 70%; padding: 10px 14px; border-radius: 14px; font-size: 13px; line-height: 1.5; word-wrap: break-word; white-space: pre-wrap; }
        .msg.incoming .bubble { background: white; color: #333; border-bottom-left-radius: 4px; box-shadow: 0 1px 2px rgba(0,0,0,0.08); }
        .msg.outgoing .bubble { background: #5D4037; color: white; border-bottom-right-radius: 4px; }
        .msg .msg-meta { font-size: 10px; opacity: 0.6; margin-top: 4px; text-align: right; }
        .msg.incoming .msg-sender { font-size: 11px; color: #7C4DFF; font-weight: 600; margin-bottom: 3px; }

        /* Reply Area */
        .reply-area { padding: 12px 16px; border-top: 1px solid #e8e8e8; background: white; }
        .canned-bar { display: flex; gap: 4px; flex-wrap: wrap; margin-bottom: 8px; max-height: 60px; overflow-y: auto; }
        .canned-btn { padding: 4px 10px; border: 1px solid #e0e0e0; background: white; border-radius: 12px; font-size: 11px; cursor: pointer; font-family: 'Prompt',sans-serif; color: #555; transition: all 0.2s; }
        .canned-btn:hover { background: #5D4037; color: white; border-color: #5D4037; }
        .reply-input-row { display: flex; gap: 8px; align-items: flex-end; }
        .reply-input-row textarea { flex: 1; padding: 10px 14px; border: 1px solid #ddd; border-radius: 10px; font-size: 13px; font-family: 'Prompt',sans-serif; resize: none; max-height: 100px; }
        .reply-input-row textarea:focus { outline: none; border-color: #5D4037; }
        .btn-send { width: 44px; height: 44px; border: none; border-radius: 50%; background: #5D4037; color: white; font-size: 16px; cursor: pointer; flex-shrink: 0; display: flex; align-items: center; justify-content: center; }
        .btn-send:hover { background: #4E342E; }
        .btn-ai-omni { width: 40px; height: 40px; border: none; border-radius: 50%; background: linear-gradient(135deg, #7C4DFF, #B388FF); color: white; font-size: 14px; cursor: pointer; flex-shrink: 0; }

        /* Empty State */
        .empty-state { display: flex; flex-direction: column; align-items: center; justify-content: center; height: 100%; color: #ccc; }
        .empty-state i { font-size: 48px; margin-bottom: 15px; }
        .empty-state h4 { margin: 0 0 5px; color: #999; font-size: 16px; }
        .empty-state p { margin: 0; font-size: 13px; color: #bbb; }

        @media (max-width: 900px) {
            .omni-layout { grid-template-columns: 1fr; height: auto; }
            .chat-panel { min-height: 500px; }
        }
    </style>

    <div class="omni-page">
        <div class="omni-header">
            <h2><i class="fas fa-comments"></i> Omni-Channel Inbox</h2>
            <div class="header-badges">
                <span class="stat-pill unread" id="pillUnread">0 ยังไม่อ่าน</span>
                <span class="stat-pill open" id="pillOpen">0 เปิดอยู่</span>
                <span class="stat-pill pending" id="pillPending">0 รอดำเนินการ</span>
            </div>
        </div>

        <div class="omni-layout">
            <!-- Left: Conversation List -->
            <div class="conv-sidebar">
                <div class="conv-filters">
                    <div class="filter-row">
                        <input type="text" id="txtSearch" placeholder="ค้นหาชื่อ เบอร์ ข้อความ..." oninput="debounceLoad()" />
                    </div>
                    <div class="filter-row">
                        <select id="ddlStatus" onchange="loadConversations()">
                            <option value="ALL">ทุกสถานะ</option>
                            <option value="OPEN" selected>เปิดอยู่</option>
                            <option value="PENDING">รอดำเนินการ</option>
                            <option value="RESOLVED">แก้ไขแล้ว</option>
                        </select>
                    </div>
                </div>

                <div class="channel-pills" id="channelPills">
                    <button class="ch-pill active" data-channel="ALL" onclick="filterChannel(this)">
                        <i class="fas fa-globe"></i> ทั้งหมด
                    </button>
                </div>

                <div class="conv-list" id="convList">
                    <div class="empty-state" style="padding:40px;">
                        <i class="fas fa-spinner fa-spin"></i>
                        <p>กำลังโหลด...</p>
                    </div>
                </div>
            </div>

            <!-- Right: Chat Detail -->
            <div class="chat-panel" id="chatPanel">
                <div class="empty-state" id="emptyState">
                    <i class="fas fa-inbox"></i>
                    <h4>Omni-Channel Inbox</h4>
                    <p>เลือกการสนทนาจากรายการด้านซ้าย</p>
                </div>

                <div id="chatContent" style="display:none; height:100%; display:none; flex-direction:column;">
                    <div class="chat-header" id="chatHeader"></div>
                    <div class="messages-area" id="messagesArea"></div>
                    <div class="reply-area">
                        <div class="canned-bar" id="cannedBar"></div>
                        <div class="reply-input-row">
                            <textarea id="txtReply" rows="1" placeholder="พิมพ์ข้อความตอบกลับ..." onkeypress="if(event.key==='Enter'&&!event.shiftKey){event.preventDefault();sendReply();}"></textarea>
                            <button type="button" class="btn-ai-omni" onclick="getAiSuggestion()" title="AI แนะนำ"><i class="fas fa-robot"></i></button>
                            <button type="button" class="btn-send" onclick="sendReply()"><i class="fas fa-paper-plane"></i></button>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    </div>

    <asp:HiddenField ID="hfChannelsData" runat="server" />
    <asp:HiddenField ID="hfCannedData" runat="server" />

    <script>
        var selectedConvId = 0;
        var channels = [];
        var cannedResponses = [];
        var currentChannel = 'ALL';
        var debounceTimer = null;
        var pollTimer = null;

        $(document).ready(function () {
            try { channels = JSON.parse($('#<%= hfChannelsData.ClientID %>').val() || '[]'); } catch (e) { }
            try { cannedResponses = JSON.parse($('#<%= hfCannedData.ClientID %>').val() || '[]'); } catch (e) { }
            buildChannelPills();
            buildCannedBar();
            loadConversations();
            loadStats();
            pollTimer = setInterval(function () { loadConversations(true); loadStats(); }, 5000);

            // Auto-resize reply textarea
            $('#txtReply').on('input', function () {
                this.style.height = 'auto';
                this.style.height = Math.min(this.scrollHeight, 100) + 'px';
            });
        });

        function buildChannelPills() {
            var html = '<button class="ch-pill active" data-channel="ALL" onclick="filterChannel(this)"><i class="fas fa-globe"></i> ทั้งหมด</button>';
            for (var i = 0; i < channels.length; i++) {
                var ch = channels[i];
                html += '<button class="ch-pill" data-channel="' + ch.code + '" onclick="filterChannel(this)" style="--brand:' + ch.color + '">';
                html += '<i class="' + ch.icon + '"></i> ' + ch.name + '</button>';
            }
            $('#channelPills').html(html);
        }

        function buildCannedBar() {
            var html = '';
            for (var i = 0; i < cannedResponses.length; i++) {
                var cr = cannedResponses[i];
                html += '<button class="canned-btn" onclick="useCanned(' + cr.id + ',\'' + escapeAttr(cr.content) + '\')" title="' + escapeAttr(cr.content) + '">' + cr.shortcut + ' ' + cr.title + '</button>';
            }
            $('#cannedBar').html(html);
        }

        function filterChannel(el) {
            $('.ch-pill').removeClass('active');
            $(el).addClass('active');
            currentChannel = $(el).data('channel');
            loadConversations();
        }

        function debounceLoad() {
            clearTimeout(debounceTimer);
            debounceTimer = setTimeout(loadConversations, 300);
        }

        function loadConversations(silent) {
            var data = {
                channel: currentChannel,
                status: $('#ddlStatus').val(),
                search: $('#txtSearch').val().trim()
            };

            $.ajax({
                url: window.location.pathname + '?action=conversations',
                type: 'POST', contentType: 'application/json',
                data: JSON.stringify(data),
                success: function (r) {
                    if (r && r.conversations) renderConvList(r.conversations);
                }
            });
        }

        function loadStats() {
            $.ajax({
                url: window.location.pathname + '?action=stats',
                type: 'GET', dataType: 'json',
                success: function (r) {
                    if (r) {
                        $('#pillUnread').text((r.unread || 0) + ' ยังไม่อ่าน');
                        $('#pillOpen').text((r.open || 0) + ' เปิดอยู่');
                        $('#pillPending').text((r.pending || 0) + ' รอดำเนินการ');
                    }
                }
            });
        }

        function renderConvList(convs) {
            if (!convs || convs.length === 0) {
                $('#convList').html('<div class="empty-state" style="padding:40px;"><i class="fas fa-inbox"></i><p>ไม่มีการสนทนา</p></div>');
                return;
            }
            var html = '';
            for (var i = 0; i < convs.length; i++) {
                var c = convs[i];
                var cls = 'conv-item' + (c.id === selectedConvId ? ' active' : '') + (c.unread > 0 ? ' has-unread' : '');
                html += '<div class="' + cls + '" onclick="openConversation(' + c.id + ')" style="' + (c.unread > 0 ? 'border-left:3px solid ' + c.brandColor : '') + '">';
                html += '<div class="conv-avatar" style="background:' + c.brandColor + '"><i class="' + c.iconClass + '"></i></div>';
                html += '<div class="conv-body">';
                html += '<div class="conv-top"><span class="conv-name">' + escHtml(c.name || 'Unknown') + '</span>';
                html += '<span class="conv-time">' + (c.lastTime || '') + '</span></div>';
                html += '<div class="conv-preview">' + escHtml(c.preview || '') + '</div>';
                html += '<div class="conv-meta">';
                html += '<span class="conv-channel-badge" style="background:' + c.brandColor + '">' + escHtml(c.channelName) + '</span>';
                if (c.unread > 0) html += '<span class="conv-unread-badge">' + c.unread + '</span>';
                if (c.assigned) html += '<span class="conv-assigned"><i class="fas fa-user"></i> ' + escHtml(c.assigned) + '</span>';
                html += '</div></div></div>';
            }
            $('#convList').html(html);
        }

        function openConversation(id) {
            selectedConvId = id;
            $('.conv-item').removeClass('active');
            // Highlight in list
            loadConversations(true);

            $('#emptyState').hide();
            $('#chatContent').css('display', 'flex');

            $.ajax({
                url: window.location.pathname + '?action=detail',
                type: 'POST', contentType: 'application/json',
                data: JSON.stringify({ conversationId: id }),
                success: function (r) {
                    if (r && r.success) renderChatDetail(r);
                }
            });
        }

        function renderChatDetail(data) {
            // Header
            var c = data.conversation;
            var headerHtml = '<div class="chat-header-left">';
            headerHtml += '<div class="ch-icon" style="background:' + c.brandColor + '"><i class="' + c.iconClass + '"></i></div>';
            headerHtml += '<div class="chat-header-info"><h4>' + escHtml(c.name) + '</h4>';
            headerHtml += '<p><i class="' + c.iconClass + '"></i> ' + escHtml(c.channelName);
            if (c.phone) headerHtml += ' &bull; ' + escHtml(c.phone);
            headerHtml += '</p></div></div>';
            headerHtml += '<div class="chat-actions">';
            if (c.status !== 'RESOLVED')
                headerHtml += '<button class="btn-act success" onclick="resolveConv()"><i class="fas fa-check"></i> แก้ไขแล้ว</button>';
            else
                headerHtml += '<button class="btn-act primary" onclick="reopenConv()"><i class="fas fa-redo"></i> เปิดใหม่</button>';
            headerHtml += '<button class="btn-act" onclick="assignConv()"><i class="fas fa-user-plus"></i> มอบหมาย</button>';
            headerHtml += '</div>';
            $('#chatHeader').html(headerHtml);

            // Messages
            var msgs = data.messages || [];
            var msgHtml = '';
            var lastDate = '';
            for (var i = 0; i < msgs.length; i++) {
                var m = msgs[i];
                if (m.dateLabel !== lastDate) {
                    msgHtml += '<div class="msg-date-sep"><span>' + m.dateLabel + '</span></div>';
                    lastDate = m.dateLabel;
                }
                var cls = m.direction === 'IN' ? 'incoming' : 'outgoing';
                msgHtml += '<div class="msg ' + cls + '"><div>';
                if (m.direction === 'IN' && m.sender) msgHtml += '<div class="msg-sender">' + escHtml(m.sender) + '</div>';
                msgHtml += '<div class="bubble">' + escHtml(m.content) + '</div>';
                msgHtml += '<div class="msg-meta">' + (m.time || '') + '</div>';
                msgHtml += '</div></div>';
            }
            $('#messagesArea').html(msgHtml);
            var area = document.getElementById('messagesArea');
            area.scrollTop = area.scrollHeight;
        }

        function sendReply() {
            var text = $('#txtReply').val().trim();
            if (!text || !selectedConvId) return;
            $('#txtReply').val('').css('height', 'auto');

            // Optimistic add
            var now = new Date();
            var time = ('0' + now.getHours()).slice(-2) + ':' + ('0' + now.getMinutes()).slice(-2);
            $('#messagesArea').append('<div class="msg outgoing"><div><div class="bubble">' + escHtml(text) + '</div><div class="msg-meta">' + time + '</div></div></div>');
            var area = document.getElementById('messagesArea');
            area.scrollTop = area.scrollHeight;

            $.ajax({
                url: window.location.pathname + '?action=send',
                type: 'POST', contentType: 'application/json',
                data: JSON.stringify({ conversationId: selectedConvId, content: text }),
                success: function (r) {
                    if (!r || !r.success) alert(r ? r.message : 'ส่งไม่สำเร็จ');
                    loadConversations(true);
                }
            });
        }

        function resolveConv() {
            if (!selectedConvId) return;
            $.ajax({
                url: window.location.pathname + '?action=updateStatus',
                type: 'POST', contentType: 'application/json',
                data: JSON.stringify({ conversationId: selectedConvId, status: 'RESOLVED' }),
                success: function () { openConversation(selectedConvId); loadConversations(); loadStats(); }
            });
        }

        function reopenConv() {
            if (!selectedConvId) return;
            $.ajax({
                url: window.location.pathname + '?action=updateStatus',
                type: 'POST', contentType: 'application/json',
                data: JSON.stringify({ conversationId: selectedConvId, status: 'OPEN' }),
                success: function () { openConversation(selectedConvId); loadConversations(); loadStats(); }
            });
        }

        function assignConv() {
            var name = prompt('มอบหมายให้ (ชื่อพนักงาน):');
            if (!name || !selectedConvId) return;
            $.ajax({
                url: window.location.pathname + '?action=assign',
                type: 'POST', contentType: 'application/json',
                data: JSON.stringify({ conversationId: selectedConvId, assignedTo: name }),
                success: function () { openConversation(selectedConvId); loadConversations(); }
            });
        }

        function useCanned(id, content) {
            $('#txtReply').val(content).focus();
            $.ajax({
                url: window.location.pathname + '?action=cannedUsed',
                type: 'POST', contentType: 'application/json',
                data: JSON.stringify({ id: id })
            });
        }

        function getAiSuggestion() {
            if (!selectedConvId) return;
            var btn = $('.btn-ai-omni');
            btn.html('<i class="fas fa-spinner fa-spin"></i>').prop('disabled', true);
            $.ajax({
                url: window.location.pathname + '?action=aiSuggest',
                type: 'POST', contentType: 'application/json',
                data: JSON.stringify({ conversationId: selectedConvId }),
                success: function (r) {
                    btn.html('<i class="fas fa-robot"></i>').prop('disabled', false);
                    if (r && r.success) $('#txtReply').val(r.suggestion).focus();
                    else alert(r ? r.message : 'ไม่สามารถสร้างคำแนะนำได้');
                },
                error: function () { btn.html('<i class="fas fa-robot"></i>').prop('disabled', false); }
            });
        }

        function escHtml(s) { return $('<div>').text(s || '').html(); }
        function escapeAttr(s) { return (s || '').replace(/'/g, "\\'").replace(/"/g, '&quot;'); }
    </script>
</asp:Content>
