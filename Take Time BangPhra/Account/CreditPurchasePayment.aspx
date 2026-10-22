<%@ Page Title="ชำระใบซื้อเชื่อ" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="CreditPurchasePayment.aspx.cs" Inherits="Take_Time_BangPhra.Account.CreditPurchasePayment" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .cpp-wrap { max-width: 98%; margin: 20px auto; padding: 16px; background: #f5f7fa; border-radius: 10px; }
        .cpp-head { background: linear-gradient(135deg, #1565c0 0%, #1e88e5 100%); color: #fff; padding: 16px 20px; border-radius: 8px; margin-bottom: 16px; }
        .cpp-head h2 { margin: 0; font-size: 22px; }
        .cpp-head p { margin: 6px 0 0 0; font-size: 13px; opacity: .9; }
        .cpp-card { background: #fff; border-radius: 8px; padding: 16px; margin-bottom: 16px; box-shadow: 0 2px 4px rgba(0,0,0,.05); }
        .cpp-table { width: 100%; border-collapse: collapse; font-size: 13px; }
        .cpp-table th { background: #e3f2fd; color: #0d47a1; padding: 8px; text-align: left; }
        .cpp-table td { padding: 8px; border-bottom: 1px solid #eceff1; }
        .cpp-row { display: flex; gap: 14px; flex-wrap: wrap; margin-bottom: 10px; }
        .cpp-field { display: flex; flex-direction: column; gap: 4px; min-width: 220px; flex: 1; }
        .cpp-field label { font-weight: 600; font-size: 13px; color: #37474f; }
        .cpp-note { font-size: 12px; color: #607d8b; }
        .cpp-warn { background: #fff8e1; border: 1px solid #ffe082; border-radius: 6px; padding: 8px 10px; font-size: 13px; margin-bottom: 10px; }
        .btn-pay { background: #2e7d32; color: #fff; border: none; padding: 8px 18px; border-radius: 5px; cursor: pointer; }
        .btn-cancel { background: #eceff1; color: #37474f; border: none; padding: 8px 18px; border-radius: 5px; cursor: pointer; }
        .btn-sel { background: #1565c0; color: #fff; border: none; padding: 4px 10px; border-radius: 4px; cursor: pointer; }
    </style>

    <div class="cpp-wrap">
        <div class="cpp-head">
            <h2>💳 ชำระใบซื้อเชื่อ (เจ้าหนี้การค้า)</h2>
            <p>ใบสำคัญจ่ายที่ติ๊ก "เครดิต (ยังไม่จ่ายเงิน)" = ตั้งเจ้าหนี้ไว้ (NextAcc: เอกสารค่าใช้จ่าย Cr เจ้าหนี้). เมื่อจ่ายจริง บันทึกที่นี่ → NextAcc บันทึกรับชำระกับเอกสารเดิม (Dr เจ้าหนี้ / Cr แหล่งเงิน) พร้อมลายเซ็นผู้จ่ายของผู้จัดทำใบเดิม</p>
        </div>

        <asp:Literal ID="litInfo" runat="server"></asp:Literal>

        <div class="cpp-card">
            <asp:CheckBox ID="chkShowPaid" runat="server" Text=" แสดงใบที่จ่ายแล้วด้วย" AutoPostBack="true" OnCheckedChanged="chkShowPaid_CheckedChanged" />
            <div style="margin-top: 10px; overflow-x: auto;">
                <asp:GridView ID="gvCredit" runat="server" AutoGenerateColumns="False" CssClass="cpp-table" GridLines="None"
                    OnRowCommand="gvCredit_RowCommand" EmptyDataText="ไม่มีใบซื้อเชื่อค้างจ่าย">
                    <Columns>
                        <asp:BoundField DataField="ID" HeaderText="ใบสำคัญจ่าย" />
                        <asp:BoundField DataField="Created_Date" HeaderText="วันที่" DataFormatString="{0:dd/MM/yyyy}" />
                        <asp:BoundField DataField="Vendor_Name" HeaderText="ผู้ขาย" />
                        <asp:BoundField DataField="Total_Amount" HeaderText="ยอดค้างจ่าย" DataFormatString="{0:N2}" ItemStyle-HorizontalAlign="Right" />
                        <asp:BoundField DataField="WHT_Amount" HeaderText="หัก ณ ที่จ่าย" DataFormatString="{0:N2}" ItemStyle-HorizontalAlign="Right" />
                        <asp:BoundField DataField="Nexaacc_Document_Number" HeaderText="เอกสาร NextAcc" />
                        <asp:BoundField DataField="Linked_Goods_Receipt" HeaderText="การรับของ" />
                        <asp:BoundField DataField="Paid_Info" HeaderText="สถานะ" />
                        <asp:TemplateField HeaderText="">
                            <ItemTemplate>
                                <asp:Button ID="btnSelect" runat="server" Text="จ่าย" CssClass="btn-sel" CommandName="PayCredit"
                                    CommandArgument='<%# Eval("ID") %>' Visible='<%# Convert.ToString(Eval("Is_Paid")) != "1" %>' />
                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                </asp:GridView>
            </div>
        </div>

        <asp:Panel ID="pnlPay" runat="server" CssClass="cpp-card" Visible="false">
            <h3 style="margin-top: 0;">บันทึกจ่ายชำระ <asp:Label ID="lblPayDoc" runat="server" /></h3>
            <asp:Literal ID="litPayInfo" runat="server"></asp:Literal>
            <div class="cpp-row">
                <div class="cpp-field">
                    <label>วันที่จ่าย</label>
                    <asp:TextBox ID="txtPayDate" runat="server" TextMode="Date" CssClass="form-control" />
                </div>
                <div class="cpp-field">
                    <label>จ่ายจาก (แหล่งเงิน)</label>
                    <asp:DropDownList ID="ddlPayPaidHow" runat="server" CssClass="form-control" AppendDataBoundItems="true">
                        <asp:ListItem Value="" Text="— เลือกแหล่งเงิน —" />
                    </asp:DropDownList>
                </div>
                <div class="cpp-field">
                    <label>บังคับบัญชีแหล่งเงิน (ผังบัญชี NextAcc)</label>
                    <asp:DropDownList ID="ddlPayNexaaccAccount" runat="server" CssClass="form-control" />
                    <span class="cpp-note">เว้นว่าง = ใช้บัญชีที่ผูกกับแหล่งเงิน (Account_Paid_How)</span>
                </div>
                <div class="cpp-field">
                    <label>ยอดจ่าย</label>
                    <asp:TextBox ID="txtPayAmount" runat="server" CssClass="form-control" />
                    <span class="cpp-note">ค่าเริ่มต้น = ยอดค้างจ่ายของใบ (หัก ณ ที่จ่ายถูกหักไว้แล้วตอนตั้งหนี้ — ไม่หักซ้ำ)</span>
                </div>
            </div>
            <asp:Button ID="btnConfirmPay" runat="server" Text="💾 บันทึกจ่ายชำระ" CssClass="btn-pay" OnClick="btnConfirmPay_Click"
                OnClientClick="return confirm('ยืนยันบันทึกจ่ายชำระใบซื้อเชื่อนี้?');" />
            <asp:Button ID="btnCancelPay" runat="server" Text="ยกเลิก" CssClass="btn-cancel" OnClick="btnCancelPay_Click" CausesValidation="false" />
            <asp:HiddenField ID="hfPayDoc" runat="server" />
        </asp:Panel>
    </div>
</asp:Content>
