using ERPAPI.Data;
using ERPAPI.Model;
using ERPAPI.Service;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using NuGet.Protocol.Plugins;
using Microsoft.CodeAnalysis;
using Microsoft.AspNetCore.Authorization;
using ERPAPI.Services;
using Microsoft.CodeAnalysis.Host;


[ApiController]
[Route("api/[controller]")]
public class QuantitySheetController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ProcessService _processService;
    private readonly ILoggerService _loggerService;

    public QuantitySheetController(AppDbContext context, ProcessService processService, ILoggerService loggerService)
    {
        _context = context;
        _processService = processService;
        _loggerService = loggerService;
    }

    [Authorize]

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] List<QuantitySheet> newSheets)
    {
        // Check if ModelState is valid
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            Console.WriteLine("ModelState Errors: " + string.Join(", ", errors)); // Output model validation errors
            return BadRequest(errors);  // Send validation errors as response
        }

        if (newSheets == null || !newSheets.Any())
        {
            Console.WriteLine("No data provided.");
            return BadRequest("No data provided.");
        }

        var projectId = newSheets.First().ProjectId;
        Console.WriteLine($"Received ProjectId: {projectId}");

        var project = await _context.Projects
            .Where(p => p.ProjectId == projectId)
            .Select(p => new { p.TypeId, p.NoOfSeries })
            .FirstOrDefaultAsync();

        if (project == null)
        {
            Console.WriteLine($"Project with ProjectId {projectId} not found.");
            return BadRequest("Project not found.");
        }

        var projectTypeId = project.TypeId;
        var projectType = await _context.Types
            .Where(t => t.TypeId == projectTypeId)
            .Select(t => t.Types)
            .FirstOrDefaultAsync();

        Console.WriteLine($"Project Type: {projectType}");

        if (projectType == "Booklet" && project.NoOfSeries.HasValue)
        {
            var noOfSeries = project.NoOfSeries.Value;


            Console.WriteLine($"Project has {noOfSeries} series.");
            if (noOfSeries == 0)
            {
                noOfSeries = 1;



            }
            var adjustedSheets = new List<QuantitySheet>();

            foreach (var sheet in newSheets)
            {
                var adjustedQuantity = sheet.Quantity / noOfSeries;
                Console.WriteLine($"Adjusted Quantity for CatchNo {sheet.CatchNo}: {adjustedQuantity}");

                for (int i = 0; i < noOfSeries; i++)
                {
                    var newSheet = new QuantitySheet
                    {
                        CatchNo = sheet.CatchNo,
                        PaperTitle = sheet.PaperTitle,
                        PaperNumber = sheet.PaperNumber,
                        CourseId = sheet.CourseId,
                        SubjectId = sheet.SubjectId,
                        InnerEnvelope = sheet.InnerEnvelope,
                        OuterEnvelope = sheet.OuterEnvelope,
                        LotNo = sheet.LotNo,
                        Quantity = adjustedQuantity,
                        Pages = sheet.Pages,
                        PercentageCatch = 0,
                        ProjectId = sheet.ProjectId,
                        Status = sheet.Status,
                        ExamDate = sheet.ExamDate,
                        ExamTime = sheet.ExamTime,
                        ProcessId = new List<int>(), // Start with an empty list for the new catch
                        StopCatch = 0,
                        StructureOfPaper = sheet.StructureOfPaper,
                        TTFStatus = sheet.TTFStatus,
                        MSSStatus = sheet.MSSStatus,
                        QPId = sheet.QPId,
                        MaxMarks = sheet.MaxMarks,
                        Duration = sheet.Duration,
                        LanguageId = sheet.LanguageId,
                        ExamTypeId = sheet.ExamTypeId,
                        NEPCode = sheet.NEPCode,
                        UniqueCode = sheet.UniqueCode,
                    };
                    adjustedSheets.Add(newSheet);
                }
            }
            newSheets = adjustedSheets;
        }

        foreach (var sheet in newSheets)
        {
            if (string.IsNullOrWhiteSpace(sheet.LotNo))
            {
                Console.WriteLine($"LotNo is missing for CatchNo {sheet.CatchNo}.");
                return BadRequest($"The LotNo field is required for sheet with CatchNo: {sheet.CatchNo}.");
            }
        }

        var existingSheets = await _context.QuantitySheets
            .Where(s => s.ProjectId == projectId && newSheets.Select(ns => ns.LotNo).Contains(s.LotNo))
            .ToListAsync();

        Console.WriteLine($"Found {existingSheets.Count} existing sheets with matching LotNo.");

        var processedNewSheets = new List<QuantitySheet>();

        foreach (var sheet in newSheets)
        {
            sheet.ProcessId.Clear();
            _processService.ProcessCatch(sheet); // Ensure this is working as expected
            Console.WriteLine($"Processed sheet with CatchNo {sheet.CatchNo}");
            processedNewSheets.Add(sheet);
        }

        var allSheets = existingSheets.Concat(processedNewSheets).ToList();
        Console.WriteLine($"Total sheets after processing: {allSheets.Count}");

        var groupedSheets = allSheets.GroupBy(sheet => sheet.LotNo);

        foreach (var group in groupedSheets)
        {
            double totalQuantityForLot = group.Sum(sheet => sheet.Quantity);
            Console.WriteLine($"Total quantity for LotNo {group.Key}: {totalQuantityForLot}");

            if (totalQuantityForLot == 0)
            {
                Console.WriteLine($"Total quantity for lot {group.Key} is zero, cannot calculate percentages.");
                return BadRequest($"Total quantity for lot {group.Key} is zero, cannot calculate percentages.");
            }

            foreach (var sheet in group)
            {
                sheet.PercentageCatch = (sheet.Quantity / totalQuantityForLot) * 100;
                Console.WriteLine($"Calculated PercentageCatch for CatchNo {sheet.CatchNo}: {sheet.PercentageCatch}");

                if (!processedNewSheets.Contains(sheet))
                {
                    continue;
                }
            }
        }

        Console.WriteLine($"Saving {processedNewSheets.Count} processed sheets to the database.");
        await _context.QuantitySheets.AddRangeAsync(processedNewSheets);
        await _context.SaveChangesAsync();

        // Log the project ID only once
        _loggerService.LogEvent(
            "New QuantitySheet added",
            "QuantitySheet",
            1, // Replace with actual user ID or triggered by value
            null,
            $"ProjectId: {projectId}"
        );

        return Ok(processedNewSheets);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<QuantitySheet>> GetById(int id)
    {
        var quantitySheet = await _context.QuantitySheets.Where(q=> q.QuantitySheetId == id).ToListAsync();

        if (quantitySheet == null)
        {
            return NotFound($"QuantitySheet with ID {id} not found.");
        }

        return Ok(quantitySheet);
    }

    //[Authorize]
    [HttpPut("update/{id}")]
    public async Task<IActionResult> Put(int id, [FromBody] QuantitySheet updatedSheet)
    {
        if (updatedSheet == null)
        {
            return BadRequest("No data provided.");
        }

        // Retrieve the existing QuantitySheet from the database
        var existingSheet = await _context.QuantitySheets.FirstOrDefaultAsync(sheet => sheet.QuantitySheetId == id);
        Console.WriteLine($"Updating QuantitySheet with ID {id}");

        if (existingSheet == null)
        {
            return NotFound("QuantitySheet not found.");
        }

        // Update the fields with the new values
        existingSheet.PaperTitle = updatedSheet.PaperTitle;
        existingSheet.PaperNumber = updatedSheet.PaperNumber;
        existingSheet.CourseId = updatedSheet.CourseId;
        existingSheet.SubjectId = updatedSheet.SubjectId;
        existingSheet.ExamDate = updatedSheet.ExamDate;
        existingSheet.ExamTime = updatedSheet.ExamTime;
        existingSheet.InnerEnvelope = updatedSheet.InnerEnvelope;
        existingSheet.OuterEnvelope = updatedSheet.OuterEnvelope;
        existingSheet.Quantity = updatedSheet.Quantity;
        existingSheet.LotNo = updatedSheet.LotNo;
        existingSheet.MaxMarks = updatedSheet.MaxMarks;
        existingSheet.Duration = updatedSheet.Duration;
        existingSheet.LanguageId.Clear();
        existingSheet.LanguageId.AddRange(updatedSheet.LanguageId);
        existingSheet.NEPCode = updatedSheet.NEPCode;
        existingSheet.UniqueCode = updatedSheet.UniqueCode;
        existingSheet.ExamTypeId = updatedSheet.ExamTypeId;
        existingSheet.QPId = updatedSheet.QPId;
        existingSheet.MSSStatus = updatedSheet.MSSStatus;
        existingSheet.TTFStatus = updatedSheet.TTFStatus;
        existingSheet.Status = updatedSheet.Status;
        existingSheet.StopCatch = updatedSheet.StopCatch;
        existingSheet.PercentageCatch = updatedSheet.PercentageCatch;
        


        // Save the changes to the database
        try
        {
            _context.QuantitySheets.Update(existingSheet);
            await _context.SaveChangesAsync();

            // Log the update operation
            _loggerService.LogEvent(
                "QuantitySheet updated",
                "QuantitySheet",
                1, // Replace with actual user ID or triggered by value
                null,
                $"QuantitySheetId: {id}"
            );

            return Ok(existingSheet);
        }
        catch (Exception ex)
        {
            // Log the error and return an appropriate error response
            _loggerService.LogError(
                "Error updating QuantitySheet",
                ex.Message,
                "QuantitySheet"
            );
            return StatusCode(500, "An error occurred while updating the record.");
        }
    }


    [HttpGet("MergeQPintoQS")]
    public async Task<IActionResult> MergeQPintoQS(int QPId)
    {
        // Fetch QPMaster data based on QPId
        var qpMaster = await _context.QpMasters
                                      .Where(p => p.QPMasterId == QPId)
                                      .FirstOrDefaultAsync();

        // If QPMaster is not found, return not found response
        if (qpMaster == null)
        {
            return NotFound("QPMaster not found.");
        }

        // Fetch QuantitySheet data where QPId exists in the QS table
        var quantitySheet = await _context.QuantitySheets
                                           .Where(qs => qs.QPId == QPId)
                                           .ToListAsync();

        // Initialize the result list that will hold the merged data
        var result = new List<QuantitySheet>();

        // Create a merged result with data from QPMaster, even if no matching QuantitySheet is found
        if (quantitySheet == null || quantitySheet.Count == 0)
        {
            var mergedQS = new QuantitySheet
            {
                QPId = qpMaster.QPMasterId,
                Quantity = 0.0,  // Default value since no QuantitySheet is found
                CourseId = qpMaster.CourseId ?? 0,
                SubjectId = qpMaster.SubjectId ?? 0,
                CatchNo = "",  // Default value since no QuantitySheet is found
                InnerEnvelope = "",  // Default value since no QuantitySheet is found
                OuterEnvelope = 0,  // Default value since no QuantitySheet is found
                PaperTitle = qpMaster.PaperTitle,
                PaperNumber = qpMaster.PaperNumber,
                ExamDate = null,  // No exam date available without QuantitySheet
                ExamTime = null,  // No exam time available without QuantitySheet
                MaxMarks = qpMaster.MaxMarks ?? 0,
                Duration = qpMaster.Duration ?? "",
                LanguageId = qpMaster.LanguageId ?? [0],  // Default empty array
                ExamTypeId = qpMaster.ExamTypeId ?? 0,
                NEPCode = qpMaster.NEPCode ?? "",
                UniqueCode = qpMaster.UniqueCode ?? "",
            };

            result.Add(mergedQS);  // Add the merged data for the case when no QuantitySheet is found
        }
        else
        {
            // Otherwise, merge data for each QuantitySheet entry
            foreach (var qs in quantitySheet)
            {
                var mergedQS = new QuantitySheet
                {
                    QPId = qpMaster.QPMasterId,
                    Quantity = qs.Quantity == null ? 0.0 : qs.Quantity,
                    CourseId = qpMaster.CourseId ?? 0,
                    SubjectId = qpMaster.SubjectId ?? 0,
                    CatchNo = string.IsNullOrEmpty(qs.CatchNo) ? "" : qs.CatchNo,
                    InnerEnvelope = string.IsNullOrEmpty(qs.InnerEnvelope) ? "" : qs.InnerEnvelope,
                    OuterEnvelope = qs.OuterEnvelope ?? 0,  // Default to 0 if OuterEnvelope is null
                    PaperTitle = qpMaster.PaperTitle,
                    PaperNumber = qpMaster.PaperNumber,
                    ExamDate = qs.ExamDate,
                    ExamTime = qs.ExamTime,
                    MaxMarks = qpMaster.MaxMarks ?? 0,
                    Duration = qpMaster.Duration ?? "",
                    LanguageId = qpMaster.LanguageId ?? [0],  // Default empty array if null
                    ExamTypeId = qpMaster.ExamTypeId ?? 0,
                    NEPCode = qpMaster.NEPCode ?? "",
                    UniqueCode = qpMaster.UniqueCode ?? "",
                };

                result.Add(mergedQS);
            }
        }

        // Return the merged result
        return Ok(result);
    }


    [Authorize]
    [HttpPost("ReleaseForProduction")]
    public async Task<IActionResult> ReleaseForProduction([FromBody] LotRequest request)
    {
        if (string.IsNullOrEmpty(request?.LotNo))
        {
            return BadRequest("Invalid lot number.");
        }

        // Find all records that belong to the given lot
        var quantitySheets = await _context.QuantitySheets
            .Where(q => q.LotNo == request.LotNo && q.ProjectId == request.ProjectId)
            .ToListAsync();

        if (quantitySheets == null || quantitySheets.Count == 0)
        {
            return NotFound($"No records found for Lot No: {request.LotNo}");
        }

        // Update the status to 1 (released for production)
        foreach (var sheet in quantitySheets)
        {
            sheet.Status = 1;
        }


        // Save changes to the database
        await _context.SaveChangesAsync();

        return Ok($"Lot {request.LotNo} has been released for production.");
    }

    public class LotRequest
    {
        public string LotNo { get; set; }
        public int ProjectId { get; set; }
    }


 
    [Authorize]
    [HttpGet("calculate-date-range")]
    public async Task<IActionResult> CalculateDateRange([FromQuery] string selectedLot, [FromQuery] int projectId)
    {
        // Validate input parameters
        if (string.IsNullOrEmpty(selectedLot))
        {
            return BadRequest("Selected lot is required.");
        }

        try
        {
            // Fetch the unique list of lots for the given project
            var lots = await _context.QuantitySheets
                .Where(l => l.ProjectId == projectId)
                .Select(l => l.LotNo)
                .Distinct()
                .ToListAsync();

            // Validate that there are lots available for the given project
            if (lots == null || !lots.Any())
            {
                return NotFound("No lots found for the specified project.");
            }

            // Sort the lots to ensure they're in order
            var sortedLots = lots.Select(lot => lot.Trim()).OrderBy(lot => lot).ToList();

            // Ensure the selected lot is valid
            if (!sortedLots.Contains(selectedLot))
            {
                return NotFound("Selected lot not found in the available lots.");
            }

            int selectedLotIndex = sortedLots.IndexOf(selectedLot);
            bool isFirstLot = selectedLotIndex == 0;
            bool isLastLot = selectedLotIndex == sortedLots.Count - 1;

            DateTime? startDate = null;
            DateTime? endDate = null;

            // Fetch dates for the previous and next lots
            List<DateTime> previousLotDates = null;
            List<DateTime> nextLotDates = null;

            // Fetch dates for previous lot if not the first lot
            if (!isFirstLot)
            {
                previousLotDates = await _context.QuantitySheets
                    .Where(l => l.ProjectId == projectId && l.LotNo == sortedLots[selectedLotIndex - 1])
                    .Select(l => DateTime.Parse(l.ExamDate))  // Parse ExamDate to DateTime
                    .ToListAsync();
            }

            // Fetch dates for next lot if not the last lot
            if (!isLastLot)
            {
                nextLotDates = await _context.QuantitySheets
                    .Where(l => l.ProjectId == projectId && l.LotNo == sortedLots[selectedLotIndex + 1])
                    .Select(l => DateTime.Parse(l.ExamDate))  // Parse ExamDate to DateTime
                    .ToListAsync();
            }

            // Logic based on the position of the selected lot
            if (isFirstLot)
            {
                // If first lot, startDate is today and endDate is the min date of the next lot
                startDate = DateTime.Today;
                if (nextLotDates != null && nextLotDates.Any())
                {
                    endDate = nextLotDates.Min().AddDays(-1);  // endDate is one day before the minimum date of the next lot
                }
            }
            else if (isLastLot)
            {
                // If last lot, startDate is the max date of the previous lot, and endDate can be any date after startDate
                if (previousLotDates != null && previousLotDates.Any())
                {
                    startDate = previousLotDates.Max();
                }

                // Set endDate to a date after startDate (e.g., 3 months after startDate)
                if (startDate.HasValue)
                {
                    endDate = startDate.Value.AddMonths(3);
                }
            }
            else
            {
                // If the selected lot is somewhere in between, startDate is the max date of the previous lot, and endDate is the min date of the next lot
                if (previousLotDates != null && previousLotDates.Any())
                {
                    startDate = previousLotDates.Max();
                }

                if (nextLotDates != null && nextLotDates.Any())
                {
                    endDate = nextLotDates.Min().AddDays(-1);  // endDate is one day before the minimum date of the next lot
                }
            }

            // If no valid date range, return an error
            if (!startDate.HasValue || !endDate.HasValue)
            {
                return BadRequest("Unable to calculate a valid date range.");
            }

            // Return the calculated date range
            return Ok(new
            {
                startDate = startDate.Value.ToString("yyyy-MM-dd"),
                endDate = endDate.Value.ToString("yyyy-MM-dd"),
                isFirstLot,
                isLastLot
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    [Authorize]
    [HttpGet("lot-dates")]
    public async Task<ActionResult<Dictionary<string, object>>> GetLotDates(int projectId)
    {
        try
        {
            // Fetch all distinct exam dates grouped by LotNo for the given project
            var lotwiseExamDates = await _context.QuantitySheets
                .Where(qs => qs.ProjectId == projectId && !string.IsNullOrEmpty(qs.ExamDate))
                .GroupBy(qs => qs.LotNo)
                .Select(group => new
                {
                    LotNo = group.Key,
                    ExamDates = group.Select(qs => qs.ExamDate).ToList() // Get all dates for the group
                })
                .ToListAsync();

            if (!lotwiseExamDates.Any())
            {
                return NotFound($"No exam dates found for project {projectId}");
            }

            // Process the exam dates and calculate Min and Max per lot
            var result = new Dictionary<string, object>();
            foreach (var lot in lotwiseExamDates)
            {
                var parsedDates = lot.ExamDates
                    .Select(date => DateTime.TryParse(date, out var parsedDate) ? parsedDate : (DateTime?)null)
                    .Where(date => date.HasValue)
                    .Select(date => date.Value)
                    .ToList();

                if (parsedDates.Any())
                {
                    var minDate = parsedDates.Min();
                    var maxDate = parsedDates.Max();

                    result[lot.LotNo] = new { MinDate = minDate.ToString("dd-MM-yyyy"), MaxDate = maxDate.ToString("dd-MM-yyyy") };
                }
                else
                {
                    result[lot.LotNo] = new { MinDate = (string)null, MaxDate = (string)null };
                }
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Failed to retrieve exam dates: {ex.Message}");
        }
    }

   // [Authorize]
    [HttpPut("{id}")]
    public async Task<IActionResult> PutQuantitySheet(int id, QuantitySheet quantity)
    {
        // Validate if the received id matches the quantity's id (this can be adjusted if you want to update all with the same lotNo/catchNo)
        if (id != quantity.QuantitySheetId)
        {
            return BadRequest();
        }

        // Find all the records matching the lotNo and catchNo
        var quantitySheetsToUpdate = _context.QuantitySheets
            .Where(qs => qs.LotNo == quantity.LotNo && qs.CatchNo == quantity.CatchNo)
            .ToList();

        if (quantitySheetsToUpdate.Count == 0)
        {
            return NotFound("No matching records found.");
        }

        // Loop through each matching record and apply the update
        foreach (var sheet in quantitySheetsToUpdate)
        {
            sheet.ProcessId = quantity.ProcessId; // Update other fields as necessary
        }

        try
        {
            await _context.SaveChangesAsync(); // Save the changes to the database
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!QuantitySheetExists(id))
            {
                return NotFound();
            }
            else
            {
                throw;
            }
        }

        return NoContent();
    }

    [Authorize]
    [HttpPut("UpdateStatus")]
    public async Task<IActionResult> UpdateMSSStatus(int id, QuantitySheet quantity)
    {
        // Validate if the received id matches the quantity's id
        if (id != quantity.QuantitySheetId)
        {
            return BadRequest();
        }

        // Retrieve the QuantitySheet from the database by the provided id
        var existingQuantitySheet = await _context.QuantitySheets.FindAsync(id);
        if (existingQuantitySheet == null)
        {
            return NotFound();
        }

        // Update the MSSStatus to 2
        existingQuantitySheet.MSSStatus = 2;

        // Save changes to the database
        try
        {
            await _context.SaveChangesAsync(); // Save the changes
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!QuantitySheetExists(id))
            {
                return NotFound();
            }
            else
            {
                throw;
            }
        }

        return NoContent(); // Return 204 No Content status to indicate success
    }

    [HttpPut("UpdateTTF")]
    public async Task<IActionResult> UpdateTTF(int id, [FromQuery] int ttfStatus)
    {
        // Retrieve the QuantitySheet from the database by the provided id
        var existingQuantitySheet = await _context.QuantitySheets.FindAsync(id);
        if (existingQuantitySheet == null)
        {
            return NotFound();
        }

        // Only update the TTFStatus
        existingQuantitySheet.TTFStatus = ttfStatus;

        // Save changes to the database
        try
        {
            await _context.SaveChangesAsync(); // Save the changes
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!QuantitySheetExists(id))
            {
                return NotFound();
            }
            else
            {
                throw;
            }
        }

        return NoContent(); // Return 204 No Content status to indicate success
    }



    [Authorize]

    [HttpPut]
    public async Task<IActionResult> UpdateQuantitySheet([FromBody] List<QuantitySheet> newSheets)
    {
        if (newSheets == null || !newSheets.Any())
        {
            return BadRequest("No data provided.");
        }

        var projectId = newSheets.First().ProjectId;
        var project = await _context.Projects
            .Where(p => p.ProjectId == projectId)
            .Select(p => new { p.TypeId, p.NoOfSeries })
            .FirstOrDefaultAsync();

        if (project == null)
        {
            return BadRequest("Project not found.");
        }

        var projectTypeId = project.TypeId;
        var projectType = await _context.Types
            .Where(t => t.TypeId == projectTypeId)
            .Select(t => t.Types)
            .FirstOrDefaultAsync();

        // Adjust for "Booklet" type project if necessary
        if (projectType == "Booklet" && project.NoOfSeries.HasValue)
        {
            var noOfSeries = project.NoOfSeries.Value;
            if (noOfSeries == 0)
            {
                noOfSeries = 1; // Default to 1 if NoOfSeries is 0
            }
            var adjustedSheets = new List<QuantitySheet>();

            foreach (var sheet in newSheets)
            {
                // Divide the quantity by the NoOfSeries for each sheet in a Booklet
                var adjustedQuantity = sheet.Quantity / noOfSeries;
                
                for (int i = 0; i < noOfSeries; i++)
                {
                    var newSheet = new QuantitySheet
                    {
                        CatchNo = sheet.CatchNo,
                        PaperTitle = sheet.PaperTitle,
                        PaperNumber = sheet.PaperNumber,
                        CourseId = sheet.CourseId,
                        SubjectId = sheet.SubjectId,
                        InnerEnvelope = sheet.InnerEnvelope,
                        OuterEnvelope = sheet.OuterEnvelope,
                        LotNo = sheet.LotNo,
                        Quantity = adjustedQuantity,  // Adjusted quantity per series
                        PercentageCatch = 0, // Will be recalculated later
                        ProjectId = sheet.ProjectId,
                        ExamDate = sheet.ExamDate,
                        ExamTime = sheet.ExamTime,
                        ProcessId = new List<int>(), // Empty list for new catch
                        StopCatch = 0,
                        MaxMarks = sheet.MaxMarks,
                        Duration = sheet.Duration,
                        LanguageId = sheet.LanguageId,
                        NEPCode = sheet.NEPCode,
                        UniqueCode = sheet.UniqueCode,
                        QPId = sheet.QPId,
                        MSSStatus = sheet.MSSStatus,
                        TTFStatus = sheet.TTFStatus,
                        Status = sheet.Status,
                        ExamTypeId = sheet.ExamTypeId,
                        

                    };
                    adjustedSheets.Add(newSheet);
                }
            }

            newSheets = adjustedSheets; // Replace with adjusted sheets
        }

        // Get existing sheets for the same projectId
        var existingSheets = await _context.QuantitySheets
            .Where(s => s.ProjectId == projectId)
            .ToListAsync();

        // Prepare a list to track new sheets that need to be processed
        var processedNewSheets = new List<QuantitySheet>();

        foreach (var sheet in newSheets)
        {
            // For new sheets, clear the ProcessId and process it
            sheet.ProcessId.Clear();
            _processService.ProcessCatch(sheet);
            processedNewSheets.Add(sheet);
        }

        // Now handle inserting or updating the QuantitySheets based on ProjectId
        foreach (var newSheet in processedNewSheets)
        {
            var existingSheet = existingSheets
                .FirstOrDefault(s => s.LotNo == newSheet.LotNo && s.ProjectId == newSheet.ProjectId && s.CatchNo == newSheet.CatchNo && s.StopCatch == 0);

            if (existingSheet != null)
            {
                // Only update fields where new data is present
                if (!string.IsNullOrEmpty(newSheet.PaperTitle)) existingSheet.PaperTitle = newSheet.PaperTitle;
                if (!string.IsNullOrEmpty(newSheet.PaperNumber)) existingSheet.PaperNumber = newSheet.PaperNumber;
                if (newSheet.CourseId > 0) existingSheet.CourseId = newSheet.CourseId;
                if (newSheet.SubjectId > 0) existingSheet.SubjectId = newSheet.SubjectId;
                if (!string.IsNullOrEmpty(newSheet.InnerEnvelope)) existingSheet.InnerEnvelope = newSheet.InnerEnvelope;
                if (newSheet.OuterEnvelope > 0) existingSheet.OuterEnvelope = newSheet.OuterEnvelope;
                if (!string.IsNullOrEmpty(newSheet.ExamDate)) existingSheet.ExamDate = newSheet.ExamDate;
                if (!string.IsNullOrEmpty(newSheet.ExamTime)) existingSheet.ExamTime = newSheet.ExamTime;
                if (newSheet.Quantity > 0) existingSheet.Quantity = newSheet.Quantity;
                if (newSheet.Pages > 0) existingSheet.Pages = newSheet.Pages;
                if(newSheet.MaxMarks > 0) existingSheet.MaxMarks = newSheet.MaxMarks;
                if (!string.IsNullOrEmpty(newSheet.Duration)) existingSheet.Duration = newSheet.Duration;
                if (newSheet.LanguageId != null && newSheet.LanguageId.Count > 0) existingSheet.LanguageId = newSheet.LanguageId;
                if (!string.IsNullOrEmpty(newSheet.NEPCode)) existingSheet.NEPCode = newSheet.NEPCode;
                if (!string.IsNullOrEmpty(newSheet.UniqueCode)) existingSheet.UniqueCode = newSheet.UniqueCode;
                if (newSheet.QPId > 0) existingSheet.QPId = newSheet.QPId;
                if (newSheet.MSSStatus != 0) existingSheet.MSSStatus = newSheet.MSSStatus;
                if (newSheet.TTFStatus != 0) existingSheet.TTFStatus = newSheet.TTFStatus;
                if (newSheet.Status != 0) existingSheet.Status = newSheet.Status;

                // If project is a "Booklet", update all matching CatchNo in the same LotNo
                if (projectType == "Booklet")
                {
                    // Find all sheets with the same LotNo and CatchNo
                    var matchingSheets = existingSheets
                        .Where(s => s.LotNo == newSheet.LotNo && s.ProjectId == newSheet.ProjectId && s.CatchNo == newSheet.CatchNo && s.StopCatch == 0)
                        .ToList();

                    // Now update the Quantity for all matching sheets, divide by NoOfSeries
                    var totalQuantity = newSheet.Quantity;
                    var series = project.NoOfSeries.Value;
                    if (series == 0)
                    {
                        series = 1; // Default to 1 if NoOfSeries is 0
                    }
                    var adjustedQuantity = totalQuantity / series;

                    foreach (var matchingSheet in matchingSheets)
                    {
                        // Update all matching sheets' quantity to the adjusted one
                        matchingSheet.Quantity = adjustedQuantity;

                        // Update other fields as necessary
                        if (!string.IsNullOrEmpty(newSheet.PaperTitle)) matchingSheet.PaperTitle = newSheet.PaperTitle;
                        if (!string.IsNullOrEmpty(newSheet.PaperNumber)) matchingSheet.PaperNumber = newSheet.PaperNumber;
                        if (newSheet.CourseId > 0) matchingSheet.CourseId = newSheet.CourseId;
                        if (newSheet.SubjectId > 0) matchingSheet.SubjectId = newSheet.SubjectId;
                        if (!string.IsNullOrEmpty(newSheet.InnerEnvelope)) matchingSheet.InnerEnvelope = newSheet.InnerEnvelope;
                        if (newSheet.OuterEnvelope > 0) matchingSheet.OuterEnvelope = newSheet.OuterEnvelope;
                        if (!string.IsNullOrEmpty(newSheet.ExamDate)) matchingSheet.ExamDate = newSheet.ExamDate;
                        if (!string.IsNullOrEmpty(newSheet.ExamTime)) matchingSheet.ExamTime = newSheet.ExamTime;
                        if (newSheet.MaxMarks > 0) existingSheet.MaxMarks = newSheet.MaxMarks;
                        if (!string.IsNullOrEmpty(newSheet.Duration)) existingSheet.Duration = newSheet.Duration;
                        if (newSheet.LanguageId != null && newSheet.LanguageId.Count > 0) existingSheet.LanguageId = newSheet.LanguageId;
                        if (!string.IsNullOrEmpty(newSheet.NEPCode)) existingSheet.NEPCode = newSheet.NEPCode;
                        if (!string.IsNullOrEmpty(newSheet.UniqueCode)) existingSheet.UniqueCode = newSheet.UniqueCode;
                        if (newSheet.QPId > 0) existingSheet.QPId = newSheet.QPId;
                        if (newSheet.MSSStatus != 0) existingSheet.MSSStatus = newSheet.MSSStatus;
                        if (newSheet.TTFStatus != 0) existingSheet.TTFStatus = newSheet.TTFStatus;
                        if (newSheet.Status != 0) existingSheet.Status = newSheet.Status;
                    }
                }
            }
            else
            {
                // If no existing sheet found for this CatchNo, add it to the context for insertion
                _context.QuantitySheets.Add(newSheet);
            }
        }

        // Recalculate the percentages and save
        var allSheets = existingSheets.Concat(processedNewSheets).ToList();
        var groupedSheets = allSheets.GroupBy(sheet => sheet.LotNo);

        foreach (var group in groupedSheets)
        {
            double totalQuantityForLot = group.Sum(sheet => sheet.Quantity);

            if (totalQuantityForLot == 0)
            {
                return BadRequest($"Total quantity for lot {group.Key} is zero, cannot calculate percentages.");
            }

            // Calculate percentage catch for each sheet in the current group
            foreach (var sheet in group)
            {
                sheet.PercentageCatch = (sheet.Quantity / totalQuantityForLot) * 100;
            }
        }

        await _context.SaveChangesAsync();

        return Ok(processedNewSheets);
    }

    [HttpPost("UpdateLotNo")]
    public async Task<IActionResult> UpdateLotNo(string StartDate, string EndDate, string newLotNo, int projectId)
    {
        if (string.IsNullOrEmpty(newLotNo) || string.IsNullOrEmpty(StartDate) || string.IsNullOrEmpty(EndDate))
        {
            return BadRequest("New LotNo is required.");
        }

        if (!DateTime.TryParse(StartDate, out var start) || !DateTime.TryParse(EndDate, out var end))
        {
            return BadRequest("Invalid date format.");
        }

        // Pull to memory before parsing
        var quantitySheet = _context.QuantitySheets
      .Where(p => p.ProjectId == projectId)
      .AsEnumerable() // now IEnumerable, not IQueryable
      .Where(p => DateTime.TryParse(p.ExamDate, out var examDate) && examDate >= start && examDate <= end)
      .ToList(); // ✅ use ToList(), not ToListAsync()


        if (!quantitySheet.Any())
        {
            return NotFound("No matching QuantitySheets found.");
        }

        foreach (var sheet in quantitySheet)
        {
            sheet.LotNo = newLotNo;
        }

        var quantitySheetIds = quantitySheet.Select(q => q.QuantitySheetId).ToList();

        var relatedTransactions = await _context.Transaction
            .Where(t => quantitySheetIds.Contains(t.QuantitysheetId))
            .ToListAsync();

        if (!int.TryParse(newLotNo, out int newLotNoInt))
        {
            return BadRequest("Invalid LotNo. Must be an integer.");
        }

        foreach (var transaction in relatedTransactions)
        {
            transaction.LotNo = newLotNoInt;
        }

        await _context.SaveChangesAsync();

        return NoContent();
    }


    [Authorize]
    [HttpGet("Lots")]
    public async Task<ActionResult<IEnumerable<string>>> GetLots(int ProjectId)
    {
        // Fetch the data from the database first
        var uniqueLotNumbers = await _context.QuantitySheets
            .Where(r => r.ProjectId == ProjectId)
            .Select(r => r.LotNo) // Select the LotNo
            .Distinct() // Get unique LotNo values
            .ToListAsync(); // Bring the data into memory

        // Sort the LotNo values by parsing them as integers
        var sortedLotNumbers = uniqueLotNumbers
            .Where(lotNo => int.TryParse(lotNo, out _)) // Filter out non-numeric LotNo values
            .OrderBy(lotNo => int.Parse(lotNo)) // Order by LotNo as integers
            .ToList();

        return Ok(sortedLotNumbers);
    }

    [Authorize]
    [HttpGet("UnReleasedLots")]
    public async Task<ActionResult<IEnumerable<string>>> GetUnReleasedLots(int ProjectId)
    {
        var uniqueLotNumbers = await _context.QuantitySheets
            .Where(r => r.ProjectId == ProjectId && r.Status != 1)
            .Select(r => r.LotNo) // Select the LotNo
            .Distinct() // Get unique LotNo values
            .ToListAsync();

        return Ok(uniqueLotNumbers);
    }

    [Authorize]
    [HttpGet("ReleasedLots")]
    public async Task<ActionResult<IEnumerable<string>>> GetReleasedLots(int ProjectId)
    {
        var uniqueLotNumbers = await _context.QuantitySheets
            .Where(r => r.ProjectId == ProjectId && r.Status == 1)
            .Select(r => r.LotNo) // Select the LotNo
            .Distinct() // Get unique LotNo values
            .ToListAsync();

        return Ok(uniqueLotNumbers);
    }

    [HttpGet("Columns")]
    public IActionResult GetColumnNames()
    {
        var columnNames = typeof(QuantitySheet).GetProperties()
            .Where(prop => prop.Name != "QuantitySheetId" &&
                           prop.Name != "PercentageCatch" &&
                           prop.Name != "ProjectId" &&
                           prop.Name != "ProcessId" &&
                           prop.Name != "Status" &&
                           prop.Name != "StopCatch" &&
                           prop.Name != "MSSStatus" &&
                           prop.Name != "TTFStatus" &&
                           prop.Name != "QPId")
            .Select(prop => prop.Name)
            .ToList();

        return Ok(columnNames);
    }

    [Authorize]
    [HttpGet("Catch")]
    public async Task<ActionResult<IEnumerable<object>>> GetCatches(int ProjectId, string lotNo)
    {
        // Retrieve current lot data with necessary fields only
        var currentLotData = await _context.QuantitySheets
            .Where(r => r.ProjectId == ProjectId && r.LotNo == lotNo && r.StopCatch == 0)
            .Select(r => new
            {
                r.QuantitySheetId,
                r.CatchNo,
                r.PaperTitle,
                r.ExamDate,
                r.ExamTime,
                r.CourseId,
                r.SubjectId,
                r.PaperNumber,
                r.InnerEnvelope,
                r.OuterEnvelope,
                r.LotNo,
                r.Quantity,
                r.PercentageCatch,
                r.ProjectId,
                r.ProcessId,
                r.Pages,
                r.StopCatch,
                r.MaxMarks,
                r.Duration,
                r.MSSStatus,
                r.TTFStatus,
                r.LanguageId,
                r.ExamTypeId,
                r.QPId,
                r.NEPCode,
                r.UniqueCode,
                r.Status,
            })
            .ToListAsync();

        if (!currentLotData.Any())
        {
            return Ok(new List<object>()); // Return empty if no data for current lot
        }

        // Retrieve previous lots' data with only necessary ExamDate field for overlap check, if not lot 1
        var previousExamDates = lotNo != "1"
            ? await _context.QuantitySheets
                .Where(r => r.ProjectId == ProjectId && r.LotNo != lotNo)
                .Select(r => r.ExamDate)
                .Distinct()
                .ToListAsync()
            : new List<string>(); // Empty list if lot 1

        // Define a function to convert Excel date to Indian format
        string ConvertToIndianDate(string examDateString)
        {
            if (DateTime.TryParse(examDateString, out var examDate))
            {
                return examDate.ToString("dd-MM-yyyy"); // Format date to Indian style
            }
            return "Invalid Date";
        }

        var result = (from current in currentLotData
                      join subject in _context.Subjects on current.SubjectId equals subject.SubjectId into subjectJoin
                      join course in _context.Courses on current.CourseId equals course.CourseId into courseJoin
                      from sub in subjectJoin.DefaultIfEmpty()  // In case there's no matching subject
                      from courses in courseJoin.DefaultIfEmpty()  // In case there's no matching course
                      select new
                      {
                          current.QuantitySheetId,
                          current.CatchNo,
                          current.PaperTitle,
                          current.PaperNumber,
                          ExamDate = ConvertToIndianDate(current.ExamDate),
                          current.ExamTime,
                          current.CourseId,
                          CourseName = courses?.CourseName,
                          SubjectName = sub?.SubjectName,
                          current.SubjectId,
                          current.InnerEnvelope,
                          current.OuterEnvelope,
                          current.LotNo,
                          current.Quantity,
                          current.PercentageCatch,
                          current.ProjectId,
                          current.ProcessId,
                          current.Pages,
                          current.StopCatch,
                          current.QPId,
                          current.NEPCode,
                          current.UniqueCode,
                          current.MaxMarks,
                          current.Duration,
                          current.MSSStatus,
                          current.TTFStatus,
                          current.LanguageId,
                          current.ExamTypeId,
                          current.Status,
                          IsExamDateOverlapped = lotNo != "1" && previousExamDates.Contains(current.ExamDate)
                      }).ToList();


        return Ok(result);
    }


    [Authorize]
    [HttpGet("Catches")]
    public async Task<ActionResult<IEnumerable<object>>> GetCatch(int ProjectId, string lotNo)
    {
        // Retrieve current lot data with necessary fields only
        var currentLotData = await _context.QuantitySheets
            .Where(r => r.ProjectId == ProjectId && r.LotNo == lotNo)
            .Select(r => new
            {
                r.QuantitySheetId,
                r.CatchNo,
                r.PaperTitle,
                r.MaxMarks,
                r.Duration,
                r.MSSStatus,
                r.TTFStatus,
                r.LanguageId,
                r.ExamTypeId,
                r.PaperNumber,
                r.ExamDate,
                r.ExamTime,
                r.CourseId,
                r.SubjectId,
                r.InnerEnvelope,
                r.OuterEnvelope,
                r.LotNo,
                r.Quantity,
                r.PercentageCatch,
                r.ProjectId,
                r.ProcessId,
                r.Pages,
                r.StopCatch,
                r.QPId,
                r.NEPCode,
                r.UniqueCode,
                r.Status,
            })
            .ToListAsync();

        if (!currentLotData.Any())
        {
            return Ok(new List<object>()); // Return empty if no data for current lot
        }
        // Retrieve previous lots' data with only necessary ExamDate field for overlap check, if not lot 1
        var previousExamDates = lotNo != "1"
            ? await _context.QuantitySheets
                .Where(r => r.ProjectId == ProjectId && r.LotNo != lotNo)
                .Select(r => r.ExamDate)
                .Distinct()
                .ToListAsync()
            : new List<string>(); // Empty list if lot 1

        // Define a function to convert Excel date to Indian format
        string ConvertToIndianDate(string examDateString)
        {
            if (DateTime.TryParse(examDateString, out var examDate))
            {
                return examDate.ToString("dd-MM-yyyy"); // Format date to Indian style
            }
            return "Invalid Date";
        }

        // Process each item in the current lot and check for overlap only if lotNo is not "1"
        var result = (from current in currentLotData
                      join subject in _context.Subjects on current.SubjectId equals subject.SubjectId into subjectJoin
                      join course in _context.Courses on current.CourseId equals course.CourseId into courseJoin
                      from sub in subjectJoin.DefaultIfEmpty()  // In case there's no matching subject
                      from courses in courseJoin.DefaultIfEmpty()  // In case there's no matching course
                      select new
                      {
                          current.QuantitySheetId,
                          current.CatchNo,
                          current.PaperTitle,
                          current.PaperNumber,
                          ExamDate = ConvertToIndianDate(current.ExamDate),
                          current.ExamTime,
                          current.CourseId,
                          CourseName = courses?.CourseName,
                          SubjectName = sub?.SubjectName,
                          current.SubjectId,
                          current.InnerEnvelope,
                          current.OuterEnvelope,
                          current.LotNo,
                          current.Quantity,
                          current.PercentageCatch,
                          current.ProjectId,
                          current.ProcessId,
                          current.Pages,
                          current.StopCatch,
                          current.QPId,
                          current.NEPCode,
                          current.UniqueCode,
                          current.MaxMarks,
                          current.Duration,
                          current.MSSStatus,
                          current.TTFStatus,
                          current.LanguageId,
                          current.ExamTypeId,
                          current.Status,
                          IsExamDateOverlapped = lotNo != "1" && previousExamDates.Contains(current.ExamDate)
                      }).ToList();


        return Ok(result);
    }

   // [Authorize]
    [HttpGet("CatchByproject")]
    public async Task<ActionResult<IEnumerable<object>>> CatchByproject(int ProjectId)
    {
        // Step 1: Fetch QuantitySheets
        var quantitySheets = await _context.QuantitySheets
            .Where(r => r.ProjectId == ProjectId && r.StopCatch == 0)
            .ToListAsync();

        // Step 2: Fetch LanguageIds from the QuantitySheets
        var languageIds = quantitySheets
            .SelectMany(q => q.LanguageId) // Assuming LanguageId is a List<int>
            .Distinct() // Get distinct LanguageIds
            .ToList();

        // Step 3: Fetch Languages based on the LanguageIds
        var languages = await _context.Languages
            .Where(l => languageIds.Contains(l.LanguageId))
            .Select(l => new { l.LanguageId, l.Languages }) // Adjust the property names as needed
            .ToListAsync();

        // Step 4: Map the languages back to the QuantitySheets
        var result = quantitySheets.Select(q => new
        {
            q.QuantitySheetId,
            q.CatchNo,
            q.PaperTitle,
            q.MaxMarks,
            q.Duration,
            q.MSSStatus,
            q.TTFStatus,
            q.LanguageId,
            Languages = languages.Where(l => q.LanguageId.Contains(l.LanguageId)).Select(l => l.Languages).ToList(),
            q.ExamTypeId,
            ExamTypes = _context.ExamTypes
                .Where(e => e.ExamTypeId == q.ExamTypeId)
                .Select(e => e.TypeName)
                .FirstOrDefault(),
            q.PaperNumber,
            q.ExamDate,
            q.ExamTime,
            q.CourseId,
            CourseName = _context.Courses
                .Where(c => c.CourseId == q.CourseId)
                .Select(c => c.CourseName)
                .FirstOrDefault(),
            q.SubjectId,
            SubjectName = _context.Subjects
                .Where(s => s.SubjectId == q.SubjectId)
                .Select(s => s.SubjectName)
                .FirstOrDefault(),
            q.InnerEnvelope,
            q.OuterEnvelope,
            q.LotNo,
            q.Quantity,
            q.PercentageCatch,
            q.ProjectId,
            q.ProcessId,
            q.Pages,
            q.StopCatch,
            q.QPId,
            q.NEPCode,
            q.UniqueCode,
        }).ToList();

        return result;
    }

    [Authorize]
    [HttpGet("check-all-quantity-sheets")]
    public async Task<ActionResult<IEnumerable<object>>> GetAllProjectsQuantitySheetStatus([FromQuery] List<int> projectIds)
    {
        // If no projectIds are passed, fetch all projects
        var projects = projectIds.Any()
            ? await _context.Projects.Where(p => projectIds.Contains(p.ProjectId)).ToListAsync()
            : await _context.Projects.ToListAsync();

        var result = new List<object>();

        foreach (var project in projects)
        {
            var hasQuantitySheet = await _context.QuantitySheets
                .AnyAsync(s => s.ProjectId == project.ProjectId);

            result.Add(new
            {
                projectId = project.ProjectId,
                quantitySheet = hasQuantitySheet
            });
        }

        return Ok(result);
    }


    [Authorize]
    [HttpPost]
    [Route("UpdatePages")]
    public async Task<IActionResult> UpdatePages([FromBody] List<PageUpdateRequest> updateRequests)
    {
        if (updateRequests == null || !updateRequests.Any())
        {
            return BadRequest("No data provided.");
        }

        try
        {
            foreach (var request in updateRequests)
            {
                // Find matching QuantitySheets
                var existingSheets = await _context.QuantitySheets
                    .Where(qs => qs.ProjectId == request.ProjectId &&
                                 qs.LotNo == request.LotNo &&
                                 qs.CatchNo == request.CatchNumber)
                    .ToListAsync();

                if (existingSheets.Any())
                {
                    // Update pages for all matching records
                    foreach (var sheet in existingSheets)
                    {
                        sheet.Pages = request.Pages;
                        sheet.ProcessId.Clear();
                        _processService.ProcessCatch(sheet);
                    }
                }
            }

            // Save changes to the database
            await _context.SaveChangesAsync();

            return Ok("Pages updated successfully.");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred while updating pages: {ex.Message}");
        }
    }
    public class PageUpdateRequest
    {
        public int ProjectId { get; set; }
        public string LotNo { get; set; }
        public string CatchNumber { get; set; }
        public int Pages { get; set; }
    }

    [Authorize]
    [HttpPost("StopCatch")]
    public async Task<IActionResult> StopCatch(int id)
    {
        // Get the 'QuantitySheet' with the provided 'id' from the database
        var sheetToUpdate = await _context.QuantitySheets
            .FirstOrDefaultAsync(q => q.QuantitySheetId == id);

        if (sheetToUpdate == null)
        {
            return NotFound($"QuantitySheet with id {id} not found.");
        }

        var getCatchNo = sheetToUpdate.CatchNo;
        var projectId = sheetToUpdate.ProjectId;
        var lotNo = sheetToUpdate.LotNo;
        // Get all 'QuantitySheets' that have the same 'CatchNo' as the provided 'quantitySheet'
        var allQuantitySheetsWithSameCatchNo = await _context.QuantitySheets
            .Where(q => q.CatchNo == getCatchNo && q.ProjectId == projectId && q.LotNo == lotNo)
            .ToListAsync();
        Console.WriteLine(allQuantitySheetsWithSameCatchNo);

        var ifstopped = sheetToUpdate.StopCatch;

        // Update the status of the specific 'QuantitySheet' (with the provided 'id') to 1
        foreach (var sheet in allQuantitySheetsWithSameCatchNo)
        {
            sheet.StopCatch = (ifstopped.Value == 1) ? 0 : 1;
        }


        // Save changes to the database
        await _context.SaveChangesAsync();



        var remainingSheets = await _context.QuantitySheets
           .Where(s => s.ProjectId == projectId && s.LotNo == lotNo && s.StopCatch == 0)
           .ToListAsync();

        double totalQuantityForLot = remainingSheets.Sum(sheet => sheet.Quantity);

        if (totalQuantityForLot > 0)
        {
            foreach (var sheet in remainingSheets)
            {
                sheet.PercentageCatch = (sheet.Quantity / totalQuantityForLot) * 100;
            }

            // Save changes to update the percentages
            await _context.SaveChangesAsync();
        }

        // Return the updated status for the specific 'QuantitySheet'
        return Ok($"Catch with QuantitySheetId {id} has been stopped, and all QuantitySheets with CatchNo {getCatchNo} were retrieved.");
    }


    [Authorize]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteQuantitysheet(int id)
    {
        var sheetToDelete = await _context.QuantitySheets.FindAsync(id);
        if (sheetToDelete == null)
        {
            return NotFound();
        }

        var projectId = sheetToDelete.ProjectId;
        var lotNo = sheetToDelete.LotNo;

        _context.QuantitySheets.Remove(sheetToDelete);
        await _context.SaveChangesAsync();

        // After deletion, recalculate percentages for remaining sheets in the same project and lot
        var remainingSheets = await _context.QuantitySheets
            .Where(s => s.ProjectId == projectId && s.LotNo == lotNo && s.StopCatch == 0)
            .ToListAsync();

        double totalQuantityForLot = remainingSheets.Sum(sheet => sheet.Quantity);

        if (totalQuantityForLot > 0)
        {
            foreach (var sheet in remainingSheets)
            {
                sheet.PercentageCatch = (sheet.Quantity / totalQuantityForLot) * 100;
            }

            // Save changes to update the percentages
            await _context.SaveChangesAsync();
        }

        return NoContent();
    }

    private bool QuantitySheetExists(int id)
    {
        return _context.QuantitySheets.Any(e => e.QuantitySheetId == id);
    }



    // First, create a DTO to handle the transfer request
    public class CatchTransferRequest
    {
        public required int ProjectId { get; set; }
        public required string SourceLotNo { get; set; }
        public required string TargetLotNo { get; set; }
        public required List<int> CatchIds { get; set; }
        public string? NewExamDate { get; set; }  // Optional exam date in dd-MM-yyyy format
    }

    [Authorize]
    [HttpPut("transfer-catches")]
    public async Task<IActionResult> TransferCatches([FromBody] CatchTransferRequest request)
    {
        try
        {
            // Validate request
            if (request == null || string.IsNullOrEmpty(request.SourceLotNo) ||
                string.IsNullOrEmpty(request.TargetLotNo) ||
                request.CatchIds == null || !request.CatchIds.Any())
            {
                return BadRequest("Invalid transfer request");
            }

            if (request.SourceLotNo == request.TargetLotNo)
            {
                return BadRequest("Source and target lots cannot be the same");
            }

            // Convert Source and Target LotNo to integers for comparison
            if (!int.TryParse(request.SourceLotNo, out int sourceLotNo) ||
                !int.TryParse(request.TargetLotNo, out int targetLotNo))
            {
                return BadRequest("Lot numbers must be valid integers");
            }

            // Retrieve the ProjectId and CatchNo for the first CatchId
            var initialCatch = await _context.QuantitySheets
                .Where(qs => qs.QuantitySheetId == request.CatchIds.First() &&
                             Convert.ToInt32(qs.LotNo) == sourceLotNo)
                .Select(qs => new { qs.ProjectId, qs.CatchNo })
                .FirstOrDefaultAsync();

            if (initialCatch == null)
            {
                return NotFound("No valid catch found for the provided CatchIds in the source lot");
            }

            var projectId = initialCatch.ProjectId;
            var catchNo = initialCatch.CatchNo;

            // Retrieve all records with the same ProjectId and CatchNo in the source lot
            var catchesToTransfer = await _context.QuantitySheets
                .Where(qs => qs.ProjectId == projectId &&
                             qs.CatchNo == catchNo &&
                             Convert.ToInt32(qs.LotNo) == sourceLotNo)
                .ToListAsync();

            if (!catchesToTransfer.Any())
            {
                return NotFound("No catches found to transfer");
            }

            using var transactionScope = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var catch_ in catchesToTransfer)
                {
                    catch_.LotNo = Convert.ToString(targetLotNo); // Update LotNo to TargetLotNo

                    // Update exam date if provided
                    if (!string.IsNullOrEmpty(request.NewExamDate))
                    {
                        if (DateTime.TryParseExact(request.NewExamDate, "dd-MM-yyyy",
                            CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
                        {
                            catch_.ExamDate = parsedDate.ToString("yyyy-MM-dd"); // Store in database format
                        }
                        else
                        {
                            return BadRequest("Invalid exam date format. Please use dd-MM-yyyy");
                        }
                    }
                }

                // Save changes to persist the LotNo updates in QuantitySheets
                await _context.SaveChangesAsync();

                // Update the LotNo in the Transaction table for the transferred QuantitySheets
                var quantitySheetIds = catchesToTransfer.Select(ct => ct.QuantitySheetId).ToList();
                var transactionsToUpdate = await _context.Transaction
                    .Where(t => quantitySheetIds.Contains(t.QuantitysheetId) &&
                                t.LotNo == sourceLotNo)
                    .ToListAsync();

                foreach (var transaction in transactionsToUpdate)
                {
                    transaction.LotNo = targetLotNo; // Update LotNo
                }

                // Save changes to persist the LotNo updates in Transactions
                await _context.SaveChangesAsync();

                // Recalculate percentages for both lots
                await RecalculatePercentages(projectId, sourceLotNo.ToString());
                await RecalculatePercentages(projectId, targetLotNo.ToString());

                // Save changes again after recalculating percentages
                await _context.SaveChangesAsync();

                await transactionScope.CommitAsync();

                return Ok(new
                {
                    Message = "Catches transferred successfully",
                    TransferredCatches = catchesToTransfer,
                    UpdatedTransactions = transactionsToUpdate
                });
            }
            catch (Exception ex)
            {
                await transactionScope.RollbackAsync();
                return StatusCode(500, $"Failed to transfer catches: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Failed to transfer catches: {ex.Message}");
        }
    }

    // Helper method to recalculate percentages for a lot
    private async Task RecalculatePercentages(int projectId, string lotNo)
    {
        var sheetsInLot = await _context.QuantitySheets
            .Where(s => s.ProjectId == projectId && s.LotNo == lotNo && s.StopCatch == 0)
            .ToListAsync();

        double totalQuantityForLot = sheetsInLot.Sum(sheet => sheet.Quantity);

        if (totalQuantityForLot > 0)
        {
            foreach (var sheet in sheetsInLot)
            {
                sheet.PercentageCatch = (sheet.Quantity / totalQuantityForLot) * 100;
            }
        }
    }
    // Get Exam Dates for a given project and lot
    [Authorize]
    [HttpGet("exam-dates")]
    public async Task<ActionResult<IEnumerable<string>>> GetExamDates(int projectId, string lotNo)
    {
        var examDates = await _context.QuantitySheets
            .Where(qs => qs.ProjectId == projectId && qs.LotNo == lotNo)
            .Select(qs => qs.ExamDate)
            .Distinct()
            .ToListAsync();

        if (!examDates.Any())
        {
            return NotFound($"No exam dates found for project {projectId} and lot {lotNo}");
        }

        // Convert dates to Indian format
        var formattedDates = examDates
            .Where(date => !string.IsNullOrEmpty(date))
            .Select(date => DateTime.TryParse(date, out var parsedDate)
                ? parsedDate.ToString("dd-MM-yyyy")
                : date)
            .ToList();

        return Ok(formattedDates);
    }

    // Get Lot Data for a given project and lot
    [Authorize]
    [HttpGet("lot-data")]
    public async Task<ActionResult<IEnumerable<QuantitySheet>>> GetLotData(int projectId, string lotNo)
    {
        var lotData = await _context.QuantitySheets
            .Where(qs => qs.ProjectId == projectId && qs.LotNo == lotNo)
            .ToListAsync();

        if (!lotData.Any())
        {
            return NotFound($"No data found for project {projectId} and lot {lotNo}");
        }

        return Ok(lotData);
    }


    // Get Catch Data for a given project, lot, and catch
    [Authorize]
    [HttpGet("catch-data")]
    public async Task<ActionResult<IEnumerable<QuantitySheet>>> GetCatchData(int projectId, string lotNo, string catchNo)
    {
        var catchData = await _context.QuantitySheets
            .Where(qs => qs.ProjectId == projectId
                      && qs.LotNo == lotNo
                      && qs.CatchNo == catchNo)
            .ToListAsync();

        if (!catchData.Any())
        {
            return NotFound($"No data found for project {projectId}, lot {lotNo}, catch {catchNo}");
        }

        return Ok(catchData);
    }

    [Authorize]
    [HttpDelete("DeleteByProjectId/{projectId}")]
    public async Task<IActionResult> DeleteByProjectId(int projectId, string LotNo)
    {
        // Find all quantity sheets for the given projectId
        var sheetsToDelete = await _context.QuantitySheets
            .Where(s => s.ProjectId == projectId && s.LotNo == LotNo.ToString())
            .ToListAsync();

        if (sheetsToDelete == null || !sheetsToDelete.Any())
        {
            return NotFound($"No quantity sheets found for Project ID: {projectId}");
        }

        // Remove the sheets from the context
        _context.QuantitySheets.RemoveRange(sheetsToDelete);
        await _context.SaveChangesAsync();

        return NoContent(); // Return 204 No Content on successful deletion
    }



}

