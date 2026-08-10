<%@ Page Title="Channel Settings" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ChannelSettings.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Chat.ChannelSettings" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .ch-page { max-width: 1200px; margin: 0 auto; padding: 20px; }
        .page-header { margin-bottom: 25px; }
        .page-header h1 { color: #5D4037; margin: 0 0 8px; font-size: 24px; display: flex; align-items: center; gap: 10px; }
        .page-header h1 i { color: #5D4037; }
        .page-header p { color: #999; margin: 0; font-size: 14px; }

        .webhook-url-box { background: #EFEBE9; border: 1px solid #D7CCC8; border-radius: 8px; padding: 12px 16px; margin-bottom: 25px; display: flex; align-items: center; gap: 10px; }
        .webhook-url-box label { font-size: 12px; font-weight: 600; color: #5D4037; white-space: nowrap; }
        .webhook-url-box code { flex: 1; font-size: 12px; color: #333; word-break: break-all; }
        .webhook-url-box button { padding: 6px 12px; border: 1px solid #5D4037; background: white; border-radius: 6px; font-size: 12px; cursor: pointer; font-family: 'Prompt',sans-serif; }

        /* Channel type sections */
        .ch-section { margin-bottom: 30px; }
        .ch-section h3 { font-size: 16px; color: #333; margin: 0 0 15px; display: flex; align-items: center; gap: 8px; }
        .ch-section h3 i { color: #5D4037; }

        .ch-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(340px, 1fr)); gap: 16px; }

        .ch-card { background: white; border-radius: 12px; padding: 20px; box-shadow: 0 2px 8px rgba(0,0,0,0.06); border: 1px solid #f0f0f0; transition: all 0.2s; }
        .ch-card.enabled { border-color: #4CAF50; }
        .ch-card-header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 15px; }
        .ch-card-title { display: flex; align-items: center; gap: 10px; }
        .ch-icon { width: 40px; height: 40px; border-radius: 10px; display: flex; align-items: center; justify-content: center; color: white; font-size: 18px; }
        .ch-card-title h4 { margin: 0; font-size: 15px; color: #333; }
        .ch-card-title span { font-size: 11px; color: #999; }

        .toggle-switch { position: relative; width: 44px; height: 24px; flex-shrink: 0; }
        .toggle-switch input { opacity: 0; width: 0; height: 0; }
        .toggle-slider { position: absolute; cursor: pointer; top: 0; left: 0; right: 0; bottom: 0; background: #ccc; border-radius: 24px; transition: 0.3s; }
        .toggle-slider:before { position: absolute; content: ""; height: 18px; width: 18px; left: 3px; bottom: 3px; background: white; border-radius: 50%; transition: 0.3s; }
        .toggle-switch input:checked + .toggle-slider { background: #4CAF50; }
        .toggle-switch input:checked + .toggle-slider:before { transform: translateX(20px); }

        .ch-config { border-top: 1px solid #f0f0f0; padding-top: 12px; }
        .cfg-item { margin-bottom: 10px; }
        .cfg-item label { display: block; font-size: 12px; font-weight: 500; color: #555; margin-bottom: 4px; }
        .cfg-item input { width: 100%; padding: 7px 10px; border: 1px solid #ddd; border-radius: 6px; font-size: 12px; font-family: 'Prompt',sans-serif; box-sizing: border-box; }
        .cfg-item input:focus { outline: none; border-color: #5D4037; }
        .cfg-item .hint { font-size: 10px; color: #999; margin-top: 2px; }

        .ch-card-actions { display: flex; gap: 6px; margin-top: 12px; }
        .btn-save { padding: 7px 14px; background: #5D4037; color: white; border: none; border-radius: 6px; font-size: 12px; cursor: pointer; font-family: 'Prompt',sans-serif; }
        .btn-save:hover { background: #4E342E; }
        .btn-test { padding: 7px 14px; background: white; color: #5D4037; border: 1px solid #5D4037; border-radius: 6px; font-size: 12px; cursor: pointer; font-family: 'Prompt',sans-serif; }
        .btn-test:hover { background: #EFEBE9; }
        .save-result { font-size: 12px; margin-top: 8px; padding: 6px 10px; border-radius: 6px; display: none; }
        .save-result.ok { display: block; background: #E8F5E9; color: #2E7D32; }
        .save-result.err { display: block; background: #FFEBEE; color: #C62828; }

        @media (max-width: 768px) { .ch-grid { grid-template-columns: 1fr; } }
    </style>

    <asp:HiddenField ID="hfChannels" runat="server" />

    <div class="ch-page">
        <div class="page-header">
            <h1><i class="fas fa-plug"></i> Channel Settings</h1>
            <p>ตั้งค่าช่องทางการสื่อสาร — เชื่อมต่อ LINE, Facebook, WhatsApp, OTA และอื่นๆ</p>
        </div>

        <div class="webhook-url-box">
            <label>Webhook URL:</label>
            <code id="webhookUrl"></code>
            <button type="button" onclick="copyWebhook()"><i class="fas fa-copy"></i> คัดลอก</button>
        </div>

        <div id="channelSections"></div>
    </div>

    <script>
        var allChannels = [];
        var baseWebhookUrl = '';

        $(document).ready(function () {
            try { allChannels = JSON.parse($('#<%= hfChannels.ClientID %>').val() || '[]'); } catch (e) { }
            baseWebhookUrl = window.location.origin + '<%= ResolveUrl("~/API/OmniChannelWebhook.ashx") %>';
            $('#webhookUrl').text(baseWebhookUrl + '?channel={CHANNEL_CODE}');
            renderChannels();
        });

        var configFields = {
            'LINE': [
                { key: 'channelAccessToken', label: 'Channel Access Token', hint: 'จาก LINE Developers Console > Messaging API' },
                { key: 'channelSecret', label: 'Channel Secret', hint: 'ใช้ตรวจสอบ Webhook Signature' }
            ],
            'FACEBOOK': [
                { key: 'pageAccessToken', label: 'Page Access Token', hint: 'จาก Facebook App > Messenger Settings' },
                { key: 'verifyToken', label: 'Verify Token', hint: 'สร้างเองและกรอกในตั้งค่า Webhook ของ Facebook App' },
                { key: 'appSecret', label: 'App Secret', hint: 'จาก Facebook App > Settings > Basic' }
            ],
            'WHATSAPP': [
                { key: 'accessToken', label: 'Access Token', hint: 'จาก Meta Business > WhatsApp > API Setup' },
                { key: 'phoneNumberId', label: 'Phone Number ID', hint: 'รหัสเบอร์โทรธุรกิจจาก WhatsApp Business API' },
                { key: 'verifyToken', label: 'Verify Token', hint: 'สร้างเองสำหรับตรวจสอบ Webhook' }
            ],
            'WECHAT': [
                { key: 'appId', label: 'App ID', hint: 'จาก WeChat Official Account Platform' },
                { key: 'appSecret', label: 'App Secret', hint: '' },
                { key: 'token', label: 'Token', hint: 'ใช้ตรวจสอบ Webhook' }
            ],
            'INSTAGRAM': [
                { key: 'pageAccessToken', label: 'Page Access Token', hint: 'ใช้ token เดียวกับ Facebook Page ที่เชื่อมกัน' }
            ],
            'TELEGRAM': [
                { key: 'botToken', label: 'Bot Token', hint: 'จาก @BotFather' }
            ],
            'AGODA': [
                { key: 'hotelId', label: 'Hotel ID', hint: 'รหัสโรงแรมบน Agoda' },
                { key: 'apiKey', label: 'API Key (ถ้ามี)', hint: 'สำหรับ Agoda Partner API' }
            ],
            'BOOKING': [
                { key: 'hotelId', label: 'Hotel ID', hint: 'รหัสโรงแรมบน Booking.com' },
                { key: 'username', label: 'API Username', hint: 'จาก Booking.com Connectivity Partner' },
                { key: 'password', label: 'API Password', hint: '' }
            ],
            'TRIP': [
                { key: 'hotelId', label: 'Hotel ID', hint: 'รหัสโรงแรมบน Trip.com' },
                { key: 'apiKey', label: 'API Key', hint: '' }
            ],
            'EXPEDIA': [
                { key: 'hotelId', label: 'Hotel ID', hint: 'รหัสโรงแรมบน Expedia' },
                { key: 'apiKey', label: 'API Key', hint: 'จาก Expedia Partner Central' }
            ],
            'EMAIL': [
                { key: 'fromDomains', label: 'โดเมนอีเมลลูกค้า OTA', hint: 'คั่นด้วยจุลภาค — ค่าเริ่มต้น agoda-messaging.com, mchat.booking.com, guest.booking.com (อีเมลจากโดเมนเหล่านี้ = ข้อความลูกค้า จะเข้ากล่องแชทอัตโนมัติ)' },
                { key: 'pollMinutes', label: 'รอบดึงอีเมล (นาที)', hint: 'ค่าเริ่มต้น 3 นาที — ใช้กล่องอีเมล IMAP เดียวกับระบบอ่านอีเมลจอง (ตั้งที่ Admin → Accounting Integration)' },
                { key: 'processedLabel', label: 'โฟลเดอร์เก็บอีเมลที่อ่านแล้ว', hint: 'ค่าเริ่มต้น Chat-Processed' },
                { key: 'notifyTelegram', label: 'แจ้งเตือน Telegram (1/0)', hint: 'แจ้งพนักงานทันทีเมื่อลูกค้าส่งข้อความมา' },
                { key: 'signature', label: 'ลายเซ็นท้ายอีเมลตอบกลับ', hint: 'ต่อท้ายทุกข้อความที่ส่งถึงลูกค้า เช่น ชื่อที่พัก + เบอร์โทร' }
            ],
            'SMS': [
                { key: 'provider', label: 'SMS Provider', hint: 'ชื่อผู้ให้บริการ SMS' },
                { key: 'apiKey', label: 'API Key', hint: '' }
            ]
        };

        function renderChannels() {
            var types = { 'SOCIAL': 'โซเชียลมีเดีย', 'OTA': 'OTA (Online Travel Agency)', 'EMAIL': 'อีเมลและ SMS', 'SMS': 'อีเมลและ SMS', 'INTERNAL': 'ภายในระบบ' };
            var grouped = {};
            for (var i = 0; i < allChannels.length; i++) {
                var ch = allChannels[i];
                var group = ch.type === 'SMS' ? 'EMAIL' : ch.type;
                if (!grouped[group]) grouped[group] = [];
                grouped[group].push(ch);
            }

            var order = ['SOCIAL', 'OTA', 'EMAIL', 'INTERNAL'];
            var html = '';
            for (var o = 0; o < order.length; o++) {
                var key = order[o];
                if (!grouped[key] || grouped[key].length === 0) continue;
                var label = types[key] || key;
                html += '<div class="ch-section"><h3><i class="fas fa-layer-group"></i> ' + label + '</h3><div class="ch-grid">';
                for (var j = 0; j < grouped[key].length; j++) {
                    html += renderChannelCard(grouped[key][j]);
                }
                html += '</div></div>';
            }
            $('#channelSections').html(html);
        }

        function renderChannelCard(ch) {
            var fields = configFields[ch.code] || [];
            var cfg = ch.config || {};
            var cardClass = ch.enabled ? 'ch-card enabled' : 'ch-card';

            var html = '<div class="' + cardClass + '" id="card_' + ch.code + '">';
            html += '<div class="ch-card-header">';
            html += '<div class="ch-card-title"><div class="ch-icon" style="background:' + ch.color + '"><i class="' + ch.icon + '"></i></div>';
            html += '<div><h4>' + ch.name + '</h4><span>' + ch.code + '</span></div></div>';
            if (ch.code !== 'GUEST_PORTAL') {
                html += '<label class="toggle-switch"><input type="checkbox" ' + (ch.enabled ? 'checked' : '') + ' onchange="toggleChannel(\'' + ch.code + '\', ' + ch.id + ', this.checked)" /><span class="toggle-slider"></span></label>';
            } else {
                html += '<span style="font-size:11px;color:#4CAF50;font-weight:600;">เปิดตลอด</span>';
            }
            html += '</div>';

            if (fields.length > 0) {
                html += '<div class="ch-config">';
                for (var i = 0; i < fields.length; i++) {
                    var f = fields[i];
                    var val = cfg[f.key] || '';
                    html += '<div class="cfg-item"><label>' + f.label + '</label>';
                    html += '<input type="text" id="cfg_' + ch.code + '_' + f.key + '" value="' + escAttr(val) + '" placeholder="' + f.label + '" />';
                    if (f.hint) html += '<div class="hint">' + f.hint + '</div>';
                    html += '</div>';
                }
                html += '<div class="ch-card-actions">';
                html += '<button type="button" class="btn-save" onclick="saveChannel(\'' + ch.code + '\', ' + ch.id + ')"><i class="fas fa-save"></i> บันทึก</button>';
                if (ch.code !== 'GUEST_PORTAL') {
                    html += '<button type="button" class="btn-test" onclick="copyChannelWebhook(\'' + ch.code + '\')"><i class="fas fa-link"></i> Webhook URL</button>';
                }
                html += '</div>';
                html += '<div class="save-result" id="result_' + ch.code + '"></div>';
                html += '</div>';
            }

            html += '</div>';
            return html;
        }

        function toggleChannel(code, id, enabled) {
            $.ajax({
                url: window.location.pathname + '?action=toggle',
                type: 'POST', contentType: 'application/json',
                data: JSON.stringify({ id: id, enabled: enabled }),
                success: function (r) {
                    var card = $('#card_' + code);
                    if (enabled) card.addClass('enabled'); else card.removeClass('enabled');
                }
            });
        }

        function saveChannel(code, id) {
            var fields = configFields[code] || [];
            var config = {};
            for (var i = 0; i < fields.length; i++) {
                config[fields[i].key] = $('#cfg_' + code + '_' + fields[i].key).val().trim();
            }

            $.ajax({
                url: window.location.pathname + '?action=saveConfig',
                type: 'POST', contentType: 'application/json',
                data: JSON.stringify({ code: code, config: config }),
                success: function (r) {
                    var el = $('#result_' + code);
                    if (r && r.success) el.removeClass('err').addClass('ok').text('✅ บันทึกสำเร็จ').show();
                    else el.removeClass('ok').addClass('err').text('❌ ' + (r ? r.message : 'Error')).show();
                    setTimeout(function () { el.fadeOut(); }, 3000);
                }
            });
        }

        function copyChannelWebhook(code) {
            var url = baseWebhookUrl + '?channel=' + code;
            navigator.clipboard.writeText(url).then(function () {
                alert('คัดลอก Webhook URL สำเร็จ:\n' + url);
            });
        }

        function copyWebhook() {
            navigator.clipboard.writeText(baseWebhookUrl + '?channel={CHANNEL_CODE}').then(function () {
                alert('คัดลอก Webhook URL สำเร็จ');
            });
        }

        function escAttr(s) { return (s || '').replace(/"/g, '&quot;').replace(/'/g, '&#39;'); }
    </script>
</asp:Content>
