<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="LeaveApprove.aspx.cs" Inherits="Take_Time_BangPhra.Mobile.LeaveApprove" %>

<!DOCTYPE html>
<html lang="th">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no" />
    <title>อนุมัติใบลา</title>
    <link href="https://fonts.googleapis.com/css2?family=Prompt:wght@300;400;500;600;700&display=swap" rel="stylesheet" />
    <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css" />
    <style>
        * { box-sizing: border-box; -webkit-tap-highlight-color: transparent; }
        body { margin: 0; font-family: 'Prompt', sans-serif; background: #f2f5f8; color: #24303a; padding-bottom: 30px; }
        .hd { background: linear-gradient(135deg, #6a4c93, #8a63b8); color: #fff; padding: 20px 18px 22px; }
        .hd .who { font-size: 13px; opacity: .85; }
        .hd h1 { margin: 4px 0 0; font-size: 1.3em; font-weight: 600; }
        .wrap { max-width: 640px; margin: 0 auto; padding: 14px; }
        .card { background: #fff; border-radius: 14px; box-shadow: 0 2px 10px rgba(0,0,0,.06); padding: 18px; margin-bottom: 14px; }
        .card h2 { margin: 0 0 14px; font-size: 1.02em; color: #5b3f80; font-weight: 600; }

        .kv { display: flex; padding: 10px 0; border-bottom: 1px solid #f0f3f6; font-size: 15px; }
        .kv:last-child { border-bottom: none; }
        .kv .k { width: 108px; flex-shrink: 0; color: #7d8f9c; font-size: 14px; }
        .kv .v { flex: 1; font-weight: 600; }
        .big { font-size: 19px; font-weight: 700; color: #3d2a58; }
        .warn-box { background: #fdf3e8; border-left: 4px solid #e67e22; border-radius: 9px;
                    padding: 11px 14px; font-size: 14px; color: #97591b; margin-top: 12px; }

        .btn { width: 100%; padding: 16px; border: none; border-radius: 13px; font-family: inherit;
               font-size: 16.5px; font-weight: 600; cursor: pointer; margin-bottom: 10px; }
        .btn-ok { background: #27ae60; color: #fff; }
        .btn-no { background: #fff; color: #c0392b; border: 2px solid #c0392b; }
        .btn-gh { background: #fff; color: #5b6b78; border: 2px solid #cfd8de; }

        .field { margin-bottom: 14px; }
        .field label { display: block; font-weight: 600; font-size: 14px; margin-bottom: 7px; }
        .field textarea { width: 100%; padding: 13px 14px; border: 2px solid #dde5ec; border-radius: 11px;
                          font-family: inherit; font-size: 16px; }
        .field textarea:focus { outline: none; border-color: #8a63b8; }
        .quick { display: flex; gap: 8px; flex-wrap: wrap; margin-bottom: 10px; }
        .quick span { border: 1.5px solid #dde5ec; border-radius: 20px; padding: 7px 14px;
                      font-size: 13.5px; cursor: pointer; background: #fff; }
        .quick span:active { background: #f0e9f7; }

        .msg { padding: 14px 16px; border-radius: 12px; margin-bottom: 14px; font-size: 14.5px; }
        .msg.ok { background: #e8f6ed; color: #1e7e42; border: 1px solid #b6e0c4; }
        .msg.err { background: #fdecea; color: #a5342a; border: 1px solid #f5c2bc; }
        .st { display: inline-block; padding: 3px 12px; border-radius: 12px; font-size: 12.5px; font-weight: 700; color: #fff; }
        .s-ap { background: #27ae60; } .s-rj { background: #c0392b; } .s-pd { background: #e67e22; }
        .doc-link { display: inline-block; margin-top: 8px; color: #2c5c8a; font-size: 14px; }
        .pending-item { border-left: 4px solid #e67e22; background: #fafcfd; border-radius: 9px;
                        padding: 12px 14px; margin-bottom: 10px; }
        .pending-item small { display: block; color: #7d8f9c; font-size: 12.5px; margin-top: 3px; }
        .empty { text-align: center; padding: 26px; color: #93a3af; font-size: 14px; }
    </style>
</head>
<body>
    <form id="form1" runat="server">
        <div class="hd">
            <div class="who"><asp:Literal ID="litWho" runat="server" /></div>
            <h1><i class="fas fa-clipboard-check"></i> อนุมัติใบลา</h1>
        </div>

        <div class="wrap">
            <asp:Panel ID="pnlMsg" runat="server" Visible="false">
                <div runat="server" id="divMsg" class="msg"><asp:Literal ID="litMsg" runat="server" /></div>
            </asp:Panel>

            <!-- รายละเอียดใบลา -->
            <asp:Panel ID="pnlDetail" runat="server" CssClass="card" Visible="false">
                <h2><i class="fas fa-file-lines"></i> รายละเอียดใบลา <asp:Literal ID="litStatus" runat="server" /></h2>
                <asp:Literal ID="litDetail" runat="server" />
                <asp:Literal ID="litWarn" runat="server" />
            </asp:Panel>

            <!-- ปุ่มตัดสิน -->
            <asp:Panel ID="pnlActions" runat="server" CssClass="card" Visible="false">
                <asp:Button ID="btnApprove" runat="server" Text="✓ อนุมัติใบลา" CssClass="btn btn-ok"
                    OnClick="btnApprove_Click" OnClientClick="return confirm('ยืนยันอนุมัติใบลานี้?');" />
                <asp:Button ID="btnShowReject" runat="server" Text="✕ ไม่อนุมัติ" CssClass="btn btn-no"
                    OnClick="btnShowReject_Click" CausesValidation="false" />
            </asp:Panel>

            <!-- ฟอร์มปฏิเสธ (ต้องมีเหตุผล) -->
            <asp:Panel ID="pnlReject" runat="server" CssClass="card" Visible="false">
                <h2><i class="fas fa-comment-dots"></i> เหตุผลที่ไม่อนุมัติ</h2>
                <div class="quick">
                    <span onclick="setReason('ช่วงเวลาดังกล่าวมีงานสำคัญ ไม่สามารถอนุมัติได้')">ช่วงนี้งานเยอะ</span>
                    <span onclick="setReason('มีพนักงานลาซ้ำช่วงเดียวกันหลายคน')">คนลาซ้ำช่วง</span>
                    <span onclick="setReason('กรุณาแนบใบรับรองแพทย์ประกอบการลา')">ขอใบรับรองแพทย์</span>
                    <span onclick="setReason('กรุณาแจ้งล่วงหน้ามากกว่านี้')">แจ้งกระชั้นเกินไป</span>
                    <span onclick="setReason('สิทธิ์วันลาคงเหลือไม่เพียงพอ')">วันลาไม่พอ</span>
                </div>
                <div class="field">
                    <asp:TextBox ID="txtReject" runat="server" TextMode="MultiLine" Rows="3"
                        placeholder="ระบุเหตุผล เพื่อให้ผู้ขอลาเข้าใจและปรับแผนได้" />
                </div>
                <asp:Button ID="btnReject" runat="server" Text="ยืนยันไม่อนุมัติ" CssClass="btn btn-no"
                    OnClick="btnReject_Click" />
                <asp:Button ID="btnCancelReject" runat="server" Text="ย้อนกลับ" CssClass="btn btn-gh"
                    OnClick="btnCancelReject_Click" CausesValidation="false" />
            </asp:Panel>

            <!-- ใบลาอื่นที่รออนุมัติ -->
            <asp:Panel ID="pnlPending" runat="server" CssClass="card" Visible="false">
                <h2><i class="fas fa-list-check"></i> ใบลาอื่นที่รออนุมัติ</h2>
                <asp:Literal ID="litPending" runat="server" />
            </asp:Panel>
        </div>

        <script>
            function setReason(t) {
                var el = document.getElementById('<%= txtReject.ClientID %>');
                el.value = t; el.focus();
            }
        </script>
    </form>
</body>
</html>
