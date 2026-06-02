<%@ Page Title="Redemption Terminal" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="RedemptionTerminal.aspx.cs" Inherits="Take_Time_BangPhra.Admin.CRM.RedemptionTerminal" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .rt-container { max-width: 1100px; margin: 20px auto; padding: 20px; }
        .rt-header { background: linear-gradient(135deg,#11998e 0%,#38ef7d 100%); color: white; padding: 26px; border-radius: 14px; margin-bottom: 24px; }
        .rt-header h1 { margin: 0 0 6px; font-size: 26px; font-weight: 800; }
        .rt-header p { margin: 0; opacity: .92; }

        .rt-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 22px; }
        @media (max-width: 900px) { .rt-grid { grid-template-columns: 1fr; } }

        .rt-card { background: white; border-radius: 14px; padding: 22px; box-shadow: 0 2px 10px rgba(0,0,0,.08); }
        .rt-card h3 { margin: 0 0 16px; font-size: 17px; color: #333; display: flex; align-items: center; gap: 8px; }
        .step-num { width: 26px; height: 26px; border-radius: 50%; background: #11998e; color: white; display: inline-flex; align-items: center; justify-content: center; font-size: 14px; }

        /* Reward choices */
        .reward-choices { display: flex; flex-direction: column; gap: 10px; max-height: 360px; overflow-y: auto; }
        .reward-choice {
            display: flex; align-items: center; justify-content: space-between; gap: 12px; padding: 14px 16px;
            border: 2px solid #eef0f4; border-radius: 12px; cursor: pointer; transition: all .2s ease;
        }
        .reward-choice:hover { border-color: #b9f0d8; background: #f6fffb; }
        .reward-choice.selected { border-color: #11998e; background: #eafff6; box-shadow: 0 2px 10px rgba(17,153,142,.2); }
        .reward-choice .rc-name { font-weight: 700; color: #333; font-size: 15px; }
        .reward-choice .rc-desc { font-size: 12px; color: #888; }
        .reward-choice .rc-cost { font-size: 17px; font-weight: 800; color: #11998e; white-space: nowrap; }
        .reward-choice .rc-cost small { font-size: 11px; color: #999; font-weight: 500; }

        /* Scanner */
        #reader { width: 100%; border-radius: 12px; overflow: hidden; }
        .scan-controls { display: flex; gap: 10px; margin-bottom: 14px; }
        .btn { border: none; border-radius: 10px; padding: 12px 18px; font-weight: 700; cursor: pointer; font-size: 14px; }
        .btn-scan { background: linear-gradient(135deg,#667eea,#764ba2); color: white; flex: 1; }
        .btn-stop { background: #eef0f4; color: #555; }
        .scan-hint { font-size: 12px; color: #999; text-align: center; margin-top: 8px; }

        .divider { text-align: center; color: #aaa; font-size: 13px; margin: 16px 0; position: relative; }
        .divider::before, .divider::after { content: ''; position: absolute; top: 50%; width: 38%; height: 1px; background: #eee; }
        .divider::before { left: 0; } .divider::after { right: 0; }

        .manual-row { display: flex; gap: 8px; }
        .form-control { flex: 1; padding: 12px; border: 1px solid #ddd; border-radius: 10px; font-size: 14px; }
        .btn-find { background: #667eea; color: white; }

        /* Member result */
        .member-result { margin-top: 18px; border-radius: 14px; padding: 20px; background: linear-gradient(135deg,#f8f9ff,#eef1f6); display: none; }
        .member-result.show { display: block; }
        .member-result.error { background: #fff5f5; }
        .mr-top { display: flex; align-items: center; gap: 14px; margin-bottom: 14px; }
        .mr-avatar { width: 52px; height: 52px; border-radius: 50%; background: linear-gradient(135deg,#11998e,#38ef7d); color: white; display: flex; align-items: center; justify-content: center; font-size: 22px; }
        .mr-name { font-size: 18px; font-weight: 800; color: #333; }
        .mr-sub { font-size: 13px; color: #888; }
        .mr-points { display: flex; gap: 14px; margin-bottom: 16px; }
        .mr-box { flex: 1; background: white; border-radius: 10px; padding: 12px; text-align: center; }
        .mr-box .l { font-size: 11px; color: #888; } .mr-box .v { font-size: 20px; font-weight: 800; }
        .mr-box .v.green { color: #11998e; } .mr-box .v.blue { color: #667eea; }
        .btn-confirm { width: 100%; border: none; padding: 15px; border-radius: 12px; font-size: 16px; font-weight: 800; color: white; cursor: pointer; background: linear-gradient(135deg,#11998e,#38ef7d); }
        .btn-confirm:disabled { background: #e0e0e0; color: #999; cursor: not-allowed; }

        /* Success */
        .redeem-success { margin-top: 18px; display: none; text-align: center; background: #e8f5e9; border-radius: 14px; padding: 26px; }
        .redeem-success.show { display: block; }
        .redeem-success i { font-size: 50px; color: #2e7d32; margin-bottom: 12px; }
        .redeem-success h3 { margin: 0 0 8px; color: #2e7d32; }
        .voucher-code { font-family: monospace; font-size: 26px; font-weight: 800; letter-spacing: 2px; color: #1b5e20; background: white; display: inline-block; padding: 10px 22px; border-radius: 10px; margin: 10px 0; border: 2px dashed #66bb6a; }

        .toast { position: fixed; top: 20px; right: 20px; padding: 14px 20px; border-radius: 10px; color: white; font-weight: 600; z-index: 2000; display: none; }
        .toast.err { background: #e74c3c; } .toast.ok { background: #27ae60; }
    </style>

    <div class="rt-container">
        <div class="rt-header">
            <h1><i class="fas fa-qrcode"></i> เคาน์เตอร์แลกของรางวัล</h1>
            <p>เลือกรางวัล → สแกน QR ของลูกค้า (หรือค้นหาด้วยเบอร์โทร) → ยืนยันการแลก</p>
        </div>

        <div class="rt-grid">
            <!-- Step 1: choose reward -->
            <div class="rt-card">
                <h3><span class="step-num">1</span> เลือกของรางวัล</h3>
                <div class="reward-choices">
                    <asp:Repeater ID="rptRewards" runat="server">
                        <ItemTemplate>
                            <div class="reward-choice" data-id='<%# Eval("ID") %>' data-cost='<%# Eval("PointsCost") %>'
                                 data-mintier='<%# Eval("MinTierRequired") %>' onclick="selectReward(this)">
                                <div>
                                    <div class="rc-name"><%# Eval("RewardName") %></div>
                                    <div class="rc-desc"><%# Eval("Description") %></div>
                                </div>
                                <div class="rc-cost"><%# Convert.ToInt32(Eval("PointsCost")).ToString("N0") %> <small>แต้ม</small></div>
                            </div>
                        </ItemTemplate>
                    </asp:Repeater>
                    <asp:Label ID="lblNoRewards" runat="server" Visible="false"
                        Text="<div style='text-align:center;padding:30px;color:#999;'>ยังไม่มีของรางวัล</div>"></asp:Label>
                </div>
            </div>

            <!-- Step 2: identify customer -->
            <div class="rt-card">
                <h3><span class="step-num">2</span> ระบุลูกค้า</h3>

                <div class="scan-controls">
                    <button type="button" class="btn btn-scan" id="btnStartScan" onclick="startScan()"><i class="fas fa-camera"></i> เปิดกล้องสแกน QR</button>
                    <button type="button" class="btn btn-stop" id="btnStopScan" onclick="stopScan()" style="display:none;"><i class="fas fa-stop"></i> ปิด</button>
                </div>
                <div id="reader"></div>
                <div class="scan-hint">ให้ลูกค้าเปิดหน้า "คะแนน & รางวัล" แล้วกด "ใช้คะแนนที่เคาน์เตอร์"</div>

                <div class="divider">หรือ</div>

                <div class="manual-row">
                    <input type="tel" class="form-control" id="txtManualPhone" placeholder="ค้นหาด้วยเบอร์โทรลูกค้า" />
                    <button type="button" class="btn btn-find" onclick="findByPhone()"><i class="fas fa-search"></i> ค้นหา</button>
                </div>

                <!-- Member result -->
                <div class="member-result" id="memberResult">
                    <div class="mr-top">
                        <div class="mr-avatar"><i class="fas fa-user"></i></div>
                        <div>
                            <div class="mr-name" id="mrName">-</div>
                            <div class="mr-sub" id="mrSub">-</div>
                        </div>
                    </div>
                    <div class="mr-points">
                        <div class="mr-box"><div class="l">คะแนนคงเหลือ</div><div class="v green" id="mrPoints">0</div></div>
                        <div class="mr-box"><div class="l">รางวัลที่เลือก</div><div class="v blue" id="mrRewardCost">-</div></div>
                    </div>
                    <button type="button" class="btn-confirm" id="btnConfirm" onclick="confirmRedeem()" disabled>ยืนยันการแลกรางวัล</button>
                </div>

                <!-- Success -->
                <div class="redeem-success" id="redeemSuccess">
                    <i class="fas fa-check-circle"></i>
                    <h3>แลกรางวัลสำเร็จ!</h3>
                    <div id="successReward" style="color:#555;"></div>
                    <div class="voucher-code" id="successCode">-</div>
                    <div style="color:#777;font-size:13px;">คะแนนคงเหลือ: <span id="successBalance">0</span> แต้ม</div>
                    <button type="button" class="btn btn-scan" style="margin-top:16px;max-width:200px;" onclick="resetTerminal()">แลกรายการถัดไป</button>
                </div>
            </div>
        </div>
    </div>

    <div class="toast" id="toast"></div>

    <script src="https://unpkg.com/html5-qrcode@2.3.8/html5-qrcode.min.js"></script>
    <script>
        var selectedRewardId = 0, selectedRewardCost = 0, selectedRewardMinTier = 1, selectedRewardName = '';
        var currentMode = '', currentToken = '', currentPhone = '', memberPoints = 0, memberTierId = 1;
        var html5Qr = null;

        function showToast(msg, ok) {
            var t = document.getElementById('toast');
            t.textContent = msg; t.className = 'toast ' + (ok ? 'ok' : 'err'); t.style.display = 'block';
            setTimeout(function () { t.style.display = 'none'; }, 3500);
        }

        function selectReward(el) {
            document.querySelectorAll('.reward-choice').forEach(function (c) { c.classList.remove('selected'); });
            el.classList.add('selected');
            selectedRewardId = parseInt(el.getAttribute('data-id'));
            selectedRewardCost = parseInt(el.getAttribute('data-cost'));
            selectedRewardMinTier = parseInt(el.getAttribute('data-mintier')) || 1;
            selectedRewardName = el.querySelector('.rc-name').textContent;
            var c = document.getElementById('mrRewardCost');
            if (c) c.textContent = selectedRewardCost.toLocaleString() + ' แต้ม';
            refreshConfirmState();
        }

        function startScan() {
            if (typeof Html5Qrcode === 'undefined') { showToast('โหลดตัวสแกนไม่สำเร็จ', false); return; }
            document.getElementById('btnStartScan').style.display = 'none';
            document.getElementById('btnStopScan').style.display = 'inline-block';
            html5Qr = new Html5Qrcode("reader");
            html5Qr.start({ facingMode: "environment" }, { fps: 10, qrbox: 240 },
                function (decoded) { stopScan(); resolveToken(decoded.trim()); },
                function () {}
            ).catch(function (err) {
                showToast('เปิดกล้องไม่ได้: ' + err, false);
                stopScan();
            });
        }

        function stopScan() {
            document.getElementById('btnStartScan').style.display = 'inline-block';
            document.getElementById('btnStopScan').style.display = 'none';
            if (html5Qr) {
                html5Qr.stop().then(function () { html5Qr.clear(); html5Qr = null; }).catch(function () { html5Qr = null; });
            }
        }

        function ajax(method, data, cb) {
            $.ajax({
                type: 'POST', url: 'RedemptionTerminal.aspx/' + method,
                data: JSON.stringify(data), contentType: 'application/json; charset=utf-8', dataType: 'json',
                success: function (r) { cb(r.d); },
                error: function () { showToast('การเชื่อมต่อผิดพลาด', false); }
            });
        }

        function resolveToken(token) {
            ajax('ResolveToken', { token: token }, function (d) {
                if (!d.valid) { showMemberError(d.message); return; }
                currentMode = 'token'; currentToken = token; currentPhone = d.phone;
                showMember(d);
            });
        }

        function findByPhone() {
            var phone = document.getElementById('txtManualPhone').value.trim();
            if (!phone) { showToast('กรุณากรอกเบอร์โทร', false); return; }
            ajax('ResolveMember', { phone: phone }, function (d) {
                if (!d.valid) { showMemberError(d.message); return; }
                currentMode = 'phone'; currentToken = ''; currentPhone = d.phone;
                showMember(d);
            });
        }

        function showMember(d) {
            memberPoints = d.points; memberTierId = d.tierId;
            document.getElementById('mrName').textContent = d.name;
            document.getElementById('mrSub').textContent = d.phone + ' · ' + d.tier;
            document.getElementById('mrPoints').textContent = d.points.toLocaleString();
            document.getElementById('memberResult').className = 'member-result show';
            document.getElementById('redeemSuccess').className = 'redeem-success';
            refreshConfirmState();
        }

        function showMemberError(msg) {
            document.getElementById('mrName').textContent = 'ไม่สำเร็จ';
            document.getElementById('mrSub').textContent = msg;
            document.getElementById('mrPoints').textContent = '-';
            document.getElementById('memberResult').className = 'member-result show error';
            document.getElementById('btnConfirm').disabled = true;
        }

        function refreshConfirmState() {
            var btn = document.getElementById('btnConfirm');
            if (!btn) return;
            var ready = selectedRewardId > 0 && currentPhone &&
                        memberPoints >= selectedRewardCost && memberTierId >= selectedRewardMinTier;
            btn.disabled = !ready;
            if (selectedRewardId > 0 && currentPhone) {
                if (memberTierId < selectedRewardMinTier) btn.textContent = 'ระดับสมาชิกไม่ถึงเกณฑ์';
                else if (memberPoints < selectedRewardCost) btn.textContent = 'คะแนนไม่เพียงพอ';
                else btn.textContent = 'ยืนยันการแลก ' + selectedRewardName;
            } else {
                btn.textContent = 'ยืนยันการแลกรางวัล';
            }
        }

        function confirmRedeem() {
            if (selectedRewardId <= 0) { showToast('กรุณาเลือกของรางวัล', false); return; }
            var btn = document.getElementById('btnConfirm');
            btn.disabled = true; btn.textContent = 'กำลังดำเนินการ...';

            var method = currentMode === 'token' ? 'RedeemByToken' : 'RedeemByMember';
            var payload = currentMode === 'token'
                ? { token: currentToken, rewardId: selectedRewardId }
                : { phone: currentPhone, rewardId: selectedRewardId };

            ajax(method, payload, function (d) {
                if (d.success) {
                    document.getElementById('memberResult').className = 'member-result';
                    document.getElementById('successReward').textContent = d.rewardName || selectedRewardName;
                    document.getElementById('successCode').textContent = d.code || '-';
                    document.getElementById('successBalance').textContent = (d.newBalance || 0).toLocaleString();
                    document.getElementById('redeemSuccess').className = 'redeem-success show';
                    showToast('แลกรางวัลสำเร็จ', true);
                } else {
                    showToast(d.message || 'แลกไม่สำเร็จ', false);
                    refreshConfirmState();
                }
            });
        }

        function resetTerminal() {
            currentMode = ''; currentToken = ''; currentPhone = ''; memberPoints = 0; memberTierId = 1;
            document.getElementById('redeemSuccess').className = 'redeem-success';
            document.getElementById('memberResult').className = 'member-result';
            document.getElementById('txtManualPhone').value = '';
        }
    </script>
</asp:Content>
