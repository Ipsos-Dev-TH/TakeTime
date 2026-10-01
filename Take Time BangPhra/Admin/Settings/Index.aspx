<%@ Page Title="ศูนย์ตั้งค่า" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Index.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Settings.SettingsIndex" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <link rel="stylesheet" href="<%= ResolveUrl("~/Content/admin-settings.css") %>?v=<%= Take_Time_BangPhra.Admin.Settings.SettingsUi.AssetVersion %>" />
    <style>
        .sh-wrap { max-width: 1180px; margin: 0 auto; padding: 18px 12px 60px; }
        .sh-head { background: linear-gradient(135deg, #5d4037, #3e2723); color: #fff;
                   border-radius: 14px; padding: 24px 28px; margin-bottom: 18px; }
        .sh-head h2 { margin: 0 0 6px; font-weight: 700; font-size: 1.55em; }
        .sh-head p { margin: 0; opacity: .92; font-size: 14px; line-height: 1.7; }

        .sh-search { position: relative; margin-bottom: 22px; }
        .sh-search input { width: 100%; padding: 13px 16px 13px 44px; font-size: 15px;
                           border: 1.5px solid #d7ccc8; border-radius: 10px; background: #fff; }
        .sh-search input:focus { outline: none; border-color: #8d6e63; box-shadow: 0 0 0 3px rgba(141,110,99,.15); }
        .sh-search i { position: absolute; left: 16px; top: 50%; transform: translateY(-50%); color: #a1887f; }

        .sh-group { margin-bottom: 26px; }
        .sh-group > h3 { font-size: 1.02em; color: #3e2723; font-weight: 700; margin: 0 0 4px;
                         display: flex; align-items: center; gap: 9px; }
        .sh-group > h3 .ico { width: 30px; height: 30px; border-radius: 8px; display: inline-flex;
                              align-items: center; justify-content: center; color: #fff; font-size: 14px; }
        .sh-group > .note { font-size: 12.5px; color: #8d8d8d; margin: 0 0 12px 39px; }

        .sh-cards { display: grid; grid-template-columns: repeat(auto-fill, minmax(268px, 1fr)); gap: 12px; }
        .sh-card { display: block; background: #fff; border: 1px solid #ece6e3; border-radius: 11px;
                   padding: 15px 16px; text-decoration: none; color: inherit; transition: all .15s;
                   border-left: 4px solid transparent; }
        .sh-card:hover { text-decoration: none; color: inherit; box-shadow: 0 4px 14px rgba(62,39,35,.10);
                         transform: translateY(-1px); }
        .sh-card .t { font-weight: 650; font-size: 14.5px; color: #3e2723; margin-bottom: 3px;
                      display: flex; align-items: center; gap: 7px; flex-wrap: wrap; }
        .sh-card .d { font-size: 12.5px; color: #78716c; line-height: 1.5; }
        .sh-card .st { display: flex; flex-wrap: wrap; gap: 5px; margin-top: 9px; }
        .sh-card .tag { font-size: 10.5px; padding: 2px 7px; border-radius: 20px; font-weight: 600;
                        background: #eceff1; color: #607d8b; white-space: nowrap; }
        .tag-off { background: #ffebee; color: #c62828; }
        .tag-owner { background: #fff8e1; color: #f57f17; }
        .sh-empty { display: none; text-align: center; padding: 40px; color: #a1887f; }

        /* ── เช็กลิสต์เปิดใช้งาน ── */
        .sh-check { background: #fff; border: 1px solid #ece6e3; border-left: 5px solid #8d6e63; border-radius: 12px;
                    padding: 14px 18px; margin-bottom: 20px; box-shadow: 0 2px 10px rgba(62,39,35,.05); }
        .sh-check > summary { cursor: pointer; display: flex; flex-wrap: wrap; align-items: center; gap: 8px 14px;
                              list-style: none; outline: none; }
        .sh-check > summary::-webkit-details-marker { display: none; }
        .sh-check-title { font-weight: 700; font-size: 1.02em; color: #3e2723; }
        .sh-check-title i { color: #8d6e63; margin-right: 4px; }
        .sh-check-score { font-size: 13px; font-weight: 600; padding: 3px 11px; border-radius: 20px; }
        .sh-check-score.todo { background: #fff6e5; color: #8a5a00; }
        .sh-check-score.done { background: #e8f6ee; color: #16653e; }
        .sh-check-bar { flex: 1 1 140px; max-width: 260px; height: 8px; border-radius: 6px; background: #efebe9; overflow: hidden; }
        .sh-check-bar > span { display: block; height: 100%; background: linear-gradient(90deg, #8d6e63, #5d4037); }
        .sh-check-note { font-size: 12.5px; color: #8d8d8d; margin: 10px 0 6px; }
        .sh-check-list { list-style: none; margin: 0; padding: 0; }
        .sh-check-list > li { display: flex; gap: 12px; align-items: flex-start; padding: 11px 0; border-top: 1px solid #f3eeec; }
        .ck-ico { flex: none; width: 26px; height: 26px; border-radius: 50%; display: inline-flex; align-items: center;
                  justify-content: center; font-weight: 700; font-size: 13px; color: #fff; background: #b0bec5; }
        .ck-ok .ck-ico { background: #2e7d32; }
        .ck-warn .ck-ico { background: #f9a825; }
        .ck-err .ck-ico { background: #c62828; }
        .ck-info .ck-ico { background: #1d4e79; }
        .ck-body { flex: 1; min-width: 0; font-size: 14px; color: #3e2723; }
        .ck-off .ck-body b { color: #8d8d8d; }
        .ck-detail { font-size: 12.8px; color: #6d6560; line-height: 1.6; margin-top: 2px; }
        .ck-detail code { font-size: 12px; background: #faf8f7; padding: 1px 5px; border-radius: 4px; word-break: break-all; }
        .ck-sub { margin: 4px 0 0; padding-left: 18px; }
        .ck-go { flex: none; align-self: center; padding: 7px 13px; border-radius: 9px; background: #5d4037; color: #fff !important;
                 text-decoration: none !important; font-size: 13px; font-weight: 600; white-space: nowrap; }
        .ck-go:hover { background: #4e342e; }
        .ck-ok .ck-go, .ck-off .ck-go, .ck-info .ck-go { background: #fff; color: #5d4037 !important; border: 1.5px solid #d7ccc8; }

        @media (max-width: 640px) {
            .sh-cards { grid-template-columns: 1fr; }
            .sh-head { padding: 18px; }
            .sh-check-list > li { flex-wrap: wrap; }
            .ck-go { margin-left: 38px; }
        }
    </style>

    <div class="sh-wrap">
        <div class="sh-head">
            <h2><i class="fas fa-sliders"></i> ศูนย์ตั้งค่า</h2>
            <p>รวมทุกหน้าตั้งค่าไว้ที่เดียว จัดกลุ่มตามงาน — ป้ายสีบนการ์ดบอกสถานะตอนนี้ · พิมพ์ค้นหาชื่อหรือสิ่งที่อยากตั้งได้เลย</p>
        </div>

        <asp:Literal ID="litChecklist" runat="server" />

        <div class="sh-search">
            <i class="fas fa-magnifying-glass"></i>
            <input type="text" id="shSearch" placeholder="ค้นหา… เช่น นโยบาย, สัตว์เลี้ยง, โอน, PaySo, LINE, ภาษี" autocomplete="off" />
        </div>

        <asp:Literal ID="litGroups" runat="server" />

        <div class="sh-empty" id="shEmpty">
            <i class="fas fa-magnifying-glass" style="font-size:30px; display:block; margin-bottom:10px;"></i>
            ไม่พบการตั้งค่าที่ค้นหา
        </div>
    </div>

    <script>
        (function () {
            var box = document.getElementById('shSearch');
            if (!box) return;
            box.addEventListener('input', function () {
                var q = this.value.trim().toLowerCase();
                var shown = 0;
                var groups = document.querySelectorAll('.sh-group');
                for (var g = 0; g < groups.length; g++) {
                    var cards = groups[g].querySelectorAll('.sh-card');
                    var groupHit = 0;
                    for (var i = 0; i < cards.length; i++) {
                        var hit = !q || (cards[i].getAttribute('data-k') || '').indexOf(q) >= 0;
                        cards[i].style.display = hit ? 'block' : 'none';
                        if (hit) { groupHit++; shown++; }
                    }
                    groups[g].style.display = groupHit ? 'block' : 'none';
                }
                document.getElementById('shEmpty').style.display = shown ? 'none' : 'block';
            });
        })();
    </script>
</asp:Content>
