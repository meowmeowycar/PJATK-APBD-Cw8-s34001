namespace ConsoleApp1.DTOs;

public class BedDto
{
    public int Id { get; set; }
    public BedTypeDto BedType { get; set; } = null!;
    public RoomDto Room { get; set; } = null!;
}
