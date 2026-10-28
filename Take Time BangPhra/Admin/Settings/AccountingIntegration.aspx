<%@ Page Title="ตั้งค่าเชื่อมระบบบัญชี NextAcc" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="AccountingIntegration.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Settings.AccountingIntegration" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .acc-page { padding: 20px 0; }
        .page-header { margin-bottom: 30px; display: flex; justify-content: space-between; align-items: flex-start; flex-wrap: wrap; gap: 15px; }
        .page-header h1 { color: #5D4037; margin: 0 0 10px 0; }
        .page-header p { color: #999; margin: 0; }
        .page-header .header-actions { display: flex; gap: 10px; }

        .acc-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(380px, 1fr)); grid-template-columns: repeat(auto-fit, minmax(min(380px, 100%), 1fr)); gap: 20px; }
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
        .btn-secondary { padding: 10px 20px; background: #607D8B; color: white; border: none; border-radius: 8px; cursor: pointer; font-weight: 500; font-size: 13px; font-family: 'Prompt', sans-serif; }
        .btn-secondary:hover { background: #455A64; }
        .btn-danger { padding: 10px 20px; background: #E53935; color: white; border: none; border-radius: 8px; cursor: pointer; font-weight: 500; font-size: 13px; font-family: 'Prompt', sans-serif; }
        .btn-danger:hover { background: #C62828; }

        .test-result { margin-top: 10px; padding: 10px 15px; border-radius: 8px; font-size: 13px; display: none; white-space: pre-line; }
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

        /* ── ข้อ 26: แท็บ + ป้ายค่าแนะนำ + โซนอันตราย + วิซาร์ด + มือถือ ── */
        .acc-page { min-width: 0; }
        .acc-page .help-text { font-size: 11.5px; color: #8a8a8a; line-height: 1.6; }
        .acc-page .acc-grid { grid-template-columns: repeat(auto-fit, minmax(min(380px, 100%), 1fr)); }
        .acc-page .page-header .header-actions { flex-wrap: wrap; }
        .acc-tabs { position: sticky; top: 0; z-index: 40; display: flex; gap: 6px; overflow-x: auto; -webkit-overflow-scrolling: touch;
                    scrollbar-width: thin; padding: 8px 2px; margin: 0 0 16px; background: rgba(245,246,248,.97); border-bottom: 1px solid #e5e7eb; }
        .acc-tab { flex: none; display: inline-flex; align-items: center; gap: 6px; padding: 8px 14px; border-radius: 20px; border: 1px solid #e0e3e7;
                   background: #fff; color: #555; font-size: 13px; font-family: 'Prompt', sans-serif; cursor: pointer; white-space: nowrap; }
        .acc-tab:hover { border-color: #FF9800; color: #e65100; }
        .acc-tab[aria-selected="true"] { background: #5D4037; border-color: #5D4037; color: #fff; font-weight: 600; }
        .acc-tab .tab-count { background: #E53935; color: #fff; border-radius: 10px; padding: 0 7px; font-size: 11px; line-height: 18px; }
        .acc-tab .tab-count[hidden] { display: none; }
        .acc-panel[hidden] { display: none !important; }
        .acc-panel > .acc-card, .acc-panel > .journey-card, .acc-panel > .danger-zone, .acc-panel > .acc-grid { margin-bottom: 20px; }
        .acc-panel .journey-card { margin-top: 0; }
        .panel-intro { font-size: 13px; color: #666; margin: 0 0 14px; line-height: 1.7; }
        .acc-flash { outline: 3px solid #FFB300 !important; outline-offset: 2px; }
        .setup-banner { display: flex; flex-wrap: wrap; gap: 10px; align-items: center; justify-content: space-between; margin: 0 0 14px;
                        padding: 12px 16px; border-radius: 10px; background: #FFF8E1; border: 1px solid #FFE082; color: #6d4c00; font-size: 13.5px; }
        .setup-banner[hidden] { display: none; }
        .preset-box { background: #e8f0fe; border: 1px solid #a8c4f7; border-radius: 8px; padding: 12px; }
        .config-item.sub { margin-left: 18px; }
        .config-item label.chk { display: flex; gap: 8px; align-items: flex-start; font-weight: 500; }
        .config-item label.chk input { width: auto; margin-top: 3px; flex: none; }
        .config-item .pair { display: flex; gap: 8px; flex-wrap: wrap; }
        .config-item .pair select { flex: 1 1 160px; width: auto; }
        .config-item details.more { margin-top: 5px; font-size: 11.5px; color: #666; line-height: 1.7; }
        .config-item details.more > summary, details.more > summary { cursor: pointer; color: #6d4c41; font-size: 11.5px; }
        details.tech-block { margin: 6px 0 4px; }
        .rec-badge { display: inline-block; font-size: 11px; font-weight: 500; padding: 1px 8px; border-radius: 10px; background: #eef4fb; color: #1d4e79;
                     margin-left: 4px; vertical-align: middle; white-space: normal; }
        .rec-badge.match { background: #e8f6ee; color: #16653e; }
        .rec-badge.differs { background: #fff6e5; color: #8a5a00; }
        .rec-badge.neutral { background: #eceff1; color: #546e7a; }
        .rec-badge.exp { background: #fdecec; color: #a12626; }
        .key-hint { font-size: 12px; margin-top: 4px; }
        .key-hint.ok { color: #2E7D32; } .key-hint.warn { color: #C62828; }
        .je-preview { margin: 8px 0 12px; font-size: 12px; }
        .je-preview table { width: 100%; border-collapse: collapse; margin: 4px 0 8px; }
        .je-preview th, .je-preview td { padding: 3px 6px; border-bottom: 1px solid #eee; text-align: left; }
        .je-preview td.n { text-align: right; font-variant-numeric: tabular-nums; white-space: nowrap; }
        .je-preview .je-title { font-weight: 600; color: #4e342e; margin-top: 6px; }
        .je-preview .je-note { color: #8a5a00; background: #fff8e1; border-radius: 6px; padding: 6px 8px; }
        .scroll-x, #docSourceResult, #depositStatusResult, #grniResult, #emailRsvLog, #lineDailyLog, #lineDailyPreview, #healthResult, #pfResult, #rlResult
            { overflow-x: auto; -webkit-overflow-scrolling: touch; max-width: 100%; }
        .acc-page img { max-width: 100%; height: auto; }
        .acc-page pre { max-width: 100%; overflow-x: auto; }
        .muted { color: #999; text-align: center; }
        .log-filter { display: flex; gap: 8px; flex-wrap: wrap; align-items: center; }
        .log-filter select, .log-filter input { padding: 7px 10px; border: 1px solid #ddd; border-radius: 6px; font-size: 13px; font-family: 'Prompt', sans-serif; }
        .log-filter input { flex: 1 1 200px; min-width: 0; }
        .log-table { width: 100%; border-collapse: collapse; font-size: 12px; }
        .log-table td, .log-table th { padding: 5px 8px; border-bottom: 1px solid #f0f0f0; vertical-align: top; text-align: left; }
        .log-table td.d { white-space: pre-wrap; word-break: break-word; }
        .danger-zone { border: 2px solid #E53935; border-radius: 12px; background: #fffafa; }
        .danger-zone > summary { padding: 14px 18px; cursor: pointer; color: #B71C1C; font-weight: 600; font-size: 14.5px; }
        .danger-zone .dz-body { padding: 0 18px 18px; }
        .danger-zone .dz-warn { font-size: 12.5px; color: #B71C1C; background: #FFEBEE; border-radius: 8px; padding: 8px 10px; margin: 0 0 6px; }
        .dz-item { border-top: 1px dashed #f3b4b4; padding: 14px 0 6px; }
        .dz-title { font-weight: 600; font-size: 13.5px; color: #333; margin-bottom: 4px; }
        .dz-row { display: flex; gap: 8px; flex-wrap: wrap; align-items: center; margin: 8px 0; }
        .dz-row input[type=text], .dz-row input[type=number] { padding: 8px; border: 1px solid #ddd; border-radius: 6px; width: 220px; max-width: 100%; }
        .dz-row input.grow { flex: 1 1 200px; }
        .dz-grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 10px; margin: 8px 0; }
        .dz-grid label { font-weight: 600; font-size: 12.5px; display: block; }
        .dz-grid input { width: 100%; padding: 8px; border: 1px solid #ddd; border-radius: 6px; }
        /* วิซาร์ด */
        .wz-overlay { position: fixed; inset: 0; z-index: 10000; background: rgba(0,0,0,.45); display: flex; align-items: flex-start;
                      justify-content: center; overflow-y: auto; padding: 24px 12px; }
        .wz-overlay[hidden] { display: none; }
        .wz-box { background: #fff; width: 100%; max-width: 880px; border-radius: 14px; box-shadow: 0 12px 40px rgba(0,0,0,.3); display: flex; flex-direction: column; min-width: 0; }
        .wz-head { display: flex; align-items: center; justify-content: space-between; gap: 10px; padding: 14px 18px; border-bottom: 1px solid #eee; }
        .wz-head h2 { margin: 0; font-size: 18px; color: #4e342e; }
        .wz-x { border: none; background: none; font-size: 26px; line-height: 1; cursor: pointer; color: #888; }
        .wz-steps { list-style: none; margin: 0; padding: 10px 12px 0; display: flex; gap: 4px; overflow-x: auto; }
        .wz-steps li { flex: 1 1 0; min-width: 84px; }
        .wz-steps button { width: 100%; border: none; background: none; cursor: pointer; display: flex; flex-direction: column; align-items: center; gap: 3px;
                           font-family: 'Prompt', sans-serif; color: #888; font-size: 11.5px; padding: 4px 2px; }
        .wz-steps .n { width: 26px; height: 26px; border-radius: 50%; display: inline-flex; align-items: center; justify-content: center; background: #eceff1;
                       color: #607d8b; font-weight: 700; font-size: 12.5px; }
        .wz-steps li.done .n { background: #4CAF50; color: #fff; }
        .wz-steps li.done .n:after { content: ""; }
        .wz-steps li.cur .n { background: #5D4037; color: #fff; }
        .wz-steps li.cur .t { color: #4e342e; font-weight: 600; }
        .wz-progress { height: 4px; background: #eceff1; margin: 8px 18px 0; border-radius: 2px; overflow: hidden; }
        .wz-progress > div { height: 100%; width: 0; background: #4CAF50; transition: width .25s; }
        .wz-body { padding: 14px 18px; min-width: 0; }
        .wz-pane h3 { margin: 0 0 6px; font-size: 16px; color: #333; }
        .wz-lead { font-size: 13px; color: #555; line-height: 1.7; margin: 0 0 10px; }
        .wz-form { display: grid; grid-template-columns: 1fr; gap: 4px; max-width: 560px; }
        .wz-form label { font-size: 13px; font-weight: 500; color: #555; margin-top: 6px; }
        .wz-form input { padding: 8px 12px; border: 1px solid #ddd; border-radius: 6px; font-size: 13px; width: 100%; }
        .wz-stats, .wz-summary { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: 8px; margin: 8px 0; }
        .wz-stat { background: #f7f8fa; border-radius: 8px; padding: 10px; font-size: 12px; color: #666; }
        .wz-stat b { display: block; font-size: 18px; color: #333; }
        .wz-stat.ok b { color: #2E7D32; } .wz-stat.warn b { color: #E65100; } .wz-stat.err b { color: #C62828; }
        .wz-table select { width: 100%; max-width: 360px; padding: 5px; font-size: 12px; border: 1px solid #ddd; border-radius: 4px; }
        .wz-table .why { font-size: 11px; color: #1d4e79; margin-top: 2px; }
        .wz-table .need { font-size: 11px; color: #8a5a00; }
        .wz-table tr.changed { background: #FFFDE7; }
        .wz-policy { display: grid; grid-template-columns: 1fr; gap: 10px; }
        .wz-policy fieldset { border: 1px solid #e5e7eb; border-radius: 10px; padding: 10px 12px; margin: 0; min-width: 0; }
        .wz-policy legend { font-weight: 600; font-size: 13.5px; color: #4e342e; padding: 0 4px; }
        .wz-opt { display: flex; gap: 8px; align-items: flex-start; font-size: 13px; margin: 5px 0; cursor: pointer; }
        .wz-opt input { margin-top: 4px; flex: none; }
        .wz-opt em { color: #2E7D32; font-style: normal; font-size: 12px; }
        .wz-foot { display: flex; justify-content: space-between; align-items: center; gap: 8px; padding: 12px 18px; border-top: 1px solid #eee;
                   position: sticky; bottom: 0; background: #fff; border-radius: 0 0 14px 14px; }
        .wz-stepinfo { font-size: 12px; color: #777; text-align: center; }
        body.wz-open { overflow: hidden; }
        @media (max-width: 640px) {
            .acc-page { padding: 12px 0; }
            .acc-page .acc-grid { grid-template-columns: minmax(0, 1fr); }
            .acc-card, .journey-card { padding: 16px; }
            .page-header { margin-bottom: 14px; }
            .page-header h1 { font-size: 20px; }
            .acc-page [style*="min-width"] { min-width: 0 !important; }
            .acc-page [style*="display:flex"], .acc-page [style*="display: flex"] { flex-wrap: wrap; }
            .acc-page div[style*="grid-template-columns"] { grid-template-columns: minmax(0, 1fr) !important; }
            .acc-page input[style*="width:"], .acc-page select[style*="width:"] { max-width: 100%; }
            .acc-page .queue-toolbar input[type=search] { flex: 1 1 100%; }
            .queue-toolbar-left, .queue-toolbar-right { flex-wrap: wrap; }
            .config-item.sub { margin-left: 8px; }
            .dz-grid { grid-template-columns: minmax(0, 1fr); }
            .dz-row input[type=text], .dz-row input[type=number] { width: 100%; }
            .wz-overlay { padding: 0; }
            .wz-box { border-radius: 0; min-height: 100%; }
            .wz-steps .t { font-size: 10.5px; }
            .wz-foot { border-radius: 0; }
            .btn-primary, .btn-success, .btn-warning, .btn-secondary, .btn-danger { padding: 9px 14px; }
        }
    </style>

    <div class="acc-page">
        <div class="page-header">
            <div>
                <h1><i class="fas fa-calculator"></i> ตั้งค่าเชื่อมระบบบัญชี NextAcc</h1>
                <p>เชื่อมต่อ ผูกบัญชี/แหล่งเงิน เลือกนโยบาย และดูคิวส่งข้อมูล — แยกเป็นแท็บด้านล่าง</p>
            </div>
            <div class="header-actions">
                <button type="button" class="btn-success" id="btnOpenWizard" style="display:none;" onclick="wzOpen()"><i class="fas fa-magic"></i> ตั้งค่าทีละขั้น</button>
                <a href="<%= ResolveUrl("~/Admin/Settings/ConnectionSettings") %>" class="btn-primary" style="text-decoration:none; display:flex; align-items:center; gap:6px;">
                    <i class="fas fa-arrow-left"></i> กลับหน้าการเชื่อมต่อ
                </a>
            </div>
        </div>

        <!-- ข้อ 26: แถบเตือนเมื่อยังตั้งค่าไม่ครบ / วิซาร์ดยังไม่จบ (เติมโดย JS จากค่าที่โหลด) -->
        <div id="setupBanner" class="setup-banner" hidden></div>

        <!-- ข้อ 26: แท็บ (สลับฝั่งเบราว์เซอร์ + จำใน URL #t-ชื่อแท็บ; ลิงก์ ?focus= / ?cls= / ?status= / ?q= / #queueStats เปิดแท็บที่ถูกให้เอง) -->
        <div class="acc-tabs" role="tablist" aria-label="หมวดการตั้งค่า">
            <button type="button" role="tab" class="acc-tab" id="tabbtn-connect" data-tab="connect" aria-controls="tab-connect" aria-selected="false"><i class="fas fa-plug"></i> การเชื่อมต่อ</button>
            <button type="button" role="tab" class="acc-tab" id="tabbtn-accounts" data-tab="accounts" aria-controls="tab-accounts" aria-selected="false"><i class="fas fa-exchange-alt"></i> ผังบัญชี &amp; แหล่งเงิน</button>
            <button type="button" role="tab" class="acc-tab" id="tabbtn-policy" data-tab="policy" aria-controls="tab-policy" aria-selected="false"><i class="fas fa-sliders-h"></i> โหมดการส่ง &amp; นโยบาย</button>
            <button type="button" role="tab" class="acc-tab" id="tabbtn-queue" data-tab="queue" aria-controls="tab-queue" aria-selected="false"><i class="fas fa-tasks"></i> คิว &amp; ตรวจสุขภาพ <span class="tab-count" id="tabFailedCount" hidden></span></button>
            <button type="button" role="tab" class="acc-tab" id="tabbtn-tools" data-tab="tools" aria-controls="tab-tools" aria-selected="false"><i class="fas fa-toolbox"></i> เครื่องมือ</button>
            <button type="button" role="tab" class="acc-tab" id="tabbtn-intake" data-tab="intake" aria-controls="tab-intake" aria-selected="false"><i class="fas fa-envelope-open-text"></i> อีเมลจอง &amp; LINE</button>
            <button type="button" role="tab" class="acc-tab" id="tabbtn-log" data-tab="log" aria-controls="tab-log" aria-selected="false"><i class="fas fa-history"></i> บันทึก</button>
        </div>

        <!-- ═══════════════ แท็บ 1: การเชื่อมต่อ ═══════════════ -->
        <section class="acc-panel" id="tab-connect" role="tabpanel" aria-labelledby="tabbtn-connect">
        <div class="acc-grid">
            <!-- API Configuration -->
            <div class="acc-card" id="cardApi">
                <h3><i class="fas fa-key"></i> การเชื่อมต่อ NextAcc</h3>
                <div class="config-item">
                    <label for="cfgBaseUrl">ที่อยู่เซิร์ฟเวอร์ NextAcc</label>
                    <input type="text" id="cfgBaseUrl" placeholder="https://api.nexaacc.com" />
                    <div class="help-text">ได้จากผู้ดูแล NextAcc — ปกติไม่ต้องเปลี่ยน</div>
                </div>
                <div class="config-item">
                    <label for="cfgApiKey">คีย์เชื่อมระบบ <span style="color:#c0392b;">*จำเป็น</span></label>
                    <input type="password" id="cfgApiKey" placeholder="int_..." autocomplete="new-password" />
                    <div id="cfgApiKeyStatus" style="margin-top:4px; font-size:0.85em;"></div>
                    <div class="help-text">สร้างใน NextAcc เมนู <b>Integrations</b> (ขึ้นต้นด้วย <code>int_</code>) — ใช้ส่งเอกสารทุกชนิด คีย์เดียวก็ทำงานได้ครบ</div>
                    <div class="key-hint" id="cfgApiKeyHint"></div>
                </div>
                <div class="config-item">
                    <label for="cfgCompanyApiKey">คีย์บริษัท <span style="color:#7f8c8d;">— ไม่บังคับ</span></label>
                    <input type="password" id="cfgCompanyApiKey" placeholder="acc_... (เว้นว่าง = ใช้คีย์เชื่อมระบบตัวเดียว)" autocomplete="new-password" />
                    <div id="cfgCompanyApiKeyStatus" style="margin-top:4px; font-size:0.85em;"></div>
                    <div class="help-text">สร้างในเมนู <b>API Keys</b> (ขึ้นต้นด้วย <code>acc_</code>) — ใส่เมื่ออยากให้งานระดับบริษัท (สแกนเอกสาร, บังคับแหล่งเงิน, e-Tax, ผังบัญชี) ใช้คีย์ของตัวเอง</div>
                    <div class="key-hint" id="cfgCompanyApiKeyHint"></div>
                    <details class="more"><summary>รายละเอียดสำหรับผู้ดูแล</summary>
                        คีย์เชื่อมระบบ (<code>int_</code>) ส่งเป็น X-Integration-Key ไปที่ /api/integration/* และใช้แทนคีย์บริษัทได้ผ่าน X-Api-Key fallback.
                        คีย์บริษัท (<code>acc_</code>) ส่งเป็น X-Api-Key ไปที่ /api/companies/* เท่านั้น — ใช้แทนคีย์เชื่อมระบบ<b>ไม่ได้</b> (ทดสอบคีย์จะขึ้น 401).
                        ตั้งทั้งสองคีย์ = แต่ละเส้นใช้คีย์ชนิดของตัวเอง (ไม่พึ่ง fallback)
                    </details>
                </div>
                <div class="config-item">
                    <label for="cfgCompanyId">รหัสบริษัทใน NextAcc</label>
                    <input type="text" id="cfgCompanyId" placeholder="00000000-0000-0000-0000-000000000000" />
                    <div class="help-text">ดูได้ในหน้าข้อมูลบริษัทของ NextAcc — ต้องมีจึงจะใช้งานระดับบริษัทได้</div>
                </div>
                <div class="btn-row">
                    <button type="button" class="btn-success" onclick="saveConfig()"><i class="fas fa-save"></i> บันทึก</button>
                    <button type="button" class="btn-primary" onclick="testApi()"><i class="fas fa-plug"></i> ทดสอบคีย์</button>
                    <button type="button" class="btn-primary" onclick="syncAccounts()"><i class="fas fa-cloud-download-alt"></i> ดึงผังบัญชี</button>
                </div>
                <div class="test-result" id="apiTestResult"></div>
            </div>

            <!-- สวิตช์หลัก + ค่าทางเทคนิค (บันทึกพร้อมค่าการส่งทั้งหมด) -->
            <div class="acc-card" id="cardRun">
                <h3><i class="fas fa-power-off"></i> การส่งข้อมูลอัตโนมัติ</h3>
                <div class="config-item">
                    <label for="cfgEnabled">เปิดการส่งข้อมูลเข้า NextAcc <span class="rec-badge" data-rec-for="cfgEnabled" data-rec="true" data-rec-label="เปิด"></span></label>
                    <select id="cfgEnabled">
                        <option value="false">ปิด — ไม่ส่งอะไรเลย (คิวสะสมรอ)</option>
                        <option value="true">เปิด</option>
                    </select>
                    <div class="help-text">ปิด = เอกสารยังเข้าคิวตามปกติ แต่ไม่ถูกส่งจนกว่าจะเปิด</div>
                </div>
                <details class="more tech-block">
                    <summary>ค่าทางเทคนิค (ปกติไม่ต้องแก้)</summary>
                    <div class="config-item">
                        <label for="cfgSyncInterval">รอบตรวจคิว (วินาที)</label>
                        <input type="number" id="cfgSyncInterval" value="30" min="10" max="3600" />
                        <div class="help-text">ระยะห่างระหว่างรอบส่งคิว (10-3600 วินาที)</div>
                    </div>
                    <div class="config-item">
                        <label for="cfgMaxRetries">ลองใหม่อัตโนมัติสูงสุด (ครั้ง)</label>
                        <input type="number" id="cfgMaxRetries" value="5" min="1" max="20" />
                        <div class="help-text">จำนวนครั้งที่ลองส่งซ้ำเมื่อส่งไม่สำเร็จ</div>
                    </div>
                    <div class="config-item">
                        <label for="cfgTimeout">รอ NextAcc ตอบสูงสุด (วินาที)</label>
                        <input type="number" id="cfgTimeout" value="30" min="5" max="120" />
                    </div>
                </details>
                <div class="btn-row">
                    <button type="button" class="btn-success" onclick="saveSyncSettings('runResult')"><i class="fas fa-save"></i> บันทึก</button>
                </div>
                <div class="help-text">ปุ่มบันทึกในแท็บนี้และแท็บ "โหมดการส่ง &amp; นโยบาย" บันทึกค่าการส่งทุกข้อพร้อมกัน</div>
                <div class="test-result" id="runResult"></div>
            </div>
        </div>
        </section>

        <!-- ═══════════════ แท็บ 2: ผังบัญชี & แหล่งเงิน ═══════════════ -->
        <section class="acc-panel" id="tab-accounts" role="tabpanel" aria-labelledby="tabbtn-accounts" hidden>
        <p class="panel-intro">ผูกรหัสบัญชีของระบบนี้ แหล่งเงิน เกตเวย์ OTA และประเภทค่าใช้จ่าย กับผังบัญชีของ NextAcc — เปิดแท็บนี้แล้วตารางโหลดให้เอง. ผังบัญชีไม่ครบ/เก่า: กด "ดึงผังบัญชี" ที่แท็บ การเชื่อมต่อ หรือ "ดึงรายการจาก NextAcc" ในการ์ดแหล่งเงิน</p>
        <!-- Account Mapping Management -->
        <div class="journey-card" id="cardMapping">
            <h3><i class="fas fa-exchange-alt"></i> ผูกบัญชี (ระบบนี้ &harr; NextAcc)</h3>
            <p style="font-size:13px; color:#666; margin-bottom:10px;">
                จับคู่รหัสบัญชีของระบบนี้กับผังบัญชี NextAcc — ผังบัญชีล่าสุดดึงได้ที่แท็บ การเชื่อมต่อ (ปุ่ม "ดึงผังบัญชี")
            </p>
            <div style="font-size:12px; color:#888; margin-bottom:15px;" id="accountSyncInfo">
                <i class="fas fa-info-circle"></i> <span id="accountSyncStatus">ยังไม่ได้โหลดข้อมูล</span>
            </div>

            <div class="btn-row" style="margin-bottom:15px;">
                <button type="button" class="btn-primary" onclick="loadMappingsWithAccounts()"><i class="fas fa-sync"></i> โหลดการผูกบัญชี</button>
                <select id="mappingTypeFilter" style="padding:6px 10px; border:1px solid #ddd; border-radius:4px; font-size:13px;" onchange="filterMappingsByType()">
                    <option value="">ทุกประเภท</option>
                    <option value="PAYMENT_METHOD">วิธีรับ/จ่ายเงิน</option>
                    <option value="REVENUE">รายได้</option>
                    <option value="EXPENSE">ค่าใช้จ่าย</option>
                    <option value="ASSET">สินทรัพย์</option>
                    <option value="LIABILITY">หนี้สิน</option>
                </select>
            </div>

            <div style="overflow-x:auto;">
                <table class="queue-table" id="mappingTable">
                    <thead>
                        <tr>
                            <th>รหัสในระบบ</th>
                            <th>คำอธิบาย</th>
                            <th style="min-width:260px;">บัญชี NextAcc</th>
                            <th>ประเภท</th>
                            <th>สถานะ</th>
                            <th></th>
                        </tr>
                    </thead>
                    <tbody id="mappingBody">
                        <tr><td colspan="6" style="text-align:center; color:#999;">กำลังโหลด...</td></tr>
                    </tbody>
                </table>
            </div>
            <div class="test-result" id="mappingResult"></div>
        </div>

        <!-- Payment Method → Account Mapping -->
        <div class="journey-card" id="cardPaidHow">
            <h3><i class="fas fa-credit-card"></i> วิธีจ่ายเงิน &rarr; บัญชี NextAcc</h3>
            <p style="font-size:13px; color:#666; margin-bottom:10px;">
                ผูกวิธีรับ/จ่ายเงินแต่ละรายการกับ <b>"กระเป๋าเงิน" ที่เปิดอยู่จริงใน NextAcc</b> — บัญชีธนาคาร/e-Wallet (เมนู Bank ของ NextAcc),
                เงินสด, บัญชีพักเงินเกตเวย์ (11340 ลูกหนี้ผู้ให้บริการรับชำระเงิน), ลูกหนี้ OTA — ระบบส่งบัญชีที่เลือกเป็นฝั่งเงิน (PaymentAccountId)
                ของเอกสารทุกครั้ง. เลือกได้เฉพาะบัญชีสินทรัพย์/หนี้สิน (ระบบปฏิเสธรายได้/ค่าใช้จ่าย)
            </p>
            <button type="button" class="btn-primary" onclick="syncWallets()" style="margin-bottom:10px;"><i class="fas fa-cloud-download-alt"></i> ดึงรายการจาก NextAcc</button>
            <button type="button" class="btn-primary" onclick="loadPaidHowMapping()" style="margin-bottom:10px;"><i class="fas fa-sync"></i> โหลดวิธีจ่ายเงิน</button>
            <span id="walletLastSync" style="font-size:12px; color:#7f8c8d; margin-left:6px;"></span>
            <div style="overflow-x:auto;">
                <table class="queue-table" id="paidHowTable">
                    <thead>
                        <tr>
                            <th>ID</th>
                            <th>วิธีจ่ายเงิน</th>
                            <th style="min-width:320px;">กระเป๋าเงิน / บัญชี NextAcc</th>
                            <th>สถานะ</th>
                        </tr>
                    </thead>
                    <tbody id="paidHowBody">
                        <tr><td colspan="4" style="text-align:center; color:#999;">กำลังโหลด...</td></tr>
                    </tbody>
                </table>
            </div>
            <div class="test-result" id="paidHowResult"></div>
        </div>

        <!-- Online gateway → แหล่งเงินรายผู้ให้บริการ -->
        <div class="journey-card" id="cardGateway">
            <h3><i class="fas fa-globe"></i> รับชำระออนไลน์ (เกตเวย์) &rarr; แหล่งเงิน</h3>
            <p style="font-size:13px; color:#666; margin-bottom:10px;">
                ยอดที่ลูกค้าจ่ายผ่านเกตเวย์ยังไม่เข้าธนาคารจนกว่าผู้ให้บริการโอน (payout หลังหักค่าธรรมเนียม) — ควรลง
                <b>บัญชีพักเงินของเกตเวย์นั้น</b> ไม่ใช่ธนาคาร/เงินสด. เลือกแหล่งเงิน (ที่ผูกกระเป๋าเงินไว้ในตารางด้านบน) ต่อผู้ให้บริการ;
                ว่าง = ใช้แหล่งเงินกลาง <code id="gwFallbackName">Payment_PaidHow_Name</code>
            </p>
            <button type="button" class="btn-primary" onclick="loadGatewayPaidHow()" style="margin-bottom:10px;"><i class="fas fa-sync"></i> โหลด</button>
            <div style="overflow-x:auto;">
                <table class="queue-table">
                    <thead><tr><th>ผู้ให้บริการ</th><th style="min-width:260px;">แหล่งเงิน</th><th>ที่ใช้จริงตอนนี้</th></tr></thead>
                    <tbody id="gwPaidHowBody"><tr><td colspan="3" style="text-align:center; color:#999;">กด "โหลด"</td></tr></tbody>
                </table>
            </div>
            <p style="font-size:12px; color:#666; margin:10px 0 6px;">
                <b>แหล่งเงินที่ใช้จริงรายช่องทาง</b> (ช่องที่ลูกค้าเห็น + ช่องเกตเวย์) — ลำดับเดียวกับตอนรับเงินจริง:
                แคตตาล็อกช่องทาง (แยกวิธี/ยี่ห้อบัตร) &rarr; แหล่งเงินรายเกตเวย์ด้านบน &rarr; ค่ากลาง.
                แถวสีส้ม = แหล่งเงินที่ใช้จริงยังไม่มีกระเป๋าเงิน/บัญชี NextAcc (NextAcc จะเดาเป็นเงินสด)
            </p>
            <div style="overflow-x:auto;">
                <table class="queue-table">
                    <thead><tr><th>ช่องทาง</th><th>เกตเวย์/วิธี</th><th>แหล่งเงินที่ใช้จริง</th><th>ที่มา</th><th>บัญชี NextAcc</th></tr></thead>
                    <tbody id="gwChannelBody"><tr><td colspan="5" style="text-align:center; color:#999;">กด "โหลด"</td></tr></tbody>
                </table>
            </div>
            <div class="test-result" id="gwPaidHowResult"></div>
        </div>

        <!-- OTA → บัญชี/ผู้ซื้อ (Channel Collect documents) -->
        <div class="journey-card" id="cardOta">
            <h3><i class="fas fa-hotel"></i> OTA &rarr; บัญชี/ผู้ซื้อ (เงินที่ OTA เก็บแทน)</h3>
            <p style="font-size:13px; color:#666; margin-bottom:10px;">
                ใช้กับโหมด <b>RECEIPT_DOC</b> (ตั้งที่แท็บ "โหมดการส่ง &amp; นโยบาย"): การจอง Channel Collect ของช่องทางนี้จะออกใบเสร็จรับเงินใน NextAcc
                โดย <b>Dr แหล่งเงินที่เลือก</b> (ควรเป็นบัญชีพักเงิน/ลูกหนี้ของ OTA รายนั้น หมวด 11x) และผู้ซื้อ = ข้อมูลด้านล่าง
                (ว่าง = ชื่อช่องทาง). ปิดช่องทาง = ไม่โพสต์อัตโนมัติ. โหมดปัจจุบัน: <b id="otaModeLabel">-</b>
            </p>
            <button type="button" class="btn-primary" onclick="loadOtaChannelMap()" style="margin-bottom:10px;"><i class="fas fa-sync"></i> โหลดช่องทาง OTA</button>
            <div style="overflow-x:auto;">
                <table class="queue-table">
                    <thead>
                        <tr>
                            <th>ช่องทาง</th>
                            <th style="min-width:220px;">แหล่งเงิน (บัญชีพักเงิน OTA)</th>
                            <th style="min-width:200px;">ผู้ซื้อบนเอกสาร</th>
                            <th>เลขภาษี / สาขา</th>
                            <th>เปิด</th>
                            <th></th>
                        </tr>
                    </thead>
                    <tbody id="otaMapBody"><tr><td colspan="6" style="text-align:center; color:#999;">กด "โหลดช่องทาง OTA"</td></tr></tbody>
                </table>
            </div>
            <div class="test-result" id="otaMapResult"></div>
            <p style="font-size:12px; color:#666; margin-top:10px;"><i class="fas fa-info-circle"></i> ยกเลิก (void) เอกสาร OTA ที่ออกผิด: แท็บ <a href="#" onclick="return accGoto('tools','dangerZone')">เครื่องมือ → โซนอันตราย</a></p>
        </div>

        <!-- Expense Category → Account Mapping -->
        <div class="journey-card" id="cardPaidType">
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
                        <tr><td colspan="4" style="text-align:center; color:#999;">กำลังโหลด...</td></tr>
                    </tbody>
                </table>
            </div>
            <div class="test-result" id="paidTypeResult"></div>
        </div>
        </section>

        <!-- ═══════════════ แท็บ 3: โหมดการส่ง & นโยบาย ═══════════════
             ทุกปุ่ม "บันทึก" ในแท็บนี้เรียก saveSyncSettings() ตัวเดิม = บันทึกค่าการส่งทุกข้อพร้อมกัน (ความหมายค่าเหมือนเดิมทุกข้อ) -->
        <section class="acc-panel" id="tab-policy" role="tabpanel" aria-labelledby="tabbtn-policy" hidden>
        <p class="panel-intro">ทุกข้อมีป้าย <span class="rec-badge">ค่าแนะนำ</span> — ป้ายเขียว = ตั้งตามค่าแนะนำแล้ว · ป้ายส้ม = ต่างจากค่าแนะนำ (ตั้งได้ ถ้ารู้เหตุผล) · ป้ายเทา = ขึ้นกับนโยบาย/การใช้งานของกิจการ.
            ปุ่ม "บันทึก" ของทุกการ์ดบันทึกค่าในแท็บนี้ทั้งหมดพร้อมกัน</p>

        <!-- ตั้งค่าแนะนำ (กดทีเดียว) -->
        <div class="acc-card" id="cardSync">
            <h3><i class="fas fa-star"></i> ตั้งค่าแนะนำ — กดทีเดียว</h3>
            <div class="preset-box">
                <div style="font-size:13px; color:#333; margin:0 0 8px;">
                    ตั้งชุดที่ทดสอบแล้ว: <b>ใบเสร็จ/ใบสำคัญจ่ายออกเป็นเอกสารใน NextAcc</b> · <b>หักมัดจำในใบเช็คเอาท์ใบเดียว</b> ·
                    <b>เปิดตัวตรวจย้อนกลับ + กู้มัดจำค้างอัตโนมัติ</b> · ปิดเส้นทดลองที่ไม่ใช้แล้ว.
                    <b>ไม่แตะนโยบาย VAT มัดจำ</b>ที่ผู้ทำบัญชีตั้งไว้แล้ว
                </div>
                <details class="more"><summary>ที่มา</summary>
                    เดิมเอกสารรับบางใบไปเส้นขายสดแบบทดลอง → หัวขึ้น "ใบเสร็จรับเงิน" และมัดจำไม่ถูกหักในใบ. ปุ่มนี้ล้างตัวเลือกที่เปิดซ้อน/เลิกใช้แล้ว
                    กลับสู่ค่ามาตรฐาน (ตัวเลือกทดลองเดิม TaxReceipt_SingleDoc / CashSale_Deposit / NativeA เอาออกจากหน้าแล้ว — ไม่มีผลต่อโค้ด)
                </details>
                <button type="button" class="btn-success" onclick="applyRecommendedPreset()"><i class="fas fa-star"></i> ใช้ค่าแนะนำ</button>
                <div class="test-result" id="presetResult"></div>
            </div>
        </div>

        <div class="acc-grid">
        <!-- ก. โหมดเอกสาร -->
        <div class="acc-card" id="cardDocMode">
            <h3><i class="fas fa-file-alt"></i> โหมดการส่งเอกสาร</h3>
            <div class="config-item">
                <label for="cfgSyncMode">รูปแบบการส่ง (ค่าเริ่มต้น) <span class="rec-badge" data-rec-for="cfgSyncMode" data-rec="DOCUMENT" data-rec-label="ออกเอกสารใน NextAcc"></span></label>
                <select id="cfgSyncMode">
                    <option value="DOCUMENT" title="DOCUMENT">ออกเอกสารใน NextAcc + ลงบัญชีอัตโนมัติ</option>
                    <option value="JOURNAL_ONLY" title="JOURNAL_ONLY">ลงสมุดรายวันอย่างเดียว (ไม่มีเอกสาร)</option>
                </select>
                <div class="help-text">ใช้กับเอกสารที่ไม่ได้เลือกแยกด้านล่าง</div>
            </div>
            <div class="config-item sub">
                <label for="cfgReceiptSyncMode">ใบเสร็จ / ใบกำกับภาษี <span class="rec-badge" data-rec-for="cfgReceiptSyncMode" data-rec="DOCUMENT" data-rec-label="ออกเอกสารใน NextAcc"></span></label>
                <select id="cfgReceiptSyncMode">
                    <option value="">ใช้ค่าเริ่มต้นด้านบน</option>
                    <option value="LOCAL" title="LOCAL">ไม่ส่ง NextAcc (ใช้เอกสารของระบบนี้)</option>
                    <option value="DOCUMENT" title="DOCUMENT">ออกเอกสารใน NextAcc</option>
                    <option value="JOURNAL_ONLY" title="JOURNAL_ONLY">ลงสมุดรายวันอย่างเดียว</option>
                </select>
            </div>
            <div class="config-item sub">
                <label for="cfgVoucherSyncMode">ใบสำคัญจ่าย <span class="rec-badge" data-rec-for="cfgVoucherSyncMode" data-rec="DOCUMENT" data-rec-label="ออกเอกสารใน NextAcc"></span></label>
                <select id="cfgVoucherSyncMode">
                    <option value="">ใช้ค่าเริ่มต้นด้านบน</option>
                    <option value="LOCAL" title="LOCAL">ไม่ส่ง NextAcc</option>
                    <option value="DOCUMENT" title="DOCUMENT">ออกเอกสารค่าใช้จ่าย/ใบสำคัญจ่ายใน NextAcc</option>
                    <option value="JOURNAL_ONLY" title="JOURNAL_ONLY">ลงสมุดรายวันอย่างเดียว</option>
                </select>
            </div>
            <div class="config-item sub">
                <label for="cfgPayrollSyncMode">เงินเดือน <span class="rec-badge neutral">เลือกตามวิธีคิดเงินเดือน</span></label>
                <select id="cfgPayrollSyncMode">
                    <option value="">ใช้ค่าเริ่มต้นของเงินเดือน (ลงสมุดรายวันจากยอดของเรา)</option>
                    <option value="LOCAL" title="LOCAL">ไม่ส่ง NextAcc</option>
                    <option value="JOURNAL_ONLY" title="JOURNAL_ONLY">ลงสมุดรายวันจากยอดของเรา (ไม่ออก ภ.ง.ด.1/สปส./สลิป)</option>
                    <option value="DOCUMENT_IMPORT" title="DOCUMENT_IMPORT">ส่งยอดของเรา → NextAcc ออกเอกสารครบ (ภ.ง.ด.1/สปส./50 ทวิ/สลิป) — แนะนำเมื่อยอดเปลี่ยนทุกงวด</option>
                    <option value="DOCUMENT" title="DOCUMENT">ให้ NextAcc คำนวณเอง (เฉพาะเงินเดือนคงที่)</option>
                </select>
                <div class="help-text">ลงสมุดรายวันอย่างเดียว = ต้องยื่น ภ.ง.ด.1/สปส.1-10/50 ทวิ จากระบบนี้เอง</div>
            </div>
            <div class="config-item">
                <label for="cfgReceiptHeaderType">หัวกระดาษเอกสารขายที่ NextAcc พิมพ์ <span class="rec-badge" data-rec-for="cfgReceiptHeaderType" data-rec="AUTO" data-rec-label="อัตโนมัติ"></span></label>
                <select id="cfgReceiptHeaderType" onchange="this.setAttribute('data-loaded','1');">
                    <option value="AUTO" title="AUTO">อัตโนมัติ — ลูกค้าข้อมูลภาษีครบ = ใบกำกับเต็มรูป / ไม่ครบ = อย่างย่อ</option>
                    <option value="ABBREVIATED" title="ABBREVIATED">อย่างย่อทุกใบ (ยกเว้นนิติบุคคลที่ข้อมูลครบ)</option>
                </select>
                <div class="help-text">คำว่า "อย่างย่อ" พิมพ์ได้เมื่อบริษัทใน NextAcc ตั้งค่าที่เกี่ยวข้องแล้ว — มีผลกับใบที่ส่งหลังบันทึก</div>
                <details class="more"><summary>รายละเอียด</summary>
                    <b>อัตโนมัติ</b> (ค่าเริ่มต้น): ลูกค้ามีเลขผู้เสียภาษี 13 หลัก + ที่อยู่ → <b>"ใบเสร็จรับเงิน/ใบกำกับภาษี"</b> (เต็มรูป §86/4, ออก e-Tax ได้)
                    · ไม่ครบ (walk-in) → <b>"ใบเสร็จรับเงิน/ใบกำกับภาษีอย่างย่อ"</b> (§86/6)
                    <small>(ลูกค้าบุคคลธรรมดาที่ไม่มีเลขภาษีแต่มีชื่อ+ที่อยู่ NextAcc อาจถือว่าครบและพิมพ์เต็มรูปเอง)</small>
                    <br /><b>อย่างย่อทุกใบ</b>: ลูกค้าบุคคลธรรมดาได้ใบอย่างย่อเสมอ แม้กรอกเลขภาษี/ที่อยู่ (ส่ง "ผู้ซื้อไม่ประสงค์รับใบกำกับภาษี") —
                    นิติบุคคล (เลขภาษีขึ้นต้น 0) ที่ข้อมูลครบยังได้ใบเต็มรูป
                    <br /><b>เงื่อนไขฝั่ง NextAcc:</b> บริษัทต้องจด VAT และติ๊ก "ค้าปลีก" ในข้อมูลบริษัท (ภ.พ.06 บังคับเฉพาะสลิปเครื่องเก็บเงิน) —
                    ยังไม่ตั้ง = NextAcc ลดหัวเป็น "ใบเสร็จรับเงิน" เอง. TakeTime บังคับหัวแทนไม่ได้ (คำนวณตอนพิมพ์) — ตรวจผลจาก log <code>หัวเอกสาร NextAcc:</code>
                    <br /><b>ข้อกฎหมาย:</b> ใบอย่างย่อผู้ซื้อเคลมภาษีซื้อไม่ได้ (§82/5) · ลูกค้านิติบุคคล/ผู้จด VAT ที่ขอใบกำกับต้องได้ใบเต็มรูปเสมอ
                    · ใบอย่างย่อออก e-Tax ไม่ได้ · ใบมัดจำไม่ขึ้นกับตั้งค่านี้
                </details>
            </div>
            <div class="config-item">
                <label for="cfgAttachFiles">แนบไฟล์ไปกับเอกสาร <span class="rec-badge" data-rec-for="cfgAttachFiles" data-rec="true" data-rec-label="เปิด"></span></label>
                <select id="cfgAttachFiles">
                    <option value="true">เปิด — ส่งไฟล์ใบเสร็จ/สลิปไปพร้อมเอกสาร</option>
                    <option value="false">ปิด</option>
                </select>
                <div class="help-text">ใบเสร็จ PDF, สลิปโอน, ใบสำคัญจ่าย จะแนบในเอกสาร NextAcc</div>
            </div>
            <div class="btn-row"><button type="button" class="btn-success" onclick="saveSyncSettings('polResultDoc')"><i class="fas fa-save"></i> บันทึก</button></div>
            <div class="test-result" id="polResultDoc"></div>
        </div>

        <!-- ข. มัดจำ & VAT -->
        <div class="acc-card" id="cardDeposit">
            <h3><i class="fas fa-piggy-bank"></i> มัดจำ &amp; VAT</h3>
            <div class="config-item">
                <label for="cfgDepositVatRecognition">จุดรับรู้ VAT ของเงินมัดจำ <span class="rec-badge neutral">นโยบายผู้ทำบัญชี</span></label>
                <select id="cfgDepositVatRecognition">
                    <option value="CHECKOUT" title="CHECKOUT">ตอนเช็คเอาท์ — ใบเสร็จมัดจำไม่แยก VAT</option>
                    <option value="RECEIPT" title="RECEIPT">ตอนรับเงินมัดจำ (ม.78/1 บริการ)</option>
                </select>
                <div class="help-text">ตัวอย่างรายการบัญชีด้านล่างเปลี่ยนตามที่เลือก</div>
            </div>
            <div class="config-item">
                <label class="chk"><input type="checkbox" id="cfgDepositDeferOutputVat" /> พัก VAT มัดจำไว้ที่ "ภาษีขายรอเรียกเก็บ" แล้วโอนเข้าภาษีขายตอนเช็คเอาท์</label>
                <span class="rec-badge neutral">ใช้คู่กับ "ตอนรับเงินมัดจำ"</span>
                <div class="help-text">ต้องผูกบัญชีภาษีขายรอเรียกเก็บ (21913) ก่อน — ไม่ผูก = ลงภาษีขายทันทีแทน</div>
            </div>
            <div class="je-preview" id="depVatPreview" aria-live="polite"></div>
            <div class="config-item">
                <label class="chk"><input type="checkbox" id="cfgDepositDrivesJournal" /> หักมัดจำในใบเช็คเอาท์ใบเดียว (รายการบัญชีเดียว)</label>
                <span class="rec-badge" data-rec-for="cfgDepositDrivesJournal" data-rec="true" data-rec-label="เปิด"></span>
                <div class="help-text">ปิด = หักมัดจำเป็นรายการปรับแยกอีกใบ (ยอดถูกเหมือนกัน)</div>
                <details class="more"><summary>รายละเอียด</summary>
                    เปิด: ใบเช็คเอาท์ลง Dr เงินสดสุทธิ + Dr เงินมัดจำ 217xx + Dr ภาษีขายรอเรียกเก็บ 21913 / Cr รายได้ / Cr ภาษีขาย ในใบเดียว.
                    <b style="color:#c0392b;">เปิดได้เมื่อ NextAcc รองรับ depositAppliedDrivesJournal แล้วเท่านั้น</b> — เปิดก่อน = 217xx/21913 ไม่ถูกกลับ
                </details>
            </div>
            <div class="config-item">
                <label class="chk"><input type="checkbox" id="cfgDepositDrivesJournalRef" /> รองรับมัดจำที่ขึ้นเป็นรายการสมุดรายวัน (JV-INT) ในโหมดหักในใบเดียว</label>
                <span class="rec-badge" data-rec-for="cfgDepositDrivesJournalRef" data-rec="true" data-rec-label="เปิด"></span>
                <div class="help-text">ใช้คู่กับข้อบน — ปิด = มัดจำแบบ JV-INT ใช้รายการกลับแยก</div>
                <details class="more"><summary>รายละเอียด</summary>
                    เปิด: มัดจำที่ sync เป็น journal (JV-INT-…) จะส่งเลขรายการให้ NextAcc กลับในใบเดียว.
                    <b style="color:#c0392b;">เปิดได้เมื่อ NextAcc รุ่น cb55e3b ขึ้นไป</b> — เปิดก่อน = เอกสารค้างร่าง (มีกลไกถอยกลับ แต่ควรเปิดตามลำดับ)
                </details>
            </div>
            <div class="config-item">
                <label class="chk"><input type="checkbox" id="cfgAutoRecoverDeposit" /> กู้มัดจำเก่าที่ถูกกลับค้างอัตโนมัติตอนเช็คเอาท์</label>
                <span class="rec-badge" data-rec-for="cfgAutoRecoverDeposit" data-rec="true" data-rec-label="เปิด"></span>
                <div class="help-text">การจองที่ส่งซ้ำหลายรอบจนมัดจำถูกกลับค้าง → คืนมัดจำให้ใช้งานได้ก่อนหักในใบ</div>
                <details class="more"><summary>รายละเอียด</summary>
                    ตอนเช็คเอาท์จะกลับรายการกลับ (un-reverse) เฉพาะ JE มัดจำ → หักในใบเดียวได้. ทำซ้ำได้ไม่เบิ้ล.
                    ปิด = ใช้ 2 รายการ (ยอดสุทธิถูก). ควรเปิดหลังตรวจ GL 1-2 ใบ — การจองที่ส่งซ้ำมากควรดูด้วยตา
                </details>
            </div>
            <div class="config-item">
                <label class="chk"><input type="checkbox" id="cfgAutoReconcileDeposit" /> ล้างรายการปรับมัดจำค้างอัตโนมัติ เมื่อบัญชีมัดจำติดลบ</label>
                <span class="rec-badge" data-rec-for="cfgAutoReconcileDeposit" data-rec="true" data-rec-label="เปิด"></span>
                <div class="help-text">ใช้คู่กับ "ตรวจย้อนกลับหลังส่ง" — กลับเฉพาะเท่าที่จำเป็นจนยอดกลับใกล้ 0 แล้วตรวจซ้ำ</div>
                <details class="more"><summary>รายละเอียด</summary>
                    ถ้าตรวจพบบัญชีมัดจำ 21510 ติดลบ + เช็คเอาท์รอบนี้หักในใบสำเร็จ → กลับรายการปรับมัดจำค้าง (-DEPADJ) ทีละรายการ
                    (หยุดเมื่อ ~0 ไม่แก้เกิน) ทุกขั้นเป็น JE กลับรายการจริง. ยังไม่ 0 = เตือนให้ตรวจเอง. ปิด = เตือนอย่างเดียว
                </details>
            </div>
            <div class="config-item">
                <label class="chk"><input type="checkbox" id="cfgPostSyncVerify" /> ตรวจย้อนกลับหลังส่ง (อ่านเอกสาร/รายการบัญชีกลับมาเทียบ)</label>
                <span class="rec-badge" data-rec-for="cfgPostSyncVerify" data-rec="true" data-rec-label="เปิด"></span>
                <div class="help-text">แสดง ✅/⚠ ในคอลัมน์ "ตรวจสอบ" ของคิว — อ่านอย่างเดียว ไม่แก้อะไรบน NextAcc</div>
                <details class="more"><summary>รายละเอียด</summary>
                    ฝั่งรับ: ยอดตรง / JE บาลานซ์ / 21510 ไม่ติดลบ / สลิปแนบครบ · ฝั่งจ่าย: JE บาลานซ์ / โพสต์แล้ว / ยอดตรง / ไฟล์แนบครบ.
                    ปิดเพื่อลดจำนวนการเรียก NextAcc ต่อเอกสาร
                </details>
            </div>
            <div class="btn-row"><button type="button" class="btn-success" onclick="saveSyncSettings('polResultDep')"><i class="fas fa-save"></i> บันทึก</button></div>
            <div class="test-result" id="polResultDep"></div>
        </div>

        <!-- ค. ใบลดหนี้ / ตัวตรวจก่อนส่ง / สลิป -->
        <div class="acc-card" id="cardControls">
            <h3><i class="fas fa-shield-alt"></i> ใบลดหนี้ ตัวตรวจ &amp; สลิป</h3>
            <div class="config-item">
                <label>ใบลดหนี้ / คืนเงินลูกค้า <span class="rec-badge neutral">เปิดเมื่อพร้อมใช้</span></label>
                <label class="chk">
                    <input type="checkbox" id="cfgCreditNoteEnabled" onchange="this.setAttribute('data-loaded','1');" />
                    เปิดระบบใบลดหนี้
                </label>
                <label class="chk">
                    <input type="checkbox" id="cfgCreditNoteRequireApproval" onchange="this.setAttribute('data-loaded','1');" />
                    ต้องให้ Owner / Admin บัญชีอนุมัติก่อนคืนเงิน/ลงบัญชี
                </label>
                <span class="rec-badge" data-rec-for="cfgCreditNoteRequireApproval" data-rec="true" data-rec-label="ต้องอนุมัติ"></span>
                <div class="help-text" id="cfgCreditNoteHelp">
                    เปิด = ยกเลิกการจอง/คืนเงินผ่านหน้า <a href="<%= ResolveUrl("~/Account/CreditNotes") %>" target="_blank">ใบลดหนี้ / คืนเงินลูกค้า</a>
                    (ใบเสร็จที่มีใบลดหนี้อ้างจะยกเลิก/ลบ/แก้ไขไม่ได้). ปิด = ใช้ปุ่มยกเลิกการจองแบบเดิม.
                    ใบลดหนี้รออนุมัติแจ้งทาง Telegram
                </div>
            </div>
            <div class="config-item">
                <label>ตัวตรวจก่อนส่ง <span class="rec-badge neutral">เริ่มที่ "เตือน" แล้วค่อยบล็อก</span></label>
                <div class="help-text" style="font-size:12.5px; color:#555;">โหมดตอนนี้: <b id="pfModeNow">-</b> — ตรวจใบเสร็จก่อนบันทึก/ส่ง (ยอด, หักมัดจำ, ข้อมูลผู้ซื้อ, งวดปิด)</div>
                <button type="button" class="btn-secondary" style="margin-top:6px;" onclick="accGoto('queue','preflightBar')"><i class="fas fa-arrow-right"></i> ไปตั้งโหมด (แท็บคิว)</button>
            </div>
            <div class="config-item">
                <label>ตรวจสลิปโอนก่อนออกเอกสาร</label>
                <div class="help-text">ตั้งที่หน้าของตัวเอง (บริการตรวจสลิป, อ่านยอดจากรูป, บัญชีรับเงิน)</div>
                <a class="btn-secondary" style="display:inline-block; margin-top:6px; text-decoration:none;" href="<%= ResolveUrl("~/Admin/Settings/SlipCheckSettings") %>"><i class="fas fa-receipt"></i> ตั้งค่าตรวจสลิป</a>
            </div>
            <div class="config-item">
                <label>เอกสารขายตามช่องทาง/วิธีรับเงิน</label>
                <div class="help-text">ออกทีละใบ / รวบรายวัน / ไม่ส่ง — แยกตามช่องทางขาย</div>
                <a class="btn-secondary" style="display:inline-block; margin-top:6px; text-decoration:none;" href="<%= ResolveUrl("~/Admin/Settings/SalesDocPolicy") %>"><i class="fas fa-store"></i> นโยบายเอกสารขาย</a>
            </div>
            <div class="btn-row"><button type="button" class="btn-success" onclick="saveSyncSettings('polResultCtl')"><i class="fas fa-save"></i> บันทึก</button></div>
            <div class="test-result" id="polResultCtl"></div>
        </div>

        <!-- ง. รายได้อื่น / OTA / ขายหน้าร้าน -->
        <div class="acc-card" id="cardRevenue">
            <h3><i class="fas fa-cash-register"></i> รายได้ช่องทางอื่น</h3>
            <div class="config-item">
                <label for="cfgPosDailyRollup">ขายหน้าร้านที่ไม่ออกใบกำกับ → รวบรายวัน <span class="rec-badge neutral">แนะนำ: เปิด ถ้าขายหน้าร้าน</span></label>
                <select id="cfgPosDailyRollup">
                    <option value="false">ปิด — ไม่รวบ (ขายที่ไม่ออกใบกำกับจะไม่ขึ้น NextAcc)</option>
                    <option value="true">เปิด — รวบเป็นใบรับเงินสดสรุปวันละใบต่อแหล่งรับเงิน</option>
                </select>
                <div class="help-text">ใบสรุปเป็นเอกสารประกอบการลงบัญชีเท่านั้น — หน้าร้านยังต้องออกใบอย่างย่อให้ลูกค้ารายใบ</div>
                <details class="more"><summary>รายละเอียด</summary>
                    รวบการขายหน้าร้านที่ <b>ไม่ติ๊ก "ออกใบกำกับภาษีในระบบ"</b> ของแต่ละวัน → ส่ง NextAcc (Dr เงินสด/Cr รายได้สินค้า/Cr ภาษีขาย)
                    + ตัดต้นทุน (Dr ต้นทุนขาย/Cr สินค้าคงเหลือ) อัตโนมัติเบื้องหลัง. ขายที่ออกใบกำกับในระบบยังส่งทีละใบเหมือนเดิม
                </details>
            </div>
            <div class="config-item">
                <label for="cfgRoomServiceRevenue">รูมเซอร์วิส (สั่งผ่าน Guest Portal) → ลงบัญชี <span class="rec-badge neutral">แนะนำ: เปิด ถ้าใช้รูมเซอร์วิส</span></label>
                <select id="cfgRoomServiceRevenue">
                    <option value="false">ปิด — ไม่ลงบัญชี (ออเดอร์ที่ลูกค้าจ่ายเองไม่มีรายได้/ไม่ตัดต้นทุน)</option>
                    <option value="true">เปิด — ลงรายได้ + ตัดต้นทุนอัตโนมัติ</option>
                </select>
                <div class="help-text">จ่ายเอง = ใบรับเงินสดรวมวันละใบ · ลงบิลห้อง = ตัดต้นทุนอย่างเดียว (รายได้ไปกับใบเช็คเอาท์)</div>
                <details class="more"><summary>รายละเอียด</summary>
                    จัดการออเดอร์ของ<b>วันที่ผ่านไปแล้ว</b>: ลูกค้าจ่ายเอง (โอน/เงินสด) → ใบรับเงินสด 1 ใบ/วัน/วิธีจ่าย
                    (Dr เงินสด/ธนาคาร · Cr รายได้สินค้า · Cr ภาษีขาย) + ตัดต้นทุน. ลงบิลห้อง → ตัดต้นทุนอย่างเดียว (ไม่โพสต์รายได้ซ้ำ).
                    ต้องอัปเดตฐานข้อมูลก่อน <abbr title="สคริปต์ฐานข้อมูล: PHASE18_20" style="cursor:help;">(รายละเอียดสำหรับผู้ดูแล)</abbr> — ออเดอร์ก่อนติดตั้งไม่ถูกลงย้อนหลัง
                </details>
            </div>
            <div class="config-item">
                <label for="cfgOtaRoomRevenue">รายได้ค่าห้องจาก OTA (Agoda/Booking) → ลงบัญชี <span class="rec-badge neutral">เปิดหลังผู้ทำบัญชีตรวจ</span></label>
                <select id="cfgOtaRoomRevenue">
                    <option value="false">ปิด — ไม่ลงบัญชี (การจอง OTA ไม่มีใบเสร็จ รายได้ค่าห้องจึงไม่เข้าบัญชี)</option>
                    <option value="true">เปิด — ลงรายได้ตามการจองจริง เป็นลูกหนี้ OTA</option>
                </select>
                <div class="help-text">โพสต์ต่อการจองหลังเลยวันเช็คเอาท์: Dr ลูกหนี้ OTA · Cr รายได้ห้อง · Cr ภาษีขาย</div>
                <details class="more"><summary>รายละเอียด</summary>
                    เงินที่ OTA โอนเข้ามาจริงไปตัดลูกหนี้ตอนปิดงวด payout (ค่าคอมเป็นค่าใช้จ่ายแยก).
                    <b>ต้องตั้งก่อน:</b> ผูกบัญชีลูกหนี้ OTA (<code>OTA_RECEIVABLE</code>) + ดึงผังบัญชี + รหัสบริษัท — ไม่ครบระบบจะข้ามและเขียน log
                    (ไม่ลงเป็นเงินสดผิด ๆ). <abbr title="สคริปต์ฐานข้อมูล: PHASE18_20, PHASE18_11" style="cursor:help;">(รายละเอียดสำหรับผู้ดูแล)</abbr>
                </details>
            </div>
            <div class="config-item sub">
                <label for="cfgOtaDocumentMode">เงินค่าห้องที่ OTA เก็บแทน → ออกใบเสร็จใน NextAcc <span class="rec-badge neutral">ตามนโยบาย OTA</span></label>
                <select id="cfgOtaDocumentMode" onchange="this.setAttribute('data-loaded','1');">
                    <option value="OFF" title="OFF">ปิด — ใช้ลูกหนี้ OTA กลางบัญชีเดียว (ข้อบน)</option>
                    <option value="RECEIPT_DOC" title="RECEIPT_DOC">ออกใบเสร็จรับเงินต่อการจอง (ผู้ซื้อ = OTA · บัญชีพักเงินรายช่องทาง)</option>
                </select>
                <div class="help-text">เปิดแล้ว<b>แทนที่</b>ข้อบน — การจองหนึ่งรายการโพสต์ทางเดียวเสมอ. ตั้งบัญชี/ผู้ซื้อรายช่องทางที่แท็บ "ผังบัญชี &amp; แหล่งเงิน"</div>
                <details class="more"><summary>รายละเอียด</summary>
                    หลังเลยวันเช็คเอาท์สร้างใบเสร็จต่อการจอง Channel Collect: Dr บัญชีพักเงิน/ลูกหนี้ของ OTA รายช่องทาง · Cr รายได้ห้อง · Cr ภาษีขาย
                    (ช่องทางที่ยังไม่ตั้งใช้ OTA_RECEIVABLE). ใช้เครื่องหมาย <code>Ota_Revenue_Ref</code> ร่วมกันจึงไม่ลงซ้ำ.
                    ต้องมีรหัสบริษัท <abbr title="สคริปต์ฐานข้อมูล: PHASE19_21" style="cursor:help;">(รายละเอียดสำหรับผู้ดูแล)</abbr>
                </details>
            </div>
            <div class="config-item">
                <label for="cfgSecurityDepositJournal">เงินประกันความเสียหาย (รับโอน) → ลงบัญชีหนี้สิน <span class="rec-badge neutral">เปิดถ้ารับเงินประกันแบบโอน</span></label>
                <select id="cfgSecurityDepositJournal">
                    <option value="false">ปิด — ตามเดิม (เงินประกันไม่เข้าบัญชี)</option>
                    <option value="true">เปิด — รับโอนเป็นหนี้สิน · โอนคืนกลับรายการ · หักค่าเสียหายด้วยใบเสร็จ</option>
                </select>
                <div class="help-text">หักค่าเสียหาย = ออกใบเสร็จเลือกแหล่งเงิน "หักจากเงินประกัน" (ห้ามเลือกเงินสด/บัญชีรับโอน — นับเงินซ้ำ)</div>
                <details class="more"><summary>รายละเอียด</summary>
                    ใช้กับโหมดรับเงินประกันแบบโอนเข้าบัญชีโรงแรม: รับโอน → Dr ธนาคาร · Cr <code>SECURITY_DEPOSIT_LIABILITY</code>; โอนคืน → กลับขา;
                    หักค่าเสียหาย → ไม่มี JE แยก ล้างด้วยใบเสร็จค่าเสียหาย (Dr หนี้สิน · Cr รายได้ค่าเสียหาย + VAT).
                    ต้องผูก <code>SECURITY_DEPOSIT_LIABILITY</code> (ผังโรงแรม NextAcc 21530) + บัญชีของแหล่งเงินรับโอน + แหล่งเงิน "หักจากเงินประกัน" กับบัญชีเดียวกับหนี้สิน.
                    เงินประกันที่รับก่อนเปิดจะไม่ถูกลงขาออก <abbr title="สคริปต์ฐานข้อมูล: PHASE19_24, PHASE19_25" style="cursor:help;">(รายละเอียดสำหรับผู้ดูแล)</abbr>
                </details>
            </div>
            <div class="config-item">
                <label class="chk"><input type="checkbox" id="cfgCashSaleCompanyDoc" /> ขายสด B2B ออกเอกสารใบเดียว (ใบกำกับภาษี/ใบเสร็จรับเงิน)</label>
                <span class="rec-badge exp">ทดลอง — เปิดหลังตรวจรายการบัญชีใบแรก</span>
                <div class="help-text">ปิด = ใบกำกับเปิดลูกหนี้แล้วปิดด้วยใบรับชำระ (ลูกค้าอาจได้ 3 ใบ)</div>
                <details class="more"><summary>รายละเอียด</summary>
                    เปิด = ส่งผ่านเอกสารระดับบริษัทแบบ TaxInvoice + IssuedAsCashReceipt → NextAcc ลง Dr เงินสด + กลับมัดจำในใบเดียว
                    ไม่เปิดลูกหนี้ ไม่มีใบรับชำระแยก + e-Tax. ถ้า NextAcc ยังเปิดลูกหนี้ ระบบปิดให้เอง (GL ถูก แต่ได้ใบเพิ่ม) และเขียน log
                </details>
            </div>
            <div class="config-item">
                <label class="chk"><input type="checkbox" id="cfgCashSaleUseReceipt" /> ใช้ "ใบเสร็จรับเงิน" แทนใบกำกับตอนเช็คเอาท์ (ทางเลี่ยงชั่วคราว)</label>
                <span class="rec-badge" data-rec-for="cfgCashSaleUseReceipt" data-rec="false" data-rec-label="ปิด"></span>
                <div class="help-text">เปิดแล้วหัวเป็น "ใบเสร็จรับเงิน" เสมอและไม่ออก e-Tax — ลูกค้าขอใบกำกับภาษีต้องปิด</div>
                <details class="more"><summary>รายละเอียด</summary>
                    เช็คเอาท์ลูกค้ามีเลขภาษี → ออกเป็น Receipt (type 3): Dr เงินสด / Cr รายได้รายบรรทัด / Cr ภาษีขาย ไม่มีลูกหนี้ + หักมัดจำในใบ.
                    หัวเอกสารมาจาก flag บนเอกสาร (Receipt ไม่ upgrade หัวไม่ว่าผู้ซื้อมีเลขภาษีครบแค่ไหน). ใช้ชั่วคราวระหว่างรอ NextAcc เท่านั้น
                </details>
            </div>
            <div class="btn-row"><button type="button" class="btn-success" onclick="saveSyncSettings('polResultRev')"><i class="fas fa-save"></i> บันทึก</button></div>
            <div class="test-result" id="polResultRev"></div>
        </div>

        <!-- จ. สต๊อก & ซื้อ -->
        <div class="acc-card" id="cardStockPolicy">
            <h3><i class="fas fa-boxes"></i> สต๊อก &amp; รับของ</h3>
            <div class="config-item">
                <label for="cfgStockQtySync">ส่งจำนวนสต๊อกไป NextAcc <span class="rec-badge neutral">เปิดถ้าดูสต๊อกใน NextAcc</span></label>
                <select id="cfgStockQtySync">
                    <option value="false">ปิด</option>
                    <option value="true">เปิด — ส่งจำนวนรับเข้า/ตัดออก (จำนวนอย่างเดียว ไม่ลงบัญชีซ้ำ)</option>
                </select>
                <div class="help-text">ต้องส่งข้อมูลสินค้าให้จับคู่กันก่อน</div>
            </div>
            <div class="config-item">
                <label for="cfgStockQtyPull">ดึงการปรับสต๊อกจาก NextAcc กลับมา <span class="rec-badge neutral">เปิดคู่กับข้อบน</span></label>
                <select id="cfgStockQtyPull">
                    <option value="false">ปิด</option>
                    <option value="true">เปิด — ดึงการปรับ/นับ/โอนที่ทำใน NextAcc กลับเข้าระบบนี้</option>
                </select>
                <div class="help-text">กันการวนซ้ำด้วยเลขรายการเคลื่อนไหว — เปิดทั้งสองทางให้สต๊อกตรงกัน</div>
            </div>
            <div class="config-item">
                <label class="chk"><input type="checkbox" id="cfgStockInUseGRNI" /> รับของเข้าสต๊อกแบบ "พักรับของ (GRNI)" แทนเจ้าหนี้</label>
                <span class="rec-badge neutral">เปิดถ้าบันทึกใบกำกับซื้อด้วยการสแกน</span>
                <div class="help-text">รับของ = Dr สินค้าคงเหลือ / Cr พักรับของ — ตอนสแกนใบกำกับซื้อต้องเลือก "ล้างรับของ GRNI"</div>
                <details class="more"><summary>รายละเอียด</summary>
                    ใบกำกับที่สแกนทีหลัง → Dr พักรับของ + Dr ภาษีซื้อ / Cr เงินสด-เจ้าหนี้ = ล้างพักรับของ + เคลมภาษีซื้อ ไม่ลงต้นทุนซ้ำ.
                    <b style="color:#c0392b;">ต้องผูกบัญชี GRNI (21240) ให้ตรงผังบัญชี NextAcc</b> และเลือก "ล้างรับของ GRNI" ทุกครั้ง มิฉะนั้นต้นทุนซ้ำ.
                    ตรวจยอดค้างที่แท็บเครื่องมือ "ยอดคงค้าง GRNI". ปิด = รับของ Cr เจ้าหนี้การค้า (เดิม)
                </details>
            </div>
            <div class="config-item">
                <label class="chk"><input type="checkbox" id="cfgStockInSkipJournal" /> ไม่ลงบัญชีตอนรับของ — ให้ใบกำกับซื้อลงทั้งหมด</label>
                <span class="rec-badge" data-rec-for="cfgStockInSkipJournal" data-rec="false" data-rec-label="ปิด"></span>
                <div class="help-text">เปิดแล้วใบกำกับซื้อต้องลง "สินค้าคงเหลือ" เสมอ ไม่งั้นสต๊อกในบัญชีติดลบตอนตัดต้นทุน</div>
                <details class="more"><summary>รายละเอียด</summary>
                    รับของจะไม่ส่งรายการบัญชีเลย (ทับข้อ GRNI). เหมาะกับกิจการที่ใบกำกับเป็นตัวลงต้นทุนสินค้าเสมอ — ปกติแนะนำ GRNI แทน
                </details>
            </div>
            <div class="btn-row"><button type="button" class="btn-success" onclick="saveSyncSettings('polResultStock')"><i class="fas fa-save"></i> บันทึก</button></div>
            <div class="test-result" id="polResultStock"></div>
        </div>

        <!-- ฉ. e-Tax -->
        <div class="acc-card" id="cardEtax">
            <h3><i class="fas fa-file-invoice"></i> ใบกำกับภาษีอิเล็กทรอนิกส์ (e-Tax)</h3>
            <div class="help-text" style="margin-bottom:6px;">ใช้ได้เมื่อใบเสร็จออกเป็นเอกสารใน NextAcc — สร้าง e-Tax ตามใบนั้น แล้วส่งสรรพากร/อีเมลลูกค้าได้อัตโนมัติ</div>
            <div class="config-item">
                <label for="cfgEtaxAutoGenerate">สร้าง e-Tax อัตโนมัติ <span class="rec-badge neutral">เปิดเมื่อพร้อมออก e-Tax</span></label>
                <select id="cfgEtaxAutoGenerate">
                    <option value="false">ปิด — สร้างเองที่หน้าใบเสร็จ</option>
                    <option value="true">เปิด — สร้างทันทีเมื่อเอกสารใน NextAcc สำเร็จ</option>
                </select>
            </div>
            <div class="config-item">
                <label for="cfgEtaxAutoSign">ลงนาม e-Tax อัตโนมัติ <span class="rec-badge" data-rec-for="cfgEtaxAutoSign" data-rec="true" data-rec-label="เปิด"></span></label>
                <select id="cfgEtaxAutoSign">
                    <option value="true">เปิด — ลงนามด้วยใบรับรองใน NextAcc</option>
                    <option value="false">ปิด — สร้างเป็นร่าง</option>
                </select>
            </div>
            <div class="config-item">
                <label for="cfgEtaxAutoSubmit">ส่งสรรพากรอัตโนมัติ <span class="rec-badge neutral">ตามนโยบาย</span></label>
                <select id="cfgEtaxAutoSubmit">
                    <option value="false">ปิด — ส่งเอง</option>
                    <option value="true">เปิด — ส่งทันทีหลังลงนาม</option>
                </select>
            </div>
            <div class="config-item">
                <label for="cfgEtaxAutoSendEmail">ส่งอีเมลให้ลูกค้าอัตโนมัติ <span class="rec-badge neutral">ตามนโยบาย</span></label>
                <select id="cfgEtaxAutoSendEmail">
                    <option value="false">ปิด</option>
                    <option value="true">เปิด — ส่งอีเมลพร้อม PDF/XML</option>
                </select>
                <div class="help-text">ใช้อีเมลลูกค้าของการจอง — ไม่มีอีเมลจะข้ามเงียบ ๆ</div>
            </div>
            <details class="more">
                <summary>ข้อความอีเมล e-Tax และช่องทางส่ง</summary>
                <div class="config-item">
                    <label for="cfgEtaxEmailSubject">หัวข้ออีเมล</label>
                    <input type="text" id="cfgEtaxEmailSubject" placeholder="ใบกำกับภาษีอิเล็กทรอนิกส์ {ReceiptNumber}" />
                    <div class="help-text">ตัวแปร: {ReceiptNumber}, {GuestName}, {Amount}, {Date}</div>
                </div>
                <div class="config-item">
                    <label for="cfgEtaxEmailCc">สำเนา (CC) เริ่มต้น</label>
                    <input type="text" id="cfgEtaxEmailCc" placeholder="acc@company.com, boss@company.com" />
                    <div class="help-text">เติมให้ในหน้าส่ง e-Tax (แก้รายใบได้) — คั่นด้วย , หรือ ;</div>
                </div>
                <div class="config-item">
                    <label for="cfgEtaxEmailBody">เนื้อหาอีเมล</label>
                    <textarea id="cfgEtaxEmailBody" rows="4" style="width:100%;" placeholder="เรียน {GuestName}&#10;&#10;กรุณาดาวน์โหลดใบกำกับภาษีอิเล็กทรอนิกส์ {ReceiptNumber}..."></textarea>
                </div>
                <div class="config-item">
                    <label>แนบ PDF / XML</label>
                    <div class="pair">
                        <select id="cfgEtaxEmailAttachPdf">
                            <option value="true">PDF: แนบ</option>
                            <option value="false">PDF: ไม่แนบ</option>
                        </select>
                        <select id="cfgEtaxEmailAttachXml">
                            <option value="false">XML: ไม่แนบ</option>
                            <option value="true">XML: แนบ (สำหรับลูกค้าธุรกิจ)</option>
                        </select>
                    </div>
                </div>
                <div class="config-item">
                    <label for="cfgEtaxEmailLocalOnly">ช่องทางส่งอีเมล <span class="rec-badge" data-rec-for="cfgEtaxEmailLocalOnly" data-rec="false" data-rec-label="ผ่าน NextAcc"></span></label>
                    <select id="cfgEtaxEmailLocalOnly">
                        <option value="false">ผ่าน NextAcc (ค่าเริ่มต้น)</option>
                        <option value="true">อีเมลของระบบนี้เท่านั้น (ข้าม NextAcc)</option>
                    </select>
                    <div class="help-text">เลือกอีเมลของระบบนี้เฉพาะเมื่อบริการอีเมลของ NextAcc ปิดอยู่</div>
                </div>
                <div class="config-item">
                    <label for="cfgEtaxEmailFallback">ส่งสำรองผ่านอีเมลของระบบนี้ <span class="rec-badge" data-rec-for="cfgEtaxEmailFallback" data-rec="true" data-rec-label="เปิด"></span></label>
                    <select id="cfgEtaxEmailFallback">
                        <option value="true">เปิด — NextAcc ส่งไม่สำเร็จ ให้ส่งผ่านอีเมลของระบบนี้</option>
                        <option value="false">ปิด — รายงานข้อผิดพลาดอย่างเดียว</option>
                    </select>
                </div>
            </details>
            <div class="config-item">
                <label for="cfgEtaxRdWatch">อ่านอีเมลตอบกลับจากสรรพากร → มาร์ค e-Tax สำเร็จ <span class="rec-badge neutral">เปิดถ้าใช้กล่องอีเมลจอง</span></label>
                <select id="cfgEtaxRdWatch">
                    <option value="false">ปิด</option>
                    <option value="true">เปิด — อ่านอีเมลตอบกลับแล้วมาร์คว่า "สรรพากรรับแล้ว"</option>
                </select>
                <div class="help-text">ใช้กล่องอีเมลเดียวกับระบบอ่านอีเมลจอง (แท็บ "อีเมลจอง &amp; LINE") — นับเฉพาะอีเมลที่ยืนยันสำเร็จ</div>
                <label for="cfgEtaxRdFrom" style="margin-top:8px;">อีเมลผู้ส่งที่ถือว่าเป็นสรรพากร (คั่นจุลภาค)</label>
                <input type="text" id="cfgEtaxRdFrom" placeholder="rd.go.th, etax, teda.th" />
            </div>
            <div class="btn-row"><button type="button" class="btn-success" onclick="saveSyncSettings('polResultEtax')"><i class="fas fa-save"></i> บันทึก</button></div>
            <div class="test-result" id="polResultEtax"></div>
        </div>
        </div><!-- /acc-grid -->
        <div class="test-result" id="syncTestResult"></div>
        </section>

        <!-- ═══════════════ แท็บ 4: คิว & ตรวจสุขภาพ ═══════════════ -->
        <section class="acc-panel" id="tab-queue" role="tabpanel" aria-labelledby="tabbtn-queue" hidden>
        <div class="journey-card" id="cardHealth">
            <h3><i class="fas fa-stethoscope"></i> ตรวจสุขภาพการเชื่อมต่อ</h3>
            <!-- 🩺 ตรวจสุขภาพ integration: จับปัญหา (mapping ชี้บัญชีที่ไม่มี, แหล่งเงินยังไม่ผูก,
                 คิวตายค้าง, NextAcc ล่ม) ก่อนที่เอกสารจริงจะ sync ไม่ผ่าน -->
            <div style="margin-bottom:14px;">
                <button type="button" class="btn-default" onclick="runHealthCheck()">
                    <i class="fas fa-stethoscope"></i> ตรวจสุขภาพการเชื่อมต่อ
                </button>
                <span style="font-size:11.5px; color:#888; margin-left:8px; display:inline-block;">
                    ตรวจ mapping/ผังบัญชี/แหล่งเงิน/คิว — ทำก่อนออกเอกสารจริงจะได้ไม่ค้างคิว
                </span>
                <div id="healthResult" style="margin-top:10px;"></div>
            </div>
        </div>

        <div class="journey-card" id="cardQueue">
            <h3><i class="fas fa-tasks"></i> คิวส่งข้อมูลเข้า NextAcc</h3>
            <div class="queue-stats" id="queueStats">
                <div class="queue-stat" onclick="filterByStatus('')" id="qsAll"><div class="num" id="qsTotal" style="color:#333;">-</div><div class="lbl">ทั้งหมด</div></div>
                <div class="queue-stat" onclick="filterByStatus('PENDING')" id="qsPendingCard"><div class="num num-pending" id="qsPending">-</div><div class="lbl">รอส่ง</div></div>
                <div class="queue-stat" onclick="filterByStatus('PROCESSING')" id="qsProcessingCard"><div class="num num-processing" id="qsProcessing">-</div><div class="lbl">กำลังส่ง</div></div>
                <div class="queue-stat" onclick="filterByStatus('COMPLETED')" id="qsCompletedCard"><div class="num num-completed" id="qsCompleted">-</div><div class="lbl">สำเร็จ</div></div>
                <div class="queue-stat" onclick="filterByStatus('FAILED')" id="qsFailedCard"><div class="num num-failed" id="qsFailed">-</div><div class="lbl">ล้มเหลว</div></div>
                <div class="queue-stat" onclick="filterByStatus('AWAITING_SLIP')" id="qsSlipCard" title="พักรอตรวจสลิป — ปล่อยเมื่อสลิปผ่าน/อนุมัติที่หน้าตรวจสลิปโอน"><div class="num" id="qsSlip" style="color:#8E24AA;">-</div><div class="lbl">รอตรวจสลิป</div></div>
            </div>

            <!-- PHASE19_37: ชนิดปัญหา (ใครต้องทำอะไร) + โหมดตัวตรวจก่อนส่ง -->
            <div id="queueClassBar" style="display:none; margin:6px 0 10px; font-size:12.5px;"></div>
            <div id="preflightBar" style="display:none; margin:0 0 12px; padding:8px 10px; border:1px solid #e5e7eb; border-radius:6px; background:#fafafa; font-size:12.5px;">
                <b>ตัวตรวจก่อนส่ง:</b>
                <select id="pfModeSel" style="padding:3px 6px;" onchange="this.dataset.touched='1'">
                    <option value="OFF">OFF — ปิด (เดิม)</option>
                    <option value="WARN">WARN — เตือน ไม่บล็อก</option>
                    <option value="BLOCK">BLOCK — บล็อกปัญหาข้อมูลที่หน้าเว็บ</option>
                </select>
                <button type="button" class="btn-default" style="padding:3px 10px;" onclick="savePreflightMode()">บันทึกโหมด</button>
                <button type="button" class="btn-default" style="padding:3px 10px;" onclick="runPreflightScan()" title="ตรวจใบเสร็จย้อนหลังด้วยกฎ (ไม่ยิง NextAcc) — ใช้ดูก่อนเปลี่ยนเป็น BLOCK">🔎 สแกนย้อนหลัง 30 วัน</button>
                <button type="button" class="btn-default" style="padding:3px 10px;" onclick="runIntegrityScan()" title="ตรวจยอด/มัดจำ/ใบไม่เข้าคิว/ใบค้างก่อนยื่น ภ.พ.30 (ปกติรันเองทุกคืน)">🧮 ตรวจรายคืนตอนนี้</button>
                <span style="color:#888;">ปัญหาตั้งค่าไม่บล็อกหน้าร้านทุกโหมด — พักคิวแล้วกลับเข้าคิวเองเมื่อบันทึก mapping</span>
                <div id="pfResult" style="margin-top:6px;"></div>
            </div>

            <div class="queue-toolbar">
                <div class="queue-toolbar-left">
                    <button type="button" class="btn-primary" onclick="loadQueueData()"><i class="fas fa-sync"></i> รีเฟรช</button>
                    <button type="button" class="btn-warning" onclick="processQueue('queueActionResult')" title="ส่งรายการที่รออยู่ตอนนี้ ไม่รอรอบอัตโนมัติ"><i class="fas fa-play"></i> ส่งคิวตอนนี้</button>
                    <button type="button" class="btn-warning" onclick="retryAllFailed()" title="ลองใหม่เฉพาะชนิดที่ปลอดภัย (ชั่วคราว/ไม่ทราบสาเหตุ/รอนานเกิน) — ข้ามรายการที่อาจซ้ำ ต้องใบลดหนี้ ต้องตั้งค่า หรือต้องแก้ข้อมูล"><i class="fas fa-redo"></i> ลองใหม่ที่ล้มเหลวทั้งหมด</button>
                    <input type="search" id="queueSearch" placeholder="ค้นหา: เลขใบเสร็จ / เลขจอง / เลขเอกสาร NextAcc / เลขคิว"
                           style="padding:6px 8px; min-width:260px; max-width:100%; border:1px solid #ddd; border-radius:4px; font-size:13px;"
                           onkeydown="if(event.key==='Enter'){event.preventDefault();searchQueue();}" />
                    <button type="button" class="btn-default" onclick="searchQueue()"><i class="fas fa-search"></i> ค้นหา</button>
                    <button type="button" class="btn-default" id="btnQueueSearchClear" style="display:none;" onclick="clearQueueSearch()">ล้างการค้นหา</button>
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

            <div class="test-result" id="queueActionResult"></div>
            <div id="retryAllResult" style="margin:0 0 10px;"></div>

            <div style="overflow-x:auto;">
                <table class="queue-table" id="queueTable">
                    <thead>
                        <tr>
                            <th style="width:30px;"><input type="checkbox" id="selectAllQueue" onchange="toggleSelectAll(this)" title="เลือกทั้งหมด" /></th>
                            <th>เลขคิว</th>
                            <th>รายการ</th>
                            <th>งาน</th>
                            <th>สถานะ</th>
                            <th>เอกสาร NextAcc</th>
                            <th>ตรวจสอบ</th>
                            <th>ครั้งที่ลอง</th>
                            <th>ข้อผิดพลาด</th>
                            <th>สร้างเมื่อ</th>
                            <th></th>
                        </tr>
                    </thead>
                    <tbody id="queueBody">
                        <tr><td colspan="11" style="text-align:center; color:#999;">กำลังโหลด...</td></tr>
                    </tbody>
                </table>
            </div>

            <div class="pagination" id="queuePagination" style="margin-top:15px; justify-content:center;"></div>
        </div>
        </section>

        <!-- ═══════════════ แท็บ 5: เครื่องมือ ═══════════════ -->
        <section class="acc-panel" id="tab-tools" role="tabpanel" aria-labelledby="tabbtn-tools" hidden>
        <p class="panel-intro">ค้นหา/ตรวจ/แก้รายเอกสาร — เครื่องมือที่เปลี่ยนรายการบัญชีจริงอยู่ใน <b>โซนอันตราย</b> ท้ายแท็บ</p>
        <!-- Document Source Lookup (เชื่อมระหว่าง TakeTime ↔ NextAcc ↔ E-Tax) -->
        <div class="journey-card" id="cardDocSource">
            <h3><i class="fas fa-link"></i> ที่มาของเอกสาร — TakeTime ↔ NextAcc ↔ e-Tax</h3>
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

        <!-- 🔗 ผูก / ปลดการจับคู่เอกสาร (ย้ายมาจากการ์ดคิว) -->
        <div class="journey-card" id="cardRelink">
            <h3><i class="fas fa-link"></i> ผูก / ปลดการจับคู่เอกสาร</h3>
                <div style="display:flex; gap:8px; flex-wrap:wrap; align-items:center;">
                    <input type="text" id="rlReceipt" placeholder="เลขใบเสร็จในระบบ เช่น REC260809004" style="padding:6px; min-width:250px; max-width:100%;" />
                    <input type="text" id="rlDoc" placeholder="เลขเอกสาร NextAcc เช่น REC-20260809-0004 (ว่าง = ปลดการผูก)" style="padding:6px; min-width:330px; max-width:100%;" />
                    <button type="button" class="btn-primary" onclick="relinkDoc()"><i class="fas fa-link"></i> ผูก / ปลด</button>
                    <button type="button" class="btn-default" onclick="inspectReceipt()"><i class="fas fa-search"></i> 🔎 ตรวจผู้ซื้อของใบนี้</button>
                    <button type="button" class="btn-warning" onclick="pushBuyerContact()"><i class="fas fa-user-check"></i> 📤 ส่งผู้ติดต่อขึ้น NextAcc</button>
                </div>
                <div style="font-size:11px; color:#888; margin-top:5px;">
                    แก้เฉพาะการจับคู่ — ไม่แตะบัญชี ไม่สร้าง/ลบเอกสารใด ๆ ·
                    เลขใบในระบบไม่มีขีดคั่น (REC260809004) เลขเอกสาร NextAcc มีขีด (REC-20260809-0004)
                </div>
                <div id="rlResult" style="margin-top:8px;"></div>
        </div>

        <!-- Deposit Lifecycle / สถานะเจ้าหนี้มัดจำ -->
        <div class="journey-card" id="cardDepositStatus">
            <h3><i class="fas fa-piggy-bank"></i> สถานะเจ้าหนี้ค่ามัดจำ (Advance Deposit Liability)</h3>
            <p style="font-size:13px; color:#666; margin-bottom:15px;">
                ติดตามมัดจำที่ลูกค้าจ่าย vs มัดจำที่ตัดออกจากเจ้าหนี้แล้ว (ตอน checkout / คืนเงิน / ริบ)<br/>
                สถานะ: <b>OPEN</b> = ยังเป็นหนี้สิน, <b>PARTIAL</b> = ตัดบางส่วน, <b>CLEARED</b> = ตัดครบ, <b>OVER_CLEARED</b> = ตัดเกิน (ตรวจสอบ)
            </p>
            <div style="display:flex; gap:10px; align-items:end; margin-bottom:15px; flex-wrap:wrap;">
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
            <p class="help-text" style="margin-top:8px;">ตัด/คืน/ริบมัดจำด้วยมือ: <a href="#" onclick="return accGoto('tools','dangerZone')">โซนอันตราย</a></p>
        </div>

        <!-- E-Tax Manual Operations -->
        <div class="journey-card">
            <h3><i class="fas fa-file-invoice"></i> e-Tax — สร้าง/ส่งอีเมลด้วยมือ</h3>
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

        <!-- ปรับสต๊อก / ตัดจำหน่าย — เครื่องมือเดิมปลดระวางแล้ว (CLAUDE.md ข้อ 23): ข้ามเหตุผลจ่ายออก/อนุมัติ/หลักฐาน/ภาษีขาย
             ของสินค้าที่ถือว่าขาย และลงซ้ำกับหน้าตรวจนับได้ → ใช้ Product/Stock. คงแค่ปุ่มส่งข้อมูลสินค้า (ไม่ลงบัญชี) -->
        <div class="journey-card">
            <h3><i class="fas fa-boxes"></i> ปรับสต๊อก / ตัดจำหน่าย — ย้ายไปหน้าตรวจนับแล้ว</h3>
            <p style="font-size:13px; color:#666; margin-bottom:12px;">
                เครื่องมือปรับสต๊อก/ตัดจำหน่ายแบบเดิมในหน้านี้<b>ปิดแล้ว</b> — ไม่มีเหตุผลการจ่ายออก การอนุมัติ หลักฐาน
                และภาษีขายของสินค้าที่ถือว่าขาย (สูญหาย/เจ้าของใช้เอง ม.77/1(8)) และอาจลงบัญชีซ้ำกับการเบิกในหน้าสต๊อก.
                ใช้หน้า <a href="<%= ResolveUrl("~/Product/Stock") %>"><b>ตรวจนับ / ปรับสต๊อก</b></a> แทน
            </p>
            <div style="display:flex; gap:10px; align-items:end; flex-wrap:wrap;">
                <a class="btn-primary" style="text-decoration:none;" href="<%= ResolveUrl("~/Product/Stock") %>"><i class="fas fa-boxes"></i> ไปหน้าตรวจนับ / ปรับสต๊อก</a>
                <div>
                    <label style="font-weight:600; display:block;">รหัสสินค้า (ส่งข้อมูลสินค้าขึ้น NextAcc)</label>
                    <input type="number" id="stockProductId" style="width:180px; padding:8px;" />
                </div>
                <button type="button" class="btn-success" onclick="stockProductSync()">
                    <i class="fas fa-sync"></i> ส่งข้อมูลสินค้า (ไม่ลงบัญชี)
                </button>
            </div>
            <div class="test-result" id="stockResult" style="margin-top:10px;"></div>
        </div>

        <div class="journey-card" id="cardGrni">
            <h3><i class="fas fa-scale-balanced"></i> ยอดคงค้างพักรับของ (GRNI)</h3>
                    <div style="margin-top:6px;">
                        <button type="button" class="btn-warning" onclick="grniReconcile()"><i class="fas fa-scale-balanced"></i> ตรวจยอดคงค้าง GRNI</button>
                    </div>
                    <div class="help-text" style="margin-top:6px;">รับของ (Product/In) เครดิต GRNI 21240 / ใบกำกับที่ OCR ล้าง GRNI + เพิ่มภาษีซื้อ. ยอดคงค้าง <b>ควรใกล้ 0</b> — บวก = ของมายังไม่วางบิล (ตามใบกำกับ), ลบ = วางบิลแล้วของยังไม่มา (ตามของ / ใบกำกับลงบัญชีผิด)</div>
                    <div class="test-result" id="grniResult"></div>
        </div>

        <!-- ⚠ โซนอันตราย: แก้/ลบ/กลับรายการบัญชีจริง — แยกไว้ท้ายแท็บ พับไว้ก่อน ทุกปุ่มมีหน้ายืนยัน -->
        <details class="danger-zone" id="dangerZone">
            <summary><i class="fas fa-exclamation-triangle"></i> โซนอันตราย — แก้/ลบ/กลับรายการบัญชีจริง (กดเพื่อเปิด)</summary>
            <div class="dz-body">
                <p class="dz-warn">ทุกปุ่มในส่วนนี้เปลี่ยนข้อมูลบัญชีจริงทั้งในระบบนี้และ NextAcc — ใช้เมื่อรู้สาเหตุแล้วเท่านั้น และทุกปุ่มจะถามยืนยันก่อน</p>

                <div class="dz-item">
                    <div class="dz-title"><i class="fas fa-trash-alt"></i> ตรวจ &amp; ลบใบที่หายจาก NextAcc</div>
                    <div class="help-text">ใบเสร็จ/ใบสำคัญจ่ายที่ส่งแล้ว ถ้า NextAcc ตอบว่าไม่มีเอกสารแล้ว (404) จะ<b>ลบในระบบนี้ถาวร</b> — ข้อผิดพลาดชั่วคราวจะข้าม</div>
                    <button type="button" class="btn-danger" onclick="reconcileDeleted()"><i class="fas fa-trash-alt"></i> ตรวจ &amp; ลบใบที่หายจาก NextAcc</button>
                </div>
                <div class="dz-item">
                    <div class="dz-title"><i class="fas fa-broom"></i> เก็บกวาดใบรับเงินที่ไม่มีใบต้นทาง</div>
                    <div class="help-text">ลบเฉพาะใบรับเงินบน NextAcc ที่ใบกำกับต้นทางถูกลบ/ยกเลิกแล้ว (ไม่กระทบรายการบัญชี)</div>
                    <button type="button" class="btn-warning" onclick="cleanupOrphanReceipts()"><i class="fas fa-broom"></i> เก็บกวาดใบรับเงิน</button>
                </div>
                <div class="dz-item">
                    <div class="dz-title"><i class="fas fa-undo"></i> กลับรายการมัดจำที่ค้างจากการส่งซ้ำ</div>
                    <div class="help-text">กลับเฉพาะรายการมัดจำที่ระบบนี้ลงเองและไม่ผูกเอกสาร — ยอดที่เหลือให้ผู้ทำบัญชีตรวจต่อ</div>
                    <button type="button" class="btn-danger" onclick="cleanupDepositDebris()"><i class="fas fa-undo"></i> กลับรายการมัดจำค้าง</button>
                </div>
                <div class="test-result" id="dangerResult"></div>

                <div class="dz-item">
                    <div class="dz-title"><i class="fas fa-eraser"></i> รีเซ็ตบัญชีการจอง (กลับรายการทั้งหมดของการจองเดียว)</div>
                    <div class="dz-row">
                        <input type="number" id="resetResId" placeholder="เลขการจอง เช่น 148936" min="1" />
                        <button type="button" class="btn-danger" onclick="resetReservation()"><i class="fas fa-eraser"></i> รีเซ็ตบัญชีการจอง</button>
                    </div>
                    <div class="help-text">กลับทุกรายการบัญชีที่ระบบนี้ลงให้การจองนี้ (มัดจำ/ใบกำกับ/ตัดมัดจำ/VAT) + ยกเลิก (void) เอกสารทุกใบของการจองบน NextAcc —
                        กดซ้ำไม่เบิ้ล แล้วส่งใหม่ได้สะอาด</div>
                    <div class="test-result" id="resetResResult"></div>
                </div>

                <div class="dz-item">
                    <div class="dz-title"><i class="fas fa-bolt"></i> มัดจำ: ทำรายการด้วยมือ (เมื่อระบบอัตโนมัติไม่ได้ทำ)</div>
                    <div class="dz-grid">
                        <div>
                            <label>เลขการจอง</label>
                            <input type="number" id="depositManualResId" />
                        </div>
                        <div>
                            <label>จำนวนเงิน (บาท)</label>
                            <input type="number" id="depositManualAmount" step="0.01" />
                        </div>
                        <div>
                            <label>เหตุผล (สำหรับริบ)</label>
                            <input type="text" id="depositManualReason" placeholder="ลูกค้าไม่มาเข้าพัก" />
                        </div>
                    </div>
                    <div class="dz-row">
                        <button type="button" class="btn-success" onclick="manualDepositAction('checkout')"><i class="fas fa-sign-out-alt"></i> ตัดมัดจำ (เช็คเอาท์)</button>
                        <button type="button" class="btn-warning" onclick="manualDepositAction('refund')"><i class="fas fa-undo"></i> คืนเงินมัดจำ</button>
                        <button type="button" class="btn-danger" onclick="manualDepositAction('forfeit')"><i class="fas fa-ban"></i> ริบมัดจำ</button>
                    </div>
                    <div class="test-result" id="depositManualResult"></div>
                </div>

                <div class="dz-item">
                    <div class="dz-title"><i class="fas fa-ban"></i> ยกเลิก (void) เอกสาร OTA ใน NextAcc</div>
                    <div class="help-text">ใช้เมื่อเอกสารรับเงิน OTA ที่ระบบสร้าง (อ้างอิง <code>OTADOC-เลขการจอง</code>) ผิด — กลับรายการบัญชีของเอกสาร
                        แล้วปลด/ล็อกเครื่องหมายให้ปลอดภัย. ติ๊ก "สร้างใหม่อัตโนมัติ" = สร้างใบใหม่รอบถัดไปตามยอด/การผูกบัญชีปัจจุบัน (เลขใหม่);
                        ไม่ติ๊ก = ไม่สร้างใหม่ (เช่น การจองถูกยกเลิก). เอกสารที่อยู่ในรายงานภาษีที่ยื่นแล้ว NextAcc จะไม่ยอมยกเลิก</div>
                    <div class="dz-row">
                        <input type="text" id="otaVoidResId" placeholder="เลขการจอง หรือ OTADOC-123" />
                        <input type="text" id="otaVoidReason" placeholder="เหตุผล (บันทึกใน log)" class="grow" />
                        <label class="chk"><input type="checkbox" id="otaVoidRepost" /> สร้างใหม่อัตโนมัติ</label>
                        <button type="button" class="btn-danger" onclick="voidOtaDoc()"><i class="fas fa-ban"></i> ยกเลิกเอกสาร OTA</button>
                    </div>
                    <div class="test-result" id="otaVoidResult"></div>
                </div>
            </div>
        </details>
        </section>

        <!-- ═══════════════ แท็บ 6: อีเมลจอง & LINE (ไม่ใช่งานบัญชี — แยกแท็บไว้) ═══════════════ -->
        <section class="acc-panel" id="tab-intake" role="tabpanel" aria-labelledby="tabbtn-intake" hidden>
        <!-- 📧 อ่านอีเมลจอง OTA (STAAH) → ลงจองอัตโนมัติ -->
        <div class="journey-card" id="cardEmail">
            <h3><i class="fas fa-envelope-open-text"></i> อ่านอีเมลจอง OTA (STAAH) → ลงจองอัตโนมัติ</h3>
            <p style="font-size:13px; color:#666; margin-bottom:15px;">
                อ่านอีเมลจอง STAAH (Agoda/Booking.com ฯลฯ) จาก Gmail แล้วลงจองในระบบให้อัตโนมัติ — แทนโปรแกรมภายนอกเดิม.<br/>
                เก็บตัวเลขทั้งสองตัวที่อีเมลให้มา — <b>refsell_amt</b> (ระดับ booking) และ <b>AMOUNT</b> (เรตต่อคืนต่อห้อง) —
    แยกกันไว้ให้กระทบยอดได้. <b>หมายเหตุ:</b> อีเมล STAAH <u>ไม่ได้บอกค่าคอมมิชชั่นหรือยอดที่ OTA จะโอนจริง</u>
    ยอดโอนต้องกระทบกับ statement ของ OTA อีกที (กดปุ่ม "ดูข้อมูลที่อ่านได้จากอีเมลจริง" เพื่อตรวจ).
                ต้องใช้ <b>Gmail App Password</b> (ไม่ใช่รหัสผ่านปกติ) และเปิด IMAP ในบัญชี Gmail.
            </p>

            <div style="display:flex; gap:12px; flex-wrap:wrap;">
                <div style="min-width:160px;">
                    <label style="display:block; font-weight:600; margin-bottom:6px;">สถานะ</label>
                    <select id="cfgEmailRsvEnabled" style="width:100%; padding:8px;">
                        <option value="false">ปิด</option>
                        <option value="true">เปิด — อ่านอีเมลอัตโนมัติ</option>
                    </select>
                </div>
                <div style="min-width:220px; flex:1;">
                    <label style="display:block; font-weight:600; margin-bottom:6px;">หลังลงจอง (สำคัญ)</label>
                    <select id="cfgEmailRsvCreateDocument" style="width:100%; padding:8px;">
                        <option value="false">ลงจองเฉย ๆ (เอกสารออกตอนเช็คอิน/เช็คเอาท์ตามปกติ)</option>
                        <option value="true">ยิงสร้างเอกสารบัญชีทันที (ต้องเปิด OTA settlement)</option>
                    </select>
                </div>
            </div>

            <div style="display:flex; gap:12px; flex-wrap:wrap; margin-top:12px;">
                <div style="min-width:220px; flex:2;">
                    <label style="display:block; font-weight:600; margin-bottom:6px;">IMAP Server</label>
                    <input type="text" id="cfgEmailRsvImapServer" placeholder="imap.gmail.com" style="width:100%; padding:8px;" />
                </div>
                <div style="min-width:90px;">
                    <label style="display:block; font-weight:600; margin-bottom:6px;">Port</label>
                    <input type="number" id="cfgEmailRsvImapPort" value="993" style="width:100%; padding:8px;" />
                </div>
                <div style="min-width:200px;">
                    <label style="display:block; font-weight:600; margin-bottom:6px;">
                        ⏱ ความถี่ในการดึงอีเมล (ทุกกี่นาที)
                    </label>
                    <input type="number" id="cfgEmailRsvPollMinutes" value="5" min="1" max="1440" style="width:100%; padding:8px;" />
                    <div style="font-size:11px; color:#777; margin-top:4px;">
                        ระบบดึงเองอัตโนมัติตามรอบนี้ (5 = ทุก 5 นาที) — ต่ำสุด 1 นาที
                    </div>
                </div>
            </div>

            <div style="display:flex; gap:12px; flex-wrap:wrap; margin-top:12px;">
                <div style="min-width:240px; flex:1;">
                    <label style="display:block; font-weight:600; margin-bottom:6px;">Gmail address</label>
                    <input type="text" id="cfgEmailRsvUsername" placeholder="booking@yourhotel.com" style="width:100%; padding:8px;" />
                </div>
                <div style="min-width:240px; flex:1;">
                    <label style="display:block; font-weight:600; margin-bottom:6px;">App Password <span id="cfgEmailRsvPwStatus" style="font-weight:400; font-size:12px;"></span></label>
                    <input type="password" id="cfgEmailRsvPassword" placeholder="Gmail App Password (16 ตัว)" autocomplete="new-password" style="width:100%; padding:8px;" />
                </div>
            </div>

            <div style="display:flex; gap:12px; flex-wrap:wrap; margin-top:12px;">
                <div style="min-width:160px; flex:1;">
                    <label style="display:block; font-weight:600; margin-bottom:6px;">Label อีเมลที่อ่านแล้ว</label>
                    <input type="text" id="cfgEmailRsvProcessedLabel" placeholder="STAAH-Processed" style="width:100%; padding:8px;" />
                </div>
                <div style="min-width:160px; flex:1;">
                    <label style="display:block; font-weight:600; margin-bottom:6px;">Label อีเมลที่ล้มเหลว</label>
                    <input type="text" id="cfgEmailRsvFailedLabel" placeholder="STAAH-Failed" style="width:100%; padding:8px;" />
                </div>
                <div style="min-width:160px; flex:1;">
                    <label style="display:block; font-weight:600; margin-bottom:6px;">Label อีเมลที่ไม่ใช่ใบจอง</label>
                    <input type="text" id="cfgEmailRsvIgnoredLabel" placeholder="STAAH-Other" style="width:100%; padding:8px;" />
                </div>
                <div style="min-width:280px; flex:1;">
                    <label style="display:block; font-weight:600; margin-bottom:6px;">
                        ถ้าอีเมลไม่บอกว่าใครเก็บเงิน ให้ถือว่า
                    </label>
                    <select id="cfgEmailRsvDefaultCollect" style="width:100%; padding:8px;">
                        <option value="CHANNEL">OTA เก็บเงินแล้ว — ลงมัดจำเต็มจำนวน</option>
                        <option value="HOTEL">เก็บเงินหน้างาน — ลงมัดจำ 0</option>
                    </select>
                    <div style="font-size:12px; color:#666; margin-top:5px; line-height:1.6;">
                        ใช้เฉพาะกรณีที่อ่านจากอีเมลไม่ได้ — คำที่ระบบรู้จักอยู่แล้ว เช่น
                        Hotel Collect / Pay at Hotel / Expedia Collect / Prepaid / VCC
                        จะถูกแยกให้อัตโนมัติและแจ้งเตือนว่าเดาให้เสมอ
                    </div>
                </div>
                <div style="min-width:130px;">
                    <label style="display:block; font-weight:600; margin-bottom:6px;">กรองผู้ส่ง (มีคำ)</label>
                    <input type="text" id="cfgEmailRsvFromContains" placeholder="staah" style="width:100%; padding:8px;" />
                </div>
            </div>

            <div style="display:flex; gap:12px; flex-wrap:wrap; margin-top:12px;">
                <div style="min-width:130px;">
                    <label style="display:block; font-weight:600; margin-bottom:6px;">คืนสูงสุด</label>
                    <input type="number" id="cfgEmailRsvMaxStayDays" value="30" min="1" style="width:100%; padding:8px;" />
                </div>
                <div style="min-width:160px;">
                    <label style="display:block; font-weight:600; margin-bottom:6px;">จองล่วงหน้าสูงสุด (วัน)</label>
                    <input type="number" id="cfgEmailRsvMaxDaysFuture" value="365" min="1" style="width:100%; padding:8px;" />
                </div>
                <div style="min-width:160px;">
                    <label style="display:block; font-weight:600; margin-bottom:6px;">แจ้ง Telegram</label>
                    <select id="cfgEmailRsvNotifyTelegram" style="width:100%; padding:8px;">
                        <option value="true">แจ้ง</option>
                        <option value="false">ไม่แจ้ง</option>
                    </select>
                </div>
                <div style="min-width:200px; flex:1;">
                    <label style="display:block; font-weight:600; margin-bottom:6px;">อีเมลที่ล้มเหลว</label>
                    <select id="cfgEmailRsvMoveFailed" style="width:100%; padding:8px;">
                        <option value="true">ย้ายไป folder Failed</option>
                        <option value="false">แค่ mark อ่านแล้ว</option>
                    </select>
                </div>
            </div>

            <div style="display:flex; gap:12px; flex-wrap:wrap; margin-top:12px;">
                <div style="min-width:220px; flex:1;">
                    <label style="display:block; font-weight:600; margin-bottom:6px;">ลองใหม่อีเมลที่ล้มเหลวอัตโนมัติ</label>
                    <select id="cfgEmailRsvRetryFailed" style="width:100%; padding:8px;">
                        <option value="true">ลองใหม่ทุกรอบจนสำเร็จ</option>
                        <option value="false">ไม่ลองใหม่ (ต้องลากอีเมลกลับเอง)</option>
                    </select>
                </div>
                <div style="min-width:160px;">
                    <label style="display:block; font-weight:600; margin-bottom:6px;">ลองใหม่ภายใน (ชั่วโมง)</label>
                    <input type="number" id="cfgEmailRsvRetryHours" value="72" min="1" style="width:100%; padding:8px;" />
                </div>
                <div style="min-width:260px; flex:1;">
                    <label style="display:block; font-weight:600; margin-bottom:6px;">ถ้าชื่อ Agency ไม่ตรงตาราง map</label>
                    <select id="cfgEmailRsvMapAnyChannel" style="width:100%; padding:8px;">
                        <option value="true">ยอม match ด้วยชื่อห้องอย่างเดียว (กันจองหล่น)</option>
                        <option value="false">ต้องตรง Agency เท่านั้น</option>
                    </select>
                </div>
            </div>

            <div style="display:flex; gap:12px; flex-wrap:wrap; margin-top:12px;">
                <div style="min-width:240px; flex:1;">
                    <label style="display:block; font-weight:600; margin-bottom:6px;">ลำดับห้องที่จัดให้ก่อน (ID คั่นจุลภาค)</label>
                    <input type="text" id="cfgEmailRsvRoomPriority" placeholder="เช่น 16,15,3,1,2,4,5 — เว้นว่าง = เรียงตามลำดับที่พัก" style="width:100%; padding:8px;" />
                </div>
                <div style="min-width:200px; flex:1;">
                    <label style="display:block; font-weight:600; margin-bottom:6px;">เบอร์สำรองเมื่ออีเมลไม่มีเบอร์</label>
                    <input type="text" id="cfgEmailRsvDefaultPhone" placeholder="เว้นว่าง = ใช้ OTA_{BookingID}" style="width:100%; padding:8px;" />
                </div>
                <div style="min-width:200px; flex:1;">
                    <label style="display:block; font-weight:600; margin-bottom:6px;">สถานะเมื่อยกเลิกจากอีเมล</label>
                    <select id="cfgEmailRsvCancelStatus" style="width:100%; padding:8px;">
                        <option value="ยกเลิก">ยกเลิก</option>
                        <option value="ยกเลิกคืนเงิน">ยกเลิกคืนเงิน (แบบโปรแกรมเดิม)</option>
                        <option value="ยกเลิกไม่คืนเงิน">ยกเลิกไม่คืนเงิน</option>
                    </select>
                </div>
            </div>

            <div style="margin-top:16px;">
                <button type="button" class="btn-primary" onclick="saveEmailIntake()"><i class="fas fa-save"></i> บันทึกการตั้งค่า</button>
                <button type="button" class="btn-default" onclick="testEmailIntake()"><i class="fas fa-plug"></i> ทดสอบการเชื่อมต่อ</button>
                <button type="button" class="btn-warning" onclick="runEmailIntake()"><i class="fas fa-download"></i> ดึงตอนนี้</button>
                <button type="button" class="btn-warning" onclick="recoverEmailBacklog()"><i class="fas fa-history"></i> กู้อีเมลย้อนหลัง</button>
                <button type="button" class="btn-default" onclick="getAction('emailIntakeTestTelegram','emailRsvResult')"><i class="fab fa-telegram"></i> ทดสอบข้อความ Telegram</button>
                <button type="button" class="btn-default" onclick="previewEmailIntake()"><i class="fas fa-eye"></i> ดูข้อมูลที่อ่านได้จากอีเมลจริง</button>
                <button type="button" class="btn-default" onclick="loadEmailLog()"><i class="fas fa-list-alt"></i> ดู logs ล่าสุด</button>
            </div>
            <div class="help-text" style="margin-top:8px;">
                ⓘ เปิด IMAP: Gmail → Settings → Forwarding and POP/IMAP → Enable IMAP. สร้าง App Password: Google Account → Security → 2-Step Verification → App passwords.
                ระบบดึงอัตโนมัติทุก N นาทีตามที่ตั้ง (อาศัย background timer เดียวกับ accounting sync). dedup ด้วย Booking ID — รันคู่โปรแกรมเดิมได้ไม่สร้างซ้ำ.
            </div>

            <!-- 🔎 ตรวจว่าทำไมอีเมลลงจองไม่ได้ (map ห้อง / ห้องว่าง) -->
            <div style="margin-top:16px; padding:12px; background:#f7f9fc; border:1px solid #dde5ef; border-radius:8px;">
                <div style="font-weight:600; margin-bottom:8px;">
                    <i class="fas fa-stethoscope"></i> ตรวจสอบ mapping / ห้องว่าง (ทำไมอีเมลลงจองไม่สำเร็จ)
                </div>
                <div style="display:flex; gap:10px; flex-wrap:wrap; align-items:flex-end;">
                    <div style="min-width:170px; flex:1;">
                        <label style="display:block; font-size:12px; margin-bottom:4px;">Channel Name (ในอีเมล)</label>
                        <input type="text" id="dgChannel" placeholder="Booking.com" style="width:100%; padding:8px;" />
                    </div>
                    <div style="min-width:190px; flex:1;">
                        <label style="display:block; font-size:12px; margin-bottom:4px;">ROOM TYPE (ในอีเมล)</label>
                        <input type="text" id="dgRoomType" placeholder="Nordic Tent" style="width:100%; padding:8px;" />
                    </div>
                    <div style="min-width:140px;">
                        <label style="display:block; font-size:12px; margin-bottom:4px;">เช็คอิน</label>
                        <input type="date" id="dgCheckin" style="width:100%; padding:8px;" />
                    </div>
                    <div style="min-width:140px;">
                        <label style="display:block; font-size:12px; margin-bottom:4px;">เช็คเอาท์</label>
                        <input type="date" id="dgCheckout" style="width:100%; padding:8px;" />
                    </div>
                    <div style="min-width:80px;">
                        <label style="display:block; font-size:12px; margin-bottom:4px;">กี่ห้อง</label>
                        <input type="number" id="dgRooms" value="1" min="1" style="width:100%; padding:8px;" />
                    </div>
                    <div style="min-width:80px;">
                        <label style="display:block; font-size:12px; margin-bottom:4px;">กี่คน</label>
                        <input type="number" id="dgAdults" value="1" min="1" style="width:100%; padding:8px;" />
                    </div>
                    <div>
                        <button type="button" class="btn-default" onclick="diagnoseEmailIntake()"><i class="fas fa-search"></i> ตรวจสอบ</button>
                    </div>
                </div>
                <div class="help-text" style="margin-top:6px;">
                    บอกว่า map ไปห้องจริงห้องไหนบ้าง และห้องที่ไม่ว่างติดใบจองเลขที่เท่าไร — ใช้เงื่อนไขเดียวกับตัวอ่านอีเมลเป๊ะ ๆ (ไม่บันทึกอะไร)
                </div>
            </div>

            <div class="test-result" id="emailRsvResult"></div>
            <div id="emailRsvLog" style="margin-top:12px;"></div>
        </div>

        <!-- 📱 ส่งรูปตารางจองรายวันเข้า LINE -->
        <div class="journey-card">
            <h3><i class="fab fa-line" style="color:#06C755;"></i> ส่งรูปตารางจองรายวันเข้า LINE (อัตโนมัติ)</h3>
            <p style="font-size:13px; color:#666; margin-bottom:15px;">
                render หน้า <b>DisplayToday</b> เป็นรูป แล้ว push เข้า LINE ให้อัตโนมัติทุกวันตามเวลาที่ตั้ง — แทนโปรแกรมภายนอกเดิม.
                ใช้ token ของ <b>LINE OA เดิม</b> (ตั้งไว้แล้วในระบบ) หรือระบุเฉพาะงานนี้ก็ได้.
                ความสูงรูปวัดจากเนื้อหาจริง (ไม่ตัด/ไม่เหลือขอบ).
            </p>

            <div style="display:flex; gap:12px; flex-wrap:wrap;">
                <div style="min-width:150px;">
                    <label style="display:block; font-weight:600; margin-bottom:6px;">สถานะ</label>
                    <select id="cfgLineDailyEnabled" style="width:100%; padding:8px;">
                        <option value="false">ปิด</option>
                        <option value="true">เปิด — ส่งอัตโนมัติ</option>
                    </select>
                </div>
                <div style="min-width:120px;">
                    <label style="display:block; font-weight:600; margin-bottom:6px;">เวลาส่ง (HH:mm)</label>
                    <input type="time" id="cfgLineDailySendTime" value="08:00" style="width:100%; padding:8px;" />
                </div>
                <div style="min-width:220px; flex:1;">
                    <label style="display:block; font-weight:600; margin-bottom:6px;">ข้อความประกอบ (ไม่บังคับ)</label>
                    <input type="text" id="cfgLineDailyCaption" placeholder="เว้นว่าง = ส่งเฉพาะรูป (แนะนำ)" style="width:100%; padding:8px;" />
                    <div class="help-text" style="margin-top:4px;">เว้นว่าง = ส่งเฉพาะรูป · ใส่ข้อความจะส่งเป็นอีก 1 ข้อความก่อนรูป ({date} = วันที่ไทย)</div>
                </div>
            </div>

            <div style="margin-top:12px;">
                <label style="display:block; font-weight:600; margin-bottom:6px;">ผู้รับ (LINE userId / groupId / roomId) — คั่นด้วย comma หรือขึ้นบรรทัดใหม่</label>
                <textarea id="cfgLineDailyRecipients" rows="2" placeholder="Cxxxxxxxx...&#10;Uxxxxxxxx..." style="width:100%; padding:8px; font-family:monospace;"></textarea>
            </div>

            <div style="display:flex; gap:12px; flex-wrap:wrap; margin-top:12px;">
                <div style="min-width:110px;">
                    <label style="display:block; font-weight:600; margin-bottom:6px;">กว้าง (px)</label>
                    <input type="number" id="cfgLineDailyImageWidth" value="1600" style="width:100%; padding:8px;" />
                </div>
                <div style="min-width:130px;">
                    <label style="display:block; font-weight:600; margin-bottom:6px;">สูงพื้นฐาน (px)</label>
                    <input type="number" id="cfgLineDailyImageHeight" value="700" style="width:100%; padding:8px;" />
                </div>
                <div style="min-width:150px;">
                    <label style="display:block; font-weight:600; margin-bottom:6px;">ปรับสูงอัตโนมัติ</label>
                    <select id="cfgLineDailyAutoHeight" style="width:100%; padding:8px;">
                        <option value="true">วัดจากเนื้อหาจริง</option>
                        <option value="false">ใช้ค่าคงที่</option>
                    </select>
                </div>
                <div style="min-width:120px;">
                    <label style="display:block; font-weight:600; margin-bottom:6px;">คุณภาพ JPEG</label>
                    <input type="number" id="cfgLineDailyJpegQuality" value="90" min="1" max="100" style="width:100%; padding:8px;" />
                </div>
                <div style="min-width:150px;">
                    <label style="display:block; font-weight:600; margin-bottom:6px;">ขนาดตัวอักษร (%)</label>
                    <input type="number" id="cfgLineDailyFontScale" value="100" min="100" max="300" step="10" style="width:100%; padding:8px;" />
                    <div class="help-text" style="margin-top:4px;">100 = ตามหน้าเว็บ · 150 = ใหญ่ขึ้น 1.5 เท่า (อ่านง่ายบนมือถือ)</div>
                </div>
            </div>

            <div style="margin-top:12px;">
                <label style="display:block; font-weight:600; margin-bottom:6px;">URL หน้าที่จะ render</label>
                <input type="text" id="cfgLineDailySourceUrl" placeholder="https://taketimebangphra.com/displaytoday" style="width:100%; padding:8px;" />
            </div>

            <div style="display:flex; gap:12px; flex-wrap:wrap; margin-top:12px;">
                <div style="min-width:240px; flex:1;">
                    <label style="display:block; font-weight:600; margin-bottom:6px;">URL สาธารณะของโฟลเดอร์รูป (HTTPS — LINE ต้องเข้าถึงได้)</label>
                    <input type="text" id="cfgLineDailyPublicBaseUrl" placeholder="https://taketimebangphra.com/Images/Reservation" style="width:100%; padding:8px;" />
                </div>
                <div style="min-width:200px; flex:1;">
                    <label style="display:block; font-weight:600; margin-bottom:6px;">โฟลเดอร์เก็บรูป (~/... หรือ physical path)</label>
                    <input type="text" id="cfgLineDailyImageFolder" placeholder="~/Images/Reservation" style="width:100%; padding:8px;" />
                </div>
            </div>

            <div style="margin-top:12px;">
                <label style="display:block; font-weight:600; margin-bottom:6px;">
                    LINE token เฉพาะงานนี้ (ไม่บังคับ) <span id="cfgLineDailyTokenStatus" style="font-weight:400; font-size:12px;"></span>
                </label>
                <input type="password" id="cfgLineDailyTokenOverride" autocomplete="new-password" placeholder="เว้นว่าง = ใช้ token ของ LINE OA เดิม (ใส่ &quot;-&quot; เพื่อล้าง)" style="width:100%; padding:8px;" />
            </div>

            <div style="margin-top:16px;">
                <button type="button" class="btn-primary" onclick="saveLineDaily()"><i class="fas fa-save"></i> บันทึกการตั้งค่า</button>
                <button type="button" class="btn-default" onclick="previewLineDaily()"><i class="fas fa-image"></i> พรีวิวรูป</button>
                <button type="button" class="btn-warning" onclick="sendLineDaily()"><i class="fab fa-line"></i> ส่งตอนนี้</button>
                <button type="button" class="btn-default" onclick="testLineDaily()"><i class="fas fa-vial"></i> ทดสอบข้อความ</button>
                <button type="button" class="btn-default" onclick="loadLineDailyLog()"><i class="fas fa-list-alt"></i> ดู logs</button>
            </div>
            <div class="help-text" style="margin-top:8px;">
                ⓘ ต้องรันบน Windows+IIS ที่ติดตั้งฟอนต์ไทย และตั้ง App Pool → Load User Profile = True (GDI+ ต้องใช้).
                LINE รับเฉพาะรูปที่เข้าถึงได้ผ่าน HTTPS สาธารณะ. หา groupId: เชิญ OA เข้ากลุ่มแล้วดู source.groupId จาก webhook.
            </div>
            <div class="test-result" id="lineDailyResult"></div>
            <div id="lineDailyPreview" style="margin-top:10px;"></div>
            <div id="lineDailyLog" style="margin-top:12px;"></div>
        </div>
        </section>

        <!-- ═══════════════ แท็บ 7: บันทึก ═══════════════ -->
        <section class="acc-panel" id="tab-log" role="tabpanel" aria-labelledby="tabbtn-log" hidden>
        <div class="journey-card" id="cardLog">
            <h3><i class="fas fa-history"></i> บันทึกการเชื่อม NextAcc ล่าสุด</h3>
            <p style="font-size:13px; color:#666; margin-bottom:12px;">
                log การส่งข้อมูลและการเปลี่ยนค่าตั้ง (อ่านอย่างเดียว) — ดู log ของรายการเดียวได้จากปุ่ม "Log" ในแท็บคิว
            </p>
            <div class="log-filter">
                <select id="logHours" aria-label="ช่วงเวลา">
                    <option value="6">6 ชั่วโมงล่าสุด</option>
                    <option value="24" selected>24 ชั่วโมงล่าสุด</option>
                    <option value="72">3 วันล่าสุด</option>
                    <option value="168">7 วันล่าสุด</option>
                </select>
                <select id="logKind" aria-label="ชนิด">
                    <option value="">ทุกชนิด</option>
                    <option value="sync">การส่งข้อมูล</option>
                    <option value="config">การเปลี่ยนค่าตั้ง</option>
                </select>
                <input type="search" id="logQuery" placeholder="คำค้น เช่น เลขใบเสร็จ / เลขจอง" maxlength="60"
                       onkeydown="if(event.key==='Enter'){event.preventDefault();loadRecentLog();}" />
                <button type="button" class="btn-primary" onclick="loadRecentLog()"><i class="fas fa-search"></i> แสดง</button>
            </div>
            <div id="recentLogResult" class="scroll-x" style="margin-top:12px;"><div class="help-text">กด "แสดง" เพื่อโหลด</div></div>
        </div>

        <!-- Guest Journey Accounting Map -->
        <div class="journey-card">
            <h3><i class="fas fa-route"></i> เส้นทางผู้เข้าพัก — รายการบัญชีที่เกิดขึ้น (อ้างอิง)</h3>
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
        </section>

        <!-- Log Detail Modal (ดู log AccountingSync เต็ม ไม่ตัด) -->
        <div id="logModal" style="display:none; position:fixed; inset:0; background:rgba(0,0,0,0.5); z-index:9999;" onclick="if(event.target===this)closeLogModal()">
            <div style="max-width:900px; margin:40px auto; background:#fff; border-radius:8px; box-shadow:0 8px 30px rgba(0,0,0,0.3); max-height:85vh; display:flex; flex-direction:column;">
                <div style="padding:14px 18px; border-bottom:1px solid #eee; display:flex; justify-content:space-between; align-items:center;">
                    <strong id="logModalTitle" style="font-size:15px;"><i class="fas fa-file-alt"></i> Log รายละเอียด</strong>
                    <button type="button" onclick="closeLogModal()" style="border:none; background:none; font-size:22px; cursor:pointer; color:#888;">&times;</button>
                </div>
                <div id="logModalBody" style="padding:16px 18px; overflow:auto;">
                    <div style="text-align:center; color:#999;">กำลังโหลด...</div>
                </div>
            </div>
        </div>
    </div>

    <!-- ═══════════════ ข้อ 26: วิซาร์ดตั้งค่า 6 ขั้น (Owner / Admin บัญชี) ═══════════════
         ทุกขั้นเรียก action เดิมของหน้านี้ (saveApi, testApi, syncAccounts, syncWallets, updatePaidHowAccount, updateMapping,
         saveSyncSettings, setPreflightMode, healthCheck) — ไม่มีตรรกะซ้ำ. จำขั้นที่ทำถึงด้วย saveWizardStep (ทำต่อข้ามเครื่องได้) -->
    <div id="accWizard" class="wz-overlay" hidden>
        <div class="wz-box" role="dialog" aria-modal="true" aria-labelledby="wzTitle">
            <div class="wz-head">
                <h2 id="wzTitle"><i class="fas fa-magic"></i> ตั้งค่าเชื่อม NextAcc ทีละขั้น</h2>
                <button type="button" class="wz-x" onclick="wzClose()" aria-label="ปิด">&times;</button>
            </div>
            <ol class="wz-steps" id="wzSteps">
                <li data-step="1"><button type="button" onclick="wzGo(1)"><span class="n">1</span><span class="t">ใส่คีย์</span></button></li>
                <li data-step="2"><button type="button" onclick="wzGo(2)"><span class="n">2</span><span class="t">ดึงผังบัญชี</span></button></li>
                <li data-step="3"><button type="button" onclick="wzGo(3)"><span class="n">3</span><span class="t">ผูกแหล่งเงิน</span></button></li>
                <li data-step="4"><button type="button" onclick="wzGo(4)"><span class="n">4</span><span class="t">ผูกบัญชีสำคัญ</span></button></li>
                <li data-step="5"><button type="button" onclick="wzGo(5)"><span class="n">5</span><span class="t">เลือกนโยบาย</span></button></li>
                <li data-step="6"><button type="button" onclick="wzGo(6)"><span class="n">6</span><span class="t">ตรวจ &amp; สรุป</span></button></li>
            </ol>
            <div class="wz-progress" aria-hidden="true"><div id="wzBar"></div></div>

            <div class="wz-body">
                <!-- ขั้น 1 -->
                <section class="wz-pane" data-step="1">
                    <h3>ขั้นที่ 1 — ใส่คีย์และทดสอบการเชื่อมต่อ</h3>
                    <p class="wz-lead">NextAcc มีคีย์ 2 ชนิด: <b>คีย์เชื่อมระบบ</b> (จำเป็น — ใช้ส่งเอกสารทุกชนิด) และ <b>คีย์บริษัท</b> (ไม่บังคับ — ใส่เมื่ออยากให้งานระดับบริษัท
                        เช่น สแกนเอกสาร บังคับแหล่งเงิน e-Tax ใช้คีย์ของตัวเอง). มีแค่คีย์เชื่อมระบบตัวเดียวก็ทำงานได้ครบ</p>
                    <div class="wz-form">
                        <label for="wzBaseUrl">ที่อยู่เซิร์ฟเวอร์ NextAcc</label>
                        <input type="text" id="wzBaseUrl" placeholder="https://api.nexaacc.com" />
                        <label for="wzApiKey">คีย์เชื่อมระบบ <span style="color:#c0392b;">*</span> <small>(NextAcc → Integrations, ขึ้นต้น int_)</small></label>
                        <input type="password" id="wzApiKey" autocomplete="new-password" placeholder="int_..." />
                        <div class="key-hint" id="wzApiKeyHint"></div>
                        <label for="wzCompanyApiKey">คีย์บริษัท <small>(ไม่บังคับ — NextAcc → API Keys, ขึ้นต้น acc_)</small></label>
                        <input type="password" id="wzCompanyApiKey" autocomplete="new-password" placeholder="เว้นว่าง = ใช้คีย์เชื่อมระบบตัวเดียว" />
                        <div class="key-hint" id="wzCompanyApiKeyHint"></div>
                        <label for="wzCompanyId">รหัสบริษัทใน NextAcc</label>
                        <input type="text" id="wzCompanyId" placeholder="00000000-0000-0000-0000-000000000000" />
                    </div>
                    <div class="btn-row">
                        <button type="button" class="btn-success" onclick="wzSaveKeys()"><i class="fas fa-plug"></i> บันทึกและทดสอบ</button>
                        <button type="button" class="btn-secondary" onclick="wzTestOnly()">ทดสอบคีย์ที่บันทึกไว้</button>
                    </div>
                    <div class="test-result" id="wzKeyResult"></div>
                </section>

                <!-- ขั้น 2 -->
                <section class="wz-pane" data-step="2" hidden>
                    <h3>ขั้นที่ 2 — ดึงผังบัญชี บัญชีธนาคาร และกระเป๋าเงิน</h3>
                    <p class="wz-lead">ดึงรายการบัญชีจาก NextAcc มาไว้ในระบบนี้ เพื่อใช้เลือกในขั้นถัดไป (ไม่แก้อะไรใน NextAcc)</p>
                    <div class="wz-stats" id="wzPullStats"></div>
                    <div class="btn-row">
                        <button type="button" class="btn-success" onclick="wzPull()"><i class="fas fa-cloud-download-alt"></i> ดึงจาก NextAcc</button>
                        <button type="button" class="btn-secondary" onclick="wzPullAccountsOnly()">ดึงเฉพาะผังบัญชี</button>
                    </div>
                    <div class="test-result" id="wzPullResult"></div>
                </section>

                <!-- ขั้น 3 -->
                <section class="wz-pane" data-step="3" hidden>
                    <h3>ขั้นที่ 3 — ผูกแหล่งเงินกับบัญชี NextAcc</h3>
                    <p class="wz-lead">แต่ละวิธีรับ/จ่ายเงิน (เงินสด, โอนเข้าธนาคาร, เกตเวย์, OTA …) ต้องชี้บัญชีเงินที่ถูกต้อง ไม่งั้น NextAcc จะเดาเป็นเงินสด.
                        แถวที่ยังไม่ผูกอยู่ด้านบน — ระบบเลือก<b>คำแนะนำ</b>ไว้ให้จากชื่อ/ชนิดช่องทาง ตรวจแล้วกดบันทึก</p>
                    <div class="wz-summary" id="wzPhSummary"></div>
                    <div class="scroll-x"><table class="queue-table wz-table">
                        <thead><tr><th>แหล่งเงิน</th><th style="min-width:240px;">บัญชี NextAcc</th><th>สถานะ</th><th></th></tr></thead>
                        <tbody id="wzPhBody"><tr><td colspan="4" class="muted">กำลังโหลด...</td></tr></tbody>
                    </table></div>
                    <div class="btn-row">
                        <button type="button" class="btn-success" onclick="wzSaveAllPaidHow()"><i class="fas fa-save"></i> บันทึกทุกแถวที่เปลี่ยน</button>
                        <button type="button" class="btn-secondary" onclick="wzLoadPaidHow()"><i class="fas fa-sync"></i> โหลดใหม่</button>
                    </div>
                    <div class="test-result" id="wzPhResult"></div>
                </section>

                <!-- ขั้น 4 -->
                <section class="wz-pane" data-step="4" hidden>
                    <h3>ขั้นที่ 4 — ผูกบัญชีสำคัญ</h3>
                    <p class="wz-lead">บัญชีที่ระบบใช้ลงรายได้ ภาษี มัดจำ ลูกหนี้/เจ้าหนี้ สต๊อก ฯลฯ — ระบบแนะนำบัญชีจากผังของ NextAcc (รหัสมาตรฐาน หรือชื่อที่ตรง)
                        เรียงข้อที่<b>จำเป็นกับฟีเจอร์ที่เปิดอยู่</b>ก่อน</p>
                    <div class="wz-summary" id="wzMapSummary"></div>
                    <div class="scroll-x"><table class="queue-table wz-table">
                        <thead><tr><th>ใช้ทำอะไร</th><th style="min-width:240px;">บัญชี NextAcc</th><th>สถานะ</th><th></th></tr></thead>
                        <tbody id="wzMapBody"><tr><td colspan="4" class="muted">กำลังโหลด...</td></tr></tbody>
                    </table></div>
                    <div class="btn-row">
                        <button type="button" class="btn-success" onclick="wzSaveAllMappings()"><i class="fas fa-save"></i> บันทึกทุกแถวที่เปลี่ยน</button>
                        <button type="button" class="btn-secondary" onclick="wzLoadMappings()"><i class="fas fa-sync"></i> โหลดใหม่</button>
                        <button type="button" class="btn-secondary" onclick="wzClose(); accGoto('accounts','mappingTable');">ดูการผูกทั้งหมด</button>
                    </div>
                    <div class="test-result" id="wzMapResult"></div>
                </section>

                <!-- ขั้น 5 -->
                <section class="wz-pane" data-step="5" hidden>
                    <h3>ขั้นที่ 5 — เลือกนโยบาย</h3>
                    <div class="wz-policy">
                        <fieldset>
                            <legend>VAT ของเงินมัดจำ <small>(ผู้ทำบัญชีเป็นผู้เลือก)</small></legend>
                            <label class="wz-opt"><input type="radio" name="wzDepMode" value="CHECKOUT" />
                                <span><b>รับรู้ตอนเช็คเอาท์</b> — มัดจำเป็นหนี้สินเต็มก้อน ใบกำกับใบเดียวเต็มยอดตอนเช็คเอาท์ <em>(แนะนำสำหรับโรงแรม)</em></span></label>
                            <label class="wz-opt"><input type="radio" name="wzDepMode" value="RECEIPT_DEFER" />
                                <span><b>แยก VAT ตอนรับมัดจำ แต่พักไว้</b> — VAT เข้า ภ.พ.30 ตอนเช็คเอาท์</span></label>
                            <label class="wz-opt"><input type="radio" name="wzDepMode" value="RECEIPT" />
                                <span><b>เสีย VAT ทันทีที่รับมัดจำ</b> (ม.78/1 เคร่ง) — VAT เข้า ภ.พ.30 เดือนที่รับเงิน; ลูกค้าอาจได้เอกสารภาษี 2 ใบ</span></label>
                            <div class="je-preview" id="wzJePreview" aria-live="polite"></div>
                        </fieldset>
                        <fieldset>
                            <legend>ใบลดหนี้ / คืนเงินลูกค้า</legend>
                            <label class="wz-opt"><input type="checkbox" id="wzCnEnabled" /> <span>เปิดระบบใบลดหนี้ (ยกเลิก/คืนเงินผ่านหน้าใบลดหนี้)</span></label>
                            <label class="wz-opt"><input type="checkbox" id="wzCnApproval" /> <span>ต้องให้ Owner / Admin บัญชีอนุมัติก่อนคืนเงิน <em>(แนะนำ)</em></span></label>
                            <div class="help-text" id="wzCnNote"></div>
                        </fieldset>
                        <fieldset>
                            <legend>ตัวตรวจก่อนส่ง</legend>
                            <label class="wz-opt"><input type="radio" name="wzPfMode" value="OFF" /> <span><b>ปิด</b> — ไม่ตรวจ (แบบเดิม)</span></label>
                            <label class="wz-opt"><input type="radio" name="wzPfMode" value="WARN" /> <span><b>เตือน</b> — แจ้งปัญหาแต่ยังบันทึกได้ <em>(แนะนำช่วงแรก)</em></span></label>
                            <label class="wz-opt"><input type="radio" name="wzPfMode" value="BLOCK" /> <span><b>บล็อก</b> — หยุดบันทึกเมื่อข้อมูลผิด (Owner/Admin บัญชียืนยันข้ามได้พร้อมเหตุผล)</span></label>
                        </fieldset>
                        <fieldset>
                            <legend>ตรวจสลิปโอน</legend>
                            <div class="help-text" style="font-size:12.5px;">ตั้งที่หน้าของตัวเอง — ตรวจยอดสลิปก่อนออกใบเสร็จ กันสลิปซ้ำ/ยอดไม่ตรง</div>
                            <a class="btn-secondary" style="display:inline-block; margin-top:6px; text-decoration:none;" target="_blank" href="<%= ResolveUrl("~/Admin/Settings/SlipCheckSettings") %>"><i class="fas fa-receipt"></i> เปิดหน้าตั้งค่าตรวจสลิป</a>
                        </fieldset>
                    </div>
                    <div class="btn-row">
                        <button type="button" class="btn-success" onclick="wzSavePolicy()"><i class="fas fa-save"></i> บันทึกนโยบาย</button>
                    </div>
                    <div class="test-result" id="wzPolicyResult"></div>
                </section>

                <!-- ขั้น 6 -->
                <section class="wz-pane" data-step="6" hidden>
                    <h3>ขั้นที่ 6 — ตรวจสุขภาพและสรุป</h3>
                    <div class="wz-summary" id="wzFinalSummary"></div>
                    <div class="btn-row">
                        <button type="button" class="btn-primary" onclick="wzRunHealth()"><i class="fas fa-stethoscope"></i> ตรวจสุขภาพอีกครั้ง</button>
                    </div>
                    <div id="wzHealthResult" style="margin-top:10px;"></div>
                    <div class="btn-row">
                        <button type="button" class="btn-success" onclick="wzFinish()"><i class="fas fa-flag-checkered"></i> เสร็จสิ้น — ไปดูคิว</button>
                    </div>
                    <div class="test-result" id="wzFinishResult"></div>
                </section>
            </div>

            <div class="wz-foot">
                <button type="button" class="btn-secondary" id="wzPrev" onclick="wzGo(wzState.step - 1)"><i class="fas fa-arrow-left"></i> ก่อนหน้า</button>
                <span class="wz-stepinfo" id="wzStepInfo"></span>
                <button type="button" class="btn-primary" id="wzNext" onclick="wzGo(wzState.step + 1)">ถัดไป <i class="fas fa-arrow-right"></i></button>
            </div>
        </div>
    </div>

    <asp:HiddenField ID="hfConfigData" runat="server" />
    <asp:HiddenField ID="hfQueueData" runat="server" />

    <script>
        var pageUrl = '<%= ResolveUrl("~/Admin/Settings/AccountingIntegration") %>';
        var queueState = { page: 1, pageSize: 20, status: '', cls: '', q: '', totalPages: 1 };
        var appBase = '<%= ResolveUrl("~/") %>'.replace(/\/$/, '');

        // ลิงก์จาก Telegram/หน้าอื่น: ?cls=DATA / ?status=FAILED / ?q=REC… → ตั้งตัวกรองคิวก่อนโหลด
        function initQueueFromQuery() {
            try {
                var q = new URLSearchParams(window.location.search);
                var st = (q.get('status') || '').toUpperCase(), cls = (q.get('cls') || '').toUpperCase(), qq = q.get('q') || '';
                if (/^[A-Z_]{3,20}$/.test(st)) queueState.status = st;
                else if (/^[A-Z]{3,12}$/.test(cls)) queueState.cls = cls;
                if (qq) { queueState.q = qq.substring(0, 100); var box = document.getElementById('queueSearch'); if (box) box.value = queueState.q; }
            } catch (e) { }
        }

        function searchQueue() {
            var box = document.getElementById('queueSearch');
            queueState.q = box ? box.value.trim() : '';
            queueState.page = 1;
            loadQueueData();
        }

        function clearQueueSearch() {
            var box = document.getElementById('queueSearch');
            if (box) box.value = '';
            queueState.q = '';
            queueState.page = 1;
            loadQueueData();
        }

        document.addEventListener('DOMContentLoaded', function () {
            loadConfigData();
            initQueueFromQuery();
            loadQueueData();
            preloadNexaaccAccounts();
            focusFromQuery();
            window.accCfgLoaded = !!window.accCfgLoaded;
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
                document.getElementById('cfgPosDailyRollup').value = cfg.posDailyRollup ? 'true' : 'false';
                document.getElementById('cfgRoomServiceRevenue').value = cfg.roomServiceRevenue ? 'true' : 'false';
                document.getElementById('cfgEtaxRdWatch').value = cfg.etaxRdWatch ? 'true' : 'false';
                document.getElementById('cfgEtaxRdFrom').value = cfg.etaxRdFrom || '';
                document.getElementById('cfgOtaRoomRevenue').value = cfg.otaRoomRevenue ? 'true' : 'false';
                var sdj = document.getElementById('cfgSecurityDepositJournal');
                if (sdj) { sdj.value = cfg.securityDepositJournal ? 'true' : 'false'; sdj.setAttribute('data-loaded', '1'); }
                // โหมดเอกสาร OTA: ส่งกลับตอนบันทึกเฉพาะเมื่อโหลดค่าจริงแล้ว (กันรีเซ็ตนโยบายเงียบ ๆ)
                var odmSel = document.getElementById('cfgOtaDocumentMode');
                if (odmSel && cfg.otaDocumentMode) { odmSel.value = cfg.otaDocumentMode; odmSel.setAttribute('data-loaded', '1'); }
                document.getElementById('cfgStockQtySync').value = cfg.stockQtySync ? 'true' : 'false';
                document.getElementById('cfgStockQtyPull').value = cfg.stockQtyPull ? 'true' : 'false';
                document.getElementById('cfgAttachFiles').value = cfg.attachFiles ? 'true' : 'false';
                if (cfg.depositVatRecognition) document.getElementById('cfgDepositVatRecognition').value = cfg.depositVatRecognition;
                document.getElementById('cfgDepositDeferOutputVat').checked = !!cfg.depositDeferOutputVat;
                var cnEn = document.getElementById('cfgCreditNoteEnabled'), cnAp = document.getElementById('cfgCreditNoteRequireApproval');
                if (cnEn && cnAp) {
                    cnEn.checked = !!cfg.creditNoteEnabled; cnAp.checked = !!cfg.creditNoteRequireApproval;
                    cnEn.setAttribute('data-loaded', '1'); cnAp.setAttribute('data-loaded', '1');
                    if (!cfg.creditNoteTableReady) {
                        cnEn.disabled = true; cnAp.disabled = true;
                        cnEn.removeAttribute('data-loaded'); cnAp.removeAttribute('data-loaded');
                        var cnHelp = document.getElementById('cfgCreditNoteHelp');
                        if (cnHelp) { cnHelp.textContent = 'ยังไม่ได้อัปเดตฐานข้อมูลส่วนใบลดหนี้ — ให้ผู้ดูแลระบบอัปเดตก่อนจึงจะเปิดใช้ได้ (ระหว่างนี้ปุ่มยกเลิกการจองแบบเดิมทำงานตามปกติ)'; cnHelp.title = 'สคริปต์ฐานข้อมูล: Database/PHASE19_Migration_33_Credit_Notes.sql'; }
                    }
                }
                document.getElementById('cfgDepositDrivesJournal').checked = !!cfg.depositDrivesJournal;
                document.getElementById('cfgDepositDrivesJournalRef').checked = !!cfg.depositDrivesJournalRef;
                document.getElementById('cfgAutoRecoverDeposit').checked = !!cfg.autoRecoverDeposit;
                document.getElementById('cfgPostSyncVerify').checked = !!cfg.postSyncVerify;
                document.getElementById('cfgAutoReconcileDeposit').checked = !!cfg.autoReconcileDeposit;
                document.getElementById('cfgCashSaleUseReceipt').checked = !!cfg.cashSaleUseReceipt;
                document.getElementById('cfgCashSaleCompanyDoc').checked = !!cfg.cashSaleCompanyDoc;
                // หัวเอกสารขาย: ทำเครื่องหมายว่าโหลดค่าจริงแล้ว — saveSyncSettings ส่งค่านี้เฉพาะเมื่อโหลดสำเร็จ
                // (กันโหลด AJAX พลาด → dropdown ค้าง AUTO → กดบันทึกเรื่องอื่นแล้วนโยบายหัวเอกสารถูกรีเซ็ตเงียบ ๆ)
                var rhtSel = document.getElementById('cfgReceiptHeaderType');
                if (rhtSel && cfg.receiptHeaderType) { rhtSel.value = cfg.receiptHeaderType; rhtSel.setAttribute('data-loaded', '1'); }
                document.getElementById('cfgStockInUseGRNI').checked = !!cfg.stockInUseGRNI;
                document.getElementById('cfgStockInSkipJournal').checked = !!cfg.stockInSkipJournal;
                document.getElementById('cfgEtaxAutoGenerate').value = cfg.etaxAutoGenerate ? 'true' : 'false';
                document.getElementById('cfgEtaxAutoSign').value = cfg.etaxAutoSign ? 'true' : 'false';
                document.getElementById('cfgEtaxAutoSubmit').value = cfg.etaxAutoSubmit ? 'true' : 'false';
                document.getElementById('cfgEtaxAutoSendEmail').value = cfg.etaxAutoSendEmail ? 'true' : 'false';
                document.getElementById('cfgEtaxEmailSubject').value = cfg.etaxEmailSubject || '';
                document.getElementById('cfgEtaxEmailCc').value = cfg.etaxEmailCc || '';
                document.getElementById('cfgEtaxEmailBody').value = cfg.etaxEmailBody || '';
                document.getElementById('cfgEtaxEmailAttachPdf').value = cfg.etaxEmailAttachPdf ? 'true' : 'false';
                document.getElementById('cfgEtaxEmailAttachXml').value = cfg.etaxEmailAttachXml ? 'true' : 'false';
                document.getElementById('cfgEtaxEmailLocalOnly').value = cfg.etaxEmailLocalOnly ? 'true' : 'false';
                document.getElementById('cfgEtaxEmailFallback').value = cfg.etaxEmailFallback ? 'true' : 'false';
                var intStatus = document.getElementById('cfgApiKeyStatus');
                if (cfg.hasApiKey) {
                    document.getElementById('cfgApiKey').placeholder = '••••••••  (มี Integration Key อยู่แล้ว — ใส่ค่าใหม่เพื่อเปลี่ยน)';
                    intStatus.innerHTML = '<span style="color:#27ae60;">✓ ตั้งค่าแล้ว: <code>' + escapeHtml(cfg.apiKeyMask || '••••') + '</code></span>';
                } else {
                    intStatus.innerHTML = '<span style="color:#c0392b;">✗ ยังไม่ได้ตั้ง Integration Key (จำเป็น)</span>';
                }
                var accStatus = document.getElementById('cfgCompanyApiKeyStatus');
                if (cfg.hasCompanyApiKey) {
                    document.getElementById('cfgCompanyApiKey').placeholder = '••••••••  (มี acc_ key อยู่แล้ว — ใส่ค่าใหม่เพื่อเปลี่ยน / ใส่ "-" เพื่อล้าง)';
                    accStatus.innerHTML = '<span style="color:#27ae60;">✓ ตั้งค่าแล้ว (acc_ แยก): <code>' + escapeHtml(cfg.companyApiKeyMask || '••••') + '</code></span>';
                } else {
                    accStatus.innerHTML = '<span style="color:#7f8c8d;">ℹ ยังไม่ได้ตั้ง acc_ แยก — company endpoints จะใช้ Integration Key (int_) ผ่าน X-Api-Key fallback</span>';
                }
                // Email reservation intake (STAAH)
                setVal('cfgEmailRsvEnabled', cfg.emailRsvEnabled ? 'true' : 'false');
                setVal('cfgEmailRsvCreateDocument', cfg.emailRsvCreateDocument ? 'true' : 'false');
                setVal('cfgEmailRsvImapServer', cfg.emailRsvImapServer || 'imap.gmail.com');
                setVal('cfgEmailRsvImapPort', cfg.emailRsvImapPort || 993);
                setVal('cfgEmailRsvPollMinutes', cfg.emailRsvPollMinutes || 5);
                setVal('cfgEmailRsvUsername', cfg.emailRsvUsername || '');
                setVal('cfgEmailRsvProcessedLabel', cfg.emailRsvProcessedLabel || 'STAAH-Processed');
                setVal('cfgEmailRsvFailedLabel', cfg.emailRsvFailedLabel || 'STAAH-Failed');
                setVal('cfgEmailRsvDefaultCollect', cfg.emailRsvDefaultCollect || 'CHANNEL');
                setVal('cfgEmailRsvIgnoredLabel', cfg.emailRsvIgnoredLabel || 'STAAH-Other');
                setVal('cfgEmailRsvFromContains', cfg.emailRsvFromContains || 'staah');
                setVal('cfgEmailRsvMaxStayDays', cfg.emailRsvMaxStayDays || 30);
                setVal('cfgEmailRsvMaxDaysFuture', cfg.emailRsvMaxDaysFuture || 365);
                setVal('cfgEmailRsvNotifyTelegram', cfg.emailRsvNotifyTelegram ? 'true' : 'false');
                setVal('cfgEmailRsvMoveFailed', cfg.emailRsvMoveFailed ? 'true' : 'false');
                setVal('cfgEmailRsvRetryFailed', cfg.emailRsvRetryFailed ? 'true' : 'false');
                setVal('cfgEmailRsvRetryHours', cfg.emailRsvRetryHours || 72);
                setVal('cfgEmailRsvMapAnyChannel', cfg.emailRsvMapAnyChannel ? 'true' : 'false');
                setVal('cfgEmailRsvRoomPriority', cfg.emailRsvRoomPriority || '');
                setVal('cfgEmailRsvDefaultPhone', cfg.emailRsvDefaultPhone || '');
                setVal('cfgEmailRsvCancelStatus', cfg.emailRsvCancelStatus || 'ยกเลิก');
                var pwStat = document.getElementById('cfgEmailRsvPwStatus');
                if (pwStat) {
                    if (cfg.emailRsvHasPassword) {
                        pwStat.innerHTML = '<span style="color:#27ae60;">✓ ตั้งไว้แล้ว — ปล่อยว่างเพื่อคงเดิม</span>';
                        document.getElementById('cfgEmailRsvPassword').placeholder = '•••••••• (มีอยู่แล้ว — ใส่ค่าใหม่เพื่อเปลี่ยน)';
                    } else {
                        pwStat.innerHTML = '<span style="color:#c0392b;">✗ ยังไม่ได้ตั้ง</span>';
                    }
                }
                // Daily reservation board → LINE
                setVal('cfgLineDailyEnabled', cfg.lineDailyEnabled ? 'true' : 'false');
                setVal('cfgLineDailySendTime', cfg.lineDailySendTime || '08:00');
                setVal('cfgLineDailyCaption', cfg.lineDailyCaption || '');
                setVal('cfgLineDailyRecipients', cfg.lineDailyRecipients || '');
                setVal('cfgLineDailyImageWidth', cfg.lineDailyImageWidth || 1600);
                setVal('cfgLineDailyImageHeight', cfg.lineDailyImageHeight || 700);
                setVal('cfgLineDailyAutoHeight', cfg.lineDailyAutoHeight ? 'true' : 'false');
                setVal('cfgLineDailyJpegQuality', cfg.lineDailyJpegQuality || 90);
                setVal('cfgLineDailyFontScale', cfg.lineDailyFontScale || 100);
                setVal('cfgLineDailySourceUrl', cfg.lineDailySourceUrl || '');
                setVal('cfgLineDailyPublicBaseUrl', cfg.lineDailyPublicBaseUrl || '');
                setVal('cfgLineDailyImageFolder', cfg.lineDailyImageFolder || '~/Images/Reservation');
                var lts = document.getElementById('cfgLineDailyTokenStatus');
                if (lts) lts.innerHTML = cfg.lineDailyHasTokenOverride
                    ? '<span style="color:#27ae60;">✓ ตั้ง token เฉพาะไว้แล้ว</span>'
                    : '<span style="color:#7f8c8d;">ℹ ใช้ token ของ LINE OA เดิม</span>';
                if (cfg.lineDailyLastSent) {
                    var lr = document.getElementById('lineDailyResult');
                    if (lr) lr.innerHTML = '<span style="color:#7f8c8d;">ส่งอัตโนมัติล่าสุด: ' + escapeHtml(cfg.lineDailyLastSent) + '</span>';
                }
                updateJourneyMap();
                window.accCfgLoaded = true;   // ข้อ 26: วิซาร์ดบันทึกนโยบายได้เฉพาะเมื่อโหลดค่าจริงครบ (กันค่าอื่นถูกทับด้วยค่าเริ่มต้น)
            } catch (e) { console.error(e); }
        }

        function setVal(id, v) {
            var el = document.getElementById(id);
            if (el) el.value = v;
        }

        function saveConfig() {
            var data = {
                action: 'saveApi',
                baseUrl: document.getElementById('cfgBaseUrl').value,
                apiKey: document.getElementById('cfgApiKey').value,
                companyApiKey: document.getElementById('cfgCompanyApiKey').value,
                companyId: document.getElementById('cfgCompanyId').value
            };
            postAction(data, 'apiTestResult');
        }

        // resultId/opts (ข้อ 26): ปุ่มบันทึกของแต่ละการ์ด/วิซาร์ด — ส่งค่าชุดเดิมทั้งหมดเหมือนเดิม
        function saveSyncSettings(resultId, opts) {
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
                posDailyRollup: document.getElementById('cfgPosDailyRollup').value === 'true',
                roomServiceRevenue: document.getElementById('cfgRoomServiceRevenue').value === 'true',
                etaxRdWatch: document.getElementById('cfgEtaxRdWatch').value === 'true',
                etaxRdFrom: document.getElementById('cfgEtaxRdFrom').value.trim(),
                otaRoomRevenue: document.getElementById('cfgOtaRoomRevenue').value === 'true',
                securityDepositJournal: document.getElementById('cfgSecurityDepositJournal').value === 'true',
                stockQtySync: document.getElementById('cfgStockQtySync').value === 'true',
                stockQtyPull: document.getElementById('cfgStockQtyPull').value === 'true',
                attachFiles: document.getElementById('cfgAttachFiles').value,
                depositVatRecognition: document.getElementById('cfgDepositVatRecognition').value,
                depositDeferOutputVat: document.getElementById('cfgDepositDeferOutputVat').checked,
                depositDrivesJournal: document.getElementById('cfgDepositDrivesJournal').checked,
                depositDrivesJournalRef: document.getElementById('cfgDepositDrivesJournalRef').checked,
                autoRecoverDeposit: document.getElementById('cfgAutoRecoverDeposit').checked,
                postSyncVerify: document.getElementById('cfgPostSyncVerify').checked,
                autoReconcileDeposit: document.getElementById('cfgAutoReconcileDeposit').checked,
                cashSaleUseReceipt: document.getElementById('cfgCashSaleUseReceipt').checked,
                cashSaleCompanyDoc: document.getElementById('cfgCashSaleCompanyDoc').checked,
                stockInUseGRNI: document.getElementById('cfgStockInUseGRNI').checked,
                stockInSkipJournal: document.getElementById('cfgStockInSkipJournal').checked,
                etaxAutoGenerate: document.getElementById('cfgEtaxAutoGenerate').value,
                etaxAutoSign: document.getElementById('cfgEtaxAutoSign').value,
                etaxAutoSubmit: document.getElementById('cfgEtaxAutoSubmit').value,
                etaxAutoSendEmail: document.getElementById('cfgEtaxAutoSendEmail').value,
                etaxEmailSubject: document.getElementById('cfgEtaxEmailSubject').value,
                etaxEmailCc: document.getElementById('cfgEtaxEmailCc').value,
                etaxEmailBody: document.getElementById('cfgEtaxEmailBody').value,
                etaxEmailAttachPdf: document.getElementById('cfgEtaxEmailAttachPdf').value,
                etaxEmailAttachXml: document.getElementById('cfgEtaxEmailAttachXml').value,
                etaxEmailLocalOnly: document.getElementById('cfgEtaxEmailLocalOnly').value,
                etaxEmailFallback: document.getElementById('cfgEtaxEmailFallback').value
            };
            var rhtEl = document.getElementById('cfgReceiptHeaderType');
            if (rhtEl && rhtEl.getAttribute('data-loaded') === '1') data.receiptHeaderType = rhtEl.value;
            var odmEl = document.getElementById('cfgOtaDocumentMode');
            if (odmEl && odmEl.getAttribute('data-loaded') === '1') data.otaDocumentMode = odmEl.value;
            var cnEnEl = document.getElementById('cfgCreditNoteEnabled'), cnApEl = document.getElementById('cfgCreditNoteRequireApproval');
            if (cnEnEl && cnEnEl.getAttribute('data-loaded') === '1') data.creditNoteEnabled = cnEnEl.checked;
            if (cnApEl && cnApEl.getAttribute('data-loaded') === '1') data.creditNoteRequireApproval = cnApEl.checked;
            postAction(data, resultId || 'syncTestResult', opts);
        }

        function testApi() {
            getAction('testApi', 'apiTestResult');
        }

        function syncAccounts(resultId, after) {
            var el = document.getElementById(resultId || 'apiTestResult');
            el.className = 'test-result loading';
            el.textContent = 'กำลัง Sync ผังบัญชี...';

            var controller = new AbortController();
            var timeoutId = setTimeout(function() { controller.abort(); }, 60000);

            fetch(pageUrl + '?action=syncAccounts&_=' + Date.now(), { signal: controller.signal })
                .then(function(r) { clearTimeout(timeoutId); return r.json(); })
                .then(function(data) {
                    el.className = 'test-result ' + (data.success ? 'success' : 'error');
                    el.innerHTML = (data.success ? '<i class="fas fa-check-circle"></i> ' : '<i class="fas fa-times-circle"></i> ') + escapeHtml(data.message);
                    if (data.success) {
                        nexaaccAccountsCache = [];
                        preloadNexaaccAccounts();
                    }
                    if (after) after(!!data.success);
                })
                .catch(function(err) {
                    clearTimeout(timeoutId);
                    el.className = 'test-result error';
                    var msg = err.name === 'AbortError' ? 'หมดเวลา — เซิร์ฟเวอร์ไม่ตอบกลับภายใน 60 วินาที' : err.message;
                    el.innerHTML = '<i class="fas fa-times-circle"></i> ' + escapeHtml(msg);
                    if (after) after(false);
                });
        }

        function processQueue(resultId) {
            getAction('processQueue', resultId || 'syncTestResult', function () { if (typeof loadQueueData === 'function') loadQueueData(); });
        }

        // ── Email reservation intake (STAAH) ──
        function saveEmailIntake() {
            var data = {
                action: 'saveEmailIntake',
                emailRsvEnabled: document.getElementById('cfgEmailRsvEnabled').value === 'true',
                emailRsvCreateDocument: document.getElementById('cfgEmailRsvCreateDocument').value === 'true',
                emailRsvImapServer: document.getElementById('cfgEmailRsvImapServer').value,
                emailRsvImapPort: document.getElementById('cfgEmailRsvImapPort').value,
                emailRsvPollMinutes: document.getElementById('cfgEmailRsvPollMinutes').value,
                emailRsvUsername: document.getElementById('cfgEmailRsvUsername').value,
                emailRsvPassword: document.getElementById('cfgEmailRsvPassword').value,
                emailRsvProcessedLabel: document.getElementById('cfgEmailRsvProcessedLabel').value,
                emailRsvFailedLabel: document.getElementById('cfgEmailRsvFailedLabel').value,
                emailRsvDefaultCollect: document.getElementById('cfgEmailRsvDefaultCollect').value,
                emailRsvIgnoredLabel: document.getElementById('cfgEmailRsvIgnoredLabel').value,
                emailRsvFromContains: document.getElementById('cfgEmailRsvFromContains').value,
                emailRsvMaxStayDays: document.getElementById('cfgEmailRsvMaxStayDays').value,
                emailRsvMaxDaysFuture: document.getElementById('cfgEmailRsvMaxDaysFuture').value,
                emailRsvNotifyTelegram: document.getElementById('cfgEmailRsvNotifyTelegram').value === 'true',
                emailRsvMoveFailed: document.getElementById('cfgEmailRsvMoveFailed').value === 'true',
                emailRsvRetryFailed: document.getElementById('cfgEmailRsvRetryFailed').value === 'true',
                emailRsvRetryHours: document.getElementById('cfgEmailRsvRetryHours').value,
                emailRsvMapAnyChannel: document.getElementById('cfgEmailRsvMapAnyChannel').value === 'true',
                emailRsvRoomPriority: document.getElementById('cfgEmailRsvRoomPriority').value,
                emailRsvDefaultPhone: document.getElementById('cfgEmailRsvDefaultPhone').value,
                emailRsvCancelStatus: document.getElementById('cfgEmailRsvCancelStatus').value
            };
            postAction(data, 'emailRsvResult');
        }

        function testEmailIntake() {
            getAction('emailIntakeTest', 'emailRsvResult');
        }

        // อ่านอีเมล STAAH ล่าสุดแบบ read-only แล้วโชว์ว่า parser แยกฟิลด์อะไรออกมาได้บ้าง
        // ใช้ยืนยันว่าอีเมลมี/ไม่มีข้อมูลบางอย่างจริง ๆ (เช่น ค่าคอมมิชชั่น) แทนการเดา
        function previewEmailIntake() {
            var el = document.getElementById('emailRsvResult');
            el.className = 'test-result loading';
            el.textContent = 'กำลังอ่านอีเมลล่าสุด...';
            fetch(pageUrl + '?action=emailIntakePreview&count=3&_=' + Date.now())
                .then(function(r) { return r.json(); })
                .then(function(data) {
                    el.className = 'test-result ' + (data.success ? 'success' : 'error');
                    el.innerHTML = '<pre style="margin:0; white-space:pre-wrap; font-size:12px;">' + escHtml(data.message || '') + '</pre>';
                })
                .catch(function(err) {
                    el.className = 'test-result error';
                    el.innerHTML = '<i class="fas fa-times-circle"></i> ' + escapeHtml(err.message);
                });
        }

        function diagnoseEmailIntake() {
            var el = document.getElementById('emailRsvResult');
            var rt = document.getElementById('dgRoomType').value.trim();
            if (!rt) { alert('ใส่ชื่อห้อง (ROOM TYPE ในอีเมล) ก่อน'); return; }
            el.className = 'test-result loading';
            el.textContent = 'กำลังตรวจสอบ...';
            var q = '?action=emailIntakeDiagnose'
                  + '&channel=' + encodeURIComponent(document.getElementById('dgChannel').value.trim())
                  + '&roomType=' + encodeURIComponent(rt)
                  + '&checkin=' + encodeURIComponent(document.getElementById('dgCheckin').value)
                  + '&checkout=' + encodeURIComponent(document.getElementById('dgCheckout').value)
                  + '&rooms=' + encodeURIComponent(document.getElementById('dgRooms').value)
                  + '&adults=' + encodeURIComponent(document.getElementById('dgAdults').value)
                  + '&_=' + Date.now();
            fetch(pageUrl + q)
                .then(function(r) { return r.json(); })
                .then(function(data) {
                    el.className = 'test-result ' + (data.success ? 'success' : 'error');
                    el.innerHTML = '<pre style="margin:0; white-space:pre-wrap; font-size:12px;">' + escHtml(data.message || '') + '</pre>';
                })
                .catch(function(err) {
                    el.className = 'test-result error';
                    el.innerHTML = '<i class="fas fa-times-circle"></i> ' + escapeHtml(err.message);
                });
        }

        function runEmailIntake() {
            var el = document.getElementById('emailRsvResult');
            el.className = 'test-result loading';
            el.textContent = 'กำลังดึงอีเมล...';
            var controller = new AbortController();
            var timeoutId = setTimeout(function() { controller.abort(); }, 120000);
            fetch(pageUrl + '?action=emailIntakeRun&_=' + Date.now(), { signal: controller.signal })
                .then(function(r) { clearTimeout(timeoutId); return r.json(); })
                .then(function(data) {
                    el.className = 'test-result ' + (data.success ? 'success' : 'error');
                    var html = (data.success ? '<i class="fas fa-check-circle"></i> ' : '<i class="fas fa-times-circle"></i> ') + escapeHtml(data.message);
                    if (data.detail) html += '<pre style="margin-top:8px; white-space:pre-wrap; font-size:12px;">' + escHtml(data.detail) + '</pre>';
                    el.innerHTML = html;
                })
                .catch(function(err) {
                    clearTimeout(timeoutId);
                    el.className = 'test-result error';
                    var msg = err.name === 'AbortError' ? 'หมดเวลา — เซิร์ฟเวอร์ไม่ตอบกลับภายใน 120 วินาที' : err.message;
                    el.innerHTML = '<i class="fas fa-times-circle"></i> ' + escapeHtml(msg);
                });
        }

        // ♻️ กวาดทุก folder ในกล่องเมลย้อนหลัง N วัน แล้วลงจองที่ยังไม่มีในระบบ
        // ใช้ตอนระบบเคยหยุดอ่าน หรือมีโปรแกรมอื่นแย่งอ่านอีเมลไป — ปลอดภัย ไม่สร้างซ้ำ (dedup Booking ID)
        function recoverEmailBacklog() {
            var days = prompt('กู้อีเมลจองย้อนหลังกี่วัน? (1-90)\n\nระบบจะกวาดทุกโฟลเดอร์ในกล่องเมล '
                + 'แล้วลงจองใบที่ยังไม่มีในระบบ — ใบที่ลงแล้วจะถูกนับเป็น "ซ้ำ" ไม่สร้างซ้อน', '7');
            if (days === null) return;
            days = parseInt(days, 10);
            if (!days || days < 1) { alert('ใส่จำนวนวันเป็นตัวเลข 1-90'); return; }
            if (days > 90) days = 90;

            var el = document.getElementById('emailRsvResult');
            el.className = 'test-result loading';
            el.textContent = 'กำลังกวาดกล่องเมลย้อนหลัง ' + days + ' วัน... (อาจใช้เวลาหลายนาที)';
            var controller = new AbortController();
            var timeoutId = setTimeout(function() { controller.abort(); }, 300000);
            fetch(pageUrl + '?action=emailIntakeRecover&days=' + days + '&_=' + Date.now(), { signal: controller.signal })
                .then(function(r) { clearTimeout(timeoutId); return r.json(); })
                .then(function(data) {
                    el.className = 'test-result ' + (data.success ? 'success' : 'error');
                    var html = (data.success ? '<i class="fas fa-check-circle"></i> ' : '<i class="fas fa-times-circle"></i> ') + escapeHtml(data.message);
                    if (data.detail) html += '<pre style="margin-top:8px; white-space:pre-wrap; font-size:12px;">' + escHtml(data.detail) + '</pre>';
                    el.innerHTML = html;
                })
                .catch(function(err) {
                    clearTimeout(timeoutId);
                    el.className = 'test-result error';
                    var msg = err.name === 'AbortError' ? 'หมดเวลา — เซิร์ฟเวอร์ไม่ตอบกลับภายใน 5 นาที (งานอาจยังทำต่อเบื้องหลัง ดู logs)' : err.message;
                    el.innerHTML = '<i class="fas fa-times-circle"></i> ' + escapeHtml(msg);
                });
        }

        // ✔ ปิดรายการคิวที่งานเสร็จจริงบน NextAcc แล้ว — ไม่ยิง API ซ้ำ เปลี่ยนแค่สถานะฝั่งเรา
        function closeItem(id) {
            var note = prompt('ปิดรายการคิว #' + id + ' โดยไม่ยิง API ซ้ำ\n\n'
                + 'ใช้เมื่อยืนยันแล้วว่างานนี้เสร็จถูกต้องบน NextAcc (เอกสาร/บัญชีขึ้นครบ) '
                + 'แต่คิวฝั่งเราปิดตัวเองไม่ได้\n\nหมายเหตุ (ไม่บังคับ) — จะถูกเก็บไว้ในประวัติ:',
                'ตรวจแล้วข้อมูลบน NextAcc ถูกต้องครบถ้วน');
            if (note === null) return;
            fetch(pageUrl + '?action=closeQueueItem&queueId=' + id + '&note=' + encodeURIComponent(note) + '&_=' + Date.now())
                .then(function(r) { return r.json(); })
                .then(function(data) {
                    alert(data.message || (data.success ? 'ปิดรายการแล้ว' : 'ปิดไม่สำเร็จ'));
                    if (data.success) loadQueueData();
                })
                .catch(function(err) { alert('ปิดไม่สำเร็จ: ' + err.message); });
        }

        function escHtml(s) { return escapeHtml(s); }

        function loadEmailLog() {
            var el = document.getElementById('emailRsvLog');
            el.innerHTML = '<div class="test-result loading">กำลังโหลด logs...</div>';
            fetch(pageUrl + '?action=emailIntakeLog&limit=100&_=' + Date.now())
                .then(function(r) { return r.json(); })
                .then(function(data) {
                    if (!data.success) {
                        el.innerHTML = '<div class="test-result error"><i class="fas fa-times-circle"></i> ' + escapeHtml(data.message || 'โหลด logs ไม่สำเร็จ') + '</div>';
                        return;
                    }
                    if (!data.items || data.items.length === 0) {
                        el.innerHTML = '<div class="test-result">ยังไม่มี log (ลองกด "ดึงตอนนี้" หรือรอ timer รอบถัดไป)</div>';
                        return;
                    }
                    var html = '<div style="max-height:340px; overflow:auto; border:1px solid #e0e0e0; border-radius:4px;">' +
                        '<table style="width:100%; border-collapse:collapse; font-size:12px;">' +
                        '<thead><tr style="background:#f5f5f5; position:sticky; top:0;">' +
                        '<th style="text-align:left; padding:6px 8px; white-space:nowrap;">เวลา</th>' +
                        '<th style="text-align:left; padding:6px 8px;">รายละเอียด</th></tr></thead><tbody>';
                    for (var i = 0; i < data.items.length; i++) {
                        var it = data.items[i];
                        var d = it.detail || '';
                        var isErr = /error|failed|ล้มเหลว|ไม่สำเร็จ|ไม่พบ|no mapping|not enough/i.test(d);
                        html += '<tr style="border-top:1px solid #eee;">' +
                            '<td style="padding:6px 8px; white-space:nowrap; color:#777;">' + escHtml(it.time) + '</td>' +
                            '<td style="padding:6px 8px; color:' + (isErr ? '#c0392b' : '#333') + ';">' + escHtml(d) + '</td></tr>';
                    }
                    html += '</tbody></table></div>' +
                        '<div style="font-size:11px; color:#999; margin-top:4px;">แสดง ' + data.items.length + ' รายการล่าสุด (LogAction = EmailReservation)</div>';
                    el.innerHTML = html;
                })
                .catch(function(err) {
                    el.innerHTML = '<div class="test-result error"><i class="fas fa-times-circle"></i> ' + escapeHtml(err.message) + '</div>';
                });
        }

        // ── Daily reservation board → LINE ──
        function saveLineDaily() {
            var data = {
                action: 'saveLineDaily',
                lineDailyEnabled: document.getElementById('cfgLineDailyEnabled').value === 'true',
                lineDailySendTime: document.getElementById('cfgLineDailySendTime').value,
                lineDailyCaption: document.getElementById('cfgLineDailyCaption').value,
                lineDailyRecipients: document.getElementById('cfgLineDailyRecipients').value,
                lineDailyImageWidth: document.getElementById('cfgLineDailyImageWidth').value,
                lineDailyImageHeight: document.getElementById('cfgLineDailyImageHeight').value,
                lineDailyAutoHeight: document.getElementById('cfgLineDailyAutoHeight').value === 'true',
                lineDailyJpegQuality: document.getElementById('cfgLineDailyJpegQuality').value,
                lineDailyFontScale: document.getElementById('cfgLineDailyFontScale').value,
                lineDailySourceUrl: document.getElementById('cfgLineDailySourceUrl').value,
                lineDailyPublicBaseUrl: document.getElementById('cfgLineDailyPublicBaseUrl').value,
                lineDailyImageFolder: document.getElementById('cfgLineDailyImageFolder').value,
                lineDailyTokenOverride: document.getElementById('cfgLineDailyTokenOverride').value
            };
            postAction(data, 'lineDailyResult');
        }

        function lineDailyRun(action, withImage) {
            var el = document.getElementById('lineDailyResult');
            var prev = document.getElementById('lineDailyPreview');
            el.className = 'test-result loading';
            el.textContent = 'กำลังดำเนินการ...';
            var controller = new AbortController();
            var timeoutId = setTimeout(function() { controller.abort(); }, 120000);
            fetch(pageUrl + '?action=' + action + '&_=' + Date.now(), { signal: controller.signal })
                .then(function(r) { clearTimeout(timeoutId); return r.json(); })
                .then(function(data) {
                    el.className = 'test-result ' + (data.success ? 'success' : 'error');
                    var html = (data.success ? '<i class="fas fa-check-circle"></i> ' : '<i class="fas fa-times-circle"></i> ') + escapeHtml(data.message);
                    if (data.detail) html += '<pre style="margin-top:8px; white-space:pre-wrap; font-size:12px;">' + escHtml(data.detail) + '</pre>';
                    el.innerHTML = html;
                    if (withImage && data.imageUrl && prev) {
                        prev.innerHTML = '<div style="font-size:12px; color:#777; margin-bottom:4px;">พรีวิว:</div>' +
                            '<img src="' + escHtml(data.imageUrl) + '" style="max-width:100%; border:1px solid #ddd; border-radius:4px;" />';
                    }
                })
                .catch(function(err) {
                    clearTimeout(timeoutId);
                    el.className = 'test-result error';
                    var msg = err.name === 'AbortError' ? 'หมดเวลา (120 วินาที)' : err.message;
                    el.innerHTML = '<i class="fas fa-times-circle"></i> ' + escapeHtml(msg);
                });
        }

        function previewLineDaily() { lineDailyRun('lineDailyPreview', true); }
        function sendLineDaily() {
            if (!confirm('ส่งรูปตารางจองวันนี้เข้า LINE ถึงผู้รับที่ตั้งไว้เลยหรือไม่?')) return;
            lineDailyRun('lineDailySend', true);
        }
        function testLineDaily() { lineDailyRun('lineDailyTest', false); }

        function loadLineDailyLog() {
            var el = document.getElementById('lineDailyLog');
            el.innerHTML = '<div class="test-result loading">กำลังโหลด logs...</div>';
            fetch(pageUrl + '?action=lineDailyLog&_=' + Date.now())
                .then(function(r) { return r.json(); })
                .then(function(data) {
                    if (!data.success) { el.innerHTML = '<div class="test-result error">' + escapeHtml(data.message || 'โหลดไม่สำเร็จ') + '</div>'; return; }
                    if (!data.items || data.items.length === 0) { el.innerHTML = '<div class="test-result">ยังไม่มี log</div>'; return; }
                    var html = '<div style="max-height:280px; overflow:auto; border:1px solid #e0e0e0; border-radius:4px;">' +
                        '<table style="width:100%; border-collapse:collapse; font-size:12px;">' +
                        '<thead><tr style="background:#f5f5f5; position:sticky; top:0;">' +
                        '<th style="text-align:left; padding:6px 8px; white-space:nowrap;">เวลา</th>' +
                        '<th style="text-align:left; padding:6px 8px;">รายละเอียด</th></tr></thead><tbody>';
                    for (var i = 0; i < data.items.length; i++) {
                        var it = data.items[i];
                        var isErr = /error|ล้มเหลว|ผิดพลาด|ไม่/i.test(it.detail || '');
                        html += '<tr style="border-top:1px solid #eee;">' +
                            '<td style="padding:6px 8px; white-space:nowrap; color:#777;">' + escHtml(it.time) + '</td>' +
                            '<td style="padding:6px 8px; color:' + (isErr ? '#c0392b' : '#333') + ';">' + escHtml(it.detail) + '</td></tr>';
                    }
                    html += '</tbody></table></div>';
                    el.innerHTML = html;
                })
                .catch(function(err) { el.innerHTML = '<div class="test-result error">' + escapeHtml(err.message) + '</div>'; });
        }

        function reconcileDeleted() {
            if (!confirm('⚠️ ยืนยันตรวจสอบ & ลบเอกสารที่หายจาก NextAcc?\n\nใบเสร็จ/ใบสำคัญจ่ายที่ sync แล้ว ถ้า NextAcc ตอบ 404 (ไม่มีเอกสารแล้ว) จะถูก "ลบถาวร" ออกจากระบบนี้\n(ลบเฉพาะ 404 ชัดเจน — error ชั่วคราวจะข้าม)')) return;
            getAction('reconcileDeleted', 'dangerResult');
        }

        function cleanupOrphanReceipts() {
            if (!confirm('เก็บกวาดใบเสร็จรับเงิน (หลักฐานรับเงิน) ที่ orphan บน NextAcc?\n\nลบเฉพาะใบที่ "ใบกำกับต้นทางถูกลบ/ยกเลิกไปแล้ว" (soft-delete บน NextAcc ไม่กระทบ GL).\nใบที่ยังมีใบกำกับใช้งานอยู่จะไม่ถูกแตะ (NextAcc กันให้).')) return;
            getAction('cleanupOrphanReceipts', 'dangerResult');
        }

        function cleanupDepositDebris() {
            if (!confirm('กลับ (reverse) JV มัดจำที่ค้างเป็นซาก GL จากการ resync ซ้ำ?\n\nกลับเฉพาะ JV ที่ TakeTime post เอง (215xx/217xx/21913 ที่ไม่ผูกเอกสาร).\nⓘ ทำหลัง NextAcc deploy + หยุด resync แล้วเท่านั้น\nⓘ ยอดที่เหลือหลังกลับต้องให้นักบัญชีตรวจ/ยืนยัน (churn แก้อัตโนมัติ 100% ไม่ได้)')) return;
            getAction('cleanupDepositDebris', 'dangerResult');
        }

        function resetReservation() {
            var input = document.getElementById('resetResId');
            var resId = (input.value || '').trim();
            var el = document.getElementById('resetResResult');
            if (!resId || parseInt(resId, 10) <= 0) {
                el.className = 'test-result error';
                el.innerHTML = '<i class="fas fa-times-circle"></i> กรอกรหัสการจอง (Reservation ID) ก่อน';
                return;
            }
            if (!confirm('🧹 รีเซ็ตบัญชีการจอง #' + resId + '?\n\nจะกลับ (reverse) ทุก JE ที่ TakeTime post ให้การจองนี้ (มัดจำ/ใบกำกับ/ตัดมัดจำ/VAT/churn)\n→ 21510/21913/ลูกหนี้ ของการจองนี้กลับเป็น 0\nพร้อม void เอกสาร (TIV/REC) ทุกใบของการจองบน NextAcc\n\nⓘ idempotent (กดซ้ำไม่เบิ้ล — ข้ามใบที่ voided แล้ว) + reset marker เพื่อ re-sync ใหม่ได้สะอาด')) return;

            el.className = 'test-result loading';
            el.textContent = 'กำลังรีเซ็ตบัญชีการจอง #' + resId + '...';
            var controller = new AbortController();
            var timeoutId = setTimeout(function() { controller.abort(); }, 120000);
            fetch(pageUrl + '?action=resetReservation&resId=' + encodeURIComponent(resId) + '&_=' + Date.now(), { signal: controller.signal })
                .then(function(r) { clearTimeout(timeoutId); return r.json(); })
                .then(function(data) {
                    el.className = 'test-result ' + (data.success ? 'success' : 'error');
                    el.innerHTML = (data.success ? '<i class="fas fa-check-circle"></i> ' : '<i class="fas fa-times-circle"></i> ') + escapeHtml(data.message);
                })
                .catch(function(err) {
                    clearTimeout(timeoutId);
                    el.className = 'test-result error';
                    var msg = err.name === 'AbortError' ? 'หมดเวลา — เซิร์ฟเวอร์ไม่ตอบกลับภายใน 120 วินาที' : err.message;
                    el.innerHTML = '<i class="fas fa-times-circle"></i> ' + escapeHtml(msg);
                });
        }

        function applyRecommendedPreset() {
            if (!confirm('⭐ ใช้ค่าแนะนำ (Production)?\n\nจะตั้งค่าทั้งชุดเป็นเส้นทางที่ทดสอบแล้วถูกต้อง:\n• เอกสารรับ/จ่าย = DOCUMENT\n• หักมัดจำ = drives (JE เดียว หักมัดจำในใบ)\n• ปิดเส้นทดลอง isCashSale / cash-sale-deposit\n• มัดจำ VAT = รับรู้ตอนเช็คเอาท์\n• เปิด safety-net (auto-recover / reconcile / verify)\n\nทับค่า toggle เดิมที่เปิดซ้อนกัน — บันทึกทันที')) return;
            var el = document.getElementById('presetResult');
            el.className = 'test-result loading';
            el.textContent = 'กำลังตั้งค่า...';
            fetch(pageUrl + '?action=applyRecommendedPreset&_=' + Date.now())
                .then(function(r){ return r.json(); })
                .then(function(d){
                    el.className = 'test-result ' + (d.success ? 'success' : 'error');
                    el.innerHTML = (d.success ? '<i class="fas fa-check-circle"></i> ' : '<i class="fas fa-times-circle"></i> ') + escapeHtml(d.message);
                    if (d.success) setTimeout(function(){ location.reload(); }, 2500);
                })
                .catch(function(err){ el.className = 'test-result error'; el.innerHTML = '<i class="fas fa-times-circle"></i> ' + escapeHtml(err.message); });
        }

        function grniReconcile() {
            var el = document.getElementById('grniResult');
            el.className = 'test-result loading';
            el.textContent = 'กำลังตรวจยอดคงค้าง GRNI...';
            var controller = new AbortController();
            var timeoutId = setTimeout(function() { controller.abort(); }, 60000);
            fetch(pageUrl + '?action=grniReconcile&_=' + Date.now(), { signal: controller.signal })
                .then(function(r) { clearTimeout(timeoutId); return r.json(); })
                .then(function(d) {
                    if (!d.success) {
                        el.className = 'test-result error';
                        el.innerHTML = '<i class="fas fa-times-circle"></i> ' + escapeHtml(d.message || 'ไม่สำเร็จ');
                        return;
                    }
                    el.className = 'test-result success';
                    var net = parseFloat(d.netOpen || 0);
                    var color = Math.abs(net) < 0.01 ? '#28a745' : '#c0392b';
                    var html = '<div style="font-size:14px;">บัญชี GRNI <b>' + escapeHtml(d.accountCode || '-') + '</b> · คงค้างสุทธิ '
                        + '<b style="color:' + color + ';">' + net.toLocaleString('en-US', {minimumFractionDigits:2, maximumFractionDigits:2}) + '</b></div>';
                    html += '<div style="margin:4px 0 8px;">' + escapeHtml(d.interpretation || '') + '</div>';
                    if (d.items && d.items.length) {
                        html += '<table style="width:100%; border-collapse:collapse; font-size:12px;">'
                            + '<thead><tr style="background:#f5f5f5;"><th style="padding:4px 6px; text-align:left;">อ้างอิง</th>'
                            + '<th style="padding:4px 6px; text-align:left;">เลข JE</th><th style="padding:4px 6px;">วันที่</th>'
                            + '<th style="padding:4px 6px; text-align:right;">รับของ (Cr GRNI)</th></tr></thead><tbody>';
                        d.items.forEach(function(it) {
                            html += '<tr' + (it.Voided ? ' style="color:#999;text-decoration:line-through;"' : '') + '>'
                                + '<td style="padding:3px 6px;">' + escapeHtml(it.Reference||'') + '</td>'
                                + '<td style="padding:3px 6px;">' + escapeHtml(it.DocNumber||'-') + '</td>'
                                + '<td style="padding:3px 6px; text-align:center;">' + escapeHtml(it.Date||'') + '</td>'
                                + '<td style="padding:3px 6px; text-align:right;">' + parseFloat(it.Amount||0).toLocaleString('en-US',{minimumFractionDigits:2,maximumFractionDigits:2}) + '</td></tr>';
                        });
                        html += '</tbody></table>';
                    }
                    el.innerHTML = html;
                })
                .catch(function(err) {
                    clearTimeout(timeoutId);
                    el.className = 'test-result error';
                    el.innerHTML = '<i class="fas fa-times-circle"></i> ' + (err.name === 'AbortError' ? 'หมดเวลา 60 วินาที' : escapeHtml(err.message));
                });
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
                        el.innerHTML = '<div class="test-result error"><i class="fas fa-times-circle"></i> ' + escapeHtml(data.message) + '</div>';
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
                              + '<td style="padding:8px; font-family:monospace;">#' + escapeHtml(it.reservationId) + '<br/><small>' + escapeHtml(it.reservationStatus || '-') + '</small></td>'
                              + '<td style="padding:8px;">' + escapeHtml(it.customerName || '-') + '<br/><small style="color:#999;">' + escapeHtml(it.customerMobilePhone || '') + '</small></td>'
                              + '<td style="padding:8px;">' + (it.checkoutDate ? new Date(it.checkoutDate).toLocaleDateString('th-TH') : '-') + '</td>'
                              + '<td style="padding:8px; text-align:right;">' + Number(it.depositPaid).toLocaleString('th-TH', {minimumFractionDigits:2}) + '</td>'
                              + '<td style="padding:8px; text-align:right;">' + Number(it.depositCleared).toLocaleString('th-TH', {minimumFractionDigits:2}) + '</td>'
                              + '<td style="padding:8px; text-align:right; font-weight:600;">' + Number(it.depositOutstanding).toLocaleString('th-TH', {minimumFractionDigits:2}) + '</td>'
                              + '<td style="padding:8px;"><span style="background:' + color + '; color:white; padding:2px 8px; border-radius:3px; font-size:11px;">' + escapeHtml(it.depositStatus) + '</span></td>'
                              + '<td style="padding:8px;">' + escapeHtml(it.lastClearAction || '-') + (it.lastClearDate ? '<br/><small>' + new Date(it.lastClearDate).toLocaleString('th-TH') + '</small>' : '') + '</td>'
                              + '</tr>';
                    });
                    html += '</tbody></table>';
                    el.innerHTML = html;
                })
                .catch(function(err) {
                    el.innerHTML = '<div class="test-result error"><i class="fas fa-times-circle"></i> ' + escapeHtml(err.message) + '</div>';
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
            var OPN = { checkout: 'ตัดมัดจำ (เช็คเอาท์)', refund: 'คืนเงินมัดจำ', forfeit: 'ริบมัดจำ' };
            if (!confirm('⚠️ ' + (OPN[action] || action) + ' ของการจอง #' + resId + ' ยอด ' + amount + ' บาท?\n\nรายการนี้ลงบัญชีจริงใน NextAcc — ใช้เมื่อระบบอัตโนมัติไม่ได้ทำเท่านั้น')) return;
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
                        el.innerHTML = '<div class="test-result error"><i class="fas fa-times-circle"></i> ' + escapeHtml(data.message) + '</div>';
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
                              + '<td style="padding:8px; font-family:monospace;">' + escapeHtml(it.receiptNumber || '-') + '</td>'
                              + '<td style="padding:8px;">' + escapeHtml(it.reservationId || '-') + '</td>'
                              + '<td style="padding:8px; text-align:right;">' + (it.total ? Number(it.total).toLocaleString('th-TH', {minimumFractionDigits:2}) : '-') + '</td>'
                              + '<td style="padding:8px;"><span style="background:' + sourceColor + '; color:white; padding:2px 8px; border-radius:3px; font-size:11px;">' + escapeHtml(it.documentSource) + '</span></td>'
                              + '<td style="padding:8px; font-family:monospace; font-size:11px;">' + (it.nexaaccDocId ? (it.nexaaccUrl ? '<a href="' + escapeHtml(it.nexaaccUrl) + '" target="_blank" class="nexaacc-doc-link">' + escapeHtml(String(it.nexaaccDocId).substring(0,12)) + '... <i class="fas fa-external-link-alt"></i></a>' : escapeHtml(String(it.nexaaccDocId).substring(0,12)) + '...') : '-') + '</td>'
                              + '<td style="padding:8px;"><span style="color:' + syncColor + ';">' + escapeHtml(it.syncStatus || '-') + '</span></td>'
                              + '<td style="padding:8px;">' + (it.etaxStatus ? '<span style="color:' + etaxColor + ';">' + escapeHtml(it.etaxStatus) + (it.etaxRefNumber ? ' (' + escapeHtml(it.etaxRefNumber) + ')' : '') + '</span>' : '-') + (it.etaxEmailSent ? ' ✉' : '') + '</td>'
                              + '<td style="padding:8px;">' + escapeHtml(it.customerName || '-') + (it.customerEmail ? '<br/><small style="color:#999;">' + escapeHtml(it.customerEmail) + '</small>' : '') + '</td>'
                              + '<td style="padding:8px;">' + (it.etaxPdfUrl ? '<a href="' + escapeHtml(it.etaxPdfUrl) + '" target="_blank">PDF</a>' : '-') + (it.etaxXmlUrl ? ' / <a href="' + escapeHtml(it.etaxXmlUrl) + '" target="_blank">XML</a>' : '') + '</td>'
                              + '</tr>';
                    });
                    html += '</tbody></table>';
                    el.innerHTML = html;
                })
                .catch(function(err) {
                    el.innerHTML = '<div class="test-result error"><i class="fas fa-times-circle"></i> ' + escapeHtml(err.message) + '</div>';
                });
        }

        // ── ส่งข้อมูลสินค้า (เครื่องมือปรับสต๊อกเดิมปลดระวางแล้ว — ใช้ Product/Stock) ──

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
            queueState.cls = '';
            queueState.page = 1;
            updateFilterUI();
            loadQueueData();
        }

        // ── PHASE19_37: ชนิดปัญหาของแถวคิว ──
        var QCLS = {
            DATA:      { icon: '🟧', label: 'แก้ข้อมูล', bg: '#FFF3E0', fg: '#E65100' },
            CONFIG:    { icon: '🟪', label: 'ตั้งค่า', bg: '#F3E5F5', fg: '#6A1B9A' },
            WAIT:      { icon: '🟦', label: 'รอ', bg: '#E3F2FD', fg: '#1565C0' },
            POLICY:    { icon: '🟥', label: 'ต้องใบลดหนี้', bg: '#FFEBEE', fg: '#C62828' },
            DUPLICATE: { icon: '⬛', label: 'ตรวจบน NextAcc', bg: '#ECEFF1', fg: '#263238' },
            TRANSIENT: { icon: '⬜', label: 'ลองใหม่อัตโนมัติ', bg: '#FAFAFA', fg: '#555' },
            UNKNOWN:   { icon: '❔', label: 'ไม่ทราบสาเหตุ', bg: '#FAFAFA', fg: '#555' }
        };

        function filterByClass(cls) {
            queueState.cls = (queueState.cls === cls) ? '' : cls;
            queueState.status = '';
            queueState.page = 1;
            updateFilterUI();
            loadQueueData();
        }

        function renderClassBar(data) {
            var bar = document.getElementById('queueClassBar');
            var pf = document.getElementById('preflightBar');
            if (!bar || !pf) return;
            if (!data.hasIssueCols) { bar.style.display = 'none'; pf.style.display = 'none'; return; }
            pf.style.display = 'block';
            var sel = document.getElementById('pfModeSel');
            if (sel && data.preflightMode && !sel.dataset.touched) sel.value = data.preflightMode;
            var cc = data.classCounts || {};
            var html = '<span style="color:#666;">ค้างตามชนิด: </span>';
            var any = false;
            ['DATA', 'CONFIG', 'WAIT', 'POLICY', 'DUPLICATE', 'TRANSIENT', 'UNKNOWN'].forEach(function (k) {
                var n = cc[k] || 0;
                if (!n && queueState.cls !== k) return;
                any = true;
                var m = QCLS[k];
                var on = queueState.cls === k;
                html += '<span onclick="filterByClass(\'' + k + '\')" style="cursor:pointer; display:inline-block; margin:2px 4px; padding:3px 9px; border-radius:12px;'
                      + ' background:' + m.bg + '; color:' + m.fg + ';' + (on ? ' outline:2px solid ' + m.fg + ';' : '') + '">'
                      + m.icon + ' ' + m.label + ' <b>' + n + '</b></span>';
            });
            if (!any) html += '<span style="color:#2E7D32;">ไม่มีรายการที่ต้องมีคนแก้ ✅</span>';
            bar.innerHTML = html;
            bar.style.display = 'block';
        }

        function classBadge(item) {
            var c = item.errorClass;
            if (!c || !QCLS[c]) return '';
            var m = QCLS[c];
            var open = item.status === 'FAILED' || (item.status === 'PENDING' && c === 'WAIT');
            if (!open) return '<div style="font-size:10.5px; color:#999;" title="ชนิดปัญหาที่เคยเกิดกับรายการนี้">เคย: ' + m.label + '</div>';
            var t = m.icon + ' ' + m.label + (item.errorRule ? ' · ' + escapeHtml(item.errorRule) : '');
            if (c === 'WAIT' && item.dependsOn) t += ' #' + (parseInt(item.dependsOn, 10) || 0);
            return '<div style="margin-top:3px;"><span style="font-size:11px; padding:2px 7px; border-radius:10px; background:' + m.bg + '; color:' + m.fg + ';"'
                 + (item.waitSince ? ' title="รอตั้งแต่ ' + escapeHtml(item.waitSince) + '"' : '') + '>' + t + '</span></div>';
        }

        function fixHereLink(item) {
            var c = item.errorClass;
            var open = item.status === 'FAILED' || (item.status === 'PENDING' && c === 'WAIT');
            if (!open || item.sensitive) return '';
            if (item.fixUrl && /^\/[A-Za-z]/.test(item.fixUrl))
                return '<a href="' + escapeHtml(appBase + item.fixUrl) + '" target="_blank" class="btn-primary" style="padding:4px 10px; font-size:11px; text-decoration:none; display:inline-block; margin:1px 0;" title="เปิดหน้าที่ต้องแก้">🛠 แก้ที่นี่</a> ';
            if (c === 'WAIT' && item.dependsOn)
                return '<button type="button" class="btn-default" style="padding:4px 10px; font-size:11px;" onclick="showItemLogs(' + (parseInt(item.dependsOn, 10) || 0) + ')" title="ดูรายการที่รอ">รอ #' + (parseInt(item.dependsOn, 10) || 0) + '</button> ';
            if (c === 'DUPLICATE' && item.nexaaccUrl)
                return '<a href="' + escapeHtml(item.nexaaccUrl) + '" target="_blank" class="btn-default" style="padding:4px 10px; font-size:11px;">ตรวจบน NextAcc</a> ';
            return '';
        }

        function recheckItem(queueId) {
            fetch(pageUrl + '?action=recheckItem&queueId=' + queueId + '&_=' + Date.now())
                .then(function (r) { return r.json(); })
                .then(function (data) { alert(data.message || (data.success ? 'คืนเข้าคิวแล้ว' : 'ยังไม่ผ่าน')); loadQueueData(); })
                .catch(function (err) { alert(err.message); });
        }

        function savePreflightMode() {
            var sel = document.getElementById('pfModeSel');
            var el = document.getElementById('pfResult');
            var mode = sel ? sel.value : '';
            if (mode === 'BLOCK' && !confirm('เปลี่ยนเป็น BLOCK: หน้าเช็คอิน/ใบเสร็จจะหยุดบันทึกเมื่อพบปัญหาข้อมูล\n(Owner / Admin บัญชี ยืนยันข้ามได้พร้อมเหตุผล)\n\nแนะนำให้กด "สแกนย้อนหลัง" ดูผลก่อน — ยืนยัน?')) return;
            fetch(pageUrl + '?action=setPreflightMode&mode=' + encodeURIComponent(mode) + '&_=' + Date.now())
                .then(function (r) { return r.json(); })
                .then(function (data) {
                    el.innerHTML = '<div class="test-result ' + (data.success ? 'success' : 'error') + '" style="display:block;">' + escapeHtml(data.message) + '</div>';
                    loadQueueData();
                })
                .catch(function (err) { el.innerHTML = '<div class="test-result error" style="display:block;">' + escapeHtml(err.message) + '</div>'; });
        }

        function runPfAction(action, label) {
            var el = document.getElementById('pfResult');
            el.innerHTML = '<div class="test-result loading">' + label + '...</div>';
            fetch(pageUrl + '?action=' + action + '&_=' + Date.now())
                .then(function (r) { return r.json(); })
                .then(function (data) {
                    el.innerHTML = '<pre style="white-space:pre-wrap; word-break:break-word; font-size:11.5px; max-height:320px; overflow:auto; margin:0; padding:8px; border-radius:5px; '
                                 + (data.success ? 'background:#F4F8FD; border:1px solid #BBDEFB;' : 'background:#FFF5F5; border:1px solid #FFCDD2;') + '">' + escapeHtml(data.message) + '</pre>';
                })
                .catch(function (err) { el.innerHTML = '<div class="test-result error" style="display:block;">' + escapeHtml(err.message) + '</div>'; });
        }
        function runPreflightScan() { runPfAction('preflightScan&days=30', 'กำลังสแกน'); }
        function runIntegrityScan() { runPfAction('integrityScan', 'กำลังตรวจ'); }

        // ลิงก์ "แก้ที่นี่" (คิว/ตรวจสุขภาพ/Telegram): ?focus=mapping&key=ROOM_REVENUE / paidHow / gateway / ota / api / sync / email / queue
        function focusFromQuery() {
            try {
                var q = new URLSearchParams(window.location.search);
                // ข้อ 26: มี #t-แท็บ = ผู้ใช้อยู่แท็บนั้นก่อนรีโหลด (ตัวกรองคิว ?cls/?status/?q ยังใช้ผ่าน initQueueFromQuery)
                if (/^#t-/.test(window.location.hash || '')) return;
                if (q.get('focus') || q.get('cls') || q.get('status') || q.get('q')) applyFocus(q, true);
            } catch (e) { }
        }

        function applyFocus(q, fromUrl) {
            var f = q.get('focus') || ((q.get('cls') || q.get('status') || q.get('q')) ? 'queue' : '');
            if (!f) return;
            var key = q.get('key');
            var loaders = {
                mapping: { el: 'mappingTable', load: typeof loadMappingsWithAccounts === 'function' ? loadMappingsWithAccounts : null },
                paidHow: { el: 'paidHowTable', load: typeof loadPaidHowMapping === 'function' ? loadPaidHowMapping : null },
                gateway: { el: 'cardGateway', load: typeof loadGatewayPaidHow === 'function' ? loadGatewayPaidHow : null },
                ota: { el: 'cardOta', load: typeof loadOtaChannelMap === 'function' ? loadOtaChannelMap : null },
                paidType: { el: 'paidTypeTable', load: typeof loadPaidTypeMapping === 'function' ? loadPaidTypeMapping : null },
                api: { el: 'cardApi' }, sync: { el: 'cardSync' }, email: { el: 'cardEmail' },
                health: { el: 'healthResult' }, queue: { el: 'queueStats' }
            };
            var cfg = loaders[f] || loaders.sync;
            if (typeof accShowFor === 'function') accShowFor(cfg.el);   // ข้อ 26: เปิดแท็บที่มีส่วนนั้นก่อน
            if (f === 'queue' && !fromUrl) {
                var st = (q.get('status') || '').toUpperCase(), cls = (q.get('cls') || '').toUpperCase();
                queueState.status = /^[A-Z_]{3,20}$/.test(st) ? st : '';
                queueState.cls = !queueState.status && /^[A-Z]{3,12}$/.test(cls) ? cls : '';
                queueState.page = 1;
                updateFilterUI();
                loadQueueData();
            }
            if (cfg.load) { try { cfg.load(); } catch (e) { } }
            setTimeout(function () {
                var target = document.getElementById(cfg.el);
                if (!target) return;
                if (typeof accShowFor === 'function') accShowFor(target);
                target.scrollIntoView({ behavior: 'smooth', block: 'start' });
                target.style.outline = '3px solid #FFB300';
                setTimeout(function () { target.style.outline = ''; }, 6000);
                if (key) {
                    var rows = target.querySelectorAll('tr'), hit = null;
                    for (var i = 0; i < rows.length && !hit; i++) {
                        var c0 = rows[i].cells && rows[i].cells[0];
                        if (c0 && (c0.textContent || '').indexOf(key) >= 0) hit = rows[i];
                    }
                    for (var j = 0; j < rows.length && !hit; j++) {
                        if ((rows[j].textContent || '').indexOf(key) >= 0) hit = rows[j];
                    }
                    if (hit) { hit.style.background = '#FFF8E1'; hit.scrollIntoView({ block: 'center' }); }
                }
            }, cfg.load ? 1500 : 200);
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
                'COMPLETED': 'qsCompletedCard', 'FAILED': 'qsFailedCard', 'AWAITING_SLIP': 'qsSlipCard'
            };
            for (var key in cards) {
                var el = document.getElementById(cards[key]);
                if (el) el.className = 'queue-stat' + (queueState.status === key && !(key === '' && queueState.cls) ? ' active' : '');
            }
        }

        function loadQueueData() {
            var url = pageUrl + '?action=queueData&page=' + queueState.page
                + '&pageSize=' + queueState.pageSize
                + (queueState.status ? '&status=' + queueState.status : '')
                + (queueState.cls ? '&cls=' + queueState.cls : '')
                + (queueState.q ? '&q=' + encodeURIComponent(queueState.q) : '')
                + '&_=' + Date.now();
            var clr = document.getElementById('btnQueueSearchClear');
            if (clr) clr.style.display = queueState.q ? 'inline-block' : 'none';

            fetch(url)
                .then(function(r) { return r.json(); })
                .then(function(data) {
                    var total = (data.pending || 0) + (data.processing || 0) + (data.completed || 0) + (data.failed || 0) + (data.awaitingSlip || 0);
                    document.getElementById('qsTotal').textContent = total;
                    document.getElementById('qsPending').textContent = data.pending || 0;
                    document.getElementById('qsProcessing').textContent = data.processing || 0;
                    document.getElementById('qsCompleted').textContent = data.completed || 0;
                    document.getElementById('qsFailed').textContent = data.failed || 0;
                    if (typeof accSetTabCount === 'function') accSetTabCount(data.failed || 0);
                    var slipEl = document.getElementById('qsSlip');
                    if (slipEl) slipEl.textContent = data.awaitingSlip || 0;
                    renderClassBar(data);

                    queueState.page = data.page || 1;
                    queueState.totalPages = data.totalPages || 1;

                    if (data.success === false && data.message) {
                        // เซิร์ฟเวอร์ตอบว่าอ่านคิวไม่สำเร็จ — ต้องบอกผู้ใช้ ไม่ใช่เงียบ
                        var tb = document.getElementById('queueBody');
                        if (tb) tb.innerHTML = '<tr><td colspan="20" style="padding:16px;color:#C62828;">'
                            + '⚠️ ' + escHtml(data.message) + '</td></tr>';
                    }
                    renderQueue(data.items || []);
                    renderPagination(data.page, data.totalPages, data.totalItems);
                    updateFilterUI();
                })
                .catch(function(err) {
                    // เดิม log ลง console อย่างเดียว → หน้าที่โหลดไม่ได้จะดูเหมือน "กดแล้วไม่ไปไหน"
                    console.error(err);
                    var tb = document.getElementById('queueBody');
                    if (tb) tb.innerHTML = '<tr><td colspan="20" style="padding:16px;color:#C62828;">'
                        + '⚠️ โหลดหน้านี้ไม่สำเร็จ: ' + escHtml(err && err.message ? err.message : String(err))
                        + '<br><span style="color:#666;font-size:12px;">ดูสาเหตุจริงได้ที่ Logs หมวด AccountingIntegration</span>'
                        + '</td></tr>';
                });
        }

        function renderQueue(items) {
            var tbody = document.getElementById('queueBody');
            var selectAll = document.getElementById('selectAllQueue');
            if (selectAll) selectAll.checked = false;
            updateSelectedCount();

            if (!items.length) {
                var msg = queueState.q ? 'ไม่พบรายการที่ตรงกับ "' + queueState.q + '"'
                        : (queueState.status ? 'ไม่มีรายการสถานะนี้' : 'ไม่มีรายการในคิว');
                tbody.innerHTML = '<tr><td colspan="11" style="text-align:center; color:#999;">' + escapeHtml(msg) + '</td></tr>';
                return;
            }
            var html = '';
            items.forEach(function(item) {
                var statusClass = item.status === 'COMPLETED' ? 'status-connected' :
                                  item.status === 'FAILED' ? 'status-error' :
                                  item.status === 'PROCESSING' ? 'status-testing' : 'status-not-configured';
                var openCls = (item.status === 'FAILED' || (item.status === 'PENDING' && item.errorClass === 'WAIT')) ? item.errorClass : '';
                var rowStyle = item.sensitive ? ' style="background:#f9f3ff; opacity:0.85;"' : '';
                html += '<tr' + rowStyle + '>';
                var qid = parseInt(item.id, 10) || 0;
                html += '<td><input type="checkbox" class="queue-check" value="' + qid + '" onchange="updateSelectedCount()" /></td>';
                html += '<td>' + qid + '</td>';
                html += '<td title="' + escapeHtml(item.entityType) + '">' + escapeHtml(item.entityLabel || item.entityType) + ' #' + escapeHtml(item.entityId) + '</td>';
                html += '<td title="' + escapeHtml(item.actionType) + '">' + escapeHtml(item.actionLabel || item.actionType) + '</td>';
                html += '<td><span class="status-badge ' + statusClass + '" title="' + escapeHtml(item.status) + '">' + escapeHtml(item.statusLabel || item.status) + '</span>' + classBadge(item) + '</td>';
                // NextAcc Document column
                html += '<td>';
                if (item.nexaaccDocNumber) {
                    if (item.nexaaccUrl) {
                        html += '<a href="' + escapeHtml(item.nexaaccUrl) + '" target="_blank" class="nexaacc-doc-link" title="เปิดใน NextAcc">' + escapeHtml(item.nexaaccDocNumber) + ' <i class="fas fa-external-link-alt"></i></a>';
                    } else {
                        html += '<span class="nexaacc-doc-label">' + escapeHtml(item.nexaaccDocNumber) + '</span>';
                    }
                } else if (item.nexaaccId && item.status === 'COMPLETED') {
                    var shortId = String(item.nexaaccId).substring(0, 8) + '...';
                    if (item.nexaaccUrl) {
                        html += '<a href="' + escapeHtml(item.nexaaccUrl) + '" target="_blank" class="nexaacc-doc-link" title="' + escapeHtml(item.nexaaccId) + '">' + escapeHtml(shortId) + ' <i class="fas fa-external-link-alt"></i></a>';
                    } else {
                        html += '<span class="nexaacc-doc-label" title="' + escapeHtml(item.nexaaccId) + '">' + escapeHtml(shortId) + '</span>';
                    }
                } else {
                    html += '-';
                }
                html += '</td>';
                // ตรวจสอบ (post-sync verify) ✅ PASS / ⚠ WARN — tooltip = รายละเอียด
                html += '<td style="text-align:center;">';
                if (item.verifyStatus === 'PASS') {
                    html += '<span title="' + escapeHtml(item.verifyDetail) + '" style="color:#2E7D32; cursor:help;">✅</span>';
                } else if (item.verifyStatus === 'WARN') {
                    html += '<span title="' + escapeHtml(item.verifyDetail) + '" style="color:#C62828; cursor:help; font-weight:600;">⚠</span>';
                } else {
                    html += '<span style="color:#ccc;">–</span>';
                }
                html += '</td>';
                html += '<td>' + escapeHtml(item.retryCount) + '/' + escapeHtml(item.maxRetries) + '</td>';
                // ข้อความ error มาจาก NextAcc/คิว (บางครั้งเป็นหน้า HTML ทั้งหน้า หรือมีชื่อลูกค้า) — encode ทั้งใน title และเนื้อหา
                html += '<td style="max-width:200px; overflow:hidden; text-overflow:ellipsis;" title="' + escapeHtml(item.error) + '">' + (item.error ? escapeHtml(item.error) : '-') + '</td>';
                html += '<td>' + escapeHtml(item.created) + '</td>';
                html += '<td>';
                if (item.sensitive) {
                    html += '<span style="font-size:11px; color:#999;">🔒</span>';
                } else {
                    html += fixHereLink(item);
                    var dup = item.status === 'FAILED' && openCls === 'DUPLICATE';
                    if (item.status === 'FAILED' && openCls === 'DATA') {
                        // ข้อมูลผิด: ลองใหม่เปล่าไม่ช่วย → ตรวจซ้ำ (คืนเข้าคิวเฉพาะเมื่อผ่าน)
                        html += '<button type="button" class="btn-primary" style="padding:4px 10px; font-size:11px;" onclick="recheckItem(' + qid + ')" title="ตรวจข้อมูลใบใหม่ — ผ่านแล้วคืนเข้าคิว">ตรวจซ้ำ</button> ';
                    } else if (item.status === 'FAILED' && (openCls === 'CONFIG' || openCls === 'POLICY')) {
                        // ตั้งค่า: กลับเข้าคิวเองเมื่อบันทึก mapping · นโยบาย: ต้องใบลดหนี้ — ซ่อนปุ่มลองใหม่
                    } else if (dup) {
                        // อาจสร้างเอกสารบน NextAcc ไปแล้ว — ตรวจก่อน · ลองใหม่ได้เฉพาะเจ้าของระบบพร้อมเหตุผล (บันทึก log)
                        html += '<button type="button" class="btn-default" style="padding:4px 10px; font-size:11px;" onclick="retryDuplicate(' + qid + ', \'retryItem\')" title="อาจมีเอกสารบน NextAcc แล้ว — เจ้าของระบบลองใหม่ได้พร้อมเหตุผล">ลองใหม่ (เจ้าของ)</button> ';
                    } else if (item.status === 'FAILED') {
                        html += '<button type="button" class="btn-primary" style="padding:4px 10px; font-size:11px;" onclick="retryItem(' + qid + ')" title="ลองส่งอีกครั้ง"><i class="fas fa-redo"></i></button> ';
                    } else if (item.status === 'PENDING' && openCls === 'WAIT') {
                        html += '<button type="button" class="btn-default" style="padding:4px 10px; font-size:11px;" onclick="retryItem(' + qid + ')" title="ไม่รอรอบตรวจถัดไป — ลองตอนนี้">ตรวจตอนนี้</button> ';
                    }
                    if (item.status === 'COMPLETED' || (item.status === 'FAILED' && !dup)) {
                        html += '<button type="button" class="btn-warning" style="padding:4px 10px; font-size:11px;" onclick="resyncItem(' + qid + ')" title="ส่งใหม่ทั้งรายการ (ลบผลเดิม)"><i class="fas fa-sync-alt"></i></button> ';
                    }
                    // ปิดรายการที่ "เสร็จจริงบน NextAcc แล้ว" โดยไม่ยิง API ซ้ำ
                    if (item.status === 'FAILED') {
                        html += '<button type="button" class="btn-success" style="padding:4px 10px; font-size:11px;" onclick="closeItem(' + qid + ')" title="ปิดรายการ — งานเสร็จบน NextAcc แล้ว (ไม่ยิง API ซ้ำ)"><i class="fas fa-check"></i></button> ';
                    }
                    html += '<button type="button" class="btn-secondary" style="padding:4px 10px; font-size:11px;" onclick="showItemLogs(' + qid + ')" title="ดู log ละเอียด (เต็ม ไม่ตัด)"><i class="fas fa-file-alt"></i> Log</button>';
                }
                html += '</td>';
                html += '</tr>';
            });
            tbody.innerHTML = html;
        }

        // ข้อความทุกตัวที่มาจากเซิร์ฟเวอร์/NextAcc/คิว ต้องผ่านตัวนี้ก่อนใส่ innerHTML (CLAUDE.md ข้อ 23 — P0-1)
        function escapeHtml(s) {
            return (s === null || s === undefined ? '' : String(s))
                .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
                .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
        }

        function showItemLogs(queueId) {
            var modal = document.getElementById('logModal');
            var body = document.getElementById('logModalBody');
            var title = document.getElementById('logModalTitle');
            title.innerHTML = '<i class="fas fa-file-alt"></i> Log รายละเอียด — คิว #' + queueId;
            body.innerHTML = '<div style="text-align:center; color:#999;">กำลังโหลด...</div>';
            modal.style.display = 'block';

            // กันค้าง: ถ้าเซิร์ฟเวอร์ไม่ตอบใน 40 วิ ให้ยกเลิกและแจ้งผู้ใช้ (เดิมหมุนค้างไปเรื่อย ๆ)
            var ctrl = (typeof AbortController !== 'undefined') ? new AbortController() : null;
            var giveUp = setTimeout(function () { if (ctrl) ctrl.abort(); }, 40000);

            fetch(pageUrl + '?action=itemLogs&queueId=' + queueId + '&_=' + Date.now(),
                  ctrl ? { signal: ctrl.signal } : undefined)
                .then(function(r) { return r.text(); })
                .then(function(txt) {
                    clearTimeout(giveUp);
                    var data;
                    try { data = JSON.parse(txt); }
                    catch (e) {
                        body.innerHTML = '<div class="test-result error" style="display:block;">เซิร์ฟเวอร์ตอบไม่ใช่ JSON</div>'
                                       + '<pre style="white-space:pre-wrap; word-break:break-word; font-size:11px; max-height:300px; overflow:auto;">'
                                       + escapeHtml(txt.substring(0, 3000)) + '</pre>';
                        return;
                    }
                    if (!data.success) {
                        body.innerHTML = '<div class="test-result error" style="display:block;"><i class="fas fa-times-circle"></i> ' + escapeHtml(data.message) + '</div>';
                        return;
                    }
                    var html = '';
                    // Key identifiers
                    if (data.keys) {
                        html += '<div style="font-size:12px; color:#666; margin-bottom:10px;">ค้นจาก: <code>' + escapeHtml(data.keys) + '</code></div>';
                    }
                    // Full error (untruncated)
                    if (data.error) {
                        html += '<div style="margin-bottom:14px;"><div style="font-weight:600; margin-bottom:4px; color:#C62828;">Error (เต็ม):</div>'
                              + '<pre style="white-space:pre-wrap; word-break:break-word; background:#FFF5F5; border:1px solid #FFCDD2; border-radius:6px; padding:10px; font-size:12px; margin:0;">' + escapeHtml(data.error) + '</pre></div>';
                    }
                    // Log lines
                    html += '<div style="font-weight:600; margin-bottom:6px;">AccountingSync log (' + (data.logs ? data.logs.length : 0) + ' บรรทัด):</div>';
                    if (data.note) {
                        html += '<div style="font-size:11.5px; color:#8a6d3b; background:#fcf8e3; border:1px solid #faebcc; border-radius:5px; padding:6px 8px; margin-bottom:8px;">'
                              + escapeHtml(data.note) + '</div>';
                    }
                    if (!data.logs || !data.logs.length) {
                        html += '<div style="color:#999; font-size:13px;">ไม่พบ log ที่เกี่ยวข้อง (ลองกด Retry ก่อนเพื่อให้เกิด log ใหม่ หรือค้นจากเลขใบเสร็จโดยตรง)</div>';
                    } else {
                        html += '<div style="border:1px solid #eee; border-radius:6px; overflow:hidden;">';
                        data.logs.forEach(function(lg, i) {
                            var bg = i % 2 ? '#fafafa' : '#fff';
                            var isWarn = (lg.detail || '').indexOf('⚠') >= 0 || /ล้มเหลว|ไม่สำเร็จ|error/i.test(lg.detail || '');
                            html += '<div style="padding:8px 10px; background:' + bg + '; border-bottom:1px solid #f0f0f0;">'
                                  + '<div style="font-size:11px; color:#999; margin-bottom:2px;">' + escapeHtml(lg.time) + '</div>'
                                  + '<div style="font-size:12.5px; white-space:pre-wrap; word-break:break-word;' + (isWarn ? ' color:#C62828;' : '') + '">' + escapeHtml(lg.detail) + '</div></div>';
                        });
                        html += '</div>';
                    }
                    body.innerHTML = html;
                })
                .catch(function(err) {
                    clearTimeout(giveUp);
                    var msg = (err && err.name === 'AbortError')
                        ? 'โหลด log ไม่สำเร็จ: เกิน 40 วินาที (ตาราง Logs ใหญ่/DB ช้า) — ลองใหม่ หรือค้นจากหน้า Logs โดยตรง'
                        : (err && err.message) || 'โหลด log ไม่สำเร็จ';
                    body.innerHTML = '<div class="test-result error" style="display:block;"><i class="fas fa-times-circle"></i> ' + escapeHtml(msg) + '</div>';
                });
        }

        function closeLogModal() {
            document.getElementById('logModal').style.display = 'none';
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

        function retryItem(queueId, reason) {
            fetch(pageUrl + '?action=retryItem&queueId=' + queueId + (reason ? '&reason=' + encodeURIComponent(reason) : '') + '&_=' + Date.now())
                .then(function(r) { return r.json(); })
                .then(function(data) {
                    if (!data.success && data.message) alert(data.message);   // เดิมเงียบ — ผู้ใช้ไม่รู้ว่าทำไมไม่ลองใหม่
                    loadQueueData();
                })
                .catch(function(err) { alert(err.message); });
        }

        // รายการชนิด "ตรวจบน NextAcc" (อาจสร้างเอกสารไปแล้ว): เจ้าของระบบลองใหม่ได้พร้อมเหตุผล — เซิร์ฟเวอร์ตรวจสิทธิ์/บันทึก log ซ้ำ
        function retryDuplicate(queueId, action) {
            var r = prompt('รายการ #' + queueId + ' อาจสร้างเอกสารบน NextAcc ไปแล้ว — ตรวจบน NextAcc ก่อน\n'
                         + 'ถ้าเอกสารขึ้นแล้ว ให้กด ✔ ปิดรายการแทน (ไม่ส่งซ้ำ)\n\n'
                         + 'ยืนยันลองใหม่: พิมพ์เหตุผลอย่างน้อย 10 ตัวอักษร (บันทึกในประวัติ)', '');
            if (r === null) return;
            r = r.trim();
            if (r.length < 10) { alert('เหตุผลต้องยาวอย่างน้อย 10 ตัวอักษร'); return; }
            if (action === 'resyncItem') resyncItem(queueId, r); else retryItem(queueId, r);
        }

        function resyncItem(queueId, reason) {
            if (!reason && !confirm('ยืนยันส่งรายการคิว #' + queueId + ' ขึ้น NextAcc ใหม่?\n(จะลบผลเดิมและสร้างใหม่บน NextAcc)'))
                return;
            fetch(pageUrl + '?action=resyncItem&queueId=' + queueId + (reason ? '&reason=' + encodeURIComponent(reason) : '') + '&_=' + Date.now())
                .then(function(r) { return r.json(); })
                .then(function(data) {
                    alert(data.message);
                    loadQueueData();
                })
                .catch(function(err) { alert(err.message); });
        }

        function pushBuyerContact() {
            var el = document.getElementById('rlResult');
            var r = document.getElementById('rlReceipt').value.trim();
            if (!r) { el.innerHTML = '<div class="test-result error" style="display:block;">ใส่เลขใบเสร็จในระบบก่อน</div>'; return; }
            el.innerHTML = '<div class="test-result loading">กำลังส่งข้อมูลผู้ติดต่อ...</div>';
            fetch(pageUrl + '?action=pushBuyerContact&receipt=' + encodeURIComponent(r) + '&_=' + Date.now())
                .then(function (x) { return x.json(); })
                .then(function (data) {
                    el.innerHTML = '<pre style="white-space:pre-wrap; word-break:break-word; padding:10px; border-radius:6px; font-size:12px; margin:0; '
                                 + (data.success ? 'background:#E8F5E9;border:1px solid #A5D6A7;' : 'background:#FFF5F5;border:1px solid #FFCDD2;')
                                 + '">' + escapeHtml(data.message) + '</pre>';
                })
                .catch(function (err) {
                    el.innerHTML = '<div class="test-result error" style="display:block;">' + escapeHtml(err.message) + '</div>';
                });
        }

        function inspectReceipt() {
            var el = document.getElementById('rlResult');
            var r = document.getElementById('rlReceipt').value.trim();
            if (!r) { el.innerHTML = '<div class="test-result error" style="display:block;">ใส่เลขใบเสร็จในระบบก่อน</div>'; return; }
            el.innerHTML = '<div class="test-result loading">กำลังตรวจ...</div>';
            fetch(pageUrl + '?action=inspectReceipt&receipt=' + encodeURIComponent(r) + '&_=' + Date.now())
                .then(function (x) { return x.json(); })
                .then(function (data) {
                    el.innerHTML = '<pre style="white-space:pre-wrap; word-break:break-word; background:#F8FAFC; border:1px solid #E2E8F0;'
                                 + ' border-radius:6px; padding:10px; font-size:12px; margin:0;">'
                                 + escapeHtml(data.message) + '</pre>';
                })
                .catch(function (err) {
                    el.innerHTML = '<div class="test-result error" style="display:block;">' + escapeHtml(err.message) + '</div>';
                });
        }

        function relinkDoc() {
            var el = document.getElementById('rlResult');
            var r = document.getElementById('rlReceipt').value.trim();
            var d = document.getElementById('rlDoc').value.trim();
            if (!r) { el.innerHTML = '<div class="test-result error" style="display:block;">ใส่เลขใบเสร็จในระบบก่อน</div>'; return; }
            el.innerHTML = '<div class="test-result loading">กำลังดำเนินการ...</div>';
            fetch(pageUrl + '?action=relinkDoc&receipt=' + encodeURIComponent(r) + '&doc=' + encodeURIComponent(d) + '&_=' + Date.now())
                .then(function (x) { return x.json(); })
                .then(function (data) {
                    el.innerHTML = '<div class="test-result ' + (data.success ? 'success' : 'error') + '" style="display:block; white-space:pre-wrap;">'
                                 + (data.success ? '✅ ' : '❌ ') + escapeHtml(data.message) + '</div>';
                })
                .catch(function (err) {
                    el.innerHTML = '<div class="test-result error" style="display:block;">' + escapeHtml(err.message) + '</div>';
                });
        }

        function runHealthCheck(targetId) {
            var el = document.getElementById(targetId || 'healthResult');
            el.innerHTML = '<div class="test-result loading">กำลังตรวจ...</div>';
            fetch(pageUrl + '?action=healthCheck&_=' + Date.now())
                .then(function (r) { return r.json(); })
                .then(function (data) {
                    if (!data.success) {
                        el.innerHTML = '<div class="test-result error" style="display:block;">ตรวจไม่สำเร็จ: ' + escapeHtml(data.message) + '</div>';
                        return;
                    }
                    // แถบรุ่นโค้ดที่รันอยู่จริง — ใช้ยืนยันว่า deploy DLL ใหม่แล้วหรือยัง
                    var build = '<div style="font-size:11px; color:#555; background:#F3F4F6; border:1px solid #E5E7EB;'
                              + ' border-radius:5px; padding:6px 9px; margin-bottom:8px;">'
                              + '<b>รุ่นโค้ดที่รันอยู่:</b> ' + escapeHtml(data.build || '?')
                              + ' <span style="color:#888;">· DLL deploy เมื่อ ' + escapeHtml(data.buildDate || '?') + '</span></div>';

                    var list = data.issues || [];
                    if (!list.length) {
                        el.innerHTML = build
                                     + '<div class="test-result success" style="display:block;"><i class="fas fa-check-circle"></i> '
                                     + 'ไม่พบปัญหา — mapping/ผังบัญชี/แหล่งเงิน/คิว ปกติทั้งหมด '
                                     + '<span style="color:#888;font-size:11px;">(' + escapeHtml(data.checkedAt) + ')</span></div>';
                        return;
                    }
                    var html = build
                             + '<div style="font-size:12px; color:#666; margin-bottom:6px;">พบ ' + list.length
                             + ' เรื่องที่ควรดู <span style="color:#999;">(' + escapeHtml(data.checkedAt) + ')</span></div>';
                    list.forEach(function (it) {
                        var err = it.level === 'error';
                        var info = it.level === 'info';   // สถานะปกติ/บอกผลลัพธ์ ไม่ใช่ปัญหา
                        var bar = err ? '#C62828' : (info ? '#1565C0' : '#F9A825');
                        var bg = err ? '#FFF5F5' : (info ? '#F4F8FD' : '#FFFDF3');
                        var fg = err ? '#C62828' : (info ? '#1565C0' : '#8a6d3b');
                        var icon = err ? '❌ ' : (info ? 'ℹ️ ' : '⚠️ ');
                        html += '<div style="border-left:4px solid ' + bar
                              + '; background:' + bg
                              + '; border-radius:5px; padding:9px 12px; margin-bottom:7px;">'
                              + '<div style="font-weight:600; color:' + fg + '; font-size:13px;">'
                              + icon + escapeHtml(it.title) + '</div>'
                              + '<div style="font-size:12px; color:#555; white-space:pre-wrap; margin-top:3px;">'
                              + escapeHtml(it.detail) + '</div>'
                              + healthFixLink(it)
                              + (it.adminCodes ? '<details style="margin-top:4px; font-size:11px; color:#888;"><summary>รายละเอียดสำหรับผู้ดูแล</summary>'
                                    + 'สคริปต์ฐานข้อมูล: ' + escapeHtml(it.adminCodes) + '</details>' : '')
                              + '</div>';
                    });
                    el.innerHTML = html;
                })
                .catch(function (err) {
                    el.innerHTML = '<div class="test-result error" style="display:block;">ตรวจไม่สำเร็จ: ' + escapeHtml(err.message) + '</div>';
                });
        }

        // "ลองใหม่ทั้งหมด": ดูจำนวนตามชนิดก่อน → ยืนยัน → เซิร์ฟเวอร์รีเซ็ตเฉพาะชนิดที่ปลอดภัย แล้วรายงานสิ่งที่ข้ามข้างปุ่ม
        // ลิงก์ "แก้ที่นี่" ของตรวจสุขภาพ: "?focus=…" = เลื่อนไปส่วนนั้นในหน้านี้ · "/…" = เปิดหน้าอื่น
        function healthFixLink(it) {
            var u = it && it.fixUrl ? String(it.fixUrl) : '';
            var label = escapeHtml((it && it.fixLabel) || 'แก้ที่นี่');
            var style = 'display:inline-block; margin-top:6px; padding:3px 10px; font-size:11.5px; text-decoration:none;';
            if (u.charAt(0) === '?')
                return '<a href="#" class="btn-primary" style="' + style + '" onclick="return gotoFix(\'' + escapeHtml(u.replace(/[\\']/g, '')) + '\')">🛠 ' + label + '</a>';
            if (/^\/[A-Za-z]/.test(u))
                return '<a href="' + escapeHtml(appBase + u) + '" target="_blank" class="btn-primary" style="' + style + '">🛠 ' + label + '</a>';
            return '';
        }

        function gotoFix(qs) {
            try { applyFocus(new URLSearchParams(qs)); } catch (e) { }
            return false;
        }

        function retryAllFailed() {
            var el = document.getElementById('retryAllResult');
            el.innerHTML = '<div class="test-result loading" style="display:block;">กำลังนับรายการ...</div>';
            fetch(pageUrl + '?action=retryAllPreview&_=' + Date.now())
                .then(function (r) { return r.json(); })
                .then(function (pv) {
                    if (!pv.success) { el.innerHTML = '<div class="test-result error" style="display:block;">' + escapeHtml(pv.message) + '</div>'; return; }
                    if (!pv.retry) {
                        el.innerHTML = '<pre style="white-space:pre-wrap; margin:0; padding:8px 10px; border-radius:5px; background:#FFFDF3; border:1px solid #FFE082; font-size:12px;">'
                                     + escapeHtml(pv.message) + '</pre>';
                        return;
                    }
                    if (!confirm(pv.message)) { el.innerHTML = ''; return; }
                    el.innerHTML = '<div class="test-result loading" style="display:block;">กำลังคืนรายการเข้าคิว...</div>';
                    return fetch(pageUrl + '?action=retryAllFailed&_=' + Date.now())
                        .then(function (r) { return r.json(); })
                        .then(function (data) {
                            el.innerHTML = '<pre style="white-space:pre-wrap; margin:0; padding:8px 10px; border-radius:5px; font-size:12px; '
                                         + (data.success ? 'background:#F1F8E9; border:1px solid #C5E1A5;' : 'background:#FFF5F5; border:1px solid #FFCDD2;') + '">'
                                         + escapeHtml(data.message) + '</pre>';
                            loadQueueData();
                        });
                })
                .catch(function (err) { el.innerHTML = '<div class="test-result error" style="display:block;">' + escapeHtml(err.message) + '</div>'; });
        }

        function getAction(action, resultId, after) {
            var el = document.getElementById(resultId);
            el.className = 'test-result loading';
            el.textContent = 'กำลังดำเนินการ...';

            var controller = new AbortController();
            var timeoutId = setTimeout(function() { controller.abort(); }, 60000);

            fetch(pageUrl + '?action=' + action + '&_=' + Date.now(), { signal: controller.signal })
                .then(function(r) { clearTimeout(timeoutId); return r.json(); })
                .then(function(data) {
                    el.className = 'test-result ' + (data.success ? 'success' : 'error');
                    el.innerHTML = (data.success ? '<i class="fas fa-check-circle"></i> ' : '<i class="fas fa-times-circle"></i> ') + escapeHtml(data.message);
                    if (after) after(!!data.success, data);
                })
                .catch(function(err) {
                    clearTimeout(timeoutId);
                    el.className = 'test-result error';
                    var msg = err.name === 'AbortError' ? 'หมดเวลา — เซิร์ฟเวอร์ไม่ตอบกลับภายใน 60 วินาที' : err.message;
                    el.innerHTML = '<i class="fas fa-times-circle"></i> ' + escapeHtml(msg);
                    if (after) after(false);
                });
        }

        var _postActionBusy = false;
        // opts (ข้อ 26 วิซาร์ด): { noReload: true, done: function(ok, result) } — ไม่ส่ง = เหมือนเดิม (สำเร็จแล้วโหลดหน้าใหม่)
        function postAction(data, resultId, opts) {
            opts = opts || {};
            var el = document.getElementById(resultId);
            if (_postActionBusy) return;          // กันกดบันทึกซ้ำระหว่างรอ
            _postActionBusy = true;
            el.className = 'test-result loading';
            el.textContent = 'กำลังบันทึก...';

            // timeout 60s กันค้าง "กำลังบันทึก..." ถาวรถ้าเครือข่าย/เซิร์ฟเวอร์ไม่ตอบ
            var controller = new AbortController();
            var timer = setTimeout(function() { controller.abort(); }, 60000);

            fetch(pageUrl + '?action=' + data.action, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(data),
                signal: controller.signal
            })
            .then(function(r) { return r.json(); })
            .then(function(result) {
                el.className = 'test-result ' + (result.success ? 'success' : 'error');
                el.innerHTML = (result.success ? '<i class="fas fa-check-circle"></i> ' : '<i class="fas fa-times-circle"></i> ') + escapeHtml(result.message);
                if (result.success && !opts.noReload) {
                    setTimeout(function() { window.location.reload(); }, 1200);
                }
                if (opts.done) opts.done(!!result.success, result);
            })
            .catch(function(err) {
                el.className = 'test-result error';
                el.innerHTML = '<i class="fas fa-times-circle"></i> ' +
                    (err.name === 'AbortError' ? 'หมดเวลาการเชื่อมต่อ (60 วินาที) — ลองใหม่อีกครั้ง' : escapeHtml(err.message));
                if (opts.done) opts.done(false);
            })
            .then(function() { clearTimeout(timer); _postActionBusy = false; });
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
                    if (!data.success) {
                        var mr = document.getElementById('mappingResult');
                        if (mr) { mr.className = 'test-result error'; mr.textContent = 'โหลดการผูกบัญชีไม่สำเร็จ: ' + (data.message || ''); }
                        return;
                    }
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
                    ? '<span class="status-badge status-connected">ผูกแล้ว</span>'
                    : '<span class="status-badge status-error">ยังไม่ผูก</span>';

                html += '<tr>';
                html += '<td><strong>' + escapeHtml(item.code) + '</strong></td>';
                html += '<td style="font-size:12px;">' + escapeHtml(item.description || '-') + '</td>';
                html += '<td>' + buildAccountSelect(item.id, item.accountCode, item.accountId) + '</td>';
                html += '<td><span style="font-size:11px; color:#666;">' + escapeHtml(item.mappingType || '') + '</span></td>';
                html += '<td>' + statusBadge + '</td>';
                html += '<td><button type="button" class="btn-primary" style="padding:4px 10px; font-size:11px;" onclick="updateMapping(' + (parseInt(item.id, 10) || 0) + ')"><i class="fas fa-save"></i></button></td>';
                html += '</tr>';
            });
            tbody.innerHTML = html;
        }

        // idPrefix (ข้อ 26 วิซาร์ด): id ของ select = idPrefix + mappingId (ค่าเริ่มต้น mapSelect_ เดิม — กัน id ชนกับตารางหลัก)
        function buildAccountSelect(mappingId, currentCode, currentAccountId, idPrefix) {
            if (nexaaccAccountsCache.length === 0) {
                // Fallback to text input if no cached accounts
                return '<input type="text" id="' + (idPrefix ? idPrefix + 'code_' : 'mapCode_') + mappingId + '" value="' + escapeHtml(currentCode || '') + '" '
                    + 'style="width:120px; padding:4px 6px; border:1px solid #ddd; border-radius:4px; font-size:12px;" />'
                    + '<input type="hidden" id="mapAccId_' + mappingId + '" value="" />'
                    + '<div style="font-size:10px; color:#999; margin-top:2px;">กด "ดึงผังบัญชี" เพื่อแสดงรายการให้เลือก</div>';
            }

            var selId = (idPrefix || 'mapSelect_') + mappingId;
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
                html += '<optgroup label="' + escapeHtml(typeName) + '">';
                accs.forEach(function(acc) {
                    var indent = '';
                    for (var i = 0; i < (acc.level || 0); i++) indent += '&nbsp;&nbsp;';
                    var selected = '';
                    if (currentAccountId && acc.id === currentAccountId) selected = ' selected';
                    else if (!currentAccountId && currentCode && acc.code === currentCode) selected = ' selected';
                    html += '<option value="' + escapeHtml(acc.id + '|' + acc.code) + '"' + selected + '>'
                        + indent + escapeHtml(acc.code) + ' - ' + escapeHtml(acc.name || acc.nameEn || '') + '</option>';
                });
                html += '</optgroup>';
            });

            // Any remaining types not in typeOrder
            for (var t in groups) {
                if (typeOrder.indexOf(t) === -1) {
                    var accs = groups[t];
                    html += '<optgroup label="' + escapeHtml(t) + '">';
                    accs.forEach(function(acc) {
                        var selected = '';
                        if (currentAccountId && acc.id === currentAccountId) selected = ' selected';
                        else if (!currentAccountId && currentCode && acc.code === currentCode) selected = ' selected';
                        html += '<option value="' + escapeHtml(acc.id + '|' + acc.code) + '"' + selected + '>'
                            + escapeHtml(acc.code) + ' - ' + escapeHtml(acc.name || acc.nameEn || '') + '</option>';
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

        // ── "กระเป๋าเงิน" จาก NextAcc (บัญชีธนาคาร/e-Wallet + ผังเงินสด/พักเงิน/ลูกหนี้) ──
        var walletOptions = null;   // { banks: [...], accounts: [...] } จาก action=walletOptions

        function waEsc(s) {
            return String(s == null ? '' : s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
                .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
        }

        function loadWalletOptions(callback) {
            fetch(pageUrl + '?action=walletOptions&_=' + Date.now())
                .then(function(r) { return r.json(); })
                .then(function(data) {
                    walletOptions = data.success ? data : { banks: [], accounts: [] };
                    var ls = document.getElementById('walletLastSync');
                    if (ls) ls.textContent = data.success
                        ? ('กระเป๋าเงิน NextAcc ' + (data.banks || []).length + ' บัญชี · ดึงล่าสุด ' + (data.lastSync || '-')
                           + (data.bankCacheReady ? '' : ' (ยังไม่ได้อัปเดตฐานข้อมูลส่วนบัญชีธนาคาร)'))
                        : '';
                    callback();
                })
                .catch(function() { walletOptions = { banks: [], accounts: [] }; callback(); });
        }

        function syncWallets(resultId, after) {
            var el = document.getElementById(resultId || 'paidHowResult');
            el.className = 'test-result loading';
            el.textContent = 'กำลังดึงผังบัญชี + บัญชีธนาคาร/กระเป๋าเงินจาก NextAcc...';
            var controller = new AbortController();
            var timer = setTimeout(function() { controller.abort(); }, 90000);
            fetch(pageUrl + '?action=syncWallets&_=' + Date.now(), { signal: controller.signal })
                .then(function(r) { clearTimeout(timer); return r.json(); })
                .then(function(data) {
                    el.className = 'test-result ' + (data.success ? 'success' : 'error');
                    el.innerHTML = (data.success ? '✓ ' : '✗ ') + waEsc(data.message).replace(/\n/g, '<br/>');
                    nexaaccAccountsCache = [];
                    loadPaidHowMapping();
                    if (after) after(!!data.success);
                })
                .catch(function(err) {
                    clearTimeout(timer);
                    el.className = 'test-result error';
                    el.innerHTML = '✗ ' + (err.name === 'AbortError' ? 'หมดเวลา (90 วินาที)' : waEsc(err.message));
                    if (after) after(false);
                });
        }

        function loadPaidHowMapping() {
            var body = document.getElementById('paidHowBody');
            body.innerHTML = '<tr><td colspan="4" style="text-align:center;color:#999;">กำลังโหลด...</td></tr>';

            loadWalletOptions(function() {
                fetch(pageUrl + '?action=getPaidHowMapping&_=' + Date.now())
                    .then(function(r) { return r.json(); })
                    .then(function(data) {
                        if (!data.success) { body.innerHTML = '<tr><td colspan="4" class="text-center" style="color:red;">' + waEsc(data.message) + '</td></tr>'; return; }
                        var items = data.items || [];
                        if (items.length === 0) { body.innerHTML = '<tr><td colspan="4" style="text-align:center;color:#999;">ไม่พบข้อมูลวิธีจ่ายเงิน</td></tr>'; return; }
                        var html = '';
                        items.forEach(function(item) {
                            var linked = item.accountId && item.accountId !== '' && item.accountId !== '00000000-0000-0000-0000-000000000000';
                            var badge = linked
                                ? '<span class="status-badge status-connected">' + waEsc(item.accountCode || '') + ' ✓</span>'
                                : '<span class="status-badge status-not-configured">ยังไม่ผูก</span>';
                            if (linked && item.bankName)
                                badge += '<div style="font-size:11px;color:#555;margin-top:3px;">' + waEsc(item.bankName) + ' ' + waEsc(item.bankNumber || '') + '</div>';
                            if (item.warning && linked)
                                badge += '<div style="font-size:11px;color:#c0392b;margin-top:3px;">⚠ ' + waEsc(item.warning) + '</div>';
                            html += '<tr>'
                                + '<td>' + item.id + '</td>'
                                + '<td><strong>' + waEsc(item.name || '') + '</strong>'
                                + (item.channelType ? '<div style="font-size:11px;color:#7f8c8d;">' + waEsc(item.channelType) + '</div>' : '') + '</td>'
                                + '<td>' + buildPaidHowSelect(item.id, item.accountCode, item.accountId, item.bankAccountId) + '</td>'
                                + '<td>' + badge + '</td>'
                                + '</tr>';
                        });
                        body.innerHTML = html;
                    })
                    .catch(function(err) { body.innerHTML = '<tr><td colspan="4" style="color:red;">' + waEsc(err.message) + '</td></tr>'; });
            });
        }

        // option value = accountId|accountCode|bankAccountId (bank ว่าง = เลือกจากผังบัญชีโดยตรง)
        // idPrefix/onchangeJs (ข้อ 26 วิซาร์ด): ใช้ id อื่น + ไม่บันทึกทันทีเมื่อเปลี่ยน (ค่าเริ่มต้น = phSelect_ + บันทึกทันทีแบบเดิม)
        function buildPaidHowSelect(itemId, currentCode, currentAccountId, currentBankId, idPrefix, onchangeJs) {
            var wo = walletOptions || { banks: [], accounts: [] };
            var banks = wo.banks || [], accs = wo.accounts || [];
            if (banks.length === 0 && accs.length === 0) {
                return '<span style="color:#e65100;font-size:12px;">กด "ดึงรายการจาก NextAcc" ก่อน</span>';
            }
            var chosen = false;
            // GUID จาก SQL CAST เป็นตัวพิมพ์ใหญ่ ส่วน Guid.ToString() เป็นตัวเล็ก → เทียบแบบไม่สนตัวพิมพ์
            currentAccountId = (currentAccountId || '').toLowerCase();
            currentBankId = (currentBankId || '').toLowerCase();
            if (currentAccountId === '00000000-0000-0000-0000-000000000000') currentAccountId = '';
            var html = '<select id="' + (idPrefix || 'phSelect_') + itemId + '" style="width:100%;padding:5px;border:1px solid #ddd;border-radius:4px;font-size:12px;" onchange="' + (onchangeJs || ('updatePaidHowAccount(' + itemId + ')')) + '">';
            html += '<option value="">-- เลือกกระเป๋าเงิน / บัญชี --</option>';

            if (banks.length > 0) {
                html += '<optgroup label="บัญชีธนาคาร / กระเป๋าเงิน (NextAcc)">';
                banks.forEach(function(b) {
                    var usable = b.active && b.linkedId;
                    var sel = (!chosen && currentBankId && String(b.id).toLowerCase() === currentBankId) ? ' selected' : '';
                    if (sel) chosen = true;
                    var label = (b.bank || '') + ' ' + (b.number || '') + (b.name ? ' · ' + b.name : '')
                        + (b.type ? ' (' + b.type + ')' : '')
                        + (b.linkedCode ? ' → ' + b.linkedCode : ' → ยังไม่ผูกผังบัญชีใน NextAcc')
                        + (b.active ? '' : ' [ปิดใช้งาน]');
                    html += '<option value="' + waEsc((b.linkedId || '') + '|' + (b.linkedCode || '') + '|' + b.id) + '"'
                        + sel + (usable ? '' : ' disabled') + '>' + waEsc(label) + '</option>';
                });
                html += '</optgroup>';
            }

            var groupLabels = [
                ['CASH', 'เงินสด / เงินฝาก / กระเป๋าเงิน Digital (111xx)'],
                ['RECEIVABLE', 'พักเงินเกตเวย์ / ลูกหนี้ OTA / ลูกหนี้อื่น (113xx)'],
                ['OTHER_ASSET', 'สินทรัพย์อื่น'],
                ['LIABILITY', 'หนี้สิน (เช่น เจ้าหนี้กรรมการ — ใช้กับฝั่งจ่ายเท่านั้น)']
            ];
            groupLabels.forEach(function(g) {
                var list = accs.filter(function(a) { return a.group === g[0]; });
                if (list.length === 0) return;
                html += '<optgroup label="' + waEsc(g[1]) + '">';
                list.forEach(function(a) {
                    var sel = '';
                    if (!chosen && !currentBankId) {
                        if (currentAccountId && String(a.id).toLowerCase() === currentAccountId) sel = ' selected';
                        else if (!currentAccountId && currentCode && a.code === currentCode) sel = ' selected';
                        if (sel) chosen = true;
                    }
                    html += '<option value="' + waEsc(a.id + '|' + a.code + '|') + '"' + sel + '>'
                        + waEsc(a.code + ' - ' + (a.name || '')) + '</option>';
                });
                html += '</optgroup>';
            });
            html += '</select>';
            return html;
        }

        function updatePaidHowAccount(itemId, idPrefix, resultId, after) {
            var sel = document.getElementById((idPrefix || 'phSelect_') + itemId);
            if (!sel || !sel.value) { if (after) after(false); return; }
            var parts = sel.value.split('|');
            var accountId = parts[0], accountCode = parts[1] || '', bankId = parts[2] || '';
            var el = document.getElementById(resultId || 'paidHowResult');
            if (!accountId) { el.className = 'test-result error'; el.textContent = '✗ บัญชีธนาคารนี้ยังไม่ผูกผังบัญชีใน NextAcc'; if (after) after(false); return; }
            el.className = 'test-result loading'; el.textContent = 'กำลังบันทึก...';

            fetch(pageUrl + '?action=updatePaidHowAccount&id=' + itemId
                + '&accountId=' + encodeURIComponent(accountId)
                + '&accountCode=' + encodeURIComponent(accountCode)
                + '&bankAccountId=' + encodeURIComponent(bankId) + '&_=' + Date.now())
                .then(function(r) { return r.json(); })
                .then(function(data) {
                    el.className = 'test-result ' + (data.success ? 'success' : 'error');
                    el.innerHTML = (data.success ? '✓ ' : '✗ ') + waEsc(data.message).replace(/\n/g, '<br/>');
                    if (after) after(!!data.success); else loadPaidHowMapping();
                })
                .catch(function(err) { el.className = 'test-result error'; el.innerHTML = '✗ ' + waEsc(err.message); if (after) after(false); });
        }

        // ── เกตเวย์ → แหล่งเงิน ──
        function loadGatewayPaidHow() {
            var body = document.getElementById('gwPaidHowBody');
            body.innerHTML = '<tr><td colspan="3" style="text-align:center;color:#999;">กำลังโหลด...</td></tr>';
            fetch(pageUrl + '?action=getGatewayPaidHow&_=' + Date.now())
                .then(function(r) { return r.json(); })
                .then(function(data) {
                    if (!data.success) { body.innerHTML = '<tr><td colspan="3" style="color:red;">' + waEsc(data.message) + '</td></tr>'; return; }
                    var fb = document.getElementById('gwFallbackName'); if (fb) fb.textContent = data.fallback || '';
                    var html = '';
                    (data.rows || []).forEach(function(row) {
                        var s = '<select id="gwSel_' + row.provider + '" style="width:100%;padding:5px;font-size:12px;" onchange="saveGatewayPaidHow(\'' + row.provider + '\')">'
                            + '<option value="">(ใช้แหล่งเงินกลาง)</option>';
                        (data.options || []).forEach(function(o) {
                            s += '<option value="' + waEsc(o.name) + '"' + (o.name === row.paidHow ? ' selected' : '') + '>'
                                + waEsc(o.name + (o.linked ? ' → ' + o.code : ' (ยังไม่ผูกบัญชี)')) + '</option>';
                        });
                        s += '</select>';
                        var eff = waEsc(row.effective || '')
                            + (row.effectiveLinked ? ' <span style="color:#27ae60;">→ ' + waEsc(row.effectiveCode || '') + '</span>'
                                                   : ' <span style="color:#e67e22;">(ยังไม่ผูกบัญชี)</span>')
                            + (row.effectiveSource ? '<div style="font-size:11px;color:#888;">' + waEsc(row.effectiveSource) + '</div>' : '');
                        html += '<tr><td><strong>' + waEsc(row.provider) + '</strong>' + (row.active ? ' <span class="status-badge status-connected">ใช้อยู่</span>' : '') + '</td>'
                            + '<td>' + s + '</td><td>' + eff + '</td></tr>';
                    });
                    body.innerHTML = html;
                    var cb = document.getElementById('gwChannelBody');
                    if (cb) {
                        var ch = data.channels || [];
                        if (!ch.length) { cb.innerHTML = '<tr><td colspan="5" style="text-align:center;color:#999;">ไม่มีช่องทางที่ลูกค้าเห็น/ช่องเกตเวย์</td></tr>'; }
                        else {
                            var h2 = '';
                            ch.forEach(function(c) {
                                var warnRow = !!c.warn;
                                h2 += '<tr' + (warnRow ? ' style="background:#fff4e5;"' : '') + '>'
                                    + '<td><strong>' + waEsc(c.name) + '</strong><div style="font-size:11px;color:#888;">' + waEsc(c.code) + ' · ' + waEsc(c.type)
                                    + (c.customerVisible ? ' · ลูกค้าเห็น' : '') + (c.live ? '' : ' · ยังใช้ไม่ได้' + (c.unavailable ? ' (' + waEsc(c.unavailable) + ')' : '')) + '</div></td>'
                                    + '<td>' + waEsc((c.provider || '-') + (c.method ? ' / ' + c.method : '')) + '</td>'
                                    + '<td>' + waEsc(c.effective) + '</td>'
                                    + '<td style="font-size:12px;">' + waEsc(c.source) + '</td>'
                                    + '<td>' + (c.linked ? '<span style="color:#27ae60;">' + waEsc(c.accountCode || '✓') + '</span>'
                                                         : '<span style="color:#e67e22;">⚠ ' + waEsc(c.warn || 'ยังไม่ผูก') + '</span>') + '</td></tr>';
                            });
                            cb.innerHTML = h2;
                        }
                    }
                })
                .catch(function(err) { body.innerHTML = '<tr><td colspan="3" style="color:red;">' + waEsc(err.message) + '</td></tr>'; });
        }

        function saveGatewayPaidHow(provider) {
            var sel = document.getElementById('gwSel_' + provider);
            var el = document.getElementById('gwPaidHowResult');
            el.className = 'test-result loading'; el.textContent = 'กำลังบันทึก...';
            fetch(pageUrl + '?action=saveGatewayPaidHow', {
                method: 'POST', headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ action: 'saveGatewayPaidHow', provider: provider, paidHow: sel ? sel.value : '' })
            })
                .then(function(r) { return r.json(); })
                .then(function(data) {
                    el.className = 'test-result ' + (data.success ? 'success' : 'error');
                    el.innerHTML = (data.success ? '✓ ' : '✗ ') + waEsc(data.message);
                    loadGatewayPaidHow();
                })
                .catch(function(err) { el.className = 'test-result error'; el.innerHTML = '✗ ' + waEsc(err.message); });
        }

        // ── ยกเลิกเอกสาร OTA (OTADOC) ──
        function voidOtaDoc() {
            var el = document.getElementById('otaVoidResult');
            var rid = (document.getElementById('otaVoidResId').value || '').trim();
            if (!rid) { el.className = 'test-result error'; el.textContent = '✗ ระบุเลขการจอง'; return; }
            var repost = document.getElementById('otaVoidRepost').checked;
            if (!confirm('ยกเลิกเอกสาร OTA ของการจอง ' + rid + ' ใน NextAcc?\n'
                + (repost ? 'หลังยกเลิกสำเร็จ ระบบจะสร้างเอกสารใหม่รอบถัดไป' : 'จะไม่สร้างใหม่อัตโนมัติ'))) return;
            el.className = 'test-result loading'; el.textContent = 'กำลังเข้าคิว...';
            fetch(pageUrl + '?action=voidOtaDoc', {
                method: 'POST', headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ action: 'voidOtaDoc', reservationId: rid, allowRepost: repost,
                    reason: (document.getElementById('otaVoidReason').value || '').trim() })
            })
                .then(function(r) { return r.json(); })
                .then(function(data) {
                    el.className = 'test-result ' + (data.success ? 'success' : 'error');
                    el.innerHTML = (data.success ? '✓ ' : '✗ ') + waEsc(data.message || '');
                })
                .catch(function(err) { el.className = 'test-result error'; el.innerHTML = '✗ ' + waEsc(err.message); });
        }

        // ── OTA → บัญชี/ผู้ซื้อ ──
        var otaMapItems = [];
        function loadOtaChannelMap() {
            var body = document.getElementById('otaMapBody');
            body.innerHTML = '<tr><td colspan="6" style="text-align:center;color:#999;">กำลังโหลด...</td></tr>';
            fetch(pageUrl + '?action=getOtaChannelMap&_=' + Date.now())
                .then(function(r) { return r.json(); })
                .then(function(data) {
                    if (!data.success) { body.innerHTML = '<tr><td colspan="6" style="color:red;">' + waEsc(data.message) + '</td></tr>'; return; }
                    var ml = document.getElementById('otaModeLabel');
                    if (ml) ml.textContent = data.mode === 'RECEIPT_DOC' ? 'RECEIPT_DOC (สร้างเอกสาร)' : 'OFF (ยังไม่สร้างเอกสาร)';
                    otaMapItems = data.items || [];
                    if (otaMapItems.length === 0) { body.innerHTML = '<tr><td colspan="6" style="text-align:center;color:#999;">ยังไม่พบช่องทาง OTA</td></tr>'; return; }
                    var html = '';
                    otaMapItems.forEach(function(it, idx) {
                        var s = '<select id="otaPh_' + idx + '" style="width:100%;padding:5px;font-size:12px;">'
                            + '<option value="0">(ใช้ลูกหนี้ OTA กลาง OTA_RECEIVABLE)</option>';
                        (data.paidHows || []).forEach(function(p) {
                            s += '<option value="' + p.id + '"' + (p.id === it.paidHowId ? ' selected' : '') + '>'
                                + waEsc(p.name + (p.linked ? ' → ' + p.code : ' (ยังไม่ผูกบัญชี)')) + '</option>';
                        });
                        s += '</select>';
                        html += '<tr>'
                            + '<td><strong>' + waEsc(it.displayName) + '</strong><div style="font-size:11px;color:#7f8c8d;">' + waEsc(it.channelKey)
                            + (it.bookings ? ' · ' + it.bookings + ' การจอง' : '') + (it.saved ? '' : ' · ยังไม่บันทึก') + '</div></td>'
                            + '<td>' + s + (it.paidHowId && !it.linked ? '<div style="font-size:11px;color:#c0392b;">⚠ แหล่งเงินยังไม่ผูกบัญชี NextAcc</div>' : '') + '</td>'
                            + '<td><input type="text" id="otaBn_' + idx + '" value="' + waEsc(it.buyerName) + '" placeholder="' + waEsc(it.displayName) + '" style="width:100%;font-size:12px;" />'
                            + '<input type="text" id="otaBa_' + idx + '" value="' + waEsc(it.buyerAddress) + '" placeholder="ที่อยู่ (ไม่บังคับ)" style="width:100%;font-size:12px;margin-top:3px;" /></td>'
                            + '<td><input type="text" id="otaBt_' + idx + '" value="' + waEsc(it.buyerTaxId) + '" placeholder="13 หลัก / ว่าง" style="width:120px;font-size:12px;" />'
                            + '<input type="text" id="otaBb_' + idx + '" value="' + waEsc(it.buyerBranch) + '" placeholder="00000" style="width:70px;font-size:12px;margin-top:3px;" /></td>'
                            + '<td><input type="checkbox" id="otaAct_' + idx + '"' + (it.isActive ? ' checked' : '') + ' /></td>'
                            + '<td><button type="button" class="btn-success" onclick="saveOtaChannelMap(' + idx + ')">บันทึก</button></td>'
                            + '</tr>';
                    });
                    body.innerHTML = html;
                })
                .catch(function(err) { body.innerHTML = '<tr><td colspan="6" style="color:red;">' + waEsc(err.message) + '</td></tr>'; });
        }

        function saveOtaChannelMap(idx) {
            var it = otaMapItems[idx]; if (!it) return;
            var v = function(id) { var e = document.getElementById(id + '_' + idx); return e ? e.value.trim() : ''; };
            var el = document.getElementById('otaMapResult');
            el.className = 'test-result loading'; el.textContent = 'กำลังบันทึก...';
            fetch(pageUrl + '?action=saveOtaChannelMap', {
                method: 'POST', headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    action: 'saveOtaChannelMap', channelKey: it.channelKey, displayName: it.displayName,
                    paidHowId: v('otaPh'), buyerName: v('otaBn'), buyerAddress: v('otaBa'),
                    buyerTaxId: v('otaBt'), buyerBranch: v('otaBb'),
                    isActive: document.getElementById('otaAct_' + idx).checked
                })
            })
                .then(function(r) { return r.json(); })
                .then(function(data) {
                    el.className = 'test-result ' + (data.success ? 'success' : 'error');
                    el.innerHTML = (data.success ? '✓ ' : '✗ ') + waEsc(data.message).replace(/\n/g, '<br/>');
                    if (data.success) loadOtaChannelMap();
                })
                .catch(function(err) { el.className = 'test-result error'; el.innerHTML = '✗ ' + waEsc(err.message); });
        }

        // ═══════════ Expense Category → Account Mapping ═══════════

        function loadPaidTypeMapping() {
            var body = document.getElementById('paidTypeBody');
            body.innerHTML = '<tr><td colspan="4" style="text-align:center;color:#999;">กำลังโหลด...</td></tr>';

            ensureAccountsLoaded(function() {
                fetch(pageUrl + '?action=getPaidTypeMapping&_=' + Date.now())
                    .then(function(r) { return r.json(); })
                    .then(function(data) {
                        if (!data.success) { body.innerHTML = '<tr><td colspan="4" style="color:red;">' + escapeHtml(data.message) + '</td></tr>'; return; }
                        var items = data.items || [];
                        if (items.length === 0) { body.innerHTML = '<tr><td colspan="4" style="text-align:center;color:#999;">ไม่พบข้อมูลประเภทค่าใช้จ่าย</td></tr>'; return; }
                        var html = '';
                        items.forEach(function(item) {
                            var linked = item.accountId && item.accountId !== '' && item.accountId !== '00000000-0000-0000-0000-000000000000';
                            var badge = linked
                                ? '<span class="status-badge status-connected">' + escapeHtml(item.accountCode || '') + ' ✓</span>'
                                : '<span class="status-badge status-not-configured">ยังไม่ผูก</span>';
                            html += '<tr>'
                                + '<td>' + item.id + '</td>'
                                + '<td><strong>' + escapeHtml(item.name || '') + '</strong></td>'
                                + '<td>' + buildPaidTypeSelect(item.id, item.accountCode, item.accountId) + '</td>'
                                + '<td>' + badge + '</td>'
                                + '</tr>';
                        });
                        body.innerHTML = html;
                    })
                    .catch(function(err) { body.innerHTML = '<tr><td colspan="4" style="color:red;">' + escapeHtml(err.message) + '</td></tr>'; });
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
                    html += '<option value="' + acc.id + '|' + acc.code + '"' + sel + '>' + escapeHtml(acc.code) + ' - ' + escapeHtml(acc.name || acc.nameEn || '') + '</option>';
                });
                html += '</optgroup>';
            });
            for (var t in groups) {
                if (typeOrder.indexOf(t) === -1) {
                    html += '<optgroup label="' + escapeHtml(t) + '">';
                    groups[t].forEach(function(acc) {
                        var sel = '';
                        if (currentAccountId && acc.id === currentAccountId) sel = ' selected';
                        html += '<option value="' + acc.id + '|' + acc.code + '"' + sel + '>' + escapeHtml(acc.code) + ' - ' + escapeHtml(acc.name || acc.nameEn || '') + '</option>';
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
                    el.innerHTML = (data.success ? '✓ ' : '✗ ') + escapeHtml(data.message);
                    if (data.success) loadPaidTypeMapping();
                })
                .catch(function(err) { el.className = 'test-result error'; el.innerHTML = '✗ ' + escapeHtml(err.message); });
        }

        // idPrefix/resultId/after (ข้อ 26 วิซาร์ด) — ไม่ส่ง = ตารางหลักแบบเดิม
        function updateMapping(id, idPrefix, resultId, after) {
            var sel = document.getElementById((idPrefix || 'mapSelect_') + id);
            var fail = function (msg) {
                var re = document.getElementById(resultId || 'mappingResult');
                if (re) { re.className = 'test-result error'; re.textContent = msg; }
                if (after) after(false);
            };
            var accountCode = '';
            var accountId = '';

            if (sel) {
                var val = sel.value;
                if (!val) { fail('กรุณาเลือกบัญชี'); return; }
                var parts = val.split('|');
                accountId = parts[0] || '';
                accountCode = parts[1] || '';
            } else {
                // Fallback: text input mode
                var inp = document.getElementById((idPrefix ? idPrefix + 'code_' : 'mapCode_') + id);
                if (inp) accountCode = inp.value.trim();
                if (!accountCode) { fail('กรุณาใส่รหัสบัญชี'); return; }
            }

            var el = document.getElementById(resultId || 'mappingResult');
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
                    el.innerHTML = (data.success ? '<i class="fas fa-check-circle"></i> ' : '<i class="fas fa-times-circle"></i> ') + escapeHtml(data.message);
                    if (after) after(!!data.success);
                    else if (data.success) loadMappingsWithAccounts();
                })
                .catch(function(err) {
                    el.className = 'test-result error';
                    el.innerHTML = '<i class="fas fa-times-circle"></i> ' + escapeHtml(err.message);
                    if (after) after(false);
                });
        }
    </script>

    <script>
        // ═══════════ ข้อ 26: แท็บ + ป้ายค่าแนะนำ + ตัวอย่างรายการบัญชีมัดจำ + บันทึก + วิซาร์ดตั้งค่า ═══════════
        // ส่วนเสริมฝั่งเบราว์เซอร์ล้วน — ทุกการบันทึกยิง action เดิมของหน้านี้ (ชื่อ/สัญญาเดิม) ความหมายค่าตั้งไม่เปลี่ยน
        var ACC_TABS = ['connect', 'accounts', 'policy', 'queue', 'tools', 'intake', 'log'];
        // ?focus=… (ลิงก์จาก Telegram/ตรวจสุขภาพ/หน้าอื่น) → แท็บ
        var ACC_FOCUS_TAB = { api: 'connect', sync: 'policy', mapping: 'accounts', paidHow: 'accounts', gateway: 'accounts',
                              ota: 'accounts', paidType: 'accounts', email: 'intake', health: 'queue', queue: 'queue' };
        var accCurrentTab = '';
        var accTabLoaded = {};
        var accCfg = {};
        try { accCfg = JSON.parse(document.getElementById('<%= hfConfigData.ClientID %>').value || '{}') || {}; } catch (e) { accCfg = {}; }

        function accTabOf(el) {
            while (el && el !== document.body) {
                if (el.classList && el.classList.contains('acc-panel') && el.id && el.id.indexOf('tab-') === 0) return el.id.substring(4);
                el = el.parentNode;
            }
            return '';
        }

        function accActivateTab(name, opts) {
            opts = opts || {};
            if (ACC_TABS.indexOf(name) < 0) name = 'connect';
            for (var i = 0; i < ACC_TABS.length; i++) {
                var t = ACC_TABS[i], on = t === name;
                var p = document.getElementById('tab-' + t), b = document.getElementById('tabbtn-' + t);
                if (p) p.hidden = !on;
                if (b) { b.setAttribute('aria-selected', on ? 'true' : 'false'); b.tabIndex = on ? 0 : -1; }
            }
            accCurrentTab = name;
            if (!opts.noHash) {
                try { history.replaceState(null, '', window.location.pathname + window.location.search + '#t-' + name); } catch (e) { }
            }
            try { localStorage.setItem('accIntegrationTab', name); } catch (e) { }
            if (!accTabLoaded[name] && !opts.noLoad) { accTabLoaded[name] = true; accLazyLoad(name); }
        }

        // เปิดแท็บครั้งแรก = โหลดตารางให้เลย (เดิมต้องกด "โหลด…" ทีละตาราง จึงไม่เห็นว่ามีรายการที่ยังไม่ผูก)
        // skipFocus = ตารางที่ applyFocus (?focus=…) โหลดเองแล้ว — ไม่โหลดซ้ำ (กันตารางถูกวาดใหม่ทับแถวที่ไฮไลต์)
        function accLazyLoad(name, skipFocus) {
            try {
                if (name === 'accounts') {
                    var L = { mapping: loadMappingsWithAccounts, paidHow: loadPaidHowMapping, gateway: loadGatewayPaidHow, ota: loadOtaChannelMap, paidType: loadPaidTypeMapping };
                    for (var k in L) if (k !== skipFocus) L[k]();
                } else if (name === 'policy') {
                    accRenderDepositPreview();
                }
            } catch (e) { if (window.console) console.error(e); }
        }

        // ให้ applyFocus/ลิงก์ภายในเปิดแท็บที่มี element นั้นก่อนเลื่อนไปหา
        function accShowFor(elOrId) {
            var el = typeof elOrId === 'string' ? document.getElementById(elOrId) : elOrId;
            var t = accTabOf(el);
            if (t && t !== accCurrentTab) accActivateTab(t);
            return el;
        }

        // ไปแท็บ + เลื่อนถึงส่วนที่ระบุ + ไฮไลต์สั้น ๆ
        function accGoto(tab, elId) {
            accActivateTab(tab);
            var el = elId ? document.getElementById(elId) : null;
            if (!el) { window.scrollTo(0, 0); return false; }
            var dz = el.closest ? el.closest('details') : null;
            if (dz && !dz.open) dz.open = true;
            setTimeout(function () {
                el.scrollIntoView({ behavior: 'smooth', block: 'start' });
                el.classList.add('acc-flash');
                setTimeout(function () { el.classList.remove('acc-flash'); }, 4000);
            }, 60);
            return false;
        }

        function accSetTabCount(n) {
            var b = document.getElementById('tabFailedCount');
            if (!b) return;
            n = parseInt(n, 10) || 0;
            b.hidden = n <= 0;
            b.textContent = n > 99 ? '99+' : String(n);
            b.title = 'ล้มเหลว ' + n + ' รายการ';
        }

        // แท็บเริ่มต้น (รันทันทีตอน parse — ก่อน DOMContentLoaded ที่ focusFromQuery ทำงาน):
        //   #t-ชื่อแท็บ (ผู้ใช้อยู่แท็บนี้ก่อนรีโหลด) → ?focus/?cls/?status/?q (ลิงก์ภายนอก) → #idของส่วน (เช่น #queueStats) → แท็บล่าสุด → การเชื่อมต่อ
        (function accInitTabs() {
            var bar = document.querySelector('.acc-tabs');
            if (bar) {
                bar.addEventListener('click', function (ev) {
                    var b = ev.target.closest ? ev.target.closest('.acc-tab') : null;
                    if (b && b.getAttribute('data-tab')) accActivateTab(b.getAttribute('data-tab'));
                });
                bar.addEventListener('keydown', function (ev) {
                    if (ev.key !== 'ArrowRight' && ev.key !== 'ArrowLeft') return;
                    var i = ACC_TABS.indexOf(accCurrentTab);
                    i = ev.key === 'ArrowRight' ? (i + 1) % ACC_TABS.length : (i - 1 + ACC_TABS.length) % ACC_TABS.length;
                    accActivateTab(ACC_TABS[i]);
                    var nb = document.getElementById('tabbtn-' + ACC_TABS[i]); if (nb) nb.focus();
                    ev.preventDefault();
                });
            }
            var hash = (window.location.hash || '').replace(/^#/, '');
            var q; try { q = new URLSearchParams(window.location.search); } catch (e) { q = null; }
            var tab = '', fromFocus = false;
            if (/^t-[a-z]+$/.test(hash) && ACC_TABS.indexOf(hash.substring(2)) >= 0) tab = hash.substring(2);
            else if (q && (q.get('focus') || q.get('cls') || q.get('status') || q.get('q'))) { tab = ACC_FOCUS_TAB[q.get('focus') || 'queue'] || 'policy'; fromFocus = true; }
            else if (hash && /^[A-Za-z][\w-]*$/.test(hash) && document.getElementById(hash)) tab = accTabOf(document.getElementById(hash));
            if (!tab) { try { tab = localStorage.getItem('accIntegrationTab') || ''; } catch (e) { } }
            if (!tab) tab = (accCfg && accCfg.hasApiKey) ? 'queue' : 'connect';
            var keepHash = hash && !/^t-/.test(hash) && document.getElementById(hash);
            // มาจากลิงก์ ?focus= : ยังไม่ตั้ง #t- (ให้ focusFromQuery เลื่อน/ไฮไลต์ตามเดิม) และเลื่อนการโหลดตารางไปหลัง applyFocus
            accActivateTab(tab, { noHash: !!keepHash || fromFocus, noLoad: fromFocus });
            window.accFocusFrom = fromFocus ? (q.get('focus') || 'queue') : '';
            if (keepHash) setTimeout(function () { var el = document.getElementById(hash); if (el) el.scrollIntoView({ block: 'start' }); }, 300);
        })();

        // ── ป้ายค่าแนะนำ ─────────────────────────────────────────────
        function accMarkRecommended() {
            var list = document.querySelectorAll('.rec-badge[data-rec-for]');
            var defMode = (document.getElementById('cfgSyncMode') || {}).value || '';
            for (var i = 0; i < list.length; i++) {
                var b = list[i], el = document.getElementById(b.getAttribute('data-rec-for'));
                if (!el) continue;
                var v = el.type === 'checkbox' ? (el.checked ? 'true' : 'false') : (el.value || '');
                if (v === '' && (el.id === 'cfgReceiptSyncMode' || el.id === 'cfgVoucherSyncMode')) v = defMode;   // "ใช้ค่าเริ่มต้น"
                var rec = b.getAttribute('data-rec') || '', label = b.getAttribute('data-rec-label') || rec;
                if (v === rec) { b.className = 'rec-badge match'; b.textContent = '✓ ตามค่าแนะนำ'; b.title = 'ค่าแนะนำ: ' + label; }
                else { b.className = 'rec-badge differs'; b.textContent = 'ค่าแนะนำ: ' + label; b.title = 'ตอนนี้ต่างจากค่าแนะนำ'; }
            }
        }

        // ── ชนิดคีย์ (int_ = คีย์เชื่อมระบบ, acc_ = คีย์บริษัท) ───────────────
        function accKeyHint(inputId, hintId, expect) {
            var inp = document.getElementById(inputId), h = document.getElementById(hintId);
            if (!inp || !h) return;
            var v = (inp.value || '').trim();
            h.className = 'key-hint';
            if (!v) { h.textContent = ''; return; }
            if (v === '-' && expect === 'acc') { h.className = 'key-hint warn'; h.textContent = 'จะล้างคีย์บริษัทเมื่อบันทึก (กลับไปใช้คีย์เชื่อมระบบตัวเดียว)'; return; }
            if (/^int_/i.test(v)) {
                if (expect === 'int') { h.className = 'key-hint ok'; h.textContent = '✓ เป็นคีย์เชื่อมระบบ'; }
                else { h.className = 'key-hint warn'; h.textContent = 'นี่คือคีย์เชื่อมระบบ (int_) — ใส่ในช่อง "คีย์เชื่อมระบบ" แทน'; }
            } else if (/^acc_/i.test(v)) {
                if (expect === 'acc') { h.className = 'key-hint ok'; h.textContent = '✓ เป็นคีย์บริษัท'; }
                else { h.className = 'key-hint warn'; h.textContent = 'นี่คือคีย์บริษัท (acc_) — ใช้ส่งเอกสารไม่ได้ ต้องใช้คีย์เชื่อมระบบจากเมนู Integrations (ขึ้นต้น int_)'; }
            } else { h.className = 'key-hint warn'; h.textContent = 'ไม่รู้จักรูปแบบคีย์ — ปกติขึ้นต้นด้วย int_ หรือ acc_'; }
        }

        // ── ตัวอย่างรายการบัญชีมัดจำ 1,000 + เช็คเอาท์ยอดรวม 3,000 (ราคารวม VAT 7%) ──
        function accFmt(n) { return Number(n).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }); }
        function accJeTable(title, rows) {
            var h = '<div class="je-title">' + escapeHtml(title) + '</div><table><thead><tr><th>บัญชี</th><th class="n">เดบิต</th><th class="n">เครดิต</th></tr></thead><tbody>';
            rows.forEach(function (r) {
                h += '<tr><td>' + escapeHtml(r[0]) + '</td><td class="n">' + (r[1] ? accFmt(r[1]) : '') + '</td><td class="n">' + (r[2] ? accFmt(r[2]) : '') + '</td></tr>';
            });
            return h + '</tbody></table>';
        }
        function accJePreviewHtml(mode) {
            var D = 1000, T = 3000;
            var dBase = Math.round(D * 100 / 107 * 100) / 100, dVat = Math.round((D - dBase) * 100) / 100;
            var tBase = Math.round(T * 100 / 107 * 100) / 100, tVat = Math.round((T - tBase) * 100) / 100;
            var cash = T - D, rest = Math.round((tVat - dVat) * 100) / 100;
            var BANK = 'ธนาคาร/เงินสด', ADV = 'เงินรับล่วงหน้า-มัดจำ (21712)', REV = 'รายได้ค่าห้อง', VAT = 'ภาษีขาย (21911)', DEF = 'ภาษีขายรอเรียกเก็บ (21913)';
            var h = '<div style="color:#666;">ตัวอย่าง: รับมัดจำ <b>1,000</b> แล้วเช็คเอาท์ยอดรวม <b>3,000</b> (รวม VAT 7% · จ่ายเพิ่ม 2,000)</div>';
            if (mode === 'RECEIPT_DEFER') {
                h += accJeTable('รับมัดจำ', [[BANK, D, 0], [ADV, 0, dBase], [DEF, 0, dVat]]);
                h += accJeTable('เช็คเอาท์', [[BANK, cash, 0], [ADV, dBase, 0], [DEF, dVat, 0], [REV, 0, tBase], [VAT, 0, tVat]]);
                h += '<div class="je-note">VAT ทั้งหมด ' + accFmt(tVat) + ' เข้า ภ.พ.30 เดือนเช็คเอาท์ · ต้องผูกบัญชี 21913 · ถ้าผู้ทำบัญชีถือว่ามัดจำ = ค่าบริการล่วงหน้า (ม.78/1) การพักไว้มีความเสี่ยงเงินเพิ่ม</div>';
            } else if (mode === 'RECEIPT') {
                h += accJeTable('รับมัดจำ', [[BANK, D, 0], [ADV, 0, dBase], [VAT, 0, dVat]]);
                h += accJeTable('เช็คเอาท์ (ผลสุทธิ)', [[BANK, cash, 0], [ADV, dBase, 0], [REV, 0, tBase], [VAT, 0, rest]]);
                h += '<div class="je-note">VAT ' + accFmt(dVat) + ' เข้า ภ.พ.30 เดือนรับมัดจำ และ ' + accFmt(rest) + ' เดือนเช็คเอาท์ · ใบเสร็จมัดจำเป็นเอกสารภาษี — ตอนนี้ใบเช็คเอาท์ยังออกเต็มยอด + รายการปรับ VAT (บัญชีถูก แต่ลูกค้าได้เอกสารภาษีซ้อน 2 ใบ)</div>';
            } else {
                h += accJeTable('รับมัดจำ', [[BANK, D, 0], [ADV, 0, D]]);
                h += accJeTable('เช็คเอาท์', [[BANK, cash, 0], [ADV, D, 0], [REV, 0, tBase], [VAT, 0, tVat]]);
                h += '<div class="je-note" style="background:#e8f6ee; color:#16653e;">ใบเสร็จมัดจำไม่แสดง VAT · ใบกำกับเต็มยอด 3,000 ใบเดียวตอนเช็คเอาท์ · VAT ' + accFmt(tVat) + ' เข้า ภ.พ.30 เดือนเช็คเอาท์</div>';
            }
            return h;
        }
        function accDepositModeFromCfg() {
            var rec = (document.getElementById('cfgDepositVatRecognition') || {}).value || 'CHECKOUT';
            var def = !!(document.getElementById('cfgDepositDeferOutputVat') || {}).checked;
            return rec === 'RECEIPT' ? (def ? 'RECEIPT_DEFER' : 'RECEIPT') : 'CHECKOUT';
        }
        function accRenderDepositPreview() {
            var box = document.getElementById('depVatPreview');
            if (box) box.innerHTML = accJePreviewHtml(accDepositModeFromCfg());
        }

        // ── แท็บบันทึก ───────────────────────────────────────────────
        function loadRecentLog() {
            var el = document.getElementById('recentLogResult');
            el.innerHTML = '<div class="test-result loading">กำลังโหลด...</div>';
            var url = pageUrl + '?action=recentSyncLog&hours=' + encodeURIComponent(document.getElementById('logHours').value)
                + '&kind=' + encodeURIComponent(document.getElementById('logKind').value)
                + '&q=' + encodeURIComponent((document.getElementById('logQuery').value || '').trim()) + '&_=' + Date.now();
            var ctrl = typeof AbortController !== 'undefined' ? new AbortController() : null;
            var timer = setTimeout(function () { if (ctrl) ctrl.abort(); }, 40000);
            fetch(url, ctrl ? { signal: ctrl.signal } : undefined)
                .then(function (r) { clearTimeout(timer); return r.json(); })
                .then(function (d) {
                    if (!d.success) { el.innerHTML = '<div class="test-result error">' + escapeHtml(d.message || 'โหลดไม่สำเร็จ') + '</div>'; return; }
                    var items = d.items || [];
                    var h = '<div class="help-text">' + items.length + ' รายการ (สูงสุด 200)' + (d.payrollHidden ? ' · ซ่อนบรรทัดเงินเดือน (เฉพาะเจ้าของเห็น)' : '') + '</div>';
                    if (!items.length) { el.innerHTML = h + '<div class="muted" style="padding:12px;">ไม่มี log ในช่วงนี้</div>'; return; }
                    h += '<table class="log-table"><thead><tr><th style="width:140px;">เวลา</th><th style="width:70px;">ชนิด</th><th>รายละเอียด</th></tr></thead><tbody>';
                    items.forEach(function (it) {
                        h += '<tr><td>' + escapeHtml(it.time) + '</td><td>' + escapeHtml(it.kind) + '</td><td class="d">' + escapeHtml(it.detail) + '</td></tr>';
                    });
                    el.innerHTML = h + '</tbody></table>';
                })
                .catch(function (err) {
                    clearTimeout(timer);
                    el.innerHTML = '<div class="test-result error">' + escapeHtml(err && err.name === 'AbortError' ? 'หมดเวลา 40 วินาที — ลดช่วงเวลาหรือใส่คำค้น' : (err && err.message)) + '</div>';
                });
        }

        // ── แถบเตือนตั้งค่าไม่ครบ ─────────────────────────────────────
        function accInitBanner() {
            var btn = document.getElementById('btnOpenWizard');
            if (btn) btn.style.display = accCfg.canWizard ? '' : 'none';
            var ban = document.getElementById('setupBanner');
            if (!ban) return;
            var step = String(accCfg.wizardStep || '').toUpperCase();
            var notConnected = !accCfg.hasApiKey || !accCfg.companyId;
            var msg = '', act = '';
            if (notConnected) {
                msg = '<b>ยังเชื่อมต่อ NextAcc ไม่ครบ</b> — ต้องมีคีย์เชื่อมระบบและรหัสบริษัทก่อนจึงจะส่งเอกสารได้';
                act = accCfg.canWizard ? '<button type="button" class="btn-success" onclick="wzOpen(1)"><i class="fas fa-magic"></i> เริ่มตั้งค่าทีละขั้น</button>'
                                       : '<span>ให้ Owner / Admin บัญชีตั้งค่า</span>';
            } else if (accCfg.canWizard && /^[1-6]$/.test(step)) {
                msg = 'ตั้งค่าทีละขั้นค้างอยู่ที่ <b>ขั้นที่ ' + step + '</b>';
                act = '<button type="button" class="btn-success" onclick="wzOpen(' + step + ')"><i class="fas fa-magic"></i> ทำต่อ</button>';
            }
            if (!msg) { ban.hidden = true; return; }
            ban.innerHTML = '<div>' + msg + '</div><div>' + act + '</div>';
            ban.hidden = false;
        }

        document.addEventListener('DOMContentLoaded', function () {
            // ฟังก์ชันหลัก (loadConfigData) รันก่อนแล้ว — ค่าในฟอร์มคือค่าจริง
            accMarkRecommended();
            accRenderDepositPreview();
            accInitBanner();
            if (window.accFocusFrom) {   // applyFocus (รันก่อนหน้านี้) โหลดตารางของตัวเองแล้ว → โหลดที่เหลือของแท็บ + จำแท็บไว้ใน #t-
                if (!accTabLoaded[accCurrentTab]) { accTabLoaded[accCurrentTab] = true; accLazyLoad(accCurrentTab, window.accFocusFrom); }
                if (!window.location.hash) { try { history.replaceState(null, '', window.location.pathname + window.location.search + '#t-' + accCurrentTab); } catch (e) { } }
            }
            var pfNow = document.getElementById('pfModeNow');
            var PFL = { OFF: 'ปิด', WARN: 'เตือน', BLOCK: 'บล็อก' };
            if (pfNow) pfNow.textContent = PFL[accCfg.preflightMode] || accCfg.preflightMode || 'ปิด';
            var ids = document.querySelectorAll('.rec-badge[data-rec-for]');
            for (var i = 0; i < ids.length; i++) {
                var el = document.getElementById(ids[i].getAttribute('data-rec-for'));
                if (el) el.addEventListener('change', accMarkRecommended);
            }
            var sm = document.getElementById('cfgSyncMode'); if (sm) sm.addEventListener('change', accMarkRecommended);
            ['cfgDepositVatRecognition', 'cfgDepositDeferOutputVat'].forEach(function (id) {
                var e = document.getElementById(id); if (e) e.addEventListener('change', accRenderDepositPreview);
            });
            [['cfgApiKey', 'cfgApiKeyHint', 'int'], ['cfgCompanyApiKey', 'cfgCompanyApiKeyHint', 'acc'],
             ['wzApiKey', 'wzApiKeyHint', 'int'], ['wzCompanyApiKey', 'wzCompanyApiKeyHint', 'acc']].forEach(function (k) {
                var e = document.getElementById(k[0]);
                if (e) e.addEventListener('input', function () { accKeyHint(k[0], k[1], k[2]); });
            });
            ['wzDepMode'].forEach(function (n) {
                var rs = document.getElementsByName(n);
                for (var j = 0; j < rs.length; j++) rs[j].addEventListener('change', function () { wzRenderJe(); });
            });
            try {
                var q = new URLSearchParams(window.location.search);
                if ((q.get('wizard') === '1' || window.location.hash === '#wizard') && accCfg.canWizard) wzOpen();
            } catch (e) { }
        });

        // ═══════════ วิซาร์ดตั้งค่า ═══════════
        var wzState = { step: 1, maxReached: 1, dirty: false, saved: '' };

        // บัญชีสำคัญ: รหัสที่ใช้ในระบบ → คำอธิบายไทย + รหัสผังมาตรฐาน NextAcc ที่แนะนำ + คำในชื่อบัญชี (สำรอง)
        // need: 'req' = จำเป็นเสมอ · ฟังก์ชัน = จำเป็นเมื่อฟีเจอร์นั้นเปิด (คืนข้อความฟีเจอร์) · ไม่มี = ไม่บังคับ
        var WZ_KEYS = [
            { code: 'ROOM_REVENUE', label: 'รายได้ค่าห้องพัก', pref: ['41100', '41110', '41000'], kw: ['ค่าห้อง', 'ห้องพัก'], types: ['Revenue'], need: 'req' },
            { code: 'OUTPUT_VAT', label: 'ภาษีขาย', pref: ['21911'], kw: ['ภาษีขาย'], types: ['Liability'], need: 'req' },
            { code: 'ADVANCE_DEPOSIT', label: 'เงินรับล่วงหน้า / มัดจำค่าห้อง', pref: ['21712', '21510'], kw: ['รับล่วงหน้า', 'มัดจำ'], types: ['Liability'], need: 'req' },
            { code: 'ROOM_AR', label: 'ลูกหนี้ค่าห้อง / ลูกค้า', pref: ['11310', '11300'], kw: ['ลูกหนี้การค้า', 'ลูกหนี้'], types: ['Asset'], need: 'req' },
            { code: 'CASH', label: 'เงินสด (ค่าเริ่มต้นเมื่อแหล่งเงินไม่ผูก)', pref: ['11111', '11110'], kw: ['เงินสด'], types: ['Asset'], need: 'req' },
            { code: 'INPUT_VAT', label: 'ภาษีซื้อ', pref: ['11610'], kw: ['ภาษีซื้อ'], types: ['Asset'], need: 'req' },
            { code: 'ACCOUNTS_PAYABLE', label: 'เจ้าหนี้การค้า', pref: ['21110', '21100', '21200'], kw: ['เจ้าหนี้การค้า'], types: ['Liability'], need: 'req' },
            { code: 'WHT_PAYABLE', label: 'ภาษีหัก ณ ที่จ่ายค้างจ่าย — ภ.ง.ด.3 (บุคคล)', pref: ['21916'], kw: ['ภ.ง.ด.3', 'หัก ณ ที่จ่าย'], types: ['Liability'], need: 'req' },
            { code: 'WHT_PAYABLE_PND53', label: 'ภาษีหัก ณ ที่จ่ายค้างจ่าย — ภ.ง.ด.53 (นิติบุคคล)', pref: ['21917'], kw: ['ภ.ง.ด.53'], types: ['Liability'], need: 'req' },
            { code: 'OUTPUT_VAT_DEFERRED', label: 'ภาษีขายรอเรียกเก็บ (พัก VAT มัดจำ)', pref: ['21913'], kw: ['ภาษีขายรอ', 'ภาษีขายยังไม่ถึง'], types: ['Liability'],
              need: function (c) { return c.depositDeferOutputVat || c.depositDrivesJournal ? 'พัก VAT มัดจำ / หักมัดจำในใบ' : ''; } },
            { code: 'PRODUCT_REVENUE', label: 'รายได้ขายสินค้า / อาหาร', pref: ['41200', '41300'], kw: ['ขายสินค้า', 'รายได้จากการขาย', 'อาหาร'], types: ['Revenue'],
              need: function (c) { return c.posDailyRollup || c.roomServiceRevenue ? 'ขายหน้าร้าน / รูมเซอร์วิส' : ''; } },
            { code: 'INVENTORY', label: 'สินค้าคงเหลือ', pref: ['11500', '11510'], kw: ['สินค้าคงเหลือ'], types: ['Asset'],
              need: function (c) { return c.posDailyRollup || c.roomServiceRevenue || c.stockInUseGRNI || c.stockQtySync ? 'สต๊อก / ตัดต้นทุน' : ''; } },
            { code: 'COGS', label: 'ต้นทุนขาย', pref: ['51100', '51000'], kw: ['ต้นทุนขาย'], types: ['Expense'],
              need: function (c) { return c.posDailyRollup || c.roomServiceRevenue ? 'ตัดต้นทุนสินค้าที่ขาย' : ''; } },
            { code: 'GRNI', label: 'พักรับของ (GRNI)', pref: ['21240'], kw: ['รับของ', 'GRNI'], types: ['Liability'],
              need: function (c) { return c.stockInUseGRNI ? 'รับของแบบพักรับของ' : ''; } },
            { code: 'OTA_RECEIVABLE', label: 'ลูกหนี้ OTA', pref: ['11340', '11320'], kw: ['OTA', 'ผู้ให้บริการรับชำระ'], types: ['Asset'],
              need: function (c) { return c.otaRoomRevenue || c.otaDocumentMode === 'RECEIPT_DOC' ? 'รายได้ค่าห้อง OTA' : ''; } },
            { code: 'SECURITY_DEPOSIT_LIABILITY', label: 'เงินประกันความเสียหายรอคืน', pref: ['21530'], kw: ['เงินประกัน'], types: ['Liability'],
              need: function (c) { return c.securityDepositJournal ? 'เงินประกันแบบโอน' : ''; } },
            { code: 'WHT_PAYABLE_PND1', label: 'ภาษีหัก ณ ที่จ่ายค้างจ่าย — ภ.ง.ด.1 (เงินเดือน)', pref: ['21915'], kw: ['ภ.ง.ด.1'], types: ['Liability'],
              need: function (c) { return c.payrollSyncMode && c.payrollSyncMode !== 'LOCAL' ? 'ส่งเงินเดือน' : ''; } },
            { code: 'VENDOR_CREDIT_RECEIVABLE', label: 'เครดิตกับผู้ขาย (ใบลดหนี้ผู้ขาย)', pref: [], kw: ['เครดิตผู้ขาย', 'ลูกหนี้อื่น'], types: ['Asset'] },
            { code: 'OTHER_INCOME', label: 'รายได้อื่น', pref: ['42000', '42900'], kw: ['รายได้อื่น'], types: ['Revenue'] }
        ];

        function wzOpen(step) {
            if (!accCfg.canWizard) { alert('วิซาร์ดตั้งค่าใช้ได้เฉพาะ Owner / Admin ที่มีสิทธิ์บัญชี'); return false; }
            var ov = document.getElementById('accWizard');
            ov.hidden = false;
            document.body.classList.add('wz-open');
            var saved = String(accCfg.wizardStep || '').toUpperCase();
            wzState.saved = saved;
            var n = parseInt(step, 10);
            if (!(n >= 1 && n <= 6)) n = /^[1-6]$/.test(saved) ? parseInt(saved, 10) : 1;
            wzState.maxReached = Math.max(n, /^[1-6]$/.test(saved) ? parseInt(saved, 10) : (saved === 'DONE' ? 6 : 1));
            wzPrefillKeys();
            wzGo(n);
            return false;
        }

        function wzClose() {
            document.getElementById('accWizard').hidden = true;
            document.body.classList.remove('wz-open');
            if (wzState.dirty) window.location.reload();   // ค่าที่บันทึกระหว่างวิซาร์ด → โหลดหน้าใหม่ให้ฟอร์มหลักตรง (คงแท็บเดิมด้วย #t-)
        }

        function wzGo(n) {
            n = Math.max(1, Math.min(6, parseInt(n, 10) || 1));
            wzState.step = n;
            if (n > wzState.maxReached) wzState.maxReached = n;
            var panes = document.querySelectorAll('#accWizard .wz-pane');
            for (var i = 0; i < panes.length; i++) panes[i].hidden = panes[i].getAttribute('data-step') !== String(n);
            var lis = document.querySelectorAll('#wzSteps li');
            for (var j = 0; j < lis.length; j++) {
                var s = parseInt(lis[j].getAttribute('data-step'), 10);
                lis[j].className = (s === n ? 'cur' : '') + (s < wzState.maxReached && s !== n ? ' done' : '');
                var btn = lis[j].querySelector('button');
                if (btn) btn.setAttribute('aria-current', s === n ? 'step' : 'false');
            }
            document.getElementById('wzBar').style.width = Math.round((wzState.maxReached - 1) / 5 * 100) + '%';
            document.getElementById('wzPrev').disabled = n === 1;
            document.getElementById('wzNext').style.visibility = n === 6 ? 'hidden' : 'visible';
            document.getElementById('wzStepInfo').textContent = 'ขั้นที่ ' + n + ' จาก 6';
            // จำขั้นที่ทำถึงไว้ในระบบ (ทำต่อข้ามเครื่องได้) — ไม่ถอยค่าที่บันทึกไว้ และไม่ทับ DONE
            if (wzState.saved !== 'DONE' && (!/^[1-6]$/.test(wzState.saved) || n > parseInt(wzState.saved, 10))) {
                wzState.saved = String(n); accCfg.wizardStep = String(n);
                fetch(pageUrl + '?action=saveWizardStep&step=' + n + '&_=' + Date.now()).catch(function () { });
            }
            if (n === 2) wzPullStats();
            else if (n === 3) wzLoadPaidHow();
            else if (n === 4) wzLoadMappings();
            else if (n === 5) wzPrefillPolicy();
            else if (n === 6) { wzFinalSummary(); wzRunHealth(); }
            var body = document.querySelector('#accWizard .wz-body'); if (body) body.scrollTop = 0;
            var ov = document.getElementById('accWizard'); if (ov) ov.scrollTop = 0;
        }

        function wzSetResult(id, ok, msg) {
            var el = document.getElementById(id);
            if (!el) return;
            el.className = 'test-result ' + (ok === null ? 'loading' : (ok ? 'success' : 'error'));
            el.textContent = msg;
        }

        // ── ขั้น 1 ──
        function wzPrefillKeys() {
            var map = [['wzBaseUrl', 'cfgBaseUrl'], ['wzCompanyId', 'cfgCompanyId']];
            map.forEach(function (m) { var a = document.getElementById(m[0]), b = document.getElementById(m[1]); if (a && b && !a.value) a.value = b.value; });
            var k = document.getElementById('wzApiKey');
            if (k && accCfg.hasApiKey) k.placeholder = 'มีคีย์อยู่แล้ว (' + (accCfg.apiKeyMask || '••••') + ') — เว้นว่างเพื่อคงเดิม';
            var c = document.getElementById('wzCompanyApiKey');
            if (c && accCfg.hasCompanyApiKey) c.placeholder = 'มีคีย์บริษัทอยู่แล้ว — เว้นว่างเพื่อคงเดิม / ใส่ - เพื่อล้าง';
        }

        function wzSaveKeys() {
            var v = function (id) { return (document.getElementById(id).value || '').trim(); };
            var apiKey = v('wzApiKey'), accKey = v('wzCompanyApiKey');
            if (!apiKey && !accCfg.hasApiKey) { wzSetResult('wzKeyResult', false, 'ใส่คีย์เชื่อมระบบก่อน (ขึ้นต้นด้วย int_ จากเมนู Integrations ของ NextAcc)'); return; }
            if (/^acc_/i.test(apiKey)) { wzSetResult('wzKeyResult', false, 'ช่องแรกเป็นคีย์บริษัท (acc_) — ใช้ส่งเอกสารไม่ได้ ต้องใช้คีย์เชื่อมระบบ (int_)'); return; }
            if (!v('wzCompanyId')) { wzSetResult('wzKeyResult', false, 'ใส่รหัสบริษัทใน NextAcc ด้วย'); return; }
            // ใช้ช่องในฟอร์มหลัก + action saveApi เดิม (ไม่โหลดหน้าใหม่ระหว่างวิซาร์ด)
            document.getElementById('cfgBaseUrl').value = v('wzBaseUrl');
            document.getElementById('cfgCompanyId').value = v('wzCompanyId');
            postAction({ action: 'saveApi', baseUrl: v('wzBaseUrl'), apiKey: apiKey, companyApiKey: accKey, companyId: v('wzCompanyId') }, 'wzKeyResult', {
                noReload: true,
                done: function (ok) {
                    if (!ok) return;
                    wzState.dirty = true;
                    if (apiKey) accCfg.hasApiKey = true;
                    accCfg.companyId = v('wzCompanyId');
                    document.getElementById('wzApiKey').value = ''; document.getElementById('wzCompanyApiKey').value = '';
                    wzTestOnly();
                }
            });
        }

        function wzTestOnly() {
            getAction('testApi', 'wzKeyResult', function (ok) {
                if (ok) {
                    var el = document.getElementById('wzKeyResult');
                    if (el) el.innerHTML += '<div style="margin-top:6px;"><button type="button" class="btn-success" onclick="wzGo(2)">ไปขั้นที่ 2 <i class="fas fa-arrow-right"></i></button></div>';
                }
            });
        }

        // ── ขั้น 2 ──
        function wzPullStats() {
            var box = document.getElementById('wzPullStats');
            box.innerHTML = '<div class="wz-stat">กำลังตรวจ...</div>';
            fetch(pageUrl + '?action=nexaaccAccounts&_=' + Date.now())
                .then(function (r) { return r.json(); })
                .then(function (a) {
                    if (a.success && a.items) nexaaccAccountsCache = a.items;
                    loadWalletOptions(function () {
                        var n = a.success ? (a.total || 0) : 0, wo = walletOptions || { banks: [] };
                        var banks = (wo.banks || []).length;
                        box.innerHTML = '<div class="wz-stat ' + (n > 0 ? 'ok' : 'warn') + '"><b>' + n + '</b>บัญชีในผังบัญชี<br/>ดึงล่าสุด ' + escapeHtml(a.lastSync || '-') + '</div>'
                            + '<div class="wz-stat ' + (banks > 0 ? 'ok' : 'warn') + '"><b>' + banks + '</b>บัญชีธนาคาร / กระเป๋าเงิน' + (wo.bankCacheReady === false ? '<br/>(ยังไม่ได้อัปเดตฐานข้อมูลส่วนนี้)' : '') + '</div>';
                        if (n > 0 && wzState.maxReached < 3) wzState.maxReached = Math.max(wzState.maxReached, 2);
                    });
                })
                .catch(function (err) { box.innerHTML = '<div class="wz-stat err">' + escapeHtml(err.message) + '</div>'; });
        }
        function wzPull() { wzState.dirty = true; syncWallets('wzPullResult', function () { wzPullStats(); }); }
        function wzPullAccountsOnly() { wzState.dirty = true; syncAccounts('wzPullResult', function () { wzPullStats(); }); }

        // ── ขั้น 3: แหล่งเงิน ──
        var WZ_BANKS = [['กสิกร', 'kbank', 'k-bank', 'kasikorn'], ['ไทยพาณิชย์', 'scb', 'siam commercial'], ['กรุงเทพ', 'bbl', 'bangkok bank'],
                        ['กรุงไทย', 'ktb', 'krungthai', 'krung thai'], ['กรุงศรี', 'krungsri', 'bay'], ['ทหารไทย', 'ทีทีบี', 'ttb', 'tmb'],
                        ['ออมสิน', 'gsb'], ['ธ.ก.ส', 'baac'], ['ยูโอบี', 'uob'], ['ซีไอเอ็มบี', 'cimb'], ['แลนด์', 'lh bank'], ['เกียรตินาคิน', 'kkp']];
        function wzLc(s) { return String(s || '').toLowerCase(); }
        function wzSuggestPaidHow(item, wo) {
            var name = wzLc(item.name), ch = String(item.channelType || '').toUpperCase();
            var banks = (wo.banks || []).filter(function (b) { return b.active && b.linkedId; });
            var accs = wo.accounts || [];
            var acc = function (pred) { for (var i = 0; i < accs.length; i++) if (pred(accs[i])) return accs[i]; return null; };
            var bankVal = function (b) { return (b.linkedId || '') + '|' + (b.linkedCode || '') + '|' + b.id; };
            var accVal = function (a) { return a.id + '|' + a.code + '|'; };
            // เลขบัญชี 4 ตัวท้ายตรงกัน
            var digits = (item.name || '').replace(/[^0-9]/g, '');
            if (digits.length >= 4) {
                for (var d = 0; d < banks.length; d++) {
                    var num = String(banks[d].number || '').replace(/[^0-9]/g, '');
                    if (num.length >= 4 && num.slice(-4) === digits.slice(-4)) return { value: bankVal(banks[d]), why: 'เลขบัญชีตรงกัน' };
                }
            }
            for (var g = 0; g < WZ_BANKS.length; g++) {
                var grp = WZ_BANKS[g];
                if (!grp.some(function (k) { return name.indexOf(k) >= 0; })) continue;
                for (var b = 0; b < banks.length; b++) {
                    var hay = wzLc((banks[b].bank || '') + ' ' + (banks[b].name || ''));
                    if (grp.some(function (k) { return hay.indexOf(k) >= 0; })) return { value: bankVal(banks[b]), why: 'ชื่อธนาคารตรงกัน' };
                }
            }
            if (ch === 'CASH' || name.indexOf('เงินสด') >= 0 || name.indexOf('cash') >= 0) {
                var c = acc(function (a) { return a.code === '11111'; }) || acc(function (a) { return a.group === 'CASH' && wzLc(a.name).indexOf('เงินสด') >= 0; });
                if (c) return { value: accVal(c), why: 'แหล่งเงินสด' };
            }
            if (name.indexOf('เงินประกัน') >= 0 || name.indexOf('security') >= 0) {
                var sd = acc(function (a) { return a.code === '21530'; }) || acc(function (a) { return a.group === 'LIABILITY' && wzLc(a.name).indexOf('เงินประกัน') >= 0; });
                if (sd) return { value: accVal(sd), why: 'หักจากเงินประกัน = บัญชีหนี้สินเงินประกัน' };
            }
            if (ch === 'DIRECTOR_LOAN' || name.indexOf('กรรมการ') >= 0) {
                var dl = acc(function (a) { return a.group === 'LIABILITY' && wzLc(a.name).indexOf('กรรมการ') >= 0; });
                if (dl) return { value: accVal(dl), why: 'เจ้าหนี้กรรมการ' };
            }
            if (ch === 'OTA' || /agoda|booking|expedia|trip\.com|ota/.test(name)) {
                var ot = acc(function (a) { return a.group === 'RECEIVABLE' && /ota/i.test(a.name || ''); }) || acc(function (a) { return a.code === '11340'; });
                if (ot) return { value: accVal(ot), why: 'เงินที่ OTA เก็บแทน = ลูกหนี้/บัญชีพัก' };
            }
            if (ch === 'GATEWAY_OTHER' || ch === 'CARD' || /omise|payso|2c2p|gb ?pay|stripe|บัตร|ออนไลน์/.test(name)) {
                var gw = acc(function (a) { return a.code === '11340'; }) || acc(function (a) { return a.group === 'RECEIVABLE' && wzLc(a.name).indexOf('ผู้ให้บริการรับชำระ') >= 0; });
                if (gw) return { value: accVal(gw), why: 'เงินผ่านเกตเวย์ยังไม่เข้าธนาคาร = บัญชีพักเงิน' };
            }
            if ((ch === 'TRANSFER' || ch === 'QR' || /โอน|พร้อมเพย์|promptpay|qr/.test(name)) && banks.length === 1)
                return { value: bankVal(banks[0]), why: 'มีบัญชีธนาคารเดียว' };
            return null;
        }

        var wzPhItems = [];
        function wzLoadPaidHow() {
            var body = document.getElementById('wzPhBody');
            body.innerHTML = '<tr><td colspan="4" class="muted">กำลังโหลด...</td></tr>';
            loadWalletOptions(function () {
                fetch(pageUrl + '?action=getPaidHowMapping&_=' + Date.now())
                    .then(function (r) { return r.json(); })
                    .then(function (data) {
                        if (!data.success) { body.innerHTML = '<tr><td colspan="4" style="color:#C62828;">' + escapeHtml(data.message) + '</td></tr>'; return; }
                        var isLinked = function (it) { return it.accountId && it.accountId !== '00000000-0000-0000-0000-000000000000'; };
                        wzPhItems = (data.items || []).slice().sort(function (a, b) { return (isLinked(a) ? 1 : 0) - (isLinked(b) ? 1 : 0); });
                        var wo = walletOptions || { banks: [], accounts: [] };
                        var html = '', nUn = 0, nSug = 0, nWarn = 0;
                        wzPhItems.forEach(function (it) {
                            var linked = isLinked(it);
                            if (!linked) nUn++;
                            if (linked && it.warning) nWarn++;
                            var sug = linked ? null : wzSuggestPaidHow(it, wo);
                            if (sug) nSug++;
                            it._sug = sug;
                            var id = parseInt(it.id, 10) || 0;
                            html += '<tr id="wzphRow_' + id + '"><td><strong>' + escapeHtml(it.name) + '</strong>'
                                + (it.channelType ? '<div style="font-size:11px;color:#7f8c8d;">' + escapeHtml(it.channelType) + '</div>' : '') + '</td>'
                                + '<td>' + buildPaidHowSelect(id, it.accountCode, it.accountId, it.bankAccountId, 'wzph_', 'wzMarkChanged(this)')
                                + (sug ? '<div class="why">💡 แนะนำ: ' + escapeHtml(sug.why) + '</div>' : '') + '</td>'
                                + '<td>' + (linked ? '<span class="status-badge status-connected">ผูกแล้ว ' + escapeHtml(it.accountCode || '') + '</span>'
                                                   : '<span class="status-badge status-not-configured">ยังไม่ผูก</span>')
                                + (linked && it.warning ? '<div style="font-size:11px;color:#c0392b;">⚠ ' + escapeHtml(it.warning) + '</div>' : '') + '</td>'
                                + '<td><button type="button" class="btn-primary" style="padding:4px 10px; font-size:11px;" onclick="wzSavePh(' + id + ')"><i class="fas fa-save"></i></button></td></tr>';
                        });
                        body.innerHTML = html || '<tr><td colspan="4" class="muted">ไม่พบแหล่งเงิน</td></tr>';
                        // ตั้งค่าเดิมเป็นจุดเทียบ แล้วเลือกคำแนะนำให้แถวที่ยังไม่ผูก (ยังไม่บันทึกจนกว่าจะกด)
                        wzPhItems.forEach(function (it) {
                            var sel = document.getElementById('wzph_' + (parseInt(it.id, 10) || 0));
                            if (!sel) return;
                            sel.setAttribute('data-orig', sel.value);
                            if (it._sug && !sel.value) { sel.value = it._sug.value; wzMarkChanged(sel); }
                        });
                        document.getElementById('wzPhSummary').innerHTML =
                            '<div class="wz-stat ' + (nUn ? 'warn' : 'ok') + '"><b>' + nUn + '</b>ยังไม่ผูก</div>'
                            + '<div class="wz-stat"><b>' + nSug + '</b>มีคำแนะนำ (ยังไม่บันทึก)</div>'
                            + '<div class="wz-stat ' + (nWarn ? 'err' : 'ok') + '"><b>' + nWarn + '</b>ผูกแล้วแต่บัญชีผิดชนิด</div>';
                    })
                    .catch(function (err) { body.innerHTML = '<tr><td colspan="4" style="color:#C62828;">' + escapeHtml(err.message) + '</td></tr>'; });
            });
        }

        function wzMarkChanged(sel) {
            var tr = sel && sel.closest ? sel.closest('tr') : null;
            if (tr) tr.classList.toggle('changed', sel.value !== (sel.getAttribute('data-orig') || ''));
        }

        function wzRunSeq(ids, fn, resultId, done) {
            var ok = 0, fail = 0, i = 0;
            var next = function () {
                if (i >= ids.length) {
                    wzSetResult(resultId, fail === 0, 'บันทึกสำเร็จ ' + ok + ' รายการ' + (fail ? ' · ไม่สำเร็จ ' + fail + ' รายการ (ดูข้อความในแถว/ลองทีละแถว)' : ''));
                    if (done) done();
                    return;
                }
                var id = ids[i++];
                fn(id, function (success) { if (success) ok++; else fail++; next(); });
            };
            if (!ids.length) { wzSetResult(resultId, true, 'ไม่มีแถวที่เปลี่ยน'); return; }
            wzSetResult(resultId, null, 'กำลังบันทึก ' + ids.length + ' รายการ...');
            next();
        }

        function wzSavePh(id, cb) {
            var sel = document.getElementById('wzph_' + id);
            if (!sel || !sel.value) { if (cb) cb(false); else wzSetResult('wzPhResult', false, 'เลือกบัญชีก่อน'); return; }
            wzState.dirty = true;
            updatePaidHowAccount(id, 'wzph_', 'wzPhResult', function (success) {
                if (success) { sel.setAttribute('data-orig', sel.value); wzMarkChanged(sel); }
                if (cb) cb(success); else wzLoadPaidHow();
            });
        }
        function wzSaveAllPaidHow() {
            var ids = [];
            wzPhItems.forEach(function (it) {
                var id = parseInt(it.id, 10) || 0, sel = document.getElementById('wzph_' + id);
                if (sel && sel.value && sel.value !== (sel.getAttribute('data-orig') || '')) ids.push(id);
            });
            wzRunSeq(ids, wzSavePh, 'wzPhResult', function () { setTimeout(wzLoadPaidHow, 600); });
        }

        // ── ขั้น 4: บัญชีสำคัญ ──
        function wzSuggestAccount(k) {
            var list = nexaaccAccountsCache || [];
            for (var i = 0; i < k.pref.length; i++)
                for (var j = 0; j < list.length; j++) if (list[j].code === k.pref[i]) return { acc: list[j], why: 'รหัสผังมาตรฐาน ' + k.pref[i] };
            for (var w = 0; w < k.kw.length; w++)
                for (var a = 0; a < list.length; a++) {
                    var acc = list[a];
                    if (k.types && k.types.length && k.types.indexOf(acc.type) < 0) continue;
                    if (wzLc(acc.name + ' ' + (acc.nameEn || '')).indexOf(wzLc(k.kw[w])) >= 0) return { acc: acc, why: 'ชื่อบัญชีมีคำว่า "' + k.kw[w] + '"' };
                }
            return null;
        }

        var wzMapRows = [];
        function wzLoadMappings() {
            var body = document.getElementById('wzMapBody');
            body.innerHTML = '<tr><td colspan="4" class="muted">กำลังโหลด...</td></tr>';
            ensureAccountsLoaded(function () {
                fetch(pageUrl + '?action=mappings&_=' + Date.now())
                    .then(function (r) { return r.json(); })
                    .then(function (data) {
                        if (!data.success) { body.innerHTML = '<tr><td colspan="4" style="color:#C62828;">' + escapeHtml(data.message) + '</td></tr>'; return; }
                        if (!(nexaaccAccountsCache || []).length) {
                            body.innerHTML = '<tr><td colspan="4" style="color:#E65100;">ยังไม่มีผังบัญชี — กลับไป <a href="#" onclick="wzGo(2);return false;">ขั้นที่ 2</a> กดดึงจาก NextAcc ก่อน</td></tr>';
                            return;
                        }
                        var byCode = {};
                        (data.items || []).forEach(function (m) { if (m.code) byCode[m.code] = m; });
                        var codes = {}; (nexaaccAccountsCache || []).forEach(function (a) { codes[a.code] = a; });
                        wzMapRows = [];
                        WZ_KEYS.forEach(function (k, idx) {
                            var need = k.need === 'req' ? 'จำเป็น' : (typeof k.need === 'function' ? k.need(accCfg) : '');
                            var rank = k.need === 'req' ? 0 : (need ? 1 : 2);
                            wzMapRows.push({ k: k, m: byCode[k.code] || null, need: need, rank: rank, idx: idx });
                        });
                        wzMapRows.sort(function (a, b) { return a.rank - b.rank || a.idx - b.idx; });
                        var html = '', nMissing = 0, nBad = 0, nReqUnlinked = 0;
                        wzMapRows.forEach(function (r) {
                            var m = r.m, k = r.k;
                            var needTxt = r.rank === 0 ? '<span class="need">จำเป็น</span>' : (r.rank === 1 ? '<span class="need">ใช้กับ: ' + escapeHtml(r.need) + '</span>' : '<span style="font-size:11px;color:#999;">ไม่บังคับ</span>');
                            var head = '<td><strong>' + escapeHtml(k.label) + '</strong><div style="font-size:11px;color:#888;">' + escapeHtml(k.code) + '</div>' + needTxt + '</td>';
                            if (!m) {
                                nMissing++;
                                html += '<tr>' + head + '<td colspan="3" style="font-size:12px;color:#999;">ไม่มีรายการนี้ในตารางผูกบัญชีของระบบ — ข้ามได้ (ให้ผู้ดูแลเพิ่มถ้าต้องใช้)</td></tr>';
                                return;
                            }
                            var id = parseInt(m.id, 10) || 0;
                            var linked = !!m.accountId;
                            var exists = !m.accountCode || !!codes[m.accountCode];
                            if (m.accountCode && !exists) nBad++;
                            if (!linked && r.rank <= 1) nReqUnlinked++;
                            var sug = (!linked || !exists) ? wzSuggestAccount(k) : null;
                            r.sug = sug; r.id = id;
                            var st = !exists ? '<span class="status-badge status-error">ไม่มีใน NextAcc (' + escapeHtml(m.accountCode) + ')</span>'
                                   : linked ? '<span class="status-badge status-connected">ผูกแล้ว</span>'
                                   : '<span class="status-badge status-not-configured">ยังไม่ผูก</span>';
                            html += '<tr id="wzmapRow_' + id + '">' + head
                                + '<td>' + buildAccountSelect(id, exists ? m.accountCode : '', m.accountId, 'wzmap_')
                                + (sug ? '<div class="why">💡 แนะนำ: ' + escapeHtml(sug.acc.code + ' ' + (sug.acc.name || '')) + ' — ' + escapeHtml(sug.why) + '</div>' : '') + '</td>'
                                + '<td>' + st + '</td>'
                                + '<td><button type="button" class="btn-primary" style="padding:4px 10px; font-size:11px;" onclick="wzSaveMap(' + id + ')"><i class="fas fa-save"></i></button></td></tr>';
                        });
                        body.innerHTML = html;
                        wzMapRows.forEach(function (r) {
                            if (!r.m) return;
                            var sel = document.getElementById('wzmap_' + r.id);
                            if (!sel) return;
                            sel.addEventListener('change', function () { wzMarkChanged(sel); });
                            sel.setAttribute('data-orig', sel.value);
                            if (r.sug && !sel.value) { sel.value = r.sug.acc.id + '|' + r.sug.acc.code; wzMarkChanged(sel); }
                        });
                        document.getElementById('wzMapSummary').innerHTML =
                            '<div class="wz-stat ' + (nReqUnlinked ? 'warn' : 'ok') + '"><b>' + nReqUnlinked + '</b>จำเป็นแต่ยังไม่ผูก</div>'
                            + '<div class="wz-stat ' + (nBad ? 'err' : 'ok') + '"><b>' + nBad + '</b>ชี้รหัสที่ NextAcc ไม่มี</div>'
                            + (nMissing ? '<div class="wz-stat"><b>' + nMissing + '</b>ไม่มีในตารางของระบบ</div>' : '');
                    })
                    .catch(function (err) { body.innerHTML = '<tr><td colspan="4" style="color:#C62828;">' + escapeHtml(err.message) + '</td></tr>'; });
            });
        }

        function wzSaveMap(id, cb) {
            var sel = document.getElementById('wzmap_' + id);
            if (!sel || !sel.value) { if (cb) cb(false); else wzSetResult('wzMapResult', false, 'เลือกบัญชีก่อน'); return; }
            wzState.dirty = true;
            updateMapping(id, 'wzmap_', 'wzMapResult', function (success) {
                if (success) { sel.setAttribute('data-orig', sel.value); wzMarkChanged(sel); }
                if (cb) cb(success); else wzLoadMappings();
            });
        }
        function wzSaveAllMappings() {
            var ids = [];
            wzMapRows.forEach(function (r) {
                if (!r.m) return;
                var sel = document.getElementById('wzmap_' + r.id);
                if (sel && sel.value && sel.value !== (sel.getAttribute('data-orig') || '')) ids.push(r.id);
            });
            wzRunSeq(ids, wzSaveMap, 'wzMapResult', function () { setTimeout(wzLoadMappings, 600); });
        }

        // ── ขั้น 5: นโยบาย ──
        function wzRadio(name, value) {
            var rs = document.getElementsByName(name);
            for (var i = 0; i < rs.length; i++) { if (value === undefined) { if (rs[i].checked) return rs[i].value; } else rs[i].checked = rs[i].value === value; }
            return '';
        }
        function wzRenderJe() {
            var box = document.getElementById('wzJePreview');
            if (box) box.innerHTML = accJePreviewHtml(wzRadio('wzDepMode') || 'CHECKOUT');
        }
        function wzPrefillPolicy() {
            wzRadio('wzDepMode', accDepositModeFromCfg());
            wzRenderJe();
            var cnE = document.getElementById('cfgCreditNoteEnabled'), cnA = document.getElementById('cfgCreditNoteRequireApproval');
            var wE = document.getElementById('wzCnEnabled'), wA = document.getElementById('wzCnApproval');
            wE.checked = !!(cnE && cnE.checked); wA.checked = !!(cnA && cnA.checked);
            var off = !cnE || cnE.disabled;
            wE.disabled = off; wA.disabled = off;
            document.getElementById('wzCnNote').textContent = off ? 'ยังไม่ได้อัปเดตฐานข้อมูลส่วนใบลดหนี้ — ให้ผู้ดูแลระบบอัปเดตก่อน (ระหว่างนี้ใช้ปุ่มยกเลิกการจองแบบเดิม)' : '';
            var pfSel = document.getElementById('pfModeSel');
            var pf = (pfSel && pfSel.dataset.touched ? pfSel.value : accCfg.preflightMode) || 'OFF';
            wzRadio('wzPfMode', pf);
        }

        function wzSavePolicy() {
            if (!window.accCfgLoaded) { wzSetResult('wzPolicyResult', false, 'โหลดค่าปัจจุบันไม่สำเร็จ — รีเฟรชหน้าก่อนบันทึก (กันค่าอื่นถูกทับด้วยค่าเริ่มต้น)'); return; }
            var dep = wzRadio('wzDepMode') || 'CHECKOUT';
            document.getElementById('cfgDepositVatRecognition').value = dep === 'CHECKOUT' ? 'CHECKOUT' : 'RECEIPT';
            document.getElementById('cfgDepositDeferOutputVat').checked = dep === 'RECEIPT_DEFER';
            var cnE = document.getElementById('cfgCreditNoteEnabled'), cnA = document.getElementById('cfgCreditNoteRequireApproval');
            if (cnE && !cnE.disabled) {
                cnE.checked = document.getElementById('wzCnEnabled').checked; cnE.setAttribute('data-loaded', '1');
                cnA.checked = document.getElementById('wzCnApproval').checked; cnA.setAttribute('data-loaded', '1');
            }
            var pf = wzRadio('wzPfMode') || 'OFF';
            var pfChanged = pf !== (accCfg.preflightMode || 'OFF');
            if (pfChanged && pf === 'BLOCK' && !confirm('ตั้งตัวตรวจก่อนส่งเป็น "บล็อก": หน้าเช็คอิน/ใบเสร็จจะหยุดบันทึกเมื่อพบปัญหาข้อมูล\n(Owner / Admin บัญชี ยืนยันข้ามได้พร้อมเหตุผล)\n\nแนะนำให้ใช้ "เตือน" ช่วงแรกแล้วกดสแกนย้อนหลังที่แท็บคิวก่อน — ยืนยัน?')) return;
            accMarkRecommended(); accRenderDepositPreview();
            saveSyncSettings('wzPolicyResult', {
                noReload: true,
                done: function (ok) {
                    if (!ok) return;
                    wzState.dirty = true;
                    if (!pfChanged) return;
                    fetch(pageUrl + '?action=setPreflightMode&mode=' + encodeURIComponent(pf) + '&_=' + Date.now())
                        .then(function (r) { return r.json(); })
                        .then(function (d) {
                            var el = document.getElementById('wzPolicyResult');
                            if (d.success) accCfg.preflightMode = pf;
                            el.innerHTML += '<div>' + (d.success ? '✓ ' : '✗ ') + escapeHtml(d.message) + '</div>';
                            if (!d.success) el.className = 'test-result error';
                        })
                        .catch(function (err) { var el = document.getElementById('wzPolicyResult'); el.className = 'test-result error'; el.innerHTML += '<div>✗ ' + escapeHtml(err.message) + '</div>'; });
                }
            });
        }

        // ── ขั้น 6: ตรวจ + สรุป ──
        function wzRunHealth() { runHealthCheck('wzHealthResult'); }
        function wzFinalSummary() {
            var box = document.getElementById('wzFinalSummary');
            var DEP = { CHECKOUT: 'รับรู้ตอนเช็คเอาท์', RECEIPT_DEFER: 'แยก VAT ตอนรับ (พักไว้)', RECEIPT: 'เสีย VAT ทันทีที่รับมัดจำ' };
            var PFL = { OFF: 'ปิด', WARN: 'เตือน', BLOCK: 'บล็อก' };
            var cards = function (nAcc, phUn, mapUn) {
                box.innerHTML =
                    '<div class="wz-stat ' + (accCfg.hasApiKey && accCfg.companyId ? 'ok' : 'err') + '"><b>' + (accCfg.hasApiKey && accCfg.companyId ? '✓' : '✗') + '</b>คีย์ + รหัสบริษัท</div>'
                    + '<div class="wz-stat ' + (nAcc > 0 ? 'ok' : 'err') + '"><b>' + nAcc + '</b>บัญชีในผังบัญชี</div>'
                    + '<div class="wz-stat ' + (phUn ? 'warn' : 'ok') + '"><b>' + phUn + '</b>แหล่งเงินยังไม่ผูก</div>'
                    + '<div class="wz-stat ' + (mapUn ? 'warn' : 'ok') + '"><b>' + mapUn + '</b>บัญชีสำคัญยังไม่ผูก</div>'
                    + '<div class="wz-stat"><b style="font-size:13px;">' + escapeHtml(DEP[accDepositModeFromCfg()] || '-') + '</b>VAT มัดจำ</div>'
                    + '<div class="wz-stat"><b style="font-size:13px;">' + escapeHtml(PFL[accCfg.preflightMode] || accCfg.preflightMode || 'ปิด') + '</b>ตัวตรวจก่อนส่ง</div>';
            };
            box.innerHTML = '<div class="wz-stat">กำลังสรุป...</div>';
            Promise.all([
                fetch(pageUrl + '?action=nexaaccAccounts&_=' + Date.now()).then(function (r) { return r.json(); }).catch(function () { return {}; }),
                fetch(pageUrl + '?action=getPaidHowMapping&_=' + Date.now()).then(function (r) { return r.json(); }).catch(function () { return {}; }),
                fetch(pageUrl + '?action=mappings&_=' + Date.now()).then(function (r) { return r.json(); }).catch(function () { return {}; })
            ]).then(function (res) {
                var nAcc = res[0] && res[0].success ? (res[0].total || 0) : 0;
                var phUn = ((res[1] && res[1].items) || []).filter(function (it) { return !it.accountId || it.accountId === '00000000-0000-0000-0000-000000000000'; }).length;
                var byCode = {}; ((res[2] && res[2].items) || []).forEach(function (m) { byCode[m.code] = m; });
                var mapUn = WZ_KEYS.filter(function (k) {
                    var need = k.need === 'req' || (typeof k.need === 'function' && k.need(accCfg));
                    return need && byCode[k.code] && !byCode[k.code].accountId;
                }).length;
                cards(nAcc, phUn, mapUn);
            });
        }

        function wzFinish() {
            wzSetResult('wzFinishResult', null, 'กำลังบันทึก...');
            fetch(pageUrl + '?action=saveWizardStep&step=DONE&_=' + Date.now())
                .then(function (r) { return r.json(); })
                .then(function (d) {
                    if (!d.success) { wzSetResult('wzFinishResult', false, d.message || 'บันทึกไม่สำเร็จ'); return; }
                    accCfg.wizardStep = 'DONE'; wzState.saved = 'DONE';
                    try { history.replaceState(null, '', window.location.pathname + '#t-queue'); } catch (e) { }
                    wzState.dirty = true;   // โหลดหน้าใหม่ → แท็บคิวพร้อมค่าทั้งหมดที่ตั้ง
                    wzClose();
                })
                .catch(function (err) { wzSetResult('wzFinishResult', false, err.message); });
        }

        document.addEventListener('keydown', function (ev) {
            var ov = document.getElementById('accWizard');
            if (ev.key === 'Escape' && ov && !ov.hidden) wzClose();
        });
    </script>
</asp:Content>
