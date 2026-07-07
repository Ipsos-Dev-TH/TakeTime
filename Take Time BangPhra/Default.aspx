<%@ Page MaintainScrollPositionOnPostback="true" Async="true" Title="ที่พักบางพระ รีสอร์ท โรงแรม บางแสน ศรีราชา ชลบุรี | Take Time Nature Resort" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="Take_Time_BangPhra._Default2" %>

<asp:Content ID="BodyContent" ContentPlaceHolderID="MainContent" runat="server">
    <style type="text/css">
        /* [Previous CSS remains exactly the same] */
    </style>

    <link rel="stylesheet" href="/Content/jquery-ui.css">
    <link rel="stylesheet" href="/Content/style.css">
    <link rel="stylesheet" type="text/css" href="/Content/GridView.css">
    
    <!-- Swiper CSS from approved CDN -->
    <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/Swiper/11.0.5/swiper-bundle.min.css" integrity="sha512-rd0qOHVMOcez6pLWPVFIv7EfSdGKLt+eafXh4RO/12Fgr41hDQxfGvoi1Vy55QIVcQEujUE1LQrATCLl2Fs+ag==" crossorigin="anonymous" referrerpolicy="no-referrer" />
    <style type="text/css">
        /* Base Styles */
        body {
            font-family: 'Prompt', sans-serif;
            background-color: #F8F4E9; /* Cream background */
            color: #5D4037; /* Brown text */
        }
        
        .jumbotron {
            background-color: #F8F4E9;
            padding: 20px;
            border-radius: 5px;
            box-shadow: 0 2px 5px rgba(0,0,0,0.1);
        }
        
        /* Calendar Styles */
        .myCalendar {
            width: 100%;
            max-width: 600px;
            margin: 0 auto;
            border: 1px solid #D7CEC7; /* Light brown border */
            border-radius: 5px;
            background-color: white;
        }
        
        .myCalendar th.myCalendarDayHeader {
            height: 35px;
            border-bottom: 1px solid #D7CEC7;
            border-right: 1px solid #D7CEC7;
            background-color: #F8F4E9;
            color: #5D4037;
        }
        
        .myCalendar td.myCalendarDay {
            border: 1px solid #D7CEC7;
            height: 40px;
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
            color: #5D4037;
            text-decoration: none;
        }
        
        .myCalendar .myCalendarToday {
            background-color: #F2E9E1;
            box-shadow: 0 0 5px 2px #E5D5C5;
        }
        
        .myCalendar .myCalendarToday a {
            color: #8D6E63 !important;
            font-weight: bold;
        }
        
        .myCalendar .myCalendarDay a:hover,
        .myCalendar .myCalendarSelector a:hover {
            background-color: #8D6E63;
            color: white !important;
        }
        
        .myCalendar .myCalendarTitle {
            background-color: #8D6E63;
            color: white;
            font-weight: bold;
        }
        
        /* GridView Styles */
        .mydatagrid {
            width: 100%;
            border: 1px solid #D7CEC7;
            border-collapse: collapse;
            margin: 10px 0;
        }
        
        .mydatagrid th {
            background-color: #8D6E63;
            color: white;
            padding: 8px;
            text-align: left;
        }
        
        .mydatagrid td {
            padding: 8px;
            border-bottom: 1px solid #D7CEC7;
        }
        
        .mydatagrid tr:nth-child(even) {
            background-color: #F8F4E9;
        }
        
        .mydatagrid tr:hover {
            background-color: #F2E9E1;
        }
        
        /* Swiper Styles */
        .swiper-container {
    width: 100%;
    height: 300px;
    margin: 20px auto;
    border-radius: 8px;
    overflow: hidden;
    box-shadow: 0 4px 8px rgba(0,0,0,0.1);
}

/* Swiper Slides */
.swiper-slide {
    display: flex;
    align-items: center;
    justify-content: center;
    background-size: cover;
    background-position: center;
    cursor: pointer;
    width: 100%;
    height: 100%;
}

