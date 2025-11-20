using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Optimization;
using System.Web.Routing;
using System.Web.Security;
using System.Web.SessionState;
using System.Globalization;
using System.Threading;

namespace Take_Time_BangPhra
{
    public class Global : HttpApplication
    {
        void Application_Start(object sender, EventArgs e)
        {
            // Code that runs on application startup
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }

        /// <summary>
        /// Force Gregorian calendar for all requests
        /// Ensures Christian year (2025) is used instead of Buddhist year (2568)
        /// While maintaining Thai language and date format
        /// </summary>
        protected void Application_BeginRequest(object sender, EventArgs e)
        {
            // Create Thai culture with Gregorian calendar
            CultureInfo culture = new CultureInfo("th-TH");

            // CRITICAL: Override calendar to Gregorian (Christian year)
            // Without this, th-TH uses ThaiBuddhistCalendar by default (Buddhist year)
            culture.DateTimeFormat.Calendar = new GregorianCalendar();

            // Apply to current thread (affects all date/time operations for this request)
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
        }
    }
}