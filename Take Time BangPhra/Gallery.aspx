<%@ Page Title="" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Gallery.aspx.cs" Inherits="Take_Time_BangPhra.Gallery" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <style>
        .gallery-container {
            max-width: 1200px;
            margin: 0 auto;
            padding: 20px;
            background-color: #F5F0E5;
            border-radius: 8px;
            box-shadow: 0 4px 8px rgba(0, 0, 0, 0.1);
        }

        .gallery-title {
            color: #5D4037;
            text-align: center;
            margin-bottom: 30px;
            font-family: 'Playfair Display', serif;
            font-size: 2.5rem;
        }

        .slideshow-container {
            position: relative;
            margin: auto;
            width: 100%;
            height: 500px; /* Fixed height for container */
            overflow: hidden;
            border-radius: 6px;
            box-shadow: 0 2px 10px rgba(0, 0, 0, 0.2);
            background-color: #F5F0E5;
        }

        .mySlides {
            position: absolute;
            width: 100%;
            height: 100%;
            opacity: 0;
            transition: opacity 1s ease-in-out;
            display: flex;
            align-items: center;
            justify-content: center;
        }

        .mySlides.active {
            opacity: 1;
            z-index: 2;
        }

        .mySlides img {
            max-height: 100%;
            max-width: 100%;
            object-fit: contain;
        }

        /* Next & previous buttons */
        .prev, .next {
            cursor: pointer;
            position: absolute;
            top: 50%;
            width: auto;
            padding: 20px;
            margin-top: -22px;
            color: #F5F0E5;
            background-color: rgba(93, 64, 55, 0.7);
            font-weight: bold;
            font-size: 24px;
            transition: 0.3s;
            border-radius: 0 4px 4px 0;
            user-select: none;
            z-index: 3;
        }

        .next {
            right: 0;
            border-radius: 4px 0 0 4px;
        }

        .prev:hover, .next:hover {
            background-color: rgba(93, 64, 55, 0.9);
        }

        /* Number text (1/3 etc) */
        .numbertext {
            color: #F5F0E5;
            font-size: 14px;
            padding: 10px 14px;
            position: absolute;
            top: 0;
            background-color: rgba(93, 64, 55, 0.7);
            border-radius: 0 0 4px 0;
            z-index: 3;
        }

        /* Caption text */
        .caption-container {
            padding: 15px;
            text-align: center;
            background-color: #5D4037;
            color: #F5F0E5;
            font-size: 1.1rem;
        }

        /* Thumbnail row */
        .thumbnail-row {
            display: flex;
            flex-wrap: wrap;
            justify-content: center;
            padding: 10px 0;
            background-color: #F5F0E5;
            gap: 10px;
        }

        /* Thumbnail columns */
        .thumbnail-column {
            flex: 0 0 calc(16.66% - 10px);
            max-width: calc(16.66% - 10px);
            box-sizing: border-box;
        }

        /* Thumbnail images */
        .thumbnail {
            width: 100%;
            height: 80px;
            object-fit: cover;
            cursor: pointer;
            opacity: 0.7;
            transition: 0.3s;
            border: 2px solid transparent;
            border-radius: 4px;
        }

        .thumbnail:hover, .active-thumbnail {
            opacity: 1;
            border-color: #5D4037;
        }

        /* Responsive layout */
        @media screen and (max-width: 900px) {
            .thumbnail-column {
                flex: 0 0 calc(20% - 10px);
                max-width: calc(20% - 10px);
            }
            
            .slideshow-container {
                height: 400px;
            }
        }

        @media screen and (max-width: 600px) {
            .thumbnail-column {
                flex: 0 0 calc(25% - 10px);
                max-width: calc(25% - 10px);
            }
            
            .gallery-title {
                font-size: 2rem;
            }
            
            .slideshow-container {
                height: 300px;
            }
        }
    </style>

    <div class="gallery-container">
        <h1 class="gallery-title">Gallery</h1>
        <asp:Literal ID="Literal1" runat="server"></asp:Literal>
    </div>

    <script>
        let slideIndex = 1;
        let slideInterval;
        const slideDelay = 5000; // 5 seconds

        // Initialize slideshow
        document.addEventListener('DOMContentLoaded', function () {
            showSlides(slideIndex);
            startSlideShow();

            // Pause on hover
            const container = document.querySelector('.slideshow-container');
            if (container) {
                container.addEventListener('mouseenter', pauseSlideShow);
                container.addEventListener('mouseleave', startSlideShow);
            }

            // Thumbnail hover events
            const thumbnails = document.getElementsByClassName('thumbnail');
            for (let thumbnail of thumbnails) {
                thumbnail.addEventListener('mouseenter', pauseSlideShow);
                thumbnail.addEventListener('mouseleave', startSlideShow);
            }
        });

        // Next/previous controls
        function plusSlides(n) {
            clearTimeout(slideInterval);
            showSlides(slideIndex += n);
        }

        // Thumbnail image controls
        function currentSlide(n) {
            clearTimeout(slideInterval);
            showSlides(slideIndex = n);
        }

        function showSlides(n) {
            let i;
            let slides = document.getElementsByClassName("mySlides");
            let thumbnails = document.getElementsByClassName("thumbnail");
            let captionText = document.getElementById("caption");

            if (n > slides.length) { slideIndex = 1 }
            if (n < 1) { slideIndex = slides.length }

            // Hide all slides
            for (i = 0; i < slides.length; i++) {
                slides[i].classList.remove("active");
            }

            // Remove active class from all thumbnails
            for (i = 0; i < thumbnails.length; i++) {
                thumbnails[i].classList.remove("active-thumbnail");
            }

            // Show current slide
            slides[slideIndex - 1].classList.add("active");

            // Add active class to current thumbnail
            thumbnails[slideIndex - 1].classList.add("active-thumbnail");

            // Update caption if exists
            if (captionText) {
                captionText.innerHTML = thumbnails[slideIndex - 1].alt;
            }

            // Reset timer
            clearTimeout(slideInterval);
            slideInterval = setTimeout(() => {
                plusSlides(1);
            }, slideDelay);
        }

        function startSlideShow() {
            clearTimeout(slideInterval);
            slideInterval = setTimeout(() => {
                plusSlides(1);
            }, slideDelay);
        }

        function pauseSlideShow() {
            clearTimeout(slideInterval);
        }
    </script>
</asp:Content>