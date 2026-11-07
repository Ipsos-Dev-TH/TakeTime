<%@ Page Title="แผนผังโต๊ะ QR สั่งอาหาร" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Tables.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Dining.Tables" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <link rel="stylesheet" href="<%= ResolveUrl("~/Content/dining-staff.css") %>?v=<%= Take_Time_BangPhra.Admin.Dining.Orders.AssetVersion %>" />

    <%-- แผนผังโต๊ะสด: tile ตามโซน สีตามสถานะ · คลิก = แผงข้างสรุป session + รับเงินสด/ตรวจสลิป/ปิดโต๊ะ/เปิดโต๊ะแทนลูกค้า · รีเฟรชทุก Dining_Staff_Poll_Sec --%>
    <div class="dn-wrap" id="dnTablesPage">
        <div class="dn-head">
            <h2><i class="fas fa-th-large"></i> สถานะโต๊ะ · QR สั่งอาหาร</h2>
            <span class="sp"></span>
            <span class="dn-stamp" id="dnStamp"></span>
            <div class="dn-nav">
                <a href="<%= ResolveUrl("~/Admin/Dining/Orders") %>"><i class="fas fa-clipboard-list"></i> กระดาน</a>
                <a class="on" href="<%= ResolveUrl("~/Admin/Dining/Tables") %>"><i class="fas fa-th-large"></i> แผนผังโต๊ะ</a>
                <a href="<%= ResolveUrl("~/Admin/Dining/Zones") %>"><i class="fas fa-cog"></i> โซน &amp; โต๊ะ</a>
                <a href="<%= ResolveUrl("~/Admin/Dining/QrSheet") %>"><i class="fas fa-qrcode"></i> พิมพ์ QR</a>
                <button type="button" data-act="sound" title="เปิด/ปิดเสียงแจ้งเตือน">🔔 เสียงเปิด</button>
                <button type="button" data-act="refresh" title="โหลดใหม่">↻</button>
            </div>
        </div>
        <div class="dn-counts" id="dnCounts"></div>
        <div class="dn-filter">
            <select id="dnFZone" class="dn-select"><option value="0">ทุกโซน</option></select>
            <div class="dn-legend"><span>ว่าง</span><span class="l-occ">มีลูกค้า</span><span class="l-bill">ขอบิล</span><span class="l-call">เรียกพนักงาน</span><span class="l-dis">ปิดใช้งาน</span></div>
        </div>
        <div id="dnTiles"><div class="dn-empty">กำลังโหลด…</div></div>
    </div>

    <div class="dn-panel" id="dnPanel">
        <div class="dn-panel-top"><span>📋 รายละเอียดโต๊ะ</span><button type="button" class="dn-btn dn-btn-ghost" data-act="closepanel">✕ ปิด</button></div>
        <div class="dn-panel-body" id="dnPanelBody"></div>
    </div>
    <div class="dn-modal" id="dnModal">
        <div class="dn-modal-box" role="dialog" aria-modal="true" aria-labelledby="dnModalTitle">
            <h3 id="dnModalTitle"></h3>
            <div class="dn-modal-body" id="dnModalBody"></div>
            <div class="dn-modal-foot" id="dnModalFoot"></div>
        </div>
    </div>
    <div class="dn-toast" id="dnToast"></div>
    <div class="dn-banner" id="dnBanner"></div>
    <div id="dnTicket"></div>
    <audio id="dnSound" preload="auto">
        <source src="data:audio/wav;base64,UklGRnoGAABXQVZFZm10IBAAAAABAAEAQB8AAEAfAAABAAgAZGF0YQoGAACBhYqFbF1sbJaQo5N6YWJ0lJOUlH50ZnKJhZqWlYN0Znd3e4KDmJmXjHlxe3p6f4GGiouJhYB8e35+gIOGiYuKiIaCgYGCgYOFhoaFhYSDgoKCgoKDg4ODg4OCgoKCgoKCgoKCgoKCgoKCgoKCgoKC" type="audio/wav">
    </audio>

    <script>var DN_BOOT = <%= BootJson %>;</script>
    <script src="<%= ResolveUrl("~/Scripts/dining-staff.js") %>?v=<%= Take_Time_BangPhra.Admin.Dining.Orders.AssetVersion %>"></script>
    <script>if (window.DnStaff) { DnStaff.init(DN_BOOT); }</script>
</asp:Content>
