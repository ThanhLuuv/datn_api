using System.ComponentModel.DataAnnotations;

namespace BookStore.Api.DTOs;

public class AreaDto
{
    public long AreaId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Keywords { get; set; }
}

public class AreaListResponse
{
    public List<AreaDto> Areas { get; set; } = new();
    public int TotalCount { get; set; }
}
