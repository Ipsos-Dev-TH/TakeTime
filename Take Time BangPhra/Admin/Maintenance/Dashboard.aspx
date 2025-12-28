<%@ Page Title="Maintenance Dashboard" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Dashboard.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Maintenance.Dashboard" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .maintenance-dashboard { padding: 20px 0; }
        .page-header { margin-bottom: 30px; }
        .page-header h1 { color: #5D4037; margin: 0 0 10px 0; }

        .status-cards { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 20px; margin-bottom: 30px; }
        .status-card { background: white; border-radius: 12px; padding: 20px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); text-align: center; }
        .status-card .count { font-size: 32px; font-weight: 700; margin-bottom: 5px; }
        .status-card .label { color: #666; font-size: 13px; }
        .status-pending { border-left: 4px solid #FF9800; }
        .status-pending .count { color: #FF9800; }
        .status-progress { border-left: 4px solid #2196F3; }
        .status-progress .count { color: #2196F3; }
        .status-completed { border-left: 4px solid #4CAF50; }
        .status-completed .count { color: #4CAF50; }

        .request-list { background: white; border-radius: 12px; padding: 20px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }
        .request-item { padding: 15px; border-bottom: 1px solid #eee; display: flex; justify-content: space-between; align-items: center; }
        .request-item:last-child { border-bottom: none; }
        .request-info .title { font-weight: 600; margin-bottom: 5px; }
        .request-info .details { color: #666; font-size: 13px; }
        .priority-high { color: #f44336; }
        .priority-medium { color: #FF9800; }
        .priority-low { color: #4CAF50; }

        .btn-action { padding: 8px 16px; border-radius: 6px; border: none; cursor: pointer; font-size: 13px; }
        .btn-primary { background: #5D4037; color: white; }
        .btn-success { background: #4CAF50; color: white; }

        .section-title { font-size: 18px; font-weight: 600; margin: 30px 0 15px 0; }
        .no-data { text-align: center; padding: 40px; color: #999; }
    </style>

    <div class="maintenance-dashboard">
        <div class="page-header">
            <h1><i class="fas fa-tools"></i> Maintenance Dashboard</h1>
            <p>จัดการงานซ่อมบำรุง</p>
        </div>

        <div class="status-cards">
            <div class="status-card status-pending">
                <div class="count"><asp:Label ID="lblPending" runat="server" Text="0" /></div>
                <div class="label">รอดำเนินการ</div>
            </div>
            <div class="status-card status-progress">
                <div class="count"><asp:Label ID="lblInProgress" runat="server" Text="0" /></div>
                <div class="label">กำลังดำเนินการ</div>
            </div>
            <div class="status-card status-completed">
                <div class="count"><asp:Label ID="lblCompleted" runat="server" Text="0" /></div>
                <div class="label">เสร็จสิ้นเดือนนี้</div>
            </div>
        </div>

        <h3 class="section-title"><i class="fas fa-exclamation-circle"></i> งานที่รอดำเนินการ</h3>
        <div class="request-list">
            <asp:Repeater ID="rptRequests" runat="server">
                <ItemTemplate>
                    <div class="request-item">
                        <div class="request-info">
                            <div class="title"><%# Eval("Title") %></div>
                            <div class="details">
                                <i class="fas fa-map-marker-alt"></i> <%# Eval("Location") %> |
                                <span class='priority-<%# Eval("Priority").ToString().ToLower() %>'>
                                    <i class="fas fa-flag"></i> <%# Eval("Priority") %>
                                </span> |
                                <i class="fas fa-calendar"></i> <%# Eval("RequestDate", "{0:dd/MM/yyyy}") %>
                            </div>
                        </div>
                        <div>
                            <asp:Button ID="btnStart" runat="server" Text="เริ่มงาน" CssClass="btn-action btn-primary" />
                        </div>
                    </div>
                </ItemTemplate>
            </asp:Repeater>
            <asp:Label ID="lblNoRequests" runat="server" CssClass="no-data" Visible="true">
                <i class="fas fa-check-circle" style="font-size:48px;display:block;margin-bottom:15px;"></i>
                ไม่มีงานซ่อมบำรุงที่รอดำเนินการ
            </asp:Label>
        </div>
    </div>
</asp:Content>
