<%@ Page Title="Tier Benefits Management" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="TierBenefitsManagement.aspx.cs" Inherits="Take_Time_BangPhra.Account.TierBenefitsManagement" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .tier-benefits-container {
            max-width: 1400px;
            margin: 20px auto;
            padding: 20px;
        }

        .page-header {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
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

        .page-header p {
            margin: 0;
            opacity: 0.9;
            font-size: 16px;
        }

        .stats-row {
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
            border-left: 5px solid #667eea;
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
            color: #667eea;
        }

        .tab-container {
            background: white;
            border-radius: 12px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
            overflow: hidden;
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
            color: #667eea;
        }

        .tab-button.active {
            color: #667eea;
            background: white;
            border-bottom-color: #667eea;
        }

        .tab-content {
            padding: 30px;
        }

        .tier-card {
            background: white;
            border-radius: 12px;
            padding: 25px;
            margin-bottom: 20px;
            border: 2px solid #e9ecef;
            transition: all 0.3s;
        }

        .tier-card:hover {
            border-color: #667eea;
            box-shadow: 0 4px 12px rgba(102, 126, 234, 0.15);
        }

        .tier-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 20px;
            padding-bottom: 15px;
            border-bottom: 2px solid #f8f9fa;
        }

        .tier-name {
            font-size: 24px;
            font-weight: bold;
            display: flex;
            align-items: center;
            gap: 10px;
        }

        .tier-badge {
            padding: 8px 16px;
            border-radius: 20px;
            font-size: 14px;
            font-weight: 500;
            color: white;
        }

        .benefits-grid {
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
            gap: 15px;
            margin-top: 15px;
        }

        .benefit-item {
            padding: 15px;
            background: #f8f9fa;
            border-radius: 8px;
            border-left: 4px solid #667eea;
        }

        .benefit-name {
            font-weight: 600;
            color: #333;
            margin-bottom: 5px;
        }

        .benefit-desc {
            font-size: 14px;
            color: #666;
        }

        .benefit-value {
            font-size: 18px;
            font-weight: bold;
            color: #667eea;
            margin-top: 8px;
        }

        .form-section {
            background: #f8f9fa;
            padding: 25px;
            border-radius: 12px;
            margin-bottom: 20px;
        }

        .form-section h3 {
            margin: 0 0 20px 0;
            color: #333;
            font-size: 20px;
        }

        .form-row {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
            gap: 20px;
            margin-bottom: 20px;
        }

        .form-group {
            display: flex;
            flex-direction: column;
        }

        .form-group label {
            font-weight: 600;
            margin-bottom: 8px;
            color: #555;
        }

        .form-control {
            padding: 12px;
            border: 1px solid #ddd;
            border-radius: 6px;
            font-size: 14px;
        }

        .btn-primary {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
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
            box-shadow: 0 4px 12px rgba(102, 126, 234, 0.3);
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
            transition: all 0.3s;
            margin-left: 10px;
        }

        .data-grid {
            width: 100%;
            border-collapse: separate;
            border-spacing: 0;
            margin-top: 20px;
        }

        .data-grid th {
            background: #667eea;
            color: white;
            padding: 15px;
            text-align: left;
            font-weight: 600;
            font-size: 14px;
            text-transform: uppercase;
            letter-spacing: 0.5px;
        }

        .data-grid th:first-child {
            border-top-left-radius: 8px;
        }

        .data-grid th:last-child {
            border-top-right-radius: 8px;
        }

        .data-grid td {
            padding: 15px;
            border-bottom: 1px solid #e9ecef;
            background: white;
        }

        .data-grid tr:hover td {
            background: #f8f9fa;
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

        .alert-info {
            background: #d1ecf1;
            border: 1px solid #bee5eb;
            color: #0c5460;
        }
    </style>

    <div class="tier-benefits-container">
        <!-- Page Header -->
        <div class="page-header">
            <h1><i class="fa fa-trophy"></i> Tier Benefits Management</h1>
            <p>จัดการสิทธิประโยชน์สมาชิกและส่วนลดตามระดับ - Manage membership benefits and tier-based discounts</p>
        </div>

        <!-- Alert Panel -->
        <asp:Panel ID="pnlAlert" runat="server" Visible="false">
            <div class="alert" id="alertBox" runat="server">
                <asp:Label ID="lblAlertMessage" runat="server"></asp:Label>
            </div>
        </asp:Panel>

        <!-- Statistics Row -->
        <div class="stats-row">
            <div class="stat-card">
                <h3>Total Members</h3>
                <div class="value"><asp:Label ID="lblTotalMembers" runat="server" Text="0"></asp:Label></div>
            </div>
            <div class="stat-card">
                <h3>Active Benefits</h3>
                <div class="value"><asp:Label ID="lblActiveBenefits" runat="server" Text="0"></asp:Label></div>
            </div>
            <div class="stat-card">
                <h3>Category Discounts</h3>
                <div class="value"><asp:Label ID="lblCategoryDiscounts" runat="server" Text="0"></asp:Label></div>
            </div>
            <div class="stat-card">
                <h3>Total Discounts Given</h3>
                <div class="value">฿<asp:Label ID="lblTotalDiscounts" runat="server" Text="0"></asp:Label></div>
            </div>
        </div>

        <!-- Tab Container -->
        <div class="tab-container">
            <div class="tab-header">
                <asp:Button ID="btnTabTiers" runat="server" Text="Loyalty Tiers & Benefits" CssClass="tab-button active" OnClick="btnTabTiers_Click" />
                <asp:Button ID="btnTabCategoryDiscounts" runat="server" Text="Product Category Discounts" CssClass="tab-button" OnClick="btnTabCategoryDiscounts_Click" />
                <asp:Button ID="btnTabUsageStatistics" runat="server" Text="Usage Statistics" CssClass="tab-button" OnClick="btnTabUsageStatistics_Click" />
            </div>

            <div class="tab-content">
                <!-- Tab 1: Tiers & Benefits -->
                <asp:Panel ID="pnlTiersBenefits" runat="server" Visible="true">
                    <h2 style="margin-bottom: 20px;">Loyalty Tiers & Their Benefits</h2>
                    <asp:Repeater ID="rptTiers" runat="server" OnItemDataBound="rptTiers_ItemDataBound">
                        <ItemTemplate>
                            <div class="tier-card">
                                <div class="tier-header">
                                    <div class="tier-name">
                                        <span class="tier-badge" style='background-color: <%# Eval("TierColor") %>'>
                                            <%# Eval("TierName") %>
                                        </span>
                                        <span><%# Eval("TierNameEN") %></span>
                                    </div>
                                    <div>
                                        <strong>Min Points:</strong> <%# Eval("MinPoints") %> |
                                        <strong>Multiplier:</strong> <%# Eval("PointsMultiplier") %>x |
                                        <strong>Discount:</strong> <%# Eval("AccommodationDiscountPercent") %>%
                                    </div>
                                </div>
                                <div class="benefits-grid">
                                    <asp:Repeater ID="rptBenefits" runat="server">
                                        <ItemTemplate>
                                            <div class="benefit-item">
                                                <div class="benefit-name">
                                                    <i class="fa fa-<%# Eval("Icon") %>"></i>
                                                    <%# Eval("BenefitName") %>
                                                </div>
                                                <div class="benefit-desc"><%# Eval("Description") %></div>
                                                <div class="benefit-value">
                                                    <%# GetBenefitDisplayValue(Eval("DiscountType"), Eval("DiscountValue"), Eval("MaxDiscountAmount"), Eval("TimeValue"), Eval("QuantityValue")) %>
                                                </div>
                                            </div>
                                        </ItemTemplate>
                                    </asp:Repeater>
                                </div>
                            </div>
                        </ItemTemplate>
                    </asp:Repeater>
                </asp:Panel>

                <!-- Tab 2: Product Category Discounts -->
                <asp:Panel ID="pnlCategoryDiscounts" runat="server" Visible="false">
                    <h2 style="margin-bottom: 20px;">Manage Product Category Discounts</h2>

                    <!-- Add/Edit Form -->
                    <div class="form-section">
                        <h3>Add/Edit Category Discount</h3>
                        <div class="form-row">
                            <div class="form-group">
                                <label>Loyalty Tier</label>
                                <asp:DropDownList ID="ddlTier" runat="server" CssClass="form-control"></asp:DropDownList>
                            </div>
                            <div class="form-group">
                                <label>Product Category</label>
                                <asp:DropDownList ID="ddlProductCategory" runat="server" CssClass="form-control"></asp:DropDownList>
                            </div>
                            <div class="form-group">
                                <label>Discount Percent (%)</label>
                                <asp:TextBox ID="txtDiscountPercent" runat="server" CssClass="form-control" TextMode="Number" step="0.01"></asp:TextBox>
                            </div>
                        </div>
                        <div class="form-row">
                            <div class="form-group">
                                <label>Max Discount Amount (฿)</label>
                                <asp:TextBox ID="txtMaxDiscount" runat="server" CssClass="form-control" TextMode="Number" step="0.01"></asp:TextBox>
                            </div>
                            <div class="form-group">
                                <label>Min Purchase Amount (฿)</label>
                                <asp:TextBox ID="txtMinPurchase" runat="server" CssClass="form-control" TextMode="Number" step="0.01"></asp:TextBox>
                            </div>
                            <div class="form-group">
                                <label>Valid From</label>
                                <asp:TextBox ID="txtValidFrom" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                            </div>
                            <div class="form-group">
                                <label>Valid To</label>
                                <asp:TextBox ID="txtValidTo" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                            </div>
                        </div>
                        <div>
                            <asp:Button ID="btnSaveCategoryDiscount" runat="server" Text="Save Discount" CssClass="btn-primary" OnClick="btnSaveCategoryDiscount_Click" />
                            <asp:Button ID="btnCancelEdit" runat="server" Text="Cancel" CssClass="btn-secondary" OnClick="btnCancelEdit_Click" />
                            <asp:HiddenField ID="hfEditingId" runat="server" Value="0" />
                        </div>
                    </div>

                    <!-- Existing Discounts -->
                    <h3>Current Category Discounts</h3>
                    <asp:GridView ID="gvCategoryDiscounts" runat="server" CssClass="data-grid" AutoGenerateColumns="False"
                        OnRowCommand="gvCategoryDiscounts_RowCommand" DataKeyNames="ID">
                        <Columns>
                            <asp:BoundField DataField="TierName" HeaderText="Tier" />
                            <asp:BoundField DataField="CategoryName" HeaderText="Category" />
                            <asp:BoundField DataField="DiscountPercent" HeaderText="Discount %" DataFormatString="{0:N2}%" />
                            <asp:BoundField DataField="MaxDiscountAmount" HeaderText="Max Discount" DataFormatString="฿{0:N2}" />
                            <asp:BoundField DataField="MinPurchaseAmount" HeaderText="Min Purchase" DataFormatString="฿{0:N2}" />
                            <asp:TemplateField HeaderText="Status">
                                <ItemTemplate>
                                    <span style='color: <%# (bool)Eval("IsActive") ? "green" : "red" %>'>
                                        <%# (bool)Eval("IsActive") ? "Active" : "Inactive" %>
                                    </span>
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="Actions">
                                <ItemTemplate>
                                    <asp:LinkButton ID="btnEdit" runat="server" CommandName="EditDiscount" CommandArgument='<%# Eval("ID") %>'
                                        Text="Edit" style="color: #667eea; margin-right: 10px;"></asp:LinkButton>
                                    <asp:LinkButton ID="btnToggle" runat="server" CommandName="ToggleStatus" CommandArgument='<%# Eval("ID") %>'
                                        Text='<%# (bool)Eval("IsActive") ? "Deactivate" : "Activate" %>' style="color: #dc3545;"></asp:LinkButton>
                                </ItemTemplate>
                            </asp:TemplateField>
                        </Columns>
                    </asp:GridView>
                </asp:Panel>

                <!-- Tab 3: Usage Statistics -->
                <asp:Panel ID="pnlUsageStatistics" runat="server" Visible="false">
                    <h2 style="margin-bottom: 20px;">Benefit Usage Statistics</h2>

                    <!-- Date Range Filter -->
                    <div class="form-section">
                        <div class="form-row">
                            <div class="form-group">
                                <label>From Date</label>
                                <asp:TextBox ID="txtStatsFromDate" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                            </div>
                            <div class="form-group">
                                <label>To Date</label>
                                <asp:TextBox ID="txtStatsToDate" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                            </div>
                            <div class="form-group" style="align-self: flex-end;">
                                <asp:Button ID="btnLoadStats" runat="server" Text="Load Statistics" CssClass="btn-primary" OnClick="btnLoadStats_Click" />
                            </div>
                        </div>
                    </div>

                    <!-- Usage Grid -->
                    <asp:GridView ID="gvUsageStatistics" runat="server" CssClass="data-grid" AutoGenerateColumns="False">
                        <Columns>
                            <asp:BoundField DataField="BenefitType" HeaderText="Benefit Type" />
                            <asp:BoundField DataField="UsageCount" HeaderText="Times Used" />
                            <asp:BoundField DataField="TotalDiscountAmount" HeaderText="Total Discount" DataFormatString="฿{0:N2}" />
                            <asp:BoundField DataField="AvgDiscountAmount" HeaderText="Avg Discount" DataFormatString="฿{0:N2}" />
                            <asp:BoundField DataField="UniqueCustomers" HeaderText="Unique Customers" />
                        </Columns>
                    </asp:GridView>
                </asp:Panel>
            </div>
        </div>
    </div>
</asp:Content>
