<%@ Page Title="ประวัติพนักงาน" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="EmployeeProfile.aspx.cs" Inherits="Take_Time_BangPhra.Admin.HR.EmployeeProfile" MaintainScrollPositionOnPostback="true" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .employee-profile {
            padding: 20px;
        }

        .section-header {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
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

        .profile-container {
            display: grid;
            grid-template-columns: 300px 1fr;
            gap: 20px;
        }

        @media (max-width: 768px) {
            .profile-container {
                grid-template-columns: 1fr;
            }
        }

        .profile-photo-section {
            background: white;
            padding: 20px;
            border-radius: 8px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
            text-align: center;
        }

        .profile-photo {
            width: 200px;
            height: 200px;
            border-radius: 50%;
            object-fit: cover;
            margin-bottom: 15px;
            border: 4px solid #667eea;
        }

        .profile-name {
            font-size: 20px;
            font-weight: 600;
            color: #333;
            margin-bottom: 5px;
        }

        .profile-position {
            color: #666;
            font-size: 14px;
            margin-bottom: 15px;
        }

        .status-badge {
            display: inline-block;
            padding: 4px 12px;
            border-radius: 20px;
            font-size: 12px;
            font-weight: 500;
        }

        .status-active {
            background: #d4edda;
            color: #155724;
        }

        .status-inactive {
            background: #f8d7da;
            color: #721c24;
        }

        .profile-details {
            background: white;
            border-radius: 8px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
        }

        .tabs {
            display: flex;
            border-bottom: 2px solid #e0e0e0;
        }

        .tab {
            padding: 15px 25px;
            cursor: pointer;
            border: none;
            background: none;
            font-size: 14px;
            font-weight: 500;
            color: #666;
            border-bottom: 2px solid transparent;
            margin-bottom: -2px;
            transition: all 0.2s;
        }

        .tab:hover {
            color: #667eea;
        }

        .tab.active {
            color: #667eea;
            border-bottom-color: #667eea;
        }

        .tab-content {
            padding: 20px;
        }

        .info-group {
            margin-bottom: 20px;
        }

        .info-group h4 {
            color: #667eea;
            font-size: 14px;
            margin-bottom: 15px;
            padding-bottom: 8px;
            border-bottom: 1px solid #e0e0e0;
        }

        .info-row {
            display: grid;
            grid-template-columns: 150px 1fr;
            margin-bottom: 10px;
        }

        .info-label {
            color: #666;
            font-size: 13px;
        }

        .info-value {
            color: #333;
            font-weight: 500;
        }

        .data-table {
            width: 100%;
            border-collapse: collapse;
        }

        .data-table th {
            background: #f8f9fa;
            padding: 12px;
            text-align: left;
            font-weight: 500;
            color: #666;
            font-size: 13px;
        }

        .data-table td {
            padding: 12px;
            border-bottom: 1px solid #f0f0f0;
        }

        .btn-edit {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            border: none;
            color: white;
            padding: 10px 25px;
            border-radius: 6px;
            font-weight: 500;
            cursor: pointer;
        }

        .btn-edit:hover {
            box-shadow: 0 4px 12px rgba(102, 126, 234, 0.4);
        }

        .quick-stats {
            display: grid;
            grid-template-columns: repeat(3, 1fr);
            gap: 10px;
            margin-top: 20px;
        }

        .quick-stat {
            text-align: center;
            padding: 10px;
            background: #f8f9fa;
            border-radius: 6px;
        }

        .quick-stat-value {
            font-size: 18px;
            font-weight: 700;
            color: #667eea;
        }

        .quick-stat-label {
            font-size: 11px;
            color: #666;
        }
    </style>

    <div class="employee-profile">
        <div class="section-header">
            <h2>ประวัติพนักงาน</h2>
            <a href="EmployeeManagement.aspx" class="btn-back">กลับไปรายการ</a>
        </div>

        <div class="profile-container">
            <!-- Photo & Quick Info -->
            <div class="profile-photo-section">
                <img src='<%= GetProfilePhoto() %>' alt="Photo" class="profile-photo" />
                <div class="profile-name"><asp:Label ID="lblName" runat="server"></asp:Label></div>
                <div class="profile-position"><asp:Label ID="lblPosition" runat="server"></asp:Label></div>
                <asp:Label ID="lblStatus" runat="server" CssClass="status-badge status-active"></asp:Label>

                <div class="quick-stats">
                    <div class="quick-stat">
                        <div class="quick-stat-value"><asp:Label ID="lblServiceYears" runat="server" Text="0"></asp:Label></div>
                        <div class="quick-stat-label">ปีทำงาน</div>
                    </div>
                    <div class="quick-stat">
                        <div class="quick-stat-value"><asp:Label ID="lblLeaveBalance" runat="server" Text="0"></asp:Label></div>
                        <div class="quick-stat-label">วันลาคงเหลือ</div>
                    </div>
                    <div class="quick-stat">
                        <div class="quick-stat-value"><asp:Label ID="lblTrainingCount" runat="server" Text="0"></asp:Label></div>
                        <div class="quick-stat-label">หลักสูตรอบรม</div>
                    </div>
                </div>
            </div>

            <!-- Hidden field for tab state -->
            <asp:HiddenField ID="hdnActiveTab" runat="server" Value="personal" />

            <!-- Details Tabs -->
            <div class="profile-details">
                <div class="tabs">
                    <button type="button" class="tab active" onclick="showTab('personal')">ข้อมูลส่วนตัว</button>
                    <button type="button" class="tab" onclick="showTab('employment')">ประวัติการทำงาน</button>
                    <button type="button" class="tab" onclick="showTab('salary')">เงินเดือน</button>
                    <button type="button" class="tab" onclick="showTab('leave')">การลา</button>
                    <button type="button" class="tab" onclick="showTab('training')">การอบรม</button>
                    <button type="button" class="tab" onclick="showTab('signature')">ลายเซ็น</button>
                </div>

                <!-- Personal Info Tab -->
                <div id="tab-personal" class="tab-content">
                    <div class="info-group">
                        <h4>ข้อมูลพื้นฐาน</h4>
                        <div class="info-row">
                            <span class="info-label">รหัสพนักงาน:</span>
                            <span class="info-value"><asp:Label ID="lblEmployeeCode" runat="server"></asp:Label></span>
                        </div>
                        <div class="info-row">
                            <span class="info-label">ชื่อ-นามสกุล (ไทย):</span>
                            <span class="info-value"><asp:Label ID="lblFullNameTH" runat="server"></asp:Label></span>
                        </div>
                        <div class="info-row">
                            <span class="info-label">ชื่อ-นามสกุล (EN):</span>
                            <span class="info-value"><asp:Label ID="lblFullNameEN" runat="server"></asp:Label></span>
                        </div>
                        <div class="info-row">
                            <span class="info-label">เลขบัตรประชาชน:</span>
                            <span class="info-value"><asp:Label ID="lblIDCard" runat="server"></asp:Label></span>
                        </div>
                        <div class="info-row">
                            <span class="info-label">วันเกิด:</span>
                            <span class="info-value"><asp:Label ID="lblBirthDate" runat="server"></asp:Label></span>
                        </div>
                    </div>

                    <div class="info-group">
                        <h4>ข้อมูลติดต่อ</h4>
                        <div class="info-row">
                            <span class="info-label">โทรศัพท์:</span>
                            <span class="info-value"><asp:Label ID="lblPhone" runat="server"></asp:Label></span>
                        </div>
                        <div class="info-row">
                            <span class="info-label">อีเมล:</span>
                            <span class="info-value"><asp:Label ID="lblEmail" runat="server"></asp:Label></span>
                        </div>
                        <div class="info-row">
                            <span class="info-label">ที่อยู่:</span>
                            <span class="info-value"><asp:Label ID="lblAddress" runat="server"></asp:Label></span>
                        </div>
                    </div>

                    <div class="info-group">
                        <h4>ผู้ติดต่อฉุกเฉิน</h4>
                        <div class="info-row">
                            <span class="info-label">ชื่อผู้ติดต่อ:</span>
                            <span class="info-value"><asp:Label ID="lblEmergencyContact" runat="server"></asp:Label></span>
                        </div>
                        <div class="info-row">
                            <span class="info-label">เบอร์โทร:</span>
                            <span class="info-value"><asp:Label ID="lblEmergencyPhone" runat="server"></asp:Label></span>
                        </div>
                    </div>
                </div>

                <!-- Employment Tab -->
                <div id="tab-employment" class="tab-content" style="display: none;">
                    <div class="info-group">
                        <h4>ข้อมูลการจ้างงาน</h4>
                        <div class="info-row">
                            <span class="info-label">วันที่เริ่มงาน:</span>
                            <span class="info-value"><asp:Label ID="lblStartDate" runat="server"></asp:Label></span>
                        </div>
                        <div class="info-row">
                            <span class="info-label">แผนก:</span>
                            <span class="info-value"><asp:Label ID="lblDepartment" runat="server"></asp:Label></span>
                        </div>
                        <div class="info-row">
                            <span class="info-label">ตำแหน่งปัจจุบัน:</span>
                            <span class="info-value"><asp:Label ID="lblCurrentPosition" runat="server"></asp:Label></span>
                        </div>
                        <div class="info-row">
                            <span class="info-label">ประเภทสัญญา:</span>
                            <span class="info-value"><asp:Label ID="lblContractType" runat="server"></asp:Label></span>
                        </div>
                        <div class="info-row">
                            <span class="info-label">สิ้นสุดสัญญา:</span>
                            <span class="info-value"><asp:Label ID="lblContractEnd" runat="server"></asp:Label></span>
                        </div>
                    </div>

                    <div class="info-group">
                        <h4>ประวัติการทำงาน</h4>
                        <asp:GridView ID="gvEmploymentHistory" runat="server" AutoGenerateColumns="False"
                            CssClass="data-table" GridLines="None">
                            <Columns>
                                <asp:BoundField DataField="EventType" HeaderText="ประเภท" />
                                <asp:TemplateField HeaderText="วันที่">
                                    <ItemTemplate>
                                        <%# Eval("EventDate") != DBNull.Value ? Convert.ToDateTime(Eval("EventDate")).ToString("dd/MM/yyyy") : "-" %>
                                    </ItemTemplate>
                                </asp:TemplateField>
                                <asp:BoundField DataField="Description" HeaderText="รายละเอียด" />
                            </Columns>
                            <EmptyDataTemplate>
                                <div style="text-align: center; padding: 20px; color: #999;">ไม่มีประวัติ</div>
                            </EmptyDataTemplate>
                        </asp:GridView>
                    </div>
                </div>

                <!-- Salary Tab -->
                <div id="tab-salary" class="tab-content" style="display: none;">
                    <div class="info-group">
                        <h4>ข้อมูลเงินเดือนปัจจุบัน</h4>
                        <div class="info-row">
                            <span class="info-label">เงินเดือน:</span>
                            <span class="info-value">฿<asp:Label ID="lblSalary" runat="server" Text="0.00"></asp:Label></span>
                        </div>
                        <div class="info-row">
                            <span class="info-label">มีผลตั้งแต่:</span>
                            <span class="info-value"><asp:Label ID="lblSalaryEffective" runat="server"></asp:Label></span>
                        </div>
                    </div>

                    <div class="info-group">
                        <h4>ประวัติการปรับเงินเดือน</h4>
                        <asp:GridView ID="gvSalaryHistory" runat="server" AutoGenerateColumns="False"
                            CssClass="data-table" GridLines="None">
                            <Columns>
                                <asp:TemplateField HeaderText="วันที่มีผล">
                                    <ItemTemplate>
                                        <%# Eval("EffectiveDate") != DBNull.Value ? Convert.ToDateTime(Eval("EffectiveDate")).ToString("dd/MM/yyyy") : "-" %>
                                    </ItemTemplate>
                                </asp:TemplateField>
                                <asp:TemplateField HeaderText="เงินเดือน">
                                    <ItemTemplate>
                                        ฿<%# string.Format("{0:N2}", Eval("MonthlySalary")) %>
                                    </ItemTemplate>
                                </asp:TemplateField>
                                <asp:BoundField DataField="Position" HeaderText="ตำแหน่ง" />
                            </Columns>
                            <EmptyDataTemplate>
                                <div style="text-align: center; padding: 20px; color: #999;">ไม่มีประวัติ</div>
                            </EmptyDataTemplate>
                        </asp:GridView>
                    </div>
                </div>

                <!-- Leave Tab -->
                <div id="tab-leave" class="tab-content" style="display: none;">
                    <div class="info-group">
                        <h4>สรุปวันลาปีนี้</h4>
                        <asp:GridView ID="gvLeaveQuota" runat="server" AutoGenerateColumns="False"
                            CssClass="data-table" GridLines="None">
                            <Columns>
                                <asp:BoundField DataField="LeaveTypeName" HeaderText="ประเภทการลา" />
                                <asp:BoundField DataField="TotalDays" HeaderText="วันลาทั้งหมด" />
                                <asp:BoundField DataField="UsedDays" HeaderText="ใช้ไปแล้ว" />
                                <asp:BoundField DataField="RemainingDays" HeaderText="คงเหลือ" />
                            </Columns>
                            <EmptyDataTemplate>
                                <div style="text-align: center; padding: 20px; color: #999;">ไม่มีข้อมูลวันลา</div>
                            </EmptyDataTemplate>
                        </asp:GridView>
                    </div>
                </div>

                <!-- Training Tab -->
                <div id="tab-training" class="tab-content" style="display: none;">
                    <div class="info-group">
                        <h4>ประวัติการอบรม</h4>
                        <asp:GridView ID="gvTraining" runat="server" AutoGenerateColumns="False"
                            CssClass="data-table" GridLines="None">
                            <Columns>
                                <asp:BoundField DataField="CourseName" HeaderText="หลักสูตร" />
                                <asp:TemplateField HeaderText="วันที่อบรม">
                                    <ItemTemplate>
                                        <%# Eval("TrainingDate") != DBNull.Value ? Convert.ToDateTime(Eval("TrainingDate")).ToString("dd/MM/yyyy") : "-" %>
                                    </ItemTemplate>
                                </asp:TemplateField>
                                <asp:BoundField DataField="Provider" HeaderText="ผู้จัด" />
                                <asp:BoundField DataField="Status" HeaderText="สถานะ" />
                            </Columns>
                            <EmptyDataTemplate>
                                <div style="text-align: center; padding: 20px; color: #999;">ไม่มีประวัติการอบรม</div>
                            </EmptyDataTemplate>
                        </asp:GridView>
                    </div>
                </div>

                <!-- Signature Tab -->
                <div id="tab-signature" class="tab-content" style="display: none;">
                    <div class="info-group">
                        <h4>ลายเซ็นพนักงาน</h4>
                        <p style="color: #666; font-size: 13px; margin-bottom: 20px;">
                            ลายเซ็นนี้จะใช้แสดงในเอกสาร PDF ทุกประเภท เช่น ใบเสร็จ, ใบสำคัญจ่าย, ใบจอง เป็นต้น
                        </p>

                        <div class="signature-container" style="text-align: center; padding: 20px;">
                            <!-- Current Signature Display -->
                            <div id="currentSignatureSection" runat="server" style="margin-bottom: 20px;">
                                <asp:Image ID="imgCurrentSignature" runat="server"
                                    Style="max-width: 300px; max-height: 150px; border: 2px dashed #ddd; border-radius: 8px; padding: 10px; background: #fafafa;" />
                                <div style="margin-top: 10px; color: #666; font-size: 12px;">
                                    <asp:Label ID="lblSignatureStatus" runat="server" Text="ลายเซ็นปัจจุบัน"></asp:Label>
                                </div>
                            </div>

                            <!-- No Signature Placeholder -->
                            <div id="noSignatureSection" runat="server" visible="false"
                                style="padding: 40px; border: 2px dashed #ddd; border-radius: 8px; background: #fafafa; margin-bottom: 20px;">
                                <i class="fas fa-signature" style="font-size: 48px; color: #ccc;"></i>
                                <p style="color: #999; margin-top: 10px;">ยังไม่มีลายเซ็น</p>
                            </div>

                            <!-- Upload Section -->
                            <div class="upload-section" style="padding: 20px; background: #f8f9fa; border-radius: 8px;">
                                <h5 style="color: #667eea; margin-bottom: 15px;">อัพโหลดลายเซ็นใหม่</h5>
                                <asp:FileUpload ID="fuSignature" runat="server"
                                    accept=".png,.jpg,.jpeg"
                                    Style="margin-bottom: 15px;" />
                                <div style="font-size: 12px; color: #999; margin-bottom: 15px;">
                                    รองรับไฟล์ PNG, JPG, JPEG ขนาดไม่เกิน 2MB<br/>
                                    แนะนำ: ลายเซ็นบนพื้นหลังสีขาวหรือโปร่งใส
                                </div>
                                <asp:Button ID="btnUploadSignature" runat="server" Text="อัพโหลด"
                                    CssClass="btn-edit" OnClick="btnUploadSignature_Click" />
                                <asp:Button ID="btnDeleteSignature" runat="server" Text="ลบลายเซ็น"
                                    CssClass="btn-delete" OnClick="btnDeleteSignature_Click"
                                    OnClientClick="return confirm('คุณต้องการลบลายเซ็นนี้หรือไม่?');"
                                    Style="background: #dc3545; margin-left: 10px;" />
                            </div>

                            <!-- Message -->
                            <asp:Label ID="lblSignatureMessage" runat="server"
                                Style="display: block; margin-top: 15px; padding: 10px; border-radius: 5px;"></asp:Label>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    </div>

    <script type="text/javascript">
        var tabNames = ['personal', 'employment', 'salary', 'leave', 'training', 'signature'];

        function showTab(tabName) {
            // Hide all tabs
            document.querySelectorAll('.tab-content').forEach(function(tab) {
                tab.style.display = 'none';
            });

            // Remove active from all buttons
            document.querySelectorAll('.tabs .tab').forEach(function(btn) {
                btn.classList.remove('active');
            });

            // Show selected tab
            var tabElement = document.getElementById('tab-' + tabName);
            if (tabElement) {
                tabElement.style.display = 'block';
            }

            // Set active button
            var tabIndex = tabNames.indexOf(tabName);
            if (tabIndex >= 0) {
                var buttons = document.querySelectorAll('.tabs .tab');
                if (buttons[tabIndex]) {
                    buttons[tabIndex].classList.add('active');
                }
            }

            // Save to hidden field
            var hdnTab = document.getElementById('<%= hdnActiveTab.ClientID %>');
            if (hdnTab) hdnTab.value = tabName;
        }

        // Restore tab on page load
        document.addEventListener('DOMContentLoaded', function () {
            var hdnTab = document.getElementById('<%= hdnActiveTab.ClientID %>');
            if (hdnTab && hdnTab.value) {
                showTab(hdnTab.value);
            }
        });
    </script>
</asp:Content>