/* Swiper Images */
.swiper-slide img {
    max-width: 90%;
    max-height: 90%;
    object-fit: contain; /* Changed from 'cover' to 'contain' to maintain aspect ratio */
    display: block;
    margin: 0 auto;
}
        
        .swiper-button-next, .swiper-button-prev {
            color: #8D6E63;
            background-color: rgba(255,255,255,0.7);
            width: 40px;
            height: 40px;
            border-radius: 50%;
            display: flex;
            align-items: center;
            justify-content: center;
        }
        
        .swiper-pagination-bullet {
            background: #D7CEC7;
            opacity: 1;
        }
        
        .swiper-pagination-bullet-active {
            background: #8D6E63;
        }
        
        /* Responsive Layout */
        .main-container {
            display: flex;
            flex-wrap: wrap;
            gap: 10px;
        }
        
        .left-section {
            flex: 1;
            min-width: 300px;
        }
        
        .right-section {
            flex: 1;
            min-width: 300px;
        }
        
        @media (max-width: 768px) {
            .main-container {
                flex-direction: column;
            }
            .right-section {
                order: -1; /* This makes right section come first on mobile */
            }
            .left-section {
                order: 1; /* This makes left section come after on mobile */
            }
        }
        
        /* Button Styles */
        .btn {
            padding: 8px 16px;
            border-radius: 4px;
            text-decoration: none;
            font-weight: bold;
            transition: all 0.3s;
        }
        
        .btn-success {
            background-color: #8D6E63;
            color: white;
            border: 1px solid #6D4C41;
        }
        
        .btn-success:hover {
            background-color: #6D4C41;
        }
        
        /* Review Styles */
        .review-item {
            background-color: white;
            border-radius: 8px;
            margin-bottom: 15px;
            padding: 15px;
            box-shadow: 0 2px 4px rgba(0,0,0,0.05);
        }
        
        .review-header {
            display: flex;
            align-items: center;
            margin-bottom: 10px;
        }
        
        .review-picture img {
            width: 50px;
            height: 50px;
            border-radius: 50%;
            object-fit: cover;
            margin-right: 15px;
        }
        
        .review-author {
            color: #5D4037;
            font-weight: bold;
        }
        
        .review-date {
            color: #8D6E63;
            font-size: 0.9em;
        }
        
        .review-stars {
            color: #FFC107;
            font-size: 1.2em;
            margin-top: 5px;
        }
        
        .review-text {
            color: #5D4037;
            line-height: 1.5;
        }
        
        /* Header Styles */
        .resort-header {
            text-align: center;
            margin-bottom: 20px;
            color: #5D4037;
        }
        
        /* Section Styles */
        .section-title {
            color: #5D4037;
            border-bottom: 2px solid #D7CEC7;
            padding-bottom: 5px;
            margin-top: 30px;
        }
        
        .calendar-container {
            margin-bottom: 20px;
        }
        
        .booking-container {
            margin-top: 20px;
        }
    </style>

     <!-- Swiper CSS from approved CDN -->
    <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/Swiper/11.0.5/swiper-bundle.min.css" integrity="sha512-rd0qOHVMOcez6pLWPVFIv7EfSdGKLt+eafXh4RO/12Fgr41hDQxfGvoi1Vy55QIVcQEujUE1LQrATCLl2Fs+ag==" crossorigin="anonymous" referrerpolicy="no-referrer" />
    <style type="text/css">
        /* [Previous CSS remains the same until .main-container] */
        
        /* Modified Layout Styles */
        .main-container {
            display: flex;
            flex-wrap: wrap;
            gap: 10px;
        }
        
        .left-section {
            flex: 1;
            min-width: 300px;
            order: 2; /* Default order for desktop */
        }
        
        .right-section {
            flex: 1;
            min-width: 300px;
            order: 1; /* Default order for desktop */
        }
        
        @media (max-width: 768px) {
            .main-container {
                flex-direction: column;
            }
            
            .left-section, .right-section {
                width: 100%;
            }
            
            .right-section {
                order: 1; /* Right section comes first on mobile */
            }
            
            .left-section {
                order: 2; /* Left section comes second on mobile */
            }
        }
        
        /* Updated Review Styles for Swiper */
        .reviews-swiper {
            width: 100%;
            height: auto;
            margin: 20px auto;
            border-radius: 8px;
            overflow: hidden;
        }
        
        .review-slide {
            background-color: white;
            border-radius: 8px;
            padding: 20px;
            box-shadow: 0 2px 4px rgba(0,0,0,0.05);
            height: auto;
            margin: 10px;
        }
        
        .review-header {
            display: flex;
            align-items: center;
            margin-bottom: 10px;
        }
        
        .review-picture img {
            width: 50px;
            height: 50px;
            border-radius: 50%;
            object-fit: cover;
            margin-right: 15px;
        }
        
        .review-author {
            color: #5D4037;
            font-weight: bold;
        }
        
        .review-date {
            color: #8D6E63;
            font-size: 0.9em;
            display: block;
            margin-top: 5px;
        }
        
        .review-stars {
            color: #FFC107;
            font-size: 1.2em;
            margin-top: 5px;
        }
        
        .review-text {
            color: #5D4037;
            line-height: 1.5;
            margin-top: 10px;
        }
        
        .reviews-pagination {
            position: relative;
            margin-top: 20px;
        }
        
        /* Adjust section title for reviews */
        .reviews-title {
            color: #5D4037;
            border-bottom: 2px solid #D7CEC7;
            padding-bottom: 5px;
            margin-top: 30px;
            text-align: center;
        }

        .label-center {
    text-align: center;
    width: 100%;
    margin: 15px auto; /* Adds spacing above and below */
    padding: 10px;
    background-color: #F8F4E9; /* Matches your theme */
    border-radius: 4px;
    box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
}

        .gridview-container {
    width: 100%;
    display: flex;
    justify-content: center;
    margin: 20px 0;
}

