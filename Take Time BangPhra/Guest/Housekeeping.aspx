<%@ Page Title="Housekeeping" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Housekeeping.aspx.cs" Inherits="Take_Time_BangPhra.Guest.Housekeeping" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .hk-header {
            background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%);
            color: white;
            padding: 25px;
            border-radius: 12px;
            margin-bottom: 25px;
            display: flex;
            justify-content: space-between;
            align-items: center;
        }

        .hk-header h2 {
            margin: 0;
            font-size: 26px;
            font-weight: 700;
        }

        .btn-back {
            background: rgba(255,255,255,0.2);
            color: white;
            border: none;
            padding: 10px 20px;
            border-radius: 20px;
            text-decoration: none;
            font-weight: 500;
            transition: all 0.3s ease;
        }

        .btn-back:hover {
            background: rgba(255,255,255,0.3);
            color: white;
            text-decoration: none;
        }

        .tabs {
            display: flex;
            gap: 10px;
            margin-bottom: 25px;
            border-bottom: 2px solid #e0e0e0;
        }

        .tab-btn {
            background: none;
            border: none;
            padding: 12px 25px;
            font-size: 16px;
            font-weight: 600;
            color: #999;
            cursor: pointer;
            border-bottom: 3px solid transparent;
            transition: all 0.3s ease;
        }

        .tab-btn.active {
            color: #f5576c;
            border-bottom-color: #f5576c;
        }

        .tab-content {
            display: none;
        }

        .tab-content.active {
            display: block;
        }

        .request-form {
            background: white;
            padding: 30px;
            border-radius: 12px;
            box-shadow: 0 5px 20px rgba(0,0,0,0.1);
            max-width: 700px;
            margin: 0 auto;
        }

        .form-group {
            margin-bottom: 25px;
        }

        .form-label {
            display: block;
            margin-bottom: 8px;
            font-weight: 600;
            color: #333;
            font-size: 14px;
        }

        .form-control {
            width: 100%;
            padding: 12px 15px;
            border: 2px solid #e0e0e0;
            border-radius: 8px;
            font-size: 14px;
            box-sizing: border-box;
            transition: all 0.3s ease;
        }

        .form-control:focus {
            outline: none;
            border-color: #f5576c;
            box-shadow: 0 0 0 3px rgba(245, 87, 108, 0.1);
        }

        .service-types {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
            gap: 15px;
        }

        .service-type-card {
            border: 2px solid #e0e0e0;
            border-radius: 10px;
            padding: 20px;
            text-align: center;
            cursor: pointer;
            transition: all 0.3s ease;
        }

        .service-type-card:hover {
            border-color: #f5576c;
            background: #fff5f7;
        }

        .service-type-card.selected {
            border-color: #f5576c;
            background: linear-gradient(135deg, #fff5f7, #ffe8eb);
        }

        .service-type-card i {
            font-size: 36px;
            color: #f5576c;
            margin-bottom: 10px;
        }

        .service-type-card label {
            display: block;
            font-weight: 600;
            cursor: pointer;
        }

        .priority-options {
            display: flex;
            gap: 10px;
        }

        .priority-option {
            flex: 1;
            padding: 12px;
            border: 2px solid #e0e0e0;
            border-radius: 8px;
            text-align: center;
            cursor: pointer;
            transition: all 0.3s ease;
        }

        .priority-option:hover {
            border-color: #f5576c;
        }

        .priority-option.selected {
            border-color: #f5576c;
            background: #fff5f7;
            font-weight: 600;
        }

        .priority-LOW { border-color: #4CAF50; }
        .priority-LOW.selected { background: #e8f5e9; border-color: #4CAF50; color: #2e7d32; }

        .priority-NORMAL { border-color: #2196F3; }
        .priority-NORMAL.selected { background: #e3f2fd; border-color: #2196F3; color: #1565c0; }

        .priority-HIGH { border-color: #FF9800; }
        .priority-HIGH.selected { background: #fff3e0; border-color: #FF9800; color: #e65100; }

        .priority-URGENT { border-color: #f44336; }
        .priority-URGENT.selected { background: #ffebee; border-color: #f44336; color: #c62828; }

        .btn-submit-request {
            width: 100%;
            background: linear-gradient(135deg, #f093fb, #f5576c);
            color: white;
            border: none;
            padding: 15px;
            border-radius: 10px;
            font-size: 18px;
            font-weight: 700;
            cursor: pointer;
            transition: all 0.3s ease;
        }

        .btn-submit-request:hover {
            transform: translateY(-2px);
            box-shadow: 0 6px 20px rgba(245, 87, 108, 0.3);
        }

        .requests-list {
            background: white;
            padding: 25px;
            border-radius: 12px;
            box-shadow: 0 3px 10px rgba(0,0,0,0.1);
        }

        .request-card {
            border: 2px solid #e0e0e0;
            border-radius: 10px;
            padding: 20px;
            margin-bottom: 15px;
            transition: all 0.3s ease;
        }

        .request-card:hover {
            box-shadow: 0 5px 15px rgba(0,0,0,0.1);
        }

        .request-header {
            display: flex;
            justify-content: space-between;
            align-items: start;
            margin-bottom: 15px;
            padding-bottom: 15px;
            border-bottom: 2px solid #f0f0f0;
        }

        .request-number {
            font-weight: 700;
            color: #f5576c;
            font-size: 16px;
        }

        .request-type-badge {
            background: #e3f2fd;
            color: #1565c0;
            padding: 5px 12px;
            border-radius: 15px;
            font-size: 12px;
            font-weight: 600;
            margin-top: 5px;
            display: inline-block;
        }

        .request-status {
            padding: 6px 15px;
            border-radius: 20px;
            font-size: 12px;
            font-weight: 600;
        }

        .status-PENDING { background: #fff3cd; color: #856404; }
        .status-ASSIGNED { background: #d1ecf1; color: #0c5460; }
        .status-IN_PROGRESS { background: #cce5ff; color: #004085; }
        .status-COMPLETED { background: #d4edda; color: #155724; }
        .status-CANCELLED { background: #f8d7da; color: #721c24; }

        .priority-badge {
            padding: 5px 12px;
            border-radius: 12px;
            font-size: 11px;
            font-weight: 600;
            margin-left: 10px;
        }

        .badge-LOW { background: #e8f5e9; color: #2e7d32; }
        .badge-NORMAL { background: #e3f2fd; color: #1565c0; }
        .badge-HIGH { background: #fff3e0; color: #e65100; }
        .badge-URGENT { background: #ffebee; color: #c62828; }

        .request-description {
            background: #f8f9fa;
            padding: 15px;
            border-radius: 8px;
            margin: 10px 0;
            color: #555;
            line-height: 1.6;
        }

        .request-meta {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 15px;
            font-size: 13px;
            color: #666;
        }

        .request-meta div {
            display: flex;
            align-items: center;
            gap: 8px;
        }

        .request-meta i {
            color: #f5576c;
        }

        @media (max-width: 768px) {
            .service-types {
                grid-template-columns: repeat(2, 1fr);
            }

            .priority-options {
                flex-direction: column;
            }
        }
    </style>

    <!-- Header -->
    <div class="hk-header">
        <h2><i class="fas fa-broom"></i> Housekeeping Service</h2>
        <a href="Dashboard.aspx" class="btn-back">
            <i class="fas fa-arrow-left"></i> Back to Dashboard
        </a>
    </div>

    <!-- Tabs -->
    <div class="tabs">
        <button type="button" class="tab-btn active" onclick="switchTab(event, 'new')">
            <i class="fas fa-plus-circle"></i> New Request
        </button>
        <button type="button" class="tab-btn" onclick="switchTab(event, 'history')">
            <i class="fas fa-history"></i> My Requests
        </button>
    </div>

    <!-- Tab: New Request -->
    <div id="new" class="tab-content active">
        <div class="request-form">
            <h3 style="margin-bottom: 25px; color: #333;">
                <i class="fas fa-clipboard-list"></i> Request Housekeeping Service
            </h3>

            <!-- Service Type -->
            <div class="form-group">
                <label class="form-label">Service Type *</label>
                <div class="service-types">
                    <div class="service-type-card" onclick="selectServiceType(this, 'CLEANING')">
                        <i class="fas fa-spray-can"></i>
                        <label>Room Cleaning</label>
                        <asp:RadioButton ID="rbCleaning" runat="server" GroupName="ServiceType" Value="CLEANING" style="display: none;" />
                    </div>
                    <div class="service-type-card" onclick="selectServiceType(this, 'TOWELS')">
                        <i class="fas fa-bath"></i>
                        <label>Towels</label>
                        <asp:RadioButton ID="rbTowels" runat="server" GroupName="ServiceType" Value="TOWELS" style="display: none;" />
                    </div>
                    <div class="service-type-card" onclick="selectServiceType(this, 'TOILETRIES')">
                        <i class="fas fa-pump-soap"></i>
                        <label>Toiletries</label>
                        <asp:RadioButton ID="rbToiletries" runat="server" GroupName="ServiceType" Value="TOILETRIES" style="display: none;" />
                    </div>
                    <div class="service-type-card" onclick="selectServiceType(this, 'MAINTENANCE')">
                        <i class="fas fa-tools"></i>
                        <label>Maintenance</label>
                        <asp:RadioButton ID="rbMaintenance" runat="server" GroupName="ServiceType" Value="MAINTENANCE" style="display: none;" />
                    </div>
                    <div class="service-type-card" onclick="selectServiceType(this, 'OTHER')">
                        <i class="fas fa-ellipsis-h"></i>
                        <label>Other</label>
                        <asp:RadioButton ID="rbOther" runat="server" GroupName="ServiceType" Value="OTHER" style="display: none;" />
                    </div>
                </div>
                <asp:HiddenField ID="hfServiceType" runat="server" />
            </div>

            <!-- Description -->
            <div class="form-group">
                <label class="form-label">Description *</label>
                <asp:TextBox ID="txtDescription" runat="server" CssClass="form-control"
                    TextMode="MultiLine" Rows="4"
                    placeholder="Please describe what you need..."></asp:TextBox>
            </div>

            <!-- Priority -->
            <div class="form-group">
                <label class="form-label">Priority</label>
                <div class="priority-options">
                    <div class="priority-option priority-LOW selected" onclick="selectPriority(this, 'NORMAL')">
                        <div>Normal</div>
                    </div>
                    <div class="priority-option priority-HIGH" onclick="selectPriority(this, 'HIGH')">
                        <div>High</div>
                    </div>
                    <div class="priority-option priority-URGENT" onclick="selectPriority(this, 'URGENT')">
                        <div>Urgent</div>
                    </div>
                </div>
                <asp:HiddenField ID="hfPriority" runat="server" Value="NORMAL" />
            </div>

            <!-- Preferred Time -->
            <div class="form-group">
                <label class="form-label">Preferred Time (Optional)</label>
                <asp:TextBox ID="txtPreferredTime" runat="server" CssClass="form-control"
                    placeholder="e.g., After 2 PM, Morning, etc."></asp:TextBox>
            </div>

            <!-- Submit Button -->
            <asp:Button ID="btnSubmitRequest" runat="server" Text="Submit Request"
                CssClass="btn-submit-request" OnClick="btnSubmitRequest_Click"
                OnClientClick="return validateRequest();" />
        </div>
    </div>

    <!-- Tab: Request History -->
    <div id="history" class="tab-content">
        <div class="requests-list">
            <h3 style="margin-bottom: 20px;">My Requests</h3>

            <asp:Repeater ID="rptRequests" runat="server">
                <ItemTemplate>
                    <div class="request-card">
                        <div class="request-header">
                            <div>
                                <div class="request-number">Request #<%# Eval("Request_Number") %></div>
                                <span class="request-type-badge">
                                    <i class="fas fa-tag"></i> <%# Eval("Request_Type") %>
                                </span>
                                <span class="priority-badge badge-<%# Eval("Priority") %>">
                                    <%# Eval("Priority") %>
                                </span>
                            </div>
                            <span class="request-status status-<%# Eval("Request_Status") %>">
                                <%# Eval("Request_Status") %>
                            </span>
                        </div>

                        <div class="request-description">
                            <i class="fas fa-comment-alt"></i> <%# Eval("Request_Description") %>
                        </div>

                        <div class="request-meta">
                            <div>
                                <i class="fas fa-calendar"></i>
                                <span><%# Eval("Request_Date", "{0:dd MMM yyyy HH:mm}") %></span>
                            </div>
                            <div>
                                <i class="fas fa-clock"></i>
                                <span><%# Eval("Preferred_Time") != DBNull.Value ? Eval("Preferred_Time") : "Anytime" %></span>
                            </div>
                            <div style='<%# Eval("Completed_Date") != DBNull.Value ? "" : "display:none;" %>'>
                                <i class="fas fa-check-circle"></i>
                                <span>Completed: <%# Eval("Completed_Date", "{0:dd MMM yyyy HH:mm}") %></span>
                            </div>
                        </div>

                        <div style='margin-top: 15px; padding-top: 15px; border-top: 1px solid #e0e0e0; color: #666; font-size: 13px; <%# !string.IsNullOrEmpty(Eval("Staff_Notes")?.ToString()) ? "" : "display:none;" %>'>
                            <strong><i class="fas fa-user-tie"></i> Staff Notes:</strong><br />
                            <%# Eval("Staff_Notes") %>
                        </div>
                    </div>
                </ItemTemplate>
            </asp:Repeater>

            <asp:Label ID="lblNoRequests" runat="server" Visible="false"
                Text="<div style='text-align: center; padding: 40px; color: #999;'><i class='fas fa-inbox' style='font-size: 60px; margin-bottom: 15px;'></i><p>No requests yet</p></div>"></asp:Label>
        </div>
    </div>

    <script>
        function selectServiceType(card, type) {
            // Remove selection from all cards
            document.querySelectorAll('.service-type-card').forEach(c => {
                c.classList.remove('selected');
            });

            // Select this card
            card.classList.add('selected');

            // Update hidden field
            document.getElementById('<%= hfServiceType.ClientID %>').value = type;

            // Check the radio button
            const radio = card.querySelector('input[type="radio"]');
            if (radio) radio.checked = true;
        }

        function selectPriority(option, priority) {
            // Remove selection from all options
            document.querySelectorAll('.priority-option').forEach(opt => {
                opt.classList.remove('selected');
            });

            // Select this option
            option.classList.add('selected');

            // Update hidden field
            document.getElementById('<%= hfPriority.ClientID %>').value = priority;
        }

        function switchTab(evt, tabName) {
            const tabs = document.querySelectorAll('.tab-content');
            tabs.forEach(tab => tab.classList.remove('active'));

            const btns = document.querySelectorAll('.tab-btn');
            btns.forEach(btn => btn.classList.remove('active'));

            document.getElementById(tabName).classList.add('active');
            evt.currentTarget.classList.add('active');
        }

        function validateRequest() {
            const serviceType = document.getElementById('<%= hfServiceType.ClientID %>').value;
            const description = document.getElementById('<%= txtDescription.ClientID %>').value.trim();

            if (!serviceType) {
                alert('Please select a service type');
                return false;
            }

            if (!description) {
                alert('Please describe your request');
                return false;
            }

            return true;
        }

        // Auto-select first service type if none selected
        document.addEventListener('DOMContentLoaded', function() {
            const serviceType = document.getElementById('<%= hfServiceType.ClientID %>').value;
            if (!serviceType) {
                const firstCard = document.querySelector('.service-type-card');
                if (firstCard) {
                    selectServiceType(firstCard, 'CLEANING');
                }
            }
        });
    </script>
</asp:Content>
