namespace LocationServer.Models;

public class LocationEntry
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Accuracy { get; set; }
    public DateTime Timestamp { get; set; }
}