/* GridView Styles */
.mydatagrid {
    width: 90%; /* Adjust this as needed */
    border: 1px solid #D7CEC7;
    border-collapse: collapse;
    margin: 0 auto; /* This centers the table within its container */
}

/* If you want to center the text in cells as well */
.mydatagrid td {
    text-align: left;
}

.mydatagrid th {
    text-align: left;
}
    </style>
          <style>
          .container {
              width: 100% !important;
          }
       </style>
    <link rel="stylesheet" href="/Content/jquery-ui.css">
    <link rel="stylesheet" href="/Content/style.css">
    <link rel="stylesheet" type="text/css" href="/Content/GridView.css">
    
    <!-- Swiper CSS -->
    <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/swiper@11/swiper-bundle.min.css"/>
      <!-- Swiper JS from approved CDN -->
   <!-- Swiper JS from approved CDN -->
    <script src="https://cdnjs.cloudflare.com/ajax/libs/Swiper/11.0.5/swiper-bundle.min.js" integrity="sha512-Ysw1DcK1P+uYLqprEAzNQJP+J4hTx4t/3X2nbVwszao8wD+9afLjBQYjz7Uk4ADP+Er++mJoScI42ueGtQOzEA==" crossorigin="anonymous" referrerpolicy="no-referrer"></script>
    
   <script>
       // Initialize Swipers with error handling
       document.addEventListener('DOMContentLoaded', function () {
           try {
               // Main Image Swiper
               if (typeof Swiper !== 'undefined') {
                   const imageSwiper = new Swiper('.mySwiper', {
                       loop: true,
                       spaceBetween: 20,
                       centeredSlides: true,
                       autoplay: {
                           delay: 5000,
                           disableOnInteraction: false,
                       },
                       pagination: {
                           el: '.swiper-pagination',
                           clickable: true,
                       },
                       navigation: {
                           nextEl: '.swiper-button-next',
                           prevEl: '.swiper-button-prev',
                       },
                       effect: 'fade',
                       fadeEffect: {
                           crossFade: true
                       }
                   });

                   // Reviews Swiper
                   const reviewsSwiper = new Swiper('.reviews-swiper', {
                       slidesPerView: 1,
                       spaceBetween: 20,
                       loop: true,
                       autoplay: {
                           delay: 8000,
                           disableOnInteraction: false,
                       },
                       pagination: {
                           el: '.reviews-pagination',
                           clickable: true,
                       },
                       breakpoints: {
                           768: {
                               slidesPerView: 1
                           },
                           992: {
                               slidesPerView: 1
                           }
                       }
                   });
               } else {
                   console.error('Swiper is not loaded. Falling back to simple display.');
                   // Fallback behavior if Swiper fails to load
                   document.querySelectorAll('.swiper-slide img').forEach(img => {
                       img.style.display = 'block';
                       img.style.width = '100%';
                       img.style.height = 'auto';
                   });
                   document.querySelectorAll('.swiper-pagination, .swiper-button-next, .swiper-button-prev').forEach(el => {
                       el.style.display = 'none';
                   });
               }
           } catch (e) {
               console.error('Swiper initialization error:', e);
           }

           // Reviews rendering code with additional checks
           try {
               if (typeof jsoninput !== 'undefined' && jsoninput && jsoninput.result && jsoninput.result.reviews) {
                   renderReviews(jsoninput.result.reviews);
               } else {
                   console.log("No reviews data available or not in expected format.");
               }
           } catch (e) {
               console.error('Error processing reviews:', e);
           }

           function renderReviews(reviews) {
               const container = document.getElementById("reviews-container");
               if (!container) return;

               container.innerHTML = "";

               // Verify reviews is an array
               if (!Array.isArray(reviews)) {
                   console.error('Reviews data is not an array');
                   return;
               }

               // Filter reviews to only show ratings >= 4
               const highRatedReviews = reviews.filter(review => {
                   const rating = review.rating || 0;
                   return rating >= 4;
               });

               // Sort high-rated reviews by rating (highest first)
               highRatedReviews.sort((a, b) => b.rating - a.rating);

               // Show top 5 high-rated reviews
               const topReviews = highRatedReviews.slice(0, 5);

               // If no high-rated reviews, show a message
               if (topReviews.length === 0) {
                   container.innerHTML = '<div class="no-reviews-message">No high-rated reviews available</div>';
                   return;
               }

               topReviews.forEach(review => {
                   // Basic validation of review data
                   if (!review.author_name || !review.text) return;

                   const reviewDiv = document.createElement("div");
                   reviewDiv.className = "swiper-slide review-slide";

                   // Create star rating
                   const rating = Math.min(5, Math.max(1, review.rating || 5)); // Ensure rating is between 1-5
                   const stars = '★★★★★'.slice(0, rating) + '☆☆☆☆☆'.slice(rating);

                   reviewDiv.innerHTML = `
                     <div class="review-header">
                         <div class="review-picture">
                             <img src="${review.profile_photo_url || 'https://via.placeholder.com/50'}" 
                                  alt="${review.author_name}" 
                                  onerror="this.src='https://via.placeholder.com/50'">
                         </div>
                         <div class="review-usergrade">
                             <div class="review-author">${review.author_name}</div>
                             <div class="review-stars" title="${rating} stars">
                                 ${stars}
                             </div>
                         </div>
                     </div>
                     <div class="review-text">${review.text}</div>
                     <span class="review-date">${review.relative_time_description || ''}</span>
                 `;

                   container.appendChild(reviewDiv);
               });

               // Reinitialize reviews swiper after adding slides
               if (typeof Swiper !== 'undefined' && topReviews.length > 0) {
                   const reviewsSwiper = new Swiper('.reviews-swiper', {
                       slidesPerView: 1,
                       spaceBetween: 20,
                       loop: topReviews.length > 1, // Only loop if there's more than 1 review
                       autoplay: topReviews.length > 1 ? {
                           delay: 8000,
                           disableOnInteraction: false,
                       } : false, // Disable autoplay if only 1 review
                       pagination: {
                           el: '.reviews-pagination',
                           clickable: true,
                       }
                   });
               }
           }
       });
   </script>

    <div class="jumbotron">
        <div class="ExampleFont">

            <!-- SEO / AEO hero: keyword-rich H1 + intro (crawlable, styled to fit) -->
            <header class="seo-hero" style="text-align:center; margin:0 auto 14px; max-width:900px;">
                <h1 style="font-size:1.5em; color:#5D4037; margin:0 0 6px; line-height:1.35;">
                    Take Time Nature Resort — ที่พัก รีสอร์ท โรงแรม บางพระ ท่ามกลางธรรมชาติ
                </h1>
                <p style="color:#6d5c4d; font-size:0.98em; margin:0;">
                    ที่พักบางพระวิวอ่างเก็บน้ำ วิลล่าและบ้านพักเงียบสงบ ใกล้<strong>บางแสน ศรีราชา ชลบุรี</strong> —
                    เหมาะพักผ่อน ครอบครัว คู่รัก และหมู่คณะ จองห้องพักออนไลน์ หรือโทร
                    <a href="tel:+66634161496" style="color:#5D4037; font-weight:600;">063-416-1496</a>
                </p>
            </header>

            <!-- Main Container with Left and Right Sections -->
            <div class="main-container">
                <!-- Left Section - Image Slider and Reviews -->
                <div class="left-section">
                    <!-- Swiper Gallery -->
                    <div class="swiper mySwiper">
                        <div class="swiper-wrapper">
                            <div class="swiper-slide">
                                <img src="./Images/Accommodation/Nordic Tent.png" alt="Nordic Tent"/>
                            </div>
                            <div class="swiper-slide">
                                <img src="./Images/Accommodation/Glass Room.png" alt="Glass Room" />
                            </div>
                            <div class="swiper-slide">
                                <img src="./Images/Accommodation/Villa.png" alt="Villa" />
                            </div>
                            <div class="swiper-slide">
                                <img src="./Images/Accommodation/Camping Tent.png" alt="Camping Tent" />
                            </div>
                            <div class="swiper-slide">
                                <img src="./Images/Accommodation/Cozy Cabin.jpg" alt="Cozy Cabin" />
                            </div>
                            <div class="swiper-slide">
                                <img src="./Images/Accommodation/Nordic Cabin.jpg" alt="Nordic Cabin" />
                            </div>
                            <div class="swiper-slide">
                                <img src="./Images/แผนผัง.jpg" alt="Resort Map" />
                            </div>
                        </div>
                        <div class="swiper-button-next"></div>
                        <div class="swiper-button-prev"></div>
                        <div class="swiper-pagination"></div>
                    </div>
                    
                    <!-- Reviews Section -->
                     <!-- Reviews Section as Swiper Slideshow -->
                    <h3 class="reviews-title">Our Recent Google Reviews</h3>
                    <div class="swiper reviews-swiper">
                        <div class="swiper-wrapper" id="reviews-container"></div>
                        <div class="reviews-pagination swiper-pagination"></div>
                    </div><center>
                    <asp:Literal ID="Literal1" runat="server"></asp:Literal>
                    </center>
                    <div style="text-align: center; margin: 20px 0;">
                        <a class="btn btn-success" href="https://www.google.com/travel/hotels/entity/CgsIsoLIgrCxpI6cARAB/reviews?q=take%20time%20%E0%B8%9A%E0%B8%B2%E0%B8%87%E0%B8%9E%E0%B8%A3%E0%B8%B0&g2lb=2502548%2C2503771%2C2503781%2C4258168%2C4270442%2C4284970%2C4291517%2C4306835%2C4597339%2C4757164%2C4814050%2C4850738%2C4864715%2C4874190%2C4886480%2C4893075%2C4920132%2C4924070%2C4965990%2C4985712%2C4990494%2C72248281%2C72253254%2C72256471%2C72266483%2C72266485%2C72271034%2C72271797%2C72272556%2C72276044%2C72281250&hl=th-TH&gl=th&ssta=1&ts=CAESABpJCisSJzIlMHgzMTAyY2JkOTBmMTM0OGJkOjB4OWMxYzkxOGIwMDUyMDEzMhoAEhoSFAoHCOcPEAYYExIHCOcPEAYYFBgBMgIQACoJCgU6A1RIQhoA&qs=CAE4AkILCTIBUgCLkRycGAFCCwkyAVIAi5EcnBgB&ictx=1&utm_campaign=sharing&utm_medium=link&utm_source=htls" 
                           target="_blank" rel="noopener noreferrer">See All Our Google Reviews</a>
                    </div>
                </div>
                
                <!-- Right Section - Calendar and Booking -->
                <div class="right-section">
                    <div class="calendar-container">
                        <asp:Calendar ID="Calendar1" runat="server" OnDayRender="Calendar1_DayRender" OnSelectionChanged="Calendar1_SelectionChanged"
                            NextMonthText=">>>" PrevMonthText="<<<" CssClass="myCalendar ExampleFont" CellPadding="0" FirstDayOfWeek="Sunday">
                            <OtherMonthDayStyle ForeColor="#b0b0b0" />
                            <DayStyle CssClass="myCalendarDay" ForeColor="#2d3338" />
                            <DayHeaderStyle CssClass="myCalendarDayHeader" ForeColor="#2d3338" HorizontalAlign="Center" VerticalAlign="Middle" />
                            <SelectedDayStyle Font-Bold="True" Font-Size="12px" CssClass="myCalendarSelector" />
                            <TodayDayStyle CssClass="myCalendarToday" />
                            <SelectorStyle CssClass="myCalendarSelector" />
                            <NextPrevStyle CssClass="myCalendarNextPrev" />
                            <TitleStyle CssClass="myCalendarTitle" />
                        </asp:Calendar>
                    </div>
                    
                    <div class="booking-container">
                        <div class="label-center">
    <strong>
        <asp:Label ID="Label1" runat="server" Text=""></asp:Label>
    </strong>
