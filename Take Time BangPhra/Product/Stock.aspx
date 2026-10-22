<%@ Page Title="จัดการสต็อกสินค้า" Language="C#" MaintainScrollPositionOnPostback="true" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Stock.aspx.cs" Inherits="Take_Time_BangPhra.Product.Stock" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <link rel="stylesheet" href="../Content/Product.css">

    <div class="product-page">
        <!-- Page Header -->
        <div class="product-header">
            <h2>📦 จัดการสต็อกสินค้า</h2>
            <div class="header-actions">
                <a href="/Product/In" class="btn btn-success">📥 นำเข้าสินค้า</a>
                <a href="/Product/Default" class="btn btn-primary">🛒 ขายสินค้า</a>
            </div>
        </div>

        <!-- Navigation Tabs -->
        <div class="product-nav">
            <a href="/Product/Default" class="product-nav-item">🛒 ขายสินค้า</a>
            <a href="/Product/In" class="product-nav-item">📥 นำเข้า</a>
            <a href="/Product/Stock" class="product-nav-item active">📦 สต็อก</a>
            <a href="/Product/SellReport" class="product-nav-item">📊 รายงาน</a>
        </div>

        <!-- Statistics Cards -->
        <div class="product-stats">
            <div class="stat-card">
                <h4>📦 สินค้าทั้งหมด</h4>
                <div class="stat-value">
                    <asp:Label ID="lblTotalProducts" runat="server" Text="0"></asp:Label>
                    <span class="stat-unit">รายการ</span>
                </div>
            </div>
            <div class="stat-card success">
                <h4>✅ สต็อกปกติ</h4>
                <div class="stat-value">
                    <asp:Label ID="lblNormalStock" runat="server" Text="0"></asp:Label>
                    <span class="stat-unit">รายการ</span>
                </div>
            </div>
            <div class="stat-card warning">
                <h4>⚠️ ใกล้หมด (≤4 สัปดาห์)</h4>
                <div class="stat-value">
                    <asp:Label ID="lblLowStock" runat="server" Text="0"></asp:Label>
                    <span class="stat-unit">รายการ</span>
                </div>
            </div>
            <div class="stat-card danger">
                <h4>🔴 สต็อกต่ำ (&lt;5)</h4>
                <div class="stat-value">
                    <asp:Label ID="lblCriticalStock" runat="server" Text="0"></asp:Label>
                    <span class="stat-unit">รายการ</span>
                </div>
            </div>
        </div>

        <!-- Predicted Low Stock Warning Panel -->
        <asp:Panel ID="pnlPredictedLowStock" runat="server" CssClass="stock-warning-panel" Visible="false">
            <h4>📊 สินค้าที่คาดว่าจะหมดใน 3-4 สัปดาห์ (คำนวณจากอัตราการใช้งาน)</h4>
            <div class="table-responsive">
                <asp:GridView ID="gvPredictedLowStock" runat="server" AutoGenerateColumns="False"
                    CssClass="stock-warning-table" GridLines="None" ShowHeaderWhenEmpty="false">
                    <Columns>
                        <asp:BoundField DataField="Product_Name" HeaderText="ชื่อสินค้า" />
                        <asp:TemplateField HeaderText="สต็อกปัจจุบัน">
                            <ItemTemplate>
                                <%# Convert.ToInt32(Eval("CurrentStock")) %>
                            </ItemTemplate>
                            <ItemStyle HorizontalAlign="Center" />
                            <HeaderStyle HorizontalAlign="Center" />
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="ใช้เฉลี่ย/สัปดาห์">
                            <ItemTemplate>
                                <%# string.Format("{0:N1}", Eval("WeeklyUsage")) %>
                            </ItemTemplate>
                            <ItemStyle HorizontalAlign="Center" />
                            <HeaderStyle HorizontalAlign="Center" />
                        </asp:TemplateField>
                        <asp:TemplateField HeaderText="คาดว่าจะหมดใน">
                            <ItemTemplate>
                                <span class='<%# Convert.ToDecimal(Eval("WeeksRemaining")) <= 2 ? "weeks-badge weeks-critical" : "weeks-badge weeks-warning" %>'>
                                    <%# string.Format("{0:N1}", Eval("WeeksRemaining")) %> สัปดาห์
                                </span>
                            </ItemTemplate>
                            <ItemStyle HorizontalAlign="Center" />
                            <HeaderStyle HorizontalAlign="Center" />
                        </asp:TemplateField>
                    </Columns>
                </asp:GridView>
            </div>
        </asp:Panel>

        <!-- Current Stock Table -->
        <div class="product-card">
            <div class="product-card-header">
                <h3>📋 รายการสต็อกปัจจุบัน</h3>
            </div>
            <div class="product-card-body" style="padding: 0;">
                <div class="table-responsive">
                    <asp:GridView ID="GridView2" runat="server" AutoGenerateColumns="False"
                        CssClass="product-table" GridLines="None"
                        AllowSorting="True" OnSorting="GridView2_Sorting">
                        <Columns>
                            <asp:BoundField DataField="ID" HeaderText="รหัส" SortExpression="ID" />
                            <asp:BoundField DataField="Barcode" HeaderText="บาร์โค้ด" SortExpression="Barcode" />
                            <asp:BoundField DataField="Category_ID" HeaderText="หมวดหมู่" SortExpression="Category_ID" />
                            <asp:BoundField DataField="Product_Name" HeaderText="ชื่อสินค้า" SortExpression="Product_Name" />
                            <asp:TemplateField HeaderText="จำนวนคงเหลือ" SortExpression="Amount">
                                <ItemTemplate>
                                    <span class='<%# Convert.ToInt32(Eval("Amount")) < 5 ? "stock-badge stock-critical" : Convert.ToInt32(Eval("Amount")) < 10 ? "stock-badge stock-warning" : "stock-badge stock-normal" %>'>
                                        <%# Eval("Amount") %>
                                    </span>
                                </ItemTemplate>
                                <ItemStyle HorizontalAlign="Center" />
                                <HeaderStyle HorizontalAlign="Center" />
                            </asp:TemplateField>
                        </Columns>
                    </asp:GridView>
                </div>
            </div>
        </div>

        <style>
            .so-msg { padding:11px 15px; border-radius:8px; margin-bottom:14px; font-size:14px; background:#e8f5e9; color:#1e5e30; white-space:pre-line; }
            .so-warn { background:#fff4e5; color:#8a4b00; border-left:4px solid #f0a020; }
            .so-help { background:#f3f7fb; border-left:4px solid #5c8fbf; padding:10px 13px; border-radius:7px; font-size:13px; color:#28445f; line-height:1.55; margin:6px 0 4px; min-height:20px; }
            .so-flags span { display:inline-block; font-size:11.5px; padding:2px 8px; border-radius:10px; margin:4px 6px 0 0; background:#eceff1; color:#37474f; }
            .so-flags span.vat { background:#ffebee; color:#b71c1c; }
            .so-flags span.ap { background:#fff8e1; color:#8d6e00; }
            .so-flags span.ev { background:#e3f2fd; color:#0d47a1; }
            .so-badge { font-size:11.5px; padding:2px 8px; border-radius:10px; white-space:nowrap; }
            .so-PENDING { background:#fff8e1; color:#8d6e00; } .so-APPROVED { background:#e8f5e9; color:#1e5e30; }
            .so-REJECTED, .so-REVERSED { background:#ffebee; color:#b71c1c; } .so-LEGACY { background:#eceff1; color:#455a64; }
            .so-links { display:flex; gap:8px; flex-wrap:wrap; margin-bottom:10px; }
            .so-sub { font-size:11.5px; color:#78909c; }
        </style>

        <asp:Panel ID="pnlStockOutMsg" runat="server" Visible="false">
            <div id="divStockOutMsg" runat="server" class="so-msg"><asp:Literal ID="litStockOutMsg" runat="server" /></div>
        </asp:Panel>

        <!-- Product Outflow Section -->
        <div class="product-card">
            <div class="product-card-header">
                <h3>📤 บันทึกเบิกสินค้า / ใช้ภายใน (ไม่ใช่การขาย)</h3>
            </div>
            <div class="product-card-body">
                <div class="so-links">
                    <a href="/Product/StockOutReport" class="btn btn-outline btn-sm" target="_blank">🖨 รายงานสินค้าเสียหาย/ทำลาย</a>
                    <a href="/Admin/Settings/StockOutReasons" class="btn btn-outline btn-sm">⚙ ตั้งค่าเหตุผลการเบิก</a>
                </div>
                <asp:Literal ID="litSchemaWarn" runat="server" />

                <div class="product-form">
                    <!-- Product Search -->
                    <div class="form-row">
                        <div class="form-group" style="flex: 2;">
                            <label>🔍 ค้นหาสินค้า (ชื่อหรือบาร์โค้ด) <span class="required">*</span></label>
                            <div class="input-group">
                                <div class="autocomplete" style="flex: 1;">
                                    <asp:TextBox ID="TextBox1" runat="server" CssClass="form-control"
                                        placeholder="พิมพ์ชื่อสินค้าหรือสแกนบาร์โค้ด..." autocomplete="off"></asp:TextBox>
                                </div>
                                <button id="btnScanBarcode" runat="server" class="btn scanner-btn" type="button">
                                    📷 สแกน
                                </button>
                            </div>
                        </div>
                    </div>

                    <div class="form-row">
                        <div class="form-group" style="flex: 2;">
                            <label>🏷 เหตุผลการเบิก <span class="required">*</span></label>
                            <asp:DropDownList ID="ddlReason" runat="server" CssClass="form-control" onchange="soShowHelp(this)"></asp:DropDownList>
                            <div id="soHelp" class="so-help">เลือกเหตุผล — ระบบจะลงบัญชีและภาษีตามเหตุผลที่เลือก</div>
                        </div>
                        <div class="form-group">
                            <label>📊 จำนวน <span class="required">*</span></label>
                            <asp:TextBox ID="TextBox2" runat="server" CssClass="form-control"
                                TextMode="Number" min="1" placeholder="ใส่จำนวน"></asp:TextBox>
                        </div>
                        <div class="form-group" style="flex: 2;">
                            <label>📝 หมายเหตุ / รายละเอียด</label>
                            <asp:TextBox ID="TextBox4" runat="server" CssClass="form-control" MaxLength="300"
                                placeholder="เช่น: ขวดแตกระหว่างขนย้าย, ใช้ในห้อง 205, งานกิจกรรมวันแม่"></asp:TextBox>
                        </div>
                    </div>

                    <div class="btn-group">
                        <asp:Button ID="Button3" runat="server" Text="➕ เพิ่มรายการ"
                            OnClick="Button3_Click" CssClass="btn btn-primary" />
                    </div>
                    <p class="so-sub" style="margin-top:8px;">มูลค่าที่ลงบัญชี = ต้นทุนสินค้า (Product.Cost_Price หรือราคาซื้อล่าสุด) — ไม่ใช้ราคาที่พิมพ์เอง.
                        เหตุผลที่ "ถือเป็นการขาย ม.77/1(8)" จะลงภาษีขาย 7% ของราคาตลาด (ราคาขายถอด VAT) เข้า ภ.พ.30 โดยไม่ออกใบกำกับ.</p>
                </div>

                <!-- Cart Items -->
                <asp:Panel ID="pnlCart" runat="server" Visible="true" style="margin-top: 25px;">
                    <h4 style="margin-bottom: 15px; color: #5d4037;">🛒 รายการที่เลือก</h4>
                    <div class="table-responsive">
                        <asp:GridView ID="GridView1" runat="server" AutoGenerateColumns="False"
                            CssClass="product-table" GridLines="None"
                            OnRowCommand="GridView1_RowCommand">
                            <Columns>
                                <asp:BoundField DataField="Product_Name" HeaderText="สินค้า" ReadOnly="True" />
                                <asp:BoundField DataField="Reason_Name" HeaderText="เหตุผล" ReadOnly="True" />
                                <asp:BoundField DataField="Amount" HeaderText="จำนวน" />
                                <asp:TemplateField HeaderText="ต้นทุน/หน่วย">
                                    <ItemTemplate>
                                        <%# string.Format("{0:N2}", Eval("Cost_PerUnit")) %>
                                        <div class="so-sub"><%# Server.HtmlEncode(Convert.ToString(Eval("Cost_Source"))) %></div>
                                    </ItemTemplate>
                                    <ItemStyle HorizontalAlign="Right" />
                                </asp:TemplateField>
                                <asp:TemplateField HeaderText="มูลค่าต้นทุน">
                                    <ItemTemplate><%# string.Format("{0:N2}", Eval("Cost_Total")) %></ItemTemplate>
                                    <ItemStyle HorizontalAlign="Right" />
                                </asp:TemplateField>
                                <asp:TemplateField HeaderText="ราคาขาย/หน่วย">
                                    <ItemTemplate><%# string.Format("{0:N2}", Eval("Market_PerUnit")) %></ItemTemplate>
                                    <ItemStyle HorizontalAlign="Right" />
                                </asp:TemplateField>
                                <asp:TemplateField HeaderText="ภาษีขาย (ประมาณ)">
                                    <ItemTemplate><%# string.Format("{0:N2}", Eval("Vat_Est")) %></ItemTemplate>
                                    <ItemStyle HorizontalAlign="Right" />
                                </asp:TemplateField>
                                <asp:BoundField DataField="Remark" HeaderText="หมายเหตุ" />
                                <asp:TemplateField HeaderText="จัดการ">
                                    <ItemTemplate>
                                        <div class="actions">
                                            <asp:Button ID="btnAdd" runat="server" Text="+"
                                                CommandName="Add" CommandArgument='<%# Container.DataItemIndex %>'
                                                CssClass="btn btn-success btn-sm" />
                                            <asp:Button ID="btnReduce" runat="server" Text="-"
                                                CommandName="Reduce" CommandArgument='<%# Container.DataItemIndex %>'
                                                CssClass="btn btn-warning btn-sm" />
                                            <asp:Button ID="btnDelete" runat="server" Text="🗑"
                                                CommandName="DeleteItem" CommandArgument='<%# Container.DataItemIndex %>'
                                                CssClass="btn btn-danger btn-sm"
                                                OnClientClick="return confirm('ยืนยันการลบรายการนี้?');" />
                                        </div>
                                    </ItemTemplate>
                                    <ItemStyle HorizontalAlign="Center" />
                                </asp:TemplateField>
                            </Columns>
                            <EmptyDataTemplate>
                                <div class="empty-state">
                                    <div class="icon">📦</div>
                                    <h4>ยังไม่มีรายการ</h4>
                                    <p>เลือกสินค้าและเหตุผลจากช่องด้านบน</p>
                                </div>
                            </EmptyDataTemplate>
                        </asp:GridView>
                    </div>

                    <div class="form-row" style="margin-top:14px;">
                        <div class="form-group" style="flex: 2;">
                            <label>📎 หลักฐาน (ภาพถ่าย / บันทึกการทำลาย / ใบแจ้งความ — jpg, png, pdf ไม่เกิน 10MB)</label>
                            <asp:FileUpload ID="fuEvidence" runat="server" CssClass="form-control" accept=".jpg,.jpeg,.png,.gif,.webp,.heic,.pdf" />
                            <div class="so-sub">บังคับเมื่อมีรายการที่เหตุผลกำหนดให้แนบหลักฐาน (ไฟล์เดียวใช้กับทุกรายการในชุดนี้)</div>
                        </div>
                    </div>
                </asp:Panel>
            </div>
            <div class="product-card-footer">
                <asp:Button ID="btnClear" runat="server" Text="🔄 ล้างรายการ"
                    CssClass="btn btn-outline" OnClick="btnClear_Click"
                    OnClientClick="return confirm('ยืนยันการล้างรายการทั้งหมด?');" />
                <asp:Button ID="Button2" runat="server" Text="💾 บันทึกการเบิก"
                    OnClick="Button2_Click" CssClass="btn btn-success" />
            </div>
        </div>

        <asp:HiddenField ID="hfActionNote" runat="server" />

        <!-- รออนุมัติ -->
        <asp:Panel ID="pnlPending" runat="server" CssClass="product-card" Visible="false">
            <div class="product-card-header">
                <h3>⏳ รายการเบิกรออนุมัติ (Owner / Admin)</h3>
            </div>
            <div class="product-card-body" style="padding:0;">
                <div class="table-responsive">
                    <asp:GridView ID="gvPending" runat="server" AutoGenerateColumns="False" CssClass="product-table"
                        GridLines="None" OnRowCommand="gvPending_RowCommand" DataKeyNames="ID">
                        <Columns>
                            <asp:BoundField DataField="ID" HeaderText="#" />
                            <asp:BoundField DataField="DateTime_Out" HeaderText="วันที่" DataFormatString="{0:dd/MM/yyyy HH:mm}" />
                            <asp:BoundField DataField="Product_Name" HeaderText="สินค้า" />
                            <asp:BoundField DataField="Reason_Name" HeaderText="เหตุผล" />
                            <asp:BoundField DataField="Amount" HeaderText="จำนวน" DataFormatString="{0:N0}" />
                            <asp:BoundField DataField="Cost_Total" HeaderText="มูลค่าต้นทุน" DataFormatString="{0:N2}" />
                            <asp:BoundField DataField="Reason" HeaderText="รายละเอียด" />
                            <asp:BoundField DataField="Recorded_By" HeaderText="ผู้บันทึก" />
                            <asp:TemplateField HeaderText="หลักฐาน">
                                <ItemTemplate><%# EvidenceLink(Eval("ID"), Eval("Evidence_Path")) %></ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="">
                                <ItemTemplate>
                                    <asp:Button ID="btnApprove" runat="server" Text="✔ อนุมัติ" CommandName="ApproveOut"
                                        CommandArgument='<%# Eval("ID") %>' CssClass="btn btn-success btn-sm"
                                        OnClientClick="return confirm('อนุมัติการเบิกนี้ และส่งลงบัญชี?');" />
                                    <asp:Button ID="btnReject" runat="server" Text="✖ ปฏิเสธ" CommandName="RejectOut"
                                        CommandArgument='<%# Eval("ID") %>' CssClass="btn btn-danger btn-sm"
                                        OnClientClick="return soAskNote('เหตุผลที่ปฏิเสธ (จำนวนจะถูกคืนเข้าสต็อก):');" />
                                </ItemTemplate>
                            </asp:TemplateField>
                        </Columns>
                    </asp:GridView>
                </div>
            </div>
        </asp:Panel>

        <!-- ประวัติการเบิก -->
        <div class="product-card">
            <div class="product-card-header">
                <h3>🧾 ประวัติการเบิก 60 วันล่าสุด</h3>
            </div>
            <div class="product-card-body" style="padding:0;">
                <div class="table-responsive">
                    <asp:GridView ID="gvHistory" runat="server" AutoGenerateColumns="False" CssClass="product-table"
                        GridLines="None" OnRowCommand="gvHistory_RowCommand" ShowHeaderWhenEmpty="false">
                        <Columns>
                            <asp:BoundField DataField="ID" HeaderText="#" />
                            <asp:BoundField DataField="DateTime_Out" HeaderText="วันที่" DataFormatString="{0:dd/MM/yyyy HH:mm}" />
                            <asp:BoundField DataField="Product_Name" HeaderText="สินค้า" />
                            <asp:BoundField DataField="Reason_Name" HeaderText="เหตุผล" />
                            <asp:BoundField DataField="Amount" HeaderText="จำนวน" DataFormatString="{0:N0}" />
                            <asp:BoundField DataField="Cost_Total" HeaderText="มูลค่าต้นทุน" DataFormatString="{0:N2}" />
                            <asp:BoundField DataField="Reason" HeaderText="รายละเอียด" />
                            <asp:TemplateField HeaderText="สถานะ">
                                <ItemTemplate><%# StatusBadge(Eval("Approval_Status"), Eval("Is_Reversed"), Eval("Out_Reason_Code")) %></ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="บัญชี">
                                <ItemTemplate><%# JournalBadge(Eval("Nexaacc_Journal_Ref")) %></ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="หลักฐาน">
                                <ItemTemplate><%# EvidenceLink(Eval("ID"), Eval("Evidence_Path")) %> <%# PrintLink(Eval("ID"), Eval("Out_Reason_Code")) %></ItemTemplate>
                            </asp:TemplateField>
                            <asp:TemplateField HeaderText="">
                                <ItemTemplate>
                                    <asp:Button ID="btnRepost" runat="server" Text="↻ ลงบัญชีอีกครั้ง" CommandName="RepostOut"
                                        CommandArgument='<%# Eval("ID") %>' CssClass="btn btn-outline btn-sm"
                                        Visible='<%# CanRepost(Eval("Approval_Status"), Eval("Is_Reversed"), Eval("Out_Reason_Code"), Eval("Nexaacc_Journal_Ref")) %>' />
                                    <asp:Button ID="btnCancelOut" runat="server" Text="ยกเลิก" CommandName="CancelOut"
                                        CommandArgument='<%# Eval("ID") %>' CssClass="btn btn-danger btn-sm"
                                        Visible='<%# CanCancel(Eval("Is_Reversed"), Eval("Out_Reason_Code")) %>'
                                        OnClientClick="return soAskNote('เหตุผลที่ยกเลิก (รายการที่ลงบัญชีแล้วจะถูกกลับรายการ และคืนจำนวนสต็อก):');" />
                                </ItemTemplate>
                            </asp:TemplateField>
                        </Columns>
                        <EmptyDataTemplate>
                            <div class="empty-state"><p>ยังไม่มีการเบิกใน 60 วันล่าสุด</p></div>
                        </EmptyDataTemplate>
                    </asp:GridView>
                </div>
            </div>
        </div>

        <asp:Literal ID="litReasonData" runat="server" />
        <script>
            function soShowHelp(sel) {
                var box = document.getElementById('soHelp');
                if (!box || !window.soReasons) return;
                var r = window.soReasons[sel.value];
                if (!r) { box.innerHTML = 'เลือกเหตุผล — ระบบจะลงบัญชีและภาษีตามเหตุผลที่เลือก'; return; }
                var flags = '<div class="so-flags">'
                    + (r.vat ? '<span class="vat">ถือเป็นการขาย — ภาษีขาย 7% (' + (r.base === 'COST' ? 'ฐานต้นทุน' : 'ฐานราคาตลาด') + ')</span>' : '<span>ไม่มีภาษีขาย</span>')
                    + (r.ap ? '<span class="ap">ต้องอนุมัติ</span>' : '')
                    + (r.ev ? '<span class="ev">ต้องแนบหลักฐาน</span>' : '')
                    + '<span>บัญชี: ' + soEsc(r.gl) + '</span></div>';
                box.innerHTML = soEsc(r.help) + flags;
            }
            function soEsc(s) {
                var d = document.createElement('div'); d.textContent = s || ''; return d.innerHTML;
            }
            function soAskNote(q) {
                var v = prompt(q, '');
                if (v === null) return false;
                // ตัด < > กัน ASP.NET request validation ปฏิเสธ postback
                document.getElementById('<%= hfActionNote.ClientID %>').value = v.replace(/[<>]/g, '').substring(0, 150);
                return true;
            }
            document.addEventListener('DOMContentLoaded', function () {
                var sel = document.getElementById('<%= ddlReason.ClientID %>');
                if (sel) soShowHelp(sel);
            });
        </script>
    </div>

    <!-- Scanner Modal -->
    <div id="scannerModal" class="scanner-modal">
        <div class="scanner-container">
            <video id="scannerVideo" autoplay playsinline class="scanner-video"></video>
        </div>
        <button id="closeScanner" class="scanner-close">❌ ปิดกล้อง</button>
    </div>

    <!-- Scripts -->
    <script src="https://cdnjs.cloudflare.com/ajax/libs/quagga/0.12.1/quagga.min.js"></script>
    <script>
        // Barcode Scanner
        let quaggaInitialized = false;
        let mediaStream = null;

        document.getElementById('<%= btnScanBarcode.ClientID %>').addEventListener('click', function () {
            const modal = document.getElementById('scannerModal');
            const video = document.getElementById('scannerVideo');
            modal.style.display = 'block';

            navigator.mediaDevices.getUserMedia({
                video: {
                    facingMode: 'environment',
                    width: { ideal: 1280 },
                    height: { ideal: 720 }
                }
            }).then(function (stream) {
                video.srcObject = stream;
                mediaStream = stream;

                video.onloadedmetadata = function () {
                    video.play().catch(err => {
                        console.error("Video play error:", err);
                        alert("ไม่สามารถเปิดกล้องได้: " + err.message);
                    });
                };

                initializeQuagga();

            }).catch(function (err) {
                console.error("Camera access error:", err);
                alert("ไม่สามารถเข้าถึงกล้องได้: " + err.message);
                modal.style.display = 'none';
            });

            function initializeQuagga() {
                if (quaggaInitialized) {
                    Quagga.stop();
                }

                Quagga.init({
                    inputStream: {
                        name: "Live",
                        type: "LiveStream",
                        target: video,
                        constraints: {
                            facingMode: "environment",
                            width: { min: 640 },
                            height: { min: 480 }
                        },
                    },
                    decoder: {
                        readers: ["code_128_reader", "ean_reader", "upc_reader"],
                    },
                    locate: true,
                    numOfWorkers: 2,
                    frequency: 10
                }, function (err) {
                    if (err) {
                        console.error("Quagga init error:", err);
                        stopScanner();
                        return;
                    }

                    quaggaInitialized = true;
                    Quagga.start();
                });

                Quagga.onDetected(function (result) {
                    if (result.codeResult) {
                        const code = result.codeResult.code;
                        document.getElementById('<%= TextBox1.ClientID %>').value = code;

                        // Play success sound
                        const audio = new Audio('https://assets.mixkit.co/sfx/preview/mixkit-achievement-bell-600.mp3');
                        audio.play().catch(e => console.log("Audio play error:", e));

                        stopScanner();
                        __doPostBack('<%= TextBox1.UniqueID %>', '');
                    }
                });
            }

            document.getElementById('closeScanner').addEventListener('click', stopScanner);

            function stopScanner() {
                if (quaggaInitialized) {
                    Quagga.stop();
                    quaggaInitialized = false;
                }
                if (mediaStream) {
                    mediaStream.getTracks().forEach(track => track.stop());
                    mediaStream = null;
                }
                const video = document.getElementById('scannerVideo');
                if (video) {
                    video.srcObject = null;
                }
                modal.style.display = 'none';
            }
        });
    </script>

    <asp:Literal ID="Literal1" runat="server"></asp:Literal>
</asp:Content>
