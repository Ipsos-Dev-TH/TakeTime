<%@ Page Title="ศูนย์ตั้งค่า" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Index.aspx.cs" Inherits="Take_Time_BangPhra.Admin.Settings.SettingsIndex" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .sh-wrap { max-width: 1180px; margin: 0 auto; padding: 18px 12px 60px; }
        .sh-head { background: linear-gradient(135deg, #37474f, #546e7a); color: #fff;
                   border-radius: 14px; padding: 24px 28px; margin-bottom: 18px; }
        .sh-head h2 { margin: 0 0 6px; font-weight: 700; font-size: 1.55em; }
        .sh-head p { margin: 0; opacity: .92; font-size: 14px; }

        .sh-search { position: relative; margin-bottom: 22px; }
        .sh-search input { width: 100%; padding: 13px 16px 13px 44px; font-size: 15px;
                           border: 1.5px solid #dbe2e7; border-radius: 10px; background: #fff; }
        .sh-search input:focus { outline: none; border-color: #546e7a; box-shadow: 0 0 0 3px rgba(84,110,122,.12); }
        .sh-search i { position: absolute; left: 16px; top: 50%; transform: translateY(-50%); color: #90a4ae; }

        .sh-group { margin-bottom: 26px; }
        .sh-group > h3 { font-size: 1.02em; color: #37474f; font-weight: 700; margin: 0 0 4px;
                         display: flex; align-items: center; gap: 9px; }
        .sh-group > h3 .ico { width: 30px; height: 30px; border-radius: 8px; display: inline-flex;
                              align-items: center; justify-content: center; color: #fff; font-size: 14px; }
        .sh-group > .note { font-size: 12.5px; color: #90a4ae; margin: 0 0 12px 39px; }

        .sh-cards { display: grid; grid-template-columns: repeat(auto-fill, minmax(268px, 1fr)); gap: 12px; }
        .sh-card { display: block; background: #fff; border: 1px solid #e8edf1; border-radius: 11px;
                   padding: 15px 16px; text-decoration: none; color: inherit; transition: all .15s;
                   border-left: 4px solid transparent; }
        .sh-card:hover { text-decoration: none; color: inherit; box-shadow: 0 4px 14px rgba(0,0,0,.09);
                         transform: translateY(-1px); }
        .sh-card .t { font-weight: 650; font-size: 14.5px; color: #263238; margin-bottom: 3px;
                      display: flex; align-items: center; gap: 7px; }
        .sh-card .d { font-size: 12.5px; color: #78909c; line-height: 1.5; }
        .sh-card .tag { font-size: 10.5px; padding: 2px 7px; border-radius: 20px; font-weight: 600;
                        background: #eceff1; color: #607d8b; white-space: nowrap; }
        .tag-off { background: #ffebee; color: #c62828; }
        .tag-owner { background: #fff8e1; color: #f57f17; }
        .sh-empty { display: none; text-align: center; padding: 40px; color: #90a4ae; }
        @media (max-width: 640px) { .sh-cards { grid-template-columns: 1fr; } }
    </style>

    <div class="sh-wrap">
        <div class="sh-head">
            <h2><i class="fas fa-sliders"></i> ศูนย์ตั้งค่า</h2>
            <p>รวมทุกหน้าตั้งค่าไว้ที่เดียว จัดกลุ่มตามงาน — พิมพ์ค้นหาชื่อหรือสิ่งที่อยากตั้งได้เลย</p>
        </div>

        <div class="sh-search">
            <i class="fas fa-magnifying-glass"></i>
            <input type="text" id="shSearch" placeholder="ค้นหา… เช่น LINE, ภาษี, อีเมล, ราคา, พนักงาน, สต๊อก" autocomplete="off" />
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
