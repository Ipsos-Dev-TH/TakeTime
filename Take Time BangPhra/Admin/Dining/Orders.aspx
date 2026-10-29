<%@ Page Title="กระดานออเดอร์ QR สั่งอาหาร" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Orders.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Dining.Orders" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <link rel="stylesheet" href="<%= ResolveUrl("~/Content/dining-staff.css") %>?v=<%= Take_Time_BangPhra.Admin.Dining.Orders.AssetVersion %>" />

    <%-- กระดานออเดอร์ QR สั่งอาหาร — ข้อมูลทั้งหมดวาดจาก JSON (Scripts/dining-staff.js, escapeHtml ทุกค่า) · ลิงก์จาก Telegram ?o={orderId} ไฮไลต์การ์ด --%>
    <div class="dn-wrap" id="dnBoard">
        <div class="dn-head">
            <h2><i class="fas fa-utensils"></i> กระดานออเดอร์ · QR สั่งอาหาร</h2>
            <span class="sp"></span>
            <span class="dn-stamp" id="dnStamp"></span>
            <div class="dn-nav">
                <a class="on" href="<%= ResolveUrl("~/Admin/Dining/Orders") %>"><i class="fas fa-clipboard-list"></i> กระดาน</a>
                <a href="<%= ResolveUrl("~/Admin/Dining/Tables") %>"><i class="fas fa-th-large"></i> แผนผังโต๊ะ</a>
                <a href="<%= ResolveUrl("~/Admin/Dining/Zones") %>"><i class="fas fa-cog"></i> โซน &amp; โต๊ะ</a>
                <a href="<%= ResolveUrl("~/Admin/Dining/Menu") %>"><i class="fas fa-book-open"></i> เมนู</a>
                <button type="button" data-act="sound" title="เปิด/ปิดเสียงแจ้งเตือน">🔔 เสียงเปิด</button>
                <button type="button" data-act="refresh" title="โหลดใหม่">↻</button>
            </div>
        </div>
        <div class="dn-counts" id="dnCounts"></div>
        <div class="dn-filter" id="dnFilter"></div>
        <div class="dn-slips" id="dnSlips" style="display:none"></div>
        <div class="dn-grid" id="dnGrid"><div class="dn-empty">กำลังโหลด…</div></div>
    </div>

    <%-- แผงข้าง: สรุปทั้งโต๊ะ (session) --%>
    <div class="dn-panel" id="dnPanel">
        <div class="dn-panel-top"><span>📋 ทั้งโต๊ะ</span><button type="button" class="dn-btn dn-btn-ghost" data-act="closepanel">✕ ปิด</button></div>
        <div class="dn-panel-body" id="dnPanelBody"></div>
    </div>

    <%-- modal ทั่วไป (เหตุผล / รับเงินสด / ปิดโต๊ะ / ออเดอร์ใหม่) --%>
    <div class="dn-modal" id="dnModal">
        <div class="dn-modal-box" role="dialog" aria-modal="true" aria-labelledby="dnModalTitle">
            <h3 id="dnModalTitle"></h3>
            <div class="dn-modal-body" id="dnModalBody"></div>
            <div class="dn-modal-foot" id="dnModalFoot"></div>
        </div>
    </div>
    <div class="dn-toast" id="dnToast"></div>
    <div class="dn-banner" id="dnBanner"></div>
    <%-- kitchen ticket (80 มม.) — แสดงเฉพาะตอนพิมพ์ --%>
    <div id="dnTicket"></div>
    <audio id="dnSound" preload="auto">
        <source src="data:audio/wav;base64,UklGRnoGAABXQVZFZm10IBAAAAABAAEAQB8AAEAfAAABAAgAZGF0YQoGAACBhYqFbF1sbJaQo5N6YWJ0lJOUlH50ZnKJhZqWlYN0Znd3e4KDmJmXjHlxe3p6f4GGiouJhYB8e35+gIOGiYuKiIaCgYGCgYOFhoaFhYSDgoKCgoKDg4ODg4OCgoKCgoKCgoKCgoKCgoKCgoKCgoKC" type="audio/wav">
    </audio>

    <script>var DN_BOOT = <%= BootJson %>;</script>
    <script src="<%= ResolveUrl("~/Scripts/dining-staff.js") %>?v=<%= Take_Time_BangPhra.Admin.Dining.Orders.AssetVersion %>"></script>
    <script>if (window.DnStaff) { DnStaff.init(DN_BOOT); }</script>
</asp:Content>
