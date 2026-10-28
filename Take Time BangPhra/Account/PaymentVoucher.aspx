<%@ Page MaintainScrollPositionOnPostback="true" Title="" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="PaymentVoucher.aspx.cs" Inherits="Take_Time_BangPhra.Account.Report.PaymentVoucher" %>
<%@ Register assembly="Microsoft.ReportViewer.WebForms" namespace="Microsoft.Reporting.WebForms" tagprefix="rsweb" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <link rel="stylesheet" href="/Content/jquery-ui.css">
    <link rel="stylesheet" href="/Content/style.css">
    <link rel="stylesheet" type="text/css" href="/Content/GridView.css">
    <link rel="stylesheet" href="<%= ResolveUrl("~/Content/pv-voucher.css?v=1") %>" />
    <style>
        .autocomplete { position: relative; display: block; }
        .autocomplete-items { position: absolute; border: 1px solid #d4d4d4; border-bottom: none; border-top: none; z-index: 99; top: 100%; left: 0; right: 0; background-color: lemonchiffon; }
        .autocomplete-items div { padding: 10px; cursor: pointer; border-bottom: 1px solid #d4d4d4; }
        .autocomplete-active { background-color: DodgerBlue !important; color: #ffffff; }
    </style>
    <%-- ค่าตั้งของสคริปต์หน้า (nonce ของ API/PvAssist.ashx, id ของ control, อัตรา VAT ต่อประเภท) --%>
    <asp:Literal ID="litPvCfg" runat="server" EnableViewState="false"></asp:Literal>

<div class="pv-wrap" id="pvWrap">
    <header class="pv-head">
        <h1 class="pv-title">ใบสำคัญจ่าย</h1>
        <p class="pv-sub">กรอกตามลำดับ: ผู้รับเงิน → รายการ → การชำระ → ภาษี → สรุป แล้วกด "บันทึก"</p>
    </header>
    <asp:Literal ID="litSavedMsg" runat="server" EnableViewState="false"></asp:Literal>

    <%-- ① ผู้รับเงิน --%>
    <section class="pv-card" aria-labelledby="pvS1">
        <h2 class="pv-card-h" id="pvS1"><span class="pv-step">1</span> ผู้รับเงิน</h2>
        <div class="pv-grid">
            <div class="pv-field">
                <label for="<%= txtVoucherNo.ClientID %>">เลขที่ใบสำคัญจ่าย</label>
                <div class="pv-inline">
                    <asp:TextBox ID="txtVoucherNo" runat="server" ReadOnly="True" BackColor="LightGray"></asp:TextBox>
                    <asp:CheckBox ID="chkEditVoucherNo" Text="แก้เลขเอง" runat="server" AutoPostBack="True" OnCheckedChanged="chkEditVoucherNo_CheckedChanged" CssClass="pv-check" />
                </div>
            </div>
            <div class="pv-field">
                <label for="<%= TextBox8.ClientID %>">วันที่ใบสำคัญจ่าย</label>
                <asp:TextBox ID="TextBox8" runat="server" TextMode="Date" AutoPostBack="True" OnTextChanged="TextBox8_TextChanged"></asp:TextBox>
            </div>
        </div>
        <asp:PlaceHolder ID="pnlNextAccRef" runat="server" Visible="false">
            <div class="pv-na-ref">
                <span class="pv-na-ref-k">เอกสาร NextAcc</span>
                <asp:Label ID="lblNextAccDocNumber" runat="server" Text="" CssClass="pv-na-ref-no"></asp:Label>
                <asp:HyperLink ID="lnkNextAccDoc" runat="server" Text="เปิดใน NextAcc" Target="_blank" Visible="false" />
                <asp:LinkButton ID="lnkNextAccPdf" runat="server" Text="ดู PDF (NextAcc)" OnClick="lnkNextAccPdf_Click" CausesValidation="false" Visible="false" />
                <asp:Label ID="lblNextAccSyncStatus" runat="server" Text="" CssClass="pv-muted" />
                <asp:Label ID="lblNextAccPaymentStatus" runat="server" Text="" />
            </div>
        </asp:PlaceHolder>

        <div class="pv-field">
            <label for="<%= TextBox9.ClientID %>">ค้นหาผู้ขาย / ผู้รับเงิน</label>
            <div class="autocomplete">
                <asp:TextBox ID="TextBox9" runat="server" autocomplete="off" AutoPostBack="True" OnTextChanged="TextBox9_TextChanged" placeholder="พิมพ์ชื่อแล้วเลือกจากรายการ"></asp:TextBox>
                <asp:Literal ID="Literal1" runat="server"></asp:Literal>
            </div>
        </div>
        <div class="pv-grid">
            <div class="pv-field">
                <label for="<%= DropDownList5.ClientID %>">กลุ่มผู้ขาย</label>
                <asp:DropDownList ID="DropDownList5" runat="server" AppendDataBoundItems="true" AutoPostBack="True" OnSelectedIndexChanged="DropDownList5_SelectedIndexChanged">
                    <asp:ListItem Value="%">— ทุกกลุ่ม —</asp:ListItem>
                </asp:DropDownList>
                <asp:SqlDataSource ID="SqlDataSource5" runat="server" ConnectionString="<%$ ConnectionStrings:TaketimeConnectionString %>" SelectCommand="Select distinct(Vendor_Group) from Vendor"></asp:SqlDataSource>
            </div>
            <div class="pv-field">
                <label for="<%= DropDownList1.ClientID %>">ผู้ขาย / ผู้รับเงิน <span class="pv-req" aria-hidden="true">*</span></label>
                <asp:DropDownList ID="DropDownList1" runat="server" AppendDataBoundItems="true">
                    <asp:ListItem>---โปรดเลือก---</asp:ListItem>
                </asp:DropDownList>
                <asp:SqlDataSource ID="SqlDataSource1" runat="server" ConnectionString="<%$ ConnectionStrings:TaketimeConnectionString %>" SelectCommand="SELECT * FROM [Vendor] WHERE ([Status] = 'True') order by Vendor_Group,Name ASC">
                    <SelectParameters>
                        <asp:Parameter DefaultValue="True" Name="Status" Type="Boolean" />
                    </SelectParameters>
                </asp:SqlDataSource>
                <asp:Button ID="Button5" runat="server" Text="เพิ่ม/แก้ไขผู้ขาย" CssClass="pv-btn pv-btn-link" OnClick="Button5_Click" CausesValidation="false" />
            </div>
        </div>
        <div class="pv-field">
            <label for="<%= txtSupplierInvoiceNo.ClientID %>">เลขที่ใบกำกับภาษี / ใบเสร็จของผู้ขาย</label>
            <asp:TextBox ID="txtSupplierInvoiceNo" runat="server" MaxLength="100" autocomplete="off" placeholder="เช่น IV6910-0012 (ใช้ตรวจว่าเคยบันทึกใบนี้แล้วหรือยัง)"></asp:TextBox>
            <div class="pv-hint">ระบบตรวจกับใบสำคัญจ่ายเดิม หน้าสแกนเอกสาร และ NextAcc — ถ้าซ้ำจะเตือนก่อนบันทึก</div>
            <div id="pvDupBox" class="pv-dup" aria-live="polite"></div>
        </div>
        <div id="pvAiVendor" class="pv-ai" aria-live="polite"></div>
    </section>

    <%-- ② รายการ --%>
    <section class="pv-card" aria-labelledby="pvS2">
        <h2 class="pv-card-h" id="pvS2"><span class="pv-step">2</span> รายการ</h2>

        <details class="pv-grni">
            <summary>ค่าสินค้าที่รับเข้าสต๊อกแล้ว? (ซื้อของเข้าคลัง)</summary>
            <div class="pv-field">
                <label for="<%= ddlGoodsReceipt.ClientID %>">เลือกการรับของ</label>
                <asp:DropDownList ID="ddlGoodsReceipt" runat="server" />
                <asp:Button ID="btnAddGrniLine" runat="server" Text="＋ เพิ่มเป็นรายการชำระค่าสินค้า" CssClass="pv-btn pv-btn-soft" OnClick="btnAddGrniLine_Click" />
                <div class="pv-hint">รายการนี้ลงบัญชีล้างรับของ (GRNI) แทนค่าใช้จ่าย — สินค้าลงต้นทุนตอนขายแล้ว ห้ามลงค่าใช้จ่ายซ้ำ · ภาษีซื้อเคลมตามปกติ</div>
                <asp:Literal ID="litGrniInfo" runat="server" EnableViewState="false"></asp:Literal>
            </div>
        </details>

        <div class="pv-line-entry">
            <div class="pv-field">
                <label for="<%= ddlLineAccount.ClientID %>">หมวดค่าใช้จ่าย <span class="pv-req" aria-hidden="true">*</span></label>
                <asp:DropDownList ID="ddlLineAccount" runat="server" />
                <div class="pv-hint">หมวดเดิมของระบบอยู่ด้านบน (แสดงบัญชี NextAcc ที่ผูกไว้) · ด้านล่างเป็นบัญชีค่าใช้จ่าย/สินทรัพย์ในผัง NextAcc ที่ยังไม่มีหมวด</div>
            </div>
            <div class="pv-grid pv-grid-line">
                <div class="pv-field">
                    <label for="<%= TextBox1.ClientID %>">รายละเอียด <span class="pv-req" aria-hidden="true">*</span></label>
                    <asp:TextBox ID="TextBox1" runat="server" autocomplete="off" placeholder="เช่น ค่าซ่อมแอร์ห้อง 203"></asp:TextBox>
                </div>
                <div class="pv-field">
                    <label for="<%= TextBox2.ClientID %>" id="pvAmtLabel">จำนวนเงิน (ไม่รวม VAT) <span class="pv-req" aria-hidden="true">*</span></label>
                    <div class="pv-money"><asp:TextBox ID="TextBox2" runat="server" inputmode="decimal" autocomplete="off" placeholder="0.00"></asp:TextBox><span>บาท</span></div>
                </div>
            </div>
            <div id="pvAiLine" class="pv-ai" aria-live="polite"></div>
            <asp:Literal ID="litLineMsg" runat="server" EnableViewState="false"></asp:Literal>
            <asp:Button ID="Button2" runat="server" Text="＋ เพิ่มรายการ" CssClass="pv-btn pv-btn-add" OnClick="Button2_Click" />
        </div>

        <div class="pv-table-wrap">
            <asp:GridView ID="GridView1" runat="server" AutoGenerateColumns="False" OnRowDeleting="GridView1_RowDeleting" CssClass="pv-lines">
                <Columns>
                    <asp:BoundField DataField="Number" HeaderText="#" />
                    <asp:BoundField DataField="PaidTypeName" HeaderText="หมวด" />
                    <asp:BoundField DataField="Detail" HeaderText="รายละเอียด" />
                    <asp:BoundField DataField="Amount" HeaderText="จำนวนเงิน" DataFormatString="{0:N2}" ItemStyle-CssClass="pv-num" HeaderStyle-CssClass="pv-num" />
                    <asp:CommandField ShowDeleteButton="True" ButtonType="Button" DeleteText="ลบ" ControlStyle-CssClass="pv-btn pv-btn-del" />
                </Columns>
                <EmptyDataTemplate><div class="pv-empty">ยังไม่มีรายการ — เลือกหมวด กรอกรายละเอียดและจำนวนเงิน แล้วกด "เพิ่มรายการ"</div></EmptyDataTemplate>
            </asp:GridView>
        </div>

        <details class="pv-adv" id="pvHeadCat">
            <summary>หมวดหลักของใบ (ใช้ในรายงาน): <b id="pvHeadCatText"></b></summary>
            <div class="pv-field">
                <label for="<%= DropDownList3.ClientID %>">หมวดหลักของใบ</label>
                <asp:DropDownList ID="DropDownList3" runat="server" AppendDataBoundItems="true" AutoPostBack="True" OnSelectedIndexChanged="DropDownList3_SelectedIndexChanged">
                    <asp:ListItem>---โปรดเลือก---</asp:ListItem>
                </asp:DropDownList>
                <asp:SqlDataSource ID="SqlDataSource3" runat="server" ConnectionString="<%$ ConnectionStrings:TaketimeConnectionString %>" SelectCommand="SELECT * FROM [Account_Paid_Type] WHERE ([Status] = 'True')">
                    <SelectParameters>
                        <asp:Parameter DefaultValue="True" Name="Status" Type="Boolean" />
                    </SelectParameters>
                </asp:SqlDataSource>
                <div class="pv-hint">ระบบเลือกให้จากรายการแรก — เปลี่ยนเฉพาะเมื่อใบนี้ควรนับในหมวดอื่น (เช่น เงินเดือน)</div>
            </div>
        </details>
        <%-- control เดิม (ซ่อน): ค่าถูกตั้งจาก "หมวดค่าใช้จ่าย" ก่อนโค้ดเพิ่มรายการเดิมทำงาน --%>
        <div class="pv-legacy" hidden>
            <asp:DropDownList ID="ddlLineCategory" runat="server" AppendDataBoundItems="true">
                <asp:ListItem Value="">---เลือกหมวดค่าใช้จ่าย---</asp:ListItem>
            </asp:DropDownList>
            <asp:DropDownList ID="ddlLineChargeNexaacc" runat="server" />
        </div>
    </section>

    <%-- ③ การชำระ --%>
    <section class="pv-card" aria-labelledby="pvS3">
        <h2 class="pv-card-h" id="pvS3"><span class="pv-step">3</span> การชำระ</h2>
        <div class="pv-field">
            <label for="<%= ddlPayFrom.ClientID %>">จ่ายจาก <span class="pv-req" aria-hidden="true">*</span></label>
            <asp:DropDownList ID="ddlPayFrom" runat="server" />
            <div class="pv-hint" id="pvPayFromHint">แหล่งเงินในระบบพร้อมบัญชี NextAcc ที่ผูกไว้ — ใบนี้จะลง Cr บัญชีนั้น</div>
        </div>
        <div class="pv-field pv-sub-field" id="pvPaidHowAs" hidden>
            <label for="<%= DropDownList2.ClientID %>">บันทึกในระบบเป็นวิธีจ่าย <span class="pv-req" aria-hidden="true">*</span></label>
            <asp:DropDownList ID="DropDownList2" runat="server" AppendDataBoundItems="true">
                <asp:ListItem>---โปรดเลือก---</asp:ListItem>
            </asp:DropDownList>
            <asp:SqlDataSource ID="SqlDataSource2" runat="server" ConnectionString="<%$ ConnectionStrings:TaketimeConnectionString %>" SelectCommand="SELECT * FROM [Account_Paid_How] WHERE ([Status] = 'True')">
                <SelectParameters>
                    <asp:Parameter DefaultValue="True" Name="Status" Type="Boolean" />
                </SelectParameters>
            </asp:SqlDataSource>
            <div class="pv-hint">เลือกบัญชีที่ยังไม่มีแหล่งเงินผูก — ระบุประเภทในระบบ (ใช้ในรายงาน และตัดสินว่าเป็นเงินสด/ธนาคารที่จ่ายทันที)</div>
        </div>
        <div class="pv-legacy" hidden>
            <asp:DropDownList ID="ddlPaidHowNexaacc" runat="server" />
        </div>
        <div class="pv-field">
            <asp:CheckBox ID="chkIsCredit" runat="server" Text=" ซื้อเชื่อ — ยังไม่จ่ายเงิน" CssClass="pv-check" />
            <div class="pv-hint">ติ๊กเมื่อยังไม่ได้จ่าย: NextAcc ตั้งเจ้าหนี้ไว้ · จ่ายภายหลังที่ <a href="/Account/CreditPurchasePayment">ชำระใบซื้อเชื่อ</a></div>
        </div>
        <div id="pvAiPay" class="pv-ai" aria-live="polite"></div>
    </section>

    <%-- ④ ภาษี --%>
    <section class="pv-card" aria-labelledby="pvS4">
        <h2 class="pv-card-h" id="pvS4"><span class="pv-step">4</span> ภาษี</h2>
        <div class="pv-grid">
            <div class="pv-field">
                <label for="<%= DropDownList4.ClientID %>">ภาษีมูลค่าเพิ่ม (VAT) <span class="pv-req" aria-hidden="true">*</span></label>
                <asp:DropDownList ID="DropDownList4" runat="server" AutoPostBack="True" OnSelectedIndexChanged="DropDownList4_SelectedIndexChanged" AppendDataBoundItems="true">
                    <asp:ListItem>---โปรดเลือก---</asp:ListItem>
                </asp:DropDownList>
                <asp:SqlDataSource ID="SqlDataSource4" runat="server" ConnectionString="<%$ ConnectionStrings:TaketimeConnectionString %>" SelectCommand="SELECT * FROM [Account_Vat_Type] WHERE ([Status] = 'True')">
                    <SelectParameters>
                        <asp:Parameter DefaultValue="True" Name="Status" Type="Boolean" />
                    </SelectParameters>
                </asp:SqlDataSource>
            </div>
            <div class="pv-field">
                <label for="<%= ddlVatClaim.ClientID %>">ภาษีซื้อ</label>
                <asp:DropDownList ID="ddlVatClaim" runat="server">
                    <asp:ListItem Value="1" Text="เคลมภาษีซื้อได้ (มีใบกำกับเต็มรูป)" Selected="True" />
                    <asp:ListItem Value="0" Text="เคลมไม่ได้ — รวม VAT เข้าค่าใช้จ่าย (§82/5)" />
                </asp:DropDownList>
                <div class="pv-hint">ใบเสร็จธรรมดา/ใบกำกับอย่างย่อ/ค่ารับรอง เคลมไม่ได้</div>
            </div>
        </div>
        <div class="pv-grid">
            <div class="pv-field">
                <label for="<%= ddlWHTRate.ClientID %>">หัก ณ ที่จ่าย</label>
                <asp:DropDownList ID="ddlWHTRate" runat="server" AutoPostBack="True" OnSelectedIndexChanged="ddlWHTRate_SelectedIndexChanged">
                    <asp:ListItem Text="ไม่หัก" Value="0" />
                    <asp:ListItem Text="1% (ค่าขนส่ง/เบี้ยประกัน)" Value="1" />
                    <asp:ListItem Text="1.5%" Value="1.5" />
                    <asp:ListItem Text="2% (ค่าโฆษณา)" Value="2" />
                    <asp:ListItem Text="3% (ค่าบริการ/จ้างทำของ)" Value="3" />
                    <asp:ListItem Text="5% (ค่าเช่า)" Value="5" />
                    <asp:ListItem Text="10%" Value="10" />
                </asp:DropDownList>
            </div>
            <div class="pv-field">
                <label for="<%= txtWHTAmount.ClientID %>">ยอดหัก ณ ที่จ่าย</label>
                <div class="pv-money"><asp:TextBox ID="txtWHTAmount" runat="server" Enabled="False" Text="0.00"></asp:TextBox><span>บาท</span></div>
            </div>
        </div>

        <div class="pv-cil">
            <asp:CheckBox ID="chkCertInLieu" runat="server" AutoPostBack="True" OnCheckedChanged="chkCertInLieu_CheckedChanged"
                Text=" ผู้รับเงินออกใบเสร็จไม่ได้ — ออก &quot;ใบรับรองแทนใบเสร็จรับเงิน&quot;" CssClass="pv-check" />
            <div class="pv-hint">เช่น ค่าแท็กซี่ / แผงลอย / ลูกจ้างรายวัน — จ่ายเงินสด ไม่มีภาษีซื้อ</div>
            <asp:Panel ID="pnlCertInLieu" runat="server" Visible="false" CssClass="pv-cil-form">
                <div class="pv-field">
                    <label for="<%= txtCilReason.ClientID %>">เหตุผลที่ไม่ได้รับใบเสร็จ <span class="pv-req" aria-hidden="true">*</span></label>
                    <asp:TextBox ID="txtCilReason" runat="server" MaxLength="500" placeholder="เช่น ผู้รับเงินเป็นผู้ขายรายย่อย ไม่มีใบเสร็จรับเงิน" data-pv-required="1" />
                </div>
                <div class="pv-grid">
                    <div class="pv-field">
                        <label for="<%= txtCilPayeeName.ClientID %>">ผู้รับเงิน (ถ้าไม่ตรงกับผู้ขาย)</label>
                        <asp:TextBox ID="txtCilPayeeName" runat="server" MaxLength="200" placeholder="ชื่อผู้รับเงิน" />
                    </div>
                    <div class="pv-field">
                        <label for="<%= txtCilPayeeAddress.ClientID %>">ที่อยู่ผู้รับเงิน</label>
                        <asp:TextBox ID="txtCilPayeeAddress" runat="server" MaxLength="500" placeholder="ถ้ามี" />
                    </div>
                    <div class="pv-field">
                        <label for="<%= txtCilCertifierName.ClientID %>">ผู้รับรอง <span class="pv-req" aria-hidden="true">*</span></label>
                        <asp:TextBox ID="txtCilCertifierName" runat="server" MaxLength="200" placeholder="ชื่อผู้รับรอง" data-pv-required="1" />
                    </div>
                    <div class="pv-field">
                        <label for="<%= txtCilCertifierPosition.ClientID %>">ตำแหน่งผู้รับรอง</label>
                        <asp:TextBox ID="txtCilCertifierPosition" runat="server" MaxLength="200" placeholder="ตำแหน่ง" />
                    </div>
                    <div class="pv-field">
                        <label for="<%= txtCilWitnessName.ClientID %>">พยาน / ผู้อนุมัติ</label>
                        <asp:TextBox ID="txtCilWitnessName" runat="server" MaxLength="200" placeholder="ชื่อพยาน/ผู้อนุมัติ" />
                    </div>
                    <div class="pv-field">
                        <label for="<%= txtCilWitnessPosition.ClientID %>">ตำแหน่งพยาน</label>
                        <asp:TextBox ID="txtCilWitnessPosition" runat="server" MaxLength="200" placeholder="ตำแหน่ง" />
                    </div>
                </div>
            </asp:Panel>
        </div>
    </section>

    <%-- ⑤ เอกสารแนบ + สินทรัพย์ --%>
    <section class="pv-card" aria-labelledby="pvS5">
        <h2 class="pv-card-h" id="pvS5"><span class="pv-step">5</span> เอกสารแนบ</h2>
        <div class="pv-grid">
            <div class="pv-field">
                <label for="<%= FileUpload1.ClientID %>">ไฟล์ (ใบกำกับ ใบเสร็จ สัญญา)</label>
                <asp:FileUpload ID="FileUpload1" runat="server" />
            </div>
            <div class="pv-field">
                <label for="<%= TextBox7.ClientID %>">ชื่อไฟล์ที่จะเก็บ</label>
                <asp:TextBox ID="TextBox7" runat="server" Text="" placeholder="เช่น ใบกำกับภาษี"></asp:TextBox>
            </div>
        </div>
        <asp:Literal ID="litUploadMsg" runat="server" EnableViewState="false"></asp:Literal>
        <asp:Button ID="Button4" runat="server" Text="อัปโหลดไฟล์" CssClass="pv-btn pv-btn-soft" OnClick="Button4_Click" />
        <div class="pv-table-wrap">
            <asp:GridView ID="GridView2" runat="server" AutoGenerateColumns="False"
                OnRowDeleted="GridView2_RowDeleted" OnRowDeleting="GridView2_RowDeleting"
                CssClass="file-grid" OnRowDataBound="GridView2_RowDataBound">
                <Columns>
                    <asp:TemplateField HeaderText="ไฟล์แนบ">
                        <ItemTemplate>
                            <asp:HyperLink ID="lnkViewFile" runat="server"
                                NavigateUrl='<%# GetFileUrl(Eval("Name")) %>'
                                Text='<%# "📄 " + Eval("Name") %>'
                                Target="_blank"
                                CssClass="file-link">
                            </asp:HyperLink>
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:CommandField ShowDeleteButton="True" DeleteText="ลบ" ButtonType="Button" HeaderText="">
                        <ControlStyle CssClass="btn-delete-file" />
                    </asp:CommandField>
                </Columns>
                <EmptyDataTemplate>
                    <div class="pv-empty">ยังไม่มีไฟล์แนบ</div>
                </EmptyDataTemplate>
            </asp:GridView>
        </div>

        <div class="pv-asset">
            <asp:CheckBox ID="chkRecordAsset" runat="server" Text=" บันทึกเป็นสินทรัพย์ถาวร (คอมพิวเตอร์ เฟอร์นิเจอร์ ยานพาหนะ ฯลฯ)"
                AutoPostBack="true" OnCheckedChanged="chkRecordAsset_CheckedChanged" CssClass="pv-check" />
            <asp:Panel ID="pnlAssetDetails" runat="server" Visible="false" CssClass="pv-asset-form">
                <div class="pv-grid">
                    <div class="pv-field">
                        <label for="<%= ddlAssetCategory.ClientID %>">หมวดหมู่สินทรัพย์</label>
                        <asp:DropDownList ID="ddlAssetCategory" runat="server" AutoPostBack="true" OnSelectedIndexChanged="ddlAssetCategory_SelectedIndexChanged"></asp:DropDownList>
                    </div>
                    <div class="pv-field">
                        <label for="<%= txtAssetName.ClientID %>">ชื่อสินทรัพย์ <span class="pv-req" aria-hidden="true">*</span></label>
                        <asp:TextBox ID="txtAssetName" runat="server" placeholder="เช่น คอมพิวเตอร์ตั้งโต๊ะ, แอร์ห้องประชุม"></asp:TextBox>
                    </div>
                    <div class="pv-field">
                        <label for="<%= txtAssetQuantity.ClientID %>">จำนวน (ชิ้น)</label>
                        <asp:TextBox ID="txtAssetQuantity" runat="server" TextMode="Number" Text="1"></asp:TextBox>
                        <div class="pv-hint">มากกว่า 1 ชิ้น = แยกสินทรัพย์ทีละชิ้น ราคาต่อชิ้นคำนวณให้</div>
                    </div>
                    <div class="pv-field">
                        <label for="<%= txtAssetBrand.ClientID %>">ยี่ห้อ / รุ่น</label>
                        <div class="pv-inline">
                            <asp:TextBox ID="txtAssetBrand" runat="server" placeholder="ยี่ห้อ"></asp:TextBox>
                            <asp:TextBox ID="txtAssetModel" runat="server" placeholder="รุ่น"></asp:TextBox>
                        </div>
                    </div>
                    <div class="pv-field">
                        <label for="<%= txtAssetSerial.ClientID %>">หมายเลขซีเรียล</label>
                        <asp:TextBox ID="txtAssetSerial" runat="server"></asp:TextBox>
                    </div>
                    <div class="pv-field">
                        <label for="<%= txtAssetLocation.ClientID %>">สถานที่ตั้ง</label>
                        <asp:TextBox ID="txtAssetLocation" runat="server" placeholder="เช่น สำนักงาน, ห้องประชุม"></asp:TextBox>
                    </div>
                    <div class="pv-field">
                        <label for="<%= txtAssetUsefulLife.ClientID %>">อายุการใช้งาน (ปี)</label>
                        <asp:TextBox ID="txtAssetUsefulLife" runat="server" TextMode="Number" Text="5"></asp:TextBox>
                    </div>
                    <div class="pv-field">
                        <label for="<%= txtAssetResidual.ClientID %>">มูลค่าซาก (บาท)</label>
                        <asp:TextBox ID="txtAssetResidual" runat="server" TextMode="Number" Text="0"></asp:TextBox>
                    </div>
                </div>
            </asp:Panel>
        </div>
    </section>

    <%-- ⑥ สรุป --%>
    <section class="pv-card pv-summary" aria-labelledby="pvS6">
        <h2 class="pv-card-h" id="pvS6"><span class="pv-step">6</span> สรุปยอด</h2>
        <div class="pv-totals">
            <div class="pv-trow">
                <label for="<%= TextBox3.ClientID %>">ยอดก่อนภาษีมูลค่าเพิ่ม</label>
                <div class="pv-money"><asp:TextBox ID="TextBox3" runat="server" Enabled="False"></asp:TextBox><span>บาท</span></div>
            </div>
            <div class="pv-trow">
                <label for="<%= TextBox4.ClientID %>"><asp:Label ID="Label1" runat="server" Text="ภาษีมูลค่าเพิ่ม"></asp:Label></label>
                <div class="pv-money"><asp:TextBox ID="TextBox4" runat="server" Enabled="False"></asp:TextBox><span>บาท</span></div>
            </div>
            <div class="pv-trow">
                <span>หัก ณ ที่จ่าย</span>
                <div class="pv-money pv-ro"><span id="pvTotWht">0.00</span><span>บาท</span></div>
            </div>
            <div class="pv-trow pv-trow-net">
                <label for="<%= TextBox6.ClientID %>">ยอดจ่ายสุทธิ</label>
                <div class="pv-money"><asp:TextBox ID="TextBox6" runat="server" Enabled="False"></asp:TextBox><span>บาท</span></div>
            </div>
        </div>
        <span id="voucherValidationWarn" class="pv-calc" role="status" style="display:none;"></span>
        <asp:CheckBox ID="CheckBox1" runat="server" Text=" แก้ไขยอดเงินด้วยตนเอง (ยอดในใบกำกับปัดเศษต่างจากที่คำนวณ)" AutoPostBack="True" OnCheckedChanged="CheckBox1_CheckedChanged" CssClass="pv-check" />
    </section>

    <div id="pvFormMsg" class="pv-formmsg"><asp:Literal ID="litFormMsg" runat="server" EnableViewState="false"></asp:Literal></div>
    <div class="pv-savebar">
        <div class="pv-savebar-net"><span>ยอดจ่ายสุทธิ</span><b id="pvNetBar">0.00</b></div>
        <asp:Button ID="Button3" runat="server" Text="บันทึกใบสำคัญจ่าย" CssClass="pv-btn pv-btn-save" OnClick="Button3_Click" />
    </div>
</div>

    <script src="<%= ResolveUrl("~/Scripts/pv-voucher.js?v=1") %>"></script>
    <rsweb:reportviewer Visible="false" ID="ReportViewer2" runat="server" BackColor="" ClientIDMode="AutoID" HighlightBackgroundColor="" InternalBorderColor="204, 204, 204" InternalBorderStyle="Solid" InternalBorderWidth="1px" LinkActiveColor="" LinkActiveHoverColor="" LinkDisabledColor="" PrimaryButtonBackgroundColor="" PrimaryButtonForegroundColor="" PrimaryButtonHoverBackgroundColor="" PrimaryButtonHoverForegroundColor="" SecondaryButtonBackgroundColor="" SecondaryButtonForegroundColor="" SecondaryButtonHoverBackgroundColor="" SecondaryButtonHoverForegroundColor="" SplitterBackColor="" ToolbarDividerColor="" ToolbarForegroundColor="" ToolbarForegroundDisabledColor="" ToolbarHoverBackgroundColor="" ToolbarHoverForegroundColor="" ToolBarItemBorderColor="" ToolBarItemBorderStyle="Solid" ToolBarItemBorderWidth="1px" ToolBarItemHoverBackColor="" ToolBarItemPressedBorderColor="51, 102, 153" ToolBarItemPressedBorderStyle="Solid" ToolBarItemPressedBorderWidth="1px" ToolBarItemPressedHoverBackColor="153, 187, 226" Height="500px" CssClass="auto-style5" style="margin-top: 172px">
        <LocalReport EnableExternalImages="true" ReportPath="Account\Report\Payment.rdlc">
        </LocalReport>
    </rsweb:reportviewer>
</asp:Content>
