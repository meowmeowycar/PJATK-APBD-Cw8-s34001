namespace ConsoleApp1.DTOs;

public class RoomDto
{
    public string Id { get; set; } = null!;
    public bool HasTv { get; set; }
    public WardDto Ward { get; set; } = null!;
}
