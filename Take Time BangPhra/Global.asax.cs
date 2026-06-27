using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Optimization;
using System.Web.Routing;
using System.Web.Security;
using System.Web.SessionState;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Take_Time_BangPhra.Integration;

namespace Take_Time_BangPhra
{
    public class Global : HttpApplication
    {
        private static Timer _accountingSyncTimer;
        private static readonly object _syncLock = new object();
        private static bool _isSyncing = false;

        void Application_Start(object sender, EventArgs e)
        {
            // Enforce TLS 1.2 for all outbound HTTPS connections.
            // .NET Framework defaults to SSL3/TLS1.0 which modern APIs (Nexaacc) reject.
            // This fixes "An error occurred while sending the request" errors.
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12
                | SecurityProtocolType.Tls11
                | SecurityProtocolType.Tls;

            // Increase default connection limit to prevent throttling on concurrent API calls
            ServicePointManager.DefaultConnectionLimit = 100;

            // Disable Nagle's algorithm for better responsiveness on small API requests
            ServicePointManager.UseNagleAlgorithm = false;
            ServicePointManager.Expect100Continue = false;

            // Code that runs on application startup
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            // Start accounting sync background timer
            StartAccountingSyncTimer();
        }

        void Application_End(object sender, EventArgs e)
        {
            // Dispose timer on app shutdown
            _accountingSyncTimer?.Dispose();
        }

        /// <summary>
        /// Background timer ที่ process accounting sync queue อัตโนมัติ
        /// ทำงานตาม interval ที่ตั้งค่าใน Accounting_Integration_Config (default 30 วินาที)
        /// </summary>
        private static void StartAccountingSyncTimer()
        {
            try
            {
                var config = new AccountingConfig();
                if (!config.IsReadyToSync) return;

                int intervalMs = config.SyncIntervalSeconds * 1000;
                if (intervalMs < 10000) intervalMs = 30000; // minimum 10 seconds

                _accountingSyncTimer = new Timer(ProcessAccountingSyncQueue, null, 60000, intervalMs); // delay 60s before first run
            }
            catch
            {
                // Config table may not exist yet — silently skip
            }
        }

        private static int _consecutiveTimerErrors = 0;

        private static void ProcessAccountingSyncQueue(object state)
        {
            if (_isSyncing) return;

            lock (_syncLock)
            {
                if (_isSyncing) return;
                _isSyncing = true;
            }

            try
            {
                var syncService = new AccountingSyncService();
                var task = syncService.ProcessQueueAsync(20);
                task.Wait(TimeSpan.FromMinutes(2));

                // รวบยอดขายหน้าร้านที่ไม่ออกใบกำกับเป็นใบรับเงินสดสรุปรายวัน (auto, ไม่ต้องกด)
                // — no-op ถ้า config ปิด; แยก try กันพังเฉพาะส่วนนี้ ไม่กระทบ queue หลัก
                try { syncService.RollupPosDailySalesIfDue(); }
                catch (Exception rex) { System.Diagnostics.Trace.TraceError($"PosDailyRollup timer error: {rex.Message}"); }

                _consecutiveTimerErrors = 0;
            }
            catch (AggregateException aex)
            {
                var inner = aex.InnerException ?? aex;
                _consecutiveTimerErrors++;
                // Only log every 10th consecutive error to avoid log flooding during outages
                if (_consecutiveTimerErrors <= 3 || _consecutiveTimerErrors % 10 == 0)
                    System.Diagnostics.Trace.TraceError($"Accounting sync timer error (#{_consecutiveTimerErrors}): {inner.Message}");
            }
            catch (Exception ex)
            {
                _consecutiveTimerErrors++;
                if (_consecutiveTimerErrors <= 3 || _consecutiveTimerErrors % 10 == 0)
                    System.Diagnostics.Trace.TraceError($"Accounting sync timer error (#{_consecutiveTimerErrors}): {ex.Message}");
            }
            finally
            {
                _isSyncing = false;
            }
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