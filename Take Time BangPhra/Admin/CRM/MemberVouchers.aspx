<%@ Page Title="Voucher & สิทธิ์สมาชิก" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="MemberVouchers.aspx.cs" Inherits="Take_Time_BangPhra.Admin.CRM.MemberVouchers" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .mv-wrap { max-width: 1080px; margin: 0 auto; padding: 18px 12px 60px; }
        .mv-head { background: linear-gradient(135deg, #4527a0, #7b1fa2); color: #fff; border-radius: 14px;
                   padding: 22px 26px; margin-bottom: 18px; }
        .mv-head h2 { margin: 0 0 6px; font-weight: 700; font-size: 1.5em; }
        .mv-head p { margin: 0; opacity: .92; font-size: 14px; }
        .mv-card { background: #fff; border-radius: 11px; box-shadow: 0 2px 10px rgba(0,0,0,.08);
                   padding: 18px 20px; margin-bottom: 16px; }
        .mv-card h3 { margin: 0 0 12px; font-size: 1.05em; color: #37474f; font-weight: 700; }
        .mv-msg { padding: 11px 15px; border-radius: 8px; margin-bottom: 14px; font-size: 14px; }
        .mv-ok { background: #e8f5e9; color: #1e7e42; } .mv-err { background: #ffebee; color: #c62828; }
        table.mv-t { width: 100%; border-collapse: collapse; font-size: 13.5px; }
        table.mv-t th { background: #f5f7f9; text-align: left; padding: 8px 10px; font-weight: 650;
                        color: #37474f; border-bottom: 2px solid #e3e9ed; }
        table.mv-t td { padding: 7px 10px; border-bottom: 1px solid #f0f3f5; vertical-align: middle; }
        .mv-t input[type=number], .mv-t input[type=text] { padding: 6px 9px; border: 1.2px solid #dbe2e7;
            border-radius: 7px; width: 90px; }
        .mv-redeem input { padding: 12px 14px; border: 2px solid #7b1fa2; border-radius: 10px;
                           font-size: 20px; letter-spacing: 2px; text-transform: uppercase; width: 240px; }
        .st { font-size: 11px; padding: 3px 9px; border-radius: 11px; font-weight: 700; white-space: nowrap; }
        .st-ISSUED { background: #e3f2fd; color: #1565c0; } .st-ACTIVATED { background: #fff3e0; color: #e65100; }
        .st-REDEEMED { background: #e8f5e9; color: #1e7e42; } .st-EXPIRED, .st-CANCELLED { background: #eceff1; color: #90a4ae; }
        .mv-field { margin-bottom: 10px; }
        .mv-field label { display: block; font-weight: 600; font-size: 13px; margin-bottom: 4px; color: #37474f; }
        .mv-field input, .mv-field select, .mv-field textarea { width: 100%; max-width: 460px; padding: 8px 11px;
            border: 1.5px solid #dbe2e7; border-radius: 8px; font-size: 13.5px; }
        .mv-inline { display: flex; gap: 10px; flex-wrap: wrap; align-items: flex-end; }
    </style>

    <div class="mv-wrap">
        <div class="mv-head">
            <h2><i class="fas fa-ticket-alt"></i> Voucher & สิทธิ์สมาชิก</h2>
            <p>ส่วนลดค่าห้องตามวันเข้าพัก · ออกแบบ/แจกคูปอง · แลกคูปองด้วยโค้ด · ประวัติการใช้</p>
        </div>

        <asp:Panel ID="pnlMsg" runat="server" Visible="false">
            <div id="divMsg" runat="server" class="mv-msg mv-ok"><asp:Literal ID="litMsg" runat="server" /></div>
        </asp:Panel>

        <%-- ① แลกคูปองด้วยโค้ด — วางบนสุดเพราะเป็นงานหน้าเคาน์เตอร์ที่ใช้บ่อยสุด --%>
        <div class="mv-card mv-redeem" style="border-left: 5px solid #7b1fa2;">
            <h3><i class="fas fa-qrcode"></i> แลกคูปอง (ลูกค้าโชว์โค้ดจากมือถือ)</h3>
            <asp:TextBox ID="txtRedeemCode" runat="server" placeholder="เช่น DRK-7K4MX" />
            <asp:TextBox ID="txtRedeemNote" runat="server" placeholder="บันทึก เช่น ลาเต้เย็น (ไม่บังคับ)"
                style="padding:12px 14px; border:1.5px solid #dbe2e7; border-radius:10px; min-width:240px;" />
            <asp:Button ID="btnRedeem" runat="server" Text="✅ แลกคูปอง" CssClass="btn btn-success btn-lg"
                OnClick="btnRedeem_Click" />
            <div style="font-size:12.5px; color:#90a4ae; margin-top:8px;">
                ระบบตรวจให้ครบ: โค้ดจริง · ลูกค้ากดใช้แล้ว · ยังไม่หมดเวลา · ยังไม่ถูกใช้ซ้ำ
            </div>
        </div>

        <%-- ② ส่วนลดค่าห้องตามวันเข้าพัก --%>
        <div class="mv-card">
            <h3><i class="fas fa-percent"></i> ส่วนลดค่าห้องตามวันเข้าพัก (ต่อระดับสมาชิก)</h3>
            <table class="mv-t">
                <tr><th>ระดับ</th><th>วันธรรมดา (จ-ศ) %</th><th>วันหยุด/นักขัตฤกษ์ %</th></tr>
                <asp:Literal ID="litDiscRows" runat="server" />
            </table>
            <div style="font-size:12px; color:#90a4ae; margin:8px 0 12px;">
                "วันหยุด" = เสาร์-อาทิตย์ และวันที่ตั้งราคาพิเศษไว้ในหน้า ราคาวันหยุด ·
                สมาชิกเห็นสิทธิ์นี้บนบัตรในมือถือ — พนักงานใช้เป็นเกณฑ์ให้ส่วนลดตอนจอง/เช็คอิน
            </div>
            <asp:Button ID="btnSaveDisc" runat="server" Text="💾 บันทึกส่วนลด" CssClass="btn btn-primary"
                OnClick="btnSaveDisc_Click" />
        </div>

        <%-- ③ แบบคูปอง (templates) --%>
        <div class="mv-card">
            <h3><i class="fas fa-layer-group"></i> แบบคูปอง</h3>
            <table class="mv-t">
                <tr><th>ชื่อ / เงื่อนไข</th><th>โค้ดขึ้นต้น</th><th>อายุ (วัน)</th><th>เวลาโค้ด (นาที)</th><th>สถานะ</th><th></th></tr>
                <asp:Literal ID="litTplRows" runat="server" />
            </table>

            <div style="margin-top:16px; padding-top:14px; border-top:1px dashed #e0e6ea;">
                <b style="font-size:13.5px;">➕ เพิ่ม/แก้แบบคูปอง</b>
                <asp:HiddenField ID="hfTplId" runat="server" Value="0" />
                <div class="mv-field" style="margin-top:10px;">
                    <label>ชื่อคูปอง</label>
                    <asp:TextBox ID="txtTplName" runat="server" placeholder="เช่น เครื่องดื่มฟรี 1 แก้ว" />
                </div>
                <div class="mv-field">
                    <label>เงื่อนไข (ลูกค้าและพนักงานเห็นข้อความนี้)</label>
                    <asp:TextBox ID="txtTplDesc" runat="server" TextMode="MultiLine" Rows="2"
                        placeholder="เช่น ใช้ได้กับเมนูเครื่องดื่มในคาเฟ่เท่านั้น ไม่รวมสินค้าขายหน้าร้าน" />
                </div>
                <div class="mv-inline">
                    <div class="mv-field"><label>โค้ดขึ้นต้น</label>
                        <asp:TextBox ID="txtTplPrefix" runat="server" Text="VC" style="width:90px;" /></div>
                    <div class="mv-field"><label>อายุคูปอง (วัน)</label>
                        <asp:TextBox ID="txtTplDays" runat="server" Text="90" TextMode="Number" style="width:90px;" /></div>
                    <div class="mv-field"><label>เวลาโค้ดหลังกดใช้ (นาที)</label>
                        <asp:TextBox ID="txtTplWindow" runat="server" Text="60" TextMode="Number" style="width:90px;" /></div>
                    <div class="mv-field"><label>ใช้งาน</label>
                        <asp:CheckBox ID="chkTplActive" runat="server" Checked="true" /></div>
                    <asp:Button ID="btnSaveTpl" runat="server" Text="💾 บันทึกแบบคูปอง" CssClass="btn btn-primary"
                        OnClick="btnSaveTpl_Click" />
                </div>
            </div>
        </div>

        <%-- ④ แจกคูปอง --%>
        <div class="mv-card">
            <h3><i class="fas fa-paper-plane"></i> แจกคูปองให้สมาชิก</h3>
            <div class="mv-inline">
                <div class="mv-field"><label>แบบคูปอง</label>
                    <asp:DropDownList ID="ddlIssueTpl" runat="server" style="min-width:260px;" /></div>
                <div class="mv-field"><label>แจกให้ (เบอร์สมาชิก)</label>
                    <asp:TextBox ID="txtIssuePhone" runat="server" placeholder="08xxxxxxxx" style="width:160px;" /></div>
                <asp:Button ID="btnIssueOne" runat="server" Text="แจกรายคน" CssClass="btn btn-success"
                    OnClick="btnIssueOne_Click" />
                <div class="mv-field"><label>หรือแจกทั้งระดับ</label>
                    <asp:DropDownList ID="ddlIssueTier" runat="server" style="min-width:170px;" /></div>
                <asp:Button ID="btnIssueTier" runat="server" Text="แจกทั้งระดับ" CssClass="btn btn-warning"
                    OnClick="btnIssueTier_Click"
                    OnClientClick="return confirm('แจกคูปองให้สมาชิกทุกคนในระดับนี้ (ที่ยังไม่มีคูปองนี้ค้างอยู่)?');" />
            </div>
        </div>

        <%-- ⑤ ประวัติ / tracking --%>
        <div class="mv-card">
            <h3><i class="fas fa-clock-rotate-left"></i> คูปองล่าสุด (Tracking)</h3>
            <table class="mv-t">
                <tr><th>โค้ด</th><th>คูปอง</th><th>สมาชิก</th><th>สถานะ</th><th>หมดอายุ</th><th>ใช้เมื่อ / โดย</th></tr>
                <asp:Literal ID="litHistory" runat="server" />
            </table>
        </div>
    </div>
</asp:Content>
