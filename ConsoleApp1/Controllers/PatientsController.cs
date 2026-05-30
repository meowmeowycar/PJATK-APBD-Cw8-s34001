using ConsoleApp1.DTOs;
using ConsoleApp1.Exceptions;
using ConsoleApp1.Services;
using Microsoft.AspNetCore.Mvc;

namespace ConsoleApp1.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PatientsController(IPatientService patientService) : ControllerBase
{
    // GET /api/patients?search=...
    [HttpGet]
    public async Task<IActionResult> GetPatients([FromQuery] string? search, CancellationToken cancellationToken)
    {
        var patients = await patientService.GetPatientsAsync(search, cancellationToken);
        return Ok(patients);
    }

    // POST /api/patients/{pesel}/bedassignments
    // Uwaga: w treści zadania route to {int:id}, ale Patients ma PK Pesel char(11) — używamy pesel.
    [HttpPost("{pesel}/bedassignments")]
    public async Task<IActionResult> AssignBed(
        [FromRoute] string pesel,
        [FromBody] CreateBedAssignmentDto dto,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var result = await patientService.AssignBedAsync(pesel, dto, cancellationToken);
            return Created($"/api/patients/{pesel}/bedassignments/{result.Id}", result);
        }
        catch (NotFoundExcpetion ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
