<%@ Page Title="เบิกของใช้ในห้อง" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Amenities.aspx.cs" Inherits="Take_Time_BangPhra.Guest.Amenities" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .am-container { max-width: 1000px; margin: 0 auto; padding: 15px 15px 140px; }

        .page-header {
            background: linear-gradient(135deg, #00b09b 0%, #96c93d 100%);
            color: #fff; padding: 25px; border-radius: 20px; margin-bottom: 22px;
            display: flex; justify-content: space-between; align-items: center; gap: 12px;
        }
        .page-header h2 { margin: 0; font-size: 22px; font-weight: 700; }
        .page-header p { margin: 4px 0 0; font-size: 13px; opacity: .92; }
        .btn-back {
            background: rgba(255,255,255,.2); color: #fff; border: none; padding: 10px 18px;
            border-radius: 20px; text-decoration: none; font-weight: 500; white-space: nowrap;
        }
        .btn-back:hover { background: rgba(255,255,255,.32); color: #fff; text-decoration: none; }

        .am-tabs { display: flex; gap: 10px; margin-bottom: 18px; }
        .am-tab {
            flex: 1; padding: 11px; border-radius: 12px; border: 2px solid #e0e0e0; background: #fff;
            font-weight: 600; color: #666; cursor: pointer; font-size: 14px;
        }
        .am-tab.active { background: #00b09b; border-color: #00b09b; color: #fff; }

        .am-panel { display: none; }
        .am-panel.active { display: block; }

        .am-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(240px, 1fr)); gap: 14px; }
        .am-card {
            background: #fff; border-radius: 14px; overflow: hidden;
            box-shadow: 0 2px 10px rgba(0,0,0,.08); display: flex; flex-direction: column;
        }
        .am-thumb {
            height: 120px; background: linear-gradient(135deg, #e8f5f1, #d6ece3);
            background-size: cover; background-position: center;
            display: flex; align-items: center; justify-content: center; font-size: 42px;
        }
        .am-body { padding: 12px 14px 14px; flex: 1; display: flex; flex-direction: column; }
        .am-body h4 { margin: 0 0 4px; font-size: 15px; font-weight: 700; color: #2e5d3a; }
        .am-desc { font-size: 12.5px; color: #777; line-height: 1.5; flex: 1; margin-bottom: 8px; }
        .am-price { font-size: 13px; font-weight: 700; margin-bottom: 10px; }
        .am-price.free { color: #27ae60; }
        .am-price.paid { color: #e67e22; }

        .am-stepper { display: flex; align-items: center; justify-content: space-between; gap: 8px; }
        .am-btn {
            width: 34px; height: 34px; border-radius: 50%; border: none; font-size: 18px;
            font-weight: 700; cursor: pointer; background: #eef6f3; color: #00796B;
        }
        .am-btn:disabled { opacity: .35; cursor: not-allowed; }
        .am-btn.plus { background: #00b09b; color: #fff; }
        .am-qty { font-size: 16px; font-weight: 700; min-width: 26px; text-align: center; }

        .am-note { margin-top: 18px; }
        .am-note label { font-weight: 600; font-size: 14px; color: #444; display: block; margin-bottom: 6px; }
        .am-note textarea {
            width: 100%; padding: 12px; border: 2px solid #e0e0e0; border-radius: 12px;
            font-family: inherit; font-size: 14px; resize: vertical;
        }

        /* แถบสรุป — ลอยล่างจอเหมือนตะกร้าสั่งอาหาร */
        .am-bar {
            position: fixed; left: 0; right: 0; bottom: 0; z-index: 40;
            background: #fff; box-shadow: 0 -4px 18px rgba(0,0,0,.13);
            padding: 12px 16px calc(12px + env(safe-area-inset-bottom));
            display: none; align-items: center; gap: 12px;
        }
        .am-bar.show { display: flex; }
        .am-bar-info { flex: 1; font-size: 13px; color: #555; }
        .am-bar-info strong { display: block; font-size: 17px; color: #2e5d3a; }
        .am-submit {
            background: #00b09b; color: #fff; border: none; padding: 13px 26px;
            border-radius: 12px; font-weight: 700; font-size: 15px; cursor: pointer;
        }
        .am-submit:hover { background: #00897B; }

        .req-card {
            background: #fff; border-radius: 14px; padding: 15px 16px; margin-bottom: 12px;
            box-shadow: 0 2px 10px rgba(0,0,0,.07);
        }
        .req-head { display: flex; justify-content: space-between; align-items: center; gap: 10px; margin-bottom: 6px; }
        .req-num { font-weight: 700; color: #2e5d3a; font-size: 14px; }
        .req-status { font-size: 12px; font-weight: 700; padding: 4px 12px; border-radius: 20px; white-space: nowrap; }
        .st-pending { background: #fff3cd; color: #856404; }
        .st-accepted { background: #cce5ff; color: #004085; }
        .st-delivered { background: #d4edda; color: #155724; }
        .st-cancelled { background: #f8d7da; color: #721c24; }
        .req-meta { font-size: 12.5px; color: #888; }
        .req-total { font-size: 14px; font-weight: 700; color: #e67e22; margin-top: 6px; }
        .req-total.free { color: #27ae60; }

        .am-empty { text-align: center; padding: 50px 20px; color: #9aa; }
        .am-empty i { font-size: 3em; display: block; margin-bottom: 12px; opacity: .5; }

        @media (max-width: 600px) {
            .am-grid { grid-template-columns: repeat(auto-fill, minmax(150px, 1fr)); gap: 10px; }
            .am-thumb { height: 92px; font-size: 34px; }
            .page-header { flex-direction: column; align-items: flex-start; }
        }
    </style>

    <div class="am-container">
        <div class="page-header">
            <div>
                <h2><i class="fas fa-concierge-bell"></i> เบิกของใช้ในห้อง</h2>
                <p>เลือกของที่ต้องการ แล้วกดส่งคำขอ — พนักงานจะนำไปส่งที่ห้อง</p>
            </div>
            <a href="Dashboard.aspx" class="btn-back"><i class="fas fa-arrow-left"></i> กลับ</a>
        </div>

        <div class="am-tabs">
            <button type="button" class="am-tab active" onclick="amTab(this,'order')">เลือกของ</button>
            <button type="button" class="am-tab" onclick="amTab(this,'history')">ประวัติคำขอ</button>
        </div>

        <!-- ── เลือกของ ─────────────────────────────────────────────────── -->
        <div id="panelOrder" class="am-panel active">
            <asp:Panel ID="pnlNotReady" runat="server" Visible="false" CssClass="am-empty">
                <i class="fas fa-screwdriver-wrench"></i>
                <h4>ระบบเบิกของใช้ยังไม่พร้อมใช้งาน</h4>
                <p>กรุณาติดต่อพนักงานเพื่อขอความช่วยเหลือ</p>
            </asp:Panel>

            <asp:Panel ID="pnlNoItems" runat="server" Visible="false" CssClass="am-empty">
                <i class="fas fa-box-open"></i>
                <h4>ยังไม่มีรายการของใช้</h4>
                <p>กรุณาติดต่อพนักงานเพื่อขอของใช้เพิ่มเติม</p>
            </asp:Panel>

            <asp:Panel ID="pnlOrder" runat="server" Visible="false">
                <div class="am-grid">
                    <% foreach (System.Data.DataRow r in DtItems.Rows) {
                           string id = Esc(r["ID"]);
                           string img = Esc(r["Image_Path"]);
                           string icon = Esc(r["Icon"]);
                           if (string.IsNullOrEmpty(icon)) icon = "🧺";
                           bool free = FreeLeft(r) == int.MaxValue;
                           decimal unitPrice = NextUnitPrice(r);
                           int freeLeft = FreeLeft(r) == int.MaxValue ? 9999 : FreeLeft(r);
                           int maxPer = r["Max_Per_Request"] == System.DBNull.Value ? 5 : System.Convert.ToInt32(r["Max_Per_Request"]);
                    %>
                    <div class="am-card">
                        <% if (!string.IsNullOrEmpty(img)) { %>
                            <div class="am-thumb" style="background-image:url('<%= img %>')"></div>
                        <% } else { %>
                            <div class="am-thumb"><%= icon %></div>
                        <% } %>
                        <div class="am-body">
                            <h4><%= Esc(r["Name"]) %></h4>
                            <div class="am-desc"><%= Esc(r["Description"]) %></div>
                            <div class="am-price <%= free ? "free" : "paid" %>"><%= Esc(PriceLabel(r)) %></div>
                            <div class="am-stepper">
                                <button type="button" class="am-btn" onclick="amStep(<%= id %>,-1)">−</button>
                                <span class="am-qty" id="q<%= id %>">0</span>
                                <button type="button" class="am-btn plus" onclick="amStep(<%= id %>,1)">+</button>
                            </div>
                        </div>
                    </div>
                    <script>
                        amRegister(<%= id %>, '<%= Esc(r["Name"]).Replace("'", "\\'") %>',
                                   <%= unitPrice.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) %>,
                                   <%= freeLeft %>, <%= maxPer %>);
                    </script>
                    <% } %>
                </div>

                <div class="am-note">
                    <label>ข้อความถึงพนักงาน (ไม่บังคับ)</label>
                    <asp:TextBox ID="txtNote" runat="server" TextMode="MultiLine" Rows="2" MaxLength="500"
                        placeholder="เช่น ฝากไว้หน้าห้องได้เลย / ขอตอนเย็น" />
                </div>
            </asp:Panel>
        </div>

        <!-- ── ประวัติคำขอ ──────────────────────────────────────────────── -->
        <div id="panelHistory" class="am-panel">
            <asp:Repeater ID="rptRequests" runat="server">
                <ItemTemplate>
                    <div class="req-card">
                        <div class="req-head">
                            <span class="req-num"><%# Eval("Request_Number") %></span>
                            <span class="req-status <%# StatusClass(Eval("Status")) %>"><%# StatusText(Eval("Status")) %></span>
                        </div>
                        <div class="req-meta"><%# Convert.ToDateTime(Eval("Requested_Date")).ToString("dd/MM/yyyy HH:mm") %></div>
                        <%# string.IsNullOrEmpty(Eval("Note") == null ? "" : Eval("Note").ToString())
                            ? "" : "<div class='req-meta'>📝 " + Server.HtmlEncode(Eval("Note").ToString()) + "</div>" %>
                        <div class="req-total <%# Convert.ToDecimal(Eval("Total_Amount")) > 0 ? "" : "free" %>">
                            <%# Convert.ToDecimal(Eval("Total_Amount")) > 0
                                ? "รวม " + Convert.ToDecimal(Eval("Total_Amount")).ToString("N0") + " บาท (คิดรวมกับค่าห้อง)"
                                : "ไม่มีค่าใช้จ่าย" %>
                        </div>
                    </div>
                </ItemTemplate>
            </asp:Repeater>

            <asp:Panel ID="pnlNoRequests" runat="server" Visible="false" CssClass="am-empty">
                <i class="fas fa-clipboard-list"></i>
                <h4>ยังไม่มีประวัติการเบิก</h4>
                <p>เมื่อคุณส่งคำขอแล้ว รายการจะแสดงที่นี่</p>
            </asp:Panel>
        </div>
    </div>

    <!-- แถบสรุป + ปุ่มส่ง -->
    <div class="am-bar" id="amBar">
        <div class="am-bar-info">
            <strong id="amTotalText">ไม่มีค่าใช้จ่าย</strong>
            <span id="amCountText">เลือกแล้ว 0 ชิ้น</span>
        </div>
        <asp:HiddenField ID="hfCart" runat="server" />
        <asp:Button ID="btnSubmit" runat="server" CssClass="am-submit" Text="ส่งคำขอ" OnClick="btnSubmit_Click" />
    </div>

    <script>
        // ── ตะกร้าเบิกของ ────────────────────────────────────────────────────
        // ยอดที่แสดงเป็น "ประมาณการ" เพื่อให้ผู้ใช้เห็นทันที — ยอดจริงคิดใหม่ที่เซิร์ฟเวอร์ตอนกดส่ง
        var amItems = {};   // id -> {name, price, freeLeft, max}
        var amQty = {};     // id -> qty

        function amRegister(id, name, price, freeLeft, max) {
            amItems[id] = { name: name, price: price, freeLeft: freeLeft, max: max };
            amQty[id] = 0;
        }

        function amStep(id, delta) {
            var it = amItems[id];
            if (!it) return;
            var q = (amQty[id] || 0) + delta;
            if (q < 0) q = 0;
            if (q > it.max) {
                q = it.max;
                alert('รายการนี้เบิกได้ครั้งละไม่เกิน ' + it.max);
            }
            amQty[id] = q;
            var el = document.getElementById('q' + id);
            if (el) el.textContent = q;
            amRefresh();
        }

        function amRefresh() {
            var total = 0, count = 0, parts = [];
            for (var id in amQty) {
                var q = amQty[id];
                if (!q) continue;
                count += q;
                var it = amItems[id];
                // ชิ้นที่เกินสิทธิ์ฟรีเท่านั้นที่คิดเงิน
                var paid = Math.max(0, q - (it.freeLeft || 0));
                total += paid * (it.price || 0);
                parts.push(id + ':' + q);
            }
            document.getElementById('<%= hfCart.ClientID %>').value = parts.join(',');
            document.getElementById('amCountText').textContent = 'เลือกแล้ว ' + count + ' ชิ้น';
            document.getElementById('amTotalText').textContent =
                total > 0 ? ('รวมประมาณ ' + total.toLocaleString() + ' บาท') : 'ไม่มีค่าใช้จ่าย';
            document.getElementById('amBar').classList.toggle('show', count > 0);
        }

        function amTab(btn, which) {
            document.querySelectorAll('.am-tab').forEach(function (t) { t.classList.remove('active'); });
            btn.classList.add('active');
            document.getElementById('panelOrder').classList.toggle('active', which === 'order');
            document.getElementById('panelHistory').classList.toggle('active', which === 'history');
            // แถบสรุปเกี่ยวกับการเลือกของเท่านั้น
            document.getElementById('amBar').style.display =
                (which === 'order' && document.getElementById('amBar').classList.contains('show')) ? '' : 'none';
        }
    </script>
</asp:Content>
