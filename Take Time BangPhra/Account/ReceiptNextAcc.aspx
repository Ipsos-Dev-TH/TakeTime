<%@ Page Title="งาน NextAcc ของใบเสร็จ" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ReceiptNextAcc.aspx.cs" Inherits="Take_Time_BangPhra.Account.ReceiptNextAcc" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .rx-wrap { max-width: 920px; margin: 16px auto 40px; padding: 0 12px; }
        .rx-head { background: linear-gradient(135deg, #1e5f74 0%, #2e8b9a 100%); color: #fff; padding: 16px 20px; border-radius: 10px; margin-bottom: 14px; }
        .rx-head h2 { margin: 0; font-size: 21px; }
        .rx-head p { margin: 6px 0 0; font-size: 13px; opacity: .92; line-height: 1.6; }
        .rx-card { background: #fff; border-radius: 10px; padding: 16px; margin-bottom: 14px; box-shadow: 0 2px 6px rgba(0,0,0,.06); }
        .rx-card h3 { margin: 0 0 6px; font-size: 17px; color: #1e3a44; }
        .rx-row { display: flex; gap: 12px; flex-wrap: wrap; margin-bottom: 10px; align-items: flex-end; }
        .rx-field { display: flex; flex-direction: column; gap: 4px; min-width: 200px; flex: 1; }
        .rx-field label { font-weight: 600; font-size: 13px; color: #37474f; margin: 0; }
        .rx-note { font-size: 12px; color: #5f7480; line-height: 1.6; }
        .rx-kv { display: grid; grid-template-columns: 170px 1fr; gap: 4px 12px; font-size: 14px; }
        .rx-kv div:nth-child(odd) { color: #607d8b; }
        .rx-alert { padding: 10px 14px; border-radius: 8px; margin-bottom: 12px; font-size: 14px; line-height: 1.7; word-break: break-word; }
        .rx-alert.ok { background: #e8f5e9; color: #1b5e20; } .rx-alert.err { background: #fdecea; color: #a12626; }
        .rx-alert.warn { background: #fff8e1; color: #6d4c00; } .rx-alert.info { background: #e3f2fd; color: #0d47a1; }
        .btn-rx { background: #1e5f74; color: #fff; border: 0; padding: 8px 18px; border-radius: 6px; cursor: pointer; font-weight: 600; }
        .btn-rx:hover { background: #17495a; color: #fff; }
        .btn-rx2 { background: #eceff1; color: #37474f; border: 0; padding: 7px 14px; border-radius: 6px; text-decoration: none; display: inline-block; }
        .rx-t { width: 100%; border-collapse: collapse; font-size: 13px; }
        .rx-t th, .rx-t td { border-bottom: 1px solid #eceff1; padding: 6px 8px; text-align: left; vertical-align: top; }
        .rx-t th { background: #f5f7f8; color: #455a64; font-weight: 600; }
        .rx-scroll { overflow-x: auto; }
        .rx-chip { display: inline-block; padding: 1px 8px; border-radius: 10px; font-size: 12px; background: #eceff1; color: #37474f; }
        .rx-chip.DONE { background: #e8f5e9; color: #1b5e20; } .rx-chip.BLOCKED { background: #fff8e1; color: #6d4c00; }
        .rx-chip.FAILED { background: #fdecea; color: #a12626; } .rx-chip.QUEUED { background: #e3f2fd; color: #0d47a1; }
        @media (max-width: 640px) { .rx-wrap { padding: 0 6px; } .rx-field { min-width: 140px; } .rx-kv { grid-template-columns: 1fr; } .rx-kv div:nth-child(odd) { margin-top: 6px; } }
    </style>

    <div class="rx-wrap">
        <div class="rx-head">
            <h2>💼 งาน NextAcc ของใบเสร็จ <asp:Literal ID="litRn" runat="server" /></h2>
            <p>ออกใบกำกับภาษีเต็มรูปให้ลูกค้าที่ขอทีหลัง · แก้แหล่งเงินที่เลือกผิด · ส่งเอกสารทางอีเมล — ทำบน NextAcc โดยไม่ยกเลิกเอกสารเดิม
               (เลขเอกสารคงเดิม) เมื่อ NextAcc อนุญาต</p>
        </div>

        <asp:Literal ID="litMsg" runat="server" />

        <asp:Panel ID="pnlMain" runat="server">
            <div class="rx-card">
                <h3>ข้อมูลใบเสร็จ</h3>
                <asp:Literal ID="litInfo" runat="server" />
            </div>

            <%-- ① ลูกค้าขอใบกำกับภาษีเต็มรูปทีหลัง --%>
            <asp:Panel ID="pnlFti" runat="server" CssClass="rx-card">
                <h3>🧾 ลูกค้าขอใบกำกับภาษีเต็มรูป</h3>
                <div class="rx-note" style="margin-bottom:10px">
                    ใบเสร็จ/ใบกำกับอย่างย่อที่ออกไปแล้ว: NextAcc ออก <b>ใบกำกับภาษีเต็มรูป "แทน"</b> ใบเดิม (วันที่เดิม ไม่มีรายการบัญชีใหม่ ใบเดิมไม่นับซ้ำในรายงานภาษีขาย) ·
                    ใบที่เป็นผู้ซื้อรายนี้อยู่แล้วแค่ข้อมูลไม่ครบ: ระบบเติมข้อมูลผู้ซื้อ แล้วเอกสารเดิมจะพิมพ์เป็นใบกำกับเต็มรูปเอง (เลขเดิม) ·
                    ใบที่ออกในนาม "ลูกค้าไม่ประสงค์รับใบกำกับ" ต้องยกเลิกแล้วออกใหม่ (เลขเอกสาร NextAcc เปลี่ยน) — ทำเมื่อติ๊กยอมรับเท่านั้น
                </div>
                <div class="rx-row">
                    <div class="rx-field" style="flex:2">
                        <label>ชื่อผู้ซื้อ (บริษัท / ชื่อ-นามสกุล)</label>
                        <asp:TextBox ID="txtName" runat="server" CssClass="form-control" MaxLength="200" />
                    </div>
                    <div class="rx-field">
                        <label>ประเภท</label>
                        <asp:DropDownList ID="ddlType" runat="server" CssClass="form-control">
                            <asp:ListItem Value="" Text="ตามเลขผู้เสียภาษี" />
                            <asp:ListItem Value="J" Text="นิติบุคคล" />
                            <asp:ListItem Value="I" Text="บุคคลธรรมดา" />
                        </asp:DropDownList>
                    </div>
                </div>
                <div class="rx-row">
                    <div class="rx-field">
                        <label>เลขประจำตัวผู้เสียภาษี 13 หลัก</label>
                        <asp:TextBox ID="txtTaxId" runat="server" CssClass="form-control" MaxLength="17" inputmode="numeric" />
                    </div>
                    <div class="rx-field" style="max-width:160px">
                        <label>สาขา (5 หลัก)</label>
                        <asp:TextBox ID="txtBranch" runat="server" CssClass="form-control" MaxLength="5" inputmode="numeric" placeholder="00000" />
                    </div>
                    <div class="rx-field" style="max-width:200px">
                        <label>เบอร์โทร (ถ้าใบยังไม่มีผู้ซื้อ)</label>
                        <asp:TextBox ID="txtPhone" runat="server" CssClass="form-control" MaxLength="12" inputmode="tel" />
                    </div>
                </div>
                <div class="rx-row">
                    <div class="rx-field">
                        <label>ที่อยู่ (ตามที่ต้องการให้พิมพ์บนใบกำกับ)</label>
                        <asp:TextBox ID="txtAddress" runat="server" CssClass="form-control" TextMode="MultiLine" Rows="2" MaxLength="500" />
                    </div>
                </div>
                <div class="rx-row">
                    <div class="rx-field">
                        <label>เหตุผล / หมายเหตุ (พิมพ์บนใบแทน — ไม่บังคับ)</label>
                        <asp:TextBox ID="txtFtiReason" runat="server" CssClass="form-control" MaxLength="300" placeholder="เช่น ลูกค้าขอใบกำกับเต็มรูปเพื่อเคลมภาษีซื้อ" />
                    </div>
                </div>
                <div class="rx-row">
                    <asp:CheckBox ID="chkRecreate" runat="server" Text=" ถ้าออกใบแทนไม่ได้ ยอมให้ยกเลิกเอกสารเดิมแล้วออกใหม่ในนามผู้ซื้อ (เลขเอกสาร NextAcc เปลี่ยน)" />
                </div>
                <asp:Button ID="btnFti" runat="server" Text="ออกใบกำกับภาษีเต็มรูป" CssClass="btn-rx" OnClick="btnFti_Click"
                    OnClientClick="if (this.getAttribute('data-busy')) return false; if (!confirm('ส่งข้อมูลผู้ซื้อไป NextAcc และออกใบกำกับภาษีเต็มรูป?')) return false; this.setAttribute('data-busy','1'); this.value='กำลังส่ง…'; return true;" />
            </asp:Panel>

            <%-- ② แก้แหล่งเงินที่เลือกผิด --%>
            <asp:Panel ID="pnlRcl" runat="server" CssClass="rx-card">
                <h3>🏦 แก้แหล่งเงินที่เลือกผิด</h3>
                <div class="rx-note" style="margin-bottom:10px">
                    เช่น เลือก "โอนกสิกร" แต่เงินเข้ากรุงไทย — NextAcc ลงรายการแก้ (ย้ายเงินระหว่างบัญชี) ในงวดเดิม เลขเอกสารคงเดิม · ทำได้เฉพาะเอกสารที่มีแหล่งเงินบนตัวเอกสาร
                    (ใบรับมัดจำ/ใบเสร็จรับเงิน) ในงวดที่ยังเปิด และยังไม่ส่ง e-Tax/ยื่นภาษี
                </div>
                <div class="rx-row">
                    <div class="rx-field">
                        <label>แหล่งเงินปัจจุบัน</label>
                        <asp:TextBox ID="txtCurPaid" runat="server" CssClass="form-control" ReadOnly="true" />
                    </div>
                    <div class="rx-field">
                        <label>แหล่งเงินที่ถูกต้อง</label>
                        <asp:DropDownList ID="ddlPaidHow" runat="server" CssClass="form-control" />
                    </div>
                </div>
                <div class="rx-row">
                    <div class="rx-field">
                        <label>เหตุผล (อย่างน้อย 10 ตัวอักษร)</label>
                        <asp:TextBox ID="txtRclReason" runat="server" CssClass="form-control" MaxLength="450" />
                    </div>
                </div>
                <asp:Button ID="btnRcl" runat="server" Text="เปลี่ยนแหล่งเงิน" CssClass="btn-rx" OnClick="btnRcl_Click"
                    OnClientClick="if (this.getAttribute('data-busy')) return false; if (!confirm('เปลี่ยนแหล่งเงินของใบนี้?')) return false; this.setAttribute('data-busy','1'); this.value='กำลังส่ง…'; return true;" />
            </asp:Panel>

            <%-- ③ ส่งอีเมลเอกสารจาก NextAcc --%>
            <asp:Panel ID="pnlEmail" runat="server" CssClass="rx-card">
                <h3>✉️ ส่งเอกสารทางอีเมล (PDF จาก NextAcc)</h3>
                <div class="rx-row">
                    <div class="rx-field">
                        <label>เอกสาร</label>
                        <asp:DropDownList ID="ddlEmailDoc" runat="server" CssClass="form-control" />
                    </div>
                    <div class="rx-field">
                        <label>อีเมลผู้รับ</label>
                        <asp:TextBox ID="txtEmail" runat="server" CssClass="form-control" MaxLength="200" TextMode="Email" />
                    </div>
                </div>
                <asp:Button ID="btnEmail" runat="server" Text="ส่งอีเมล" CssClass="btn-rx" OnClick="btnEmail_Click"
                    OnClientClick="if (this.getAttribute('data-busy')) return false; this.setAttribute('data-busy','1'); this.value='กำลังส่ง…'; return true;" />
            </asp:Panel>

            <div class="rx-card">
                <h3>ประวัติ</h3>
                <div class="rx-scroll"><asp:Literal ID="litHistory" runat="server" /></div>
            </div>
            <a class="btn-rx2" href="<%= ResolveUrl("~/Account/CheckDocument_New") %>">กลับรายการเอกสารขาย</a>
        </asp:Panel>
    </div>
</asp:Content>
