<%@ Page MaintainScrollPositionOnPostback="true" Async="true" Title="Home Page" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Default_Backup.aspx.cs" Inherits="Take_Time_BangPhra._Default" %>


<asp:Content ID="BodyContent" ContentPlaceHolderID="MainContent" runat="server">
    <link rel="stylesheet" href="/Content/jquery-ui.css">
  <link rel="stylesheet" href="/Content/style.css">
    <link rel="stylesheet" type="text/css" href="/Content/GridView.css">
     <style>
            th, td {
  padding: 5px;
}
                .auto-style2 {
                    width: 75%;
                    height: 35px;
                }
                </style> 
    <style>
 .header-center{
        text-align:center;
    }
  .header-right{
        text-align:right;
    }
         </style>

    <style>
        .product-info-table-row {
  display: grid;
  grid-template-columns: 30% auto;
}

.product-info-table-cell {
  border: 1px solid #999999;
  display: table-cell;
  padding: 3px 10px;
}

@media screen and (min-width: 600px) {
  .product-info-table {
    display: table;
    width: 100%;
  }
  .product-info-table-body {
    display: table-row-group;
  }
  .product-info-table-row {
    display: table-row;
  }
  .product-info-table-cell {
    display: table-cell;
  }
}
    </style>

    <style>


.myCalendar th.myCalendarDayHeader  
{
    height:25px;
    border-bottom: outset 2px #fbfbfb; 
    border-right: outset 2px #fbfbfb; 
}
.myCalendar td.myCalendarDay {  
    border: outset 2px #fbfbfb;
}  

.myCalendar .myCalendarNextPrev {  
    text-align: center;  
}  



