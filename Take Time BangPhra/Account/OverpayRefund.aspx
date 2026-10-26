<%@ Page Title="คืนเงินส่วนที่ลูกค้าจ่ายเกิน" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="OverpayRefund.aspx.cs" Inherits="Take_Time_BangPhra.Account.OverpayRefund" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .op-wrap { max-width: 860px; margin: 16px auto 40px; padding: 0 12px; }
        .op-head { background: linear-gradient(135deg, #6d4c41 0%, #8d6e63 100%); color: #fff; padding: 16px 20px; border-radius: 10px; margin-bottom: 14px; }
        .op-head h2 { margin: 0; font-size: 21px; }
        .op-head p { margin: 6px 0 0; font-size: 13px; opacity: .92; line-height: 1.6; }
        .op-card { background: #fff; border-radius: 10px; padding: 16px; margin-bottom: 14px; box-shadow: 0 2px 6px rgba(0,0,0,.06); }
        .op-row { display: flex; gap: 12px; flex-wrap: wrap; margin-bottom: 10px; align-items: flex-end; }
        .op-field { display: flex; flex-direction: column; gap: 4px; min-width: 200px; flex: 1; }
        .op-field label { font-weight: 600; font-size: 13px; color: #37474f; margin: 0; }
        .op-note { font-size: 12px; color: #6b7c85; line-height: 1.6; }
        .op-alert { padding: 10px 14px; border-radius: 8px; margin-bottom: 12px; font-size: 14px; line-height: 1.7; }
        .op-alert.ok { background: #e8f5e9; color: #1b5e20; } .op-alert.err { background: #fdecea; color: #a12626; }
        .op-alert.warn { background: #fff8e1; color: #6d4c00; } .op-alert.info { background: #e3f2fd; color: #0d47a1; }
        .btn-op { background: #6d4c41; color: #fff; border: 0; padding: 8px 18px; border-radius: 6px; cursor: pointer; font-weight: 600; }
        .btn-op:hover { background: #5d4037; color: #fff; }
        .btn-op2 { background: #eceff1; color: #37474f; border: 0; padding: 7px 14px; border-radius: 6px; text-decoration: none; display: inline-block; }
        @media (max-width: 640px) { .op-wrap { padding: 0 6px; } .op-field { min-width: 140px; } }
    </style>

    <div class="op-wrap">
        <div class="op-head">
            <h2>💰 คืนเงินส่วนที่ลูกค้าจ่ายเกิน</h2>
            <p>ส่วนที่ลูกค้าจ่ายเกินยอดเข้าพักเป็นเงินของลูกค้า (หนี้สิน) ไม่ใช่รายได้ — ระบบกันไว้ในเงินรับล่วงหน้าตอนเช็คเอาท์ แล้วคืนด้วย
               ใบลดหนี้ของใบเสร็จมัดจำ (NextAcc refund-deposit: Dr เงินรับล่วงหน้า / Cr แหล่งเงินที่จ่ายคืน)</p>
        </div>

        <asp:Literal ID="litMsg" runat="server" />

        <asp:Panel ID="pnlForm" runat="server" CssClass="op-card">
            <asp:Literal ID="litInfo" runat="server" />
            <div class="op-row">
                <div class="op-field">
                    <label>ใบเสร็จมัดจำต้นทาง</label>
                    <asp:DropDownList ID="ddlSource" runat="server" CssClass="form-control" />
                </div>
                <div class="op-field" style="max-width:200px">
                    <label>ยอดคืน (บาท)</label>
                    <asp:TextBox ID="txtAmount" runat="server" CssClass="form-control" style="text-align:right" />
                </div>
            </div>
            <div class="op-row">
                <div class="op-field">
                    <label>วิธีคืนเงิน</label>
                    <asp:DropDownList ID="ddlMethod" runat="server" CssClass="form-control" />
                </div>
                <div class="op-field">
                    <label>แหล่งเงินที่จ่ายคืน</label>
                    <asp:DropDownList ID="ddlPaidHow" runat="server" CssClass="form-control" />
                </div>
            </div>
            <div class="op-row">
                <div class="op-field">
                    <label>เลขอ้างอิงการโอน/สลิป (ถ้ามี)</label>
                    <asp:TextBox ID="txtRef" runat="server" CssClass="form-control" MaxLength="200" />
                </div>
                <div class="op-field">
                    <label>หมายเหตุ</label>
                    <asp:TextBox ID="txtNotes" runat="server" CssClass="form-control" MaxLength="500" />
                </div>
            </div>
            <div class="op-note" style="margin-bottom:10px">
                คืนผ่านเกตเวย์ = คืนเข้าบัตร/บัญชีเดิมของลูกค้าตอนอนุมัติ (แหล่งเงินใช้บัญชีพักเกตเวย์เดิมอัตโนมัติ) ·
                ต้องอนุมัติ (CreditNote_Require_Approval) = สร้างเป็นร่าง รอ Owner / Admin ที่มีสิทธิ์บัญชีอนุมัติก่อนคืนเงิน/ลงบัญชี
            </div>
            <asp:Button ID="btnCreate" runat="server" Text="สร้างใบลดหนี้ + คืนเงิน" CssClass="btn-op" OnClick="btnCreate_Click"
                OnClientClick="if (this.getAttribute('data-busy')) return false; if (!confirm('ยืนยันคืนเงินส่วนที่จ่ายเกิน?')) return false; this.setAttribute('data-busy','1'); return true;" />
            <a class="btn-op2" href="<%= ResolveUrl("~/ReserveTable") %>">กลับตารางการจอง</a>
        </asp:Panel>
    </div>
</asp:Content>
