<%@ Page Title="Loyalty Dashboard" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="LoyaltyDashboard.aspx.cs" Inherits="Take_Time_BangPhra.Admin.CRM.LoyaltyDashboard" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .loyalty-dashboard-container {
            max-width: 1600px;
            margin: 20px auto;
            padding: 20px;
        }

        .page-header {
            background: linear-gradient(135deg, #f39c12 0%, #e74c3c 100%);
            color: white;
            padding: 30px;
            border-radius: 12px;
            margin-bottom: 30px;
            box-shadow: 0 4px 6px rgba(0,0,0,0.1);
        }

        .page-header h1 {
            margin: 0 0 10px 0;
            font-size: 32px;
            font-weight: bold;
        }

        .stats-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
            gap: 20px;
            margin-bottom: 30px;
        }

        .stat-card {
            background: white;
            padding: 25px;
            border-radius: 12px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
            border-left: 5px solid #f39c12;
            transition: all 0.3s;
        }

        .stat-card:hover {
            transform: translateY(-5px);
            box-shadow: 0 4px 12px rgba(0,0,0,0.15);
        }

        .stat-card h3 {
            margin: 0 0 10px 0;
            color: #666;
            font-size: 14px;
            text-transform: uppercase;
            letter-spacing: 0.5px;
        }

        .stat-card .value {
            font-size: 36px;
            font-weight: bold;
            color: #f39c12;
        }

        .stat-card .change {
            font-size: 14px;
            margin-top: 8px;
        }

        .change.positive {
            color: #28a745;
        }

        .change.negative {
            color: #dc3545;
        }

        .content-grid {
            display: grid;
            grid-template-columns: 2fr 1fr;
            gap: 20px;
            margin-bottom: 20px;
        }

        @media (max-width: 1200px) {
            .content-grid {
                grid-template-columns: 1fr;
            }
        }

        .dashboard-card {
            background: white;
            border-radius: 12px;
            padding: 25px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
        }

        .dashboard-card h3 {
            margin: 0 0 20px 0;
            color: #333;
            font-size: 20px;
            border-bottom: 2px solid #f39c12;
            padding-bottom: 10px;
            display: flex;
            align-items: center;
            justify-content: space-between;
        }

        .tier-breakdown {
            margin-bottom: 30px;
        }

        .tier-item {
            display: flex;
            align-items: center;
            padding: 15px;
            margin-bottom: 15px;
            background: #f8f9fa;
            border-radius: 8px;
            transition: all 0.3s;
        }

        .tier-item:hover {
            background: #e9ecef;
            transform: translateX(5px);
        }

        .tier-badge-large {
            width: 60px;
            height: 60px;
            border-radius: 50%;
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 24px;
            color: white;
            font-weight: bold;
            margin-right: 20px;
        }

        .tier-info {
            flex: 1;
        }

        .tier-name {
            font-weight: 600;
            font-size: 18px;
            color: #333;
            margin-bottom: 5px;
        }

        .tier-stats {
            font-size: 14px;
            color: #666;
        }

        .tier-count {
            font-size: 28px;
            font-weight: bold;
            color: #f39c12;
        }

        .progress-bar {
            width: 100%;
            height: 20px;
            background: #e9ecef;
            border-radius: 10px;
            overflow: hidden;
            margin-top: 10px;
        }

        .progress-fill {
            height: 100%;
            background: linear-gradient(90deg, #f39c12 0%, #e74c3c 100%);
            transition: width 0.5s ease;
        }

        .rewards-grid {
            display: grid;
            gap: 15px;
        }

        .reward-item {
            padding: 15px;
            background: #f8f9fa;
            border-radius: 8px;
            border-left: 4px solid #f39c12;
            display: flex;
            justify-content: space-between;
            align-items: center;
        }

        .reward-info h4 {
            margin: 0 0 5px 0;
            color: #333;
        }

        .reward-info p {
            margin: 0;
            font-size: 14px;
            color: #666;
        }

        .reward-points {
            font-size: 24px;
            font-weight: bold;
            color: #f39c12;
        }

        .reward-actions {
            display: flex;
            gap: 10px;
        }

        .btn-icon {
            background: none;
            border: none;
            font-size: 18px;
            cursor: pointer;
            color: #666;
            transition: all 0.3s;
        }

        .btn-icon:hover {
            color: #f39c12;
        }

        .btn-primary {
            background: linear-gradient(135deg, #f39c12 0%, #e74c3c 100%);
            color: white;
            padding: 12px 30px;
            border: none;
            border-radius: 6px;
            font-size: 16px;
            font-weight: 500;
            cursor: pointer;
            transition: all 0.3s;
        }

        .btn-primary:hover {
            transform: translateY(-2px);
            box-shadow: 0 4px 12px rgba(243, 156, 18, 0.3);
        }

        .btn-secondary {
            background: #6c757d;
            color: white;
            padding: 12px 30px;
            border: none;
            border-radius: 6px;
            font-size: 16px;
            font-weight: 500;
            cursor: pointer;
            margin-left: 10px;
        }

        .tab-container {
            background: white;
            border-radius: 12px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
            overflow: hidden;
            margin-bottom: 30px;
        }

        .tab-header {
            display: flex;
            background: #f8f9fa;
            border-bottom: 2px solid #e9ecef;
        }

        .tab-button {
            flex: 1;
            padding: 20px;
            background: none;
            border: none;
            cursor: pointer;
            font-size: 16px;
            font-weight: 500;
            color: #666;
            transition: all 0.3s;
            border-bottom: 3px solid transparent;
        }

        .tab-button:hover {
            background: #fff;
            color: #f39c12;
        }

        .tab-button.active {
            color: #f39c12;
            background: white;
            border-bottom-color: #f39c12;
        }

        .tab-content {
            padding: 30px;
        }

        .data-table {
            width: 100%;
            border-collapse: collapse;
            margin-top: 20px;
        }

        .data-table th {
            background: #f39c12;
            color: white;
            padding: 15px;
            text-align: left;
            font-weight: 600;
            font-size: 14px;
            text-transform: uppercase;
        }

        .data-table td {
            padding: 15px;
            border-bottom: 1px solid #e9ecef;
        }

        .data-table tr:hover {
            background: #f8f9fa;
        }

        .form-modal {
            position: fixed;
            top: 0;
            left: 0;
            width: 100%;
            height: 100%;
            background: rgba(0,0,0,0.5);
            display: none;
            align-items: center;
            justify-content: center;
            z-index: 1000;
        }

        .form-modal.show {
            display: flex;
        }

        .modal-content {
            background: white;
            padding: 30px;
            border-radius: 12px;
            max-width: 600px;
            width: 90%;
            max-height: 90vh;
            overflow-y: auto;
        }

        .modal-header {
            margin-bottom: 20px;
            padding-bottom: 15px;
            border-bottom: 2px solid #f39c12;
        }

        .modal-header h3 {
            margin: 0;
            color: #333;
        }

        .form-row {
            margin-bottom: 20px;
        }

        .form-row label {
            display: block;
            font-weight: 600;
            margin-bottom: 8px;
            color: #555;
        }

        .form-control {
            width: 100%;
            padding: 12px;
            border: 1px solid #ddd;
            border-radius: 6px;
            font-size: 14px;
        }

        .alert {
            padding: 15px 20px;
            border-radius: 8px;
            margin-bottom: 20px;
        }

        .alert-success {
            background: #d4edda;
            border: 1px solid #c3e6cb;
            color: #155724;
        }

        .alert-error {
            background: #f8d7da;
            border: 1px solid #f5c6cb;
            color: #721c24;
        }

        .transaction-item {
            padding: 15px;
            background: #f8f9fa;
            border-radius: 8px;
            margin-bottom: 10px;
            border-left: 4px solid;
        }

        .transaction-item.earn {
            border-left-color: #28a745;
        }

        .transaction-item.redeem {
            border-left-color: #dc3545;
        }

        .transaction-item.expire {
            border-left-color: #6c757d;
        }

        .transaction-header {
            display: flex;
            justify-content: space-between;
            margin-bottom: 8px;
        }

        .transaction-type {
            font-weight: 600;
            color: #333;
        }

        .transaction-points {
            font-weight: 600;
            font-size: 18px;
        }

        .transaction-points.positive {
            color: #28a745;
        }

        .transaction-points.negative {
            color: #dc3545;
        }

        .transaction-desc {
            font-size: 14px;
            color: #666;
            margin-bottom: 5px;
        }

        .transaction-date {
            font-size: 12px;
            color: #999;
        }

        .empty-state {
            text-align: center;
            padding: 60px 20px;
            color: #999;
        }

        .empty-state i {
            font-size: 64px;
            margin-bottom: 20px;
            opacity: 0.5;
        }
    </style>

    <div class="loyalty-dashboard-container">
        <!-- Page Header -->
        <div class="page-header">
            <h1><i class="fa fa-trophy"></i> Loyalty Dashboard</h1>
            <p>แดชบอร์ดโปรแกรมความภักดี - Manage loyalty program, rewards, and tier performance</p>
        </div>

        <!-- Alert Panel -->
        <asp:Panel ID="pnlAlert" runat="server" Visible="false">
            <div class="alert" id="alertBox" runat="server">
                <asp:Label ID="lblAlertMessage" runat="server"></asp:Label>
            </div>
        </asp:Panel>

        <!-- Statistics Grid -->
        <div class="stats-grid">
            <div class="stat-card">
                <h3>Total Members</h3>
                <div class="value"><asp:Label ID="lblTotalMembers" runat="server">0</asp:Label></div>
                <div class="change positive">
                    <i class="fa fa-arrow-up"></i> <asp:Label ID="lblMembersChange" runat="server">0</asp:Label> this month
                </div>
            </div>
            <div class="stat-card">
                <h3>Active Members</h3>
                <div class="value"><asp:Label ID="lblActiveMembers" runat="server">0</asp:Label></div>
                <div class="change">
                    <asp:Label ID="lblActivePercent" runat="server">0</asp:Label>% of total
                </div>
            </div>
            <div class="stat-card">
                <h3>Points Earned (Month)</h3>
                <div class="value"><asp:Label ID="lblPointsEarned" runat="server">0</asp:Label></div>
                <div class="change positive">
                    <i class="fa fa-arrow-up"></i> <asp:Label ID="lblPointsEarnedChange" runat="server">0</asp:Label>%
                </div>
            </div>
            <div class="stat-card">
                <h3>Points Redeemed (Month)</h3>
                <div class="value"><asp:Label ID="lblPointsRedeemed" runat="server">0</asp:Label></div>
                <div class="change">
                    <asp:Label ID="lblRedemptionRate" runat="server">0</asp:Label>% redemption rate
                </div>
            </div>
        </div>

        <!-- Content Grid -->
        <div class="content-grid">
            <!-- Left Column: Tier Breakdown -->
            <div class="dashboard-card">
                <h3>
                    <span><i class="fa fa-layer-group"></i> Member Distribution by Tier</span>
                    <span style="font-size: 14px; font-weight: normal; color: #666;">
                        Total: <asp:Label ID="lblTotalMembersBreakdown" runat="server">0</asp:Label>
                    </span>
                </h3>
                <div class="tier-breakdown">
                    <asp:Repeater ID="rptTierBreakdown" runat="server">
                        <ItemTemplate>
                            <div class="tier-item">
                                <div class="tier-badge-large" style='background: <%# Eval("TierColor") %>'>
                                    <%# GetTierIcon(Eval("TierNameEN")) %>
                                </div>
                                <div class="tier-info">
                                    <div class="tier-name"><%# Eval("TierName") %> (<%# Eval("TierNameEN") %>)</div>
                                    <div class="tier-stats">
                                        Min Points: <%# Eval("MinPoints") %> |
                                        Multiplier: <%# Eval("PointsMultiplier") %>x |
                                        Discount: <%# Eval("AccommodationDiscountPercent") %>%
                                    </div>
                                    <div class="progress-bar">
                                        <div class="progress-fill" style='width: <%# Eval("MemberPercent") %>%'></div>
                                    </div>
                                    <div style="margin-top: 5px; font-size: 14px; color: #666;">
                                        <%# Eval("MemberCount") %> members (<%# Eval("MemberPercent", "{0:N1}") %>%)
                                    </div>
                                </div>
                                <div class="tier-count">
                                    <%# Eval("MemberCount") %>
                                </div>
                            </div>
                        </ItemTemplate>
                    </asp:Repeater>
                </div>
            </div>

            <!-- Right Column: Recent Transactions -->
            <div class="dashboard-card">
                <h3><i class="fa fa-exchange"></i> Recent Transactions</h3>
                <div style="max-height: 600px; overflow-y: auto;">
                    <asp:Repeater ID="rptRecentTransactions" runat="server">
                        <ItemTemplate>
                            <div class="transaction-item <%# Eval("TransactionType").ToString().ToLower() %>">
                                <div class="transaction-header">
                                    <span class="transaction-type"><%# FormatTransactionType(Eval("TransactionType")) %></span>
                                    <span class="transaction-points <%# GetTransactionClass(Eval("TransactionType")) %>">
                                        <%# GetTransactionSign(Eval("TransactionType")) %><%# Eval("Points") %>
                                    </span>
                                </div>
                                <div class="transaction-desc">
                                    <%# Eval("Description") %>
                                </div>
                                <div class="transaction-date">
                                    Customer: <%# Eval("Customer_MobilePhone") %> |
                                    <%# Eval("TransactionDate", "{0:dd MMM yyyy HH:mm}") %>
                                </div>
                            </div>
                        </ItemTemplate>
                    </asp:Repeater>
                    <asp:Label ID="lblNoTransactions" runat="server" CssClass="empty-state" Visible="false">
                        <i class="fa fa-exchange"></i><br />No recent transactions
                    </asp:Label>
                </div>
            </div>
        </div>

        <!-- Tabs for Rewards and Reports -->
        <div class="tab-container">
            <div class="tab-header">
                <asp:Button ID="btnTabRewards" runat="server" Text="Rewards Management" CssClass="tab-button active" OnClick="btnTabRewards_Click" />
                <asp:Button ID="btnTabReports" runat="server" Text="Performance Reports" CssClass="tab-button" OnClick="btnTabReports_Click" />
            </div>

            <div class="tab-content">
                <!-- Tab 1: Rewards Management -->
                <asp:Panel ID="pnlRewards" runat="server" Visible="true">
                    <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px;">
                        <h3 style="margin: 0;">Rewards Catalog</h3>
                        <asp:Button ID="btnAddReward" runat="server" Text="Add New Reward" CssClass="btn-primary" OnClick="btnAddReward_Click" />
                    </div>

                    <div class="rewards-grid">
                        <asp:Repeater ID="rptRewards" runat="server" OnItemCommand="rptRewards_ItemCommand">
                            <ItemTemplate>
                                <div class="reward-item">
                                    <div class="reward-info">
                                        <h4><%# Eval("RewardName") %></h4>
                                        <p><%# Eval("Description") %></p>
                                        <p style="font-size: 12px; color: #999;">
                                            Category: <%# Eval("CategoryName") %> |
                                            Stock: <%# Eval("AvailableQuantity") != DBNull.Value ? Eval("AvailableQuantity").ToString() : "Unlimited" %> |
                                            Valid: <%# Eval("ValidFrom", "{0:dd MMM yyyy}") %> - <%# Eval("ValidTo", "{0:dd MMM yyyy}") %>
                                        </p>
                                    </div>
                                    <div style="text-align: center;">
                                        <div class="reward-points"><%# Eval("PointsRequired") %></div>
                                        <div style="font-size: 12px; color: #666;">points</div>
                                        <div class="reward-actions" style="margin-top: 10px;">
                                            <asp:LinkButton ID="btnEdit" runat="server" CommandName="EditReward" CommandArgument='<%# Eval("ID") %>'
                                                CssClass="btn-icon" ToolTip="Edit">
                                                <i class="fa fa-edit"></i>
                                            </asp:LinkButton>
                                            <asp:LinkButton ID="btnToggle" runat="server" CommandName="ToggleStatus" CommandArgument='<%# Eval("ID") %>'
                                                CssClass="btn-icon" ToolTip='<%# (bool)Eval("IsActive") ? "Deactivate" : "Activate" %>'>
                                                <i class='fa fa-<%# (bool)Eval("IsActive") ? "toggle-on" : "toggle-off" %>'></i>
                                            </asp:LinkButton>
                                        </div>
                                    </div>
                                </div>
                            </ItemTemplate>
                        </asp:Repeater>
                        <asp:Label ID="lblNoRewards" runat="server" CssClass="empty-state" Visible="false">
                            <i class="fa fa-gift"></i><br />No rewards available
                        </asp:Label>
                    </div>
                </asp:Panel>

                <!-- Tab 2: Performance Reports -->
                <asp:Panel ID="pnlReports" runat="server" Visible="false">
                    <h3>Tier Performance Metrics</h3>
                    <asp:GridView ID="gvTierPerformance" runat="server" CssClass="data-table" AutoGenerateColumns="False">
                        <Columns>
                            <asp:BoundField DataField="TierName" HeaderText="Tier" />
                            <asp:BoundField DataField="MemberCount" HeaderText="Members" DataFormatString="{0:N0}" />
                            <asp:BoundField DataField="TotalPointsEarned" HeaderText="Points Earned" DataFormatString="{0:N0}" />
                            <asp:BoundField DataField="TotalPointsRedeemed" HeaderText="Points Redeemed" DataFormatString="{0:N0}" />
                            <asp:BoundField DataField="AvgPointsBalance" HeaderText="Avg Balance" DataFormatString="{0:N0}" />
                            <asp:BoundField DataField="TotalRevenue" HeaderText="Revenue" DataFormatString="฿{0:N2}" />
                            <asp:BoundField DataField="AvgRevenuePerMember" HeaderText="Avg Revenue/Member" DataFormatString="฿{0:N2}" />
                        </Columns>
                    </asp:GridView>

                    <h3 style="margin-top: 40px;">Monthly Trends</h3>
                    <asp:GridView ID="gvMonthlyTrends" runat="server" CssClass="data-table" AutoGenerateColumns="False">
                        <Columns>
                            <asp:BoundField DataField="Month" HeaderText="Month" DataFormatString="{0:MMM yyyy}" />
                            <asp:BoundField DataField="NewMembers" HeaderText="New Members" />
                            <asp:BoundField DataField="PointsEarned" HeaderText="Points Earned" DataFormatString="{0:N0}" />
                            <asp:BoundField DataField="PointsRedeemed" HeaderText="Points Redeemed" DataFormatString="{0:N0}" />
                            <asp:BoundField DataField="RedemptionRate" HeaderText="Redemption Rate" DataFormatString="{0:N1}%" />
                        </Columns>
                    </asp:GridView>
                </asp:Panel>
            </div>
        </div>

        <!-- Reward Form Modal -->
        <asp:Panel ID="pnlRewardModal" runat="server" CssClass="form-modal">
            <div class="modal-content">
                <div class="modal-header">
                    <h3><asp:Label ID="lblModalTitle" runat="server" Text="Add New Reward"></asp:Label></h3>
                </div>
                <div class="form-row">
                    <label>Reward Name (TH)</label>
                    <asp:TextBox ID="txtRewardName" runat="server" CssClass="form-control"></asp:TextBox>
                </div>
                <div class="form-row">
                    <label>Reward Name (EN)</label>
                    <asp:TextBox ID="txtRewardNameEN" runat="server" CssClass="form-control"></asp:TextBox>
                </div>
                <div class="form-row">
                    <label>Description</label>
                    <asp:TextBox ID="txtRewardDescription" runat="server" CssClass="form-control" TextMode="MultiLine" Rows="3"></asp:TextBox>
                </div>
                <div class="form-row">
                    <label>Category</label>
                    <asp:DropDownList ID="ddlRewardCategory" runat="server" CssClass="form-control"></asp:DropDownList>
                </div>
                <div class="form-row">
                    <label>Points Required</label>
                    <asp:TextBox ID="txtPointsRequired" runat="server" CssClass="form-control" TextMode="Number"></asp:TextBox>
                </div>
                <div class="form-row">
                    <label>Available Quantity (leave empty for unlimited)</label>
                    <asp:TextBox ID="txtAvailableQuantity" runat="server" CssClass="form-control" TextMode="Number"></asp:TextBox>
                </div>
                <div class="form-row">
                    <label>Valid From</label>
                    <asp:TextBox ID="txtValidFrom" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                </div>
                <div class="form-row">
                    <label>Valid To</label>
                    <asp:TextBox ID="txtValidTo" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                </div>
                <div style="text-align: right; margin-top: 20px;">
                    <asp:Button ID="btnSaveReward" runat="server" Text="Save Reward" CssClass="btn-primary" OnClick="btnSaveReward_Click" />
                    <asp:Button ID="btnCancelReward" runat="server" Text="Cancel" CssClass="btn-secondary" OnClick="btnCancelReward_Click" />
                    <asp:HiddenField ID="hfEditingRewardId" runat="server" Value="0" />
                </div>
            </div>
        </asp:Panel>
    </div>
</asp:Content>
