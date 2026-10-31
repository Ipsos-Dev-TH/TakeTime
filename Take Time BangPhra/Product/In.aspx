<%@ Page Title="นำเข้าสินค้า" Language="C#" MaintainScrollPositionOnPostback="true" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="In.aspx.cs" Inherits="Take_Time_BangPhra.Product.In" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <link rel="stylesheet" href="../Content/Product.css">

    <div class="product-page">
        <!-- Page Header -->
        <div class="product-header">
            <h2>📥 นำเข้าสินค้า</h2>
            <div class="header-actions">
                <a href="/Product/Stock" class="btn btn-outline" style="background: white; color: #e65100;">📦 ดูสต็อก</a>
            </div>
        </div>

        <!-- Navigation Tabs -->
        <div class="product-nav">
            <a href="/Product/Default" class="product-nav-item">🛒 ขายสินค้า</a>
            <a href="/Product/In" class="product-nav-item active">📥 นำเข้า</a>
            <a href="/Product/PurchaseReturn" class="product-nav-item">↩️ คืนผู้ขาย</a>
            <a href="/Product/Stock" class="product-nav-item">📦 สต็อก</a>
            <a href="/Product/SellReport" class="product-nav-item">📊 รายงาน</a>
        </div>

        <!-- Product Entry Form -->
        <div class="product-card">
            <div class="product-card-header">
                <h3>📦 บันทึกรับสินค้าเข้าสต็อก</h3>
            </div>
            <div class="product-card-body">
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
                        <div class="form-group">
                            <label>📊 จำนวนที่รับเข้า <span class="required">*</span></label>
                            <asp:TextBox ID="TextBox2" runat="server" CssClass="form-control"
                                TextMode="Number" min="1" placeholder="ใส่จำนวน"></asp:TextBox>
                        </div>
                        <div class="form-group">
                            <label>💰 ราคาต้นทุนต่อชิ้น <span class="required">*</span></label>
                            <asp:TextBox ID="TextBox3" runat="server" CssClass="form-control"
                                placeholder="0.00"></asp:TextBox>
                        </div>
                    </div>

                    <div class="btn-group">
                        <asp:Button ID="Button3" runat="server" Text="➕ เพิ่มรายการ"
                            OnClick="Button3_Click" CssClass="btn btn-primary" />
                    </div>
                </div>

                <!-- Cart Items -->
                <div style="margin-top: 30px;">
                    <h4 style="margin-bottom: 15px; color: #2e7d32; display: flex; align-items: center; gap: 8px;">
                        📋 รายการสินค้าที่จะนำเข้า
                    </h4>
                    <div class="table-responsive">
                        <asp:GridView ID="GridView1" runat="server" AutoGenerateColumns="False"
                            CssClass="product-table" GridLines="None"
                            OnRowCommand="GridView1_RowCommand"
                            OnRowCancelingEdit="GridView1_RowCancelingEdit"
                            OnRowEditing="GridView1_RowEditing"
                            OnRowUpdating="GridView1_RowUpdating">
                            <Columns>
                                <asp:BoundField DataField="Product_Name" HeaderText="สินค้า" ReadOnly="True" />
                                <asp:BoundField DataField="Amount" HeaderText="จำนวน" />
                                <asp:BoundField DataField="Sell_Price" HeaderText="ราคา/ชิ้น" />
                                <asp:TemplateField HeaderText="ราคารวม">
                                    <ItemTemplate>
                                        <%# string.Format("{0:N2}", Eval("Price_Total")) %>
                                    </ItemTemplate>
                                    <ItemStyle HorizontalAlign="Right" />
                                </asp:TemplateField>
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
                                <asp:CommandField ShowEditButton="True" ButtonType="Button"
                                    EditText="✏️" UpdateText="💾" CancelText="❌" />
                            </Columns>
                            <EmptyDataTemplate>
                                <div class="empty-state">
                                    <div class="icon">📥</div>
                                    <h4>ยังไม่มีรายการ</h4>
                                    <p>เลือกสินค้าจากช่องค้นหาด้านบนเพื่อเริ่มนำเข้า</p>
                                </div>
                            </EmptyDataTemplate>
                        </asp:GridView>
                    </div>
                </div>

                <!-- เอกสารซื้อ + การชำระ (PHASE19_31) -->
                <div style="margin-top: 30px; padding: 16px; border: 1px solid #c8e6c9; border-radius: 8px; background: #f9fdf9;">
                    <h4 style="margin: 0 0 12px 0; color: #2e7d32;">🧾 เอกสารซื้อและการชำระเงิน</h4>
                    <asp:Literal ID="litPurchaseInfo" runat="server"></asp:Literal>
                    <div class="form-row">
                        <div class="form-group" style="flex: 2;">
                            <label>ผู้ขาย (เลือกจากทะเบียนผู้ขาย หรือพิมพ์เอง)</label>
                            <asp:DropDownList ID="ddlSupplierVendor" runat="server" CssClass="form-control" AutoPostBack="true"
                                OnSelectedIndexChanged="ddlSupplierVendor_SelectedIndexChanged" AppendDataBoundItems="true">
                                <asp:ListItem Value="" Text="— ไม่เลือก / พิมพ์ชื่อเอง —" />
                            </asp:DropDownList>
                        </div>
                        <div class="form-group" style="flex: 2;">
                            <label>ชื่อผู้ขาย</label>
                            <asp:TextBox ID="txtSupplierName" runat="server" CssClass="form-control" MaxLength="200" placeholder="ชื่อร้าน/บริษัทผู้ขาย"></asp:TextBox>
                        </div>
                        <div class="form-group">
                            <label>เลขผู้เสียภาษีผู้ขาย</label>
                            <asp:TextBox ID="txtSupplierTaxId" runat="server" CssClass="form-control" MaxLength="20" placeholder="13 หลัก"></asp:TextBox>
                        </div>
                    </div>
                    <div class="form-row">
                        <div class="form-group">
                            <label>เลขที่ใบกำกับ/ใบส่งของ</label>
                            <asp:TextBox ID="txtSupplierDocNo" runat="server" CssClass="form-control" MaxLength="60"></asp:TextBox>
                        </div>
                        <div class="form-group">
                            <label>วันที่เอกสาร</label>
                            <asp:TextBox ID="txtSupplierDocDate" runat="server" CssClass="form-control" TextMode="Date"></asp:TextBox>
                        </div>
                        <div class="form-group">
                            <label>ภาษีซื้อ (VAT)</label>
                            <asp:DropDownList ID="ddlVatMode" runat="server" CssClass="form-control">
                                <asp:ListItem Value="NONE" Text="ไม่มี VAT (ผู้ขายไม่จด VAT / ใบเสร็จธรรมดา)" Selected="True" />
                                <asp:ListItem Value="EXCL" Text="ราคาที่กรอกยังไม่รวม VAT — คิด VAT 7%" />
                                <asp:ListItem Value="INCL" Text="ราคาที่กรอกรวม VAT แล้ว — ถอด VAT 7% ออก" />
                                <asp:ListItem Value="MANUAL" Text="กรอกยอด VAT เอง (ราคาที่กรอก = ไม่รวม VAT)" />
                            </asp:DropDownList>
                        </div>
                        <div class="form-group">
                            <label>ยอด VAT (เฉพาะกรอกเอง)</label>
                            <asp:TextBox ID="txtVatAmount" runat="server" CssClass="form-control" placeholder="0.00"></asp:TextBox>
                        </div>
                    </div>
                    <div class="form-row">
                        <div class="form-group">
                            <label>สถานะการชำระ</label>
                            <asp:DropDownList ID="ddlPaymentStatus" runat="server" CssClass="form-control">
                                <asp:ListItem Value="UNPAID" Text="รับของแล้ว — รอใบแจ้งหนี้/ยังไม่จ่าย" Selected="True" />
                                <asp:ListItem Value="PAID" Text="จ่ายแล้ว (เงินสด/โอน)" />
                                <asp:ListItem Value="CREDIT" Text="ซื้อเชื่อ (ตั้งเจ้าหนี้ จ่ายทีหลัง)" />
                            </asp:DropDownList>
                        </div>
                        <div class="form-group">
                            <label>จ่ายจาก (กรณีจ่ายแล้ว)</label>
                            <asp:DropDownList ID="ddlPaidHow" runat="server" CssClass="form-control" AppendDataBoundItems="true">
                                <asp:ListItem Value="" Text="— เลือกแหล่งเงิน —" />
                            </asp:DropDownList>
                        </div>
                        <div class="form-group" style="flex: 2; justify-content: flex-end;">
                            <asp:CheckBox ID="chkOpenVoucher" runat="server" Checked="true"
                                Text=" เปิดหน้าใบสำคัญจ่ายต่อทันที (กรอกผู้ขาย/รายการล้างรับของ/VAT ให้แล้ว) — จ่ายแล้วหรือซื้อเชื่อ" />
                        </div>
                    </div>
                    <div style="font-size: 12px; color: #607d8b;">
                        ราคาต้นทุนต่อชิ้นที่บันทึกเป็นราคาสุทธิไม่รวม VAT เสมอ · ต้นทุนเฉลี่ยของสินค้า (Product.Cost_Price) คำนวณใหม่ทุกครั้งที่รับของ (ถัวเฉลี่ยเคลื่อนที่)
                        · ค่าสินค้าจ่ายผ่านใบสำคัญจ่ายที่ "ผูกการรับของ" เท่านั้น (ลง Dr GRNI ไม่ใช่ค่าใช้จ่าย — กันต้นทุนซ้ำ)
                    </div>
                </div>

                <asp:Panel ID="Panel1" runat="server" Visible="false"></asp:Panel>
            </div>
            <div class="product-card-footer">
                <asp:Button ID="btnClear" runat="server" Text="🔄 ล้างรายการ"
                    CssClass="btn btn-outline" OnClick="btnClear_Click"
                    OnClientClick="return confirm('ยืนยันการล้างรายการทั้งหมด?');" />
                <asp:Button ID="Button2" runat="server" Text="💾 บันทึกการนำเข้า"
                    OnClick="Button2_Click" CssClass="btn btn-success" />
            </div>
        </div>

        <!-- รายการรับของล่าสุด (PHASE19_31) -->
        <asp:Panel ID="pnlRecentBatches" runat="server" CssClass="product-card" Visible="false">
            <div class="product-card-header">
                <h3>🗂️ การรับของล่าสุด</h3>
            </div>
            <div class="product-card-body">
                <div class="table-responsive">
                    <asp:GridView ID="gvRecentBatches" runat="server" AutoGenerateColumns="False" CssClass="product-table" GridLines="None">
                        <Columns>
                            <asp:BoundField DataField="Batch" HeaderText="เลขที่รับของ" />
                            <asp:BoundField DataField="Received_At" HeaderText="วันที่รับ" DataFormatString="{0:dd/MM/yyyy HH:mm}" />
                            <asp:BoundField DataField="Supplier_Name" HeaderText="ผู้ขาย" />
                            <asp:BoundField DataField="Supplier_Doc_No" HeaderText="เลขที่ใบกำกับ" />
                            <asp:BoundField DataField="Net_Total" HeaderText="ยอดสุทธิ" DataFormatString="{0:N2}" ItemStyle-HorizontalAlign="Right" />
                            <asp:BoundField DataField="Vat_Amount" HeaderText="VAT" DataFormatString="{0:N2}" ItemStyle-HorizontalAlign="Right" />
                            <asp:BoundField DataField="Payment_Status" HeaderText="สถานะ" />
                            <asp:BoundField DataField="Linked_Voucher_Id" HeaderText="ใบสำคัญจ่าย" />
                            <asp:TemplateField HeaderText="NextAcc">
                                <ItemTemplate><div class="na-stack"><%# NaBatchBadge(Eval("Batch")) %></div></ItemTemplate>
                            </asp:TemplateField>
                            <asp:HyperLinkField Text="💳 ใบสำคัญจ่าย" DataNavigateUrlFields="Batch" DataNavigateUrlFormatString="/Account/PaymentVoucher?grn={0}" />
                            <asp:HyperLinkField Text="↩️ คืนสินค้า" DataNavigateUrlFields="Batch" DataNavigateUrlFormatString="/Product/PurchaseReturn?batch={0}" />
                        </Columns>
                    </asp:GridView>
                </div>
            </div>
        </asp:Panel>

        <!-- Info Panel -->
        <div class="alert-panel info">
            <div class="alert-icon">💡</div>
            <div class="alert-content">
                <div class="alert-title">คำแนะนำการนำเข้าสินค้า</div>
                <div class="alert-text">
                    1. ค้นหาสินค้าด้วยชื่อหรือสแกนบาร์โค้ด<br>
                    2. ใส่จำนวนและราคาต้นทุน แล้วกด "เพิ่มรายการ"<br>
                    3. กรอกผู้ขาย เลขที่/วันที่ใบกำกับ VAT และสถานะการชำระ แล้วกด "บันทึกการนำเข้า"<br>
                    4. จ่ายแล้ว/ซื้อเชื่อ → ระบบเปิดหน้าใบสำคัญจ่ายที่ผูกการรับของนี้ให้ (ซื้อเชื่อจ่ายภายหลังที่หน้า "ชำระใบซื้อเชื่อ")<br>
                    5. คืนของให้ผู้ขาย → ปุ่ม "↩️ คืนสินค้า" ในตารางการรับของล่าสุด
                </div>
            </div>
        </div>
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
