<%@ Page Title="จองกิจกรรม" Language="C#" AutoEventWireup="true" CodeBehind="ActivityBooking.aspx.cs" Inherits="Take_Time_BangPhra.Guest.ActivityBooking" %>

<!DOCTYPE html>
<html lang="th">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no" />
    <title>จองกิจกรรม | Take Time Nature Resort</title>
    <link href="https://fonts.googleapis.com/css2?family=Prompt:wght@300;400;500;600;700&display=swap" rel="stylesheet" />
    <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css" />
    <style>
        * { box-sizing: border-box; }
        body { margin: 0; font-family: 'Prompt', sans-serif; background: #f4f7f5; color: #2c3e34; padding-bottom: 40px; }
        .gp-head {
            background: linear-gradient(135deg, #2e5d3a, #4a7c59); color: #fff;
            padding: 18px 20px; display: flex; align-items: center; gap: 14px;
            position: sticky; top: 0; z-index: 20; box-shadow: 0 2px 10px rgba(0,0,0,.15);
        }
        .gp-head a.back { color: #fff; text-decoration: none; font-size: 1.3em; }
        .gp-head h1 { margin: 0; font-size: 1.15em; font-weight: 600; flex: 1; }
        .gp-room { font-size: 12px; opacity: .85; }
        .wrap { max-width: 760px; margin: 0 auto; padding: 16px; }
        .card { background: #fff; border-radius: 14px; box-shadow: 0 2px 10px rgba(0,0,0,.07); padding: 18px; margin-bottom: 16px; }
        .card h2 { margin: 0 0 14px; font-size: 1.05em; color: #2e5d3a; font-weight: 600; }

        .act-item {
            display: flex; gap: 14px; padding: 14px; border: 2px solid #e6efe9; border-radius: 12px;
            margin-bottom: 12px; cursor: pointer; transition: .18s; background: #fff; align-items: center;
        }
        .act-item:hover { border-color: #4a7c59; background: #f7fbf8; }
        .act-item.selected { border-color: #4a7c59; background: #eef6f0; box-shadow: 0 3px 12px rgba(74,124,89,.18); }
        .act-ico {
            width: 56px; height: 56px; border-radius: 12px; flex-shrink: 0;
            background: linear-gradient(135deg, #dfeae2, #c3d9c9); background-size: cover; background-position: center;
            display: flex; align-items: center; justify-content: center; color: #4a7c59; font-size: 1.5em;
        }
        .act-info { flex: 1; min-width: 0; }
        .act-info b { display: block; font-size: 15px; margin-bottom: 2px; }
        .act-info small { color: #7d8f84; font-size: 12.5px; line-height: 1.5; display: block; }
        .act-cost { font-weight: 700; color: #e67e22; font-size: 14px; white-space: nowrap; }
        .act-cost.free { color: #27ae60; }

        .slot-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(104px, 1fr)); gap: 9px; }
        .slot {
            padding: 11px 6px; border-radius: 10px; border: 2px solid #dfe8e2; background: #fff;
            text-align: center; cursor: pointer; font-size: 13.5px; font-weight: 600; transition: .15s;
        }
        .slot:hover:not(.disabled) { border-color: #4a7c59; }
        .slot.selected { background: #4a7c59; color: #fff; border-color: #4a7c59; }
        .slot.disabled { background: #f2f4f3; color: #b3bfb8; cursor: not-allowed; border-color: #eceeed; text-decoration: line-through; }
        .slot small { display: block; font-weight: 400; font-size: 10.5px; opacity: .8; margin-top: 2px; }

        .pay-opt {
            display: flex; align-items: center; gap: 12px; padding: 14px; border: 2px solid #e6efe9;
            border-radius: 12px; margin-bottom: 10px; cursor: pointer; transition: .15s;
        }
        .pay-opt:hover { border-color: #4a7c59; }
        .pay-opt.selected { border-color: #4a7c59; background: #eef6f0; }
        .pay-opt i { font-size: 1.4em; color: #4a7c59; width: 30px; text-align: center; }
        .pay-opt b { display: block; font-size: 14.5px; }
        .pay-opt small { color: #7d8f84; font-size: 12.5px; }

        .field { margin-bottom: 14px; }
        .field label { display: block; font-weight: 600; font-size: 13.5px; margin-bottom: 6px; }
        .field input, .field select, .field textarea {
            width: 100%; padding: 11px 13px; border: 2px solid #dfe8e2; border-radius: 10px;
            font-family: inherit; font-size: 14.5px;
        }
        .field input:focus, .field select:focus { outline: none; border-color: #4a7c59; }

        .summary { background: #f2f8f4; border-radius: 12px; padding: 15px; margin-bottom: 14px; }
        .summary div { display: flex; justify-content: space-between; padding: 5px 0; font-size: 14px; }
        .summary .total { border-top: 2px dashed #c3d9c9; margin-top: 8px; padding-top: 10px; font-weight: 700; font-size: 1.15em; color: #2e5d3a; }

        .btn-main {
            width: 100%; padding: 15px; border: none; border-radius: 12px; font-family: inherit;
            font-size: 16px; font-weight: 600; cursor: pointer; background: #4a7c59; color: #fff;
        }
        .btn-main:disabled { background: #b9c9be; cursor: not-allowed; }
        .btn-ghost { background: #fff; color: #4a7c59; border: 2px solid #4a7c59; }

        .msg { padding: 14px 16px; border-radius: 12px; margin-bottom: 16px; font-size: 14.5px; }
        .msg.ok { background: #e8f6ed; color: #1e7e42; border: 1px solid #b6e0c4; }
        .msg.err { background: #fdecea; color: #a5342a; border: 1px solid #f5c2bc; }
        .rules { background: #fffaf0; border-left: 4px solid #f0ad4e; padding: 12px 14px; border-radius: 8px; font-size: 13px; color: #7a6027; white-space: pre-line; margin-bottom: 14px; }
        .empty { text-align: center; padding: 40px 20px; color: #93a399; }
        .empty i { font-size: 2.8em; display: block; margin-bottom: 12px; opacity: .5; }

        .mybook { border-left: 4px solid #4a7c59; background: #f8fbf9; padding: 12px 14px; border-radius: 8px; margin-bottom: 10px; }
        .mybook b { font-size: 14px; }
        .mybook small { display: block; color: #7d8f84; font-size: 12.5px; margin-top: 3px; }
        .st { display: inline-block; padding: 2px 10px; border-radius: 12px; font-size: 11px; font-weight: 700; color: #fff; }
        .st-ok { background: #27ae60; } .st-wait { background: #e67e22; } .st-no { background: #95a5a6; }
    </style>
</head>
<body>
    <form id="form1" runat="server">
        <div class="gp-head">
            <a href="Portal" class="back"><i class="fas fa-arrow-left"></i></a>
            <div style="flex:1;">
                <h1><i class="fas fa-person-hiking"></i> จองกิจกรรม</h1>
                <div class="gp-room"><asp:Literal ID="litRoom" runat="server" /></div>
            </div>
        </div>

        <div class="wrap">
            <asp:Panel ID="pnlMsg" runat="server" Visible="false">
                <div class="msg" runat="server" id="divMsg"><asp:Literal ID="litMsg" runat="server" /></div>
            </asp:Panel>

            <!-- การจองของฉัน -->
            <asp:Panel ID="pnlMyBookings" runat="server" CssClass="card" Visible="false">
                <h2><i class="fas fa-calendar-check"></i> การจองของฉัน</h2>
                <asp:Repeater ID="rptMyBookings" runat="server" OnItemCommand="rptMyBookings_ItemCommand">
                    <ItemTemplate>
                        <div class="mybook">
                            <b><%# Eval("ActivityName") %></b>
                            <span class='<%# StatusClass(Container.DataItem) %>'><%# StatusText(Container.DataItem) %></span>
                            <small><%# SlotText(Container.DataItem) %> · <%# AmountText(Container.DataItem) %></small>
                            <small><%# PaymentText(Container.DataItem) %></small>
                            <asp:LinkButton runat="server" CommandName="CancelBooking" CommandArgument='<%# Eval("ID") %>'
                                Visible='<%# CanCancel(Container.DataItem) %>'
                                OnClientClick="return confirm('ยกเลิกการจองนี้?');"
                                style="color:#c0392b;font-size:12.5px;margin-top:6px;display:inline-block;">
                                <i class="fas fa-ban"></i> ยกเลิกการจอง</asp:LinkButton>
                        </div>
                    </ItemTemplate>
                </asp:Repeater>
            </asp:Panel>

            <!-- ขั้นที่ 1: เลือกกิจกรรม -->
            <asp:Panel ID="pnlPickActivity" runat="server" CssClass="card">
                <h2><i class="fas fa-list-check"></i> เลือกกิจกรรมที่ต้องการจอง</h2>
                <asp:Repeater ID="rptActivities" runat="server" OnItemCommand="rptActivities_ItemCommand">
                    <ItemTemplate>
                        <asp:LinkButton runat="server" CommandName="PickActivity" CommandArgument='<%# Eval("ID") %>'
                            style="text-decoration:none;color:inherit;display:block;">
                            <div class="act-item">
                                <div class="act-ico" style='<%# ThumbStyle(Container.DataItem) %>'>
                                    <%# ThumbIcon(Container.DataItem) %>
                                </div>
                                <div class="act-info">
                                    <b><%# Eval("ActivityName") %></b>
                                    <small><%# ActivityMeta(Container.DataItem) %></small>
                                </div>
                                <div class='<%# CostClass(Container.DataItem) %>'><%# CostText(Container.DataItem) %></div>
                            </div>
                        </asp:LinkButton>
                    </ItemTemplate>
                </asp:Repeater>
                <asp:Panel ID="pnlNoActivities" runat="server" Visible="false" CssClass="empty">
                    <i class="fas fa-calendar-xmark"></i>
                    <div>ยังไม่มีกิจกรรมที่เปิดให้จองเวลา</div>
                    <small>กิจกรรมอื่น ๆ ใช้บริการได้เลยโดยไม่ต้องจอง</small>
                </asp:Panel>
            </asp:Panel>

            <!-- ขั้นที่ 2: เลือกวัน-เวลา -->
            <asp:Panel ID="pnlPickSlot" runat="server" CssClass="card" Visible="false">
                <h2><i class="fas fa-clock"></i> <asp:Literal ID="litActivityName" runat="server" /></h2>
                <asp:Panel ID="pnlRules" runat="server" Visible="false" CssClass="rules">
                    <asp:Literal ID="litRules" runat="server" />
                </asp:Panel>

                <div class="field">
                    <label>เลือกวันที่</label>
                    <asp:DropDownList ID="ddlDate" runat="server" AutoPostBack="true"
                        OnSelectedIndexChanged="ddlDate_Changed" />
                </div>

                <div class="field">
                    <label>เลือกช่วงเวลา (กดได้หลายช่วงติดกัน)</label>
                    <asp:Literal ID="litSlots" runat="server" />
                    <asp:HiddenField ID="hfSlots" runat="server" />
                </div>

                <asp:Panel ID="pnlParticipants" runat="server" CssClass="field" Visible="false">
                    <label>จำนวนผู้ใช้บริการ</label>
                    <asp:TextBox ID="txtParticipants" runat="server" TextMode="Number" Text="1" />
                </asp:Panel>

                <div class="field">
                    <label>หมายเหตุ (ถ้ามี)</label>
                    <asp:TextBox ID="txtNotes" runat="server" TextMode="MultiLine" Rows="2" />
                </div>

                <div style="display:flex;gap:10px;">
                    <asp:Button ID="btnBackToList" runat="server" Text="← ย้อนกลับ" CssClass="btn-main btn-ghost"
                        OnClick="btnBackToList_Click" CausesValidation="false" />
                    <asp:Button ID="btnNextToPay" runat="server" Text="ถัดไป →" CssClass="btn-main" OnClick="btnNextToPay_Click" />
                </div>
            </asp:Panel>

            <!-- ขั้นที่ 3: ชำระเงิน + ยืนยัน -->
            <asp:Panel ID="pnlPay" runat="server" CssClass="card" Visible="false">
                <h2><i class="fas fa-receipt"></i> ยืนยันการจอง</h2>

                <div class="summary">
                    <div><span>กิจกรรม</span><b><asp:Literal ID="litSumActivity" runat="server" /></b></div>
                    <div><span>วันที่</span><b><asp:Literal ID="litSumDate" runat="server" /></b></div>
                    <div><span>เวลา</span><b><asp:Literal ID="litSumTime" runat="server" /></b></div>
                    <div class="total"><span>ยอดชำระ</span><span><asp:Literal ID="litSumAmount" runat="server" /></span></div>
                </div>

                <asp:Panel ID="pnlPayMethods" runat="server">
                    <div class="field"><label>เลือกวิธีชำระเงิน</label></div>
                    <asp:RadioButtonList ID="rblPayment" runat="server" CssClass="paylist" RepeatLayout="Flow" AutoPostBack="true"
                        OnSelectedIndexChanged="rblPayment_Changed" />

                    <asp:Panel ID="pnlSlip" runat="server" Visible="false" CssClass="field">
                        <label>แนบสลิปโอนเงิน</label>
                        <asp:FileUpload ID="fuSlip" runat="server" accept="image/*,.pdf" />
                        <small style="color:#7d8f84;font-size:12.5px;display:block;margin-top:6px;">
                            แนบตอนนี้เลย หรือจองไว้ก่อนแล้วมาแนบทีหลังในหน้า "การจองของฉัน" ก็ได้
                        </small>
                    </asp:Panel>
                </asp:Panel>

                <div style="display:flex;gap:10px;margin-top:8px;">
                    <asp:Button ID="btnBackToSlot" runat="server" Text="← ย้อนกลับ" CssClass="btn-main btn-ghost"
                        OnClick="btnBackToSlot_Click" CausesValidation="false" />
                    <asp:Button ID="btnConfirm" runat="server" Text="✓ ยืนยันการจอง" CssClass="btn-main" OnClick="btnConfirm_Click" />
                </div>
            </asp:Panel>
        </div>

        <script>
            // เลือกช่วงเวลา (หลายช่วงติดกันได้) — เก็บลง hidden field ให้ server อ่าน
            function toggleSlot(el, key) {
                if (el.classList.contains('disabled')) return;
                el.classList.toggle('selected');
                var hf = document.getElementById('<%= hfSlots.ClientID %>');
                var chosen = [];
                document.querySelectorAll('.slot.selected').forEach(function (s) {
                    chosen.push(s.getAttribute('data-key'));
                });
                hf.value = chosen.join(',');
            }
        </script>
    </form>
</body>
</html>
