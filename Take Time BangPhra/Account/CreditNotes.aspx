<%@ Page Title="ใบลดหนี้ / คืนเงินลูกค้า" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="CreditNotes.aspx.cs" Inherits="Take_Time_BangPhra.Account.CreditNotes" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .cn-wrap { max-width: 1280px; margin: 16px auto 40px; padding: 0 12px; }
        .cn-head { background: linear-gradient(135deg, #6d4c41 0%, #8d6e63 100%); color: #fff; padding: 16px 20px; border-radius: 10px; margin-bottom: 14px; }
        .cn-head h2 { margin: 0; font-size: 22px; }
        .cn-head p { margin: 6px 0 0; font-size: 13px; opacity: .92; line-height: 1.6; }
        .cn-tabs { display: flex; gap: 8px; flex-wrap: wrap; margin-bottom: 12px; }
        .cn-tabs a { padding: 8px 14px; border-radius: 8px; background: #efebe9; color: #4e342e; text-decoration: none; font-weight: 600; font-size: 14px; }
        .cn-tabs a.on { background: #6d4c41; color: #fff; }
        .cn-card { background: #fff; border-radius: 10px; padding: 16px; margin-bottom: 14px; box-shadow: 0 2px 6px rgba(0,0,0,.06); }
        .cn-card h3 { margin: 0 0 10px; font-size: 16px; color: #4e342e; }
        .cn-row { display: flex; gap: 12px; flex-wrap: wrap; margin-bottom: 10px; align-items: flex-end; }
        .cn-field { display: flex; flex-direction: column; gap: 4px; min-width: 180px; flex: 1; }
        .cn-field label { font-weight: 600; font-size: 13px; color: #37474f; margin: 0; }
        .cn-note { font-size: 12px; color: #6b7c85; line-height: 1.6; }
        .cn-alert { padding: 10px 14px; border-radius: 8px; margin-bottom: 12px; font-size: 14px; line-height: 1.7; }
        .cn-alert.ok { background: #e8f5e9; color: #1b5e20; } .cn-alert.err { background: #fdecea; color: #a12626; }
        .cn-alert.warn { background: #fff8e1; color: #6d4c00; } .cn-alert.info { background: #e3f2fd; color: #0d47a1; }
        .cn-legal { background: #fff6e5; border-left: 5px solid #f0a020; border-radius: 8px; padding: 10px 14px; font-size: 13px; line-height: 1.75; margin-bottom: 12px; color: #5f3d00; }
        .cn-scroll { overflow-x: auto; }
        table.cn-t { width: 100%; border-collapse: collapse; font-size: 13px; min-width: 900px; }
        table.cn-t th { background: #efebe9; color: #3e2723; padding: 7px 8px; text-align: left; white-space: nowrap; }
        table.cn-t td { padding: 7px 8px; border-bottom: 1px solid #eceff1; vertical-align: top; }
        table.cn-t td.num, table.cn-t th.num { text-align: right; white-space: nowrap; }
        .cn-chip { display: inline-block; padding: 2px 8px; border-radius: 10px; font-size: 12px; font-weight: 600; white-space: nowrap; }
        .cn-chip.DRAFT { background: #fff3e0; color: #e65100; } .cn-chip.APPROVED { background: #e3f2fd; color: #0d47a1; }
        .cn-chip.SYNCED { background: #e8f5e9; color: #1b5e20; } .cn-chip.VOIDED { background: #eceff1; color: #607d8b; }
        .cn-chip.FAILED, .cn-chip.ERR { background: #fdecea; color: #b71c1c; }
        .cn-act a, .cn-act input { margin: 0 6px 4px 0; font-size: 12.5px; }
        .btn-cn { background: #6d4c41; color: #fff; border: 0; padding: 8px 18px; border-radius: 6px; cursor: pointer; font-weight: 600; }
        .btn-cn:hover { background: #5d4037; color: #fff; }
        .btn-cn2 { background: #eceff1; color: #37474f; border: 0; padding: 7px 14px; border-radius: 6px; cursor: pointer; }
        .cn-amt { width: 120px; text-align: right; }
        .cn-qty { width: 80px; text-align: right; }
        .cn-sum { font-size: 15px; font-weight: 700; color: #3e2723; }
        .cnp { max-width: 820px; margin: 0 auto; background: #fff; padding: 28px; font-size: 14px; color: #222; }
        .cnp-h { display: flex; justify-content: space-between; gap: 20px; border-bottom: 2px solid #444; padding-bottom: 10px; margin-bottom: 12px; }
        .cnp-t { font-size: 20px; font-weight: 700; margin-bottom: 6px; }
        .cnp-tb { width: 100%; border-collapse: collapse; margin: 10px 0; } .cnp-tb th, .cnp-tb td { border: 1px solid #999; padding: 6px; }
        .cnp-sum { margin-left: auto; min-width: 360px; } .cnp-sum td { padding: 3px 8px; } .cnp-sum td:last-child { text-align: right; }
        .cnp-note { font-size: 12px; color: #555; } .cnp-sig { display: flex; justify-content: space-around; margin-top: 40px; text-align: center; }
        @media print { .cn-noprint, header, nav, footer, .navbar { display: none !important; } .cnp { padding: 0; } }
        @media (max-width: 640px) { .cn-wrap { padding: 0 6px; } .cn-field { min-width: 140px; } }
    </style>

    <asp:HiddenField ID="hfActReason" runat="server" ClientIDMode="Static" />

    <div class="cn-wrap">
        <asp:Panel ID="pnlPrint" runat="server" Visible="false">
            <div class="cn-noprint" style="margin-bottom:10px">
                <button type="button" class="btn-cn" onclick="window.print()">🖨 พิมพ์</button>
                <a class="btn-cn2" href="<%= ResolveUrl("~/Account/CreditNotes") %>">← กลับรายการ</a>
            </div>
            <asp:Literal ID="litPrint" runat="server" />
        </asp:Panel>

        <asp:Panel ID="pnlMain" runat="server">
            <div class="cn-head cn-noprint">
                <h2>🧾 ใบลดหนี้ / คืนเงินลูกค้า</h2>
                <p>ยกเลิกการจองแล้วคืนเงิน (เต็ม/บางส่วน หักค่าธรรมเนียม) · ลดราคา/ชดเชย/รับคืนสินค้า/คิดเงินผิด หลังออกใบเสร็จ-ใบกำกับแล้ว ·
                   เลือกแหล่งเงินที่จ่ายคืนได้ · ส่งเข้า NextAcc อัตโนมัติ (ใบลดหนี้อ้างใบเดิม / คืนมัดจำ + กลับภาษีขายตามโหมด VAT)</p>
            </div>

            <asp:Literal ID="litMsg" runat="server" />

            <asp:Panel ID="pnlDisabled" runat="server" Visible="false" CssClass="cn-card">
                <asp:Literal ID="litDisabled" runat="server" />
            </asp:Panel>

            <asp:Panel ID="pnlEnabled" runat="server">
                <div class="cn-tabs cn-noprint">
                    <a href="<%= ResolveUrl("~/Account/CreditNotes") %>" class="<%= TabCss("list") %>">📋 รายการใบลดหนี้</a>
                    <a href="<%= ResolveUrl("~/Account/CreditNotes?new=1") %>" class="<%= TabCss("new") %>">➕ ออกใบลดหนี้ (manual)</a>
                    <% if (Mode == "cancel") { %><a href="#" class="on">↩ ยกเลิกการจอง + คืนเงิน</a><% } %>
                    <% if (Mode == "refund") { %><a href="#" class="on">💸 บันทึกคืนเงินภายหลัง</a><% } %>
                </div>

                <!-- ═══ รายการ ═══ -->
                <asp:Panel ID="pnlList" runat="server">
                    <div class="cn-card cn-noprint">
                        <div class="cn-row">
                            <div class="cn-field"><label>ตั้งแต่</label><asp:TextBox ID="txtFrom" runat="server" TextMode="Date" CssClass="form-control" /></div>
                            <div class="cn-field"><label>ถึง</label><asp:TextBox ID="txtTo" runat="server" TextMode="Date" CssClass="form-control" /></div>
                            <div class="cn-field"><label>สถานะ</label>
                                <asp:DropDownList ID="ddlStatus" runat="server" CssClass="form-control">
                                    <asp:ListItem Value="" Text="ทั้งหมด" />
                                    <asp:ListItem Value="DRAFT" Text="ร่าง (รออนุมัติ)" />
                                    <asp:ListItem Value="APPROVED" Text="อนุมัติแล้ว (รอส่ง NextAcc)" />
                                    <asp:ListItem Value="SYNCED" Text="ส่ง NextAcc แล้ว" />
                                    <asp:ListItem Value="VOIDED" Text="ยกเลิกแล้ว" />
                                </asp:DropDownList>
                            </div>
                            <div class="cn-field" style="flex:2"><label>ค้นหา (เลขใบลดหนี้ / ใบเสร็จต้นทาง / เลขจอง / ลูกค้า / เลข NextAcc)</label>
                                <asp:TextBox ID="txtSearch" runat="server" CssClass="form-control" /></div>
                            <div><asp:Button ID="btnSearch" runat="server" Text="🔍 ค้นหา" CssClass="btn-cn" OnClick="btnSearch_Click" /></div>
                        </div>
                    </div>
                    <div class="cn-card">
                        <div class="cn-scroll">
                            <asp:Repeater ID="rptList" runat="server" OnItemCommand="rptList_ItemCommand">
                                <HeaderTemplate>
                                    <table class="cn-t"><tr>
                                        <th>เลขที่</th><th>วันที่</th><th>ต้นทาง</th><th>ลูกค้า / การจอง</th><th>เหตุผล</th>
                                        <th class="num">ลดหนี้</th><th class="num">VAT</th><th class="num">คืนเงิน</th><th class="num">ยึด</th>
                                        <th>คืนจาก</th><th>สถานะ</th><th>NextAcc</th><th class="cn-noprint">จัดการ</th></tr>
                                </HeaderTemplate>
                                <ItemTemplate>
                                    <tr>
                                        <td><b><%# H(Eval("CN_No")) %></b></td>
                                        <td><%# D(Eval("CN_Date")) %></td>
                                        <td><%# H(Eval("Source_Receipt_ID")) %><br /><span class="cn-note"><%# Eval("Source_Doc_Type").ToString() == "DEPOSIT" ? "มัดจำ" : "ใบเสร็จ/ใบกำกับ" %></span></td>
                                        <td><%# H(Eval("Customer_Name")) %><%# Eval("Reservation_ID") == DBNull.Value ? "" : "<br/><span class='cn-note'>จอง #" + Eval("Reservation_ID") + "</span>" %></td>
                                        <td><%# H(Take_Time_BangPhra.Integration.CreditNoteService.ReasonLabel(Convert.ToString(Eval("Reason_Code")))) %><br /><span class="cn-note"><%# H(Eval("Reason_Text")) %></span></td>
                                        <td class="num"><%# M(Eval("Amount_Gross")) %></td>
                                        <td class="num"><%# M(Eval("Vat_Amount")) %></td>
                                        <td class="num"><%# M(Eval("Refund_Amount")) %><%# Convert.ToString(Eval("Refund_Status")) == "PENDING" ? "<br/><span class='cn-chip DRAFT'>รอคืน</span>" : "" %></td>
                                        <td class="num"><%# M(Eval("Retained_Amount")) %></td>
                                        <td><%# H(Take_Time_BangPhra.Integration.CreditNoteService.MethodLabel(Convert.ToString(Eval("Refund_Method")))) %><br /><span class="cn-note"><%# H(Eval("PaidHowName")) %> <%# H(Eval("Refund_Ref")) %></span></td>
                                        <td><span class="cn-chip <%# Eval("Status") %>"><%# H(Take_Time_BangPhra.Integration.CreditNoteService.StatusLabel(Convert.ToString(Eval("Status")))) %></span>
                                            <%# string.IsNullOrEmpty(Convert.ToString(Eval("Sync_Error"))) || Convert.ToString(Eval("Status")) == "SYNCED" || Convert.ToString(Eval("Status")) == "VOIDED" ? "" : "<br/><span class='cn-chip ERR' title='" + H(Eval("Sync_Error")) + "'>⚠ ส่งไม่สำเร็จ</span>" %></td>
                                        <td><%# H(Eval("Nexaacc_Doc_No")) %></td>
                                        <td class="cn-act cn-noprint">
                                            <a href="<%# ResolveUrl("~/Account/CreditNotes?print=" + Eval("ID")) %>" target="_blank">🖨 พิมพ์</a>
                                            <%# Eval("Nexaacc_Doc_Id") != DBNull.Value && Convert.ToString(Eval("Nexaacc_Path")) != "JOURNAL" ? "<a href='" + ResolveUrl("~/Account/CreditNotes?pdf=" + Eval("ID")) + "' target='_blank'>📄 PDF NextAcc</a>" : "" %>
                                            <asp:LinkButton ID="lbApprove" runat="server" CommandName="Approve" CommandArgument='<%# Eval("ID") %>' Text="✔ อนุมัติ"
                                                Visible='<%# CanApprove && Convert.ToString(Eval("Status")) == "DRAFT" %>' OnClientClick="return confirm('อนุมัติใบลดหนี้นี้? (คืนผ่านเกตเวย์/คืนสต๊อก/ส่ง NextAcc จะเกิดขึ้นทันที)');" />
                                            <asp:LinkButton ID="lbRefund" runat="server" CommandName="Refund" CommandArgument='<%# Eval("ID") %>' Text="💸 บันทึกคืนเงิน"
                                                Visible='<%# Convert.ToString(Eval("Refund_Status")) == "PENDING" && Convert.ToString(Eval("Status")) != "VOIDED" && Convert.ToString(Eval("Status")) != "DRAFT" %>' />
                                            <asp:LinkButton ID="lbVoid" runat="server" CommandName="Void" CommandArgument='<%# Eval("ID") %>' Text="✖ ยกเลิก"
                                                Visible='<%# CanApprove && Convert.ToString(Eval("Status")) != "VOIDED" %>'
                                                OnClientClick="var r=prompt('เหตุผลการยกเลิกใบลดหนี้:');if(!r)return false;document.getElementById('hfActReason').value=r;return true;" />
                                        </td>
                                    </tr>
                                </ItemTemplate>
                                <FooterTemplate></table></FooterTemplate>
                            </asp:Repeater>
                            <asp:Literal ID="litListFoot" runat="server" />
                        </div>
                    </div>
                </asp:Panel>

                <!-- ═══ ออกใบลดหนี้ manual ═══ -->
                <asp:Panel ID="pnlNew" runat="server" Visible="false">
                    <div class="cn-legal">
                        <b>ใช้เมื่อ</b> มูลค่าลดลงหลังออกใบเสร็จ/ใบกำกับแล้ว (ม.86/10, ม.82/10): ลดราคา/ส่วนลดภายหลัง · ชดเชยบริการไม่ตรงตกลง · รับคืนสินค้า ·
                        คำนวณราคาผิดสูงไป · ยกเลิกบริการที่ชำระแล้ว — ต้องออกในเดือนที่เกิดเหตุ (หรือเดือนถัดไปตาม ป.80/2542) ภาษีขายลดในเดือนที่ออกใบลดหนี้.
                        <b>ใบเสร็จมัดจำ</b> ไม่ออกจากหน้านี้ — ใช้ "ยกเลิกคืนเงิน → คืนเงินแบบละเอียด" ในหน้าตารางการจอง.
                        ใบกำกับที่ลงวันที่ผิด/ข้อมูลผิดทั้งใบภายในเดือนเดียวกัน ใช้การยกเลิกแล้วออกใหม่แทน
                    </div>
                    <div class="cn-card">
                        <h3>1) เลือกใบต้นทาง</h3>
                        <div class="cn-row">
                            <div class="cn-field" style="flex:2"><label>ค้นหาเลขใบเสร็จ / เลขจอง / ชื่อลูกค้า / เบอร์โทร (เว้นว่าง = 30 วันล่าสุด)</label>
                                <asp:TextBox ID="txtSrcSearch" runat="server" CssClass="form-control" /></div>
                            <div><asp:Button ID="btnSrcSearch" runat="server" Text="🔍 ค้นหา" CssClass="btn-cn2" OnClick="btnSrcSearch_Click" CausesValidation="false" /></div>
                        </div>
                        <div class="cn-row">
                            <div class="cn-field" style="flex:3"><label>ใบต้นทาง</label>
                                <asp:DropDownList ID="ddlSource" runat="server" CssClass="form-control" /></div>
                            <div><asp:Button ID="btnLoadSource" runat="server" Text="ใช้ใบนี้ →" CssClass="btn-cn" OnClick="btnLoadSource_Click" CausesValidation="false" /></div>
                        </div>
                    </div>

                    <asp:Panel ID="pnlSrc" runat="server" Visible="false">
                        <asp:HiddenField ID="hfSrcId" runat="server" />
                        <div class="cn-card">
                            <h3>2) รายการที่ลดหนี้ (ยอดรวม VAT)</h3>
                            <asp:Literal ID="litSrcInfo" runat="server" />
                            <div class="cn-scroll">
                                <asp:Repeater ID="rptLines" runat="server">
                                    <HeaderTemplate>
                                        <table class="cn-t" style="min-width:720px"><tr><th>#</th><th>รายการในใบเดิม</th><th class="num">ยอดเดิม</th>
                                            <th class="num">ลดหนี้ (บาท)</th><th class="num">จำนวนที่คืน</th><th>คืนเข้าสต๊อก</th></tr>
                                    </HeaderTemplate>
                                    <ItemTemplate>
                                        <tr>
                                            <td><%# Eval("No") %>
                                                <asp:HiddenField ID="hfNo" runat="server" Value='<%# Eval("No") %>' /></td>
                                            <td><%# H(Eval("Description")) %><br /><span class="cn-note"><%# Q(Eval("Qty")) %> <%# H(Eval("Unit")) %> × <%# M(Eval("UnitPrice")) %></span></td>
                                            <td class="num"><%# M(Eval("Amount")) %></td>
                                            <td class="num"><asp:TextBox ID="txtLineAmt" runat="server" CssClass="form-control cn-amt" Text="0" /></td>
                                            <td class="num"><asp:TextBox ID="txtLineQty" runat="server" CssClass="form-control cn-qty" Text='<%# Q(Eval("Qty")) %>' /></td>
                                            <td><asp:CheckBox ID="chkRestock" runat="server" Enabled='<%# Convert.ToBoolean(Eval("Restockable")) %>'
                                                    Text='<%# Convert.ToBoolean(Eval("Restockable")) ? " คืนสต๊อก" : " —" %>' /></td>
                                        </tr>
                                    </ItemTemplate>
                                    <FooterTemplate></table></FooterTemplate>
                                </asp:Repeater>
                            </div>
                            <div class="cn-row" style="margin-top:10px">
                                <div class="cn-field"><label>ลดหนี้ทั้งใบ (ใส่ยอดรวมแล้วระบบแตกตามสัดส่วนบรรทัด)</label>
                                    <asp:TextBox ID="txtWholeAmount" runat="server" CssClass="form-control" placeholder="เว้นว่าง = ใช้ยอดรายบรรทัด" /></div>
                                <div class="cn-field"><label>เหตุผล (พิมพ์บนใบลดหนี้)</label>
                                    <asp:DropDownList ID="ddlReason" runat="server" CssClass="form-control" /></div>
                                <div class="cn-field" style="flex:2"><label>รายละเอียดเหตุผล</label>
                                    <asp:TextBox ID="txtReasonText" runat="server" CssClass="form-control" MaxLength="450" /></div>
                            </div>
                        </div>
                        <div class="cn-card">
                            <h3>3) คืนเงินลูกค้า</h3>
                            <div class="cn-row">
                                <div class="cn-field"><label>วิธีคืนเงิน</label>
                                    <asp:DropDownList ID="ddlRefundMethod" runat="server" CssClass="form-control">
                                        <asp:ListItem Value="TRANSFER" Text="โอนคืน" />
                                        <asp:ListItem Value="CASH" Text="เงินสด" />
                                        <asp:ListItem Value="GATEWAY" Text="คืนผ่านเกตเวย์ (บัตร/QR ออนไลน์ของใบนี้)" />
                                        <asp:ListItem Value="NONE" Text="ยังไม่คืนเงิน / ไม่คืน" />
                                    </asp:DropDownList></div>
                                <div class="cn-field"><label>คืนจากแหล่งเงิน</label>
                                    <asp:DropDownList ID="ddlRefundPaidHow" runat="server" CssClass="form-control" /></div>
                                <div class="cn-field"><label>เลขอ้างอิงการโอน/หมายเหตุ</label>
                                    <asp:TextBox ID="txtRefundRef" runat="server" CssClass="form-control" MaxLength="190" /></div>
                                <div class="cn-field"><label>แนบสลิป (JPG/PNG/PDF)</label>
                                    <asp:FileUpload ID="fuRefundSlip" runat="server" CssClass="form-control" /></div>
                            </div>
                            <asp:CheckBox ID="chkRefundLater" runat="server" Text=" ยังไม่คืนตอนนี้ — จะคืนภายหลัง (บันทึกคืนเงินจากรายการ)" />
                            <div class="cn-note" style="margin-top:6px">ใบเดิมรับเงินครบแล้ว → ใบลดหนี้ต้องคืนเงินหรือตั้ง "จะคืนภายหลัง" (ยอดพักเป็นเครดิตลูกค้าในลูกหนี้)
                                · เกตเวย์: เงินคืนเข้าบัตร/บัญชีเดิมของลูกค้า และลงบัญชีออกจากบัญชีพักเกตเวย์เดิม</div>
                            <div style="margin-top:12px">
                                <asp:Button ID="btnCreate" runat="server" Text="💾 บันทึกใบลดหนี้" CssClass="btn-cn" OnClick="btnCreate_Click"
                                    OnClientClick="return confirm('ยืนยันบันทึกใบลดหนี้?');" />
                            </div>
                        </div>
                    </asp:Panel>
                </asp:Panel>

                <!-- ═══ ยกเลิกการจอง + คืนเงิน ═══ -->
                <asp:Panel ID="pnlCancel" runat="server" Visible="false">
                    <asp:HiddenField ID="hfCancelRes" runat="server" />
                    <div class="cn-card">
                        <h3>เงินที่รับไว้ของการจอง</h3>
                        <asp:Literal ID="litCancelInfo" runat="server" />
                    </div>
                    <div class="cn-card">
                        <h3>คืนเงิน / ยึดเงิน</h3>
                        <div class="cn-row">
                            <div class="cn-field"><label>ยอดคืนลูกค้า (บาท)</label>
                                <asp:TextBox ID="txtCancelRefund" runat="server" CssClass="form-control" ClientIDMode="Static" /></div>
                            <div class="cn-field"><label>ส่วนที่ไม่คืน (ยึด)</label>
                                <div class="cn-sum" id="cnRetainedView">-</div></div>
                            <div class="cn-field"><label>ส่วนที่ยึดถือเป็น</label>
                                <asp:DropDownList ID="ddlRetainedAs" runat="server" CssClass="form-control">
                                    <asp:ListItem Value="PRICE_OR_FEE" Text="ค่าธรรมเนียมยกเลิก/ค่าบริการ (มี VAT)" />
                                    <asp:ListItem Value="COMPENSATION" Text="ค่าเสียหาย/ค่าปรับ (ไม่มี VAT — ให้ผู้ทำบัญชียืนยัน)" />
                                </asp:DropDownList></div>
                        </div>
                        <div class="cn-row">
                            <div class="cn-field"><label>วิธีคืนเงิน</label>
                                <asp:DropDownList ID="ddlCancelMethod" runat="server" CssClass="form-control">
                                    <asp:ListItem Value="TRANSFER" Text="โอนคืน" />
                                    <asp:ListItem Value="CASH" Text="เงินสด" />
                                    <asp:ListItem Value="GATEWAY" Text="คืนผ่านเกตเวย์ (บัตร/QR ออนไลน์)" />
                                </asp:DropDownList></div>
                            <div class="cn-field"><label>คืนจากแหล่งเงิน</label>
                                <asp:DropDownList ID="ddlCancelPaidHow" runat="server" CssClass="form-control" /></div>
                            <div class="cn-field"><label>เลขอ้างอิงการโอน</label>
                                <asp:TextBox ID="txtCancelRef" runat="server" CssClass="form-control" MaxLength="190" /></div>
                            <div class="cn-field"><label>แนบสลิป</label>
                                <asp:FileUpload ID="fuCancelSlip" runat="server" CssClass="form-control" /></div>
                        </div>
                        <div class="cn-row">
                            <div class="cn-field" style="flex:3"><label>เหตุผลการยกเลิก</label>
                                <asp:TextBox ID="txtCancelReason" runat="server" CssClass="form-control" MaxLength="450" /></div>
                        </div>
                        <div class="cn-note">ระบบแบ่งยอดคืนลงใบต้นทางให้: <b>มัดจำคงเหลือก่อน</b> (NextAcc: คืนมัดจำ — กลับหนี้สินมัดจำ + ภาษีขายตามโหมด VAT,
                            ส่วนที่ยึด = รับรู้เป็นรายได้/ออกใบกำกับของยอดที่ยึด) แล้วจึง <b>ใบกำกับ</b> (ใบลดหนี้อ้างใบเดิม) — ส่วนที่ไม่คืนของใบกำกับคงเป็นรายได้
                            (ไม่ออกใบลดหนี้ส่วนที่ยึด). ไม่ได้ใช้ปุ่มยกเลิกแบบเดิม จึงไม่ยกเลิกใบเสร็จมัดจำ/ไม่ลงรายการคืน-ริบแบบเดิมซ้ำ</div>
                        <div style="margin-top:12px">
                            <asp:Button ID="btnCancelConfirm" runat="server" Text="ยืนยันยกเลิกการจอง + บันทึกคืนเงิน" CssClass="btn-cn" OnClick="btnCancelConfirm_Click"
                                OnClientClick="return confirm('ยืนยันยกเลิกการจองและบันทึกคืนเงินตามยอดที่ระบุ?');" />
                            <a class="btn-cn2" href="<%= ResolveUrl("~/ReserveTable") %>">ยกเลิก (กลับตารางการจอง)</a>
                        </div>
                    </div>
                </asp:Panel>

                <!-- ═══ บันทึกคืนเงินภายหลัง ═══ -->
                <asp:Panel ID="pnlRefundLater" runat="server" Visible="false">
                    <asp:HiddenField ID="hfRefundCn" runat="server" />
                    <div class="cn-card">
                        <h3>บันทึกคืนเงินของใบลดหนี้ <asp:Label ID="lblRefundCn" runat="server" /></h3>
                        <div class="cn-row">
                            <div class="cn-field"><label>วิธีคืนเงิน</label>
                                <asp:DropDownList ID="ddlLaterMethod" runat="server" CssClass="form-control">
                                    <asp:ListItem Value="TRANSFER" Text="โอนคืน" />
                                    <asp:ListItem Value="CASH" Text="เงินสด" />
                                </asp:DropDownList></div>
                            <div class="cn-field"><label>คืนจากแหล่งเงิน</label>
                                <asp:DropDownList ID="ddlLaterPaidHow" runat="server" CssClass="form-control" /></div>
                            <div class="cn-field"><label>เลขอ้างอิง</label>
                                <asp:TextBox ID="txtLaterRef" runat="server" CssClass="form-control" MaxLength="190" /></div>
                            <div class="cn-field"><label>แนบสลิป</label>
                                <asp:FileUpload ID="fuLaterSlip" runat="server" CssClass="form-control" /></div>
                        </div>
                        <asp:Button ID="btnLaterSave" runat="server" Text="💾 บันทึกคืนเงิน" CssClass="btn-cn" OnClick="btnLaterSave_Click" />
                        <a class="btn-cn2" href="<%= ResolveUrl("~/Account/CreditNotes") %>">กลับ</a>
                    </div>
                </asp:Panel>
            </asp:Panel>
        </asp:Panel>
    </div>

    <script>
    (function () {
        'use strict';
        var inp = document.getElementById('txtCancelRefund');
        var view = document.getElementById('cnRetainedView');
        if (!inp || !view) return;
        var total = parseFloat(inp.getAttribute('data-total') || '0') || 0;
        function render() {
            var v = parseFloat(String(inp.value || '').replace(/,/g, ''));
            if (isNaN(v)) v = 0;
            var ret = Math.max(0, Math.round((total - v) * 100) / 100);
            view.textContent = (v < 0 || v > total + 0.001) ? 'ยอดคืนต้องอยู่ระหว่าง 0 – ' + total.toFixed(2)
                : ret.toLocaleString('th-TH', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + ' บาท';
        }
        inp.addEventListener('input', render);
        render();
    })();
    </script>
</asp:Content>
