<%@ Page Title="AI Settings (DeepSeek)" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="AISettings.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Settings.AISettings" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .ai-page { padding: 20px 0; max-width: 1200px; margin: 0 auto; }

        .page-header { margin-bottom: 30px; display: flex; justify-content: space-between; align-items: flex-start; flex-wrap: wrap; gap: 15px; }
        .page-header h1 { color: #5D4037; margin: 0 0 8px 0; font-size: 24px; display: flex; align-items: center; gap: 12px; }
        .page-header h1 i { color: #7C4DFF; }
        .page-header p { color: #999; margin: 0; font-size: 14px; }

        .ai-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(380px, 1fr)); gap: 20px; }
        .ai-card { background: white; border-radius: 12px; padding: 25px; box-shadow: 0 2px 10px rgba(0,0,0,0.08); }
        .ai-card h3 { margin: 0 0 20px 0; font-size: 16px; color: #333; display: flex; align-items: center; gap: 10px; border-bottom: 2px solid #f0f0f0; padding-bottom: 15px; }
        .ai-card h3 i { color: #7C4DFF; font-size: 20px; }

        .config-item { padding: 12px 0; border-bottom: 1px solid #f5f5f5; }
        .config-item:last-child { border-bottom: none; }
        .config-item label { display: block; font-weight: 500; margin-bottom: 6px; font-size: 13px; color: #555; }
        .config-item input, .config-item select, .config-item textarea {
            width: 100%; padding: 8px 12px; border: 1px solid #ddd; border-radius: 6px;
            font-size: 13px; font-family: 'Prompt', sans-serif; box-sizing: border-box;
        }
        .config-item input:focus, .config-item select:focus, .config-item textarea:focus { outline: none; border-color: #7C4DFF; }
        .config-item textarea { resize: vertical; min-height: 80px; }
        .config-item .help-text { font-size: 11px; color: #999; margin-top: 4px; }

        .toggle-row { display: flex; align-items: center; justify-content: space-between; padding: 14px 0; border-bottom: 1px solid #f5f5f5; }
        .toggle-row:last-child { border-bottom: none; }
        .toggle-label { font-size: 13px; font-weight: 500; color: #333; }
        .toggle-label span { display: block; font-weight: 400; font-size: 11px; color: #999; margin-top: 2px; }

        .toggle-switch { position: relative; width: 48px; height: 26px; flex-shrink: 0; }
        .toggle-switch input { opacity: 0; width: 0; height: 0; }
        .toggle-slider { position: absolute; cursor: pointer; top: 0; left: 0; right: 0; bottom: 0; background: #ccc; border-radius: 26px; transition: 0.3s; }
        .toggle-slider:before { position: absolute; content: ""; height: 20px; width: 20px; left: 3px; bottom: 3px; background: white; border-radius: 50%; transition: 0.3s; }
        .toggle-switch input:checked + .toggle-slider { background: #7C4DFF; }
        .toggle-switch input:checked + .toggle-slider:before { transform: translateX(22px); }

        .status-badge { padding: 4px 12px; border-radius: 10px; font-size: 11px; font-weight: 600; }
        .status-connected { background: #E8F5E9; color: #2E7D32; }
        .status-not-configured { background: #FFF3E0; color: #E65100; }
        .status-error { background: #FFEBEE; color: #C62828; }

        .btn-row { display: flex; gap: 8px; margin-top: 15px; flex-wrap: wrap; }
        .btn-primary { padding: 10px 20px; background: #7C4DFF; color: white; border: none; border-radius: 8px; cursor: pointer; font-weight: 500; font-size: 13px; font-family: 'Prompt', sans-serif; display: inline-flex; align-items: center; gap: 6px; }
        .btn-primary:hover { background: #651FFF; }
        .btn-success { padding: 10px 20px; background: #4CAF50; color: white; border: none; border-radius: 8px; cursor: pointer; font-weight: 500; font-size: 13px; font-family: 'Prompt', sans-serif; display: inline-flex; align-items: center; gap: 6px; }
        .btn-success:hover { background: #388E3C; }
        .btn-outline { padding: 10px 20px; background: white; color: #7C4DFF; border: 1px solid #7C4DFF; border-radius: 8px; cursor: pointer; font-weight: 500; font-size: 13px; font-family: 'Prompt', sans-serif; display: inline-flex; align-items: center; gap: 6px; }
        .btn-outline:hover { background: #F3E5F5; }
        .btn-danger { padding: 10px 20px; background: #F44336; color: white; border: none; border-radius: 8px; cursor: pointer; font-weight: 500; font-size: 13px; font-family: 'Prompt', sans-serif; display: inline-flex; align-items: center; gap: 6px; }
        .btn-danger:hover { background: #D32F2F; }

        .test-result { margin-top: 12px; padding: 12px 15px; border-radius: 8px; font-size: 13px; display: none; }
        .test-result.success { display: block; background: #E8F5E9; color: #2E7D32; border: 1px solid #C8E6C9; }
        .test-result.error { display: block; background: #FFEBEE; color: #C62828; border: 1px solid #FFCDD2; }
        .test-result.loading { display: block; background: #EDE7F6; color: #4527A0; border: 1px solid #D1C4E9; }

        /* Stats Grid */
        .stats-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(140px, 1fr)); gap: 12px; margin-bottom: 20px; }
        .stat-card { background: #f9f9f9; border-radius: 10px; padding: 16px; text-align: center; }
        .stat-card .stat-value { font-size: 24px; font-weight: 700; color: #333; }
        .stat-card .stat-label { font-size: 11px; color: #999; margin-top: 4px; }
        .stat-card.purple .stat-value { color: #7C4DFF; }
        .stat-card.green .stat-value { color: #4CAF50; }
        .stat-card.orange .stat-value { color: #FF9800; }
        .stat-card.red .stat-value { color: #F44336; }

        /* Test Chat */
        .chat-test-box { background: #f9f9f9; border-radius: 10px; padding: 15px; margin-top: 15px; }
        .chat-messages { max-height: 300px; overflow-y: auto; margin-bottom: 12px; padding: 10px; background: white; border-radius: 8px; min-height: 100px; }
        .chat-msg { margin-bottom: 10px; display: flex; }
        .chat-msg.user { justify-content: flex-end; }
        .chat-msg .bubble { max-width: 80%; padding: 10px 14px; border-radius: 12px; font-size: 13px; line-height: 1.5; white-space: pre-wrap; word-wrap: break-word; }
        .chat-msg.user .bubble { background: #7C4DFF; color: white; border-bottom-right-radius: 4px; }
        .chat-msg.assistant .bubble { background: #f0f0f0; color: #333; border-bottom-left-radius: 4px; }
        .chat-input-row { display: flex; gap: 8px; }
        .chat-input-row input { flex: 1; padding: 10px 14px; border: 1px solid #ddd; border-radius: 8px; font-size: 13px; font-family: 'Prompt', sans-serif; }
        .chat-input-row input:focus { outline: none; border-color: #7C4DFF; }
        .chat-input-row button { padding: 10px 18px; background: #7C4DFF; color: white; border: none; border-radius: 8px; cursor: pointer; font-size: 13px; font-family: 'Prompt', sans-serif; }

        /* Provider logo */
        .provider-badge { display: inline-flex; align-items: center; gap: 8px; background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%); color: white; padding: 8px 16px; border-radius: 8px; font-size: 13px; font-weight: 600; margin-bottom: 15px; }
        .provider-badge img { height: 20px; }

        @media (max-width: 768px) {
            .ai-grid { grid-template-columns: 1fr; }
            .stats-grid { grid-template-columns: repeat(2, 1fr); }
            .page-header { flex-direction: column; }
        }
    </style>

    <asp:HiddenField ID="hfConfigData" runat="server" />

    <div class="ai-page">
        <div class="page-header">
            <div>
                <h1><i class="fas fa-robot"></i> AI Settings</h1>
                <p>ตั้งค่าการเชื่อมต่อ DeepSeek AI สำหรับระบบผู้ช่วยอัจฉริยะ</p>
            </div>
            <div>
                <span id="spnStatus" class="status-badge status-not-configured">ยังไม่ได้ตั้งค่า</span>
            </div>
        </div>

        <!-- Usage Stats -->
        <div class="stats-grid" id="statsGrid" style="display:none;">
            <div class="stat-card purple">
                <div class="stat-value" id="statTodayReqs">0</div>
                <div class="stat-label">คำขอวันนี้</div>
            </div>
            <div class="stat-card green">
                <div class="stat-value" id="statTodayTokens">0</div>
                <div class="stat-label">Tokens วันนี้</div>
            </div>
            <div class="stat-card orange">
                <div class="stat-value" id="statTotalTokens">0</div>
                <div class="stat-label">Tokens ทั้งหมด</div>
            </div>
            <div class="stat-card">
                <div class="stat-value" id="statAvgMs">0</div>
                <div class="stat-label">Avg Response (ms)</div>
            </div>
        </div>

        <div class="ai-grid">
            <!-- Card 1: API Connection -->
            <div class="ai-card">
                <h3><i class="fas fa-plug"></i> การเชื่อมต่อ API</h3>
                <div class="provider-badge">
                    <i class="fas fa-brain"></i> DeepSeek AI
                </div>
                <div class="config-item">
                    <label>API Base URL</label>
                    <input type="text" id="txtBaseUrl" value="https://api.deepseek.com" placeholder="https://api.deepseek.com" />
                    <div class="help-text">URL ของ DeepSeek API (ค่าเริ่มต้น: https://api.deepseek.com)</div>
                </div>
                <div class="config-item">
                    <label>API Key</label>
                    <input type="password" id="txtApiKey" placeholder="sk-xxxxxxxxxxxxxxxx" />
                    <div class="help-text" id="apiKeyHint">รับ API Key ได้ที่ platform.deepseek.com</div>
                </div>
                <div class="config-item">
                    <label>Model</label>
                    <select id="ddlModel">
                        <option value="deepseek-chat">DeepSeek Chat (deepseek-chat)</option>
                        <option value="deepseek-reasoner">DeepSeek Reasoner (deepseek-reasoner)</option>
                    </select>
                    <div class="help-text">deepseek-chat เหมาะสำหรับการสนทนาทั่วไป / deepseek-reasoner สำหรับงานที่ต้องการเหตุผลเชิงลึก</div>
                </div>
                <div class="btn-row">
                    <button type="button" class="btn-primary" onclick="saveApiConfig()"><i class="fas fa-save"></i> บันทึก</button>
                    <button type="button" class="btn-outline" onclick="testConnection()"><i class="fas fa-plug"></i> ทดสอบการเชื่อมต่อ</button>
                </div>
                <div id="testResult" class="test-result"></div>
            </div>

            <!-- Card 2: Features Toggle -->
            <div class="ai-card">
                <h3><i class="fas fa-sliders-h"></i> เปิด/ปิดฟีเจอร์</h3>

                <div class="toggle-row">
                    <div class="toggle-label">
                        เปิดใช้งาน AI
                        <span>เปิด/ปิดระบบ AI ทั้งหมด</span>
                    </div>
                    <label class="toggle-switch">
                        <input type="checkbox" id="chkEnabled" onchange="saveFeatures()" />
                        <span class="toggle-slider"></span>
                    </label>
                </div>

                <div class="toggle-row">
                    <div class="toggle-label">
                        AI ผู้ช่วยแขก (Guest Chat)
                        <span>แชทบอท AI ตอบคำถามแขกอัตโนมัติ</span>
                    </div>
                    <label class="toggle-switch">
                        <input type="checkbox" id="chkGuestChat" onchange="saveFeatures()" />
                        <span class="toggle-slider"></span>
                    </label>
                </div>

                <div class="toggle-row">
                    <div class="toggle-label">
                        AI แนะนำคำตอบ (Admin)
                        <span>แนะนำข้อความตอบกลับสำหรับพนักงาน</span>
                    </div>
                    <label class="toggle-switch">
                        <input type="checkbox" id="chkAdminSuggest" onchange="saveFeatures()" />
                        <span class="toggle-slider"></span>
                    </label>
                </div>
            </div>

            <!-- Card 3: Parameters -->
            <div class="ai-card">
                <h3><i class="fas fa-cogs"></i> พารามิเตอร์</h3>
                <div class="config-item">
                    <label>Max Tokens</label>
                    <input type="number" id="txtMaxTokens" value="2048" min="100" max="8192" />
                    <div class="help-text">จำนวนคำสูงสุดที่ AI จะตอบต่อครั้ง (100-8192)</div>
                </div>
                <div class="config-item">
                    <label>Temperature</label>
                    <input type="number" id="txtTemperature" value="0.7" min="0" max="2" step="0.1" />
                    <div class="help-text">ความสร้างสรรค์ในการตอบ (0 = แม่นยำ, 2 = สร้างสรรค์สูง)</div>
                </div>
                <div class="config-item">
                    <label>Timeout (วินาที)</label>
                    <input type="number" id="txtTimeout" value="30" min="5" max="120" />
                </div>
                <div class="config-item">
                    <label>Max Chat History</label>
                    <input type="number" id="txtMaxHistory" value="20" min="1" max="50" />
                    <div class="help-text">จำนวนข้อความเก่าที่ส่งให้ AI เพื่อบริบท</div>
                </div>
                <div class="btn-row">
                    <button type="button" class="btn-primary" onclick="saveParams()"><i class="fas fa-save"></i> บันทึก</button>
                </div>
            </div>

            <!-- Card 4: System Prompt -->
            <div class="ai-card">
                <h3><i class="fas fa-comment-dots"></i> System Prompt</h3>
                <div class="config-item">
                    <label>คำสั่งระบบ (System Prompt)</label>
                    <textarea id="txtSystemPrompt" rows="6" placeholder="กำหนดบทบาทและพฤติกรรมของ AI..."></textarea>
                    <div class="help-text">กำหนดบุคลิก ข้อมูล และข้อจำกัดของ AI</div>
                </div>
                <div class="btn-row">
                    <button type="button" class="btn-primary" onclick="saveSystemPrompt()"><i class="fas fa-save"></i> บันทึก</button>
                    <button type="button" class="btn-outline" onclick="resetSystemPrompt()"><i class="fas fa-undo"></i> คืนค่าเริ่มต้น</button>
                </div>
            </div>
        </div>

        <!-- Test Chat -->
        <div class="ai-card" style="margin-top: 20px;">
            <h3><i class="fas fa-comments"></i> ทดสอบสนทนา</h3>
            <div class="chat-test-box">
                <div class="chat-messages" id="chatMessages">
                    <div style="text-align:center; color:#999; padding:30px; font-size:13px;">
                        <i class="fas fa-robot" style="font-size:32px; margin-bottom:10px; display:block; color:#ddd;"></i>
                        พิมพ์ข้อความเพื่อทดสอบ AI
                    </div>
                </div>
                <div class="chat-input-row">
                    <input type="text" id="txtTestMsg" placeholder="พิมพ์ข้อความทดสอบ..." onkeypress="if(event.key==='Enter')sendTestMsg()" />
                    <button type="button" onclick="sendTestMsg()"><i class="fas fa-paper-plane"></i> ส่ง</button>
                </div>
            </div>
        </div>
    </div>

    <script>
        var config = {};
        var testSessionKey = 'admin_test_' + new Date().getTime();
        var defaultSystemPrompt = 'คุณเป็นผู้ช่วย AI ของ TakeTime BangPhra ที่พักริมทะเล ตอบคำถามเป็นภาษาไทย สุภาพ เป็นมิตร ให้ข้อมูลเกี่ยวกับที่พัก สิ่งอำนวยความสะดวก กิจกรรม สถานที่ใกล้เคียง และบริการต่างๆ หากไม่ทราบข้อมูลให้แนะนำติดต่อ Front Desk';

        $(document).ready(function () {
            var raw = $('#<%= hfConfigData.ClientID %>').val();
            if (raw) {
                try { config = JSON.parse(raw); } catch (e) { }
            }
            applyConfig();
            loadStats();
        });

        function applyConfig() {
            $('#txtBaseUrl').val(config.baseUrl || 'https://api.deepseek.com');
            if (config.maskedApiKey) $('#apiKeyHint').text('ตั้งค่าแล้ว: ' + config.maskedApiKey);
            $('#ddlModel').val(config.model || 'deepseek-chat');
            $('#chkEnabled').prop('checked', config.enabled === true);
            $('#chkGuestChat').prop('checked', config.guestChatEnabled === true);
            $('#chkAdminSuggest').prop('checked', config.adminSuggestEnabled === true);
            $('#txtMaxTokens').val(config.maxTokens || 2048);
            $('#txtTemperature').val(config.temperature || 0.7);
            $('#txtTimeout').val(config.timeout || 30);
            $('#txtMaxHistory').val(config.maxHistory || 20);
            $('#txtSystemPrompt').val(config.systemPrompt || defaultSystemPrompt);
            updateStatus();
        }

        function updateStatus() {
            var s = $('#spnStatus');
            if (config.enabled && config.hasApiKey) {
                s.removeClass().addClass('status-badge status-connected').text('เชื่อมต่อแล้ว');
            } else if (config.hasApiKey) {
                s.removeClass().addClass('status-badge status-not-configured').text('ปิดใช้งาน');
            } else {
                s.removeClass().addClass('status-badge status-not-configured').text('ยังไม่ได้ตั้งค่า');
            }
        }

        function ajaxPost(action, data, cb) {
            $.ajax({
                url: window.location.pathname + '?action=' + action,
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify(data),
                success: function (r) { cb(r); },
                error: function () { cb({ success: false, message: 'เกิดข้อผิดพลาด' }); }
            });
        }

        function ajaxGet(action, cb) {
            $.ajax({
                url: window.location.pathname + '?action=' + action,
                type: 'GET',
                dataType: 'json',
                success: function (r) { cb(r); },
                error: function () { cb({}); }
            });
        }

        function saveApiConfig() {
            var data = {
                baseUrl: $('#txtBaseUrl').val().trim(),
                apiKey: $('#txtApiKey').val().trim(),
                model: $('#ddlModel').val()
            };
            ajaxPost('saveApi', data, function (r) {
                showTest(r.success, r.message);
                if (r.success) {
                    config.baseUrl = data.baseUrl;
                    config.model = data.model;
                    if (data.apiKey) { config.hasApiKey = true; config.maskedApiKey = r.maskedKey || ''; }
                    $('#apiKeyHint').text(config.maskedApiKey ? 'ตั้งค่าแล้ว: ' + config.maskedApiKey : 'รับ API Key ได้ที่ platform.deepseek.com');
                    $('#txtApiKey').val('');
                    updateStatus();
                }
            });
        }

        function testConnection() {
            showTest(null, '<i class="fas fa-spinner fa-spin"></i> กำลังทดสอบการเชื่อมต่อ...');
            ajaxGet('testConnection', function (r) {
                showTest(r.success, r.message);
            });
        }

        function showTest(success, msg) {
            var el = $('#testResult');
            el.removeClass('success error loading');
            if (success === null) el.addClass('loading');
            else if (success) el.addClass('success');
            else el.addClass('error');
            el.html(msg).show();
        }

        function saveFeatures() {
            var data = {
                enabled: $('#chkEnabled').is(':checked'),
                guestChat: $('#chkGuestChat').is(':checked'),
                adminSuggest: $('#chkAdminSuggest').is(':checked')
            };
            ajaxPost('saveFeatures', data, function (r) {
                config.enabled = data.enabled;
                config.guestChatEnabled = data.guestChat;
                config.adminSuggestEnabled = data.adminSuggest;
                updateStatus();
            });
        }

        function saveParams() {
            var data = {
                maxTokens: parseInt($('#txtMaxTokens').val()) || 2048,
                temperature: parseFloat($('#txtTemperature').val()) || 0.7,
                timeout: parseInt($('#txtTimeout').val()) || 30,
                maxHistory: parseInt($('#txtMaxHistory').val()) || 20
            };
            ajaxPost('saveParams', data, function (r) {
                alert(r.success ? 'บันทึกสำเร็จ' : r.message);
            });
        }

        function saveSystemPrompt() {
            var data = { systemPrompt: $('#txtSystemPrompt').val().trim() };
            ajaxPost('saveSystemPrompt', data, function (r) {
                alert(r.success ? 'บันทึกสำเร็จ' : r.message);
            });
        }

        function resetSystemPrompt() {
            if (!confirm('คืนค่า System Prompt เป็นค่าเริ่มต้น?')) return;
            $('#txtSystemPrompt').val(defaultSystemPrompt);
            saveSystemPrompt();
        }

        function loadStats() {
            ajaxGet('stats', function (r) {
                if (r && r.totalRequests > 0) {
                    $('#statsGrid').show();
                    $('#statTodayReqs').text(r.todayRequests || 0);
                    $('#statTodayTokens').text(formatNum(r.todayTokens || 0));
                    $('#statTotalTokens').text(formatNum(r.totalTokensUsed || 0));
                    $('#statAvgMs').text(r.avgResponseMs || 0);
                }
            });
        }

        function formatNum(n) {
            if (n >= 1000000) return (n / 1000000).toFixed(1) + 'M';
            if (n >= 1000) return (n / 1000).toFixed(1) + 'K';
            return n.toString();
        }

        // Test Chat
        var chatHistory = [];

        function sendTestMsg() {
            var msg = $('#txtTestMsg').val().trim();
            if (!msg) return;
            $('#txtTestMsg').val('');

            appendChat('user', msg);
            appendChat('assistant', '<i class="fas fa-spinner fa-spin"></i> กำลังคิด...');

            ajaxPost('chat', { message: msg, sessionKey: testSessionKey }, function (r) {
                // Remove loading bubble
                $('#chatMessages .chat-msg:last').remove();
                if (r.success) {
                    appendChat('assistant', r.message);
                    if (r.tokens) {
                        // show token info
                    }
                } else {
                    appendChat('assistant', '❌ ' + r.message);
                }
            });
        }

        function appendChat(role, text) {
            var html = '<div class="chat-msg ' + role + '"><div class="bubble">' + escapeHtml(text) + '</div></div>';
            // If text contains HTML (loading spinner), don't escape
            if (text.indexOf('<i ') >= 0 || text.indexOf('❌') >= 0) {
                html = '<div class="chat-msg ' + role + '"><div class="bubble">' + text + '</div></div>';
            }
            var container = $('#chatMessages');
            if (container.find('.chat-msg').length === 0) container.html('');
            container.append(html);
            container.scrollTop(container[0].scrollHeight);
        }

        function escapeHtml(text) {
            return $('<div>').text(text).html();
        }
    </script>
</asp:Content>