.myCalendar .myCalendarDayHeader a,
.myCalendar .myCalendarDay a,   
.myCalendar .myCalendarSelector a,  
.myCalendar .myCalendarNextPrev a {  
    display: block;  
    line-height: 20px;  
}  
.myCalendar .myCalendarToday{  background-color: #f2f2f2; -webkit-box-shadow: 0 0 7px 3px #e5e5e5;
box-shadow: 0 0 7px 3px #e5e5e5;}
.myCalendar .myCalendarToday a{color:#25bae5 !important;}
.myCalendar .myCalendarDay a:hover,   
.myCalendar .myCalendarSelector a:hover {  
    background-color: #25bae5;  
}
        .auto-style2 {
            width: 50%;
            height: 239px;
        }
        </style>
    <div class="jumbotron">
         <div>
             <div class="ExampleFont">
                 <center>
                    <asp:Calendar ID="Calendar1" runat="server" Height="199px" Width="80%" OnDayRender="Calendar1_DayRender" OnSelectionChanged="Calendar1_SelectionChanged" NextMonthText=">>>" PrevMonthText="<<<" CssClass="myCalendar ExampleFont" CellPadding="0" >
             <OtherMonthDayStyle ForeColor="#b0b0b0" />
            <DayStyle CssClass="myCalendarDay" ForeColor="#2d3338" />
            <DayHeaderStyle CssClass="myCalendarDayHeader" ForeColor="#2d3338" HorizontalAlign="Center" VerticalAlign="Middle" />
<SelectedDayStyle Font-Bold="True" Font-Size="12px" CssClass="myCalendarSelector" />
            <TodayDayStyle CssClass="myCalendarToday" />
            <SelectorStyle CssClass="myCalendarSelector" />
            <NextPrevStyle CssClass="myCalendarNextPrev" />
            <TitleStyle CssClass="myCalendarTitle" /></asp:Calendar>
                     <br /><center>
        <table style="width:100%">
       <tr >
                <td style="align-content:center;align-items:center" >
                        <center>
   
                                             <asp:Label ID="Label1" runat="server" Text=""></asp:Label>
                    <br />
                    <asp:GridView ID="GridView1" runat="server" AutoGenerateColumns="False" CssClass="mydatagrid ExampleFont" PagerStyle-CssClass="pager" HeaderStyle-CssClass="header" RowStyle-CssClass="rows" OnRowCommand="GridView1_RowCommand" OnSelectedIndexChanged="GridView1_SelectedIndexChanged">
                        <Columns>
                            <asp:CommandField ButtonType="Button" SelectText="เลือก" ShowSelectButton="True" ControlStyle-CssClass="ExampleFont">
<ControlStyle CssClass="ExampleFont"></ControlStyle>
                            </asp:CommandField>
                            <asp:BoundField DataField="AccomName" HeaderText="รายชื่อห้องพัก" HeaderStyle-CssClass="ExampleFont" ItemStyle-CssClass="ExampleFont" >
<HeaderStyle CssClass="ExampleFont"></HeaderStyle>

<ItemStyle CssClass="ExampleFont"></ItemStyle>
                            </asp:BoundField>
                            <asp:BoundField DataField="StatusOnDate" HeaderText="สถานะ" HeaderStyle-CssClass="ExampleFont" ItemStyle-CssClass="ExampleFont" >
<HeaderStyle CssClass="ExampleFont"></HeaderStyle>

<ItemStyle CssClass="ExampleFont"></ItemStyle>
                            </asp:BoundField>
                            <asp:BoundField DataField="People" HeaderText="จำนวนผู้เข้าพัก" Visible="false" HeaderStyle-CssClass="ExampleFont" ItemStyle-CssClass="ExampleFont" >
<HeaderStyle CssClass="ExampleFont"></HeaderStyle>

<ItemStyle CssClass="ExampleFont"></ItemStyle>
                            </asp:BoundField>
                            <asp:ButtonField ButtonType="Button" CommandName="Reserve" Text="จอง" ControlStyle-CssClass="ExampleFont">
<ControlStyle CssClass="ExampleFont"></ControlStyle>
                            </asp:ButtonField>
                        </Columns>

<HeaderStyle CssClass="header" Font-Names="Chulabhorn Likit Text Medium"></HeaderStyle>

<PagerStyle CssClass="pager"></PagerStyle>

<RowStyle CssClass="rows" Font-Names="Chulabhorn Likit Text"></RowStyle>
                    </asp:GridView></center>
                </td>
                
            </tr>
                 <tr><td class="auto-style2" style="width:50%;" >
    <center>
        
        
        
        
    <a href="./Images/Accommodation/Nordic Tent.png" target="_blank"><asp:Image ID="Image1" runat="server" Width="100%" ImageUrl="./Images/Accommodation/Nordic Tent.png" /></a>
    <a href="./Images/Accommodation/Glass Room.png" target="_blank"><asp:Image ID="Image2" runat="server" Width="100%" ImageUrl="./Images/Accommodation/Glass Room.png"/></a>
    <a href="./Images/Accommodation/Villa.png" target="_blank"><asp:Image ID="Image3" runat="server" Width="100%" ImageUrl="./Images/Accommodation/Villa.png"/></a>
        <a href="./Images/Accommodation/Camping Tent.png" target="_blank"><asp:Image ID="Image5" runat="server" Width="100%" ImageUrl="./Images/Accommodation/Camping Tent.png"/></a>
     <a href="./Images/Accommodation/Cozy Cabin.jpg" target="_blank"><asp:Image ID="Image6" runat="server" Width="100%" ImageUrl="./Images/Accommodation/Cozy Cabin.jpg"/></a>
         <a href="./Images/Accommodation/Nordic Cabin.jpg" target="_blank"><asp:Image ID="Image7" runat="server" Width="100%" ImageUrl="./Images/Accommodation/Nordic Cabin.jpg"/></a>
        <a href="./Images/แผนผัง.jpg" target="_blank"><asp:Image ID="Image4" runat="server" Width="100%" ImageUrl="./Images/แผนผัง.jpg"/></a>
  </center>
    </td></tr>
        </table></center>
    </div>
             </div>
          <style>
#map-plug {display:none;}
 
#google-reviews {
display:flex;
flex-wrap:wrap;
/*display: grid;
grid-template-columns: repeat( auto-fit, minmax(320px, 1fr));*/
}
span.review-profile-image {
    float: left;
        padding: 0px 15px 0px 0px;
}
span.review-profile-image img {
    width: 40px;
}
.modal-backdrop.in{display:none;}
.review-item {
    border-bottom: solid 1px rgba(190,190,190,.35);
    margin: 5px auto;
    display: block;
    width: 100%;
    padding: 15px 0px;
}
 
@media ( max-width:1200px) {
  .review-item { flex: 1 1 40%; }
}
 
@media ( max-width:450px) {
  .review-item { flex: 1 1 90%; }
}
 
.review-meta, .review-stars {text-align:left; font-size:115%;}
.review-author { text-transform: capitalize; font-weight:bold; }
.review-date {opacity:.6; display:block;}
.review-text {  line-height: 1.55;
    text-align: left;
    max-width: 72em;
    margin: auto;}
 
 
 
.review-stars ul {
display: inline-block;
list-style: none !important;
margin:0; padding:0;
}
 
.review-stars ul li {
float: left;
list-style: none !important;
margin-right: 1px;
line-height:1;
}
 
.review-stars ul li i {
  color: #E4B248;
  font-size: 1.4em;
  font-style:normal;
}
.review-stars ul li i.inactive { color: #c6c6c6;}
.star:after { content: "\2605"; }

#google-reviews {
  display: flex;
  flex-wrap: wrap;
  /*display: grid;*/
  /*grid-template-columns: repeat(auto-fit, minmax(320px, 1fr));*/
}

.review-item {
  border: solid 1px rgba(190, 190, 190, .35);
  margin: 0 auto;
  padding: 1em;
  flex: 1 1 50%;
  display: flex;
  flex-direction: column;
  align-content: stretch;
}

@media ( max-width:1200px) {
  .review-item {
    flex: 1 1 50%;
  }
}

@media ( max-width:450px) {
  .review-item {
    flex: 1 1 90%;
  }
}

.review-item-long {
  border: solid 1px rgba(190, 190, 190, .35);
  margin: 0 auto;
  padding: 1em;
  flex: 1 1 90%;
  display: flex;
  flex-direction: column;
  align-content: stretch;
}

@media ( max-width:1200px) {
  .review-item-long {
    flex: 1 1 90%;
  }
}

@media ( max-width:450px) {
  .review-item-long {
    flex: 1 1 90%;
  }
}

.review-header{
  display: flex;
}

.review-picture{
  width: 5em;
  height: auto;
  align-self: center;
  margin-right: 1em;
}

.review-usergrade{
  display: flex;
  flex-direction: column;
  justify-content: center;
  align-items: flex-start;
}

.review-meta, .review-stars {
  text-align: center;
  font-size: 115%;
}

.review-author {
  text-transform: capitalize;
  font-weight: bold;
}

.review-date {
  opacity: .6;
  display: block;
}

.review-text {
  line-height: 1.55;
  text-align: left;
  max-width: 100%;
  text-align: justify;
}

.review-stars ul {
  display: inline-block;
  list-style: none !important;
  margin: 0;
  padding: 0;
}

.review-stars ul li {
  float: left;
  list-style: none !important;
  margin-right: 1px;
  line-height: 1;
}

.review-stars ul li i {
  color: #eb6e00;
  /* Google's Star Orange in Nov 2017 */
  font-size: 1.4em;
  font-style: normal;
}

.review-stars ul li i.inactive {
  color: #c6c6c6;
}

.star:after {
  content: "\2605";
}


.buttons {
  margin: 20px 0 0 0; 
  display: flex;
  justify-content: center;
  align-items: center;
  flex-wrap: wrap;
}

.more-reviews {
  text-align: center;
}

.write-review {
  text-align: center;
}

.more-reviews a,
.write-review a {
  margin: 5px;
  border: 1px #eb6e00 solid;
  border-radius: 40px;
  padding: 10px;
  background-color: #eb6e00;
  color: #FFF;
  text-decoration: none;
}
</style>
        <br />
         <div class="container ExampleFont">
          <!-----------------google review-------->
            <div class="col-md-12 service-package" style="text-align: center;">
                 <h4 class="f-primary-b b-h4-special f-h4-special" style="margin: 10px 0 10px 0;"><span class="service-package-title">Our Recent Google Review</span></h4>
    <div id="reviews-container"></div>
                <asp:Literal ID="Literal1" runat="server"></asp:Literal>
    <div id="reviews-container"></div>

        <script>
            // Wait for the page to load
            document.addEventListener("DOMContentLoaded", function () {
                if (jsoninput && jsoninput.result && jsoninput.result.reviews) {
                    renderReviews(jsoninput.result.reviews);
                } else {
                    console.error("No reviews found or error in response.");
                }
            });

            var settings = $.extend(
                {
                    // These are the defaults.
                    header: "<h3>Google Reviews</h3>",
                    footer: "",
                    maxRows: 6,
                    minRating: 4,
                    months: [
                        "Jan",
                        "Feb",
                        "Mar",
                        "Apr",
                        "May",
                        "Jun",
                        "Jul",
                        "Aug",
                        "Sep",
                        "Oct",
                        "Nov",
                        "Dec",
                    ],
                    textBreakLength: "90",
                    shortenNames: false,
                    placeId: "",
                    moreReviewsButtonUrl: "",
                    moreReviewsButtonLabel: "Show More Reviews",
                    writeReviewButtonUrl: "",
                    writeReviewButtonLabel: "Write New Review",
                    showReviewDate: true,
                    showProfilePicture: true,
                }
            );



            var renderMoreReviewsButton = function () {
                return (
                    '<div class="more-reviews"><a href="' +
                    settings.moreReviewsButtonUrl +
                    '" target="_blank">' +
                    settings.moreReviewsButtonLabel +
                    "</a></div>"
                );
            };

            var renderWriteReviewButton = function () {
                return (
                    '<div class="write-review"><a href="' +
                    settings.writeReviewButtonUrl +
                    '" target="_blank">' +
                    settings.writeReviewButtonLabel +
                    "</a></div>"
                );
            };

            var renderPicture = function (picture) {
                return "<img class='review-picture' src='" + picture + "'>";
            };

            var renderHeader = function (header) {
                var html = "";
                html += header + "<br>";
                targetDivJquery.append(html);
            };

            var renderFooter = function (footer) {
                var html = "";
                var htmlButtons = "";

                if (settings.moreReviewsButtonUrl) {
                    htmlButtons += renderMoreReviewsButton();
                }
                if (settings.writeReviewButtonUrl) {
                    htmlButtons += renderWriteReviewButton();
                }
                if (htmlButtons != "") {
                    html += '<div class="buttons">' + htmlButtons + "</div>";
                }

                html += "<br>" + footer + "<br>";
                targetDivJquery.after(html);
            };

            var shortenName = function (name) {
                if (name.split(" ").length > 1) {
                    var shortenedName = "";
                    shortenedName = name.split(" ");
                    var lastNameFirstLetter = shortenedName[1][0];
                    var firstName = shortenedName[0];
                    if (lastNameFirstLetter == ".") {
                        return firstName;
                    } else {
                        return firstName + " " + lastNameFirstLetter + ".";
                    }
                } else if (name != undefined) {
                    return name;
                } else {
                    return "";
                }
            };

            var renderStars = function (rating) {
                var stars = '<div class="review-stars"><ul>';
                // fills gold stars
                for (var i = 0; i < rating; i++) {
                    stars += '<li><i class="star"></i></li>';
                }
                // fills empty stars
                if (rating < 5) {
                    for (var i = 0; i < 5 - rating; i++) {
                        stars += '<li><i class="star inactive"></i></li>';
                    }
                }
                stars += "</ul></div>";
                return stars;
            };

            var convertTime = function (UNIX_timestamp) {
                var newDate = new Date(UNIX_timestamp * 1000);
                var months = settings.months;
                var time =
                    newDate.getDate() +
                    ". " +
                    months[newDate.getMonth()] +
                    " " +
                    newDate.getFullYear();
                return time;
            };

            var filterReviewsByMinRating = function (reviews) {
                if (reviews === void 0) {
                    return [];
                } else {
                    for (var i = reviews.length - 1; i >= 0; i--) {
                        var review = reviews[i];
                        if (review.rating < settings.minRating) {
                            reviews.splice(i, 1);
                        }
                    }
                    return reviews;
                }
            };

            var sortReviewsByDateDesc = function (reviews) {
                if (
                    typeof reviews != "undefined" &&
                    reviews != null &&
                    reviews.length != null &&
                    reviews.length > 0
                ) {
                    return reviews
                        .sort(function (a, b) {
                            return a.time > b.time ? 1 : b.time > a.time ? -1 : 0;
                        })
                        .reverse();
                } else {
                    return [];
                }
            };

            var sanitizedReviewText = function (text) {
                text = text.replace("<script>", "");
                text = text.replace("<iframe>", "");
                return text;
            };

            // Function to render reviews
            function renderReviews(reviews) {
                const container = document.getElementById("reviews-container");
                container.innerHTML = ""; // Clear any previous content
                var i = 0;
                reviews.forEach(review => {
                    
                    const reviewDiv = document.createElement("div");
                    reviewDiv.className = "review";

                    var picture = "";
                    var review = reviews[i];
                    var reviewText = sanitizedReviewText(review.text);
                    var stars = renderStars(review.rating);
                    var date = convertTime(review.time);
                    var name = settings.shortenNames
                        ? shortenName(review.author_name)
                        : review.author_name;
                    var style =
                        reviewText.length > parseInt(settings.textBreakLength)
                            ? "review-item-long"
                            : "review-item";

                    if (settings.showProfilePicture) {
                        picture = renderPicture(review.profile_photo_url);
                    }


                    if (review.rating > 4) {

                        var html =
                            "<div class=" +
                            style +
                            "><div class='review-header'>" +
                            picture +
                            "<div class='review-usergrade'><div class='review-meta'><span class='review-author'>" +
                            name +
                            "</span><span class='review-sep'></span>" +
                            "</div>" +
                            stars +
                            "</div></div><p class='review-text'>" +
                            reviewText + " [" + review.relative_time_description + "]"
                        "</p></div>";


                        reviewDiv.innerHTML = html;
                        container.appendChild(reviewDiv);
                    }
                    i++;
                });
            }
        </script>
 
<a class="btn btn-success" style="margin:20px auto;" href="https://www.google.com/travel/hotels/entity/CgsIsoLIgrCxpI6cARAB/reviews?q=take%20time%20%E0%B8%9A%E0%B8%B2%E0%B8%87%E0%B8%9E%E0%B8%A3%E0%B8%B0&g2lb=2502548%2C2503771%2C2503781%2C4258168%2C4270442%2C4284970%2C4291517%2C4306835%2C4597339%2C4757164%2C4814050%2C4850738%2C4864715%2C4874190%2C4886480%2C4893075%2C4920132%2C4924070%2C4965990%2C4985712%2C4990494%2C72248281%2C72253254%2C72256471%2C72266483%2C72266485%2C72271034%2C72271797%2C72272556%2C72276044%2C72281250&hl=th-TH&gl=th&ssta=1&ts=CAESABpJCisSJzIlMHgzMTAyY2JkOTBmMTM0OGJkOjB4OWMxYzkxOGIwMDUyMDEzMhoAEhoSFAoHCOcPEAYYExIHCOcPEAYYFBgBMgIQACoJCgU6A1RIQhoA&qs=CAE4AkILCTIBUgCLkRycGAFCCwkyAVIAi5EcnBgB&ictx=1&utm_campaign=sharing&utm_medium=link&utm_source=htls" target="_blank" rel="noopener noreferrer">See All Our Google Reviews</a>
</div>
        <!-----------------end review------------->
    </div>
        </center>
    </div>
</asp:Content>
