<%@ Page Title="QR สั่งอาหาร — โซน & โต๊ะ" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Zones.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Dining.ZonesPage" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <link rel="stylesheet" href="<%= ResolveUrl("~/Content/admin-settings.css") %>?v=<%= Take_Time_BangPhra.Admin.Settings.SettingsUi.AssetVersion %>" />
    <link rel="stylesheet" href="<%= ResolveUrl("~/Content/dining-admin.css") %>?v=<%= Take_Time_BangPhra.Admin.Settings.SettingsUi.AssetVersion %>" />

    <div class="dn-wrap">
        <nav class="as-crumb" aria-label="เส้นทาง">
            <a href="<%= ResolveUrl("~/Admin/Settings/Index") %>"><i class="fas fa-sliders"></i> ศูนย์ตั้งค่า</a>
            <span class="sep">›</span><span>บริการในที่พัก</span>
            <span class="sep">›</span><span class="here">QR สั่งอาหาร — โซน &amp; โต๊ะ</span>
            <a class="as-back" href="<%= ResolveUrl("~/Admin/Settings/Index") %>">← กลับศูนย์ตั้งค่า</a>
        </nav>

        <div class="dn-head">
            <h2><i class="fas fa-utensils"></i> QR สั่งอาหาร — โซน &amp; โต๊ะ</h2>
            <p>
                ตั้งโซน (ริมน้ำ / ในร้าน / สระ) เวลาเปิด-ปิด วิธีชำระที่รับ แล้วสร้างโต๊ะพร้อม QR ประจำโต๊ะ (ลิงก์ <code>/Order/{token}</code>)
                · <a href="<%= ResolveUrl("~/Admin/Dining/Menu") %>">จัดการเมนู</a>
                · <a href="<%= ResolveUrl("~/Admin/Dining/QrSheet") %>">พิมพ์ QR ทุกโต๊ะ</a>
                · <a href="<%= ResolveUrl("~/Admin/Dining/Tables") %>">สถานะโต๊ะสด</a>
                · <a href="<%= ResolveUrl("~/Admin/Dining/Orders") %>">กระดานออเดอร์</a>
            </p>
        </div>

        <asp:Literal ID="litMsg" runat="server" />

        <div class="dn-tabs" id="dnTabs">
            <a href="#t-zones" data-tab="zones" class="on"><i class="fas fa-map"></i> โซน &amp; โต๊ะ</a>
            <a href="#t-settings" data-tab="settings"><i class="fas fa-gear"></i> ตั้งค่า &amp; เช็กลิสต์</a>
        </div>

        <!-- ═══════════════ แท็บ โซน & โต๊ะ ═══════════════ -->
        <section class="dn-tab on" id="tab-zones">

            <div class="dn-card">
                <h3><i class="fas fa-map"></i> โซน
                    <span class="dn-right">
                        <button type="button" class="dn-btn sm" onclick="dnAct('newZone', 0, '')"><i class="fas fa-plus"></i> เพิ่มโซน</button>
                    </span>
                </h3>
                <div class="sub">โซน = พื้นที่ที่มีเวลาเปิด-ปิด / วิธีชำระ / สถานีครัวของตัวเอง — ปิดโซน = ทุกโต๊ะในโซนสั่งไม่ได้ (ลูกค้าเห็นข้อความที่ตั้ง)</div>
                <div class="dn-scroll"><asp:Literal ID="litZones" runat="server" /></div>
            </div>

            <asp:Panel ID="pnlZoneForm" runat="server" CssClass="dn-card" Visible="false">
                <h3><i class="fas fa-pen"></i> <asp:Literal ID="litZoneFormTitle" runat="server" Text="เพิ่มโซน" /></h3>
                <asp:HiddenField ID="hfZoneId" runat="server" Value="0" />
                <div class="dn-form">
                    <div>
                        <label class="lb">รหัสโซน <span class="mini">(A-Z 0-9 _ - ≤20)</span></label>
                        <asp:TextBox ID="txtZoneCode" runat="server" CssClass="dn-in" MaxLength="20" placeholder="เช่น RIVER" />
                    </div>
                    <div>
                        <label class="lb">ชื่อโซน (ไทย)</label>
                        <asp:TextBox ID="txtZoneName" runat="server" CssClass="dn-in" MaxLength="100" placeholder="เช่น ริมน้ำ" />
                    </div>
                    <div>
                        <label class="lb">ลำดับ</label>
                        <asp:TextBox ID="txtZoneSort" runat="server" CssClass="dn-in" TextMode="Number" Text="0" />
                    </div>
                    <div>
                        <label class="lb">สถานีครัว <span class="mini">(ครัว / บาร์ — ไม่บังคับ)</span></label>
                        <asp:TextBox ID="txtZoneKitchen" runat="server" CssClass="dn-in" MaxLength="50" placeholder="เช่น ครัวหลัก" />
                    </div>
                    <div>
                        <label class="lb">โหมดเปิด-ปิด</label>
                        <asp:DropDownList ID="ddlZoneMode" runat="server" CssClass="dn-sel">
                            <asp:ListItem Value="AUTO" Text="อัตโนมัติตามเวลา" />
                            <asp:ListItem Value="OPEN" Text="บังคับเปิด (ไม่สนเวลา)" />
                            <asp:ListItem Value="CLOSED" Text="บังคับปิด (ไม่สนเวลา)" />
                        </asp:DropDownList>
                        <div class="dn-hint">อัตโนมัติ + ไม่ตั้งเวลา = เปิดตลอด · ตั้งปิดน้อยกว่าเปิดได้ (ข้ามเที่ยงคืน)</div>
                    </div>
                    <div>
                        <label class="lb">เวลาเปิด</label>
                        <asp:TextBox ID="txtZoneOpen" runat="server" CssClass="dn-in" TextMode="Time" />
                    </div>
                    <div>
                        <label class="lb">เวลาปิด</label>
                        <asp:TextBox ID="txtZoneClose" runat="server" CssClass="dn-in" TextMode="Time" />
                    </div>
                    <div class="full">
                        <label class="lb">ข้อความเมื่อปิด (ลูกค้าเห็นตอนสแกน)</label>
                        <asp:TextBox ID="txtZoneClosedMsg" runat="server" CssClass="dn-in" MaxLength="300" placeholder="ขณะนี้ปิดรับออเดอร์ กรุณาสั่งในเวลาทำการ" />
                    </div>
                    <div class="full">
                        <label class="lb">วิธีชำระที่รับในโซนนี้ (อย่างน้อย 1)</label>
                        <div class="dn-cbl">
                            <span class="dn-chk"><asp:CheckBox ID="chkZoneCash" runat="server" Checked="true" /> เงินสด (พนักงานเก็บที่โต๊ะ)</span>
                            <span class="dn-chk"><asp:CheckBox ID="chkZoneTransfer" runat="server" Checked="true" /> โอนเงิน (ระบบตรวจสลิป)</span>
                            <span class="dn-chk"><asp:CheckBox ID="chkZoneRoomCharge" runat="server" /> ลงบิลห้องพัก (ยืนยันเบอร์โทรที่จอง)</span>
                        </div>
                    </div>
                    <div class="dn-chk"><asp:CheckBox ID="chkZoneActive" runat="server" Checked="true" /> ใช้งานโซนนี้</div>
                </div>
                <div class="dn-bar">
                    <asp:Button ID="btnSaveZone" runat="server" CssClass="dn-btn" Text="💾 บันทึกโซน" OnClick="btnSaveZone_Click" />
                    <asp:Button ID="btnCancelZone" runat="server" CssClass="dn-btn sec" Text="ยกเลิก" OnClick="btnCancelForm_Click" CausesValidation="false" />
                </div>
            </asp:Panel>

            <div class="dn-card">
                <h3><i class="fas fa-chair"></i> โต๊ะ
                    <span class="dn-right">
                        <asp:DropDownList ID="ddlZoneFilter" runat="server" CssClass="dn-sel" AutoPostBack="true" OnSelectedIndexChanged="ddlZoneFilter_Changed" style="width:auto; min-width:160px; padding:6px 10px;" />
                        <button type="button" class="dn-btn sm sec" onclick="dnAct('newTable', 0, '')"><i class="fas fa-plus"></i> เพิ่มโต๊ะ</button>
                        <button type="button" class="dn-btn sm sec" onclick="dnAct('bulk', 0, '')"><i class="fas fa-layer-group"></i> สร้างหลายโต๊ะ</button>
                        <a class="dn-btn sm" id="lnkPrintZone" runat="server" href="~/Admin/Dining/QrSheet"><i class="fas fa-print"></i> พิมพ์ QR</a>
                    </span>
                </h3>
                <div class="sub">
                    QR ของโต๊ะพิมพ์ติดโต๊ะถาวร · <b>หมุน QR</b> เมื่อสงสัยว่าลิงก์หลุดออกนอกร้าน (QR เดิมใช้ไม่ได้ทันที ต้องพิมพ์ใหม่; ลูกค้าที่นั่งอยู่ไม่กระทบ)
                    · ปิดใช้โต๊ะ = สแกนแล้วขึ้น "โต๊ะนี้ปิดใช้งานชั่วคราว"
                </div>
                <div class="dn-scroll"><asp:Literal ID="litTables" runat="server" /></div>
            </div>

            <asp:Panel ID="pnlTableForm" runat="server" CssClass="dn-card" Visible="false">
                <h3><i class="fas fa-pen"></i> <asp:Literal ID="litTableFormTitle" runat="server" Text="เพิ่มโต๊ะ" /></h3>
                <asp:HiddenField ID="hfTableId" runat="server" Value="0" />
                <div class="dn-form">
                    <div>
                        <label class="lb">โซน</label>
                        <asp:DropDownList ID="ddlTableZone" runat="server" CssClass="dn-sel" />
                    </div>
                    <div>
                        <label class="lb">รหัสโต๊ะ <span class="mini">(พิมพ์บน QR เช่น A1)</span></label>
                        <asp:TextBox ID="txtTableCode" runat="server" CssClass="dn-in" MaxLength="20" />
                    </div>
                    <div>
                        <label class="lb">ชื่อโต๊ะ (ไม่บังคับ)</label>
                        <asp:TextBox ID="txtTableName" runat="server" CssClass="dn-in" MaxLength="100" placeholder="เช่น โต๊ะริมน้ำ 1" />
                    </div>
                    <div>
                        <label class="lb">ที่นั่ง</label>
                        <asp:TextBox ID="txtTableSeats" runat="server" CssClass="dn-in" TextMode="Number" Text="4" />
                    </div>
                    <div>
                        <label class="lb">ลำดับ</label>
                        <asp:TextBox ID="txtTableSort" runat="server" CssClass="dn-in" TextMode="Number" Text="0" />
                    </div>
                    <div class="full">
                        <label class="lb">หมายเหตุ (พนักงานเห็น)</label>
                        <asp:TextBox ID="txtTableNote" runat="server" CssClass="dn-in" MaxLength="300" />
                    </div>
                    <div class="dn-chk"><asp:CheckBox ID="chkTableActive" runat="server" Checked="true" /> ใช้งานโต๊ะนี้</div>
                </div>
                <div class="dn-bar">
                    <asp:Button ID="btnSaveTable" runat="server" CssClass="dn-btn" Text="💾 บันทึกโต๊ะ" OnClick="btnSaveTable_Click" />
                    <asp:Button ID="btnCancelTable" runat="server" CssClass="dn-btn sec" Text="ยกเลิก" OnClick="btnCancelForm_Click" CausesValidation="false" />
                </div>
            </asp:Panel>

            <asp:Panel ID="pnlBulk" runat="server" CssClass="dn-card" Visible="false">
                <h3><i class="fas fa-layer-group"></i> สร้างหลายโต๊ะ</h3>
                <div class="sub">สร้าง {คำนำหน้า}{จาก}..{คำนำหน้า}{ถึง} เช่น A1–A20 (รหัสที่มีอยู่แล้วถูกข้าม, สูงสุด 200 โต๊ะต่อครั้ง) — ทุกโต๊ะได้ QR ใหม่ทันที</div>
                <div class="dn-form">
                    <div>
                        <label class="lb">โซน</label>
                        <asp:DropDownList ID="ddlBulkZone" runat="server" CssClass="dn-sel" />
                    </div>
                    <div>
                        <label class="lb">คำนำหน้า</label>
                        <asp:TextBox ID="txtBulkPrefix" runat="server" CssClass="dn-in" MaxLength="10" Text="A" />
                    </div>
                    <div>
                        <label class="lb">จากเลข</label>
                        <asp:TextBox ID="txtBulkFrom" runat="server" CssClass="dn-in" TextMode="Number" Text="1" />
                    </div>
                    <div>
                        <label class="lb">ถึงเลข</label>
                        <asp:TextBox ID="txtBulkTo" runat="server" CssClass="dn-in" TextMode="Number" Text="10" />
                    </div>
                    <div>
                        <label class="lb">ที่นั่งต่อโต๊ะ</label>
                        <asp:TextBox ID="txtBulkSeats" runat="server" CssClass="dn-in" TextMode="Number" Text="4" />
                    </div>
                </div>
                <div class="dn-bar">
                    <asp:Button ID="btnBulk" runat="server" CssClass="dn-btn" Text="สร้างโต๊ะ" OnClick="btnBulk_Click" />
                    <asp:Button ID="btnCancelBulk" runat="server" CssClass="dn-btn sec" Text="ยกเลิก" OnClick="btnCancelForm_Click" CausesValidation="false" />
                </div>
            </asp:Panel>
        </section>

        <!-- ═══════════════ แท็บ ตั้งค่า & เช็กลิสต์ ═══════════════ -->
        <section class="dn-tab" id="tab-settings">

            <div class="dn-card">
                <h3><i class="fas fa-list-check"></i> เช็กลิสต์เปิดใช้งาน
                    <span class="dn-right"><button type="button" class="dn-btn sm sec" onclick="dnAct('recheck', 0, '')"><i class="fas fa-rotate"></i> ตรวจอีกครั้ง</button></span>
                </h3>
                <div class="sub">ทำครบทุกข้อแล้วค่อยเปิดสวิตช์ "เปิดใช้ QR สั่งอาหาร" — ลูกค้าจึงสแกนสั่งได้</div>
                <asp:Literal ID="litChecklist" runat="server" />
            </div>

            <div class="dn-card">
                <h3><i class="fas fa-gear"></i> ค่าตั้งระบบ</h3>
                <div class="sub">ค่าเริ่มต้นจาก migration PHASE19_42 (System_Config) — แก้ที่นี่มีผลภายใน 30 วินาที</div>
                <div class="dn-form">
                    <div class="full dn-chk" style="background:#fff3e0; border-radius:10px; padding:10px 12px;">
                        <asp:CheckBox ID="chkFeature" runat="server" /> <b>เปิดใช้ QR สั่งอาหาร (Feature_Dining)</b>
                        <span class="mini" style="margin-left:6px;">ปิด = ลูกค้าสแกนแล้วเห็น "ปิดใช้งาน", เมนูพนักงานซ่อน (หน้าตั้งค่านี้ยังเข้าได้)</span>
                    </div>
                    <div>
                        <label class="lb">ปิดโต๊ะอัตโนมัติเมื่อไม่มีกิจกรรม (นาที)</label>
                        <asp:TextBox ID="txtIdle" runat="server" CssClass="dn-in" TextMode="Number" />
                        <div class="dn-hint">หลังจ่ายครบ/ยกเลิกหมด และลูกค้าไม่มีกิจกรรมนานเท่านี้ — ลิงก์มือถือเดิมใช้ต่อไม่ได้ (5–1440)</div>
                    </div>
                    <div>
                        <label class="lb">ยอดสั่งขั้นต่ำต่อรอบ (บาท)</label>
                        <asp:TextBox ID="txtMinOrder" runat="server" CssClass="dn-in" TextMode="Number" step="0.01" />
                        <div class="dn-hint">0 = ไม่มีขั้นต่ำ</div>
                    </div>
                    <div>
                        <label class="lb">หน้าลูกค้าตรวจสถานะทุก (วินาที)</label>
                        <asp:TextBox ID="txtGuestPoll" runat="server" CssClass="dn-in" TextMode="Number" />
                        <div class="dn-hint">2–60</div>
                    </div>
                    <div>
                        <label class="lb">กระดานพนักงานตรวจออเดอร์ใหม่ทุก (วินาที)</label>
                        <asp:TextBox ID="txtStaffPoll" runat="server" CssClass="dn-in" TextMode="Number" />
                        <div class="dn-hint">2–60 · เสียง + ป้ายออเดอร์ใหม่</div>
                    </div>
                    <div>
                        <label class="lb">ยืนยันเบอร์โทรลงบิลห้องผิดได้กี่ครั้ง</label>
                        <asp:TextBox ID="txtMaxFail" runat="server" CssClass="dn-in" TextMode="Number" />
                        <div class="dn-hint">ครบแล้วล็อก 15 นาทีต่อโต๊ะ + แจ้ง Telegram (1–50)</div>
                    </div>
                </div>
                <div class="dn-bar">
                    <asp:Button ID="btnSaveSettings" runat="server" CssClass="dn-btn" Text="💾 บันทึกค่าตั้ง" OnClick="btnSaveSettings_Click" />
                    <asp:Button ID="btnTestTelegram" runat="server" CssClass="dn-btn sec" Text="📨 ทดสอบ Telegram" OnClick="btnTestTelegram_Click" CausesValidation="false" />
                    <asp:Literal ID="litTestResult" runat="server" />
                </div>
            </div>

            <div class="dn-card">
                <h3><i class="fas fa-link"></i> ตั้งค่าที่เกี่ยวข้อง</h3>
                <div class="sub">ส่วนที่ระบบ QR สั่งอาหารใช้ร่วมกับโมดูลอื่น — ตั้งที่หน้าของโมดูลนั้น</div>
                <asp:Literal ID="litLinks" runat="server" />
            </div>
        </section>

        <asp:HiddenField ID="hfAct" runat="server" />
        <asp:HiddenField ID="hfArg" runat="server" />
        <asp:Button ID="btnAct" runat="server" OnClick="btnAct_Click" CausesValidation="false" style="display:none" Text="go" />
        <asp:HiddenField ID="hfTab" runat="server" Value="zones" />
    </div>

    <script>
        (function () {
            // แท็บฝั่งเบราว์เซอร์: จำใน hash + hidden field (postback กลับมาแท็บเดิม)
            var tabs = document.querySelectorAll('#dnTabs a');
            var hf = document.getElementById('<%= hfTab.ClientID %>');
            function show(name) {
                for (var i = 0; i < tabs.length; i++) {
                    var t = tabs[i].getAttribute('data-tab');
                    tabs[i].className = t === name ? 'on' : '';
                    var sec = document.getElementById('tab-' + t);
                    if (sec) sec.className = 'dn-tab' + (t === name ? ' on' : '');
                }
                if (hf) hf.value = name;
                try { history.replaceState(null, '', '#t-' + name); } catch (e) { }
            }
            for (var i = 0; i < tabs.length; i++) {
                tabs[i].addEventListener('click', function (ev) { ev.preventDefault(); show(this.getAttribute('data-tab')); });
            }
            var want = (location.hash || '').replace('#t-', '');
            if (!want && hf && hf.value) want = hf.value;
            var qs = new RegExp('[?&]focus=([^&#]*)').exec(location.search);
            if (qs) want = decodeURIComponent(qs[1]) === 'settings' ? 'settings' : want;
            if (!document.getElementById('tab-' + want)) want = 'zones';
            show(want);
        })();

        function dnAct(act, arg, msg) {
            if (msg && !confirm(msg)) return;
            document.getElementById('<%= hfAct.ClientID %>').value = act;
            document.getElementById('<%= hfArg.ClientID %>').value = arg;
            document.getElementById('<%= btnAct.ClientID %>').click();
        }

        function dnCopy(id, btn) {
            var el = document.getElementById(id);
            if (!el) return;
            el.select(); el.setSelectionRange(0, 99999);
            var done = function () { if (btn) { var o = btn.textContent; btn.textContent = '✓'; setTimeout(function () { btn.textContent = o; }, 1200); } };
            if (navigator.clipboard && navigator.clipboard.writeText) navigator.clipboard.writeText(el.value).then(done, function () { try { document.execCommand('copy'); done(); } catch (e) { } });
            else { try { document.execCommand('copy'); done(); } catch (e) { } }
        }
    </script>
</asp:Content>
