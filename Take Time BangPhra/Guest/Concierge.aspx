<%@ Page Title="Concierge Services" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Concierge.aspx.cs" Inherits="Take_Time_BangPhra.Guest.Concierge" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <link rel="stylesheet" href="Housekeeping.aspx" />
    <style>
        .cc-header {
            background: linear-gradient(135deg, #4facfe 0%, #00f2fe 100%);
            color: white;
            padding: 25px;
            border-radius: 12px;
            margin-bottom: 25px;
            display: flex;
            justify-content: space-between;
        }
    </style>

    <div class="cc-header">
        <h2><i class="fas fa-concierge-bell"></i> Concierge Services</h2>
        <a href="Dashboard.aspx" class="btn-back"><i class="fas fa-arrow-left"></i> Back</a>
    </div>

    <div class="tabs">
        <button class="tab-btn active" onclick="switchTab(event, 'new')">New Request</button>
        <button class="tab-btn" onclick="switchTab(event, 'history')">My Requests</button>
    </div>

    <div id="new" class="tab-content active">
        <div class="request-form">
            <h3><i class="fas fa-clipboard-list"></i> Request Concierge Service</h3>

            <div class="form-group">
                <label class="form-label">Service Type *</label>
                <div class="service-types">
                    <div class="service-type-card" onclick="selectServiceType(this, 'TOUR')">
                        <i class="fas fa-map-marked-alt"></i>
                        <label>Tour</label>
                    </div>
                    <div class="service-type-card" onclick="selectServiceType(this, 'SPA')">
                        <i class="fas fa-spa"></i>
                        <label>Spa</label>
                    </div>
                    <div class="service-type-card" onclick="selectServiceType(this, 'RESTAURANT')">
                        <i class="fas fa-utensils"></i>
                        <label>Restaurant</label>
                    </div>
                    <div class="service-type-card" onclick="selectServiceType(this, 'TRANSPORTATION')">
                        <i class="fas fa-car"></i>
                        <label>Transport</label>
                    </div>
                    <div class="service-type-card" onclick="selectServiceType(this, 'ACTIVITY')">
                        <i class="fas fa-hiking"></i>
                        <label>Activity</label>
                    </div>
                </div>
                <asp:HiddenField ID="hfServiceType" runat="server" />
            </div>

            <div class="form-group">
                <label class="form-label">Service Name *</label>
                <asp:TextBox ID="txtServiceName" runat="server" CssClass="form-control" placeholder="e.g., Koh Samet Day Trip"></asp:TextBox>
            </div>

            <div class="form-group">
                <label class="form-label">Description *</label>
                <asp:TextBox ID="txtDescription" runat="server" CssClass="form-control" TextMode="MultiLine" Rows="3"></asp:TextBox>
            </div>

            <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 15px;">
                <div class="form-group">
                    <label class="form-label">Preferred Date</label>
                    <asp:TextBox ID="txtPreferredDate" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                </div>
                <div class="form-group">
                    <label class="form-label">Number of Guests</label>
                    <asp:TextBox ID="txtNumberOfGuests" runat="server" CssClass="form-control" TextMode="Number" Value="1"></asp:TextBox>
                </div>
            </div>

            <asp:Button ID="btnSubmitRequest" runat="server" Text="Submit Request" CssClass="btn-submit-request" OnClick="btnSubmitRequest_Click" />
        </div>
    </div>

    <div id="history" class="tab-content">
        <div class="requests-list">
            <h3>My Requests</h3>
            <asp:Repeater ID="rptRequests" runat="server">
                <ItemTemplate>
                    <div class="request-card">
                        <div class="request-header">
                            <div>
                                <div class="request-number">Request #<%# Eval("Request_Number") %></div>
                                <span class="request-type-badge"><%# Eval("Service_Type") %></span>
                            </div>
                            <span class="request-status status-<%# Eval("Request_Status") %>"><%# Eval("Request_Status") %></span>
                        </div>
                        <div><strong><%# Eval("Service_Name") %></strong></div>
                        <div class="request-description"><%# Eval("Request_Description") %></div>
                        <div class="request-meta">
                            <div><i class="fas fa-calendar"></i> <%# Eval("Request_Date", "{0:dd MMM yyyy}") %></div>
                            <div style='<%# Eval("Estimated_Cost") != DBNull.Value ? "" : "display:none;" %>'>
                                <i class="fas fa-dollar-sign"></i> ฿<%# Eval("Estimated_Cost", "{0:N0}") %>
                            </div>
                        </div>
                    </div>
                </ItemTemplate>
            </asp:Repeater>
            <asp:Label ID="lblNoRequests" runat="server" Visible="false" Text="<div style='text-align:center;padding:40px;color:#999;'><i class='fas fa-inbox' style='font-size:60px;'></i><p>No requests yet</p></div>"></asp:Label>
        </div>
    </div>

    <script src="Housekeeping.aspx"></script>
</asp:Content>
