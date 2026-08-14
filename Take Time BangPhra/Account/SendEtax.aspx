<%@ Page Title="ส่ง e-Tax ทางอีเมล" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="SendEtax.aspx.cs" Inherits="Take_Time_BangPhra.Account.SendEtax" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .etax-mail { max-width: 760px; margin: 20px auto; background:#fff; border-radius:8px; box-shadow:0 2px 10px rgba(0,0,0,.08); overflow:hidden; }
        .etax-mail .hd { background:linear-gradient(135deg,#667eea,#764ba2); color:#fff; padding:16px 22px; font-size:18px; font-weight:bold; }
        .etax-mail .bd { padding:22px; }
        .etax-row { margin-bottom:14px; }
        .etax-row label { display:block; font-weight:bold; margin-bottom:5px; color:#333; }
        .etax-row input[type=text], .etax-row textarea { width:100%; padding:9px 11px; border:1px solid #ccc; border-radius:5px; box-sizing:border-box; font-family:inherit; font-size:14px; }
        .etax-row textarea { min-height:130px; resize:vertical; }
        .etax-meta { background:#f5f6fa; border-radius:6px; padding:12px 15px; margin-bottom:18px; font-size:13px; color:#555; line-height:1.7; }
        .etax-meta b { color:#333; }
        .etax-actions { display:flex; gap:10px; margin-top:18px; align-items:center; }
        .btn-send { background:#27ae60; color:#fff; border:none; padding:11px 26px; border-radius:6px; font-size:15px; font-weight:bold; cursor:pointer; }
        .btn-cancel { background:#eee; color:#333; border:none; padding:11px 20px; border-radius:6px; font-size:14px; cursor:pointer; text-decoration:none; }
        .btn-preview { color:#2980b9; font-size:13px; text-decoration:none; margin-left:auto; }
        .chk-inline { display:inline-flex; align-items:center; gap:6px; margin-right:20px; font-weight:normal; }
        .msg-ok { background:#eafaf1; border:1px solid #27ae60; color:#1e7e46; padding:12px 15px; border-radius:6px; margin-bottom:16px; }
        .msg-err { background:#fdecea; border:1px solid #e74c3c; color:#c0392b; padding:12px 15px; border-radius:6px; margin-bottom:16px; white-space:pre-wrap; }
    </style>

    <div class="etax-mail">
        <div class="hd">📧 ส่งใบกำกับภาษีอิเล็กทรอนิกส์ (e-Tax) ทางอีเมล</div>
        <div class="bd">
            <asp:Literal ID="litMsg" runat="server" />

            <asp:Panel ID="pnlList" runat="server" Visible="false">
                <div style="margin-bottom:12px; display:flex; gap:8px; flex-wrap:wrap; align-items:center;">
                    <asp:TextBox ID="txtSearchEtax" runat="server" placeholder="ค้นหา เลขใบเสร็จ / เลขจอง / ชื่อลูกค้า"
                        style="padding:9px 12px; border:1.5px solid #dbe2e7; border-radius:8px; min-width:260px;" />
                    <asp:Button ID="btnSearchEtax" runat="server" Text="🔍 ค้นหา" CssClass="btn btn-default"
                        OnClick="btnSearchEtax_Click" />
                    <span style="font-size:12.5px; color:#90a4ae;">แสดงเอกสาร e-Tax ที่ออกแล้ว — กด "ส่งอีเมล" เพื่อไปหน้าส่ง</span>
                </div>
                <div style="overflow-x:auto;">
                <table style="width:100%; border-collapse:collapse; font-size:13.5px;">
                    <tr style="background:#f5f7f9;">
                        <th style="text-align:left; padding:9px 10px; border-bottom:2px solid #e3e9ed;">เลขที่ใบเสร็จ</th>
                        <th style="text-align:left; padding:9px 10px; border-bottom:2px solid #e3e9ed;">การจอง</th>
                        <th style="text-align:left; padding:9px 10px; border-bottom:2px solid #e3e9ed;">ลูกค้า</th>
                        <th style="text-align:right; padding:9px 10px; border-bottom:2px solid #e3e9ed;">ยอด</th>
                        <th style="text-align:left; padding:9px 10px; border-bottom:2px solid #e3e9ed;">วันที่ออก</th>
                        <th style="text-align:center; padding:9px 10px; border-bottom:2px solid #e3e9ed;">สถานะอีเมล</th>
                        <th style="text-align:center; padding:9px 10px; border-bottom:2px solid #e3e9ed;">กรมสรรพากร</th>
                        <th style="border-bottom:2px solid #e3e9ed;"></th>
                    </tr>
                    <asp:Literal ID="litEtaxRows" runat="server" />
                </table>
                </div>
            </asp:Panel>

            <asp:Panel ID="pnlForm" runat="server">
                <div class="etax-meta">
                    <div>เลขที่ใบเสร็จ: <b><asp:Literal ID="litReceipt" runat="server" /></b>
                        &nbsp;•&nbsp; ลูกค้า: <b><asp:Literal ID="litGuest" runat="server" /></b>
                        &nbsp;•&nbsp; ยอด: <b><asp:Literal ID="litAmount" runat="server" /></b></div>
                </div>

                <div class="etax-row">
                    <label>ถึง (อีเมลผู้รับ) *</label>
                    <asp:TextBox ID="txtTo" runat="server" />
                </div>
                <div class="etax-row">
                    <label>สำเนา (CC) — คั่นหลายอีเมลด้วย , หรือ ;</label>
                    <asp:TextBox ID="txtCc" runat="server" />
                </div>
                <div class="etax-row">
                    <label>หัวข้อ</label>
                    <asp:TextBox ID="txtSubject" runat="server" />
                </div>
                <div class="etax-row">
                    <label>เนื้อหา</label>
                    <asp:TextBox ID="txtBody" runat="server" TextMode="MultiLine" />
                </div>
                <div class="etax-row">
                    <label class="chk-inline"><asp:CheckBox ID="chkPdf" runat="server" /> แนบไฟล์ PDF</label>
                    <label class="chk-inline"><asp:CheckBox ID="chkXml" runat="server" /> แนบไฟล์ XML</label>
                </div>

                <div class="etax-actions">
                    <asp:Button ID="btnSend" runat="server" CssClass="btn-send" Text="✉️ ส่งอีเมล"
                        OnClick="btnSend_Click"
                        OnClientClick="if(!confirm('ยืนยันส่ง e-Tax ทางอีเมล?'))return false; this.disabled=true; this.value='⏳ กำลังส่ง...';"
                        UseSubmitBehavior="false" />
                    <a href="/Account/CheckDocument_New" class="btn-cancel">ยกเลิก / กลับ</a>
                    <asp:HyperLink ID="lnkPreview" runat="server" CssClass="btn-preview" Target="_blank" Visible="false">👁 ดูใบก่อนส่ง</asp:HyperLink>
                </div>
            </asp:Panel>
        </div>
    </div>
</asp:Content>
