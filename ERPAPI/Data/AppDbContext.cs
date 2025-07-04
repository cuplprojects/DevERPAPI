﻿using ERPAPI.Model;
using ERPGenericFunctions.Model;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Reflection.Emit;
using static ERPAPI.Controllers.DispatchController;
using System.Text.Json;

namespace ERPAPI.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Process> Processes { get; set; }
        public DbSet<Project> Projects { get; set; }
        public DbSet<ProjectProcess> ProjectProcesses { get; set; }
        public DbSet<QuantitySheet> QuantitySheets { get; set; }
        public DbSet<PaperType> Types { get; set; }
        public DbSet<Group> Groups { get; set; }
        public DbSet<Feature> Features { get; set; }
        public DbSet<ProcessGroupType> ProcessGroups { get; set; }
        public DbSet<FeatureEnabling> FeatureEnabling { get; set; }
        public DbSet<Transaction> Transaction { get; set; }
        public DbSet<Camera> Camera { get; set; }
        public DbSet<Alarm> Alarm { get; set; }
        public DbSet<QpMaster> QpMasters { get; set; }
        public DbSet<Message> Message { get; set; }
        public DbSet<TextLabel> TextLabel { get; set; }
        public DbSet<QC> QC { get; set; }
        public DbSet<User> Users { get; set; } // Assuming this is already present
        public DbSet<UserAuth> UserAuths { get; set; } // Add this for UserAuth
        public DbSet<SecurityQuestion> SecurityQuestions { get; set; }
        public DbSet<Location> Locations { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Reports> Reports { get; set; }
        public DbSet<Machine> Machine { get; set; }
        public DbSet<Zone> Zone { get; set; }
        public DbSet<EventLog> EventLogs { get; set; } // Assuming you have event logs
        public DbSet<ErrorLog> ErrorLogs { get; set; } // Assuming you have error logs

        public DbSet<Team> Teams { get; set; }
        public DbSet<CatchTeam> CatchTeams { get; set; }
        public DbSet<Dispatch> Dispatch { get; set; }
        public DbSet<ExamType> ExamTypes { get; set; }
        public DbSet<Course> Courses { get; set; }
        public DbSet<Session> Sessions { get; set; }
        public DbSet<Subject> Subjects { get; set; }
        public DbSet<Language> Languages { get; set; }
        public DbSet<ABCD> ABCD { get; set; }
        public DbSet<DailyTask> DailyTasks { get; set; }

        public DbSet<Display> Displays { get; set; }
        public DbSet<MySettings> MySettings { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<UserDisplay> UserDisplays { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ProcessGroupType>()
                .HasNoKey();

            // Configure LabelKey to be unique
            modelBuilder.Entity<TextLabel>()
                .HasIndex(t => t.LabelKey)
                .IsUnique(); // This makes LabelKey a unique index



            modelBuilder.Entity<Dispatch>()
        .Property(d => d.DispatchDetails)
        .HasConversion(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
            v => JsonSerializer.Deserialize<List<DispatchDetail>>(v, (JsonSerializerOptions)null));
        }



    }
}