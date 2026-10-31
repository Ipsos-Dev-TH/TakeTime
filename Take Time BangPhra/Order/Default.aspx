<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="Take_Time_BangPhra.Order.OrderDefault" EnableViewState="false" EnableSessionState="true" %>
<!DOCTYPE html>
<html lang="th">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover" />
    <meta name="robots" content="noindex, nofollow" />
    <meta name="theme-color" content="#5d4037" />
    <title>สั่งอาหาร<%= TableLabel.Length > 0 ? " — " + H(TableLabel) : "" %></title>
    <link rel="preconnect" href="https://fonts.googleapis.com" />
    <link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Prompt:wght@400;500;600&display=swap" />
    <link rel="stylesheet" href="/Content/dining.css?v=1" />
</head>
<body class="dn-body">
<% if (State == "ok") { %>
<div class="dn-app" id="dnApp">
    <header class="dn-head">
        <div class="dn-head-row">
            <div class="dn-head-txt">
                <div class="dn-kicker">สั่งอาหาร · Order</div>
                <h1 class="dn-table" id="dnTable"><%= H(TableLabel) %></h1>
            </div>
            <a class="dn-head-btn" id="dnStatusLink" href="<%= H(StatusUrl) %>">สถานะ / เช็คบิล</a>
        </div>
        <div class="dn-resume" id="dnResume" hidden>โต๊ะนี้มีออเดอร์อยู่แล้ว — สั่งเพิ่มได้ รวมบิลเดียวกัน</div>
    </header>

    <div class="dn-sticky">
        <div class="dn-search">
            <input id="dnSearch" type="search" placeholder="ค้นหาเมนู… (Search)" autocomplete="off" aria-label="ค้นหาเมนู" />
        </div>
        <div class="dn-chips" id="dnChips" role="tablist" aria-label="หมวดเมนู"></div>
    </div>

    <main class="dn-main" id="dnMenu" aria-live="polite">
        <div class="dn-skel"><div class="dn-skel-card"></div><div class="dn-skel-card"></div><div class="dn-skel-card"></div></div>
    </main>

    <footer class="dn-foot">ราคารวมภาษีมูลค่าเพิ่มแล้ว · ยอดที่ต้องชำระระบบคำนวณให้ตอนยืนยัน</footer>

    <div class="dn-bar" id="dnBar" hidden>
        <button type="button" class="dn-bar-btn" id="dnBarBtn">
            <span class="dn-bar-count" id="dnBarCount">0</span>
            <span class="dn-bar-label">ดูตะกร้า · Cart</span>
            <span class="dn-bar-total" id="dnBarTotal">฿0.00</span>
        </button>
    </div>

    <%-- รายละเอียดเมนู + ตัวเลือก --%>
    <div class="dn-sheet" id="dnItemSheet" hidden>
        <div class="dn-sheet-bg" data-close="dnItemSheet"></div>
        <div class="dn-sheet-panel" role="dialog" aria-modal="true" aria-labelledby="dnItemName">
            <button type="button" class="dn-sheet-x" data-close="dnItemSheet" aria-label="ปิด">✕</button>
            <div class="dn-sheet-body">
                <img class="dn-item-img" id="dnItemImg" alt="" hidden />
                <h2 class="dn-item-name" id="dnItemName"></h2>
                <div class="dn-item-en" id="dnItemEn"></div>
                <p class="dn-item-desc" id="dnItemDesc"></p>
                <div class="dn-tags" id="dnItemTags"></div>
                <div id="dnItemGroups"></div>
                <label class="dn-field">
                    <span>หมายเหตุถึงครัว (ไม่บังคับ)</span>
                    <input id="dnItemNote" type="text" maxlength="300" placeholder="เช่น ไม่ใส่ผัก, เผ็ดน้อย" />
                </label>
            </div>
            <div class="dn-sheet-foot">
                <div class="dn-qty">
                    <button type="button" id="dnQtyMinus" aria-label="ลดจำนวน">−</button>
                    <span id="dnQty">1</span>
                    <button type="button" id="dnQtyPlus" aria-label="เพิ่มจำนวน">+</button>
                </div>
                <button type="button" class="dn-btn dn-btn-primary dn-grow" id="dnAddBtn">ใส่ตะกร้า · <span id="dnAddTotal">฿0.00</span></button>
            </div>
        </div>
    </div>

    <%-- ตะกร้า --%>
    <div class="dn-sheet" id="dnCartSheet" hidden>
        <div class="dn-sheet-bg" data-close="dnCartSheet"></div>
        <div class="dn-sheet-panel" role="dialog" aria-modal="true" aria-labelledby="dnCartTitle">
            <button type="button" class="dn-sheet-x" data-close="dnCartSheet" aria-label="ปิด">✕</button>
            <div class="dn-sheet-body">
                <h2 id="dnCartTitle">ตะกร้า <span class="dn-h-en">Cart</span></h2>
                <div id="dnCartLines" class="dn-cart-lines"></div>
                <div class="dn-sum" id="dnCartSum"></div>
                <div class="dn-muted dn-minorder" id="dnMinOrder" hidden></div>
            </div>
            <div class="dn-sheet-foot">
                <button type="button" class="dn-btn dn-btn-ghost" data-close="dnCartSheet">เลือกเพิ่ม</button>
                <button type="button" class="dn-btn dn-btn-primary dn-grow" id="dnCheckoutBtn">สั่งเลย</button>
            </div>
        </div>
    </div>

    <%-- ยืนยันสั่ง --%>
    <div class="dn-sheet" id="dnCheckoutSheet" hidden>
        <div class="dn-sheet-bg" data-close="dnCheckoutSheet"></div>
        <div class="dn-sheet-panel" role="dialog" aria-modal="true" aria-labelledby="dnCoTitle">
            <button type="button" class="dn-sheet-x" data-close="dnCheckoutSheet" aria-label="ปิด">✕</button>
            <div class="dn-sheet-body">
                <h2 id="dnCoTitle">ยืนยันออเดอร์ <span class="dn-h-en">Confirm</span></h2>
                <div class="dn-quote" id="dnQuote"><div class="dn-skel-line"></div><div class="dn-skel-line"></div></div>
                <div class="dn-alert dn-alert-err" id="dnCoErr" hidden></div>
                <label class="dn-field"><span>ชื่อผู้สั่ง (ไม่บังคับ)</span><input id="dnCoName" type="text" maxlength="100" autocomplete="name" /></label>
                <label class="dn-field"><span>เบอร์โทรศัพท์ <small id="dnCoPhoneHint">(ไม่บังคับ · จำเป็นเมื่อโอนเงิน)</small></span><input id="dnCoPhone" type="tel" inputmode="tel" maxlength="30" autocomplete="tel" placeholder="08x-xxx-xxxx" /></label>
                <label class="dn-field"><span>หมายเหตุออเดอร์ (ไม่บังคับ)</span><textarea id="dnCoNote" rows="2" maxlength="500" placeholder="เช่น ขอช้อนเพิ่ม, แพ้ถั่ว"></textarea></label>
                <div class="dn-pay-pick" id="dnPayPick">
                    <div class="dn-field-h">ชำระเงิน</div>
                    <label class="dn-radio"><input type="radio" name="dnMethod" value="CASH" checked /><span><b>จ่ายตอนเช็คบิล</b><small>สั่งได้หลายรอบ แล้วกด "เช็คบิล" — เงินสดพนักงานมาเก็บที่โต๊ะ</small></span></label>
                    <label class="dn-radio" id="dnMethodTransfer"><input type="radio" name="dnMethod" value="TRANSFER" /><span><b>โอนชำระรอบนี้เลย</b><small>หน้าถัดไปแสดงบัญชีและยอดที่ต้องโอน แล้วแนบสลิป — ระบบตรวจให้เอง</small></span></label>
                </div>
            </div>
            <div class="dn-sheet-foot dn-sheet-foot-col">
                <button type="button" class="dn-btn dn-btn-primary dn-grow" id="dnPlaceBtn">สั่งเลย (จ่ายตอนเช็คบิล)</button>
                <div class="dn-muted dn-center">กดแล้วครัวเริ่มทำทันที · ออเดอร์รวมในบิลของโต๊ะนี้</div>
            </div>
        </div>
    </div>

    <div class="dn-toast" id="dnToast" role="status" aria-live="polite" hidden></div>
</div>
<script id="dnBoot" type="application/json"><%= BootJson %></script>
<script src="/Scripts/dining.js?v=1"></script>
<script>if (window.DN) { DN.menuPage(); }</script>
<% } else { %>
<div class="dn-app dn-app-state">
    <div class="dn-state dn-state-<%= H(State) %>">
        <div class="dn-state-icon"><%= State == "closed" ? "🕒" : State == "off" ? "🔧" : "⚠️" %></div>
        <% if (TableLabel.Length > 0) { %><div class="dn-kicker"><%= H(TableLabel) %></div><% } %>
        <h1><%= H(Heading) %></h1>
        <p><%= H(Message) %></p>
        <% if (HoursText.Length > 0) { %><p class="dn-hours"><%= H(HoursText) %></p><% } %>
        <% if (SubMessage.Length > 0) { %><p class="dn-muted"><%= H(SubMessage) %></p><% } %>
        <% if (HasStatusLink) { %><a class="dn-btn dn-btn-primary" href="<%= H(StatusUrl) %>">ดูออเดอร์ / เช็คบิลของโต๊ะนี้</a><% } %>
        <p class="dn-muted">หากต้องการความช่วยเหลือ กรุณาเรียกพนักงาน</p>
    </div>
</div>
<% } %>
</body>
</html>
