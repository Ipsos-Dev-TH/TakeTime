namespace Take_Time_BangPhra.PolicyPages
{
    /// <summary>หน้าสาธารณะ นโยบายการนำสัตว์เลี้ยงเข้าพัก (Pet Policy) — /Policy/Pet</summary>
    public partial class Pet : PolicyPage
    {
        public override string PolicyKey
        {
            get { return BookingPolicy.KeyPet; }
        }
    }
}
