using ConsoleApp1.DTOs;

namespace ConsoleApp1.Services;

public interface IPatientService
{
    Task<List<PatientDto>> GetPatientsAsync(string? search, CancellationToken cancellationToken);

    Task<BedAssignmentDto> AssignBedAsync(string pesel, CreateBedAssignmentDto dto, CancellationToken cancellationToken);
}
