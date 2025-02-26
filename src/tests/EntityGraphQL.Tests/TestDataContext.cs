using System.Collections.Generic;
using System;
using System.Linq;

namespace EntityGraphQL.Tests
{
    /// <summary>
    /// This is a mock datamodel, what would be your real datamodel and/or EF context
    ///
    /// Used by most of the tests
    /// </summary>
    public class TestDataContext
    {
        public int TotalPeople => People.Count;
        [Obsolete("This is obsolete, use Projects instead")]
        public IEnumerable<ProjectOld> ProjectsOld { get; set; }
        public IEnumerable<Project> Projects { get; set; }
        public IEnumerable<Task> Tasks { get; set; } = new List<Task>();
        public List<Location> Locations { get; set; } = new List<Location>();
        public virtual List<Person> People { get; set; } = new List<Person>();
        public IEnumerable<User> Users { get; set; } = new List<User>();
    }

    public class ProjectOld
    {
    }

    public enum Gender
    {
        Female,
        Male,
        NotSpecified
    }

    public enum UserType
    {
        Admin,
        User
    }

    public class User
    {
        public int Id { get; set; }
        public int Field1 { get; set; }
        public string Field2 { get; set; }
        public Person Relation { get; set; }
        public Task NestedRelation { get; set; }
    }

    public class Person
    {
        public int Id { get; set; }
        public Guid Guid { get; set; }
        public string Name { get; set; }
        public string LastName { get; set; }
        public Person Manager { get; set; }
        public Gender Gender { get; set; }
        public List<Project> Projects { get; set; }
        public List<Task> Tasks { get; set; }
        public DateTime? Birthday { get; set; }
        public User User { get; set; }
        public double Height { get; set; }
        // fake an error
        public static string Error { get => throw new Exception("Field failed to execute"); set => throw new Exception("Field failed to execute"); }

        public double GetHeight(HeightUnit unit)
        {
            return unit switch
            {
                HeightUnit.Cm => Height,
                HeightUnit.Meter => Height / 100,
                HeightUnit.Feet => Height * 0.0328,
                _ => throw new NotSupportedException($"Height unit {unit} not supported"),
            };
        }
    }

    public class Project
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Type { get; set; }
        public Location Location { get; set; }
        public IEnumerable<Task> Tasks { get; set; }
        public Person Owner { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }
    }

    public class Task
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public bool IsActive { get; set; }
        public Person Assignee { get; set; }
        public Project Project { get; set; }
    }
    public class Location
    {
        public int Id { get; set; }
        public string Address { get; set; }
        public string State { get; set; }
        public string Country { get; set; }
        public string Planet { get; set; }
    }

    public enum HeightUnit
    {
        Cm,
        Meter,
        Feet
    }

    internal static class DataFiller
    {
        internal static TestDataContext FillWithTestData(this TestDataContext context)
        {
            var user = new User
            {
                Id = 100,
                Field1 = 2,
                Field2 = "2",
                Relation = new Person(),
                NestedRelation = new Task()
            };
            context.Users = new List<User> { user };

            var project = new Project
            {
                Id = 55,
                Name = "Project 3",
                Tasks = new List<Task> {
                    new Task
                    {
                        Id = 1,
                        Name = "task 1"
                    },
                    new Task
                    {
                        Id = 2,
                        Name = "task 2"
                    },
                    new Task
                    {
                        Id = 3,
                        Name = "task 3"
                    },new Task
                    {
                        Id = 4,
                        Name = "task 4"
                    }
                },
            };
            context.People = new List<Person>
            {
                new Person
                {
                    Id = 99,
                    Guid = new Guid("cccccccc-bbbb-4444-1111-ccddeeff0033"),
                    Name ="Luke",
                    LastName ="Last Name",
                    Birthday = new DateTime(2000, 1, 1, 1, 1, 1, 1),
                    User = user,
                    Height = 183,
                    Gender = Gender.Male,
                    Projects = new List<Project> { project },
                }
            };
            context.Projects = new List<Project>
            {
                project
            };
            return context;
        }
    }
}