</div>
                        <asp:GridView ID="GridView1" runat="server" AutoGenerateColumns="False" CssClass="mydatagrid ExampleFont gridview-container" 
                            PagerStyle-CssClass="pager" HeaderStyle-CssClass="header" RowStyle-CssClass="rows" 
                            OnRowCommand="GridView1_RowCommand" OnSelectedIndexChanged="GridView1_SelectedIndexChanged" Width="90%">
                            <Columns>
                                <asp:CommandField ButtonType="Button" SelectText="เลือก" ShowSelectButton="True" ControlStyle-CssClass="ExampleFont">
                                    <ControlStyle CssClass="ExampleFont"></ControlStyle>
                                </asp:CommandField>
                                <asp:BoundField DataField="AccomName" HeaderText="รายชื่อห้องพัก" HeaderStyle-CssClass="ExampleFont" ItemStyle-CssClass="ExampleFont">
                                    <HeaderStyle CssClass="ExampleFont"></HeaderStyle>
                                    <ItemStyle CssClass="ExampleFont"></ItemStyle>
                                </asp:BoundField>
                                <asp:BoundField DataField="StatusOnDate" HeaderText="สถานะ" HeaderStyle-CssClass="ExampleFont" ItemStyle-CssClass="ExampleFont">
                                    <HeaderStyle CssClass="ExampleFont"></HeaderStyle>
                                    <ItemStyle CssClass="ExampleFont"></ItemStyle>
                                </asp:BoundField>
                                <asp:BoundField DataField="People" HeaderText="จำนวนผู้เข้าพัก" Visible="false" HeaderStyle-CssClass="ExampleFont" ItemStyle-CssClass="ExampleFont">
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
                        </asp:GridView>
                    </div>
                </div>
            </div>
        </div>
    </div>

    
</asp:Content>