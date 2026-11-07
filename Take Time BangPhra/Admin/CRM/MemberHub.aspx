<%@ Page Title="จัดการสมาชิก" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="MemberHub.aspx.cs" Inherits="Take_Time_BangPhra.Admin.CRM.MemberHub" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .mh-wrap { max-width: 1060px; margin: 0 auto; padding: 18px 12px 60px; }
        .mh-head { background: linear-gradient(135deg, #1565c0, #42a5f5); color: #fff; border-radius: 14px;
                   padding: 22px 26px; margin-bottom: 18px; }
        .mh-head h2 { margin: 0 0 6px; font-weight: 700; font-size: 1.5em; }
        .mh-head p { margin: 0; opacity: .93; font-size: 14px; }
        .mh-card { background: #fff; border-radius: 11px; box-shadow: 0 2px 10px rgba(0,0,0,.08);
                   padding: 18px 20px; margin-bottom: 16px; }
        .mh-card h3 { margin: 0 0 12px; font-size: 1.05em; color: #37474f; font-weight: 700; }
        .mh-msg { padding: 11px 15px; border-radius: 8px; margin-bottom: 14px; font-size: 14px; }
        .mh-ok { background: #e8f5e9; color: #1e7e42; } .mh-err { background: #ffebee; color: #c62828; }
        .mh-search input { padding: 12px 14px; border: 1.5px solid #dbe2e7; border-radius: 10px;
                           font-size: 15px; min-width: 280px; }
        .mh-result { display: block; padding: 10px 14px; border: 1px solid #e8edf1; border-radius: 9px;
                     margin-top: 8px; text-decoration: none; color: inherit; }
        .mh-result:hover { background: #f5f9ff; text-decoration: none; color: inherit; }
        .mh-profile { display: flex; gap: 18px; flex-wrap: wrap; align-items: center; }
        .mh-avatar { width: 62px; height: 62px; border-radius: 50%; display: flex; align-items: center;
                     justify-content: center; color: #fff; font-size: 24px; font-weight: 800; }
        .mh-kv { font-size: 13px; color: #78909c; }
        .mh-kv b { display: block; font-size: 15px; color: #263238; }
        .tierpill { padding: 4px 14px; border-radius: 20px; color: #fff; font-weight: 700; font-size: 13px; }
        .mh-field { margin-bottom: 10px; }
        .mh-field label { display: block; font-weight: 600; font-size: 13px; margin-bottom: 4px; color: #37474f; }
        .mh-field input, .mh-field select { padding: 8px 11px; border: 1.5px solid #dbe2e7; border-radius: 8px; }
        .mh-inline { display: flex; gap: 12px; flex-wrap: wrap; align-items: flex-end; }
        table.mh-t { width: 100%; border-collapse: collapse; font-size: 13.5px; }
        table.mh-t th { background: #f5f7f9; text-align: left; padding: 8px 10px; font-weight: 650;
                        color: #37474f; border-bottom: 2px solid #e3e9ed; }
        table.mh-t td { padding: 7px 10px; border-bottom: 1px solid #f0f3f5; vertical-align: middle; }
        .st { font-size: 11px; padding: 3px 9px; border-radius: 11px; font-weight: 700; white-space: nowrap; }
        .st-on { background: #e8f5e9; color: #1e7e42; } .st-off { background: #ffebee; color: #c62828; }
        .st-hold { background: #e3f2fd; color: #1565c0; }
    </style>

    <div class="mh-wrap">
        <div class="mh-head">
            <h2><i class="fas fa-users-gear"></i> จัดการสมาชิก</h2>
            <p>สมัคร / ต่ออายุ (เก็บค่าสมัคร + ลงบัญชีอัตโนมัติ) · ดูการใช้งาน · ตัดสิทธิ์คูปองรายคน · รีเซ็ต PIN</p>
        </div>

        <asp:Panel ID="pnlMsg" runat="server" Visible="false">
            <div id="divMsg" runat="server" class="mh-msg mh-ok"><asp:Literal ID="litMsg" runat="server" /></div>
        </asp:Panel>

        <%-- ① ค้นหา --%>
        <div class="mh-card mh-search">
            <h3><i class="fas fa-magnifying-glass"></i> ค้นหาสมาชิก</h3>
            <asp:TextBox ID="txtSearch" runat="server" placeholder="เบอร์โทร หรือ ชื่อลูกค้า…" />
            <asp:Button ID="btnSearch" runat="server" Text="ค้นหา" CssClass="btn btn-primary" OnClick="btnSearch_Click" />
            <span style="font-size:12.5px; color:#90a4ae; margin-left:8px;">
                ยังไม่เป็นสมาชิก? ใส่เบอร์แล้วไปที่ส่วน "สมัคร/ต่ออายุ" ได้เลย — ระบบสร้างให้อัตโนมัติ
            </span>
            <asp:Literal ID="litSearchResults" runat="server" />
        </div>

        <asp:Panel ID="pnlMember" runat="server" Visible="false">
            <%-- ② โปรไฟล์ --%>
            <div class="mh-card">
                <div class="mh-profile">
                    <div class="mh-avatar" id="divAvatar" runat="server"><asp:Literal ID="litInitial" runat="server" /></div>
                    <div><span class="tierpill" id="spanTier" runat="server"><asp:Literal ID="litTier" runat="server" /></span></div>
                    <div class="mh-kv">ชื่อ<b><asp:Literal ID="litName" runat="server" /></b></div>
                    <div class="mh-kv">เบอร์<b><asp:Literal ID="litPhone" runat="server" /></b></div>
                    <div class="mh-kv">สมาชิกตั้งแต่<b><asp:Literal ID="litSince" runat="server" /></b></div>
                    <div class="mh-kv">หมดอายุ<b><asp:Literal ID="litExpiry" runat="server" /></b></div>
                    <div class="mh-kv">แต้มคงเหลือ<b><asp:Literal ID="litPoints" runat="server" /></b></div>
                    <div class="mh-kv">PIN<b><asp:Literal ID="litPinState" runat="server" /></b></div>
                </div>
                <div style="margin-top:14px; display:flex; gap:10px; flex-wrap:wrap; align-items:flex-end;">
                    <asp:Button ID="btnResetPin" runat="server" Text="🔑 รีเซ็ต PIN" CssClass="btn btn-default"
                        OnClick="btnResetPin_Click"
                        OnClientClick="return confirm('รีเซ็ต PIN? สมาชิกจะกลับไปใช้เลขท้ายเบอร์ 4 ตัวชั่วคราวแล้วถูกบังคับตั้งใหม่');" />
                    <div class="mh-field" style="margin:0;">
                        <label>แก้วันหมดอายุเอง (เว้นว่าง = ตลอดชีพ)</label>
                        <asp:TextBox ID="txtExpiry" runat="server" TextMode="Date" />
                        <asp:Button ID="btnSetExpiry" runat="server" Text="บันทึก" CssClass="btn btn-default"
                            OnClick="btnSetExpiry_Click" />
                    </div>
                </div>
            </div>

            <%-- ③ สมัคร / ต่ออายุ --%>
            <div class="mh-card" style="border-left: 5px solid #1565c0;">
                <h3><i class="fas fa-id-card"></i> สมัคร / ต่ออายุ / อัปเกรด</h3>
                <div class="mh-inline">
                    <div class="mh-field">
                        <label>ระดับสมาชิก</label>
                        <asp:DropDownList ID="ddlEnrollTier" runat="server" AutoPostBack="true"
                            OnSelectedIndexChanged="ddlEnrollTier_Changed" style="min-width:220px;" />
                    </div>
                    <div class="mh-field">
                        <label>ค่าสมัคร (แก้ได้ เช่น มีโปรลด)</label>
                        <asp:TextBox ID="txtEnrollFee" runat="server" TextMode="Number" step="0.01" style="width:130px;" />
                    </div>
                    <div class="mh-field">
                        <label>ชำระโดย</label>
                        <asp:DropDownList ID="ddlPaidHow" runat="server" style="min-width:150px;" />
                    </div>
                    <asp:Button ID="btnEnroll" runat="server" Text="✅ ยืนยันสมัคร/ต่ออายุ + เก็บเงิน"
                        CssClass="btn btn-success btn-lg" OnClick="btnEnroll_Click"
                        OnClientClick="return confirm('ยืนยันการสมัคร/ต่ออายุ และบันทึกรายได้ค่าสมัคร?');" />
                </div>
                <div style="font-size:12.5px; color:#90a4ae;" id="divEnrollHint" runat="server"></div>
            </div>

            <%-- ④ สิทธิ์คูปองรายคน --%>
            <div class="mh-card">
                <h3><i class="fas fa-ticket-alt"></i> สิทธิ์คูปองของสมาชิกคนนี้</h3>
                <table class="mh-t">
                    <tr><th>คูปอง</th><th style="width:110px;">ใบที่ถืออยู่</th>
                        <th style="width:140px; text-align:center;">🚫 ไม่ให้สิทธิ์คนนี้</th>
                        <th style="width:110px;"></th></tr>
                    <asp:Literal ID="litVoucherMatrix" runat="server" />
                </table>
                <div style="font-size:12px; color:#90a4ae; margin-top:8px;">
                    ติ๊ก "ไม่ให้สิทธิ์" = แจกทั้งระดับจะข้ามคนนี้ + ใบที่ยังไม่ใช้ของคูปองนั้นถูกยกเลิกทันที
                    (เช่น ให้ระดับนี้แต่ไม่ให้คูปอง "พักฟรี")
                </div>
                <asp:Button ID="btnSaveMatrix" runat="server" Text="💾 บันทึกสิทธิ์คูปอง" CssClass="btn btn-primary"
                    style="margin-top:10px;" OnClick="btnSaveMatrix_Click" />
            </div>

            <%-- ⑤ การใช้งาน --%>
            <div class="mh-card">
                <h3><i class="fas fa-clock-rotate-left"></i> การใช้งานของสมาชิก</h3>
                <b style="font-size:13.5px; color:#546e7a;">คูปอง</b>
                <table class="mh-t" style="margin:6px 0 16px;">
                    <tr><th>โค้ด</th><th>คูปอง</th><th>สถานะ</th><th>หมดอายุ</th><th>ใช้เมื่อ</th><th></th></tr>
                    <asp:Literal ID="litUsage" runat="server" />
                </table>
                <b style="font-size:13.5px; color:#546e7a;">การชำระค่าสมาชิก</b>
                <table class="mh-t" style="margin-top:6px;">
                    <tr><th>วันที่</th><th>รายการ</th><th>ยอด</th><th>ชำระโดย</th><th>ใบรับเงิน</th><th>โดย</th></tr>
                    <asp:Literal ID="litPayments" runat="server" />
                </table>
            </div>
        </asp:Panel>

        <%-- ⑥ ตั้งค่าระดับ (Owner) --%>
        <asp:Panel ID="pnlTierConfig" runat="server" CssClass="mh-card" Visible="false">
            <h3><i class="fas fa-sliders"></i> ตั้งค่าค่าสมัคร & อายุสมาชิก (ต่อระดับ)</h3>
            <table class="mh-t">
                <tr><th>ระดับ</th><th>ค่าสมัคร (บาท)</th><th>อายุ (เดือน · 0 = ตลอดชีพ)</th></tr>
                <asp:Literal ID="litTierFees" runat="server" />
            </table>
            <asp:Button ID="btnSaveTierFees" runat="server" Text="💾 บันทึกตั้งค่าระดับ" CssClass="btn btn-primary"
                style="margin-top:10px;" OnClick="btnSaveTierFees_Click" />
        </asp:Panel>
    </div>
</asp:Content>
