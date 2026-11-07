<%@ Page Title="QR สั่งอาหาร — เมนู" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Menu.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Dining.MenuPage" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <link rel="stylesheet" href="<%= ResolveUrl("~/Content/admin-settings.css") %>?v=<%= Take_Time_BangPhra.Admin.Settings.SettingsUi.AssetVersion %>" />
    <link rel="stylesheet" href="<%= ResolveUrl("~/Content/dining-admin.css") %>?v=<%= Take_Time_BangPhra.Admin.Settings.SettingsUi.AssetVersion %>" />
    <style>
        .mn-filter { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; margin-bottom: 12px; }
        .mn-filter .dn-in, .mn-filter .dn-sel { width: auto; min-width: 150px; padding: 7px 10px; }
        .mn-opts { margin: 4px 0 0; padding-left: 16px; font-size: 12.5px; color: #455a64; }
        .mn-opts li { margin-bottom: 2px; }
        .mn-group { border: 1px solid #eef1f3; border-radius: 10px; padding: 12px 14px; margin-bottom: 10px; }
        .mn-group h4 { margin: 0 0 4px; font-size: 15px; color: #3e2723; display: flex; flex-wrap: wrap; gap: 6px; align-items: center; }
        .mn-group h4 .dn-right { margin-left: auto; display: flex; gap: 4px; flex-wrap: wrap; }
        .mn-opt { display: flex; flex-wrap: wrap; gap: 6px; align-items: center; padding: 5px 0; border-top: 1px solid #f5f5f5; font-size: 13.5px; }
        .mn-opt .pr { color: #e65100; font-weight: 600; min-width: 60px; }
        .mn-opt .dn-actions { margin-left: auto; }
    </style>

    <div class="dn-wrap">
        <nav class="as-crumb" aria-label="เส้นทาง">
            <a href="<%= ResolveUrl("~/Admin/Settings/Index") %>"><i class="fas fa-sliders"></i> ศูนย์ตั้งค่า</a>
            <span class="sep">›</span><a href="<%= ResolveUrl("~/Admin/Dining/Zones") %>">QR สั่งอาหาร</a>
            <span class="sep">›</span><span class="here">เมนู</span>
            <a class="as-back" href="<%= ResolveUrl("~/Admin/Dining/Zones") %>">← โซน &amp; โต๊ะ</a>
        </nav>

        <div class="dn-head">
            <h2><i class="fas fa-book-open"></i> QR สั่งอาหาร — เมนู</h2>
            <p>
                เมนู QR = สินค้าในคลัง (Product) ที่ติ๊ก "อยู่ในเมนู" — ราคาใช้ราคาขายของสินค้า (รวม VAT แล้ว, แก้ที่หน้าสินค้า) · ตั้งหมวด ชื่อแสดง sold-out โซนที่ขาย และกลุ่มตัวเลือก (เผ็ด/ขนาด + ราคาเพิ่ม)
                · รูป = รูปหลักของสินค้าใน <a href="<%= ResolveUrl("~/Admin/ProductImages") %>">รูปสินค้า</a> · บันทึกแล้วลูกค้าเห็นทันที
            </p>
        </div>

        <asp:Literal ID="litMsg" runat="server" />

        <div class="dn-tabs" id="dnTabs">
            <a href="#t-items" data-tab="items" class="on"><i class="fas fa-utensils"></i> รายการ</a>
            <a href="#t-cats" data-tab="cats"><i class="fas fa-folder"></i> หมวด</a>
            <a href="#t-opts" data-tab="opts"><i class="fas fa-sliders"></i> กลุ่มตัวเลือก</a>
        </div>

        <!-- ═══════════════ รายการ ═══════════════ -->
        <section class="dn-tab on" id="tab-items">
            <asp:Panel ID="pnlItem" runat="server" CssClass="dn-card" Visible="false">
                <h3><i class="fas fa-pen"></i> <asp:Literal ID="litItemTitle" runat="server" />
                    <span class="dn-right"><a class="dn-btn sm sec" href="<%= ResolveUrl("~/Admin/ProductImages") %>" target="_blank"><i class="fas fa-image"></i> รูปสินค้า</a></span>
                </h3>
                <asp:HiddenField ID="hfItemPid" runat="server" Value="0" />
                <div class="dn-form">
                    <div class="full dn-chk" style="background:#fff3e0; border-radius:10px; padding:8px 12px;">
                        <asp:CheckBox ID="chkItemDining" runat="server" Checked="true" /> <b>อยู่ในเมนู QR (ลูกค้าเห็น)</b>
                    </div>
                    <div>
                        <label class="lb">หมวด</label>
                        <asp:DropDownList ID="ddlItemCat" runat="server" CssClass="dn-sel" />
                    </div>
                    <div>
                        <label class="lb">ลำดับในหมวด</label>
                        <asp:TextBox ID="txtItemSort" runat="server" CssClass="dn-in" TextMode="Number" Text="0" />
                    </div>
                    <div>
                        <label class="lb">ชื่อแสดง (ไทย) <span class="mini">ว่าง = ใช้ชื่อสินค้า</span></label>
                        <asp:TextBox ID="txtItemName" runat="server" CssClass="dn-in" MaxLength="200" />
                    </div>
                    <div>
                        <label class="lb">ชื่อแสดง (English)</label>
                        <asp:TextBox ID="txtItemNameEn" runat="server" CssClass="dn-in" MaxLength="200" />
                    </div>
                    <div class="full">
                        <label class="lb">คำอธิบาย</label>
                        <asp:TextBox ID="txtItemDesc" runat="server" CssClass="dn-ta" TextMode="MultiLine" MaxLength="500" />
                    </div>
                    <div>
                        <label class="lb">เวลาเตรียม (นาที)</label>
                        <asp:TextBox ID="txtItemPrep" runat="server" CssClass="dn-in" TextMode="Number" />
                    </div>
                    <div>
                        <label class="lb">แพ้อาหาร / ป้าย <span class="mini">คั่นด้วย ,</span></label>
                        <asp:TextBox ID="txtItemAllergen" runat="server" CssClass="dn-in" MaxLength="200" placeholder="เช่น ถั่ว, นม, กุ้ง, เผ็ด, มังสวิรัติ" />
                    </div>
                    <div>
                        <div class="dn-chk"><asp:CheckBox ID="chkItemSoldOut" runat="server" /> หมด (sold-out)</div>
                        <label class="lb">หมดถึงเวลา <span class="mini">ว่าง = จนกว่าจะกดคืน</span></label>
                        <asp:TextBox ID="txtItemSoldOutUntil" runat="server" CssClass="dn-in" TextMode="DateTimeLocal" />
                    </div>
                    <div>
                        <div class="dn-chk"><asp:CheckBox ID="chkItemTrack" runat="server" /> ตัดสต๊อก (เครื่องดื่ม/ของสำเร็จ)</div>
                        <div class="dn-hint">ติ๊ก = สั่งได้เมื่อมีของ (ΣProduct_In − ΣProduct_Out &gt; 0) และตัดสต๊อก+COGS ตอนจ่ายสำเร็จ · อาหารปรุงไม่ติ๊ก</div>
                        <div class="dn-chk"><asp:CheckBox ID="chkItemRecommended" runat="server" /> แนะนำ (ป้ายบนเมนู)</div>
                    </div>
                    <div class="full">
                        <label class="lb">ขายเฉพาะโซน <span class="mini">ไม่ติ๊กเลย = ทุกโซน</span></label>
                        <asp:CheckBoxList ID="cblItemZones" runat="server" RepeatLayout="Flow" CssClass="dn-cbl" />
                    </div>
                    <div class="full">
                        <label class="lb">กลุ่มตัวเลือกของรายการนี้ <span class="mini">(สร้าง/แก้ที่แท็บ "กลุ่มตัวเลือก")</span></label>
                        <asp:CheckBoxList ID="cblItemGroups" runat="server" RepeatLayout="Flow" CssClass="dn-cbl" />
                    </div>
                </div>
                <div class="dn-bar">
                    <asp:Button ID="btnSaveItem" runat="server" CssClass="dn-btn" Text="💾 บันทึกรายการ" OnClick="btnSaveItem_Click" />
                    <asp:Button ID="btnCancelItem" runat="server" CssClass="dn-btn sec" Text="ยกเลิก" OnClick="btnCancelForm_Click" CausesValidation="false" />
                </div>
            </asp:Panel>

            <div class="dn-card">
                <h3><i class="fas fa-utensils"></i> รายการเมนู <asp:Literal ID="litItemCount" runat="server" /></h3>
                <div class="sub">เลือกสินค้าจากคลังเข้าเมนู · ปุ่ม "หมด" ซ่อนปุ่มสั่งให้ลูกค้าทันที ("หมดวันนี้" = คืนอัตโนมัติพรุ่งนี้) · "เอาออก" = ลบออกจากเมนู (สินค้าในคลังไม่หาย)</div>
                <div class="mn-filter">
                    <asp:TextBox ID="txtSearch" runat="server" CssClass="dn-in" placeholder="ค้นหาชื่อ / บาร์โค้ด" />
                    <asp:DropDownList ID="ddlShow" runat="server" CssClass="dn-sel">
                        <asp:ListItem Value="IN" Text="เฉพาะที่อยู่ในเมนู" />
                        <asp:ListItem Value="ALL" Text="สินค้าทั้งหมดในคลัง" />
                        <asp:ListItem Value="OUT" Text="ยังไม่อยู่ในเมนู" />
                        <asp:ListItem Value="SOLD" Text="ที่หมดอยู่" />
                    </asp:DropDownList>
                    <asp:DropDownList ID="ddlCatFilter" runat="server" CssClass="dn-sel" />
                    <asp:Button ID="btnFilter" runat="server" CssClass="dn-btn sm" Text="ค้นหา" OnClick="btnFilter_Click" CausesValidation="false" />
                </div>
                <div class="dn-scroll"><asp:Literal ID="litItems" runat="server" /></div>
            </div>
        </section>

        <!-- ═══════════════ หมวด ═══════════════ -->
        <section class="dn-tab" id="tab-cats">
            <asp:Panel ID="pnlCat" runat="server" CssClass="dn-card" Visible="false">
                <h3><i class="fas fa-pen"></i> <asp:Literal ID="litCatTitle" runat="server" /></h3>
                <asp:HiddenField ID="hfCatId" runat="server" Value="0" />
                <div class="dn-form">
                    <div>
                        <label class="lb">ชื่อหมวด (ไทย)</label>
                        <asp:TextBox ID="txtCatName" runat="server" CssClass="dn-in" MaxLength="100" placeholder="เช่น อาหารจานเดียว" />
                    </div>
                    <div>
                        <label class="lb">ชื่อหมวด (English)</label>
                        <asp:TextBox ID="txtCatNameEn" runat="server" CssClass="dn-in" MaxLength="100" />
                    </div>
                    <div>
                        <label class="lb">ไอคอน <span class="mini">อีโมจิ เช่น 🍜 หรือ fa-mug-hot</span></label>
                        <asp:TextBox ID="txtCatIcon" runat="server" CssClass="dn-in" MaxLength="50" />
                    </div>
                    <div>
                        <label class="lb">ลำดับ</label>
                        <asp:TextBox ID="txtCatSort" runat="server" CssClass="dn-in" TextMode="Number" Text="0" />
                    </div>
                    <div class="dn-chk"><asp:CheckBox ID="chkCatActive" runat="server" Checked="true" /> ใช้งาน</div>
                </div>
                <div class="dn-bar">
                    <asp:Button ID="btnSaveCat" runat="server" CssClass="dn-btn" Text="💾 บันทึกหมวด" OnClick="btnSaveCat_Click" />
                    <asp:Button ID="btnCancelCat" runat="server" CssClass="dn-btn sec" Text="ยกเลิก" OnClick="btnCancelForm_Click" CausesValidation="false" />
                </div>
            </asp:Panel>

            <div class="dn-card">
                <h3><i class="fas fa-folder"></i> หมวดเมนู
                    <span class="dn-right"><button type="button" class="dn-btn sm" onclick="dnAct('newCat', 0, '')"><i class="fas fa-plus"></i> เพิ่มหมวด</button></span>
                </h3>
                <div class="sub">หมวดเรียงตามลำดับเป็นแถบบนหน้าเมนูลูกค้า · รายการที่ไม่มีหมวดไปอยู่ "อื่น ๆ" · ลบหมวด = รายการในหมวดกลายเป็นไม่มีหมวด</div>
                <div class="dn-scroll"><asp:Literal ID="litCats" runat="server" /></div>
            </div>
        </section>

        <!-- ═══════════════ กลุ่มตัวเลือก ═══════════════ -->
        <section class="dn-tab" id="tab-opts">
            <asp:Panel ID="pnlGroup" runat="server" CssClass="dn-card" Visible="false">
                <h3><i class="fas fa-pen"></i> <asp:Literal ID="litGroupTitle" runat="server" /></h3>
                <asp:HiddenField ID="hfGroupId" runat="server" Value="0" />
                <div class="dn-form">
                    <div>
                        <label class="lb">ชื่อกลุ่ม</label>
                        <asp:TextBox ID="txtGroupName" runat="server" CssClass="dn-in" MaxLength="100" placeholder="เช่น ระดับความเผ็ด / ขนาด / ท็อปปิ้ง" />
                    </div>
                    <div>
                        <label class="lb">เลือกขั้นต่ำ</label>
                        <asp:TextBox ID="txtGroupMin" runat="server" CssClass="dn-in" TextMode="Number" Text="0" />
                    </div>
                    <div>
                        <label class="lb">เลือกสูงสุด <span class="mini">0 = ไม่จำกัด</span></label>
                        <asp:TextBox ID="txtGroupMax" runat="server" CssClass="dn-in" TextMode="Number" Text="1" />
                    </div>
                    <div>
                        <label class="lb">ลำดับ</label>
                        <asp:TextBox ID="txtGroupSort" runat="server" CssClass="dn-in" TextMode="Number" Text="0" />
                    </div>
                    <div>
                        <div class="dn-chk"><asp:CheckBox ID="chkGroupReq" runat="server" /> บังคับเลือก</div>
                        <div class="dn-chk"><asp:CheckBox ID="chkGroupActive" runat="server" Checked="true" /> ใช้งาน</div>
                    </div>
                </div>
                <div class="dn-bar">
                    <asp:Button ID="btnSaveGroup" runat="server" CssClass="dn-btn" Text="💾 บันทึกกลุ่ม" OnClick="btnSaveGroup_Click" />
                    <asp:Button ID="btnCancelGroup" runat="server" CssClass="dn-btn sec" Text="ยกเลิก" OnClick="btnCancelForm_Click" CausesValidation="false" />
                </div>
            </asp:Panel>

            <asp:Panel ID="pnlOpt" runat="server" CssClass="dn-card" Visible="false">
                <h3><i class="fas fa-pen"></i> <asp:Literal ID="litOptTitle" runat="server" /></h3>
                <asp:HiddenField ID="hfOptId" runat="server" Value="0" />
                <asp:HiddenField ID="hfOptGroup" runat="server" Value="0" />
                <div class="dn-form">
                    <div>
                        <label class="lb">ชื่อตัวเลือก</label>
                        <asp:TextBox ID="txtOptName" runat="server" CssClass="dn-in" MaxLength="100" placeholder="เช่น เผ็ดมาก / ไซส์ L / ไข่ดาว" />
                    </div>
                    <div>
                        <label class="lb">ราคาเพิ่ม (บาท) <span class="mini">0 = ไม่เพิ่ม, ติดลบได้</span></label>
                        <asp:TextBox ID="txtOptDelta" runat="server" CssClass="dn-in" TextMode="Number" step="0.01" Text="0" />
                    </div>
                    <div>
                        <label class="lb">ลำดับ</label>
                        <asp:TextBox ID="txtOptSort" runat="server" CssClass="dn-in" TextMode="Number" Text="0" />
                    </div>
                    <div class="dn-chk"><asp:CheckBox ID="chkOptActive" runat="server" Checked="true" /> ใช้งาน</div>
                </div>
                <div class="dn-bar">
                    <asp:Button ID="btnSaveOpt" runat="server" CssClass="dn-btn" Text="💾 บันทึกตัวเลือก" OnClick="btnSaveOpt_Click" />
                    <asp:Button ID="btnCancelOpt" runat="server" CssClass="dn-btn sec" Text="ยกเลิก" OnClick="btnCancelForm_Click" CausesValidation="false" />
                </div>
            </asp:Panel>

            <div class="dn-card">
                <h3><i class="fas fa-sliders"></i> กลุ่มตัวเลือก
                    <span class="dn-right"><button type="button" class="dn-btn sm" onclick="dnAct('newGroup', 0, '')"><i class="fas fa-plus"></i> เพิ่มกลุ่ม</button></span>
                </h3>
                <div class="sub">กลุ่ม = คำถามตอนสั่ง (เผ็ดแค่ไหน / ขนาด / ท็อปปิ้ง) มีตัวเลือกพร้อมราคาเพิ่ม · ผูกกลุ่มกับรายการในฟอร์มแก้ไขรายการ · ลบกลุ่ม = ตัวเลือกและการผูกหายทั้งหมด</div>
                <asp:Literal ID="litGroups" runat="server" />
            </div>
        </section>

        <asp:HiddenField ID="hfAct" runat="server" />
        <asp:HiddenField ID="hfArg" runat="server" />
        <asp:Button ID="btnAct" runat="server" OnClick="btnAct_Click" CausesValidation="false" style="display:none" Text="go" />
        <asp:HiddenField ID="hfTab" runat="server" Value="items" />
    </div>

    <script>
        (function () {
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
            // หลัง postback hash หาย → ใช้ค่าที่เซิร์ฟเวอร์ตั้งใน hidden field; เปิดจากลิงก์ #t-… → ใช้ hash
            var want = (location.hash || '').replace('#t-', '');
            if (!want && hf && hf.value) want = hf.value;
            if (!document.getElementById('tab-' + want)) want = 'items';
            show(want);
        })();

        function dnAct(act, arg, msg) {
            if (msg && !confirm(msg)) return;
            document.getElementById('<%= hfAct.ClientID %>').value = act;
            document.getElementById('<%= hfArg.ClientID %>').value = arg;
            document.getElementById('<%= btnAct.ClientID %>').click();
        }
    </script>
</asp:Content>
