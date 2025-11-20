<%@ Page Title="ประวัติการชำระเงิน" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="PaymentHistory.aspx.cs" Inherits="Take_Time_BangPhra.Payment.PaymentHistory" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .history-container {
            max-width: 1400px;
            margin: 20px auto;
            padding: 20px;
        }

        .history-card {
            background: white;
            border-radius: 8px;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
            padding: 25px;
            margin-bottom: 20px;
        }

        .card-header {
            font-size: 22px;
            font-weight: bold;
            color: #2c3e50;
            margin-bottom: 20px;
            padding-bottom: 10px;
            border-bottom: 2px solid #3498db;
            display: flex;
            justify-content: space-between;
            align-items: center;
        }

        .filter-section {
            display: flex;
            gap: 15px;
            flex-wrap: wrap;
            margin-bottom: 20px;
            padding: 15px;
            background: #f8f9fa;
            border-radius: 4px;
        }

        .filter-group {
            display: flex;
            flex-direction: column;
            gap: 5px;
        }

        .filter-label {
            font-weight: bold;
            font-size: 14px;
            color: #2c3e50;
        }

        .filter-control {
            padding: 8px;
            border: 1px solid #ddd;
            border-radius: 4px;
            font-size: 14px;
        }

        .btn-filter {
            background-color: #3498db;
            color: white;
            padding: 8px 20px;
            border: none;
            border-radius: 4px;
            cursor: pointer;
            align-self: flex-end;
        }

        .btn-filter:hover {
            background-color: #2980b9;
        }

        .summary-cards {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 15px;
            margin-bottom: 20px;
        }

        .summary-card {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 20px;
            border-radius: 8px;
            text-align: center;
        }

        .summary-card.green {
            background: linear-gradient(135deg, #56ab2f 0%, #a8e063 100%);
        }

        .summary-card.orange {
            background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%);
        }

        .summary-card.blue {
            background: linear-gradient(135deg, #4facfe 0%, #00f2fe 100%);
        }

        .summary-label {
            font-size: 14px;
            opacity: 0.9;
            margin-bottom: 10px;
        }

        .summary-value {
            font-size: 28px;
            font-weight: bold;
        }

        .payment-table {
            width: 100%;
            border-collapse: collapse;
        }

        .payment-table th {
            background-color: #34495e;
            color: white;
            padding: 12px;
            text-align: left;
            font-weight: bold;
            position: sticky;
            top: 0;
        }

        .payment-table td {
            padding: 12px;
            border-bottom: 1px solid #ecf0f1;
        }

        .payment-table tr:hover {
            background-color: #f8f9fa;
        }

        .badge {
            padding: 5px 10px;
            border-radius: 4px;
            font-size: 12px;
            font-weight: bold;
            text-transform: uppercase;
        }

        .badge-success {
            background-color: #27ae60;
            color: white;
        }

        .badge-warning {
            background-color: #f39c12;
            color: white;
        }

        .badge-danger {
            background-color: #e74c3c;
            color: white;
        }

        .badge-info {
            background-color: #3498db;
            color: white;
        }

        .btn-action {
            padding: 6px 12px;
            border: none;
            border-radius: 3px;
            cursor: pointer;
            font-size: 12px;
            margin-right: 5px;
        }

        .btn-view-slip {
            background-color: #3498db;
            color: white;
        }

        .btn-view-receipt {
            background-color: #27ae60;
            color: white;
        }

        .btn-view-slip:hover {
            background-color: #2980b9;
        }

        .btn-view-receipt:hover {
            background-color: #229954;
        }

        .alert {
            padding: 15px;
            border-radius: 4px;
            margin-bottom: 20px;
        }

        .alert-info {
            background-color: #d1ecf1;
            border: 1px solid #bee5eb;
            color: #0c5460;
        }

        .empty-state {
            text-align: center;
            padding: 60px 20px;
            color: #7f8c8d;
        }

        .empty-state i {
            font-size: 64px;
            margin-bottom: 20px;
        }

        .modal {
            display: none;
            position: fixed;
            z-index: 1000;
            left: 0;
            top: 0;
            width: 100%;
            height: 100%;
            overflow: auto;
            background-color: rgba(0,0,0,0.4);
        }

        .modal-content {
            background-color: #fefefe;
            margin: 5% auto;
            padding: 20px;
            border: 1px solid #888;
            width: 80%;
            max-width: 800px;
            border-radius: 8px;
        }

        .modal-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 20px;
        }

        .close {
            color: #aaa;
            font-size: 28px;
            font-weight: bold;
            cursor: pointer;
        }

        .close:hover {
            color: #000;
        }
    </style>

    <div class="history-container">
        <h2 style="color: #2c3e50; margin-bottom: 30px;">
            <i class="fa fa-history"></i> ประวัติการชำระเงิน
        </h2>

        <!-- Summary Cards -->
        <div class="summary-cards">
            <div class="summary-card green">
                <div class="summary-label">ยอดชำระทั้งหมด</div>
                <div class="summary-value">
                    <asp:Label ID="lblTotalAmount" runat="server" Text="0.00"></asp:Label> ฿
                </div>
            </div>

            <div class="summary-card blue">
                <div class="summary-label">จำนวนรายการ</div>
                <div class="summary-value">
                    <asp:Label ID="lblTotalRecords" runat="server" Text="0"></asp:Label>
                </div>
            </div>

            <div class="summary-card orange">
                <div class="summary-label">รอตรวจสอบ</div>
                <div class="summary-value">
                    <asp:Label ID="lblPendingRecords" runat="server" Text="0"></asp:Label>
                </div>
            </div>

            <div class="summary-card">
                <div class="summary-label">สำเร็จแล้ว</div>
                <div class="summary-value">
                    <asp:Label ID="lblCompletedRecords" runat="server" Text="0"></asp:Label>
                </div>
            </div>
        </div>

        <!-- Filter Section -->
        <div class="history-card">
            <div class="card-header">
                <span><i class="fa fa-filter"></i> ค้นหาและกรอง</span>
            </div>

            <div class="filter-section">
                <div class="filter-group">
                    <label class="filter-label">รหัสการจอง</label>
                    <asp:TextBox ID="txtReservationID" runat="server" CssClass="filter-control"
                        placeholder="รหัสการจอง" TextMode="Number"></asp:TextBox>
                </div>

                <div class="filter-group">
                    <label class="filter-label">เบอร์โทร</label>
                    <asp:TextBox ID="txtPhoneNumber" runat="server" CssClass="filter-control"
                        placeholder="เบอร์โทรศัพท์"></asp:TextBox>
                </div>

                <div class="filter-group">
                    <label class="filter-label">วันที่เริ่มต้น</label>
                    <asp:TextBox ID="txtStartDate" runat="server" CssClass="filter-control"
                        TextMode="Date"></asp:TextBox>
                </div>

                <div class="filter-group">
                    <label class="filter-label">วันที่สิ้นสุด</label>
                    <asp:TextBox ID="txtEndDate" runat="server" CssClass="filter-control"
                        TextMode="Date"></asp:TextBox>
                </div>

                <div class="filter-group">
                    <label class="filter-label">สถานะ</label>
                    <asp:DropDownList ID="ddlStatus" runat="server" CssClass="filter-control">
                        <asp:ListItem Value="">-- ทั้งหมด --</asp:ListItem>
                        <asp:ListItem Value="COMPLETED">สำเร็จ</asp:ListItem>
                        <asp:ListItem Value="PENDING">รอดำเนินการ</asp:ListItem>
                        <asp:ListItem Value="CANCELLED">ยกเลิก</asp:ListItem>
                    </asp:DropDownList>
                </div>

                <div class="filter-group">
                    <label class="filter-label">&nbsp;</label>
                    <asp:Button ID="btnFilter" runat="server" Text="ค้นหา" CssClass="btn-filter"
                        OnClick="btnFilter_Click" />
                </div>
            </div>
        </div>

        <!-- Payment History Table -->
        <div class="history-card">
            <div class="card-header">
                <span><i class="fa fa-list"></i> รายการชำระเงิน</span>
                <asp:Button ID="btnExport" runat="server" Text="Export Excel" CssClass="btn-filter"
                    OnClick="btnExport_Click" Visible="false" />
            </div>

            <asp:GridView ID="gvPaymentHistory" runat="server" CssClass="payment-table"
                AutoGenerateColumns="False" EmptyDataText="ไม่พบข้อมูลการชำระเงิน"
                OnRowCommand="gvPaymentHistory_RowCommand" DataKeyNames="ID">
                <Columns>
                    <asp:BoundField DataField="ID" HeaderText="ID" Visible="false" />

                    <asp:TemplateField HeaderText="วันที่ชำระ">
                        <ItemTemplate>
                            <%# Convert.ToDateTime(Eval("PaymentDate")).ToString("dd/MM/yyyy HH:mm") %>
                        </ItemTemplate>
                    </asp:TemplateField>

                    <asp:BoundField DataField="Reservation_ID" HeaderText="รหัสการจอง" />

                    <asp:BoundField DataField="Customer_MobilePhone" HeaderText="เบอร์โทร" />

                    <asp:TemplateField HeaderText="จำนวนเงิน">
                        <ItemTemplate>
                            <span style="font-weight: bold; color: #27ae60;">
                                <%# Convert.ToDecimal(Eval("PaymentAmount")).ToString("N2") %>
                            </span>
                        </ItemTemplate>
                    </asp:TemplateField>

                    <asp:TemplateField HeaderText="ประเภท">
                        <ItemTemplate>
                            <span class="badge badge-info"><%# Eval("PaymentType") %></span>
                        </ItemTemplate>
                    </asp:TemplateField>

                    <asp:BoundField DataField="PaymentMethod" HeaderText="วิธีชำระ" />

                    <asp:BoundField DataField="Receipt_ID" HeaderText="เลขที่ใบเสร็จ" />

                    <asp:TemplateField HeaderText="สถานะ">
                        <ItemTemplate>
                            <span class="badge <%# GetStatusClass(Eval("Status").ToString()) %>">
                                <%# GetStatusText(Eval("Status").ToString()) %>
                            </span>
                        </ItemTemplate>
                    </asp:TemplateField>

                    <asp:TemplateField HeaderText="ยอดคงเหลือ">
                        <ItemTemplate>
                            <%# Eval("RemainingBalance") != DBNull.Value ? Convert.ToDecimal(Eval("RemainingBalance")).ToString("N2") : "-" %>
                        </ItemTemplate>
                    </asp:TemplateField>

                    <asp:TemplateField HeaderText="การดำเนินการ">
                        <ItemTemplate>
                            <asp:Button ID="btnViewSlip" runat="server" Text="ดูสลิป"
                                CssClass="btn-action btn-view-slip"
                                CommandName="ViewSlip" CommandArgument='<%# Eval("PaymentSlip_ID") %>'
                                Visible='<%# Eval("PaymentSlip_ID") != DBNull.Value %>' />

                            <asp:Button ID="btnViewReceipt" runat="server" Text="ดูใบเสร็จ"
                                CssClass="btn-action btn-view-receipt"
                                CommandName="ViewReceipt" CommandArgument='<%# Eval("Receipt_ID") %>'
                                Visible='<%# Eval("Receipt_ID") != DBNull.Value %>' />
                        </ItemTemplate>
                    </asp:TemplateField>
                </Columns>
            </asp:GridView>

            <asp:Panel ID="pnlEmpty" runat="server" CssClass="empty-state" Visible="false">
                <i class="fa fa-inbox"></i>
                <h3>ไม่พบข้อมูลการชำระเงิน</h3>
                <p>ลองเปลี่ยนเกณฑ์การค้นหาหรือกรองข้อมูลใหม่</p>
            </asp:Panel>
        </div>
    </div>

    <!-- Modal for viewing slip/receipt -->
    <div id="viewModal" class="modal">
        <div class="modal-content">
            <div class="modal-header">
                <h3 id="modalTitle">รายละเอียด</h3>
                <span class="close" onclick="closeModal()">&times;</span>
            </div>
            <div id="modalBody">
                <iframe id="documentFrame" style="width: 100%; height: 600px; border: none;"></iframe>
            </div>
        </div>
    </div>

    <script type="text/javascript">
        function showModal(title, url) {
            document.getElementById('modalTitle').innerText = title;
            document.getElementById('documentFrame').src = url;
            document.getElementById('viewModal').style.display = 'block';
        }

        function closeModal() {
            document.getElementById('viewModal').style.display = 'none';
            document.getElementById('documentFrame').src = '';
        }

        window.onclick = function (event) {
            var modal = document.getElementById('viewModal');
            if (event.target == modal) {
                closeModal();
            }
        }
    </script>
</asp:Content>
