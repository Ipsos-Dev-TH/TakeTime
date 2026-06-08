<%@ Page Title="เอกสารฝั่งจ่ายจาก NextAcc" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="NextAccPayments.aspx.cs" Inherits="Take_Time_BangPhra.Account.NextAccPayments" %>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">

    <style>
        .nap-wrap { max-width: 1200px; margin: 0 auto; padding: 16px; }
        .nap-toolbar { display: flex; flex-wrap: wrap; gap: 12px; align-items: flex-end; margin-bottom: 16px; }
        .nap-toolbar label { display: block; font-size: 13px; color: #555; margin-bottom: 4px; }
        .nap-toolbar input[type=date] { padding: 6px 8px; border: 1px solid #ccc; border-radius: 6px; }
        .nap-btn { background: #2563eb; color: #fff; border: none; padding: 8px 18px; border-radius: 6px; cursor: pointer; font-size: 14px; }
        .nap-btn:hover { background: #1d4ed8; }
        .nap-status { margin: 8px 0; color: #1d4ed8; font-size: 14px; }
        .nap-card { border: 1px solid #e5e7eb; border-radius: 10px; padding: 14px 16px; margin-bottom: 14px; background: #fff; box-shadow: 0 1px 3px rgba(0,0,0,.05); }
        .nap-card-head { display: flex; justify-content: space-between; flex-wrap: wrap; gap: 8px; border-bottom: 1px solid #f0f0f0; padding-bottom: 8px; margin-bottom: 8px; }
        .nap-docno { font-weight: 600; font-size: 16px; color: #111827; }
        .nap-type { display: inline-block; background: #eef2ff; color: #4338ca; font-size: 12px; padding: 2px 8px; border-radius: 12px; margin-left: 8px; }
        .nap-meta { font-size: 13px; color: #6b7280; }
        .nap-amt { font-size: 16px; font-weight: 600; color: #047857; }
        .nap-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(220px,1fr)); gap: 6px 24px; font-size: 13px; color: #374151; margin-bottom: 8px; }
        .nap-att { display: flex; flex-wrap: wrap; gap: 10px; margin-top: 6px; }
        .nap-att a { display: inline-flex; align-items: center; gap: 6px; border: 1px solid #d1d5db; border-radius: 8px; padding: 6px 10px; text-decoration: none; color: #374151; font-size: 13px; background: #f9fafb; }
        .nap-att a:hover { border-color: #2563eb; color: #2563eb; }
        .nap-att img { width: 56px; height: 56px; object-fit: cover; border-radius: 6px; border: 1px solid #e5e7eb; }
        .nap-badge { font-size: 12px; padding: 2px 8px; border-radius: 12px; }
        .nap-paid { background: #dcfce7; color: #166534; }
        .nap-open { background: #fef9c3; color: #854d0e; }
        .nap-empty { padding: 24px; text-align: center; color: #9ca3af; }
        .nap-noatt { font-size: 12px; color: #9ca3af; }
    </style>

    <div class="nap-wrap">
        <h2>📄 เอกสารฝั่งจ่ายจาก NextAcc</h2>

        <div class="nap-toolbar">
            <div>
                <label>ตั้งแต่วันที่</label>
                <asp:TextBox ID="txtFrom" runat="server" TextMode="Date" />
            </div>
            <div>
                <label>ถึงวันที่</label>
                <asp:TextBox ID="txtTo" runat="server" TextMode="Date" />
            </div>
            <div>
                <asp:Button ID="btnFetch" runat="server" CssClass="nap-btn" Text="🔄 ดึงข้อมูลจาก NextAcc" OnClick="btnFetch_Click" />
            </div>
        </div>

        <asp:Label ID="lblStatus" runat="server" CssClass="nap-status" />

        <asp:Repeater ID="rptDocs" runat="server">
            <ItemTemplate>
                <div class="nap-card">
                    <div class="nap-card-head">
                        <div>
                            <span class="nap-docno"><%# Server.HtmlEncode(Eval("DocumentNumber") as string ?? "-") %></span>
                            <span class="nap-type"><%# Server.HtmlEncode(Eval("DocumentTypeLabel") as string ?? "") %></span>
                        </div>
                        <div class="nap-amt"><%# Convert.ToDecimal(Eval("TotalAmount")).ToString("N2") %> บาท</div>
                    </div>
                    <div class="nap-grid">
                        <div>📅 วันที่: <%# Convert.ToDateTime(Eval("DocumentDate")).ToString("dd/MM/yyyy") %></div>
                        <div>🏢 ผู้ติดต่อ: <%# Server.HtmlEncode(Eval("ContactName") as string ?? "-") %></div>
                        <div>🧾 เลขผู้เสียภาษี: <%# Server.HtmlEncode(Eval("ContactTaxId") as string ?? "-") %></div>
                        <div>📌 อ้างอิง: <%# Server.HtmlEncode(Eval("Reference") as string ?? "-") %></div>
                        <div>💰 ภาษี: <%# Convert.ToDecimal(Eval("VatAmount")).ToString("N2") %></div>
                        <div>💵 คงค้าง: <%# Convert.ToDecimal(Eval("BalanceDue")).ToString("N2") %></div>
                        <div>
                            สถานะ:
                            <span class='nap-badge <%# Convert.ToDecimal(Eval("BalanceDue")) <= 0 ? "nap-paid" : "nap-open" %>'>
                                <%# Convert.ToDecimal(Eval("BalanceDue")) <= 0 ? "ชำระแล้ว" : Server.HtmlEncode(Eval("Status") as string ?? "ค้างชำระ") %>
                            </span>
                        </div>
                        <div>
                            <a href='<%# Eval("DocumentUrl") %>' target="_blank" rel="noopener">เปิดใน NextAcc ↗</a>
                        </div>
                    </div>

                    <div>
                        <strong style="font-size:13px;">📎 ไฟล์แนบ:</strong>
                        <asp:Repeater ID="rptAtt" runat="server" DataSource='<%# Eval("Attachments") %>'>
                            <HeaderTemplate><div class="nap-att"></HeaderTemplate>
                            <ItemTemplate>
                                <a href='<%# Eval("Url") %>' target="_blank" rel="noopener" title='<%# Server.HtmlEncode(Eval("FileName") as string ?? "") %>'>
                                    <%# (bool)Eval("IsImage")
                                        ? "<img src='" + Eval("Url") + "' alt='att' />"
                                        : "<span>📄 " + Server.HtmlEncode(Eval("FileName") as string ?? "ไฟล์") + "</span>" %>
                                </a>
                            </ItemTemplate>
                            <FooterTemplate></div></FooterTemplate>
                        </asp:Repeater>
                        <asp:Literal ID="litNoAtt" runat="server"
                            Visible='<%# ((System.Collections.ICollection)Eval("Attachments")).Count == 0 %>'
                            Text="<span class='nap-noatt'> ไม่มีไฟล์แนบ</span>" />
                    </div>
                </div>
            </ItemTemplate>
        </asp:Repeater>

        <asp:PlaceHolder ID="phEmpty" runat="server" Visible="false">
            <div class="nap-empty">— ไม่พบเอกสารฝั่งจ่ายในช่วงวันที่ที่เลือก —</div>
        </asp:PlaceHolder>
    </div>

</asp:Content>
