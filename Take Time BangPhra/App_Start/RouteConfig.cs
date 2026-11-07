using System;
using System.Collections.Generic;
using System.Web;
using System.Web.Routing;
using Microsoft.AspNet.FriendlyUrls;

namespace Take_Time_BangPhra
{
    public static class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            // QR สั่งอาหาร (CLAUDE.md Dining): /Order/{token} → Order/Default.aspx (token = QR ของโต๊ะ 43 ตัว base64url)
            // ลงทะเบียนก่อน FriendlyUrls ให้ชนะ; constraint 43 ตัวกัน /Order/Status, /Order/Default ไม่ถูกจับเป็น token
            routes.MapPageRoute("DiningOrder", "Order/{token}", "~/Order/Default.aspx", false,
                new RouteValueDictionary(), new RouteValueDictionary { { "token", "[A-Za-z0-9_-]{43}" } });

            var settings = new FriendlyUrlSettings();
            settings.AutoRedirectMode = RedirectMode.Permanent;
            routes.EnableFriendlyUrls(settings);
        }
    }
}
