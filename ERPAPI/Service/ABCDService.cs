using ERPAPI.Model;
using ERPAPI.Data;
using Microsoft.EntityFrameworkCore;


namespace ERPAPI.Service
{
        public interface IABCDService
        {
         
            Task<string> ResolveTemplateAsync(string template, object quantitySheet, Project project,
                                              Dictionary<int, string> subjects,
                                              Dictionary<int, string> courses,
                                              Dictionary<int, string> sessions,
                                              string sessionFormat);
            Task<string> GetPropertyValueAsync(object obj, string propertyName,
                                               Dictionary<int, string> subjects,
                                               Dictionary<int, string> courses,
                                               Dictionary<int, string> sessions);
        }

        public class ABCDService : IABCDService
        {
            private readonly AppDbContext _context;

            public ABCDService(AppDbContext context)
            {
                _context = context;
            }

        

            public async Task<string> ResolveTemplateAsync(string template, object quantitySheet, Project project,
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

            public async Task<string> GetPropertyValueAsync(object obj, string propertyName,
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

                return value?.ToString();
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
        }


    }

