namespace Take_Time_BangPhra.PolicyPages
{
    /// <summary>หน้าสาธารณะ นโยบายความเป็นส่วนตัว (Privacy Policy) — /Policy/Privacy</summary>
    public partial class Privacy : PolicyPage
    {
        public override string PolicyKey
        {
            get { return BookingPolicy.KeyPrivacy; }
        }
    }
}
