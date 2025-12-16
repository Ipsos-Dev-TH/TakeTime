<%@ Page Title="Review Management" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="ReviewManagement.aspx.cs" Inherits="Take_Time_BangPhra.Admin.CRM.ReviewManagement" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/4.7.0/css/font-awesome.min.css">
    <style>
        .review-container { max-width: 1600px; margin: 20px auto; padding: 20px; }
        .page-header { background: linear-gradient(135deg, #3498db 0%, #9b59b6 100%); color: white; padding: 30px; border-radius: 12px; margin-bottom: 30px; box-shadow: 0 4px 6px rgba(0,0,0,0.1); }
        .page-header h1 { margin: 0 0 10px 0; font-size: 32px; font-weight: bold; }
        .stats-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 20px; margin-bottom: 30px; }
        .stat-card { background: white; padding: 20px; border-radius: 12px; box-shadow: 0 2px 8px rgba(0,0,0,0.1); text-align: center; }
        .stat-value { font-size: 36px; font-weight: bold; color: #3498db; }
        .star-rating { color: #f39c12; font-size: 24px; }
        .review-card { background: white; border-radius: 12px; padding: 25px; margin-bottom: 20px; box-shadow: 0 2px 8px rgba(0,0,0,0.1); }
        .review-header { display: flex; justify-content: space-between; margin-bottom: 15px; padding-bottom: 15px; border-bottom: 2px solid #f0f0f0; }
        .review-status { padding: 6px 12px; border-radius: 20px; font-size: 12px; font-weight: 600; }
        .status-pending { background: #fff3cd; color: #856404; }
        .status-approved { background: #d4edda; color: #155724; }
        .status-rejected { background: #f8d7da; color: #721c24; }
        .review-ratings { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: 15px; margin: 15px 0; }
        .rating-item { text-align: center; padding: 10px; background: #f8f9fa; border-radius: 8px; }
        .btn-approve { background: #28a745; color: white; padding: 8px 20px; border: none; border-radius: 6px; cursor: pointer; margin-right: 10px; }
        .btn-reject { background: #dc3545; color: white; padding: 8px 20px; border: none; border-radius: 6px; cursor: pointer; margin-right: 10px; }
        .btn-respond { background: #3498db; color: white; padding: 8px 20px; border: none; border-radius: 6px; cursor: pointer; }
        .filter-bar { background: white; padding: 20px; border-radius: 12px; margin-bottom: 20px; box-shadow: 0 2px 8px rgba(0,0,0,0.1); display: flex; gap: 15px; align-items: center; }
        .response-box { background: #e8f4f8; padding: 15px; border-left: 4px solid #3498db; border-radius: 4px; margin-top: 15px; }
        .alert { padding: 15px 20px; border-radius: 8px; margin-bottom: 20px; }
        .alert-success { background: #d4edda; border: 1px solid #c3e6cb; color: #155724; }
        .alert-error { background: #f8d7da; border: 1px solid #f5c6cb; color: #721c24; }

        /* Tab Navigation */
        .tab-nav { display: flex; border-bottom: 2px solid #e0e0e0; margin-bottom: 20px; }
        .tab-btn { padding: 12px 24px; border: none; background: none; cursor: pointer; font-size: 16px; font-weight: 600; color: #666; border-bottom: 3px solid transparent; transition: all 0.3s; }
        .tab-btn:hover { color: #3498db; }
        .tab-btn.active { color: #3498db; border-bottom-color: #3498db; }
        .tab-content { display: none; }
        .tab-content.active { display: block; }

        /* Checkout History Styles */
        .checkout-card { background: white; border-radius: 12px; padding: 20px; margin-bottom: 15px; box-shadow: 0 2px 8px rgba(0,0,0,0.1); border-left: 4px solid #9b59b6; }
        .checkout-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 15px; }
        .checkout-rating { display: flex; align-items: center; gap: 10px; }
        .checkout-rating .big-rating { font-size: 42px; font-weight: bold; color: #f39c12; }
        .checkout-meta { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 15px; margin: 15px 0; padding: 15px; background: #f8f9fa; border-radius: 8px; }
        .checkout-meta-item { display: flex; flex-direction: column; }
        .checkout-meta-label { font-size: 12px; color: #666; }
        .checkout-meta-value { font-weight: 600; color: #2c3e50; }
        .checkout-badge { padding: 4px 10px; border-radius: 15px; font-size: 11px; font-weight: 600; }
        .badge-damage { background: #f8d7da; color: #721c24; }
        .badge-missing { background: #fff3cd; color: #856404; }
        .badge-key-ok { background: #d4edda; color: #155724; }
        .badge-key-missing { background: #f8d7da; color: #721c24; }
        .badge-clean { background: #d4edda; color: #155724; }
        .badge-dirty { background: #fff3cd; color: #856404; }
    </style>

    <div class="review-container">
        <div class="page-header">
            <h1><i class="fa fa-star"></i> Review Management</h1>
            <p>จัดการรีวิวและคำติชม - Approve, respond to, and analyze guest reviews</p>
        </div>

        <asp:Panel ID="pnlAlert" runat="server" Visible="false">
            <div class="alert" id="alertBox" runat="server">
                <asp:Label ID="lblAlertMessage" runat="server"></asp:Label>
            </div>
        </asp:Panel>

        <div class="stats-grid">
            <div class="stat-card">
                <h4>Guest Reviews</h4>
                <div class="stat-value"><asp:Label ID="lblTotalReviews" runat="server">0</asp:Label></div>
            </div>
            <div class="stat-card">
                <h4>Average Rating</h4>
                <div class="star-rating"><asp:Label ID="lblAvgRating" runat="server">0.0</asp:Label> <i class="fa fa-star"></i></div>
            </div>
            <div class="stat-card">
                <h4>Pending Reviews</h4>
                <div class="stat-value"><asp:Label ID="lblPending" runat="server">0</asp:Label></div>
            </div>
            <div class="stat-card" style="border-left: 4px solid #9b59b6;">
                <h4>Checkout Reviews</h4>
                <div class="stat-value" style="color: #9b59b6;"><asp:Label ID="lblCheckoutReviews" runat="server">0</asp:Label></div>
            </div>
            <div class="stat-card">
                <h4>Checkout Avg</h4>
                <div class="star-rating"><asp:Label ID="lblCheckoutAvg" runat="server">0.0</asp:Label> <i class="fa fa-star"></i></div>
            </div>
        </div>

        <!-- Hidden field to preserve tab state across postbacks -->
        <asp:HiddenField ID="hdnActiveTab" runat="server" Value="guest-reviews" />

        <!-- Tab Navigation -->
        <div class="tab-nav">
            <button type="button" class="tab-btn active" id="btn-guest-reviews" onclick="showTab('guest-reviews')">
                <i class="fa fa-comments"></i> Guest Reviews
            </button>
            <button type="button" class="tab-btn" id="btn-checkout-reviews" onclick="showTab('checkout-reviews')">
                <i class="fa fa-sign-out"></i> Checkout Reviews
            </button>
        </div>

        <!-- Guest Reviews Tab -->
        <div id="guest-reviews" class="tab-content active">
        <div class="filter-bar">
            <label style="font-weight: 600;">Filter:</label>
            <asp:DropDownList ID="ddlStatusFilter" runat="server" AutoPostBack="true" OnSelectedIndexChanged="ddlStatusFilter_SelectedIndexChanged" style="padding: 8px; border-radius: 6px; border: 1px solid #ddd;">
                <asp:ListItem Value="">All Status</asp:ListItem>
                <asp:ListItem Value="PENDING">Pending</asp:ListItem>
                <asp:ListItem Value="APPROVED">Approved</asp:ListItem>
                <asp:ListItem Value="REJECTED">Rejected</asp:ListItem>
            </asp:DropDownList>
            <asp:DropDownList ID="ddlRatingFilter" runat="server" AutoPostBack="true" OnSelectedIndexChanged="ddlRatingFilter_SelectedIndexChanged" style="padding: 8px; border-radius: 6px; border: 1px solid #ddd;">
                <asp:ListItem Value="0">All Ratings</asp:ListItem>
                <asp:ListItem Value="5">5 Stars</asp:ListItem>
                <asp:ListItem Value="4">4 Stars</asp:ListItem>
                <asp:ListItem Value="3">3 Stars</asp:ListItem>
                <asp:ListItem Value="2">2 Stars</asp:ListItem>
                <asp:ListItem Value="1">1 Star</asp:ListItem>
            </asp:DropDownList>
            <asp:TextBox ID="txtSearchCustomer" runat="server" placeholder="Search by customer..." style="padding: 8px; border-radius: 6px; border: 1px solid #ddd; flex: 1;"></asp:TextBox>
            <asp:Button ID="btnSearch" runat="server" Text="Search" OnClick="btnSearch_Click" style="padding: 8px 20px; background: #3498db; color: white; border: none; border-radius: 6px; cursor: pointer;" />
        </div>

        <asp:Repeater ID="rptReviews" runat="server" OnItemCommand="rptReviews_ItemCommand">
            <ItemTemplate>
                <div class="review-card">
                    <div class="review-header">
                        <div>
                            <h3 style="margin: 0 0 5px 0;">Booking #<%# Eval("Reservation_ID") %></h3>
                            <div style="color: #666;">
                                <i class="fa fa-user"></i> <%# Eval("Customer_Name") %> (<%# Eval("Customer_MobilePhone") %>)
                                | <i class="fa fa-calendar"></i> <%# Eval("Review_Date", "{0:dd MMM yyyy}") %>
                            </div>
                        </div>
                        <div>
                            <span class="review-status status-<%# Eval("Status").ToString().ToLower() %>">
                                <%# Eval("Status") %>
                            </span>
                        </div>
                    </div>

                    <div class="review-ratings">
                        <div class="rating-item">
                            <div style="font-size: 28px; font-weight: bold; color: #3498db;"><%# Eval("Overall_Rating", "{0:N1}") %></div>
                            <div style="font-size: 12px; color: #666;">Overall</div>
                        </div>
                        <div class="rating-item">
                            <div><%# GetStars(Eval("Accommodation_Rating")) %></div>
                            <div style="font-size: 12px; color: #666;">Accommodation</div>
                        </div>
                        <div class="rating-item">
                            <div><%# GetStars(Eval("Cleanliness_Rating")) %></div>
                            <div style="font-size: 12px; color: #666;">Cleanliness</div>
                        </div>
                        <div class="rating-item">
                            <div><%# GetStars(Eval("Staff_Rating")) %></div>
                            <div style="font-size: 12px; color: #666;">Staff</div>
                        </div>
                        <div class="rating-item">
                            <div><%# GetStars(Eval("Value_Rating")) %></div>
                            <div style="font-size: 12px; color: #666;">Value</div>
                        </div>
                        <div class="rating-item">
                            <div><%# GetStars(Eval("Location_Rating")) %></div>
                            <div style="font-size: 12px; color: #666;">Location</div>
                        </div>
                    </div>

                    <div style="margin: 15px 0; padding: 15px; background: #f8f9fa; border-radius: 8px;">
                        <strong>Review:</strong><br />
                        <%# Eval("Review_Text") %>
                    </div>

                    <%# Eval("Management_Response") != null && !string.IsNullOrEmpty(Eval("Management_Response").ToString()) ?
                        "<div class='response-box'><strong><i class='fa fa-reply'></i> Management Response:</strong><br />" +
                        Eval("Management_Response") +
                        "<div style='font-size: 12px; color: #666; margin-top: 8px;'>Responded: " +
                        (Eval("Response_Date") != DBNull.Value ? Convert.ToDateTime(Eval("Response_Date")).ToString("dd MMM yyyy") : "") +
                        "</div></div>" : "" %>

                    <div style="margin-top: 15px; padding-top: 15px; border-top: 1px solid #e0e0e0;">
                        <%# Eval("Status").ToString() == "PENDING" ?
                            "<asp:LinkButton runat='server' CommandName='Approve' CommandArgument='" + Eval("ID") + "' CssClass='btn-approve'><i class='fa fa-check'></i> Approve</asp:LinkButton>" +
                            "<asp:LinkButton runat='server' CommandName='Reject' CommandArgument='" + Eval("ID") + "' CssClass='btn-reject'><i class='fa fa-times'></i> Reject</asp:LinkButton>" : "" %>
                        <%# (Eval("Management_Response") == null || string.IsNullOrEmpty(Eval("Management_Response").ToString())) && Eval("Status").ToString() == "APPROVED" ?
                            "<asp:LinkButton runat='server' CommandName='Respond' CommandArgument='" + Eval("ID") + "' CssClass='btn-respond'><i class='fa fa-reply'></i> Add Response</asp:LinkButton>" : "" %>
                    </div>
                </div>
            </ItemTemplate>
        </asp:Repeater>

        <asp:Label ID="lblNoReviews" runat="server" Visible="false" style="text-align: center; padding: 60px; color: #999;">
            <i class="fa fa-star-o" style="font-size: 64px; opacity: 0.5;"></i><br />
            No reviews found
        </asp:Label>
        </div> <!-- End Guest Reviews Tab -->

        <!-- Checkout Reviews Tab -->
        <div id="checkout-reviews" class="tab-content">
            <div class="filter-bar">
                <label style="font-weight: 600;">Filter:</label>
                <asp:DropDownList ID="ddlCheckoutRatingFilter" runat="server" AutoPostBack="true" OnSelectedIndexChanged="ddlCheckoutRatingFilter_SelectedIndexChanged" style="padding: 8px; border-radius: 6px; border: 1px solid #ddd;">
                    <asp:ListItem Value="0">All Ratings</asp:ListItem>
                    <asp:ListItem Value="5">5 Stars</asp:ListItem>
                    <asp:ListItem Value="4">4 Stars</asp:ListItem>
                    <asp:ListItem Value="3">3 Stars</asp:ListItem>
                    <asp:ListItem Value="2">2 Stars</asp:ListItem>
                    <asp:ListItem Value="1">1 Star</asp:ListItem>
                </asp:DropDownList>
                <asp:TextBox ID="txtCheckoutStartDate" runat="server" TextMode="Date" style="padding: 8px; border-radius: 6px; border: 1px solid #ddd;" placeholder="Start Date"></asp:TextBox>
                <asp:TextBox ID="txtCheckoutEndDate" runat="server" TextMode="Date" style="padding: 8px; border-radius: 6px; border: 1px solid #ddd;" placeholder="End Date"></asp:TextBox>
                <label style="display: flex; align-items: center; gap: 5px; cursor: pointer;">
                    <asp:CheckBox ID="chkHasNotes" runat="server" AutoPostBack="true" OnCheckedChanged="chkHasNotes_CheckedChanged" />
                    <span>มี Notes</span>
                </label>
                <asp:Button ID="btnFilterCheckout" runat="server" Text="Filter" OnClick="btnFilterCheckout_Click" style="padding: 8px 20px; background: #9b59b6; color: white; border: none; border-radius: 6px; cursor: pointer;" />
            </div>

            <asp:Repeater ID="rptCheckoutReviews" runat="server">
                <ItemTemplate>
                    <div class="checkout-card">
                        <div class="checkout-header">
                            <div>
                                <h3 style="margin: 0 0 5px 0;">
                                    <i class="fa fa-bed"></i> Booking #<%# Eval("Reservation_ID") %>
                                </h3>
                                <div style="color: #666; font-size: 14px;">
                                    <i class="fa fa-user"></i> <%# Eval("CustomerName") %>
                                    | <i class="fa fa-phone"></i> <%# Eval("Customer_MobilePhone") %>
                                </div>
                            </div>
                            <div class="checkout-rating">
                                <div class="big-rating"><%# Eval("GuestSatisfaction") %></div>
                                <div style="display: flex; flex-direction: column;">
                                    <span style="color: #f39c12;"><%# GetStars(Eval("GuestSatisfaction")) %></span>
                                    <span style="font-size: 12px; color: #666;">Guest Satisfaction</span>
                                </div>
                            </div>
                        </div>

                        <div style="background: #e8f4fd; padding: 12px; border-radius: 8px; margin-bottom: 15px;">
                            <div style="display: flex; align-items: center; gap: 8px; margin-bottom: 8px;">
                                <i class="fa fa-home" style="color: #3498db; font-size: 18px;"></i>
                                <strong style="color: #2c3e50;"><%# Eval("AccommodationNames") %></strong>
                            </div>
                            <div style="display: flex; gap: 20px; font-size: 13px; color: #666;">
                                <span><i class="fa fa-calendar"></i> Check-in: <%# Eval("CheckinDate", "{0:dd MMM yyyy}") %></span>
                                <span><i class="fa fa-calendar-check-o"></i> Check-out: <%# Eval("CheckoutDate", "{0:dd MMM yyyy}") %></span>
                                <span><i class="fa fa-moon-o"></i> <%# Eval("StayDays") %> คืน</span>
                            </div>
                        </div>

                        <div class="checkout-meta">
                            <div class="checkout-meta-item">
                                <span class="checkout-meta-label"><i class="fa fa-calendar-check-o"></i> Checkout Date</span>
                                <span class="checkout-meta-value"><%# Eval("CheckoutDate", "{0:dd MMM yyyy HH:mm}") %></span>
                            </div>
                            <div class="checkout-meta-item">
                                <span class="checkout-meta-label"><i class="fa fa-user-circle"></i> Checked Out By</span>
                                <span class="checkout-meta-value"><%# Eval("AdminName") %></span>
                            </div>
                            <div class="checkout-meta-item">
                                <span class="checkout-meta-label"><i class="fa fa-money"></i> Final Amount</span>
                                <span class="checkout-meta-value"><%# Eval("FinalAmount", "{0:N2}") %> THB</span>
                            </div>
                            <div class="checkout-meta-item">
                                <span class="checkout-meta-label"><i class="fa fa-credit-card"></i> Payment Status</span>
                                <span class="checkout-meta-value"><%# Eval("PaymentStatus") %></span>
                            </div>
                        </div>

                        <div style="display: flex; gap: 10px; flex-wrap: wrap; margin: 15px 0;">
                            <%# Convert.ToBoolean(Eval("RoomDamage")) ? "<span class='checkout-badge badge-damage'><i class='fa fa-exclamation-triangle'></i> Room Damage</span>" : "" %>
                            <%# Convert.ToBoolean(Eval("MissingItems")) ? "<span class='checkout-badge badge-missing'><i class='fa fa-warning'></i> Missing Items</span>" : "" %>
                            <%# Convert.ToBoolean(Eval("KeyReturned")) ? "<span class='checkout-badge badge-key-ok'><i class='fa fa-key'></i> Key Returned</span>" : "<span class='checkout-badge badge-key-missing'><i class='fa fa-key'></i> Key Not Returned</span>" %>
                            <%# Convert.ToString(Eval("CleaningStatus")) == "GOOD" ? "<span class='checkout-badge badge-clean'><i class='fa fa-check'></i> Clean</span>" : "<span class='checkout-badge badge-dirty'><i class='fa fa-trash'></i> " + Convert.ToString(Eval("CleaningStatus")) + "</span>" %>
                        </div>

                        <%# !string.IsNullOrEmpty(Convert.ToString(Eval("DamageDescription"))) ?
                            "<div style='margin: 10px 0; padding: 10px; background: #f8d7da; border-radius: 6px;'><strong>Damage:</strong> " + Eval("DamageDescription") +
                            (Convert.ToDecimal(Eval("DamageCharge")) > 0 ? " - <strong>Charge: " + String.Format("{0:N2}", Eval("DamageCharge")) + " THB</strong>" : "") + "</div>" : "" %>

                        <%# !string.IsNullOrEmpty(Convert.ToString(Eval("MissingItemsDescription"))) ?
                            "<div style='margin: 10px 0; padding: 10px; background: #fff3cd; border-radius: 6px;'><strong>Missing Items:</strong> " + Eval("MissingItemsDescription") +
                            (Convert.ToDecimal(Eval("MissingItemsCharge")) > 0 ? " - <strong>Charge: " + String.Format("{0:N2}", Eval("MissingItemsCharge")) + " THB</strong>" : "") + "</div>" : "" %>

                        <%# !string.IsNullOrEmpty(Convert.ToString(Eval("Notes"))) ?
                            "<div style='margin: 10px 0; padding: 15px; background: #f8f9fa; border-radius: 6px; border-left: 4px solid #9b59b6;'><strong><i class='fa fa-comment'></i> Notes:</strong><br />" + Eval("Notes") + "</div>" : "" %>
                    </div>
                </ItemTemplate>
            </asp:Repeater>

            <asp:Label ID="lblNoCheckoutReviews" runat="server" Visible="false" style="display: block; text-align: center; padding: 60px; color: #999;">
                <i class="fa fa-sign-out" style="font-size: 64px; opacity: 0.5;"></i><br />
                No checkout reviews found
            </asp:Label>
        </div> <!-- End Checkout Reviews Tab -->
    </div>

    <script type="text/javascript">
        function showTab(tabId) {
            // Hide all tabs
            var tabs = document.querySelectorAll('.tab-content');
            tabs.forEach(function(tab) {
                tab.classList.remove('active');
            });

            // Remove active class from all buttons
            var buttons = document.querySelectorAll('.tab-btn');
            buttons.forEach(function(btn) {
                btn.classList.remove('active');
            });

            // Show selected tab
            document.getElementById(tabId).classList.add('active');

            // Add active class to clicked button
            document.getElementById('btn-' + tabId).classList.add('active');

            // Save tab state to hidden field
            var hdnField = document.getElementById('<%= hdnActiveTab.ClientID %>');
            if (hdnField) {
                hdnField.value = tabId;
            }
        }

        // Restore tab state on page load (for postbacks)
        document.addEventListener('DOMContentLoaded', function() {
            var hdnField = document.getElementById('<%= hdnActiveTab.ClientID %>');
            if (hdnField && hdnField.value) {
                showTab(hdnField.value);
            }
        });
    </script>
</asp:Content>
