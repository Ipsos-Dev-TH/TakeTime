<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Status.aspx.cs" Inherits="Take_Time_BangPhra.Order.OrderStatus" EnableViewState="false" EnableSessionState="true" %>
<!DOCTYPE html>
<html lang="th">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover" />
    <meta name="robots" content="noindex, nofollow" />
    <meta name="theme-color" content="#5d4037" />
    <title>สถานะออเดอร์<%= TableLabel.Length > 0 ? " — " + H(TableLabel) : "" %></title>
    <link rel="preconnect" href="https://fonts.googleapis.com" />
    <link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Prompt:wght@400;500;600&display=swap" />
    <link rel="stylesheet" href="/Content/slip-first.css?v=2" />
    <link rel="stylesheet" href="/Content/dining.css?v=1" />
</head>
<body class="dn-body">
<% if (State == "ok") { %>
<div class="dn-app" id="dnApp">
    <header class="dn-head">
        <div class="dn-head-row">
            <div class="dn-head-txt">
                <div class="dn-kicker">สถานะออเดอร์ · Order status</div>
                <h1 class="dn-table"><%= H(TableLabel) %></h1>
            </div>
            <a class="dn-head-btn" id="dnMenuLink" href="<%= H(MenuUrl) %>">＋ สั่งเพิ่ม</a>
        </div>
        <div class="dn-live" id="dnLive"><span class="dn-dot"></span><span id="dnLiveTxt">อัปเดตอัตโนมัติ</span></div>
    </header>

    <main class="dn-main dn-status-main">
        <div class="dn-alert dn-alert-warn" id="dnZoneMsg" hidden></div>

        <%-- ยอดค้าง + ปุ่มหลัก --%>
        <section class="dn-card dn-due" id="dnDue">
            <div class="dn-due-row">
                <div><div class="dn-kicker">ยอดค้างชำระ · Outstanding</div><div class="dn-due-amt" id="dnDueAmt">฿0.00</div></div>
                <div class="dn-due-side" id="dnDueSide"></div>
            </div>
            <div class="dn-due-sub" id="dnDueSub"></div>
            <div class="dn-actions">
                <button type="button" class="dn-btn dn-btn-primary" id="dnBillBtn">เช็คบิล · Bill</button>
                <button type="button" class="dn-btn dn-btn-ghost" id="dnCallBtn">🙋 เรียกพนักงาน</button>
            </div>
        </section>

        <%-- เลือกวิธีชำระ --%>
        <section class="dn-card dn-pay" id="dnPay" hidden>
            <h2>ชำระเงิน <span class="dn-h-en">Payment</span> <small id="dnPayAmt"></small></h2>
            <div class="dn-pay-tabs" id="dnPayTabs">
                <button type="button" data-pay="cash" id="dnTabCash">💵 เงินสด</button>
                <button type="button" data-pay="transfer" id="dnTabTransfer">🏦 โอนเข้าบัญชี</button>
                <button type="button" data-pay="room" id="dnTabRoom">🛏 ลงบิลห้องพัก</button>
            </div>

            <div class="dn-pay-panel" id="dnPayCash" hidden>
                <div class="dn-wait"><div class="dn-wait-icon">💵</div><b>พนักงานจะมาเก็บเงินที่โต๊ะ</b><p>แจ้งพนักงานแล้ว กรุณารอสักครู่ — เมื่อรับเงินแล้วหน้านี้จะอัปเดตเป็น "ชำระแล้ว"</p></div>
            </div>

            <div class="dn-pay-panel" id="dnPayTransfer" hidden>
                <div class="dn-guide">
                    <div class="dn-guide-h">💸 โอนเงินตามยอดนี้</div>
                    <div class="dn-line dn-line-amt"><div class="dn-line-l"><b>ยอดที่ต้องโอน</b><small>โอนให้ตรงยอด</small></div><div class="dn-line-v" id="dnXferAmt">฿0.00</div><button type="button" class="dn-copy" id="dnXferCopy" data-sf-copy="0">คัดลอก</button></div>
                    <div id="dnAccounts"><%= AccountsHtml %></div>
                    <div class="dn-guide-sub">โอนแล้วแนบสลิปด้านล่าง — ระบบอ่านยอดจากสลิปและยืนยันให้อัตโนมัติ</div>
                </div>
                <div class="dn-slip" id="dnSlipBox">
                    <input type="file" id="dnSlipFile" accept="image/*" />
                </div>
                <div class="dn-slip-result" id="dnSlipResult" hidden></div>
            </div>

            <div class="dn-pay-panel" id="dnPayRoom" hidden>
                <div class="dn-guide">
                    <div class="dn-guide-h">🛏 ลงบิลห้องพัก</div>
                    <div class="dn-guide-sub">เลือกห้องของคุณ แล้วกรอกเบอร์โทรที่ใช้จอง — ยอดจะรวมในบิลตอนเช็คเอาท์</div>
                </div>
                <div class="dn-rooms" id="dnRooms"><div class="dn-skel-line"></div></div>
                <label class="dn-field"><span>เบอร์โทรที่ใช้จอง</span><input id="dnRoomPhone" type="tel" inputmode="tel" maxlength="30" placeholder="08x-xxx-xxxx" autocomplete="tel" /></label>
                <div class="dn-alert dn-alert-err" id="dnRoomErr" hidden></div>
                <button type="button" class="dn-btn dn-btn-primary dn-wide" id="dnRoomBtn" disabled>ยืนยันลงบิลห้อง</button>
            </div>
        </section>

        <%-- ออเดอร์ทุกรอบ --%>
        <section class="dn-card">
            <h2>ออเดอร์ของโต๊ะนี้ <span class="dn-h-en">Orders</span></h2>
            <div id="dnOrders" class="dn-orders"><div class="dn-skel-line"></div><div class="dn-skel-line"></div></div>
        </section>

        <%-- การชำระ --%>
        <section class="dn-card" id="dnPaysCard" hidden>
            <h2>การชำระเงิน <span class="dn-h-en">Payments</span></h2>
            <div id="dnPays"></div>
        </section>

        <div class="dn-muted dn-center dn-foot-note">ราคารวมภาษีมูลค่าเพิ่มแล้ว · ไม่มีค่าบริการ</div>
    </main>

    <%-- เรียกพนักงาน --%>
    <div class="dn-sheet" id="dnCallSheet" hidden>
        <div class="dn-sheet-bg" data-close="dnCallSheet"></div>
        <div class="dn-sheet-panel dn-sheet-sm" role="dialog" aria-modal="true" aria-labelledby="dnCallTitle">
            <button type="button" class="dn-sheet-x" data-close="dnCallSheet" aria-label="ปิด">✕</button>
            <div class="dn-sheet-body">
                <h2 id="dnCallTitle">🙋 เรียกพนักงาน</h2>
                <label class="dn-field"><span>ต้องการอะไรคะ (ไม่บังคับ)</span><input id="dnCallMsg" type="text" maxlength="200" placeholder="เช่น ขอน้ำแข็ง, ขอช้อนเพิ่ม" /></label>
                <div class="dn-quick" id="dnCallQuick">
                    <button type="button" data-q="ขอน้ำเปล่า/น้ำแข็ง">น้ำเปล่า/น้ำแข็ง</button>
                    <button type="button" data-q="ขอช้อนส้อม/จานเพิ่ม">ช้อนส้อม/จาน</button>
                    <button type="button" data-q="ขอเมนูเพิ่ม">ขอเมนู</button>
                    <button type="button" data-q="อาหารยังไม่มา">อาหารยังไม่มา</button>
                </div>
            </div>
            <div class="dn-sheet-foot">
                <button type="button" class="dn-btn dn-btn-ghost" data-close="dnCallSheet">ยกเลิก</button>
                <button type="button" class="dn-btn dn-btn-primary dn-grow" id="dnCallSend">ส่งเรียกพนักงาน</button>
            </div>
        </div>
    </div>

    <div class="dn-closed-overlay" id="dnClosed" hidden>
        <div class="dn-state dn-state-closed">
            <div class="dn-state-icon">🧾</div>
            <h1>โต๊ะนี้ปิดบิลแล้ว</h1>
            <p>สแกน QR ที่โต๊ะใหม่เพื่อสั่งอีกครั้ง</p>
            <a class="dn-btn dn-btn-primary" id="dnRescan" href="<%= H(MenuUrl) %>">สั่งใหม่</a>
        </div>
    </div>

    <template id="dnContactTpl"><%= ContactHtml %></template>
    <div class="dn-toast" id="dnToast" role="status" aria-live="polite" hidden></div>
</div>
<script id="dnBoot" type="application/json"><%= BootJson %></script>
<script src="/Scripts/slip-first.js?v=3"></script>
<script src="/Scripts/dining.js?v=1"></script>
<script>if (window.DN) { DN.statusPage(); }</script>
<% } else { %>
<div class="dn-app dn-app-state">
    <div class="dn-state dn-state-<%= H(State) %>">
        <div class="dn-state-icon"><%= State == "closed" ? "🧾" : "🔧" %></div>
        <h1><%= H(Heading) %></h1>
        <p><%= H(Message) %></p>
        <% if (State == "closed") { %><a class="dn-btn dn-btn-primary" href="<%= H(MenuUrl) %>">สั่งใหม่</a><% } %>
        <p class="dn-muted">หากต้องการความช่วยเหลือ กรุณาเรียกพนักงาน</p>
    </div>
</div>
<% } %>
</body>
</html>
