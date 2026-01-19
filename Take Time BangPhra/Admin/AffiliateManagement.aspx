<%@ Page Title="จัดการ Affiliate" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="AffiliateManagement.aspx.cs" Inherits="Take_Time_BangPhra.Admin.AffiliateManagement" MaintainScrollPositionOnPostback="true" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .affiliate-management {
            padding: 20px;
            max-width: 1400px;
            margin: 0 auto;
        }

        .section-header {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 15px 20px;
            border-radius: 8px;
            margin-bottom: 20px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
        }

        .section-header h2 {
            margin: 0;
            font-size: 24px;
            font-weight: 600;
        }

        /* Dashboard Stats */
        .stats-container {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
            gap: 15px;
            margin-bottom: 25px;
        }

        .stat-card {
            background: white;
            padding: 20px;
            border-radius: 10px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
            text-align: center;
            border-top: 4px solid #667eea;
        }

        .stat-card.warning {
            border-top-color: #f5576c;
        }

        .stat-card.success {
            border-top-color: #11998e;
        }

        .stat-card h4 {
            margin: 0 0 10px 0;
            font-size: 13px;
            color: #666;
            font-weight: 500;
        }

        .stat-card .value {
            font-size: 28px;
            font-weight: 700;
            color: #333;
        }

        .stat-card .value.amount {
            font-size: 22px;
            color: #667eea;
        }

        /* Tab Navigation */
        .tab-navigation {
            display: flex;
            gap: 5px;
            margin-bottom: 20px;
            border-bottom: 2px solid #e0e0e0;
            padding-bottom: 0;
            flex-wrap: wrap;
        }

        .tab-btn {
            background: #f5f5f5;
            border: none;
            padding: 12px 20px;
            font-size: 14px;
            font-weight: 500;
            color: #666;
            cursor: pointer;
            border-radius: 8px 8px 0 0;
            transition: all 0.3s ease;
            margin-bottom: -2px;
        }

        .tab-btn:hover {
            background: #e0e0e0;
        }

        .tab-btn.active {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            border-bottom: 2px solid #667eea;
        }

        .tab-content {
            display: none;
        }

        .tab-content.active {
            display: block;
        }

        /* Filter Section */
        .filter-section {
            background: white;
            padding: 20px;
            border-radius: 8px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
            margin-bottom: 20px;
        }

        .filter-row {
            display: flex;
            gap: 15px;
            flex-wrap: wrap;
            align-items: flex-end;
        }

        .filter-group {
            display: flex;
            flex-direction: column;
            gap: 5px;
        }

        .filter-group label {
            font-size: 13px;
            color: #666;
            font-weight: 500;
        }

        .form-control {
            padding: 10px 15px;
            border: 2px solid #e0e0e0;
            border-radius: 6px;
            font-size: 14px;
            min-width: 150px;
            min-height: 42px;
        }

        .form-control:focus {
            border-color: #667eea;
            outline: none;
        }

        /* Buttons */
        .btn {
            padding: 10px 20px;
            border: none;
            border-radius: 6px;
            font-weight: 500;
            cursor: pointer;
            font-size: 14px;
            transition: all 0.3s;
        }

        .btn-primary {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
        }

        .btn-success {
            background: linear-gradient(135deg, #11998e 0%, #38ef7d 100%);
            color: white;
        }

        .btn-warning {
            background: #ffc107;
            color: #333;
        }

        .btn-danger {
            background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%);
            color: white;
        }

        .btn-info {
            background: linear-gradient(135deg, #4facfe 0%, #00f2fe 100%);
            color: white;
        }

        .btn-sm {
            padding: 6px 12px;
            font-size: 12px;
        }

        .btn:hover {
            transform: translateY(-1px);
            box-shadow: 0 4px 12px rgba(0,0,0,0.2);
        }

        /* Data Table */
        .data-table {
            background: white;
            border-radius: 8px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
            overflow-x: auto;
            -webkit-overflow-scrolling: touch;
        }

        .data-table table {
            width: 100%;
            border-collapse: collapse;
            min-width: 800px;
        }

        .data-table th {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 12px;
            text-align: left;
            font-weight: 500;
            font-size: 13px;
            white-space: nowrap;
        }

        .data-table td {
            padding: 12px;
            border-bottom: 1px solid #f0f0f0;
            font-size: 13px;
        }

        .data-table tr:hover {
            background-color: #f8f9fa;
        }

        /* Badges */
        .badge {
            display: inline-block;
            padding: 4px 10px;
            border-radius: 4px;
            font-size: 11px;
            font-weight: 500;
        }

        .badge-active {
            background: #d4edda;
            color: #155724;
        }

        .badge-inactive {
            background: #f8d7da;
            color: #721c24;
        }

        .badge-new {
            background: #fff3cd;
            color: #856404;
        }

        .badge-transferred {
            background: #d4edda;
            color: #155724;
        }

        .badge-pending {
            background: #fff3cd;
            color: #856404;
        }

        /* Affiliate Card */
        .affiliate-code {
            font-family: 'Courier New', monospace;
            background: #f8f9fa;
            padding: 4px 8px;
            border-radius: 4px;
            font-weight: 600;
            color: #667eea;
        }

        .amount-positive {
            color: #28a745;
            font-weight: 600;
        }

        .amount-pending {
            color: #ffc107;
            font-weight: 600;
        }

        /* Alert */
        .alert {
            padding: 15px 20px;
            border-radius: 8px;
            margin-bottom: 20px;
        }

        .alert-success {
            background: #d4edda;
            color: #155724;
            border: 1px solid #c3e6cb;
        }

        .alert-error {
            background: #f8d7da;
            color: #721c24;
            border: 1px solid #f5c6cb;
        }

        /* Commission Summary Card */
        .commission-summary {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 20px;
            border-radius: 10px;
            margin-bottom: 20px;
        }

        .commission-summary h3 {
            margin: 0 0 15px 0;
            font-size: 18px;
        }

        .summary-row {
            display: flex;
            justify-content: space-between;
            padding: 8px 0;
            border-bottom: 1px solid rgba(255,255,255,0.2);
        }

        .summary-row:last-child {
            border-bottom: none;
            font-weight: 700;
            font-size: 18px;
        }

        /* Checkbox styling for payment selection */
        .payment-checkbox {
            transform: scale(1.3);
            cursor: pointer;
        }

        /* Mobile responsive */
        @media (max-width: 768px) {
            .affiliate-management {
                padding: 10px;
            }
            .stats-container {
                grid-template-columns: repeat(2, 1fr);
            }
            .tab-navigation {
                flex-wrap: wrap;
            }
            .tab-btn {
                padding: 10px 15px;
                font-size: 12px;
                flex: 1;
                min-width: 120px;
                text-align: center;
            }
            .filter-row {
                flex-direction: column;
            }
            .filter-group {
                width: 100%;
            }
            .filter-group .form-control {
                width: 100%;
            }
            .data-table {
                margin: 10px -15px;
                border-radius: 0;
                overflow-x: scroll !important;
                -webkit-overflow-scrolling: touch !important;
                touch-action: pan-x pan-y !important;
            }
        }
    </style>

    <div class="affiliate-management">
        <div class="section-header">
            <h2>Affiliate Management</h2>
        </div>

        <!-- Message Panel -->
        <asp:Panel ID="pnlMessage" runat="server" Visible="false" CssClass="alert">
            <asp:Label ID="lblMessage" runat="server"></asp:Label>
        </asp:Panel>

        <!-- Dashboard Statistics -->
        <div class="stats-container">
            <div class="stat-card">
                <h4>Affiliate ทั้งหมด</h4>
                <div class="value"><asp:Label ID="lblTotalAffiliates" runat="server" Text="0"></asp:Label></div>
            </div>
            <div class="stat-card warning">
                <h4>รอจ่ายค่าคอมมิชชั่น</h4>
                <div class="value"><asp:Label ID="lblPendingCommissions" runat="server" Text="0"></asp:Label></div>
            </div>
            <div class="stat-card warning">
                <h4>ยอดรอจ่าย (บาท)</h4>
                <div class="value amount"><asp:Label ID="lblPendingAmount" runat="server" Text="0"></asp:Label></div>
            </div>
            <div class="stat-card success">
                <h4>จ่ายแล้ว (บาท)</h4>
                <div class="value amount"><asp:Label ID="lblTotalPaidAmount" runat="server" Text="0"></asp:Label></div>
            </div>
        </div>

        <!-- Tab Navigation -->
        <div class="tab-navigation">
            <asp:Button ID="btnTabMembers" runat="server" Text="สมาชิก Affiliate" CssClass="tab-btn active" OnClick="btnTabMembers_Click" />
            <asp:Button ID="btnTabCommissions" runat="server" Text="ค่าคอมมิชชั่น" CssClass="tab-btn" OnClick="btnTabCommissions_Click" />
            <asp:Button ID="btnTabCreatePayment" runat="server" Text="สร้างใบสำคัญจ่าย" CssClass="tab-btn" OnClick="btnTabCreatePayment_Click" />
            <asp:Button ID="btnTabHistory" runat="server" Text="ประวัติการจ่าย" CssClass="tab-btn" OnClick="btnTabHistory_Click" />
        </div>

        <asp:HiddenField ID="hdnActiveTab" runat="server" Value="members" />

        <!-- Tab 1: Affiliate Members -->
        <asp:Panel ID="pnlMembers" runat="server" CssClass="tab-content active">
            <div class="filter-section">
                <div class="filter-row">
                    <div class="filter-group">
                        <label>ค้นหา</label>
                        <asp:TextBox ID="txtSearchMember" runat="server" CssClass="form-control" placeholder="ชื่อ, รหัสคูปอง..."></asp:TextBox>
                    </div>
                    <div class="filter-group">
                        <label>&nbsp;</label>
                        <asp:Button ID="btnSearchMember" runat="server" Text="ค้นหา" CssClass="btn btn-primary" OnClick="btnSearchMember_Click" />
                    </div>
                </div>
            </div>

            <div class="data-table">
                <asp:GridView ID="gvMembers" runat="server" AutoGenerateColumns="False" CssClass="table" GridLines="None" OnRowCommand="gvMembers_RowCommand">
                    <Columns>
                        <asp:TemplateField HeaderText="รหัสคูปอง">
                            <ItemTemplate>
                                <span class="affiliate-code"><%# Eval("CouponCode") %></span>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="ชื่อ">
                            <ItemTemplate>
                                <strong><%# Eval("VendorName") %></strong><br />
                                <small style="color: #666;"><%# Eval("FirstName") %> <%# Eval("LastName") %></small>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:BoundField DataField="PhoneNumber" HeaderText="เบอร์โทร" />
                        <asp:TemplateField HeaderText="ธนาคาร">
                            <ItemTemplate>
                                <%# Eval("BankCode") %><br />
                                <small><%# Eval("BankNumber") %></small>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="สถานะ">
                            <ItemTemplate>
                                <%# Convert.ToBoolean(Eval("Status"))
                                    ? "<span class='badge badge-active'>เปิดใช้งาน</span>"
                                    : "<span class='badge badge-inactive'>ปิดใช้งาน</span>" %>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="จองทั้งหมด">
                            <ItemTemplate>
                                <%# Eval("TotalReservations") %> รายการ
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="รายได้รวม">
                            <ItemTemplate>
                                <span class="amount-positive"><%# string.Format("{0:N2}", Eval("TotalEarned")) %></span>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="รอจ่าย">
                            <ItemTemplate>
                                <span class="amount-pending"><%# string.Format("{0:N2}", Eval("PendingEarnings")) %></span>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="จัดการ">
                            <ItemTemplate>
                                <asp:Button ID="btnToggleStatus" runat="server"
                                    Text='<%# Convert.ToBoolean(Eval("Status")) ? "ปิดใช้งาน" : "เปิดใช้งาน" %>'
                                    CssClass='<%# Convert.ToBoolean(Eval("Status")) ? "btn btn-danger btn-sm" : "btn btn-success btn-sm" %>'
                                    CommandName="ToggleStatus" CommandArgument='<%# Eval("ID") %>'
                                    OnClientClick="return confirm('ยืนยันการเปลี่ยนสถานะ?');" />
                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                    <EmptyDataTemplate>
                        <div style="text-align: center; padding: 40px; color: #999;">
                            ไม่พบข้อมูล Affiliate
                        </div>
                    </EmptyDataTemplate>
                </asp:GridView>
            </div>
        </asp:Panel>

        <!-- Tab 2: Commission Tracking -->
        <asp:Panel ID="pnlCommissions" runat="server" CssClass="tab-content">
            <div class="filter-section">
                <div class="filter-row">
                    <div class="filter-group">
                        <label>Affiliate</label>
                        <asp:DropDownList ID="ddlFilterAffiliate" runat="server" CssClass="form-control" AutoPostBack="true" OnSelectedIndexChanged="ddlFilterAffiliate_SelectedIndexChanged">
                            <asp:ListItem Value="">ทั้งหมด</asp:ListItem>
                        </asp:DropDownList>
                    </div>
                    <div class="filter-group">
                        <label>สถานะ</label>
                        <asp:DropDownList ID="ddlFilterStatus" runat="server" CssClass="form-control" AutoPostBack="true" OnSelectedIndexChanged="ddlFilterStatus_SelectedIndexChanged">
                            <asp:ListItem Value="">ทั้งหมด</asp:ListItem>
                            <asp:ListItem Value="NEW">รอจ่าย</asp:ListItem>
                            <asp:ListItem Value="TRANSFERED">จ่ายแล้ว</asp:ListItem>
                        </asp:DropDownList>
                    </div>
                    <div class="filter-group">
                        <label>ปี</label>
                        <asp:DropDownList ID="ddlFilterYear" runat="server" CssClass="form-control" AutoPostBack="true" OnSelectedIndexChanged="ddlFilterYear_SelectedIndexChanged">
                        </asp:DropDownList>
                    </div>
                </div>
            </div>

            <div class="data-table">
                <asp:GridView ID="gvCommissions" runat="server" AutoGenerateColumns="False" CssClass="table" GridLines="None">
                    <Columns>
                        <asp:TemplateField HeaderText="Affiliate">
                            <ItemTemplate>
                                <span class="affiliate-code"><%# Eval("CouponCode") %></span><br />
                                <small><%# Eval("AffiliateName") %></small>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="วันเข้าพัก">
                            <ItemTemplate>
                                <%# Convert.ToDateTime(Eval("StayDate")).ToString("dd/MM/yyyy") %>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:BoundField DataField="RoomName" HeaderText="ห้องพัก" />
                        <asp:BoundField DataField="CustomerName" HeaderText="ลูกค้า" />
                        <asp:TemplateField HeaderText="ราคา">
                            <ItemTemplate>
                                <%# string.Format("{0:N2}", Eval("Price")) %>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="ค่าคอมมิชชั่น">
                            <ItemTemplate>
                                <strong style="color: #667eea;"><%# string.Format("{0:N2}", Eval("Commission")) %></strong>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="สถานะการจอง">
                            <ItemTemplate>
                                <%# Eval("ReservationStatus") %>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="สถานะจ่าย">
                            <ItemTemplate>
                                <%# Eval("Status").ToString() == "NEW"
                                    ? "<span class='badge badge-new'>รอจ่าย</span>"
                                    : "<span class='badge badge-transferred'>จ่ายแล้ว</span>" %>
                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                    <EmptyDataTemplate>
                        <div style="text-align: center; padding: 40px; color: #999;">
                            ไม่พบข้อมูลค่าคอมมิชชั่น
                        </div>
                    </EmptyDataTemplate>
                </asp:GridView>
            </div>
        </asp:Panel>

        <!-- Tab 3: Create Payment Voucher -->
        <asp:Panel ID="pnlCreatePayment" runat="server" CssClass="tab-content">
            <div class="filter-section">
                <h4 style="margin-top: 0;">สร้างใบสำคัญจ่าย Affiliate</h4>
                <div class="filter-row">
                    <div class="filter-group">
                        <label>วันที่ใบสำคัญจ่าย</label>
                        <asp:TextBox ID="txtPaymentDate" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                    </div>
                    <div class="filter-group">
                        <label>วิธีจ่ายเงิน</label>
                        <asp:DropDownList ID="ddlPaymentMethod" runat="server" CssClass="form-control">
                            <asp:ListItem Value="">---โปรดเลือก---</asp:ListItem>
                            <asp:ListItem Value="โอนเงิน">โอนเงิน</asp:ListItem>
                            <asp:ListItem Value="เงินสด">เงินสด</asp:ListItem>
                            <asp:ListItem Value="เช็ค">เช็ค</asp:ListItem>
                        </asp:DropDownList>
                    </div>
                    <div class="filter-group">
                        <label>เลือก Affiliate</label>
                        <asp:DropDownList ID="ddlSelectAffiliate" runat="server" CssClass="form-control" AutoPostBack="true" OnSelectedIndexChanged="ddlSelectAffiliate_SelectedIndexChanged">
                            <asp:ListItem Value="">---โปรดเลือก---</asp:ListItem>
                        </asp:DropDownList>
                    </div>
                </div>
            </div>

            <!-- Pending Commissions to Pay -->
            <asp:Panel ID="pnlPendingToPay" runat="server" Visible="false">
                <div class="commission-summary">
                    <h3>รายการรอจ่ายของ <asp:Label ID="lblSelectedAffiliate" runat="server"></asp:Label></h3>
                    <div class="summary-row">
                        <span>จำนวนรายการ:</span>
                        <span><asp:Label ID="lblPaymentCount" runat="server" Text="0"></asp:Label> รายการ</span>
                    </div>
                    <div class="summary-row">
                        <span>ยอดรวม:</span>
                        <span><asp:Label ID="lblPaymentTotal" runat="server" Text="0"></asp:Label> บาท</span>
                    </div>
                </div>

                <div class="data-table" style="margin-bottom: 20px;">
                    <asp:GridView ID="gvPendingPayments" runat="server" AutoGenerateColumns="False" CssClass="table" GridLines="None">
                        <Columns>
                            <asp:TemplateField HeaderText="เลือก">
                                <ItemTemplate>
                                    <asp:CheckBox ID="chkSelect" runat="server" CssClass="payment-checkbox" Checked="true" />
                                </ItemTemplate>
                                <HeaderStyle Width="60px" />
                            </asp:TemplateField>
                            <asp:BoundField DataField="ID" HeaderText="ID" />
                            <asp:TemplateField HeaderText="วันเข้าพัก">
                                <ItemTemplate>
                                    <%# Convert.ToDateTime(Eval("StayDate")).ToString("dd/MM/yyyy") %>
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:BoundField DataField="RoomName" HeaderText="ห้องพัก" />
                            <asp:TemplateField HeaderText="ราคา">
                                <ItemTemplate>
                                    <%# string.Format("{0:N2}", Eval("Price")) %>
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="ค่าคอมมิชชั่น">
                                <ItemTemplate>
                                    <strong style="color: #fff; background: #667eea; padding: 4px 8px; border-radius: 4px;">
                                        <%# string.Format("{0:N2}", Eval("Commission")) %>
                                    </strong>
                                </ItemTemplate>
                            </asp:TemplateField>
                        </Columns>
                        <EmptyDataTemplate>
                            <div style="text-align: center; padding: 40px; color: #999;">
                                ไม่มีรายการรอจ่ายสำหรับ Affiliate นี้
                            </div>
                        </EmptyDataTemplate>
                    </asp:GridView>
                </div>

                <div style="text-align: center;">
                    <asp:Button ID="btnCreatePaymentVoucher" runat="server" Text="สร้างใบสำคัญจ่าย"
                        CssClass="btn btn-success" OnClick="btnCreatePaymentVoucher_Click"
                        OnClientClick="return confirm('ยืนยันการสร้างใบสำคัญจ่าย?');" />
                </div>
            </asp:Panel>
        </asp:Panel>

        <!-- Tab 4: Payment History -->
        <asp:Panel ID="pnlHistory" runat="server" CssClass="tab-content">
            <div class="filter-section">
                <div class="filter-row">
                    <div class="filter-group">
                        <label>Affiliate</label>
                        <asp:DropDownList ID="ddlHistoryAffiliate" runat="server" CssClass="form-control" AutoPostBack="true" OnSelectedIndexChanged="ddlHistoryAffiliate_SelectedIndexChanged">
                            <asp:ListItem Value="">ทั้งหมด</asp:ListItem>
                        </asp:DropDownList>
                    </div>
                    <div class="filter-group">
                        <label>ปี</label>
                        <asp:DropDownList ID="ddlHistoryYear" runat="server" CssClass="form-control" AutoPostBack="true" OnSelectedIndexChanged="ddlHistoryYear_SelectedIndexChanged">
                        </asp:DropDownList>
                    </div>
                </div>
            </div>

            <div class="data-table">
                <asp:GridView ID="gvHistory" runat="server" AutoGenerateColumns="False" CssClass="table" GridLines="None" OnRowCommand="gvHistory_RowCommand">
                    <Columns>
                        <asp:BoundField DataField="PaymentID" HeaderText="เลขที่" />
                        <asp:TemplateField HeaderText="วันที่จ่าย">
                            <ItemTemplate>
                                <%# Convert.ToDateTime(Eval("PaymentDate")).ToString("dd/MM/yyyy") %>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="Affiliate">
                            <ItemTemplate>
                                <span class="affiliate-code"><%# Eval("CouponCode") %></span><br />
                                <small><%# Eval("AffiliateName") %></small>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="จำนวนรายการ">
                            <ItemTemplate>
                                <%# Eval("ReservationCount") %> รายการ
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="ยอดรวม">
                            <ItemTemplate>
                                <strong style="color: #28a745;"><%# string.Format("{0:N2}", Eval("TotalAmount")) %></strong>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:BoundField DataField="PaymentMethod" HeaderText="วิธีจ่าย" />
                        <asp:TemplateField HeaderText="เอกสาร">
                            <ItemTemplate>
                                <asp:Button ID="btnViewVoucher" runat="server" Text="ใบสำคัญจ่าย"
                                    CssClass="btn btn-info btn-sm" CommandName="ViewVoucher"
                                    CommandArgument='<%# Eval("PaymentID") %>' />
                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                    <EmptyDataTemplate>
                        <div style="text-align: center; padding: 40px; color: #999;">
                            ไม่พบประวัติการจ่ายเงิน
                        </div>
                    </EmptyDataTemplate>
                </asp:GridView>
            </div>
        </asp:Panel>
    </div>
</asp:Content>
