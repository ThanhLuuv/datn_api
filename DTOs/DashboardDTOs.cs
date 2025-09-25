namespace BookStore.Api.DTOs;

public class DashboardSummaryDto
{
	public int TotalBooks { get; set; }
}

public class DashboardSummaryResponse
{
	public DashboardSummaryDto Summary { get; set; } = new DashboardSummaryDto();
}


