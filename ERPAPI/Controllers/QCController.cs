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
                return BadRequest("QuantitySheetId is required.");

            Console.WriteLine($"Received QC with QuantitySheetId: {qc.QuantitySheetId}");
            Console.WriteLine($"qc.Status.HasValue: {qc.Status.HasValue}, qc.Status: {qc.Status}");

            var existingQC = _context.QC
                .FirstOrDefault(x => x.QuantitySheetId == qc.QuantitySheetId);

            if (existingQC != null)
            {
                // Update fields
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
            }
            else
            {
                // Add new QC
                _context.QC.Add(qc);
            }

            // Save QC changes (either added or updated)
            await _context.SaveChangesAsync();

            // ✅ After saving, check status and update MSSStatus if needed
            if (qc.Status.HasValue && qc.Status.Value == true)
            {
                Console.WriteLine($"Status is true, updating MSSStatus.");

                var quantitySheetsToUpdate = _context.QuantitySheets
                    .Where(q => q.QuantitySheetId == qc.QuantitySheetId)
                    .ToList();
                Console.WriteLine($"Found {quantitySheetsToUpdate.Count} QuantitySheet(s) to update.");

                if (quantitySheetsToUpdate.Any())
                {
                    quantitySheetsToUpdate.ForEach(q => q.MSSStatus = 3);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    Console.WriteLine($"No QuantitySheet found with QuantitySheetId {qc.QuantitySheetId}");
                }
            }
            else
            {

                var quantitySheetsToUpdate = _context.QuantitySheets
                    .Where(q => q.QuantitySheetId == qc.QuantitySheetId)
                    .ToList();
                Console.WriteLine($"Found {quantitySheetsToUpdate.Count} QuantitySheet(s) to update.");

                if (quantitySheetsToUpdate.Any())
                {
                    quantitySheetsToUpdate.ForEach(q => q.MSSStatus = 4);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    Console.WriteLine($"No QuantitySheet found with QuantitySheetId {qc.QuantitySheetId}");
                }

            }

            return Ok(qc);
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
            var project = await _context.Projects
                                .FirstOrDefaultAsync(p => p.ProjectId == projectId);
            var abcd = await _context.ABCD
                                .Where(p => p.GroupId == project.GroupId)
                                .FirstOrDefaultAsync();
            var subjects = await _context.Subjects.ToDictionaryAsync(s => s.SubjectId, s => s.SubjectName);
            var courses = await _context.Courses.ToDictionaryAsync(c => c.CourseId, c => c.CourseName);
            var sessions = await _context.Sessions.ToDictionaryAsync(s => s.SessionId, s => s.session);
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
                             x.QuantitySheet.StructureOfPaper,
                             Av = abcd != null ? ResolveTemplate(abcd.A, x.QuantitySheet, project, subjects, courses, sessions, abcd.SessionFormat) : null,
                             Bv = abcd != null ? GetPropertyValue(x.QuantitySheet, abcd.B, subjects, courses, sessions) : null,
                             Cv = abcd != null ? GetPropertyValue(x.QuantitySheet, abcd.C, subjects, courses, sessions) : null,
                             Dv = abcd != null ? GetPropertyValue(x.QuantitySheet, abcd.D, subjects, courses, sessions) : null,
                             QCId = qc?.QCId, 
                             Languages = qc?.Language,
                             MaximumMarks = qc?.MaxMarks,
                             Durations = qc?.Duration,
                             Status = qc?.Status,
                             TotalQuestions = qc?.TotalQuestions,
                             SummationofMarksEqualsTotalMarks = qc?.SummationofMarksEqualsTotalMarks,
                             StructureOfPapers = qc?.StructureOfPaper,
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
        public async Task<IActionResult> GetQCByProject(int projectId, int pagesize, int currentpage)
        {
            // Fetch QuantitySheets for the given projectId
            var quantitySheets = await _context.QuantitySheets
                                                .Where(q => q.ProjectId == projectId && q.MSSStatus >=2)
                                                .ToListAsync();

            var project = await _context.Projects
                                .FirstOrDefaultAsync(p => p.ProjectId == projectId);

            var abcd = await _context.ABCD
                              .Where(p => p.GroupId == project.GroupId)
                              .FirstOrDefaultAsync();
            var subjects = await _context.Subjects.ToDictionaryAsync(s => s.SubjectId, s => s.SubjectName);
            var courses = await _context.Courses.ToDictionaryAsync(c => c.CourseId, c => c.CourseName);
            var sessions = await _context.Sessions.ToDictionaryAsync(s => s.SessionId, s => s.session);
            var languageIds = quantitySheets
           .SelectMany(q => q.LanguageId) // Assuming LanguageId is a List<int>
           .Distinct() // Get distinct LanguageIds
           .ToList();

            // Step 3: Fetch Languages based on the LanguageIds
            var languages = await _context.Languages
                .Where(l => languageIds.Contains(l.LanguageId))
                .Select(l => new { l.LanguageId, l.Languages }) // Adjust the property names as needed
                .ToListAsync();
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
                    StructureOfPaper = qs.StructureOfPaper,
                    MSSStatus = qs.MSSStatus,
                    Language = languages.Where(l => qs.LanguageId.Contains(l.LanguageId)).Select(l => l.Languages).ToList(),
                    A = abcd != null ? ResolveTemplate(abcd.A, qs, project, subjects, courses, sessions, abcd.SessionFormat) : null,
                    B = abcd != null ? GetPropertyValue(qs, abcd.B, subjects, courses, sessions) : null,
                    C = abcd != null ? GetPropertyValue(qs, abcd.C, subjects, courses, sessions) : null,
                    D = abcd != null ? GetPropertyValue(qs, abcd.D, subjects, courses, sessions) : null,
                    Series = project.SeriesName, // Default series, adjust as needed
                    Verified = qcGroup.Any() ? new
                    {
                        // Use actual values from the QC group if available
                        Language = qcGroup.FirstOrDefault()?.Language ?? false,  // Use actual value or false if null
                        MaxMarks = qcGroup.FirstOrDefault()?.MaxMarks ?? false,  // Use actual value or false if null
                        Duration = qcGroup.FirstOrDefault()?.Duration ?? false,  // Use actual value or false if null
                        StructureOfPaper = qcGroup.FirstOrDefault()?.StructureOfPaper ?? false,  // Use actual value or false if null
                        Series = qcGroup.FirstOrDefault()?.Series ?? false,  // Use actual value or false if null
                        Status = qcGroup.FirstOrDefault()?.Status ?? false, // Use actual Status or 0 if null
                        A = qcGroup.FirstOrDefault()?.A ?? false,
                        B = qcGroup.FirstOrDefault()?.B ?? false,
                        C = qcGroup.FirstOrDefault()?.C ?? false,
                        D = qcGroup.FirstOrDefault()?.D ?? false,
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
         x.MSSStatus,
         x.StructureOfPaper,
         x.Language,
         x.A,
         x.B,
         x.C,
         x.D,
         Verified = x.Verified // Return Verified with actual values from QC
     })
     .ToList();

            return Ok(result);
        }

        private string ResolveTemplate(string template, object quantitySheet, Project project,
    Dictionary<int, string> subjects,
    Dictionary<int, string> courses,
    Dictionary<int, string> sessions,
     string sessionFormat)
        {
            var tokens = template.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var resolvedParts = new List<string>();

            foreach (var token in tokens)
            {
                if (token.EndsWith("Id", StringComparison.OrdinalIgnoreCase))
                {
                    object idValue = null;

                    // Check if it's in QuantitySheet
                    var prop = quantitySheet.GetType().GetProperty(token);
                    if (prop != null)
                    {
                        idValue = prop.GetValue(quantitySheet);
                    }
                    // Or in Project
                    else if (project != null)
                    {
                        prop = project.GetType().GetProperty(token);
                        if (prop != null)
                        {
                            idValue = prop.GetValue(project);
                        }
                    }

                    if (idValue != null)
                    {
                        var id = Convert.ToInt32(idValue);

                        if (token == "SubjectId" && subjects.TryGetValue(id, out var subjectName))
                            resolvedParts.Add(subjectName);
                        else if (token == "CourseId" && courses.TryGetValue(id, out var courseName))
                            resolvedParts.Add(courseName);
                        else if (token == "SessionId" && sessions.TryGetValue(id, out var sessionName))
                            resolvedParts.Add(FormatSessionName(sessionName, sessionFormat));
                        else
                            resolvedParts.Add(id.ToString()); // fallback to raw ID
                    }
                }
                else
                {
                    // Literal text
                    resolvedParts.Add(token);
                }
            }

            return string.Join(" ", resolvedParts);
        }
        private string FormatSessionName(string sessionName, string format)
        {
            if (string.IsNullOrEmpty(sessionName) || !sessionName.Contains('-'))
                return sessionName;

            var parts = sessionName.Split('-');
            if (parts.Length != 2) return sessionName;

            var fullStart = parts[0]; // e.g. 2023
            var fullEnd = parts[1];   // e.g. 2024
            var shortStart = fullStart.Substring(2); // e.g. 23
            var shortEnd = fullEnd.Substring(2);     // e.g. 24

            return format switch
            {
                "2022-23" => $"{fullStart}-{shortEnd}",
                "22-23" => $"{shortStart}-{shortEnd}",
                "22-2023" => $"{shortStart}-{fullEnd}",
                _ => $"{fullStart}-{fullEnd}",
            };
        }

        private object GetPropertyValue(object obj, string propertyName,
     Dictionary<int, string> subjects,
     Dictionary<int, string> courses,
     Dictionary<int, string> sessions)
        {
            var value = obj?.GetType().GetProperty(propertyName)?.GetValue(obj, null);

            if (value == null) return null;

            if (propertyName.EndsWith("Id"))
            {
                var id = Convert.ToInt32(value);

                if (propertyName == "SubjectId" && subjects.ContainsKey(id))
                    return subjects[id];
                if (propertyName == "CourseId" && courses.ContainsKey(id))
                    return courses[id];
                if (propertyName == "SessionId" && sessions.ContainsKey(id))
                    return sessions[id];
                // Add more if you have other Ids like PaperId, DepartmentId, etc.
            }

            return value;
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
