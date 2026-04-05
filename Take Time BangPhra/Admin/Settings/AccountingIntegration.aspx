<%@ Page Title="Accounting Integration Settings" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="AccountingIntegration.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Settings.AccountingIntegration" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .acc-page { padding: 20px 0; }
        .page-header { margin-bottom: 30px; display: flex; justify-content: space-between; align-items: flex-start; flex-wrap: wrap; gap: 15px; }
        .page-header h1 { color: #5D4037; margin: 0 0 10px 0; }
        .page-header p { color: #999; margin: 0; }
        .page-header .header-actions { display: flex; gap: 10px; }

        .acc-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(380px, 1fr)); gap: 20px; }
        .acc-card { background: white; border-radius: 12px; padding: 25px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }
        .acc-card h3 { margin: 0 0 20px 0; font-size: 16px; color: #333; display: flex; align-items: center; gap: 10px; border-bottom: 2px solid #f0f0f0; padding-bottom: 15px; }
        .acc-card h3 i { color: #FF9800; font-size: 20px; }

        .config-item { padding: 12px 0; border-bottom: 1px solid #f5f5f5; }
        .config-item:last-child { border-bottom: none; }
        .config-item label { display: block; font-weight: 500; margin-bottom: 6px; font-size: 13px; color: #555; }
        .config-item input, .config-item select { width: 100%; padding: 8px 12px; border: 1px solid #ddd; border-radius: 6px; font-size: 13px; font-family: 'Prompt', sans-serif; box-sizing: border-box; }
        .config-item input:focus, .config-item select:focus { outline: none; border-color: #5D4037; }
        .config-item .help-text { font-size: 11px; color: #999; margin-top: 4px; }

        .status-badge { padding: 4px 10px; border-radius: 10px; font-size: 11px; font-weight: 500; }
        .status-connected { background: #E8F5E9; color: #2E7D32; }
        .status-not-configured { background: #FFF3E0; color: #E65100; }
        .status-error { background: #FFEBEE; color: #C62828; }

        .btn-row { display: flex; gap: 8px; margin-top: 15px; flex-wrap: wrap; }
        .btn-primary { padding: 10px 20px; background: #5D4037; color: white; border: none; border-radius: 8px; cursor: pointer; font-weight: 500; font-size: 13px; font-family: 'Prompt', sans-serif; }
        .btn-primary:hover { background: #4E342E; }
        .btn-success { padding: 10px 20px; background: #4CAF50; color: white; border: none; border-radius: 8px; cursor: pointer; font-weight: 500; font-size: 13px; font-family: 'Prompt', sans-serif; }
        .btn-success:hover { background: #388E3C; }
        .btn-warning { padding: 10px 20px; background: #FF9800; color: white; border: none; border-radius: 8px; cursor: pointer; font-weight: 500; font-size: 13px; font-family: 'Prompt', sans-serif; }
        .btn-warning:hover { background: #F57C00; }

        .test-result { margin-top: 10px; padding: 10px 15px; border-radius: 8px; font-size: 13px; display: none; }
        .test-result.success { display: block; background: #E8F5E9; color: #2E7D32; border: 1px solid #C8E6C9; }
        .test-result.error { display: block; background: #FFEBEE; color: #C62828; border: 1px solid #FFCDD2; }
        .test-result.loading { display: block; background: #E3F2FD; color: #1565C0; border: 1px solid #BBDEFB; }

        /* Journey Map */
        .journey-card { background: white; border-radius: 12px; padding: 25px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); margin-top: 20px; }
        .journey-card h3 { margin: 0 0 20px 0; font-size: 16px; color: #333; display: flex; align-items: center; gap: 10px; border-bottom: 2px solid #f0f0f0; padding-bottom: 15px; }
        .journey-card h3 i { color: #2196F3; font-size: 20px; }

        .journey-flow { display: flex; flex-wrap: wrap; gap: 10px; align-items: center; }
        .journey-step { background: #f5f5f5; border-radius: 10px; padding: 12px 16px; min-width: 140px; text-align: center; position: relative; }
        .journey-step .step-icon { font-size: 24px; margin-bottom: 6px; }
        .journey-step .step-name { font-size: 12px; font-weight: 600; color: #333; }
        .journey-step .step-accounting { font-size: 10px; color: #999; margin-top: 4px; }
        .journey-step.active { background: #E8F5E9; border: 2px solid #4CAF50; }
        .journey-step.active .step-accounting { color: #2E7D32; }
        .journey-step.warning { background: #FFF3E0; border: 2px solid #FF9800; }
        .journey-step.warning .step-accounting { color: #E65100; }
        .journey-arrow { color: #ccc; font-size: 20px; }

        /* Queue Monitor */
        .queue-table { width: 100%; border-collapse: collapse; margin-top: 15px; }
        .queue-table th { background: #f5f5f5; padding: 10px; text-align: left; font-size: 12px; font-weight: 600; color: #555; border-bottom: 2px solid #e0e0e0; }
        .queue-table td { padding: 10px; font-size: 13px; border-bottom: 1px solid #f0f0f0; }
        .queue-table tr:hover { background: #fafafa; }

        .queue-stats { display: grid; grid-template-columns: repeat(auto-fit, minmax(120px, 1fr)); gap: 12px; margin-bottom: 15px; }
        .queue-stat { background: #f9f9f9; border-radius: 8px; padding: 12px; text-align: center; }
        .queue-stat .num { font-size: 22px; font-weight: 700; }
        .queue-stat .lbl { font-size: 11px; color: #999; }
        .num-pending { color: #FF9800; }
        .num-processing { color: #2196F3; }
        .num-completed { color: #4CAF50; }
        .num-failed { color: #F44336; }
    </style>

    <div class="acc-page">
        <div class="page-header">
            <div>
                <h1><i class="fas fa-calculator"></i> Accounting Integration Settings</h1>
                <p>ตั้งค่าการเชื่อมต่อระบบบัญชี Nexaacc และจัดการ Sync Queue</p>
            </div>
            <div class="header-actions">
                <a href="<%= ResolveUrl("~/Admin/Settings/ConnectionSettings") %>" class="btn-primary" style="text-decoration:none; display:flex; align-items:center; gap:6px;">
                    <i class="fas fa-arrow-left"></i> กลับหน้า Connection Settings
                </a>
            </div>
        </div>

        <div class="acc-grid">
            <!-- API Configuration -->
            <div class="acc-card">
                <h3><i class="fas fa-key"></i> API Configuration</h3>
                <div class="config-item">
                    <label>Base URL</label>
                    <input type="text" id="cfgBaseUrl" placeholder="https://api.nexaacc.com" />
                    <div class="help-text">URL ของ Nexaacc API Server</div>
                </div>
                <div class="config-item">
                    <label>API Key</label>
                    <input type="password" id="cfgApiKey" placeholder="ใส่ API Key จากระบบ Nexaacc" />
                    <div class="help-text">API Key สร้างได้จากหน้า Settings ของระบบ Nexaacc (ส่งผ่าน X-Api-Key header)</div>
                </div>
                <div class="config-item">
                    <label>Company ID (GUID)</label>
                    <input type="text" id="cfgCompanyId" placeholder="00000000-0000-0000-0000-000000000000" />
                </div>
                <div class="btn-row">
                    <button type="button" class="btn-success" onclick="saveConfig()"><i class="fas fa-save"></i> บันทึก</button>
                    <button type="button" class="btn-primary" onclick="testApi()"><i class="fas fa-plug"></i> ทดสอบ API Key</button>
                    <button type="button" class="btn-primary" onclick="testFetchAccounts()"><i class="fas fa-list"></i> ดึง Chart of Accounts</button>
                </div>
                <div class="test-result" id="apiTestResult"></div>
            </div>

            <!-- Sync Settings -->
            <div class="acc-card">
                <h3><i class="fas fa-sync-alt"></i> Sync Settings</h3>
                <div class="config-item">
                    <label>เปิดใช้งาน Sync</label>
                    <select id="cfgEnabled">
                        <option value="false">ปิด</option>
                        <option value="true">เปิด</option>
                    </select>
                </div>
                <div class="config-item">
                    <label>Sync Interval (วินาที)</label>
                    <input type="number" id="cfgSyncInterval" value="30" min="10" max="3600" />
                    <div class="help-text">ระยะเวลาระหว่าง queue processing cycles (10-3600 วินาที)</div>
                </div>
                <div class="config-item">
                    <label>Max Retries</label>
                    <input type="number" id="cfgMaxRetries" value="5" min="1" max="20" />
                    <div class="help-text">จำนวนครั้งสูงสุดที่จะ retry หาก sync ล้มเหลว</div>
                </div>
                <div class="config-item">
                    <label>API Timeout (วินาที)</label>
                    <input type="number" id="cfgTimeout" value="30" min="5" max="120" />
                </div>
                <div class="btn-row">
                    <button type="button" class="btn-success" onclick="saveSyncSettings()"><i class="fas fa-save"></i> บันทึก</button>
                    <button type="button" class="btn-warning" onclick="processQueue()"><i class="fas fa-play"></i> Process Queue ตอนนี้</button>
                </div>
                <div class="test-result" id="syncTestResult"></div>
            </div>
        </div>

        <!-- Guest Journey Accounting Map -->
        <div class="journey-card">
            <h3><i class="fas fa-route"></i> Guest Journey - Accounting Events Map</h3>
            <p style="font-size:13px; color:#666; margin-bottom:20px;">แผนผังแสดงทุก Event ที่เกิดขึ้นตลอด Journey ของการเข้าพัก พร้อมการบันทึกบัญชีอัตโนมัติ</p>

            <div class="journey-flow">
                <div class="journey-step active">
                    <div class="step-icon"><i class="fas fa-calendar-plus"></i></div>
                    <div class="step-name">จอง (Booking)</div>
                    <div class="step-accounting">ไม่บันทึกบัญชี</div>
                </div>
                <div class="journey-arrow"><i class="fas fa-arrow-right"></i></div>

                <div class="journey-step active">
                    <div class="step-icon"><i class="fas fa-money-bill-wave"></i></div>
                    <div class="step-name">มัดจำ (Deposit)</div>
                    <div class="step-accounting">DR เงินสด/ธนาคาร<br/>CR เงินรับล่วงหน้า</div>
                </div>
                <div class="journey-arrow"><i class="fas fa-arrow-right"></i></div>

                <div class="journey-step active">
                    <div class="step-icon"><i class="fas fa-credit-card"></i></div>
                    <div class="step-name">ชำระเพิ่ม (Payment)</div>
                    <div class="step-accounting">DR เงินสด/ธนาคาร<br/>CR รายได้ห้องพัก</div>
                </div>
                <div class="journey-arrow"><i class="fas fa-arrow-right"></i></div>

                <div class="journey-step active">
                    <div class="step-icon"><i class="fas fa-door-open"></i></div>
                    <div class="step-name">เช็คอิน (Check-in)</div>
                    <div class="step-accounting">ไม่บันทึกบัญชี</div>
                </div>
                <div class="journey-arrow"><i class="fas fa-arrow-right"></i></div>

                <div class="journey-step active">
                    <div class="step-icon"><i class="fas fa-concierge-bell"></i></div>
                    <div class="step-name">Room Service</div>
                    <div class="step-accounting">DR ลูกหนี้ห้องพัก<br/>CR รายได้สินค้า</div>
                </div>
                <div class="journey-arrow"><i class="fas fa-arrow-right"></i></div>

                <div class="journey-step active">
                    <div class="step-icon"><i class="fas fa-door-closed"></i></div>
                    <div class="step-name">เช็คเอาท์ (Checkout)</div>
                    <div class="step-accounting">DR เงินรับล่วงหน้า<br/>CR รายได้ห้องพัก</div>
                </div>
            </div>

            <div style="margin-top:20px; padding-top:15px; border-top:2px solid #f0f0f0;">
                <h4 style="font-size:14px; color:#555; margin-bottom:15px;"><i class="fas fa-exclamation-triangle" style="color:#FF9800;"></i> กรณีพิเศษ</h4>
                <div class="journey-flow">
                    <div class="journey-step active">
                        <div class="step-icon"><i class="fas fa-calendar-alt"></i></div>
                        <div class="step-name">เลื่อนวัน (Postpone)</div>
                        <div class="step-accounting">บันทึกส่วนต่างราคา<br/>(ถ้ามี)</div>
                    </div>

                    <div class="journey-step active">
                        <div class="step-icon"><i class="fas fa-times-circle"></i></div>
                        <div class="step-name">ยกเลิก+คืนเงิน</div>
                        <div class="step-accounting">DR เงินรับล่วงหน้า<br/>CR เงินสด/ธนาคาร</div>
                    </div>

                    <div class="journey-step active">
                        <div class="step-icon"><i class="fas fa-ban"></i></div>
                        <div class="step-name">ยกเลิก ไม่คืนเงิน</div>
                        <div class="step-accounting">DR เงินรับล่วงหน้า<br/>CR รายได้อื่น</div>
                    </div>

                    <div class="journey-step active">
                        <div class="step-icon"><i class="fas fa-cash-register"></i></div>
                        <div class="step-name">ขาย POS</div>
                        <div class="step-accounting">DR เงินสด/ธนาคาร<br/>CR รายได้สินค้า</div>
                    </div>

                    <div class="journey-step active">
                        <div class="step-icon"><i class="fas fa-file-invoice-dollar"></i></div>
                        <div class="step-name">ใบสำคัญจ่าย</div>
                        <div class="step-accounting">DR ค่าใช้จ่าย<br/>CR เงินสด/ธนาคาร</div>
                    </div>

                    <div class="journey-step active">
                        <div class="step-icon"><i class="fas fa-money-check-alt"></i></div>
                        <div class="step-name">เงินเดือน (Payroll)</div>
                        <div class="step-accounting">DR เงินเดือน<br/>CR เงินสด/ธนาคาร</div>
                    </div>
                </div>
            </div>
        </div>

        <!-- Queue Monitor -->
        <div class="journey-card">
            <h3><i class="fas fa-tasks"></i> Sync Queue Monitor</h3>

            <div class="queue-stats" id="queueStats">
                <div class="queue-stat"><div class="num num-pending" id="qsPending">-</div><div class="lbl">Pending</div></div>
                <div class="queue-stat"><div class="num num-processing" id="qsProcessing">-</div><div class="lbl">Processing</div></div>
                <div class="queue-stat"><div class="num num-completed" id="qsCompleted">-</div><div class="lbl">Completed</div></div>
                <div class="queue-stat"><div class="num num-failed" id="qsFailed">-</div><div class="lbl">Failed</div></div>
            </div>

            <div class="btn-row" style="margin-bottom:15px;">
                <button type="button" class="btn-primary" onclick="loadQueueData()"><i class="fas fa-sync"></i> รีเฟรช</button>
                <button type="button" class="btn-warning" onclick="retryAllFailed()"><i class="fas fa-redo"></i> Retry Failed ทั้งหมด</button>
            </div>

            <div style="overflow-x:auto;">
                <table class="queue-table" id="queueTable">
                    <thead>
                        <tr>
                            <th>ID</th>
                            <th>Entity</th>
                            <th>Action</th>
                            <th>Status</th>
                            <th>Retry</th>
                            <th>Error</th>
                            <th>Created</th>
                            <th></th>
                        </tr>
                    </thead>
                    <tbody id="queueBody">
                        <tr><td colspan="8" style="text-align:center; color:#999;">กำลังโหลด...</td></tr>
                    </tbody>
                </table>
            </div>
        </div>
    </div>

    <asp:HiddenField ID="hfConfigData" runat="server" />
    <asp:HiddenField ID="hfQueueData" runat="server" />

    <script>
        var pageUrl = '<%= ResolveUrl("~/Admin/Settings/AccountingIntegration") %>';

        document.addEventListener('DOMContentLoaded', function () {
            loadConfigData();
            loadQueueData();
        });

        function loadConfigData() {
            var raw = document.getElementById('<%= hfConfigData.ClientID %>').value;
            if (!raw) return;
            try {
                var cfg = JSON.parse(raw);
                document.getElementById('cfgBaseUrl').value = cfg.baseUrl || '';
                document.getElementById('cfgCompanyId').value = cfg.companyId || '';
                document.getElementById('cfgEnabled').value = cfg.enabled ? 'true' : 'false';
                document.getElementById('cfgSyncInterval').value = cfg.syncInterval || 30;
                document.getElementById('cfgMaxRetries').value = cfg.maxRetries || 5;
                document.getElementById('cfgTimeout').value = cfg.timeout || 30;
                // API Key จะไม่โหลดกลับมาแสดง เพื่อความปลอดภัย (เหมือน password)
                if (cfg.hasApiKey) {
                    document.getElementById('cfgApiKey').placeholder = '••••••••  (มี API Key อยู่แล้ว — ใส่ค่าใหม่เพื่อเปลี่ยน)';
                }
            } catch (e) { console.error(e); }
        }

        function saveConfig() {
            var data = {
                action: 'saveApi',
                baseUrl: document.getElementById('cfgBaseUrl').value,
                apiKey: document.getElementById('cfgApiKey').value,
                companyId: document.getElementById('cfgCompanyId').value
            };
            postAction(data, 'apiTestResult');
        }

        function saveSyncSettings() {
            var data = {
                action: 'saveSyncSettings',
                enabled: document.getElementById('cfgEnabled').value,
                syncInterval: document.getElementById('cfgSyncInterval').value,
                maxRetries: document.getElementById('cfgMaxRetries').value,
                timeout: document.getElementById('cfgTimeout').value
            };
            postAction(data, 'syncTestResult');
        }

        function testApi() {
            getAction('testApi', 'apiTestResult');
        }

        function testFetchAccounts() {
            getAction('fetchAccounts', 'apiTestResult');
        }

        function processQueue() {
            getAction('processQueue', 'syncTestResult');
        }

        function loadQueueData() {
            fetch(pageUrl + '?action=queueData&_=' + Date.now())
                .then(function(r) { return r.json(); })
                .then(function(data) {
                    document.getElementById('qsPending').textContent = data.pending || 0;
                    document.getElementById('qsProcessing').textContent = data.processing || 0;
                    document.getElementById('qsCompleted').textContent = data.completed || 0;
                    document.getElementById('qsFailed').textContent = data.failed || 0;
                    renderQueue(data.items || []);
                })
                .catch(function(err) { console.error(err); });
        }

        function renderQueue(items) {
            var tbody = document.getElementById('queueBody');
            if (!items.length) {
                tbody.innerHTML = '<tr><td colspan="8" style="text-align:center; color:#999;">ไม่มีรายการใน Queue</td></tr>';
                return;
            }
            var html = '';
            items.forEach(function(item) {
                var statusClass = item.status === 'COMPLETED' ? 'status-connected' :
                                  item.status === 'FAILED' ? 'status-error' :
                                  item.status === 'PROCESSING' ? 'status-testing' : 'status-not-configured';
                html += '<tr>';
                html += '<td>' + item.id + '</td>';
                html += '<td>' + item.entityType + ' #' + item.entityId + '</td>';
                html += '<td>' + item.actionType + '</td>';
                html += '<td><span class="status-badge ' + statusClass + '">' + item.status + '</span></td>';
                html += '<td>' + item.retryCount + '/' + item.maxRetries + '</td>';
                html += '<td style="max-width:200px; overflow:hidden; text-overflow:ellipsis;">' + (item.error || '-') + '</td>';
                html += '<td>' + item.created + '</td>';
                html += '<td>';
                if (item.status === 'FAILED') {
                    html += '<button class="btn-primary" style="padding:4px 10px; font-size:11px;" onclick="retryItem(' + item.id + ')"><i class="fas fa-redo"></i></button>';
                }
                html += '</td>';
                html += '</tr>';
            });
            tbody.innerHTML = html;
        }

        function retryItem(queueId) {
            fetch(pageUrl + '?action=retryItem&queueId=' + queueId + '&_=' + Date.now())
                .then(function(r) { return r.json(); })
                .then(function(data) { loadQueueData(); })
                .catch(function(err) { alert(err.message); });
        }

        function retryAllFailed() {
            getAction('retryAllFailed', 'syncTestResult');
            setTimeout(loadQueueData, 1000);
        }

        function getAction(action, resultId) {
            var el = document.getElementById(resultId);
            el.className = 'test-result loading';
            el.textContent = 'กำลังดำเนินการ...';

            fetch(pageUrl + '?action=' + action + '&_=' + Date.now())
                .then(function(r) { return r.json(); })
                .then(function(data) {
                    el.className = 'test-result ' + (data.success ? 'success' : 'error');
                    el.innerHTML = (data.success ? '<i class="fas fa-check-circle"></i> ' : '<i class="fas fa-times-circle"></i> ') + data.message;
                })
                .catch(function(err) {
                    el.className = 'test-result error';
                    el.innerHTML = '<i class="fas fa-times-circle"></i> ' + err.message;
                });
        }

        function postAction(data, resultId) {
            var el = document.getElementById(resultId);
            el.className = 'test-result loading';
            el.textContent = 'กำลังบันทึก...';

            fetch(pageUrl + '?action=' + data.action, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(data)
            })
            .then(function(r) { return r.json(); })
            .then(function(result) {
                el.className = 'test-result ' + (result.success ? 'success' : 'error');
                el.innerHTML = (result.success ? '<i class="fas fa-check-circle"></i> ' : '<i class="fas fa-times-circle"></i> ') + result.message;
                if (result.success) {
                    setTimeout(function() { window.location.reload(); }, 1200);
                }
            })
            .catch(function(err) {
                el.className = 'test-result error';
                el.innerHTML = '<i class="fas fa-times-circle"></i> ' + err.message;
            });
        }
    </script>
</asp:Content>
