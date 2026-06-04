<%@ Page Title="AI Knowledge Base" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="AIKnowledgeBase.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Settings.AIKnowledgeBase" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .kb-page { padding: 20px 0; max-width: 1400px; margin: 0 auto; }
        .page-header { margin-bottom: 25px; display: flex; justify-content: space-between; align-items: flex-start; flex-wrap: wrap; gap: 15px; }
        .page-header h1 { color: #5D4037; margin: 0 0 8px 0; font-size: 24px; display: flex; align-items: center; gap: 12px; }
        .page-header h1 i { color: #7C4DFF; }
        .page-header p { color: #999; margin: 0; font-size: 14px; }

        .stats-row { display: grid; grid-template-columns: repeat(auto-fit, minmax(160px, 1fr)); gap: 12px; margin-bottom: 25px; }
        .stat-card { background: white; border-radius: 10px; padding: 18px; text-align: center; box-shadow: 0 2px 8px rgba(0,0,0,0.06); }
        .stat-card .val { font-size: 28px; font-weight: 700; }
        .stat-card .lbl { font-size: 12px; color: #999; margin-top: 4px; }
        .stat-card.purple .val { color: #7C4DFF; }
        .stat-card.green .val { color: #4CAF50; }
        .stat-card.blue .val { color: #2196F3; }
        .stat-card.orange .val { color: #FF9800; }

        .kb-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 20px; }
        .kb-card { background: white; border-radius: 12px; padding: 25px; box-shadow: 0 2px 10px rgba(0,0,0,0.08); }
        .kb-card.full { grid-column: 1 / -1; }
        .kb-card h3 { margin: 0 0 20px 0; font-size: 16px; color: #333; display: flex; align-items: center; gap: 10px; border-bottom: 2px solid #f0f0f0; padding-bottom: 15px; }
        .kb-card h3 i { color: #7C4DFF; font-size: 20px; }

        .toolbar { display: flex; gap: 8px; margin-bottom: 15px; flex-wrap: wrap; align-items: center; }
        .toolbar input, .toolbar select { padding: 8px 12px; border: 1px solid #ddd; border-radius: 6px; font-size: 13px; font-family: 'Prompt', sans-serif; }
        .toolbar input { flex: 1; min-width: 180px; }
        .toolbar select { min-width: 120px; }

        .btn { padding: 8px 16px; border: none; border-radius: 8px; cursor: pointer; font-weight: 500; font-size: 13px; font-family: 'Prompt', sans-serif; display: inline-flex; align-items: center; gap: 6px; }
        .btn-primary { background: #7C4DFF; color: white; }
        .btn-primary:hover { background: #651FFF; }
        .btn-success { background: #4CAF50; color: white; }
        .btn-success:hover { background: #388E3C; }
        .btn-outline { background: white; color: #7C4DFF; border: 1px solid #7C4DFF; }
        .btn-outline:hover { background: #F3E5F5; }
        .btn-danger { background: #F44336; color: white; }
        .btn-danger:hover { background: #D32F2F; }
        .btn-sm { padding: 5px 10px; font-size: 12px; }
        .btn-warning { background: #FF9800; color: white; }

        .kb-table { width: 100%; border-collapse: collapse; font-size: 13px; }
        .kb-table th { background: #f5f5f5; padding: 10px 12px; text-align: left; font-weight: 600; color: #555; border-bottom: 2px solid #e0e0e0; }
        .kb-table td { padding: 10px 12px; border-bottom: 1px solid #f0f0f0; vertical-align: top; }
        .kb-table tr:hover { background: #fafafa; }
        .kb-table .cat-badge { display: inline-block; padding: 2px 8px; border-radius: 10px; font-size: 11px; font-weight: 600; background: #EDE7F6; color: #7C4DFF; }
        .kb-table .match-count { color: #999; font-size: 11px; }
        .kb-table .q-text { color: #333; font-weight: 500; max-width: 250px; }
        .kb-table .a-text { color: #666; max-width: 350px; line-height: 1.4; }
        .kb-table .actions { white-space: nowrap; }

        .pattern-table { width: 100%; border-collapse: collapse; font-size: 13px; }
        .pattern-table th { background: #f5f5f5; padding: 10px 12px; text-align: left; font-weight: 600; color: #555; border-bottom: 2px solid #e0e0e0; }
        .pattern-table td { padding: 10px 12px; border-bottom: 1px solid #f0f0f0; vertical-align: top; }
        .pattern-table tr:hover { background: #fafafa; }
        .confidence-bar { width: 60px; height: 6px; background: #eee; border-radius: 3px; display: inline-block; vertical-align: middle; margin-right: 6px; }
        .confidence-fill { height: 100%; border-radius: 3px; background: #4CAF50; }

        .modal-overlay { display: none; position: fixed; top: 0; left: 0; right: 0; bottom: 0; background: rgba(0,0,0,0.5); z-index: 1000; justify-content: center; align-items: center; }
        .modal-overlay.active { display: flex; }
        .modal { background: white; border-radius: 12px; padding: 25px; width: 90%; max-width: 600px; max-height: 85vh; overflow-y: auto; }
        .modal h3 { margin: 0 0 20px 0; font-size: 18px; color: #333; }
        .modal .form-group { margin-bottom: 15px; }
        .modal .form-group label { display: block; font-weight: 500; font-size: 13px; color: #555; margin-bottom: 6px; }
        .modal .form-group input, .modal .form-group select, .modal .form-group textarea {
            width: 100%; padding: 8px 12px; border: 1px solid #ddd; border-radius: 6px;
            font-size: 13px; font-family: 'Prompt', sans-serif; box-sizing: border-box;
        }
        .modal .form-group textarea { resize: vertical; min-height: 80px; }
        .modal .btn-row { display: flex; gap: 8px; justify-content: flex-end; margin-top: 20px; }

        .upload-area { border: 2px dashed #ddd; border-radius: 10px; padding: 30px; text-align: center; cursor: pointer; transition: border-color 0.3s; }
        .upload-area:hover { border-color: #7C4DFF; }
        .upload-area i { font-size: 36px; color: #ccc; margin-bottom: 10px; }
        .upload-area p { color: #999; font-size: 13px; margin: 5px 0; }
        .upload-area input[type=file] { display: none; }

        .upload-format { background: #f9f9f9; border-radius: 8px; padding: 12px; margin-top: 12px; font-size: 12px; color: #666; }
        .upload-format code { background: #eee; padding: 1px 4px; border-radius: 3px; font-size: 11px; }

        .empty-state { text-align: center; padding: 40px 20px; color: #ccc; }
        .empty-state i { font-size: 40px; margin-bottom: 10px; }
        .empty-state p { font-size: 14px; }

        .tab-bar { display: flex; gap: 0; border-bottom: 2px solid #f0f0f0; margin-bottom: 20px; }
        .tab-bar .tab { padding: 10px 20px; font-size: 13px; font-weight: 500; color: #999; cursor: pointer; border-bottom: 2px solid transparent; margin-bottom: -2px; transition: all 0.2s; }
        .tab-bar .tab.active { color: #7C4DFF; border-bottom-color: #7C4DFF; }
        .tab-bar .tab:hover { color: #7C4DFF; }

        .tab-content { display: none; }
        .tab-content.active { display: block; }

        .toast { position: fixed; bottom: 20px; right: 20px; padding: 12px 20px; border-radius: 8px; color: white; font-size: 13px; z-index: 2000; opacity: 0; transition: opacity 0.3s; }
        .toast.show { opacity: 1; }
        .toast.success { background: #4CAF50; }
        .toast.error { background: #F44336; }

        @media (max-width: 768px) {
            .kb-grid { grid-template-columns: 1fr; }
            .stats-row { grid-template-columns: repeat(2, 1fr); }
            .toolbar { flex-direction: column; }
            .toolbar input, .toolbar select { min-width: 100%; }
        }
    </style>

    <asp:HiddenField ID="hfInitData" runat="server" />

    <div class="kb-page">
        <div class="page-header">
            <div>
                <h1><i class="fas fa-brain"></i> AI Knowledge Base & Learning</h1>
                <p>จัดการฐานข้อมูลความรู้ AI และดูรูปแบบที่เรียนรู้จากพนักงาน</p>
            </div>
            <div style="display:flex; gap:8px;">
                <button class="btn btn-outline" onclick="showImportModal()"><i class="fas fa-file-upload"></i> นำเข้าข้อมูล</button>
                <button class="btn btn-primary" onclick="showAddModal()"><i class="fas fa-plus"></i> เพิ่ม Q&A</button>
            </div>
        </div>

        <!-- Stats -->
        <div class="stats-row" id="statsRow">
            <div class="stat-card purple">
                <div class="val" id="statKBCount">0</div>
                <div class="lbl">Knowledge Base</div>
            </div>
            <div class="stat-card green">
                <div class="val" id="statPatternCount">0</div>
                <div class="lbl">Learned Patterns</div>
            </div>
            <div class="stat-card blue">
                <div class="val" id="statKBMatches">0</div>
                <div class="lbl">KB Matches</div>
            </div>
            <div class="stat-card orange">
                <div class="val" id="statPatternMatches">0</div>
                <div class="lbl">Pattern Matches</div>
            </div>
            <div class="stat-card">
                <div class="val" id="statAvgConf">0%</div>
                <div class="lbl">Avg Confidence</div>
            </div>
        </div>

        <!-- Tabs -->
        <div class="tab-bar">
            <div class="tab active" onclick="switchTab('kb')"><i class="fas fa-book"></i> Knowledge Base</div>
            <div class="tab" onclick="switchTab('patterns')"><i class="fas fa-graduation-cap"></i> Learned Patterns</div>
            <div class="tab" onclick="switchTab('features')"><i class="fas fa-sliders-h"></i> AI Feature Settings</div>
        </div>

        <!-- Knowledge Base Tab -->
        <div id="tabKB" class="tab-content active">
            <div class="toolbar">
                <input type="text" id="txtKBSearch" placeholder="ค้นหาคำถาม/คำตอบ..." onkeypress="if(event.key==='Enter')loadKB()" />
                <select id="ddlKBCategory" onchange="loadKB()">
                    <option value="">ทุกหมวดหมู่</option>
                    <option value="CHECK_IN">Check-in</option>
                    <option value="CHECK_OUT">Check-out</option>
                    <option value="WIFI">WiFi</option>
                    <option value="LOCATION">Location</option>
                    <option value="PARKING">Parking</option>
                    <option value="PET">Pet</option>
                    <option value="BREAKFAST">Breakfast</option>
                    <option value="CANCEL">Cancel</option>
                    <option value="BOOKING">Booking</option>
                    <option value="PRICE">Price</option>
                    <option value="GENERAL">General</option>
                </select>
                <button class="btn btn-outline" onclick="loadKB()"><i class="fas fa-search"></i> ค้นหา</button>
            </div>

            <div style="overflow-x: auto;">
                <table class="kb-table">
                    <thead>
                        <tr>
                            <th style="width:90px;">หมวดหมู่</th>
                            <th>คำถาม</th>
                            <th>คำตอบ</th>
                            <th style="width:60px;">Keywords</th>
                            <th style="width:50px;">Match</th>
                            <th style="width:50px;">Source</th>
                            <th style="width:100px;">จัดการ</th>
                        </tr>
                    </thead>
                    <tbody id="kbBody">
                        <tr><td colspan="7"><div class="empty-state"><i class="fas fa-spinner fa-spin"></i><p>กำลังโหลด...</p></div></td></tr>
                    </tbody>
                </table>
            </div>
        </div>

        <!-- Learned Patterns Tab -->
        <div id="tabPatterns" class="tab-content">
            <div class="toolbar">
                <span style="color:#666; font-size:13px;"><i class="fas fa-info-circle"></i> รูปแบบที่เรียนรู้จากการตอบของพนักงาน (confidence เพิ่มขึ้นทุกครั้งที่ถูกใช้ซ้ำ)</span>
            </div>
            <div style="overflow-x: auto;">
                <table class="pattern-table">
                    <thead>
                        <tr>
                            <th>Input Pattern</th>
                            <th>Response</th>
                            <th style="width:80px;">Keywords</th>
                            <th style="width:100px;">Confidence</th>
                            <th style="width:60px;">Success</th>
                            <th style="width:80px;">Learned From</th>
                            <th style="width:80px;">จัดการ</th>
                        </tr>
                    </thead>
                    <tbody id="patternBody">
                        <tr><td colspan="7"><div class="empty-state"><i class="fas fa-spinner fa-spin"></i><p>กำลังโหลด...</p></div></td></tr>
                    </tbody>
                </table>
            </div>
        </div>

        <!-- Feature Settings Tab -->
        <div id="tabFeatures" class="tab-content">
            <div class="kb-card" style="box-shadow:none; padding:0;">
                <div id="featuresList"></div>
            </div>
        </div>
    </div>

    <!-- Add/Edit Modal -->
    <div class="modal-overlay" id="modalKB">
        <div class="modal">
            <h3 id="modalTitle"><i class="fas fa-plus-circle"></i> เพิ่ม Q&A ใหม่</h3>
            <input type="hidden" id="editId" value="0" />
            <div class="form-group">
                <label>หมวดหมู่</label>
                <select id="modalCategory">
                    <option value="GENERAL">General</option>
                    <option value="CHECK_IN">Check-in</option>
                    <option value="CHECK_OUT">Check-out</option>
                    <option value="WIFI">WiFi</option>
                    <option value="LOCATION">Location</option>
                    <option value="PARKING">Parking</option>
                    <option value="PET">Pet</option>
                    <option value="BREAKFAST">Breakfast</option>
                    <option value="CANCEL">Cancel</option>
                    <option value="BOOKING">Booking</option>
                    <option value="PRICE">Price</option>
                </select>
            </div>
            <div class="form-group">
                <label>คำถาม</label>
                <textarea id="modalQuestion" rows="2" placeholder="เช่น: เช็คอินกี่โมง"></textarea>
            </div>
            <div class="form-group">
                <label>คำตอบ</label>
                <textarea id="modalAnswer" rows="4" placeholder="คำตอบที่ AI จะใช้ตอบ"></textarea>
            </div>
            <div class="form-group">
                <label>Keywords (คั่นด้วย ,)</label>
                <input type="text" id="modalKeywords" placeholder="เช็คอิน,check in,กี่โมง" />
            </div>
            <div class="btn-row">
                <button class="btn btn-outline" onclick="closeModal('modalKB')">ยกเลิก</button>
                <button class="btn btn-primary" onclick="saveKBEntry()"><i class="fas fa-save"></i> บันทึก</button>
            </div>
        </div>
    </div>

    <!-- Import Modal -->
    <div class="modal-overlay" id="modalImport">
        <div class="modal">
            <h3><i class="fas fa-file-upload"></i> นำเข้าข้อมูล Knowledge Base</h3>
            <div class="tab-bar" style="margin-bottom:15px;">
                <div class="tab active" onclick="switchImportTab('csv')">CSV File</div>
                <div class="tab" onclick="switchImportTab('text')">Text (Q&A)</div>
            </div>

            <div id="importCSV" class="tab-content active">
                <div class="upload-area" onclick="document.getElementById('fileCsv').click()">
                    <i class="fas fa-file-csv"></i>
                    <p>คลิกเพื่อเลือกไฟล์ CSV</p>
                    <p id="csvFileName" style="color:#7C4DFF; font-weight:500;"></p>
                    <input type="file" id="fileCsv" accept=".csv" onchange="csvSelected(this)" />
                </div>
                <div class="upload-format">
                    <strong>รูปแบบ CSV:</strong><br/>
                    <code>category,question,answer,keywords</code> (แถวแรกเป็น header)<br/>
                    หรือ <code>question,answer</code> (จะใช้หมวด GENERAL อัตโนมัติ)
                </div>
            </div>

            <div id="importText" class="tab-content">
                <div class="form-group">
                    <label>วาง Q&A (สลับบรรทัด คำถาม/คำตอบ)</label>
                    <textarea id="txtImportText" rows="10" placeholder="Q: เช็คอินกี่โมง&#10;A: เวลาเช็คอิน 14:00 น. ค่ะ&#10;Q: WiFi คืออะไร&#10;A: WiFi ชื่อ TakeTime-Guest รหัส TakeTime2024"></textarea>
                </div>
                <div class="upload-format">
                    <strong>รูปแบบ:</strong> สลับบรรทัด Q/A ทีละคู่<br/>
                    <code>Q: คำถาม</code> (บรรทัดคี่)<br/>
                    <code>A: คำตอบ</code> (บรรทัดคู่)
                </div>
            </div>

            <div id="importResult" style="display:none; margin-top:10px; padding:10px; border-radius:8px; font-size:13px;"></div>

            <div class="btn-row">
                <button class="btn btn-outline" onclick="closeModal('modalImport')">ปิด</button>
                <button class="btn btn-success" onclick="doImport()"><i class="fas fa-upload"></i> นำเข้า</button>
            </div>
        </div>
    </div>

    <div id="toast" class="toast"></div>

    <script>
        $(document).ready(function () {
            loadKB();
            loadPatterns();
            loadFeatures();
            loadStats();
        });

        function ajaxPost(action, data, cb) {
            $.ajax({
                url: window.location.pathname + '?action=' + action,
                type: 'POST', contentType: 'application/json',
                data: JSON.stringify(data),
                success: function (r) { cb(r); },
                error: function () { cb({ success: false, message: 'เกิดข้อผิดพลาด' }); }
            });
        }

        // === Tabs ===
        function switchTab(tab) {
            $('.tab-bar .tab').removeClass('active');
            $('.tab-content').removeClass('active');
            if (tab === 'kb') { $('.tab-bar .tab:eq(0)').addClass('active'); $('#tabKB').addClass('active'); }
            else if (tab === 'patterns') { $('.tab-bar .tab:eq(1)').addClass('active'); $('#tabPatterns').addClass('active'); }
            else { $('.tab-bar .tab:eq(2)').addClass('active'); $('#tabFeatures').addClass('active'); }
        }

        function switchImportTab(t) {
            $('#modalImport .tab-bar .tab').removeClass('active');
            $('#modalImport .tab-content').removeClass('active');
            if (t === 'csv') { $('#modalImport .tab-bar .tab:eq(0)').addClass('active'); $('#importCSV').addClass('active'); }
            else { $('#modalImport .tab-bar .tab:eq(1)').addClass('active'); $('#importText').addClass('active'); }
        }

        // === Knowledge Base ===
        function loadKB() {
            var cat = $('#ddlKBCategory').val();
            var search = $('#txtKBSearch').val().trim();
            ajaxPost('getKB', { category: cat, search: search }, function (r) {
                var html = '';
                if (!r.entries || r.entries.length === 0) {
                    html = '<tr><td colspan="7"><div class="empty-state"><i class="fas fa-inbox"></i><p>ไม่พบข้อมูล</p></div></td></tr>';
                } else {
                    for (var i = 0; i < r.entries.length; i++) {
                        var e = r.entries[i];
                        html += '<tr>' +
                            '<td><span class="cat-badge">' + esc(e.category) + '</span></td>' +
                            '<td class="q-text">' + esc(e.question).substring(0, 80) + '</td>' +
                            '<td class="a-text">' + esc(e.answer).substring(0, 120) + '</td>' +
                            '<td style="font-size:11px; color:#999;">' + esc(e.keywords || '').substring(0, 40) + '</td>' +
                            '<td class="match-count">' + e.matchCount + '</td>' +
                            '<td style="font-size:11px;">' + esc(e.source) + '</td>' +
                            '<td class="actions">' +
                                '<button class="btn btn-outline btn-sm" onclick="editKB(' + e.id + ')"><i class="fas fa-edit"></i></button> ' +
                                '<button class="btn btn-danger btn-sm" onclick="deleteKB(' + e.id + ')"><i class="fas fa-trash"></i></button>' +
                            '</td></tr>';
                    }
                }
                $('#kbBody').html(html);
            });
        }

        function showAddModal() {
            $('#editId').val(0);
            $('#modalTitle').html('<i class="fas fa-plus-circle"></i> เพิ่ม Q&A ใหม่');
            $('#modalCategory').val('GENERAL');
            $('#modalQuestion').val('');
            $('#modalAnswer').val('');
            $('#modalKeywords').val('');
            $('#modalKB').addClass('active');
        }

        function editKB(id) {
            ajaxPost('getKBEntry', { id: id }, function (r) {
                if (!r.entry) return;
                $('#editId').val(r.entry.id);
                $('#modalTitle').html('<i class="fas fa-edit"></i> แก้ไข Q&A');
                $('#modalCategory').val(r.entry.category);
                $('#modalQuestion').val(r.entry.question);
                $('#modalAnswer').val(r.entry.answer);
                $('#modalKeywords').val(r.entry.keywords || '');
                $('#modalKB').addClass('active');
            });
        }

        function saveKBEntry() {
            var data = {
                id: parseInt($('#editId').val()) || 0,
                category: $('#modalCategory').val(),
                question: $('#modalQuestion').val().trim(),
                answer: $('#modalAnswer').val().trim(),
                keywords: $('#modalKeywords').val().trim()
            };
            if (!data.question || !data.answer) { showToast('กรุณากรอกคำถามและคำตอบ', 'error'); return; }
            ajaxPost('saveKB', data, function (r) {
                if (r.success) {
                    closeModal('modalKB');
                    loadKB();
                    loadStats();
                    showToast('บันทึกสำเร็จ', 'success');
                } else {
                    showToast(r.message || 'เกิดข้อผิดพลาด', 'error');
                }
            });
        }

        function deleteKB(id) {
            if (!confirm('ต้องการลบรายการนี้?')) return;
            ajaxPost('deleteKB', { id: id }, function (r) {
                loadKB();
                loadStats();
                showToast('ลบแล้ว', 'success');
            });
        }

        // === Patterns ===
        function loadPatterns() {
            ajaxPost('getPatterns', {}, function (r) {
                var html = '';
                if (!r.patterns || r.patterns.length === 0) {
                    html = '<tr><td colspan="7"><div class="empty-state"><i class="fas fa-graduation-cap"></i><p>ยังไม่มีรูปแบบที่เรียนรู้</p></div></td></tr>';
                } else {
                    for (var i = 0; i < r.patterns.length; i++) {
                        var p = r.patterns[i];
                        var confPct = Math.round(p.confidence * 100);
                        var confColor = confPct >= 80 ? '#4CAF50' : confPct >= 50 ? '#FF9800' : '#F44336';
                        html += '<tr>' +
                            '<td style="max-width:200px;">' + esc(p.inputPattern).substring(0, 80) + '</td>' +
                            '<td style="max-width:250px;">' + esc(p.response).substring(0, 100) + '</td>' +
                            '<td style="font-size:11px; color:#999;">' + esc(p.keywords || '').substring(0, 30) + '</td>' +
                            '<td><span class="confidence-bar"><span class="confidence-fill" style="width:' + confPct + '%; background:' + confColor + ';"></span></span>' + confPct + '%</td>' +
                            '<td>' + p.successCount + '</td>' +
                            '<td style="font-size:11px;">' + esc(p.learnedFrom || '-') + '</td>' +
                            '<td><button class="btn btn-danger btn-sm" onclick="deletePattern(' + p.id + ')"><i class="fas fa-trash"></i></button></td>' +
                            '</tr>';
                    }
                }
                $('#patternBody').html(html);
            });
        }

        function deletePattern(id) {
            if (!confirm('ต้องการลบรูปแบบนี้?')) return;
            ajaxPost('deletePattern', { id: id }, function (r) {
                loadPatterns();
                loadStats();
                showToast('ลบแล้ว', 'success');
            });
        }

        // === Features ===
        function loadFeatures() {
            ajaxPost('getFeatures', {}, function (r) {
                if (!r.features || r.features.length === 0) { $('#featuresList').html('<p style="color:#999;">ไม่พบข้อมูล</p>'); return; }
                var html = '';
                for (var i = 0; i < r.features.length; i++) {
                    var f = r.features[i];
                    html += '<div style="display:flex; align-items:center; justify-content:space-between; padding:16px 0; border-bottom:1px solid #f0f0f0;">' +
                        '<div>' +
                            '<div style="font-weight:500; font-size:14px; color:#333;">' + esc(f.name) + ' <span style="font-size:11px; color:#999;">(' + esc(f.code) + ')</span></div>' +
                            '<div style="font-size:12px; color:#999; margin-top:3px;">' + esc(f.description || '') + '</div>' +
                        '</div>' +
                        '<label style="position:relative; width:48px; height:26px; flex-shrink:0;">' +
                            '<input type="checkbox" ' + (f.enabled ? 'checked' : '') + ' onchange="toggleFeature(\'' + esc(f.code) + '\', this.checked)" style="opacity:0; width:0; height:0;" />' +
                            '<span style="position:absolute; cursor:pointer; top:0; left:0; right:0; bottom:0; background:' + (f.enabled ? '#7C4DFF' : '#ccc') + '; border-radius:26px; transition:0.3s;">' +
                                '<span style="position:absolute; content:\'\'; height:20px; width:20px; left:' + (f.enabled ? '25px' : '3px') + '; bottom:3px; background:white; border-radius:50%; transition:0.3s; display:block;"></span>' +
                            '</span>' +
                        '</label>' +
                    '</div>';
                }
                $('#featuresList').html(html);
            });
        }

        function toggleFeature(code, enabled) {
            ajaxPost('setFeature', { code: code, enabled: enabled }, function (r) {
                loadFeatures();
                showToast(r.success ? 'อัพเดทสำเร็จ' : (r.message || 'เกิดข้อผิดพลาด'), r.success ? 'success' : 'error');
            });
        }

        // === Stats ===
        function loadStats() {
            ajaxPost('getStats', {}, function (r) {
                $('#statKBCount').text(r.kbCount || 0);
                $('#statPatternCount').text(r.patternCount || 0);
                $('#statKBMatches').text(r.kbMatches || 0);
                $('#statPatternMatches').text(r.patternMatches || 0);
                var conf = r.avgConfidence || 0;
                $('#statAvgConf').text(Math.round(conf * 100) + '%');
            });
        }

        // === Import ===
        function showImportModal() {
            $('#importResult').hide();
            $('#csvFileName').text('');
            $('#txtImportText').val('');
            $('#modalImport').addClass('active');
        }

        function csvSelected(input) {
            if (input.files.length > 0) $('#csvFileName').text(input.files[0].name);
        }

        function doImport() {
            var $res = $('#importResult');
            if ($('#importCSV').hasClass('active')) {
                var file = document.getElementById('fileCsv').files[0];
                if (!file) { showToast('กรุณาเลือกไฟล์ CSV', 'error'); return; }
                var reader = new FileReader();
                reader.onload = function (e) {
                    ajaxPost('importCsv', { content: e.target.result }, function (r) {
                        $res.show().css({ background: r.success ? '#E8F5E9' : '#FFEBEE', color: r.success ? '#2E7D32' : '#C62828' })
                            .text(r.success ? 'นำเข้าสำเร็จ ' + r.count + ' รายการ' : (r.message || 'เกิดข้อผิดพลาด'));
                        if (r.success) { loadKB(); loadStats(); }
                    });
                };
                reader.readAsText(file);
            } else {
                var text = $('#txtImportText').val().trim();
                if (!text) { showToast('กรุณาวางข้อมูล Q&A', 'error'); return; }
                ajaxPost('importText', { content: text }, function (r) {
                    $res.show().css({ background: r.success ? '#E8F5E9' : '#FFEBEE', color: r.success ? '#2E7D32' : '#C62828' })
                        .text(r.success ? 'นำเข้าสำเร็จ ' + r.count + ' รายการ' : (r.message || 'เกิดข้อผิดพลาด'));
                    if (r.success) { loadKB(); loadStats(); }
                });
            }
        }

        // === Helpers ===
        function closeModal(id) { $('#' + id).removeClass('active'); }
        function esc(s) { return $('<div>').text(s || '').html(); }
        function showToast(msg, type) {
            var $t = $('#toast');
            $t.removeClass('success error show').addClass(type).text(msg).addClass('show');
            setTimeout(function () { $t.removeClass('show'); }, 3000);
        }
    </script>
</asp:Content>
