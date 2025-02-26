using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Schema;
using System.Linq.Expressions;
using System;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace EntityGraphQL.Tests
{
    internal class PeopleMutations
    {
        [GraphQLMutation]

        public Person AddPerson(PeopleMutationsArgs args)
        {
            return new Person { Name = string.IsNullOrEmpty(args.Name) ? "Default" : args.Name, Id = 555 };
        }

        [GraphQLMutation]

        public Expression<Func<TestDataContext, Person>> AddPersonNames(TestDataContext db, PeopleMutationsArgs args)
        {
            db.People.Add(new Person { Id = 11, Name = args.Names[0], LastName = args.Names[1] });
            return ctx => ctx.People.First(p => p.Id == 11);
        }

        [GraphQLMutation]

        public Person AddPersonInput(PeopleMutationsArgs args)
        {
            return new Person { Name = args.NameInput.Name, LastName = args.NameInput.LastName };
        }

        [GraphQLMutation]
        public Expression<Func<TestDataContext, Person>> AddPersonAdv(PeopleMutationsArgs args)
        {
            // test returning a constant in the expression which allows graphql selection over the schema (assuming the constant is a type in the schema)
            // Ie. in the mutation query you can select any valid fields in the schema from Person
            var person = new Person
            {
                Name = args.Name,
                Tasks = new List<Task> { new Task { Name = "A" } },
                Projects = new List<Project> { new Project { Id = 123 } }
            };
            return ctx => person;
        }

        [GraphQLMutation]
        public Expression<Func<TestDataContext, IEnumerable<Person>>> AddPersonReturnAll(TestDataContext db, PeopleMutationsArgs args)
        {
            db.People.Add(new Person { Id = 11, Name = args.Name });
            return ctx => ctx.People;
        }

        [GraphQLMutation]
        public IEnumerable<Person> AddPersonReturnAllConst(TestDataContext db, PeopleMutationsArgs args)
        {
            db.People.Add(new Person { Id = 11, Name = args.Name });
            return db.People.ToList();
        }

        [GraphQLMutation]
        public int AddPersonError(PeopleMutationsArgs args)
        {
            throw new ArgumentNullException("name", "Name can not be null");
        }

        [GraphQLMutation]
        public async Task<bool> DoGreatThing()
        {
            return await Task<bool>.Run(() =>
            {
                return true;
            });
        }
        [GraphQLMutation]
        public async Task<bool> NeedsGuid(GuidArgs args)
        {
            return await Task<bool>.Run(() =>
            {
                return true;
            });
        }
        [GraphQLMutation]
        public async Task<bool> NeedsGuidNonNull(GuidNonNullArgs args)
        {
            return await Task<bool>.Run(() =>
            {
                return true;
            });
        }
    }

    [MutationArguments]
    internal class PeopleMutationsArgs
    {
        public string Name { get; set; }
        public List<string> Names { get; set; }

        public InputObject NameInput { get; set; }
    }

    [MutationArguments]
    internal class GuidArgs
    {
        [Required]
        public Guid Id { get; set; }
    }
    [MutationArguments]
    internal class GuidNonNullArgs
    {
        public Guid Id { get; set; }
    }
    public class InputObject
    {
        public string Name { get; set; }
        public string LastName { get; set; }
    }
}