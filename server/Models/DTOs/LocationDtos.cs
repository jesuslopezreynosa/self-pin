namespace LocationServer.Models.DTOs;

public record LocationUpdateRequest(double Latitude, double Longitude, double Accuracy, DateTime? Timestamp);
public record CreateUserRequest(string Name);