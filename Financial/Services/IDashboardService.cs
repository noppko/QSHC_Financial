namespace Financial.Services
{
    /// <summary>
    /// Interface สำหรับ Dashboard Service
    /// </summary>
    public interface IDashboardService
    {
        /// <summary>
        /// ดึงข้อมูล KPI สรุปสำหรับ Dashboard
        /// </summary>
        Task<DashboardKpiDto> GetKpiSummaryAsync(DateTime? startDate = null, DateTime? endDate = null);

        /// <summary>
        /// ดึงข้อมูลกราฟแนวโน้มรายเดือน (12 เดือนย้อนหลัง)
        /// </summary>
        Task<DashboardTrendDto> GetMonthlyTrendAsync();

        /// <summary>
        /// ดึงข้อมูลกราฟสัดส่วนตามสิทธิ์การรักษา
        /// </summary>
        Task<DashboardDistributionDto> GetTreatmentRightDistributionAsync(DateTime? startDate = null, DateTime? endDate = null);

        /// <summary>
        /// ดึงรายการล่าสุด 10 รายการ
        /// </summary>
        Task<List<DashboardRecentItemDto>> GetRecentItemsAsync(int count = 10);
    }

    // DTOs สำหรับ Dashboard
    public class DashboardKpiDto
    {
        public int TotalCount { get; set; }
        public int ThisMonthCount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal ThisMonthAmount { get; set; }
        public decimal PercentChange { get; set; }
    }

    public class DashboardTrendDto
    {
        public List<string> Labels { get; set; } = new();
        public List<int> Counts { get; set; } = new();
        public List<decimal> Amounts { get; set; } = new();
    }

    public class DashboardDistributionDto
    {
        public List<string> Labels { get; set; } = new();
        public List<int> Values { get; set; } = new();
    }

    public class DashboardRecentItemDto
    {
        public string PatientName { get; set; } = string.Empty;
        public string HN { get; set; } = string.Empty;
        public string TreatmentRight { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
    }
}
