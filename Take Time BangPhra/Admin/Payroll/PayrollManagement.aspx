<%@ Page Title="จัดการเงินเดือนและ OT" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="PayrollManagement.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Payroll.PayrollManagement" MaintainScrollPositionOnPostback="true" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .payroll-management {
            padding: 20px;
        }

        .section-header {
            background: linear-gradient(135deg, #11998e 0%, #38ef7d 100%);
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

        .period-section {
            background: white;
            padding: 20px;
            border-radius: 8px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
            margin-bottom: 20px;
        }

        .period-controls {
            display: flex;
            gap: 10px;
            align-items: center;
            flex-wrap: wrap;
        }

        .form-control {
            padding: 10px 15px;
            border: 2px solid #e0e0e0;
            border-radius: 6px;
            font-size: 14px;
        }

        .form-control:focus {
            border-color: #11998e;
            outline: none;
        }

        .btn-primary {
            background: linear-gradient(135deg, #11998e 0%, #38ef7d 100%);
            border: none;
            color: white;
            padding: 10px 25px;
            border-radius: 6px;
            font-weight: 500;
            cursor: pointer;
        }

        .btn-warning {
            background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%);
            border: none;
            color: white;
            padding: 10px 25px;
            border-radius: 6px;
            font-weight: 500;
            cursor: pointer;
        }

        .btn-info {
            background: linear-gradient(135deg, #4facfe 0%, #00f2fe 100%);
            border: none;
            color: white;
            padding: 8px 15px;
            border-radius: 6px;
            font-size: 13px;
            cursor: pointer;
        }

        .stats-container {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 15px;
            margin-bottom: 20px;
        }

        .stat-card {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 15px;
            border-radius: 8px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.2);
        }

        .stat-card h4 {
            margin: 0 0 8px 0;
            font-size: 13px;
            opacity: 0.9;
        }

        .stat-card .value {
            font-size: 24px;
            font-weight: 700;
        }

        .data-table {
            background: white;
            border-radius: 8px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
            overflow: hidden;
        }

        .data-table table {
            width: 100%;
            border-collapse: collapse;
        }

        .data-table th {
            background: linear-gradient(135deg, #11998e 0%, #38ef7d 100%);
            color: white;
            padding: 12px;
            text-align: left;
            font-weight: 500;
        }

        .data-table td {
            padding: 12px;
            border-bottom: 1px solid #f0f0f0;
        }

        .data-table tr:hover {
            background-color: #f8f9fa;
        }

        .badge {
            display: inline-block;
            padding: 4px 8px;
            border-radius: 4px;
            font-size: 11px;
            font-weight: 500;
        }

        .badge-draft {
            background: #e3f2fd;
            color: #1976d2;
        }

        .badge-approved {
            background: #d4edda;
            color: #155724;
        }

        .badge-pending {
            background: #fff3cd;
            color: #856404;
        }

        .text-right {
            text-align: right;
        }
    </style>

    <div class="payroll-management">
        <div class="section-header">
            <h2>💰 ระบบจัดการเงินเดือนและ OT</h2>
        </div>

        <!-- Period Selection -->
        <div class="period-section">
            <h4 style="margin-top: 0;">เลือกงวดเงินเดือน</h4>
            <div class="period-controls">
                <asp:DropDownList ID="ddlYear" runat="server" CssClass="form-control" AutoPostBack="true" OnSelectedIndexChanged="ddlYear_SelectedIndexChanged">
                </asp:DropDownList>
                <asp:DropDownList ID="ddlMonth" runat="server" CssClass="form-control" AutoPostBack="true" OnSelectedIndexChanged="ddlMonth_SelectedIndexChanged">
                    <asp:ListItem Value="1">มกราคม</asp:ListItem>
                    <asp:ListItem Value="2">กุมภาพันธ์</asp:ListItem>
                    <asp:ListItem Value="3">มีนาคม</asp:ListItem>
                    <asp:ListItem Value="4">เมษายน</asp:ListItem>
                    <asp:ListItem Value="5">พฤษภาคม</asp:ListItem>
                    <asp:ListItem Value="6">มิถุนายน</asp:ListItem>
                    <asp:ListItem Value="7">กรกฎาคม</asp:ListItem>
                    <asp:ListItem Value="8">สิงหาคม</asp:ListItem>
                    <asp:ListItem Value="9">กันยายน</asp:ListItem>
                    <asp:ListItem Value="10">ตุลาคม</asp:ListItem>
                    <asp:ListItem Value="11">พฤศจิกายน</asp:ListItem>
                    <asp:ListItem Value="12">ธันวาคม</asp:ListItem>
                </asp:DropDownList>
                <asp:Button ID="btnGeneratePayroll" runat="server" Text="สร้างรอบเงินเดือน" CssClass="btn-primary" OnClick="btnGeneratePayroll_Click" />
                <asp:Button ID="btnApprovePayroll" runat="server" Text="อนุมัติเงินเดือน" CssClass="btn-warning" OnClick="btnApprovePayroll_Click" Visible="false" />
            </div>
        </div>

        <!-- Statistics -->
        <asp:Panel ID="pnlStats" runat="server" Visible="false">
            <div class="stats-container">
                <div class="stat-card">
                    <h4>พนักงานทั้งหมด</h4>
                    <div class="value"><asp:Label ID="lblTotalEmployees" runat="server" Text="0"></asp:Label> คน</div>
                </div>
                <div class="stat-card" style="background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%);">
                    <h4>เงินเดือนรวม</h4>
                    <div class="value">฿<asp:Label ID="lblTotalGrossPay" runat="server" Text="0.00"></asp:Label></div>
                </div>
                <div class="stat-card" style="background: linear-gradient(135deg, #fa709a 0%, #fee140 100%);">
                    <h4>หักรวม</h4>
                    <div class="value">฿<asp:Label ID="lblTotalDeductions" runat="server" Text="0.00"></asp:Label></div>
                </div>
                <div class="stat-card" style="background: linear-gradient(135deg, #11998e 0%, #38ef7d 100%);">
                    <h4>สุทธิจ่าย</h4>
                    <div class="value">฿<asp:Label ID="lblTotalNetPay" runat="server" Text="0.00"></asp:Label></div>
                </div>
            </div>
        </asp:Panel>

        <!-- Payroll Records -->
        <div class="data-table">
            <asp:GridView ID="gvPayroll" runat="server" AutoGenerateColumns="False"
                CssClass="table" GridLines="None" OnRowCommand="gvPayroll_RowCommand">
                <Columns>
                    <asp:BoundField DataField="EmployeeName" HeaderText="ชื่อ-นามสกุล" />
                    <asp:BoundField DataField="Position" HeaderText="ตำแหน่ง" />

                    <asp:TemplateField HeaderText="เงินเดือน" ItemStyle-CssClass="text-right" HeaderStyle-CssClass="text-right">
                        <ItemTemplate>
                            ฿<%# string.Format("{0:N2}", Eval("BaseSalary")) %>
                        </ItemTemplate>
                    </asp:TemplateField>

                    <asp:TemplateField HeaderText="วันทำงาน/ลา" ItemStyle-CssClass="text-right" HeaderStyle-CssClass="text-right">
                        <ItemTemplate>
                            <%# Eval("WorkDays") %> / <%# Eval("LeaveDays") %>
                        </ItemTemplate>
                    </asp:TemplateField>

                    <asp:TemplateField HeaderText="OT (ชม.)" ItemStyle-CssClass="text-right" HeaderStyle-CssClass="text-right">
                        <ItemTemplate>
                            <%# Eval("OTHours") != DBNull.Value ? string.Format("{0:N2}", Eval("OTHours")) : "0.00" %>
                        </ItemTemplate>
                    </asp:TemplateField>

                    <asp:TemplateField HeaderText="เงิน OT" ItemStyle-CssClass="text-right" HeaderStyle-CssClass="text-right">
                        <ItemTemplate>
                            ฿<%# Eval("OTAmount") != DBNull.Value ? string.Format("{0:N2}", Eval("OTAmount")) : "0.00" %>
                        </ItemTemplate>
                    </asp:TemplateField>

                    <asp:TemplateField HeaderText="หักเงิน" ItemStyle-CssClass="text-right" HeaderStyle-CssClass="text-right">
                        <ItemTemplate>
                            ฿<%# Eval("TotalDeductions") != DBNull.Value ? string.Format("{0:N2}", Eval("TotalDeductions")) : "0.00" %>
                        </ItemTemplate>
                    </asp:TemplateField>

                    <asp:TemplateField HeaderText="สุทธิ" ItemStyle-CssClass="text-right" HeaderStyle-CssClass="text-right">
                        <ItemTemplate>
                            <strong>฿<%# Eval("NetSalary") != DBNull.Value ? string.Format("{0:N2}", Eval("NetSalary")) : "0.00" %></strong>
                        </ItemTemplate>
                    </asp:TemplateField>

                    <asp:TemplateField HeaderText="Voucher">
                        <ItemTemplate>
                            <%# GetVoucherStatusBadge(Eval("VoucherGenerated"), Eval("VoucherNumber")) %>
                        </ItemTemplate>
                    </asp:TemplateField>

                    <asp:TemplateField HeaderText="จัดการ">
                        <ItemTemplate>
                            <asp:Button ID="btnDetails" runat="server" Text="รายละเอียด"
                                CssClass="btn-info" CommandName="ViewDetails"
                                CommandArgument='<%# Eval("ID") %>' />
                        </ItemTemplate>
                    </asp:TemplateField>
                </Columns>
                <EmptyDataTemplate>
                    <div style="text-align: center; padding: 40px; color: #999;">
                        ยังไม่มีข้อมูลเงินเดือนสำหรับงวดนี้<br />
                        กรุณาคลิก "สร้างรอบเงินเดือน" เพื่อสร้างข้อมูล
                    </div>
                </EmptyDataTemplate>
            </asp:GridView>
        </div>
    </div>
</asp:Content>
