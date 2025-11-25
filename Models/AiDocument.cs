using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BookStore.Api.Models;

[Table("ai_documents")]
public class AiDocument
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("ref_type")]
    public string RefType { get; set; } = string.Empty;

    [Required]
    [MaxLength(120)]
    [Column("ref_id")]
    public string RefId { get; set; } = string.Empty;

    [Required]
    [Column("content", TypeName = "text")]
    public string Content { get; set; } = string.Empty;

    [Required]
    [Column("embedding_json", TypeName = "json")]
    public string EmbeddingJson { get; set; } = string.Empty;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}





