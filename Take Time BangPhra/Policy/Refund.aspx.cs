namespace Take_Time_BangPhra.PolicyPages
{
    /// <summary>หน้าสาธารณะ นโยบายการคืนเงิน (Refund Policy) — /Policy/Refund</summary>
    public partial class Refund : PolicyPage
    {
        public override string PolicyKey
        {
            get { return BookingPolicy.KeyRefund; }
        }
    }
}
