using ERPAPI.Data;
using ERPAPI.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ERPAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class QCController : ControllerBase
    {
        private readonly AppDbContext _context;

        public QCController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> CreateQC([FromBody] QC qc)
        {
            if (qc == null)
                return BadRequest("Invalid QC data.");

            if (qc.QuantitySheetId == 0)
            {
                return BadRequest("QuantitySheetId is required.");
            }

            // Check if the QC entry already exists for the QuantitySheetId
            var existingQC = _context.QC
                                    .FirstOrDefault(x => x.QuantitySheetId == qc.QuantitySheetId);

            if (existingQC != null)
            {
                if (qc.Language.HasValue)
                    existingQC.Language = qc.Language.Value;
                if (qc.MaxMarks.HasValue)
                    existingQC.MaxMarks = qc.MaxMarks.Value;
                if (qc.Duration.HasValue)
                    existingQC.Duration = qc.Duration.Value;
                if (qc.Status.HasValue)
                    existingQC.Status = qc.Status.Value;
                if (qc.TotalQuestions.HasValue)
                    existingQC.TotalQuestions = qc.TotalQuestions.Value;
                if (qc.SummationofMarksEqualsTotalMarks.HasValue)
                    existingQC.SummationofMarksEqualsTotalMarks = qc.SummationofMarksEqualsTotalMarks.Value;
                if (qc.StructureOfPaper.HasValue)
                    existingQC.StructureOfPaper = qc.StructureOfPaper.Value;
                if (qc.Series.HasValue)
                    existingQC.Series = qc.Series.Value;
                if (qc.A.HasValue)
                    existingQC.A = qc.A.Value;
                if (qc.B.HasValue)
                    existingQC.B = qc.B.Value;
                if (qc.C.HasValue)
                    existingQC.C = qc.C.Value;
                if (qc.D.HasValue)
                    existingQC.D = qc.D.Value;
                // Save changes for the existing QC entry
                await _context.SaveChangesAsync();
                return Ok(existingQC); // Return the updated QC
            }
            else
            {
                // If the entry does not exist, add a new QC
                _context.QC.Add(qc);
                await _context.SaveChangesAsync();
                return CreatedAtAction(nameof(GetQCById), new { id = qc.QCId }, qc);
            }
        }


        [HttpGet("{id}")]
        public async Task<IActionResult> GetQCById(int id)
        {
            var qc = await _context.QC.FindAsync(id);
            if (qc == null)
                return NotFound("QC not found.");

            return Ok(qc);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllQC(int projectId)
        {
            // Fetch all QuantitySheets for the given ProjectId
            var quantitySheets = await _context.QuantitySheets
                                                .Where(p => p.ProjectId == projectId)
                                                .ToListAsync();

            // Perform a left join with the QC table based on QuantitySheetId
            var result = quantitySheets
                         .GroupJoin(_context.QC,
                                    qs => qs.QuantitySheetId,   // Key from QuantitySheets
                                    qc => qc.QuantitySheetId,   // Key from QC
                                    (qs, qcGroup) => new        // Project a new object
                                    {
                                        QuantitySheet = qs,
                                        QCs = qcGroup.DefaultIfEmpty() // This ensures all QuantitySheets are included even if no match is found
                                    })
                         .SelectMany(x => x.QCs.DefaultIfEmpty(), (x, qc) => new // Flatten the result and include the QC if exists
                         {
                             x.QuantitySheet.QuantitySheetId,
                             x.QuantitySheet.ProjectId,
                             x.QuantitySheet.CatchNo,
                             x.QuantitySheet.LanguageId,
                             x.QuantitySheet.MaxMarks,
                             x.QuantitySheet.Duration,
                             QCId = qc?.QCId, 
                             Languages = qc?.Language,
                             MaximumMarks = qc?.MaxMarks,
                             Durations = qc?.Duration,
                             Status = qc?.Status,
                             TotalQuestions = qc?.TotalQuestions,
                             SummationofMarksEqualsTotalMarks = qc?.SummationofMarksEqualsTotalMarks,
                             StructureOfPaper = qc?.StructureOfPaper,
                             Series = qc?.Series,
                             A = qc?.A,
                             B = qc?.B,
                             C = qc?.C,
                             D = qc?.D
                         })
                         .ToList();

            return Ok(result); // Return the joined result
        }

        [HttpGet("ByProject")]
        public async Task<IActionResult> GetQCByProject(int projectId)
        {
            // Fetch QuantitySheets for the given projectId
            var quantitySheets = await _context.QuantitySheets
                                                .Where(q => q.ProjectId == projectId && q.MSSStatus ==2)
                                                .ToListAsync();

            var project = await _context.Projects
                                .Where(p => p.ProjectId == projectId)
                                .Select(p => new { p.SeriesName })
                                .FirstOrDefaultAsync();

            if (project == null)
            {
                return NotFound($"Project with ID {projectId} not found.");
            }

            // Perform a left join with the QC table based on QuantitySheetId
            var result = quantitySheets
     .GroupJoin(_context.QC,
                qs => qs.QuantitySheetId,  // Key from QuantitySheets
                qc => qc.QuantitySheetId,  // Key from QC
                (qs, qcGroup) => new       // Create a new object with QuantitySheet and QC
                {
                    CatchNo = qs.CatchNo,
                    QuantitysheetId = qs.QuantitySheetId,
                    MaxMarks = qs.MaxMarks,
                    Duration = qs.Duration,


                    LanguageId = qs.LanguageId,


                    Language = qs.LanguageId,

                    Series = project.SeriesName, // Default series, adjust as needed
                    Verified = qcGroup.Any() ? new
                    {
                        // Use actual values from the QC group if available
                        CatchNo = qs.CatchNo,  // Use actual value or false if null
                        Language = qcGroup.FirstOrDefault()?.Language ?? false,  // Use actual value or false if null
                        MaxMarks = qcGroup.FirstOrDefault()?.MaxMarks ?? false,  // Use actual value or false if null
                        Duration = qcGroup.FirstOrDefault()?.Duration ?? false,  // Use actual value or false if null
                        Structure = qcGroup.FirstOrDefault()?.StructureOfPaper ?? false,  // Use actual value or false if null
                        Series = qcGroup.FirstOrDefault()?.Series ?? false,  // Use actual value or false if null
                        Status = qcGroup.FirstOrDefault()?.Status ?? false // Use actual Status or 0 if null
                    } : null, // Only include Verified if qcGroup exists
                    QC = qcGroup.FirstOrDefault() // Get the first QC object or null if no match
                })
    
     .Select(x => new
     {
         x.CatchNo,
         x.QuantitysheetId,
         x.LanguageId,  // Ensure Language is an array, empty if null
         x.Duration,
         x.MaxMarks,
         x.Series,
         Verified = x.Verified // Return Verified with actual values from QC
     })
     .ToList();

            return Ok(result);
        }


        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateQC(int id, [FromBody] QC updatedQC)
        {
            if (id != updatedQC.QuantitySheetId)
                return BadRequest("QSID mismatch.");

            var existingQC = await _context.QC.FindAsync(id);
            if (existingQC == null)
                return NotFound("QC not found.");

            existingQC.Language = updatedQC.Language;
            existingQC.MaxMarks = updatedQC.MaxMarks;
            existingQC.Duration = updatedQC.Duration;
            existingQC.Status = updatedQC.Status;
            existingQC.TotalQuestions = updatedQC.TotalQuestions;
            existingQC.SummationofMarksEqualsTotalMarks = updatedQC.SummationofMarksEqualsTotalMarks;
            existingQC.StructureOfPaper = updatedQC.StructureOfPaper;
            existingQC.Series = updatedQC.Series;
            existingQC.A = updatedQC.A;
            existingQC.B = updatedQC.B;
            existingQC.C = updatedQC.C;
            existingQC.D = updatedQC.D;
            existingQC.ProjectId = updatedQC.ProjectId;
            existingQC.QuantitySheetId = updatedQC.QuantitySheetId;

            await _context.SaveChangesAsync();
            return Ok(existingQC);
        }
    }
}
