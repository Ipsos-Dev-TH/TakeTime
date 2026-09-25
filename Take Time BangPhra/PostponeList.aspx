<%@ Page Title="" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="PostponeList.aspx.cs" Inherits="Take_Time_BangPhra.PostponeList" validateRequest="false" enableEventValidation="false"  %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <link rel="stylesheet" href="/Content/jquery-ui.css">
    <link rel="stylesheet" href="/Content/style.css">
    <link rel="stylesheet" type="text/css" href="/Content/GridView2.css">
    <style type="text/css">
        .wrap { white-space: normal; width: 100px; }

        .header-center { text-align: center; }
        .header-right { text-align: right; }

        th, td { padding: 5px; }

        .page-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 15px;
            flex-wrap: wrap;
            gap: 10px;
        }

        .stat-badge {
            display: inline-block;
            background: #f0ad4e;
            color: #fff;
            padding: 4px 12px;
            border-radius: 12px;
            font-size: 14px;
            font-weight: bold;
        }

        .reschedule-count {
            display: inline-block;
            background: #5bc0de;
            color: #fff;
            padding: 2px 8px;
            border-radius: 10px;
            font-size: 12px;
            cursor: pointer;
        }
        .reschedule-count:hover { background: #46b8da; }

        .btn-edit {
            background-color: #337ab7;
            color: white;
            border: none;
            padding: 6px 14px;
            border-radius: 4px;
            cursor: pointer;
            font-size: 13px;
        }
        .btn-edit:hover { background-color: #286090; }

        .btn-cancel {
            background-color: #d9534f;
            color: white;
            border: none;
            padding: 6px 14px;
            border-radius: 4px;
            cursor: pointer;
            font-size: 13px;
        }
        .btn-cancel:hover { background-color: #c9302c; }

        .btn-refresh {
            background-color: #5cb85c;
            color: white;
            border: none;
            padding: 6px 14px;
            border-radius: 4px;
            cursor: pointer;
            font-size: 13px;
        }
        .btn-refresh:hover { background-color: #449d44; }

        .btn-history {
            background-color: #5bc0de;
            color: white;
            border: none;
            padding: 4px 10px;
            border-radius: 4px;
            cursor: pointer;
            font-size: 12px;
        }
        .btn-history:hover { background-color: #46b8da; }

        .reason-text {
            color: #888;
            font-size: 12px;
            font-style: italic;
        }

        /* Modal styles */
        .modal-overlay {
            display: none;
            position: fixed;
            top: 0; left: 0;
            width: 100%; height: 100%;
            background: rgba(0,0,0,0.5);
            z-index: 9999;
            justify-content: center;
            align-items: center;
        }
        .modal-overlay.active { display: flex; }

        .modal-content {
            background: #fff;
            border-radius: 8px;
            padding: 24px;
            max-width: 600px;
            width: 90%;
            max-height: 80vh;
            overflow-y: auto;
            box-shadow: 0 4px 20px rgba(0,0,0,0.3);
        }

        .modal-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 16px;
            border-bottom: 1px solid #eee;
            padding-bottom: 12px;
        }
        .modal-header h3 { margin: 0; }

        .modal-close {
            background: none;
            border: none;
            font-size: 24px;
            cursor: pointer;
            color: #999;
            padding: 0 4px;
        }
        .modal-close:hover { color: #333; }

        .history-item {
            border-left: 3px solid #5bc0de;
            padding: 10px 14px;
            margin-bottom: 12px;
            background: #f9f9f9;
            border-radius: 0 4px 4px 0;
        }
        .history-item.postpone { border-left-color: #f0ad4e; }
        .history-item.cancel { border-left-color: #d9534f; }

        .history-date { color: #888; font-size: 12px; }
        .history-type {
            display: inline-block;
            padding: 2px 8px;
            border-radius: 4px;
            font-size: 11px;
            font-weight: bold;
            color: #fff;
        }
        .history-type.date-change { background: #5bc0de; }
        .history-type.postpone { background: #f0ad4e; }
        .history-type.cancel { background: #d9534f; }

        .history-dates {
            margin-top: 6px;
            font-size: 13px;
        }

        /* Cancel reason modal */
        .cancel-reason-input {
            width: 100%;
            padding: 8px 12px;
            border: 1px solid #ddd;
            border-radius: 4px;
            margin: 12px 0;
            font-size: 14px;
        }

        .modal-actions {
            display: flex;
            justify-content: flex-end;
            gap: 8px;
            margin-top: 16px;
        }

        .btn-modal-cancel {
            background: #ccc;
            border: none;
            padding: 8px 20px;
            border-radius: 4px;
            cursor: pointer;
        }

        .btn-modal-confirm {
            background: #d9534f;
            color: white;
            border: none;
            padding: 8px 20px;
            border-radius: 4px;
            cursor: pointer;
        }
        .btn-modal-confirm:hover { background: #c9302c; }

        .no-history { color: #999; font-style: italic; text-align: center; padding: 20px; }

        /* Summary cards */
        .summary-row {
            display: flex;
            flex-wrap: wrap;
            gap: 10px;
            margin-bottom: 12px;
        }
        .summary-card {
            flex: 1 1 160px;
            background: #fff;
            border: 1px solid #e3e6ea;
            border-left: 4px solid #5bc0de;
            border-radius: 6px;
            padding: 10px 14px;
        }
        .summary-card .sc-label { color: #7a8794; font-size: 12px; }
        .summary-card .sc-value { font-size: 20px; font-weight: bold; color: #333; }
        .summary-card.card-held { border-left-color: #5cb85c; }
        .summary-card.card-warn { border-left-color: #f0ad4e; }
        .summary-card.card-danger { border-left-color: #d9534f; }

        .toolbar {
            display: flex;
            flex-wrap: wrap;
            gap: 8px;
            align-items: center;
            margin-bottom: 10px;
        }
        .toolbar .search-input {
            flex: 1 1 240px;
            max-width: 360px;
            padding: 6px 10px;
            border: 1px solid #ccc;
            border-radius: 4px;
            font-size: 14px;
        }
        .toolbar select {
            padding: 6px 8px;
            border: 1px solid #ccc;
            border-radius: 4px;
            font-size: 14px;
        }
        .btn-search {
            background-color: #337ab7; color: #fff; border: none;
            padding: 6px 14px; border-radius: 4px; cursor: pointer; font-size: 13px;
        }
        .btn-clear {
            background-color: #e0e0e0; color: #333; border: none;
            padding: 6px 14px; border-radius: 4px; cursor: pointer; font-size: 13px;
        }
        .shown-text { color: #7a8794; font-size: 12px; }
        .policy-text { color: #7a8794; font-size: 12px; margin: -4px 0 10px 0; }

        /* Message banner */
        .msg-banner {
            padding: 10px 14px;
            border-radius: 4px;
            margin-bottom: 12px;
            font-size: 14px;
        }
        .msg-ok { background: #dff0d8; color: #3c763d; border: 1px solid #d6e9c6; }
        .msg-warn { background: #fcf8e3; color: #8a6d3b; border: 1px solid #faebcc; }
        .msg-error { background: #f2dede; color: #a94442; border: 1px solid #ebccd1; }

        a.btn-edit { display: inline-block; text-decoration: none; }
        a.btn-edit:hover { color: #fff; text-decoration: none; }

        .muted { color: #7a8794; }
        .money-held { color: #2e7d32; }
        .remark-text { font-size: 12px; }
        .phone-link { color: #337ab7; }
        .booking-link { font-weight: bold; color: #337ab7; }

        .age-normal { font-weight: bold; color: #333; }
        .age-amber { font-weight: bold; color: #c49000; }
        .age-orange { font-weight: bold; color: #e67e22; }
        .age-red { font-weight: bold; color: #d9534f; }

        .badge {
            display: inline-block;
            padding: 2px 8px;
            border-radius: 10px;
            font-size: 11px;
            font-weight: bold;
            color: #fff;
            white-space: nowrap;
        }
        .badge-red { background: #d9534f; }
        .badge-orange { background: #f0ad4e; }
        .badge-grey { background: #999; }

        tr.row-expired td { background-color: #fdecea !important; }
        tr.row-expiring td { background-color: #fff6e5 !important; }

        .cancel-warning {
            background: #fcf8e3;
            border: 1px solid #faebcc;
            color: #8a6d3b;
            border-radius: 4px;
            padding: 8px 12px;
            font-size: 13px;
            margin-top: 8px;
        }

        /* Mobile responsive */
        @media (max-width: 768px) {
            .mydatagrid {
                display: block;
                overflow-x: auto;
                -webkit-overflow-scrolling: touch;
                font-size: 12px;
            }
            .mydatagrid th, .mydatagrid td {
                padding: 6px 4px !important;
                white-space: nowrap;
            }
            .page-header {
                flex-direction: column;
                align-items: flex-start;
            }
            .toolbar .search-input { max-width: none; }
            .modal-content { width: 95%; padding: 16px; }
        }
    </style>

    <div class="page-header">
        <div>
            <strong><span style="font-size: large">รายการผู้เลื่อนเข้าพัก</span></strong>
            <span class="stat-badge">
                <asp:Label ID="lblPostponeCount" runat="server" Text="0" /> รายการ
            </span>
        </div>
        <div>
            <asp:Button ID="btnRefresh" runat="server" Text="รีเฟรช" CssClass="btn-refresh" OnClick="btnRefresh_Click" />
        </div>
    </div>

    <asp:Panel ID="pnlMessage" runat="server" Visible="false" CssClass="msg-banner">
        <asp:Literal ID="litMessage" runat="server" />
    </asp:Panel>

    <div class="summary-row">
        <div class="summary-card">
            <div class="sc-label">ใบที่เลื่อนอยู่</div>
            <div class="sc-value"><asp:Label ID="lblSummaryCount" runat="server" Text="0" /></div>
        </div>
        <div class="summary-card card-held">
            <div class="sc-label">มัดจำที่ถือไว้ (ต้องให้บริการ/คืนลูกค้า)</div>
            <div class="sc-value"><asp:Label ID="lblTotalHeld" runat="server" Text="0.00" /> บาท</div>
        </div>
        <div class="summary-card card-warn">
            <div class="sc-label">ใกล้หมดอายุ</div>
            <div class="sc-value"><asp:Label ID="lblExpiringCount" runat="server" Text="0" /></div>
        </div>
        <div class="summary-card card-danger">
            <div class="sc-label">หมดอายุแล้ว</div>
            <div class="sc-value"><asp:Label ID="lblExpiredCount" runat="server" Text="0" /></div>
        </div>
        <div class="summary-card">
            <div class="sc-label">เลื่อนนานสุด</div>
            <div class="sc-value"><asp:Label ID="lblOldestDays" runat="server" Text="0" /> วัน</div>
        </div>
    </div>
    <div class="policy-text"><asp:Literal ID="litPolicy" runat="server" /></div>

    <asp:Panel ID="pnlSearch" runat="server" DefaultButton="btnSearch" CssClass="toolbar">
        <asp:TextBox ID="txtSearch" runat="server" CssClass="search-input" MaxLength="100"
            placeholder="ค้นหา เลขที่จอง / ชื่อ / ชื่อเล่น / เบอร์โทร / หมายเหตุ" />
        <asp:DropDownList ID="ddlFilter" runat="server">
            <asp:ListItem Value="all" Text="ทั้งหมด" Selected="True" />
            <asp:ListItem Value="old90" Text="เลื่อนเกิน 90 วัน" />
            <asp:ListItem Value="old180" Text="เลื่อนเกิน 180 วัน" />
            <asp:ListItem Value="expiring" Text="ใกล้หมดอายุ / หมดอายุ" />
            <asp:ListItem Value="pending" Text="สถานะรอชำระเงิน" />
        </asp:DropDownList>
        <asp:Button ID="btnSearch" runat="server" Text="ค้นหา" CssClass="btn-search" OnClick="btnSearch_Click" />
        <asp:Button ID="btnClearSearch" runat="server" Text="ล้าง" CssClass="btn-clear" OnClick="btnClearSearch_Click" />
        <asp:Label ID="lblShown" runat="server" CssClass="shown-text" />
    </asp:Panel>

    <center>
    <asp:GridView ID="GridView1" runat="server" OnRowDataBound="GridView1_RowDataBound"
        AutoGenerateColumns="False" CssClass="mydatagrid"
        PagerStyle-CssClass="pager" HeaderStyle-CssClass="header" RowStyle-CssClass="rows"
        EmptyDataText="ไม่มีรายการเลื่อนเข้าพัก">
        <Columns>
            <asp:TemplateField HeaderText="" HeaderStyle-CssClass="header-center" ItemStyle-CssClass="header-center">
                <ItemTemplate>
                    <a class="btn-edit" href="<%# H(EditUrl(Container.DataItem)) %>"
                        title="เปิดใบจองเดิม (เลขเดิม มัดจำเดิม) เพื่อเลือกวันเข้าพักใหม่">ลงจอง</a>
                </ItemTemplate>
                <HeaderStyle CssClass="header-center" />
                <ItemStyle CssClass="header-center" />
            </asp:TemplateField>

            <asp:TemplateField HeaderText="เลขที่จอง" HeaderStyle-CssClass="header-center" ItemStyle-CssClass="header-center">
                <ItemTemplate>
                    <a class="booking-link" href="<%# H(EditUrl(Container.DataItem)) %>"><%# IdText(Container.DataItem) %></a>
                </ItemTemplate>
                <HeaderStyle CssClass="header-center" />
                <ItemStyle CssClass="header-center" />
            </asp:TemplateField>

            <asp:TemplateField HeaderText="ลูกค้า" HeaderStyle-CssClass="header-center">
                <ItemTemplate>
                    <%# CustomerHtml(Container.DataItem) %>
                </ItemTemplate>
                <HeaderStyle CssClass="header-center" />
            </asp:TemplateField>

            <asp:TemplateField HeaderText="มัดจำที่รับจริง" HeaderStyle-CssClass="header-center" ItemStyle-CssClass="header-right">
                <ItemTemplate>
                    <%# HeldHtml(Container.DataItem) %>
                </ItemTemplate>
                <HeaderStyle CssClass="header-center" />
                <ItemStyle CssClass="header-right" />
            </asp:TemplateField>

            <asp:TemplateField HeaderText="เลื่อนมาแล้ว" HeaderStyle-CssClass="header-center" ItemStyle-CssClass="header-center">
                <ItemTemplate>
                    <%# AgeHtml(Container.DataItem) %>
                </ItemTemplate>
                <HeaderStyle CssClass="header-center" />
                <ItemStyle CssClass="header-center" />
            </asp:TemplateField>

            <asp:TemplateField HeaderText="เลื่อน" HeaderStyle-CssClass="header-center"
                ItemStyle-CssClass="header-center">
                <ItemTemplate>
                    <span class="reschedule-count" data-id="<%# IdText(Container.DataItem) %>"
                        onclick="showHistory(this.getAttribute('data-id'))" title="คลิกดูประวัติ">
                        <%# RescheduleCountText(Container.DataItem) %> ครั้ง
                    </span>
                </ItemTemplate>
                <HeaderStyle CssClass="header-center" />
                <ItemStyle CssClass="header-center" />
            </asp:TemplateField>

            <asp:TemplateField HeaderText="การจองเดิม (ก่อนเลื่อน)" HeaderStyle-CssClass="header-center">
                <ItemTemplate>
                    <%# OrigHtml(Container.DataItem) %>
                </ItemTemplate>
                <HeaderStyle CssClass="header-center" />
            </asp:TemplateField>

            <asp:TemplateField HeaderText="วันที่ขอเลื่อน" HeaderStyle-CssClass="header-center">
                <ItemTemplate>
                    <%# RequestedHtml(Container.DataItem) %>
                </ItemTemplate>
                <HeaderStyle CssClass="header-center" />
            </asp:TemplateField>

            <asp:TemplateField HeaderText="หมายเหตุ / เหตุผลล่าสุด" HeaderStyle-CssClass="header-center">
                <ItemTemplate>
                    <%# NoteHtml(Container.DataItem) %>
                </ItemTemplate>
                <HeaderStyle CssClass="header-center" />
            </asp:TemplateField>

            <asp:TemplateField HeaderStyle-Width="3%">
                <ItemTemplate>
                    <button type="button" class="btn-cancel" <%# CancelAttrs(Container.DataItem) %>
                        onclick="showCancelModal(this)">
                        ยกเลิก
                    </button>
                </ItemTemplate>
                <HeaderStyle Width="3%" />
            </asp:TemplateField>
        </Columns>

        <HeaderStyle CssClass="header" />
        <PagerStyle CssClass="pager" />
        <RowStyle CssClass="rows" />
    </asp:GridView>
    </center>

    <!-- Hidden fields for cancel with reason -->
    <asp:HiddenField ID="hdnCancelReservationId" runat="server" />
    <asp:HiddenField ID="hdnCancelReason" runat="server" />
    <asp:Button ID="btnCancelWithReason" runat="server" OnClick="btnCancelWithReason_Click"
        style="display:none" />

    <!-- Cancel Reason Modal -->
    <div id="cancelModal" class="modal-overlay">
        <div class="modal-content">
            <div class="modal-header">
                <h3>ยกเลิกรายการเลื่อนเข้าพัก</h3>
                <button type="button" class="modal-close" onclick="closeCancelModal()">&times;</button>
            </div>
            <p>ยืนยันการยกเลิกรายการเลื่อนเข้าพัก #<span id="cancelReservationNo"></span> ของ <strong id="cancelCustomerName"></strong> ?</p>
            <div id="cancelHeldWarning" class="cancel-warning" style="display:none">
                มัดจำที่รับไว้ <b id="cancelHeldAmount"></b> บาท จะ<b>ยังคงเป็นเงินรับล่วงหน้า (หนี้สินต่อลูกค้า)</b> ในระบบบัญชี —
                การยกเลิกที่หน้านี้ไม่คืนเงินและไม่ริบมัดจำให้อัตโนมัติ
                หากคืนเงินหรือริบมัดจำ ต้องทำรายการคืนเงิน/บันทึกรายได้ในระบบบัญชีแยกต่างหาก
            </div>
            <label>เหตุผลในการยกเลิก:</label>
            <input type="text" id="cancelReasonInput" class="cancel-reason-input"
                placeholder="ระบุเหตุผล เช่น ลูกค้าขอยกเลิก / คืนมัดจำแล้ว / ริบมัดจำ (ไม่บังคับ)" maxlength="450" />
            <div class="modal-actions">
                <button type="button" class="btn-modal-cancel" onclick="closeCancelModal()">ปิด</button>
                <button type="button" class="btn-modal-confirm" onclick="confirmCancel()">ยืนยันยกเลิก</button>
            </div>
        </div>
    </div>

    <!-- History Modal -->
    <div id="historyModal" class="modal-overlay">
        <div class="modal-content">
            <div class="modal-header">
                <h3>ประวัติการเลื่อนเข้าพัก - การจอง #<span id="historyReservationId"></span></h3>
                <button type="button" class="modal-close" onclick="closeHistoryModal()">&times;</button>
            </div>
            <div id="historyContent">
                <p class="no-history">กำลังโหลด...</p>
            </div>
        </div>
    </div>

    <script type="text/javascript">
        function escHtml(s) {
            return String(s == null ? '' : s)
                .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
                .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
        }

        // Cancel modal — ค่ามาจาก data-* ของปุ่ม (encode ฝั่งเซิร์ฟเวอร์แล้ว) ไม่ต่อสตริงลง onclick
        var cancelReservationId = 0;

        function showCancelModal(btn) {
            var id = parseInt(btn.getAttribute('data-id'), 10) || 0;
            var name = btn.getAttribute('data-name') || '';
            var held = btn.getAttribute('data-held') || '0.00';
            cancelReservationId = id;
            document.getElementById('cancelReservationNo').textContent = id;
            document.getElementById('cancelCustomerName').textContent = name || 'ลูกค้า';
            document.getElementById('cancelHeldAmount').textContent = held;
            var heldNum = parseFloat(held.replace(/,/g, '')) || 0;
            document.getElementById('cancelHeldWarning').style.display = heldNum > 0 ? 'block' : 'none';
            document.getElementById('cancelReasonInput').value = '';
            document.getElementById('cancelModal').classList.add('active');
        }

        function closeCancelModal() {
            document.getElementById('cancelModal').classList.remove('active');
            cancelReservationId = 0;
        }

        function confirmCancel() {
            if (cancelReservationId <= 0) return;
            var reason = document.getElementById('cancelReasonInput').value.trim();

            document.getElementById('<%= hdnCancelReservationId.ClientID %>').value = cancelReservationId;
            document.getElementById('<%= hdnCancelReason.ClientID %>').value = reason || 'ยกเลิกจากหน้ารายการเลื่อนเข้าพัก';

            closeCancelModal();
            document.getElementById('<%= btnCancelWithReason.ClientID %>').click();
        }

        // History modal
        function showHistory(reservationId) {
            var rid = parseInt(reservationId, 10) || 0;
            document.getElementById('historyReservationId').textContent = rid;
            document.getElementById('historyContent').innerHTML = '<p class="no-history">กำลังโหลด...</p>';
            document.getElementById('historyModal').classList.add('active');

            var xhr = new XMLHttpRequest();
            xhr.open('GET', '<%= ResolveUrl("~/PostponeList") %>?action=gethistory&rid=' + rid, true);
            xhr.onreadystatechange = function() {
                if (xhr.readyState === 4) {
                    if (xhr.status === 200) {
                        try {
                            var data = JSON.parse(xhr.responseText);
                            renderHistory(data);
                        } catch (e) {
                            document.getElementById('historyContent').innerHTML =
                                '<p class="no-history">ไม่สามารถโหลดข้อมูลได้</p>';
                        }
                    } else if (xhr.status === 401) {
                        document.getElementById('historyContent').innerHTML =
                            '<p class="no-history">หมดเวลาการเข้าสู่ระบบ กรุณาเข้าสู่ระบบใหม่</p>';
                    } else {
                        document.getElementById('historyContent').innerHTML =
                            '<p class="no-history">ไม่สามารถโหลดข้อมูลได้</p>';
                    }
                }
            };
            xhr.send();
        }

        function renderHistory(items) {
            var container = document.getElementById('historyContent');
            if (!items || items.length === 0) {
                container.innerHTML = '<p class="no-history">ยังไม่มีประวัติการเลื่อน</p>';
                return;
            }

            var html = '';
            for (var i = 0; i < items.length; i++) {
                var item = items[i];
                var typeClass = 'date-change';
                var typeName = 'ลงวันใหม่/เปลี่ยนวัน';
                var itemClass = '';

                if (item.Type === 'POSTPONE') {
                    typeClass = 'postpone'; typeName = 'เลื่อนวัน'; itemClass = ' postpone';
                } else if (item.Type === 'CANCEL_POSTPONE') {
                    typeClass = 'cancel'; typeName = 'ยกเลิกเลื่อน'; itemClass = ' cancel';
                }

                html += '<div class="history-item' + itemClass + '">';
                html += '<span class="history-type ' + typeClass + '">' + typeName + '</span> ';
                html += '<span class="history-date">' + escHtml(item.Date) + '</span>';
                if (item.Admin) {
                    html += ' <span style="color:#666;font-size:12px">โดย ' + escHtml(item.Admin) + '</span>';
                }
                if (item.Reason) {
                    html += '<div style="margin-top:4px;font-size:13px">เหตุผล: ' + escHtml(item.Reason) + '</div>';
                }
                if (item.OldDate || item.NewDate) {
                    html += '<div class="history-dates">';
                    if (item.OldDate) html += 'วันเดิม: ' + escHtml(item.OldDate) + (item.NewDate ? ' &rarr; ' : '');
                    if (item.NewDate) html += 'วันใหม่: ' + escHtml(item.NewDate);
                    html += '</div>';
                }
                html += '</div>';
            }
            container.innerHTML = html;
        }

        function closeHistoryModal() {
            document.getElementById('historyModal').classList.remove('active');
        }

        // Close modals on overlay click
        document.addEventListener('click', function(e) {
            if (e.target.classList && e.target.classList.contains('modal-overlay')) {
                e.target.classList.remove('active');
            }
        });
    </script>
</asp:Content>
