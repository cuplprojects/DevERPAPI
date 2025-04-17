using ERPAPI.Data;   // For AppDbContext
using ERPAPI.Model;  // For ABCD, Project, QuantitySheet
using System.Collections.Generic;
using System.Linq;

namespace ERPAPI.Service
{
    public class ABCDService
    {
        private readonly IDictionary<int, string> _subjects;
        private readonly IDictionary<int, string> _courses;
        private readonly IDictionary<int, string> _sessions;
        private readonly ABCD _abcd;
        private readonly Project _project;
        private readonly AppDbContext _context;

        // Inject AppDbContext along with other necessary dependencies
        public ABCDService(AppDbContext context, ABCD abcd, Project project)
        {
            _context = context;
            _abcd = abcd;
            _project = project;

            // Dynamically generate dictionaries from the database
            _subjects = _context.Subjects.ToDictionary(s => s.SubjectId, s => s.SubjectName);
            _courses = _context.Courses.ToDictionary(c => c.CourseId, c => c.CourseName);
            _sessions = _context.Sessions.ToDictionary(s => s.SessionId, s => s.session);
        }

        public string ResolveTemplateForField(string fieldName, QuantitySheet quantitySheet)
        {
            return fieldName switch
            {
                "A" => ResolveTemplate(_abcd.A, quantitySheet),
                "B" => GetPropertyValue(quantitySheet, _abcd.B),
                "C" => GetPropertyValue(quantitySheet, _abcd.C),
                "D" => GetPropertyValue(quantitySheet, _abcd.D),
                _ => null
            };
        }

        private string ResolveTemplate(string template, QuantitySheet quantitySheet)
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
                    else if (_project != null)
                    {
                        prop = _project.GetType().GetProperty(token);
                        if (prop != null)
                        {
                            idValue = prop.GetValue(_project);
                        }
                    }

                    if (idValue != null)
                    {
                        var id = Convert.ToInt32(idValue);

                        if (token == "SubjectId" && _subjects.TryGetValue(id, out var subjectName))
                            resolvedParts.Add(subjectName);
                        else if (token == "CourseId" && _courses.TryGetValue(id, out var courseName))
                            resolvedParts.Add(courseName);
                        else if (token == "SessionId" && _sessions.TryGetValue(id, out var sessionName))
                            resolvedParts.Add(sessionName);
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

        private string GetPropertyValue(QuantitySheet quantitySheet, string propertyName)
        {
            var value = quantitySheet?.GetType().GetProperty(propertyName)?.GetValue(quantitySheet, null);

            if (value == null) return null;

            if (propertyName.EndsWith("Id"))
            {
                var id = Convert.ToInt32(value);

                if (propertyName == "SubjectId" && _subjects.ContainsKey(id))
                    return _subjects[id];
                if (propertyName == "CourseId" && _courses.ContainsKey(id))
                    return _courses[id];
                if (propertyName == "SessionId" && _sessions.ContainsKey(id))
                    return _sessions[id];
            }

            return value?.ToString();
        }
    }
}
