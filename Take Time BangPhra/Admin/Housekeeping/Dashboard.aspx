<%@ Page Title="Housekeeping Dashboard" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Dashboard.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Housekeeping.Dashboard" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .housekeeping-dashboard { padding: 20px 0; }
        .page-header { margin-bottom: 30px; }
        .page-header h1 { color: #5D4037; margin: 0 0 10px 0; }
        .page-header p { color: #666; margin: 0; }

        .status-cards { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 20px; margin-bottom: 30px; }
        .status-card { background: white; border-radius: 12px; padding: 20px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); text-align: center; }
        .status-card .icon { font-size: 40px; margin-bottom: 10px; }
        .status-card .count { font-size: 36px; font-weight: 700; }
        .status-card .label { color: #666; font-size: 14px; }

        .status-clean { border-left: 4px solid #4CAF50; }
        .status-clean .icon, .status-clean .count { color: #4CAF50; }

        .status-dirty { border-left: 4px solid #f44336; }
        .status-dirty .icon, .status-dirty .count { color: #f44336; }

        .status-inspecting { border-left: 4px solid #FF9800; }
        .status-inspecting .icon, .status-inspecting .count { color: #FF9800; }

        .status-occupied { border-left: 4px solid #2196F3; }
        .status-occupied .icon, .status-occupied .count { color: #2196F3; }

        .room-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(250px, 1fr)); gap: 15px; }
        .room-card { background: white; border-radius: 10px; padding: 15px; box-shadow: 0 2px 8px rgba(0,0,0,0.08); }
        .room-card .room-name { font-weight: 600; font-size: 16px; margin-bottom: 8px; }
        .room-card .room-status { display: inline-block; padding: 4px 12px; border-radius: 15px; font-size: 12px; font-weight: 500; }
        .room-card .room-status.clean { background: #E8F5E9; color: #2E7D32; }
        .room-card .room-status.dirty { background: #FFEBEE; color: #C62828; }
        .room-card .room-status.inspecting { background: #FFF3E0; color: #E65100; }
        .room-card .room-status.occupied { background: #E3F2FD; color: #1565C0; }
        .room-card .room-actions { margin-top: 12px; display: flex; gap: 8px; }
        .room-card .btn-sm { padding: 5px 12px; font-size: 12px; border-radius: 5px; border: none; cursor: pointer; }
        .btn-mark-clean { background: #4CAF50; color: white; }
        .btn-mark-dirty { background: #f44336; color: white; }
        .btn-inspect { background: #FF9800; color: white; }

        .section-title { font-size: 18px; font-weight: 600; color: #333; margin: 30px 0 15px 0; padding-bottom: 10px; border-bottom: 2px solid #5D4037; }

        .no-data { text-align: center; padding: 40px; color: #999; }
        .no-data i { font-size: 48px; margin-bottom: 15px; display: block; }
    </style>

    <div class="housekeeping-dashboard">
        <div class="page-header">
            <h1><i class="fas fa-broom"></i> Housekeeping Dashboard</h1>
            <p>จัดการสถานะความสะอาดของห้องพัก</p>
        </div>

        <!-- Status Summary Cards -->
        <div class="status-cards">
            <div class="status-card status-clean">
                <i class="fas fa-check-circle icon"></i>
                <div class="count"><asp:Label ID="lblCleanCount" runat="server" Text="0" /></div>
                <div class="label">ห้องสะอาด (Clean)</div>
            </div>
            <div class="status-card status-dirty">
                <i class="fas fa-exclamation-circle icon"></i>
                <div class="count"><asp:Label ID="lblDirtyCount" runat="server" Text="0" /></div>
                <div class="label">รอทำความสะอาด (Dirty)</div>
            </div>
            <div class="status-card status-inspecting">
                <i class="fas fa-search icon"></i>
                <div class="count"><asp:Label ID="lblInspectingCount" runat="server" Text="0" /></div>
                <div class="label">รอตรวจสอบ (Inspecting)</div>
            </div>
            <div class="status-card status-occupied">
                <i class="fas fa-user icon"></i>
                <div class="count"><asp:Label ID="lblOccupiedCount" runat="server" Text="0" /></div>
                <div class="label">มีผู้เข้าพัก (Occupied)</div>
            </div>
        </div>

        <!-- Rooms Requiring Attention -->
        <h3 class="section-title"><i class="fas fa-exclamation-triangle"></i> ห้องที่ต้องทำความสะอาด</h3>
        <asp:Panel ID="pnlDirtyRooms" runat="server">
            <div class="room-grid">
                <asp:Repeater ID="rptDirtyRooms" runat="server" OnItemCommand="rptDirtyRooms_ItemCommand">
                    <ItemTemplate>
                        <div class="room-card">
                            <div class="room-name"><%# Eval("AccomName") %></div>
                            <span class="room-status dirty">รอทำความสะอาด</span>
                            <div class="room-actions">
                                <asp:Button ID="btnMarkClean" runat="server" Text="ทำความสะอาดเสร็จ"
                                    CssClass="btn-sm btn-mark-clean" CommandName="MarkClean"
                                    CommandArgument='<%# Eval("ID") %>' />
                            </div>
                        </div>
                    </ItemTemplate>
                </asp:Repeater>
            </div>
            <asp:Label ID="lblNoDirtyRooms" runat="server" CssClass="no-data" Visible="false">
                <i class="fas fa-check-circle"></i>
                ไม่มีห้องที่ต้องทำความสะอาด
            </asp:Label>
        </asp:Panel>

        <!-- All Rooms Status -->
        <h3 class="section-title"><i class="fas fa-door-open"></i> สถานะห้องทั้งหมด</h3>
        <div class="room-grid">
            <asp:Repeater ID="rptAllRooms" runat="server" OnItemCommand="rptAllRooms_ItemCommand">
                <ItemTemplate>
                    <div class="room-card">
                        <div class="room-name"><%# Eval("AccomName") %></div>
                        <span class='room-status <%# GetStatusClass(Eval("HousekeepingStatus")) %>'>
                            <%# GetStatusText(Eval("HousekeepingStatus")) %>
                        </span>
                        <div class="room-actions">
                            <asp:Button ID="btnClean" runat="server" Text="สะอาด"
                                CssClass="btn-sm btn-mark-clean" CommandName="SetClean"
                                CommandArgument='<%# Eval("ID") %>' />
                            <asp:Button ID="btnDirty" runat="server" Text="ไม่สะอาด"
                                CssClass="btn-sm btn-mark-dirty" CommandName="SetDirty"
                                CommandArgument='<%# Eval("ID") %>' />
                        </div>
                    </div>
                </ItemTemplate>
            </asp:Repeater>
        </div>
    </div>
</asp:Content>
