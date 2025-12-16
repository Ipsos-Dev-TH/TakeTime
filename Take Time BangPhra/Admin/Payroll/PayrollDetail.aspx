<%@ Page Title="รายละเอียดเงินเดือน" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="PayrollDetail.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Payroll.PayrollDetail" MaintainScrollPositionOnPostback="true" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .payroll-detail {
            padding: 20px;
        }

        .section-header {
            background: linear-gradient(135deg, #11998e 0%, #38ef7d 100%);
            color: white;
            padding: 15px 20px;
            border-radius: 8px;
            margin-bottom: 20px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
            display: flex;
            justify-content: space-between;
            align-items: center;
        }

        .section-header h2 {
            margin: 0;
            font-size: 24px;
            font-weight: 600;
        }

        .btn-back {
            background: rgba(255,255,255,0.2);
            border: 1px solid rgba(255,255,255,0.5);
            color: white;
            padding: 8px 20px;
            border-radius: 6px;
            text-decoration: none;
        }

        .payslip-container {
            background: white;
            border-radius: 8px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
            padding: 30px;
            max-width: 800px;
            margin: 0 auto;
        }

        .payslip-header {
            text-align: center;
            margin-bottom: 30px;
            padding-bottom: 20px;
            border-bottom: 2px solid #e0e0e0;
        }

        .payslip-header h3 {
            color: #333;
            margin: 0 0 5px 0;
        }

        .payslip-header .period {
            color: #666;
            font-size: 14px;
        }

        .employee-info {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 15px;
            margin-bottom: 30px;
            padding: 15px;
            background: #f8f9fa;
            border-radius: 6px;
        }

        .employee-info .info-item {
            display: flex;
            justify-content: space-between;
        }

        .employee-info .label {
            color: #666;
            font-size: 13px;
        }

        .employee-info .value {
            font-weight: 500;
        }

        .salary-details {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 30px;
            margin-bottom: 30px;
        }

        .salary-section {
            padding: 15px;
            border-radius: 6px;
        }

        .salary-section.earnings {
            background: linear-gradient(135deg, #d4edda 0%, #c3e6cb 100%);
        }

        .salary-section.deductions {
            background: linear-gradient(135deg, #f8d7da 0%, #f5c6cb 100%);
        }

        .salary-section h4 {
            margin: 0 0 15px 0;
            font-size: 14px;
            color: #333;
        }

        .salary-item {
            display: flex;
            justify-content: space-between;
            padding: 8px 0;
            border-bottom: 1px dashed rgba(0,0,0,0.1);
        }

        .salary-item:last-child {
            border-bottom: none;
        }

        .salary-item .label {
            color: #333;
        }

        .salary-item .amount {
            font-weight: 500;
        }

        .total-section {
            display: grid;
            grid-template-columns: 1fr 1fr 1fr;
            gap: 15px;
            padding: 20px;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            border-radius: 6px;
            color: white;
            margin-bottom: 30px;
        }

        .total-item {
            text-align: center;
        }

        .total-item .label {
            font-size: 12px;
            opacity: 0.9;
        }

        .total-item .amount {
            font-size: 24px;
            font-weight: 700;
        }

        .ot-section {
            background: #f8f9fa;
            padding: 15px;
            border-radius: 6px;
            margin-bottom: 30px;
        }

        .ot-section h4 {
            margin: 0 0 15px 0;
            font-size: 14px;
            color: #333;
        }

        .data-table {
            width: 100%;
            border-collapse: collapse;
        }

        .data-table th {
            background: #e9ecef;
            padding: 10px;
            text-align: left;
            font-weight: 500;
            color: #333;
            font-size: 13px;
        }

        .data-table td {
            padding: 10px;
            border-bottom: 1px solid #f0f0f0;
        }

        .action-buttons {
            text-align: center;
            margin-top: 30px;
        }

        .btn-print {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            border: none;
            color: white;
            padding: 12px 30px;
            border-radius: 6px;
            font-weight: 500;
            cursor: pointer;
            margin: 0 10px;
        }

        .btn-voucher {
            background: linear-gradient(135deg, #11998e 0%, #38ef7d 100%);
            border: none;
            color: white;
            padding: 12px 30px;
            border-radius: 6px;
            font-weight: 500;
            cursor: pointer;
            margin: 0 10px;
        }

        @media print {
            .section-header, .action-buttons {
                display: none !important;
            }

            .payslip-container {
                box-shadow: none;
                max-width: 100%;
            }
        }
    </style>

    <div class="payroll-detail">
        <div class="section-header">
            <h2>รายละเอียดเงินเดือน</h2>
            <a href="PayrollManagement.aspx" class="btn-back">กลับไปรายการ</a>
        </div>

        <div class="payslip-container">
            <div class="payslip-header">
                <h3>สลิปเงินเดือน</h3>
                <div class="period">งวด: <asp:Label ID="lblPeriod" runat="server"></asp:Label></div>
            </div>

            <!-- Employee Info -->
            <div class="employee-info">
                <div class="info-item">
                    <span class="label">ชื่อ-นามสกุล:</span>
                    <span class="value"><asp:Label ID="lblEmployeeName" runat="server"></asp:Label></span>
                </div>
                <div class="info-item">
                    <span class="label">ตำแหน่ง:</span>
                    <span class="value"><asp:Label ID="lblPosition" runat="server"></asp:Label></span>
                </div>
                <div class="info-item">
                    <span class="label">รหัสพนักงาน:</span>
                    <span class="value"><asp:Label ID="lblEmployeeCode" runat="server"></asp:Label></span>
                </div>
                <div class="info-item">
                    <span class="label">แผนก:</span>
                    <span class="value"><asp:Label ID="lblDepartment" runat="server"></asp:Label></span>
                </div>
            </div>

            <!-- Salary Details -->
            <div class="salary-details">
                <div class="salary-section earnings">
                    <h4>รายได้</h4>
                    <div class="salary-item">
                        <span class="label">เงินเดือนพื้นฐาน</span>
                        <span class="amount">฿<asp:Label ID="lblBaseSalary" runat="server"></asp:Label></span>
                    </div>
                    <div class="salary-item">
                        <span class="label">ค่าล่วงเวลา (OT)</span>
                        <span class="amount">฿<asp:Label ID="lblOTAmount" runat="server"></asp:Label></span>
                    </div>
                    <div class="salary-item">
                        <span class="label">โบนัส</span>
                        <span class="amount">฿<asp:Label ID="lblBonus" runat="server"></asp:Label></span>
                    </div>
                    <div class="salary-item">
                        <span class="label">ค่าเบี้ยเลี้ยง</span>
                        <span class="amount">฿<asp:Label ID="lblAllowances" runat="server"></asp:Label></span>
                    </div>
                    <div class="salary-item" style="font-weight: bold; border-top: 2px solid rgba(0,0,0,0.2); padding-top: 10px;">
                        <span class="label">รายได้รวม</span>
                        <span class="amount">฿<asp:Label ID="lblTotalEarnings" runat="server"></asp:Label></span>
                    </div>
                </div>

                <div class="salary-section deductions">
                    <h4>รายการหัก</h4>
                    <div class="salary-item">
                        <span class="label">หักลา</span>
                        <span class="amount">฿<asp:Label ID="lblLeaveDeduction" runat="server"></asp:Label></span>
                    </div>
                    <div class="salary-item">
                        <span class="label">ประกันสังคม</span>
                        <span class="amount">฿<asp:Label ID="lblSocialSecurity" runat="server"></asp:Label></span>
                    </div>
                    <div class="salary-item">
                        <span class="label">ภาษี</span>
                        <span class="amount">฿<asp:Label ID="lblTax" runat="server"></asp:Label></span>
                    </div>
                    <div class="salary-item">
                        <span class="label">รายการหักอื่นๆ</span>
                        <span class="amount">฿<asp:Label ID="lblOtherDeductions" runat="server"></asp:Label></span>
                    </div>
                    <div class="salary-item" style="font-weight: bold; border-top: 2px solid rgba(0,0,0,0.2); padding-top: 10px;">
                        <span class="label">หักรวม</span>
                        <span class="amount">฿<asp:Label ID="lblTotalDeductions" runat="server"></asp:Label></span>
                    </div>
                </div>
            </div>

            <!-- Totals -->
            <div class="total-section">
                <div class="total-item">
                    <div class="label">วันทำงาน</div>
                    <div class="amount"><asp:Label ID="lblWorkDays" runat="server"></asp:Label> วัน</div>
                </div>
                <div class="total-item">
                    <div class="label">วันลา</div>
                    <div class="amount"><asp:Label ID="lblLeaveDays" runat="server"></asp:Label> วัน</div>
                </div>
                <div class="total-item">
                    <div class="label">เงินสุทธิ</div>
                    <div class="amount">฿<asp:Label ID="lblNetSalary" runat="server"></asp:Label></div>
                </div>
            </div>

            <!-- OT Details -->
            <asp:Panel ID="pnlOTDetails" runat="server" Visible="false" CssClass="ot-section">
                <h4>รายละเอียดการทำ OT</h4>
                <asp:GridView ID="gvOTDetails" runat="server" AutoGenerateColumns="False"
                    CssClass="data-table" GridLines="None">
                    <Columns>
                        <asp:TemplateField HeaderText="วันที่">
                            <ItemTemplate>
                                <%# Eval("OTDate") != DBNull.Value ? Convert.ToDateTime(Eval("OTDate")).ToString("dd/MM/yyyy") : "-" %>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:BoundField DataField="OTHours" HeaderText="ชั่วโมง" DataFormatString="{0:N2}" />
                        <asp:TemplateField HeaderText="อัตรา/ชม.">
                            <ItemTemplate>
                                ฿<%# string.Format("{0:N2}", Eval("OTRate")) %>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="จำนวนเงิน">
                            <ItemTemplate>
                                ฿<%# string.Format("{0:N2}", Eval("OTAmount")) %>
                            </ItemTemplate>
                        </asp:TemplateField>
                        <asp:BoundField DataField="Description" HeaderText="หมายเหตุ" />
                    </Columns>
                    <EmptyDataTemplate>
                        <div style="text-align: center; padding: 15px; color: #999;">ไม่มีรายการ OT</div>
                    </EmptyDataTemplate>
                </asp:GridView>
            </asp:Panel>

            <!-- Action Buttons -->
            <div class="action-buttons">
                <asp:Button ID="btnPrint" runat="server" Text="พิมพ์สลิป" CssClass="btn-print" OnClientClick="window.print(); return false;" />
                <asp:Button ID="btnGenerateVoucher" runat="server" Text="สร้าง Payment Voucher" CssClass="btn-voucher" OnClick="btnGenerateVoucher_Click" />
            </div>
        </div>
    </div>
</asp:Content>
