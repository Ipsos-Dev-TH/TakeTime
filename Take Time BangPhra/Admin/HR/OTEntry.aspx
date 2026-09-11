<%@ Page Title="บันทึกชั่วโมง OT" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="OTEntry.aspx.cs" Inherits="Take_Time_BangPhra.Admin.HR.OTEntry" MaintainScrollPositionOnPostback="true" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .ot-entry {
            padding: 20px;
            max-width: 1400px;
            margin: 0 auto;
        }

        .section-header {
            background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%);
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

        .stats-container {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 15px;
            margin-bottom: 20px;
        }

        .stat-card {
            background: white;
            padding: 20px;
            border-radius: 8px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
            border-left: 4px solid #f093fb;
        }

        .stat-card.warning { border-left-color: #f5576c; }
        .stat-card.success { border-left-color: #11998e; }
        .stat-card.info { border-left-color: #4facfe; }

        .stat-card h4 {
            margin: 0 0 8px 0;
            font-size: 13px;
            color: #666;
        }

        .stat-card .value {
            font-size: 28px;
            font-weight: 700;
            color: #333;
        }

        .form-section {
            background: white;
            padding: 25px;
            border-radius: 8px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
            margin-bottom: 20px;
        }

        .form-section h3 {
            margin: 0 0 20px 0;
            padding-bottom: 10px;
            border-bottom: 2px solid #f0f0f0;
            color: #333;
        }

        .form-row {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 20px;
            margin-bottom: 15px;
        }

        .form-group {
            margin-bottom: 15px;
        }

        .form-group label {
            display: block;
            margin-bottom: 5px;
            font-weight: 500;
            color: #333;
        }

        .form-group label .required {
            color: #dc3545;
        }

        .form-control {
            width: 100%;
            padding: 10px 15px;
            border: 2px solid #e0e0e0;
            border-radius: 6px;
            font-size: 14px;
            box-sizing: border-box;
            min-height: 44px;
            line-height: 1.6;
        }

        .form-control:focus {
            border-color: #f093fb;
            outline: none;
        }

        select.form-control {
            height: auto;
            min-height: 44px;
        }

        textarea.form-control {
            min-height: 80px;
            resize: vertical;
        }

        .btn {
            padding: 12px 25px;
            border: none;
            border-radius: 6px;
            font-weight: 500;
            cursor: pointer;
            font-size: 14px;
            transition: all 0.3s;
        }

        .btn-primary {
            background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%);
            color: white;
        }

        .btn-success {
            background: linear-gradient(135deg, #11998e 0%, #38ef7d 100%);
            color: white;
        }

        .btn-danger {
            background: #dc3545;
            color: white;
        }

        .btn-secondary {
            background: #6c757d;
            color: white;
        }

        .btn-sm {
            padding: 6px 12px;
            font-size: 12px;
        }

        .btn:hover {
            transform: translateY(-2px);
            box-shadow: 0 4px 12px rgba(0,0,0,0.2);
        }

        .btn-group {
            display: flex;
            gap: 10px;
            margin-top: 20px;
        }

        .data-table {
            background: white;
            border-radius: 8px;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
            /* ⚠ เดิมเป็น overflow:hidden — ตารางถูกตัดทิ้งที่ขอบจอ เลื่อนดูก็ไม่ได้
               (ใส่ไว้เพื่อให้มุมโค้งไม่โดนตารางทับ แต่แลกมาด้วยข้อมูลที่อ่านไม่ได้) */
            overflow-x: auto;
            -webkit-overflow-scrolling: touch;
        }

        .data-table table {
            width: 100%;
            border-collapse: collapse;
            /* ไม่มี min-width มาก่อน ⇒ คอลัมน์ถูกบีบจนตัวอักษรไทยตกบรรทัดทีละตัว */
            min-width: 720px;
        }

        .data-table th {
            background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%);
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

        /* บนมือถือใช้พื้นที่ให้เต็ม — กินขอบซ้ายขวาของ padding หน้าออกไป
           (ตัวตารางเปลี่ยนเป็นการ์ดโดยระบบกลางใน Site.Master) */
        @media (max-width: 820px) {
            .data-table {
                background: transparent;
                box-shadow: none;
                border-radius: 0;
                margin-left: -12px;
                margin-right: -12px;
                overflow-x: visible;
            }
            .data-table table { min-width: 0; }
            .filter-row { flex-direction: column; align-items: stretch; }
            .filter-group { width: 100%; }
            .btn-group { flex-wrap: wrap; }
            .btn-group .btn, .btn-group input[type=submit] { flex: 1 1 auto; }
        }

        .badge {
            display: inline-block;
            padding: 4px 8px;
            border-radius: 4px;
            font-size: 11px;
            font-weight: 500;
        }

        .badge-pending { background: #fff3cd; color: #856404; }
        .badge-approved { background: #d4edda; color: #155724; }
        .badge-rejected { background: #f8d7da; color: #721c24; }

        .alert {
            padding: 15px;
            border-radius: 6px;
            margin-bottom: 20px;
        }

        .alert-success { background: #d4edda; color: #155724; border: 1px solid #c3e6cb; }
        .alert-error { background: #f8d7da; color: #721c24; border: 1px solid #f5c6cb; }
        .alert-info { background: #cce5ff; color: #004085; border: 1px solid #b8daff; }

        .supervisor-mode {
            background: #fff3cd;
            border: 2px solid #ffc107;
            padding: 15px;
            border-radius: 6px;
            margin-bottom: 20px;
        }

        .supervisor-mode h4 {
            margin: 0 0 10px 0;
            color: #856404;
        }

        .filter-controls {
            display: flex;
            gap: 10px;
            flex-wrap: wrap;
            align-items: center;
            margin-bottom: 15px;
        }

        .tabs {
            display: flex;
            border-bottom: 2px solid #f0f0f0;
            margin-bottom: 20px;
        }

        .tab {
            padding: 10px 20px;
            cursor: pointer;
            border-bottom: 3px solid transparent;
            color: #666;
            font-weight: 500;
        }

        .tab.active {
            color: #f093fb;
            border-bottom-color: #f093fb;
        }

        .tab-content {
            display: none;
        }

        .tab-content.active {
            display: block;
        }
    </style>

    <div class="ot-entry">
        <div class="section-header">
            <h2>&#128337; บันทึกชั่วโมง OT</h2>
        </div>

        <!-- Hidden field for tab state -->
        <asp:HiddenField ID="hdnActiveTab" runat="server" Value="entry" />

        <!-- Message Panel -->
        <asp:Panel ID="pnlMessage" runat="server" Visible="false" CssClass="alert">
            <asp:Label ID="lblMessage" runat="server"></asp:Label>
        </asp:Panel>

        <!-- Statistics -->
        <div class="stats-container">
            <div class="stat-card">
                <h4>&#9989; OT อนุมัติแล้ว (เดือนนี้)</h4>
                <div class="value">
                    <asp:Label ID="lblApprovedHours" runat="server" Text="0"></asp:Label>
                    <span style="font-size: 14px; font-weight: normal;">ชม.</span>
                </div>
            </div>
            <div class="stat-card warning">
                <h4>&#128336; OT รออนุมัติ</h4>
                <div class="value">
                    <asp:Label ID="lblPendingHours" runat="server" Text="0"></asp:Label>
                    <span style="font-size: 14px; font-weight: normal;">ชม.</span>
                </div>
            </div>
            <div class="stat-card info">
                <h4>&#128101; ลูกน้อง OT รออนุมัติ</h4>
                <div class="value">
                    <asp:Label ID="lblSubordinatePending" runat="server" Text="0"></asp:Label>
                    <span style="font-size: 14px; font-weight: normal;">รายการ</span>
                </div>
            </div>
        </div>

        <!-- Tabs -->
        <div class="tabs">
            <div class="tab active" onclick="showTab('entry')">&#128221; บันทึก OT</div>
            <div class="tab" onclick="showTab('myot')">&#128203; OT ของฉัน</div>
            <asp:Panel ID="pnlApprovalTab" runat="server">
                <div class="tab" onclick="showTab('approval')">&#9989; อนุมัติ OT ลูกน้อง</div>
            </asp:Panel>
        </div>

        <!-- Tab: Entry Form -->
        <div id="tabEntry" class="tab-content active">
            <div class="form-section">
                <h3>&#128221; บันทึกชั่วโมง OT</h3>

                <%-- ══ "บันทึกให้ใคร" เป็นฟิลด์แรกของฟอร์ม ══
                     เดิมอยู่ในกล่องสีเหลืองแยกต่างหากเหนือฟอร์ม และค่าตั้งต้นคือ "ให้ตัวเอง"
                     ⇒ หัวหน้างานตั้งใจลงให้ลูกน้องแต่ลืมเลือก กลายเป็นลงให้ตัวเองบ่อย ๆ
                     ตอนนี้: อยู่ในฟอร์ม + แถบสรุปชื่อคนที่จะได้ OT เปลี่ยนสีตามที่เลือก --%>
                <asp:Panel ID="pnlSupervisorMode" runat="server" Visible="false"
                    CssClass="form-group" style="margin-bottom:18px;">
                    <label style="font-weight:700; font-size:15px;">
                        บันทึก OT ให้ใคร <span class="required">*</span>
                    </label>
                    <asp:DropDownList ID="ddlEmployee" runat="server" CssClass="form-control"
                        style="max-width:460px; font-size:15px;"
                        onchange="otUpdateWhoBanner()">
                    </asp:DropDownList>

                    <div id="otWhoBanner" style="margin-top:12px; padding:14px 16px; border-radius:10px;
                         font-size:15.5px; line-height:1.6; border-left:5px solid transparent;">
                    </div>
                </asp:Panel>

                <%-- ให้ JS รู้ว่า "ตัวเอง" คือ id ไหน/ชื่ออะไร (ใช้เทียบว่ากำลังลงให้คนอื่นอยู่ไหม) --%>
                <input type="hidden" id="otSelfAdminId" value="<%= SelfAdminIdJs %>" />
                <input type="hidden" id="otSelfName" value="<%= SelfNameJs %>" />

                <%-- คนที่ไม่ใช่หัวหน้างานเห็นแค่บรรทัดยืนยันว่าเป็นของตัวเอง --%>
                <asp:Panel ID="pnlSelfOnly" runat="server" Visible="false"
                    style="margin-bottom:18px; padding:12px 15px; border-radius:10px;
                           background:#E3F2FD; color:#1565C0; border-left:5px solid #1E88E5; font-size:15px;">
                    &#128100; บันทึก OT ของ <b><asp:Literal ID="litSelfName" runat="server" /></b>
                </asp:Panel>

                <div class="form-row">
                    <div class="form-group">
                        <label>วันที่ทำ OT <span class="required">*</span></label>
                        <asp:TextBox ID="txtOTDate" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                    </div>
                    <div class="form-group">
                        <label>จำนวนชั่วโมง OT <span class="required">*</span></label>
                        <asp:TextBox ID="txtOTHours" runat="server" CssClass="form-control" TextMode="Number" step="0.5" min="0.5" max="24" Text="1"></asp:TextBox>
                    </div>
                    <div class="form-group">
                        <label>อัตรา OT (เท่า)</label>
                        <asp:DropDownList ID="ddlOTRate" runat="server" CssClass="form-control">
                            <asp:ListItem Value="1">1 เท่า (ปกติ)</asp:ListItem>
                            <asp:ListItem Value="1.5" Selected="True">1.5 เท่า (ล่วงเวลา)</asp:ListItem>
                            <asp:ListItem Value="2">2 เท่า (วันหยุด)</asp:ListItem>
                        </asp:DropDownList>
                    </div>
                </div>
                <div class="form-group">
                    <label>รายละเอียดงานที่ทำ <span class="required">*</span></label>
                    <asp:TextBox ID="txtWorkDescription" runat="server" CssClass="form-control" TextMode="MultiLine" Rows="3" placeholder="อธิบายงานที่ทำระหว่าง OT..."></asp:TextBox>
                </div>
                <div class="form-group">
                    <label>หมายเหตุ (ถ้ามี)</label>
                    <asp:TextBox ID="txtNotes" runat="server" CssClass="form-control" placeholder="หมายเหตุเพิ่มเติม..."></asp:TextBox>
                </div>
                <div class="btn-group">
                    <asp:Button ID="btnSaveOT" runat="server" Text="&#128190; บันทึก OT" CssClass="btn btn-primary"
                        OnClick="btnSaveOT_Click" OnClientClick="return otConfirmSave();" />
                    <asp:Button ID="btnClear" runat="server" Text="&#128260; ล้างฟอร์ม" CssClass="btn btn-secondary" OnClick="btnClear_Click" CausesValidation="false" />
                </div>
            </div>
        </div>

        <!-- Tab: My OT -->
        <div id="tabMyOT" class="tab-content">
            <div class="form-section">
                <h3>&#128203; ประวัติ OT ของฉัน</h3>
                <div class="filter-controls">
                    <asp:DropDownList ID="ddlMyMonth" runat="server" CssClass="form-control" AutoPostBack="true" OnSelectedIndexChanged="ddlMyMonth_SelectedIndexChanged">
                    </asp:DropDownList>
                    <asp:DropDownList ID="ddlMyYear" runat="server" CssClass="form-control" AutoPostBack="true" OnSelectedIndexChanged="ddlMyYear_SelectedIndexChanged">
                    </asp:DropDownList>
                </div>
                <div class="data-table">
                    <asp:GridView ID="gvMyOT" runat="server" AutoGenerateColumns="False"
                        CssClass="table" GridLines="None" OnRowCommand="gvMyOT_RowCommand">
                        <Columns>
                            <asp:TemplateField HeaderText="วันที่">
                                <ItemTemplate>
                                    <%# Convert.ToDateTime(Eval("OTDate")).ToString("dd/MM/yyyy") %>
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="ชั่วโมง">
                                <ItemTemplate>
                                    <%# Eval("OTHours") %> ชม. x <%# Eval("OTRate") %> = <strong><%# string.Format("{0:N1}", Eval("OTMultipliedHours")) %></strong>
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:BoundField DataField="WorkDescription" HeaderText="รายละเอียดงาน" />
                            <asp:TemplateField HeaderText="สถานะ">
                                <ItemTemplate>
                                    <%# GetStatusBadge(Eval("Status").ToString()) %>
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:BoundField DataField="EnteredByName" HeaderText="บันทึกโดย" />
                            <asp:TemplateField HeaderText="จัดการ">
                                <ItemTemplate>
                                    <asp:Button ID="btnDelete" runat="server" Text="&#128465; ลบ"
                                        CssClass="btn btn-danger btn-sm" CommandName="DeleteOT"
                                        CommandArgument='<%# Eval("ID") %>'
                                        Visible='<%# Eval("Status").ToString() == "PENDING" %>'
                                        OnClientClick="return confirm('ยืนยันการลบรายการนี้?');" />
                                </ItemTemplate>
                            </asp:TemplateField>
                        </Columns>
                        <EmptyDataTemplate>
                            <div style="text-align: center; padding: 40px; color: #999;">
                                ยังไม่มีประวัติ OT
                            </div>
                        </EmptyDataTemplate>
                    </asp:GridView>
                </div>
            </div>
        </div>

        <!-- Tab: Approval -->
        <div id="tabApproval" class="tab-content">
            <div class="form-section">
                <h3>&#9989; อนุมัติ OT ลูกน้อง</h3>
                <div class="filter-controls">
                    <asp:DropDownList ID="ddlApprovalStatus" runat="server" CssClass="form-control" AutoPostBack="true" OnSelectedIndexChanged="ddlApprovalStatus_SelectedIndexChanged">
                        <asp:ListItem Value="PENDING">รออนุมัติ</asp:ListItem>
                        <asp:ListItem Value="">ทั้งหมด</asp:ListItem>
                        <asp:ListItem Value="APPROVED">อนุมัติแล้ว</asp:ListItem>
                        <asp:ListItem Value="REJECTED">ปฏิเสธ</asp:ListItem>
                    </asp:DropDownList>
                    <asp:DropDownList ID="ddlApprovalMonth" runat="server" CssClass="form-control" AutoPostBack="true" OnSelectedIndexChanged="ddlApprovalMonth_SelectedIndexChanged">
                    </asp:DropDownList>
                    <asp:DropDownList ID="ddlApprovalYear" runat="server" CssClass="form-control" AutoPostBack="true" OnSelectedIndexChanged="ddlApprovalYear_SelectedIndexChanged">
                    </asp:DropDownList>
                </div>
                <div class="data-table">
                    <asp:GridView ID="gvSubordinateOT" runat="server" AutoGenerateColumns="False"
                        CssClass="table" GridLines="None" OnRowCommand="gvSubordinateOT_RowCommand">
                        <Columns>
                            <asp:TemplateField HeaderText="พนักงาน">
                                <ItemTemplate>
                                    <strong><%# Eval("EmployeeName") %></strong><br />
                                    <small style="color: #666;"><%# Eval("EmployeePosition") %></small>
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="วันที่">
                                <ItemTemplate>
                                    <%# Convert.ToDateTime(Eval("OTDate")).ToString("dd/MM/yyyy") %>
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="ชั่วโมง">
                                <ItemTemplate>
                                    <%# Eval("OTHours") %> ชม. x <%# Eval("OTRate") %> = <strong><%# string.Format("{0:N1}", Eval("OTMultipliedHours")) %></strong>
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:BoundField DataField="WorkDescription" HeaderText="รายละเอียดงาน" />
                            <asp:TemplateField HeaderText="สถานะ">
                                <ItemTemplate>
                                    <%# GetStatusBadge(Eval("Status").ToString()) %>
                                </ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="จัดการ">
                                <ItemTemplate>
                                    <asp:Panel ID="pnlActions" runat="server" Visible='<%# Eval("Status").ToString() == "PENDING" %>'>
                                        <asp:Button ID="btnApprove" runat="server" Text="&#9989; อนุมัติ"
                                            CssClass="btn btn-success btn-sm" CommandName="ApproveOT"
                                            CommandArgument='<%# Eval("ID") %>'
                                            OnClientClick="return confirm('ยืนยันการอนุมัติ OT นี้?');" />
                                        <asp:Button ID="btnReject" runat="server" Text="&#10060; ปฏิเสธ"
                                            CssClass="btn btn-danger btn-sm" CommandName="RejectOT"
                                            CommandArgument='<%# Eval("ID") %>'
                                            OnClientClick="return confirm('ยืนยันการปฏิเสธ OT นี้?');" />
                                    </asp:Panel>
                                    <asp:Panel ID="pnlProcessed" runat="server" Visible='<%# Eval("Status").ToString() != "PENDING" %>'>
                                        <small style="color: #666;">
                                            <%# Eval("ApprovedByName") != DBNull.Value ? "โดย " + Eval("ApprovedByName") : "" %>
                                        </small>
                                    </asp:Panel>
                                </ItemTemplate>
                            </asp:TemplateField>
                        </Columns>
                        <EmptyDataTemplate>
                            <div style="text-align: center; padding: 40px; color: #999;">
                                &#128230; ไม่พบรายการ OT
                            </div>
                        </EmptyDataTemplate>
                    </asp:GridView>
                </div>
            </div>
        </div>
    </div>

    <script type="text/javascript">
        function showTab(tabName) {
            // Hide all tabs
            document.querySelectorAll('.tab-content').forEach(function (tab) {
                tab.classList.remove('active');
            });
            document.querySelectorAll('.tab').forEach(function (tab) {
                tab.classList.remove('active');
            });

            // Show selected tab
            if (tabName === 'entry') {
                document.getElementById('tabEntry').classList.add('active');
                document.querySelectorAll('.tab')[0].classList.add('active');
            } else if (tabName === 'myot') {
                document.getElementById('tabMyOT').classList.add('active');
                document.querySelectorAll('.tab')[1].classList.add('active');
            } else if (tabName === 'approval') {
                document.getElementById('tabApproval').classList.add('active');
                document.querySelectorAll('.tab')[2].classList.add('active');
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
            otUpdateWhoBanner();
        });

        // ══ กัน "ลง OT ให้ลูกน้องแล้วกลายเป็นของตัวเอง" ══
        // สองชั้น: (1) แถบบอกชื่อคนที่จะได้ OT เปลี่ยนสีตามที่เลือก เห็นตลอดเวลา
        //          (2) กดบันทึกแล้วถามยืนยันอีกรอบ พร้อมสรุปว่า "ให้ใคร กี่ชั่วโมง วันไหน"

        function otSelfId() {
            var el = document.getElementById('otSelfAdminId');
            return el ? el.value : '';
        }

        function otTargetName() {
            var ddl = document.getElementById('<%= ddlEmployee.ClientID %>');
            if (!ddl) {
                var self = document.getElementById('otSelfName');
                return { name: self ? self.value : 'ตัวคุณเอง', isSelf: true };
            }
            var opt = ddl.options[ddl.selectedIndex];
            return {
                name: opt ? opt.text.replace(/^\s*ตัวฉันเอง\s*[—-]\s*/, '') : '',
                isSelf: ddl.value === otSelfId()
            };
        }

        function otUpdateWhoBanner() {
            var box = document.getElementById('otWhoBanner');
            if (!box) return;
            var t = otTargetName();
            if (t.isSelf) {
                box.style.background = '#E3F2FD';
                box.style.color = '#0D47A1';
                box.style.borderLeftColor = '#1E88E5';
                box.innerHTML = '&#128100; OT นี้จะเข้าชื่อ <b>' + otEsc(t.name) + '</b> (ตัวคุณเอง)';
            } else {
                // สีส้ม = กำลังลงให้คนอื่น ต้องสะดุดตา
                box.style.background = '#FFF3E0';
                box.style.color = '#E65100';
                box.style.borderLeftColor = '#FB8C00';
                box.innerHTML = '&#128101; OT นี้จะเข้าชื่อ <b>' + otEsc(t.name) + '</b>'
                    + '<br><small>ไม่ใช่ของคุณ — ตรวจชื่อให้ชัวร์ก่อนบันทึก</small>';
            }
        }

        function otEsc(s) {
            return (s || '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
        }

        function otConfirmSave() {
            var d = document.getElementById('<%= txtOTDate.ClientID %>');
            var h = document.getElementById('<%= txtOTHours.ClientID %>');
            var r = document.getElementById('<%= ddlOTRate.ClientID %>');
            var w = document.getElementById('<%= txtWorkDescription.ClientID %>');

            // เช็คขั้นต่ำก่อน จะได้ไม่ต้องถามยืนยันแล้วค่อยไปเด้ง error ที่เซิร์ฟเวอร์
            if (!d || !d.value) { alert('กรุณาเลือกวันที่ทำ OT'); if (d) d.focus(); return false; }
            if (!h || !(parseFloat(h.value) > 0)) { alert('กรุณาระบุจำนวนชั่วโมง OT'); if (h) h.focus(); return false; }
            if (!w || !w.value.trim()) { alert('กรุณาระบุรายละเอียดงานที่ทำ'); if (w) w.focus(); return false; }

            var t = otTargetName();
            var rateText = r && r.options[r.selectedIndex] ? r.options[r.selectedIndex].text : '';

            var msg = (t.isSelf ? 'บันทึก OT ให้ตัวคุณเอง\n\n' : '⚠ บันทึก OT ให้ "คนอื่น"\n\n')
                + 'พนักงาน : ' + t.name + '\n'
                + 'วันที่   : ' + d.value + '\n'
                + 'ชั่วโมง  : ' + h.value + ' ชม.\n'
                + 'อัตรา    : ' + rateText + '\n\n'
                + 'ยืนยันบันทึกตามนี้?';

            return confirm(msg);
        }
    </script>
</asp:Content>
