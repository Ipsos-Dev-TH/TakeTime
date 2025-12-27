<%@ Page Title="ขายสินค้า" Language="C#" MaintainScrollPositionOnPostback="true" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="Take_Time_BangPhra.Product.Default" %>
<%@ Register assembly="Microsoft.ReportViewer.WebForms" namespace="Microsoft.Reporting.WebForms" tagprefix="rsweb" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <link rel="stylesheet" href="../Content/Product.css">

    <div class="product-page">
        <!-- Page Header -->
        <div class="product-header">
            <h2>🛒 ขายสินค้า (POS)</h2>
            <div class="header-actions">
                <a href="/Product/Stock" class="btn btn-outline" style="background: white; color: #e65100;">📦 ดูสต็อก</a>
                <a href="/Product/SellReport" class="btn btn-primary">📊 รายงาน</a>
            </div>
        </div>

        <!-- Navigation Tabs -->
        <div class="product-nav">
            <a href="/Product/Default" class="product-nav-item active">🛒 ขายสินค้า</a>
            <a href="/Product/In" class="product-nav-item">📥 นำเข้า</a>
            <a href="/Product/Stock" class="product-nav-item">📦 สต็อก</a>
            <a href="/Product/SellReport" class="product-nav-item">📊 รายงาน</a>
        </div>

        <!-- Main POS Form -->
        <div class="product-card">
            <div class="product-card-header">
                <h3>💳 บันทึกการขาย</h3>
            </div>
            <div class="product-card-body">
                <div class="product-form">
                    <!-- Date Selection -->
                    <div class="form-row">
                        <div class="form-group">
                            <label>📅 วันที่ขาย</label>
                            <asp:TextBox ID="TextBox12" runat="server" CssClass="form-control"
                                AutoPostBack="True" TextMode="Date" OnTextChanged="TextBox12_TextChanged"></asp:TextBox>
                        </div>
                    </div>

                    <!-- Room Charge Feature -->
                    <div class="form-row">
                        <div class="form-group" style="flex: 2;">
                            <label>🏨 เลือกห้องพัก (Room Charge)</label>
                            <asp:DropDownList ID="ddlGuestReservation" runat="server"
                                CssClass="form-control"
                                AutoPostBack="True"
                                OnSelectedIndexChanged="ddlGuestReservation_SelectedIndexChanged">
                            </asp:DropDownList>
                            <asp:Label ID="lblActiveGuestCount" runat="server" CssClass="form-hint"></asp:Label>
                        </div>
                    </div>

                    <!-- Guest Info Display -->
                    <asp:Panel ID="pnlGuestInfo" runat="server" Visible="false">
                        <div class="guest-info-panel" style="background: #fff8e1; padding: 15px; border-radius: 8px; border-left: 4px solid #ff9800; margin-bottom: 15px;">
                            <asp:Label ID="lblGuestInfo" runat="server" style="color: #5d4037; line-height: 1.6;"></asp:Label>
                        </div>
                    </asp:Panel>

                    <!-- Charge Mode Selection -->
                    <asp:Panel ID="pnlChargeMode" runat="server" Visible="false">
                        <div class="form-group">
                            <label>💰 โหมดการชำระเงิน</label>
                            <asp:RadioButtonList ID="rblChargeMode" runat="server" RepeatDirection="Horizontal"
                                AutoPostBack="True" OnSelectedIndexChanged="rblChargeMode_SelectedIndexChanged"
                                CssClass="charge-mode-list">
                                <asp:ListItem Value="ROOM_CHARGE" Selected="True">🏨 ชาร์จเข้าห้อง (ชำระทีหลัง)</asp:ListItem>
                                <asp:ListItem Value="PAY_NOW">💵 ชำระเงินทันที</asp:ListItem>
                            </asp:RadioButtonList>
                            <div class="form-hint" style="margin-top: 8px;">
                                💡 <strong>ชาร์จเข้าห้อง:</strong> ตัดสต๊อก แต่ไม่เก็บเงิน (รวมในบิลเช็คเอาท์)<br />
                                💡 <strong>ชำระเงินทันที:</strong> ตัดสต๊อกและเก็บเงินเลย + ออกใบเสร็จ
                            </div>
                        </div>
                    </asp:Panel>

                    <!-- Product Search -->
                    <div class="form-row">
                        <div class="form-group" style="flex: 2;">
                            <label>🔍 ค้นหาสินค้า (ชื่อหรือบาร์โค้ด) <span class="required">*</span></label>
                            <div class="input-group">
                                <div class="autocomplete" style="flex: 1;">
                                    <asp:TextBox ID="TextBox1" runat="server" CssClass="form-control"
                                        AutoPostBack="True" OnTextChanged="TextBox1_TextChanged"
                                        onkeydown="checkEnter(event)" autocomplete="off"
                                        placeholder="พิมพ์ชื่อสินค้าหรือสแกนบาร์โค้ด..."></asp:TextBox>
                                </div>
                                <asp:Button ID="Button3" runat="server" Text="➕ เพิ่ม"
                                    OnClick="Button3_Click" CssClass="btn btn-primary" />
                                <button id="btnScanBarcode" runat="server" class="btn scanner-btn" type="button">
                                    📷 สแกน
                                </button>
                            </div>
                        </div>
                    </div>

                    <!-- Cart Items -->
                    <div style="margin-top: 25px;">
                        <h4 style="margin-bottom: 15px; color: #e65100; display: flex; align-items: center; gap: 8px;">
                            🛒 รายการสินค้า
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
                                        <div class="icon">🛒</div>
                                        <h4>ยังไม่มีรายการ</h4>
                                        <p>เลือกสินค้าจากช่องค้นหาด้านบน</p>
                                    </div>
                                </EmptyDataTemplate>
                            </asp:GridView>
                        </div>
                    </div>

                    <!-- Order Summary -->
                    <div class="order-summary" style="margin-top: 20px; background: linear-gradient(135deg, #e65100, #ff8a50); padding: 20px; border-radius: 12px; color: white;">
                        <div style="display: flex; justify-content: space-between; align-items: center;">
                            <span style="font-size: 1.2em;">💰 ยอดรวมทั้งสิ้น</span>
                            <asp:TextBox ID="TextBox2" runat="server" CssClass="total-display" Enabled="false"
                                style="background: rgba(255,255,255,0.2); border: none; color: white; font-size: 1.8em; font-weight: bold; text-align: right; width: 200px;"></asp:TextBox>
                        </div>
                    </div>

                    <!-- Payment Method -->
                    <div class="form-row" style="margin-top: 20px;">
                        <div class="form-group">
                            <label>💳 ชำระเข้าบัญชี <span class="required">*</span></label>
                            <asp:DropDownList ID="DropDownList1" runat="server" CssClass="form-control"
                                DataSourceID="SqlDataSource1" DataTextField="Paid_How" DataValueField="ID"
                                AutoPostBack="True" OnSelectedIndexChanged="DropDownList1_SelectedIndexChanged"
                                AppendDataBoundItems="true">
                                <asp:ListItem>--- โปรดเลือก ---</asp:ListItem>
                            </asp:DropDownList>
                            <asp:SqlDataSource ID="SqlDataSource1" runat="server"
                                ConnectionString="<%$ ConnectionStrings:TaketimeConnectionString %>"
                                SelectCommand="SELECT * FROM [Account_Paid_How] WHERE ([Status] = @Status)">
                                <SelectParameters>
                                    <asp:Parameter DefaultValue="True" Name="Status" Type="Boolean" />
                                </SelectParameters>
                            </asp:SqlDataSource>
                        </div>
                    </div>

                    <!-- Tax Invoice Options -->
                    <div class="form-row">
                        <div class="form-group">
                            <div class="checkbox-group" style="display: flex; flex-direction: column; gap: 10px;">
                                <asp:CheckBox ID="CheckBox1" runat="server" Text="📄 ออกใบกำกับภาษีในระบบ"
                                    AutoPostBack="True" OnCheckedChanged="CheckBox1_CheckedChanged1" CssClass="custom-checkbox" />
                                <asp:CheckBox ID="CheckBox2" runat="server" Text="✍️ ระบุชื่อในใบกำกับภาษี"
                                    AutoPostBack="True" OnCheckedChanged="CheckBox2_CheckedChanged" CssClass="custom-checkbox" />
                            </div>
                        </div>
                    </div>

                    <!-- Tax Invoice Details Panel -->
                    <asp:Panel ID="Panel1" runat="server" Visible="false" CssClass="tax-invoice-panel">
                        <div class="product-card" style="background: #fafafa; margin-top: 20px;">
                            <div class="product-card-header" style="background: linear-gradient(135deg, #5d4037, #8d6e63);">
                                <h3>📋 ข้อมูลใบกำกับภาษี</h3>
                            </div>
                            <div class="product-card-body">
                                <div class="product-form">
                                    <div class="form-row">
                                        <div class="form-group">
                                            <label>📱 เบอร์โทรลูกค้า</label>
                                            <asp:TextBox ID="TextBox3" runat="server" CssClass="form-control"
                                                AutoPostBack="True" OnTextChanged="TextBox3_TextChanged"
                                                placeholder="ค้นหาข้อมูลลูกค้าจากเบอร์โทร"></asp:TextBox>
                                        </div>
                                    </div>
                                    <div class="form-row">
                                        <div class="form-group" style="flex: 0.5;">
                                            <label>ประเภท</label>
                                            <asp:DropDownList ID="DropDownList2" runat="server" CssClass="form-control"
                                                AppendDataBoundItems="True" AutoPostBack="True"
                                                DataSourceID="SqlDataSource2" DataTextField="Customer_Type" DataValueField="ID"
                                                OnSelectedIndexChanged="DropDownList2_SelectedIndexChanged">
                                                <asp:ListItem Value="0">--- โปรดเลือก ---</asp:ListItem>
                                            </asp:DropDownList>
                                            <asp:SqlDataSource ID="SqlDataSource2" runat="server"
                                                ConnectionString="<%$ ConnectionStrings:TaketimeConnectionString %>"
                                                SelectCommand="SELECT * FROM [Customer_Type]"></asp:SqlDataSource>
                                        </div>
                                        <div class="form-group" style="flex: 1;">
                                            <label>ชื่อ / ชื่อบริษัท</label>
                                            <asp:TextBox ID="TextBox4" runat="server" CssClass="form-control"
                                                placeholder="ชื่อลูกค้าหรือบริษัท"></asp:TextBox>
                                        </div>
                                        <div class="form-group" style="flex: 0.3;">
                                            <label>รหัสสาขา</label>
                                            <asp:TextBox ID="TextBox5" runat="server" CssClass="form-control"
                                                Text="00000" Visible="false"></asp:TextBox>
                                        </div>
                                    </div>
                                    <div class="form-row">
                                        <div class="form-group">
                                            <label>🆔 เลขประจำตัวผู้เสียภาษี</label>
                                            <asp:TextBox ID="TextBox6" runat="server" CssClass="form-control"
                                                AutoPostBack="True" OnTextChanged="TextBox6_TextChanged"
                                                placeholder="เลข 13 หลัก"></asp:TextBox>
                                        </div>
                                    </div>
                                    <div class="form-row">
                                        <div class="form-group" style="flex: 0.3;">
                                            <label>เลขที่</label>
                                            <asp:TextBox ID="TextBox7" runat="server" CssClass="form-control"
                                                placeholder="เลขที่"></asp:TextBox>
                                        </div>
                                        <div class="form-group" style="flex: 1;">
                                            <label>ที่อยู่</label>
                                            <asp:TextBox ID="TextBox8" runat="server" CssClass="form-control"
                                                placeholder="หมู่ อาคาร ถนน ซอย"></asp:TextBox>
                                        </div>
                                    </div>
                                    <div class="form-row">
                                        <div class="form-group">
                                            <label>📮 รหัสไปรษณีย์</label>
                                            <asp:TextBox ID="TextBox9" runat="server" CssClass="form-control"
                                                AutoPostBack="True" OnTextChanged="TextBox9_TextChanged"
                                                placeholder="รหัสไปรษณีย์"></asp:TextBox>
                                        </div>
                                    </div>
                                    <div class="form-row">
                                        <div class="form-group">
                                            <label>จังหวัด</label>
                                            <asp:DropDownList ID="DropDownList3" runat="server" CssClass="form-control"
                                                AutoPostBack="True"></asp:DropDownList>
                                        </div>
                                        <div class="form-group">
                                            <label>อำเภอ</label>
                                            <asp:DropDownList ID="DropDownList4" runat="server" CssClass="form-control"
                                                AutoPostBack="True"></asp:DropDownList>
                                        </div>
                                        <div class="form-group">
                                            <label>ตำบล</label>
                                            <asp:DropDownList ID="DropDownList5" runat="server" CssClass="form-control"
                                                AutoPostBack="True"></asp:DropDownList>
                                        </div>
                                    </div>
                                    <div class="form-row">
                                        <div class="form-group" style="flex: 1;">
                                            <label>📧 อีเมล์</label>
                                            <asp:TextBox ID="TextBox10" runat="server" CssClass="form-control"
                                                placeholder="email@example.com"></asp:TextBox>
                                        </div>
                                        <div class="form-group" style="flex: 0.5; display: flex; align-items: flex-end;">
                                            <asp:CheckBox ID="CheckBox3" runat="server" Text="📨 ต้องการรับ e-Tax" CssClass="custom-checkbox" />
                                        </div>
                                    </div>
                                    <div class="form-row">
                                        <div class="form-group">
                                            <label>📝 รายละเอียดใบกำกับภาษี</label>
                                            <asp:TextBox ID="TextBox11" runat="server" CssClass="form-control"
                                                Text="อาหารและเครื่องดื่ม"></asp:TextBox>
                                        </div>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </asp:Panel>
                </div>
            </div>
            <div class="product-card-footer">
                <asp:Button ID="btnClear" runat="server" Text="🔄 ล้างรายการ"
                    CssClass="btn btn-outline" OnClick="btnClear_Click"
                    OnClientClick="return confirm('ยืนยันการล้างรายการทั้งหมด?');" />
                <asp:Button ID="Button2" runat="server" Text="💾 บันทึกการขาย"
                    OnClick="Button2_Click" CssClass="btn btn-success" />
            </div>
        </div>

        <!-- Info Panel -->
        <div class="alert-panel info">
            <div class="alert-icon">💡</div>
            <div class="alert-content">
                <div class="alert-title">คำแนะนำการขายสินค้า</div>
                <div class="alert-text">
                    1. เลือกวันที่ขาย และห้องพัก (ถ้าต้องการ Room Charge)<br>
                    2. ค้นหาสินค้าด้วยชื่อหรือสแกนบาร์โค้ด<br>
                    3. เลือกวิธีชำระเงินและออกใบกำกับภาษี (ถ้าต้องการ)<br>
                    4. กด "บันทึกการขาย" เพื่อยืนยัน
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
        function checkEnter(event) {
            if (event.key === 'Enter') {
                __doPostBack('<%= TextBox1.UniqueID %>', '');
                event.preventDefault();
            }
        }

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
    <rsweb:reportviewer Visible="false" ID="ReportViewer2" runat="server" Height="500px">
        <LocalReport EnableExternalImages="true" ReportPath="Account\Report\Receipt.rdlc">
        </LocalReport>
    </rsweb:reportviewer>
</asp:Content>
