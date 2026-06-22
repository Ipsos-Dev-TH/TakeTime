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
        .nexaacc-doc-link { color: #1a73e8; text-decoration: none; font-size: 12px; font-weight: 500; }
        .nexaacc-doc-link:hover { text-decoration: underline; color: #0d47a1; }
        .nexaacc-doc-link i { font-size: 10px; margin-left: 3px; }
        .nexaacc-doc-label { font-size: 12px; color: #555; }

        .queue-stats { display: grid; grid-template-columns: repeat(auto-fit, minmax(120px, 1fr)); gap: 12px; margin-bottom: 15px; }
        .queue-stat { background: #f9f9f9; border-radius: 8px; padding: 12px; text-align: center; cursor: pointer; transition: all 0.2s; border: 2px solid transparent; }
        .queue-stat:hover { border-color: #ddd; }
        .queue-stat.active { border-color: #5D4037; background: #EFEBE9; }
        .queue-stat .num { font-size: 22px; font-weight: 700; }
        .queue-stat .lbl { font-size: 11px; color: #999; }
        .num-pending { color: #FF9800; }
        .num-processing { color: #2196F3; }
        .num-completed { color: #4CAF50; }
        .num-failed { color: #F44336; }

        /* Pagination */
        .queue-toolbar { display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 10px; margin-bottom: 15px; }
        .queue-toolbar-left { display: flex; align-items: center; gap: 8px; }
        .queue-toolbar-right { display: flex; align-items: center; gap: 8px; }
        .queue-toolbar select { padding: 6px 10px; border: 1px solid #ddd; border-radius: 6px; font-size: 12px; font-family: 'Prompt', sans-serif; }
        .pagination { display: flex; align-items: center; gap: 4px; }
        .pagination button { width: 32px; height: 32px; border: 1px solid #ddd; background: white; border-radius: 6px; cursor: pointer; font-size: 12px; font-family: 'Prompt', sans-serif; display: flex; align-items: center; justify-content: center; }
        .pagination button:hover:not(:disabled) { background: #f5f5f5; border-color: #bbb; }
        .pagination button:disabled { opacity: 0.4; cursor: default; }
        .pagination button.active { background: #5D4037; color: white; border-color: #5D4037; }
        .pagination .page-info { font-size: 12px; color: #777; padding: 0 8px; white-space: nowrap; }
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
                    <button type="button" class="btn-primary" onclick="syncAccounts()"><i class="fas fa-cloud-download-alt"></i> Sync บัญชี</button>
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
                    <label>โหมดการ Sync (ค่าเริ่มต้น)</label>
                    <select id="cfgSyncMode">
                        <option value="DOCUMENT">สร้างเอกสาร + บันทึกบัญชีอัตโนมัติ</option>
                        <option value="JOURNAL_ONLY">บันทึกสมุดบัญชีอย่างเดียว</option>
                    </select>
                    <div class="help-text">ค่าเริ่มต้นสำหรับเอกสารที่ไม่ได้ตั้งค่าเฉพาะด้านล่าง</div>
                </div>
                <div class="config-item" style="margin-left:20px;">
                    <label>ใบเสร็จ / ใบกำกับภาษี</label>
                    <select id="cfgReceiptSyncMode">
                        <option value="">ใช้ค่าเริ่มต้น</option>
                        <option value="LOCAL">LOCAL — ใช้เอกสารจากระบบ TakeTime (ไม่ส่ง NextAcc)</option>
                        <option value="DOCUMENT">DOCUMENT — สร้างเอกสารใน NextAcc</option>
                        <option value="JOURNAL_ONLY">JOURNAL_ONLY — บันทึกสมุดบัญชีอย่างเดียว</option>
                    </select>
                </div>
                <div class="config-item" style="margin-left:20px;">
                    <label>ใบสำคัญจ่าย</label>
                    <select id="cfgVoucherSyncMode">
                        <option value="">ใช้ค่าเริ่มต้น</option>
                        <option value="LOCAL">LOCAL — ไม่ส่ง NextAcc</option>
                        <option value="DOCUMENT">DOCUMENT — สร้างเอกสารค่าใช้จ่ายใน NextAcc</option>
                        <option value="JOURNAL_ONLY">JOURNAL_ONLY — บันทึกสมุดบัญชีอย่างเดียว</option>
                    </select>
                </div>
                <div class="config-item" style="margin-left:20px;">
                    <label>เงินเดือน (Payroll)</label>
                    <select id="cfgPayrollSyncMode">
                        <option value="">ใช้ค่าเริ่มต้น</option>
                        <option value="LOCAL">LOCAL — ไม่ส่ง NextAcc</option>
                        <option value="DOCUMENT">DOCUMENT — สร้างเอกสารค่าใช้จ่ายใน NextAcc</option>
                        <option value="JOURNAL_ONLY">JOURNAL_ONLY — บันทึกสมุดบัญชีอย่างเดียว</option>
                    </select>
                </div>
                <div class="config-item">
                    <label>แนบไฟล์เอกสาร</label>
                    <select id="cfgAttachFiles">
                        <option value="true">เปิด — ส่งไฟล์ใบเสร็จ/สลิปไปพร้อมเอกสาร</option>
                        <option value="false">ปิด</option>
                    </select>
                    <div class="help-text">ส่งไฟล์แนบ (ใบเสร็จ PDF, สลิปจ่ายเงิน, ใบสำคัญจ่าย) ไปพร้อมเอกสารใน NextAcc</div>
                </div>
                <div class="config-item">
                    <label>จุดรับรู้ VAT ของเงินมัดจำ</label>
                    <select id="cfgDepositVatRecognition">
                        <option value="CHECKOUT">CHECKOUT — รับรู้ VAT ตอนตัดมัดจำเป็นรายได้ (เช็คเอาท์)</option>
                        <option value="RECEIPT">RECEIPT — รับรู้ VAT ทันทีที่รับเงินมัดจำ (ม.78/1 บริการ)</option>
                    </select>
                    <div class="help-text">
                        RECEIPT: เมื่อรับมัดจำ ระบบจะแยก VAT ยิงเข้าบัญชีภาษีขาย (OUTPUT_VAT) ทันที —
                        ถูกต้องตามประมวลรัษฎากร ม.78/1 (ธุรกิจบริการ จุดความรับผิด VAT = วันรับเงิน).
                        CHECKOUT: เลื่อนรับรู้ VAT ไปตอนตัดมัดจำเป็นรายได้
                    </div>
                </div>
                <div class="config-item">
                    <label>
                        <input type="checkbox" id="cfgDepositDeferOutputVat" />
                        พักภาษีขายมัดจำไว้ที่ "ภาษีขายรอเรียกเก็บ" (21913) แล้วโอนเข้าภาษีขายตอนเช็คเอาท์
                    </label>
                    <div class="help-text">
                        ใช้ได้เฉพาะโหมด RECEIPT — เมื่อเปิด ตอนรับมัดจำจะ Cr บัญชี OUTPUT_VAT_DEFERRED (21913)
                        แทน OUTPUT_VAT แล้วโอนกลับเข้า OUTPUT_VAT (21911) ตอนเช็คเอาท์ (VAT ขึ้น ภ.พ.30 ตอนรับรู้รายได้).
                        ⚠ ต้อง map บัญชี OUTPUT_VAT_DEFERRED ก่อนเปิด มิฉะนั้นระบบจะ fallback กลับไปใช้ OUTPUT_VAT
                    </div>
                </div>
                <div class="config-item" style="border-top:1px solid #ddd; margin-top:15px; padding-top:15px;">
                    <label><i class="fas fa-file-invoice"></i> ใบกำกับภาษีอิเล็กทรอนิกส์ (E-Tax)</label>
                    <div style="background:#f8f9fa; padding:10px; border-radius:4px; font-size:12px; color:#555; margin-bottom:8px;">
                        เมื่อระบบสร้างใบเสร็จใน NextAcc สำเร็จ จะสร้าง E-Tax Invoice ตามใบนั้น และส่งให้กรมสรรพากร/อีเมลลูกค้าได้อัตโนมัติ
                    </div>
                </div>
                <div class="config-item">
                    <label>สร้าง E-Tax อัตโนมัติ</label>
                    <select id="cfgEtaxAutoGenerate">
                        <option value="false">ปิด — สร้าง E-Tax ด้วยตัวเองในหน้า Receipt</option>
                        <option value="true">เปิด — สร้างทันทีเมื่อสร้างใบเสร็จในระบบ NextAcc สำเร็จ</option>
                    </select>
                    <div class="help-text">ใช้ได้เฉพาะเมื่อโหมด Receipt = DOCUMENT (สร้างเอกสารใน NextAcc)</div>
                </div>
                <div class="config-item">
                    <label>ลงนาม E-Tax อัตโนมัติ</label>
                    <select id="cfgEtaxAutoSign">
                        <option value="true">เปิด — ลงนามด้วย Certificate ใน NextAcc</option>
                        <option value="false">ปิด — สร้างเป็น draft</option>
                    </select>
                </div>
                <div class="config-item">
                    <label>ส่งกรมสรรพากรอัตโนมัติ</label>
                    <select id="cfgEtaxAutoSubmit">
                        <option value="false">ปิด — ส่งด้วยตนเอง</option>
                        <option value="true">เปิด — submit ทันทีหลังลงนาม</option>
                    </select>
                </div>
                <div class="config-item">
                    <label>ส่งอีเมลให้ลูกค้าอัตโนมัติ</label>
                    <select id="cfgEtaxAutoSendEmail">
                        <option value="false">ปิด</option>
                        <option value="true">เปิด — ส่งอีเมลพร้อม PDF/XML แนบ</option>
                    </select>
                    <div class="help-text">ดึงอีเมลลูกค้าจาก Customer.Email ตามการจอง — ถ้าไม่พบจะข้ามอย่างเงียบ ๆ</div>
                </div>
                <div class="config-item">
                    <label>หัวข้ออีเมล E-Tax</label>
                    <input type="text" id="cfgEtaxEmailSubject" placeholder="ใบกำกับภาษีอิเล็กทรอนิกส์ {ReceiptNumber}" />
                    <div class="help-text">ตัวแปร: {ReceiptNumber}, {GuestName}, {Amount}, {Date}</div>
                </div>
                <div class="config-item">
                    <label>เนื้อหาอีเมล E-Tax</label>
                    <textarea id="cfgEtaxEmailBody" rows="4" style="width:100%;" placeholder="เรียน {GuestName}&#10;&#10;กรุณาดาวน์โหลดใบกำกับภาษีอิเล็กทรอนิกส์ {ReceiptNumber}..."></textarea>
                </div>
                <div class="config-item">
                    <label>แนบ PDF / XML</label>
                    <select id="cfgEtaxEmailAttachPdf" style="width:48%;">
                        <option value="true">PDF: แนบ</option>
                        <option value="false">PDF: ไม่แนบ</option>
                    </select>
                    <select id="cfgEtaxEmailAttachXml" style="width:48%;">
                        <option value="false">XML: ไม่แนบ</option>
                        <option value="true">XML: แนบ (สำหรับลูกค้าธุรกิจ)</option>
                    </select>
                </div>
                <div class="config-item">
                    <label>ช่องทางส่งอีเมล</label>
                    <select id="cfgEtaxEmailLocalOnly">
                        <option value="false">NextAcc API (ค่าเริ่มต้น)</option>
                        <option value="true">SMTP ของ TakeTime เท่านั้น (ข้าม NextAcc)</option>
                    </select>
                    <div class="help-text">เลือก SMTP เฉพาะกรณี NextAcc email service ปิดอยู่ — ระบบจะดึง PDF/XML มาแนบเอง</div>
                </div>
                <div class="config-item">
                    <label>Fallback ผ่าน TakeTime SMTP</label>
                    <select id="cfgEtaxEmailFallback">
                        <option value="true">เปิด — ถ้า NextAcc ส่งไม่สำเร็จ ให้ส่งผ่าน SMTP ของ TakeTime</option>
                        <option value="false">ปิด — รายงาน error อย่างเดียว</option>
                    </select>
                    <div class="help-text">SMTP จะดึง PDF/XML จาก URL ของ NextAcc มาแนบในอีเมล (ตั้ง SMTP ใน Web.config)</div>
                </div>
                <div class="config-item" style="border-top:1px solid #ddd; margin-top:15px; padding-top:15px;">
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

        <!-- Deposit Lifecycle / สถานะเจ้าหนี้มัดจำ -->
        <div class="journey-card">
            <h3><i class="fas fa-piggy-bank"></i> สถานะเจ้าหนี้ค่ามัดจำ (Advance Deposit Liability)</h3>
            <p style="font-size:13px; color:#666; margin-bottom:15px;">
                ติดตามมัดจำที่ลูกค้าจ่าย vs มัดจำที่ตัดออกจากเจ้าหนี้แล้ว (ตอน checkout / คืนเงิน / ริบ)<br/>
                สถานะ: <b>OPEN</b> = ยังคาเจ้าหนี้, <b>PARTIAL</b> = ตัดบางส่วน, <b>CLEARED</b> = ตัดครบ, <b>OVER_CLEARED</b> = ตัดเกิน (ตรวจสอบ)
            </p>
            <div style="display:flex; gap:10px; align-items:end; margin-bottom:15px;">
                <div style="flex:1;">
                    <label style="display:block; font-weight:600; margin-bottom:6px;">Reservation ID หรือชื่อ/เบอร์ลูกค้า</label>
                    <input type="text" id="depositLookup" placeholder="เลขจอง / ชื่อลูกค้า / เบอร์โทร" style="width:100%; padding:8px;" />
                </div>
                <div>
                    <label style="display:block; font-weight:600; margin-bottom:6px;">สถานะ</label>
                    <select id="depositStatusFilter" style="padding:8px;">
                        <option value="">ทุกสถานะ</option>
                        <option value="OPEN">OPEN — ยังไม่ตัด</option>
                        <option value="PARTIAL">PARTIAL — ตัดบางส่วน</option>
                        <option value="CLEARED">CLEARED — ตัดครบ</option>
                        <option value="OVER_CLEARED">OVER_CLEARED — ตัดเกิน</option>
                    </select>
                </div>
                <button type="button" class="btn-primary" onclick="lookupDepositStatus()"><i class="fas fa-search"></i> ค้นหา</button>
            </div>
            <div id="depositStatusResult"></div>

            <div style="border-top:1px solid #ddd; margin-top:20px; padding-top:15px;">
                <h4 style="margin:0 0 10px 0; font-size:14px;"><i class="fas fa-bolt"></i> Manual Operations (ใช้กรณีระบบ auto ไม่ได้ทำงาน)</h4>
                <div style="display:grid; grid-template-columns:1fr 1fr 1fr; gap:10px; margin-bottom:10px;">
                    <div>
                        <label style="font-weight:600;">Reservation ID</label>
                        <input type="number" id="depositManualResId" style="width:100%; padding:8px;" />
                    </div>
                    <div>
                        <label style="font-weight:600;">จำนวนเงิน (บาท)</label>
                        <input type="number" id="depositManualAmount" step="0.01" style="width:100%; padding:8px;" />
                    </div>
                    <div>
                        <label style="font-weight:600;">เหตุผล (สำหรับ Forfeit)</label>
                        <input type="text" id="depositManualReason" placeholder="ลูกค้าไม่มาเข้าพัก" style="width:100%; padding:8px;" />
                    </div>
                </div>
                <div style="display:flex; gap:10px;">
                    <button type="button" class="btn-success" onclick="manualDepositAction('checkout')"><i class="fas fa-sign-out-alt"></i> ตัดมัดจำ (checkout)</button>
                    <button type="button" class="btn-warning" onclick="manualDepositAction('refund')"><i class="fas fa-undo"></i> คืนเงินมัดจำ</button>
                    <button type="button" class="btn-danger" onclick="manualDepositAction('forfeit')"><i class="fas fa-ban"></i> ริบมัดจำ</button>
                </div>
                <div class="test-result" id="depositManualResult" style="margin-top:10px;"></div>
            </div>
        </div>

        <!-- Document Source Lookup (เชื่อมระหว่าง TakeTime ↔ NextAcc ↔ E-Tax) -->
        <div class="journey-card">
            <h3><i class="fas fa-link"></i> Document Source — ตรวจสอบที่มาของเอกสาร</h3>
            <p style="font-size:13px; color:#666; margin-bottom:15px;">
                ค้นหาด้วยเลขที่ใบเสร็จหรือเลขจอง — ระบบจะแสดง: ที่มาของเอกสาร (LOCAL/NEXAACC), เลขเอกสาร NextAcc, สถานะ E-Tax, ลูกค้า, ลิงก์ PDF/XML
            </p>
            <div style="display:flex; gap:10px; align-items:end; margin-bottom:15px;">
                <div style="flex:1;">
                    <label style="display:block; font-weight:600; margin-bottom:6px;">เลขที่ใบเสร็จ หรือ Reservation ID</label>
                    <input type="text" id="docSourceLookup" placeholder="เช่น REC25050800001 หรือ 12345" style="width:100%; padding:8px;" />
                </div>
                <button type="button" class="btn-primary" onclick="lookupDocumentSource()"><i class="fas fa-search"></i> ค้นหา</button>
            </div>
            <div id="docSourceResult"></div>
        </div>

        <!-- Stock Adjustment / Write-off -->
        <div class="journey-card">
            <h3><i class="fas fa-boxes"></i> Stock Adjustment & Write-off</h3>
            <p style="font-size:13px; color:#666; margin-bottom:15px;">
                บันทึกการนับสต็อก (count variance) หรือตัดจำหน่ายสินค้าเสียหาย/หมดอายุ — ระบบจะสร้าง journal entry ใน NextAcc อัตโนมัติ
            </p>
            <div style="display:grid; grid-template-columns: 1fr 1fr 1fr 1fr; gap:10px; margin-bottom:10px;">
                <div>
                    <label style="font-weight:600;">Product ID</label>
                    <input type="number" id="stockProductId" style="width:100%; padding:8px;" />
                </div>
                <div>
                    <label style="font-weight:600;">Quantity</label>
                    <input type="number" id="stockQuantity" step="0.01" placeholder="+/- จำนวน" style="width:100%; padding:8px;" />
                </div>
                <div>
                    <label style="font-weight:600;">Cost / unit</label>
                    <input type="number" id="stockCostPerUnit" step="0.01" placeholder="ต้นทุน/หน่วย" style="width:100%; padding:8px;" />
                </div>
                <div>
                    <label style="font-weight:600;">เหตุผล</label>
                    <input type="text" id="stockReason" placeholder="เช่น นับสต็อก/หมดอายุ/แตก" style="width:100%; padding:8px;" />
                </div>
            </div>
            <div style="display:flex; gap:10px;">
                <button type="button" class="btn-primary" onclick="stockAdjustment('COUNT_VARIANCE')">
                    <i class="fas fa-balance-scale"></i> ปรับยอดจากการนับ (+/- diff)
                </button>
                <button type="button" class="btn-danger" onclick="stockAdjustment('WRITEOFF')">
                    <i class="fas fa-trash-alt"></i> ตัดจำหน่ายสินค้า (Write-off)
                </button>
                <button type="button" class="btn-success" onclick="stockProductSync()">
                    <i class="fas fa-sync"></i> Sync Product Master
                </button>
            </div>
            <div class="test-result" id="stockResult" style="margin-top:10px;"></div>
        </div>

        <!-- E-Tax Manual Operations -->
        <div class="journey-card">
            <h3><i class="fas fa-file-invoice"></i> E-Tax Manual Operations</h3>
            <p style="font-size:13px; color:#666; margin-bottom:15px;">
                สร้าง E-Tax ด้วยมือ หรือส่งอีเมล E-Tax ให้ลูกค้าซ้ำ — ใช้เมื่อต้องการดำเนินการเฉพาะใบเสร็จ หรือกรณี auto-generate ปิดอยู่
            </p>
            <div style="display:grid; grid-template-columns: 1fr 1fr; gap:20px;">
                <div>
                    <label style="display:block; font-weight:600; margin-bottom:6px;">เลขที่ใบเสร็จ</label>
                    <input type="text" id="etaxReceiptNumber" placeholder="เช่น REC25050800001" style="width:100%; padding:8px;" />
                    <div style="margin-top:10px;">
                        <button type="button" class="btn-primary" onclick="manualEtaxGenerate()"><i class="fas fa-bolt"></i> สร้าง E-Tax</button>
                        <button type="button" class="btn-success" onclick="manualEtaxSendEmail()"><i class="fas fa-envelope"></i> ส่งอีเมล E-Tax</button>
                    </div>
                </div>
                <div>
                    <label style="display:block; font-weight:600; margin-bottom:6px;">อีเมลปลายทาง (ทางเลือก)</label>
                    <input type="email" id="etaxOverrideEmail" placeholder="ปล่อยว่างเพื่อใช้อีเมลที่บันทึกใน Customer" style="width:100%; padding:8px;" />
                    <div style="font-size:12px; color:#666; margin-top:6px;">
                        ถ้าไม่ระบุ ระบบจะดึงอีเมลจาก Customer.Email ตามการจองอัตโนมัติ
                    </div>
                </div>
            </div>
            <div class="test-result" id="etaxTestResult" style="margin-top:15px;"></div>
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
                    <div class="step-accounting" id="jnDeposit">DR เงินสด/ธนาคาร<br/>CR เงินรับล่วงหน้า</div>
                </div>
                <div class="journey-arrow"><i class="fas fa-arrow-right"></i></div>

                <div class="journey-step active">
                    <div class="step-icon"><i class="fas fa-credit-card"></i></div>
                    <div class="step-name">ชำระเพิ่ม (Payment)</div>
                    <div class="step-accounting" id="jnPayment">DR เงินสด/ธนาคาร<br/>CR รายได้ห้องพัก</div>
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
                    <div class="step-accounting" id="jnCheckout">DR เงินรับล่วงหน้า<br/>CR รายได้ห้องพัก</div>
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
                        <div class="step-accounting" id="jnVoucher">DR ค่าใช้จ่าย<br/>CR เงินสด/ธนาคาร</div>
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
                <div class="queue-stat" onclick="filterByStatus('')" id="qsAll"><div class="num" id="qsTotal" style="color:#333;">-</div><div class="lbl">ทั้งหมด</div></div>
                <div class="queue-stat" onclick="filterByStatus('PENDING')" id="qsPendingCard"><div class="num num-pending" id="qsPending">-</div><div class="lbl">Pending</div></div>
                <div class="queue-stat" onclick="filterByStatus('PROCESSING')" id="qsProcessingCard"><div class="num num-processing" id="qsProcessing">-</div><div class="lbl">Processing</div></div>
                <div class="queue-stat" onclick="filterByStatus('COMPLETED')" id="qsCompletedCard"><div class="num num-completed" id="qsCompleted">-</div><div class="lbl">Completed</div></div>
                <div class="queue-stat" onclick="filterByStatus('FAILED')" id="qsFailedCard"><div class="num num-failed" id="qsFailed">-</div><div class="lbl">Failed</div></div>
            </div>

            <div class="queue-toolbar">
                <div class="queue-toolbar-left">
                    <button type="button" class="btn-primary" onclick="loadQueueData()"><i class="fas fa-sync"></i> รีเฟรช</button>
                    <button type="button" class="btn-warning" onclick="retryAllFailed()"><i class="fas fa-redo"></i> Retry Failed ทั้งหมด</button>
                    <button type="button" id="btnDeleteSelected" style="background:#dc3545;color:#fff;border:none;padding:6px 14px;border-radius:4px;cursor:pointer;font-size:13px;display:none;" onclick="deleteSelected()"><i class="fas fa-trash"></i> ลบที่เลือก (<span id="selectedCount">0</span>)</button>
                </div>
                <div class="queue-toolbar-right">
                    <label style="font-size:12px; color:#777;">แสดง</label>
                    <select id="queuePageSize" onchange="changePageSize()">
                        <option value="10">10</option>
                        <option value="20" selected>20</option>
                        <option value="50">50</option>
                        <option value="100">100</option>
                    </select>
                    <label style="font-size:12px; color:#777;">รายการ/หน้า</label>
                </div>
            </div>

            <div style="overflow-x:auto;">
                <table class="queue-table" id="queueTable">
                    <thead>
                        <tr>
                            <th style="width:30px;"><input type="checkbox" id="selectAllQueue" onchange="toggleSelectAll(this)" title="เลือกทั้งหมด" /></th>
                            <th>ID</th>
                            <th>Entity</th>
                            <th>Action</th>
                            <th>Status</th>
                            <th>NextAcc Doc</th>
                            <th>Retry</th>
                            <th>Error</th>
                            <th>Created</th>
                            <th></th>
                        </tr>
                    </thead>
                    <tbody id="queueBody">
                        <tr><td colspan="9" style="text-align:center; color:#999;">กำลังโหลด...</td></tr>
                    </tbody>
                </table>
            </div>

            <div class="pagination" id="queuePagination" style="margin-top:15px; justify-content:center;"></div>
        </div>

        <!-- Account Mapping Management -->
        <div class="journey-card">
            <h3><i class="fas fa-exchange-alt"></i> Account Mapping (TakeTime &harr; Nexaacc)</h3>
            <p style="font-size:13px; color:#666; margin-bottom:10px;">
                จับคู่รหัสบัญชี TakeTime กับ Nexaacc — กด "Sync บัญชี" ด้านบนเพื่อดึงผังบัญชีล่าสุด
            </p>
            <div style="font-size:12px; color:#888; margin-bottom:15px;" id="accountSyncInfo">
                <i class="fas fa-info-circle"></i> <span id="accountSyncStatus">ยังไม่ได้โหลดข้อมูล</span>
            </div>

            <div class="btn-row" style="margin-bottom:15px;">
                <button type="button" class="btn-primary" onclick="loadMappingsWithAccounts()"><i class="fas fa-sync"></i> โหลด Mapping</button>
                <select id="mappingTypeFilter" style="padding:6px 10px; border:1px solid #ddd; border-radius:4px; font-size:13px;" onchange="filterMappingsByType()">
                    <option value="">ทุกประเภท</option>
                    <option value="PAYMENT_METHOD">PAYMENT_METHOD</option>
                    <option value="REVENUE">REVENUE</option>
                    <option value="EXPENSE">EXPENSE</option>
                    <option value="ASSET">ASSET</option>
                    <option value="LIABILITY">LIABILITY</option>
                </select>
            </div>

            <div style="overflow-x:auto;">
                <table class="queue-table" id="mappingTable">
                    <thead>
                        <tr>
                            <th>TakeTime Code</th>
                            <th>คำอธิบาย</th>
                            <th style="min-width:260px;">บัญชี Nexaacc</th>
                            <th>ประเภท</th>
                            <th>สถานะ</th>
                            <th></th>
                        </tr>
                    </thead>
                    <tbody id="mappingBody">
                        <tr><td colspan="6" style="text-align:center; color:#999;">กดปุ่ม "โหลด Mapping" เพื่อดูข้อมูล</td></tr>
                    </tbody>
                </table>
            </div>
            <div class="test-result" id="mappingResult"></div>
        </div>

        <!-- Payment Method → Account Mapping -->
        <div class="journey-card">
            <h3><i class="fas fa-credit-card"></i> วิธีจ่ายเงิน &rarr; บัญชี NextAcc</h3>
            <p style="font-size:13px; color:#666; margin-bottom:10px;">
                ผูกวิธีจ่ายเงินแต่ละรายการกับบัญชี NextAcc โดยตรง — ระบบจะส่งไปบัญชีที่เลือกไว้ทุกครั้ง (ไม่ต้องพึ่ง text matching)
            </p>
            <button type="button" class="btn-primary" onclick="loadPaidHowMapping()" style="margin-bottom:10px;"><i class="fas fa-sync"></i> โหลดวิธีจ่ายเงิน</button>
            <div style="overflow-x:auto;">
                <table class="queue-table" id="paidHowTable">
                    <thead>
                        <tr>
                            <th>ID</th>
                            <th>วิธีจ่ายเงิน</th>
                            <th style="min-width:280px;">บัญชี NextAcc</th>
                            <th>สถานะ</th>
                        </tr>
                    </thead>
                    <tbody id="paidHowBody">
                        <tr><td colspan="4" style="text-align:center; color:#999;">กดปุ่ม "โหลดวิธีจ่ายเงิน" เพื่อดูข้อมูล</td></tr>
                    </tbody>
                </table>
            </div>
            <div class="test-result" id="paidHowResult"></div>
        </div>

        <!-- Expense Category → Account Mapping -->
        <div class="journey-card">
            <h3><i class="fas fa-receipt"></i> ประเภทค่าใช้จ่าย &rarr; บัญชี NextAcc</h3>
            <p style="font-size:13px; color:#666; margin-bottom:10px;">
                ผูกประเภทค่าใช้จ่ายกับบัญชี NextAcc — เช่น ค่าไฟ, ค่าน้ำ, ค่าแรง ฯลฯ จะไปลงบัญชีที่ถูกต้องอัตโนมัติ
            </p>
            <button type="button" class="btn-primary" onclick="loadPaidTypeMapping()" style="margin-bottom:10px;"><i class="fas fa-sync"></i> โหลดประเภทค่าใช้จ่าย</button>
            <div style="overflow-x:auto;">
                <table class="queue-table" id="paidTypeTable">
                    <thead>
                        <tr>
                            <th>ID</th>
                            <th>ประเภทค่าใช้จ่าย</th>
                            <th style="min-width:280px;">บัญชี NextAcc</th>
                            <th>สถานะ</th>
                        </tr>
                    </thead>
                    <tbody id="paidTypeBody">
                        <tr><td colspan="4" style="text-align:center; color:#999;">กดปุ่ม "โหลดประเภทค่าใช้จ่าย" เพื่อดูข้อมูล</td></tr>
                    </tbody>
                </table>
            </div>
            <div class="test-result" id="paidTypeResult"></div>
        </div>
    </div>

    <asp:HiddenField ID="hfConfigData" runat="server" />
    <asp:HiddenField ID="hfQueueData" runat="server" />

    <script>
        var pageUrl = '<%= ResolveUrl("~/Admin/Settings/AccountingIntegration") %>';
        var queueState = { page: 1, pageSize: 20, status: '', totalPages: 1 };

        document.addEventListener('DOMContentLoaded', function () {
            loadConfigData();
            loadQueueData();
            preloadNexaaccAccounts();
            document.getElementById('cfgSyncMode').addEventListener('change', updateJourneyMap);
            document.getElementById('cfgReceiptSyncMode').addEventListener('change', updateJourneyMap);
            document.getElementById('cfgVoucherSyncMode').addEventListener('change', updateJourneyMap);
            document.getElementById('cfgPayrollSyncMode').addEventListener('change', updateJourneyMap);
        });

        function preloadNexaaccAccounts() {
            fetch(pageUrl + '?action=nexaaccAccounts&_=' + Date.now())
                .then(function(r) { return r.json(); })
                .then(function(data) {
                    if (data.success && data.items) {
                        nexaaccAccountsCache = data.items;
                        var syncEl = document.getElementById('accountSyncStatus');
                        if (syncEl) syncEl.textContent = 'ผังบัญชี ' + data.total + ' รายการ (sync ล่าสุด: ' + (data.lastSync || '-') + ')';
                    }
                })
                .catch(function() {});
        }

        function updateJourneyMap() {
            var globalMode = document.getElementById('cfgSyncMode').value;
            var receiptMode = document.getElementById('cfgReceiptSyncMode').value || globalMode;
            var voucherMode = document.getElementById('cfgVoucherSyncMode').value || globalMode;
            var isReceiptDoc = receiptMode === 'DOCUMENT';
            var isReceiptLocal = receiptMode === 'LOCAL';
            var isVoucherDoc = voucherMode === 'DOCUMENT';
            var isVoucherLocal = voucherMode === 'LOCAL';
            var jn = {
                jnDeposit: isReceiptLocal
                    ? 'จัดการโดย TakeTime<br/>(ไม่ส่ง NextAcc)'
                    : isReceiptDoc
                        ? 'สร้างใบกำกับภาษี<br/>(รับมัดจำ) + Journal'
                        : 'DR เงินสด/ธนาคาร<br/>CR เงินรับล่วงหน้า',
                jnPayment: isReceiptLocal
                    ? 'จัดการโดย TakeTime<br/>(ไม่ส่ง NextAcc)'
                    : isReceiptDoc
                        ? 'สร้างใบกำกับภาษี<br/>(รับชำระ) + Journal'
                        : 'DR เงินสด/ธนาคาร<br/>CR รายได้ห้องพัก',
                jnCheckout: isReceiptLocal
                    ? 'จัดการโดย TakeTime<br/>(ไม่ส่ง NextAcc)'
                    : isReceiptDoc
                        ? 'สร้างใบกำกับภาษี<br/>(รับรู้รายได้) + Journal'
                        : 'DR เงินรับล่วงหน้า<br/>CR รายได้ห้องพัก',
                jnVoucher: isVoucherLocal
                    ? 'จัดการโดย TakeTime<br/>(ไม่ส่ง NextAcc)'
                    : isVoucherDoc
                        ? 'สร้างใบสำคัญจ่าย<br/>(ค่าใช้จ่าย) + Journal'
                        : 'DR ค่าใช้จ่าย<br/>CR เงินสด/ธนาคาร'
            };
            for (var id in jn) {
                var el = document.getElementById(id);
                if (el) el.innerHTML = jn[id];
            }
        }

        function loadConfigData() {
            var raw = document.getElementById('<%= hfConfigData.ClientID %>').value;
            if (!raw) return;
            try {
                var cfg = JSON.parse(raw);
                document.getElementById('cfgBaseUrl').value = cfg.baseUrl || '';
                document.getElementById('cfgCompanyId').value = cfg.companyId || '';
                document.getElementById('cfgEnabled').value = cfg.enabled ? 'true' : 'false';
                document.getElementById('cfgSyncMode').value = cfg.syncMode || 'DOCUMENT';
                document.getElementById('cfgSyncInterval').value = cfg.syncInterval || 30;
                document.getElementById('cfgMaxRetries').value = cfg.maxRetries || 5;
                document.getElementById('cfgTimeout').value = cfg.timeout || 30;
                if (cfg.receiptSyncMode) document.getElementById('cfgReceiptSyncMode').value = cfg.receiptSyncMode;
                if (cfg.voucherSyncMode) document.getElementById('cfgVoucherSyncMode').value = cfg.voucherSyncMode;
                if (cfg.payrollSyncMode) document.getElementById('cfgPayrollSyncMode').value = cfg.payrollSyncMode;
                document.getElementById('cfgAttachFiles').value = cfg.attachFiles ? 'true' : 'false';
                if (cfg.depositVatRecognition) document.getElementById('cfgDepositVatRecognition').value = cfg.depositVatRecognition;
                document.getElementById('cfgDepositDeferOutputVat').checked = !!cfg.depositDeferOutputVat;
                document.getElementById('cfgEtaxAutoGenerate').value = cfg.etaxAutoGenerate ? 'true' : 'false';
                document.getElementById('cfgEtaxAutoSign').value = cfg.etaxAutoSign ? 'true' : 'false';
                document.getElementById('cfgEtaxAutoSubmit').value = cfg.etaxAutoSubmit ? 'true' : 'false';
                document.getElementById('cfgEtaxAutoSendEmail').value = cfg.etaxAutoSendEmail ? 'true' : 'false';
                document.getElementById('cfgEtaxEmailSubject').value = cfg.etaxEmailSubject || '';
                document.getElementById('cfgEtaxEmailBody').value = cfg.etaxEmailBody || '';
                document.getElementById('cfgEtaxEmailAttachPdf').value = cfg.etaxEmailAttachPdf ? 'true' : 'false';
                document.getElementById('cfgEtaxEmailAttachXml').value = cfg.etaxEmailAttachXml ? 'true' : 'false';
                document.getElementById('cfgEtaxEmailLocalOnly').value = cfg.etaxEmailLocalOnly ? 'true' : 'false';
                document.getElementById('cfgEtaxEmailFallback').value = cfg.etaxEmailFallback ? 'true' : 'false';
                if (cfg.hasApiKey) {
                    document.getElementById('cfgApiKey').placeholder = '••••••••  (มี API Key อยู่แล้ว — ใส่ค่าใหม่เพื่อเปลี่ยน)';
                }
                updateJourneyMap();
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
                syncMode: document.getElementById('cfgSyncMode').value,
                syncInterval: document.getElementById('cfgSyncInterval').value,
                maxRetries: document.getElementById('cfgMaxRetries').value,
                timeout: document.getElementById('cfgTimeout').value,
                receiptSyncMode: document.getElementById('cfgReceiptSyncMode').value,
                voucherSyncMode: document.getElementById('cfgVoucherSyncMode').value,
                payrollSyncMode: document.getElementById('cfgPayrollSyncMode').value,
                attachFiles: document.getElementById('cfgAttachFiles').value,
                depositVatRecognition: document.getElementById('cfgDepositVatRecognition').value,
                depositDeferOutputVat: document.getElementById('cfgDepositDeferOutputVat').checked,
                etaxAutoGenerate: document.getElementById('cfgEtaxAutoGenerate').value,
                etaxAutoSign: document.getElementById('cfgEtaxAutoSign').value,
                etaxAutoSubmit: document.getElementById('cfgEtaxAutoSubmit').value,
                etaxAutoSendEmail: document.getElementById('cfgEtaxAutoSendEmail').value,
                etaxEmailSubject: document.getElementById('cfgEtaxEmailSubject').value,
                etaxEmailBody: document.getElementById('cfgEtaxEmailBody').value,
                etaxEmailAttachPdf: document.getElementById('cfgEtaxEmailAttachPdf').value,
                etaxEmailAttachXml: document.getElementById('cfgEtaxEmailAttachXml').value,
                etaxEmailLocalOnly: document.getElementById('cfgEtaxEmailLocalOnly').value,
                etaxEmailFallback: document.getElementById('cfgEtaxEmailFallback').value
            };
            postAction(data, 'syncTestResult');
        }

        function testApi() {
            getAction('testApi', 'apiTestResult');
        }

        function syncAccounts() {
            var el = document.getElementById('apiTestResult');
            el.className = 'test-result loading';
            el.textContent = 'กำลัง Sync ผังบัญชี...';

            var controller = new AbortController();
            var timeoutId = setTimeout(function() { controller.abort(); }, 60000);

            fetch(pageUrl + '?action=syncAccounts&_=' + Date.now(), { signal: controller.signal })
                .then(function(r) { clearTimeout(timeoutId); return r.json(); })
                .then(function(data) {
                    el.className = 'test-result ' + (data.success ? 'success' : 'error');
                    el.innerHTML = (data.success ? '<i class="fas fa-check-circle"></i> ' : '<i class="fas fa-times-circle"></i> ') + data.message;
                    if (data.success) {
                        nexaaccAccountsCache = [];
                        preloadNexaaccAccounts();
                    }
                })
                .catch(function(err) {
                    clearTimeout(timeoutId);
                    el.className = 'test-result error';
                    var msg = err.name === 'AbortError' ? 'หมดเวลา — เซิร์ฟเวอร์ไม่ตอบกลับภายใน 60 วินาที' : err.message;
                    el.innerHTML = '<i class="fas fa-times-circle"></i> ' + msg;
                });
        }

        function processQueue() {
            getAction('processQueue', 'syncTestResult');
        }

        // ── Deposit Lifecycle ──

        function lookupDepositStatus() {
            var q = (document.getElementById('depositLookup').value || '').trim();
            var status = document.getElementById('depositStatusFilter').value;
            var el = document.getElementById('depositStatusResult');
            el.innerHTML = '<div class="test-result loading">กำลังค้นหา...</div>';
            var url = pageUrl + '?action=depositStatus&q=' + encodeURIComponent(q) + '&status=' + encodeURIComponent(status) + '&_=' + Date.now();
            fetch(url)
                .then(function(r) { return r.json(); })
                .then(function(data) {
                    if (!data.success) {
                        el.innerHTML = '<div class="test-result error"><i class="fas fa-times-circle"></i> ' + data.message + '</div>';
                        return;
                    }
                    if (!data.items || data.items.length === 0) {
                        el.innerHTML = '<div class="test-result error"><i class="fas fa-info-circle"></i> ไม่พบข้อมูล</div>';
                        return;
                    }
                    var html = '<table style="width:100%; border-collapse:collapse; font-size:13px;">';
                    html += '<thead><tr style="background:#f5f5f5;">'
                          + '<th style="padding:8px; text-align:left;">Reservation</th>'
                          + '<th style="padding:8px;">ลูกค้า</th>'
                          + '<th style="padding:8px;">เช็คเอาท์</th>'
                          + '<th style="padding:8px;">มัดจำจ่าย</th>'
                          + '<th style="padding:8px;">ตัดแล้ว</th>'
                          + '<th style="padding:8px;">คงเหลือ</th>'
                          + '<th style="padding:8px;">สถานะ</th>'
                          + '<th style="padding:8px;">การกระทำล่าสุด</th>'
                          + '</tr></thead><tbody>';
                    data.items.forEach(function(it) {
                        var color = it.depositStatus === 'CLEARED' ? '#28a745'
                                  : it.depositStatus === 'OPEN' ? '#dc3545'
                                  : it.depositStatus === 'PARTIAL' ? '#ffc107'
                                  : it.depositStatus === 'OVER_CLEARED' ? '#dc3545'
                                  : '#6c757d';
                        html += '<tr style="border-bottom:1px solid #eee;">'
                              + '<td style="padding:8px; font-family:monospace;">#' + it.reservationId + '<br/><small>' + (it.reservationStatus || '-') + '</small></td>'
                              + '<td style="padding:8px;">' + (it.customerName || '-') + '<br/><small style="color:#999;">' + (it.customerMobilePhone || '') + '</small></td>'
                              + '<td style="padding:8px;">' + (it.checkoutDate ? new Date(it.checkoutDate).toLocaleDateString('th-TH') : '-') + '</td>'
                              + '<td style="padding:8px; text-align:right;">' + Number(it.depositPaid).toLocaleString('th-TH', {minimumFractionDigits:2}) + '</td>'
                              + '<td style="padding:8px; text-align:right;">' + Number(it.depositCleared).toLocaleString('th-TH', {minimumFractionDigits:2}) + '</td>'
                              + '<td style="padding:8px; text-align:right; font-weight:600;">' + Number(it.depositOutstanding).toLocaleString('th-TH', {minimumFractionDigits:2}) + '</td>'
                              + '<td style="padding:8px;"><span style="background:' + color + '; color:white; padding:2px 8px; border-radius:3px; font-size:11px;">' + it.depositStatus + '</span></td>'
                              + '<td style="padding:8px;">' + (it.lastClearAction || '-') + (it.lastClearDate ? '<br/><small>' + new Date(it.lastClearDate).toLocaleString('th-TH') + '</small>' : '') + '</td>'
                              + '</tr>';
                    });
                    html += '</tbody></table>';
                    el.innerHTML = html;
                })
                .catch(function(err) {
                    el.innerHTML = '<div class="test-result error"><i class="fas fa-times-circle"></i> ' + err.message + '</div>';
                });
        }

        function manualDepositAction(action) {
            var resId = document.getElementById('depositManualResId').value;
            var amount = document.getElementById('depositManualAmount').value;
            var reason = document.getElementById('depositManualReason').value;
            if (!resId || !amount) {
                var el = document.getElementById('depositManualResult');
                el.className = 'test-result error';
                el.innerHTML = '<i class="fas fa-times-circle"></i> กรุณาใส่ Reservation ID และจำนวนเงิน';
                return;
            }
            postAction({
                action: 'depositManual',
                operation: action,
                reservationId: resId,
                amount: amount,
                reason: reason
            }, 'depositManualResult');
        }

        // ── Document Source Lookup ──

        function lookupDocumentSource() {
            var key = (document.getElementById('docSourceLookup').value || '').trim();
            var el = document.getElementById('docSourceResult');
            if (!key) {
                el.innerHTML = '<div class="test-result error"><i class="fas fa-times-circle"></i> กรุณาระบุเลขที่ใบเสร็จหรือ Reservation ID</div>';
                return;
            }
            el.innerHTML = '<div class="test-result loading">กำลังค้นหา...</div>';
            fetch(pageUrl + '?action=lookupDocSource&q=' + encodeURIComponent(key) + '&_=' + Date.now())
                .then(function(r) { return r.json(); })
                .then(function(data) {
                    if (!data.success) {
                        el.innerHTML = '<div class="test-result error"><i class="fas fa-times-circle"></i> ' + data.message + '</div>';
                        return;
                    }
                    if (!data.items || data.items.length === 0) {
                        el.innerHTML = '<div class="test-result error"><i class="fas fa-info-circle"></i> ไม่พบข้อมูล</div>';
                        return;
                    }
                    var html = '<table style="width:100%; border-collapse:collapse; font-size:13px;">';
                    html += '<thead><tr style="background:#f5f5f5;">'
                          + '<th style="padding:8px; text-align:left;">เลขใบเสร็จ</th>'
                          + '<th style="padding:8px;">Reservation</th>'
                          + '<th style="padding:8px;">ยอด</th>'
                          + '<th style="padding:8px;">Source</th>'
                          + '<th style="padding:8px;">NextAcc Doc</th>'
                          + '<th style="padding:8px;">Sync</th>'
                          + '<th style="padding:8px;">E-Tax</th>'
                          + '<th style="padding:8px;">ลูกค้า</th>'
                          + '<th style="padding:8px;">PDF</th>'
                          + '</tr></thead><tbody>';
                    data.items.forEach(function(it) {
                        var sourceColor = it.documentSource === 'NEXAACC' ? '#28a745' : '#6c757d';
                        var syncColor = it.syncStatus === 'COMPLETED' ? '#28a745' : (it.syncStatus === 'FAILED' ? '#dc3545' : '#ffc107');
                        var etaxColor = it.etaxStatus ? (it.etaxStatus === 'FAILED' ? '#dc3545' : '#28a745') : '#999';
                        html += '<tr style="border-bottom:1px solid #eee;">'
                              + '<td style="padding:8px; font-family:monospace;">' + (it.receiptNumber || '-') + '</td>'
                              + '<td style="padding:8px;">' + (it.reservationId || '-') + '</td>'
                              + '<td style="padding:8px; text-align:right;">' + (it.total ? Number(it.total).toLocaleString('th-TH', {minimumFractionDigits:2}) : '-') + '</td>'
                              + '<td style="padding:8px;"><span style="background:' + sourceColor + '; color:white; padding:2px 8px; border-radius:3px; font-size:11px;">' + it.documentSource + '</span></td>'
                              + '<td style="padding:8px; font-family:monospace; font-size:11px;">' + (it.nexaaccDocId ? (it.nexaaccUrl ? '<a href="' + it.nexaaccUrl + '" target="_blank" class="nexaacc-doc-link">' + it.nexaaccDocId.substring(0,12) + '... <i class="fas fa-external-link-alt"></i></a>' : it.nexaaccDocId.substring(0,12) + '...') : '-') + '</td>'
                              + '<td style="padding:8px;"><span style="color:' + syncColor + ';">' + (it.syncStatus || '-') + '</span></td>'
                              + '<td style="padding:8px;">' + (it.etaxStatus ? '<span style="color:' + etaxColor + ';">' + it.etaxStatus + (it.etaxRefNumber ? ' (' + it.etaxRefNumber + ')' : '') + '</span>' : '-') + (it.etaxEmailSent ? ' ✉' : '') + '</td>'
                              + '<td style="padding:8px;">' + (it.customerName || '-') + (it.customerEmail ? '<br/><small style="color:#999;">' + it.customerEmail + '</small>' : '') + '</td>'
                              + '<td style="padding:8px;">' + (it.etaxPdfUrl ? '<a href="' + it.etaxPdfUrl + '" target="_blank">PDF</a>' : '-') + (it.etaxXmlUrl ? ' / <a href="' + it.etaxXmlUrl + '" target="_blank">XML</a>' : '') + '</td>'
                              + '</tr>';
                    });
                    html += '</tbody></table>';
                    el.innerHTML = html;
                })
                .catch(function(err) {
                    el.innerHTML = '<div class="test-result error"><i class="fas fa-times-circle"></i> ' + err.message + '</div>';
                });
        }

        // ── Stock Adjustment / Write-off / Product Sync ──

        function stockAdjustment(adjustType) {
            var productId = document.getElementById('stockProductId').value;
            var qty = document.getElementById('stockQuantity').value;
            var cost = document.getElementById('stockCostPerUnit').value;
            var reason = document.getElementById('stockReason').value;
            var el = document.getElementById('stockResult');
            if (!productId || !qty || !cost) {
                el.className = 'test-result error';
                el.innerHTML = '<i class="fas fa-times-circle"></i> กรุณากรอก Product ID, Quantity, Cost ให้ครบ';
                return;
            }
            postAction({
                action: 'stockAdjustment',
                adjustType: adjustType,
                productId: productId,
                quantity: qty,
                costPerUnit: cost,
                reason: reason
            }, 'stockResult');
        }

        function stockProductSync() {
            var productId = document.getElementById('stockProductId').value;
            var el = document.getElementById('stockResult');
            if (!productId) {
                el.className = 'test-result error';
                el.innerHTML = '<i class="fas fa-times-circle"></i> กรุณาระบุ Product ID';
                return;
            }
            postAction({ action: 'stockProductSync', productId: productId }, 'stockResult');
        }

        // ── E-Tax Manual Operations ──

        function manualEtaxGenerate() {
            var receiptNumber = (document.getElementById('etaxReceiptNumber').value || '').trim();
            if (!receiptNumber) {
                var el = document.getElementById('etaxTestResult');
                el.className = 'test-result error';
                el.innerHTML = '<i class="fas fa-times-circle"></i> กรุณาระบุเลขที่ใบเสร็จ';
                return;
            }
            postAction({ action: 'etaxGenerate', receiptNumber: receiptNumber }, 'etaxTestResult');
        }

        function manualEtaxSendEmail() {
            var receiptNumber = (document.getElementById('etaxReceiptNumber').value || '').trim();
            var email = (document.getElementById('etaxOverrideEmail').value || '').trim();
            if (!receiptNumber) {
                var el = document.getElementById('etaxTestResult');
                el.className = 'test-result error';
                el.innerHTML = '<i class="fas fa-times-circle"></i> กรุณาระบุเลขที่ใบเสร็จ';
                return;
            }
            postAction({ action: 'etaxSendEmail', receiptNumber: receiptNumber, email: email }, 'etaxTestResult');
        }

        // ── Queue Pagination & Filter ──

        function filterByStatus(status) {
            queueState.status = status;
            queueState.page = 1;
            updateFilterUI();
            loadQueueData();
        }

        function changePageSize() {
            queueState.pageSize = parseInt(document.getElementById('queuePageSize').value) || 20;
            queueState.page = 1;
            loadQueueData();
        }

        function goToPage(p) {
            if (p < 1 || p > queueState.totalPages) return;
            queueState.page = p;
            loadQueueData();
        }

        function updateFilterUI() {
            var cards = {
                '': 'qsAll', 'PENDING': 'qsPendingCard', 'PROCESSING': 'qsProcessingCard',
                'COMPLETED': 'qsCompletedCard', 'FAILED': 'qsFailedCard'
            };
            for (var key in cards) {
                var el = document.getElementById(cards[key]);
                if (el) el.className = 'queue-stat' + (queueState.status === key ? ' active' : '');
            }
        }

        function loadQueueData() {
            var url = pageUrl + '?action=queueData&page=' + queueState.page
                + '&pageSize=' + queueState.pageSize
                + (queueState.status ? '&status=' + queueState.status : '')
                + '&_=' + Date.now();

            fetch(url)
                .then(function(r) { return r.json(); })
                .then(function(data) {
                    var total = (data.pending || 0) + (data.processing || 0) + (data.completed || 0) + (data.failed || 0);
                    document.getElementById('qsTotal').textContent = total;
                    document.getElementById('qsPending').textContent = data.pending || 0;
                    document.getElementById('qsProcessing').textContent = data.processing || 0;
                    document.getElementById('qsCompleted').textContent = data.completed || 0;
                    document.getElementById('qsFailed').textContent = data.failed || 0;

                    queueState.page = data.page || 1;
                    queueState.totalPages = data.totalPages || 1;

                    renderQueue(data.items || []);
                    renderPagination(data.page, data.totalPages, data.totalItems);
                    updateFilterUI();
                })
                .catch(function(err) { console.error(err); });
        }

        function renderQueue(items) {
            var tbody = document.getElementById('queueBody');
            var selectAll = document.getElementById('selectAllQueue');
            if (selectAll) selectAll.checked = false;
            updateSelectedCount();

            if (!items.length) {
                var msg = queueState.status ? 'ไม่มีรายการสถานะ ' + queueState.status : 'ไม่มีรายการใน Queue';
                tbody.innerHTML = '<tr><td colspan="10" style="text-align:center; color:#999;">' + msg + '</td></tr>';
                return;
            }
            var html = '';
            items.forEach(function(item) {
                var statusClass = item.status === 'COMPLETED' ? 'status-connected' :
                                  item.status === 'FAILED' ? 'status-error' :
                                  item.status === 'PROCESSING' ? 'status-testing' : 'status-not-configured';
                var rowStyle = item.sensitive ? ' style="background:#f9f3ff; opacity:0.85;"' : '';
                html += '<tr' + rowStyle + '>';
                html += '<td><input type="checkbox" class="queue-check" value="' + item.id + '" onchange="updateSelectedCount()" /></td>';
                html += '<td>' + item.id + '</td>';
                html += '<td>' + item.entityType + ' #' + item.entityId + '</td>';
                html += '<td>' + item.actionType + '</td>';
                html += '<td><span class="status-badge ' + statusClass + '">' + item.status + '</span></td>';
                // NextAcc Document column
                html += '<td>';
                if (item.nexaaccDocNumber) {
                    if (item.nexaaccUrl) {
                        html += '<a href="' + item.nexaaccUrl + '" target="_blank" class="nexaacc-doc-link" title="เปิดใน NextAcc">' + item.nexaaccDocNumber + ' <i class="fas fa-external-link-alt"></i></a>';
                    } else {
                        html += '<span class="nexaacc-doc-label">' + item.nexaaccDocNumber + '</span>';
                    }
                } else if (item.nexaaccId && item.status === 'COMPLETED') {
                    var shortId = item.nexaaccId.substring(0, 8) + '...';
                    if (item.nexaaccUrl) {
                        html += '<a href="' + item.nexaaccUrl + '" target="_blank" class="nexaacc-doc-link" title="' + item.nexaaccId + '">' + shortId + ' <i class="fas fa-external-link-alt"></i></a>';
                    } else {
                        html += '<span class="nexaacc-doc-label" title="' + item.nexaaccId + '">' + shortId + '</span>';
                    }
                } else {
                    html += '-';
                }
                html += '</td>';
                html += '<td>' + item.retryCount + '/' + item.maxRetries + '</td>';
                html += '<td style="max-width:200px; overflow:hidden; text-overflow:ellipsis;" title="' + (item.error || '').replace(/"/g, '&quot;') + '">' + (item.error || '-') + '</td>';
                html += '<td>' + item.created + '</td>';
                html += '<td>';
                if (item.sensitive) {
                    html += '<span style="font-size:11px; color:#999;">🔒</span>';
                } else {
                    if (item.status === 'FAILED') {
                        html += '<button type="button" class="btn-primary" style="padding:4px 10px; font-size:11px;" onclick="retryItem(' + item.id + ')" title="Retry"><i class="fas fa-redo"></i></button> ';
                    }
                    if (item.status === 'COMPLETED' || item.status === 'FAILED') {
                        html += '<button type="button" class="btn-warning" style="padding:4px 10px; font-size:11px;" onclick="resyncItem(' + item.id + ')" title="ยิง API ใหม่ (ลบผลเดิม)"><i class="fas fa-sync-alt"></i></button>';
                    }
                }
                html += '</td>';
                html += '</tr>';
            });
            tbody.innerHTML = html;
        }

        function toggleSelectAll(el) {
            var checks = document.querySelectorAll('.queue-check');
            checks.forEach(function(cb) { cb.checked = el.checked; });
            updateSelectedCount();
        }

        function updateSelectedCount() {
            var checks = document.querySelectorAll('.queue-check:checked');
            var count = checks.length;
            document.getElementById('selectedCount').textContent = count;
            document.getElementById('btnDeleteSelected').style.display = count > 0 ? 'inline-block' : 'none';
        }

        function getSelectedIds() {
            var ids = [];
            document.querySelectorAll('.queue-check:checked').forEach(function(cb) { ids.push(parseInt(cb.value)); });
            return ids;
        }

        function deleteSelected() {
            var ids = getSelectedIds();
            if (!ids.length) return;
            if (!confirm('ยืนยันลบ ' + ids.length + ' รายการที่เลือก?\n(จะลบออกจาก Queue ถาวร)')) return;

            fetch(pageUrl + '?action=deleteQueueItems', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ ids: ids })
            })
            .then(function(r) { return r.json(); })
            .then(function(data) {
                alert(data.message);
                if (data.success) loadQueueData();
            })
            .catch(function(err) { alert(err.message); });
        }

        function renderPagination(currentPage, totalPages, totalItems) {
            var el = document.getElementById('queuePagination');
            if (totalPages <= 1) {
                el.innerHTML = '<span class="page-info">' + totalItems + ' รายการ</span>';
                return;
            }

            var html = '';
            html += '<button type="button" onclick="goToPage(1)" ' + (currentPage <= 1 ? 'disabled' : '') + '><i class="fas fa-angle-double-left"></i></button>';
            html += '<button type="button" onclick="goToPage(' + (currentPage - 1) + ')" ' + (currentPage <= 1 ? 'disabled' : '') + '><i class="fas fa-angle-left"></i></button>';

            // Show page numbers with ellipsis
            var startPage = Math.max(1, currentPage - 2);
            var endPage = Math.min(totalPages, currentPage + 2);
            if (startPage > 1) {
                html += '<button type="button" onclick="goToPage(1)">1</button>';
                if (startPage > 2) html += '<span class="page-info">...</span>';
            }
            for (var i = startPage; i <= endPage; i++) {
                html += '<button type="button" onclick="goToPage(' + i + ')" class="' + (i === currentPage ? 'active' : '') + '">' + i + '</button>';
            }
            if (endPage < totalPages) {
                if (endPage < totalPages - 1) html += '<span class="page-info">...</span>';
                html += '<button type="button" onclick="goToPage(' + totalPages + ')">' + totalPages + '</button>';
            }

            html += '<button type="button" onclick="goToPage(' + (currentPage + 1) + ')" ' + (currentPage >= totalPages ? 'disabled' : '') + '><i class="fas fa-angle-right"></i></button>';
            html += '<button type="button" onclick="goToPage(' + totalPages + ')" ' + (currentPage >= totalPages ? 'disabled' : '') + '><i class="fas fa-angle-double-right"></i></button>';
            html += '<span class="page-info">หน้า ' + currentPage + '/' + totalPages + ' (' + totalItems + ' รายการ)</span>';

            el.innerHTML = html;
        }

        function retryItem(queueId) {
            fetch(pageUrl + '?action=retryItem&queueId=' + queueId + '&_=' + Date.now())
                .then(function(r) { return r.json(); })
                .then(function(data) { loadQueueData(); })
                .catch(function(err) { alert(err.message); });
        }

        function resyncItem(queueId) {
            if (!confirm('ยืนยันยิง API ใหม่สำหรับ Queue #' + queueId + '?\n(จะลบผลเดิมและสร้างใหม่บน NextAcc)'))
                return;
            fetch(pageUrl + '?action=resyncItem&queueId=' + queueId + '&_=' + Date.now())
                .then(function(r) { return r.json(); })
                .then(function(data) {
                    alert(data.message);
                    loadQueueData();
                })
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

            var controller = new AbortController();
            var timeoutId = setTimeout(function() { controller.abort(); }, 60000);

            fetch(pageUrl + '?action=' + action + '&_=' + Date.now(), { signal: controller.signal })
                .then(function(r) { clearTimeout(timeoutId); return r.json(); })
                .then(function(data) {
                    el.className = 'test-result ' + (data.success ? 'success' : 'error');
                    el.innerHTML = (data.success ? '<i class="fas fa-check-circle"></i> ' : '<i class="fas fa-times-circle"></i> ') + data.message;
                })
                .catch(function(err) {
                    clearTimeout(timeoutId);
                    el.className = 'test-result error';
                    var msg = err.name === 'AbortError' ? 'หมดเวลา — เซิร์ฟเวอร์ไม่ตอบกลับภายใน 60 วินาที' : err.message;
                    el.innerHTML = '<i class="fas fa-times-circle"></i> ' + msg;
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
        // ── Account Sync & Mapping ──

        var nexaaccAccountsCache = [];
        var allMappings = [];

        function loadMappingsWithAccounts() {
            // Load cached accounts first, then mappings
            fetch(pageUrl + '?action=nexaaccAccounts&_=' + Date.now())
                .then(function(r) { return r.json(); })
                .then(function(data) {
                    if (data.success) {
                        nexaaccAccountsCache = data.items || [];
                        var syncEl = document.getElementById('accountSyncStatus');
                        syncEl.textContent = 'ผังบัญชี ' + data.total + ' รายการ (sync ล่าสุด: ' + (data.lastSync || '-') + ')';
                    }
                    return fetch(pageUrl + '?action=mappings&_=' + Date.now());
                })
                .then(function(r) { return r.json(); })
                .then(function(data) {
                    if (!data.success) { alert(data.message); return; }
                    allMappings = data.items || [];
                    filterMappingsByType();
                })
                .catch(function(err) { console.error(err); });
        }

        function filterMappingsByType() {
            var filter = document.getElementById('mappingTypeFilter').value;
            var filtered = filter ? allMappings.filter(function(m) { return m.mappingType === filter; }) : allMappings;
            renderMappings(filtered);
        }

        function renderMappings(items) {
            var tbody = document.getElementById('mappingBody');
            if (!items.length) {
                tbody.innerHTML = '<tr><td colspan="6" style="text-align:center; color:#999;">ไม่พบข้อมูล Mapping</td></tr>';
                return;
            }
            var html = '';
            items.forEach(function(item) {
                var hasId = item.accountId && item.accountId !== '';
                var statusBadge = hasId
                    ? '<span class="status-badge status-connected">Linked</span>'
                    : '<span class="status-badge status-error">Not Linked</span>';

                html += '<tr>';
                html += '<td><strong>' + item.code + '</strong></td>';
                html += '<td style="font-size:12px;">' + (item.description || '-') + '</td>';
                html += '<td>' + buildAccountSelect(item.id, item.accountCode, item.accountId) + '</td>';
                html += '<td><span style="font-size:11px; color:#666;">' + (item.mappingType || '') + '</span></td>';
                html += '<td>' + statusBadge + '</td>';
                html += '<td><button class="btn-primary" style="padding:4px 10px; font-size:11px;" onclick="updateMapping(' + item.id + ')"><i class="fas fa-save"></i></button></td>';
                html += '</tr>';
            });
            tbody.innerHTML = html;
        }

        function buildAccountSelect(mappingId, currentCode, currentAccountId) {
            if (nexaaccAccountsCache.length === 0) {
                // Fallback to text input if no cached accounts
                return '<input type="text" id="mapCode_' + mappingId + '" value="' + (currentCode || '') + '" '
                    + 'style="width:120px; padding:4px 6px; border:1px solid #ddd; border-radius:4px; font-size:12px;" />'
                    + '<input type="hidden" id="mapAccId_' + mappingId + '" value="" />'
                    + '<div style="font-size:10px; color:#999; margin-top:2px;">กด Sync บัญชี เพื่อแสดง dropdown</div>';
            }

            var selId = 'mapSelect_' + mappingId;
            var html = '<select id="' + selId + '" style="width:100%; max-width:320px; padding:5px 6px; border:1px solid #ddd; border-radius:4px; font-size:12px;">';
            html += '<option value="">-- เลือกบัญชี --</option>';

            var groups = {};
            nexaaccAccountsCache.forEach(function(acc) {
                var t = acc.type || 'Other';
                if (!groups[t]) groups[t] = [];
                groups[t].push(acc);
            });

            var typeOrder = ['Asset', 'Liability', 'Equity', 'Revenue', 'Expense'];
            typeOrder.forEach(function(typeName) {
                var accs = groups[typeName];
                if (!accs) return;
                html += '<optgroup label="' + typeName + '">';
                accs.forEach(function(acc) {
                    var indent = '';
                    for (var i = 0; i < (acc.level || 0); i++) indent += '&nbsp;&nbsp;';
                    var selected = '';
                    if (currentAccountId && acc.id === currentAccountId) selected = ' selected';
                    else if (!currentAccountId && currentCode && acc.code === currentCode) selected = ' selected';
                    html += '<option value="' + acc.id + '|' + acc.code + '"' + selected + '>'
                        + indent + acc.code + ' - ' + (acc.name || acc.nameEn || '') + '</option>';
                });
                html += '</optgroup>';
            });

            // Any remaining types not in typeOrder
            for (var t in groups) {
                if (typeOrder.indexOf(t) === -1) {
                    var accs = groups[t];
                    html += '<optgroup label="' + t + '">';
                    accs.forEach(function(acc) {
                        var selected = '';
                        if (currentAccountId && acc.id === currentAccountId) selected = ' selected';
                        else if (!currentAccountId && currentCode && acc.code === currentCode) selected = ' selected';
                        html += '<option value="' + acc.id + '|' + acc.code + '"' + selected + '>'
                            + acc.code + ' - ' + (acc.name || acc.nameEn || '') + '</option>';
                    });
                    html += '</optgroup>';
                }
            }

            html += '</select>';
            return html;
        }

        // ═══════════ Payment Method → Account Mapping ═══════════

        function ensureAccountsLoaded(callback) {
            if (nexaaccAccountsCache && nexaaccAccountsCache.length > 0) { callback(); return; }
            fetch(pageUrl + '?action=nexaaccAccounts&_=' + Date.now())
                .then(function(r) {
                    if (!r.ok) throw new Error('HTTP ' + r.status);
                    return r.json();
                })
                .then(function(data) {
                    if (data.success && data.items) {
                        nexaaccAccountsCache = data.items;
                    }
                    callback();
                })
                .catch(function(err) {
                    console.error('ensureAccountsLoaded error:', err);
                    callback();
                });
        }

        function loadPaidHowMapping() {
            var body = document.getElementById('paidHowBody');
            body.innerHTML = '<tr><td colspan="4" style="text-align:center;color:#999;">กำลังโหลด...</td></tr>';

            ensureAccountsLoaded(function() {
                fetch(pageUrl + '?action=getPaidHowMapping&_=' + Date.now())
                    .then(function(r) { return r.json(); })
                    .then(function(data) {
                        if (!data.success) { body.innerHTML = '<tr><td colspan="4" class="text-center" style="color:red;">' + data.message + '</td></tr>'; return; }
                        var items = data.items || [];
                        if (items.length === 0) { body.innerHTML = '<tr><td colspan="4" style="text-align:center;color:#999;">ไม่พบข้อมูลวิธีจ่ายเงิน</td></tr>'; return; }
                        var html = '';
                        items.forEach(function(item) {
                            var linked = item.accountId && item.accountId !== '' && item.accountId !== '00000000-0000-0000-0000-000000000000';
                            var badge = linked
                                ? '<span class="status-badge status-connected">' + (item.accountCode || '') + ' ✓</span>'
                                : '<span class="status-badge status-not-configured">ยังไม่ผูก</span>';
                            html += '<tr>'
                                + '<td>' + item.id + '</td>'
                                + '<td><strong>' + (item.name || '') + '</strong></td>'
                                + '<td>' + buildPaidHowSelect(item.id, item.accountCode, item.accountId) + '</td>'
                                + '<td>' + badge + '</td>'
                                + '</tr>';
                        });
                        body.innerHTML = html;
                    })
                    .catch(function(err) { body.innerHTML = '<tr><td colspan="4" style="color:red;">' + err.message + '</td></tr>'; });
            });
        }

        function buildPaidHowSelect(itemId, currentCode, currentAccountId) {
            if (!nexaaccAccountsCache || nexaaccAccountsCache.length === 0) {
                return '<span style="color:#e65100;font-size:12px;">กรุณากด "Sync บัญชี" ด้านบน แล้วรีเฟรชหน้านี้</span>';
            }
            var accs = nexaaccAccountsCache;
            var groups = {};
            accs.forEach(function(a) { var t = a.type || 'OTHER'; if (!groups[t]) groups[t] = []; groups[t].push(a); });
            var typeOrder = ['Asset', 'Liability'];
            var html = '<select id="phSelect_' + itemId + '" style="width:100%;padding:5px;border:1px solid #ddd;border-radius:4px;font-size:12px;" onchange="updatePaidHowAccount(' + itemId + ')">';
            html += '<option value="">-- เลือกบัญชี --</option>';
            typeOrder.forEach(function(t) {
                if (!groups[t]) return;
                html += '<optgroup label="' + t + '">';
                groups[t].forEach(function(acc) {
                    var sel = '';
                    if (currentAccountId && acc.id === currentAccountId) sel = ' selected';
                    else if (!currentAccountId && currentCode && acc.code === currentCode) sel = ' selected';
                    html += '<option value="' + acc.id + '|' + acc.code + '"' + sel + '>' + acc.code + ' - ' + (acc.name || acc.nameEn || '') + '</option>';
                });
                html += '</optgroup>';
            });
            for (var t in groups) {
                if (typeOrder.indexOf(t) === -1) {
                    html += '<optgroup label="' + t + '">';
                    groups[t].forEach(function(acc) {
                        var sel = '';
                        if (currentAccountId && acc.id === currentAccountId) sel = ' selected';
                        html += '<option value="' + acc.id + '|' + acc.code + '"' + sel + '>' + acc.code + ' - ' + (acc.name || acc.nameEn || '') + '</option>';
                    });
                    html += '</optgroup>';
                }
            }
            html += '</select>';
            return html;
        }

        function updatePaidHowAccount(itemId) {
            var sel = document.getElementById('phSelect_' + itemId);
            if (!sel || !sel.value) return;
            var parts = sel.value.split('|');
            var accountId = parts[0], accountCode = parts[1] || '';
            var el = document.getElementById('paidHowResult');
            el.className = 'test-result loading'; el.textContent = 'กำลังบันทึก...';

            fetch(pageUrl + '?action=updatePaidHowAccount&id=' + itemId
                + '&accountId=' + encodeURIComponent(accountId)
                + '&accountCode=' + encodeURIComponent(accountCode) + '&_=' + Date.now())
                .then(function(r) { return r.json(); })
                .then(function(data) {
                    el.className = 'test-result ' + (data.success ? 'success' : 'error');
                    el.innerHTML = (data.success ? '✓ ' : '✗ ') + data.message;
                    if (data.success) loadPaidHowMapping();
                })
                .catch(function(err) { el.className = 'test-result error'; el.innerHTML = '✗ ' + err.message; });
        }

        // ═══════════ Expense Category → Account Mapping ═══════════

        function loadPaidTypeMapping() {
            var body = document.getElementById('paidTypeBody');
            body.innerHTML = '<tr><td colspan="4" style="text-align:center;color:#999;">กำลังโหลด...</td></tr>';

            ensureAccountsLoaded(function() {
                fetch(pageUrl + '?action=getPaidTypeMapping&_=' + Date.now())
                    .then(function(r) { return r.json(); })
                    .then(function(data) {
                        if (!data.success) { body.innerHTML = '<tr><td colspan="4" style="color:red;">' + data.message + '</td></tr>'; return; }
                        var items = data.items || [];
                        if (items.length === 0) { body.innerHTML = '<tr><td colspan="4" style="text-align:center;color:#999;">ไม่พบข้อมูลประเภทค่าใช้จ่าย</td></tr>'; return; }
                        var html = '';
                        items.forEach(function(item) {
                            var linked = item.accountId && item.accountId !== '' && item.accountId !== '00000000-0000-0000-0000-000000000000';
                            var badge = linked
                                ? '<span class="status-badge status-connected">' + (item.accountCode || '') + ' ✓</span>'
                                : '<span class="status-badge status-not-configured">ยังไม่ผูก</span>';
                            html += '<tr>'
                                + '<td>' + item.id + '</td>'
                                + '<td><strong>' + (item.name || '') + '</strong></td>'
                                + '<td>' + buildPaidTypeSelect(item.id, item.accountCode, item.accountId) + '</td>'
                                + '<td>' + badge + '</td>'
                                + '</tr>';
                        });
                        body.innerHTML = html;
                    })
                    .catch(function(err) { body.innerHTML = '<tr><td colspan="4" style="color:red;">' + err.message + '</td></tr>'; });
            });
        }

        function buildPaidTypeSelect(itemId, currentCode, currentAccountId) {
            if (!nexaaccAccountsCache || nexaaccAccountsCache.length === 0) {
                return '<span style="color:#e65100;font-size:12px;">กรุณากด "Sync บัญชี" ด้านบน แล้วรีเฟรชหน้านี้</span>';
            }
            var accs = nexaaccAccountsCache;
            var groups = {};
            accs.forEach(function(a) { var t = a.type || 'OTHER'; if (!groups[t]) groups[t] = []; groups[t].push(a); });
            var typeOrder = ['Expense', 'Asset', 'Liability'];
            var html = '<select id="ptSelect_' + itemId + '" style="width:100%;padding:5px;border:1px solid #ddd;border-radius:4px;font-size:12px;" onchange="updatePaidTypeAccount(' + itemId + ')">';
            html += '<option value="">-- เลือกบัญชี --</option>';
            typeOrder.forEach(function(t) {
                if (!groups[t]) return;
                html += '<optgroup label="' + t + '">';
                groups[t].forEach(function(acc) {
                    var sel = '';
                    if (currentAccountId && acc.id === currentAccountId) sel = ' selected';
                    else if (!currentAccountId && currentCode && acc.code === currentCode) sel = ' selected';
                    html += '<option value="' + acc.id + '|' + acc.code + '"' + sel + '>' + acc.code + ' - ' + (acc.name || acc.nameEn || '') + '</option>';
                });
                html += '</optgroup>';
            });
            for (var t in groups) {
                if (typeOrder.indexOf(t) === -1) {
                    html += '<optgroup label="' + t + '">';
                    groups[t].forEach(function(acc) {
                        var sel = '';
                        if (currentAccountId && acc.id === currentAccountId) sel = ' selected';
                        html += '<option value="' + acc.id + '|' + acc.code + '"' + sel + '>' + acc.code + ' - ' + (acc.name || acc.nameEn || '') + '</option>';
                    });
                    html += '</optgroup>';
                }
            }
            html += '</select>';
            return html;
        }

        function updatePaidTypeAccount(itemId) {
            var sel = document.getElementById('ptSelect_' + itemId);
            if (!sel || !sel.value) return;
            var parts = sel.value.split('|');
            var accountId = parts[0], accountCode = parts[1] || '';
            var el = document.getElementById('paidTypeResult');
            el.className = 'test-result loading'; el.textContent = 'กำลังบันทึก...';

            fetch(pageUrl + '?action=updatePaidTypeAccount&id=' + itemId
                + '&accountId=' + encodeURIComponent(accountId)
                + '&accountCode=' + encodeURIComponent(accountCode) + '&_=' + Date.now())
                .then(function(r) { return r.json(); })
                .then(function(data) {
                    el.className = 'test-result ' + (data.success ? 'success' : 'error');
                    el.innerHTML = (data.success ? '✓ ' : '✗ ') + data.message;
                    if (data.success) loadPaidTypeMapping();
                })
                .catch(function(err) { el.className = 'test-result error'; el.innerHTML = '✗ ' + err.message; });
        }

        function updateMapping(id) {
            var sel = document.getElementById('mapSelect_' + id);
            var accountCode = '';
            var accountId = '';

            if (sel) {
                var val = sel.value;
                if (!val) { alert('กรุณาเลือกบัญชี'); return; }
                var parts = val.split('|');
                accountId = parts[0] || '';
                accountCode = parts[1] || '';
            } else {
                // Fallback: text input mode
                var inp = document.getElementById('mapCode_' + id);
                if (inp) accountCode = inp.value.trim();
                if (!accountCode) { alert('กรุณาใส่ Account Code'); return; }
            }

            var el = document.getElementById('mappingResult');
            el.className = 'test-result loading';
            el.textContent = 'กำลังอัปเดต...';

            var url = pageUrl + '?action=updateMapping&id=' + id
                + '&accountCode=' + encodeURIComponent(accountCode)
                + '&accountId=' + encodeURIComponent(accountId)
                + '&_=' + Date.now();

            fetch(url)
                .then(function(r) { return r.json(); })
                .then(function(data) {
                    el.className = 'test-result ' + (data.success ? 'success' : 'error');
                    el.innerHTML = (data.success ? '<i class="fas fa-check-circle"></i> ' : '<i class="fas fa-times-circle"></i> ') + data.message;
                    if (data.success) loadMappingsWithAccounts();
                })
                .catch(function(err) {
                    el.className = 'test-result error';
                    el.innerHTML = '<i class="fas fa-times-circle"></i> ' + err.message;
                });
        }
    </script>
</asp:Content>
