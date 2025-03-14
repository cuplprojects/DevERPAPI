﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using ERPAPI.Model;
using ERPAPI.Data;

namespace ERPAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CourseController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CourseController(AppDbContext context)
        {
            _context = context;
        }

        // POST: api/Course
        [HttpPost]
        public async Task<IActionResult> CreateCourse([FromBody] Course course)
        {
            if (course == null)
                return BadRequest("Invalid course data.");

            _context.Courses.Add(course);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetCourseById), new { id = course.CourseId }, course);
        }

        // PUT: api/Course/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCourse(int id, [FromBody] Course updatedCourse)
        {
            if (id != updatedCourse.CourseId)
                return BadRequest("Course ID mismatch.");

            var course = await _context.Courses.FindAsync(id);
            if (course == null)
                return NotFound("Course not found.");

            course.CourseName = updatedCourse.CourseName;

            await _context.SaveChangesAsync();
            return Ok(course);
        }

        // DELETE: api/Course/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCourse(int id)
        {
            var course = await _context.Courses.FindAsync(id);
            if (course == null)
                return NotFound("Course not found.");

            _context.Courses.Remove(course);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // Optional GET: api/Course/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetCourseById(int id)
        {
            var course = await _context.Courses.FindAsync(id);
            if (course == null)
                return NotFound("Course not found.");

            return Ok(course);
        }
    }
}
