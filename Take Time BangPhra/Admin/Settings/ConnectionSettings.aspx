<%@ Page Title="System Connection Settings" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ConnectionSettings.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Settings.ConnectionSettings" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .connection-page { padding: 20px 0; }
        .page-header { margin-bottom: 30px; }
        .page-header h1 { color: #5D4037; margin: 0 0 10px 0; }
        .page-header p { color: #999; margin: 0; }

        .conn-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(380px, 1fr)); gap: 20px; }
        .conn-card { background: white; border-radius: 12px; padding: 25px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }
        .conn-card h3 { margin: 0 0 20px 0; font-size: 16px; color: #333; display: flex; align-items: center; gap: 10px; border-bottom: 2px solid #f0f0f0; padding-bottom: 15px; }
        .conn-card h3 i { font-size: 20px; }
        .conn-card h3 .icon-db { color: #1976D2; }
        .conn-card h3 .icon-line { color: #06C755; }
        .conn-card h3 .icon-telegram { color: #0088cc; }
        .conn-card h3 .icon-email { color: #EA4335; }
        .conn-card h3 .icon-accounting { color: #FF9800; }
        .conn-card h3 .icon-api { color: #9C27B0; }
        .conn-card h3 .icon-ota { color: #00BCD4; }

        .conn-item { padding: 12px 0; border-bottom: 1px solid #f5f5f5; }
        .conn-item:last-child { border-bottom: none; }
        .conn-item label { display: block; font-weight: 500; margin-bottom: 6px; font-size: 13px; color: #555; }
        .conn-item .field-row { display: flex; gap: 8px; align-items: center; }
        .conn-item input[type="text"],
        .conn-item input[type="password"],
        .conn-item input[type="number"] { flex: 1; padding: 8px 12px; border: 1px solid #ddd; border-radius: 6px; font-size: 13px; font-family: 'Prompt', sans-serif; }
        .conn-item input:focus { outline: none; border-color: #5D4037; }
        .conn-item input[readonly] { background: #f9f9f9; color: #999; }

        .status-badge { padding: 4px 10px; border-radius: 10px; font-size: 11px; font-weight: 500; white-space: nowrap; }
        .status-connected { background: #E8F5E9; color: #2E7D32; }
        .status-not-configured { background: #FFF3E0; color: #E65100; }
        .status-error { background: #FFEBEE; color: #C62828; }
        .status-testing { background: #E3F2FD; color: #1565C0; }

        .btn-row { display: flex; gap: 8px; margin-top: 15px; padding-top: 15px; border-top: 2px solid #f0f0f0; flex-wrap: wrap; }
        .btn-test { padding: 8px 16px; background: #5D4037; color: white; border: none; border-radius: 8px; cursor: pointer; font-weight: 500; font-size: 13px; font-family: 'Prompt', sans-serif; display: flex; align-items: center; gap: 6px; }
        .btn-test:hover { background: #4E342E; }
        .btn-test:disabled { background: #ccc; cursor: not-allowed; }
        .btn-save { padding: 8px 16px; background: #4CAF50; color: white; border: none; border-radius: 8px; cursor: pointer; font-weight: 500; font-size: 13px; font-family: 'Prompt', sans-serif; display: flex; align-items: center; gap: 6px; }
        .btn-save:hover { background: #388E3C; }

        .test-result { margin-top: 10px; padding: 10px 15px; border-radius: 8px; font-size: 13px; display: none; }
        .test-result.success { display: block; background: #E8F5E9; color: #2E7D32; border: 1px solid #C8E6C9; }
        .test-result.error { display: block; background: #FFEBEE; color: #C62828; border: 1px solid #FFCDD2; }
        .test-result.loading { display: block; background: #E3F2FD; color: #1565C0; border: 1px solid #BBDEFB; }

        .toggle-switch { position: relative; width: 50px; height: 26px; background: #ccc; border-radius: 13px; cursor: pointer; transition: background 0.3s; flex-shrink: 0; }
        .toggle-switch.active { background: #4CAF50; }
        .toggle-switch::after { content: ''; position: absolute; width: 22px; height: 22px; background: white; border-radius: 50%; top: 2px; left: 2px; transition: left 0.3s; box-shadow: 0 2px 4px rgba(0,0,0,0.2); }
        .toggle-switch.active::after { left: 26px; }

        .conn-summary { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: 15px; margin-bottom: 25px; }
        .summary-card { background: white; border-radius: 10px; padding: 15px; box-shadow: 0 2px 8px rgba(0,0,0,0.08); text-align: center; }
        .summary-card .count { font-size: 28px; font-weight: 700; }
        .summary-card .label { font-size: 12px; color: #999; margin-top: 5px; }
        .count-green { color: #4CAF50; }
        .count-orange { color: #FF9800; }
        .count-red { color: #F44336; }
        .count-blue { color: #2196F3; }

        .password-toggle { background: none; border: 1px solid #ddd; border-radius: 6px; padding: 8px 10px; cursor: pointer; color: #999; }
        .password-toggle:hover { color: #333; }
    </style>

    <div class="connection-page">
        <div class="page-header">
            <h1><i class="fas fa-plug"></i> System Connection Settings</h1>
            <p>ตั้งค่าและทดสอบการเชื่อมต่อระบบภายนอกทั้งหมดจากที่เดียว</p>
        </div>

        <!-- Summary -->
        <div class="conn-summary">
            <div class="summary-card">
                <div class="count count-green" id="connectedCount">0</div>
                <div class="label">เชื่อมต่อแล้ว</div>
            </div>
            <div class="summary-card">
                <div class="count count-orange" id="notConfiguredCount">0</div>
                <div class="label">ยังไม่ตั้งค่า</div>
            </div>
            <div class="summary-card">
                <div class="count count-red" id="errorCount">0</div>
                <div class="label">มีปัญหา</div>
            </div>
            <div class="summary-card">
                <div class="count count-blue" id="totalCount">0</div>
                <div class="label">ทั้งหมด</div>
            </div>
        </div>

        <div class="conn-grid">
            <!-- Database Connection -->
            <div class="conn-card" id="cardDatabase">
                <h3><i class="fas fa-database icon-db"></i> Database (SQL Server)
                    <span class="status-badge status-connected" id="dbStatus">เชื่อมต่อแล้ว</span>
                </h3>
                <div class="conn-item">
                    <label>Server</label>
                    <div class="field-row">
                        <input type="text" id="dbServer" readonly />
                    </div>
                </div>
                <div class="conn-item">
                    <label>Database</label>
                    <div class="field-row">
                        <input type="text" id="dbName" readonly />
                    </div>
                </div>
                <div class="conn-item">
                    <label>User</label>
                    <div class="field-row">
                        <input type="text" id="dbUser" readonly />
                    </div>
                </div>
                <div class="btn-row">
                    <button class="btn-test" onclick="testConnection('database')">
                        <i class="fas fa-plug"></i> ทดสอบการเชื่อมต่อ
                    </button>
                </div>
                <div class="test-result" id="dbTestResult"></div>
            </div>

            <!-- LINE Notify -->
            <div class="conn-card" id="cardLine">
                <h3><i class="fab fa-line icon-line"></i> LINE Notify
                    <span class="status-badge" id="lineStatus">กำลังตรวจสอบ...</span>
                </h3>
                <div class="conn-item">
                    <label>API URL</label>
                    <div class="field-row">
                        <input type="text" id="lineUrl" readonly />
                    </div>
                </div>
                <div class="conn-item">
                    <label>Token</label>
                    <div class="field-row">
                        <input type="password" id="lineToken" readonly />
                        <button class="password-toggle" onclick="togglePassword('lineToken')"><i class="fas fa-eye"></i></button>
                    </div>
                </div>
                <div class="conn-item">
                    <label>Channel Access Token</label>
                    <div class="field-row">
                        <input type="password" id="lineChannelToken" readonly />
                        <button class="password-toggle" onclick="togglePassword('lineChannelToken')"><i class="fas fa-eye"></i></button>
                    </div>
                </div>
                <div class="btn-row">
                    <button class="btn-test" onclick="testConnection('line')">
                        <i class="fas fa-paper-plane"></i> ทดสอบส่งข้อความ
                    </button>
                </div>
                <div class="test-result" id="lineTestResult"></div>
            </div>

            <!-- Telegram Bot -->
            <div class="conn-card" id="cardTelegram">
                <h3><i class="fab fa-telegram icon-telegram"></i> Telegram Bot
                    <span class="status-badge" id="telegramStatus">กำลังตรวจสอบ...</span>
                </h3>
                <div class="conn-item">
                    <label>Bot Token</label>
                    <div class="field-row">
                        <input type="password" id="telegramToken" readonly />
                        <button class="password-toggle" onclick="togglePassword('telegramToken')"><i class="fas fa-eye"></i></button>
                    </div>
                </div>
                <div class="btn-row">
                    <button class="btn-test" onclick="testConnection('telegram')">
                        <i class="fas fa-paper-plane"></i> ทดสอบส่งข้อความ
                    </button>
                </div>
                <div class="test-result" id="telegramTestResult"></div>
            </div>

            <!-- Email SMTP -->
            <div class="conn-card" id="cardEmail">
                <h3><i class="fas fa-envelope icon-email"></i> Email (SMTP)
                    <span class="status-badge" id="emailStatus">กำลังตรวจสอบ...</span>
                </h3>
                <div class="conn-item">
                    <label>SMTP Server</label>
                    <div class="field-row">
                        <input type="text" id="smtpServer" readonly />
                    </div>
                </div>
                <div class="conn-item">
                    <label>Port</label>
                    <div class="field-row">
                        <input type="text" id="smtpPort" readonly />
                    </div>
                </div>
                <div class="conn-item">
                    <label>Email From</label>
                    <div class="field-row">
                        <input type="text" id="emailFrom" readonly />
                    </div>
                </div>
                <div class="conn-item">
                    <label>SSL</label>
                    <div class="field-row">
                        <input type="text" id="smtpSsl" readonly />
                    </div>
                </div>
                <div class="btn-row">
                    <button class="btn-test" onclick="testConnection('email')">
                        <i class="fas fa-paper-plane"></i> ทดสอบส่งอีเมล
                    </button>
                </div>
                <div class="test-result" id="emailTestResult"></div>
            </div>

            <!-- Nexaacc Accounting -->
            <div class="conn-card" id="cardAccounting">
                <h3><i class="fas fa-calculator icon-accounting"></i> Nexaacc Accounting
                    <span class="status-badge" id="accountingStatus">กำลังตรวจสอบ...</span>
                </h3>
                <div class="conn-item">
                    <label>API URL</label>
                    <div class="field-row">
                        <input type="text" id="nexaaccUrl" placeholder="https://api.nexaacc.com" />
                    </div>
                </div>
                <div class="conn-item">
                    <label>API Key</label>
                    <div class="field-row">
                        <input type="password" id="nexaaccApiKey" placeholder="ใส่ API Key จากระบบ Nexaacc" />
                        <button class="password-toggle" onclick="togglePassword('nexaaccApiKey')"><i class="fas fa-eye"></i></button>
                    </div>
                </div>
                <div class="conn-item">
                    <label>Company ID</label>
                    <div class="field-row">
                        <input type="text" id="nexaaccCompanyId" placeholder="GUID" />
                    </div>
                </div>
                <div class="conn-item">
                    <label>เปิดใช้งาน</label>
                    <div class="field-row">
                        <div class="toggle-switch" id="nexaaccEnabled" onclick="this.classList.toggle('active')"></div>
                    </div>
                </div>
                <div class="btn-row">
                    <button class="btn-save" onclick="saveAccountingConfig()">
                        <i class="fas fa-save"></i> บันทึก
                    </button>
                    <button class="btn-test" onclick="testConnection('accounting')">
                        <i class="fas fa-plug"></i> ทดสอบการเชื่อมต่อ
                    </button>
                    <a href="<%= ResolveUrl("~/Admin/Settings/AccountingIntegration") %>" class="btn-test" style="text-decoration:none;">
                        <i class="fas fa-cog"></i> ตั้งค่าเพิ่มเติม
                    </a>
                </div>
                <div class="test-result" id="accountingTestResult"></div>
            </div>

            <!-- Google Places API -->
            <div class="conn-card" id="cardGoogle">
                <h3><i class="fas fa-map-marker-alt icon-api"></i> Google Places API
                    <span class="status-badge" id="googleStatus">กำลังตรวจสอบ...</span>
                </h3>
                <div class="conn-item">
                    <label>API Key</label>
                    <div class="field-row">
                        <input type="password" id="googleApiKey" readonly />
                        <button class="password-toggle" onclick="togglePassword('googleApiKey')"><i class="fas fa-eye"></i></button>
                    </div>
                </div>
                <div class="btn-row">
                    <button class="btn-test" onclick="testConnection('google')">
                        <i class="fas fa-plug"></i> ทดสอบ API
                    </button>
                </div>
                <div class="test-result" id="googleTestResult"></div>
            </div>

            <!-- Tax Invoice API -->
            <div class="conn-card" id="cardTaxInvoice">
                <h3><i class="fas fa-file-invoice icon-api"></i> Tax Invoice API
                    <span class="status-badge" id="taxStatus">กำลังตรวจสอบ...</span>
                </h3>
                <div class="conn-item">
                    <label>API Key</label>
                    <div class="field-row">
                        <input type="password" id="taxApiKey" readonly />
                        <button class="password-toggle" onclick="togglePassword('taxApiKey')"><i class="fas fa-eye"></i></button>
                    </div>
                </div>
                <div class="btn-row">
                    <button class="btn-test" onclick="testConnection('taxinvoice')">
                        <i class="fas fa-plug"></i> ทดสอบ API
                    </button>
                </div>
                <div class="test-result" id="taxTestResult"></div>
            </div>

            <!-- OTA Channel Manager -->
            <div class="conn-card" id="cardOTA">
                <h3><i class="fas fa-globe icon-ota"></i> OTA Channels
                    <span class="status-badge" id="otaStatus">กำลังตรวจสอบ...</span>
                </h3>
                <div class="conn-item">
                    <label>Booking.com</label>
                    <div class="field-row">
                        <span class="status-badge status-not-configured">ยังไม่เชื่อมต่อ</span>
                    </div>
                </div>
                <div class="conn-item">
                    <label>Agoda</label>
                    <div class="field-row">
                        <span class="status-badge status-not-configured">ยังไม่เชื่อมต่อ</span>
                    </div>
                </div>
                <div class="conn-item">
                    <label>Airbnb</label>
                    <div class="field-row">
                        <span class="status-badge status-not-configured">ยังไม่เชื่อมต่อ</span>
                    </div>
                </div>
                <div class="conn-item">
                    <label>Direct Booking</label>
                    <div class="field-row">
                        <span class="status-badge status-connected">เชื่อมต่อแล้ว</span>
                    </div>
                </div>
                <div class="btn-row">
                    <a href="<%= ResolveUrl("~/Admin/ChannelManager/Dashboard") %>" class="btn-test" style="text-decoration:none;">
                        <i class="fas fa-external-link-alt"></i> จัดการ Channel Manager
                    </a>
                </div>
            </div>
        </div>
    </div>

    <asp:HiddenField ID="hfConnectionData" runat="server" />
    <asp:HiddenField ID="hfAccountingData" runat="server" />

    <script>
        document.addEventListener('DOMContentLoaded', function () {
            loadConnectionData();
        });

        function loadConnectionData() {
            var raw = document.getElementById('<%= hfConnectionData.ClientID %>').value;
            if (!raw) return;

            try {
                var data = JSON.parse(raw);

                // Database
                document.getElementById('dbServer').value = data.dbServer || '';
                document.getElementById('dbName').value = data.dbName || '';
                document.getElementById('dbUser').value = data.dbUser || '';
                setStatus('dbStatus', data.dbConnected ? 'connected' : 'error');

                // LINE
                document.getElementById('lineUrl').value = data.lineUrl || '';
                document.getElementById('lineToken').value = data.lineToken || '';
                document.getElementById('lineChannelToken').value = data.lineChannelToken || '';
                setStatus('lineStatus', data.lineConfigured ? 'connected' : 'not-configured');

                // Telegram
                document.getElementById('telegramToken').value = data.telegramToken || '';
                setStatus('telegramStatus', data.telegramConfigured ? 'connected' : 'not-configured');

                // Email
                document.getElementById('smtpServer').value = data.smtpServer || '';
                document.getElementById('smtpPort').value = data.smtpPort || '';
                document.getElementById('emailFrom').value = data.emailFrom || '';
                document.getElementById('smtpSsl').value = data.smtpSsl || '';
                setStatus('emailStatus', data.emailConfigured ? 'connected' : 'not-configured');

                // Google
                document.getElementById('googleApiKey').value = data.googleApiKey || '';
                setStatus('googleStatus', data.googleConfigured ? 'connected' : 'not-configured');

                // Tax Invoice
                document.getElementById('taxApiKey').value = data.taxApiKey || '';
                setStatus('taxStatus', data.taxConfigured ? 'connected' : 'not-configured');

                // OTA
                setStatus('otaStatus', 'not-configured');

                updateSummary();
            } catch (e) {
                console.error('Error loading connection data', e);
            }

            // Accounting
            var accRaw = document.getElementById('<%= hfAccountingData.ClientID %>').value;
            if (accRaw) {
                try {
                    var acc = JSON.parse(accRaw);
                    document.getElementById('nexaaccUrl').value = acc.baseUrl || '';
                    document.getElementById('nexaaccCompanyId').value = acc.companyId || '';
                    if (acc.enabled) document.getElementById('nexaaccEnabled').classList.add('active');
                    if (acc.hasApiKey) document.getElementById('nexaaccApiKey').placeholder = '••••••••  (มี API Key แล้ว)';
                    setStatus('accountingStatus', acc.isConfigured ? 'connected' : 'not-configured');
                    updateSummary();
                } catch (e) { }
            }
        }

        function setStatus(elementId, status) {
            var el = document.getElementById(elementId);
            el.className = 'status-badge';
            switch (status) {
                case 'connected':
                    el.className += ' status-connected';
                    el.textContent = 'เชื่อมต่อแล้ว';
                    break;
                case 'not-configured':
                    el.className += ' status-not-configured';
                    el.textContent = 'ยังไม่ตั้งค่า';
                    break;
                case 'error':
                    el.className += ' status-error';
                    el.textContent = 'เชื่อมต่อไม่ได้';
                    break;
                case 'testing':
                    el.className += ' status-testing';
                    el.textContent = 'กำลังทดสอบ...';
                    break;
            }
        }

        function updateSummary() {
            var badges = document.querySelectorAll('.conn-card .status-badge');
            var connected = 0, notConfigured = 0, errors = 0;
            badges.forEach(function (b) {
                if (b.classList.contains('status-connected')) connected++;
                else if (b.classList.contains('status-not-configured')) notConfigured++;
                else if (b.classList.contains('status-error')) errors++;
            });
            document.getElementById('connectedCount').textContent = connected;
            document.getElementById('notConfiguredCount').textContent = notConfigured;
            document.getElementById('errorCount').textContent = errors;
            document.getElementById('totalCount').textContent = connected + notConfigured + errors;
        }

        function togglePassword(inputId) {
            var input = document.getElementById(inputId);
            input.type = input.type === 'password' ? 'text' : 'password';
        }

        function testConnection(type) {
            var resultId = type === 'database' ? 'dbTestResult' :
                           type === 'line' ? 'lineTestResult' :
                           type === 'telegram' ? 'telegramTestResult' :
                           type === 'email' ? 'emailTestResult' :
                           type === 'accounting' ? 'accountingTestResult' :
                           type === 'google' ? 'googleTestResult' :
                           type === 'taxinvoice' ? 'taxTestResult' : '';

            var resultEl = document.getElementById(resultId);
            resultEl.className = 'test-result loading';
            resultEl.textContent = 'กำลังทดสอบการเชื่อมต่อ...';

            fetch('<%= ResolveUrl("~/Admin/Settings/ConnectionSettings") %>?action=test&type=' + type + '&_=' + Date.now())
                .then(function(r) { return r.json(); })
                .then(function(data) {
                    if (data.success) {
                        resultEl.className = 'test-result success';
                        resultEl.innerHTML = '<i class="fas fa-check-circle"></i> ' + data.message;
                    } else {
                        resultEl.className = 'test-result error';
                        resultEl.innerHTML = '<i class="fas fa-times-circle"></i> ' + data.message;
                    }
                })
                .catch(function(err) {
                    resultEl.className = 'test-result error';
                    resultEl.innerHTML = '<i class="fas fa-times-circle"></i> ไม่สามารถทดสอบได้: ' + err.message;
                });
        }

        function saveAccountingConfig() {
            var data = {
                baseUrl: document.getElementById('nexaaccUrl').value,
                apiKey: document.getElementById('nexaaccApiKey').value,
                companyId: document.getElementById('nexaaccCompanyId').value,
                enabled: document.getElementById('nexaaccEnabled').classList.contains('active')
            };

            fetch('<%= ResolveUrl("~/Admin/Settings/ConnectionSettings") %>?action=saveAccounting', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(data)
            })
            .then(function(r) { return r.json(); })
            .then(function(result) {
                var el = document.getElementById('accountingTestResult');
                if (result.success) {
                    el.className = 'test-result success';
                    el.innerHTML = '<i class="fas fa-check-circle"></i> ' + result.message;
                    setStatus('accountingStatus', result.isConfigured ? 'connected' : 'not-configured');
                } else {
                    el.className = 'test-result error';
                    el.innerHTML = '<i class="fas fa-times-circle"></i> ' + result.message;
                }
                updateSummary();
            })
            .catch(function(err) {
                var el = document.getElementById('accountingTestResult');
                el.className = 'test-result error';
                el.innerHTML = '<i class="fas fa-times-circle"></i> ' + err.message;
            });
        }
    </script>
</asp:Content>
