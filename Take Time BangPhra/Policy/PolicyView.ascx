<%@ Control Language="C#" AutoEventWireup="true" CodeBehind="PolicyView.ascx.cs" Inherits="Take_Time_BangPhra.PolicyPages.PolicyView" %>
<%-- เนื้อหาหน้านโยบายสาธารณะ — ใช้ร่วมทุกหน้าใน /Policy (ข้อความแก้ที่ ศูนย์ตั้งค่า → นโยบายการจอง / สัตว์เลี้ยงเข้าพัก) --%>
<style>
    .pp-wrap { max-width: 880px; margin: 18px auto 10px; font-family: 'Prompt', sans-serif; color: #3E2723; }
    .pp-nav { display: flex; flex-wrap: wrap; gap: 8px; margin-bottom: 14px; }
    .pp-nav a { display: inline-flex; align-items: center; min-height: 38px; padding: 6px 14px; border-radius: 20px;
                background: #EFEBE9; color: #5D4037; text-decoration: none; font-size: .92em; font-weight: 500; }
    .pp-nav a:hover, .pp-nav a:focus { background: #D7CCC8; color: #3E2723; text-decoration: none; }
    .pp-nav a.on { background: #5D4037; color: #fff; }
    .pp-card { background: #fff; border-radius: 14px; box-shadow: 0 2px 12px rgba(62, 39, 35, .08); overflow: hidden; }
    .pp-head { background: linear-gradient(135deg, #5D4037 0%, #3E2723 100%); color: #fff; padding: 22px 24px 18px;
               display: flex; flex-wrap: wrap; align-items: flex-end; justify-content: space-between; gap: 12px; }
    .pp-head h1 { margin: 0; font-size: 1.7em; font-weight: 600; line-height: 1.35; }
    .pp-head .pp-en { margin: 4px 0 0; font-size: 1.05em; opacity: .9; }
    .pp-head .pp-meta { margin: 8px 0 0; font-size: .88em; opacity: .85; }
    .pp-btn { display: inline-flex; align-items: center; gap: 8px; min-height: 40px; padding: 8px 16px; border: 1.5px solid rgba(255,255,255,.6);
              border-radius: 10px; background: transparent; color: #fff; font-family: inherit; font-size: .92em; cursor: pointer; }
    .pp-btn:hover, .pp-btn:focus { background: rgba(255, 255, 255, .15); }
    .pp-body { padding: 22px 24px; line-height: 1.85; font-size: 1em; overflow-wrap: anywhere; }
    .pp-body strong { color: #4E342E; }
    .pp-body mark.as-ph { background: #FFE082; padding: 0 2px; border-radius: 3px; }
    .pp-note { margin: 0 24px; padding: 10px 0 18px; border-top: 1px dashed #D7CCC8; color: #8D6E63; font-size: .86em; line-height: 1.7; }
    .pp-notice { background: #FFF8E1; border-left: 4px solid #FFB300; border-radius: 8px; padding: 12px 16px; margin: 18px 24px 0;
                 color: #5D4037; line-height: 1.7; }
    .pp-admin { background: #EEF4FB; color: #1D4E79; border-radius: 10px; padding: 10px 14px; margin-bottom: 14px; font-size: .92em; line-height: 1.7; }
    .pp-admin.warn { background: #FFF6E5; color: #8A5A00; border: 1px solid #FFD58A; }
    .pp-contact { margin-top: 16px; background: #FAF8F7; border: 1px solid #EFEBE9; border-radius: 14px; padding: 16px 20px;
                  line-height: 1.8; font-size: .94em; color: #4E342E; }
    .pp-contact h2 { margin: 0 0 6px; font-size: 1.08em; color: #5D4037; font-weight: 600; }
    .pp-contact a { color: #5D4037; font-weight: 500; }
    @media (max-width: 600px) {
        .pp-head { padding: 18px 16px 14px; }
        .pp-head h1 { font-size: 1.4em; }
        .pp-body { padding: 16px; }
        .pp-note { margin: 0 16px; }
        .pp-notice { margin: 14px 16px 0; }
    }
    @media print {
        .navbar, footer, .body-content > hr, .pp-nav, .pp-btn, .pp-admin { display: none !important; }
        body { padding: 0 !important; margin: 0 !important; background: #fff !important; }
        .container, .body-content { width: auto !important; max-width: none !important; padding: 0 !important; margin: 0 !important; }
        .pp-wrap { max-width: none; margin: 0; }
        .pp-card { box-shadow: none; border: 0; }
        .pp-head { background: none !important; color: #000 !important; padding: 0 0 8px; border-bottom: 2px solid #000; }
        .pp-head .pp-en, .pp-head .pp-meta { opacity: 1; }
        .pp-body { padding: 12px 0; font-size: 11pt; }
        .pp-note { margin: 0; }
        .pp-contact { break-inside: avoid; border-color: #999; background: none; }
        a[href]:after { content: none !important; }
    }
</style>

<article class="pp-wrap" lang="th">
    <nav class="pp-nav" aria-label="นโยบายอื่น ๆ (Other policies)"><%= NavHtml %></nav>
    <%= AdminHtml %>

    <div class="pp-card">
        <header class="pp-head">
            <div>
                <h1><%: TitleTh %></h1>
                <p class="pp-en" lang="en"><%: TitleEnglish %></p>
                <p class="pp-meta"><%: MetaLine %></p>
            </div>
            <button type="button" class="pp-btn" onclick="window.print();"><i class="fas fa-print"></i> พิมพ์ / Print</button>
        </header>
        <%= NoticeHtml %>
        <div class="pp-body"><%= BodyHtml %></div>
        <p class="pp-note">
            นโยบายฉบับนี้มีผลกับการจองใหม่ — การจองที่ยืนยันแล้วอ้างอิงฉบับที่ผู้จองยอมรับไว้ตอนจอง (แสดงในหน้ายืนยันการจอง)
        </p>
    </div>

    <aside class="pp-contact" aria-label="ติดต่อที่พัก">
        <h2>ติดต่อสอบถามเกี่ยวกับนโยบาย <span lang="en" style="font-weight:normal; color:#8D6E63;">(Contact us)</span></h2>
        Take Time Nature Resort · หจก.แอม แฮปปี้เนส<br />
        202/24 หมู่ 5 ตำบลบางพระ อำเภอศรีราชา จังหวัดชลบุรี 20110<br />
        โทร <a href="tel:0634161496">063-416-1496</a> ·
        LINE <a href="https://lin.ee/U8N29MP" target="_blank" rel="noopener">@taketime</a> ·
        Facebook <a href="https://www.facebook.com/TakeTimeBangPhra" target="_blank" rel="noopener">TakeTimeBangPhra</a> ·
        <a href="<%= ResolveUrl("~/Contact") %>">ช่องทางติดต่ออื่น ๆ</a>
    </aside>
</article>
