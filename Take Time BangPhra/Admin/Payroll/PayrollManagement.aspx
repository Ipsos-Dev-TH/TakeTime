<%@ Page Title="จัดการเงินเดือนและ OT" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="PayrollManagement.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Payroll.PayrollManagement" MaintainScrollPositionOnPostback="true" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .payroll-management { padding: 20px; }

        .section-header {
            background: linear-gradient(135deg, #11998e 0%, #38ef7d 100%);
            color: white;
            padding: 15px 20px;
            border-radius: 8px;
            margin-bottom: 20px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
        }

        .section-header h2 { margin: 0; font-size: 24px; font-weight: 600; }

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
            min-height: 44px;
        }

        .form-control:focus { border-color: #11998e; outline: none; }

        select.form-control { height: auto; min-height: 44px; }

        .btn {
            border: none;
            padding: 10px 20px;
            border-radius: 6px;
            font-weight: 500;
            cursor: pointer;
            transition: all 0.2s;
        }

        .btn:hover { transform: translateY(-2px); box-shadow: 0 4px 12px rgba(0,0,0,0.2); }

        .btn-primary { background: linear-gradient(135deg, #11998e 0%, #38ef7d 100%); color: white; }
        .btn-warning { background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%); color: white; }
        .btn-info { background: linear-gradient(135deg, #4facfe 0%, #00f2fe 100%); color: white; padding: 6px 12px; font-size: 12px; }
        .btn-success { background: linear-gradient(135deg, #11998e 0%, #38ef7d 100%); color: white; }
        .btn-export { background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; }
        .btn-process { background: linear-gradient(135deg, #fa709a 0%, #fee140 100%); color: #333; }
        .btn-edit { background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 5px 10px; font-size: 11px; }
        .btn-pay { background: linear-gradient(135deg, #11998e 0%, #38ef7d 100%); color: white; padding: 5px 10px; font-size: 11px; }
        .btn-cancel { background: #e0e0e0; color: #333; }

        .stats-container {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(160px, 1fr));
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

        .stat-card h4 { margin: 0 0 8px 0; font-size: 11px; opacity: 0.9; }
        .stat-card .value { font-size: 18px; font-weight: 700; }

        .action-bar {
            display: flex;
            gap: 10px;
            margin-bottom: 20px;
            flex-wrap: wrap;
        }

        .data-table {
            background: white;
            border-radius: 8px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
            overflow-x: auto;
        }

        .data-table table { width: 100%; border-collapse: collapse; min-width: 1100px; }
        .data-table th { background: linear-gradient(135deg, #11998e 0%, #38ef7d 100%); color: white; padding: 10px 6px; text-align: left; font-weight: 500; font-size: 11px; }
        .data-table td { padding: 10px 6px; border-bottom: 1px solid #f0f0f0; font-size: 12px; }
        .data-table tr:hover { background-color: #f8f9fa; }

        .badge { display: inline-block; padding: 4px 8px; border-radius: 4px; font-size: 10px; font-weight: 500; }
        .badge-draft { background: #e3f2fd; color: #1976d2; }
        .badge-approved { background: #d4edda; color: #155724; }
        .badge-pending { background: #fff3cd; color: #856404; }
        .badge-paid { background: #d4edda; color: #155724; }

        .text-right { text-align: right; }
        .text-center { text-align: center; }
        .action-cell { display: flex; gap: 4px; }

        /* Modal Styles */
        .modal-overlay {
            display: none;
            position: fixed;
            top: 0; left: 0;
            width: 100%; height: 100%;
            background: rgba(0,0,0,0.5);
            z-index: 1000;
            justify-content: center;
            align-items: center;
        }

        .modal-content {
            background: white;
            border-radius: 10px;
            padding: 25px;
            max-width: 700px;
            width: 95%;
            max-height: 90vh;
            overflow-y: auto;
            box-shadow: 0 10px 40px rgba(0,0,0,0.3);
        }

        .modal-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 20px;
            padding-bottom: 15px;
            border-bottom: 2px solid #f0f0f0;
        }

        .modal-header h3 { margin: 0; color: #333; font-size: 18px; }
        .modal-close { background: none; border: none; font-size: 24px; cursor: pointer; color: #999; }

        .form-group { margin-bottom: 15px; }
        .form-group label { display: block; margin-bottom: 5px; font-weight: 500; color: #555; font-size: 13px; }
        .form-group input { width: 100%; padding: 10px 12px; border: 2px solid #e0e0e0; border-radius: 6px; font-size: 14px; box-sizing: border-box; }
        .form-group input:focus { border-color: #11998e; outline: none; }

        .form-row { display: grid; grid-template-columns: 1fr 1fr; gap: 15px; }

        .form-section { background: #f8f9fa; padding: 15px; border-radius: 8px; margin-bottom: 15px; }
        .form-section h4 { margin: 0 0 10px 0; font-size: 14px; color: #333; }

        .summary-row { display: flex; justify-content: space-between; padding: 8px 0; border-bottom: 1px solid #e0e0e0; }
        .summary-row.total { font-weight: bold; font-size: 16px; border-bottom: none; border-top: 2px solid #333; padding-top: 10px; margin-top: 5px; }

        .modal-footer { margin-top: 20px; padding-top: 15px; border-top: 2px solid #f0f0f0; display: flex; justify-content: flex-end; gap: 10px; }

        .alert { padding: 15px; border-radius: 8px; margin-bottom: 20px; }
        .alert-success { background: #d4edda; color: #155724; }
        .alert-error { background: #f8d7da; color: #721c24; }

        .ss-info { background: #e3f2fd; border-left: 4px solid #1976d2; padding: 10px 15px; margin: 10px 0; font-size: 12px; }
    </style>

    <div class="payroll-management">
        <div class="section-header">
            <h2>&#128176; ระบบจัดการเงินเดือนและ OT</h2>
        </div>

        <!-- Message Panel -->
        <asp:Panel ID="pnlMessage" runat="server" Visible="false" CssClass="alert">
            <asp:Label ID="lblMessage" runat="server"></asp:Label>
        </asp:Panel>

        <!-- Period Selection -->
        <div class="period-section">
            <h4 style="margin-top: 0;">&#128197; เลือกงวดเงินเดือน</h4>
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
                <asp:Button ID="btnGeneratePayroll" runat="server" Text="&#10133; สร้างรอบเงินเดือน" CssClass="btn btn-primary" OnClick="btnGeneratePayroll_Click" />
            </div>
        </div>

        <!-- Statistics -->
        <asp:Panel ID="pnlStats" runat="server" Visible="false">
            <div class="stats-container">
                <div class="stat-card">
                    <h4>&#128100; พนักงาน</h4>
                    <div class="value"><asp:Label ID="lblTotalEmployees" runat="server" Text="0"></asp:Label> คน</div>
                </div>
                <div class="stat-card" style="background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%);">
                    <h4>&#128176; เงินเดือนรวม</h4>
                    <div class="value">&#3647;<asp:Label ID="lblTotalGrossPay" runat="server" Text="0"></asp:Label></div>
                </div>
                <div class="stat-card" style="background: linear-gradient(135deg, #4facfe 0%, #00f2fe 100%);">
                    <h4>&#128337; OT รวม</h4>
                    <div class="value">&#3647;<asp:Label ID="lblTotalOT" runat="server" Text="0"></asp:Label></div>
                </div>
                <div class="stat-card" style="background: linear-gradient(135deg, #fa709a 0%, #fee140 100%); color: #333;">
                    <h4>&#128179; ประกันสังคม</h4>
                    <div class="value">&#3647;<asp:Label ID="lblTotalSS" runat="server" Text="0"></asp:Label></div>
                </div>
                <div class="stat-card" style="background: linear-gradient(135deg, #11998e 0%, #38ef7d 100%);">
                    <h4>&#128178; สุทธิจ่าย</h4>
                    <div class="value">&#3647;<asp:Label ID="lblTotalNetPay" runat="server" Text="0"></asp:Label></div>
                </div>
            </div>

            <!-- Action Bar -->
            <div class="action-bar">
                <asp:Button ID="btnRecalculate" runat="server" Text="&#128260; คำนวณใหม่" CssClass="btn btn-info" OnClick="btnRecalculate_Click" />
                <asp:Button ID="btnApprovePayroll" runat="server" Text="&#9989; อนุมัติเงินเดือน" CssClass="btn btn-warning" OnClick="btnApprovePayroll_Click" Visible="false" />
                <asp:Button ID="btnProcessAll" runat="server" Text="&#128179; ทำจ่ายทั้งหมด" CssClass="btn btn-process" OnClick="btnProcessAll_Click" Visible="false" />
                <asp:Button ID="btnExportSS" runat="server" Text="&#128190; Export ประกันสังคม" CssClass="btn btn-export" OnClick="btnExportSS_Click" />
            </div>

            <div class="ss-info">
                <strong>&#9432; ประกันสังคม:</strong> หักพนักงาน 5% ของเงินเดือน (สูงสุด 750 บาท) | ฐานค่าจ้างขั้นต่ำ 1,650 บาท สูงสุด 15,000 บาท
            </div>
        </asp:Panel>

        <!-- Payroll Records -->
        <div class="data-table">
            <asp:GridView ID="gvPayroll" runat="server" AutoGenerateColumns="False"
                CssClass="table" GridLines="None" OnRowCommand="gvPayroll_RowCommand" DataKeyNames="ID">
                <Columns>
                    <asp:BoundField DataField="EmployeeName" HeaderText="ชื่อ-นามสกุล" />
                    <asp:BoundField DataField="Position" HeaderText="ตำแหน่ง" />

                    <asp:TemplateField HeaderText="เงินเดือน" ItemStyle-CssClass="text-right" HeaderStyle-CssClass="text-right">
                        <ItemTemplate><%# string.Format("{0:N0}", Eval("BaseSalary")) %></ItemTemplate>
                    </asp:TemplateField>

                    <asp:TemplateField HeaderText="OT" ItemStyle-CssClass="text-right" HeaderStyle-CssClass="text-right">
                        <ItemTemplate><%# Eval("OTAmount") != DBNull.Value && Convert.ToDecimal(Eval("OTAmount")) > 0 ? string.Format("{0:N0}", Eval("OTAmount")) : "-" %></ItemTemplate>
                    </asp:TemplateField>

                    <asp:TemplateField HeaderText="โบนัส" ItemStyle-CssClass="text-right" HeaderStyle-CssClass="text-right">
                        <ItemTemplate><%# (Convert.ToDecimal(Eval("BonusAmount") ?? 0) + Convert.ToDecimal(Eval("AllowanceAmount") ?? 0)) > 0 ? string.Format("{0:N0}", Convert.ToDecimal(Eval("BonusAmount") ?? 0) + Convert.ToDecimal(Eval("AllowanceAmount") ?? 0)) : "-" %></ItemTemplate>
                    </asp:TemplateField>

                    <asp:TemplateField HeaderText="รายได้รวม" ItemStyle-CssClass="text-right" HeaderStyle-CssClass="text-right">
                        <ItemTemplate><strong><%# string.Format("{0:N0}", Eval("TotalEarnings")) %></strong></ItemTemplate>
                    </asp:TemplateField>

                    <asp:TemplateField HeaderText="ปกส." ItemStyle-CssClass="text-right" HeaderStyle-CssClass="text-right">
                        <ItemTemplate><%# Eval("SocialSecurity") != DBNull.Value && Convert.ToDecimal(Eval("SocialSecurity")) > 0 ? string.Format("{0:N0}", Eval("SocialSecurity")) : "-" %></ItemTemplate>
                    </asp:TemplateField>

                    <asp:TemplateField HeaderText="หักอื่นๆ" ItemStyle-CssClass="text-right" HeaderStyle-CssClass="text-right">
                        <ItemTemplate><%# (Convert.ToDecimal(Eval("LeaveDeduction") ?? 0) + Convert.ToDecimal(Eval("Tax") ?? 0) + Convert.ToDecimal(Eval("OtherDeductions") ?? 0)) > 0 ? string.Format("{0:N0}", Convert.ToDecimal(Eval("LeaveDeduction") ?? 0) + Convert.ToDecimal(Eval("Tax") ?? 0) + Convert.ToDecimal(Eval("OtherDeductions") ?? 0)) : "-" %></ItemTemplate>
                    </asp:TemplateField>

                    <asp:TemplateField HeaderText="สุทธิ" ItemStyle-CssClass="text-right" HeaderStyle-CssClass="text-right">
                        <ItemTemplate><strong style="color: #11998e;"><%# string.Format("{0:N0}", Eval("NetSalary")) %></strong></ItemTemplate>
                    </asp:TemplateField>

                    <asp:TemplateField HeaderText="สถานะ" ItemStyle-CssClass="text-center">
                        <ItemTemplate><%# GetPaymentStatusBadge(Eval("VoucherGenerated"), Eval("VoucherNumber")) %></ItemTemplate>
                    </asp:TemplateField>

                    <asp:TemplateField HeaderText="จัดการ">
                        <ItemTemplate>
                            <div class="action-cell">
                                <asp:Button ID="btnEdit" runat="server" Text="&#9998;" ToolTip="แก้ไข"
                                    CssClass="btn-edit" CommandName="EditPayroll" CommandArgument='<%# Eval("ID") %>' />
                                <asp:Button ID="btnPay" runat="server" Text="&#128179;" ToolTip="ทำจ่าย"
                                    CssClass="btn-pay" CommandName="ProcessPayment" CommandArgument='<%# Eval("ID") %>'
                                    Visible='<%# Eval("VoucherGenerated") == DBNull.Value || !Convert.ToBoolean(Eval("VoucherGenerated")) %>' />
                            </div>
                        </ItemTemplate>
                    </asp:TemplateField>
                </Columns>
                <EmptyDataTemplate>
                    <div style="text-align: center; padding: 40px; color: #999;">
                        &#128230; ยังไม่มีข้อมูลเงินเดือนสำหรับงวดนี้<br />
                        กรุณาคลิก "สร้างรอบเงินเดือน" เพื่อสร้างข้อมูล
                    </div>
                </EmptyDataTemplate>
            </asp:GridView>
        </div>

        <!-- Hidden Fields -->
        <asp:HiddenField ID="hdnPayrollRecordId" runat="server" />
        <asp:HiddenField ID="hdnPayrollPeriodId" runat="server" />

        <!-- Edit Payroll Modal -->
        <div id="editModal" class="modal-overlay">
            <div class="modal-content">
                <div class="modal-header">
                    <h3>&#9998; แก้ไขข้อมูลเงินเดือน</h3>
                    <button type="button" class="modal-close" onclick="closeEditModal()">&times;</button>
                </div>

                <div class="form-group">
                    <label>พนักงาน</label>
                    <asp:Label ID="lblEditEmployeeName" runat="server" CssClass="form-control" style="background: #f0f0f0;"></asp:Label>
                </div>

                <div class="form-section">
                    <h4>&#128176; รายได้</h4>
                    <div class="form-row">
                        <div class="form-group">
                            <label>เงินเดือน (บาท)</label>
                            <asp:TextBox ID="txtEditBaseSalary" runat="server" CssClass="form-control" TextMode="Number" onchange="calculateTotals()"></asp:TextBox>
                        </div>
                        <div class="form-group">
                            <label>OT (บาท)</label>
                            <asp:TextBox ID="txtEditOTAmount" runat="server" CssClass="form-control" TextMode="Number" onchange="calculateTotals()"></asp:TextBox>
                        </div>
                    </div>
                    <div class="form-row">
                        <div class="form-group">
                            <label>โบนัส (บาท)</label>
                            <asp:TextBox ID="txtEditBonus" runat="server" CssClass="form-control" TextMode="Number" onchange="calculateTotals()"></asp:TextBox>
                        </div>
                        <div class="form-group">
                            <label>เบี้ยขยัน/ค่าเดินทาง (บาท)</label>
                            <asp:TextBox ID="txtEditAllowance" runat="server" CssClass="form-control" TextMode="Number" onchange="calculateTotals()"></asp:TextBox>
                        </div>
                    </div>
                </div>

                <div class="form-section">
                    <h4>&#128179; รายการหัก</h4>
                    <div class="form-row">
                        <div class="form-group">
                            <label>ประกันสังคม (บาท)</label>
                            <asp:TextBox ID="txtEditSocialSecurity" runat="server" CssClass="form-control" TextMode="Number" onchange="calculateTotals()"></asp:TextBox>
                            <small style="color: #666;">5% ของเงินเดือน สูงสุด 750 บาท</small>
                        </div>
                        <div class="form-group">
                            <label>หักขาด/ลา (บาท)</label>
                            <asp:TextBox ID="txtEditLeaveDeduction" runat="server" CssClass="form-control" TextMode="Number" onchange="calculateTotals()"></asp:TextBox>
                        </div>
                    </div>
                    <div class="form-row">
                        <div class="form-group">
                            <label>ภาษี ณ ที่จ่าย (บาท)</label>
                            <asp:TextBox ID="txtEditTax" runat="server" CssClass="form-control" TextMode="Number" onchange="calculateTotals()"></asp:TextBox>
                        </div>
                        <div class="form-group">
                            <label>หักอื่นๆ (บาท)</label>
                            <asp:TextBox ID="txtEditOtherDeductions" runat="server" CssClass="form-control" TextMode="Number" onchange="calculateTotals()"></asp:TextBox>
                        </div>
                    </div>
                </div>

                <div class="form-section" style="background: #e8f5e9;">
                    <h4>&#128178; สรุป</h4>
                    <div class="summary-row">
                        <span>รายได้รวม:</span>
                        <span id="summaryEarnings">0.00</span>
                    </div>
                    <div class="summary-row">
                        <span>หักรวม:</span>
                        <span id="summaryDeductions">0.00</span>
                    </div>
                    <div class="summary-row total">
                        <span>เงินสุทธิ:</span>
                        <span id="summaryNet" style="color: #11998e;">0.00</span>
                    </div>
                </div>

                <div class="modal-footer">
                    <button type="button" class="btn btn-cancel" onclick="closeEditModal()">ยกเลิก</button>
                    <button type="button" class="btn btn-info" onclick="calculateSS()">&#128260; คำนวณ ปกส.</button>
                    <asp:Button ID="btnSavePayroll" runat="server" Text="&#128190; บันทึก" CssClass="btn btn-success" OnClick="btnSavePayroll_Click" />
                </div>
            </div>
        </div>
    </div>

    <script type="text/javascript">
        function openEditModal(recordId, employeeName, baseSalary, otAmount, bonus, allowance, ss, leaveDeduction, tax, otherDeductions) {
            document.getElementById('<%= hdnPayrollRecordId.ClientID %>').value = recordId;
            document.getElementById('<%= lblEditEmployeeName.ClientID %>').innerText = employeeName;
            document.getElementById('<%= txtEditBaseSalary.ClientID %>').value = baseSalary || 0;
            document.getElementById('<%= txtEditOTAmount.ClientID %>').value = otAmount || 0;
            document.getElementById('<%= txtEditBonus.ClientID %>').value = bonus || 0;
            document.getElementById('<%= txtEditAllowance.ClientID %>').value = allowance || 0;
            document.getElementById('<%= txtEditSocialSecurity.ClientID %>').value = ss || 0;
            document.getElementById('<%= txtEditLeaveDeduction.ClientID %>').value = leaveDeduction || 0;
            document.getElementById('<%= txtEditTax.ClientID %>').value = tax || 0;
            document.getElementById('<%= txtEditOtherDeductions.ClientID %>').value = otherDeductions || 0;

            calculateTotals();
            document.getElementById('editModal').style.display = 'flex';
        }

        function closeEditModal() {
            document.getElementById('editModal').style.display = 'none';
        }

        function calculateTotals() {
            var baseSalary = parseFloat(document.getElementById('<%= txtEditBaseSalary.ClientID %>').value) || 0;
            var otAmount = parseFloat(document.getElementById('<%= txtEditOTAmount.ClientID %>').value) || 0;
            var bonus = parseFloat(document.getElementById('<%= txtEditBonus.ClientID %>').value) || 0;
            var allowance = parseFloat(document.getElementById('<%= txtEditAllowance.ClientID %>').value) || 0;

            var ss = parseFloat(document.getElementById('<%= txtEditSocialSecurity.ClientID %>').value) || 0;
            var leaveDeduction = parseFloat(document.getElementById('<%= txtEditLeaveDeduction.ClientID %>').value) || 0;
            var tax = parseFloat(document.getElementById('<%= txtEditTax.ClientID %>').value) || 0;
            var otherDeductions = parseFloat(document.getElementById('<%= txtEditOtherDeductions.ClientID %>').value) || 0;

            var totalEarnings = baseSalary + otAmount + bonus + allowance;
            var totalDeductions = ss + leaveDeduction + tax + otherDeductions;
            var netSalary = totalEarnings - totalDeductions;

            document.getElementById('summaryEarnings').innerText = totalEarnings.toLocaleString('th-TH', {minimumFractionDigits: 2});
            document.getElementById('summaryDeductions').innerText = totalDeductions.toLocaleString('th-TH', {minimumFractionDigits: 2});
            document.getElementById('summaryNet').innerText = netSalary.toLocaleString('th-TH', {minimumFractionDigits: 2});
        }

        function calculateSS() {
            var baseSalary = parseFloat(document.getElementById('<%= txtEditBaseSalary.ClientID %>').value) || 0;

            // Social Security: 5% of salary, min base 1650, max base 15000, max deduction 750
            var ssBase = Math.max(1650, Math.min(15000, baseSalary));
            var ss = ssBase * 0.05;
            ss = Math.min(ss, 750);
            ss = Math.round(ss);

            document.getElementById('<%= txtEditSocialSecurity.ClientID %>').value = ss;
            calculateTotals();
        }

        // Close modal when clicking outside
        window.onclick = function (event) {
            if (event.target.className === 'modal-overlay') {
                event.target.style.display = 'none';
            }
        }
    </script>
</asp:Content>
