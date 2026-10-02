<%@ Page Title="บัตรสมาชิกของฉัน" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Card.aspx.cs" Inherits="Take_Time_BangPhra.Member.MemberCard" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .mc-wrap { max-width: 520px; margin: 22px auto 80px; padding: 0 14px; }

        /* ── บัตรสมาชิก ── */
        .mc-card { position: relative; border-radius: 18px; overflow: hidden; color: #fff;
                   box-shadow: 0 10px 34px rgba(0,0,0,.25); aspect-ratio: 1.586; margin-bottom: 8px; }
        .mc-card .bgimg { position: absolute; inset: 0; width: 100%; height: 100%; object-fit: cover; }
        .mc-card .shade { position: absolute; inset: 0; background: linear-gradient(160deg, rgba(0,0,0,.15), rgba(0,0,0,.55)); }
        .mc-card .inner { position: relative; z-index: 2; height: 100%; display: flex; flex-direction: column;
                          justify-content: space-between; padding: 18px 20px; }
        .mc-brand { display: flex; justify-content: space-between; align-items: flex-start; }
        .mc-brand .rn { font-weight: 700; font-size: 14.5px; letter-spacing: .4px; text-shadow: 0 1px 4px rgba(0,0,0,.5); }
        .mc-tier { font-size: 12px; font-weight: 800; letter-spacing: 1.5px; text-transform: uppercase;
                   padding: 4px 12px; border-radius: 20px; background: rgba(255,255,255,.22);
                   backdrop-filter: blur(2px); text-shadow: 0 1px 3px rgba(0,0,0,.4); }
        .mc-name { font-size: 19px; font-weight: 700; text-shadow: 0 1px 5px rgba(0,0,0,.55); }
        .mc-meta { display: flex; justify-content: space-between; align-items: flex-end; font-size: 12px; opacity: .95; }
        .mc-meta b { display: block; font-size: 13.5px; }
        .mc-expired { position: absolute; z-index: 3; inset: 0; background: rgba(120,20,20,.55);
                      display: flex; align-items: center; justify-content: center; font-size: 22px; font-weight: 800;
                      letter-spacing: 2px; transform: rotate(-8deg); text-shadow: 0 2px 6px rgba(0,0,0,.6); }

        .mc-sub { text-align: center; font-size: 12.5px; color: #90a4ae; margin-bottom: 18px; }

        .mc-sec { background: #fff; border-radius: 14px; box-shadow: 0 2px 12px rgba(0,0,0,.08);
                  padding: 16px 18px; margin-bottom: 14px; }
        .mc-sec h3 { margin: 0 0 12px; font-size: 1em; font-weight: 700; color: #37474f;
                     display: flex; align-items: center; gap: 8px; }

        table.mc-disc { width: 100%; border-collapse: collapse; font-size: 13.5px; }
        table.mc-disc td { padding: 7px 4px; border-bottom: 1px solid #f2f4f6; }
        table.mc-disc td.v { text-align: right; font-weight: 700; color: #2e7d32; }

        .mc-benefit { display: flex; gap: 9px; padding: 6px 0; font-size: 13.5px; color: #455a64; }
        .mc-benefit i { color: #8D6E63; margin-top: 3px; }

        /* ── voucher ── */
        .vc { border: 1.5px dashed #c9b8ae; border-radius: 13px; padding: 13px 15px; margin-bottom: 11px;
              background: #fffdf9; position: relative; }
        .vc.used { opacity: .55; background: #f5f5f5; border-style: solid; }
        .vc .t { font-weight: 700; color: #4e342e; font-size: 14.5px; }
        .vc .d { font-size: 12.5px; color: #7a6a60; margin: 3px 0 6px; line-height: 1.5; }
        .vc .exp { font-size: 11.5px; color: #a1887f; }
        .vc .code { font-size: 24px; font-weight: 800; letter-spacing: 2px; color: #1b5e20;
                    background: #e8f5e9; border-radius: 9px; padding: 8px 10px; text-align: center; margin: 8px 0 4px; }
        .vc .win { text-align: center; font-size: 12px; color: #c62828; }
        .vc .badge { position: absolute; top: 12px; right: 13px; font-size: 10.5px; font-weight: 700;
                     padding: 3px 9px; border-radius: 12px; }
        .b-issued { background: #e3f2fd; color: #1565c0; }
        .b-active { background: #fff3e0; color: #e65100; }
        .b-used { background: #eceff1; color: #78909c; }

        .mc-out { text-align: center; margin-top: 4px; }
        .mc-out a { color: #90a4ae; font-size: 13px; }
    </style>

    <div class="mc-wrap">
        <%-- บัตร --%>
        <div class="mc-card" id="divCard" runat="server">
            <asp:Literal ID="litCardBg" runat="server" />
            <div class="shade"></div>
            <asp:Panel ID="pnlExpired" runat="server" Visible="false" CssClass="mc-expired">หมดอายุ</asp:Panel>
            <div class="inner">
                <div class="mc-brand">
                    <span class="rn">🌲 Take Time Nature Resort</span>
                    <span class="mc-tier"><asp:Literal ID="litTier" runat="server" /></span>
                </div>
                <div>
                    <div class="mc-name"><asp:Literal ID="litName" runat="server" /></div>
                    <div style="font-size:12.5px; opacity:.9;"><asp:Literal ID="litPhone" runat="server" /></div>
                </div>
                <div class="mc-meta">
                    <span>สมาชิกตั้งแต่<b><asp:Literal ID="litSince" runat="server" /></b></span>
                    <span>แต้มสะสม<b><asp:Literal ID="litPoints" runat="server" /> แต้ม</b></span>
                    <span style="text-align:right;">บัตรหมดอายุ<b><asp:Literal ID="litExpiry" runat="server" /></b></span>
                </div>
            </div>
        </div>
        <div class="mc-sub">แสดงหน้านี้ให้พนักงานเพื่อยืนยันสถานะสมาชิกได้เลยค่ะ</div>

        <%-- ส่วนลดค่าห้อง --%>
        <asp:Panel ID="pnlDisc" runat="server" CssClass="mc-sec">
            <h3><i class="fas fa-percent"></i> ส่วนลดค่าห้องของระดับคุณ</h3>
            <table class="mc-disc">
                <tr><td>เข้าพักวันธรรมดา (จ-ศ)</td><td class="v"><asp:Literal ID="litWeekday" runat="server" /></td></tr>
                <tr><td>เข้าพักวันหยุด / นักขัตฤกษ์</td><td class="v"><asp:Literal ID="litWeekend" runat="server" /></td></tr>
            </table>
            <div style="font-size:11.5px; color:#90a4ae; margin-top:8px;">
                แจ้งสิทธิ์กับพนักงานตอนจอง/เช็คอิน — ส่วนลดตามเงื่อนไขของที่พัก
            </div>
        </asp:Panel>

        <%-- สิทธิ์อื่นของ tier --%>
        <asp:Panel ID="pnlBenefits" runat="server" CssClass="mc-sec" Visible="false">
            <h3><i class="fas fa-gift"></i> สิทธิพิเศษของคุณ</h3>
            <asp:Literal ID="litBenefits" runat="server" />
        </asp:Panel>

        <%-- voucher --%>
        <div class="mc-sec">
            <h3><i class="fas fa-ticket-alt"></i> คูปองของฉัน</h3>
            <asp:Repeater ID="rptVouchers" runat="server" OnItemCommand="rptVouchers_ItemCommand">
                <ItemTemplate>
                    <div class='vc <%# Eval("CssClass") %>'>
                        <span class='badge <%# Eval("BadgeCss") %>'><%# Eval("BadgeText") %></span>
                        <div class="t"><%# Eval("Name") %></div>
                        <div class="d"><%# Eval("Description") %></div>
                        <div class="exp">ใช้ได้ถึง <%# Eval("ExpiryText") %></div>

                        <asp:Panel runat="server" Visible='<%# (bool)Eval("ShowCode") %>'>
                            <div class="code"><%# Eval("Code") %></div>
                            <div class="win">⏳ แสดงโค้ดนี้ให้พนักงานภายใน <%# Eval("WindowText") %></div>
                        </asp:Panel>

                        <asp:Button runat="server" Text="🎟 กดใช้คูปอง" CssClass="btn btn-success btn-sm"
                            style="margin-top:8px; width:100%;"
                            CommandName="activate" CommandArgument='<%# Eval("ID") %>'
                            Visible='<%# (bool)Eval("ShowUseButton") %>'
                            OnClientClick="return confirm('กดใช้แล้วโค้ดจะมีเวลาจำกัด — กดตอนอยู่หน้าเคาน์เตอร์/แจ้งพนักงานนะคะ ยืนยันใช้คูปองนี้?');" />
                    </div>
                </ItemTemplate>
            </asp:Repeater>
            <asp:Panel ID="pnlNoVoucher" runat="server" Visible="false"
                style="text-align:center; color:#b0bec5; font-size:13.5px; padding:14px 0;">
                ยังไม่มีคูปองในตอนนี้ — ติดตามสิทธิพิเศษได้เร็ว ๆ นี้ค่ะ
            </asp:Panel>
        </div>

        <div class="mc-out">
            <asp:LinkButton ID="btnLogout" runat="server" OnClick="btnLogout_Click">ออกจากระบบสมาชิก</asp:LinkButton>
        </div>
    </div>
</asp:Content>
