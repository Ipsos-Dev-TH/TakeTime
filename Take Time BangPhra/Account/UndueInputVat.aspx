<%@ Page Title="ภาษีซื้อยังไม่ถึงกำหนด" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="UndueInputVat.aspx.cs" Inherits="Take_Time_BangPhra.Account.UndueInputVat" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .uv-wrap { max-width: 1100px; margin: 16px auto 40px; padding: 0 12px; }
        .uv-head { background: linear-gradient(135deg, #5d4037 0%, #8d6e63 100%); color: #fff; padding: 16px 20px; border-radius: 10px; margin-bottom: 14px; }
        .uv-head h2 { margin: 0; font-size: 21px; }
        .uv-head p { margin: 6px 0 0; font-size: 13px; opacity: .92; line-height: 1.6; }
        .uv-card { background: #fff; border-radius: 10px; padding: 16px; margin-bottom: 14px; box-shadow: 0 2px 6px rgba(0,0,0,.06); }
        .uv-card h3 { margin: 0 0 8px; font-size: 17px; color: #3e2723; }
        .uv-row { display: flex; gap: 12px; flex-wrap: wrap; margin-bottom: 10px; align-items: flex-end; }
        .uv-field { display: flex; flex-direction: column; gap: 4px; min-width: 180px; flex: 1; }
        .uv-field label { font-weight: 600; font-size: 13px; color: #37474f; margin: 0; }
        .uv-note { font-size: 12px; color: #6b7c85; line-height: 1.6; }
        .uv-alert { padding: 10px 14px; border-radius: 8px; margin-bottom: 12px; font-size: 14px; line-height: 1.7; word-break: break-word; }
        .uv-alert.ok { background: #e8f5e9; color: #1b5e20; } .uv-alert.err { background: #fdecea; color: #a12626; }
        .uv-alert.warn { background: #fff8e1; color: #6d4c00; } .uv-alert.info { background: #e3f2fd; color: #0d47a1; }
        .btn-uv { background: #5d4037; color: #fff; border: 0; padding: 8px 18px; border-radius: 6px; cursor: pointer; font-weight: 600; }
        .btn-uv:hover { background: #4e342e; color: #fff; }
        .btn-uv2 { background: #eceff1; color: #37474f; border: 0; padding: 7px 14px; border-radius: 6px; text-decoration: none; display: inline-block; cursor: pointer; }
        .uv-scroll { overflow-x: auto; }
        .uv-t { width: 100%; border-collapse: collapse; font-size: 13px; min-width: 760px; }
        .uv-t th, .uv-t td { border-bottom: 1px solid #eceff1; padding: 7px 8px; text-align: left; vertical-align: top; }
        .uv-t th { background: #f5f5f5; color: #4e342e; font-weight: 600; }
        .uv-t td.num, .uv-t th.num { text-align: right; white-space: nowrap; }
        .uv-chip { display: inline-block; padding: 1px 8px; border-radius: 10px; font-size: 12px; background: #eceff1; color: #37474f; margin: 1px 2px 1px 0; }
        .uv-chip.exp { background: #fdecea; color: #a12626; } .uv-chip.soon { background: #fff8e1; color: #6d4c00; } .uv-chip.fs { background: #e3f2fd; color: #0d47a1; }
        .uv-sum { font-size: 14px; margin-bottom: 8px; }
        @media (max-width: 640px) { .uv-wrap { padding: 0 6px; } .uv-field { min-width: 140px; } }
    </style>

    <div class="uv-wrap">
        <div class="uv-head">
            <h2>🧾 ภาษีซื้อยังไม่ถึงกำหนด (รอใบกำกับครบ)</h2>
            <p>เอกสารซื้อบน NextAcc ที่ภาษีซื้อพักไว้ที่ 11640 เพราะใบกำกับภาษียังไม่ครบตาม ม.86/4 — เติมเลขที่/วันที่ใบกำกับ สาขา เลขผู้เสียภาษี และที่อยู่ผู้ขาย
               แล้ว NextAcc ย้ายเข้า 11610 (เคลมได้) ให้เอง · ต้องเคลมภายใน 6 เดือนนับจากเดือนของใบกำกับ (ม.82/3) — เลยกำหนดแล้ว NextAcc ลงเป็นต้นทุน</p>
        </div>

        <asp:Literal ID="litMsg" runat="server" />

        <asp:Panel ID="pnlMain" runat="server">
            <div class="uv-card">
                <div class="uv-row">
                    <div class="uv-field" style="max-width:240px">
                        <label>เดือนของเอกสาร</label>
                        <asp:DropDownList ID="ddlMonth" runat="server" CssClass="form-control" AutoPostBack="true" OnSelectedIndexChanged="Filter_Changed" />
                    </div>
                    <div class="uv-field" style="max-width:260px">
                        <label>แสดง</label>
                        <asp:DropDownList ID="ddlShow" runat="server" CssClass="form-control" AutoPostBack="true" OnSelectedIndexChanged="Filter_Changed">
                            <asp:ListItem Value="open" Text="ยังเคลมได้ (ไม่รวมที่เลย 6 เดือน)" />
                            <asp:ListItem Value="all" Text="ทั้งหมด" />
                            <asp:ListItem Value="expired" Text="เลย 6 เดือนแล้ว" />
                        </asp:DropDownList>
                    </div>
                    <div><asp:Button ID="btnRefresh" runat="server" Text="🔄 ดึงจาก NextAcc ใหม่" CssClass="btn-uv2" OnClick="Filter_Changed" /></div>
                </div>
                <asp:Literal ID="litSummary" runat="server" />
                <div class="uv-scroll">
                    <asp:Repeater ID="rptList" runat="server" OnItemCommand="rptList_ItemCommand">
                        <HeaderTemplate>
                            <table class="uv-t"><tr><th>เอกสาร</th><th>วันที่</th><th>ผู้ขาย</th><th class="num">ภาษีซื้อพัก</th><th>กำหนดเคลม</th><th>ยังขาด</th><th></th></tr>
                        </HeaderTemplate>
                        <ItemTemplate>
                            <tr>
                                <td><b><%# H(Eval("DocumentNumber")) %></b><br /><a href="<%# ResolveUrl("~/Account/UndueInputVat?pdf=" + Eval("Id")) %>" target="_blank" class="uv-note">📄 PDF</a></td>
                                <td><%# D(Eval("DocumentDate")) %><br /><span class="uv-note"><%# Eval("AgeDays") %> วัน</span></td>
                                <td><%# H(Eval("SupplierName")) %><br /><span class="uv-note"><%# H(Eval("SupplierTaxId")) %></span></td>
                                <td class="num"><%# M(Eval("VatAmount")) %></td>
                                <td><%# Deadline(Container.DataItem) %></td>
                                <td><%# Missing(Container.DataItem) %></td>
                                <td><asp:LinkButton ID="lbFill" runat="server" CommandName="Fill" CommandArgument='<%# Eval("Id") %>' Text="✏️ เติมใบกำกับ"
                                        Visible='<%# CanAct && !Convert.ToBoolean(Eval("IsForeignService")) && !Convert.ToBoolean(Eval("IsExpired")) %>' /></td>
                            </tr>
                        </ItemTemplate>
                        <FooterTemplate></table></FooterTemplate>
                    </asp:Repeater>
                </div>
            </div>

            <asp:Panel ID="pnlForm" runat="server" CssClass="uv-card" Visible="false">
                <asp:HiddenField ID="hfDocId" runat="server" />
                <h3>✏️ เติมใบกำกับภาษีซื้อ — <asp:Literal ID="litFormDoc" runat="server" /></h3>
                <asp:Literal ID="litFormInfo" runat="server" />
                <div class="uv-row">
                    <div class="uv-field">
                        <label>เลขที่ใบกำกับภาษีของผู้ขาย</label>
                        <asp:TextBox ID="txtInvNo" runat="server" CssClass="form-control" MaxLength="60" />
                    </div>
                    <div class="uv-field" style="max-width:200px">
                        <label>วันที่ในใบกำกับ</label>
                        <asp:TextBox ID="txtInvDate" runat="server" CssClass="form-control" TextMode="Date" />
                    </div>
                    <div class="uv-field" style="max-width:160px">
                        <label>สาขาผู้ขาย (5 หลัก)</label>
                        <asp:TextBox ID="txtBranch" runat="server" CssClass="form-control" MaxLength="5" placeholder="00000" />
                    </div>
                </div>
                <div class="uv-row">
                    <div class="uv-field" style="max-width:260px">
                        <label>เลขผู้เสียภาษีผู้ขาย 13 หลัก</label>
                        <asp:TextBox ID="txtTaxId" runat="server" CssClass="form-control" MaxLength="17" />
                    </div>
                    <div class="uv-field">
                        <label>ที่อยู่ผู้ขาย (ตามใบกำกับ)</label>
                        <asp:TextBox ID="txtAddress" runat="server" CssClass="form-control" MaxLength="500" />
                    </div>
                </div>
                <div class="uv-note" style="margin-bottom:10px">เลขผู้เสียภาษี/ที่อยู่/สาขา บันทึกที่ผู้ติดต่อของผู้ขายบน NextAcc (ใช้กับทุกใบของผู้ขายรายนี้) · ช่องที่ไม่เปลี่ยนจะไม่ถูกส่ง</div>
                <asp:Button ID="btnSave" runat="server" Text="บันทึกและย้ายภาษีซื้อเข้าเคลม" CssClass="btn-uv" OnClick="btnSave_Click"
                    OnClientClick="if (this.getAttribute('data-busy')) return false; this.setAttribute('data-busy','1'); this.value='กำลังส่ง…'; return true;" />
                <asp:Button ID="btnCancel" runat="server" Text="ปิด" CssClass="btn-uv2" OnClick="btnCancel_Click" CausesValidation="false" />
            </asp:Panel>
        </asp:Panel>
    </div>
</asp:Content>
