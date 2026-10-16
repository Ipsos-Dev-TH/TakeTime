namespace Take_Time_BangPhra.PolicyPages
{
    /// <summary>หน้าสาธารณะ นโยบายการยกเลิกการจอง (Cancellation Policy) — /Policy/Cancellation</summary>
    public partial class Cancellation : PolicyPage
    {
        public override string PolicyKey
        {
            get { return BookingPolicy.KeyCancellation; }
        }
    }
}
