using ConsoleApp1.Data;
using ConsoleApp1.DTOs;
using ConsoleApp1.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ConsoleApp1.Services;

public class PatientService(HospitalContext context) : IPatientService
{
    public async Task<List<PatientDto>> GetPatientsAsync(string? search, CancellationToken cancellationToken)
    {
        var query = context.Patients
            .Include(p => p.Admissions)
                .ThenInclude(a => a.Ward)
            .Include(p => p.BedAssignments)
                .ThenInclude(ba => ba.Bed)
                    .ThenInclude(b => b.BedType)
            .Include(p => p.BedAssignments)
                .ThenInclude(ba => ba.Bed)
                    .ThenInclude(b => b.Room)
                        .ThenInclude(r => r.Ward)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search}%";
            query = query.Where(p =>
                EF.Functions.Like(p.FirstName, pattern) ||
                EF.Functions.Like(p.LastName, pattern));
        }

        var patients = await query.ToListAsync(cancellationToken);

        return patients.Select(MapPatient).ToList();
    }

    public async Task<BedAssignmentDto> AssignBedAsync(string pesel, CreateBedAssignmentDto dto,
        CancellationToken cancellationToken)
    {
        var patient = await context.Patients
            .FirstOrDefaultAsync(p => p.Pesel == pesel, cancellationToken);

        if (patient is null)
        {
            throw new NotFoundExcpetion($"Patient with pesel '{pesel}' was not found.");
        }

        var ward = await context.Wards
            .FirstOrDefaultAsync(w => w.Name == dto.Ward, cancellationToken);

        if (ward is null)
        {
            throw new NotFoundExcpetion($"Ward with name '{dto.Ward}' was not found.");
        }

        var bedType = await context.BedTypes
            .FirstOrDefaultAsync(bt => bt.Name == dto.BedType, cancellationToken);

        if (bedType is null)
        {
            throw new NotFoundExcpetion($"BedType with name '{dto.BedType}' was not found.");
        }

        if (dto.To.HasValue && dto.To.Value <= dto.From)
        {
            throw new ArgumentException("'To' must be greater than 'From'.");
        }

        // Szukamy łóżka:
        //  - w pokoju, który należy do żądanego oddziału
        //  - o żądanym typie
        //  - które nie ma kolidującego przypisania w żądanym przedziale [From, To)
        //    Kolizja: istniejący.From < newTo  AND  (istniejący.To IS NULL OR istniejący.To > newFrom)
        var newFrom = dto.From;
        var newTo = dto.To;

        var freeBed = await context.Beds
            .Where(b => b.BedTypeId == bedType.Id)
            .Where(b => b.Room.WardId == ward.Id)
            .Where(b => !b.BedAssignments.Any(ba =>
                ba.From < (newTo ?? DateTime.MaxValue) &&
                (ba.To == null || ba.To > newFrom)))
            .FirstOrDefaultAsync(cancellationToken);

        if (freeBed is null)
        {
            throw new NotFoundExcpetion(
                $"No free bed of type '{dto.BedType}' in ward '{dto.Ward}' available in the requested time range.");
        }

        var assignment = new BedAssignment
        {
            PatientPesel = patient.Pesel,
            BedId = freeBed.Id,
            From = dto.From,
            To = dto.To
        };

        context.BedAssignments.Add(assignment);
        await context.SaveChangesAsync(cancellationToken);

        // Doładowanie nawigacji do zwrócenia DTO
        await context.Entry(assignment).Reference(a => a.Bed).LoadAsync(cancellationToken);
        await context.Entry(assignment.Bed).Reference(b => b.BedType).LoadAsync(cancellationToken);
        await context.Entry(assignment.Bed).Reference(b => b.Room).LoadAsync(cancellationToken);
        await context.Entry(assignment.Bed.Room).Reference(r => r.Ward).LoadAsync(cancellationToken);

        return MapBedAssignment(assignment);
    }

    private static PatientDto MapPatient(Patient p) => new()
    {
        Pesel = p.Pesel,
        FirstName = p.FirstName,
        LastName = p.LastName,
        Age = p.Age,
        Sex = p.Sex ? "Male" : "Female",
        Admissions = p.Admissions
            .Select(a => new AdmissionDto
            {
                Id = a.Id,
                AdmissionDate = a.AdmissionDate,
                DischargeDate = a.DischargeDate,
                Ward = new WardDto
                {
                    Id = a.Ward.Id,
                    Name = a.Ward.Name,
                    Description = a.Ward.Description
                }
            })
            .ToList(),
        BedAssignments = p.BedAssignments
            .Select(MapBedAssignment)
            .ToList()
    };

    private static BedAssignmentDto MapBedAssignment(BedAssignment ba) => new()
    {
        Id = ba.Id,
        From = ba.From,
        To = ba.To,
        Bed = new BedDto
        {
            Id = ba.Bed.Id,
            BedType = new BedTypeDto
            {
                Id = ba.Bed.BedType.Id,
                Name = ba.Bed.BedType.Name,
                Description = ba.Bed.BedType.Description
            },
            Room = new RoomDto
            {
                Id = ba.Bed.Room.Id,
                HasTv = ba.Bed.Room.HasTv,
                Ward = new WardDto
                {
                    Id = ba.Bed.Room.Ward.Id,
                    Name = ba.Bed.Room.Ward.Name,
                    Description = ba.Bed.Room.Ward.Description
                }
            }
        }
    };
}
