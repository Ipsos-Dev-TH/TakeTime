<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Leave.aspx.cs" Inherits="Take_Time_BangPhra.Mobile.LeaveMobile" %>

<!DOCTYPE html>
<html lang="th">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no" />
    <title>ยื่นใบลา</title>
    <link href="https://fonts.googleapis.com/css2?family=Prompt:wght@300;400;500;600;700&display=swap" rel="stylesheet" />
    <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css" />
    <style>
        * { box-sizing: border-box; -webkit-tap-highlight-color: transparent; }
        body { margin: 0; font-family: 'Prompt', sans-serif; background: #f2f5f8; color: #24303a; padding-bottom: 30px; }

        .hd { background: linear-gradient(135deg, #2c5c8a, #3f7fb5); color: #fff; padding: 20px 18px 22px; }
        .hd .who { font-size: 13px; opacity: .85; }
        .hd h1 { margin: 4px 0 0; font-size: 1.35em; font-weight: 600; }

        .wrap { max-width: 640px; margin: 0 auto; padding: 14px; }
        .card { background: #fff; border-radius: 14px; box-shadow: 0 2px 10px rgba(0,0,0,.06); padding: 18px; margin-bottom: 14px; }
        .card h2 { margin: 0 0 14px; font-size: 1.02em; color: #2c5c8a; font-weight: 600; }

        .quota { display: flex; gap: 10px; overflow-x: auto; padding-bottom: 4px; }
        .q-item { flex: 0 0 auto; min-width: 108px; background: #eef4fa; border-radius: 12px; padding: 12px 14px; text-align: center; }
        .q-item .n { font-size: 22px; font-weight: 700; color: #2c5c8a; }
        .q-item .l { font-size: 12px; color: #6b7f8f; margin-top: 2px; }
        .q-item.low .n { color: #c0392b; }

        .field { margin-bottom: 15px; }
        .field label { display: block; font-weight: 600; font-size: 14px; margin-bottom: 7px; }
        .field input[type=text], .field input[type=date], .field input[type=number],
        .field select, .field textarea {
            width: 100%; padding: 13px 14px; border: 2px solid #dde5ec; border-radius: 11px;
            font-family: inherit; font-size: 16px; background: #fff; color: #24303a;
        }
        .field input:focus, .field select:focus, .field textarea:focus { outline: none; border-color: #3f7fb5; }
        .hintline { font-size: 12.5px; color: #7d8f9c; margin-top: 5px; }

        /* เลือกประเภทลาแบบปุ่มใหญ่ กดง่ายด้วยนิ้ว */
        .types { display: grid; grid-template-columns: repeat(auto-fill, minmax(140px, 1fr)); gap: 9px; }
        .type-btn {
            border: 2px solid #dde5ec; background: #fff; border-radius: 12px; padding: 13px 10px;
            text-align: center; cursor: pointer; font-size: 14.5px; font-weight: 600; color: #37474f;
        }
        .type-btn i { display: block; font-size: 19px; margin-bottom: 5px; color: #3f7fb5; }
        .type-btn.sel { background: #e8f1fa; border-color: #3f7fb5; color: #1e4b73; }

        .row2 { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }
        .seg { display: flex; gap: 8px; }
        .seg-btn { flex: 1; border: 2px solid #dde5ec; background: #fff; border-radius: 11px;
                   padding: 11px; text-align: center; font-size: 14px; font-weight: 600; cursor: pointer; color: #37474f; }
        .seg-btn.sel { background: #e8f1fa; border-color: #3f7fb5; color: #1e4b73; }

        .calc { background: #eef7f0; border-radius: 11px; padding: 13px 15px; font-size: 14.5px; color: #1e7040; font-weight: 600; }
        .calc.warn { background: #fdf3e8; color: #97591b; }

        .btn-main { width: 100%; padding: 16px; border: none; border-radius: 13px; font-family: inherit;
                    font-size: 16.5px; font-weight: 600; cursor: pointer; background: #2c5c8a; color: #fff; }
        .btn-main:disabled { background: #b4c3ce; }

        .msg { padding: 14px 16px; border-radius: 12px; margin-bottom: 14px; font-size: 14.5px; }
        .msg.ok { background: #e8f6ed; color: #1e7e42; border: 1px solid #b6e0c4; }
        .msg.err { background: #fdecea; color: #a5342a; border: 1px solid #f5c2bc; }

        .item { border-left: 4px solid #b4c3ce; background: #fafcfd; border-radius: 9px; padding: 12px 14px; margin-bottom: 10px; }
        .item.ap { border-left-color: #27ae60; } .item.rj { border-left-color: #c0392b; } .item.pd { border-left-color: #e67e22; }
        .item b { font-size: 14.5px; }
        .item small { display: block; color: #7d8f9c; font-size: 12.5px; margin-top: 3px; }
        .st { display: inline-block; padding: 2px 10px; border-radius: 11px; font-size: 11.5px; font-weight: 700; color: #fff; }
        .s-ap { background: #27ae60; } .s-rj { background: #c0392b; } .s-pd { background: #e67e22; } .s-cn { background: #95a5a6; }
        .reject-note { background: #fdecea; border-radius: 8px; padding: 8px 10px; margin-top: 7px; font-size: 13px; color: #a5342a; }
        .empty { text-align: center; padding: 26px; color: #93a3af; font-size: 14px; }
    </style>
</head>
<body>
    <form id="form1" runat="server">
        <div class="hd">
            <div class="who"><asp:Literal ID="litWho" runat="server" /></div>
            <h1><i class="fas fa-file-pen"></i> ยื่นใบลา</h1>
        </div>

        <div class="wrap">
            <asp:Panel ID="pnlMsg" runat="server" Visible="false">
                <div runat="server" id="divMsg" class="msg"><asp:Literal ID="litMsg" runat="server" /></div>
            </asp:Panel>

            <!-- สิทธิ์วันลาคงเหลือ -->
            <div class="card">
                <h2><i class="fas fa-calendar-check"></i> วันลาคงเหลือปีนี้</h2>
                <div class="quota"><asp:Literal ID="litQuota" runat="server" /></div>
            </div>

            <!-- ฟอร์มขอลา -->
            <div class="card">
                <h2><i class="fas fa-pen-to-square"></i> กรอกใบลา</h2>

                <div class="field">
                    <label>ประเภทการลา</label>
                    <div class="types"><asp:Literal ID="litTypes" runat="server" /></div>
                    <asp:HiddenField ID="hfLeaveType" runat="server" />
                </div>

                <div class="field">
                    <label>ช่วงเวลา</label>
                    <div class="seg">
                        <div class="seg-btn sel" data-mode="FULL" onclick="setMode(this)">เต็มวัน / หลายวัน</div>
                        <div class="seg-btn" data-mode="HALF" onclick="setMode(this)">ครึ่งวัน</div>
                    </div>
                    <asp:HiddenField ID="hfMode" runat="server" Value="FULL" />
                </div>

                <div id="fullRange">
                    <div class="row2">
                        <div class="field">
                            <label>วันที่เริ่ม</label>
                            <asp:TextBox ID="txtStart" runat="server" TextMode="Date" onchange="calcDays()" />
                        </div>
                        <div class="field">
                            <label>ถึงวันที่</label>
                            <asp:TextBox ID="txtEnd" runat="server" TextMode="Date" onchange="calcDays()" />
                        </div>
                    </div>
                </div>

                <div id="halfBox" style="display:none;">
                    <div class="field">
                        <label>วันที่ลา</label>
                        <asp:TextBox ID="txtHalfDate" runat="server" TextMode="Date" />
                    </div>
                    <div class="field">
                        <label>ช่วง</label>
                        <div class="seg">
                            <div class="seg-btn sel" data-half="MORNING" onclick="setHalf(this)">ครึ่งเช้า</div>
                            <div class="seg-btn" data-half="AFTERNOON" onclick="setHalf(this)">ครึ่งบ่าย</div>
                        </div>
                        <asp:HiddenField ID="hfHalf" runat="server" Value="MORNING" />
                    </div>
                </div>

                <div class="field">
                    <div class="calc" id="calcBox">เลือกวันที่เพื่อคำนวณจำนวนวันลา</div>
                </div>

                <div class="field">
                    <label>เหตุผลการลา</label>
                    <asp:TextBox ID="txtReason" runat="server" TextMode="MultiLine" Rows="3"
                        placeholder="ระบุเหตุผล เช่น ไปพบแพทย์ / ธุระส่วนตัว" />
                </div>

                <div class="field">
                    <label>แนบใบรับรองแพทย์ / เอกสาร (ถ้ามี)</label>
                    <asp:FileUpload ID="fuDoc" runat="server" accept="image/*,.pdf" CssClass="form-control" />
                    <div class="hintline">ลาป่วยตั้งแต่ 3 วันขึ้นไป ควรแนบใบรับรองแพทย์</div>
                </div>

                <asp:Button ID="btnSubmit" runat="server" Text="ส่งใบลา" CssClass="btn-main" OnClick="btnSubmit_Click" />
                <div class="hintline" style="text-align:center; margin-top:9px;">
                    เมื่อส่งแล้ว ระบบจะแจ้งหัวหน้าทาง LINE ทันที
                </div>
            </div>

            <!-- ประวัติการลา -->
            <div class="card">
                <h2><i class="fas fa-clock-rotate-left"></i> ใบลาของฉัน</h2>
                <asp:Literal ID="litHistory" runat="server" />
            </div>
        </div>

        <script>
            function setMode(el) {
                document.querySelectorAll('.seg-btn[data-mode]').forEach(function (b) { b.classList.remove('sel'); });
                el.classList.add('sel');
                var mode = el.getAttribute('data-mode');
                document.getElementById('<%= hfMode.ClientID %>').value = mode;
                document.getElementById('fullRange').style.display = mode === 'FULL' ? '' : 'none';
                document.getElementById('halfBox').style.display = mode === 'HALF' ? '' : 'none';
                calcDays();
            }
            function setHalf(el) {
                document.querySelectorAll('.seg-btn[data-half]').forEach(function (b) { b.classList.remove('sel'); });
                el.classList.add('sel');
                document.getElementById('<%= hfHalf.ClientID %>').value = el.getAttribute('data-half');
            }
            function pickType(el, id) {
                document.querySelectorAll('.type-btn').forEach(function (b) { b.classList.remove('sel'); });
                el.classList.add('sel');
                document.getElementById('<%= hfLeaveType.ClientID %>').value = id;
            }
            function calcDays() {
                var box = document.getElementById('calcBox');
                var mode = document.getElementById('<%= hfMode.ClientID %>').value;
                if (mode === 'HALF') { box.className = 'calc'; box.textContent = 'ลาครึ่งวัน = 0.5 วัน'; return; }
                var s = document.getElementById('<%= txtStart.ClientID %>').value;
                var e = document.getElementById('<%= txtEnd.ClientID %>').value;
                if (!s) { box.className = 'calc'; box.textContent = 'เลือกวันที่เพื่อคำนวณจำนวนวันลา'; return; }
                if (!e) e = s;
                var d1 = new Date(s), d2 = new Date(e);
                if (d2 < d1) { box.className = 'calc warn'; box.textContent = '⚠️ วันสิ้นสุดต้องไม่ก่อนวันเริ่ม'; return; }
                var days = Math.round((d2 - d1) / 86400000) + 1;
                box.className = 'calc';
                box.textContent = 'รวม ' + days + ' วัน';
            }
            // เติมวันสิ้นสุดอัตโนมัติเมื่อเลือกวันเริ่ม (ลาวันเดียวกดครั้งเดียวจบ)
            document.addEventListener('DOMContentLoaded', function () {
                var s = document.getElementById('<%= txtStart.ClientID %>');
                var e = document.getElementById('<%= txtEnd.ClientID %>');
                if (s) s.addEventListener('change', function () { if (!e.value || e.value < s.value) e.value = s.value; calcDays(); });
                calcDays();
            });
        </script>
    </form>
</body>
</html>
