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

    <center>
    <asp:GridView ID="GridView1" runat="server" OnRowCommand="GridView1_RowCommand"
        AutoGenerateColumns="False" CssClass="mydatagrid"
        PagerStyle-CssClass="pager" HeaderStyle-CssClass="header" RowStyle-CssClass="rows"
        EmptyDataText="ไม่มีรายการเลื่อนเข้าพัก">
        <Columns>
            <asp:ButtonField ButtonType="Button" CommandName="EditReservation" Text="ลงจอง"
                ControlStyle-CssClass="btn-edit" />

            <asp:BoundField DataField="ID" HeaderText="เลขที่จอง" HeaderStyle-CssClass="header-center"
                ItemStyle-CssClass="header-center">
                <HeaderStyle CssClass="header-center" />
                <ItemStyle CssClass="header-center" />
            </asp:BoundField>

            <asp:BoundField DataField="Name" HeaderText="ชื่อผู้จอง" HeaderStyle-CssClass="header-center">
                <HeaderStyle CssClass="header-center" />
            </asp:BoundField>

            <asp:BoundField DataField="NickName" HeaderText="ชื่อ Facebook" HeaderStyle-CssClass="header-center">
                <HeaderStyle CssClass="header-center" />
            </asp:BoundField>

            <asp:BoundField DataField="Customer_MobilePhone" HeaderText="เบอร์โทรศัพท์"
                HeaderStyle-CssClass="header-center" ItemStyle-CssClass="header-center">
                <HeaderStyle CssClass="header-center" />
                <ItemStyle CssClass="header-center" />
            </asp:BoundField>

            <asp:BoundField DataField="Deposit" HeaderText="ยอดมัดจำ"
                HeaderStyle-CssClass="header-center" ItemStyle-CssClass="header-center"
                DataFormatString="{0:N0}">
                <HeaderStyle CssClass="header-center" />
                <ItemStyle CssClass="header-center" />
            </asp:BoundField>

            <asp:BoundField DataField="TotalPrice" HeaderText="ยอดรวม"
                HeaderStyle-CssClass="header-center" ItemStyle-CssClass="header-center"
                DataFormatString="{0:N0}">
                <HeaderStyle CssClass="header-center" />
                <ItemStyle CssClass="header-center" />
            </asp:BoundField>

            <asp:TemplateField HeaderText="เลื่อน" HeaderStyle-CssClass="header-center"
                ItemStyle-CssClass="header-center">
                <ItemTemplate>
                    <span class="reschedule-count" onclick="showHistory(<%# Eval("ID") %>)" title="คลิกดูประวัติ">
                        <%# Eval("RescheduleCount") != DBNull.Value ? Eval("RescheduleCount") : "0" %> ครั้ง
                    </span>
                </ItemTemplate>
                <HeaderStyle CssClass="header-center" />
                <ItemStyle CssClass="header-center" />
            </asp:TemplateField>

            <asp:TemplateField HeaderText="การจองเดิม (ก่อนเลื่อน)" HeaderStyle-CssClass="header-center">
                <ItemTemplate>
                    <%# Eval("OrigCheckin") == DBNull.Value ? "<span style='color:#b0bec5;'>ไม่มีประวัติ</span>"
                        : string.Format("<b>{0:dd/MM/yyyy}</b> → {1:dd/MM/yyyy}<br/><small style='color:#7a8794;'>{2} คืน</small>",
                            Eval("OrigCheckin"), Eval("OrigCheckout"),
                            Eval("OrigStayDays") == DBNull.Value ? "-" : Eval("OrigStayDays")) %>
                </ItemTemplate>
            </asp:TemplateField>

            <asp:TemplateField HeaderText="วันที่ขอเลื่อน" HeaderStyle-CssClass="header-center">
                <ItemTemplate>
                    <%# Eval("LastRescheduledDate") == DBNull.Value
                        ? (Eval("PostponedDate") == DBNull.Value ? "<span style='color:#b0bec5;'>-</span>"
                            : string.Format("{0:dd/MM/yyyy HH:mm}", Eval("PostponedDate")))
                        : string.Format("{0:dd/MM/yyyy HH:mm}", Eval("LastRescheduledDate")) %>
                    <%# Eval("LastRescheduledBy") == DBNull.Value || Eval("LastRescheduledBy").ToString() == ""
                        ? "" : "<br/><small style='color:#7a8794;'>โดย " + Eval("LastRescheduledBy") + "</small>" %>
                </ItemTemplate>
            </asp:TemplateField>

            <asp:TemplateField HeaderText="วันที่จองเข้ามา" HeaderStyle-CssClass="header-center">
                <ItemTemplate>
                    <%# Eval("Created_Date") == DBNull.Value ? "-" : string.Format("{0:dd/MM/yyyy}", Eval("Created_Date")) %>
                </ItemTemplate>
            </asp:TemplateField>

            <asp:BoundField DataField="Remark" HeaderText="หมายเหตุ" HeaderStyle-CssClass="header-center">
                <HeaderStyle CssClass="header-center" />
            </asp:BoundField>

            <asp:TemplateField HeaderText="เหตุผลล่าสุด" HeaderStyle-CssClass="header-center">
                <ItemTemplate>
                    <span class="reason-text">
                        <%# Eval("LastRescheduleReason") != DBNull.Value ? Eval("LastRescheduleReason") : "-" %>
                    </span>
                </ItemTemplate>
                <HeaderStyle CssClass="header-center" />
            </asp:TemplateField>

            <asp:TemplateField HeaderStyle-Width="3%">
                <ItemTemplate>
                    <button type="button" class="btn-cancel"
                        onclick="showCancelModal(<%# Eval("ID") %>, '<%# Eval("Name") %>')">
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
            <p>ยืนยันการยกเลิกรายการเลื่อนเข้าพักของ <strong id="cancelCustomerName"></strong> ?</p>
            <label>เหตุผลในการยกเลิก:</label>
            <input type="text" id="cancelReasonInput" class="cancel-reason-input"
                placeholder="ระบุเหตุผล (ไม่บังคับ)" maxlength="500" />
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
        // Cancel modal
        var cancelReservationId = 0;

        function showCancelModal(reservationId, customerName) {
            cancelReservationId = reservationId;
            document.getElementById('cancelCustomerName').textContent = customerName || 'ลูกค้า';
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
            document.getElementById('historyReservationId').textContent = reservationId;
            document.getElementById('historyContent').innerHTML = '<p class="no-history">กำลังโหลด...</p>';
            document.getElementById('historyModal').classList.add('active');

            // Fetch history via page method
            var xhr = new XMLHttpRequest();
            xhr.open('GET', '/PostponeList.aspx?action=gethistory&rid=' + reservationId, true);
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
                var typeName = 'เปลี่ยนวัน';
                var itemClass = '';

                if (item.Type === 'POSTPONE') {
                    typeClass = 'postpone'; typeName = 'เลื่อนวัน'; itemClass = ' postpone';
                } else if (item.Type === 'CANCEL_POSTPONE') {
                    typeClass = 'cancel'; typeName = 'ยกเลิกเลื่อน'; itemClass = ' cancel';
                }

                html += '<div class="history-item' + itemClass + '">';
                html += '<span class="history-type ' + typeClass + '">' + typeName + '</span> ';
                html += '<span class="history-date">' + item.Date + '</span>';
                if (item.Admin) {
                    html += ' <span style="color:#666;font-size:12px">โดย ' + item.Admin + '</span>';
                }
                if (item.Reason) {
                    html += '<div style="margin-top:4px;font-size:13px">เหตุผล: ' + item.Reason + '</div>';
                }
                if (item.OldDate || item.NewDate) {
                    html += '<div class="history-dates">';
                    if (item.OldDate) html += 'วันเดิม: ' + item.OldDate + ' &rarr; ';
                    if (item.NewDate) html += 'วันใหม่: ' + item.NewDate;
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
            if (e.target.classList.contains('modal-overlay')) {
                e.target.classList.remove('active');
            }
        });
    </script>
</asp:Content>
